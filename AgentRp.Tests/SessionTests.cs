using AgentRp.Components.Chat;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

namespace AgentRp.Tests;

public sealed class SessionTests
{
    [Fact]
    public async Task TwoSessionsShareOneLoadedLiveChat()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);

        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        await sessionA.Chat.Characters.ToggleInSceneAsync("c1");

        Assert.Equal(
            sessionA.Chat.Characters.Items.First(character => character.Id == "c1").InScene,
            sessionB.Chat.Characters.Items.First(character => character.Id == "c1").InScene);
    }

    [Fact]
    public async Task SelectAsyncAwaitsFullChatContextBeforeSwitchCompletes()
    {
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore);
        await session.InitializeAsync();

        await session.Chats.SelectAsync("ch2");

        Assert.Equal("ch2", session.Chats.Active?.ChatId);
        Assert.NotEmpty(session.Chat.Characters.Items);
        Assert.NotEmpty(session.Chat.Locations.Items);
        Assert.NotEmpty(session.Chat.Images.Items);
        Assert.NotEmpty(session.Chat.Transcript.Items);
    }

    [Fact]
    public async Task CharacterChangeInOneSessionUpdatesSecondSessionOnSameChat()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        var notifications = 0;
        sessionB.Chat.Characters.Changed += () =>
        {
            notifications++;
            return Task.CompletedTask;
        };

        var original = sessionB.Chat.Characters.Items.First(character => character.Id == "c1").InScene;
        await sessionA.Chat.Characters.ToggleInSceneAsync("c1");

        Assert.True(notifications > 0);
        Assert.NotEqual(original, sessionB.Chat.Characters.Items.First(character => character.Id == "c1").InScene);
    }

    [Fact]
    public async Task TranscriptPostInOneSessionUpdatesSecondSessionOnSameChat()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        await sessionA.Chat.Transcript.PostManualAsync("Shared live transcript message.", null);

        Assert.Contains(sessionB.Chat.Transcript.Items, message => message.Body == "Shared live transcript message.");
    }

    [Fact]
    public async Task SessionOnDifferentChatIgnoresChatLocalNotification()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();
        await sessionB.Chats.SelectAsync("ch2");

        var notifications = 0;
        sessionB.Chat.Characters.Changed += () =>
        {
            notifications++;
            return Task.CompletedTask;
        };

        await sessionA.Chat.Characters.ToggleInSceneAsync("c1");

        Assert.Equal(0, notifications);
    }

    [Fact]
    public async Task ActiveChatSwitchUsesLiveMemoryInsteadOfFreshSeedClone()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        await sessionA.Chat.Transcript.PostManualAsync("Memory should win.", null);
        await sessionB.Chats.SelectAsync("ch2");
        await sessionB.Chats.SelectAsync("ch1");

        Assert.Contains(sessionB.Chat.Transcript.Items, message => message.Body == "Memory should win.");
    }

    [Fact]
    public async Task ProviderChangesNotifyAllSessions()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        var notifications = 0;
        sessionB.Providers.Changed += () =>
        {
            notifications++;
            return Task.CompletedTask;
        };

        var provider = sessionA.Providers.Items.First();
        provider.Enabled = !provider.Enabled;
        await sessionA.Providers.MarkChangedAsync();

        Assert.True(notifications > 0);
        Assert.Equal(provider.Enabled, sessionB.Providers.Items.First(item => item.Id == provider.Id).Enabled);
    }

    [Fact]
    public async Task ProviderWidgetAutoLoadRunsOncePerProviderPerSession()
    {
        var widgetService = new CountingProviderWidgetService();
        await using var liveStore = NewLiveStore();
        var session = new RoleplaySession(
            liveStore,
            new TestModelCapabilityCatalog(),
            providerWidgetService: widgetService);
        await session.InitializeAsync();
        var providerId = session.Providers.Items.First().Id;

        await session.Providers.EnsureWidgetLoadedAsync(providerId);
        await session.Providers.EnsureWidgetLoadedAsync(providerId);

        Assert.Equal(1, widgetService.RefreshCalls);
        Assert.NotEmpty(session.Providers.Items.First(provider => provider.Id == providerId).Metrics);
    }

    [Fact]
    public async Task ProviderWidgetAutoLoadRunsAgainForNewSession()
    {
        var widgetService = new CountingProviderWidgetService();
        await using var liveStore = NewLiveStore();
        var sessionA = new RoleplaySession(
            liveStore,
            new TestModelCapabilityCatalog(),
            providerWidgetService: widgetService);
        await sessionA.InitializeAsync();
        var providerId = sessionA.Providers.Items.First().Id;
        await sessionA.Providers.EnsureWidgetLoadedAsync(providerId);

        var sessionB = new RoleplaySession(
            liveStore,
            new TestModelCapabilityCatalog(),
            providerWidgetService: widgetService);
        await sessionB.InitializeAsync();
        await sessionB.Providers.EnsureWidgetLoadedAsync(providerId);

        Assert.Equal(2, widgetService.RefreshCalls);
    }

    [Fact]
    public async Task ProviderWidgetManualRefreshAlwaysReloads()
    {
        var widgetService = new CountingProviderWidgetService();
        await using var liveStore = NewLiveStore();
        var session = new RoleplaySession(
            liveStore,
            new TestModelCapabilityCatalog(),
            providerWidgetService: widgetService);
        await session.InitializeAsync();
        var providerId = session.Providers.Items.First().Id;

        await session.Providers.RefreshWidgetAsync(providerId);
        await session.Providers.RefreshWidgetAsync(providerId);

        Assert.Equal(2, widgetService.RefreshCalls);
    }

    [Fact]
    public async Task EntityNotifierPublishesImageCropAndProfileChangesWithChatScope()
    {
        var notifier = new RecordingEntityNotifier();
        await using var liveStore = NewLiveStore();
        var session = new RoleplaySession(liveStore, new TestModelCapabilityCatalog(), entityNotifier: notifier);
        await session.InitializeAsync();
        var chatId = session.Chats.Active?.ChatId ?? "";
        var character = session.Chat.Characters.Items.First();
        var image = session.Chat.Images.Items.First();

        await session.Chat.Characters.SetImageAsync(character.Id, image.Id);
        character.Name = "Renamed Character";
        await session.Chat.Characters.MarkChangedAsync();
        await session.Chat.Images.SetCropAsync(image.Id, new(42, 58, 136));

        Assert.Contains(notifier.Notifications, notification =>
            notification.EntityType == EntityTypes.Character
            && notification.EntityId == character.Id
            && notification.ChangeKind == EntityChangeKinds.Image
            && notification.ImageId == image.Id
            && notification.ChatId == chatId);
        Assert.Contains(notifier.Notifications, notification =>
            notification.EntityType == EntityTypes.Character
            && notification.EntityId == character.Id
            && notification.ChangeKind == EntityChangeKinds.Profile
            && notification.ChatId == chatId);
        Assert.Contains(notifier.Notifications, notification =>
            notification.EntityType == EntityTypes.Character
            && notification.EntityId == character.Id
            && notification.ChangeKind == EntityChangeKinds.ImageCrop
            && notification.ImageId == image.Id
            && notification.ChatId == chatId);
    }

    [Fact]
    public async Task UnreferencedInactiveChatsCanUnloadAfterTtl()
    {
        await using var liveStore = NewLiveStore(TimeSpan.FromMilliseconds(10));
        var session = NewSession(liveStore);
        await session.InitializeAsync();

        Assert.True(liveStore.IsChatLoaded("ch1"));

        await session.Chats.SelectAsync("ch2");
        await Task.Delay(20);
        liveStore.CleanupExpiredChats();

        Assert.False(liveStore.IsChatLoaded("ch1"));
        Assert.True(liveStore.IsChatLoaded("ch2"));
    }

    [Fact]
    public async Task ChatAreaRerendersFromCrossSessionTranscriptNotification()
    {
        using var context = new BunitContext();
        ConfigureChatAreaContext(context);
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();
        var component = context.Render<ChatArea>(parameters => parameters.AddCascadingValue(sessionB));

        await sessionA.Chat.Transcript.PostManualAsync("Rendered from another session.", null);

        component.WaitForAssertion(() => Assert.Contains("Rendered from another session.", component.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChatAreaLocksFooterWhileTranscriptOperationRuns()
    {
        using var context = new BunitContext();
        ConfigureChatAreaContext(context);
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var component = context.Render<ChatArea>(parameters => parameters.AddCascadingValue(session));

        var operation = session.Chat.Transcript.GenerateAsync("", null, false, "automatic", "Brief");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        component.WaitForAssertion(() =>
        {
            Assert.NotNull(component.Find(".claude-composer.is-locked"));
            Assert.NotNull(component.Find("textarea[disabled]"));
            Assert.Contains("Generating...", component.Markup, StringComparison.Ordinal);
            Assert.False(session.Chat.Transcript.Options.ShowProcessTraces);
            Assert.NotNull(component.Find(".process-row.is-live"));
            Assert.DoesNotContain("process-steps", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Appearance", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Selection", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Planning", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Prose", component.Markup, StringComparison.Ordinal);
            Assert.Contains("process-flow-step is-pending", component.Markup, StringComparison.Ordinal);
            Assert.Contains("fa-spinner", component.Markup, StringComparison.Ordinal);
            Assert.True(component.FindAll(".claude-composer-actions button[disabled]").Count > 0);
        });

        await component.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", CtrlKey = true });
        Assert.Equal(1, generation.GenerateCalls);

        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));

        component.WaitForAssertion(() =>
        {
            Assert.Empty(component.FindAll(".process-row.is-live"));
            Assert.Contains(BlockingTextGenerationService.GeneratedBody, component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ChatAreaStreamsDraftTranscriptTurnUntilFinalCommit()
    {
        using var context = new BunitContext();
        ConfigureChatAreaContext(context);
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var component = context.Render<ChatArea>(parameters => parameters.AddCascadingValue(session));

        var operation = session.Chat.Transcript.GenerateAsync("", null, false, "automatic", "Brief");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        component.WaitForAssertion(() =>
        {
            Assert.NotNull(session.Chat.Transcript.ActiveDraftTurn);
            Assert.Equal(BlockingTextGenerationService.PartialBody, session.Chat.Transcript.ActiveDraftTurn?.Body);
            Assert.DoesNotContain(session.Chat.Transcript.Items, turn => turn.Body == BlockingTextGenerationService.PartialBody);
            Assert.Contains(BlockingTextGenerationService.PartialBody, component.Markup, StringComparison.Ordinal);
        });

        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));

        component.WaitForAssertion(() =>
        {
            Assert.Null(session.Chat.Transcript.ActiveDraftTurn);
            Assert.DoesNotContain(session.Chat.Transcript.Items, turn => turn.Body == BlockingTextGenerationService.PartialBody);
            Assert.Contains(session.Chat.Transcript.Items, turn => turn.Body == BlockingTextGenerationService.GeneratedBody);
            Assert.Contains(BlockingTextGenerationService.GeneratedBody, component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ChatMessageEditPlanShowsCapturedTtsInput()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        ConfigureChatAreaContext(context);
        var speech = new TestMessageSpeechService(new(
            "Ready",
            "ElevenLabs",
            "elevenlabs",
            "eleven_v3",
            DateTime.UtcNow,
            [new SpeechGenerationInput("Exact TTS model text.", "voice-1")]));
        context.Services.AddSingleton<IMessageSpeechService>(speech);
        await using var liveStore = NewLiveStore();
        var session = new RoleplaySession(liveStore, new TestModelCapabilityCatalog(), messageSpeechService: speech);
        await session.InitializeAsync();
        var message = session.Chat.Transcript.Items.Last();
        message.Speech.VoiceMessageId = "speech-test";

        var component = context.Render<ChatMessage>(parameters => parameters
            .AddCascadingValue(session)
            .Add(value => value.Message, message)
            .Add(value => value.Characters, session.Chat.Characters.Items.ToList())
            .Add(value => value.ShowAppearance, false)
            .Add(value => value.ShowProcess, false)
            .Add(value => value.TranscriptBusy, false)
            .Add(value => value.SubsequentCount, 0)
            .Add(value => value.OnOpenEntities, _ => Task.CompletedTask));

        await component.Find("button[title='Edit saved plan']").ClickAsync(new());
        await component.Find("button[title='Speech']").ClickAsync(new());

        Assert.Contains("Exact TTS model text.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("ElevenLabs", component.Markup, StringComparison.Ordinal);
        Assert.Contains("eleven_v3", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TranscriptStoreIgnoresOverlappingOperationsUntilCurrentOperationCompletes()
    {
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();

        var operation = session.Chat.Transcript.GenerateAsync("", null, false, "automatic", "Brief");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(session.Chat.Transcript.IsBusy);
        Assert.Equal("Generating...", session.Chat.Transcript.BusyMessage);

        await session.Chat.Transcript.PostManualAsync("This should not be posted while busy.", null);

        Assert.DoesNotContain(session.Chat.Transcript.Items, message => message.Body == "This should not be posted while busy.");
        Assert.Equal(1, generation.GenerateCalls);

        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(session.Chat.Transcript.IsBusy);
        Assert.Equal("", session.Chat.Transcript.BusyMessage);
        Assert.Contains(session.Chat.Transcript.Items, message => message.Body == BlockingTextGenerationService.GeneratedBody);
    }

    [Fact]
    public async Task SetSceneRunsNormalNarratorGenerationWithSceneOverride()
    {
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var location = session.Chat.Locations.Items.Last();
        var characterIds = session.Chat.Characters.Items.Take(2).Select(character => character.Id).ToList();
        var request = new SetSceneRequest(
            location.Id,
            characterIds,
            [],
            new(SceneNarratorGuidancePurpose.LocationTransition, "Guide the narrator into the next room without resolving the argument."));

        var operation = session.Chat.Transcript.SetSceneAsync(request);
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var generationRequest = generation.Requests.Single();
        Assert.Empty(generation.ProseRequests);
        Assert.True(generationRequest.RequestedNarrator);
        Assert.Equal("scene-transition", generationRequest.Mode);
        Assert.Equal(location.Id, generationRequest.SceneOverride?.LocationId);
        Assert.Equal(characterIds, generationRequest.SceneOverride?.InSceneCharacterIds);
        Assert.Contains("Scene setting purpose: location transition", generationRequest.Guidance, StringComparison.Ordinal);
        Assert.Contains("Guide the narrator into the next room", generationRequest.Guidance, StringComparison.Ordinal);

        generation.Release.SetResult();
        var result = await operation.WaitAsync(TimeSpan.FromSeconds(5));
        var turn = session.Chat.Transcript.Items.Last();
        Assert.NotNull(result);
        Assert.Equal("", turn.ActorCharacterId);
        Assert.Equal("Narrator", turn.ActorName);
        Assert.Equal(location.Id, turn.Scene.LocationId);
        Assert.Equal(characterIds, turn.Scene.InSceneCharacterIds);
    }

    [Fact]
    public void TurnShapePickerOptionsNormalizeKnownLabels()
    {
        Assert.Equal("Brief", TurnShapePickerOptions.Normalize("brief"));
        Assert.Equal("Brief", TurnShapePickerOptions.Normalize("Brief"));
        Assert.Equal("Narrative", TurnShapePickerOptions.Normalize("narrative"));
        Assert.Equal("Silent Extended", TurnShapePickerOptions.Normalize("silent-extended"));
        Assert.Equal("Silent Extended", TurnShapePickerOptions.Normalize("silent extended"));
        Assert.Equal("Monologue", TurnShapePickerOptions.Normalize("Monologue"));
        Assert.Equal("silent-monologue", TurnShapePickerOptions.Normalize("silent-monologue"));
        Assert.Equal("Brief", TurnShapePickerOptions.Normalize(""));
        Assert.Equal("Auto", TurnShapePickerOptions.Normalize("Auto"));
        Assert.Equal("Brief", TurnShapePickerOptions.NormalizeExplicit("Auto"));
    }

    static void ConfigureChatAreaContext(BunitContext context)
    {
        context.Services.AddScoped<DialogHelper>();
        context.Services.AddScoped<OverlayService>();
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        context.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
    }

    [Fact]
    public async Task RegenerationHidesOriginalTurnWhileRunning()
    {
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var original = session.Chat.Transcript.Items.Last();

        var operation = session.Chat.Transcript.RegenerateAsync(original.Id, original.Guidance, null, "Extended");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(session.Chat.Transcript.IsBusy);
        Assert.Equal("Regenerating...", session.Chat.Transcript.BusyMessage);
        Assert.DoesNotContain(session.Chat.Transcript.Items, turn => turn.Id == original.Id);
        Assert.Equal(original.ParentTurnId, session.Chat.Transcript.Items.Last().Id);

        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RegenerationCommitsSiblingWithRequestedTurnShape()
    {
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var original = session.Chat.Transcript.Items.Last();
        var originalShape = original.Plan.TurnShape;

        var operation = session.Chat.Transcript.RegenerateAsync(original.Id, original.Guidance, null, "Silent Extended");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));

        var regenerated = session.Chat.Transcript.Items.Last();
        Assert.NotEqual(original.Id, regenerated.Id);
        Assert.Equal(original.ParentTurnId, regenerated.ParentTurnId);
        Assert.Equal("Silent Extended", regenerated.Plan.TurnShape);
        Assert.Empty(generation.Requests);
        Assert.Equal("Silent Extended", generation.ProseRequests.Single().Plan.TurnShape);
        Assert.Equal(originalShape, original.Plan.TurnShape);
        Assert.Equal(2, session.Chat.Transcript.SiblingsFor(regenerated.Id).Count);
    }

    [Fact]
    public async Task RegenerationFromSavedPlanPreservesSavedPlanStateAndContext()
    {
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var original = session.Chat.Transcript.Items.Last();
        var actorId = original.ActorCharacterId;
        original.Plan.Beat = "Saved beat";
        original.Plan.Intent = "Saved intent";
        original.Plan.ImmediateGoal = "Saved goal";
        original.Plan.WhyNow = "Saved why now";
        original.Plan.ChangeIntroduced = "Saved change";
        original.Plan.Guardrails = "Saved guardrails";
        original.Plan.Data["marker"] = "saved-plan-data";
        original.AppearanceByCharacterId[actorId] = "Saved appearance";
        original.PrivateIntentByCharacterId[actorId] = "Saved private intent";
        original.Scene.Data["marker"] = "saved-scene-data";

        var operation = session.Chat.Transcript.RegenerateAsync(original.Id, original.Guidance, null, "");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));

        var request = generation.ProseRequests.Single();
        var regenerated = session.Chat.Transcript.Items.Last();
        Assert.Empty(generation.Requests);
        Assert.Equal("Saved beat", request.Plan.Beat);
        Assert.Equal("Saved beat", regenerated.Plan.Beat);
        Assert.Equal("Saved intent", regenerated.Plan.Intent);
        Assert.Equal("saved-plan-data", regenerated.Plan.Data["marker"]?.GetValue<string>());
        Assert.Equal("Saved appearance", regenerated.AppearanceByCharacterId[actorId]);
        Assert.Equal("Saved private intent", regenerated.PrivateIntentByCharacterId[actorId]);
        Assert.Equal("saved-scene-data", regenerated.Scene.Data["marker"]?.GetValue<string>());
    }

    [Fact]
    public async Task ReplanCommitsSiblingWithoutAppearanceOrSelection()
    {
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var original = session.Chat.Transcript.Items.Last();
        var actorId = original.ActorCharacterId;
        original.AppearanceByCharacterId[actorId] = "Saved appearance";

        var operation = session.Chat.Transcript.ReplanAsync(original.Id, "Make a sharper plan.", null, "Extended");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(session.Chat.Transcript.IsBusy);
        Assert.Equal("Planning new branch...", session.Chat.Transcript.BusyMessage);
        Assert.DoesNotContain(session.Chat.Transcript.ActiveTrace?.Steps ?? [], step => step.Id is "appearance" or "selection");
        Assert.Contains(session.Chat.Transcript.ActiveTrace?.Steps ?? [], step => step.Id == "planning");
        Assert.Contains(session.Chat.Transcript.ActiveTrace?.Steps ?? [], step => step.Id == "prose");

        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));

        var request = generation.PlanAndProseRequests.Single();
        var replanned = session.Chat.Transcript.Items.Last();
        Assert.Empty(generation.Requests);
        Assert.Empty(generation.ProseRequests);
        Assert.NotEqual(original.Id, replanned.Id);
        Assert.Equal(original.ParentTurnId, replanned.ParentTurnId);
        Assert.Equal("replanned", replanned.Mode);
        Assert.Equal("Extended", request.RequestedTurnShape);
        Assert.Equal("Saved appearance", request.AppearanceByCharacterId[actorId]);
        Assert.Equal("Saved appearance", replanned.AppearanceByCharacterId[actorId]);
        Assert.Equal(2, session.Chat.Transcript.SiblingsFor(replanned.Id).Count);
    }

    [Fact]
    public async Task FailedRegenerationCommitsFailedSiblingAndKeepsOriginalHidden()
    {
        var generation = new FailingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var original = session.Chat.Transcript.Items.Last();

        await session.Chat.Transcript.RegenerateAsync(original.Id, original.Guidance, null, "Extended");

        var failedTurn = session.Chat.Transcript.Items.Last();
        Assert.NotEqual(original.Id, failedTurn.Id);
        Assert.Equal(original.ParentTurnId, failedTurn.ParentTurnId);
        Assert.Equal("Extended", failedTurn.Plan.TurnShape);
        Assert.Equal("failed", failedTurn.Trace?.Status);
        Assert.DoesNotContain(session.Chat.Transcript.Items, turn => turn.Id == original.Id);
        Assert.Equal(2, session.Chat.Transcript.SiblingsFor(failedTurn.Id).Count);
    }

    [Fact]
    public async Task TranscriptStoreClearsLiveTraceAndPersistsFailedTrace()
    {
        var generation = new FailingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();

        await session.Chat.Transcript.GenerateAsync("", null, true, "automatic", "Brief");

        Assert.Null(session.Chat.Transcript.ActiveTrace);
        var failedTurn = session.Chat.Transcript.Items.Last();
        Assert.Equal("failed", failedTurn.Trace?.Status);
        Assert.Equal("failed", failedTurn.Trace?.Steps.Single().Status);
        Assert.NotNull(session.Chat.Transcript.LastBackgroundError);
    }

    [Fact]
    public async Task TranscriptStoreClearsFailedDraftWithoutPersistingPartialBody()
    {
        var generation = new FailingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();

        await session.Chat.Transcript.GenerateAsync("", null, true, "automatic", "Brief");

        Assert.Null(session.Chat.Transcript.ActiveDraftTurn);
        Assert.DoesNotContain(session.Chat.Transcript.Items, turn => turn.Body == FailingTextGenerationService.PartialBody);
        Assert.NotNull(session.Chat.Transcript.LastBackgroundError);
    }

    static LiveRoleplayStore NewLiveStore(TimeSpan? ttl = null) =>
        new(new SeedRoleplayPersistence(), ttl ?? TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

    static RoleplaySession NewSession(LiveRoleplayStore liveStore, ITextGenerationService? generator = null) =>
        new(liveStore, new TestModelCapabilityCatalog(), generator);

    static RpTurnPlan ClonePlan(RpTurnPlan plan) => new()
    {
        TurnShape = plan.TurnShape,
        Beat = plan.Beat,
        Intent = plan.Intent,
        ImmediateGoal = plan.ImmediateGoal,
        WhyNow = plan.WhyNow,
        ChangeIntroduced = plan.ChangeIntroduced,
        Guardrails = plan.Guardrails,
        Data = plan.Data.DeepClone().AsObject()
    };

    sealed class BlockingTextGenerationService : ITextGenerationService
    {
        public const string GeneratedBody = "Generated while lock is held.";
        public const string PartialBody = "Partial streamed body.";
        int _generateCalls;

        public int GenerateCalls => _generateCalls;
        public List<GenerateTurnRequest> Requests { get; } = [];
        public List<GeneratePlanAndProseRequest> PlanAndProseRequests { get; } = [];
        public List<GenerateProseFromPlanRequest> ProseRequests { get; } = [];
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<GeneratedTurnResult> GenerateTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateTurnRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _generateCalls);
            Requests.Add(request);
            var startedUtc = DateTime.UtcNow;
            var trace = new RpTurnTrace
            {
                Summary = "Generating · Appearance · Appearance -> Selection -> Planning -> Prose",
                Status = "running",
                StartedUtc = startedUtc,
                Steps =
                [
                    new() { Id = "appearance", Label = "Appearance", Status = "running", StartedUtc = startedUtc },
                    new() { Id = "selection", Label = "Selection", Status = "pending" },
                    new() { Id = "planning", Label = "Planning", Status = "pending" },
                    new() { Id = "prose", Label = "Prose", Status = "pending" }
                ]
            };
            if (progress is not null)
            {
                await progress.ReportAsync(trace);
                await progress.ReportProseAsync(new(
                    request.ParentTurnId,
                    request.Mode,
                    request.Guidance,
                    "",
                    "Narrator",
                    new() { TurnShape = request.RequestedTurnShape },
                    CloneScene(request.SceneOverride ?? document.Transcript.RootScene),
                    PartialBody));
            }

            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            trace.Status = "completed";
            trace.CompletedUtc = DateTime.UtcNow;
            trace.DurationSeconds = (trace.CompletedUtc - trace.StartedUtc).TotalSeconds;
            trace.Steps[0].Status = "completed";
            trace.Steps[0].CompletedUtc = trace.CompletedUtc;
            trace.Steps[0].DurationSeconds = trace.DurationSeconds;

            return new(
                "",
                "Narrator",
                new() { TurnShape = request.RequestedTurnShape },
                [],
                [],
                CloneScene(request.SceneOverride ?? document.Transcript.RootScene),
                GeneratedBody,
                trace);
        }

        public async Task<GeneratedTurnResult> GeneratePlanAndProseAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GeneratePlanAndProseRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _generateCalls);
            PlanAndProseRequests.Add(request);
            var startedUtc = DateTime.UtcNow;
            var trace = new RpTurnTrace
            {
                Summary = "Generating Â· Planning -> Prose",
                Status = "running",
                StartedUtc = startedUtc,
                Steps =
                [
                    new() { Id = "planning", Label = "Planning", Status = "running", StartedUtc = startedUtc },
                    new() { Id = "prose", Label = "Prose", Status = "pending" }
                ]
            };
            var plan = new RpTurnPlan { TurnShape = request.RequestedTurnShape };
            if (progress is not null)
            {
                await progress.ReportAsync(trace);
                await progress.ReportProseAsync(new(
                    request.ParentTurnId,
                    request.Mode,
                    request.Guidance,
                    request.ActorCharacterId,
                    string.IsNullOrWhiteSpace(request.ActorName) ? "Narrator" : request.ActorName,
                    ClonePlan(plan),
                    CloneScene(request.Scene),
                    PartialBody));
            }

            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            trace.Status = "completed";
            trace.CompletedUtc = DateTime.UtcNow;
            trace.DurationSeconds = (trace.CompletedUtc - trace.StartedUtc).TotalSeconds;
            trace.Steps[0].Status = "completed";
            trace.Steps[0].CompletedUtc = trace.CompletedUtc;
            trace.Steps[0].DurationSeconds = trace.DurationSeconds;

            return new(
                request.ActorCharacterId,
                string.IsNullOrWhiteSpace(request.ActorName) ? "Narrator" : request.ActorName,
                plan,
                request.AppearanceByCharacterId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                [],
                CloneScene(request.Scene),
                GeneratedBody,
                trace);
        }

        public async Task<GeneratedTurnResult> GenerateProseFromPlanAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateProseFromPlanRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _generateCalls);
            ProseRequests.Add(request);
            var startedUtc = DateTime.UtcNow;
            var trace = new RpTurnTrace
            {
                Summary = "Generating · Prose",
                Status = "running",
                StartedUtc = startedUtc,
                Steps =
                [
                    new() { Id = "prose", Label = "Prose", Status = "running", StartedUtc = startedUtc }
                ]
            };
            if (progress is not null)
            {
                await progress.ReportAsync(trace);
                await progress.ReportProseAsync(new(
                    request.ParentTurnId,
                    request.Mode,
                    request.Guidance,
                    request.ActorCharacterId,
                    string.IsNullOrWhiteSpace(request.ActorName) ? "Narrator" : request.ActorName,
                    ClonePlan(request.Plan),
                    CloneScene(request.Scene),
                    PartialBody));
            }

            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            trace.Status = "completed";
            trace.CompletedUtc = DateTime.UtcNow;
            trace.DurationSeconds = (trace.CompletedUtc - trace.StartedUtc).TotalSeconds;
            trace.Steps[0].Status = "completed";
            trace.Steps[0].CompletedUtc = trace.CompletedUtc;
            trace.Steps[0].DurationSeconds = trace.DurationSeconds;

            return new(
                request.ActorCharacterId,
                string.IsNullOrWhiteSpace(request.ActorName) ? "Narrator" : request.ActorName,
                ClonePlan(request.Plan),
                request.AppearanceByCharacterId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                request.PrivateIntentByCharacterId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                CloneScene(request.Scene),
                GeneratedBody,
                trace);
        }

        public Task<GeneratedSnapshotResult> GenerateSnapshotAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateSnapshotRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        static RpSceneFrame CloneScene(RpSceneFrame scene) => new()
        {
            LocationId = scene.LocationId,
            LocationName = scene.LocationName,
            InSceneCharacterIds = [.. scene.InSceneCharacterIds],
            InSceneItemIds = [.. scene.InSceneItemIds],
            Data = scene.Data.DeepClone().AsObject()
        };
    }

    sealed class RecordingEntityNotifier : IEntityNotifier
    {
        public List<EntityChangeNotification> Notifications { get; } = [];
        public event Func<EntityChangeNotification, Task>? Changed;

        public async Task PublishAsync(EntityChangeNotification notification)
        {
            Notifications.Add(notification);
            var changed = Changed;
            if (changed is not null)
                await changed(notification);
        }
    }

    sealed class FailingTextGenerationService : ITextGenerationService
    {
        public const string PartialBody = "Failed partial streamed body.";

        public async Task<GeneratedTurnResult> GenerateTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateTurnRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default)
        {
            var startedUtc = DateTime.UtcNow;
            var trace = new RpTurnTrace
            {
                Summary = "Generating · Prose",
                Status = "running",
                StartedUtc = startedUtc,
                Steps =
                [
                    new() { Id = "prose", Label = "Prose", Status = "running", StartedUtc = startedUtc }
                ]
            };
            if (progress is not null)
            {
                await progress.ReportAsync(trace);
                await progress.ReportProseAsync(new(
                    request.ParentTurnId,
                    request.Mode,
                    request.Guidance,
                    request.RequestedActorCharacterId,
                    string.IsNullOrWhiteSpace(request.RequestedActorName) ? "Narrator" : request.RequestedActorName,
                    new() { TurnShape = request.RequestedTurnShape },
                    CloneScene(request.SceneOverride ?? document.Transcript.RootScene),
                    PartialBody));
            }

            trace.Status = "failed";
            trace.CompletedUtc = DateTime.UtcNow;
            trace.DurationSeconds = (trace.CompletedUtc - trace.StartedUtc).TotalSeconds;
            trace.Data["error"] = "Prose failed for test.";
            trace.Steps[0].Status = "failed";
            trace.Steps[0].CompletedUtc = trace.CompletedUtc;
            trace.Steps[0].DurationSeconds = trace.DurationSeconds;
            trace.Steps[0].Error = "Prose failed for test.";
            throw new TranscriptGenerationException("Prose failed for test.", trace);
        }

        public async Task<GeneratedTurnResult> GeneratePlanAndProseAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GeneratePlanAndProseRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default)
        {
            var startedUtc = DateTime.UtcNow;
            var trace = new RpTurnTrace
            {
                Summary = "Generating Â· Planning -> Prose",
                Status = "running",
                StartedUtc = startedUtc,
                Steps =
                [
                    new() { Id = "planning", Label = "Planning", Status = "running", StartedUtc = startedUtc },
                    new() { Id = "prose", Label = "Prose", Status = "pending" }
                ]
            };
            if (progress is not null)
            {
                await progress.ReportAsync(trace);
                await progress.ReportProseAsync(new(
                    request.ParentTurnId,
                    request.Mode,
                    request.Guidance,
                    request.ActorCharacterId,
                    string.IsNullOrWhiteSpace(request.ActorName) ? "Narrator" : request.ActorName,
                    new() { TurnShape = request.RequestedTurnShape },
                    CloneScene(request.Scene),
                    PartialBody));
            }

            trace.Status = "failed";
            trace.CompletedUtc = DateTime.UtcNow;
            trace.DurationSeconds = (trace.CompletedUtc - trace.StartedUtc).TotalSeconds;
            trace.Data["error"] = "Planning failed for test.";
            trace.Steps[0].Status = "failed";
            trace.Steps[0].CompletedUtc = trace.CompletedUtc;
            trace.Steps[0].DurationSeconds = trace.DurationSeconds;
            trace.Steps[0].Error = "Planning failed for test.";
            throw new TranscriptGenerationException("Planning failed for test.", trace);
        }

        public async Task<GeneratedTurnResult> GenerateProseFromPlanAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateProseFromPlanRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default)
        {
            var startedUtc = DateTime.UtcNow;
            var trace = new RpTurnTrace
            {
                Summary = "Generating · Prose",
                Status = "running",
                StartedUtc = startedUtc,
                Steps =
                [
                    new() { Id = "prose", Label = "Prose", Status = "running", StartedUtc = startedUtc }
                ]
            };
            if (progress is not null)
            {
                await progress.ReportAsync(trace);
                await progress.ReportProseAsync(new(
                    request.ParentTurnId,
                    request.Mode,
                    request.Guidance,
                    request.ActorCharacterId,
                    string.IsNullOrWhiteSpace(request.ActorName) ? "Narrator" : request.ActorName,
                    ClonePlan(request.Plan),
                    new()
                    {
                        LocationId = request.Scene.LocationId,
                        LocationName = request.Scene.LocationName,
                        InSceneCharacterIds = [.. request.Scene.InSceneCharacterIds],
                        InSceneItemIds = [.. request.Scene.InSceneItemIds],
                        Data = request.Scene.Data.DeepClone().AsObject()
                    },
                    PartialBody));
            }

            trace.Status = "failed";
            trace.CompletedUtc = DateTime.UtcNow;
            trace.DurationSeconds = (trace.CompletedUtc - trace.StartedUtc).TotalSeconds;
            trace.Data["error"] = "Prose failed for test.";
            trace.Steps[0].Status = "failed";
            trace.Steps[0].CompletedUtc = trace.CompletedUtc;
            trace.Steps[0].DurationSeconds = trace.DurationSeconds;
            trace.Steps[0].Error = "Prose failed for test.";
            throw new TranscriptGenerationException("Prose failed for test.", trace);
        }

        public Task<GeneratedSnapshotResult> GenerateSnapshotAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateSnapshotRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        static RpSceneFrame CloneScene(RpSceneFrame scene) => new()
        {
            LocationId = scene.LocationId,
            LocationName = scene.LocationName,
            InSceneCharacterIds = [.. scene.InSceneCharacterIds],
            InSceneItemIds = [.. scene.InSceneItemIds],
            Data = scene.Data.DeepClone().AsObject()
        };
    }

    sealed class TestModelCapabilityCatalog : IModelCapabilityCatalog
    {
        public string UserCatalogPath => "";

        public ModelGenerationCapabilities Resolve(AiProvider provider, AiProviderModel model) => model.Capabilities;

        public ModelGenerationCapabilities Resolve(string providerType, string modelId) => ModelGenerationCapabilities.Fallback;

        public void ApplyResolvedCapabilities(AiProvider provider)
        {
        }

        public void SaveUserCapabilities(string providerType, string modelId, ModelGenerationCapabilities capabilities)
        {
        }

        public void UpdateLiveGrokCapabilities(JsonNode languageModelsJson)
        {
        }
    }

    sealed class CountingProviderWidgetService : IAiProviderWidgetService
    {
        int _refreshCalls;

        public int RefreshCalls => _refreshCalls;

        public Task<IReadOnlyList<AiProviderMetric>> RefreshMetricsAsync(AiProvider provider, CancellationToken cancellationToken = default)
        {
            var count = Interlocked.Increment(ref _refreshCalls);
            return Task.FromResult<IReadOnlyList<AiProviderMetric>>(
            [
                new()
                {
                    Id = $"metric-{count}",
                    Kind = "test",
                    Label = "Test",
                    Value = count.ToString(),
                    RefreshedUtc = DateTime.UtcNow
                }
            ]);
        }

        public Task<IReadOnlyList<ManagedEndpointStatusView>> GetHuggingFaceStatusesAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ManagedEndpointStatusView>>([]);

        public Task<ManagedEndpointStatusView> ExecuteHuggingFaceActionAsync(AiProvider provider, AiProviderModel model, ManagedEndpointAction action, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    sealed class TestMessageSpeechService(MessageSpeechInputSnapshot? snapshot) : IMessageSpeechService
    {
        public MessageSpeechAvailability ResolveAvailability(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, RpTranscriptTurn turn) =>
            new(MessageSpeechAvailabilityKind.NoVoiceModel);

        public MessageSpeechAvailability ResolveSnapshotAvailability(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, RpTranscriptSnapshot snapshot) =>
            new(MessageSpeechAvailabilityKind.NoVoiceModel);

        public Task<MessageSpeechPlayback> GetOrGenerateAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, RpTranscriptTurn turn, bool regenerate, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageSpeechPlayback(MessageSpeechService.PlaybackKey(turn), "", false));

        public Task<MessageSpeechPlayback> GetOrGenerateSnapshotAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, RpTranscriptSnapshot snapshot, bool regenerate, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageSpeechPlayback(MessageSpeechService.SnapshotPlaybackKey(snapshot), "", false));

        public Task<MessageSpeechInputSnapshot?> LoadInputSnapshotAsync(RpTranscriptTurn turn, CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public Task DiscardTurnSpeechAsync(RpTranscriptTurn turn, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DiscardSnapshotSpeechAsync(RpTranscriptSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
