namespace AgentRp.Components.Chat;

public static class TurnShapePickerOptions
{
    public const string Auto = "Auto";
    public const string Default = "Brief";

    public static readonly IReadOnlyList<string> All =
    [
        Auto,
        "Compact",
        Default,
        "Extended",
        "Monologue",
        "Silent",
        "Silent Monologue"
    ];

    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Default : value.Trim();
}
