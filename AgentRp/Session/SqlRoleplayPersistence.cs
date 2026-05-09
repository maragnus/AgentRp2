using System.Text.Json;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Serialization;
using AgentRp.Services;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Session;

public sealed class SqlRoleplayPersistence(IDbContextFactory<RpDbContext> dbContextFactory) : IRoleplayPersistence
{
    public async Task<List<RpChat>> LoadChatsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.Chats
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.LastMessageUtc ?? x.UpdatedUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(ToModel).ToList();
    }

    public async Task<List<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.AiProviders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Models)
            .Include(x => x.Metrics)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(AiProviderPersistenceMapper.ToModel).ToList();
    }

    public async Task<RpChatDocument> LoadChatDocumentAsync(string chatId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.ChatDocuments
            .AsNoTracking()
            .Include(x => x.Chat)
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (row is null)
        {
            var chat = await dbContext.Chats
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == chatId, cancellationToken);

            return new RpChatDocument
            {
                Chat = chat is null ? new RpChat { Id = chatId } : ToModel(chat)
            };
        }

        return new RpChatDocument
        {
            Chat = ToModel(row.Chat),
            Characters = Deserialize(row.CharactersJson, new List<RpCharacter>()),
            CharacterRelationships = Deserialize(row.CharacterRelationshipsJson, new List<RpCharacterRelationship>()),
            Locations = Deserialize(row.LocationsJson, new List<RpLocation>()),
            Items = Deserialize(row.ItemsJson, new List<RpItem>()),
            Timeline = Deserialize(row.TimelineJson, new List<RpTimelineEntry>()),
            Images = Deserialize(row.ImagesJson, new List<GalleryImage>()),
            Transcript = Deserialize(row.MessagesJson, new RpTranscriptState()),
            StoryAssistant = Deserialize(row.StoryAssistantJson, new StoryAssistantState()),
            ChatDirection = Deserialize(row.ChatDirectionJson, ChatDirectionState.CreateDefault()),
            NarratorProfile = Deserialize(row.NarratorProfileJson, NarratorProfileState.CreateDefault()),
            PromptLibrary = Deserialize(row.PromptLibraryJson, PromptLibraryState.CreateDefault()),
            CharacterTraitLibrary = Deserialize(row.CharacterTraitLibraryJson, CharacterTraitLibraryState.CreateDefault()),
            ModelTuning = Deserialize(row.ModelTuningJson, ModelTuningState.CreateDefault())
        }.ApplyProjection();
    }

    public async Task SaveChatsAsync(IReadOnlyList<RpChat> chats, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var existing = await dbContext.Chats.ToDictionaryAsync(x => x.Id, cancellationToken);

        for (var index = 0; index < chats.Count; index++)
        {
            var chat = chats[index];
            if (!existing.TryGetValue(chat.Id, out var row))
            {
                row = new RpChatRow
                {
                    Id = chat.Id,
                    CreatedUtc = now
                };
                dbContext.Chats.Add(row);
            }

            Apply(chat, row, index, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveProvidersAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        dbContext.AiProviderModels.RemoveRange(dbContext.AiProviderModels);
        dbContext.AiProviders.RemoveRange(dbContext.AiProviders);
        await dbContext.SaveChangesAsync(cancellationToken);

        for (var providerIndex = 0; providerIndex < providers.Count; providerIndex++)
        {
            var provider = providers[providerIndex];
            dbContext.AiProviders.Add(new AiProviderRow
            {
                Id = provider.Id,
                Name = provider.Name,
                Type = provider.Type,
                Enabled = provider.Enabled,
                ApiKey = provider.ApiKey,
                ManagementApiKey = provider.ManagementApiKey,
                Endpoint = provider.Endpoint,
                AccountId = provider.AccountId,
                ProjectId = provider.ProjectId,
                TeamId = provider.TeamId,
                LastMetricsRefreshUtc = provider.LastMetricsRefreshUtc,
                LastMetricsError = provider.LastMetricsError,
                SortOrder = providerIndex,
                CreatedUtc = now,
                UpdatedUtc = now,
                Models = provider.Models.Select((model, modelIndex) => new AiProviderModelRow
                {
                    Id = model.Id,
                    DisplayName = model.DisplayName,
                    Endpoint = model.Endpoint,
                    Repository = model.Repository,
                    CreatedUnix = model.CreatedUnix,
                    Enabled = model.Enabled,
                    RolesJson = Serialize(model.Roles),
                    LastVoiceRefreshUtc = model.LastVoiceRefreshUtc,
                    LastVoiceRefreshError = model.LastVoiceRefreshError,
                    VoicesJson = Serialize(model.Voices),
                    SortOrder = modelIndex
                }).ToList(),
                Metrics = provider.Metrics.Select(metric => new AiProviderMetricRow
                {
                    Id = string.IsNullOrWhiteSpace(metric.Id) ? $"pm{Guid.NewGuid():N}" : metric.Id,
                    Kind = metric.Kind,
                    Label = metric.Label,
                    Value = metric.Value,
                    Detail = metric.Detail,
                    RefreshedUtc = metric.RefreshedUtc
                }).ToList()
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var chat = await dbContext.Chats.FirstOrDefaultAsync(x => x.Id == document.Chat.Id, cancellationToken);
        if (chat is null)
        {
            chat = new RpChatRow
            {
                Id = document.Chat.Id,
                CreatedUtc = now,
                SortOrder = await dbContext.Chats.CountAsync(cancellationToken)
            };
            dbContext.Chats.Add(chat);
        }

        TranscriptProjector.Apply(document, now);
        ChatPreviewProjector.Apply(document.Chat, document);
        Apply(document.Chat, chat, chat.SortOrder, now);

        var row = await dbContext.ChatDocuments.FirstOrDefaultAsync(x => x.ChatId == document.Chat.Id, cancellationToken);
        if (row is null)
        {
            row = new RpChatDocumentRow
            {
                ChatId = document.Chat.Id,
                CreatedUtc = now
            };
            dbContext.ChatDocuments.Add(row);
        }

        row.CharactersJson = Serialize(document.Characters);
        row.CharacterRelationshipsJson = Serialize(document.CharacterRelationships);
        row.LocationsJson = Serialize(document.Locations);
        row.ItemsJson = Serialize(document.Items);
        row.TimelineJson = Serialize(document.Timeline);
        row.ImagesJson = Serialize(document.Images);
        row.MessagesJson = Serialize(document.Transcript);
        row.StoryAssistantJson = Serialize(document.StoryAssistant);
        row.ChatDirectionJson = Serialize(ChatDirectionService.NormalizeState(document.ChatDirection));
        row.NarratorProfileJson = Serialize(NarratorProfileService.NormalizeState(document.NarratorProfile));
        row.PromptLibraryJson = Serialize(PromptLibraryService.CreateOverridesFromResolved(document.PromptLibrary));
        row.CharacterTraitLibraryJson = Serialize(document.CharacterTraitLibrary);
        row.ModelTuningJson = Serialize(document.ModelTuning);
        row.UpdatedUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    static RpChat ToModel(RpChatRow row) => new()
    {
        Id = row.Id,
        Title = row.Title,
        Updated = row.Updated,
        LastMessageUtc = row.LastMessageUtc,
        LastGeneratedTurnNumber = row.LastGeneratedTurnNumber,
        Starred = row.Starred,
        Messages = row.Messages,
        Location = row.Location,
        ActiveLocation = Deserialize(row.ActiveLocationJson, (RpChatSceneLocation?)null) ?? FallbackLocation(row),
        SceneCharacters = Deserialize(row.SceneCharactersJson, new List<RpChatSceneCharacter>())
    };

    static void Apply(RpChat chat, RpChatRow row, int sortOrder, DateTime now)
    {
        row.Title = chat.Title;
        row.Updated = chat.Updated;
        row.LastMessageUtc = chat.LastMessageUtc;
        row.LastGeneratedTurnNumber = chat.LastGeneratedTurnNumber;
        row.Starred = chat.Starred;
        row.Messages = chat.Messages;
        row.Location = chat.Location;
        row.ActiveLocationJson = Serialize(chat.ActiveLocation);
        row.SceneCharactersJson = Serialize(chat.SceneCharacters);
        row.SortOrder = sortOrder;
        row.UpdatedUtc = now;
    }

    static RpChatSceneLocation? FallbackLocation(RpChatRow row) =>
        string.IsNullOrWhiteSpace(row.Location)
            ? null
            : new() { Name = row.Location };

    static string Serialize<T>(T value) => JsonSerializer.Serialize(value, AppJsonSerializerOptions.Web);

    static T Deserialize<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;

        try
        {
            return JsonSerializer.Deserialize<T>(json, AppJsonSerializerOptions.Web) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}

static class SqlRoleplayPersistenceExtensions
{
    public static RpChatDocument ApplyProjection(this RpChatDocument document)
    {
        TranscriptProjector.Apply(document);
        return document;
    }
}
