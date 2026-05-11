using System;
using System.Threading.Tasks;
using AgentRp.Services;
using AgentRp.UserSystem;

namespace AgentRp.Session;

public sealed class ChatRegistry(Guid sessionId, ILiveRoleplayStore liveStore, ActiveChatContext activeChat, CurrentAppUser user)
{
	public async Task CloseAsync()
	{
		await activeChat.ClearAsync();
	}

	public async Task<RpChatDocument> OpenAsync(string chatId)
	{
		RpChatDocument document = await liveStore.OpenChatAsync(user, sessionId, chatId);
		await activeChat.SetAsync(document);
		return document;
	}

	public async Task ReplaceAreaAsync(RpChatDocument document, RoleplayStoreArea area)
	{
		await liveStore.ReplaceChatAreaAsync(user, sessionId, document.Chat.Id, document, area);
	}

	public async Task RefreshActiveAsync(RoleplayStoreArea area)
	{
		if (activeChat.Current != null)
		{
			RpChatDocument snapshot = await liveStore.GetChatSnapshotAsync(user, activeChat.Current.Chat.Id);
			await activeChat.UpdateAsync(snapshot, area);
		}
	}
}
