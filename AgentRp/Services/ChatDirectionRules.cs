using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public static class ChatDirectionRules
{
    public static readonly string[] ControlledFields =
    [
        "genres",
        "tones",
        "themes",
        "pacing",
        "storyFocus",
        "boundaries",
        "explicitContent",
        "violentContent"
    ];

    static readonly string[] PatchFields =
    [
        .. ControlledFields,
        "setting",
        "premise",
        "customGuidance"
    ];

    public static JsonObject OptionsSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["fields"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Optional chat direction fields to read. Omit to read all chat direction options and current values.",
                ["uniqueItems"] = true,
                ["items"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray(ControlledFields.Select(field => (JsonNode?)JsonValue.Create(field)).ToArray())
                }
            }
        },
        ["additionalProperties"] = false
    };

    public static JsonObject PatchSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["updates"] = new JsonObject
            {
                ["type"] = "object",
                ["description"] = "Only the chat direction fields to set or replace. Use already-read options unless the relevant options are unknown or stale.",
                ["properties"] = new JsonObject
                {
                    ["genres"] = ControlledArray("Genre ids.", ChatDirectionService.MaxGenres),
                    ["tones"] = ControlledArray("Tone ids.", ChatDirectionService.MaxTones),
                    ["themes"] = ControlledArray("Theme ids.", ChatDirectionService.MaxThemes),
                    ["pacing"] = ControlledArray("Pacing ids.", ChatDirectionService.MaxPacing),
                    ["storyFocus"] = ControlledArray("Story focus ids.", ChatDirectionService.MaxStoryFocus),
                    ["boundaries"] = ControlledArray("Boundary ids.", ChatDirectionService.MaxBoundaries),
                    ["explicitContent"] = IntensityField("Explicit content intensity."),
                    ["violentContent"] = IntensityField("Violent content intensity."),
                    ["setting"] = StringField("Where the story lives, socially and physically."),
                    ["premise"] = StringField("The current story promise or direction."),
                    ["customGuidance"] = StringField("Extra direction this chat should consistently honor.")
                },
                ["additionalProperties"] = false
            },
            ["reason"] = new JsonObject { ["type"] = "string" }
        },
        ["required"] = new JsonArray { "updates" },
        ["additionalProperties"] = false
    };

    public static object Context(ChatDirectionState state) => new
    {
        current = Shape(ChatDirectionService.NormalizeState(state)),
        controlledFields = ControlledFields,
        instruction = "Use already-read chat direction options when available; call get_chat_direction_options only when relevant options are unknown or stale."
    };

    public static object Options(ChatDirectionState state, IReadOnlyList<string> requestedFields)
    {
        var normalized = ChatDirectionService.NormalizeState(state);
        var fields = requestedFields.Count == 0 ? ControlledFields : requestedFields;
        return new
        {
            current = Shape(normalized),
            limits = Limits(),
            fields = fields.Distinct(StringComparer.Ordinal).ToDictionary(field => field, OptionPayload, StringComparer.Ordinal)
        };
    }

    public static JsonObject JsonObject(ChatDirectionState state) => StoryEntityPatchService.ToJsonObject(Shape(ChatDirectionService.NormalizeState(state)));

    public static object Shape(ChatDirectionState state) => new
    {
        state.Genres,
        state.Tones,
        state.Themes,
        state.Pacing,
        state.StoryFocus,
        state.Boundaries,
        state.ExplicitContent,
        state.ViolentContent,
        state.Setting,
        state.Premise,
        state.CustomGuidance
    };

    public static void ValidatePatch(JsonElement updates)
    {
        if (updates.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("The chat direction patch must provide an updates object.");

        foreach (var property in updates.EnumerateObject())
            if (!PatchFields.Contains(property.Name, StringComparer.Ordinal))
                throw new InvalidOperationException($"The chat direction patch contains unsupported field '{property.Name}'. Call get_chat_direction_options only if you need the current shape, then retry with only supported fields.");

        ValidateOptionArray(updates, "genres", "genres", ChatDirectionService.Library.Genres, ChatDirectionService.MaxGenres);
        ValidateOptionArray(updates, "tones", "tones", ChatDirectionService.Library.Tones, ChatDirectionService.MaxTones);
        ValidateOptionArray(updates, "themes", "themes", ChatDirectionService.Library.Themes, ChatDirectionService.MaxThemes);
        ValidateOptionArray(updates, "pacing", "pacing", ChatDirectionService.Library.Pacing, ChatDirectionService.MaxPacing);
        ValidateOptionArray(updates, "storyFocus", "story focus", ChatDirectionService.Library.StoryFocus, ChatDirectionService.MaxStoryFocus);
        ValidateOptionArray(updates, "boundaries", "boundaries", ChatDirectionService.Library.Boundaries, ChatDirectionService.MaxBoundaries);
        ValidateIntensity(updates, "explicitContent", "explicit content");
        ValidateIntensity(updates, "violentContent", "violent content");
    }

    public static StoryAssistantChangeRisk Risk(JsonElement updates) =>
        updates.TryGetProperty("explicitContent", out _) ||
        updates.TryGetProperty("violentContent", out _) ||
        updates.TryGetProperty("boundaries", out _) ||
        updates.TryGetProperty("setting", out _) ||
        updates.TryGetProperty("premise", out _) ||
        updates.TryGetProperty("customGuidance", out _)
            ? StoryAssistantChangeRisk.Major
            : StoryAssistantChangeRisk.Low;

    public static void Apply(ChatDirectionState target, JsonElement updates)
    {
        SetList(updates, "genres", value => target.Genres = value);
        SetList(updates, "tones", value => target.Tones = value);
        SetList(updates, "themes", value => target.Themes = value);
        SetList(updates, "pacing", value => target.Pacing = value);
        SetList(updates, "storyFocus", value => target.StoryFocus = value);
        SetList(updates, "boundaries", value => target.Boundaries = value);
        SetIntensity(updates, "explicitContent", value => target.ExplicitContent = value);
        SetIntensity(updates, "violentContent", value => target.ViolentContent = value);
        Set(updates, "setting", value => target.Setting = value);
        Set(updates, "premise", value => target.Premise = value);
        Set(updates, "customGuidance", value => target.CustomGuidance = value);
    }

    static object Limits() => new
    {
        maxGenres = ChatDirectionService.MaxGenres,
        maxTones = ChatDirectionService.MaxTones,
        maxThemes = ChatDirectionService.MaxThemes,
        maxPacing = ChatDirectionService.MaxPacing,
        maxStoryFocus = ChatDirectionService.MaxStoryFocus,
        maxBoundaries = ChatDirectionService.MaxBoundaries
    };

    static object OptionPayload(string field) => field switch
    {
        "genres" => new { maxItems = ChatDirectionService.MaxGenres, options = Options(ChatDirectionService.Library.Genres) },
        "tones" => new { maxItems = ChatDirectionService.MaxTones, options = Options(ChatDirectionService.Library.Tones) },
        "themes" => new { maxItems = ChatDirectionService.MaxThemes, options = Options(ChatDirectionService.Library.Themes) },
        "pacing" => new { maxItems = ChatDirectionService.MaxPacing, options = Options(ChatDirectionService.Library.Pacing) },
        "storyFocus" => new { maxItems = ChatDirectionService.MaxStoryFocus, options = Options(ChatDirectionService.Library.StoryFocus) },
        "boundaries" => new { maxItems = ChatDirectionService.MaxBoundaries, options = Options(ChatDirectionService.Library.Boundaries) },
        "explicitContent" => new { options = IntensityOptions() },
        "violentContent" => new { options = IntensityOptions() },
        _ => throw ChatDirectionValidationException.ForField(field, $"Reading chat direction options failed because '{field}' is not a supported controlled direction field.")
    };

    static object Options(IReadOnlyList<CharacterOption> options) =>
        options.Select(option => new
        {
            option.Id,
            option.Label,
            description = option.Hover
        });

    static object IntensityOptions() =>
        Enum.GetValues<ContentIntensity>().Select(value => new
        {
            id = value.ToString(),
            label = ChatDirectionService.FormatIntensityLabel(value),
            description = ChatDirectionService.FormatIntensity(value)
        });

    static JsonObject StringField(string description) => new()
    {
        ["type"] = "string",
        ["description"] = description
    };

    static JsonObject IntensityField(string description) => new()
    {
        ["type"] = "string",
        ["description"] = $"{description} Use already-read options unless this field is unknown or stale.",
        ["enum"] = new JsonArray(Enum.GetNames<ContentIntensity>().Select(name => (JsonNode?)JsonValue.Create(name)).ToArray())
    };

    static JsonObject ControlledArray(string description, int max) => new()
    {
        ["type"] = "array",
        ["description"] = $"{description} Use already-read options unless this field is unknown or stale.",
        ["uniqueItems"] = true,
        ["maxItems"] = max,
        ["items"] = new JsonObject { ["type"] = "string" }
    };

    static void ValidateOptionArray(JsonElement root, string field, string label, IReadOnlyList<CharacterOption> options, int max)
    {
        if (!root.TryGetProperty(field, out var value))
            return;

        if (value.ValueKind != JsonValueKind.Array)
            throw ChatDirectionValidationException.ForField(field, $"The chat direction patch failed because {label} must be an array of string ids.");

        var values = ReadArray(value, field, label);
        if (values.Count > max)
            throw ChatDirectionValidationException.ForField(field, $"The chat direction patch failed because {label} contains {values.Count} values, but the maximum is {max}.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in values)
        {
            if (!seen.Add(item))
                throw ChatDirectionValidationException.ForField(field, $"The chat direction patch failed because {label} contains duplicate value '{item}'.");

            if (!options.Any(option => string.Equals(option.Id, item, StringComparison.Ordinal)))
                throw ChatDirectionValidationException.ForField(field, $"The chat direction patch failed because {label} contains invalid value '{item}'. Controlled chat direction fields must use valid ids.");
        }
    }

    static void ValidateIntensity(JsonElement root, string field, string label)
    {
        if (!root.TryGetProperty(field, out var value))
            return;

        if (value.ValueKind != JsonValueKind.String)
            throw ChatDirectionValidationException.ForField(field, $"The chat direction patch failed because {label} must be a string intensity value.");

        var text = value.GetString() ?? "";
        if (!Enum.TryParse<ContentIntensity>(text, ignoreCase: false, out _))
            throw ChatDirectionValidationException.ForField(field, $"The chat direction patch failed because {label} contains invalid value '{text}'. Use Forbidden, Allowed, or Encouraged.");
    }

    static List<string> ReadArray(JsonElement array, string field, string label)
    {
        var values = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw ChatDirectionValidationException.ForField(field, $"The chat direction patch failed because {label} must contain only string ids.");

            var value = item.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(value))
                throw ChatDirectionValidationException.ForField(field, $"The chat direction patch failed because {label} contains an empty value.");

            values.Add(value);
        }

        return values;
    }

    static void Set(JsonElement root, string name, Action<string> setter)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            setter(value.GetString() ?? "");
    }

    static void SetList(JsonElement root, string name, Action<List<string>> setter)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
            setter(value.EnumerateArray().Select(item => item.GetString() ?? "").ToList());
    }

    static void SetIntensity(JsonElement root, string name, Action<ContentIntensity> setter)
    {
        if (root.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            Enum.TryParse<ContentIntensity>(value.GetString(), ignoreCase: false, out var intensity))
            setter(intensity);
    }
}

public sealed class ChatDirectionValidationException(string message, IReadOnlyList<string> fields) : InvalidOperationException(message)
{
    public IReadOnlyList<string> Fields { get; } = fields;

    public static ChatDirectionValidationException ForField(string field, string message) => new(message, [field]);
}
