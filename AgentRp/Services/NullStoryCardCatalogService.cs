using AgentRp.Models;
using AgentRp.UserSystem;

namespace AgentRp.Services;

public sealed class NullStoryCardCatalogService : IStoryCardCatalogService
{
    public static NullStoryCardCatalogService Instance { get; } = new();

    public Task<IReadOnlyList<StoryCardTemplate>> LoadCatalogAsync(CurrentAppUser user, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StoryCardTemplate>>([]);

    public Task<StoryCardTemplate?> LoadTemplateAsync(CurrentAppUser user, string templateId, bool lineageView = false, CancellationToken cancellationToken = default) =>
        Task.FromResult<StoryCardTemplate?>(null);

    public Task<StoryCardTemplateDetails?> LoadTemplateDetailsAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default) =>
        Task.FromResult<StoryCardTemplateDetails?>(null);

    public Task<StoryCardTemplate> SaveTemplateAsync(CurrentAppUser user, StoryCardTemplate template, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Saving story cards is not available.");

    public Task<StoryCardTemplate> RemixAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Remixing story cards is not available.");

    public Task ArchiveAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Archiving story cards is not available.");

    public Task<StoryCardTemplate> RefreshStatsAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Refreshing story card stats is not available.");

    public Task<StoryCardInstance> CreateInstanceAsync(CurrentAppUser user, string chatId, string templateId, int startTurnNumber, bool injected, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Adding story cards is not available.");
}
