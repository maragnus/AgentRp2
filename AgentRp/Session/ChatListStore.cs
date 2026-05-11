using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.UserSystem;

namespace AgentRp.Session;

public sealed class ChatListStore(Guid sessionId, ILiveRoleplayStore liveStore, ChatRegistry registry, ActiveChatContext activeChat, CurrentAppUser user) : StoreBase
{
	private readonly List<StoryPreview> _items = new List<StoryPreview>();

	public IReadOnlyList<StoryPreview> Items => _items;

	public StoryPreview? Active => (activeChat.Current == null) ? null : StoryPreviewProjector.FromDocument(activeChat.Current);

	public RoleplaySession? ActiveSession { get; set; }

	public async Task LoadAsync()
	{
		await RefreshAsync();
	}

	public async Task RefreshAsync()
	{
		_items.Clear();
		List<StoryPreview> items = _items;
		items.AddRange((await liveStore.LoadStoryPreviewsAsync(user)).Select(SessionCloner.Clone));
		await NotifyChangedAsync();
	}

	public async Task SelectAsync(string chatId)
	{
		if (!_items.Any((StoryPreview chat) => string.Equals(chat.ChatId, chatId, StringComparison.Ordinal)))
		{
			throw new InvalidOperationException("Story '" + chatId + "' was not found.");
		}
		RpChatDocument document = await registry.OpenAsync(chatId);
		await RefreshAsync();
		ActiveSession?.SetActiveChatId(document.Chat.Id);
	}

	public async Task ClearAsync()
	{
		await registry.CloseAsync();
		ActiveSession?.SetActiveChatId(null);
		await NotifyChangedAsync();
	}

	public async Task<StoryPreview> AddAsync(StoryCreationOptions options)
	{
		IReadOnlyList<StoryPreview> chats = await liveStore.AddChatAsync(user, sessionId, options, activeChat.Current);
		_items.Clear();
		_items.AddRange(chats.Select(SessionCloner.Clone));
		StoryPreview chat = _items.First();
		await SelectAsync(chat.ChatId);
		await NotifyChangedAsync();
		return chat;
	}
}
