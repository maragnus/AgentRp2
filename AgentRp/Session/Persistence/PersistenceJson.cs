using System.Text.Json;
using AgentRp.Serialization;

namespace AgentRp.Session;

internal static class PersistenceJson
{
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, AppJsonSerializerOptions.Web);

    public static T Deserialize<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;

        try
        {
            return JsonSerializer.Deserialize<T>(json, AppJsonSerializerOptions.Web) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
