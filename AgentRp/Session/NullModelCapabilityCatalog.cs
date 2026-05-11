using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

internal sealed class NullModelCapabilityCatalog : IModelCapabilityCatalog
{
	public static NullModelCapabilityCatalog Instance { get; } = new NullModelCapabilityCatalog();

	public string UserCatalogPath => "";

	public ModelGenerationCapabilities Resolve(AiProvider provider, AiProviderModel model)
	{
		return model.Capabilities;
	}

	public ModelGenerationCapabilities Resolve(string providerType, string modelId)
	{
		return ModelGenerationCapabilities.Fallback;
	}

	public void ApplyResolvedCapabilities(AiProvider provider)
	{
	}

	public void SaveUserCapabilities(string providerType, string modelId, ModelGenerationCapabilities capabilities)
	{
	}

	public void UpdateLiveGrokCapabilities(JsonNode languageModelsJson)
	{
	}
}
