using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed record NarratorVoicePreset(string Id, string Label, string Description);

public static class NarratorProfileService
{
    public static readonly IReadOnlyList<NarratorVoicePreset> VoicePresets =
    [
        new("neutral-continuity", "Neutral Continuity", "Clear orientation, clean handoffs, no extra mood."),
        new("cinematic-descriptive", "Cinematic Descriptive", "Visual, grounded, and scene-aware without taking over."),
        new("atmospheric-literary", "Atmospheric Literary", "Sensory, textured, and emotionally observant."),
        new("tense-foreshadowing", "Tense Foreshadowing", "Pressure, implication, and hints of trouble ahead."),
        new("noir-observer", "Noir Observer", "Dry, shadowed, perceptive, and a little fatalistic."),
        new("mythic-fable", "Mythic Fable", "Elevated, symbolic, and shaped like an old story being retold."),
        new("wry-companion", "Wry Companion", "Lightly amused, warm, and human without becoming jokey."),
        new("sparse-screenplay", "Sparse Screenplay", "Lean staging, crisp action, and minimal interior framing.")
    ];

    public static NarratorProfileState CreateDefaultState() => new()
    {
        VoicePreset = "cinematic-descriptive",
        SetupDepth = 1,
        VisualDetail = 1,
        TransitionContext = 1,
        Foreshadowing = 0,
        DirectionStrength = 1,
        CustomGuidance = "",
        VoiceSelections = []
    };

    public static NarratorProfileState NormalizeState(NarratorProfileState? state)
    {
        var defaults = CreateDefaultState();
        if (state is null)
            return defaults;

        var preset = VoicePresets.Any(option => option.Id == state.VoicePreset)
            ? state.VoicePreset
            : defaults.VoicePreset;

        return new()
        {
            SchemaVersion = 1,
            VoicePreset = preset,
            SetupDepth = ClampLevel(state.SetupDepth),
            VisualDetail = ClampLevel(state.VisualDetail),
            TransitionContext = ClampLevel(state.TransitionContext),
            Foreshadowing = ClampLevel(state.Foreshadowing),
            DirectionStrength = ClampLevel(state.DirectionStrength),
            CustomGuidance = state.CustomGuidance.Trim(),
            VoiceSelections = state.VoiceSelections.ToDictionary(pair => pair.Key, pair => Clone(pair.Value), StringComparer.Ordinal)
        };
    }

    static CharacterVoiceSelection Clone(CharacterVoiceSelection value) => new()
    {
        VoiceId = value.VoiceId,
        VoiceName = value.VoiceName,
        UpdatedUtc = value.UpdatedUtc
    };

    public static string BuildPromptGuidance(NarratorProfileState state)
    {
        var normalized = NormalizeState(state);
        var preset = VoicePresets.First(option => option.Id == normalized.VoicePreset);
        var lines = new List<string>
        {
            "Narrator voice tuning:",
            $"- Voice: {preset.Label}. {preset.Description}",
            $"- Scene setup: {SetupDepthText(normalized.SetupDepth)}",
            $"- Visual detail: {VisualDetailText(normalized.VisualDetail)}",
            $"- Transition context: {TransitionContextText(normalized.TransitionContext)}",
            $"- Foreshadowing: {ForeshadowingText(normalized.Foreshadowing)}",
            $"- Story direction: {DirectionText(normalized.DirectionStrength)}",
            "- Write as narration only; never present the narrator as a character in the scene."
        };

        if (!string.IsNullOrWhiteSpace(normalized.CustomGuidance))
            lines.Add($"- Custom guidance: {normalized.CustomGuidance}");

        return string.Join(Environment.NewLine, lines);
    }

    public static string VoiceLabel(string id) =>
        VoicePresets.FirstOrDefault(option => option.Id == id)?.Label ?? VoicePresets.First(option => option.Id == "cinematic-descriptive").Label;

    public static string SetupDepthText(int value) => ClampLevel(value) switch
    {
        0 => "Brief orientation only; establish where everyone is without recapping.",
        1 => "Balanced setup; include enough placement and recent context to make the scene readable.",
        _ => "Detailed setup; clearly stage location, arrivals, positions, and immediate circumstances."
    };

    public static string VisualDetailText(int value) => ClampLevel(value) switch
    {
        0 => "Use spare visual detail.",
        1 => "Use concrete sensory detail where it clarifies the moment.",
        _ => "Use rich visual staging, atmosphere, physical distance, light, sound, and texture."
    };

    public static string TransitionContextText(int value) => ClampLevel(value) switch
    {
        0 => "Stay in the current moment; avoid explaining skipped time.",
        1 => "Bridge scene changes with concise continuity when useful.",
        _ => "When time has passed, summarize the transition and what changed before the new moment."
    };

    public static string ForeshadowingText(int value) => ClampLevel(value) switch
    {
        0 => "None; do not hint at future events beyond visible tension.",
        1 => "Subtle; hint through mood, detail, or implication without spoilers.",
        _ => "Strong; actively seed ominous or meaningful future pressure while staying playable."
    };

    public static string DirectionText(int value) => ClampLevel(value) switch
    {
        0 => "Hands-off; frame the scene without steering the story.",
        1 => "Light guidance; nudge pacing, stakes, and focus while respecting the current thread.",
        _ => "Active direction; introduce pressure, clarify stakes, and point the story toward the next interesting beat."
    };

    static int ClampLevel(int value) => Math.Clamp(value, 0, 2);
}
