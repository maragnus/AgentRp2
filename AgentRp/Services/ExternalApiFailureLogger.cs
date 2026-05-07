using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;

namespace AgentRp.Services;

public sealed record ExternalApiCallLogContext(
    string Operation,
    string ProviderName = "",
    string ModelId = "",
    string Endpoint = "");

public static class ExternalApiFailureLogger
{
    const int MaxLoggedDetailLength = 8000;

    public static void LogHttpFailure(
        ILogger logger,
        HttpRequestMessage request,
        HttpResponseMessage response,
        string responseBody)
    {
        logger.LogError(
            "External API HTTP call failed. Method: {Method}; Uri: {Uri}; Status: {StatusCode} ({ReasonPhrase}); ResponseBody: {ResponseBody}",
            request.Method.Method,
            RedactUri(request.RequestUri),
            (int)response.StatusCode,
            response.ReasonPhrase,
            PrepareDetail(responseBody));
    }

    public static void LogHttpException(ILogger logger, Exception exception, HttpRequestMessage request)
    {
        logger.LogError(
            exception,
            "External API HTTP call threw before a response was available. Method: {Method}; Uri: {Uri}; Details: {Details}",
            request.Method.Method,
            RedactUri(request.RequestUri),
            PrepareDetail(exception.Message));
    }

    public static void LogModelFailure(ILogger logger, Exception exception, ExternalApiCallLogContext context)
    {
        var details = BuildExceptionDetails(exception);
        logger.LogError(
            exception,
            "External model API call failed. Operation: {Operation}; Provider: {Provider}; Model: {Model}; Endpoint: {Endpoint}; Status: {Status}; Details: {Details}",
            context.Operation,
            context.ProviderName,
            context.ModelId,
            context.Endpoint,
            StatusFor(exception),
            PrepareDetail(details));
    }

    static string BuildExceptionDetails(Exception exception)
    {
        var details = new List<string> { UserFacingErrorMessageBuilder.BuildDetails(exception) };
        if (exception is ClientResultException clientException)
        {
            var response = clientException.GetRawResponse();
            if (response is not null)
            {
                details.Add($"Response status: {response.Status} {response.ReasonPhrase}");
                var content = TryReadPipelineResponseContent(response);
                if (!string.IsNullOrWhiteSpace(content))
                    details.Add($"Response body:\n{content}");
            }
        }

        return string.Join(
            "\n\n",
            details
                .Where(detail => !string.IsNullOrWhiteSpace(detail))
                .Distinct(StringComparer.Ordinal));
    }

    static string StatusFor(Exception exception) =>
        exception switch
        {
            ClientResultException clientException when clientException.Status > 0 => clientException.Status.ToString(),
            ExternalServiceFailureException external => $"{(int)external.StatusCode} ({external.StatusCode})",
            _ => ""
        };

    static string TryReadPipelineResponseContent(PipelineResponse response)
    {
        try
        {
            return response.Content.ToString();
        }
        catch (InvalidOperationException)
        {
            return "";
        }
    }

    static string PrepareDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return "";

        var sanitized = UserFacingErrorMessageBuilder.SanitizeDetails(detail);
        return sanitized.Length <= MaxLoggedDetailLength
            ? sanitized
            : sanitized[..MaxLoggedDetailLength].TrimEnd() + "...";
    }

    static string RedactUri(Uri? uri)
    {
        if (uri is null)
            return "";

        var builder = new UriBuilder(uri) { Query = RedactQuery(uri.Query) };
        return builder.Uri.ToString();
    }

    static string RedactQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "";

        var parts = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            var pair = parts[index].Split('=', 2);
            if (pair.Length == 2 && IsSensitiveQueryName(pair[0]))
                parts[index] = $"{pair[0]}=***";
        }

        return string.Join('&', parts);
    }

    static bool IsSensitiveQueryName(string name) =>
        name.Contains("key", StringComparison.OrdinalIgnoreCase)
        || name.Contains("token", StringComparison.OrdinalIgnoreCase)
        || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || name.Contains("authorization", StringComparison.OrdinalIgnoreCase);
}

public sealed class ExternalApiLoggingHandler(ILogger<ExternalApiLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                ExternalApiFailureLogger.LogHttpFailure(
                    logger,
                    request,
                    response,
                    await response.Content.ReadAsStringAsync(cancellationToken));

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ExternalApiFailureLogger.LogHttpException(logger, exception, request);
            throw;
        }
    }
}
