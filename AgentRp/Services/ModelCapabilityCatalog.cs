using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Serialization;
using Microsoft.AspNetCore.Hosting;

namespace AgentRp.Services;

public interface IModelCapabilityCatalog
{
    ModelGenerationCapabilities Resolve(AiProvider provider, AiProviderModel model);
    ModelGenerationCapabilities Resolve(string providerType, string modelId);
    void ApplyResolvedCapabilities(AiProvider provider);
    void SaveUserCapabilities(string providerType, string modelId, ModelGenerationCapabilities capabilities);
    void UpdateLiveGrokCapabilities(JsonNode languageModelsJson);
    string UserCatalogPath { get; }
}

public sealed class ModelCapabilityCatalog : IModelCapabilityCatalog
{
    const string DefaultCatalogFileName = "model-capabilities.default.json";
    const string UserCatalogFileName = "model-capabilities.user.json";
    readonly object gate = new();
    readonly IWebHostEnvironment environment;
    Dictionary<ModelCapabilityKey, CapabilityRecord> live = [];
    Dictionary<ModelCapabilityKey, CapabilityRecord> user = [];
    Dictionary<ModelCapabilityKey, CapabilityRecord> shipped = [];
    Dictionary<ModelCapabilityKey, ModelCapabilityKey> aliases = [];

    public ModelCapabilityCatalog(IWebHostEnvironment environment)
        : this(environment, null)
    {
    }

    public ModelCapabilityCatalog(IWebHostEnvironment environment, string? userCatalogPath)
    {
        this.environment = environment;
        UserCatalogPath = userCatalogPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AgentRp2",
            UserCatalogFileName);
        LoadCatalogs();
    }

    public string UserCatalogPath { get; }

    public ModelGenerationCapabilities Resolve(AiProvider provider, AiProviderModel model) => Resolve(provider.Type, model.Id);

    public ModelGenerationCapabilities Resolve(string providerType, string modelId)
    {
        var normalized = NormalizeKey(providerType, modelId);
        lock (gate)
        {
            var key = ResolveAlias(normalized);
            var merged = new CapabilityRecord
            {
                Provider = providerType,
                Id = modelId,
                TextInput = true,
                TextOutput = true,
                Tools = DefaultsToTools(providerType),
                Source = "fallback",
                Guidance = ModelGenerationCapabilities.Fallback.Guidance
            };

            Overlay(merged, shipped.GetValueOrDefault(key), "default");
            Overlay(merged, live.GetValueOrDefault(key), "live");
            Overlay(merged, user.GetValueOrDefault(key), "user");
            return ToCapabilities(merged);
        }
    }

    public void ApplyResolvedCapabilities(AiProvider provider)
    {
        foreach (var model in provider.Models)
            model.Capabilities = Resolve(provider, model);
    }

    public void SaveUserCapabilities(string providerType, string modelId, ModelGenerationCapabilities capabilities)
    {
        var key = NormalizeKey(providerType, modelId);
        var record = new CapabilityRecord
        {
            Provider = providerType,
            Id = modelId,
            TextInput = capabilities.TextInput,
            ImageInput = capabilities.ImageInput,
            TextOutput = capabilities.TextOutput,
            ImageOutput = capabilities.ImageOutput,
            Streaming = capabilities.Streaming,
            StructuredOutput = capabilities.StructuredOutput,
            Tools = capabilities.Tools,
            ImageGenerationModel = capabilities.ImageGenerationModel,
            Temperature = capabilities.Temperature,
            TopP = capabilities.TopP,
            MaxTokens = capabilities.MaxTokens,
            Seed = capabilities.Seed,
            FrequencyPenalty = capabilities.FrequencyPenalty,
            PresencePenalty = capabilities.PresencePenalty,
            StopSequences = capabilities.StopSequences,
            SupportsReasoningEffort = capabilities.SupportsReasoningEffort,
            SupportsVerbosity = capabilities.SupportsVerbosity,
            Guidance = "Configured in AgentRp.",
            Source = "user",
            Aliases = capabilities.Aliases.ToList()
        };

        lock (gate)
        {
            user[key] = record;
            WriteUserCatalog();
            RebuildAliases();
        }
    }

    public void UpdateLiveGrokCapabilities(JsonNode languageModelsJson)
    {
        var records = new Dictionary<ModelCapabilityKey, CapabilityRecord>();
        foreach (var node in languageModelsJson["models"]?.AsArray() ?? [])
        {
            var id = node?["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var input = ReadStringSet(node?["input_modalities"]);
            var output = ReadStringSet(node?["output_modalities"]);
            var aliases = node?["aliases"]?.AsArray()
                .Select(alias => alias?.GetValue<string>())
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Select(alias => alias!)
                .ToList()
                ?? [];

            var record = new CapabilityRecord
            {
                Provider = "grok",
                Id = id,
                TextInput = input.Contains("text"),
                ImageInput = input.Contains("image"),
                TextOutput = output.Contains("text"),
                ImageOutput = output.Contains("image"),
                Streaming = output.Contains("text"),
                StructuredOutput = output.Contains("text"),
                Tools = true,
                Aliases = aliases,
                Source = "live",
                Guidance = "Resolved from xAI /v1/language-models."
            };
            records[NormalizeKey("grok", id)] = record;
        }

        lock (gate)
        {
            live = records;
            RebuildAliases();
        }
    }

    void LoadCatalogs()
    {
        var defaultPath = Path.Combine(environment.WebRootPath ?? environment.ContentRootPath, DefaultCatalogFileName);
        lock (gate)
        {
            shipped = LoadCatalog(defaultPath);
            user = LoadCatalog(UserCatalogPath);
            RebuildAliases();
        }
    }

    static Dictionary<ModelCapabilityKey, CapabilityRecord> LoadCatalog(string path)
    {
        if (!File.Exists(path))
            return [];

        try
        {
            var file = JsonSerializer.Deserialize<CapabilityFile>(File.ReadAllText(path), AppJsonSerializerOptions.IndentedWeb);
            var records = new Dictionary<ModelCapabilityKey, CapabilityRecord>();
            foreach (var model in file?.Models ?? [])
            {
                if (string.IsNullOrWhiteSpace(model.Provider) || string.IsNullOrWhiteSpace(model.Id))
                    continue;

                records[NormalizeKey(model.Provider, model.Id)] = model;
            }

            return records;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    void WriteUserCatalog()
    {
        var directory = Path.GetDirectoryName(UserCatalogPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var file = new CapabilityFile
        {
            Models = user.Values
                .OrderBy(model => model.Provider, StringComparer.OrdinalIgnoreCase)
                .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
        File.WriteAllText(UserCatalogPath, JsonSerializer.Serialize(file, AppJsonSerializerOptions.IndentedWeb));
    }

    void RebuildAliases()
    {
        aliases = [];
        foreach (var source in new[] { shipped, live, user })
        {
            foreach (var pair in source)
            {
                foreach (var alias in pair.Value.Aliases ?? [])
                    aliases[NormalizeKey(pair.Value.Provider ?? pair.Key.Provider, alias)] = pair.Key;
            }
        }
    }

    ModelCapabilityKey ResolveAlias(ModelCapabilityKey key) => aliases.GetValueOrDefault(key, key);

    static void Overlay(CapabilityRecord target, CapabilityRecord? source, string sourceName)
    {
        if (source is null)
            return;

        target.Provider = source.Provider ?? target.Provider;
        target.Id = source.Id ?? target.Id;
        target.TextInput = source.TextInput ?? target.TextInput;
        target.ImageInput = source.ImageInput ?? target.ImageInput;
        target.TextOutput = source.TextOutput ?? target.TextOutput;
        target.ImageOutput = source.ImageOutput ?? target.ImageOutput;
        target.Streaming = source.Streaming ?? target.Streaming;
        target.StructuredOutput = source.StructuredOutput ?? target.StructuredOutput;
        target.Tools = source.Tools ?? target.Tools;
        target.ImageGenerationModel = string.IsNullOrWhiteSpace(source.ImageGenerationModel) ? target.ImageGenerationModel : source.ImageGenerationModel;
        target.Temperature = source.Temperature ?? target.Temperature;
        target.TopP = source.TopP ?? target.TopP;
        target.MaxTokens = source.MaxTokens ?? target.MaxTokens;
        target.Seed = source.Seed ?? target.Seed;
        target.FrequencyPenalty = source.FrequencyPenalty ?? target.FrequencyPenalty;
        target.PresencePenalty = source.PresencePenalty ?? target.PresencePenalty;
        target.StopSequences = source.StopSequences ?? target.StopSequences;
        target.SupportsReasoningEffort = source.SupportsReasoningEffort ?? target.SupportsReasoningEffort;
        target.SupportsVerbosity = source.SupportsVerbosity ?? target.SupportsVerbosity;
        target.Guidance = string.IsNullOrWhiteSpace(source.Guidance) ? target.Guidance : source.Guidance;
        target.Aliases = source.Aliases is { Count: > 0 } ? source.Aliases : target.Aliases;
        target.Source = sourceName;
    }

    static ModelGenerationCapabilities ToCapabilities(CapabilityRecord record) => new()
    {
        TextInput = record.TextInput ?? true,
        ImageInput = record.ImageInput ?? false,
        TextOutput = record.TextOutput ?? true,
        ImageOutput = record.ImageOutput ?? false,
        Streaming = record.Streaming ?? true,
        StructuredOutput = record.StructuredOutput ?? true,
        Tools = record.Tools ?? false,
        ImageGenerationModel = record.ImageGenerationModel ?? "",
        Temperature = record.Temperature ?? TuningSupport.Unsupported,
        TopP = record.TopP ?? TuningSupport.Unsupported,
        MaxTokens = record.MaxTokens ?? TuningSupport.Unsupported,
        Seed = record.Seed ?? TuningSupport.Unsupported,
        FrequencyPenalty = record.FrequencyPenalty ?? TuningSupport.Unsupported,
        PresencePenalty = record.PresencePenalty ?? TuningSupport.Unsupported,
        StopSequences = record.StopSequences ?? TuningSupport.Unsupported,
        SupportsReasoningEffort = record.SupportsReasoningEffort ?? false,
        SupportsVerbosity = record.SupportsVerbosity ?? false,
        Guidance = record.Guidance ?? "",
        Source = record.Source ?? "fallback",
        Aliases = record.Aliases ?? []
    };

    static HashSet<string> ReadStringSet(JsonNode? node) =>
        node?.AsArray()
            .Select(value => value?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
        ?? new(StringComparer.OrdinalIgnoreCase);

    static ModelCapabilityKey NormalizeKey(string providerType, string modelId) => new(
        providerType.Trim().ToLowerInvariant(),
        modelId.Trim().ToLowerInvariant());

    static bool DefaultsToTools(string providerType) =>
        providerType.Equals("openai", StringComparison.OrdinalIgnoreCase)
        || providerType.Equals("claude", StringComparison.OrdinalIgnoreCase)
        || providerType.Equals("grok", StringComparison.OrdinalIgnoreCase);

    sealed record ModelCapabilityKey(string Provider, string ModelId);

    sealed class CapabilityFile
    {
        public List<CapabilityRecord> Models { get; set; } = [];
    }

    sealed record CapabilityRecord
    {
        public string? Provider { get; set; }
        public string? Id { get; set; }
        public bool? TextInput { get; set; }
        public bool? ImageInput { get; set; }
        public bool? TextOutput { get; set; }
        public bool? ImageOutput { get; set; }
        public bool? Streaming { get; set; }
        public bool? StructuredOutput { get; set; }
        public bool? Tools { get; set; }
        public string? ImageGenerationModel { get; set; }
        public TuningSupport? Temperature { get; set; }
        public TuningSupport? TopP { get; set; }
        public TuningSupport? MaxTokens { get; set; }
        public TuningSupport? Seed { get; set; }
        public TuningSupport? FrequencyPenalty { get; set; }
        public TuningSupport? PresencePenalty { get; set; }
        public TuningSupport? StopSequences { get; set; }
        public bool? SupportsReasoningEffort { get; set; }
        public bool? SupportsVerbosity { get; set; }
        public string? Guidance { get; set; }
        public string? Source { get; set; }
        public List<string>? Aliases { get; set; }
    }
}
