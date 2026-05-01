using AgentRp.Models;

namespace AgentRp.Session;

public interface IRoleplayPersistence
{
    Task<List<RpChat>> LoadChatsAsync(CancellationToken cancellationToken = default);
    Task<List<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default);
    Task<RpChatDocument> LoadChatDocumentAsync(string chatId, CancellationToken cancellationToken = default);
    Task SaveChatsAsync(IReadOnlyList<RpChat> chats, CancellationToken cancellationToken = default);
    Task SaveProvidersAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default);
    Task SaveChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default);
}
