using AgentRp.Models;
using Microsoft.Extensions.Logging;

namespace AgentRp.Services;

public interface IAiProviderVoiceInventoryService
{
    bool IsRefreshing(AiProvider provider, AiProviderModel model);
    bool NeedsInitialRefresh(AiProviderModel model);
    Task<AiProviderVoiceRefreshResult> RefreshModelAsync(AiProvider provider, AiProviderModel model, CancellationToken cancellationToken = default);
}

public sealed class AiProviderVoiceInventoryService(
    IAiProviderVoiceDiscoveryService voiceDiscoveryService,
    ILogger<AiProviderVoiceInventoryService>? logger = null) : IAiProviderVoiceInventoryService
{
    readonly object gate = new();
    readonly HashSet<string> refreshingKeys = new(StringComparer.Ordinal);

    public bool IsRefreshing(AiProvider provider, AiProviderModel model)
    {
        lock (gate)
            return refreshingKeys.Contains(Key(provider, model));
    }

    public bool NeedsInitialRefresh(AiProviderModel model) =>
        AiProviderModelSelectionRules.IsSelectedForVoice(model)
        && model.Voices.Count == 0
        && model.LastVoiceRefreshUtc is null
        && string.IsNullOrWhiteSpace(model.LastVoiceRefreshError);

    public async Task<AiProviderVoiceRefreshResult> RefreshModelAsync(
        AiProvider provider,
        AiProviderModel model,
        CancellationToken cancellationToken = default)
    {
        var key = Key(provider, model);
        lock (gate)
        {
            if (!refreshingKeys.Add(key))
                return new(model.Id, DisplayName(model), true, "");
        }

        try
        {
            model.Voices = (await voiceDiscoveryService.RefreshVoicesAsync(provider, model, cancellationToken)).ToList();
            model.LastVoiceRefreshUtc = DateTime.UtcNow;
            model.LastVoiceRefreshError = "";
            return new(model.Id, DisplayName(model), true, "");
        }
        catch (Exception exception)
        {
            model.LastVoiceRefreshError = UserFacingErrorReporter.Capture(
                logger,
                exception,
                $"Refreshing voices for {DisplayName(model)} failed.",
                "Refreshing voices for provider {ProviderId} model {ModelId} failed.",
                provider.Id,
                model.Id);
            return new(model.Id, DisplayName(model), false, model.LastVoiceRefreshError);
        }
        finally
        {
            lock (gate)
                refreshingKeys.Remove(key);
        }
    }

    static string Key(AiProvider provider, AiProviderModel model) => $"{provider.Id}::{model.Id}";

    static string DisplayName(AiProviderModel model) =>
        string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName;
}

public sealed record AiProviderVoiceRefreshResult(string ModelId, string ModelName, bool Succeeded, string Error);
