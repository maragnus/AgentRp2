using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
using Xunit;
using AgentRp.UserSystem;

namespace AgentRp.Tests;

public sealed class StoryAssistantStoreTests
{
	private sealed class WorkItemCaptureCallbacks : IStoryAssistantCallbacks
	{
		public List<StoryAssistantWorkItem> WorkItems { get; } = new List<StoryAssistantWorkItem>();

		public Task AppendAssistantTextAsync(string delta, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public Task RecordToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public Task UpdateToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public Task RecordWorkItemAsync(StoryAssistantWorkItem workItem, CancellationToken cancellationToken)
		{
			WorkItems.Add(workItem);
			return Task.CompletedTask;
		}

		public Task UpdateWorkItemAsync(StoryAssistantWorkItem workItem, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public Task<SceneTransitionResult> SetSceneAsync(SetSceneRequest request, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public Task SaveEntityAreaAsync(RoleplayStoreArea area, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public Task SaveAssistantStateAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class ScriptedStoryAssistantService(RpChatDocument document, Func<StoryAssistantTurnRequest, IStoryAssistantCallbacks, CancellationToken, Task> script, Func<RpChatDocument, IReadOnlyList<AiProvider>, CancellationToken, Task>? clearScript) : IStoryAssistantService
	{
		public Task RunTurnAsync(RpChatDocument document, StoryAssistantChat assistantChat, IReadOnlyList<AiProvider> providers, GenerationRuntimeConfig runtimeConfig, StoryAssistantTurnRequest request, IStoryAssistantCallbacks callbacks, CancellationToken cancellationToken = default(CancellationToken))
		{
			return script(request, callbacks, cancellationToken);
		}

		public Task ClearRemoteStateAsync(StoryAssistantChat assistantChat, IReadOnlyList<AiProvider> providers, GenerationRuntimeConfig runtimeConfig, CancellationToken cancellationToken = default(CancellationToken))
		{
			return clearScript?.Invoke(document, providers, cancellationToken) ?? Task.CompletedTask;
		}

		public Task ResolveWorkItemAsync(RpChatDocument document, StoryAssistantWorkItem workItem, StoryAssistantWorkItemResolution resolution, IStoryAssistantCallbacks callbacks, CancellationToken cancellationToken = default(CancellationToken))
		{
			return new StoryEntityPatchService().ResolveWorkItemAsync(document, workItem, resolution, callbacks, cancellationToken);
		}
	}

	private sealed class TestLiveRoleplayStore(RpChatDocument document, IReadOnlyList<AiProvider> providers) : ILiveRoleplayStore
	{
		public List<RoleplayStoreArea> ReplacedAreas { get; } = new List<RoleplayStoreArea>();

		public event Func<RoleplayStoreNotification, Task>? Changed;

		public Task<IReadOnlyList<StoryPreview>> LoadStoryPreviewsAsync(CurrentAppUser user, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult((IReadOnlyList<StoryPreview>)new List<StoryPreview> { StoryPreviewProjector.FromDocument(document) });
		}

		public Task<IReadOnlyList<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult(providers);
		}

		public Task<RpChatDocument> OpenChatAsync(CurrentAppUser user, Guid sessionId, string chatId, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult(document);
		}

		public void ReleaseChat(Guid sessionId, string? chatId)
		{
		}

		public Task<RpChatDocument> GetChatSnapshotAsync(CurrentAppUser user, string chatId, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult(document);
		}

		public Task<IReadOnlyList<StoryPreview>> AddChatAsync(CurrentAppUser user, Guid originSessionId, StoryCreationOptions options, RpChatDocument? template, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult((IReadOnlyList<StoryPreview>)new List<StoryPreview> { StoryPreviewProjector.FromDocument(document) });
		}

		public Task ReplaceProvidersAsync(CurrentAppUser user, Guid originSessionId, IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.CompletedTask;
		}

		public Task ReplaceChatAreaAsync(CurrentAppUser user, Guid originSessionId, string chatId, RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default(CancellationToken))
		{
			ReplacedAreas.Add(area);
			return this.Changed?.Invoke(new RoleplayStoreNotification(originSessionId, chatId, area, 1L)) ?? Task.CompletedTask;
		}
	}

	private static CurrentAppUser TestUser { get; } = new CurrentAppUser(Guid.Parse("11111111-1111-1111-1111-111111111111"), "dev.user@local", "DEV.USER@LOCAL", "Development User", new HashSet<string>(StringComparer.Ordinal) { "Admin", "User" });

	[Fact]
	public void DefaultStateCreatesAndSelectsOneAssistantChat()
	{
		RpChatDocument document = CreateDocument();
		StoryAssistantStore storyAssistantStore = CreateStore(document, (IStoryAssistantCallbacks _, CancellationToken _) => Task.CompletedTask);
		StoryAssistantState state = storyAssistantStore.State;
		Assert.Single(state.Chats);
		Assert.Equal(state.Chats[0].Id, state.ActiveChatId);
		Assert.Same(state.Chats[0], storyAssistantStore.ActiveAssistantChat);
		Assert.Equal("New chat", storyAssistantStore.ActiveAssistantChat.Title);
	}

	[Fact]
	public async Task ChatCommandsCreateSelectRenameAndDeleteWithFreshFinalChat()
	{
		RpChatDocument document = CreateDocument();
		StoryAssistantStore store = CreateStore(document, (IStoryAssistantCallbacks _, CancellationToken _) => Task.CompletedTask);
		string firstChatId = store.ActiveAssistantChat.Id;
		await store.CreateChatAsync();
		string secondChatId = store.ActiveAssistantChat.Id;
		await store.RenameChatAsync(secondChatId, "  Cast planning  ");
		await store.SelectChatAsync(firstChatId);
		await store.DeleteChatAsync(firstChatId);
		Assert.Equal(secondChatId, store.ActiveAssistantChat.Id);
		Assert.Equal("Cast planning", store.ActiveAssistantChat.Title);
		Assert.Single(store.Chats);
		await store.DeleteChatAsync(secondChatId);
		Assert.Single(store.Chats);
		Assert.NotEqual(secondChatId, store.ActiveAssistantChat.Id);
		Assert.Equal("New chat", store.ActiveAssistantChat.Title);
		Assert.Empty(store.ActiveAssistantChat.Items);
	}

	[Fact]
	public async Task OpeningAssistantSelectsMostRecentChatWhenActiveChatHasMessages()
	{
		RpChatDocument document = CreateDocument();
		StoryAssistantStore store = CreateStore(document, (IStoryAssistantCallbacks _, CancellationToken _) => Task.CompletedTask);
		StoryAssistantChat olderChat = store.ActiveAssistantChat;
		olderChat.Items.Add(AddUserMessage("Earlier planning."));
		olderChat.UpdatedUtc = DateTime.UtcNow.AddMinutes(-5.0);
		await store.CreateChatAsync();
		StoryAssistantChat recentChat = store.ActiveAssistantChat;
		recentChat.Items.Add(AddUserMessage("Recent planning."));
		recentChat.UpdatedUtc = DateTime.UtcNow;
		await store.SelectChatAsync(olderChat.Id);
		Assert.Equal(recentChat.Id, store.OpenAssistantChatPreview.Id);
		await store.OpenLatestOrNewChatAsync();
		Assert.Equal(recentChat.Id, store.ActiveAssistantChat.Id);
	}

	[Fact]
	public async Task OpeningAssistantKeepsEmptyActiveChatAsNewChat()
	{
		RpChatDocument document = CreateDocument();
		DateTime now = DateTime.UtcNow;
		StoryAssistantChat emptyChat = new StoryAssistantChat
		{
			Id = "assistant-chat-empty",
			Title = "New chat",
			CreatedUtc = now.AddMinutes(-10.0),
			UpdatedUtc = now.AddMinutes(-10.0)
		};
		StoryAssistantChat recentChat = new StoryAssistantChat
		{
			Id = "assistant-chat-recent",
			Title = "Recent chat",
			CreatedUtc = now,
			UpdatedUtc = now,
			Items = new List<StoryAssistantTranscriptItem>(1) { AddUserMessage("Recent planning.") }
		};
		document.StoryAssistant.Chats.AddRange(new StoryAssistantChat[2] { recentChat, emptyChat });
		document.StoryAssistant.ActiveChatId = emptyChat.Id;
		StoryAssistantStore store = CreateStore(document, (IStoryAssistantCallbacks _, CancellationToken _) => Task.CompletedTask);
		Assert.Equal(emptyChat.Id, store.OpenAssistantChatPreview.Id);
		Assert.True(store.OpenAssistantChatPreviewIsEmpty);
		await store.OpenLatestOrNewChatAsync();
		Assert.Equal(emptyChat.Id, store.ActiveAssistantChat.Id);
		Assert.Empty(store.ActiveAssistantChat.Items);
	}

	[Fact]
	public async Task SendingInOneAssistantChatDoesNotMutateAnotherChat()
	{
		RpChatDocument document = CreateDocument();
		int turn = 0;
		StoryAssistantStore store = CreateStore(document, async delegate(StoryAssistantTurnRequest request, IStoryAssistantCallbacks callbacks, CancellationToken cancellationToken)
		{
			turn++;
			document.StoryAssistant.LastResponseId = $"resp-{turn}";
			document.StoryAssistant.ResponseIds.Add($"resp-{turn}");
			await callbacks.AppendAssistantTextAsync("Reply to " + request.DisplayMessage, cancellationToken);
			if (request.DisplayMessage == "Second chat prompt.")
			{
				await callbacks.RecordWorkItemAsync(PendingQuestionWorkItem(), cancellationToken);
			}
		});
		StoryAssistantChat firstChat = store.ActiveAssistantChat;
		await store.SendAsync("First chat prompt.");
		await store.CreateChatAsync();
		StoryAssistantChat secondChat = store.ActiveAssistantChat;
		await store.SendAsync("Second chat prompt.");
		Assert.Equal("resp-1", firstChat.LastResponseId);
		Assert.Equal("resp-2", secondChat.LastResponseId);
		Assert.Contains((IEnumerable<StoryAssistantTranscriptItem>)firstChat.Items, (Predicate<StoryAssistantTranscriptItem>)((StoryAssistantTranscriptItem item) => item.Text == "First chat prompt."));
		Assert.DoesNotContain((IEnumerable<StoryAssistantTranscriptItem>)firstChat.Items, (Predicate<StoryAssistantTranscriptItem>)((StoryAssistantTranscriptItem item) => item.Text.Contains("Second chat", StringComparison.Ordinal)));
		Assert.Contains((IEnumerable<StoryAssistantTranscriptItem>)secondChat.Items, (Predicate<StoryAssistantTranscriptItem>)((StoryAssistantTranscriptItem item) => item.Text == "Second chat prompt."));
		Assert.DoesNotContain((IEnumerable<StoryAssistantTranscriptItem>)secondChat.Items, (Predicate<StoryAssistantTranscriptItem>)((StoryAssistantTranscriptItem item) => item.Text.Contains("First chat", StringComparison.Ordinal)));
		Assert.Empty(firstChat.WorkItems);
		Assert.Single(secondChat.WorkItems);
	}

	[Fact]
	public async Task SendStartsWithThinkingPlaceholderAndToolFirstReplacesIt()
	{
		RpChatDocument document = CreateDocument();
		StoryAssistantStore store = CreateStore(document, async delegate(IStoryAssistantCallbacks callbacks)
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
		Assert.NotEqual(default(DateTime), document.StoryAssistant.LastStoryEntitiesReadUtc);
	}

	[Fact]
	public async Task SendPrependsFreshnessNoteForStaleTranscriptAndEntities()
	{
		RpChatDocument document = CreateDocument();
		DateTime readAt = DateTime.UtcNow.AddMinutes(-5.0);
		document.StoryAssistant.LastTranscriptReadUtc = readAt;
		document.StoryAssistant.LastStoryEntitiesReadUtc = readAt;
		document.Characters[0].UpdatedUtc = readAt.AddMinutes(1.0);
		document.Locations.Add(new RpLocation
		{
			Id = "l1",
			Name = "Poolside Bar",
			UpdatedUtc = readAt.AddMinutes(2.0)
		});
		document.Transcript.Turns.Add(new RpTranscriptTurn
		{
			Id = "turn-1",
			CreatedUtc = readAt.AddMinutes(3.0),
			UpdatedUtc = readAt.AddMinutes(3.0),
			AuthorName = "Lucia",
			Body = "New story beat."
		});
		document.Transcript.ActiveLeafTurnId = "turn-1";
		StoryAssistantTurnRequest? captured = null;
		StoryAssistantStore store = CreateStore(document, delegate(StoryAssistantTurnRequest request, IStoryAssistantCallbacks _, CancellationToken _)
		{
			captured = request;
			return Task.CompletedTask;
		});
		await store.SendAsync("Tweak the cast.");
		Assert.NotNull(captured);
		Assert.StartsWith("NOTE: Updates since you last checked:", captured.ModelInput, StringComparison.Ordinal);
		Assert.Contains("added 1 message", captured.ModelInput, StringComparison.Ordinal);
		Assert.Contains("Lucia", captured.ModelInput, StringComparison.Ordinal);
		Assert.Contains("Poolside Bar", captured.ModelInput, StringComparison.Ordinal);
		Assert.EndsWith("Tweak the cast.", captured.ModelInput, StringComparison.Ordinal);
		Assert.Equal("Tweak the cast.", document.StoryAssistant.Items[0].Text);
	}

	[Fact]
	public async Task AcceptedMutationOutputRefreshesStoryEntitiesReceipt()
	{
		RpChatDocument document = CreateDocument();
		DateTime staleRead = DateTime.UtcNow.AddMinutes(-5.0);
		document.StoryAssistant.LastStoryEntitiesReadUtc = staleRead;
		StoryAssistantStore store = CreateStore(document, async delegate(IStoryAssistantCallbacks callbacks)
		{
			await callbacks.UpdateToolCallAsync(new StoryAssistantTranscriptItem
			{
				Id = "tool-update",
				Kind = StoryAssistantItemKind.ToolCall,
				Status = StoryAssistantItemStatus.Applied,
				Operation = StoryAssistantOperationKind.Update,
				ToolName = "update_character",
				ToolCallId = "call-update",
				EntityType = "character",
				EntityId = "c1",
				EntityName = "Lucia"
			}, CancellationToken.None);
		});
		await store.SendAsync("Update Lucia.");
		Assert.True(document.StoryAssistant.LastStoryEntitiesReadUtc > staleRead);
	}

	[Fact]
	public async Task TextAfterToolCallStartsNewAssistantBubble()
	{
		RpChatDocument document = CreateDocument();
		StoryAssistantStore store = CreateStore(document, async delegate(IStoryAssistantCallbacks callbacks)
		{
			await callbacks.AppendAssistantTextAsync("First thought.", CancellationToken.None);
			await callbacks.RecordToolCallAsync(ReadTool(), CancellationToken.None);
			await callbacks.AppendAssistantTextAsync("Second thought.", CancellationToken.None);
		});
		await store.SendAsync("Plan the cast.");
		Assert.Equal<StoryAssistantItemKind[]>(new StoryAssistantItemKind[4]
		{
			StoryAssistantItemKind.UserMessage,
			StoryAssistantItemKind.AssistantMessage,
			StoryAssistantItemKind.ToolCall,
			StoryAssistantItemKind.AssistantMessage
		}, document.StoryAssistant.Items.Select((StoryAssistantTranscriptItem item) => item.Kind).ToArray());
		Assert.Equal("First thought.", document.StoryAssistant.Items[1].Text);
		Assert.Equal(StoryAssistantItemStatus.Applied, document.StoryAssistant.Items[1].Status);
		Assert.Equal("Second thought.", document.StoryAssistant.Items[3].Text);
		Assert.Equal(StoryAssistantItemStatus.Applied, document.StoryAssistant.Items[3].Status);
	}

	[Fact]
	public async Task StopCancelsActiveRunWithCalmStoppedMessage()
	{
		RpChatDocument document = CreateDocument();
		TaskCompletionSource started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		StoryAssistantStore store = CreateStore(document, async delegate(IStoryAssistantCallbacks _, CancellationToken cancellationToken)
		{
			started.SetResult();
			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
		});
		Task sendTask = store.SendAsync("Keep working.");
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
		RpChatDocument document = CreateDocument();
		StoryAssistantStore store = CreateStore(document, (Func<IStoryAssistantCallbacks, CancellationToken, Task>)delegate
		{
			throw new ModelAssistantThreadLostException("Grok / xAI", "grok-4.3", "resp-old", new InvalidOperationException("The requested resource was not found."));
		}, (Func<RpChatDocument, IReadOnlyList<AiProvider>, CancellationToken, Task>?)null, (IReadOnlyList<AiProvider>?)null, (PromptLibraryState?)null);
		await store.SendAsync("Keep going.");
		Assert.True(document.StoryAssistant.RemoteThreadLost);
		Assert.Contains("fresh thread", document.StoryAssistant.RemoteThreadError, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(StoryAssistantItemStatus.Failed, document.StoryAssistant.Items.Last().Status);
		Assert.Contains("fresh thread", document.StoryAssistant.Items.Last().Text, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ClearResetsLocalStateAfterRemoteCleanup()
	{
		RpChatDocument document = CreateDocument();
		document.StoryAssistant.LastResponseId = "resp-2";
		document.StoryAssistant.ResponseIds.Add("resp-1");
		document.StoryAssistant.ResponseIds.Add("resp-2");
		document.StoryAssistant.ResponseProviderId = "provider-1";
		document.StoryAssistant.ResponseModelId = "model-1";
		document.StoryAssistant.RemoteThreadLost = true;
		document.StoryAssistant.RemoteThreadError = "Needs restart.";
		document.StoryAssistant.Items.Add(ReadTool());
		bool cleanupCalled = false;
		StoryAssistantStore store = CreateStore(document, (IStoryAssistantCallbacks _, CancellationToken _) => Task.CompletedTask, delegate(RpChatDocument cleanupDocument, IReadOnlyList<AiProvider> _, CancellationToken _)
		{
			cleanupCalled = document == cleanupDocument;
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
		RpChatDocument document = CreateDocument();
		PromptLibraryState promptLibrary = PromptLibraryService.CreateDefaultState();
		promptLibrary.Prompts["storyAssistantPrepareStory"].User = "Custom prepare workflow guidance.";
		StoryAssistantTurnRequest? captured = null;
		StoryAssistantStore store = CreateStore(document, delegate(StoryAssistantTurnRequest request, IStoryAssistantCallbacks _, CancellationToken _)
		{
			captured = request;
			return Task.CompletedTask;
		}, null, null, promptLibrary);
		await store.SendWorkflowAsync("prepare-story");
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
	public async Task StoryMetadataSaveRefreshesPreviewTitle()
	{
		RpChatDocument document = CreateDocument();
		document.Chat.Title = "Old Story";
		var (store, liveStore) = CreateStoreWithLiveStore(document, async delegate(StoryAssistantTurnRequest _, IStoryAssistantCallbacks callbacks, CancellationToken cancellationToken)
		{
			document.Chat.Title = "New Story";
			await callbacks.SaveEntityAreaAsync(RoleplayStoreArea.ChatDirection, cancellationToken);
		});
		await store.SendAsync("Rename the story.");
		Assert.Equal("New Story", (await liveStore.LoadStoryPreviewsAsync(TestUser)).Single().Title);
		Assert.Contains(RoleplayStoreArea.ChatDirection, (IEnumerable<RoleplayStoreArea>)liveStore.ReplacedAreas);
	}

	[Fact]
	public async Task RetryResendsStoredModelInputWithResumeInstruction()
	{
		RpChatDocument document = CreateDocument();
		StoryAssistantTranscriptItem failed = AddAssistantMessage(StoryAssistantItemStatus.Failed, "The model finished without returning a message or action.");
		failed.Retry = new StoryAssistantRetryContext
		{
			DisplayMessage = "Prepare a new story",
			ModelInput = "Original workflow prompt."
		};
		document.StoryAssistant.Items.Add(AddUserMessage("Prepare a new story"));
		document.StoryAssistant.Items.Add(failed);
		StoryAssistantTurnRequest? captured = null;
		StoryAssistantStore store = CreateStore(document, delegate(StoryAssistantTurnRequest request, IStoryAssistantCallbacks _, CancellationToken _)
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
		RpChatDocument rpChatDocument = CreateDocument();
		rpChatDocument.StoryAssistant.Items.Add(AddUserMessage("Prepare a new story"));
		rpChatDocument.StoryAssistant.Items.Add(AddAssistantMessage(StoryAssistantItemStatus.Streaming, ""));
		StoryAssistantStore storyAssistantStore = CreateStore(rpChatDocument, (IStoryAssistantCallbacks _, CancellationToken _) => Task.CompletedTask);
		IReadOnlyList<StoryAssistantTranscriptItem> items = storyAssistantStore.Items;
		Assert.Single(items);
		Assert.Equal(StoryAssistantItemKind.UserMessage, items[0].Kind);
	}

	[Fact]
	public void IdleStateMarksAbandonedPartialStreamingMessageStopped()
	{
		RpChatDocument rpChatDocument = CreateDocument();
		rpChatDocument.StoryAssistant.Items.Add(AddUserMessage("Prepare a new story"));
		rpChatDocument.StoryAssistant.Items.Add(AddAssistantMessage(StoryAssistantItemStatus.Streaming, "Partial answer."));
		StoryAssistantStore storyAssistantStore = CreateStore(rpChatDocument, (IStoryAssistantCallbacks _, CancellationToken _) => Task.CompletedTask);
		StoryAssistantTranscriptItem storyAssistantTranscriptItem = storyAssistantStore.Items.Last();
		Assert.Equal(StoryAssistantItemKind.AssistantMessage, storyAssistantTranscriptItem.Kind);
		Assert.Equal(StoryAssistantItemStatus.Stopped, storyAssistantTranscriptItem.Status);
		Assert.Equal("Partial answer.", storyAssistantTranscriptItem.Text);
	}

	[Fact]
	public async Task AnsweringSavedQuestionAfterStoreRecreationResumesFunctionCallOutput()
	{
		RpChatDocument document = CreateDocument();
		document.StoryAssistant.ResponseProviderId = "provider-1";
		document.StoryAssistant.ResponseModelId = "model-1";
		document.StoryAssistant.LastResponseId = "resp-question";
		document.StoryAssistant.ResponseIds.Add("resp-question");
		StoryAssistantWorkItem workItem = PendingQuestionWorkItem();
		document.StoryAssistant.WorkItems.Add(workItem);
		document.StoryAssistant.Items.Add(TranscriptItem(workItem));
		StoryAssistantTurnRequest? captured = null;
		StoryAssistantStore store = CreateStore(document, delegate(StoryAssistantTurnRequest request, IStoryAssistantCallbacks _, CancellationToken _)
		{
			captured = request;
			return Task.CompletedTask;
		}, null, new List<AiProvider> { ReasoningProvider() });
		await store.ResolveQuestionAsync(workItem.TranscriptItemId, "Go noir.");
		Assert.NotNull(captured);
		Assert.Equal(StoryAssistantTurnRequestKind.WorkItemResume, captured.Kind);
		Assert.Equal("call-question", captured.ToolCallId);
		Assert.Equal("resp-question", captured.PreviousResponseId);
		Assert.Equal(StoryAssistantWorkItemStatus.Completed, workItem.Status);
		using JsonDocument json = JsonDocument.Parse(captured.ModelInput);
		Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
		Assert.Equal("Go noir.", json.RootElement.GetProperty("answer").GetString());
	}

	[Fact]
	public async Task AcceptingSavedReviewAfterStoreRecreationAppliesMutationAndResumes()
	{
		RpChatDocument document = CreateDocument();
		document.StoryAssistant.ResponseProviderId = "provider-1";
		document.StoryAssistant.ResponseModelId = "model-1";
		document.StoryAssistant.LastResponseId = "resp-review";
		document.StoryAssistant.ResponseIds.Add("resp-review");
		StoryAssistantWorkItem workItem = await PendingCharacterReviewWorkItemAsync(document);
		document.StoryAssistant.WorkItems.Add(workItem);
		document.StoryAssistant.Items.Add(TranscriptItem(workItem));
		StoryAssistantTurnRequest? captured = null;
		StoryAssistantStore store = CreateStore(document, delegate(StoryAssistantTurnRequest request, IStoryAssistantCallbacks _, CancellationToken _)
		{
			captured = request;
			return Task.CompletedTask;
		}, null, new List<AiProvider> { ReasoningProvider() });
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
		RpChatDocument document = CreateDocument();
		document.StoryAssistant.ResponseProviderId = "provider-1";
		document.StoryAssistant.ResponseModelId = "model-1";
		document.StoryAssistant.LastResponseId = "resp-review";
		document.StoryAssistant.ResponseIds.Add("resp-review");
		StoryAssistantWorkItem workItem = await PendingCharacterReviewWorkItemAsync(document);
		document.StoryAssistant.WorkItems.Add(workItem);
		document.StoryAssistant.Items.Add(TranscriptItem(workItem));
		document.Characters[0].Summary = "Someone edited first.";
		StoryAssistantTurnRequest? captured = null;
		StoryAssistantStore store = CreateStore(document, delegate(StoryAssistantTurnRequest request, IStoryAssistantCallbacks _, CancellationToken _)
		{
			captured = request;
			return Task.CompletedTask;
		}, null, new List<AiProvider> { ReasoningProvider() });
		await store.ResolveReviewAsync(workItem.TranscriptItemId, StoryAssistantDecisionKind.Accept, "");
		Assert.Equal("Someone edited first.", document.Characters[0].Summary);
		Assert.Equal(StoryAssistantWorkItemStatus.Conflict, workItem.Status);
		Assert.NotNull(captured);
		using JsonDocument json = JsonDocument.Parse(captured.ModelInput);
		Assert.Equal("conflict", json.RootElement.GetProperty("status").GetString());
	}

	[Fact]
	public async Task SavedWorkItemWithMissingContinuationFailsWithoutChangingStory()
	{
		RpChatDocument document = CreateDocument();
		StoryAssistantWorkItem workItem = await PendingCharacterReviewWorkItemAsync(document);
		workItem.AwaitingResponseId = "";
		document.StoryAssistant.WorkItems.Add(workItem);
		document.StoryAssistant.Items.Add(TranscriptItem(workItem));
		StoryAssistantStore store = CreateStore(document, (StoryAssistantTurnRequest _, IStoryAssistantCallbacks _, CancellationToken _) => Task.CompletedTask, null, new List<AiProvider> { ReasoningProvider() });
		await store.ResolveReviewAsync(workItem.TranscriptItemId, StoryAssistantDecisionKind.Accept, "");
		Assert.Equal("Old summary", document.Characters[0].Summary);
		Assert.Equal(StoryAssistantWorkItemStatus.Failed, workItem.Status);
		Assert.Contains("continuation", workItem.DecisionReason, StringComparison.OrdinalIgnoreCase);
	}

	private static StoryAssistantStore CreateStore(RpChatDocument document, Func<IStoryAssistantCallbacks, Task> script)
	{
		return CreateStore(document, (IStoryAssistantCallbacks callbacks, CancellationToken _) => script(callbacks));
	}

	private static StoryAssistantStore CreateStore(RpChatDocument document, Func<IStoryAssistantCallbacks, CancellationToken, Task> script, Func<RpChatDocument, IReadOnlyList<AiProvider>, CancellationToken, Task>? clearScript = null, IReadOnlyList<AiProvider>? providers = null, PromptLibraryState? promptLibrary = null)
	{
		return CreateStore(document, (StoryAssistantTurnRequest _, IStoryAssistantCallbacks callbacks, CancellationToken cancellationToken) => script(callbacks, cancellationToken), clearScript, providers, promptLibrary);
	}

	private static StoryAssistantStore CreateStore(RpChatDocument document, Func<StoryAssistantTurnRequest, IStoryAssistantCallbacks, CancellationToken, Task> script, Func<RpChatDocument, IReadOnlyList<AiProvider>, CancellationToken, Task>? clearScript = null, IReadOnlyList<AiProvider>? configuredProviders = null, PromptLibraryState? promptLibrary = null)
	{
		return CreateStoreWithLiveStore(document, script, clearScript, configuredProviders, promptLibrary).Store;
	}

	private static (StoryAssistantStore Store, TestLiveRoleplayStore LiveStore) CreateStoreWithLiveStore(RpChatDocument document, Func<StoryAssistantTurnRequest, IStoryAssistantCallbacks, CancellationToken, Task> script, Func<RpChatDocument, IReadOnlyList<AiProvider>, CancellationToken, Task>? clearScript = null, IReadOnlyList<AiProvider>? configuredProviders = null, PromptLibraryState? promptLibrary = null)
	{
		ActiveChatContext activeChatContext = new ActiveChatContext();
		activeChatContext.SetAsync(document).GetAwaiter().GetResult();
		TestLiveRoleplayStore testLiveRoleplayStore = new TestLiveRoleplayStore(document, configuredProviders ?? Array.Empty<AiProvider>());
		ChatRegistry registry = new ChatRegistry(Guid.NewGuid(), testLiveRoleplayStore, activeChatContext, TestUser);
		ProviderStore providerStore = new ProviderStore(Guid.NewGuid(), testLiveRoleplayStore, TestUser);
		ModelSelectionStore modelSelectionStore = new ModelSelectionStore(providerStore, activeChatContext, registry, new GlobalModelSelectionStore(new InMemoryAppSettingsService()));
		GlobalPromptLibraryStore globalPromptLibraryStore = new GlobalPromptLibraryStore(new InMemoryAppSettingsService());
		if (promptLibrary != null)
		{
			globalPromptLibraryStore.SaveAsync(promptLibrary).GetAwaiter().GetResult();
		}
		GlobalPromptLibrarySessionStore globalPromptLibrarySessionStore = new GlobalPromptLibrarySessionStore(globalPromptLibraryStore);
		globalPromptLibrarySessionStore.LoadAsync().GetAwaiter().GetResult();
		GlobalModelTuningSessionStore globalModelTuningSessionStore = new GlobalModelTuningSessionStore(new GlobalModelTuningStore(new InMemoryAppSettingsService()));
		globalModelTuningSessionStore.LoadAsync().GetAwaiter().GetResult();
		providerStore.LoadAsync().GetAwaiter().GetResult();
		AiProvider? aiProvider = configuredProviders?.FirstOrDefault();
		if (aiProvider != null)
		{
			AiProviderModel? aiProviderModel = aiProvider.Models.FirstOrDefault();
			if (aiProviderModel != null)
			{
				modelSelectionStore.SetActiveModelAsync(AiModelRole.Reasoning, aiProvider.Id, aiProviderModel.Id).GetAwaiter().GetResult();
			}
		}
		TranscriptStore transcript = new TranscriptStore(activeChatContext, registry, providerStore, modelSelectionStore, globalPromptLibrarySessionStore, globalModelTuningSessionStore, NullTextGenerationService.Instance, new SceneTransitionService());
		return (Store: new StoryAssistantStore(activeChatContext, registry, providerStore, modelSelectionStore, globalPromptLibrarySessionStore, globalModelTuningSessionStore, transcript, new ScriptedStoryAssistantService(document, script, clearScript)), LiveStore: testLiveRoleplayStore);
	}

	private static RpChatDocument CreateDocument()
	{
		RpChatDocument obj = new RpChatDocument
		{
			Chat = new RpChat
			{
				Id = "chat-1"
			}
		};
		int num = 1;
		List<RpCharacter> list = new List<RpCharacter>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = new RpCharacter
		{
			Id = "c1",
			Name = "Lucia",
			Summary = "Old summary",
			Backstory = "Keeps the old backstory."
		};
		obj.Characters = list;
		return obj;
	}

	private static StoryAssistantTranscriptItem ReadTool()
	{
		return new StoryAssistantTranscriptItem
		{
			Id = $"tool-{Guid.NewGuid():N}",
			Kind = StoryAssistantItemKind.ToolCall,
			Status = StoryAssistantItemStatus.Read,
			Title = "Read story entities",
			ToolName = "get_story_entities",
			ToolCallId = "call-1"
		};
	}

	private static StoryAssistantTranscriptItem AddUserMessage(string text)
	{
		return AddMessage(StoryAssistantItemKind.UserMessage, StoryAssistantItemStatus.Applied, text);
	}

	private static StoryAssistantTranscriptItem AddAssistantMessage(StoryAssistantItemStatus status, string text)
	{
		return AddMessage(StoryAssistantItemKind.AssistantMessage, status, text);
	}

	private static StoryAssistantWorkItem PendingQuestionWorkItem()
	{
		DateTime utcNow = DateTime.UtcNow;
		return new StoryAssistantWorkItem
		{
			Id = "work-question",
			TranscriptItemId = "item-question",
			Kind = StoryAssistantWorkItemKind.Question,
			Status = StoryAssistantWorkItemStatus.Pending,
			CreatedUtc = utcNow,
			UpdatedUtc = utcNow,
			Title = "Question",
			ToolName = "ask_user",
			ToolCallId = "call-question",
			AwaitingResponseId = "resp-question",
			ResponseProviderId = "provider-1",
			ResponseModelId = "model-1",
			Operation = StoryAssistantOperationKind.Question,
			Question = new StoryAssistantQuestion
			{
				Prompt = "What tone?",
				AllowsFreeform = true
			}
		};
	}

	private static async Task<StoryAssistantWorkItem> PendingCharacterReviewWorkItemAsync(RpChatDocument document)
	{
		DateTime now = DateTime.UtcNow;
		JsonObject before = await CharacterAssistantShapeAsync(document);
		JsonObject after = JsonNode.Parse(before.ToJsonString())!.AsObject();
		after["summary"] = "Durable summary";
		return new StoryAssistantWorkItem
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
			ArgumentsJson = "{\"entityId\":\"c1\",\"updates\":{\"summary\":\"Durable summary\"}}",
			Before = before,
			After = after,
			Diffs = new List<StoryAssistantFieldDiff>(1)
			{
				new StoryAssistantFieldDiff
				{
					Field = "summary",
					Label = "Summary",
					Before = "Old summary",
					After = "Durable summary"
				}
			}
		};
	}

	private static async Task<JsonObject> CharacterAssistantShapeAsync(RpChatDocument document)
	{
		JsonObject node = JsonNode.Parse(await new StoryEntityPatchService().ExecuteAsync(document, "call-read", "get_story_entities", "{}", new WorkItemCaptureCallbacks(), CancellationToken.None))!.AsObject();
		return node["entities"]!["characters"]!.AsArray()[0]!.AsObject();
	}

	private static StoryAssistantTranscriptItem TranscriptItem(StoryAssistantWorkItem workItem)
	{
		return new StoryAssistantTranscriptItem
		{
			Id = workItem.TranscriptItemId,
			Kind = ((workItem.Kind == StoryAssistantWorkItemKind.Question) ? StoryAssistantItemKind.Question : StoryAssistantItemKind.ToolCall),
			Status = ((workItem.Kind != StoryAssistantWorkItemKind.Question) ? StoryAssistantItemStatus.NeedsReview : StoryAssistantItemStatus.Pending),
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
	}

	private static StoryAssistantTranscriptItem AddMessage(StoryAssistantItemKind kind, StoryAssistantItemStatus status, string text)
	{
		return new StoryAssistantTranscriptItem
		{
			Id = $"message-{Guid.NewGuid():N}",
			Kind = kind,
			Status = status,
			Text = text,
			CreatedUtc = DateTime.UtcNow,
			UpdatedUtc = DateTime.UtcNow
		};
	}

	private static AiProvider ReasoningProvider()
	{
		AiProvider obj = new AiProvider
		{
			Id = "provider-1",
			Name = "Test Provider",
			Enabled = true
		};
		int num = 1;
		List<AiProviderModel> list = new List<AiProviderModel>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = new AiProviderModel
		{
			Id = "model-1",
			DisplayName = "Model 1",
			Enabled = true,
			Roles = new HashSet<AiModelRole> { AiModelRole.Chat },
			Capabilities = new ModelGenerationCapabilities
			{
				Tools = true
			}
		};
		obj.Models = list;
		return obj;
	}
}
