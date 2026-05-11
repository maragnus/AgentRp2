using Microsoft.EntityFrameworkCore;

namespace AgentRp.Data;

public sealed partial class RpDbContext(DbContextOptions<RpDbContext> options) : DbContext(options)
{
    public DbSet<UserRow> Users => Set<UserRow>();
    public DbSet<UserExternalIdentityRow> UserExternalIdentities => Set<UserExternalIdentityRow>();
    public DbSet<UserRoleRow> UserRoles => Set<UserRoleRow>();
    public DbSet<RpChatRow> Chats => Set<RpChatRow>();
    public DbSet<ChatCharacterRow> ChatCharacters => Set<ChatCharacterRow>();
    public DbSet<ChatCharacterRelationshipRow> ChatCharacterRelationships => Set<ChatCharacterRelationshipRow>();
    public DbSet<ChatLocationRow> ChatLocations => Set<ChatLocationRow>();
    public DbSet<ChatItemRow> ChatItems => Set<ChatItemRow>();
    public DbSet<ChatTimelineEntryRow> ChatTimelineEntries => Set<ChatTimelineEntryRow>();
    public DbSet<ChatTranscriptStateRow> ChatTranscriptStates => Set<ChatTranscriptStateRow>();
    public DbSet<ChatDirectionStateRow> ChatDirectionStates => Set<ChatDirectionStateRow>();
    public DbSet<NarratorProfileStateRow> NarratorProfileStates => Set<NarratorProfileStateRow>();
    public DbSet<CharacterTraitLibraryStateRow> CharacterTraitLibraryStates => Set<CharacterTraitLibraryStateRow>();
    public DbSet<StoryModelSelectionRow> StoryModelSelections => Set<StoryModelSelectionRow>();
    public DbSet<TranscriptTurnRow> TranscriptTurns => Set<TranscriptTurnRow>();
    public DbSet<TranscriptSnapshotRow> TranscriptSnapshots => Set<TranscriptSnapshotRow>();
    public DbSet<ChatCurrentSceneCharacterRow> ChatCurrentSceneCharacters => Set<ChatCurrentSceneCharacterRow>();
    public DbSet<ChatCurrentSceneItemRow> ChatCurrentSceneItems => Set<ChatCurrentSceneItemRow>();
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
        ConfigureUsers(modelBuilder);
        ConfigureStory(modelBuilder);
        ConfigureTranscript(modelBuilder);
        ConfigureProviders(modelBuilder);
        ConfigureAssets(modelBuilder);
    }
}
