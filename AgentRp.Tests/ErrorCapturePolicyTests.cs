using System.Runtime.CompilerServices;
using AgentRp.Services;
using Microsoft.Extensions.Logging;

namespace AgentRp.Tests;

public sealed class ErrorCapturePolicyTests
{
    [Fact]
    public void UserFacingErrorsAreBuiltThroughReporter()
    {
        var root = FindRepoRoot();
        var violations = Directory
            .EnumerateFiles(Path.Combine(root, "AgentRp"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Normalize(path).EndsWith("AgentRp/Services/UserFacingErrorReporter.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("UserFacingErrorMessageBuilder.Build(", StringComparison.Ordinal))
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .ToList();

        Assert.True(violations.Count == 0, $"Use UserFacingErrorReporter.Capture so user-facing exceptions are logged and displayed together: {string.Join(", ", violations)}");
    }

    [Fact]
    public void UserFacingErrorReporterLogsCapturedExceptions()
    {
        var logger = new RecordingLogger();
        var exception = new InvalidOperationException("The database was rude.");

        var message = UserFacingErrorReporter.Capture(
            logger,
            exception,
            "Opening story failed.",
            "Opening story {ChatId} failed.",
            "ch3");

        Assert.Equal("Opening story failed: The database was rude.", message);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Contains("Opening story", entry.Message, StringComparison.Ordinal);
        Assert.Contains("ch3", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayPathRecursiveCteDoesNotUseOuterJoins()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "AgentRp", "Session", "Persistence", "TranscriptDisplayPathQuery.cs"));

        Assert.Contains("DisplayEdges AS", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM DisplayPath p\r\n                    LEFT JOIN", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM DisplayPath p\n                    LEFT JOIN", source, StringComparison.Ordinal);
    }

    static string FindRepoRoot([CallerFilePath] string sourcePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgentRp.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate AgentRp.slnx.");
    }

    static string Normalize(string path) => path.Replace('\\', '/');

    sealed class RecordingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new(logLevel, formatter(state, exception), exception));
        }
    }

    sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
