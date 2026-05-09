namespace AgentRp.Data;

public sealed class AppSettingRow
{
    public string Key { get; set; } = "";
    public string JsonValue { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
