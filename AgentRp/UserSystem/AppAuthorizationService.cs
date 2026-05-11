using AgentRp.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.UserSystem;

public interface IAppAuthorizationService
{
    bool CanManageSystemGlobals(CurrentAppUser user);
    bool CanInspectGenerationProcess(CurrentAppUser user);
    bool CanAccessStory(CurrentAppUser user, Guid storyOwnerId);
    Task<bool> CanAccessStoryAsync(CurrentAppUser user, string chatId, CancellationToken cancellationToken = default);
}

public sealed class AppAuthorizationService(IDbContextFactory<RpDbContext> dbContextFactory) : IAppAuthorizationService
{
    public bool CanManageSystemGlobals(CurrentAppUser user) => user.IsAdmin;

    public bool CanInspectGenerationProcess(CurrentAppUser user) => user.CanInspectGenerationProcess;

    public bool CanAccessStory(CurrentAppUser user, Guid storyOwnerId) =>
        user.IsAdmin || user.Id == storyOwnerId;

    public async Task<bool> CanAccessStoryAsync(CurrentAppUser user, string chatId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var ownerId = await dbContext.Chats
            .AsNoTracking()
            .Where(chat => chat.Id == chatId)
            .OrderBy(chat => chat.Id)
            .Select(chat => (Guid?)chat.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        return ownerId is not null && CanAccessStory(user, ownerId.Value);
    }
}
