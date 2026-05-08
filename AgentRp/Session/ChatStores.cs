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
            CharacterRelationshipGraph.RemoveCharacter(Document, id);
            RemoveCharacterReferences(Document.Transcript.RootScene, id);
            RemoveCharacterReferences(Document.Transcript.WorkingScene.Scene, id);
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

        var scene = TranscriptGraph.GetEditableWorkingScene(Document.Transcript);
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
            UpdateScene(Document.Transcript.WorkingScene.Scene, id, replacement);
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

        var scene = TranscriptGraph.GetEditableWorkingScene(Document.Transcript);
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
            Document.Transcript.WorkingScene.Scene.InSceneItemIds.RemoveAll(itemId => itemId == id);
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

        var scene = TranscriptGraph.GetEditableWorkingScene(Document.Transcript);
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
    ModelSelectionStore modelSelection,
    TranscriptStore transcript,
    IStoryAssistantService? storyAssistantService) : ActiveChatStoreBase(activeChat, registry), IStoryAssistantCallbacks
{
    static readonly IReadOnlyDictionary<string, string> EmptyPromptValues = new Dictionary<string, string>(StringComparer.Ordinal);
    readonly object _operationLock = new();
    CancellationTokenSource? _activeRunCancellation;

    protected override RoleplayStoreArea Area => RoleplayStoreArea.StoryAssistant;

    public StoryAssistantState State
    {
        get
        {
            var state = Document?.StoryAssistant ?? new();
            state.EnsureActiveChat();
            SyncWorkItemTranscriptItems(state);
            if (!IsBusy)
                RecoverIdleStreamingMessages(state);

            return state;
        }
    }
    public IReadOnlyList<StoryAssistantTranscriptItem> Items => State.Items;
    public IReadOnlyList<StoryAssistantChat> Chats => State.Chats;
    public StoryAssistantChat ActiveAssistantChat => State.ActiveChat;
    public bool IsBusy { get; private set; }
    public string BusyMessage { get; private set; } = "";

    protected override bool ShouldHandleArea(RoleplayStoreArea? changedArea) => changedArea is null || changedArea == Area;

    public async Task SetReviewModeAsync(StoryAssistantReviewMode mode)
    {
        State.ReviewMode = mode;
        await SaveAssistantStateAsync(CancellationToken.None);
        await NotifyChangedAsync();
    }

    public async Task RecoverIdleStreamingMessagesAsync()
    {
        if (IsBusy || Document is null)
            return;

        if (!RecoverIdleStreamingMessages(Document.StoryAssistant))
            return;

        await SaveAssistantStateAsync(CancellationToken.None);
        await NotifyChangedAsync();
    }

    public async Task CreateChatAsync()
    {
        var state = State;
        var now = DateTime.UtcNow;
        var chat = new StoryAssistantChat
        {
            Id = $"assistant-chat-{Guid.NewGuid():N}",
            Title = "New chat",
            CreatedUtc = now,
            UpdatedUtc = now
        };
        state.Chats.Insert(0, chat);
        state.ActiveChatId = chat.Id;
        await SaveAssistantStateAsync(CancellationToken.None);
        await NotifyChangedAsync();
    }

    public async Task SelectChatAsync(string chatId)
    {
        var state = State;
        if (!state.Chats.Any(chat => chat.Id == chatId))
            return;

        state.ActiveChatId = chatId;
        if (RecoverIdleStreamingMessages(state))
            await SaveAssistantStateAsync(CancellationToken.None);

        await NotifyChangedAsync();
    }

    public async Task RenameChatAsync(string chatId, string title)
    {
        var chat = State.Chats.FirstOrDefault(chat => chat.Id == chatId);
        if (chat is null)
            return;

        chat.Title = CleanTitle(title);
        chat.UpdatedUtc = DateTime.UtcNow;
        await SaveAssistantStateAsync(CancellationToken.None);
        await NotifyChangedAsync();
    }

    public async Task DeleteChatAsync(string chatId)
    {
        if (Document is null)
            return;

        var state = State;
        if (state.Chats.Count <= 1)
            return;

        var chat = state.Chats.FirstOrDefault(chat => chat.Id == chatId);
        if (chat is null)
            return;

        ClearBackgroundError();
        await ClearRemoteStateForChatAsync(chat);
        state.Chats.Remove(chat);
        if (state.ActiveChatId == chat.Id)
            state.ActiveChatId = state.Chats.OrderByDescending(chat => chat.UpdatedUtc).First().Id;

        await SaveAssistantStateAsync(CancellationToken.None);
        await NotifyChangedAsync();
    }

    public Task ClearAsync() => ClearActiveChatAsync();

    public async Task ClearActiveChatAsync()
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        try
        {
            if (storyAssistantService is not null)
                await storyAssistantService.ClearRemoteStateAsync(Document, providers.Items.ToList(), modelSelection.State, CancellationToken.None);
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception);
        }

        State.Items.Clear();
        State.WorkItems.Clear();
        State.ActiveChat.UpdatedUtc = DateTime.UtcNow;
        StoryAssistantService.ClearResponseChain(State);
        await SaveAssistantStateAsync(CancellationToken.None);
        await NotifyChangedAsync();
    }

    public Task SendAsync(string text, CancellationToken cancellationToken = default) =>
        SendAsync(text, text, cancellationToken);

    public Task SendWithModelInputAsync(string displayMessage, string modelInput, CancellationToken cancellationToken = default) =>
        SendAsync(displayMessage, modelInput, cancellationToken);

    public Task SendWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        if (Document is null)
            return Task.CompletedTask;

        var workflow = StoryAssistantWorkflowCatalog.Find(workflowId)
            ?? throw new InvalidOperationException($"Starting Story Assistant workflow failed because '{workflowId}' is not a supported workflow.");
        var prompt = PromptLibraryService.RenderStage(Document.PromptLibrary, workflow.PromptStageId, EmptyPromptValues);
        var modelInput = string.IsNullOrWhiteSpace(prompt.UserPrompt) ? workflow.DisplayMessage : prompt.UserPrompt;
        return SendAsync(workflow.DisplayMessage, modelInput, cancellationToken);
    }

    public Task RetryAsync(string itemId, CancellationToken cancellationToken = default)
    {
        var item = State.Items.FirstOrDefault(item => item.Id == itemId);
        if (item?.Retry.CanRetry != true)
            return Task.CompletedTask;

        var display = string.IsNullOrWhiteSpace(item.Retry.DisplayMessage)
            ? "Retry Story Assistant"
            : $"Retry: {item.Retry.DisplayMessage}";
        var modelInput = $"""
            Continue from the previous Story Assistant request.
            If the last attempt returned no useful output, resume by asking one focused question or taking the next appropriate tool action.

            Original request:
            {item.Retry.ModelInput}
            """;
        return SendAsync(display, modelInput, cancellationToken, isRetry: true);
    }

    async Task SendAsync(string displayMessage, string modelInput, CancellationToken cancellationToken, bool isRetry = false)
    {
        if (Document is null || string.IsNullOrWhiteSpace(modelInput) || storyAssistantService is null)
            return;

        displayMessage = string.IsNullOrWhiteSpace(displayMessage) ? modelInput : displayMessage;
        if (RecoverIdleStreamingMessages(Document.StoryAssistant))
            await SaveAssistantStateAsync(cancellationToken);

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
        AutoTitleActiveChat(displayMessage);
        var retry = new StoryAssistantRetryContext
        {
            DisplayMessage = displayMessage.Trim(),
            ModelInput = modelInput.Trim(),
            IsRetry = isRetry
        };
        State.Items.Add(AddMessage(StoryAssistantItemKind.UserMessage, StoryAssistantItemStatus.Applied, displayMessage.Trim()));
        var assistantItem = AddMessage(StoryAssistantItemKind.AssistantMessage, StoryAssistantItemStatus.Streaming, "");
        assistantItem.Retry = Clone(retry);
        assistantItem.Diagnostics = BuildDiagnostics("Running", "Story Assistant turn started.", displayMessage);
        State.Items.Add(assistantItem);
        State.ActiveChat.UpdatedUtc = DateTime.UtcNow;
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();

        await RunAssistantAsync(
            StoryAssistantTurnRequest.Start(modelInput, displayMessage),
            retry,
            displayMessage,
            State.Items.Count - 1,
            runCancellation);
    }

    async Task RunAssistantAsync(
        StoryAssistantTurnRequest request,
        StoryAssistantRetryContext retry,
        string displayMessage,
        int firstRunItemIndex,
        CancellationTokenSource runCancellation,
        string workItemId = "")
    {
        var runToken = runCancellation.Token;
        try
        {
            await storyAssistantService!.RunTurnAsync(
                Document!,
                providers.Items.ToList(),
                modelSelection.State,
                request,
                this,
                runToken);
            CompleteStreamingAssistantMessages(firstRunItemIndex, retry, displayMessage);
            await SaveAssistantStateAsync(runToken);
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
            MarkStopped(retry, displayMessage);
            if (!string.IsNullOrWhiteSpace(workItemId))
                MarkWorkItemCancelled(workItemId);

            await SaveAssistantStateAsync(CancellationToken.None);
        }
        catch (ModelAssistantThreadLostException exception)
        {
            CaptureBackgroundError(exception);
            State.RemoteThreadLost = true;
            State.RemoteThreadError = UserFacingErrorMessageBuilder.Build("Story Assistant needs a fresh thread.", exception);
            FailCurrentAssistantMessage(State.RemoteThreadError, retry, displayMessage, exception);
            if (!string.IsNullOrWhiteSpace(workItemId))
                MarkWorkItemFailed(workItemId, State.RemoteThreadError);

            await SaveAssistantStateAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception);
            var message = UserFacingErrorMessageBuilder.Build("Story Assistant failed.", exception);
            FailCurrentAssistantMessage(message, retry, displayMessage, exception);
            if (!string.IsNullOrWhiteSpace(workItemId))
                MarkWorkItemFailed(workItemId, message);

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

            if (RecoverIdleStreamingMessages(Document?.StoryAssistant))
                await SaveAssistantStateAsync(CancellationToken.None);

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

        await SaveAssistantStateAsync(CancellationToken.None);
        await NotifyChangedAsync();
    }

    async Task ClearRemoteStateForChatAsync(StoryAssistantChat chat)
    {
        if (Document is null || storyAssistantService is null)
            return;

        var state = Document.StoryAssistant;
        var previousChatId = state.ActiveChatId;
        try
        {
            state.ActiveChatId = chat.Id;
            await storyAssistantService.ClearRemoteStateAsync(Document, providers.Items.ToList(), modelSelection.State, CancellationToken.None);
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception);
        }
        finally
        {
            state.ActiveChatId = previousChatId;
        }
    }

    void AutoTitleActiveChat(string displayMessage)
    {
        var chat = State.ActiveChat;
        if (!string.Equals(chat.Title, "New chat", StringComparison.Ordinal) || chat.Items.Count > 0)
            return;

        chat.Title = CleanTitle(displayMessage);
    }

    static string CleanTitle(string title)
    {
        var clean = string.Join(' ', title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(clean))
            return "New chat";

        return clean.Length <= 44 ? clean : $"{clean[..41]}...";
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
        State.ActiveChat.UpdatedUtc = item.UpdatedUtc;
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();
    }

    public async Task RecordToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken)
    {
        CloseTrailingAssistantMessage();
        item.UpdatedUtc = DateTime.UtcNow;
        State.Items.Add(item);
        State.ActiveChat.UpdatedUtc = item.UpdatedUtc;
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();
    }

    public async Task UpdateToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken)
    {
        item.UpdatedUtc = DateTime.UtcNow;
        State.ActiveChat.UpdatedUtc = item.UpdatedUtc;
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();
    }

    public async Task RecordWorkItemAsync(StoryAssistantWorkItem workItem, CancellationToken cancellationToken)
    {
        CloseTrailingAssistantMessage();
        workItem.UpdatedUtc = DateTime.UtcNow;
        State.WorkItems.Add(workItem);
        State.Items.Add(TranscriptItemFor(workItem));
        State.ActiveChat.UpdatedUtc = workItem.UpdatedUtc;
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();
    }

    public async Task UpdateWorkItemAsync(StoryAssistantWorkItem workItem, CancellationToken cancellationToken)
    {
        workItem.UpdatedUtc = DateTime.UtcNow;
        var index = State.WorkItems.FindIndex(item => item.Id == workItem.Id);
        if (index >= 0)
            State.WorkItems[index] = workItem;
        else
            State.WorkItems.Add(workItem);

        var transcriptItem = State.Items.FirstOrDefault(item => item.WorkItemId == workItem.Id || item.Id == workItem.TranscriptItemId);
        if (transcriptItem is not null)
            SyncTranscriptItem(transcriptItem, workItem);

        State.ActiveChat.UpdatedUtc = workItem.UpdatedUtc;
        await SaveAssistantStateAsync(cancellationToken);
        await NotifyChangedAsync();
    }

    public Task ResolveReviewAsync(string itemId, StoryAssistantDecisionKind kind, string reason) =>
        ResolveWorkItemAsync(itemId, new(ToResolutionKind(kind), "", reason));

    public Task ResolveQuestionAsync(string itemId, string answer) =>
        ResolveWorkItemAsync(itemId, new(StoryAssistantWorkItemResolutionKind.Answer, answer, ""));

    async Task ResolveWorkItemAsync(string itemId, StoryAssistantWorkItemResolution resolution)
    {
        if (Document is null || storyAssistantService is null)
            return;

        var workItem = WorkItemFor(itemId);
        if (workItem is null || workItem.Status != StoryAssistantWorkItemStatus.Pending)
            return;

        if (string.IsNullOrWhiteSpace(workItem.AwaitingResponseId))
        {
            MarkWorkItemFailed(workItem.Id, "This assistant action cannot continue because its saved Responses continuation is missing.");
            await SaveAssistantStateAsync(CancellationToken.None);
            await NotifyChangedAsync();
            return;
        }

        var activeModel = modelSelection.Resolve(AiModelRole.Reasoning);
        if (activeModel is null
            || !string.Equals(activeModel.Provider.Id, workItem.ResponseProviderId, StringComparison.Ordinal)
            || !string.Equals(activeModel.Model.Id, workItem.ResponseModelId, StringComparison.Ordinal))
        {
            MarkWorkItemFailed(workItem.Id, "This assistant action cannot continue because the active reasoning model does not match the saved Responses continuation.");
            await SaveAssistantStateAsync(CancellationToken.None);
            await NotifyChangedAsync();
            return;
        }

        CancellationTokenSource runCancellation;
        lock (_operationLock)
        {
            if (IsBusy)
                return;

            IsBusy = true;
            BusyMessage = "Thinking...";
            runCancellation = new();
            _activeRunCancellation = runCancellation;
        }

        ClearBackgroundError();
        try
        {
            await storyAssistantService.ResolveWorkItemAsync(Document, workItem, resolution, this, runCancellation.Token);
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
            MarkWorkItemCancelled(workItem.Id);
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception);
            MarkWorkItemFailed(workItem.Id, UserFacingErrorMessageBuilder.Build("Resolving Story Assistant action failed.", exception));
        }

        if (string.IsNullOrWhiteSpace(workItem.ResultJson) || workItem.Status is StoryAssistantWorkItemStatus.Failed)
        {
            ReleaseRunCancellation(runCancellation);
            await NotifyChangedAsync();
            return;
        }

        var assistantItem = AddMessage(StoryAssistantItemKind.AssistantMessage, StoryAssistantItemStatus.Streaming, "");
        assistantItem.Diagnostics = BuildDiagnostics("Running", "Story Assistant action resumed.", workItem.Title);
        State.Items.Add(assistantItem);
        await SaveAssistantStateAsync(runCancellation.Token);
        await NotifyChangedAsync();

        await RunAssistantAsync(
            StoryAssistantTurnRequest.Resume(workItem),
            new(),
            workItem.Title,
            State.Items.Count - 1,
            runCancellation,
            workItem.Id);
    }

    void ReleaseRunCancellation(CancellationTokenSource runCancellation)
    {
        lock (_operationLock)
        {
            IsBusy = false;
            BusyMessage = "";
            if (ReferenceEquals(_activeRunCancellation, runCancellation))
                _activeRunCancellation = null;
        }

        runCancellation.Dispose();
    }

    public async Task<SceneTransitionResult> SetSceneAsync(SetSceneRequest request, CancellationToken cancellationToken)
    {
        var transition = await transcript.SetSceneAsync(request, cancellationToken)
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

    void CompleteStreamingAssistantMessages(int firstRunItemIndex, StoryAssistantRetryContext retry, string displayMessage)
    {
        for (var index = State.Items.Count - 1; index >= 0; index--)
        {
            var item = State.Items[index];
            if (item is not { Kind: StoryAssistantItemKind.AssistantMessage, Status: StoryAssistantItemStatus.Streaming })
                continue;

            if (string.IsNullOrWhiteSpace(item.Text))
            {
                if (index == firstRunItemIndex)
                {
                    item.Status = StoryAssistantItemStatus.Failed;
                    item.Text = "The model finished without returning a message or action.";
                    item.Retry = Clone(retry);
                    item.Diagnostics = BuildDiagnostics("No output", "The Responses stream completed without text or tool calls.", displayMessage, "Completed");
                    item.UpdatedUtc = DateTime.UtcNow;
                }
                else
                    State.Items.RemoveAt(index);
            }
            else
            {
                item.Status = StoryAssistantItemStatus.Applied;
                item.Diagnostics = BuildDiagnostics("Completed", "The assistant returned text.", displayMessage, "TextDelta");
                item.UpdatedUtc = DateTime.UtcNow;
            }
        }
    }

    static bool RecoverIdleStreamingMessages(StoryAssistantState? state)
    {
        if (state is null)
            return false;

        var changed = false;
        for (var index = state.Items.Count - 1; index >= 0; index--)
        {
            var item = state.Items[index];
            if (item is not { Kind: StoryAssistantItemKind.AssistantMessage, Status: StoryAssistantItemStatus.Streaming })
                continue;

            changed = true;
            if (string.IsNullOrWhiteSpace(item.Text))
            {
                if (item.Retry.CanRetry)
                {
                    item.Status = StoryAssistantItemStatus.Failed;
                    item.Text = "The assistant run ended before returning a message or action.";
                    item.Diagnostics.Outcome = "Interrupted";
                    item.Diagnostics.Reason = "A saved streaming placeholder was recovered while the assistant was idle.";
                    item.Diagnostics.LastStreamEvent = string.IsNullOrWhiteSpace(item.Diagnostics.LastStreamEvent) ? "Unknown" : item.Diagnostics.LastStreamEvent;
                    item.Diagnostics.RecordedUtc = DateTime.UtcNow;
                    item.UpdatedUtc = DateTime.UtcNow;
                }
                else
                    state.Items.RemoveAt(index);
            }
            else
            {
                item.Status = StoryAssistantItemStatus.Stopped;
                item.Diagnostics.Outcome = "Interrupted";
                item.Diagnostics.Reason = "A partial streaming assistant message was recovered while the assistant was idle.";
                item.Diagnostics.LastStreamEvent = string.IsNullOrWhiteSpace(item.Diagnostics.LastStreamEvent) ? "TextDelta" : item.Diagnostics.LastStreamEvent;
                item.Diagnostics.RecordedUtc = DateTime.UtcNow;
                item.UpdatedUtc = DateTime.UtcNow;
            }
        }

        return changed;
    }

    static void SyncWorkItemTranscriptItems(StoryAssistantState state)
    {
        foreach (var workItem in state.WorkItems)
        {
            var transcriptItem = state.Items.FirstOrDefault(item => item.WorkItemId == workItem.Id || item.Id == workItem.TranscriptItemId);
            if (transcriptItem is not null)
                SyncTranscriptItem(transcriptItem, workItem);
        }
    }

    void FailCurrentAssistantMessage(string message, StoryAssistantRetryContext retry, string displayMessage, Exception exception)
    {
        if (State.Items.LastOrDefault() is { Kind: StoryAssistantItemKind.AssistantMessage, Status: StoryAssistantItemStatus.Streaming } item)
        {
            item.Status = StoryAssistantItemStatus.Failed;
            item.Text = message;
            item.Retry = Clone(retry);
            item.Diagnostics = BuildDiagnostics("Failed", "The Story Assistant run raised an exception.", displayMessage, "Exception", exception);
            item.UpdatedUtc = DateTime.UtcNow;
            return;
        }

        var failed = AddMessage(StoryAssistantItemKind.AssistantMessage, StoryAssistantItemStatus.Failed, message);
        failed.Retry = Clone(retry);
        failed.Diagnostics = BuildDiagnostics("Failed", "The Story Assistant run raised an exception.", displayMessage, "Exception", exception);
        State.Items.Add(failed);
    }

    void MarkStopped(StoryAssistantRetryContext retry, string displayMessage)
    {
        if (State.Items.LastOrDefault() is { Kind: StoryAssistantItemKind.AssistantMessage, Status: StoryAssistantItemStatus.Streaming } item)
        {
            if (string.IsNullOrWhiteSpace(item.Text))
                item.Text = "Stopped.";

            item.Status = StoryAssistantItemStatus.Stopped;
            item.Retry = Clone(retry);
            item.Diagnostics = BuildDiagnostics("Stopped", "The assistant run was stopped before it completed.", displayMessage, "Cancelled");
            item.UpdatedUtc = DateTime.UtcNow;
            return;
        }

        var stopped = AddMessage(StoryAssistantItemKind.AssistantMessage, StoryAssistantItemStatus.Stopped, "Stopped.");
        stopped.Retry = Clone(retry);
        stopped.Diagnostics = BuildDiagnostics("Stopped", "The assistant run was stopped before it completed.", displayMessage, "Cancelled");
        State.Items.Add(stopped);
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

    StoryAssistantDiagnostics BuildDiagnostics(
        string outcome,
        string reason,
        string displayMessage,
        string lastStreamEvent = "",
        Exception? exception = null)
    {
        var activeModel = modelSelection.Resolve(AiModelRole.Reasoning);
        return new()
        {
            Outcome = outcome,
            Reason = reason,
            ProviderId = activeModel?.Provider.Id ?? "",
            ProviderName = activeModel?.Provider.Name ?? "",
            ModelId = activeModel?.Model.Id ?? "",
            ModelName = activeModel?.Model.DisplayName ?? "",
            PreviousResponseId = Document?.StoryAssistant.ResponseIds.LastOrDefault(responseId => responseId != Document.StoryAssistant.LastResponseId) ?? "",
            ResponseId = Document?.StoryAssistant.LastResponseId ?? "",
            RequestDisplay = displayMessage,
            LastStreamEvent = lastStreamEvent,
            Error = exception is null ? "" : UserFacingErrorMessageBuilder.Build("Story Assistant failed.", exception),
            RecordedUtc = DateTime.UtcNow
        };
    }

    StoryAssistantWorkItem? WorkItemFor(string itemId) =>
        State.WorkItems.FirstOrDefault(item =>
            string.Equals(item.Id, itemId, StringComparison.Ordinal)
            || string.Equals(item.TranscriptItemId, itemId, StringComparison.Ordinal));

    void MarkWorkItemCancelled(string workItemId)
    {
        var workItem = WorkItemFor(workItemId);
        if (workItem is null)
            return;

        workItem.Status = StoryAssistantWorkItemStatus.Cancelled;
        workItem.DecisionReason = "Stopped before the assistant could continue.";
        workItem.UpdatedUtc = DateTime.UtcNow;
        SyncTranscriptFor(workItem);
    }

    void MarkWorkItemFailed(string workItemId, string message)
    {
        var workItem = WorkItemFor(workItemId);
        if (workItem is null)
            return;

        workItem.Status = StoryAssistantWorkItemStatus.Failed;
        workItem.DecisionReason = message;
        workItem.UpdatedUtc = DateTime.UtcNow;
        SyncTranscriptFor(workItem);
    }

    void SyncTranscriptFor(StoryAssistantWorkItem workItem)
    {
        var transcriptItem = State.Items.FirstOrDefault(item => item.WorkItemId == workItem.Id || item.Id == workItem.TranscriptItemId);
        if (transcriptItem is not null)
            SyncTranscriptItem(transcriptItem, workItem);
    }

    static StoryAssistantTranscriptItem TranscriptItemFor(StoryAssistantWorkItem workItem)
    {
        var item = new StoryAssistantTranscriptItem
        {
            Id = workItem.TranscriptItemId,
            Kind = workItem.Kind == StoryAssistantWorkItemKind.Question ? StoryAssistantItemKind.Question : StoryAssistantItemKind.ToolCall,
            CreatedUtc = workItem.CreatedUtc,
            WorkItemId = workItem.Id
        };
        SyncTranscriptItem(item, workItem);
        return item;
    }

    static void SyncTranscriptItem(StoryAssistantTranscriptItem item, StoryAssistantWorkItem workItem)
    {
        item.Status = TranscriptStatusFor(workItem);
        item.UpdatedUtc = workItem.UpdatedUtc;
        item.Title = workItem.Title;
        item.ToolName = workItem.ToolName;
        item.ToolCallId = workItem.ToolCallId;
        item.WorkItemId = workItem.Id;
        item.Operation = workItem.Operation;
        item.EntityType = workItem.EntityType;
        item.EntityId = workItem.EntityId;
        item.EntityName = workItem.EntityName;
        item.ArgumentsJson = workItem.ArgumentsJson;
        item.ResultJson = workItem.ResultJson;
        item.Before = workItem.Before;
        item.After = workItem.After;
        item.Diffs = workItem.Diffs;
        item.Risk = workItem.Risk;
        item.DecisionReason = workItem.DecisionReason;
        item.Question = workItem.Question;
        item.Diagnostics = workItem.Diagnostics;
    }

    static StoryAssistantItemStatus TranscriptStatusFor(StoryAssistantWorkItem workItem) => workItem.Status switch
    {
        StoryAssistantWorkItemStatus.Pending when workItem.Kind == StoryAssistantWorkItemKind.Question => StoryAssistantItemStatus.Pending,
        StoryAssistantWorkItemStatus.Pending => StoryAssistantItemStatus.NeedsReview,
        StoryAssistantWorkItemStatus.Completed when workItem.Kind == StoryAssistantWorkItemKind.Question => StoryAssistantItemStatus.Answered,
        StoryAssistantWorkItemStatus.Completed => StoryAssistantItemStatus.Accepted,
        StoryAssistantWorkItemStatus.RetryRequested => StoryAssistantItemStatus.RetryRequested,
        StoryAssistantWorkItemStatus.Rejected => StoryAssistantItemStatus.Rejected,
        StoryAssistantWorkItemStatus.Cancelled => StoryAssistantItemStatus.Stopped,
        StoryAssistantWorkItemStatus.Conflict or StoryAssistantWorkItemStatus.Failed => StoryAssistantItemStatus.Failed,
        _ => StoryAssistantItemStatus.Pending
    };

    static StoryAssistantWorkItemResolutionKind ToResolutionKind(StoryAssistantDecisionKind kind) => kind switch
    {
        StoryAssistantDecisionKind.Accept => StoryAssistantWorkItemResolutionKind.Accept,
        StoryAssistantDecisionKind.TryAgain => StoryAssistantWorkItemResolutionKind.TryAgain,
        StoryAssistantDecisionKind.Reject => StoryAssistantWorkItemResolutionKind.Reject,
        _ => StoryAssistantWorkItemResolutionKind.Reject
    };

    static StoryAssistantRetryContext Clone(StoryAssistantRetryContext value) => new()
    {
        DisplayMessage = value.DisplayMessage,
        ModelInput = value.ModelInput,
        IsRetry = value.IsRetry
    };

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
