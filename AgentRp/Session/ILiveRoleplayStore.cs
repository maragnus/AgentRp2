using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.UserSystem;

namespace AgentRp.Session;

public interface ILiveRoleplayStore
{
	event Func<RoleplayStoreNotification, Task>? Changed;

	Task<IReadOnlyList<StoryPreview>> LoadStoryPreviewsAsync(CurrentAppUser user, CancellationToken cancellationToken = default(CancellationToken));

	Task<IReadOnlyList<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task<RpChatDocument> OpenChatAsync(CurrentAppUser user, Guid sessionId, string chatId, CancellationToken cancellationToken = default(CancellationToken));

	void ReleaseChat(Guid sessionId, string? chatId);

	Task<RpChatDocument> GetChatSnapshotAsync(CurrentAppUser user, string chatId, CancellationToken cancellationToken = default(CancellationToken));

	Task<IReadOnlyList<StoryPreview>> AddChatAsync(CurrentAppUser user, Guid originSessionId, StoryCreationOptions options, RpChatDocument? template, CancellationToken cancellationToken = default(CancellationToken));

	Task ReplaceProvidersAsync(CurrentAppUser user, Guid originSessionId, IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default(CancellationToken));

	Task ReplaceChatAreaAsync(CurrentAppUser user, Guid originSessionId, string chatId, RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default(CancellationToken));
}
