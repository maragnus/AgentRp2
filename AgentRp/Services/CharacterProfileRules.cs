using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public static class CharacterProfileRules
{
    public const string ExtraAppearanceDetailsField = "extraAppearanceDetails";
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

    public static readonly string[] AppearanceFields =
    [
        "hairColor",
        "hairStyles",
        "eyeColor",
        "faceShape",
        "skinTone",
        "complexion",
        "height",
        "build",
        "bodyProportions",
        "presentation",
        "attractiveness"
    ];

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
        "hairColor",
        "hairStyles",
        "eyeColor",
        "faceShape",
        "skinTone",
        "complexion",
        "height",
        "build",
        "bodyProportions",
        "presentation",
        "attractiveness",
        "relationshipTypes",
        "privateTensions"
    ];

    public static JsonObject CharacterPatchSchema() => new()
    {
        ["type"] = "object",
        ["description"] = "Character fields to set or replace. Prefer a complete, useful character profile; existing unchanged values do not need to be resent. Before setting controlled profile fields, call get_character_profile_options for the relevant fields.",
        ["properties"] = new JsonObject
        {
            ["name"] = StringField("Character name."),
            ["summary"] = StringField("One-sentence character summary."),
            ["personality"] = StringField("Freeform personality notes."),
            [ExtraAppearanceDetailsField] = StringField("Extra visible appearance details such as scars, tattoos, birthmarks, lazy eye, prosthetics, distinctive marks, signature clothing, or other visual specifics."),
            ["hairColor"] = ControlledString("Hair color id. Use with the other appearance fields to create a complete visual profile."),
            ["hairStyles"] = ControlledArray("Hair style, texture, length, baldness, or styling ids.", MaxHairStyles),
            ["eyeColor"] = ControlledString("Eye color id."),
            ["faceShape"] = ControlledString("Overall face shape id."),
            ["skinTone"] = ControlledString("Skin tone id."),
            ["complexion"] = ControlledArray("Broad skin complexion quality ids.", MaxComplexion),
            ["height"] = ControlledString("Height id."),
            ["build"] = ControlledString("Body build id."),
            ["bodyProportions"] = ControlledArray("Body shape and proportion ids.", MaxBodyProportions),
            ["presentation"] = ControlledArray("Posture, bearing, movement, and visual presence ids.", MaxPresentation),
            ["attractiveness"] = ControlledString("Overall appeal or attractiveness id."),
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
        ["description"] = "Flat directional relationship fields to set between two characters. Every relationship update must populate every field; use complete arrays for relationshipTypes and privateTensions.",
        ["properties"] = new JsonObject
        {
            ["sourceCharacterId"] = StringField("Character id for the point-of-view/source side of the relationship."),
            ["targetCharacterId"] = StringField("Character id for the target/other side of the relationship."),
            ["howSourceSeesTarget"] = StringField("How the source character perceives, feels about, or treats the target character."),
            ["howTargetSeesSource"] = StringField("How the target character perceives, feels about, or treats the source character."),
            ["publicDynamic"] = StringField("How others would summarize the visible dynamic between these characters."),
            ["privateTensions"] = ControlledStringArray("Controlled relationship dynamic values. Call get_character_profile_options with fields ['privateTensions'] before setting."),
            ["relationshipTypes"] = ControlledStringArray("Controlled bond type values. Call get_character_profile_options with fields ['relationshipTypes'] before setting."),
            ["reason"] = new JsonObject { ["type"] = "string" }
        },
        ["required"] = new JsonArray(CharacterRelationshipRules.RequiredPatchFields.Select(field => (JsonNode?)JsonValue.Create(field)).ToArray()),
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
            appearanceFields = AppearanceFields,
            appearancePolicy = AppearancePolicy,
            instruction = "Call get_character_profile_options with specific fields before setting controlled profile ids. For appearance, use the flat appearance fields to build a complete visual profile."
        };
    }

    public static object ProfileOptions(CharacterTraitLibraryState library, IReadOnlyList<string> requestedFields)
    {
        var normalized = CharacterTraitLibraryService.NormalizeState(library);
        var fields = ExpandRequestedFields(requestedFields.Count == 0 ? ControlledCharacterFields : requestedFields);
        return new
        {
            limits = Limits(),
            appearanceFields = AppearanceFields,
            appearancePolicy = AppearancePolicy,
            fields = fields.Distinct(StringComparer.Ordinal).ToDictionary(field => field, field => OptionPayload(normalized, field), StringComparer.Ordinal)
        };
    }

    static string AppearancePolicy =>
        "When creating or updating appearance, use the flat appearance fields together to make a complete visual profile: hair, eyes, face, skin, height, build, body proportions, presentation, and attractiveness. extraAppearanceDetails adds distinctive visible specifics such as scars, tattoos, birthmarks, prosthetics, signature clothing, or other details.";

    static IReadOnlyList<string> ExpandRequestedFields(IReadOnlyList<string> requestedFields)
    {
        if (!requestedFields.Any(IsAppearanceField))
            return requestedFields;

        return requestedFields
            .Where(field => !IsAppearanceField(field))
            .Concat(AppearanceFields)
            .ToList();
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
        ValidateLegacyAppearanceFields(updates);
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
        ValidateAppearancePatch(updates, normalized);
        ValidateExtraAppearanceDetails(updates);
    }

    public static void ValidateCreatedCharacter(RpCharacter character)
    {
        var missing = RequiredCreateFields(character).ToList();
        if (missing.Count > 0)
            throw CharacterProfileValidationException.ForFields(missing, $"Creating a character needs a fuller profile. Add {JoinFieldList(missing)} so the character is immediately usable.");
    }

    public static void ValidateCompleteAppearance(RpCharacter character)
    {
        var missing = MissingAppearanceFields(character.AppearanceProfile).ToList();
        if (missing.Count > 0)
            throw CharacterProfileValidationException.ForFields(missing, $"Updating appearance needs a complete visual profile. Add {JoinFieldList(missing)}.");
    }

    public static bool HasAppearancePatch(JsonElement updates) =>
        updates.ValueKind == JsonValueKind.Object
        && updates.EnumerateObject().Any(property => IsAppearanceField(property.Name) || property.Name == ExtraAppearanceDetailsField);

    public static bool IsAppearanceField(string field) =>
        AppearanceFields.Contains(field, StringComparer.Ordinal);

    public static void ValidateRelationshipPatch(JsonElement root, CharacterTraitLibraryState library)
    {
        var normalized = CharacterTraitLibraryService.NormalizeState(library);
        var missing = CharacterRelationshipRules.MissingPatchFields(root);
        if (missing.Count > 0)
            throw CharacterProfileValidationException.ForFields(missing, $"Updating a character relationship needs every relationship field populated. Add {JoinFieldList(missing)}.");

        ValidateStringArrayValue(root, "relationshipTypes", "relationship types", normalized.BondTypes);
        ValidateStringArrayValue(root, "privateTensions", "relationship dynamics", normalized.Dynamics);
    }

    static JsonObject StringField(string description) => new()
    {
        ["type"] = "string",
        ["description"] = description
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

    static void ValidateStringArrayValue(JsonElement root, string field, string label, IReadOnlyList<string> values)
    {
        if (!root.TryGetProperty(field, out var value))
            return;

        if (value.ValueKind != JsonValueKind.Array)
            throw CharacterProfileValidationException.ForField(field, $"The relationship patch failed because {label} must be an array of strings.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw CharacterProfileValidationException.ForField(field, $"The relationship patch failed because {label} must be an array of strings.");

            var text = item.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (!seen.Add(text))
                throw CharacterProfileValidationException.ForField(field, $"The relationship patch failed because {label} contains duplicate value '{text}'.");

            if (!values.Contains(text, StringComparer.Ordinal))
                throw CharacterProfileValidationException.ForField(field, $"The relationship patch failed because {label} contains invalid value '{text}'. Controlled relationship fields must use valid values.");
        }
    }

    static void ValidateAppearancePatch(JsonElement root, CharacterTraitLibraryState library)
    {
        ValidateOption(root, "hairColor", "hair color", library.HairColors);
        ValidateOptionArray(root, "hairStyles", "hair styles", library.HairStyles, MaxHairStyles);
        ValidateOption(root, "eyeColor", "eye color", library.EyeColors);
        ValidateOption(root, "faceShape", "face shape", library.FaceShapes);
        ValidateOption(root, "skinTone", "skin tone", library.SkinTones);
        ValidateOptionArray(root, "complexion", "complexion", library.Complexions, MaxComplexion);
        ValidateOption(root, "height", "height", library.Heights);
        ValidateOption(root, "build", "build", library.Builds);
        ValidateOptionArray(root, "bodyProportions", "body proportions", library.BodyProportions, MaxBodyProportions);
        ValidateOptionArray(root, "presentation", "presentation", library.Presentations, MaxPresentation);
        ValidateOption(root, "attractiveness", "attractiveness", library.AttractivenessLevels);
    }

    static void ValidateLegacyAppearanceFields(JsonElement root)
    {
        if (root.TryGetProperty("appearance", out _))
            throw CharacterProfileValidationException.ForField(ExtraAppearanceDetailsField, $"The character patch failed because appearance is not a supported Story Assistant field. Use {ExtraAppearanceDetailsField} for extra visible details.");

        if (root.TryGetProperty("appearanceProfile", out _))
            throw CharacterProfileValidationException.ForFields(AppearanceFields, "The character patch failed because appearanceProfile is no longer used by Story Assistant. Set the flat appearance fields instead.");
    }

    static void ValidateExtraAppearanceDetails(JsonElement root)
    {
        if (!root.TryGetProperty(ExtraAppearanceDetailsField, out var value))
            return;

        if (value.ValueKind != JsonValueKind.String)
            throw CharacterProfileValidationException.ForField(ExtraAppearanceDetailsField, $"The character patch failed because {ExtraAppearanceDetailsField} must be a string.");

    }

    static IEnumerable<string> RequiredCreateFields(RpCharacter character)
    {
        if (string.IsNullOrWhiteSpace(character.Name) || string.Equals(character.Name, "New Character", StringComparison.Ordinal))
            yield return "name";
        if (string.IsNullOrWhiteSpace(character.Summary))
            yield return "summary";
        if (string.IsNullOrWhiteSpace(character.Personality))
            yield return "personality";
        if (string.IsNullOrWhiteSpace(character.Voice))
            yield return "voice";
        if (!HasStoryProfileAnchor(character))
            yield return "traits";

        foreach (var field in MissingAppearanceFields(character.AppearanceProfile))
            yield return field;
    }

    static bool HasStoryProfileAnchor(RpCharacter character) =>
        character.SceneRoles.Count > 0
        || character.Traits.Count > 0
        || !string.IsNullOrWhiteSpace(character.CoreDrive)
        || !string.IsNullOrWhiteSpace(character.CoreFear)
        || !string.IsNullOrWhiteSpace(character.Backstory)
        || !string.IsNullOrWhiteSpace(character.HiddenTruth);

    static IEnumerable<string> MissingAppearanceFields(CharacterAppearanceState appearance)
    {
        if (string.IsNullOrWhiteSpace(appearance.HairColor))
            yield return "hairColor";
        if (appearance.HairStyles.Count == 0)
            yield return "hairStyles";
        if (string.IsNullOrWhiteSpace(appearance.EyeColor))
            yield return "eyeColor";
        if (string.IsNullOrWhiteSpace(appearance.FaceShape))
            yield return "faceShape";
        if (string.IsNullOrWhiteSpace(appearance.SkinTone))
            yield return "skinTone";
        if (appearance.Complexion.Count == 0)
            yield return "complexion";
        if (string.IsNullOrWhiteSpace(appearance.Height))
            yield return "height";
        if (string.IsNullOrWhiteSpace(appearance.Build))
            yield return "build";
        if (appearance.BodyProportions.Count == 0)
            yield return "bodyProportions";
        if (appearance.Presentation.Count == 0)
            yield return "presentation";
        if (string.IsNullOrWhiteSpace(appearance.Attractiveness))
            yield return "attractiveness";
    }

    static string JoinFieldList(IReadOnlyList<string> fields) =>
        string.Join(", ", fields.Select(LabelForField));

    static string LabelForField(string field) => field switch
    {
        ExtraAppearanceDetailsField => "extra appearance details",
        "hairColor" => "hair color",
        "hairStyles" => "hair styles",
        "eyeColor" => "eye color",
        "faceShape" => "face shape",
        "skinTone" => "skin tone",
        "bodyProportions" => "body proportions",
        "sceneRoles" => "scene roles",
        "coreDrive" => "core drive",
        "coreFear" => "core fear",
        _ => string.Concat(field.Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {char.ToLowerInvariant(ch)}" : ch.ToString()))
    };

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

    static JsonObject ControlledStringArray(string description) => new()
    {
        ["type"] = "array",
        ["description"] = description,
        ["uniqueItems"] = true,
        ["items"] = new JsonObject { ["type"] = "string" }
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
        "hairColor" => new { options = Options(library.HairColors), allowsEmpty = true, instruction = AppearancePolicy },
        "hairStyles" => new { maxItems = MaxHairStyles, options = Options(library.HairStyles), instruction = AppearancePolicy },
        "eyeColor" => new { options = Options(library.EyeColors), allowsEmpty = true, instruction = AppearancePolicy },
        "faceShape" => new { options = Options(library.FaceShapes), allowsEmpty = true, instruction = AppearancePolicy },
        "skinTone" => new { options = Options(library.SkinTones), allowsEmpty = true, instruction = AppearancePolicy },
        "complexion" => new { maxItems = MaxComplexion, options = Options(library.Complexions), instruction = AppearancePolicy },
        "height" => new { options = Options(library.Heights), allowsEmpty = true, instruction = AppearancePolicy },
        "build" => new { options = Options(library.Builds), allowsEmpty = true, instruction = AppearancePolicy },
        "bodyProportions" => new { maxItems = MaxBodyProportions, options = Options(library.BodyProportions), instruction = AppearancePolicy },
        "presentation" => new { maxItems = MaxPresentation, options = Options(library.Presentations), instruction = AppearancePolicy },
        "attractiveness" => new { options = Options(library.AttractivenessLevels), allowsEmpty = true, instruction = AppearancePolicy },
        "relationshipTypes" => new { options = library.BondTypes },
        "privateTensions" => new { options = library.Dynamics },
        _ => throw CharacterProfileValidationException.ForField(field, $"Reading character profile options failed because '{field}' is not a supported controlled profile field.")
    };
}

public sealed class CharacterProfileValidationException(string message, IReadOnlyList<string> fields) : InvalidOperationException(message)
{
    public IReadOnlyList<string> Fields { get; } = fields;

    public static CharacterProfileValidationException ForField(string field, string message) => new(message, [NormalizeField(field)]);
    public static CharacterProfileValidationException ForFields(IEnumerable<string> fields, string message) => new(message, fields.Select(NormalizeField).Distinct(StringComparer.Ordinal).ToList());

    static string NormalizeField(string field) => field switch
    {
        "scene roles" => "sceneRoles",
        "pronouns" => "pronouns",
        "traits" => "traits",
        "soft spots" => "softSpots",
        "avoid patterns" => "avoidPatterns",
        "appearance profile" => "appearanceProfile",
        "relationship type" => "relationshipTypes",
        "relationship types" => "relationshipTypes",
        "relationship dynamic" => "privateTensions",
        "relationship dynamics" => "privateTensions",
        _ => field
    };
}
