namespace AgentRp.Services;

public static class TurnShapeRules
{
    public const string AutoLabel = "Auto";
    public const string CompactId = "compact";
    public const string BriefId = "brief";
    public const string ExtendedId = "extended";
    public const string NarrativeId = "narrative";
    public const string SilentId = "silent";
    public const string SilentExtendedId = "silent-extended";

    public const string CompactLabel = "Compact";
    public const string BriefLabel = "Brief";
    public const string ExtendedLabel = "Extended";
    public const string NarrativeLabel = "Narrative";
    public const string SilentLabel = "Silent";
    public const string SilentExtendedLabel = "Silent Extended";

    public const string DefaultLabel = BriefLabel;

    public static readonly IReadOnlyList<string> ExplicitLabels =
    [
        CompactLabel,
        BriefLabel,
        ExtendedLabel,
        NarrativeLabel,
        SilentLabel,
        SilentExtendedLabel
    ];

    public static readonly IReadOnlyList<string> AllLabels =
    [
        AutoLabel,
        ..ExplicitLabels
    ];

    public static readonly IReadOnlyList<string> PromptDefinitionOrder =
    [
        CompactId,
        SilentId,
        SilentExtendedId,
        BriefId,
        ExtendedId,
        NarrativeId
    ];

    public static string NormalizeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultLabel;

        var trimmed = value.Trim();
        return NormalizeKey(trimmed) switch
        {
            "auto" => AutoLabel,
            "compact" => CompactLabel,
            "brief" => BriefLabel,
            "extended" => ExtendedLabel,
            "narrative" => NarrativeLabel,
            "silent" => SilentLabel,
            "silentextended" => SilentExtendedLabel,
            _ => trimmed
        };
    }

    public static string NormalizeExplicitLabel(string? value)
    {
        var normalized = NormalizeLabel(value);
        return normalized == AutoLabel ? DefaultLabel : normalized;
    }

    public static string ToId(string value) =>
        value.Trim()
            .Replace("_", "-", StringComparison.Ordinal)
            .Replace(" ", "-", StringComparison.Ordinal)
            .ToLowerInvariant();

    public static string FormatPromptLabel(string value) =>
        value.Trim().ToLowerInvariant();

    static string NormalizeKey(string value) =>
        value
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .ToLowerInvariant();
}
