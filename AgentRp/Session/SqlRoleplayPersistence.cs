using AgentRp.Data;
using AgentRp.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Session;

public sealed class SqlRoleplayPersistence(IDbContextFactory<RpDbContext> dbContextFactory) : IRoleplayPersistence
{
    public async Task<List<StoryPreview>> LoadStoryPreviewsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var chats = await dbContext.Chats
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.LastMessageUtc ?? x.UpdatedUtc)
            .ToListAsync(cancellationToken);
        var chatIds = chats.Select(chat => chat.Id).ToHashSet(StringComparer.Ordinal);
        var locations = await dbContext.ChatLocations
            .AsNoTracking()
            .Where(row => chatIds.Contains(row.ChatId))
            .ToListAsync(cancellationToken);
        var sceneCharacters = await dbContext.ChatCurrentSceneCharacters
            .AsNoTracking()
            .Where(row => chatIds.Contains(row.ChatId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var characters = await dbContext.ChatCharacters
            .AsNoTracking()
            .Where(row => chatIds.Contains(row.ChatId))
            .ToListAsync(cancellationToken);
        var images = await dbContext.ImageAssets
            .AsNoTracking()
            .Where(row => chatIds.Contains(row.ChatId))
            .ToListAsync(cancellationToken);

        var locationsByKey = locations.ToDictionary(row => $"{row.ChatId}:{row.Id}", StringComparer.Ordinal);
        var charactersByKey = characters.ToDictionary(row => $"{row.ChatId}:{row.Id}", StringComparer.Ordinal);
        var imagesByKey = images.ToDictionary(row => $"{row.ChatId}:{row.Id}", StringComparer.Ordinal);

        return chats
            .Select(chat => BuildPreview(chat, sceneCharacters.Where(row => row.ChatId == chat.Id), locationsByKey, charactersByKey, imagesByKey))
            .ToList();
    }

    public Task<List<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default) =>
        AiProviderPersistenceStore.LoadAsync(dbContextFactory, cancellationToken);

    public async Task<RpChatDocument> LoadChatDocumentAsync(string chatId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var chat = await dbContext.Chats
            .AsNoTracking()
            .Where(x => x.Id == chatId)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (chat is null)
            return ChatDocumentPersistenceMapper.CreateEmpty(chatId, null);

        var rows = new ChatDocumentRows(
            chat,
            await dbContext.ChatCharacters.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken),
            await dbContext.ChatCharacterRelationships.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken),
            await dbContext.ChatLocations.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken),
            await dbContext.ChatItems.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken),
            await dbContext.ChatTimelineEntries.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken),
            await dbContext.ImageAssets.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.SortOrder).ThenByDescending(x => x.CreatedUtc).ToListAsync(cancellationToken),
            await dbContext.ChatTranscriptStates.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.ChatId).FirstOrDefaultAsync(cancellationToken),
            await dbContext.ChatDirectionStates.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.ChatId).FirstOrDefaultAsync(cancellationToken),
            await dbContext.NarratorProfileStates.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.ChatId).FirstOrDefaultAsync(cancellationToken),
            await dbContext.PromptLibraryStates.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.ChatId).FirstOrDefaultAsync(cancellationToken),
            await dbContext.CharacterTraitLibraryStates.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.ChatId).FirstOrDefaultAsync(cancellationToken),
            await dbContext.ModelTuningStates.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.ChatId).FirstOrDefaultAsync(cancellationToken));

        var transcript = await TranscriptPersistenceStore.LoadActiveAsync(dbContext, chatId, rows.TranscriptState, chat.ActiveLeafTurnId, cancellationToken);
        return ChatDocumentPersistenceMapper.ToModel(rows, transcript);
    }

    public Task<RpTranscriptState> LoadActiveTranscriptAsync(string chatId, CancellationToken cancellationToken = default) =>
        TranscriptPersistenceStore.LoadActiveAsync(dbContextFactory, chatId, cancellationToken);

    public async Task SaveStoryPreviewsAsync(IReadOnlyList<StoryPreview> previews, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.Chats.ToDictionaryAsync(x => x.Id, cancellationToken);
        var now = DateTime.UtcNow;
        for (var index = 0; index < previews.Count; index++)
        {
            var preview = previews[index];
            if (!existing.TryGetValue(preview.ChatId, out var row))
                continue;

            row.SortOrder = index;
            row.Title = preview.Title;
            row.Starred = preview.Starred;
            row.UpdatedUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveProvidersAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default) =>
        AiProviderPersistenceStore.SaveAsync(dbContextFactory, providers, cancellationToken);

    public async Task CreateChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await SaveDocumentAsync(dbContext, document, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default) =>
        CreateChatDocumentAsync(document, cancellationToken);

    public async Task SaveChatAreaAsync(RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await SaveDocumentAsync(dbContext, document, area, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    static StoryPreview BuildPreview(
        RpChatRow chat,
        IEnumerable<ChatCurrentSceneCharacterRow> sceneCharacters,
        IReadOnlyDictionary<string, ChatLocationRow> locationsByKey,
        IReadOnlyDictionary<string, ChatCharacterRow> charactersByKey,
        IReadOnlyDictionary<string, ImageAssetRow> imagesByKey)
    {
        locationsByKey.TryGetValue($"{chat.Id}:{chat.ActiveLocationId}", out var location);
        var locationImage = location is null ? null : ImageById(imagesByKey, chat.Id, location.ImageId);
        var characters = sceneCharacters
            .Select(row => charactersByKey.TryGetValue($"{row.ChatId}:{row.CharacterId}", out var character)
                ? new StoryPreviewCharacter
                {
                    CharacterId = character.Id,
                    Name = character.Name,
                    Avatar = ChatPersistenceMapper.ToPreviewAvatar(ImageById(imagesByKey, row.ChatId, character.ImageId))
                }
                : null)
            .OfType<StoryPreviewCharacter>()
            .ToList();
        var previewLocation = location is null && string.IsNullOrWhiteSpace(chat.ActiveLocationName)
            ? null
            : new StoryPreviewLocation
            {
                LocationId = location?.Id ?? chat.ActiveLocationId,
                Name = location?.Name ?? chat.ActiveLocationName,
                Avatar = ChatPersistenceMapper.ToPreviewAvatar(locationImage)
            };

        return ChatPersistenceMapper.ToPreview(chat, previewLocation, characters);
    }

    static ImageAssetRow? ImageById(IReadOnlyDictionary<string, ImageAssetRow> imagesByKey, string chatId, string imageId) =>
        string.IsNullOrWhiteSpace(imageId) || !imagesByKey.TryGetValue($"{chatId}:{imageId}", out var image)
            ? null
            : image;

    static async Task SaveDocumentAsync(RpDbContext dbContext, RpChatDocument document, RoleplayStoreArea? area, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        TranscriptProjector.Apply(document, now);
        var chat = await EnsureChatAsync(dbContext, document, now, cancellationToken);
        var preview = StoryPreviewProjector.FromDocument(document);
        ApplyPreviewToChat(preview, chat);
        ChatPersistenceMapper.Apply(document.Chat, chat, chat.SortOrder, now);
        ChatPersistenceMapper.ApplyTranscriptPreview(document, chat);

        if (area is null or RoleplayStoreArea.Characters)
        {
            await SaveCharactersAsync(dbContext, document, now, cancellationToken);
            await SaveRelationshipsAsync(dbContext, document, now, cancellationToken);
        }

        if (area is null or RoleplayStoreArea.Locations)
            await SaveLocationsAsync(dbContext, document, now, cancellationToken);

        if (area is null or RoleplayStoreArea.Items)
            await SaveItemsAsync(dbContext, document, now, cancellationToken);

        if (area is null or RoleplayStoreArea.Timeline)
            await SaveTimelineAsync(dbContext, document, now, cancellationToken);

        if (area is null or RoleplayStoreArea.Images)
            await SaveImagesAsync(dbContext, document, cancellationToken);

        if (area is null or RoleplayStoreArea.Transcript)
        {
            await SaveTranscriptStateAsync(dbContext, document, now, cancellationToken);
            await TranscriptPersistenceStore.SaveRowsAsync(dbContext, document, cancellationToken);
        }

        if (area is null or RoleplayStoreArea.ChatDirection)
            await SaveStateAsync(dbContext.ChatDirectionStates, document.Chat.Id, now, (row, timestamp) => ChatDocumentPersistenceMapper.Apply(document.ChatDirection, row, timestamp), cancellationToken);

        if (area is null or RoleplayStoreArea.NarratorProfile)
            await SaveStateAsync(dbContext.NarratorProfileStates, document.Chat.Id, now, (row, timestamp) => ChatDocumentPersistenceMapper.Apply(document.NarratorProfile, row, timestamp), cancellationToken);

        if (area is null or RoleplayStoreArea.PromptLibrary)
            await SaveStateAsync(dbContext.PromptLibraryStates, document.Chat.Id, now, (row, timestamp) => ChatDocumentPersistenceMapper.Apply(document.PromptLibrary, row, timestamp), cancellationToken);

        if (area is null or RoleplayStoreArea.CharacterTraitLibrary)
            await SaveStateAsync(dbContext.CharacterTraitLibraryStates, document.Chat.Id, now, (row, timestamp) => ChatDocumentPersistenceMapper.Apply(document.CharacterTraitLibrary, row, timestamp), cancellationToken);

        if (area is null or RoleplayStoreArea.ModelTuning)
            await SaveStateAsync(dbContext.ModelTuningStates, document.Chat.Id, now, (row, timestamp) => ChatDocumentPersistenceMapper.Apply(document.ModelTuning, row, timestamp), cancellationToken);
    }

    static async Task<RpChatRow> EnsureChatAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
    {
        var chat = await dbContext.Chats
            .Where(x => x.Id == document.Chat.Id)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (chat is not null)
            return chat;

        chat = new()
        {
            Id = document.Chat.Id,
            CreatedUtc = now,
            SortOrder = await dbContext.Chats.CountAsync(cancellationToken)
        };
        dbContext.Chats.Add(chat);
        return chat;
    }

    static void ApplyPreviewToChat(StoryPreview preview, RpChatRow row)
    {
        row.ActiveLocationId = preview.ActiveLocation?.LocationId ?? "";
        row.ActiveLocationName = preview.ActiveLocation?.Name ?? "";
        row.ActiveTurnCount = preview.VisibleTurnCount;
    }

    static async Task SaveCharactersAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ChatCharacters.Where(x => x.ChatId == document.Chat.Id).ToDictionaryAsync(x => x.Id, cancellationToken);
        SaveRows(document.Chat.Id, document.Characters, existing, dbContext.ChatCharacters, now, StoryEntityPersistenceMapper.Apply);
    }

    static async Task SaveRelationshipsAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ChatCharacterRelationships.Where(x => x.ChatId == document.Chat.Id).ToDictionaryAsync(x => x.Id, cancellationToken);
        SaveRows(document.Chat.Id, document.CharacterRelationships, existing, dbContext.ChatCharacterRelationships, now, StoryEntityPersistenceMapper.Apply);
    }

    static async Task SaveLocationsAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ChatLocations.Where(x => x.ChatId == document.Chat.Id).ToDictionaryAsync(x => x.Id, cancellationToken);
        SaveRows(document.Chat.Id, document.Locations, existing, dbContext.ChatLocations, now, StoryEntityPersistenceMapper.Apply);
    }

    static async Task SaveItemsAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ChatItems.Where(x => x.ChatId == document.Chat.Id).ToDictionaryAsync(x => x.Id, cancellationToken);
        SaveRows(document.Chat.Id, document.Items, existing, dbContext.ChatItems, now, StoryEntityPersistenceMapper.Apply);
    }

    static async Task SaveTimelineAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ChatTimelineEntries.Where(x => x.ChatId == document.Chat.Id).ToDictionaryAsync(x => x.Id, cancellationToken);
        SaveRows(document.Chat.Id, document.Timeline, existing, dbContext.ChatTimelineEntries, now, StoryEntityPersistenceMapper.Apply);
    }

    static async Task SaveImagesAsync(RpDbContext dbContext, RpChatDocument document, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ImageAssets.Where(x => x.ChatId == document.Chat.Id).ToDictionaryAsync(x => x.Id, cancellationToken);
        var desiredIds = document.Images.Select(image => image.Id).ToHashSet(StringComparer.Ordinal);
        dbContext.ImageAssets.RemoveRange(existing.Values.Where(row => !desiredIds.Contains(row.Id)));
        for (var index = 0; index < document.Images.Count; index++)
        {
            var image = document.Images[index];
            if (!existing.TryGetValue(image.Id, out var row))
                continue;

            StoryEntityPersistenceMapper.Apply(image, row, index);
        }
    }

    static async Task SaveTranscriptStateAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
    {
        var row = await dbContext.ChatTranscriptStates
            .Where(x => x.ChatId == document.Chat.Id)
            .OrderBy(x => x.ChatId)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new() { ChatId = document.Chat.Id, CreatedUtc = now };
            dbContext.ChatTranscriptStates.Add(row);
        }

        ChatDocumentPersistenceMapper.ApplyTranscriptShell(document.Transcript, row, now);
    }

    static async Task SaveStateAsync<TRow>(
        DbSet<TRow> rows,
        string chatId,
        DateTime now,
        Action<TRow, DateTime> apply,
        CancellationToken cancellationToken)
        where TRow : class, new()
    {
        var row = await rows
            .Where(x => EF.Property<string>(x, "ChatId") == chatId)
            .OrderBy(x => EF.Property<string>(x, "ChatId"))
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new();
            typeof(TRow).GetProperty("ChatId")?.SetValue(row, chatId);
            typeof(TRow).GetProperty("CreatedUtc")?.SetValue(row, now);
            rows.Add(row);
        }

        apply(row, now);
    }

    static void SaveRows<TModel, TRow>(
        string chatId,
        IReadOnlyList<TModel> models,
        Dictionary<string, TRow> existing,
        DbSet<TRow> rows,
        DateTime now,
        Action<TModel, TRow, int, DateTime> apply)
        where TRow : class, new()
    {
        var desiredIds = models.Select(GetId).ToHashSet(StringComparer.Ordinal);
        rows.RemoveRange(existing.Where(pair => !desiredIds.Contains(pair.Key)).Select(pair => pair.Value));
        for (var index = 0; index < models.Count; index++)
        {
            var model = models[index];
            var id = GetId(model);
            if (!existing.TryGetValue(id, out var row))
            {
                row = new();
                typeof(TRow).GetProperty("ChatId")?.SetValue(row, chatId);
                typeof(TRow).GetProperty("Id")?.SetValue(row, id);
                typeof(TRow).GetProperty("CreatedUtc")?.SetValue(row, now);
                rows.Add(row);
            }

            apply(model, row, index, now);
        }
    }

    static string GetId<TModel>(TModel model) =>
        (string?)typeof(TModel).GetProperty("Id")?.GetValue(model) ?? "";
}
