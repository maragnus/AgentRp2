using AgentRp.Services;

namespace AgentRp.Models;

public sealed class RpCharacter
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageId { get; set; } = "";
    public bool InScene { get; set; }
    public string Summary { get; set; } = "";
    public string Personality { get; set; } = "";
    public string Appearance { get; set; } = "";
    public string Relationships { get; set; } = "";
    public string Backstory { get; set; } = "";
    public string Voice { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<string> SceneRoles { get; set; } = [];
    public List<string> Traits { get; set; } = [];
    public List<string> Drives { get; set; } = [];
    public List<string> Limits { get; set; } = [];
    public string CoreDrive { get; set; } = "";
    public string CoreFear { get; set; } = "";
    public string SurfaceMask { get; set; } = "";
    public string HiddenTruth { get; set; } = "";
    public string SentenceStyle { get; set; } = "";
    public string HonestyStyle { get; set; } = "";
    public string EmotionalLeakage { get; set; } = "";
    public string ActionFingerprint { get; set; } = "";
    public string StressPattern { get; set; } = "";
    public List<string> SoftSpots { get; set; } = [];
    public List<string> AvoidPatterns { get; set; } = [];
    public List<RpRelationship> ProfileRelationships { get; set; } = [];
}

public sealed class RpRelationship
{
    public string CharacterId { get; set; } = "";
    public List<string> Bonds { get; set; } = [];
    public List<string> Dynamics { get; set; } = [];
    public string NoteAtoB { get; set; } = "";
    public string NoteBtoA { get; set; } = "";
    public string NoteExternal { get; set; } = "";
}

public sealed class RpLocation
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageId { get; set; } = "";
    public bool IsActive { get; set; }
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public string Atmosphere { get; set; } = "";
    public string Features { get; set; } = "";
}

public sealed class RpItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageId { get; set; } = "";
    public bool InScene { get; set; }
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public string History { get; set; } = "";
    public string Properties { get; set; } = "";
}

public sealed class RpTimelineEntry
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Date { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Characters { get; set; } = [];
    public string Significance { get; set; } = "";
}

public sealed class RpChat
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Updated { get; set; } = "";
    public bool Starred { get; set; }
    public int Messages { get; set; }
    public string Location { get; set; } = "";
}

public sealed class GalleryImage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Entity { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string Date { get; set; } = "";
    public int Hue { get; set; }
    public string Url { get; set; } = "";
}

public enum ImageGalleryMode
{
    View,
    Select
}

public sealed class AiProvider
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string ManagementApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string TeamId { get; set; } = "";
    public DateTime? LastMetricsRefreshUtc { get; set; }
    public string LastMetricsError { get; set; } = "";
    public List<AiProviderModel> Models { get; set; } = [];
    public List<AiProviderMetric> Metrics { get; set; } = [];
}

public sealed class AiProviderModel
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string Repository { get; set; } = "";
    public long? CreatedUnix { get; set; }
    public bool Enabled { get; set; }
    public bool Text { get; set; }
    public bool Image { get; set; }
    public bool ActiveText { get; set; }
    public ModelGenerationCapabilities Capabilities { get; set; } = ModelGenerationCapabilities.Fallback;
}

public sealed class AiProviderMetric
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime RefreshedUtc { get; set; }
}

public sealed class AiProviderMeta
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string KeyLabel { get; set; } = "";
    public string? KeyLink { get; set; }
    public bool NeedsEndpoint { get; set; }
    public bool ApiKeyRequired { get; set; }
    public bool EndpointRequired { get; set; }
    public List<AiProviderModel> SampleModels { get; set; } = [];
}

public sealed record TaxonomyGroup(string Name, string Color, IReadOnlyList<string> Values);
