using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgentRp.Models;
using AgentRp.Services;
using Microsoft.Extensions.Logging;
using AgentRp.UserSystem;

namespace AgentRp.Session;

public sealed class ProviderStore(Guid sessionId, ILiveRoleplayStore liveStore, CurrentAppUser user, IAiProviderCapabilityPipeline? capabilityPipeline = null, IAiProviderWidgetService? widgetService = null, ILogger<ProviderStore>? logger = null) : StoreBase
{
	private readonly List<AiProvider> _items = new List<AiProvider>();

	private readonly HashSet<string> _widgetLoadAttempts = new HashSet<string>(StringComparer.Ordinal);

	private readonly IAiProviderCapabilityPipeline _capabilityPipeline = capabilityPipeline ?? new AiProviderCapabilityPipeline(NullModelCapabilityCatalog.Instance);

	private readonly IAiProviderWidgetService _widgetService = widgetService ?? NullAiProviderWidgetService.Instance;

	public IReadOnlyList<AiProvider> Items => _items;

	public ModelSelectionStore? ModelSelection { get; set; }

	public async Task LoadAsync()
	{
		await RefreshAsync();
	}

	public async Task RefreshAsync()
	{
		_items.Clear();
		List<AiProvider> items = _items;
		items.AddRange((await liveStore.LoadProvidersAsync()).Select(SessionCloner.Clone));
		NormalizeProviders();
		if (ModelSelection != null)
		{
			await ModelSelection.EnsureValidAsync();
		}
		await NotifyChangedAsync();
	}

	public async Task AddAsync(AiProvider provider)
	{
		_capabilityPipeline.Normalize(provider);
		_items.Add(provider);
		await MarkChangedAsync();
	}

	public async Task DeleteAsync(string id)
	{
		_items.RemoveAll(provider => provider.Id == id);
		await MarkChangedAsync();
	}

	public async Task SetModelsAsync(AiProvider provider, bool enabled)
	{
		foreach (AiProviderModel model in provider.Models)
		{
			if (enabled)
			{
				AiProviderModelSelectionRules.SelectAvailableRoles(model);
			}
			else
			{
				AiProviderModelSelectionRules.ClearSelectedRoles(model);
			}
		}
		await MarkChangedAsync();
	}

	public async Task EnsureWidgetLoadedAsync(string providerId)
	{
		if (_widgetLoadAttempts.Add(providerId))
		{
			await RefreshWidgetAsync(providerId);
		}
	}

	public async Task RefreshWidgetAsync(string providerId)
	{
		var provider = _items.FirstOrDefault(aiProvider2 => aiProvider2.Id == providerId);
		if (provider != null)
		{
			try
			{
				var aiProvider = provider;
				aiProvider.Metrics = (await _widgetService.RefreshMetricsAsync(provider)).ToList();
				provider.LastMetricsRefreshUtc = DateTime.UtcNow;
				provider.LastMetricsError = "";
			}
			catch (Exception ex)
			{
				provider.LastMetricsError = UserFacingErrorReporter.Capture(logger, ex, "Refreshing widget details for " + provider.Name + " failed.", "Refreshing widget details for provider {ProviderId} failed.", provider.Id);
			}
			await MarkChangedAsync();
		}
	}

	public async Task MarkChangedAsync()
	{
		NormalizeProviders();
		await liveStore.ReplaceProvidersAsync(user, sessionId, _items);
		if (ModelSelection != null)
		{
			await ModelSelection.EnsureValidAsync();
		}
		await NotifyChangedAsync();
	}

	private void NormalizeProviders()
	{
		_capabilityPipeline.Normalize(_items);
	}
}
