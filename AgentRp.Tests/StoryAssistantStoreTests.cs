using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Tests;

public sealed class StoryAssistantStoreTests
{
    [Fact]
    public async Task SendStartsWithThinkingPlaceholderAndToolFirstReplacesIt()
    {
        var document = CreateDocument();
        var store = CreateStore(document, async callbacks =>
        {
            Assert.Equal(2, document.StoryAssistant.Items.Count);
            Assert.Equal(StoryAssistantItemKind.AssistantMessage, document.StoryAssistant.Items[1].Kind);
            Assert.Equal(StoryAssistantItemStatus.Streaming, document.StoryAssistant.Items[1].Status);
            Assert.Equal("", document.StoryAssistant.Items[1].Text);

            await callbacks.RecordToolCallAsync(ReadTool(), CancellationToken.None);
        });

        await store.SendAsync("Read the current canon.");

        Assert.Equal(2, document.StoryAssistant.Items.Count);
        Assert.Equal(StoryAssistantItemKind.UserMessage, document.StoryAssistant.Items[0].Kind);
        Assert.Equal(StoryAssistantItemKind.ToolCall, document.StoryAssistant.Items[1].Kind);
        Assert.Equal(StoryAssistantItemStatus.Read, document.StoryAssistant.Items[1].Status);
    }

    [Fact]
    public async Task TextAfterToolCallStartsNewAssistantBubble()
    {
        var document = CreateDocument();
        var store = CreateStore(document, async callbacks =>
        {
            await callbacks.AppendAssistantTextAsync("First thought.", CancellationToken.None);
            await callbacks.RecordToolCallAsync(ReadTool(), CancellationToken.None);
            await callbacks.AppendAssistantTextAsync("Second thought.", CancellationToken.None);
        });

        await store.SendAsync("Plan the cast.");

        Assert.Equal(
            [
                StoryAssistantItemKind.UserMessage,
                StoryAssistantItemKind.AssistantMessage,
                StoryAssistantItemKind.ToolCall,
                StoryAssistantItemKind.AssistantMessage
            ],
            document.StoryAssistant.Items.Select(item => item.Kind).ToArray());
        Assert.Equal("First thought.", document.StoryAssistant.Items[1].Text);
        Assert.Equal(StoryAssistantItemStatus.Applied, document.StoryAssistant.Items[1].Status);
        Assert.Equal("Second thought.", document.StoryAssistant.Items[3].Text);
        Assert.Equal(StoryAssistantItemStatus.Applied, document.StoryAssistant.Items[3].Status);
    }

    [Fact]
    public async Task StopCancelsActiveRunWithCalmStoppedMessage()
    {
        var document = CreateDocument();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = CreateStore(document, async (_, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });

        var sendTask = store.SendAsync("Keep working.");
        await started.Task;

        await store.StopAsync();
        await sendTask;

        Assert.False(store.IsBusy);
        Assert.Equal(StoryAssistantItemKind.AssistantMessage, document.StoryAssistant.Items.Last().Kind);
        Assert.Equal(StoryAssistantItemStatus.Stopped, document.StoryAssistant.Items.Last().Status);
        Assert.Equal("Stopped.", document.StoryAssistant.Items.Last().Text);
    }

    [Fact]
    public async Task SendMarksRemoteThreadLostWhenProviderDropsStoredResponse()
    {
        var document = CreateDocument();
        var store = CreateStore(document, (_, _) =>
            throw new ModelAssistantThreadLostException(
                "Grok / xAI",
                "grok-4.3",
                "resp-old",
                new InvalidOperationException("The requested resource was not found.")));

        await store.SendAsync("Keep going.");

        Assert.True(document.StoryAssistant.RemoteThreadLost);
        Assert.Contains("fresh thread", document.StoryAssistant.RemoteThreadError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(StoryAssistantItemStatus.Failed, document.StoryAssistant.Items.Last().Status);
        Assert.Contains("fresh thread", document.StoryAssistant.Items.Last().Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearResetsLocalStateAfterRemoteCleanup()
    {
        var document = CreateDocument();
        document.StoryAssistant.LastResponseId = "resp-2";
        document.StoryAssistant.ResponseIds.Add("resp-1");
        document.StoryAssistant.ResponseIds.Add("resp-2");
        document.StoryAssistant.ResponseProviderId = "provider-1";
        document.StoryAssistant.ResponseModelId = "model-1";
        document.StoryAssistant.RemoteThreadLost = true;
        document.StoryAssistant.RemoteThreadError = "Needs restart.";
        document.StoryAssistant.Items.Add(ReadTool());
        var cleanupCalled = false;
        var store = CreateStore(
            document,
            (_, _) => Task.CompletedTask,
            (cleanupDocument, _, _) =>
            {
                cleanupCalled = ReferenceEquals(document, cleanupDocument);
                return Task.CompletedTask;
            });

        await store.ClearAsync();

        Assert.True(cleanupCalled);
        Assert.Empty(document.StoryAssistant.Items);
        Assert.Equal("", document.StoryAssistant.LastResponseId);
        Assert.Empty(document.StoryAssistant.ResponseIds);
        Assert.False(document.StoryAssistant.RemoteThreadLost);
        Assert.Equal("", document.StoryAssistant.RemoteThreadError);
    }

    static StoryAssistantStore CreateStore(RpChatDocument document, Func<IStoryAssistantCallbacks, Task> script) =>
        CreateStore(document, (callbacks, _) => script(callbacks));

    static StoryAssistantStore CreateStore(
        RpChatDocument document,
        Func<IStoryAssistantCallbacks, CancellationToken, Task> script,
        Func<RpChatDocument, IReadOnlyList<AiProvider>, CancellationToken, Task>? clearScript = null)
    {
        var activeChat = new ActiveChatContext();
        activeChat.SetAsync(document).GetAwaiter().GetResult();
        var liveStore = new TestLiveRoleplayStore(document);
        var registry = new ChatRegistry(Guid.NewGuid(), liveStore, activeChat);
        var providers = new ProviderStore(Guid.NewGuid(), liveStore);
        var modelSelection = new ModelSelectionStore(providers, new GlobalModelSelectionStore(new InMemoryAppSettingsService()));
        var transcript = new TranscriptStore(activeChat, registry, providers, modelSelection, NullTextGenerationService.Instance, new SceneTransitionService());
        return new(activeChat, registry, providers, modelSelection, transcript, new ScriptedStoryAssistantService(script, clearScript));
    }

    static RpChatDocument CreateDocument() => new()
    {
        Chat = new() { Id = "chat-1" }
    };

    static StoryAssistantTranscriptItem ReadTool() => new()
    {
        Id = $"tool-{Guid.NewGuid():N}",
        Kind = StoryAssistantItemKind.ToolCall,
        Status = StoryAssistantItemStatus.Read,
        Title = "Read story entities",
        ToolName = "get_story_entities",
        ToolCallId = "call-1"
    };

    sealed class ScriptedStoryAssistantService(
        Func<IStoryAssistantCallbacks, CancellationToken, Task> script,
        Func<RpChatDocument, IReadOnlyList<AiProvider>, CancellationToken, Task>? clearScript) : IStoryAssistantService
    {
        public Task RunTurnAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            StoryAssistantTurnRequest request,
            IStoryAssistantCallbacks callbacks,
            CancellationToken cancellationToken = default) => script(callbacks, cancellationToken);

        public Task ClearRemoteStateAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            CancellationToken cancellationToken = default) =>
            clearScript?.Invoke(document, providers, cancellationToken) ?? Task.CompletedTask;
    }

    sealed class TestLiveRoleplayStore(RpChatDocument document) : ILiveRoleplayStore
    {
        public event Func<RoleplayStoreNotification, Task>? Changed;

        public Task<IReadOnlyList<RpChat>> LoadChatsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RpChat>>([document.Chat]);

        public Task<IReadOnlyList<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiProvider>>([]);

        public Task<RpChatDocument> OpenChatAsync(Guid sessionId, string chatId, CancellationToken cancellationToken = default) =>
            Task.FromResult(document);

        public void ReleaseChat(Guid sessionId, string? chatId)
        {
        }

        public Task<RpChatDocument> GetChatSnapshotAsync(string chatId, CancellationToken cancellationToken = default) =>
            Task.FromResult(document);

        public Task<IReadOnlyList<RpChat>> AddChatAsync(Guid originSessionId, StoryCreationOptions options, RpChatDocument? template, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RpChat>>([document.Chat]);

        public Task ReplaceProvidersAsync(Guid originSessionId, IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReplaceChatAreaAsync(Guid originSessionId, string chatId, RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default) =>
            Changed?.Invoke(new(originSessionId, chatId, area, 1)) ?? Task.CompletedTask;
    }
}
