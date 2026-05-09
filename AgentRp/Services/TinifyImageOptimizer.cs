using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AgentRp.Serialization;
using Microsoft.Extensions.Options;

namespace AgentRp.Services;

public sealed class TinifyOptions
{
    public string ApiKey { get; set; } = "";
}

public sealed record ImageOptimizationRequest(byte[] Bytes, string ContentType, string FileName);

public sealed record ImageOptimizationResult(
    byte[] Bytes,
    string ContentType,
    string FileExtension,
    bool Attempted,
    bool Succeeded,
    string Provider,
    string ErrorMessage)
{
    public static ImageOptimizationResult NotAttempted(ImageOptimizationRequest request, string extension) =>
        new(request.Bytes, request.ContentType, extension, false, false, "", "");

    public static ImageOptimizationResult Failed(ImageOptimizationRequest request, string extension, string errorMessage) =>
        new(request.Bytes, request.ContentType, extension, true, false, TinifyImageOptimizer.ProviderName, errorMessage);
}

public interface IImageOptimizer
{
    Task<ImageOptimizationResult> OptimizeAsync(ImageOptimizationRequest request, CancellationToken cancellationToken = default);
}

public sealed class TinifyImageOptimizer(
    HttpClient httpClient,
    IOptions<TinifyOptions> options,
    ILogger<TinifyImageOptimizer> logger) : IImageOptimizer
{
    public const string ProviderName = "tinify";
    const string AvifContentType = "image/avif";
    const string AvifExtension = ".avif";

    public async Task<ImageOptimizationResult> OptimizeAsync(ImageOptimizationRequest request, CancellationToken cancellationToken = default)
    {
        var fallbackExtension = ImageContentTypeRules.FileExtensionFor(request.ContentType, request.FileName);
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            return ImageOptimizationResult.NotAttempted(request, fallbackExtension);

        try
        {
            var outputUri = await ShrinkAsync(request, cancellationToken);
            var optimized = await ConvertToAvifAsync(outputUri, cancellationToken);
            if (optimized.Length == 0)
                throw new InvalidOperationException("Tinify returned an empty AVIF image.");

            return new(optimized, AvifContentType, AvifExtension, true, true, ProviderName, "");
        }
        catch (Exception exception)
        {
            return ImageOptimizationResult.Failed(
                request,
                fallbackExtension,
                UserFacingErrorReporter.Capture(
                    logger,
                    exception,
                    "Optimizing the image with Tinify failed.",
                    "Tinify image optimization failed; storing the original image."));
        }
    }

    async Task<Uri> ShrinkAsync(ImageOptimizationRequest request, CancellationToken cancellationToken)
    {
        using var content = new ByteArrayContent(request.Bytes);
        if (!string.IsNullOrWhiteSpace(request.ContentType))
            content.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);

        using var message = new HttpRequestMessage(HttpMethod.Post, "shrink")
        {
            Content = content
        };
        ApplyAuthorization(message);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, "Tinify image compression failed.", cancellationToken);
        return response.Headers.Location
            ?? throw new InvalidOperationException("Tinify image compression did not return an output location.");
    }

    async Task<byte[]> ConvertToAvifAsync(Uri outputUri, CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(
            new TinifyConvertRequest(new(AvifContentType)),
            options: AppJsonSerializerOptions.Web);
        using var message = new HttpRequestMessage(HttpMethod.Post, outputUri)
        {
            Content = content
        };
        ApplyAuthorization(message);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, "Tinify AVIF conversion failed.", cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    void ApplyAuthorization(HttpRequestMessage message)
    {
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{options.Value.ApiKey}"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    static async Task EnsureSuccessAsync(HttpResponseMessage response, string fallbackMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var error = await response.Content.ReadFromJsonAsync<TinifyErrorResponse>(AppJsonSerializerOptions.Web, cancellationToken);
        var reason = string.IsNullOrWhiteSpace(error?.Message)
            ? response.ReasonPhrase ?? response.StatusCode.ToString()
            : error.Message;
        throw new InvalidOperationException($"{fallbackMessage} {reason}");
    }

    sealed record TinifyConvertRequest(TinifyConvertOptions Convert);

    sealed record TinifyConvertOptions(string Type);

    sealed record TinifyErrorResponse(string Error, string Message);
}

public static class TinifyServiceCollectionExtensions
{
    public static IServiceCollection AddTinify(this IServiceCollection services)
    {
        services.AddOptions<TinifyOptions>().BindConfiguration("Tinify");
        services.AddHttpClient<TinifyImageOptimizer>(client =>
        {
            client.BaseAddress = new Uri("https://api.tinify.com/");
        });
        services.AddScoped<IImageOptimizer>(provider => provider.GetRequiredService<TinifyImageOptimizer>());
        return services;
    }
}
