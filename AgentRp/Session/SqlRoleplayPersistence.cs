using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Services;
using Microsoft.EntityFrameworkCore;
using AgentRp.UserSystem;

namespace AgentRp.Session;

public sealed class SqlRoleplayPersistence(IDbContextFactory<RpDbContext> dbContextFactory) : IRoleplayPersistence
{
	public async Task<List<StoryPreview>> LoadStoryPreviewsAsync(CurrentAppUser user, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<StoryPreview> result;
		await using (RpDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
		{
			IQueryable<RpChatRow> query = dbContext.Chats.AsNoTracking();
			if (!user.IsAdmin)
			{
				query = query.Where((RpChatRow chat) => chat.UserId == user.Id);
			}
			List<RpChatRow> chats = await (from x in query.AsNoTracking()
				orderby x.SortOrder, x.LastMessageUtc ?? x.UpdatedUtc descending
				select x).ToListAsync(cancellationToken);
			HashSet<string> chatIds = chats.Select((RpChatRow chat) => chat.Id).ToHashSet<string>(StringComparer.Ordinal);
			List<ChatLocationRow> locations = await (from row in dbContext.ChatLocations.AsNoTracking()
				where chatIds.Contains(row.ChatId)
				select row).ToListAsync(cancellationToken);
			List<ChatCurrentSceneCharacterRow> sceneCharacters = await (from row in dbContext.ChatCurrentSceneCharacters.AsNoTracking()
				where chatIds.Contains(row.ChatId)
				orderby row.SortOrder
				select row).ToListAsync(cancellationToken);
			List<ChatCharacterRow> characters = await (from row in dbContext.ChatCharacters.AsNoTracking()
				where chatIds.Contains(row.ChatId)
				select row).ToListAsync(cancellationToken);
			List<ImageAssetRow> images = await (from row in dbContext.ImageAssets.AsNoTracking()
				where chatIds.Contains(row.ChatId)
				select row).ToListAsync(cancellationToken);
			Dictionary<string, ChatLocationRow> locationsByKey = locations.ToDictionary<ChatLocationRow, string>((ChatLocationRow row) => row.ChatId + ":" + row.Id, StringComparer.Ordinal);
			Dictionary<string, ChatCharacterRow> charactersByKey = characters.ToDictionary<ChatCharacterRow, string>((ChatCharacterRow row) => row.ChatId + ":" + row.Id, StringComparer.Ordinal);
			Dictionary<string, ImageAssetRow> imagesByKey = images.ToDictionary<ImageAssetRow, string>((ImageAssetRow row) => row.ChatId + ":" + row.Id, StringComparer.Ordinal);
			result = chats.Select((RpChatRow chat) => BuildPreview(chat, sceneCharacters.Where((ChatCurrentSceneCharacterRow row) => row.ChatId == chat.Id), locationsByKey, charactersByKey, imagesByKey)).ToList();
		}
		return result;
	}

	public Task<List<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return AiProviderPersistenceStore.LoadAsync(dbContextFactory, cancellationToken);
	}

	public async Task<RpChatDocument> LoadChatDocumentAsync(CurrentAppUser user, string chatId, CancellationToken cancellationToken = default(CancellationToken))
	{
		RpChatDocument result;
		await using (RpDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
		{
			IQueryable<RpChatRow> query = from x in dbContext.Chats.AsNoTracking()
				where x.Id == chatId
				select x;
			if (!user.IsAdmin)
			{
				query = query.Where((RpChatRow x) => x.UserId == user.Id);
			}
			RpChatRow chat = await (from x in query.AsNoTracking()
				orderby x.Id
				select x).FirstOrDefaultAsync(cancellationToken);
			if (chat == null)
			{
				result = ChatDocumentPersistenceMapper.CreateEmpty(chatId, null);
			}
			else
			{
				RpChatRow chat2 = chat;
				ChatDocumentRows rows = new ChatDocumentRows(chat2, await (from x in dbContext.ChatCharacters.AsNoTracking()
					where x.ChatId == chatId
					orderby x.SortOrder
					select x).ToListAsync(cancellationToken), await (from x in dbContext.ChatCharacterRelationships.AsNoTracking()
					where x.ChatId == chatId
					orderby x.SortOrder
					select x).ToListAsync(cancellationToken), await (from x in dbContext.ChatLocations.AsNoTracking()
					where x.ChatId == chatId
					orderby x.SortOrder
					select x).ToListAsync(cancellationToken), await (from x in dbContext.ChatItems.AsNoTracking()
					where x.ChatId == chatId
					orderby x.SortOrder
					select x).ToListAsync(cancellationToken), await (from x in dbContext.ChatTimelineEntries.AsNoTracking()
					where x.ChatId == chatId
					orderby x.SortOrder
					select x).ToListAsync(cancellationToken), await (from x in dbContext.ImageAssets.AsNoTracking()
					where x.ChatId == chatId
					orderby x.SortOrder, x.CreatedUtc descending
					select x).ToListAsync(cancellationToken), await (from x in dbContext.ChatTranscriptStates.AsNoTracking()
					where x.ChatId == chatId
					orderby x.ChatId
					select x).FirstOrDefaultAsync(cancellationToken), await (from x in dbContext.ChatDirectionStates.AsNoTracking()
					where x.ChatId == chatId
					orderby x.ChatId
					select x).FirstOrDefaultAsync(cancellationToken), await (from x in dbContext.NarratorProfileStates.AsNoTracking()
					where x.ChatId == chatId
					orderby x.ChatId
					select x).FirstOrDefaultAsync(cancellationToken), await (from x in dbContext.CharacterTraitLibraryStates.AsNoTracking()
					where x.ChatId == chatId
					orderby x.ChatId
					select x).FirstOrDefaultAsync(cancellationToken), await (from x in dbContext.StoryModelSelections.AsNoTracking()
					where x.ChatId == chatId
					orderby x.Role
					select x).ToListAsync(cancellationToken));
				result = ChatDocumentPersistenceMapper.ToModel(rows, await TranscriptPersistenceStore.LoadActiveAsync(dbContext, chatId, rows.TranscriptState, chat.ActiveLeafTurnId, cancellationToken));
			}
		}
		return result;
	}

	public Task<RpTranscriptState> LoadActiveTranscriptAsync(string chatId, CancellationToken cancellationToken = default(CancellationToken))
	{
		return TranscriptPersistenceStore.LoadActiveAsync(dbContextFactory, chatId, cancellationToken);
	}

	public async Task SaveStoryPreviewsAsync(CurrentAppUser user, IReadOnlyList<StoryPreview> previews, CancellationToken cancellationToken = default(CancellationToken))
	{
		await using RpDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		IQueryable<RpChatRow> query = dbContext.Chats.AsQueryable();
		if (!user.IsAdmin)
		{
			query = query.Where((RpChatRow chat) => chat.UserId == user.Id);
		}
		Dictionary<string, RpChatRow> existing = await query.ToDictionaryAsync((RpChatRow x) => x.Id, cancellationToken);
		DateTime now = DateTime.UtcNow;
		for (int index = 0; index < previews.Count; index++)
		{
			StoryPreview preview = previews[index];
			if (existing.TryGetValue(preview.ChatId, out var row))
			{
				row.SortOrder = index;
				row.Title = preview.Title;
				row.Starred = preview.Starred;
				row.UpdatedUtc = now;
				row = null;
			}
		}
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	public Task SaveProvidersAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default(CancellationToken))
	{
		return AiProviderPersistenceStore.SaveAsync(dbContextFactory, providers, cancellationToken);
	}

	public async Task CreateChatDocumentAsync(CurrentAppUser user, RpChatDocument document, CancellationToken cancellationToken = default(CancellationToken))
	{
		await using RpDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		document.Chat.UserId = user.Id;
		await SaveDocumentAsync(dbContext, user, document, null, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	public Task SaveChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default(CancellationToken))
	{
		throw new NotSupportedException("Saving a chat document requires the current user.");
	}

	public async Task SaveChatAreaAsync(CurrentAppUser user, RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default(CancellationToken))
	{
		await using RpDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await SaveDocumentAsync(dbContext, user, document, area, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	private static StoryPreview BuildPreview(RpChatRow chat, IEnumerable<ChatCurrentSceneCharacterRow> sceneCharacters, IReadOnlyDictionary<string, ChatLocationRow> locationsByKey, IReadOnlyDictionary<string, ChatCharacterRow> charactersByKey, IReadOnlyDictionary<string, ImageAssetRow> imagesByKey)
	{
		locationsByKey.TryGetValue(chat.Id + ":" + chat.ActiveLocationId, out ChatLocationRow value);
		ImageAssetRow image = ((value == null) ? null : ImageById(imagesByKey, chat.Id, value.ImageId));
		List<StoryPreviewCharacter> characters = sceneCharacters.Select((ChatCurrentSceneCharacterRow row) => charactersByKey.TryGetValue(row.ChatId + ":" + row.CharacterId, out ChatCharacterRow value2) ? new StoryPreviewCharacter
		{
			CharacterId = value2.Id,
			Name = value2.Name,
			Avatar = ChatPersistenceMapper.ToPreviewAvatar(ImageById(imagesByKey, row.ChatId, value2.ImageId))
		} : null).OfType<StoryPreviewCharacter>().ToList();
		StoryPreviewLocation location = ((value == null && string.IsNullOrWhiteSpace(chat.ActiveLocationName)) ? null : new StoryPreviewLocation
		{
			LocationId = (value?.Id ?? chat.ActiveLocationId),
			Name = (value?.Name ?? chat.ActiveLocationName),
			Avatar = ChatPersistenceMapper.ToPreviewAvatar(image)
		});
		return ChatPersistenceMapper.ToPreview(chat, location, characters);
	}

	private static ImageAssetRow? ImageById(IReadOnlyDictionary<string, ImageAssetRow> imagesByKey, string chatId, string imageId)
	{
		ImageAssetRow value;
		return (string.IsNullOrWhiteSpace(imageId) || !imagesByKey.TryGetValue(chatId + ":" + imageId, out value)) ? null : value;
	}

	private static async Task SaveDocumentAsync(RpDbContext dbContext, CurrentAppUser user, RpChatDocument document, RoleplayStoreArea? area, CancellationToken cancellationToken)
	{
		DateTime now = DateTime.UtcNow;
		TranscriptProjector.Apply(document, now);
		EnsureStoryAccess(user, document);
		RpChatRow chat = await EnsureChatAsync(dbContext, user, document, now, cancellationToken);
		StoryPreview preview = StoryPreviewProjector.FromDocument(document);
		ApplyPreviewToChat(preview, chat);
		ChatPersistenceMapper.Apply(document.Chat, chat, chat.SortOrder, now);
		ChatPersistenceMapper.ApplyTranscriptPreview(document, chat);
		bool flag;
		if (area.HasValue)
		{
			RoleplayStoreArea valueOrDefault = area.GetValueOrDefault();
			if (valueOrDefault != RoleplayStoreArea.Characters)
			{
				flag = false;
				goto IL_020c;
			}
		}
		flag = true;
		goto IL_020c;
		IL_08c3:
		if (flag)
		{
			await SaveStateAsync(dbContext.CharacterTraitLibraryStates, document.Chat.Id, now, delegate(CharacterTraitLibraryStateRow row, DateTime timestamp)
			{
				ChatDocumentPersistenceMapper.Apply(document.CharacterTraitLibrary, row, timestamp);
			}, cancellationToken);
		}
		if (area.HasValue)
		{
			RoleplayStoreArea valueOrDefault = area.GetValueOrDefault();
			if (valueOrDefault != RoleplayStoreArea.ModelSelections)
			{
				flag = false;
				goto IL_0996;
			}
		}
		flag = true;
		goto IL_0996;
		IL_07f0:
		if (flag)
		{
			await SaveStateAsync(dbContext.NarratorProfileStates, document.Chat.Id, now, delegate(NarratorProfileStateRow row, DateTime timestamp)
			{
				ChatDocumentPersistenceMapper.Apply(document.NarratorProfile, row, timestamp);
			}, cancellationToken);
		}
		if (area.HasValue)
		{
			RoleplayStoreArea valueOrDefault = area.GetValueOrDefault();
			if (valueOrDefault != RoleplayStoreArea.CharacterTraitLibrary)
			{
				flag = false;
				goto IL_08c3;
			}
		}
		flag = true;
		goto IL_08c3;
		IL_049a:
		if (flag)
		{
			await SaveTimelineAsync(dbContext, document, now, cancellationToken);
		}
		if (area.HasValue)
		{
			RoleplayStoreArea valueOrDefault = area.GetValueOrDefault();
			if (valueOrDefault != RoleplayStoreArea.Images)
			{
				flag = false;
				goto IL_0548;
			}
		}
		flag = true;
		goto IL_0548;
		IL_020c:
		if (flag)
		{
			await SaveCharactersAsync(dbContext, document, now, cancellationToken);
			await SaveRelationshipsAsync(dbContext, document, now, cancellationToken);
		}
		if (area.HasValue)
		{
			RoleplayStoreArea valueOrDefault = area.GetValueOrDefault();
			if (valueOrDefault != RoleplayStoreArea.Locations)
			{
				flag = false;
				goto IL_033e;
			}
		}
		flag = true;
		goto IL_033e;
		IL_05f0:
		if (flag)
		{
			await SaveTranscriptStateAsync(dbContext, document, now, cancellationToken);
			await TranscriptPersistenceStore.SaveRowsAsync(dbContext, document, cancellationToken);
		}
		if (area.HasValue)
		{
			RoleplayStoreArea valueOrDefault = area.GetValueOrDefault();
			if (valueOrDefault != RoleplayStoreArea.ChatDirection)
			{
				flag = false;
				goto IL_071d;
			}
		}
		flag = true;
		goto IL_071d;
		IL_0548:
		if (flag)
		{
			await SaveImagesAsync(dbContext, document, cancellationToken);
		}
		if (area.HasValue)
		{
			RoleplayStoreArea valueOrDefault = area.GetValueOrDefault();
			if (valueOrDefault != RoleplayStoreArea.Transcript)
			{
				flag = false;
				goto IL_05f0;
			}
		}
		flag = true;
		goto IL_05f0;
		IL_0996:
		if (flag)
		{
			await SaveModelSelectionsAsync(dbContext, document, now, cancellationToken);
		}
		return;
		IL_03ec:
		if (flag)
		{
			await SaveItemsAsync(dbContext, document, now, cancellationToken);
		}
		if (area.HasValue)
		{
			RoleplayStoreArea valueOrDefault = area.GetValueOrDefault();
			if (valueOrDefault != RoleplayStoreArea.Timeline)
			{
				flag = false;
				goto IL_049a;
			}
		}
		flag = true;
		goto IL_049a;
		IL_071d:
		if (flag)
		{
			await SaveStateAsync(dbContext.ChatDirectionStates, document.Chat.Id, now, delegate(ChatDirectionStateRow row, DateTime timestamp)
			{
				ChatDocumentPersistenceMapper.Apply(document.ChatDirection, row, timestamp);
			}, cancellationToken);
		}
		if (area.HasValue)
		{
			RoleplayStoreArea valueOrDefault = area.GetValueOrDefault();
			if (valueOrDefault != RoleplayStoreArea.NarratorProfile)
			{
				flag = false;
				goto IL_07f0;
			}
		}
		flag = true;
		goto IL_07f0;
		IL_033e:
		if (flag)
		{
			await SaveLocationsAsync(dbContext, document, now, cancellationToken);
		}
		if (area.HasValue)
		{
			RoleplayStoreArea valueOrDefault = area.GetValueOrDefault();
			if (valueOrDefault != RoleplayStoreArea.Items)
			{
				flag = false;
				goto IL_03ec;
			}
		}
		flag = true;
		goto IL_03ec;
	}

	private static async Task<RpChatRow> EnsureChatAsync(RpDbContext dbContext, CurrentAppUser user, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
	{
		RpChatRow chat = await (from x in dbContext.Chats
			where x.Id == document.Chat.Id
			orderby x.Id
			select x).FirstOrDefaultAsync(cancellationToken);
		if (chat != null)
		{
			if (!user.IsAdmin && chat.UserId != user.Id)
			{
				throw new UnauthorizedAccessException("Saving story '" + document.Chat.Id + "' failed because it belongs to a different user.");
			}
			return chat;
		}
		RpChatRow rpChatRow = new RpChatRow
		{
			Id = document.Chat.Id,
			UserId = user.Id,
			CreatedUtc = now
		};
		RpChatRow rpChatRow2 = rpChatRow;
		rpChatRow2.SortOrder = await dbContext.Chats.Where((RpChatRow row) => row.UserId == user.Id).CountAsync(cancellationToken);
		chat = rpChatRow;
		dbContext.Chats.Add(chat);
		return chat;
	}

	private static void ApplyPreviewToChat(StoryPreview preview, RpChatRow row)
	{
		row.ActiveLocationId = preview.ActiveLocation?.LocationId ?? "";
		row.ActiveLocationName = preview.ActiveLocation?.Name ?? "";
		row.ActiveTurnCount = preview.VisibleTurnCount;
	}

	private static async Task SaveCharactersAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
	{
		SaveRows(existing: await dbContext.ChatCharacters.Where((ChatCharacterRow x) => x.ChatId == document.Chat.Id).ToDictionaryAsync((ChatCharacterRow x) => x.Id, cancellationToken), chatId: document.Chat.Id, models: document.Characters, rows: dbContext.ChatCharacters, now: now, apply: StoryEntityPersistenceMapper.Apply);
	}

	private static async Task SaveRelationshipsAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
	{
		SaveRows(existing: await dbContext.ChatCharacterRelationships.Where((ChatCharacterRelationshipRow x) => x.ChatId == document.Chat.Id).ToDictionaryAsync((ChatCharacterRelationshipRow x) => x.Id, cancellationToken), chatId: document.Chat.Id, models: document.CharacterRelationships, rows: dbContext.ChatCharacterRelationships, now: now, apply: StoryEntityPersistenceMapper.Apply);
	}

	private static async Task SaveLocationsAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
	{
		SaveRows(existing: await dbContext.ChatLocations.Where((ChatLocationRow x) => x.ChatId == document.Chat.Id).ToDictionaryAsync((ChatLocationRow x) => x.Id, cancellationToken), chatId: document.Chat.Id, models: document.Locations, rows: dbContext.ChatLocations, now: now, apply: StoryEntityPersistenceMapper.Apply);
	}

	private static async Task SaveItemsAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
	{
		SaveRows(existing: await dbContext.ChatItems.Where((ChatItemRow x) => x.ChatId == document.Chat.Id).ToDictionaryAsync((ChatItemRow x) => x.Id, cancellationToken), chatId: document.Chat.Id, models: document.Items, rows: dbContext.ChatItems, now: now, apply: StoryEntityPersistenceMapper.Apply);
	}

	private static async Task SaveTimelineAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
	{
		SaveRows(existing: await dbContext.ChatTimelineEntries.Where((ChatTimelineEntryRow x) => x.ChatId == document.Chat.Id).ToDictionaryAsync((ChatTimelineEntryRow x) => x.Id, cancellationToken), chatId: document.Chat.Id, models: document.Timeline, rows: dbContext.ChatTimelineEntries, now: now, apply: StoryEntityPersistenceMapper.Apply);
	}

	private static async Task SaveImagesAsync(RpDbContext dbContext, RpChatDocument document, CancellationToken cancellationToken)
	{
		Dictionary<string, ImageAssetRow> existing = await dbContext.ImageAssets.Where((ImageAssetRow x) => x.ChatId == document.Chat.Id).ToDictionaryAsync((ImageAssetRow x) => x.Id, cancellationToken);
		HashSet<string> desiredIds = document.Images.Select((GalleryImage galleryImage) => galleryImage.Id).ToHashSet<string>(StringComparer.Ordinal);
		dbContext.ImageAssets.RemoveRange(existing.Values.Where((ImageAssetRow imageAssetRow) => !desiredIds.Contains(imageAssetRow.Id)));
		for (int index = 0; index < document.Images.Count; index++)
		{
			GalleryImage image = document.Images[index];
			if (existing.TryGetValue(image.Id, out var row))
			{
				StoryEntityPersistenceMapper.Apply(image, row, index);
				row.UserId = document.Chat.UserId;
				row = null;
			}
		}
	}

	private static async Task SaveTranscriptStateAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
	{
		ChatTranscriptStateRow row = await (from x in dbContext.ChatTranscriptStates
			where x.ChatId == document.Chat.Id
			orderby x.ChatId
			select x).FirstOrDefaultAsync(cancellationToken);
		if (row == null)
		{
			row = new ChatTranscriptStateRow
			{
				ChatId = document.Chat.Id,
				CreatedUtc = now
			};
			dbContext.ChatTranscriptStates.Add(row);
		}
		ChatDocumentPersistenceMapper.ApplyTranscriptShell(document.Transcript, row, now);
	}

	private static async Task SaveModelSelectionsAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
	{
		Dictionary<string, StoryModelSelectionRow> existing = await dbContext.StoryModelSelections.Where((StoryModelSelectionRow storyModelSelectionRow) => storyModelSelectionRow.ChatId == document.Chat.Id).ToDictionaryAsync((StoryModelSelectionRow storyModelSelectionRow) => storyModelSelectionRow.Role, cancellationToken);
		HashSet<string> desiredRoles = document.ModelSelections.Values.Keys.Select((AiModelRole aiModelRole) => aiModelRole.ToString()).ToHashSet<string>(StringComparer.Ordinal);
		dbContext.StoryModelSelections.RemoveRange(existing.Values.Where((StoryModelSelectionRow storyModelSelectionRow) => !desiredRoles.Contains(storyModelSelectionRow.Role)));
		foreach (KeyValuePair<AiModelRole, ActiveModelSelectionState> pair in document.ModelSelections.Values)
		{
			string role = pair.Key.ToString();
			if (!existing.TryGetValue(role, out var row))
			{
				row = new StoryModelSelectionRow
				{
					ChatId = document.Chat.Id,
					Role = role,
					CreatedUtc = now
				};
				dbContext.StoryModelSelections.Add(row);
			}
			row.ProviderId = pair.Value.ProviderId;
			row.ModelId = pair.Value.ModelId;
			row.UpdatedUtc = now;
			row = null;
		}
	}

	private static void EnsureStoryAccess(CurrentAppUser user, RpChatDocument document)
	{
		if (document.Chat.UserId == Guid.Empty)
		{
			document.Chat.UserId = user.Id;
		}
		if (user.IsAdmin || document.Chat.UserId == user.Id)
		{
			return;
		}
		throw new UnauthorizedAccessException("Saving story '" + document.Chat.Id + "' failed because it belongs to a different user.");
	}

	private static async Task SaveStateAsync<TRow>(DbSet<TRow> rows, string chatId, DateTime now, Action<TRow, DateTime> apply, CancellationToken cancellationToken) where TRow : class, new()
	{
		TRow row = await (from x in rows
			where EF.Property<string>(x, "ChatId") == chatId
			orderby EF.Property<string>(x, "ChatId")
			select x).FirstOrDefaultAsync(cancellationToken);
		if (row == null)
		{
			row = new TRow();
			typeof(TRow).GetProperty("ChatId")?.SetValue(row, chatId);
			typeof(TRow).GetProperty("CreatedUtc")?.SetValue(row, now);
			rows.Add(row);
		}
		apply(row, now);
	}

	private static void SaveRows<TModel, TRow>(string chatId, IReadOnlyList<TModel> models, Dictionary<string, TRow> existing, DbSet<TRow> rows, DateTime now, Action<TModel, TRow, int, DateTime> apply) where TRow : class, new()
	{
		HashSet<string> desiredIds = models.Select(GetId).ToHashSet<string>(StringComparer.Ordinal);
		rows.RemoveRange(from pair in existing
			where !desiredIds.Contains(pair.Key)
			select pair.Value);
		for (int num = 0; num < models.Count; num++)
		{
			TModel val = models[num];
			string id = GetId(val);
			if (!existing.TryGetValue(id, out TRow value))
			{
				value = new TRow();
				typeof(TRow).GetProperty("ChatId")?.SetValue(value, chatId);
				typeof(TRow).GetProperty("Id")?.SetValue(value, id);
				typeof(TRow).GetProperty("CreatedUtc")?.SetValue(value, now);
				rows.Add(value);
			}
			apply(val, value, num, now);
		}
	}

	private static string GetId<TModel>(TModel model)
	{
		return ((string)typeof(TModel).GetProperty("Id")?.GetValue(model)) ?? "";
	}
}
