using Microsoft.Extensions.Logging;

namespace AgentRp.Services;

public static class UserFacingErrorReporter
{
    public static string Capture(
        ILogger? logger,
        Exception exception,
        string fallbackMessage,
        string logMessage,
        params object?[] logArgs)
    {
        Log(logger, exception, logMessage, logArgs);
        return UserFacingErrorMessageBuilder.Build(fallbackMessage, exception);
    }

    public static UserFacingError CaptureWithDetails(
        ILogger? logger,
        Exception exception,
        string fallbackMessage,
        string logMessage,
        params object?[] logArgs)
    {
        Log(logger, exception, logMessage, logArgs);
        return new(
            UserFacingErrorMessageBuilder.Build(fallbackMessage, exception),
            UserFacingErrorMessageBuilder.BuildDetails(exception));
    }

    public static void Log(
        ILogger? logger,
        Exception exception,
        string logMessage,
        params object?[] logArgs)
    {
        logger?.LogError(exception, logMessage, logArgs);
    }

    public static string BuildMessage(string fallbackMessage, Exception exception) =>
        UserFacingErrorMessageBuilder.Build(fallbackMessage, exception);
}

public readonly record struct UserFacingError(string Message, string Details);
