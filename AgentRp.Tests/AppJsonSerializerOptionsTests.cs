using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentRp.Serialization;
using AgentRp.Services;

namespace AgentRp.Tests;

public sealed class AppJsonSerializerOptionsTests
{
    [Fact]
    public void SharedOptionsUseWebNamingAndStringEnums()
    {
        var value = new JsonOptionsProbe("Primary Model", TuningSupport.Supported);

        var compact = JsonSerializer.Serialize(value, AppJsonSerializerOptions.Web);
        var indented = JsonSerializer.Serialize(value, AppJsonSerializerOptions.IndentedWeb);

        Assert.Contains("\"displayName\"", compact, StringComparison.Ordinal);
        Assert.DoesNotContain("\"DisplayName\"", compact, StringComparison.Ordinal);
        Assert.Contains("\"support\":\"Supported\"", compact, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", compact, StringComparison.Ordinal);
        Assert.Contains("\n", indented, StringComparison.Ordinal);
        Assert.Contains("\"support\": \"Supported\"", indented, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCodeDoesNotCreateAdHocJsonSerializerOptions()
    {
        var root = FindRepoRoot();
        var pattern = "new " + "JsonSerializerOptions";
        var violations = EnumerateProductionSourceFiles(root)
            .SelectMany(path => FindViolations(root, path, pattern))
            .ToList();

        Assert.True(violations.Count == 0, $"Use AppJsonSerializerOptions instead of ad hoc JsonSerializerOptions: {string.Join(", ", violations)}");
    }

    [Fact]
    public void ProductionJsonSerializerCallsUseSharedOptions()
    {
        var root = FindRepoRoot();
        var violations = EnumerateProductionSourceFiles(root)
            .SelectMany(path => FindSerializerCallsWithoutSharedOptions(root, path))
            .ToList();

        Assert.True(violations.Count == 0, $"JsonSerializer calls must pass AppJsonSerializerOptions: {string.Join(", ", violations)}");
    }

    static IEnumerable<string> FindViolations(string root, string path, string pattern)
    {
        var lines = File.ReadAllLines(path);
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].Contains(pattern, StringComparison.Ordinal))
                continue;
            if (HasExplicitExceptionComment(lines, index))
                continue;

            yield return $"{Normalize(Path.GetRelativePath(root, path))}:{index + 1}";
        }
    }

    static IEnumerable<string> FindSerializerCallsWithoutSharedOptions(string root, string path)
    {
        var lines = File.ReadAllLines(path);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (!IsJsonSerializerCall(line))
                continue;
            if (line.Contains("AppJsonSerializerOptions.", StringComparison.Ordinal))
                continue;

            yield return $"{Normalize(Path.GetRelativePath(root, path))}:{index + 1}";
        }
    }

    static IEnumerable<string> EnumerateProductionSourceFiles(string root) =>
        Directory
            .EnumerateFiles(Path.Combine(root, "AgentRp"), "*.*", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !Normalize(Path.GetRelativePath(root, path)).Equals("AgentRp/Serialization/AppJsonSerializerOptions.cs", StringComparison.OrdinalIgnoreCase));

    static bool IsJsonSerializerCall(string line) =>
        line.Contains("JsonSerializer.Serialize", StringComparison.Ordinal)
        || line.Contains("JsonSerializer.Deserialize", StringComparison.Ordinal)
        || line.Contains("ReadFromJsonAsync", StringComparison.Ordinal);

    static bool HasExplicitExceptionComment(IReadOnlyList<string> lines, int index)
    {
        var start = Math.Max(0, index - 2);
        var end = Math.Min(lines.Count - 1, index + 2);
        for (var commentIndex = start; commentIndex <= end; commentIndex++)
            if (lines[commentIndex].Contains("JsonSerializerOptions exception:", StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    static bool IsSourceFile(string path) =>
        path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);

    static bool IsBuildOutput(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    static string FindRepoRoot([CallerFilePath] string sourcePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgentRp.slnx")))
            directory = directory.Parent;

        if (directory is not null)
            return directory.FullName;

        directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgentRp.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate AgentRp.slnx.");
    }

    static string Normalize(string path) => path.Replace('\\', '/');

    sealed record JsonOptionsProbe(string DisplayName, TuningSupport Support);
}
