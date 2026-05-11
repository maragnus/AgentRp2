using AgentRp.Models;

namespace AgentRp.Services;

public static class TimelineEntityLinkResolver
{
    public static List<string> ResolveCharacterIds(IReadOnlyList<RpCharacter> characters, IEnumerable<string>? values) =>
        ResolveIds(characters.Select(character => (character.Id, character.Name)), values);

    public static List<string> ResolveLocationIds(IReadOnlyList<RpLocation> locations, IEnumerable<string>? values) =>
        ResolveIds(locations.Select(location => (location.Id, location.Name)), values);

    public static List<string> ResolveIds(IEnumerable<(string Id, string Name)> entities, IEnumerable<string>? values)
    {
        if (values is null)
            return [];

        var entityList = entities.ToList();
        var resolved = new List<string>();
        foreach (var value in values)
        {
            var id = ResolveId(entityList, value);
            if (!string.IsNullOrWhiteSpace(id) && !resolved.Contains(id, StringComparer.Ordinal))
                resolved.Add(id);
        }

        return resolved;
    }

    public static string ResolveId(IReadOnlyList<(string Id, string Name)> entities, string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return "";

        var match = entities.FirstOrDefault(entity =>
            string.Equals(entity.Id, trimmed, StringComparison.Ordinal)
            || string.Equals(entity.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        return match.Id ?? "";
    }
}
