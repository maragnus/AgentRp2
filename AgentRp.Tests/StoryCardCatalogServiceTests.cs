using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.UserSystem;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Tests;

public sealed class StoryCardCatalogServiceTests
{
    [Fact]
    public async Task SaveTemplatePersistsPhaseRequirements()
    {
        var service = new StoryCardCatalogService(new TestDbContextFactory());
        var user = TestUser();
        var template = BuildTemplate(user);

        var saved = await service.SaveTemplateAsync(user, template);
        var loaded = await service.LoadTemplateAsync(user, saved.Id, lineageView: true);

        var requirement = Assert.Single(loaded!.PhaseRequirements);
        Assert.Equal(saved.Phases[0].Id, requirement.PhaseId);
        Assert.Equal(StoryCardChildCardType.Role, requirement.ChildCardType);
        Assert.Equal(saved.Roles[0].Id, requirement.ChildCardId);
        Assert.Equal(4, requirement.RequiredCount);
    }

    [Fact]
    public async Task RemixRemapsPhaseRequirementReferences()
    {
        var service = new StoryCardCatalogService(new TestDbContextFactory());
        var user = TestUser();
        var source = await service.SaveTemplateAsync(user, BuildTemplate(user));

        var remix = await service.RemixAsync(user, source.Id);
        var requirement = Assert.Single(remix.PhaseRequirements);

        Assert.NotEqual(source.Phases[0].Id, requirement.PhaseId);
        Assert.NotEqual(source.Roles[0].Id, requirement.ChildCardId);
        Assert.Equal(remix.Phases[0].Id, requirement.PhaseId);
        Assert.Equal(remix.Roles[0].Id, requirement.ChildCardId);
        Assert.Equal(4, requirement.RequiredCount);
    }

    [Fact]
    public async Task CreateInstanceRemapsPhaseRequirementReferences()
    {
        var service = new StoryCardCatalogService(new TestDbContextFactory());
        var user = TestUser();
        var source = BuildTemplate(user);
        source.IsShared = true;
        var saved = await service.SaveTemplateAsync(user, source);

        var instance = await service.CreateInstanceAsync(user, "chat-1", saved.Id, 7, injected: false);
        var requirement = Assert.Single(instance.PhaseRequirements);

        Assert.NotEqual(saved.Phases[0].Id, requirement.PhaseId);
        Assert.NotEqual(saved.Roles[0].Id, requirement.ChildCardId);
        Assert.Equal(instance.Phases[0].Id, requirement.PhaseId);
        Assert.Equal(instance.Roles[0].Id, requirement.ChildCardId);
        Assert.Equal(4, requirement.RequiredCount);
    }

    static StoryCardTemplate BuildTemplate(CurrentAppUser user) => new()
    {
        OwnerUserId = user.Id,
        OwnerDisplayName = user.DisplayName,
        Title = "Emergency Repair",
        Summary = "A malfunction creates pressure.",
        Phases =
        [
            new() { Id = "phase-source", Title = "Equipment malfunction threatens lives", SortOrder = 0, IsEnding = true }
        ],
        Roles =
        [
            new() { Id = "role-source", Title = "Endangered", SortOrder = 0 }
        ],
        PhaseRequirements =
        [
            new()
            {
                Id = "requirement-source",
                PhaseId = "phase-source",
                ChildCardType = StoryCardChildCardType.Role,
                ChildCardId = "role-source",
                RequiredCount = 4
            }
        ]
    };

    static CurrentAppUser TestUser() => new(
        Guid.NewGuid(),
        "user@example.test",
        "USER@EXAMPLE.TEST",
        "Test User",
        new HashSet<string>(StringComparer.Ordinal));

    sealed class TestDbContextFactory : IDbContextFactory<RpDbContext>
    {
        readonly DbContextOptions<RpDbContext> options = new DbContextOptionsBuilder<RpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        public RpDbContext CreateDbContext() => new(options);
    }
}
