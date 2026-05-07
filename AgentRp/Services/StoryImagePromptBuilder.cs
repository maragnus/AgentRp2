namespace AgentRp.Services;

public static class StoryImagePromptBuilder
{
    public const string SquareSize = "1024x1024";
    public const string LandscapeSize = "1536x1024";
    public const string PortraitSize = "1024x1536";

    public static string BuildReferencePrompt(string? entityType, string? entityName)
    {
        var normalizedType = NormalizeEntityType(entityType);
        var displayName = string.IsNullOrWhiteSpace(entityName)
            ? DisplayEntityType(normalizedType)
            : entityName.Trim();
        var subject = normalizedType switch
        {
            "character" => "profile image",
            "location" => "scene",
            "item" => "image",
            _ => "image"
        };

        return $"Create a vivid roleplaying reference {subject} for {displayName}.";
    }

    public static string BuildReferenceSize(string? entityType) => NormalizeEntityType(entityType) switch
    {
        "character" => PortraitSize,
        "location" => LandscapeSize,
        "item" => LandscapeSize,
        _ => SquareSize
    };

    public static string NormalizeEntityType(string? entityType) => entityType?.Trim().ToLowerInvariant() switch
    {
        "characters" => "character",
        "character" => "character",
        "locations" => "location",
        "location" => "location",
        "items" => "item",
        "item" => "item",
        _ => ""
    };

    static string DisplayEntityType(string normalizedType) => normalizedType switch
    {
        "character" => "Character",
        "location" => "Location",
        "item" => "Item",
        _ => "Story image"
    };
}
