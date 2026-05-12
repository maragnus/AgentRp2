using System.Text.Json;
using AgentRp.Serialization;

namespace AgentRp.Components.Common;

public static class StatefulFormSnapshot
{
    public static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, AppJsonSerializerOptions.Web);
        return JsonSerializer.Deserialize<T>(json, AppJsonSerializerOptions.Web)!;
    }

    public static bool Equivalent<T>(T current, T baseline) =>
        JsonSerializer.Serialize(current, AppJsonSerializerOptions.Web) == JsonSerializer.Serialize(baseline, AppJsonSerializerOptions.Web);
}
