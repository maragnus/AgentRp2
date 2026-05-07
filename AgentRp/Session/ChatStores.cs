using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

public sealed class CharacterStore(ActiveChatContext activeChat, ChatRegistry registry, IEntityNotifier entityNotifier) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Characters;
    public List<RpCharacter> Items => Document?.Characters ?? [];

    protected override bool ShouldHandleArea(RoleplayStoreArea? changedArea) =>
        changedArea is null || changedArea == Area || changedArea == RoleplayStoreArea.Transcript;

    public async Task<RpCharacter> AddAsync()
    {
        var character = new RpCharacter { Id = NextId(), Name = "New Character" };
        Items.Insert(0, character);
        await SaveActiveDocumentAsync();
        return character;
    }

    public async Task DeleteAsync(string id)
    {
        Items.RemoveAll(character => character.Id == id);
        if (Document is not null)
        {
            RemoveCharacterReferences(Document.Transcript.RootScene, id);
            foreach (var turn in Document.Transcript.Turns)
            {
                RemoveCharacterReferences(turn.Scene, id);
                turn.AppearanceByCharacterId.Remove(id);
                turn.PrivateIntentByCharacterId.Remove(id);
            }

            foreach (var snapshot in Document.Transcript.Snapshots)
            {
                snapshot.CharacterAppearances.Remove(id);
                snapshot.PrivateIntentByCharacterId.Remove(id);
            }

            TranscriptProjector.Apply(Document);
            await SaveCatalogAndTranscriptAsync();
            await entityNotifier.PublishAsync(new(EntityTypes.Character, id, EntityChangeKinds.Deleted, ChatId: Document.Chat.Id));
            return;
        }

        await SaveActiveDocumentAsync();
        await entityNotifier.PublishAsync(new(EntityTypes.Character, id, EntityChangeKinds.Deleted));
    }

    public async Task ToggleInSceneAsync(string id)
    {
        if (Document is null)
            return;

        var scene = TranscriptGraph.GetEditableActiveScene(Document.Transcript);
        if (!scene.InSceneCharacterIds.Remove(id))
            scene.InSceneCharacterIds.Add(id);

        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    }

    public async Task SetImageAsync(string id, string imageId)
    {
        Items.First(character => character.Id == id).ImageId = imageId;
        await SaveActiveDocumentAsync();
        await entityNotifier.PublishAsync(new(EntityTypes.Character, id, EntityChangeKinds.Image, imageId, Document?.Chat.Id ?? ""));
    }

    public async Task MarkChangedAsync()
    {
        await SaveActiveDocumentAsync();
        foreach (var character in Items)
            await entityNotifier.PublishAsync(new(EntityTypes.Character, character.Id, EntityChangeKinds.Profile, character.ImageId, Document?.Chat.Id ?? ""));
    }

    async Task SaveCatalogAndTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, Area);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyActiveDocumentChangedAsync(RoleplayStoreArea.Transcript);
    }

    async Task SaveTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyActiveDocumentChangedAsync(RoleplayStoreArea.Transcript);
    }

    static void RemoveCharacterReferences(RpSceneFrame scene, string id) =>
        scene.InSceneCharacterIds.RemoveAll(characterId => characterId == id);

    string NextId() => NextIdFor(Items.Select(character => character.Id), "c");

    static string NextIdFor(IEnumerable<string> ids, string prefix)
    {
        var next = ids
            .Where(id => id.Length > prefix.Length && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(id[prefix.Length..], out _))
            .Select(id => int.Parse(id[prefix.Length..]))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{prefix}{next}";
    }
}

public sealed class LocationStore(ActiveChatContext activeChat, ChatRegistry registry, IEntityNotifier entityNotifier) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Locations;
    public List<RpLocation> Items => Document?.Locations ?? [];
    public RpLocation? Active => Items.FirstOrDefault(location => location.IsActive);

    protected override bool ShouldHandleArea(RoleplayStoreArea? changedArea) =>
        changedArea is null || changedArea == Area || changedArea == RoleplayStoreArea.Transcript;

    public async Task<RpLocation> AddAsync()
    {
        var location = new RpLocation { Id = NextId(), Name = "New Location", Summary = "New location summary." };
        Items.Add(location);
        await SaveActiveDocumentAsync();
        return location;
    }

    public async Task DeleteAsync(string id)
    {
        Items.RemoveAll(location => location.Id == id);
        var replacement = Items.FirstOrDefault();
        if (Document is not null)
        {
            UpdateScene(Document.Transcript.RootScene, id, replacement);
            foreach (var turn in Document.Transcript.Turns)
                UpdateScene(turn.Scene, id, replacement);

            foreach (var snapshot in Document.Transcript.Snapshots)
                UpdateScene(snapshot.Scene, id, replacement);

            TranscriptProjector.Apply(Document);
            await SaveCatalogAndTranscriptAsync();
            await entityNotifier.PublishAsync(new(EntityTypes.Location, id, EntityChangeKinds.Deleted, ChatId: Document.Chat.Id));
            return;
        }

        await SaveActiveDocumentAsync();
        await entityNotifier.PublishAsync(new(EntityTypes.Location, id, EntityChangeKinds.Deleted));
    }

    public async Task SetActiveAsync(string id)
    {
        if (Document is null)
            return;

        var scene = TranscriptGraph.GetEditableActiveScene(Document.Transcript);
        var location = Items.FirstOrDefault(item => item.Id == id);
        scene.LocationId = location?.Id ?? "";
        scene.LocationName = location?.Name ?? "";
        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    }

    public async Task SetImageAsync(string id, string imageId)
    {
        Items.First(location => location.Id == id).ImageId = imageId;
        await SaveActiveDocumentAsync();
        await entityNotifier.PublishAsync(new(EntityTypes.Location, id, EntityChangeKinds.Image, imageId, Document?.Chat.Id ?? ""));
    }

    public async Task MarkChangedAsync()
    {
        await SaveActiveDocumentAsync();
        foreach (var location in Items)
            await entityNotifier.PublishAsync(new(EntityTypes.Location, location.Id, EntityChangeKinds.Profile, location.ImageId, Document?.Chat.Id ?? ""));
    }

    async Task SaveCatalogAndTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, Area);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyActiveDocumentChangedAsync(RoleplayStoreArea.Transcript);
    }

    async Task SaveTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyActiveDocumentChangedAsync(RoleplayStoreArea.Transcript);
    }

    static void UpdateScene(RpSceneFrame scene, string deletedId, RpLocation? replacement)
    {
        if (scene.LocationId != deletedId)
            return;

        scene.LocationId = replacement?.Id ?? "";
        scene.LocationName = replacement?.Name ?? "";
    }

    string NextId() => NextIdFor(Items.Select(location => location.Id), "l");

    static string NextIdFor(IEnumerable<string> ids, string prefix)
    {
        var next = ids
            .Where(id => id.Length > prefix.Length && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(id[prefix.Length..], out _))
            .Select(id => int.Parse(id[prefix.Length..]))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{prefix}{next}";
    }
}

public sealed class ItemStore(ActiveChatContext activeChat, ChatRegistry registry, IEntityNotifier entityNotifier) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Items;
    public List<RpItem> Items => Document?.Items ?? [];

    protected override bool ShouldHandleArea(RoleplayStoreArea? changedArea) =>
        changedArea is null || changedArea == Area || changedArea == RoleplayStoreArea.Transcript;

    public async Task<RpItem> AddAsync()
    {
        var item = new RpItem { Id = NextId(), Name = "New Item", Summary = "New item summary." };
        Items.Add(item);
        await SaveActiveDocumentAsync();
        return item;
    }

    public async Task DeleteAsync(string id)
    {
        Items.RemoveAll(item => item.Id == id);
        if (Document is not null)
        {
            Document.Transcript.RootScene.InSceneItemIds.RemoveAll(itemId => itemId == id);
            foreach (var turn in Document.Transcript.Turns)
                turn.Scene.InSceneItemIds.RemoveAll(itemId => itemId == id);

            foreach (var snapshot in Document.Transcript.Snapshots)
                snapshot.Scene.InSceneItemIds.RemoveAll(itemId => itemId == id);

            TranscriptProjector.Apply(Document);
            await SaveCatalogAndTranscriptAsync();
            await entityNotifier.PublishAsync(new(EntityTypes.Item, id, EntityChangeKinds.Deleted, ChatId: Document.Chat.Id));
            return;
        }

        await SaveActiveDocumentAsync();
        await entityNotifier.PublishAsync(new(EntityTypes.Item, id, EntityChangeKinds.Deleted));
    }

    public async Task ToggleInSceneAsync(string id)
    {
        if (Document is null)
            return;

        var scene = TranscriptGraph.GetEditableActiveScene(Document.Transcript);
        if (!scene.InSceneItemIds.Remove(id))
            scene.InSceneItemIds.Add(id);

        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    }

    public async Task SetImageAsync(string id, string imageId)
    {
        Items.First(item => item.Id == id).ImageId = imageId;
        await SaveActiveDocumentAsync();
        await entityNotifier.PublishAsync(new(EntityTypes.Item, id, EntityChangeKinds.Image, imageId, Document?.Chat.Id ?? ""));
    }

    public async Task MarkChangedAsync()
    {
        await SaveActiveDocumentAsync();
        foreach (var item in Items)
            await entityNotifier.PublishAsync(new(EntityTypes.Item, item.Id, EntityChangeKinds.Profile, item.ImageId, Document?.Chat.Id ?? ""));
    }

    async Task SaveCatalogAndTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, Area);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyActiveDocumentChangedAsync(RoleplayStoreArea.Transcript);
    }

    async Task SaveTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyActiveDocumentChangedAsync(RoleplayStoreArea.Transcript);
    }

    string NextId() => NextIdFor(Items.Select(item => item.Id), "i");

    static string NextIdFor(IEnumerable<string> ids, string prefix)
    {
        var next = ids
            .Where(id => id.Length > prefix.Length && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(id[prefix.Length..], out _))
            .Select(id => int.Parse(id[prefix.Length..]))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{prefix}{next}";
    }
}

public sealed class TimelineStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Timeline;
    public List<RpTimelineEntry> Items => Document?.Timeline ?? [];

    public async Task<RpTimelineEntry> AddAsync()
    {
        var entry = new RpTimelineEntry { Id = NextId(), Title = "New Event", Date = "today" };
        Items.Add(entry);
        await SaveActiveDocumentAsync();
        return entry;
    }

    public async Task DeleteAsync(string id)
    {
        Items.RemoveAll(entry => entry.Id == id);
        await SaveActiveDocumentAsync();
    }

    public Task MarkChangedAsync() => SaveActiveDocumentAsync();

    string NextId()
    {
        var next = Items
            .Select(entry => entry.Id)
            .Where(id => id.Length > 1 && id.StartsWith("t", StringComparison.OrdinalIgnoreCase) && int.TryParse(id[1..], out _))
            .Select(id => int.Parse(id[1..]))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"t{next}";
    }
}

public sealed class ImageStore(ActiveChatContext activeChat, ChatRegistry registry, IEntityNotifier entityNotifier) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Images;
    public List<GalleryImage> Items => Document?.Images ?? [];

    public async Task AddAsync(GalleryImage image)
    {
        Items.Insert(0, image);
        await SaveActiveDocumentAsync();
    }

    public async Task DeleteAsync(string id)
    {
        Items.RemoveAll(image => image.Id == id);
        await SaveActiveDocumentAsync();
    }

    public async Task SetCropAsync(string id, ImageAvatarCropView crop)
    {
        var image = Items.FirstOrDefault(image => image.Id == id);
        if (image is null)
            return;

        image.AvatarFocusXPercent = crop.FocusXPercent;
        image.AvatarFocusYPercent = crop.FocusYPercent;
        image.AvatarZoomPercent = crop.ZoomPercent;
        await SaveActiveDocumentAsync();
        foreach (var notification in BuildCropNotifications(id))
            await entityNotifier.PublishAsync(notification);
    }

    IReadOnlyList<EntityChangeNotification> BuildCropNotifications(string imageId)
    {
        if (Document is null)
            return [];

        return Document.Characters
            .Where(character => character.ImageId == imageId)
            .Select(character => new EntityChangeNotification(EntityTypes.Character, character.Id, EntityChangeKinds.ImageCrop, imageId, Document.Chat.Id))
            .Concat(Document.Locations
                .Where(location => location.ImageId == imageId)
                .Select(location => new EntityChangeNotification(EntityTypes.Location, location.Id, EntityChangeKinds.ImageCrop, imageId, Document.Chat.Id)))
            .Concat(Document.Items
                .Where(item => item.ImageId == imageId)
                .Select(item => new EntityChangeNotification(EntityTypes.Item, item.Id, EntityChangeKinds.ImageCrop, imageId, Document.Chat.Id)))
            .ToList();
    }

    public string NextGalleryImageId()
    {
        var index = Items
            .Select(image => image.Id)
            .Where(id => id.Length > 1 && id[0] == 'g' && int.TryParse(id[1..], out _))
            .Select(id => int.Parse(id[1..]))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"g{index}";
    }
}

public sealed class StoryAssistantStore(
    ActiveChatContext activeChat,
    ChatRegistry registry,
    ProviderStore providers,
    TranscriptStore transcript,
    IStoryAssistantService? storyAssistantService) : ActiveChatStoreBase(activeChat, registry), IStoryAssistantCallbacks
{
    readonly Dictionary<string, TaskCompletionSource<StoryAssistantDecision>> _pendingReviews = [];
    readonly Dictionary<string, TaskCompletionSource<string>> _pendingQuestions = [];
    readonly object _operationLock = new();
    CancellationTokenSource? _activeRunCancellation;

    protected override RoleplayStoreArea Area => RoleplayStoreArea.StoryAssistant;

    public StoryAssistantState State => Document?.StoryAssistant ?? new();
    public IReadOnlyList<StoryAssistantTranscriptItem> Items => State.Items;
    public bool IsBusy { get; private set; }
    public string BusyMessage { get; private set; } = "";

    protected override bool ShouldHandleArea(RoleplayStoreArea? changedArea) => changedArea is null || changedArea == Area;

    public async Task SetReviewModeAsync(StoryAssistantReviewMode mode)
    {
        State.ReviewMode = mode;
        await SaveAssistantStateAsync(CancellationToken.None);
        await NotifyChangedAsync();
    }

    public async Task ClearAsync()
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        try
        {
            if (storyAssistantService is not null)
                await storyAssistantService.ClearRemoteStateAsync(Document, providers.Items.ToList(), CancellationToken.None);
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception);
        }

        State.Items.Clear();
        StoryAssistantService.ClearResponseChain(State);
        CancelPendingInteractions();
        _pendingReviews.Clear();
        _pendingQuestions.Clear();
        await SaveAssistantStateAsync(CancellationToken.None);
        await NotifyChangedAsync();
    }

    public async Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        if (Document is null || string.IsNullOrWhiteSpace(text) || storyAssistantService is null)
            return;

        CancellationTokenSource runCancellation;
        lock (_operationLock)
        {
            if (IsBusy)
                return;

            IsBusy = true;
            BusyMessage = "Thinking...";
            runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeRunCancellation = runCancellation;
        }

        ClearBackgroundError();
        State.Items.Add(AddMessage(StoryAssistantItemKind.UserMessage, StoryAssistantItemStatus.Applied, text.Trim()));
        State.Items.Add(AddMessage(StoryAssistantItemKind.AssistantMessage, StoryAssistantItemStatus.Streaming, ""));
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();

        var runToken = runCancellation.Token;
        try
        {
            await storyAssistantService.RunTurnAsync(
                Document,
                providers.Items.ToList(),
                new(text),
                this,
                runToken);
            CompleteStreamingAssistantMessages();
            await SaveAssistantStateAsync(runToken);
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
            MarkPendingInteractionsStopped();
            CancelPendingInteractions();
            MarkStopped();
            await SaveAssistantStateAsync(CancellationToken.None);
        }
        catch (ModelAssistantThreadLostException exception)
        {
            CaptureBackgroundError(exception);
            State.RemoteThreadLost = true;
            State.RemoteThreadError = UserFacingErrorMessageBuilder.Build("Story Assistant needs a fresh thread.", exception);
            FailCurrentAssistantMessage(State.RemoteThreadError);
            await SaveAssistantStateAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception);
            FailCurrentAssistantMessage(UserFacingErrorMessageBuilder.Build("Story Assistant failed.", exception));
            await SaveAssistantStateAsync(CancellationToken.None);
        }
        finally
        {
            lock (_operationLock)
            {
                IsBusy = false;
                BusyMessage = "";
                if (ReferenceEquals(_activeRunCancellation, runCancellation))
                    _activeRunCancellation = null;
            }

            runCancellation.Dispose();
            await NotifyChangedAsync();
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cancellation;
        lock (_operationLock)
        {
            cancellation = _activeRunCancellation;
            if (cancellation is null || cancellation.IsCancellationRequested)
                return;

            BusyMessage = "Stopping...";
            cancellation.Cancel();
        }

        MarkPendingInteractionsStopped();
        CancelPendingInteractions();
        await SaveAssistantStateAsync(CancellationToken.None);
        await NotifyChangedAsync();
    }

    public Task ResolveReviewAsync(string itemId, StoryAssistantDecisionKind kind, string reason)
    {
        if (_pendingReviews.TryGetValue(itemId, out var pending))
        {
            _pendingReviews.Remove(itemId);
            pending.TrySetResult(new(kind, reason.Trim()));
        }

        return Task.CompletedTask;
    }

    void CancelPendingInteractions()
    {
        foreach (var pending in _pendingReviews.Values)
            pending.TrySetCanceled();

        foreach (var pending in _pendingQuestions.Values)
            pending.TrySetCanceled();

        _pendingReviews.Clear();
        _pendingQuestions.Clear();
    }

    void MarkPendingInteractionsStopped()
    {
        var pendingIds = _pendingReviews.Keys.Concat(_pendingQuestions.Keys).ToHashSet(StringComparer.Ordinal);
        foreach (var item in State.Items.Where(item => pendingIds.Contains(item.Id)))
        {
            item.Status = StoryAssistantItemStatus.Stopped;
            item.DecisionReason = "Stopped before a decision.";
            item.UpdatedUtc = DateTime.UtcNow;
        }
    }

    public Task ResolveQuestionAsync(string itemId, string answer)
    {
        if (_pendingQuestions.TryGetValue(itemId, out var pending))
        {
            _pendingQuestions.Remove(itemId);
            pending.TrySetResult(answer.Trim());
        }

        return Task.CompletedTask;
    }

    public async Task AppendAssistantTextAsync(string delta, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(delta))
            return;

        var item = State.Items.LastOrDefault();
        if (item is not { Kind: StoryAssistantItemKind.AssistantMessage, Status: StoryAssistantItemStatus.Streaming })
        {
            item = AddMessage(StoryAssistantItemKind.AssistantMessage, StoryAssistantItemStatus.Streaming, "");
            State.Items.Add(item);
        }

        item.Text += delta;
        item.UpdatedUtc = DateTime.UtcNow;
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();
    }

    public async Task RecordToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken)
    {
        CloseTrailingAssistantMessage();
        item.UpdatedUtc = DateTime.UtcNow;
        State.Items.Add(item);
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();
    }

    public async Task UpdateToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken)
    {
        item.UpdatedUtc = DateTime.UtcNow;
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();
    }

    public async Task<StoryAssistantDecision> ReviewChangeAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken)
    {
        var pending = new TaskCompletionSource<StoryAssistantDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingReviews[item.Id] = pending;
        using var registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
        return await pending.Task;
    }

    public async Task<string> AskQuestionAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken)
    {
        CloseTrailingAssistantMessage();
        item.UpdatedUtc = DateTime.UtcNow;
        State.Items.Add(item);
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingQuestions[item.Id] = pending;
        using var registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
        var answer = await pending.Task;
        item.Question.Answer = answer;
        item.Status = StoryAssistantItemStatus.Answered;
        item.UpdatedUtc = DateTime.UtcNow;
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();
        return answer;
    }

    public async Task<SceneTransitionResult> GenerateSceneTransitionAsync(SceneTransitionRequest request, CancellationToken cancellationToken)
    {
        var transition = await transcript.GenerateSceneTransitionAsync(request, cancellationToken)
            ?? throw new InvalidOperationException("Setting the scene failed because no active chat is loaded.");
        return transition;
    }

    void CloseTrailingAssistantMessage()
    {
        if (State.Items.LastOrDefault() is not { Kind: StoryAssistantItemKind.AssistantMessage, Status: StoryAssistantItemStatus.Streaming } item)
            return;

        if (string.IsNullOrWhiteSpace(item.Text))
            State.Items.RemoveAt(State.Items.Count - 1);
        else
        {
            item.Status = StoryAssistantItemStatus.Applied;
            item.UpdatedUtc = DateTime.UtcNow;
        }
    }

    void CompleteStreamingAssistantMessages()
    {
        for (var index = State.Items.Count - 1; index >= 0; index--)
        {
            var item = State.Items[index];
            if (item is not { Kind: StoryAssistantItemKind.AssistantMessage, Status: StoryAssistantItemStatus.Streaming })
                continue;

            if (string.IsNullOrWhiteSpace(item.Text))
                State.Items.RemoveAt(index);
            else
            {
                item.Status = StoryAssistantItemStatus.Applied;
                item.UpdatedUtc = DateTime.UtcNow;
            }
        }
    }

    void FailCurrentAssistantMessage(string message)
    {
        if (State.Items.LastOrDefault() is { Kind: StoryAssistantItemKind.AssistantMessage, Status: StoryAssistantItemStatus.Streaming } item)
        {
            item.Status = StoryAssistantItemStatus.Failed;
            item.Text = message;
            item.UpdatedUtc = DateTime.UtcNow;
            return;
        }

        State.Items.Add(AddMessage(StoryAssistantItemKind.AssistantMessage, StoryAssistantItemStatus.Failed, message));
    }

    void MarkStopped()
    {
        if (State.Items.LastOrDefault() is { Kind: StoryAssistantItemKind.AssistantMessage, Status: StoryAssistantItemStatus.Streaming } item)
        {
            if (string.IsNullOrWhiteSpace(item.Text))
                item.Text = "Stopped.";

            item.Status = StoryAssistantItemStatus.Stopped;
            item.UpdatedUtc = DateTime.UtcNow;
            return;
        }

        State.Items.Add(AddMessage(StoryAssistantItemKind.AssistantMessage, StoryAssistantItemStatus.Stopped, "Stopped."));
    }

    public async Task SaveEntityAreaAsync(RoleplayStoreArea area, CancellationToken cancellationToken)
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, area);
        await ActiveChat.UpdateAsync(Document, area);
    }

    public async Task SaveAssistantStateAsync(CancellationToken cancellationToken)
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, Area);
    }

    static StoryAssistantTranscriptItem AddMessage(StoryAssistantItemKind kind, StoryAssistantItemStatus status, string text)
    {
        var now = DateTime.UtcNow;
        return new()
        {
            Id = $"assistant-item-{Guid.NewGuid():N}",
            Kind = kind,
            Status = status,
            CreatedUtc = now,
            UpdatedUtc = now,
            Text = text
        };
    }
}

public sealed class PromptLibraryStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.PromptLibrary;
    public PromptLibraryState State
    {
        get
        {
            if (Document is null)
                return PromptLibraryState.CreateDefault();

            Document.PromptLibrary = PromptLibraryService.NormalizeState(Document.PromptLibrary);
            return Document.PromptLibrary;
        }
    }

    public IReadOnlyDictionary<string, PromptPairState> Prompts => State.Prompts;
    public IReadOnlyDictionary<string, List<ShapePromptState>> TurnShapes => State.TurnShapes;

    public async Task MarkChangedAsync()
    {
        if (Document is not null)
        {
            Document.PromptLibrary = PromptLibraryService.NormalizeState(Document.PromptLibrary);
            PromptLibraryService.ValidateState(Document.PromptLibrary);
        }

        await SaveActiveDocumentAsync();
    }

    public void ResetPrompt(string stepId, string field)
    {
        var defaults = PromptLibraryService.CreateDefaultState();
        if (field == "system")
            State.Prompts[stepId].System = defaults.Prompts[stepId].System;
        else
            State.Prompts[stepId].User = defaults.Prompts[stepId].User;
    }

    public void ResetTurnShape(string stepId, string shapeId)
    {
        var defaults = PromptLibraryService.CreateDefaultState();
        State.TurnShapes[stepId].First(shape => shape.Id == shapeId).Value = defaults.TurnShapes[stepId].First(shape => shape.Id == shapeId).Value;
    }
}

public sealed class ChatDirectionStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.ChatDirection;
    public ChatDirectionState State
    {
        get
        {
            if (Document is null)
                return ChatDirectionState.CreateDefault();

            Document.ChatDirection = ChatDirectionService.NormalizeState(Document.ChatDirection);
            return Document.ChatDirection;
        }
    }

    public async Task MarkChangedAsync()
    {
        if (Document is not null)
            Document.ChatDirection = ChatDirectionService.NormalizeState(Document.ChatDirection);

        await SaveActiveDocumentAsync();
    }

    public void SetTitle(string title)
    {
        if (Document is not null)
            Document.Chat.Title = string.IsNullOrWhiteSpace(title) ? "Untitled Scene" : title.Trim();
    }

    public async Task ResetAsync()
    {
        if (Document is null)
            return;

        Document.ChatDirection = ChatDirectionState.CreateDefault();
        await MarkChangedAsync();
    }
}

public sealed class NarratorProfileStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.NarratorProfile;
    public NarratorProfileState State
    {
        get
        {
            if (Document is null)
                return NarratorProfileState.CreateDefault();

            Document.NarratorProfile = NarratorProfileService.NormalizeState(Document.NarratorProfile);
            return Document.NarratorProfile;
        }
    }

    public async Task MarkChangedAsync()
    {
        if (Document is not null)
            Document.NarratorProfile = NarratorProfileService.NormalizeState(Document.NarratorProfile);

        await SaveActiveDocumentAsync();
    }

    public async Task ResetAsync()
    {
        if (Document is null)
            return;

        Document.NarratorProfile = NarratorProfileState.CreateDefault();
        await MarkChangedAsync();
    }
}

public sealed class CharacterTraitLibraryStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.CharacterTraitLibrary;
    public CharacterTraitLibraryState State
    {
        get
        {
            if (Document is null)
                return CharacterTraitLibraryState.CreateDefault();

            Document.CharacterTraitLibrary = CharacterTraitLibraryService.NormalizeState(Document.CharacterTraitLibrary);
            return Document.CharacterTraitLibrary;
        }
    }

    public async Task MarkChangedAsync()
    {
        if (Document is not null)
        {
            Document.CharacterTraitLibrary = CharacterTraitLibraryService.NormalizeState(Document.CharacterTraitLibrary);
            CharacterTraitLibraryService.ValidateState(Document.CharacterTraitLibrary);
        }

        await SaveActiveDocumentAsync();
    }

    public async Task ResetAsync()
    {
        if (Document is null)
            return;

        Document.CharacterTraitLibrary = CharacterTraitLibraryService.CreateDefaultState();
        await MarkChangedAsync();
    }
}

public sealed class ModelTuningStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.ModelTuning;
    public ModelTuningState State => EnsureDefaults(Document?.ModelTuning ?? ModelTuningState.CreateDefault());
    public IReadOnlyDictionary<string, ModelTuningStepState> Values => State.Values;

    public Task MarkChangedAsync() => SaveActiveDocumentAsync();

    public void Reset(string stepId)
    {
        State.Values[stepId] = SessionCloner.Clone(ModelTuningState.CreateDefault()).Values[stepId];
    }

    static ModelTuningState EnsureDefaults(ModelTuningState state)
    {
        var defaults = ModelTuningState.CreateDefault();
        foreach (var pair in defaults.Values)
            state.Values.TryAdd(pair.Key, new ModelTuningStepState
            {
                Temperature = pair.Value.Temperature,
                TopP = pair.Value.TopP,
                MaxTokens = pair.Value.MaxTokens,
                Seed = pair.Value.Seed,
                FrequencyPenalty = pair.Value.FrequencyPenalty,
                PresencePenalty = pair.Value.PresencePenalty,
                StopSequences = pair.Value.StopSequences
            });

        return state;
    }
}

public sealed class ChatModelSelectionStore(ActiveChatContext activeChat, ChatRegistry registry, ProviderStore providers) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.ModelSelection;
    public ActiveModelSelectionsState State => Document?.ActiveModelSelections ?? ActiveModelSelectionsState.CreateDefault();

    public ActiveModelSelection? Resolve(AiModelRole role) =>
        role == AiModelRole.Reasoning
            ? TextModelTuningCatalog.TryResolveActiveReasoningModel(providers.Items, State)
            : TextModelTuningCatalog.TryResolveActiveModel(providers.Items, role, State);

    public async Task SetActiveModelAsync(AiModelRole role, string providerId, string modelId)
    {
        if (Document is null)
            return;

        var provider = providers.Items.FirstOrDefault(provider => provider.Id == providerId)
            ?? throw new InvalidOperationException("Selecting the AI model failed because the provider is not available.");
        if (!provider.Enabled)
            throw new InvalidOperationException($"Selecting the AI model failed because {provider.Name} is disabled.");

        var model = provider.Models.FirstOrDefault(model => model.Id == modelId)
            ?? throw new InvalidOperationException("Selecting the AI model failed because the model is not available.");
        if (!AiProviderModelSelectionRules.IsSelectedForRole(model, role))
            throw new InvalidOperationException($"Selecting the AI model failed because {DisplayName(model)} is not enabled for {AiProviderModelSelectionRules.Label(role)}.");

        Document.ActiveModelSelections.Values[role] = new()
        {
            ProviderId = providerId,
            ModelId = modelId
        };
        await SaveActiveDocumentAsync();
    }

    public async Task ClearActiveModelAsync(AiModelRole role)
    {
        if (Document is null)
            return;

        Document.ActiveModelSelections.Values.Remove(role);
        await SaveActiveDocumentAsync();
    }

    public async Task NormalizeAsync()
    {
        if (Document is null)
            return;

        var invalid = Document.ActiveModelSelections.Values
            .Where(pair => !IsValid(pair.Key, pair.Value))
            .Select(pair => pair.Key)
            .ToList();
        if (invalid.Count == 0)
            return;

        foreach (var role in invalid)
            Document.ActiveModelSelections.Values.Remove(role);

        await SaveActiveDocumentAsync();
    }

    bool IsValid(AiModelRole role, ActiveModelSelectionState selection)
    {
        var provider = providers.Items.FirstOrDefault(provider => provider.Id == selection.ProviderId);
        var model = provider?.Models.FirstOrDefault(model => model.Id == selection.ModelId);
        return provider?.Enabled == true && model is not null && AiProviderModelSelectionRules.IsSelectedForRole(model, role);
    }

    static string DisplayName(AiProviderModel model) =>
        string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName;
}
