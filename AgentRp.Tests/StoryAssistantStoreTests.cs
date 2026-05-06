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

    static StoryAssistantStore CreateStore(RpChatDocument document, Func<IStoryAssistantCallbacks, Task> script) =>
        CreateStore(document, (callbacks, _) => script(callbacks));

    static StoryAssistantStore CreateStore(RpChatDocument document, Func<IStoryAssistantCallbacks, CancellationToken, Task> script)
    {
        var activeChat = new ActiveChatContext();
        activeChat.SetAsync(document).GetAwaiter().GetResult();
        var liveStore = new TestLiveRoleplayStore(document);
        var registry = new ChatRegistry(Guid.NewGuid(), liveStore, activeChat);
        var providers = new ProviderStore(Guid.NewGuid(), liveStore);
        return new(activeChat, registry, providers, new ScriptedStoryAssistantService(script));
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

    sealed class ScriptedStoryAssistantService(Func<IStoryAssistantCallbacks, CancellationToken, Task> script) : IStoryAssistantService
    {
        public Task RunTurnAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            StoryAssistantTurnRequest request,
            IStoryAssistantCallbacks callbacks,
            CancellationToken cancellationToken = default) => script(callbacks, cancellationToken);
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

        public Task<IReadOnlyList<RpChat>> AddChatAsync(Guid originSessionId, string location, RpChatDocument? template, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RpChat>>([document.Chat]);

        public Task ReplaceProvidersAsync(Guid originSessionId, IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReplaceChatAreaAsync(Guid originSessionId, string chatId, RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default) =>
            Changed?.Invoke(new(originSessionId, chatId, area, 1)) ?? Task.CompletedTask;
    }
}
