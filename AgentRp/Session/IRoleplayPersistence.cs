using AgentRp.Models;

namespace AgentRp.Session;

public interface IRoleplayPersistence
{
    Task<List<StoryPreview>> LoadStoryPreviewsAsync(CancellationToken cancellationToken = default);
    Task<List<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default);
    Task<RpChatDocument> LoadChatDocumentAsync(string chatId, CancellationToken cancellationToken = default);
    Task<RpTranscriptState> LoadActiveTranscriptAsync(string chatId, CancellationToken cancellationToken = default);
    Task SaveStoryPreviewsAsync(IReadOnlyList<StoryPreview> previews, CancellationToken cancellationToken = default);
    Task SaveProvidersAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default);
    Task CreateChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default);
    Task SaveChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default);
    Task SaveChatAreaAsync(RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default);
}
