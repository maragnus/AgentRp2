using Microsoft.EntityFrameworkCore;

namespace AgentRp.Data;

public sealed partial class RpDbContext
{
    static void ConfigureStoryCards(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoryCardTemplateRow>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.OwnerDisplayName).HasMaxLength(500);
            builder.Property(x => x.Title).HasMaxLength(500);
            builder.Property(x => x.Summary).HasMaxLength(1200);
            builder.Property(x => x.ParentTemplateId).HasMaxLength(80);
            builder.Property(x => x.RootTemplateId).HasMaxLength(80);
            builder.Property(x => x.Instructions).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => x.OwnerUserId);
            builder.HasIndex(x => x.IsShared);
            builder.HasIndex(x => x.ParentTemplateId);
            builder.HasIndex(x => x.RootTemplateId);
        });

        ConfigureTemplateChild<StoryCardTemplatePhaseRow>(modelBuilder, "StoryCardTemplatePhases");
        ConfigureTemplateChild<StoryCardTemplatePhaseTransitionRow>(modelBuilder, "StoryCardTemplatePhaseTransitions");
        ConfigureTemplateChild<StoryCardTemplatePhaseRequirementRow>(modelBuilder, "StoryCardTemplatePhaseRequirements");
        ConfigureTemplateChild<StoryCardTemplateRoleRow>(modelBuilder, "StoryCardTemplateRoles");
        ConfigureTemplateChild<StoryCardTemplateItemRow>(modelBuilder, "StoryCardTemplateItems");
        ConfigureTemplateChild<StoryCardTemplateLocationRow>(modelBuilder, "StoryCardTemplateLocations");
        modelBuilder.Entity<StoryCardTemplatePhaseRequirementRow>(builder =>
        {
            builder.Property(x => x.PhaseId).HasMaxLength(80);
            builder.Property(x => x.ChildCardType).HasMaxLength(64);
            builder.Property(x => x.ChildCardId).HasMaxLength(80);
        });

        modelBuilder.Entity<StoryCardInstanceRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.Id });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.SourceTemplateId).HasMaxLength(80);
            builder.Property(x => x.ParentTemplateId).HasMaxLength(80);
            builder.Property(x => x.RootTemplateId).HasMaxLength(80);
            builder.Property(x => x.SourceOwnerDisplayName).HasMaxLength(500);
            builder.Property(x => x.Title).HasMaxLength(500);
            builder.Property(x => x.Summary).HasMaxLength(1200);
            builder.Property(x => x.Status).HasMaxLength(64);
            builder.Property(x => x.ActivePhaseId).HasMaxLength(80);
            builder.Property(x => x.Instructions).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => new { x.ChatId, x.SortOrder });
            builder.HasIndex(x => x.SourceTemplateId);
        });

        ConfigureInstanceChild<StoryCardInstancePhaseRow>(modelBuilder, "StoryCardInstancePhases");
        ConfigureInstanceChild<StoryCardInstancePhaseTransitionRow>(modelBuilder, "StoryCardInstancePhaseTransitions");
        ConfigureInstanceChild<StoryCardInstancePhaseRequirementRow>(modelBuilder, "StoryCardInstancePhaseRequirements");
        ConfigureInstanceChild<StoryCardInstanceRoleRow>(modelBuilder, "StoryCardInstanceRoles");
        ConfigureInstanceChild<StoryCardInstanceItemRow>(modelBuilder, "StoryCardInstanceItems");
        ConfigureInstanceChild<StoryCardInstanceLocationRow>(modelBuilder, "StoryCardInstanceLocations");
        ConfigureInstanceChild<StoryCardInstanceAssignmentRow>(modelBuilder, "StoryCardInstanceAssignments");
        modelBuilder.Entity<StoryCardInstancePhaseRequirementRow>(builder =>
        {
            builder.Property(x => x.SourceTemplateChildId).HasMaxLength(80);
            builder.Property(x => x.PhaseId).HasMaxLength(80);
            builder.Property(x => x.ChildCardType).HasMaxLength(64);
            builder.Property(x => x.ChildCardId).HasMaxLength(80);
        });
        modelBuilder.Entity<StoryCardInstanceAssignmentRow>(builder =>
        {
            builder.Property(x => x.ChildCardType).HasMaxLength(64);
            builder.Property(x => x.ChildCardId).HasMaxLength(80);
            builder.Property(x => x.EntityId).HasMaxLength(80);
            builder.Property(x => x.EntityName).HasMaxLength(500);
        });

        modelBuilder.Entity<StoryCardHistoryRow>(builder =>
        {
            builder.HasKey(x => new { x.ChatId, x.StoryCardInstanceId, x.Id });
            builder.Property(x => x.ChatId).HasMaxLength(80);
            builder.Property(x => x.StoryCardInstanceId).HasMaxLength(80);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.Kind).HasMaxLength(64);
            builder.Property(x => x.Title).HasMaxLength(500);
            builder.Property(x => x.Details).HasMaxLength(1200);
            builder.HasIndex(x => new { x.ChatId, x.StoryCardInstanceId, x.CreatedUtc });
        });
    }

    static void ConfigureTemplateChild<TRow>(ModelBuilder modelBuilder, string tableName)
        where TRow : class, IStoryCardTemplateChildRow
    {
        modelBuilder.Entity<TRow>(builder =>
        {
            builder.ToTable(tableName);
            builder.HasKey("StoryCardTemplateId", "Id");
            builder.Property<string>("StoryCardTemplateId").HasMaxLength(80);
            builder.Property<string>("Id").HasMaxLength(80);
            builder.Property<int>("SortOrder");
            builder.HasIndex("StoryCardTemplateId", "SortOrder");
        });
    }

    static void ConfigureInstanceChild<TRow>(ModelBuilder modelBuilder, string tableName)
        where TRow : class, IStoryCardInstanceChildRow
    {
        modelBuilder.Entity<TRow>(builder =>
        {
            builder.ToTable(tableName);
            builder.HasKey("ChatId", "StoryCardInstanceId", "Id");
            builder.Property<string>("ChatId").HasMaxLength(80);
            builder.Property<string>("StoryCardInstanceId").HasMaxLength(80);
            builder.Property<string>("Id").HasMaxLength(80);
            builder.Property<int>("SortOrder");
            builder.HasIndex("ChatId", "StoryCardInstanceId", "SortOrder");
        });
    }
}
