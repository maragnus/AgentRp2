using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public enum TuningSupport
{
    Supported,
    DefaultOnly,
    Unsupported
}

public sealed class ModelTuningCapabilities
{
    public TuningSupport Temperature { get; init; } = TuningSupport.Supported;
    public TuningSupport TopP { get; init; } = TuningSupport.Supported;
    public TuningSupport MaxTokens { get; init; } = TuningSupport.Supported;
    public TuningSupport Seed { get; init; } = TuningSupport.Supported;
    public TuningSupport FrequencyPenalty { get; init; } = TuningSupport.Supported;
    public TuningSupport PresencePenalty { get; init; } = TuningSupport.Supported;
    public TuningSupport StopSequences { get; init; } = TuningSupport.Supported;
    public bool SupportsReasoningEffort { get; init; }
    public bool SupportsVerbosity { get; init; }
    public string Guidance { get; init; } = "";
}

public sealed record ActiveTextModel(AiProvider Provider, AiProviderModel Model);

public static class TextModelTuningCatalog
{
    static readonly ModelTuningCapabilities DefaultCapabilities = new();
    static readonly ModelTuningCapabilities OpenAiGpt55Capabilities = new()
    {
        Temperature = TuningSupport.DefaultOnly,
        SupportsReasoningEffort = true,
        SupportsVerbosity = true,
        Guidance = "OpenAI's current GPT-5.5 guidance focuses on reasoning effort and verbosity. Temperature only accepts the model default."
    };

    public static ActiveTextModel? TryResolveActiveTextModel(IReadOnlyList<AiProvider> providers)
    {
        foreach (var provider in providers.Where(provider => provider.Enabled))
        {
            foreach (var model in provider.Models.Where(model => model.Enabled && model.Text))
                return new(provider, model);
        }

        return null;
    }

    public static ModelTuningCapabilities Resolve(AiProvider provider, AiProviderModel model) => Resolve(provider.Type, model.Id);

    public static ModelTuningCapabilities Resolve(string providerType, string modelId)
    {
        if (providerType == "openai" && IsOpenAiGpt55Model(modelId))
            return OpenAiGpt55Capabilities;

        return DefaultCapabilities;
    }

    public static void Apply(JsonObject body, string providerType, string modelId, ModelTuningStepState tuning)
    {
        var capabilities = Resolve(providerType, modelId);

        if (capabilities.Temperature != TuningSupport.Unsupported && tuning.Temperature is double temperature)
        {
            if (capabilities.Temperature == TuningSupport.Supported || IsDefaultTemperature(temperature))
                body["temperature"] = temperature;
        }

        if (capabilities.TopP == TuningSupport.Supported && TryParseDouble(tuning.TopP, out var topP))
            body["top_p"] = topP;
        if (capabilities.MaxTokens == TuningSupport.Supported && ParsePositiveInt(tuning.MaxTokens) is int maxTokens)
            body["max_tokens"] = maxTokens;
        if (capabilities.Seed == TuningSupport.Supported && ParsePositiveInt(tuning.Seed) is int seed)
            body["seed"] = seed;
        if (capabilities.FrequencyPenalty == TuningSupport.Supported && TryParseDouble(tuning.FrequencyPenalty, out var frequencyPenalty))
            body["frequency_penalty"] = frequencyPenalty;
        if (capabilities.PresencePenalty == TuningSupport.Supported && TryParseDouble(tuning.PresencePenalty, out var presencePenalty))
            body["presence_penalty"] = presencePenalty;

        if (capabilities.StopSequences != TuningSupport.Unsupported)
        {
            var stops = tuning.StopSequences
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (stops.Length > 0)
                body["stop"] = new JsonArray(stops.Select(stop => (JsonNode)stop).ToArray());
        }
    }

    static bool IsOpenAiGpt55Model(string modelId) => modelId.StartsWith("gpt-5.5", StringComparison.OrdinalIgnoreCase);

    static bool IsDefaultTemperature(double value) => Math.Abs(value - 1d) < 0.000001d;

    static int? ParsePositiveInt(string value) => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;

    static bool TryParseDouble(string value, out double parsed) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed);
}
