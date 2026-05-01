using Microsoft.EntityFrameworkCore;

namespace AgentRp.Data;

public sealed class RpDbContext(DbContextOptions<RpDbContext> options) : DbContext(options)
{
    public DbSet<RpChatRow> Chats => Set<RpChatRow>();
    public DbSet<RpChatDocumentRow> ChatDocuments => Set<RpChatDocumentRow>();
    public DbSet<AiProviderRow> AiProviders => Set<AiProviderRow>();
    public DbSet<AiProviderModelRow> AiProviderModels => Set<AiProviderModelRow>();
    public DbSet<ImageAssetRow> ImageAssets => Set<ImageAssetRow>();

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
            builder.Property(x => x.PromptLibraryJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.ModelTuningJson).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<AiProviderRow>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.Name).HasMaxLength(200);
            builder.Property(x => x.Type).HasMaxLength(100);
            builder.Property(x => x.ApiKey).HasColumnType("nvarchar(max)");
            builder.Property(x => x.Endpoint).HasMaxLength(1000);
            builder.HasIndex(x => x.SortOrder);
            builder.HasMany(x => x.Models)
                .WithOne(x => x.Provider)
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiProviderModelRow>(builder =>
        {
            builder.HasKey(x => new { x.ProviderId, x.Id });
            builder.Property(x => x.ProviderId).HasMaxLength(80);
            builder.Property(x => x.Id).HasMaxLength(500);
            builder.HasIndex(x => x.SortOrder);
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
            builder.Property(x => x.ProviderId).HasMaxLength(80);
            builder.Property(x => x.ProviderName).HasMaxLength(200);
            builder.Property(x => x.ProviderModelId).HasMaxLength(500);
            builder.Property(x => x.Bytes).HasColumnType("varbinary(max)");
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
    public string PromptLibraryJson { get; set; } = "";
    public string ModelTuningJson { get; set; } = "";
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
    public string Endpoint { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<AiProviderModelRow> Models { get; set; } = [];
}

public sealed class AiProviderModelRow
{
    public string ProviderId { get; set; } = "";
    public string Id { get; set; } = "";
    public bool Enabled { get; set; }
    public bool Text { get; set; }
    public bool Image { get; set; }
    public int SortOrder { get; set; }
    public AiProviderRow Provider { get; set; } = null!;
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
    public string ProviderId { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string ProviderModelId { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}
