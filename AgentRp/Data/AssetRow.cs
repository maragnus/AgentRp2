namespace AgentRp.Data;

public sealed class ImageAssetRow
{
    public string Id { get; set; } = "";
    public string ChatId { get; set; } = "";
    public string BlobName { get; set; } = "";
    public string StoredContentType { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string OriginalContentType { get; set; } = "";
    public long OriginalByteLength { get; set; }
    public long StoredByteLength { get; set; }
    public bool OptimizationAttempted { get; set; }
    public bool OptimizationSucceeded { get; set; }
    public string OptimizationProvider { get; set; } = "";
    public string OptimizationError { get; set; } = "";
    public DateTime? OptimizedUtc { get; set; }
    public string Title { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? AvatarFocusXPercent { get; set; }
    public int? AvatarFocusYPercent { get; set; }
    public int? AvatarZoomPercent { get; set; }
    public string UserPrompt { get; set; } = "";
    public string FinalPrompt { get; set; } = "";
    public string GenerationMetadataJson { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string ProviderModelId { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public sealed class SpeechAssetRow
{
    public string Id { get; set; } = "";
    public string ChatId { get; set; } = "";
    public string TurnId { get; set; } = "";
    public byte[] Bytes { get; set; } = [];
    public string Status { get; set; } = SpeechAssetStatus.Pending;
    public string ContentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string ProviderType { get; set; } = "";
    public string ProviderModelId { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string InputsJson { get; set; } = "[]";
    public string VoiceIdsJson { get; set; } = "{}";
    public string ErrorMessage { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}

public static class SpeechAssetStatus
{
    public const string Pending = "Pending";
    public const string Streaming = "Streaming";
    public const string Ready = "Ready";
    public const string Failed = "Failed";
}
