using Microsoft.EntityFrameworkCore;

namespace AgentRp.Data;

public sealed class RpDbContext(DbContextOptions<RpDbContext> options) : DbContext(options)
{
    public DbSet<RpChatRow> Chats => Set<RpChatRow>();
    public DbSet<RpChatDocumentRow> ChatDocuments => Set<RpChatDocumentRow>();
    public DbSet<TranscriptTurnRow> TranscriptTurns => Set<TranscriptTurnRow>();
    public DbSet<TranscriptSnapshotRow> TranscriptSnapshots => Set<TranscriptSnapshotRow>();
    public DbSet<ChatCurrentSceneCharacterRow> ChatCurrentSceneCharacters => Set<ChatCurrentSceneCharacterRow>();
    public DbSet<AiProviderRow> AiProviders => Set<AiProviderRow>();
    public DbSet<AiProviderModelRow> AiProviderModels => Set<AiProviderModelRow>();
    public DbSet<AiProviderMetricRow> AiProviderMetrics => Set<AiProviderMetricRow>();
    public DbSet<ElevenLabsVoiceCatalogRow> ElevenLabsVoiceCatalog => Set<ElevenLabsVoiceCatalogRow>();
    public DbSet<ElevenLabsVoiceCatalogStateRow> ElevenLabsVoiceCatalogStates => Set<ElevenLabsVoiceCatalogStateRow>();
    public DbSet<AppSettingRow> AppSettings => Set<AppSettingRow>();
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
            builder.Property(x => x.ActiveLeafTurnId).HasMaxLength(80);
            builder.Property(x => x.ActiveLocationId).HasMaxLength(80);
            builder.Property(x => x.ActiveLocationName).HasMaxLength(500);
            builder.Property(x => x.ActiveLocationJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.SceneCharactersJson).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => x.SortOrder);
            builder.HasIndex(x => x.UpdatedUtc);
            builder.HasIndex(x => x.LastMessageUtc);
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
            builder.Property(x => x.CharacterRelationshipsJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.LocationsJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ItemsJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.TimelineJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ImagesJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.MessagesJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.StoryAssistantJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ChatDirectionJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.NarratorProfileJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.PromptLibraryJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.CharacterTraitLibraryJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ModelTuningJson).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<TranscriptTurnRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.Id });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.ParentTurnId).HasMaxLength(80);
            builder.Property(x => x.Mode).HasMaxLength(80);
            builder.Property(x => x.AuthorCharacterId).HasMaxLength(80);
            builder.Property(x => x.AuthorName).HasMaxLength(500);
            builder.Property(x => x.ActorCharacterId).HasMaxLength(80);
            builder.Property(x => x.ActorName).HasMaxLength(500);
            builder.Property(x => x.Guidance).HasColumnType("nvarchar(max)");
            builder.Property(x => x.Body).HasColumnType("nvarchar(max)");
            builder.Property(x => x.SceneLocationId).HasMaxLength(80);
            builder.Property(x => x.SceneLocationName).HasMaxLength(500);
            builder.Property(x => x.SceneJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.PlanJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.AppearanceJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.PrivateIntentJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.SpeechJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.TraceJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ConsumedBySnapshotId).HasMaxLength(80);
            builder.HasIndex(x => new { x.ChatId, x.ParentTurnId });
            builder.HasIndex(x => new { x.ChatId, x.ConsumedBySnapshotId, x.ConsumedBySnapshotOrdinal });
        });

        modelBuilder.Entity<TranscriptSnapshotRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.Id });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.TurnId).HasMaxLength(80);
            builder.Property(x => x.StartTurnId).HasMaxLength(80);
            builder.Property(x => x.EndTurnId).HasMaxLength(80);
            builder.Property(x => x.ParentBeforeStartTurnId).HasMaxLength(80);
            builder.Property(x => x.Summary).HasColumnType("nvarchar(max)");
            builder.Property(x => x.SceneLocationId).HasMaxLength(80);
            builder.Property(x => x.SceneLocationName).HasMaxLength(500);
            builder.Property(x => x.SceneJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.SpeechJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.PrivateIntentJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.CharacterAppearancesJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.TraceJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ConsumedBySnapshotId).HasMaxLength(80);
            builder.HasIndex(x => new { x.ChatId, x.EndTurnId, x.IsActive });
            builder.HasIndex(x => new { x.ChatId, x.ConsumedBySnapshotId, x.ConsumedBySnapshotOrdinal });
        });

        modelBuilder.Entity<ChatCurrentSceneCharacterRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.CharacterId });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.CharacterId).HasMaxLength(80);
            builder.HasIndex(x => new { x.ChatId, x.SortOrder });
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

        modelBuilder.Entity<AppSettingRow>(builder =>
        {
            builder.HasKey(x => x.Key);
            builder.Property(x => x.Key).HasMaxLength(200);
            builder.Property(x => x.JsonValue).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<ImageAssetRow>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.BlobName).HasMaxLength(500);
            builder.Property(x => x.StoredContentType).HasMaxLength(100);
            builder.Property(x => x.StoredFileName).HasMaxLength(500);
            builder.Property(x => x.OriginalContentType).HasMaxLength(100);
            builder.Property(x => x.OptimizationProvider).HasMaxLength(100);
            builder.Property(x => x.OptimizationError).HasMaxLength(1000);
            builder.Property(x => x.Title).HasMaxLength(500);
            builder.Property(x => x.AvatarFocusXPercent);
            builder.Property(x => x.AvatarFocusYPercent);
            builder.Property(x => x.AvatarZoomPercent);
            builder.Property(x => x.UserPrompt).HasColumnType("nvarchar(max)");
            builder.Property(x => x.FinalPrompt).HasColumnType("nvarchar(max)");
            builder.Property(x => x.GenerationMetadataJson).HasColumnType("nvarchar(max)").HasDefaultValue("");
            builder.Property(x => x.ProviderId).HasMaxLength(80);
            builder.Property(x => x.ProviderName).HasMaxLength(200);
            builder.Property(x => x.ProviderModelId).HasMaxLength(500);
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
