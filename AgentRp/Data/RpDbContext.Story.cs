using Microsoft.EntityFrameworkCore;

namespace AgentRp.Data;

public sealed partial class RpDbContext
{
    static void ConfigureStory(ModelBuilder modelBuilder)
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
            builder.HasIndex(x => x.SortOrder);
            builder.HasIndex(x => x.UpdatedUtc);
            builder.HasIndex(x => x.LastMessageUtc);
        });

        modelBuilder.Entity<ChatCharacterRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.Id });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.Name).HasMaxLength(500);
            builder.Property(x => x.ImageId).HasMaxLength(80);
            builder.Property(x => x.ProfileJson).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => new { x.ChatId, x.SortOrder });
            builder.HasIndex(x => new { x.ChatId, x.ImageId });
        });

        modelBuilder.Entity<ChatCharacterRelationshipRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.Id });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.CharacterAId).HasMaxLength(80);
            builder.Property(x => x.CharacterBId).HasMaxLength(80);
            builder.Property(x => x.DetailsJson).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => new { x.ChatId, x.CharacterAId });
            builder.HasIndex(x => new { x.ChatId, x.CharacterBId });
            builder.HasIndex(x => new { x.ChatId, x.CharacterAId, x.CharacterBId });
        });

        modelBuilder.Entity<ChatLocationRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.Id });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.Name).HasMaxLength(500);
            builder.Property(x => x.ImageId).HasMaxLength(80);
            builder.Property(x => x.DetailsJson).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => new { x.ChatId, x.SortOrder });
            builder.HasIndex(x => new { x.ChatId, x.ImageId });
        });

        modelBuilder.Entity<ChatItemRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.Id });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.Name).HasMaxLength(500);
            builder.Property(x => x.ImageId).HasMaxLength(80);
            builder.Property(x => x.DetailsJson).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => new { x.ChatId, x.SortOrder });
            builder.HasIndex(x => new { x.ChatId, x.ImageId });
        });

        modelBuilder.Entity<ChatTimelineEntryRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.Id });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.SnapshotId).HasMaxLength(80);
            builder.Property(x => x.Title).HasMaxLength(500);
            builder.Property(x => x.DateText).HasMaxLength(200);
            builder.Property(x => x.DetailsJson).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => new { x.ChatId, x.SortOrder });
            builder.HasIndex(x => new { x.ChatId, x.SnapshotId });
        });

        modelBuilder.Entity<ChatTranscriptStateRow>(builder =>
        {
            builder.HasKey(x => x.ChatId);
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.RootSceneJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.WorkingSceneJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.OptionsJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.BranchSelectionsJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.DataJson).HasColumnType("nvarchar(max)");
        });

        ConfigureJsonState<ChatDirectionStateRow>(modelBuilder, "ChatDirectionStates");
        ConfigureJsonState<NarratorProfileStateRow>(modelBuilder, "NarratorProfileStates");
        ConfigureJsonState<PromptLibraryStateRow>(modelBuilder, "PromptLibraryStates");
        ConfigureJsonState<CharacterTraitLibraryStateRow>(modelBuilder, "CharacterTraitLibraryStates");
        ConfigureJsonState<ModelTuningStateRow>(modelBuilder, "ModelTuningStates");

        modelBuilder.Entity<ChatCurrentSceneCharacterRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.CharacterId });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.CharacterId).HasMaxLength(80);
            builder.HasIndex(x => new { x.ChatId, x.SortOrder });
        });

        modelBuilder.Entity<ChatCurrentSceneItemRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.ItemId });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.ItemId).HasMaxLength(80);
            builder.HasIndex(x => new { x.ChatId, x.SortOrder });
        });
    }

    static void ConfigureJsonState<TRow>(ModelBuilder modelBuilder, string tableName)
        where TRow : class
    {
        modelBuilder.Entity<TRow>(builder =>
        {
            builder.ToTable(tableName);
            builder.HasKey("ChatId");
            builder.Property<string>("ChatId").HasMaxLength(80);
            builder.Property<string>("StateJson").HasColumnType("nvarchar(max)");
        });
    }
}
