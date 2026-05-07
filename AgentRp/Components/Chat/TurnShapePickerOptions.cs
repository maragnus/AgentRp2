namespace AgentRp.Components.Chat;

public static class TurnShapePickerOptions
{
    public const string Auto = "Auto";
    public const string Default = "Brief";

    public static readonly IReadOnlyList<string> Explicit =
    [
        "Compact",
        Default,
        "Extended",
        "Monologue",
        "Silent",
        "Silent Monologue"
    ];

    public static readonly IReadOnlyList<string> All =
    [
        Auto,
        ..Explicit
    ];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Default;

        var trimmed = value.Trim();
        var key = trimmed
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .ToLowerInvariant();

        return key switch
        {
            "auto" => Auto,
            "compact" => "Compact",
            "brief" => Default,
            "extended" => "Extended",
            "monologue" => "Monologue",
            "silent" => "Silent",
            "silentmonologue" => "Silent Monologue",
            _ => trimmed
        };
    }

    public static string NormalizeExplicit(string? value)
    {
        var normalized = Normalize(value);
        return normalized == Auto ? Default : normalized;
    }
}
