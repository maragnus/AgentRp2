using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public static class CharacterProfileRules
{
    public const int MaxSceneRoles = 2;
    public const int RecommendedMinTraits = 3;
    public const int MaxTraits = 6;
    public const int MaxPronouns = 4;
    public const int MaxSoftSpots = 3;
    public const int MaxAvoidPatterns = 5;
    public const int MaxHairStyles = 4;
    public const int MaxComplexion = 4;
    public const int MaxBodyProportions = 5;
    public const int MaxPresentation = 5;

    public static readonly CharacterOption[] PronounOptions =
    [
        new("he/him", "he/him", "Use he/him pronouns."),
        new("she/her", "she/her", "Use she/her pronouns."),
        new("they/them", "they/them", "Use they/them pronouns."),
        new("xe/xem", "xe/xem", "Use xe/xem pronouns.")
    ];

    public static readonly string[] ControlledCharacterFields =
    [
        "pronouns",
        "sceneRoles",
        "traits",
        "coreDrive",
        "coreFear",
        "surfaceMask",
        "hiddenTruth",
        "sentenceStyle",
        "honestyStyle",
        "emotionalLeakage",
        "actionFingerprint",
        "stressPattern",
        "softSpots",
        "avoidPatterns",
        "appearanceProfile",
        "relationshipType",
        "privateTension"
    ];

    public static JsonObject CharacterPatchSchema() => new()
    {
        ["type"] = "object",
        ["description"] = "Only the character fields to set or replace. Before setting controlled profile fields, call get_character_profile_options for the relevant fields.",
        ["properties"] = new JsonObject
        {
            ["name"] = StringField("Character name."),
            ["summary"] = StringField("One-sentence character summary."),
            ["personality"] = StringField("Freeform personality notes."),
            ["appearance"] = StringField("Freeform appearance notes."),
            ["appearanceProfile"] = AppearanceProfileSchema(),
            ["backstory"] = StringField("Freeform backstory."),
            ["voice"] = StringField("Freeform voice notes."),
            ["notes"] = StringField("Freeform private or extra notes."),
            ["pronouns"] = ControlledArray("Pronoun values. Use only the allowed values.", MaxPronouns, PronounOptions),
            ["sceneRoles"] = ControlledArray("Scene role ids. Call get_character_profile_options with fields ['sceneRoles'] before setting.", MaxSceneRoles),
            ["traits"] = ControlledArray($"Trait ids. Aim for {RecommendedMinTraits}-{MaxTraits} total when bootstrapping. Call get_character_profile_options with fields ['traits'] before setting.", MaxTraits),
            ["coreDrive"] = ControlledString("Core drive id. Call get_character_profile_options with fields ['coreDrive'] before setting; empty string clears it."),
            ["coreFear"] = ControlledString("Core fear id. Call get_character_profile_options with fields ['coreFear'] before setting; empty string clears it."),
            ["surfaceMask"] = ControlledString("Surface mask id. Call get_character_profile_options with fields ['surfaceMask'] before setting; empty string clears it."),
            ["hiddenTruth"] = ControlledString("Hidden truth id. Call get_character_profile_options with fields ['hiddenTruth'] before setting; empty string clears it."),
            ["sentenceStyle"] = ControlledString("Sentence style id. Call get_character_profile_options with fields ['sentenceStyle'] before setting; empty string clears it."),
            ["honestyStyle"] = ControlledString("Honesty style id. Call get_character_profile_options with fields ['honestyStyle'] before setting; empty string clears it."),
            ["emotionalLeakage"] = ControlledString("Emotional leakage id. Call get_character_profile_options with fields ['emotionalLeakage'] before setting; empty string clears it."),
            ["actionFingerprint"] = ControlledString("Action fingerprint id. Call get_character_profile_options with fields ['actionFingerprint'] before setting; empty string clears it."),
            ["stressPattern"] = ControlledString("Stress pattern id. Call get_character_profile_options with fields ['stressPattern'] before setting; empty string clears it."),
            ["softSpots"] = ControlledArray("Soft spot ids. Call get_character_profile_options with fields ['softSpots'] before setting.", MaxSoftSpots),
            ["avoidPatterns"] = ControlledArray("Avoid pattern ids. Call get_character_profile_options with fields ['avoidPatterns'] before setting.", MaxAvoidPatterns)
        },
        ["additionalProperties"] = false
    };

    public static JsonObject RelationshipSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["sourceCharacterId"] = new JsonObject { ["type"] = "string" },
            ["targetCharacterId"] = new JsonObject { ["type"] = "string" },
            ["howSourceSeesTarget"] = new JsonObject { ["type"] = "string" },
            ["howTargetSeesSource"] = new JsonObject { ["type"] = "string" },
            ["publicDynamic"] = new JsonObject { ["type"] = "string" },
            ["privateTension"] = ControlledString("Shared dynamic value. Call get_character_profile_options with fields ['privateTension'] before setting."),
            ["relationshipType"] = ControlledString("Bond type value. Call get_character_profile_options with fields ['relationshipType'] before setting."),
            ["reason"] = new JsonObject { ["type"] = "string" }
        },
        ["required"] = new JsonArray { "sourceCharacterId", "targetCharacterId" },
        ["additionalProperties"] = false
    };

    public static JsonObject ProfileOptionsSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["fields"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Optional controlled profile fields to read. Omit to read all character profile options.",
                ["uniqueItems"] = true,
                ["items"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray(ControlledCharacterFields.Select(field => (JsonNode?)JsonValue.Create(field)).ToArray())
                }
            }
        },
        ["additionalProperties"] = false
    };

    public static object Context(CharacterTraitLibraryState library)
    {
        return new
        {
            limits = Limits(),
            controlledFields = ControlledCharacterFields,
            instruction = "Call get_character_profile_options with specific fields before setting controlled profile ids."
        };
    }

    public static object ProfileOptions(CharacterTraitLibraryState library, IReadOnlyList<string> requestedFields)
    {
        var normalized = CharacterTraitLibraryService.NormalizeState(library);
        var fields = requestedFields.Count == 0 ? ControlledCharacterFields : requestedFields;
        return new
        {
            limits = Limits(),
            fields = fields.Distinct(StringComparer.Ordinal).ToDictionary(field => field, field => OptionPayload(normalized, field), StringComparer.Ordinal)
        };
    }

    public static string FormatPronouns(IEnumerable<string> values) =>
        string.Join(", ", values.Where(value => PronounOptions.Any(option => string.Equals(option.Id, value, StringComparison.Ordinal))));

    static object Limits() => new
    {
        maxSceneRoles = MaxSceneRoles,
        recommendedMinTraits = RecommendedMinTraits,
        maxTraits = MaxTraits,
        maxPronouns = MaxPronouns,
        maxSoftSpots = MaxSoftSpots,
        maxAvoidPatterns = MaxAvoidPatterns,
        maxHairStyles = MaxHairStyles,
        maxComplexion = MaxComplexion,
        maxBodyProportions = MaxBodyProportions,
        maxPresentation = MaxPresentation
    };

    public static void ValidateCharacterPatch(JsonElement updates, CharacterTraitLibraryState library)
    {
        var normalized = CharacterTraitLibraryService.NormalizeState(library);
        ValidateOptionArray(updates, "pronouns", "pronouns", PronounOptions, MaxPronouns);
        ValidateOptionArray(updates, "sceneRoles", "scene roles", normalized.SceneRoles, MaxSceneRoles);
        ValidateOptionArray(updates, "traits", "traits", TraitOptions(normalized), MaxTraits);
        ValidateOption(updates, "coreDrive", "core drive", normalized.CoreDrives);
        ValidateOption(updates, "coreFear", "core fear", normalized.CoreFears);
        ValidateOption(updates, "surfaceMask", "surface mask", normalized.SurfaceMasks);
        ValidateOption(updates, "hiddenTruth", "hidden truth", normalized.HiddenTruths);
        ValidateOption(updates, "sentenceStyle", "sentence style", normalized.SentenceStyles);
        ValidateOption(updates, "honestyStyle", "honesty style", normalized.HonestyStyles);
        ValidateOption(updates, "emotionalLeakage", "emotional leakage", normalized.EmotionalLeakages);
        ValidateOption(updates, "actionFingerprint", "action fingerprint", normalized.ActionFingerprints);
        ValidateOption(updates, "stressPattern", "stress pattern", normalized.StressPatterns);
        ValidateOptionArray(updates, "softSpots", "soft spots", normalized.SoftSpots, MaxSoftSpots);
        ValidateOptionArray(updates, "avoidPatterns", "avoid patterns", normalized.AvoidPatterns, MaxAvoidPatterns);
        ValidateAppearanceProfile(updates, normalized);
    }

    public static void ValidateRelationshipPatch(JsonElement root, CharacterTraitLibraryState library)
    {
        var normalized = CharacterTraitLibraryService.NormalizeState(library);
        ValidateStringValue(root, "relationshipType", "relationship type", normalized.BondTypes);
        ValidateStringValue(root, "privateTension", "relationship dynamic", normalized.Dynamics);
    }

    static JsonObject StringField(string description) => new()
    {
        ["type"] = "string",
        ["description"] = description
    };

    static JsonObject AppearanceProfileSchema() => new()
    {
        ["type"] = "object",
        ["description"] = "Optional structured appearance selections. Use get_character_profile_options with fields ['appearanceProfile'] before setting. Omit unchanged fields.",
        ["properties"] = new JsonObject
        {
            ["hairColor"] = ControlledString("Hair color id; empty string clears it."),
            ["hairStyles"] = ControlledArray("Hair style or length ids.", MaxHairStyles),
            ["eyeColor"] = ControlledString("Eye color id; empty string clears it."),
            ["faceShape"] = ControlledString("Face shape id; empty string clears it."),
            ["skinTone"] = ControlledString("Skin tone id; empty string clears it."),
            ["complexion"] = ControlledArray("Complexion ids.", MaxComplexion),
            ["height"] = ControlledString("Height id; empty string clears it."),
            ["build"] = ControlledString("Build id; empty string clears it."),
            ["bodyProportions"] = ControlledArray("Body proportion ids.", MaxBodyProportions),
            ["presentation"] = ControlledArray("Presentation and bearing ids.", MaxPresentation),
            ["attractiveness"] = ControlledString("Attractiveness id; empty string clears it.")
        },
        ["additionalProperties"] = false
    };

    static IReadOnlyList<CharacterOption> TraitOptions(CharacterTraitLibraryState library) =>
        library.TraitCategories.SelectMany(group => group.Items).ToList();

    static object Options(IReadOnlyList<CharacterOption> options) =>
        options.Select(option => new
        {
            option.Id,
            option.Label,
            description = option.Hover
        });

    static void ValidateOption(JsonElement root, string field, string label, IReadOnlyList<CharacterOption> options)
    {
        if (!root.TryGetProperty(field, out var value))
            return;

        if (value.ValueKind != JsonValueKind.String)
            throw CharacterProfileValidationException.ForField(field, $"The character patch failed because {label} must be a string id.");

        var id = value.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(id))
            return;

        if (!options.Any(option => string.Equals(option.Id, id, StringComparison.Ordinal)))
            throw CharacterProfileValidationException.ForField(field, $"The character patch failed because {label} contains invalid value '{id}'. Controlled character profile fields must use valid ids.");
    }

    static void ValidateOptionArray(JsonElement root, string field, string label, IReadOnlyList<CharacterOption> options, int max)
    {
        if (!root.TryGetProperty(field, out var value))
            return;

        if (value.ValueKind != JsonValueKind.Array)
            throw CharacterProfileValidationException.ForField(field, $"The character patch failed because {label} must be an array of string ids.");

        var values = ReadArray(value, field, label);
        if (values.Count > max)
            throw CharacterProfileValidationException.ForField(field, $"The character patch failed because {label} contains {values.Count} values, but the maximum is {max}.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in values)
        {
            if (!seen.Add(item))
                throw CharacterProfileValidationException.ForField(field, $"The character patch failed because {label} contains duplicate value '{item}'.");

            if (!options.Any(option => string.Equals(option.Id, item, StringComparison.Ordinal)))
                throw CharacterProfileValidationException.ForField(field, $"The character patch failed because {label} contains invalid value '{item}'. Controlled character profile fields must use valid ids.");
        }
    }

    static void ValidateStringValue(JsonElement root, string field, string label, IReadOnlyList<string> values)
    {
        if (!root.TryGetProperty(field, out var value))
            return;

        if (value.ValueKind != JsonValueKind.String)
            throw CharacterProfileValidationException.ForField(field, $"The relationship patch failed because {label} must be a string.");

        var text = value.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (!values.Contains(text, StringComparer.Ordinal))
            throw CharacterProfileValidationException.ForField(field, $"The relationship patch failed because {label} contains invalid value '{text}'. Controlled relationship fields must use valid values.");
    }

    static void ValidateAppearanceProfile(JsonElement root, CharacterTraitLibraryState library)
    {
        if (!root.TryGetProperty("appearanceProfile", out var value))
            return;

        if (value.ValueKind != JsonValueKind.Object)
            throw CharacterProfileValidationException.ForField("appearanceProfile", "The character patch failed because appearanceProfile must be an object.");

        ValidateOption(value, "hairColor", "hair color", library.HairColors);
        ValidateOptionArray(value, "hairStyles", "hair styles", library.HairStyles, MaxHairStyles);
        ValidateOption(value, "eyeColor", "eye color", library.EyeColors);
        ValidateOption(value, "faceShape", "face shape", library.FaceShapes);
        ValidateOption(value, "skinTone", "skin tone", library.SkinTones);
        ValidateOptionArray(value, "complexion", "complexion", library.Complexions, MaxComplexion);
        ValidateOption(value, "height", "height", library.Heights);
        ValidateOption(value, "build", "build", library.Builds);
        ValidateOptionArray(value, "bodyProportions", "body proportions", library.BodyProportions, MaxBodyProportions);
        ValidateOptionArray(value, "presentation", "presentation", library.Presentations, MaxPresentation);
        ValidateOption(value, "attractiveness", "attractiveness", library.AttractivenessLevels);
    }

    static List<string> ReadArray(JsonElement array, string field, string label)
    {
        var values = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw CharacterProfileValidationException.ForField(field, $"The character patch failed because {label} must contain only string ids.");

            var value = item.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(value))
                throw CharacterProfileValidationException.ForField(field, $"The character patch failed because {label} contains an empty value.");

            values.Add(value);
        }

        return values;
    }

    static JsonObject ControlledString(string description) => new()
    {
        ["type"] = "string",
        ["description"] = description
    };

    static JsonObject ControlledArray(string description, int max, IReadOnlyList<CharacterOption>? options = null)
    {
        var items = new JsonObject { ["type"] = "string" };
        if (options is not null)
            items["enum"] = new JsonArray(options.Select(option => (JsonNode?)JsonValue.Create(option.Id)).ToArray());

        return new()
        {
            ["type"] = "array",
            ["description"] = description,
            ["uniqueItems"] = true,
            ["maxItems"] = max,
            ["items"] = items
        };
    }

    static object OptionPayload(CharacterTraitLibraryState library, string field) => field switch
    {
        "pronouns" => new { maxItems = MaxPronouns, options = Options(PronounOptions) },
        "sceneRoles" => new { maxItems = MaxSceneRoles, options = Options(library.SceneRoles) },
        "traits" => new { recommendedMinItems = RecommendedMinTraits, maxItems = MaxTraits, groups = library.TraitCategories.Select(group => new { group.Name, options = Options(group.Items) }) },
        "coreDrive" => new { options = Options(library.CoreDrives), allowsEmpty = true },
        "coreFear" => new { options = Options(library.CoreFears), allowsEmpty = true },
        "surfaceMask" => new { options = Options(library.SurfaceMasks), allowsEmpty = true },
        "hiddenTruth" => new { options = Options(library.HiddenTruths), allowsEmpty = true },
        "sentenceStyle" => new { options = Options(library.SentenceStyles), allowsEmpty = true },
        "honestyStyle" => new { options = Options(library.HonestyStyles), allowsEmpty = true },
        "emotionalLeakage" => new { options = Options(library.EmotionalLeakages), allowsEmpty = true },
        "actionFingerprint" => new { options = Options(library.ActionFingerprints), allowsEmpty = true },
        "stressPattern" => new { options = Options(library.StressPatterns), allowsEmpty = true },
        "softSpots" => new { maxItems = MaxSoftSpots, options = Options(library.SoftSpots) },
        "avoidPatterns" => new { maxItems = MaxAvoidPatterns, options = Options(library.AvoidPatterns) },
        "appearanceProfile" => new
        {
            fields = new
            {
                hairColor = new { options = Options(library.HairColors), allowsEmpty = true },
                hairStyles = new { maxItems = MaxHairStyles, options = Options(library.HairStyles) },
                eyeColor = new { options = Options(library.EyeColors), allowsEmpty = true },
                faceShape = new { options = Options(library.FaceShapes), allowsEmpty = true },
                skinTone = new { options = Options(library.SkinTones), allowsEmpty = true },
                complexion = new { maxItems = MaxComplexion, options = Options(library.Complexions) },
                height = new { options = Options(library.Heights), allowsEmpty = true },
                build = new { options = Options(library.Builds), allowsEmpty = true },
                bodyProportions = new { maxItems = MaxBodyProportions, options = Options(library.BodyProportions) },
                presentation = new { maxItems = MaxPresentation, options = Options(library.Presentations) },
                attractiveness = new { options = Options(library.AttractivenessLevels), allowsEmpty = true }
            }
        },
        "relationshipType" => new { options = library.BondTypes },
        "privateTension" => new { options = library.Dynamics },
        _ => throw CharacterProfileValidationException.ForField(field, $"Reading character profile options failed because '{field}' is not a supported controlled profile field.")
    };
}

public sealed class CharacterProfileValidationException(string message, IReadOnlyList<string> fields) : InvalidOperationException(message)
{
    public IReadOnlyList<string> Fields { get; } = fields;

    public static CharacterProfileValidationException ForField(string field, string message) => new(message, [NormalizeField(field)]);

    static string NormalizeField(string field) => field switch
    {
        "scene roles" => "sceneRoles",
        "pronouns" => "pronouns",
        "traits" => "traits",
        "soft spots" => "softSpots",
        "avoid patterns" => "avoidPatterns",
        "appearance profile" => "appearanceProfile",
        "relationship type" => "relationshipType",
        "relationship dynamic" => "privateTension",
        _ => field
    };
}
