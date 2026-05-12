using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Serialization;
using AgentRp.Services;
using AgentRp.Session;
using AgentRp.UserSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentRp.Tests;

public sealed class ImageGenerationServiceTests
{
    static readonly byte[] PngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
    static readonly CurrentAppUser User = new(Guid.Empty, "user@test.local", "USER@TEST.LOCAL", "Test User", new HashSet<string>(StringComparer.Ordinal));

    [Fact]
    public async Task ImageOnlyModelUsesSameProviderResponsesHost()
    {
        var dbFactory = new TestDbContextFactory();
        var client = new FakeModelGenerationClient();
        var service = BuildService(dbFactory, client);
        var document = new RpChatDocument
        {
            Chat = new() { Id = "chat-1", Title = "Test chat" }
        };
        var provider = new AiProvider
        {
            Id = "openai",
            Name = "OpenAI",
            Type = "openai",
            Enabled = true,
            Models =
            [
                new()
                {
                    Id = "gpt-image-1-mini",
                    Enabled = true,
                    Roles = [AiModelRole.Image],
                    Capabilities = new() { TextInput = true, TextOutput = false, ImageOutput = true, Tools = true }
                },
                new()
                {
                    Id = "gpt-5.5",
                    Enabled = true,
                    Roles = [AiModelRole.Chat],
                    Capabilities = new() { TextInput = true, TextOutput = true, ImageInput = true, Tools = true }
                }
            ]
        };

        var result = await service.GenerateAsync(
            document,
            [provider],
            new(
                ImageGenerationService.BuildModelKey("openai", "gpt-image-1-mini"),
                "A cinematic portrait.",
                "none",
                "Square",
                "Low",
                "Low",
                [],
                [],
                null,
                null));

        Assert.NotNull(client.ImageRequest);
        Assert.Equal("gpt-5.5", client.ImageRequest.HostModel.Id);
        Assert.Equal("gpt-image-1-mini", client.ImageRequest.ImageModel.Id);
        Assert.Equal("gpt-image-1-mini", result.ModelId);

        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var image = await dbContext.ImageAssets.SingleAsync();
        Assert.Equal("gpt-image-1-mini", image.ProviderModelId);
    }

    [Fact]
    public async Task NoneArtStyleDoesNotInjectStyleInstruction()
    {
        var dbFactory = new TestDbContextFactory();
        var client = new FakeModelGenerationClient();
        var service = BuildService(dbFactory, client);
        var document = new RpChatDocument
        {
            Chat = new() { Id = "chat-1", Title = "Test chat" }
        };
        var provider = BuildImageProvider();

        await service.GenerateAsync(
            document,
            [provider],
            new(
                ImageGenerationService.BuildModelKey("openai", "gpt-image-1"),
                "A candlelit tavern.",
                "none",
                "Landscape",
                "Auto",
                "Low",
                [],
                [],
                null,
                null));

        Assert.NotNull(client.ImageRequest);
        Assert.Contains("A candlelit tavern.", client.ImageRequest.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Art style:", client.ImageRequest.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectedArtStyleInjectsStyleInstruction()
    {
        var dbFactory = new TestDbContextFactory();
        var client = new FakeModelGenerationClient();
        var service = BuildService(dbFactory, client);
        var document = new RpChatDocument
        {
            Chat = new() { Id = "chat-1", Title = "Test chat" }
        };
        var provider = BuildImageProvider();

        await service.GenerateAsync(
            document,
            [provider],
            new(
                ImageGenerationService.BuildModelKey("openai", "gpt-image-1"),
                "A candlelit tavern.",
                "graphic-novel",
                "Landscape",
                "Auto",
                "Low",
                [],
                [],
                null,
                null));

        Assert.NotNull(client.ImageRequest);
        Assert.Contains("A candlelit tavern.", client.ImageRequest.Prompt);
        Assert.Contains("Art style: graphic novel art", client.ImageRequest.Prompt);
    }

    [Fact]
    public async Task ImageRequestWiresSettingsAndExplicitReferenceMetadata()
    {
        var dbFactory = new TestDbContextFactory();
        var blobStorage = new TestAssetBlobStorage();
        await SeedReferenceImageAsync(dbFactory, blobStorage, "chat-1", "ref-1");
        var client = new FakeModelGenerationClient();
        var service = BuildService(dbFactory, client, blobStorage);
        var document = new RpChatDocument
        {
            Chat = new() { Id = "chat-1", Title = "Test chat" },
            Characters =
            [
                new() { Id = "c1", Name = "Gemma", Summary = "A player character." }
            ],
            Locations =
            [
                new() { Id = "l1", Name = "Ambient Location", IsActive = true }
            ],
            Images =
            [
                new() { Id = "ref-1", Name = "Reference Pose", EntityType = "character", Url = "/story-images/ref-1" }
            ]
        };

        await service.GenerateAsync(
            document,
            [BuildImageProvider()],
            new(
                ImageGenerationService.BuildModelKey("openai", "gpt-image-1"),
                "A portrait.",
                "anime",
                "Portrait",
                "Low",
                "High",
                [new("character", "c1"), new("location", "l1"), new("item", "i1"), new("item", "i1")],
                ["ref-1"],
                "Silver Ring",
                "items",
                "i1"));

        Assert.NotNull(client.ImageRequest);
        Assert.Equal("1024x1536", client.ImageRequest.Size);
        Assert.Equal("low", client.ImageRequest.Quality);
        Assert.Equal("high", client.ImageRequest.ReferenceDetail);
        Assert.Single(client.ImageRequest.ReferenceImages);

        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var generated = await dbContext.ImageAssets.SingleAsync(image => image.Id != "ref-1");
        var metadata = JsonSerializer.Deserialize<ImageAssetGenerationMetadata>(generated.GenerationMetadataJson, AppJsonSerializerOptions.Web);

        Assert.NotNull(metadata);
        Assert.Equal("1024x1536", metadata.Size);
        Assert.Equal("low", metadata.Quality);
        Assert.Equal("high", metadata.ReferenceDetail);
        Assert.Contains(metadata.References, reference => reference.Kind == "entity" && reference.Id == "i1" && reference.Name == "Silver Ring");
        Assert.Contains(metadata.References, reference => reference.Kind == "entity" && reference.Id == "c1" && reference.Name == "Gemma");
        Assert.Contains(metadata.References, reference => reference.Kind == "entity" && reference.Id == "l1" && reference.Name == "Ambient Location");
        Assert.Contains(metadata.References, reference => reference.Kind == "image" && reference.Id == "ref-1" && reference.Name == "Reference Pose");
        Assert.Equal(metadata.References.Select(reference => $"{reference.Kind}:{reference.EntityType}:{reference.Id}:{reference.Name}").Distinct().Count(), metadata.References.Count);
        Assert.False(string.IsNullOrWhiteSpace(metadata.Rationale));
    }

    [Fact]
    public async Task GenerateStreamingAsyncYieldsPartialPreviewBeforeCompletedResult()
    {
        var dbFactory = new TestDbContextFactory();
        var client = new FakeModelGenerationClient { EmitPartialPreview = true };
        var service = BuildService(dbFactory, client);
        var document = new RpChatDocument
        {
            Chat = new() { Id = "chat-1", Title = "Test chat" }
        };
        var updates = new List<ImageGenerationStreamingUpdate>();

        await foreach (var update in service.GenerateStreamingAsync(
            document,
            [BuildImageProvider()],
            new(
                ImageGenerationService.BuildModelKey("openai", "gpt-image-1"),
                "A candlelit tavern.",
                "none",
                "Landscape",
                "Auto",
                "Auto",
                [],
                [],
                null,
                null)))
        {
            updates.Add(update);
            if (!string.IsNullOrWhiteSpace(update.PreviewImageDataUrl))
            {
                await using var dbContext = await dbFactory.CreateDbContextAsync();
                Assert.Empty(dbContext.ImageAssets);
            }
        }

        Assert.StartsWith("data:image/png;base64,", updates.First().PreviewImageDataUrl);
        Assert.NotNull(updates.Last().Result);
        Assert.True(updates.Last().Completed);

        await using var finalDbContext = await dbFactory.CreateDbContextAsync();
        await finalDbContext.ImageAssets.SingleAsync();
    }

    [Fact]
    public async Task ImageDetailsServiceShowsExplicitReferencesWithoutAmbientLocation()
    {
        var dbFactory = new TestDbContextFactory();
        var blobStorage = new TestAssetBlobStorage();
        await SeedReferenceImageAsync(dbFactory, blobStorage, "chat-1", "ref-1");
        var client = new FakeModelGenerationClient();
        var generationService = BuildService(dbFactory, client, blobStorage);
        var detailsService = new ImageDetailsService(dbFactory);
        var document = new RpChatDocument
        {
            Chat = new() { Id = "chat-1", Title = "Test chat" },
            Characters =
            [
                new() { Id = "c1", Name = "Gemma", Summary = "A player character." }
            ],
            Locations =
            [
                new() { Id = "l1", Name = "Ambient Location", IsActive = true }
            ],
            Images =
            [
                new() { Id = "ref-1", Name = "Reference Pose", EntityType = "character", Url = "/story-images/ref-1" }
            ]
        };

        var generated = await generationService.GenerateAsync(
            document,
            [BuildImageProvider()],
            new(
                ImageGenerationService.BuildModelKey("openai", "gpt-image-1"),
                "A portrait.",
                "none",
                "Square",
                "Medium",
                "Low",
                [new("character", "c1")],
                ["ref-1"],
                "Gemma",
                "characters",
                "c1"));

        var details = await detailsService.GetAsync(User, generated.Image.Id);

        Assert.True(details.HasGenerationMetadata);
        Assert.Contains(details.References, reference => reference.Kind == "entity" && reference.Name == "Gemma");
        Assert.Contains(details.References, reference => reference.Kind == "image" && reference.Name == "Reference Pose");
        Assert.DoesNotContain(details.References, reference => reference.Name == "Ambient Location");
        Assert.False(string.IsNullOrWhiteSpace(details.Rationale));
    }

    [Fact]
    public async Task StructuredTextModelComposesFinalPromptAndRationale()
    {
        var dbFactory = new TestDbContextFactory();
        var client = new FakeModelGenerationClient
        {
            StructuredPrompt = new("Composed portrait prompt.", "Used Gemma's appearance.")
        };
        var service = BuildService(dbFactory, client);
        var document = new RpChatDocument
        {
            Chat = new() { Id = "chat-1", Title = "Test chat" },
            Characters =
            [
                new() { Id = "c1", Name = "Gemma", Appearance = "Tall blonde." }
            ]
        };
        var provider = BuildImageProvider();
        provider.Models.Add(new()
        {
            Id = "gpt-5.5",
            Enabled = true,
            Roles = [AiModelRole.Chat],
            Capabilities = new() { TextInput = true, TextOutput = true, StructuredOutput = true }
        });

        var result = await service.GenerateAsync(
            document,
            [provider],
            new(
                ImageGenerationService.BuildModelKey("openai", "gpt-image-1"),
                "A portrait.",
                "none",
                "Portrait",
                "Auto",
                "Low",
                [new("character", "c1")],
                [],
                "Gemma",
                "characters",
                "c1"));

        Assert.NotNull(client.StructuredRequest);
        Assert.NotNull(client.ImageRequest);
        Assert.Equal("Composed portrait prompt.", client.ImageRequest.Prompt);
        Assert.Equal("Composed portrait prompt.", result.FinalPrompt);
        Assert.Equal("Used Gemma's appearance.", result.Rationale);
    }

    static AiProvider BuildImageProvider() => new()
    {
        Id = "openai",
        Name = "OpenAI",
        Type = "openai",
        Enabled = true,
        Models =
        [
            new()
            {
                Id = "gpt-image-1",
                Enabled = true,
                Roles = [AiModelRole.Image],
                Capabilities = new() { TextInput = true, TextOutput = true, ImageInput = true, ImageOutput = true, Tools = true }
            }
        ]
    };

    sealed class FakeModelGenerationClient : IModelGenerationClient
    {
        public ResponseImageGenerationRequest? ImageRequest { get; private set; }
        public ModelGenerationRequest? StructuredRequest { get; private set; }
        public ImagePromptResult? StructuredPrompt { get; init; }
        public bool EmitPartialPreview { get; init; }

        public Task<ModelStructuredCompletion<T>> GenerateStructuredAsync<T>(ModelGenerationRequest request, CancellationToken cancellationToken = default)
        {
            StructuredRequest = request;
            if (StructuredPrompt is null)
                throw new NotSupportedException();

            var value = Activator.CreateInstance<T>()
                ?? throw new InvalidOperationException("Could not create structured response.");
            typeof(T).GetProperty("FinalPrompt")?.SetValue(value, StructuredPrompt.FinalPrompt);
            typeof(T).GetProperty("Rationale")?.SetValue(value, StructuredPrompt.Rationale);
            return Task.FromResult(new ModelStructuredCompletion<T>(value, "", 1, 1, "structured-response-id"));
        }

        public Task<ModelTextCompletion> GenerateTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ModelTextCompletion> GenerateStreamingTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ModelTextStreamingUpdate> GenerateStreamingTextUpdatesAsync(ModelGenerationRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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

        public async IAsyncEnumerable<ResponseImageStreamingUpdate> GenerateStreamingImageAsync(ResponseImageGenerationRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ImageRequest = request;
            await Task.Yield();
            if (EmitPartialPreview)
                yield return new(PngBytes, "image/png", null, 0, 0, "", false);

            yield return new(PngBytes, "image/png", "Revised image prompt.", 1, 1, "response-id", true);
        }
    }

    public sealed record ImagePromptResult(string FinalPrompt, string Rationale);

    static ImageGenerationService BuildService(
        TestDbContextFactory dbFactory,
        FakeModelGenerationClient client,
        TestAssetBlobStorage? blobStorage = null,
        IImageOptimizer? imageOptimizer = null)
    {
        var storedImageService = new StoredImageService(
            dbFactory,
            imageOptimizer ?? new TestImageOptimizer(),
            blobStorage ?? new TestAssetBlobStorage(),
            NullLogger<StoredImageService>.Instance);
        return new(client, new NoOpCapabilityCatalog(), storedImageService);
    }

    static async Task SeedReferenceImageAsync(TestDbContextFactory dbFactory, TestAssetBlobStorage blobStorage, string chatId, string imageId)
    {
        var storedImageService = new StoredImageService(
            dbFactory,
            new TestImageOptimizer(),
            blobStorage,
            NullLogger<StoredImageService>.Instance);
        await storedImageService.StoreAsync(new(
            chatId,
            User.Id,
            imageId,
            PngBytes,
            "image/png",
            "reference.png",
            "Reference Pose",
            "Reference Pose",
            "character",
            210,
            1,
            1,
            "",
            "",
            new(),
            "",
            "",
            "",
            DateTime.UtcNow));
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

    sealed class TestDbContextFactory : IDbContextFactory<RpDbContext>
    {
        readonly DbContextOptions<RpDbContext> options = new DbContextOptionsBuilder<RpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        public RpDbContext CreateDbContext() => new(options);

        public Task<RpDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
