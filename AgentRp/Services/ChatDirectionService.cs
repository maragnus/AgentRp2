using System.Text;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed class ChatDirectionService
{
    public const int MaxGenres = 3;
    public const int MaxTones = 4;
    public const int MaxThemes = 5;
    public const int MaxPacing = 2;
    public const int MaxStoryFocus = 4;
    public const int MaxBoundaries = 6;

    public static ChatDirectionLibrary Library { get; } = new(
        Genres:
        [
            new("slice-of-life", "Slice of Life", "Everyday scenes where small choices matter."),
            new("modern-drama", "Modern Drama", "Grounded conflict in a contemporary setting."),
            new("gothic-romance", "Gothic Romance", "Intimacy, secrets, atmosphere, and emotional risk."),
            new("political-intrigue", "Political Intrigue", "Power, alliances, leverage, and public masks."),
            new("survival-drama", "Survival Drama", "Scarcity, danger, endurance, and difficult choices."),
            new("mystery", "Mystery", "Clues, suspicion, investigation, and reveals."),
            new("crime-drama", "Crime Drama", "Pressure from law, loyalty, debt, and consequence."),
            new("adventure", "Adventure", "Movement, discovery, obstacles, and momentum."),
            new("science-fiction", "Science Fiction", "Speculation, technology, systems, and future pressure."),
            new("historical-drama", "Historical Drama", "Period texture, social constraints, and legacy."),
            new("supernatural", "Supernatural", "Unexplained forces treated as part of the story world."),
            new("psychological-drama", "Psychological Drama", "Internal conflict, perception, and emotional strain.")
        ],
        Tones:
        [
            new("intimate", "Intimate", "Close emotional attention and charged quiet."),
            new("tense", "Tense", "Pressure sits under each exchange."),
            new("wry", "Wry", "Dry, restrained humor without breaking stakes."),
            new("melancholic", "Melancholic", "Loss, longing, and restrained sadness."),
            new("cinematic", "Cinematic", "Visually clear prose with strong scene movement."),
            new("grounded", "Grounded", "Practical, believable reactions and consequences."),
            new("playful", "Playful", "Lightness, teasing, and quick social rhythm."),
            new("bleak", "Bleak", "Hard choices and limited comfort."),
            new("hopeful", "Hopeful", "Difficulty remains, but repair feels possible."),
            new("dreamlike", "Dreamlike", "Soft logic, heightened sensory detail, and ambiguity."),
            new("gritty", "Gritty", "Texture, discomfort, and practical consequence."),
            new("tender", "Tender", "Gentleness and care get narrative weight.")
        ],
        Themes:
        [
            new("trust", "Trust", "Who can be believed, relied on, or allowed close."),
            new("betrayal", "Betrayal", "Promises break or reveal hidden cost."),
            new("redemption", "Redemption", "Repairing harm through action, not easy apology."),
            new("obsession", "Obsession", "Want narrows judgment and raises stakes."),
            new("power", "Power", "Control, leverage, status, and dependence."),
            new("identity", "Identity", "Self-understanding shifts under pressure."),
            new("loyalty", "Loyalty", "Commitment is tested by competing needs."),
            new("grief", "Grief", "Loss shapes action and silence."),
            new("healing", "Healing", "Recovery is uneven and earned."),
            new("temptation", "Temptation", "The wrong choice has real appeal."),
            new("duty", "Duty", "Obligation competes with desire."),
            new("freedom", "Freedom", "Autonomy, escape, and self-direction matter.")
        ],
        Pacing:
        [
            new("slow-burn", "Slow Burn", "Let tension accumulate before payoff."),
            new("pressure-cooker", "Pressure Cooker", "Keep stakes close and hard to escape."),
            new("episodic", "Episodic", "Let scenes resolve into distinct beats."),
            new("escalating", "Escalating", "Each turn should raise consequence or commitment."),
            new("reflective", "Reflective", "Give interiority and aftermath room."),
            new("action-forward", "Action-Forward", "Prefer decisions, movement, and visible consequence.")
        ],
        StoryFocus:
        [
            new("character-conflict", "Character Conflict", "Prioritize disagreement, need, and friction."),
            new("relationship-tension", "Relationship Tension", "Track closeness, distance, and charged subtext."),
            new("exploration", "Exploration", "Reveal place, history, and atmosphere through action."),
            new("mystery-solving", "Mystery Solving", "Advance clues, suspicion, and deduction."),
            new("survival", "Survival", "Center resources, danger, and difficult tradeoffs."),
            new("political-moves", "Political Moves", "Track leverage, alliances, and reputation."),
            new("emotional-fallout", "Emotional Fallout", "Let consequences change how people act."),
            new("moral-choice", "Moral Choice", "Put values under pressure."),
            new("slice-of-life-beats", "Slice-of-Life Beats", "Make ordinary tasks reveal character."),
            new("action-setpieces", "Action Setpieces", "Use physical stakes and spatial clarity.")
        ],
        Boundaries:
        [
            new("no-fourth-wall-breaks", "No Fourth-Wall Breaks", "Keep narration inside the story."),
            new("no-random-cruelty", "No Random Cruelty", "Do not add cruelty without setup or consequence."),
            new("no-slapstick-undercut", "No Slapstick Undercut", "Do not dissolve serious stakes into broad comedy."),
            new("no-easy-reconciliation", "No Easy Reconciliation", "Repair should be earned on the page."),
            new("no-psychic-knowledge", "No Psychic Knowledge", "Characters only act on what they know."),
            new("no-deus-ex-machina", "No Sudden Rescue", "Avoid convenient solutions that bypass choices."),
            new("no-genre-parody", "No Genre Parody", "Do not mock the premise unless asked."),
            new("keep-consequences", "Keep Consequences", "Let choices affect later scenes."),
            new("no-unearned-romance", "No Unearned Romance", "Do not jump to intimacy before the scene earns it."),
            new("no-sudden-tone-swerves", "No Sudden Tone Swerves", "Keep tonal changes motivated.")
        ]);

    public static ChatDirectionState CreateDefaultState() => new()
    {
        SchemaVersion = 1
    };

    public static ChatDirectionState NormalizeState(ChatDirectionState? state)
    {
        if (state is null)
            return CreateDefaultState();

        return new()
        {
            SchemaVersion = 1,
            UpdatedUtc = state.UpdatedUtc,
            Genres = NormalizeSelection(state.Genres, Library.Genres, MaxGenres),
            Tones = NormalizeSelection(state.Tones, Library.Tones, MaxTones),
            Themes = NormalizeSelection(state.Themes, Library.Themes, MaxThemes),
            Pacing = NormalizeSelection(state.Pacing, Library.Pacing, MaxPacing),
            StoryFocus = NormalizeSelection(state.StoryFocus, Library.StoryFocus, MaxStoryFocus),
            Boundaries = NormalizeSelection(state.Boundaries, Library.Boundaries, MaxBoundaries),
            ExplicitContent = NormalizeIntensity(state.ExplicitContent),
            ViolentContent = NormalizeIntensity(state.ViolentContent),
            Setting = state.Setting.Trim(),
            Premise = state.Premise.Trim(),
            CustomGuidance = state.CustomGuidance.Trim()
        };
    }

    public static string BuildStoryContext(ChatDirectionState state)
    {
        var normalized = NormalizeState(state);
        var builder = new StringBuilder();
        AppendOptions(builder, "Genres", normalized.Genres, Library.Genres);
        AppendOptions(builder, "Tone", normalized.Tones, Library.Tones);
        AppendOptions(builder, "Themes", normalized.Themes, Library.Themes);
        AppendOptions(builder, "Pacing", normalized.Pacing, Library.Pacing);
        AppendOptions(builder, "Story focus", normalized.StoryFocus, Library.StoryFocus);
        AppendField(builder, "Setting", normalized.Setting);
        AppendField(builder, "Premise", normalized.Premise);
        AppendField(builder, "Custom guidance", normalized.CustomGuidance);
        return builder.ToString().TrimEnd();
    }

    public static string BuildContentGuidance(ChatDirectionState state)
    {
        var normalized = NormalizeState(state);
        var builder = new StringBuilder();
        builder.AppendLine("**Content guidance:**");
        builder.AppendLine($"- Explicit content: {FormatIntensity(normalized.ExplicitContent)}");
        builder.AppendLine($"- Violent content: {FormatIntensity(normalized.ViolentContent)}");
        AppendOptions(builder, "Boundaries", normalized.Boundaries, Library.Boundaries);
        return builder.ToString().TrimEnd();
    }

    public static string FormatIntensity(ContentIntensity intensity) => NormalizeIntensity(intensity) switch
    {
        ContentIntensity.Forbidden => "Forbidden. Do not introduce or describe this content.",
        ContentIntensity.Encouraged => "Encouraged when supported and scene-relevant. Lean into it without inventing it.",
        _ => "Allowed when naturally supported by the scene."
    };

    public static string FormatIntensityLabel(ContentIntensity intensity) => NormalizeIntensity(intensity) switch
    {
        ContentIntensity.Forbidden => "Forbidden",
        ContentIntensity.Encouraged => "Encouraged",
        _ => "Allowed"
    };

    static void AppendField(StringBuilder builder, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            builder.AppendLine($"- {label}: {CollapseWhitespace(value)}");
    }

    static void AppendOptions(StringBuilder builder, string label, IReadOnlyList<string> selectedIds, IReadOnlyList<CharacterOption> options)
    {
        var labels = selectedIds
            .Select(id => options.FirstOrDefault(option => string.Equals(option.Id, id, StringComparison.Ordinal))?.Label ?? id)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (labels.Count > 0)
            builder.AppendLine($"- {label}: {string.Join(", ", labels)}");
    }

    static List<string> NormalizeSelection(IReadOnlyList<string> values, IReadOnlyList<CharacterOption> options, int max)
    {
        var knownIds = options.Select(option => option.Id).ToHashSet(StringComparer.Ordinal);
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Where(knownIds.Contains)
            .Take(max)
            .ToList();
    }

    static ContentIntensity NormalizeIntensity(ContentIntensity intensity) =>
        intensity is ContentIntensity.Forbidden or ContentIntensity.Encouraged ? intensity : ContentIntensity.Allowed;

    static string CollapseWhitespace(string value) =>
        string.Join(" ", value.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

public sealed record ChatDirectionLibrary(
    IReadOnlyList<CharacterOption> Genres,
    IReadOnlyList<CharacterOption> Tones,
    IReadOnlyList<CharacterOption> Themes,
    IReadOnlyList<CharacterOption> Pacing,
    IReadOnlyList<CharacterOption> StoryFocus,
    IReadOnlyList<CharacterOption> Boundaries);
