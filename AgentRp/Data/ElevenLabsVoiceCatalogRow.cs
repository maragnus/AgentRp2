namespace AgentRp.Data;

public sealed class ElevenLabsVoiceCatalogRow
{
    public string VoiceId { get; set; } = "";
    public string PublicOwnerId { get; set; } = "";
    public long? DateUnix { get; set; }
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
    public string VerifiedLanguagesJson { get; set; } = "[]";
    public bool IsBookmarked { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public string RawJson { get; set; } = "";
}

public sealed class ElevenLabsVoiceCatalogStateRow
{
    public string Id { get; set; } = "";
    public DateTime? LastRefreshUtc { get; set; }
    public string LastRefreshError { get; set; } = "";
    public int TotalCount { get; set; }
    public int CachedCount { get; set; }
}
