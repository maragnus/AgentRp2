using System.Reflection;
using System.Collections;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
using System.Text.Json.Nodes;

namespace AgentRp.Tests;

public sealed class TextGenerationServiceTests
{
    [Fact]
    public async Task StructuredGenerationRunsStructuredStagesBeforeProse()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        var result = await service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            new("turn-3", "automatic", "", "Brief", "", ""));

        Assert.Equal(["AppearanceResponse", "SelectionResponse", "PlanningResponse"], client.StructuredCalls);
        Assert.Equal(1, client.StreamingTextCalls);
        Assert.Equal("Gemma", result.ActorName);
        Assert.Equal("Generated prose", result.Body);
        Assert.Contains(result.Scene.SceneObjects, item => item.Name == "Glass of water" && item.State == "full and cold");
        Assert.Contains(result.Scene.CharacterPhysicalStates, state => state.CharacterId == "c2" && state.LeftHand.Contains("glass", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["scene-continuity", "selection", "planning", "prose"], result.Trace.Steps.Select(step => step.Id));
    }

    [Fact]
    public async Task StructuredGenerationReportsLiveStepProgress()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        var reports = new List<RpGenerationTrace>();

        await service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            ActiveModelSelectionsState.CreateDefault(),
            new("turn-3", "automatic", "", "Brief", "", ""),
            new(trace =>
            {
                reports.Add(CloneTrace(trace));
                return Task.CompletedTask;
            }));

        Assert.NotEmpty(reports);
        Assert.Equal(["scene-continuity", "selection", "planning", "prose"], reports.First().Steps.Select(step => step.Id));
        Assert.All(reports.First().Steps, step => Assert.Equal("pending", step.Status));
        Assert.Contains(reports, report => report.Steps.First(step => step.Id == "scene-continuity").Status == "running");
        Assert.Contains(reports, report =>
            report.Steps.First(step => step.Id == "scene-continuity").Status == "completed" &&
            report.Steps.First(step => step.Id == "selection").Status == "running");
        Assert.Equal("completed", reports.Last().Status);
        Assert.All(reports.Last().Steps, step => Assert.Equal("completed", step.Status));
    }

    [Fact]
    public async Task SelectedCyoaTurnPrependsChoicesAndRunsFullStructuredTurnPipeline()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        var choicesStarted = DateTime.UtcNow.AddSeconds(-5);
        var decision = new RpCyoaPendingDecision
        {
            Id = "cyoa-test",
            ParentTurnId = "turn-3",
            Mode = RpCyoaModes.Adventure,
            ActorCharacterId = "c2",
            ActorName = "Gemma",
            CreatedUtc = choicesStarted,
            Trace = new()
            {
                Summary = "Completed - Gemma - Choices",
                Status = "completed",
                StartedUtc = choicesStarted,
                CompletedUtc = choicesStarted.AddSeconds(1),
                Steps =
                [
                    new()
                    {
                        Id = "cyoa-options",
                        Label = "Choices",
                        Status = "completed",
                        StartedUtc = choicesStarted,
                        CompletedUtc = choicesStarted.AddSeconds(1),
                        DurationSeconds = 1
                    }
                ]
            }
        };
        var option = new RpCyoaOption
        {
            Id = "option-continue",
            Direction = RpCyoaDirections.Continue,
            Title = "Keep pressure on",
            Summary = "Gemma presses the current beat.",
            Guidance = "Press the current beat.",
            ActorCharacterId = "c2",
            ActorName = "Gemma",
            Plan = new()
            {
                TurnShape = "Brief",
                Beat = "Seed beat",
                Intent = "Seed intent",
                ImmediateGoal = "Seed goal",
                WhyNow = "Seed why now",
                ChangeIntroduced = "Seed change",
                Guardrails = "Seed guardrails"
            }
        };

        var result = await service.GenerateSelectedCyoaTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            ActiveModelSelectionsState.CreateDefault(),
            new(decision, option, ""));

        Assert.Equal(["AppearanceResponse", "PlanningResponse"], client.StructuredCalls);
        Assert.Equal(1, client.StreamingTextCalls);
        Assert.Equal(["cyoa-options", "scene-continuity", "planning", "prose"], result.Trace.Steps.Select(step => step.Id));
        Assert.All(result.Trace.Steps, step => Assert.Equal("completed", step.Status));
        Assert.Equal("Gemma", result.ActorName);
        var planning = client.GenerationRequests.First(request => request.OperationName == "Planning transcript turn");
        Assert.Contains("Press the current beat.", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Choice planning seed:", planning.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CyoaFastForwardDecisionUsesConcreteOptionTextWhenSceneGuidanceIsBlank()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        var result = await service.GenerateCyoaDecisionAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            ActiveModelSelectionsState.CreateDefault(),
            new("turn-3", RpCyoaModes.Adventure, "c2", "Gemma", false));

        var fastForward = result.Decision.Options.Single(option => option.Direction == RpCyoaDirections.FastForward);
        Assert.NotNull(fastForward.SceneProposal);
        Assert.Equal("Fast-forward six hours to sunrise. Alex and Elena have spent the night reviewing leads off the notebook and are now outlining next steps at the same desks as the city wakes up.", fastForward.SceneProposal.Guidance);
    }

    [Fact]
    public async Task ProseStreamingReportsPartialTextBeforeFinalResult()
    {
        var client = new FakeModelGenerationClient { StreamingTextDeltas = ["Generated ", "prose"] };
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        var proseReports = new List<TranscriptProseUpdate>();

        var result = await service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            ActiveModelSelectionsState.CreateDefault(),
            new("turn-3", "automatic", "Keep moving.", "Brief", "", ""),
            new(_ => Task.CompletedTask, update =>
            {
                proseReports.Add(update);
                return Task.CompletedTask;
            }));

        Assert.Equal("Generated prose", result.Body);
        Assert.Equal(["", "Generated ", "Generated prose"], proseReports.Select(report => report.Body));
        Assert.All(proseReports, report =>
        {
            Assert.Equal("turn-3", report.ParentTurnId);
            Assert.Equal("automatic", report.Mode);
            Assert.Equal("Keep moving.", report.Guidance);
            Assert.Equal("Gemma", report.ActorName);
            Assert.Equal("Brief", report.Plan.TurnShape);
        });
    }

    [Fact]
    public async Task DumbProseGenerationReportsOnlyProseProgress()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        var reports = new List<RpGenerationTrace>();

        await service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = false, Streaming = true })],
            ActiveModelSelectionsState.CreateDefault(),
            new("turn-3", "automatic", "", "Brief", "c1", "Bella"),
            new(trace =>
            {
                reports.Add(CloneTrace(trace));
                return Task.CompletedTask;
            }));

        Assert.NotEmpty(reports);
        Assert.All(reports, report => Assert.Equal(["prose"], report.Steps.Select(step => step.Id)));
        Assert.Contains(reports, report => report.Steps.Single().Status == "running");
        Assert.Equal("completed", reports.Last().Status);
        Assert.Equal("completed", reports.Last().Steps.Single().Status);
    }

    [Fact]
    public async Task FailedGenerationReportsFailedRunningStep()
    {
        var client = new FakeModelGenerationClient { FailStreamingText = true };
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        var reports = new List<RpGenerationTrace>();

        var exception = await Assert.ThrowsAsync<TranscriptGenerationException>(() => service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = false, Streaming = true })],
            ActiveModelSelectionsState.CreateDefault(),
            new("turn-3", "automatic", "", "Brief", "c1", "Bella"),
            new(trace =>
            {
                reports.Add(CloneTrace(trace));
                return Task.CompletedTask;
            })));

        Assert.Equal("failed", exception.Trace.Status);
        Assert.Equal("failed", exception.Trace.Steps.Single().Status);
        Assert.Equal("failed", reports.Last().Status);
        Assert.Equal("failed", reports.Last().Steps.Single().Status);
    }

    [Fact]
    public async Task StructuredGenerationRendersPromptLibraryDefaultsAndPlannerPrivateIntent()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        var result = await service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            new("turn-3", "automatic", "", "Brief", "", ""));

        var appearance = client.GenerationRequests.First(request => request.OperationName == "Reconciling scene continuity");
        var planning = client.GenerationRequests.First(request => request.OperationName == "Planning transcript turn");
        var prose = client.GenerationRequests.First(request => request.OperationName == "Writing transcript prose");

        Assert.Contains("You update scene continuity state.", appearance.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Prior physical/body/object scene ledger:", appearance.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Private Intent usage:", planning.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Continuity Intent usage:", planning.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Turn shape definitions:", planning.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Required turn shape: Brief", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Turn shape definition:", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("- brief = one action beat, one to two short lines with a tag in between (rare)", planning.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("- silent extended =", planning.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Prioritize compact", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Test private intent", prose.UserPrompt, StringComparison.Ordinal);
        Assert.EndsWith(PromptLibraryService.ProseFormatReminder, prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Every action must include an explicit subject pronoun or character name", prose.UserPrompt, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(prose.UserPrompt, PromptLibraryService.ProseFormatReminder));
        Assert.Contains("This turn has a brief shape", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("**Actor:** Gemma", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("- Gemma only", planning.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("**Actor:** Bella", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("You are Gemma", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Equal("Brief", result.Plan.TurnShape);
    }

    [Fact]
    public async Task AutoTurnShapeLetsStructuredPlannerChooseFromDefinitions()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        var result = await service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            new("turn-3", "automatic", "", "Auto", "", ""));

        var planning = client.GenerationRequests.First(request => request.OperationName == "Planning transcript turn");
        var prose = client.GenerationRequests.First(request => request.OperationName == "Writing transcript prose");

        Assert.Contains("Choose the turn shape that best fits this turn.", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Turn shape definitions:", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("- compact =", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("- narrative =", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("- Never end a conversation.", planning.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Required turn shape:", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("This turn has a brief shape", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Equal("Brief", result.Plan.TurnShape);
    }

    [Fact]
    public async Task ProsePromptUsesAgentRp1StrictGuidanceHeading()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        await service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            new("turn-3", "automatic", "Keep this sharp.", "Brief", "", ""));

        var prose = client.GenerationRequests.First(request => request.OperationName == "Writing transcript prose");

        Assert.Contains("**Guidance to follow strictly:**\nKeep this sharp.", prose.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProsePromptInjectsElevenLabsAudioTagGuideFromActiveVoiceProvider()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        document.Transcript.Options.InjectAudioTags = true;

        await service.GenerateTurnAsync(
            document,
            [
                BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true }),
                BuildProvider(new() { TextInput = true, SpeechOutput = true }, "elevenlabs", AiModelRole.Voice, "voice-provider", "eleven_v3")
            ],
            new("turn-3", "automatic", "", "Brief", "", ""));

        var prose = client.GenerationRequests.First(request => request.OperationName == "Writing transcript prose");

        Assert.Contains("Audio tag guidance for ElevenLabs v3", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("[whispers]", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Audio tag reminder:", prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Inject supported ElevenLabs-style square-bracket tags directly into the prose", prose.UserPrompt, StringComparison.Ordinal);
        Assert.EndsWith(PromptLibraryService.ProseFormatReminder, prose.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProsePromptInjectsXAiAudioTagGuideFromActiveVoiceProvider()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        document.Transcript.Options.InjectAudioTags = true;

        await service.GenerateTurnAsync(
            document,
            [
                BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true }),
                BuildProvider(new() { TextInput = true, SpeechOutput = true }, "grok", AiModelRole.Voice, "voice-provider", "voice-model")
            ],
            new("turn-3", "automatic", "", "Brief", "", ""));

        var prose = client.GenerationRequests.First(request => request.OperationName == "Writing transcript prose");

        Assert.Contains("Audio tag guidance for xAI text-to-speech", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("<whisper>quiet text</whisper>", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Audio tag reminder:", prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Inject supported xAI-compatible speech tags directly into the prose", prose.UserPrompt, StringComparison.Ordinal);
        Assert.EndsWith(PromptLibraryService.ProseFormatReminder, prose.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProsePromptDoesNotInjectAudioTagGuideForUnsupportedElevenLabsVoiceModel()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        document.Transcript.Options.InjectAudioTags = true;

        await service.GenerateTurnAsync(
            document,
            [
                BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true }),
                BuildProvider(new() { TextInput = true, SpeechOutput = true }, "elevenlabs", AiModelRole.Voice, "voice-provider", "eleven_multilingual_v2")
            ],
            new("turn-3", "automatic", "", "Brief", "", ""));

        var prose = client.GenerationRequests.First(request => request.OperationName == "Writing transcript prose");

        Assert.DoesNotContain("Audio tag guidance", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Audio tag reminder", prose.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProsePromptDoesNotInjectAudioTagGuideWhenOptionIsOff()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        await service.GenerateTurnAsync(
            document,
            [
                BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true }),
                BuildProvider(new() { TextInput = true, SpeechOutput = true }, "elevenlabs", AiModelRole.Voice, "voice-provider", "voice-model")
            ],
            new("turn-3", "automatic", "", "Brief", "", ""));

        var prose = client.GenerationRequests.First(request => request.OperationName == "Writing transcript prose");

        Assert.DoesNotContain("Audio tag guidance", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Audio tag reminder", prose.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitNarratorGenerationUsesNarratorTuningWithoutCharacterContext()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        document.NarratorProfile.VoicePreset = "tense-foreshadowing";
        document.NarratorProfile.Foreshadowing = 2;
        document.NarratorProfile.CustomGuidance = "Frame the room like something is about to break.";

        var result = await service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            new("turn-3", "guided", "Set the next scene.", "Extended", "", "", true));

        var planning = client.GenerationRequests.First(request => request.OperationName == "Planning transcript turn");
        var prose = client.GenerationRequests.First(request => request.OperationName == "Writing transcript prose");

        Assert.Equal(["AppearanceResponse", "PlanningResponse"], client.StructuredCalls);
        Assert.Equal("", result.ActorCharacterId);
        Assert.Equal("Narrator", result.ActorName);
        Assert.Empty(result.PrivateIntentByCharacterId);
        Assert.Contains("Narrator voice tuning:", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Narrator staging only", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Do not write dialogue, internal monologue, new emotional reactions", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Tense Foreshadowing", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Frame the room like something is about to break.", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Narrator contract:", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("NEVER speak as, quote, roleplay, decide for, or take a turn as any character", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("You may summarize transitional action, elapsed time, travel, mundane logistics", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains(PromptLibraryService.NarratorWardrobeGuidance, prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("End with the scene staged so a character can react next", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Narrator turn shape:", prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Treat \"brief\" as a length and pacing request only", prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains(PromptLibraryService.NarratorWardrobeGuidance, prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Do not include quoted speech, internal monologue, new character reactions", prose.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("spoken lines", prose.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("speech in \"quotes\"", prose.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(PromptLibraryService.NarratorProseFormatReminder, prose.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain(PromptLibraryService.ProseFormatReminder, prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Do not include quoted character speech", prose.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("**Actor:** Gemma", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Equal(["scene-continuity", "planning", "prose"], result.Trace.Steps.Select(step => step.Id));
    }

    [Fact]
    public async Task AutomaticGenerationDoesNotReceiveNarratorTuning()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        document.NarratorProfile.CustomGuidance = "This should only affect narrator turns.";

        await service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            new("turn-3", "automatic", "", "Brief", "", ""));

        Assert.All(client.GenerationRequests, request =>
        {
            Assert.DoesNotContain("Narrator voice tuning:", request.SystemPrompt, StringComparison.Ordinal);
            Assert.DoesNotContain("Narrator voice tuning:", request.UserPrompt, StringComparison.Ordinal);
            Assert.DoesNotContain("This should only affect narrator turns.", request.SystemPrompt, StringComparison.Ordinal);
            Assert.DoesNotContain("This should only affect narrator turns.", request.UserPrompt, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SnapshotGenerationReturnsSummaryAndTimelineEntries()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        var result = await service.GenerateSnapshotAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            new("turn-3"));

        var snapshotRequest = client.GenerationRequests.First(request => request.OperationName == "Generating snapshot");

        Assert.Contains("Return a concise narrative summary, then propose timeline entries", snapshotRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Thread title: Devonshire Games", snapshotRequest.UserPrompt, StringComparison.Ordinal);
        Assert.Equal("Test snapshot narrative", result.Summary);
        var timelineEntry = Assert.Single(result.TimelineEntries);
        Assert.Equal(3, timelineEntry.TurnNumber);
        Assert.Equal("Test event", timelineEntry.Title);
        Assert.Equal("completed", result.Trace.Status);
    }

    [Fact]
    public async Task SnapshotGenerationRequiresReasoningModelSelection()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateSnapshotAsync(
            document,
            [],
            new("turn-3")));

        Assert.Contains("reasoning model", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DumbProseModeRequiresExplicitRespondAs()
    {
        var service = new TextGenerationService(new FakeModelGenerationClient(), new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        var exception = await Assert.ThrowsAsync<TranscriptGenerationException>(() => service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = false, Streaming = true })],
            new("turn-3", "automatic", "", "Brief", "", "")));

        Assert.Contains("Respond As", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DumbProseModeSkipsStructuredStagesWhenActorIsExplicit()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        var result = await service.GenerateTurnAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = false, Streaming = true })],
            new("turn-3", "automatic", "", "Brief", "c1", "Bella"));

        Assert.Empty(client.StructuredCalls);
        Assert.Equal(1, client.StreamingTextCalls);
        Assert.Equal("Bella", result.ActorName);
        Assert.Empty(result.AppearanceByCharacterId);
        Assert.Empty(result.PrivateIntentByCharacterId);
        Assert.Equal(["prose"], result.Trace.Steps.Select(step => step.Id));
    }

    [Fact]
    public async Task ProseOnlyGenerationSkipsStructuredStagesAndPreservesSavedState()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        var source = document.Transcript.Turns.First(turn => turn.Id == "turn-3");
        var plan = new RpTurnPlan
        {
            TurnShape = "Extended",
            Beat = "Saved beat",
            Intent = "Saved intent",
            ImmediateGoal = "Saved goal",
            WhyNow = "Saved why now",
            ChangeIntroduced = "Saved change",
            Guardrails = "Saved guardrails"
        };
        plan.Data["marker"] = "saved-plan-data";
        var scene = CloneScene(source.Scene);
        scene.Data["marker"] = "saved-scene-data";

        var result = await service.GenerateProseFromPlanAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = false, Streaming = true })],
            new(
                "turn-2",
                "regenerated",
                "Keep the saved beat.",
                "c2",
                "Gemma",
                false,
                plan,
                new Dictionary<string, string> { ["c2"] = "Saved appearance" },
                new Dictionary<string, string> { ["c2"] = "Saved private intent" },
                scene));

        Assert.Empty(client.StructuredCalls);
        Assert.Equal(1, client.StreamingTextCalls);
        Assert.Equal(["prose"], result.Trace.Steps.Select(step => step.Id));
        Assert.Equal("Saved beat", result.Plan.Beat);
        Assert.Equal("saved-plan-data", result.Plan.Data["marker"]?.GetValue<string>());
        Assert.Equal("Saved appearance", result.AppearanceByCharacterId["c2"]);
        Assert.Equal("Saved private intent", result.PrivateIntentByCharacterId["c2"]);
        Assert.Equal("saved-scene-data", result.Scene.Data["marker"]?.GetValue<string>());
    }

    [Fact]
    public async Task ProseOnlyPromptUsesSavedPlanPrivateIntentAppearanceAndTurnShape()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();
        var plan = new RpTurnPlan
        {
            TurnShape = "Silent",
            Beat = "Saved beat for prose only.",
            Intent = "Saved intent for prose only.",
            ImmediateGoal = "Saved immediate goal.",
            WhyNow = "Saved why now.",
            ChangeIntroduced = "Saved change.",
            Guardrails = "Saved guardrails."
        };

        await service.GenerateProseFromPlanAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = false, Streaming = true })],
            new(
                "turn-2",
                "regenerated",
                "",
                "c2",
                "Gemma",
                false,
                plan,
                new Dictionary<string, string> { ["c2"] = "Saved appearance for prose only." },
                new Dictionary<string, string> { ["c2"] = "Saved private intent for prose only." },
                CloneScene(document.Transcript.Turns.First(turn => turn.Id == "turn-3").Scene)));

        var prose = client.GenerationRequests.Single(request => request.OperationName == "Writing transcript prose");
        Assert.Contains("This turn has a silent shape", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Saved beat for prose only.", prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Saved intent for prose only.", prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Saved guardrails.", prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Saved private intent for prose only.", prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Saved appearance for prose only.", prose.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SnapshotRequiresStructuredOutput()
    {
        var service = new TextGenerationService(new FakeModelGenerationClient(), new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        var exception = await Assert.ThrowsAsync<TranscriptGenerationException>(() => service.GenerateSnapshotAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = false, Streaming = true })],
            new("turn-3")));

        Assert.Contains("structured output disabled", exception.Message, StringComparison.Ordinal);
    }

    static async Task<RpChatDocument> LoadDocumentAsync() =>
        await new SeedRoleplayPersistence().LoadChatDocumentAsync("ch1");

    static RpGenerationTrace CloneTrace(RpGenerationTrace trace) => new()
    {
        Summary = trace.Summary,
        Status = trace.Status,
        StartedUtc = trace.StartedUtc,
        CompletedUtc = trace.CompletedUtc,
        DurationSeconds = trace.DurationSeconds,
        Steps = trace.Steps.Select(step => new RpGenerationTraceStep
        {
            Id = step.Id,
            Label = step.Label,
            Status = step.Status,
            StartedUtc = step.StartedUtc,
            CompletedUtc = step.CompletedUtc,
            DurationSeconds = step.DurationSeconds,
            Error = step.Error
        }).ToList()
    };

    static RpSceneFrame CloneScene(RpSceneFrame scene) => new()
    {
        LocationId = scene.LocationId,
        LocationName = scene.LocationName,
        InSceneCharacterIds = [.. scene.InSceneCharacterIds],
        InSceneItemIds = [.. scene.InSceneItemIds],
        Data = scene.Data.DeepClone().AsObject()
    };

    static AiProvider BuildProvider(
        ModelGenerationCapabilities capabilities,
        string providerType = "openai",
        AiModelRole role = AiModelRole.Chat,
        string providerId = "provider",
        string modelId = "test-model") => new()
    {
        Id = providerId,
        Name = "Provider",
        Type = providerType,
        Enabled = true,
        ApiKey = "test-key",
        Models =
        [
            new()
            {
                Id = modelId,
                Enabled = true,
                Roles = [role],
                Capabilities = capabilities
            }
        ]
    };

    static int CountOccurrences(string text, string value) =>
        text.Split(value, StringSplitOptions.None).Length - 1;

    sealed class FakeModelGenerationClient : IModelGenerationClient
    {
        public List<string> StructuredCalls { get; } = [];
        public List<ModelGenerationRequest> GenerationRequests { get; } = [];
        public IReadOnlyList<string> StreamingTextDeltas { get; init; } = ["Generated prose"];
        public int StreamingTextCalls { get; private set; }
        public bool FailStreamingText { get; init; }

        public Task<ModelStructuredCompletion<T>> GenerateStructuredAsync<T>(ModelGenerationRequest request, CancellationToken cancellationToken = default)
        {
            GenerationRequests.Add(request);
            StructuredCalls.Add(typeof(T).Name);
            var value = CreateStructuredValue<T>();
            return Task.FromResult(new ModelStructuredCompletion<T>(value, $"{typeof(T).Name} raw", 1, 2, $"{typeof(T).Name}-response"));
        }

        public Task<ModelTextCompletion> GenerateTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default)
        {
            GenerationRequests.Add(request);
            return Task.FromResult(new ModelTextCompletion("Generated prose", 3, 4, "text-response"));
        }

        public async Task<ModelTextCompletion> GenerateStreamingTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default)
        {
            var text = "";
            var inputTokens = 0;
            var outputTokens = 0;
            var responseId = "";
            await foreach (var update in GenerateStreamingTextUpdatesAsync(request, cancellationToken))
            {
                text += update.TextDelta;
                if (!update.Completed)
                    continue;

                inputTokens = update.InputTokens;
                outputTokens = update.OutputTokens;
                responseId = update.ResponseId;
            }

            return new(text, inputTokens, outputTokens, responseId);
        }

        public async IAsyncEnumerable<ModelTextStreamingUpdate> GenerateStreamingTextUpdatesAsync(ModelGenerationRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            GenerationRequests.Add(request);
            StreamingTextCalls++;
            if (FailStreamingText)
                throw new InvalidOperationException("Streaming failed for test.");

            foreach (var delta in StreamingTextDeltas)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return new(TextDelta: delta);
            }

            yield return new(InputTokens: 3, OutputTokens: 4, ResponseId: "text-response", Completed: true);
        }

        public async IAsyncEnumerable<ResponseImageStreamingUpdate> GenerateStreamingImageAsync(ResponseImageGenerationRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<ModelAssistantStreamingUpdate> GenerateAssistantStreamingAsync(ModelAssistantRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task DeleteAssistantResponsesAsync(AiProvider provider, AiProviderModel model, IReadOnlyCollection<string> responseIds, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        static T CreateStructuredValue<T>()
        {
            var type = typeof(T);
            if (type == typeof(TextGenerationService.AppearanceResponse))
            {
                var response = new TextGenerationService.AppearanceResponse(
                    "Test appearance summary",
                    [new("Bella", true, "Bella test appearance")],
                    [new("c2", "Gemma", "near the couch", "standing", "turned toward Bella", "", "", "holding a glass", "", "", "", "", "", "", "holding water")],
                    [new("glass-water-1", "Glass of water", "c2", "c2", "left hand", "", "full and cold", "full glass of cold water held by Gemma in left hand")]);
                return (T)(object)response;
            }

            if (type.Name == "CyoaDecisionResponse")
            {
                var response = Activator.CreateInstance(type, nonPublic: true)
                    ?? throw new InvalidOperationException($"Could not create {type.Name}.");
                var optionType = type.DeclaringType?.GetNestedType("CyoaOptionResponse", BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Could not find CYOA option response type.");
                var option = Activator.CreateInstance(optionType, nonPublic: true)
                    ?? throw new InvalidOperationException($"Could not create {optionType.Name}.");
                SetProperty(optionType, option, "Direction", "fast-forward");
                SetProperty(optionType, option, "Title", "Wait it out");
                SetProperty(optionType, option, "Summary", "Fast-forward six hours to sunrise. Alex and Elena have spent the night reviewing leads off the notebook and are now outlining next steps at the same desks as the city wakes up.");
                SetProperty(optionType, option, "Guidance", "");
                SetProperty(optionType, option, "SceneGuidance", "");
                var options = (IList)(Activator.CreateInstance(typeof(List<>).MakeGenericType(optionType))
                    ?? throw new InvalidOperationException("Could not create CYOA option response list."));
                options.Add(option);
                type.GetProperty("Options", BindingFlags.Public | BindingFlags.Instance)?.SetValue(response, options);
                return (T)response;
            }

            var value = Activator.CreateInstance(type, nonPublic: true)
                ?? throw new InvalidOperationException($"Could not create {type.Name}.");
            SetProperty(type, value, "CharacterName", "Gemma");
            SetProperty(type, value, "Reason", "Test selection");
            SetProperty(type, value, "TurnShape", "Brief");
            SetProperty(type, value, "Beat", "Test beat");
            SetProperty(type, value, "Intent", "Test intent");
            SetProperty(type, value, "ImmediateGoal", "Test goal");
            SetProperty(type, value, "WhyNow", "Test why now");
            SetProperty(type, value, "ChangeIntroduced", "Test change");
            SetProperty(type, value, "Guardrails", "Test guardrails");
            SetProperty(type, value, "PrivateIntent", "Test private intent");
            SetProperty(type, value, "NarrativeSummary", "Test snapshot narrative");
            SetProperty(type, value, "Summary", "Test snapshot");
            SetProperty(type, value, "TimelineEntries", new List<TextGenerationService.SnapshotTimelineEntryResponse>
            {
                new()
                {
                    TurnNumber = 3,
                    Title = "Test event",
                    Description = "Test event details",
                    CharacterNames = ["Gemma"],
                    LocationNames = ["Devonshire Apartment 822"],
                    ItemNames = ["Tesla Model S Plaid"]
                }
            });
            return (T)value;
        }

        static void SetProperty(Type type, object target, string name, object value)
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property is not null && property.PropertyType.IsInstanceOfType(value))
                property.SetValue(target, value);
        }
    }

    sealed class NoOpCapabilityCatalog : IModelCapabilityCatalog
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
}
