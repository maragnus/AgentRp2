using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public static class CharacterAppearanceFormatter
{
    public static string FormatBase(RpCharacter character, CharacterTraitLibraryState library) =>
        FormatBase(character.AppearanceProfile, character.Appearance, library);

    public static string FormatBase(CharacterAppearanceState appearance, string customText, CharacterTraitLibraryState library)
    {
        var normalized = CharacterTraitLibraryService.NormalizeState(library);
        var parts = new List<string>();

        Add(parts, FormatHair(appearance, normalized));
        Add(parts, FormatEyes(appearance, normalized));
        Add(parts, FormatFace(appearance, normalized));
        Add(parts, FormatSkin(appearance, normalized));
        Add(parts, FormatHeightBuild(appearance, normalized));
        Add(parts, FormatMulti(appearance.BodyProportions, normalized.BodyProportions));
        Add(parts, FormatPresentation(appearance, normalized));
        Add(parts, Label(appearance.Attractiveness, normalized.AttractivenessLevels).ToLowerInvariant());
        Add(parts, customText.Trim());

        return string.Join("; ", parts);
    }

    public static string FormatWithSceneState(RpCharacter character, CharacterTraitLibraryState library, string currentAppearance)
    {
        var baseAppearance = FormatBase(character, library);
        var current = currentAppearance.Trim();
        if (string.IsNullOrWhiteSpace(current))
            return baseAppearance;

        if (string.IsNullOrWhiteSpace(baseAppearance))
            return current;

        return $"{baseAppearance}. Current: {current}";
    }

    public static bool HasStructuredAppearance(CharacterAppearanceState appearance) =>
        !string.IsNullOrWhiteSpace(appearance.HairColor)
        || appearance.HairStyles.Count > 0
        || !string.IsNullOrWhiteSpace(appearance.EyeColor)
        || !string.IsNullOrWhiteSpace(appearance.FaceShape)
        || !string.IsNullOrWhiteSpace(appearance.SkinTone)
        || appearance.Complexion.Count > 0
        || !string.IsNullOrWhiteSpace(appearance.Height)
        || !string.IsNullOrWhiteSpace(appearance.Build)
        || appearance.BodyProportions.Count > 0
        || appearance.Presentation.Count > 0
        || !string.IsNullOrWhiteSpace(appearance.Attractiveness);

    public static string Label(string id, IReadOnlyList<CharacterOption> options)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "";

        return options.FirstOrDefault(option => string.Equals(option.Id, id, StringComparison.Ordinal))?.Label
            ?? id.Replace('-', ' ');
    }

    public static IReadOnlyList<string> Labels(IEnumerable<string> ids, IReadOnlyList<CharacterOption> options) =>
        ids.Select(id => Label(id, options))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

    static string FormatHair(CharacterAppearanceState appearance, CharacterTraitLibraryState library)
    {
        var styles = Labels(appearance.HairStyles, library.HairStyles)
            .Select(value => value.ToLowerInvariant())
            .ToList();
        var color = Label(appearance.HairColor, library.HairColors).ToLowerInvariant();
        if (styles.Contains("bald", StringComparer.OrdinalIgnoreCase))
            return "bald";

        var parts = styles;
        if (!string.IsNullOrWhiteSpace(color))
            parts.Add(color);

        return parts.Count == 0 ? "" : $"{string.Join(" ", parts)} hair";
    }

    static string FormatEyes(CharacterAppearanceState appearance, CharacterTraitLibraryState library)
    {
        var color = Label(appearance.EyeColor, library.EyeColors).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(color) ? "" : $"{color} eyes";
    }

    static string FormatFace(CharacterAppearanceState appearance, CharacterTraitLibraryState library)
    {
        var face = Label(appearance.FaceShape, library.FaceShapes).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(face) ? "" : $"{face} face";
    }

    static string FormatSkin(CharacterAppearanceState appearance, CharacterTraitLibraryState library)
    {
        var tone = Label(appearance.SkinTone, library.SkinTones).ToLowerInvariant();
        var complexion = Labels(appearance.Complexion, library.Complexions)
            .Select(value => value.ToLowerInvariant())
            .ToList();
        if (string.IsNullOrWhiteSpace(tone))
            return complexion.Count == 0 ? "" : $"{string.Join(", ", complexion)} complexion";

        complexion.Add(tone);
        return $"{string.Join(", ", complexion)} skin";
    }

    static string FormatHeightBuild(CharacterAppearanceState appearance, CharacterTraitLibraryState library)
    {
        var height = Label(appearance.Height, library.Heights).ToLowerInvariant();
        var build = Label(appearance.Build, library.Builds).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(height))
            return string.IsNullOrWhiteSpace(build) ? "" : $"{build} build";

        if (string.IsNullOrWhiteSpace(build))
            return $"{height} height";

        if (height == "average" && build == "average")
            return "average height and build";

        return $"{height}, {build} build";
    }

    static string FormatPresentation(CharacterAppearanceState appearance, CharacterTraitLibraryState library)
    {
        var labels = Labels(appearance.Presentation, library.Presentations)
            .Select(value => value.ToLowerInvariant())
            .ToList();
        return labels.Count == 0 ? "" : $"{string.Join(", ", labels)} bearing";
    }

    static string FormatMulti(IReadOnlyList<string> ids, IReadOnlyList<CharacterOption> options) =>
        string.Join(", ", Labels(ids, options).Select(value => value.ToLowerInvariant()));

    static void Add(List<string> parts, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add(value.Trim());
    }
}
