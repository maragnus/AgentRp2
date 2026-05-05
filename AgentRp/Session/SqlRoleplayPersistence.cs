using System.Text.Json;
using AgentRp.Data;
using AgentRp.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Session;

public sealed class SqlRoleplayPersistence(IDbContextFactory<RpDbContext> dbContextFactory) : IRoleplayPersistence
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<List<RpChat>> LoadChatsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Chats
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.UpdatedUtc)
            .Select(x => new RpChat
            {
                Id = x.Id,
                Title = x.Title,
                Updated = x.Updated,
                Starred = x.Starred,
                Messages = x.Messages,
                Location = x.Location
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.AiProviders
            .AsNoTracking()
            .Include(x => x.Models)
            .Include(x => x.Metrics)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new AiProvider
        {
            Id = row.Id,
            Name = row.Name,
            Type = row.Type,
            Enabled = row.Enabled,
            ApiKey = row.ApiKey,
            ManagementApiKey = row.ManagementApiKey,
            Endpoint = row.Endpoint,
            AccountId = row.AccountId,
            ProjectId = row.ProjectId,
            TeamId = row.TeamId,
            LastMetricsRefreshUtc = row.LastMetricsRefreshUtc,
            LastMetricsError = row.LastMetricsError,
            Models = row.Models
                .OrderBy(model => model.SortOrder)
                .ThenBy(model => model.Id)
                .Select(model => new AiProviderModel
                {
                    Id = model.Id,
                    DisplayName = model.DisplayName,
                    Endpoint = model.Endpoint,
                    Repository = model.Repository,
                    CreatedUnix = model.CreatedUnix,
                    Enabled = model.Enabled,
                    Text = model.Text,
                    Image = model.Image,
                    ActiveText = model.ActiveText
                })
                .ToList(),
            Metrics = row.Metrics
                .OrderBy(metric => metric.Label)
                .ThenBy(metric => metric.Kind)
                .Select(metric => new AiProviderMetric
                {
                    Id = metric.Id,
                    Kind = metric.Kind,
                    Label = metric.Label,
                    Value = metric.Value,
                    Detail = metric.Detail,
                    RefreshedUtc = metric.RefreshedUtc
                })
                .ToList()
        }).ToList();
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
            Locations = Deserialize(row.LocationsJson, new List<RpLocation>()),
            Items = Deserialize(row.ItemsJson, new List<RpItem>()),
            Timeline = Deserialize(row.TimelineJson, new List<RpTimelineEntry>()),
            Images = Deserialize(row.ImagesJson, new List<GalleryImage>()),
            Transcript = Deserialize(row.MessagesJson, new RpTranscriptState()),
            PromptLibrary = Deserialize(row.PromptLibraryJson, PromptLibraryState.CreateDefault()),
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
                    Text = model.Text,
                    Image = model.Image,
                    ActiveText = model.ActiveText,
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
        row.LocationsJson = Serialize(document.Locations);
        row.ItemsJson = Serialize(document.Items);
        row.TimelineJson = Serialize(document.Timeline);
        row.ImagesJson = Serialize(document.Images);
        TranscriptProjector.Apply(document, now);
        row.MessagesJson = Serialize(document.Transcript);
        row.PromptLibraryJson = Serialize(document.PromptLibrary);
        row.ModelTuningJson = Serialize(document.ModelTuning);
        row.UpdatedUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    static RpChat ToModel(RpChatRow row) => new()
    {
        Id = row.Id,
        Title = row.Title,
        Updated = row.Updated,
        Starred = row.Starred,
        Messages = row.Messages,
        Location = row.Location
    };

    static void Apply(RpChat chat, RpChatRow row, int sortOrder, DateTime now)
    {
        row.Title = chat.Title;
        row.Updated = chat.Updated;
        row.Starred = chat.Starred;
        row.Messages = chat.Messages;
        row.Location = chat.Location;
        row.SortOrder = sortOrder;
        row.UpdatedUtc = now;
    }

    static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    static T Deserialize<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback;
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
