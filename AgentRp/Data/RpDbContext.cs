using Microsoft.EntityFrameworkCore;

namespace AgentRp.Data;

public sealed class RpDbContext(DbContextOptions<RpDbContext> options) : DbContext(options)
{
    public DbSet<RpChatRow> Chats => Set<RpChatRow>();
    public DbSet<RpChatDocumentRow> ChatDocuments => Set<RpChatDocumentRow>();
    public DbSet<AiProviderRow> AiProviders => Set<AiProviderRow>();
    public DbSet<AiProviderModelRow> AiProviderModels => Set<AiProviderModelRow>();
    public DbSet<AiProviderMetricRow> AiProviderMetrics => Set<AiProviderMetricRow>();
    public DbSet<ElevenLabsVoiceCatalogRow> ElevenLabsVoiceCatalog => Set<ElevenLabsVoiceCatalogRow>();
    public DbSet<ElevenLabsVoiceCatalogStateRow> ElevenLabsVoiceCatalogStates => Set<ElevenLabsVoiceCatalogStateRow>();
    public DbSet<ImageAssetRow> ImageAssets => Set<ImageAssetRow>();
    public DbSet<SpeechAssetRow> SpeechAssets => Set<SpeechAssetRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RpChatRow>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.Title).HasMaxLength(500);
            builder.Property(x => x.Updated).HasMaxLength(100);
            builder.Property(x => x.Location).HasMaxLength(500);
            builder.HasIndex(x => x.SortOrder);
            builder.HasIndex(x => x.UpdatedUtc);
            builder.HasOne(x => x.Document)
                .WithOne(x => x.Chat)
                .HasForeignKey<RpChatDocumentRow>(x => x.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RpChatDocumentRow>(builder =>
        {
            builder.HasKey(x => x.ChatId);
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.CharactersJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.LocationsJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ItemsJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.TimelineJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ImagesJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.MessagesJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.StoryAssistantJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.NarratorProfileJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.PromptLibraryJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.CharacterTraitLibraryJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ModelTuningJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ActiveModelSelectionsJson).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<AiProviderRow>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.Name).HasMaxLength(200);
            builder.Property(x => x.Type).HasMaxLength(100);
            builder.Property(x => x.ApiKey).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ManagementApiKey).HasColumnType("nvarchar(max)");
            builder.Property(x => x.Endpoint).HasMaxLength(1000);
            builder.Property(x => x.AccountId).HasMaxLength(200);
            builder.Property(x => x.ProjectId).HasMaxLength(200);
            builder.Property(x => x.TeamId).HasMaxLength(200);
            builder.Property(x => x.LastMetricsError).HasMaxLength(1000);
            builder.HasIndex(x => x.SortOrder);
            builder.HasMany(x => x.Models)
                .WithOne(x => x.Provider)
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.Metrics)
                .WithOne(x => x.Provider)
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiProviderModelRow>(builder =>
        {
            builder.HasKey(x => new { x.ProviderId, x.Id });
            builder.Property(x => x.ProviderId).HasMaxLength(80);
            builder.Property(x => x.Id).HasMaxLength(500);
            builder.Property(x => x.DisplayName).HasMaxLength(500);
            builder.Property(x => x.Endpoint).HasMaxLength(1000);
            builder.Property(x => x.Repository).HasMaxLength(500);
            builder.Property(x => x.RolesJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.VoicesJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.LastVoiceRefreshError).HasMaxLength(1000);
            builder.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<AiProviderMetricRow>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.ProviderId).HasMaxLength(80);
            builder.Property(x => x.Kind).HasMaxLength(100);
            builder.Property(x => x.Label).HasMaxLength(200);
            builder.Property(x => x.Value).HasMaxLength(500);
            builder.Property(x => x.Detail).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => new { x.ProviderId, x.Kind });
        });

        modelBuilder.Entity<ElevenLabsVoiceCatalogRow>(builder =>
        {
            builder.HasKey(x => x.VoiceId);
            builder.Property(x => x.VoiceId).HasMaxLength(120);
            builder.Property(x => x.PublicOwnerId).HasMaxLength(200);
            builder.Property(x => x.Name).HasMaxLength(500);
            builder.Property(x => x.Description).HasColumnType("nvarchar(max)");
            builder.Property(x => x.PreviewUrl).HasMaxLength(1000);
            builder.Property(x => x.Accent).HasMaxLength(200);
            builder.Property(x => x.Gender).HasMaxLength(100);
            builder.Property(x => x.Age).HasMaxLength(100);
            builder.Property(x => x.UseCase).HasMaxLength(200);
            builder.Property(x => x.Category).HasMaxLength(100);
            builder.Property(x => x.Language).HasMaxLength(100);
            builder.Property(x => x.Locale).HasMaxLength(100);
            builder.Property(x => x.Descriptive).HasMaxLength(500);
            builder.Property(x => x.VerifiedLanguagesJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.RawJson).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => x.Name);
            builder.HasIndex(x => x.IsBookmarked);
            builder.HasIndex(x => x.IsAvailable);
        });

        modelBuilder.Entity<ElevenLabsVoiceCatalogStateRow>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.LastRefreshError).HasMaxLength(1000);
        });

        modelBuilder.Entity<ImageAssetRow>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.ContentType).HasMaxLength(100);
            builder.Property(x => x.FileName).HasMaxLength(500);
            builder.Property(x => x.Title).HasMaxLength(500);
            builder.Property(x => x.UserPrompt).HasColumnType("nvarchar(max)");
            builder.Property(x => x.FinalPrompt).HasColumnType("nvarchar(max)");
            builder.Property(x => x.GenerationMetadataJson).HasColumnType("nvarchar(max)").HasDefaultValue("");
            builder.Property(x => x.ProviderId).HasMaxLength(80);
            builder.Property(x => x.ProviderName).HasMaxLength(200);
            builder.Property(x => x.ProviderModelId).HasMaxLength(500);
            builder.Property(x => x.Bytes).HasColumnType("varbinary(max)");
            builder.HasIndex(x => new { x.ChatId, x.CreatedUtc });
        });

        modelBuilder.Entity<SpeechAssetRow>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.TurnId).HasMaxLength(80);
            builder.Property(x => x.Status).HasMaxLength(40).HasDefaultValue(SpeechAssetStatus.Pending);
            builder.Property(x => x.ContentType).HasMaxLength(100);
            builder.Property(x => x.FileName).HasMaxLength(500);
            builder.Property(x => x.ProviderId).HasMaxLength(80);
            builder.Property(x => x.ProviderName).HasMaxLength(200);
            builder.Property(x => x.ProviderType).HasMaxLength(100);
            builder.Property(x => x.ProviderModelId).HasMaxLength(500);
            builder.Property(x => x.SourceHash).HasMaxLength(200);
            builder.Property(x => x.InputsJson).HasColumnType("nvarchar(max)").HasDefaultValue("[]");
            builder.Property(x => x.VoiceIdsJson).HasColumnType("nvarchar(max)").HasDefaultValue("{}");
            builder.Property(x => x.ErrorMessage).HasMaxLength(1000).HasDefaultValue("");
            builder.Property(x => x.Bytes).HasColumnType("varbinary(max)");
            builder.HasIndex(x => new { x.ChatId, x.TurnId });
            builder.HasIndex(x => new { x.ChatId, x.CreatedUtc });
        });
    }
}

public sealed class RpChatRow
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Updated { get; set; } = "";
    public bool Starred { get; set; }
    public int Messages { get; set; }
    public string Location { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public RpChatDocumentRow? Document { get; set; }
}

public sealed class RpChatDocumentRow
{
    public string ChatId { get; set; } = "";
    public string CharactersJson { get; set; } = "[]";
    public string LocationsJson { get; set; } = "[]";
    public string ItemsJson { get; set; } = "[]";
    public string TimelineJson { get; set; } = "[]";
    public string ImagesJson { get; set; } = "[]";
    public string MessagesJson { get; set; } = "[]";
    public string StoryAssistantJson { get; set; } = "";
    public string NarratorProfileJson { get; set; } = "";
    public string PromptLibraryJson { get; set; } = "";
    public string CharacterTraitLibraryJson { get; set; } = "";
    public string ModelTuningJson { get; set; } = "";
    public string ActiveModelSelectionsJson { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public RpChatRow Chat { get; set; } = null!;
}

public sealed class AiProviderRow
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
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<AiProviderModelRow> Models { get; set; } = [];
    public List<AiProviderMetricRow> Metrics { get; set; } = [];
}

public sealed class AiProviderModelRow
{
    public string ProviderId { get; set; } = "";
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string Repository { get; set; } = "";
    public long? CreatedUnix { get; set; }
    public bool Enabled { get; set; }
    public string RolesJson { get; set; } = "[]";
    public DateTime? LastVoiceRefreshUtc { get; set; }
    public string LastVoiceRefreshError { get; set; } = "";
    public string VoicesJson { get; set; } = "[]";
    public int SortOrder { get; set; }
    public AiProviderRow Provider { get; set; } = null!;
}

public sealed class AiProviderMetricRow
{
    public string Id { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime RefreshedUtc { get; set; }
    public AiProviderRow Provider { get; set; } = null!;
}

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

public sealed class ImageAssetRow
{
    public string Id { get; set; } = "";
    public string ChatId { get; set; } = "";
    public byte[] Bytes { get; set; } = [];
    public string ContentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Title { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
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
