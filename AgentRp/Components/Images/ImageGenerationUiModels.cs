using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Components.Images;

public sealed record ImageEntityOption(
    string Key,
    string EntityType,
    string EntityId,
    string Name,
    string ImageId,
    GalleryImage? Image)
{
    public StoryImageEntitySelection ToSelection() => new(EntityType, EntityId);
}

public sealed class GeneratedImageOption
{
    public string Id { get; init; } = "";
    public GalleryImage Image { get; init; } = new();
    public string ProviderName { get; init; } = "";
    public string ModelId { get; init; } = "";
    public string FinalPrompt { get; init; } = "";
    public string Rationale { get; init; } = "";
    public string? SavedImageId { get; set; }
    public bool IsSaved => !string.IsNullOrWhiteSpace(SavedImageId);
}
