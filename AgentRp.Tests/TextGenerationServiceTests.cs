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
        public int StreamingTextCalls { get; private set; }

        public Task<ModelStructuredCompletion<T>> GenerateStructuredAsync<T>(ModelGenerationRequest request, CancellationToken cancellationToken = default)
        {
            StructuredCalls.Add(typeof(T).Name);
            var value = CreateStructuredValue<T>();
            return Task.FromResult(new ModelStructuredCompletion<T>(value, $"{typeof(T).Name} raw", 1, 2, $"{typeof(T).Name}-response"));
        }

        public Task<ModelTextCompletion> GenerateTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModelTextCompletion("Generated prose", 3, 4, "text-response"));

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
            SetProperty(type, value, "Beat", "Test beat");
            SetProperty(type, value, "Intent", "Test intent");
            SetProperty(type, value, "ImmediateGoal", "Test goal");
            SetProperty(type, value, "WhyNow", "Test why now");
            SetProperty(type, value, "ChangeIntroduced", "Test change");
            SetProperty(type, value, "Guardrails", "Test guardrails");
            SetProperty(type, value, "PrivateIntent", "Test private intent");
            SetProperty(type, value, "Summary", "Test snapshot");
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
