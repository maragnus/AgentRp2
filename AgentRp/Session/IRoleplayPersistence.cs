using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.UserSystem;

namespace AgentRp.Session;

public interface IRoleplayPersistence
{
	Task<List<StoryPreview>> LoadStoryPreviewsAsync(CurrentAppUser user, CancellationToken cancellationToken = default(CancellationToken));

	Task<List<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task<RpChatDocument> LoadChatDocumentAsync(CurrentAppUser user, string chatId, CancellationToken cancellationToken = default(CancellationToken));

	Task<RpTranscriptState> LoadActiveTranscriptAsync(string chatId, CancellationToken cancellationToken = default(CancellationToken));

	Task SaveStoryPreviewsAsync(CurrentAppUser user, IReadOnlyList<StoryPreview> previews, CancellationToken cancellationToken = default(CancellationToken));

	Task SaveProvidersAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default(CancellationToken));

	Task CreateChatDocumentAsync(CurrentAppUser user, RpChatDocument document, CancellationToken cancellationToken = default(CancellationToken));

	Task SaveChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default(CancellationToken));

	Task SaveChatAreaAsync(CurrentAppUser user, RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default(CancellationToken));
}
