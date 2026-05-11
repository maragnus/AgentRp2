using AgentRp.Models;
using AgentRp.Services;
using AgentRp.UserSystem;

namespace AgentRp.Session;

public sealed class StoryCardStore(
    ActiveChatContext activeChat,
    ChatRegistry registry,
    IStoryCardCatalogService catalog,
    CurrentAppUser user) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.StoryCards;
    public List<StoryCardInstance> Items => Document?.StoryCards ?? [];

    public async Task<StoryCardInstance> InjectAsync(string templateId)
    {
        if (Document is null)
            throw new InvalidOperationException("Adding a story card failed because no story is open.");

        var instance = await catalog.CreateInstanceAsync(user, Document.Chat.Id, templateId, Document.Chat.LastGeneratedTurnNumber, injected: true);
        Items.Add(instance);
        await SaveActiveDocumentAsync();
        return instance;
    }

    public async Task SetStatusAsync(string instanceId, StoryCardStatus status)
    {
        if (Document is null)
            return;

        var instance = Items.FirstOrDefault(card => card.Id == instanceId);
        if (instance is null || instance.Status == status)
            return;

        var previous = instance.Status;
        instance.Status = status;
        if (status == StoryCardStatus.Active)
            instance.EndTurnNumber = null;
        else
            instance.EndTurnNumber ??= Document.Chat.LastGeneratedTurnNumber;

        Touch(instance);
        instance.History.Insert(0, new()
        {
            Id = $"history-{Guid.NewGuid():N}",
            Kind = StoryCardHistoryKind.StatusChanged,
            Title = "Status changed",
            Details = $"{previous} to {status}",
            TurnNumber = Document.Chat.LastGeneratedTurnNumber,
            CreatedUtc = DateTime.UtcNow
        });
        await SaveActiveDocumentAsync();
    }

    public async Task SaveInstanceAsync(StoryCardInstance updated)
    {
        if (Document is null)
            return;

        var index = Items.FindIndex(card => card.Id == updated.Id);
        if (index < 0)
            return;

        updated.ChatId = Document.Chat.Id;
        Touch(updated);
        updated.History.Insert(0, new()
        {
            Id = $"history-{Guid.NewGuid():N}",
            Kind = StoryCardHistoryKind.Edited,
            Title = "Story card edited",
            Details = updated.Title,
            TurnNumber = Document.Chat.LastGeneratedTurnNumber,
            CreatedUtc = DateTime.UtcNow
        });
        Items[index] = updated;
        await SaveActiveDocumentAsync();
    }

    public bool CanInspectInternals() =>
        Document is not null && (user.IsAdmin || (user.IsSuperUser && Document.Chat.UserId == user.Id));

    static void Touch(StoryCardInstance instance) => instance.UpdatedUtc = DateTime.UtcNow;
}

public sealed class StoryCardContextProjection
{
    public IReadOnlyList<StoryCardInstance> ActiveCards(RpChatDocument document) =>
        document.StoryCards.Where(card => card.Status == StoryCardStatus.Active).ToList();
}
