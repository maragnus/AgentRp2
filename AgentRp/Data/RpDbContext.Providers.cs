using Microsoft.EntityFrameworkCore;

namespace AgentRp.Data;

public sealed partial class RpDbContext
{
    static void ConfigureProviders(ModelBuilder modelBuilder)
    {
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
    }
}
