using System.Text.Json;
using System.Text.Json.Nodes;
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

    [Fact]
    public async Task WorkflowSendUsesPromptGuidanceAsModelInputAndCleanDisplayTextInTranscript()
    {
        var document = CreateDocument();
        document.PromptLibrary = PromptLibraryService.CreateDefaultState();
        document.PromptLibrary.Prompts[PromptLibraryStageIds.StoryAssistantPrepareStory].User = "Custom prepare workflow guidance.";
        StoryAssistantTurnRequest? captured = null;
        var store = CreateStore(document, (request, _, _) =>
        {
            captured = request;
            return Task.CompletedTask;
        });

        await store.SendWorkflowAsync(StoryAssistantWorkflowCatalog.PrepareStory);

        Assert.NotNull(captured);
        Assert.Equal("Custom prepare workflow guidance.", captured.ModelInput);
        Assert.Equal("Prepare a new story", captured.DisplayMessage);
        Assert.Equal(StoryAssistantItemKind.UserMessage, document.StoryAssistant.Items[0].Kind);
        Assert.Equal("Prepare a new story", document.StoryAssistant.Items[0].Text);
        Assert.Equal(StoryAssistantItemStatus.Failed, document.StoryAssistant.Items[1].Status);
        Assert.True(document.StoryAssistant.Items[1].Retry.CanRetry);
        Assert.Equal("No output", document.StoryAssistant.Items[1].Diagnostics.Outcome);
    }

    [Fact]
    public async Task RetryResendsStoredModelInputWithResumeInstruction()
    {
        var document = CreateDocument();
        var failed = AddAssistantMessage(StoryAssistantItemStatus.Failed, "The model finished without returning a message or action.");
        failed.Retry = new()
        {
            DisplayMessage = "Prepare a new story",
            ModelInput = "Original workflow prompt."
        };
        document.StoryAssistant.Items.Add(AddUserMessage("Prepare a new story"));
        document.StoryAssistant.Items.Add(failed);
        StoryAssistantTurnRequest? captured = null;
        var store = CreateStore(document, (request, _, _) =>
        {
            captured = request;
            return Task.CompletedTask;
        });

        await store.RetryAsync(failed.Id);

        Assert.NotNull(captured);
        Assert.Equal("Retry: Prepare a new story", captured.DisplayMessage);
        Assert.Contains("Continue from the previous Story Assistant request.", captured.ModelInput, StringComparison.Ordinal);
        Assert.Contains("Original workflow prompt.", captured.ModelInput, StringComparison.Ordinal);
    }

    [Fact]
    public void IdleStateRemovesAbandonedEmptyStreamingPlaceholder()
    {
        var document = CreateDocument();
        document.StoryAssistant.Items.Add(AddUserMessage("Prepare a new story"));
        document.StoryAssistant.Items.Add(AddAssistantMessage(StoryAssistantItemStatus.Streaming, ""));
        var store = CreateStore(document, (_, _) => Task.CompletedTask);

        var items = store.Items;

        Assert.Single(items);
        Assert.Equal(StoryAssistantItemKind.UserMessage, items[0].Kind);
    }

    [Fact]
    public void IdleStateMarksAbandonedPartialStreamingMessageStopped()
    {
        var document = CreateDocument();
        document.StoryAssistant.Items.Add(AddUserMessage("Prepare a new story"));
        document.StoryAssistant.Items.Add(AddAssistantMessage(StoryAssistantItemStatus.Streaming, "Partial answer."));
        var store = CreateStore(document, (_, _) => Task.CompletedTask);

        var item = store.Items.Last();

        Assert.Equal(StoryAssistantItemKind.AssistantMessage, item.Kind);
        Assert.Equal(StoryAssistantItemStatus.Stopped, item.Status);
        Assert.Equal("Partial answer.", item.Text);
    }

    [Fact]
    public async Task AnsweringSavedQuestionAfterStoreRecreationResumesFunctionCallOutput()
    {
        var document = CreateDocument();
        document.StoryAssistant.ResponseProviderId = "provider-1";
        document.StoryAssistant.ResponseModelId = "model-1";
        document.StoryAssistant.LastResponseId = "resp-question";
        document.StoryAssistant.ResponseIds.Add("resp-question");
        var workItem = PendingQuestionWorkItem();
        document.StoryAssistant.WorkItems.Add(workItem);
        document.StoryAssistant.Items.Add(TranscriptItem(workItem));
        StoryAssistantTurnRequest? captured = null;
        var store = CreateStore(
            document,
            (request, _, _) =>
            {
                captured = request;
                return Task.CompletedTask;
            },
            configuredProviders: [ReasoningProvider()]);

        await store.ResolveQuestionAsync(workItem.TranscriptItemId, "Go noir.");

        Assert.NotNull(captured);
        Assert.Equal(StoryAssistantTurnRequestKind.WorkItemResume, captured.Kind);
        Assert.Equal("call-question", captured.ToolCallId);
        Assert.Equal("resp-question", captured.PreviousResponseId);
        Assert.Equal(StoryAssistantWorkItemStatus.Completed, workItem.Status);
        using var json = JsonDocument.Parse(captured.ModelInput);
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Go noir.", json.RootElement.GetProperty("answer").GetString());
    }

    [Fact]
    public async Task AcceptingSavedReviewAfterStoreRecreationAppliesMutationAndResumes()
    {
        var document = CreateDocument();
        document.StoryAssistant.ResponseProviderId = "provider-1";
        document.StoryAssistant.ResponseModelId = "model-1";
        document.StoryAssistant.LastResponseId = "resp-review";
        document.StoryAssistant.ResponseIds.Add("resp-review");
        var workItem = await PendingCharacterReviewWorkItemAsync(document);
        document.StoryAssistant.WorkItems.Add(workItem);
        document.StoryAssistant.Items.Add(TranscriptItem(workItem));
        StoryAssistantTurnRequest? captured = null;
        var store = CreateStore(
            document,
            (request, _, _) =>
            {
                captured = request;
                return Task.CompletedTask;
            },
            configuredProviders: [ReasoningProvider()]);

        await store.ResolveReviewAsync(workItem.TranscriptItemId, StoryAssistantDecisionKind.Accept, "");

        Assert.Equal("Durable summary", document.Characters[0].Summary);
        Assert.Equal(StoryAssistantWorkItemStatus.Completed, workItem.Status);
        Assert.NotNull(captured);
        Assert.Equal(StoryAssistantTurnRequestKind.WorkItemResume, captured.Kind);
        Assert.Equal("call-review", captured.ToolCallId);
    }

    [Fact]
    public async Task AcceptingStaleReviewMarksConflictAndDoesNotApplyMutation()
    {
        var document = CreateDocument();
        document.StoryAssistant.ResponseProviderId = "provider-1";
        document.StoryAssistant.ResponseModelId = "model-1";
        document.StoryAssistant.LastResponseId = "resp-review";
        document.StoryAssistant.ResponseIds.Add("resp-review");
        var workItem = await PendingCharacterReviewWorkItemAsync(document);
        document.StoryAssistant.WorkItems.Add(workItem);
        document.StoryAssistant.Items.Add(TranscriptItem(workItem));
        document.Characters[0].Summary = "Someone edited first.";
        StoryAssistantTurnRequest? captured = null;
        var store = CreateStore(
            document,
            (request, _, _) =>
            {
                captured = request;
                return Task.CompletedTask;
            },
            configuredProviders: [ReasoningProvider()]);

        await store.ResolveReviewAsync(workItem.TranscriptItemId, StoryAssistantDecisionKind.Accept, "");

        Assert.Equal("Someone edited first.", document.Characters[0].Summary);
        Assert.Equal(StoryAssistantWorkItemStatus.Conflict, workItem.Status);
        Assert.NotNull(captured);
        using var json = JsonDocument.Parse(captured.ModelInput);
        Assert.Equal("conflict", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task SavedWorkItemWithMissingContinuationFailsWithoutChangingStory()
    {
        var document = CreateDocument();
        var workItem = await PendingCharacterReviewWorkItemAsync(document);
        workItem.AwaitingResponseId = "";
        document.StoryAssistant.WorkItems.Add(workItem);
        document.StoryAssistant.Items.Add(TranscriptItem(workItem));
        var store = CreateStore(document, (_, _, _) => Task.CompletedTask, configuredProviders: [ReasoningProvider()]);

        await store.ResolveReviewAsync(workItem.TranscriptItemId, StoryAssistantDecisionKind.Accept, "");

        Assert.Equal("Old summary", document.Characters[0].Summary);
        Assert.Equal(StoryAssistantWorkItemStatus.Failed, workItem.Status);
        Assert.Contains("continuation", workItem.DecisionReason, StringComparison.OrdinalIgnoreCase);
    }

    static StoryAssistantStore CreateStore(RpChatDocument document, Func<IStoryAssistantCallbacks, Task> script) =>
        CreateStore(document, (callbacks, _) => script(callbacks));

    static StoryAssistantStore CreateStore(
        RpChatDocument document,
        Func<IStoryAssistantCallbacks, CancellationToken, Task> script,
        Func<RpChatDocument, IReadOnlyList<AiProvider>, CancellationToken, Task>? clearScript = null,
        IReadOnlyList<AiProvider>? providers = null)
        => CreateStore(document, (_, callbacks, cancellationToken) => script(callbacks, cancellationToken), clearScript, providers);

    static StoryAssistantStore CreateStore(
        RpChatDocument document,
        Func<StoryAssistantTurnRequest, IStoryAssistantCallbacks, CancellationToken, Task> script,
        Func<RpChatDocument, IReadOnlyList<AiProvider>, CancellationToken, Task>? clearScript = null,
        IReadOnlyList<AiProvider>? configuredProviders = null)
    {
        var activeChat = new ActiveChatContext();
        activeChat.SetAsync(document).GetAwaiter().GetResult();
        var liveStore = new TestLiveRoleplayStore(document, configuredProviders ?? []);
        var registry = new ChatRegistry(Guid.NewGuid(), liveStore, activeChat);
        var providers = new ProviderStore(Guid.NewGuid(), liveStore);
        var modelSelection = new ModelSelectionStore(providers, new GlobalModelSelectionStore(new InMemoryAppSettingsService()));
        providers.LoadAsync().GetAwaiter().GetResult();
        if (configuredProviders?.FirstOrDefault() is { } provider && provider.Models.FirstOrDefault() is { } model)
            modelSelection.SetActiveModelAsync(AiModelRole.Reasoning, provider.Id, model.Id).GetAwaiter().GetResult();

        var transcript = new TranscriptStore(activeChat, registry, providers, modelSelection, NullTextGenerationService.Instance, new SceneTransitionService());
        return new(activeChat, registry, providers, modelSelection, transcript, new ScriptedStoryAssistantService(script, clearScript));
    }

    static RpChatDocument CreateDocument() => new()
    {
        Chat = new() { Id = "chat-1" },
        Characters =
        [
            new()
            {
                Id = "c1",
                Name = "Lucia",
                Summary = "Old summary",
                Backstory = "Keeps the old backstory."
            }
        ]
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

    static StoryAssistantTranscriptItem AddUserMessage(string text) =>
        AddMessage(StoryAssistantItemKind.UserMessage, StoryAssistantItemStatus.Applied, text);

    static StoryAssistantTranscriptItem AddAssistantMessage(StoryAssistantItemStatus status, string text) =>
        AddMessage(StoryAssistantItemKind.AssistantMessage, status, text);

    static StoryAssistantWorkItem PendingQuestionWorkItem()
    {
        var now = DateTime.UtcNow;
        return new()
        {
            Id = "work-question",
            TranscriptItemId = "item-question",
            Kind = StoryAssistantWorkItemKind.Question,
            Status = StoryAssistantWorkItemStatus.Pending,
            CreatedUtc = now,
            UpdatedUtc = now,
            Title = "Question",
            ToolName = "ask_user",
            ToolCallId = "call-question",
            AwaitingResponseId = "resp-question",
            ResponseProviderId = "provider-1",
            ResponseModelId = "model-1",
            Operation = StoryAssistantOperationKind.Question,
            Question = new() { Prompt = "What tone?", AllowsFreeform = true }
        };
    }

    static async Task<StoryAssistantWorkItem> PendingCharacterReviewWorkItemAsync(RpChatDocument document)
    {
        var now = DateTime.UtcNow;
        var before = await CharacterAssistantShapeAsync(document);
        var after = JsonNode.Parse(before.ToJsonString())!.AsObject();
        after["summary"] = "Durable summary";
        return new()
        {
            Id = "work-review",
            TranscriptItemId = "item-review",
            Kind = StoryAssistantWorkItemKind.MutationReview,
            Status = StoryAssistantWorkItemStatus.Pending,
            CreatedUtc = now,
            UpdatedUtc = now,
            Title = "Update Lucia",
            ToolName = "update_character",
            ToolCallId = "call-review",
            AwaitingResponseId = "resp-review",
            ResponseProviderId = "provider-1",
            ResponseModelId = "model-1",
            EntityArea = RoleplayStoreArea.Characters.ToString(),
            Operation = StoryAssistantOperationKind.Update,
            EntityType = "character",
            EntityId = "c1",
            EntityName = "Lucia",
            ArgumentsJson = """{"entityId":"c1","updates":{"summary":"Durable summary"}}""",
            Before = before,
            After = after,
            Diffs = [new() { Field = "summary", Label = "Summary", Before = "Old summary", After = "Durable summary" }]
        };
    }

    static async Task<JsonObject> CharacterAssistantShapeAsync(RpChatDocument document)
    {
        var result = await new StoryEntityPatchService().ExecuteAsync(document, "call-read", "get_story_entities", "{}", new WorkItemCaptureCallbacks(), CancellationToken.None);
        var node = JsonNode.Parse(result)!.AsObject();
        return node["entities"]!["characters"]!.AsArray()[0]!.AsObject();
    }

    static StoryAssistantTranscriptItem TranscriptItem(StoryAssistantWorkItem workItem) => new()
    {
        Id = workItem.TranscriptItemId,
        Kind = workItem.Kind == StoryAssistantWorkItemKind.Question ? StoryAssistantItemKind.Question : StoryAssistantItemKind.ToolCall,
        Status = workItem.Kind == StoryAssistantWorkItemKind.Question ? StoryAssistantItemStatus.Pending : StoryAssistantItemStatus.NeedsReview,
        CreatedUtc = workItem.CreatedUtc,
        UpdatedUtc = workItem.UpdatedUtc,
        WorkItemId = workItem.Id,
        Title = workItem.Title,
        ToolName = workItem.ToolName,
        ToolCallId = workItem.ToolCallId,
        Operation = workItem.Operation,
        EntityType = workItem.EntityType,
        EntityId = workItem.EntityId,
        EntityName = workItem.EntityName,
        ArgumentsJson = workItem.ArgumentsJson,
        Before = workItem.Before,
        After = workItem.After,
        Diffs = workItem.Diffs,
        Question = workItem.Question
    };

    sealed class WorkItemCaptureCallbacks : IStoryAssistantCallbacks
    {
        public List<StoryAssistantWorkItem> WorkItems { get; } = [];

        public Task AppendAssistantTextAsync(string delta, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordWorkItemAsync(StoryAssistantWorkItem workItem, CancellationToken cancellationToken)
        {
            WorkItems.Add(workItem);
            return Task.CompletedTask;
        }

        public Task UpdateWorkItemAsync(StoryAssistantWorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SceneTransitionResult> GenerateSceneTransitionAsync(SceneTransitionRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveEntityAreaAsync(RoleplayStoreArea area, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveAssistantStateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    static StoryAssistantTranscriptItem AddMessage(StoryAssistantItemKind kind, StoryAssistantItemStatus status, string text) => new()
    {
        Id = $"message-{Guid.NewGuid():N}",
        Kind = kind,
        Status = status,
        Text = text,
        CreatedUtc = DateTime.UtcNow,
        UpdatedUtc = DateTime.UtcNow
    };

    sealed class ScriptedStoryAssistantService(
        Func<StoryAssistantTurnRequest, IStoryAssistantCallbacks, CancellationToken, Task> script,
        Func<RpChatDocument, IReadOnlyList<AiProvider>, CancellationToken, Task>? clearScript) : IStoryAssistantService
    {
        public Task RunTurnAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            StoryAssistantTurnRequest request,
            IStoryAssistantCallbacks callbacks,
            CancellationToken cancellationToken = default) => script(request, callbacks, cancellationToken);

        public Task ClearRemoteStateAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            CancellationToken cancellationToken = default) =>
            clearScript?.Invoke(document, providers, cancellationToken) ?? Task.CompletedTask;

        public Task ResolveWorkItemAsync(
            RpChatDocument document,
            StoryAssistantWorkItem workItem,
            StoryAssistantWorkItemResolution resolution,
            IStoryAssistantCallbacks callbacks,
            CancellationToken cancellationToken = default) =>
            new StoryEntityPatchService().ResolveWorkItemAsync(document, workItem, resolution, callbacks, cancellationToken);
    }

    static AiProvider ReasoningProvider() => new()
    {
        Id = "provider-1",
        Name = "Test Provider",
        Enabled = true,
        Models =
        [
            new()
            {
                Id = "model-1",
                DisplayName = "Model 1",
                Enabled = true,
                Roles = [AiModelRole.Chat],
                Capabilities = new() { Tools = true }
            }
        ]
    };

    sealed class TestLiveRoleplayStore(RpChatDocument document, IReadOnlyList<AiProvider> providers) : ILiveRoleplayStore
    {
        public event Func<RoleplayStoreNotification, Task>? Changed;

        public Task<IReadOnlyList<RpChat>> LoadChatsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RpChat>>([document.Chat]);

        public Task<IReadOnlyList<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(providers);

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
