using System.Reflection;
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
        Assert.Equal(["appearance", "selection", "planning", "prose"], result.Trace.Steps.Select(step => step.Id));
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

        var appearance = client.GenerationRequests.First(request => request.OperationName == "Generating appearance state");
        var planning = client.GenerationRequests.First(request => request.OperationName == "Planning transcript turn");
        var prose = client.GenerationRequests.First(request => request.OperationName == "Writing transcript prose");

        Assert.Contains("You update character scene state.", appearance.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Private Intent usage:", planning.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Turn shape definitions:", planning.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Test private intent", prose.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("This turn has a brief shape", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("**Actor:** Gemma", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("- Gemma only", planning.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("**Actor:** Bella", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("You are Gemma", prose.SystemPrompt, StringComparison.Ordinal);
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
        Assert.Contains("Tense Foreshadowing", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Frame the room like something is about to break.", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Write natural prose narration instead of dialogue", prose.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("**Actor:** Gemma", planning.UserPrompt, StringComparison.Ordinal);
        Assert.Equal(["appearance", "selection", "planning", "prose"], result.Trace.Steps.Select(step => step.Id));
        Assert.Equal("User override", result.Trace.Steps.First(step => step.Id == "selection").SystemPrompt);
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
    public async Task SnapshotGenerationReturnsAgentRp1SnapshotShape()
    {
        var client = new FakeModelGenerationClient();
        var service = new TextGenerationService(client, new NoOpCapabilityCatalog(), new TranscriptPromptContextBuilder());
        var document = await LoadDocumentAsync();

        var result = await service.GenerateSnapshotAsync(
            document,
            [BuildProvider(new() { TextInput = true, TextOutput = true, StructuredOutput = true, Streaming = true })],
            new("turn-3"));

        var snapshotRequest = client.GenerationRequests.First(request => request.OperationName == "Generating snapshot");

        Assert.Contains("Return a concise narrative summary, then propose canonical facts and timeline entries", snapshotRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Thread title: Devonshire Games", snapshotRequest.UserPrompt, StringComparison.Ordinal);
        Assert.Equal("Test snapshot narrative", result.Summary);
        var fact = Assert.Single(result.Facts);
        Assert.Equal("Test fact", fact.Title);
        var timelineEntry = Assert.Single(result.TimelineEntries);
        Assert.Equal("Test event", timelineEntry.Title);
        Assert.NotEmpty(result.CharacterAppearances);
        Assert.Equal("completed", result.Trace.Status);
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

    static AiProvider BuildProvider(ModelGenerationCapabilities capabilities) => new()
    {
        Id = "provider",
        Name = "Provider",
        Type = "openai",
        Enabled = true,
        ApiKey = "test-key",
        Models =
        [
            new()
            {
                Id = "test-model",
                Enabled = true,
                Text = true,
                ActiveText = true,
                Capabilities = capabilities
            }
        ]
    };

    sealed class FakeModelGenerationClient : IModelGenerationClient
    {
        public List<string> StructuredCalls { get; } = [];
        public List<ModelGenerationRequest> GenerationRequests { get; } = [];
        public int StreamingTextCalls { get; private set; }

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

        public Task<ModelTextCompletion> GenerateStreamingTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default)
        {
            StreamingTextCalls++;
            return GenerateTextAsync(request, cancellationToken);
        }

        public async IAsyncEnumerable<ResponseImageStreamingUpdate> GenerateStreamingImageAsync(ResponseImageGenerationRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<string> CreateAssistantConversationAsync(AiProvider provider, AiProviderModel model, CancellationToken cancellationToken = default) =>
            Task.FromResult("conv-test");

        public async IAsyncEnumerable<ModelAssistantStreamingUpdate> GenerateAssistantStreamingAsync(ModelAssistantRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        static T CreateStructuredValue<T>()
        {
            var type = typeof(T);
            if (type == typeof(TextGenerationService.AppearanceResponse))
            {
                var response = new TextGenerationService.AppearanceResponse(
                    "Test appearance summary",
                    [new("Bella", true, "Bella test appearance")]);
                return (T)(object)response;
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
            SetProperty(type, value, "Facts", new List<TextGenerationService.SnapshotFactResponse>
            {
                new()
                {
                    Title = "Test fact",
                    Summary = "Test fact summary",
                    Details = "Test fact details",
                    CharacterNames = ["Gemma"],
                    LocationNames = ["Devonshire Apartment 822"],
                    ItemNames = ["Tesla Model S Plaid"]
                }
            });
            SetProperty(type, value, "TimelineEntries", new List<TextGenerationService.SnapshotTimelineEntryResponse>
            {
                new()
                {
                    WhenText = "Today",
                    Title = "Test event",
                    Summary = "Test event summary",
                    Details = "Test event details",
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
