using AgentRp.Data;
using AgentRp.Models;
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

        return rows.Select(ChatPersistenceMapper.ToModel).ToList();
    }

    public Task<List<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default) =>
        AiProviderPersistenceStore.LoadAsync(dbContextFactory, cancellationToken);

    public async Task<RpChatDocument> LoadChatDocumentAsync(string chatId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.ChatDocuments
            .AsNoTracking()
            .AsSingleQuery()
            .Include(x => x.Chat)
            .Where(x => x.ChatId == chatId)
            .OrderBy(x => x.ChatId)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            var chat = await dbContext.Chats
                .AsNoTracking()
                .Where(x => x.Id == chatId)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return ChatDocumentPersistenceMapper.CreateEmpty(chatId, chat);
        }

        var transcript = await TranscriptPersistenceStore.LoadActiveAsync(
            dbContext,
            row.ChatId,
            row.MessagesJson,
            cancellationToken);
        return ChatDocumentPersistenceMapper.ToModel(row, transcript);
    }

    public Task<RpTranscriptState> LoadActiveTranscriptAsync(string chatId, CancellationToken cancellationToken = default) =>
        TranscriptPersistenceStore.LoadActiveAsync(dbContextFactory, chatId, cancellationToken);

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

            ChatPersistenceMapper.Apply(chat, row, index, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveProvidersAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default) =>
        AiProviderPersistenceStore.SaveAsync(dbContextFactory, providers, cancellationToken);

    public async Task SaveChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var chat = await dbContext.Chats
            .Where(x => x.Id == document.Chat.Id)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
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
        ChatPersistenceMapper.Apply(document.Chat, chat, chat.SortOrder, now);
        ChatPersistenceMapper.ApplyTranscriptPreview(document, chat);

        var row = await dbContext.ChatDocuments
            .Where(x => x.ChatId == document.Chat.Id)
            .OrderBy(x => x.ChatId)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new RpChatDocumentRow
            {
                ChatId = document.Chat.Id,
                CreatedUtc = now
            };
            dbContext.ChatDocuments.Add(row);
        }

        ChatDocumentPersistenceMapper.Apply(document, row, now);
        await TranscriptPersistenceStore.SaveRowsAsync(dbContext, document, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
