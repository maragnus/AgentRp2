namespace AgentRp.Models;

public sealed class RpCharacter
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool InScene { get; set; }
    public string Summary { get; set; } = "";
    public string Personality { get; set; } = "";
    public string Appearance { get; set; } = "";
    public string Relationships { get; set; } = "";
    public string Backstory { get; set; } = "";
    public string Voice { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Version { get; set; } = "v1";
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
    public List<RpRelationship> V2Relationships { get; set; } = [];
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

public sealed class RpProcessStep
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public int TokensIn { get; set; }
    public int TokensOut { get; set; }
    public int TotalTokens { get; set; }
    public string Duration { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public string UserPrompt { get; set; } = "";
    public string Output { get; set; } = "";
}

public sealed class RpMessage
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Status { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public string Author { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Body { get; set; } = "";
    public string Branch { get; set; } = "";
    public int CharacterCount { get; set; }
    public List<RpProcessStep> Steps { get; set; } = [];
}

public sealed class GalleryImage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Entity { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string Date { get; set; } = "";
    public int Hue { get; set; }
}

public sealed class AiProvider
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public List<AiProviderModel> Models { get; set; } = [];
}

public sealed class AiProviderModel
{
    public string Id { get; set; } = "";
    public bool Enabled { get; set; }
    public bool Text { get; set; }
    public bool Image { get; set; }
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
