using AgentRp.Services;

namespace AgentRp.Models;

public sealed class RpCharacter
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
    public string ImageId { get; set; } = "";
    public bool InScene { get; set; }
    public string Summary { get; set; } = "";
    public string Personality { get; set; } = "";
    public string Appearance { get; set; } = "";
    public CharacterAppearanceState AppearanceProfile { get; set; } = new();
    public string Backstory { get; set; } = "";
    public string Voice { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<string> Pronouns { get; set; } = [];
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
    public Dictionary<string, CharacterVoiceSelection> VoiceSelections { get; set; } = [];
}

public sealed class CharacterAppearanceState
{
    public string HairColor { get; set; } = "";
    public List<string> HairStyles { get; set; } = [];
    public string EyeColor { get; set; } = "";
    public string FaceShape { get; set; } = "";
    public string SkinTone { get; set; } = "";
    public List<string> Complexion { get; set; } = [];
    public string Height { get; set; } = "";
    public string Build { get; set; } = "";
    public List<string> BodyProportions { get; set; } = [];
    public List<string> Presentation { get; set; } = [];
    public string Attractiveness { get; set; } = "";
}

public sealed class CharacterVoiceSelection
{
    public string VoiceId { get; set; } = "";
    public string VoiceName { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
}

public sealed class RpCharacterRelationship
{
    public string Id { get; set; } = "";
    public string CharacterAId { get; set; } = "";
    public string CharacterBId { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
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
    public DateTime UpdatedUtc { get; set; }
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
    public DateTime UpdatedUtc { get; set; }
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
    public string SnapshotId { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
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
    public DateTime? LastMessageUtc { get; set; }
    public int LastGeneratedTurnNumber { get; set; }
    public bool Starred { get; set; }
    public int Messages { get; set; }
    public string Location { get; set; } = "";
    public RpChatSceneLocation? ActiveLocation { get; set; }
    public List<RpChatSceneCharacter> SceneCharacters { get; set; } = [];
}

public sealed class RpChatSceneLocation
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageId { get; set; } = "";
    public GalleryImage? Image { get; set; }
}

public sealed class RpChatSceneCharacter
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageId { get; set; } = "";
    public GalleryImage? Image { get; set; }
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
    public int AvatarFocusXPercent { get; set; } = 50;
    public int AvatarFocusYPercent { get; set; } = 50;
    public int AvatarZoomPercent { get; set; } = 100;
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
    public HashSet<AiModelRole> Roles { get; set; } = [];
    public DateTime? LastVoiceRefreshUtc { get; set; }
    public string LastVoiceRefreshError { get; set; } = "";
    public List<AiProviderVoice> Voices { get; set; } = [];
    public ModelGenerationCapabilities Capabilities { get; set; } = ModelGenerationCapabilities.Fallback;
}

public enum AiModelRole
{
    Chat,
    Reasoning,
    Image,
    Voice
}

public sealed class AiProviderVoice
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string PreviewUrl { get; set; } = "";
    public Dictionary<string, string> Labels { get; set; } = [];
    public string Source { get; set; } = "";
    public bool IsCatalogVoice { get; set; }
    public bool IsBookmarked { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime UpdatedUtc { get; set; }
}

public sealed class ElevenLabsVoiceCatalogEntry
{
    public string VoiceId { get; set; } = "";
    public string PublicOwnerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string PreviewUrl { get; set; } = "";
    public bool Featured { get; set; }
    public string Accent { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Age { get; set; } = "";
    public string UseCase { get; set; } = "";
    public string Category { get; set; } = "";
    public string Language { get; set; } = "";
    public string Locale { get; set; } = "";
    public string Descriptive { get; set; } = "";
    public bool IsBookmarked { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime? LastSeenUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed record ElevenLabsVoiceCatalogFilter(
    string View,
    string Search,
    bool FeaturedOnly,
    string Accent,
    string Gender,
    string Age,
    string UseCase,
    string Category)
{
    public static ElevenLabsVoiceCatalogFilter SearchAll { get; } = new("search", "", false, "", "", "", "", "");
    public static ElevenLabsVoiceCatalogFilter Bookmarked { get; } = new("bookmarked", "", false, "", "", "", "", "");
}

public sealed record ElevenLabsVoiceCatalogSnapshot(
    IReadOnlyList<ElevenLabsVoiceCatalogEntry> Voices,
    IReadOnlyList<string> Accents,
    IReadOnlyList<string> Genders,
    IReadOnlyList<string> Ages,
    IReadOnlyList<string> UseCases,
    IReadOnlyList<string> Categories,
    DateTime? LastRefreshUtc,
    string LastRefreshError,
    int TotalCount,
    int CachedCount);

public sealed record ElevenLabsVoiceCatalogRefreshProgress(
    int CurrentPage,
    int? TotalPages,
    int VoiceCount,
    int? TotalCount,
    string Stage);

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
