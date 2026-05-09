using Microsoft.EntityFrameworkCore;

namespace AgentRp.Data;

public sealed partial class RpDbContext
{
    static void ConfigureAssets(ModelBuilder modelBuilder)
    {
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
            builder.Property(x => x.Entity).HasMaxLength(500);
            builder.Property(x => x.EntityType).HasMaxLength(80);
            builder.Property(x => x.GenerationMetadataJson).HasColumnType("nvarchar(max)").HasDefaultValue("");
            builder.Property(x => x.UserPrompt).HasColumnType("nvarchar(max)");
            builder.Property(x => x.FinalPrompt).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ProviderId).HasMaxLength(80);
            builder.Property(x => x.ProviderName).HasMaxLength(200);
            builder.Property(x => x.ProviderModelId).HasMaxLength(500);
            builder.HasIndex(x => new { x.ChatId, x.CreatedUtc });
            builder.HasIndex(x => new { x.ChatId, x.EntityType, x.Entity });
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
