using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentRp.Serialization;

public static class AppJsonSerializerOptions
{
    public static readonly JsonSerializerOptions Web = Create(writeIndented: false);
    public static readonly JsonSerializerOptions IndentedWeb = Create(writeIndented: true);

    static JsonSerializerOptions Create(bool writeIndented)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = writeIndented
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
