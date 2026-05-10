using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Serialization;
using AgentRp.Session;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public enum MessageSpeechAvailabilityKind
{
    NoVoiceModel,
    Ready,
    MissingVoice
}

public sealed record MessageSpeechAvailability(
    MessageSpeechAvailabilityKind Kind,
    string MissingEntityId = "",
    string MissingEntityName = "",
    bool MissingNarrator = false)
{
    public bool CanDisplay => Kind != MessageSpeechAvailabilityKind.NoVoiceModel;
    public bool CanGenerate => Kind == MessageSpeechAvailabilityKind.Ready;
}

public sealed record MessageSpeechPlayback(string Key, string Url, bool Generated);

public sealed record MessageSpeechInputSnapshot(
    string Status,
    string ProviderName,
    string ProviderType,
    string ModelId,
    DateTime CreatedUtc,
    IReadOnlyList<SpeechGenerationInput> Inputs);

public sealed record MessageSpeechPlan(
    ActiveModelSelection VoiceModel,
    IReadOnlyList<SpeechGenerationInput> Inputs,
    Dictionary<string, string> VoiceIds,
    string SourceHash);

public sealed class MessageSpeechMissingVoiceException(MessageSpeechAvailability availability)
    : InvalidOperationException("Reading this message aloud needs a selected voice.")
{
    public MessageSpeechAvailability Availability { get; } = availability;
}

public interface IMessageSpeechService
{
    MessageSpeechAvailability ResolveAvailability(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        RpTranscriptTurn turn);

    MessageSpeechAvailability ResolveSnapshotAvailability(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        RpTranscriptSnapshot snapshot);

    Task<MessageSpeechPlayback> GetOrGenerateAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        RpTranscriptTurn turn,
        bool regenerate,
        CancellationToken cancellationToken = default);

    Task<MessageSpeechPlayback> GetOrGenerateSnapshotAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        RpTranscriptSnapshot snapshot,
        bool regenerate,
        CancellationToken cancellationToken = default);

    Task<MessageSpeechInputSnapshot?> LoadInputSnapshotAsync(
        RpTranscriptTurn turn,
        CancellationToken cancellationToken = default);

    Task DiscardTurnSpeechAsync(RpTranscriptTurn turn, CancellationToken cancellationToken = default);

    Task DiscardSnapshotSpeechAsync(RpTranscriptSnapshot snapshot, CancellationToken cancellationToken = default);
}

public sealed class MessageSpeechService(
    IDbContextFactory<RpDbContext> dbContextFactory,
    IVoiceMessageStreamCoordinator streamCoordinator,
    IStoredSpeechAssetService storedSpeechAssetService,
    IModelCapabilityCatalog capabilityCatalog) : IMessageSpeechService
{
    public const int MaxSpeechCharacters = 2000;
    public const string NarratorVoiceKey = EntityIds.Narrator;

    public MessageSpeechAvailability ResolveAvailability(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        RpTranscriptTurn turn) =>
        ResolveAvailability(document, providers, modelSelections, turn.Body, turn.AuthorCharacterId, turn.AuthorName);

    public MessageSpeechAvailability ResolveSnapshotAvailability(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        RpTranscriptSnapshot snapshot) =>
        ResolveAvailability(document, providers, modelSelections, snapshot.Summary, "", "Snapshot");

    MessageSpeechAvailability ResolveAvailability(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        string text,
        string authorCharacterId,
        string authorName)
    {
        var voiceModel = ResolveVoiceModel(providers, modelSelections);
        if (voiceModel is null || !HasSpeechContent(NormalizeSpeechText(text)))
            return new(MessageSpeechAvailabilityKind.NoVoiceModel);

        return BuildPlan(document, providers, modelSelections, text, authorCharacterId, authorName) is not null
            ? new(MessageSpeechAvailabilityKind.Ready)
            : MissingVoiceAvailability(document, voiceModel, authorCharacterId, authorName);
    }

    public async Task<MessageSpeechPlayback> GetOrGenerateAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        RpTranscriptTurn turn,
        bool regenerate,
        CancellationToken cancellationToken = default) =>
        await GetOrGenerateCoreAsync(
            document,
            providers,
            modelSelections,
            turn.Id,
            PlaybackKey(turn),
            turn.Speech,
            (speech, generatedUtc) =>
            {
                turn.Speech = speech;
                turn.UpdatedUtc = generatedUtc;
            },
            turn.Body,
            turn.AuthorCharacterId,
            turn.AuthorName,
            regenerate,
            cancellationToken);

    public async Task<MessageSpeechPlayback> GetOrGenerateSnapshotAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        RpTranscriptSnapshot snapshot,
        bool regenerate,
        CancellationToken cancellationToken = default) =>
        await GetOrGenerateCoreAsync(
            document,
            providers,
            modelSelections,
            snapshot.Id,
            SnapshotPlaybackKey(snapshot),
            snapshot.Speech,
            (speech, _) => snapshot.Speech = speech,
            snapshot.Summary,
            "",
            "Snapshot",
            regenerate,
            cancellationToken);

    async Task<MessageSpeechPlayback> GetOrGenerateCoreAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        string ownerId,
        string key,
        RpMessageSpeechState speech,
        Action<RpMessageSpeechState, DateTime> setSpeech,
        string text,
        string authorCharacterId,
        string authorName,
        bool regenerate,
        CancellationToken cancellationToken)
    {
        var plan = BuildPlan(document, providers, modelSelections, text, authorCharacterId, authorName)
            ?? throw new MessageSpeechMissingVoiceException(ResolveAvailability(document, providers, modelSelections, text, authorCharacterId, authorName));
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!regenerate
            && !string.IsNullOrWhiteSpace(speech.VoiceMessageId)
            && string.Equals(speech.SourceHash, plan.SourceHash, StringComparison.Ordinal)
            && await CanReuseVoiceMessageAsync(dbContext, speech.VoiceMessageId, cancellationToken))
            return new(key, BuildAudioUrl(speech.VoiceMessageId), false);

        var now = DateTime.UtcNow;
        var voiceMessageId = $"speech-{Guid.NewGuid():N}";

        dbContext.SpeechAssets.Add(new()
        {
            Id = voiceMessageId,
            ChatId = document.Chat.Id,
            TurnId = ownerId,
            Status = SpeechAssetStatus.Pending,
            ContentType = "audio/mpeg",
            FileName = $"{ownerId}-{voiceMessageId}.mp3",
            ProviderId = plan.VoiceModel.Provider.Id,
            ProviderName = plan.VoiceModel.Provider.Name,
            ProviderType = plan.VoiceModel.Provider.Type,
            ProviderModelId = plan.VoiceModel.Model.Id,
            SourceHash = plan.SourceHash,
            InputsJson = JsonSerializer.Serialize(plan.Inputs, AppJsonSerializerOptions.Web),
            VoiceIdsJson = JsonSerializer.Serialize(plan.VoiceIds, AppJsonSerializerOptions.Web),
            CreatedUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        setSpeech(
            new()
            {
                VoiceMessageId = voiceMessageId,
                GeneratedUtc = now,
                SourceHash = plan.SourceHash,
                ProviderId = plan.VoiceModel.Provider.Id,
                ProviderName = plan.VoiceModel.Provider.Name,
                ProviderType = plan.VoiceModel.Provider.Type,
                ModelId = plan.VoiceModel.Model.Id,
                VoiceIds = plan.VoiceIds.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            },
            now);

        streamCoordinator.Start(new(
            voiceMessageId,
            SnapshotProvider(plan.VoiceModel.Provider),
            SnapshotModel(plan.VoiceModel.Model),
            plan.Inputs.ToList()));

        return new(key, BuildAudioUrl(voiceMessageId), true);
    }

    public async Task<MessageSpeechInputSnapshot?> LoadInputSnapshotAsync(
        RpTranscriptTurn turn,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(turn.Speech.VoiceMessageId))
            return null;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.SpeechAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(asset =>
                asset.Id == turn.Speech.VoiceMessageId
                && asset.TurnId == turn.Id,
                cancellationToken);
        if (row is null)
            return null;

        return new(
            row.Status,
            row.ProviderName,
            row.ProviderType,
            row.ProviderModelId,
            row.CreatedUtc,
            SpeechGenerationInputJson.Deserialize(row.InputsJson));
    }

    static async Task<bool> CanReuseVoiceMessageAsync(
        RpDbContext dbContext,
        string voiceMessageId,
        CancellationToken cancellationToken) =>
        await dbContext.SpeechAssets
            .AsNoTracking()
            .AnyAsync(asset =>
                asset.Id == voiceMessageId
                && asset.Status != SpeechAssetStatus.Failed,
                cancellationToken);

    public async Task DiscardTurnSpeechAsync(RpTranscriptTurn turn, CancellationToken cancellationToken = default) =>
        await DiscardSpeechAsync(turn.Speech, speech => turn.Speech = speech, cancellationToken);

    public async Task DiscardSnapshotSpeechAsync(RpTranscriptSnapshot snapshot, CancellationToken cancellationToken = default) =>
        await DiscardSpeechAsync(snapshot.Speech, speech => snapshot.Speech = speech, cancellationToken);

    async Task DiscardSpeechAsync(
        RpMessageSpeechState speech,
        Action<RpMessageSpeechState> setSpeech,
        CancellationToken cancellationToken)
    {
        var voiceMessageId = speech.VoiceMessageId;
        if (!string.IsNullOrWhiteSpace(voiceMessageId))
            await storedSpeechAssetService.DeleteAsync(voiceMessageId, cancellationToken);

        setSpeech(new());
    }

    MessageSpeechPlan? BuildPlan(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        string body,
        string authorCharacterId,
        string authorName)
    {
        var voiceModel = ResolveVoiceModel(providers, modelSelections);
        if (voiceModel is null)
            return null;

        var text = NormalizeSpeechText(body);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var narratorVoiceId = ResolveVoiceId(document.NarratorProfile.VoiceSelections, voiceModel.Key);
        text = NormalizeSpeechText(AudioTagTransportRules.SupportsAudioTags(voiceModel)
            ? text
            : AudioTagTransportRules.StripAudioTags(text));
        if (!HasSpeechContent(text))
            return null;

        if (IsNarrator(authorCharacterId))
        {
            if (string.IsNullOrWhiteSpace(narratorVoiceId))
                return null;

            var voiceIds = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [NarratorVoiceKey] = narratorVoiceId
            };
            return BuildSingleVoicePlan(voiceModel, text, narratorVoiceId, voiceIds);
        }

        var character = document.Characters.FirstOrDefault(character => character.Id == authorCharacterId);
        var characterVoiceId = character is null ? "" : ResolveVoiceId(character.VoiceSelections, voiceModel.Key);
        var canUseNarratorActions = CanUseNarratorActions(document, voiceModel, authorCharacterId, characterVoiceId, narratorVoiceId);
        if (canUseNarratorActions)
        {
            var voiceIds = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [authorCharacterId] = characterVoiceId,
                [NarratorVoiceKey] = narratorVoiceId
            };
            var inputs = BuildDialogueInputs(text, characterVoiceId, narratorVoiceId);
            if (inputs.Count == 0)
                return null;

            return new(voiceModel, inputs, voiceIds, BuildSourceHash(voiceModel, inputs, voiceIds));
        }

        var fallbackVoiceId = string.IsNullOrWhiteSpace(characterVoiceId) ? narratorVoiceId : characterVoiceId;
        if (string.IsNullOrWhiteSpace(fallbackVoiceId))
            return null;

        var fallbackVoiceIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [string.IsNullOrWhiteSpace(characterVoiceId) ? NarratorVoiceKey : authorCharacterId] = fallbackVoiceId
        };
        return BuildSingleVoicePlan(voiceModel, text, fallbackVoiceId, fallbackVoiceIds, true);
    }

    MessageSpeechAvailability MissingVoiceAvailability(
        RpChatDocument document,
        ActiveModelSelection voiceModel,
        string authorCharacterId,
        string authorName)
    {
        var narratorVoiceId = ResolveVoiceId(document.NarratorProfile.VoiceSelections, voiceModel.Key);
        if (IsNarrator(authorCharacterId))
            return new(MessageSpeechAvailabilityKind.MissingVoice, NarratorVoiceKey, "Narrator", true);

        var character = document.Characters.FirstOrDefault(character => character.Id == authorCharacterId);
        var characterVoiceId = character is null ? "" : ResolveVoiceId(character.VoiceSelections, voiceModel.Key);

        if (!string.IsNullOrWhiteSpace(characterVoiceId))
            return new(MessageSpeechAvailabilityKind.MissingVoice, authorCharacterId, authorName);

        if (!string.IsNullOrWhiteSpace(narratorVoiceId))
            return new(MessageSpeechAvailabilityKind.MissingVoice, NarratorVoiceKey, "Narrator", true);

        return new(MessageSpeechAvailabilityKind.MissingVoice, authorCharacterId, authorName);
    }

    ActiveModelSelection? ResolveVoiceModel(IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections)
    {
        foreach (var provider in providers)
            capabilityCatalog.ApplyResolvedCapabilities(provider);

        return TextModelTuningCatalog.TryResolveActiveModel(providers, AiModelRole.Voice, modelSelections);
    }

    static MessageSpeechPlan BuildSingleVoicePlan(
        ActiveModelSelection voiceModel,
        string text,
        string voiceId,
        Dictionary<string, string> voiceIds,
        bool formatElevenLabsV3 = false)
    {
        if (formatElevenLabsV3)
            text = FormatSingleVoiceText(voiceModel, text);

        var inputs = new[] { new SpeechGenerationInput(Truncate(text), voiceId) };
        return new(voiceModel, inputs, voiceIds, BuildSourceHash(voiceModel, inputs, voiceIds));
    }

    static string FormatSingleVoiceText(ActiveModelSelection voiceModel, string text) =>
        IsElevenLabsV3(voiceModel)
            ? FormatElevenLabsV3SingleVoiceText(text)
            : text;

    public static string FormatElevenLabsV3SingleVoiceText(string text)
    {
        var segments = SplitSpeechAndActions(text);
        if (segments.Count == 0)
            return NormalizeSpeechText(text);

        var parts = segments
            .Select(segment => segment.Spoken
                ? FormatElevenLabsV3Dialogue(segment.Text)
                : FormatElevenLabsV3Action(segment.Text))
            .Where(part => !string.IsNullOrWhiteSpace(part));

        return JoinSpeechParts(parts);
    }

    static string FormatElevenLabsV3Dialogue(string text)
    {
        var (prefix, body) = ExtractLeadingSquareTags(text);
        body = TrimWrappingQuotes(body);
        if (string.IsNullOrWhiteSpace(body))
            return EnsureTerminalPunctuation(prefix);

        var quoted = $"\"{EnsureTerminalPunctuation(body)}\"";
        return string.IsNullOrWhiteSpace(prefix)
            ? quoted
            : JoinSpeechParts(prefix, quoted);
    }

    static string FormatElevenLabsV3Action(string text) =>
        EnsureTerminalPunctuation(TrimWrappingQuotes(text));

    static bool CanUseNarratorActions(
        RpChatDocument document,
        ActiveModelSelection voiceModel,
        string authorCharacterId,
        string characterVoiceId,
        string narratorVoiceId) =>
        !IsNarrator(authorCharacterId)
        && document.Transcript.Options.SpeakActionsInNarratorVoice
        && IsElevenLabs(voiceModel.Provider)
        && !string.IsNullOrWhiteSpace(characterVoiceId)
        && !string.IsNullOrWhiteSpace(narratorVoiceId);

    public static IReadOnlyList<SpeechGenerationInput> BuildDialogueInputs(
        string text,
        string characterVoiceId,
        string narratorVoiceId)
    {
        var segments = SplitSpeechAndActions(text);
        var inputs = new List<SpeechGenerationInput>();
        var pendingPrefixes = new Dictionary<string, string>(StringComparer.Ordinal);
        var remaining = MaxSpeechCharacters;
        foreach (var segment in segments)
        {
            if (remaining <= 0)
                break;

            var voiceId = segment.Spoken ? characterVoiceId : narratorVoiceId;
            var clipped = segment.Text.Length > remaining ? segment.Text[..remaining] : segment.Text;
            remaining -= clipped.Length;
            if (!HasSpeechContent(clipped))
            {
                if (HasStrippableSpeechCue(clipped))
                    pendingPrefixes[voiceId] = pendingPrefixes.TryGetValue(voiceId, out var pending)
                        ? JoinSpeechParts(pending, clipped)
                        : clipped;

                continue;
            }

            if (pendingPrefixes.Remove(voiceId, out var prefix))
                clipped = JoinSpeechParts(prefix, clipped);

            if (inputs.LastOrDefault() is { } previous && previous.VoiceId == voiceId)
            {
                inputs[^1] = previous with { Text = JoinSpeechParts(previous.Text, clipped) };
                continue;
            }

            inputs.Add(new(clipped, voiceId));
        }

        return inputs;
    }

    public static IReadOnlyList<SpeechTextSegment> SplitSpeechAndActions(string text)
    {
        var normalized = NormalizeSpeechText(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return [];

        var segments = new List<SpeechTextSegment>();
        var start = 0;
        var action = false;
        for (var index = 0; index < normalized.Length; index++)
        {
            if (normalized[index] != '*')
                continue;

            if (action)
            {
                AddSegment(segments, normalized[start..index], false);
                action = false;
                start = index + 1;
                continue;
            }

            if (index > start)
                AddSegment(segments, normalized[start..index], true);

            action = true;
            start = index + 1;
        }

        if (start < normalized.Length)
            AddSegment(segments, normalized[start..], !action);

        return segments;
    }

    public static string NormalizeSpeechText(string text) =>
        string.Join("\n", (text ?? "")
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0))
            .Trim();

    static void AddSegment(List<SpeechTextSegment> segments, string text, bool spoken)
    {
        var normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        if (segments.LastOrDefault() is { } previous && previous.Spoken == spoken)
        {
            segments[^1] = previous with { Text = JoinSpeechParts(previous.Text, normalized) };
            return;
        }

        segments.Add(new(normalized, spoken));
    }

    static bool HasSpeechContent(string text)
    {
        var stripped = AudioTagTransportRules.StripAudioTags(text);
        foreach (var rune in stripped.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
                continue;

            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.UppercaseLetter
                or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter
                or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.OtherNumber)
                return true;
        }

        return false;
    }

    static bool HasStrippableSpeechCue(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var stripped = AudioTagTransportRules.StripAudioTags(text);
        return !string.Equals(stripped, text, StringComparison.Ordinal);
    }

    static string JoinSpeechParts(string left, string right) =>
        $"{left.TrimEnd()} {right.TrimStart()}".Trim();

    static string JoinSpeechParts(IEnumerable<string> parts) =>
        string.Join(" ", parts.Select(part => part.Trim()).Where(part => part.Length > 0)).Trim();

    static (string Prefix, string Body) ExtractLeadingSquareTags(string text)
    {
        var remaining = text.Trim();
        var tags = new List<string>();
        while (remaining.StartsWith('['))
        {
            if (!AudioTagTransportRules.TryReadSquareTag(remaining, 0, out var end))
                break;

            tags.Add(remaining[..(end + 1)]);
            remaining = remaining[(end + 1)..].TrimStart();
        }

        return (JoinSpeechParts(tags), remaining.Trim());
    }

    static string TrimWrappingQuotes(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
            ? trimmed[1..^1].Trim()
            : trimmed;
    }

    static string EnsureTerminalPunctuation(string text)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || EndsWithTerminalPunctuation(trimmed))
            return trimmed;

        return $"{trimmed.TrimEnd(',', ';', ':')}.";
    }

    static bool EndsWithTerminalPunctuation(string text)
    {
        for (var index = text.Length - 1; index >= 0; index--)
        {
            var current = text[index];
            if (char.IsWhiteSpace(current) || current is '"' or '\'' or ')' or ']')
                continue;

            return current is '.' or '!' or '?' or '…';
        }

        return false;
    }

    static string BuildSourceHash(
        ActiveModelSelection voiceModel,
        IReadOnlyList<SpeechGenerationInput> inputs,
        IReadOnlyDictionary<string, string> voiceIds)
    {
        var source = new StringBuilder()
            .Append(voiceModel.Provider.Id).Append('|')
            .Append(voiceModel.Provider.Type).Append('|')
            .Append(voiceModel.Model.Id).Append('|');
        foreach (var input in inputs)
            source.Append(input.VoiceId).Append(':').Append(input.Text).Append('|');
        foreach (var pair in voiceIds.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            source.Append(pair.Key).Append('=').Append(pair.Value).Append('|');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString())));
    }

    static string ResolveVoiceId(IReadOnlyDictionary<string, CharacterVoiceSelection> selections, string modelKey) =>
        selections.TryGetValue(modelKey, out var selection) ? selection.VoiceId : "";

    static string Truncate(string text) =>
        text.Length <= MaxSpeechCharacters ? text : text[..MaxSpeechCharacters];

    static bool IsNarrator(string authorCharacterId) =>
        string.IsNullOrWhiteSpace(authorCharacterId);

    static bool IsElevenLabs(AiProvider provider) =>
        string.Equals(provider.Type.Trim(), "elevenlabs", StringComparison.OrdinalIgnoreCase);

    static bool IsElevenLabsV3(ActiveModelSelection voiceModel) =>
        IsElevenLabs(voiceModel.Provider)
        && string.Equals(voiceModel.Model.Id.Trim(), "eleven_v3", StringComparison.OrdinalIgnoreCase);

    public static string PlaybackKey(RpTranscriptTurn turn) => $"message-speech::{turn.Id}";

    public static string SnapshotPlaybackKey(RpTranscriptSnapshot snapshot) => $"snapshot-speech::{snapshot.Id}";

    static AiProvider SnapshotProvider(AiProvider provider) => new()
    {
        Id = provider.Id,
        Name = provider.Name,
        Type = provider.Type,
        Enabled = provider.Enabled,
        ApiKey = provider.ApiKey,
        Endpoint = provider.Endpoint
    };

    static AiProviderModel SnapshotModel(AiProviderModel model) => new()
    {
        Id = model.Id,
        DisplayName = model.DisplayName,
        Enabled = model.Enabled
    };

    public static string BuildAudioUrl(string voiceMessageId) =>
        $"/story-audio/{Uri.EscapeDataString(voiceMessageId)}";
}

public sealed record SpeechTextSegment(string Text, bool Spoken);
