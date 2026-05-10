using Microsoft.EntityFrameworkCore;

namespace AgentRp.Data;

public sealed partial class RpDbContext
{
    static void ConfigureTranscript(ModelBuilder modelBuilder)
    {
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
            builder.Property(x => x.RelationshipUpdatesJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.TraceJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ConsumedBySnapshotId).HasMaxLength(80);
            builder.HasIndex(x => new { x.ChatId, x.EndTurnId, x.IsActive });
            builder.HasIndex(x => new { x.ChatId, x.ConsumedBySnapshotId, x.ConsumedBySnapshotOrdinal });
        });
    }
}
