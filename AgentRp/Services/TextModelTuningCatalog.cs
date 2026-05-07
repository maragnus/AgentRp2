using System.Globalization;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public enum TuningSupport
{
    Supported,
    DefaultOnly,
    Unsupported
}

public class ModelTuningCapabilities
{
    public TuningSupport Temperature { get; init; } = TuningSupport.Unsupported;
    public TuningSupport TopP { get; init; } = TuningSupport.Unsupported;
    public TuningSupport MaxTokens { get; init; } = TuningSupport.Unsupported;
    public TuningSupport Seed { get; init; } = TuningSupport.Unsupported;
    public TuningSupport FrequencyPenalty { get; init; } = TuningSupport.Unsupported;
    public TuningSupport PresencePenalty { get; init; } = TuningSupport.Unsupported;
    public TuningSupport StopSequences { get; init; } = TuningSupport.Unsupported;
    public bool SupportsReasoningEffort { get; init; }
    public bool SupportsVerbosity { get; init; }
    public string Guidance { get; init; } = "";
}

public sealed class ModelGenerationCapabilities : ModelTuningCapabilities
{
    public bool TextInput { get; init; } = true;
    public bool ImageInput { get; init; }
    public bool TextOutput { get; init; } = true;
    public bool ImageOutput { get; init; }
    public bool SpeechOutput { get; init; }
    public bool Streaming { get; init; } = true;
    public bool StructuredOutput { get; init; }
    public bool Tools { get; init; }
    public string ImageGenerationModel { get; init; } = "";
    public bool ImageInputFidelity { get; init; } = true;
    public string Source { get; init; } = "fallback";
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public bool CanGenerateText => TextInput && TextOutput;
    public bool CanGenerateStructuredText => CanGenerateText && StructuredOutput;
    public bool CanGenerateStreamingText => CanGenerateText;
    public bool CanGenerateImage => TextInput && ImageOutput;
    public bool CanGenerateSpeech => TextInput && SpeechOutput;

    public static ModelGenerationCapabilities Fallback { get; } = new()
    {
        TextInput = true,
        TextOutput = true,
        Streaming = true,
        StructuredOutput = true,
        Source = "fallback",
        Guidance = "No capability record was found. AgentRp assumes roleplay-ready text, streaming, and structured output support unless the user disables structured output."
    };
}

public sealed record ActiveModelSelection(AiProvider Provider, AiProviderModel Model, ModelGenerationCapabilities Capabilities, AiModelRole Role)
{
    public string Key => ModelSelectionKey.Build(Provider.Id, Model.Id);
}

public sealed record ResponseTuningOptions(
    float? Temperature,
    float? TopP,
    int? MaxOutputTokenCount);

public static class TextModelTuningCatalog
{
    public static ActiveModelSelection? TryResolveActiveTextModel(
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState? selections = null) =>
        TryResolveActiveModel(providers, AiModelRole.Chat, selections);

    public static ActiveModelSelection? TryResolveActiveReasoningModel(
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState? selections = null) =>
        TryResolveExplicitActiveModel(providers, AiModelRole.Reasoning, selections);

    public static ActiveModelSelection? TryResolveActiveModel(
        IReadOnlyList<AiProvider> providers,
        AiModelRole role,
        ActiveModelSelectionsState? selections = null)
    {
        var enabled = providers.Where(provider => provider.Enabled).ToList();
        if (selections?.Values.TryGetValue(role, out var selected) == true
            && !string.IsNullOrWhiteSpace(selected.ProviderId)
            && !string.IsNullOrWhiteSpace(selected.ModelId))
        {
            var provider = enabled.FirstOrDefault(provider => provider.Id == selected.ProviderId);
            var model = provider?.Models.FirstOrDefault(model => model.Id == selected.ModelId);
            if (provider is not null && model is not null && AiProviderModelSelectionRules.IsSelectedForRole(model, role))
                return new(provider, model, model.Capabilities, role);
        }

        foreach (var provider in enabled)
        {
            foreach (var model in provider.Models.Where(model => AiProviderModelSelectionRules.IsSelectedForRole(model, role)))
                return new(provider, model, model.Capabilities, role);
        }

        return null;
    }

    static ActiveModelSelection? TryResolveExplicitActiveModel(
        IReadOnlyList<AiProvider> providers,
        AiModelRole role,
        ActiveModelSelectionsState? selections)
    {
        var enabled = providers.Where(provider => provider.Enabled).ToList();
        if (selections is null || !selections.Values.TryGetValue(role, out var selected))
            return null;

        if (string.IsNullOrWhiteSpace(selected.ProviderId) || string.IsNullOrWhiteSpace(selected.ModelId))
            return null;

        var provider = enabled.FirstOrDefault(provider => provider.Id == selected.ProviderId);
        var model = provider?.Models.FirstOrDefault(model => model.Id == selected.ModelId);
        if (provider is null || model is null || !AiProviderModelSelectionRules.IsSelectedForRole(model, role))
            return null;

        return new(provider, model, model.Capabilities, role);
    }

    public static ResponseTuningOptions Filter(ModelTuningStepState tuning, ModelGenerationCapabilities capabilities) => new(
        FilterTemperature(tuning.Temperature, capabilities.Temperature),
        capabilities.TopP == TuningSupport.Supported && TryParseFloat(tuning.TopP, out var topP) ? topP : null,
        capabilities.MaxTokens == TuningSupport.Supported ? ParsePositiveInt(tuning.MaxTokens) : null);

    static float? FilterTemperature(double? value, TuningSupport support)
    {
        if (support == TuningSupport.Unsupported || value is not double temperature)
            return null;

        if (support == TuningSupport.DefaultOnly && !IsDefaultTemperature(temperature))
            return null;

        return (float)temperature;
    }

    static bool IsDefaultTemperature(double value) => Math.Abs(value - 1d) < 0.000001d;

    static int? ParsePositiveInt(string value) => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;

    static bool TryParseFloat(string value, out float parsed) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
}

public static class ModelSelectionKey
{
    public static string Build(string providerId, string modelId) => $"{providerId}::{modelId}";
}
