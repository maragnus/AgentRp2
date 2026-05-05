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
    public bool Streaming { get; init; }
    public bool StructuredOutput { get; init; }
    public bool Tools { get; init; }
    public string ImageGenerationModel { get; init; } = "";
    public string Source { get; init; } = "fallback";
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public bool CanGenerateText => TextInput && TextOutput;
    public bool CanGenerateStructuredText => CanGenerateText && StructuredOutput;
    public bool CanGenerateStreamingText => CanGenerateText && Streaming;
    public bool CanGenerateImage => TextInput && ImageOutput;

    public static ModelGenerationCapabilities Fallback { get; } = new()
    {
        TextInput = true,
        TextOutput = true,
        Source = "fallback",
        Guidance = "No capability record was found. Text is allowed, but tuning, structured output, streaming, tools, and image generation stay disabled until capabilities are provided."
    };
}

public sealed record ActiveTextModel(AiProvider Provider, AiProviderModel Model, ModelGenerationCapabilities Capabilities);

public sealed record ResponseTuningOptions(
    float? Temperature,
    float? TopP,
    int? MaxOutputTokenCount);

public static class TextModelTuningCatalog
{
    public static ActiveTextModel? TryResolveActiveTextModel(IReadOnlyList<AiProvider> providers)
    {
        var enabled = providers.Where(provider => provider.Enabled).ToList();
        foreach (var provider in enabled)
        {
            foreach (var model in provider.Models.Where(IsActiveTextCandidate))
                return new(provider, model, model.Capabilities);
        }

        foreach (var provider in enabled)
        {
            foreach (var model in provider.Models.Where(IsTextCandidate))
                return new(provider, model, model.Capabilities);
        }

        return null;
    }

    static bool IsActiveTextCandidate(AiProviderModel model) =>
        model.ActiveText && IsTextCandidate(model);

    static bool IsTextCandidate(AiProviderModel model) =>
        AiProviderModelSelectionRules.IsSelectedForChat(model);

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
