using AgentRp.Services;

namespace AgentRp.Components.Chat;

public static class TurnShapePickerOptions
{
    public const string Auto = TurnShapeRules.AutoLabel;
    public const string Default = TurnShapeRules.DefaultLabel;

    public static readonly IReadOnlyList<string> Explicit = TurnShapeRules.ExplicitLabels;

    public static readonly IReadOnlyList<string> All = TurnShapeRules.AllLabels;

    public static string Normalize(string? value) => TurnShapeRules.NormalizeLabel(value);

    public static string NormalizeExplicit(string? value) => TurnShapeRules.NormalizeExplicitLabel(value);
}
