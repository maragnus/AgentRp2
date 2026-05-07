namespace AgentRp.Services;

public sealed class ImageAssetGenerationMetadata
{
    public string Size { get; init; } = "";
    public string Quality { get; init; } = "";
    public string ReferenceDetail { get; init; } = "";
    public string ArtStyleKey { get; init; } = "";
    public string ArtStyleLabel { get; init; } = "";
    public string RevisedPrompt { get; init; } = "";
    public string ResponseId { get; init; } = "";
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public List<ImageAssetReferenceMetadata> References { get; init; } = [];
}

public sealed class ImageAssetReferenceMetadata
{
    public string Kind { get; init; } = "";
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string EntityType { get; init; } = "";
    public string ImageUrl { get; init; } = "";
}
