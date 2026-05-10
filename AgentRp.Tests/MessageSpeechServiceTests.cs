using System.Text.Json.Nodes;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentRp.Tests;

public sealed class MessageSpeechServiceTests
{
    [Fact]
    public void ResolveAvailabilityHidesButtonWhenNoVoiceModelIsSelected()
    {
        var service = CreateService();
        var document = CreateDocument();
        var turn = CharacterTurn(document);

        var availability = service.ResolveAvailability(document, [], ActiveModelSelectionsState.CreateDefault(), turn);

        Assert.Equal(MessageSpeechAvailabilityKind.NoVoiceModel, availability.Kind);
        Assert.False(availability.CanDisplay);
    }

    [Fact]
    public void ResolveAvailabilityUsesCharacterVoiceWhenPresent()
    {
        var service = CreateService();
        var document = CreateDocument();
        var provider = VoiceProvider();
        var modelSelections = SelectVoiceModel(document, provider);
        Character(document).VoiceSelections[ModelSelectionKey.Build(provider.Id, "voice-model")] = Voice("bella-voice");

        var availability = service.ResolveAvailability(document, [provider], modelSelections, CharacterTurn(document));

        Assert.Equal(MessageSpeechAvailabilityKind.Ready, availability.Kind);
    }

    [Fact]
    public void ResolveAvailabilityFallsBackToNarratorVoiceForCharacterMessages()
    {
        var service = CreateService();
        var document = CreateDocument();
        var provider = VoiceProvider();
        var modelSelections = SelectVoiceModel(document, provider);
        document.NarratorProfile.VoiceSelections[ModelSelectionKey.Build(provider.Id, "voice-model")] = Voice("narrator-voice");

        var availability = service.ResolveAvailability(document, [provider], modelSelections, CharacterTurn(document));

        Assert.Equal(MessageSpeechAvailabilityKind.Ready, availability.Kind);
    }

    [Fact]
    public void ResolveAvailabilityReportsCharacterWhenNoCharacterOrNarratorVoiceExists()
    {
        var service = CreateService();
        var document = CreateDocument();
        var provider = VoiceProvider();
        var modelSelections = SelectVoiceModel(document, provider);
        var turn = CharacterTurn(document);

        var availability = service.ResolveAvailability(document, [provider], modelSelections, turn);

        Assert.Equal(MessageSpeechAvailabilityKind.MissingVoice, availability.Kind);
        Assert.Equal(turn.AuthorCharacterId, availability.MissingEntityId);
        Assert.False(availability.MissingNarrator);
    }

    [Fact]
    public void ResolveAvailabilityUsesCharacterVoiceWhenElevenLabsNarratorActionsHasNoNarratorVoice()
    {
        var service = CreateService();
        var document = CreateDocument();
        var provider = VoiceProvider("elevenlabs");
        var modelSelections = SelectVoiceModel(document, provider);
        document.Transcript.Options.SpeakActionsInNarratorVoice = true;
        Character(document).VoiceSelections[ModelSelectionKey.Build(provider.Id, "voice-model")] = Voice("bella-voice");

        var availability = service.ResolveAvailability(document, [provider], modelSelections, CharacterTurn(document));

        Assert.Equal(MessageSpeechAvailabilityKind.Ready, availability.Kind);
    }

    [Fact]
    public void ResolveAvailabilityFallsBackToNarratorWhenElevenLabsNarratorActionsHasNoCharacterVoice()
    {
        var service = CreateService();
        var document = CreateDocument();
        var provider = VoiceProvider("elevenlabs");
        var modelSelections = SelectVoiceModel(document, provider);
        document.Transcript.Options.SpeakActionsInNarratorVoice = true;
        document.NarratorProfile.VoiceSelections[ModelSelectionKey.Build(provider.Id, "voice-model")] = Voice("narrator-voice");

        var availability = service.ResolveAvailability(document, [provider], modelSelections, CharacterTurn(document));

        Assert.Equal(MessageSpeechAvailabilityKind.Ready, availability.Kind);
    }

    [Fact]
    public void BuildDialogueInputsUsesAsteriskActionsForNarratorVoice()
    {
        var inputs = MessageSpeechService.BuildDialogueInputs(
            "We have spoken text *then some actions* [whispers] then more spoken text *claps loudly*",
            "character-voice",
            "narrator-voice");

        Assert.Collection(
            inputs,
            input =>
            {
                Assert.Equal("character-voice", input.VoiceId);
                Assert.Equal("We have spoken text", input.Text);
            },
            input =>
            {
                Assert.Equal("narrator-voice", input.VoiceId);
                Assert.Equal("then some actions", input.Text);
            },
            input =>
            {
                Assert.Equal("character-voice", input.VoiceId);
                Assert.Equal("[whispers] then more spoken text", input.Text);
            },
            input =>
            {
                Assert.Equal("narrator-voice", input.VoiceId);
                Assert.Equal("claps loudly", input.Text);
            });
    }

    [Fact]
    public void FormatElevenLabsV3SingleVoiceTextRemovesAsteriskActionsAndQuotesDialogue()
    {
        var text = MessageSpeechService.FormatElevenLabsV3SingleVoiceText(
            "*trembles, hands shaking water from her arms* [stammers] G-Gemma... shadow... in the pool... [voice cracks] chased me, too fast! *eyes pleading*");

        Assert.Equal(
            "trembles, hands shaking water from her arms. [stammers] \"G-Gemma... shadow... in the pool... [voice cracks] chased me, too fast!\" eyes pleading.",
            text);
    }

    [Fact]
    public void FormatElevenLabsV3SingleVoiceTextPreservesExistingDialoguePunctuation()
    {
        var text = MessageSpeechService.FormatElevenLabsV3SingleVoiceText(
            "*glances back* \"Stay close\" *nods once,*");

        Assert.Equal("glances back. \"Stay close.\" nods once.", text);
    }

    [Fact]
    public void BuildDialogueInputsSkipsElevenLabsEmptySegments()
    {
        var inputs = MessageSpeechService.BuildDialogueInputs(
            "[sighs] *[door creaks]* 🙂 *[pause]* Actual spoken text",
            "character-voice",
            "narrator-voice");

        var input = Assert.Single(inputs);
        Assert.Equal("character-voice", input.VoiceId);
        Assert.Equal("[sighs] Actual spoken text", input.Text);
    }

    [Fact]
    public void BuildDialogueInputsReturnsNoInputsWhenOnlyCuesRemain()
    {
        var inputs = MessageSpeechService.BuildDialogueInputs(
            "[sighs] *[door creaks]* 🙂",
            "character-voice",
            "narrator-voice");

        Assert.Empty(inputs);
    }

    [Fact]
    public void BuildDialogueInputsSilentlyTruncatesAtSpeechLimit()
    {
        var text = new string('a', MessageSpeechService.MaxSpeechCharacters + 100);

        var input = Assert.Single(MessageSpeechService.BuildDialogueInputs(text, "character-voice", "narrator-voice"));

        Assert.Equal(MessageSpeechService.MaxSpeechCharacters, input.Text.Length);
    }

    [Fact]
    public void StripAudioTagsRemovesSquareAndXmlTagsForUnsupportedVoiceModels()
    {
        var text = AudioTagTransportRules.StripAudioTags("""[sighs] <whisper>"Hello."</whisper>""");

        Assert.Equal(" \"Hello.\"", text);
    }

    [Fact]
    public void SupportsAudioTagsForXAiAndElevenV3Only()
    {
        Assert.True(AudioTagTransportRules.SupportsAudioTags(VoiceProvider("grok"), new() { Id = "xai-tts" }));
        Assert.True(AudioTagTransportRules.SupportsAudioTags(VoiceProvider("elevenlabs"), new() { Id = "eleven_v3" }));
        Assert.False(AudioTagTransportRules.SupportsAudioTags(VoiceProvider("elevenlabs"), new() { Id = "eleven_multilingual_v2" }));
        Assert.False(AudioTagTransportRules.SupportsAudioTags(VoiceProvider("openai"), new() { Id = "gpt-4o-mini-tts" }));
    }

    [Fact]
    public async Task GetOrGenerateAsyncCreatesVoiceMessageAndReusesMatchingSource()
    {
        var dbFactory = new TestDbContextFactory();
        var coordinator = new RecordingVoiceMessageStreamCoordinator();
        var service = new MessageSpeechService(dbFactory, coordinator, BuildStoredSpeechService(dbFactory), new NoOpCapabilityCatalog());
        var document = CreateDocument();
        var provider = VoiceProvider();
        var modelSelections = SelectVoiceModel(document, provider);
        Character(document).VoiceSelections[ModelSelectionKey.Build(provider.Id, "voice-model")] = Voice("bella-voice");
        var turn = CharacterTurn(document);

        var generated = await service.GetOrGenerateAsync(document, [provider], modelSelections, turn, false);
        var replay = await service.GetOrGenerateAsync(document, [provider], modelSelections, turn, false);

        Assert.True(generated.Generated);
        Assert.False(replay.Generated);
        Assert.Equal(generated.Url, replay.Url);
        Assert.StartsWith("/story-audio/speech-", generated.Url, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(turn.Speech.VoiceMessageId));
        Assert.Single(coordinator.Starts);
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var row = await dbContext.SpeechAssets.AsNoTracking().SingleAsync();
        Assert.Equal(SpeechAssetStatus.Pending, row.Status);
        Assert.Equal(turn.Speech.VoiceMessageId, row.Id);
        Assert.Contains("bella-voice", row.InputsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadInputSnapshotReadsPersistedSpeechInputs()
    {
        var dbFactory = new TestDbContextFactory();
        var service = new MessageSpeechService(dbFactory, new RecordingVoiceMessageStreamCoordinator(), BuildStoredSpeechService(dbFactory), new NoOpCapabilityCatalog());
        var document = CreateDocument();
        var turn = CharacterTurn(document);
        turn.Speech.VoiceMessageId = "speech-1";
        await using (var dbContext = await dbFactory.CreateDbContextAsync())
        {
            dbContext.SpeechAssets.Add(new()
            {
                Id = "speech-1",
                ChatId = document.Chat.Id,
                TurnId = turn.Id,
                Status = SpeechAssetStatus.Ready,
                ProviderName = "ElevenLabs",
                ProviderType = "elevenlabs",
                ProviderModelId = "eleven_v3",
                InputsJson = """[{"text":"Exact model text","voiceId":"voice-1"}]""",
                CreatedUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var snapshot = await service.LoadInputSnapshotAsync(turn);

        Assert.NotNull(snapshot);
        Assert.Equal("Ready", snapshot.Status);
        Assert.Equal("ElevenLabs", snapshot.ProviderName);
        var input = Assert.Single(snapshot.Inputs);
        Assert.Equal("Exact model text", input.Text);
        Assert.Equal("voice-1", input.VoiceId);
    }

    [Fact]
    public async Task DiscardTurnSpeechDeletesSpeechRowAndBlob()
    {
        var dbFactory = new TestDbContextFactory();
        var blobStorage = new TestAssetBlobStorage();
        var service = new MessageSpeechService(
            dbFactory,
            new RecordingVoiceMessageStreamCoordinator(),
            BuildStoredSpeechService(dbFactory, blobStorage),
            new NoOpCapabilityCatalog());
        var document = CreateDocument();
        var turn = CharacterTurn(document);
        turn.Speech.VoiceMessageId = "speech-1";
        await blobStorage.UploadAsync("audio/chat-1/speech-1", [1, 2], "audio/mpeg");
        await using (var dbContext = await dbFactory.CreateDbContextAsync())
        {
            dbContext.SpeechAssets.Add(new()
            {
                Id = "speech-1",
                ChatId = document.Chat.Id,
                TurnId = turn.Id,
                Status = SpeechAssetStatus.Ready,
                BlobName = "audio/chat-1/speech-1",
                StoredByteLength = 2,
                ContentType = "audio/mpeg",
                FileName = "turn-1.mp3",
                CreatedUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        await service.DiscardTurnSpeechAsync(turn);

        await using var verifyContext = await dbFactory.CreateDbContextAsync();
        Assert.False(await verifyContext.SpeechAssets.AnyAsync(asset => asset.Id == "speech-1"));
        Assert.Contains("audio/chat-1/speech-1", blobStorage.DeletedBlobNames);
        Assert.False(blobStorage.Blobs.ContainsKey("audio/chat-1/speech-1"));
        Assert.Equal("", turn.Speech.VoiceMessageId);
    }

    [Fact]
    public async Task GetOrGenerateAsyncNormalizesElevenV3SingleVoiceInput()
    {
        var dbFactory = new TestDbContextFactory();
        var coordinator = new RecordingVoiceMessageStreamCoordinator();
        var service = new MessageSpeechService(dbFactory, coordinator, BuildStoredSpeechService(dbFactory), new NoOpCapabilityCatalog());
        var document = CreateDocument();
        var provider = VoiceProvider("elevenlabs", "eleven_v3");
        var modelSelections = SelectVoiceModel(document, provider);
        Character(document).VoiceSelections[ModelSelectionKey.Build(provider.Id, "eleven_v3")] = Voice("bella-voice");
        var turn = CharacterTurn(document);
        turn.Body = "*trembles, hands shaking water from her arms* [stammers] G-Gemma... shadow... in the pool... [voice cracks] chased me, too fast! *eyes pleading*";

        await service.GetOrGenerateAsync(document, [provider], modelSelections, turn, false);

        var start = Assert.Single(coordinator.Starts);
        var input = Assert.Single(start.Inputs);
        Assert.Equal("bella-voice", input.VoiceId);
        Assert.Equal(
            "trembles, hands shaking water from her arms. [stammers] \"G-Gemma... shadow... in the pool... [voice cracks] chased me, too fast!\" eyes pleading.",
            input.Text);
        Assert.DoesNotContain("*", input.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOrGenerateAsyncLeavesNonElevenV3SingleVoiceActionsUnchanged()
    {
        var dbFactory = new TestDbContextFactory();
        var coordinator = new RecordingVoiceMessageStreamCoordinator();
        var service = new MessageSpeechService(dbFactory, coordinator, BuildStoredSpeechService(dbFactory), new NoOpCapabilityCatalog());
        var document = CreateDocument();
        var provider = VoiceProvider("elevenlabs", "eleven_multilingual_v2");
        var modelSelections = SelectVoiceModel(document, provider);
        Character(document).VoiceSelections[ModelSelectionKey.Build(provider.Id, "eleven_multilingual_v2")] = Voice("bella-voice");
        var turn = CharacterTurn(document);
        turn.Body = "*glances back* Stay close *nods once*";

        await service.GetOrGenerateAsync(document, [provider], modelSelections, turn, false);

        var start = Assert.Single(coordinator.Starts);
        var input = Assert.Single(start.Inputs);
        Assert.Equal("*glances back* Stay close *nods once*", input.Text);
    }

    [Fact]
    public async Task GetOrGenerateAsyncLeavesElevenV3NarratorInputUnchanged()
    {
        var dbFactory = new TestDbContextFactory();
        var coordinator = new RecordingVoiceMessageStreamCoordinator();
        var service = new MessageSpeechService(dbFactory, coordinator, BuildStoredSpeechService(dbFactory), new NoOpCapabilityCatalog());
        var document = CreateDocument();
        var provider = VoiceProvider("elevenlabs", "eleven_v3");
        var modelSelections = SelectVoiceModel(document, provider);
        document.NarratorProfile.VoiceSelections[ModelSelectionKey.Build(provider.Id, "eleven_v3")] = Voice("narrator-voice");
        var turn = CharacterTurn(document);
        turn.AuthorCharacterId = "";
        turn.AuthorName = "Narrator";
        turn.Body = "The room goes quiet.";

        await service.GetOrGenerateAsync(document, [provider], modelSelections, turn, false);

        var start = Assert.Single(coordinator.Starts);
        var input = Assert.Single(start.Inputs);
        Assert.Equal("The room goes quiet.", input.Text);
    }

    static MessageSpeechService CreateService(IVoiceMessageStreamCoordinator? streamCoordinator = null) =>
        new(null!, streamCoordinator ?? new NoOpVoiceMessageStreamCoordinator(), new NoOpStoredSpeechAssetService(), new NoOpCapabilityCatalog());

    static StoredSpeechAssetService BuildStoredSpeechService(IDbContextFactory<RpDbContext> dbFactory, TestAssetBlobStorage? blobStorage = null) =>
        new(dbFactory, blobStorage ?? new TestAssetBlobStorage(), NullLogger<StoredSpeechAssetService>.Instance);

    static RpChatDocument CreateDocument()
    {
        var document = new RpChatDocument
        {
            Chat = new() { Id = "chat-1", Title = "Test" },
            Characters =
            [
                new()
                {
                    Id = "c1",
                    Name = "Bella",
                    InScene = true
                }
            ],
            Transcript = new()
            {
                Turns =
                [
                    new()
                    {
                        Id = "turn-1",
                        AuthorCharacterId = "c1",
                        AuthorName = "Bella",
                        Body = "\"Hello.\""
                    }
                ]
            }
        };
        return document;
    }

    static RpCharacter Character(RpChatDocument document) => document.Characters.Single(character => character.Id == "c1");

    static RpTranscriptTurn CharacterTurn(RpChatDocument document) => document.Transcript.Turns.Single();

    static AiProvider VoiceProvider(string type = "openai", string modelId = "voice-model") => new()
    {
        Id = "voice-provider",
        Name = "Voice Provider",
        Type = type,
        Enabled = true,
        Models =
        [
            new()
            {
                Id = modelId,
                Enabled = true,
                Roles = [AiModelRole.Voice],
                Capabilities = new() { TextInput = true, SpeechOutput = true }
            }
        ]
    };

    static ActiveModelSelectionsState SelectVoiceModel(RpChatDocument document, AiProvider provider)
    {
        var selections = ActiveModelSelectionsState.CreateDefault();
        selections.Values[AiModelRole.Voice] = new()
        {
            ProviderId = provider.Id,
            ModelId = provider.Models.Single().Id
        };
        return selections;
    }

    static CharacterVoiceSelection Voice(string id) => new()
    {
        VoiceId = id,
        VoiceName = id,
        UpdatedUtc = DateTime.UtcNow
    };

    sealed class NoOpVoiceMessageStreamCoordinator : IVoiceMessageStreamCoordinator
    {
        public void Start(VoiceMessageGenerationRequest request)
        {
        }

        public Task<VoiceMessageStartResult> EnsureStartedAsync(string voiceMessageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new VoiceMessageStartResult(true));

        public Task CopyLiveAsync(string voiceMessageId, Stream output, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    sealed class RecordingVoiceMessageStreamCoordinator : IVoiceMessageStreamCoordinator
    {
        public List<VoiceMessageGenerationRequest> Starts { get; } = [];

        public void Start(VoiceMessageGenerationRequest request)
        {
            Starts.Add(request);
        }

        public Task<VoiceMessageStartResult> EnsureStartedAsync(string voiceMessageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new VoiceMessageStartResult(true));

        public Task CopyLiveAsync(string voiceMessageId, Stream output, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    sealed class NoOpStoredSpeechAssetService : IStoredSpeechAssetService
    {
        public Task StoreReadyAsync(string voiceMessageId, byte[] bytes, string contentType, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string voiceMessageId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
