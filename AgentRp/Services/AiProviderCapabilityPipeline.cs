using AgentRp.Models;

namespace AgentRp.Services;

public interface IAiProviderCapabilityPipeline
{
    void Normalize(AiProvider provider);
    void Normalize(IEnumerable<AiProvider> providers);
}

public sealed class AiProviderCapabilityPipeline(IModelCapabilityCatalog capabilityCatalog) : IAiProviderCapabilityPipeline
{
    public void Normalize(IEnumerable<AiProvider> providers)
    {
        foreach (var provider in providers)
            Normalize(provider);
    }

    public void Normalize(AiProvider provider)
    {
        capabilityCatalog.ApplyResolvedCapabilities(provider);
        AiProviderModelIdentityRules.EnsureProviderManagedVoiceModels(provider, capabilityCatalog);
        capabilityCatalog.ApplyResolvedCapabilities(provider);

        foreach (var model in provider.Models)
            AiProviderModelSelectionRules.SynchronizeEnabled(model);
    }
}

