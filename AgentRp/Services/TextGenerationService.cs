using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed record GenerateTurnRequest(
    string ParentTurnId,
    string Mode,
    string Guidance,
    string RequestedTurnShape,
    string RequestedActorCharacterId,
    string RequestedActorName);

public sealed record GenerateSnapshotRequest(string TurnId);

public sealed record GeneratedTurnResult(
    string ActorCharacterId,
    string ActorName,
    RpTurnPlan Plan,
    Dictionary<string, string> AppearanceByCharacterId,
    Dictionary<string, string> PrivateIntentByCharacterId,
    RpSceneFrame Scene,
    string Body,
    RpTurnTrace Trace);

public sealed record GeneratedSnapshotResult(
    string Summary,
    string EarlierPrivateIntentContinuity,
    Dictionary<string, string> CharacterAppearances,
    RpSceneFrame Scene,
    RpTurnTrace Trace);

public interface ITextGenerationService
{
    Task<GeneratedTurnResult> GenerateTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateTurnRequest request, CancellationToken cancellationToken = default);
    Task<GeneratedSnapshotResult> GenerateSnapshotAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateSnapshotRequest request, CancellationToken cancellationToken = default);
}

public sealed class TranscriptGenerationException(string message, RpTurnTrace trace) : Exception(message)
{
    public RpTurnTrace Trace { get; } = trace;
}

public sealed class TextGenerationService(
    IHttpClientFactory httpClientFactory,
    TranscriptPromptContextBuilder promptContextBuilder) : ITextGenerationService
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<GeneratedTurnResult> GenerateTurnAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        GenerateTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        var selection = ResolveTextModel(providers);
        var trace = new RpTurnTrace
        {
            Status = "running",
            StartedUtc = DateTime.UtcNow,
            ProviderId = selection.Provider.Id,
            ProviderName = selection.Provider.Name,
            ModelId = selection.Model.Id
        };
        try
        {
            var context = promptContextBuilder.BuildTurnContext(
                document,
                request.ParentTurnId,
                request.Guidance,
                request.RequestedTurnShape,
                document.Characters.FirstOrDefault(character => character.Id == request.RequestedActorCharacterId));
            var appearance = await RunAppearanceStepAsync(document, selection, context, trace, cancellationToken);
            var selectedActor = await RunSelectionStepAsync(document, selection, context, request, trace, cancellationToken);
            var plan = await RunPlanningStepAsync(document, selection, context, selectedActor, trace, cancellationToken);
            var prose = await RunProseStepAsync(document, selection, context, selectedActor, plan, trace, cancellationToken);
            FinalizeTrace(trace, "completed");

            var privateIntents = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(plan.PrivateIntent) && !string.IsNullOrWhiteSpace(selectedActor.Id))
                privateIntents[selectedActor.Id] = plan.PrivateIntent;

            var scene = SessionCloner.Clone(TranscriptGraph.GetActiveScene(document.Transcript));
            return new(
                selectedActor.Id,
                selectedActor.Name,
                new RpTurnPlan
                {
                    TurnShape = context.RequestedTurnShape,
                    Beat = plan.Beat,
                    Intent = plan.Intent,
                    ImmediateGoal = plan.ImmediateGoal,
                    WhyNow = plan.WhyNow,
                    ChangeIntroduced = plan.ChangeIntroduced,
                    Guardrails = plan.Guardrails
                },
                appearance,
                privateIntents,
                scene,
                prose,
                trace);
        }
        catch (Exception exception) when (exception is not TranscriptGenerationException)
        {
            FinalizeTrace(trace, "failed");
            trace.Data["error"] = exception.Message;
            throw new TranscriptGenerationException(exception.Message, trace);
        }
    }

    public async Task<GeneratedSnapshotResult> GenerateSnapshotAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        GenerateSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var selection = ResolveTextModel(providers);
        var trace = new RpTurnTrace
        {
            Status = "running",
            StartedUtc = DateTime.UtcNow,
            ProviderId = selection.Provider.Id,
            ProviderName = selection.Provider.Name,
            ModelId = selection.Model.Id
        };
        try
        {
            var context = promptContextBuilder.BuildSnapshotContext(document, request.TurnId);
            var tuning = ResolveTuning(document.ModelTuning, "snapshot");
            var tokens = promptContextBuilder.BuildTokens(context);
            var systemPrompt = RenderPrompt(document.PromptLibrary, "snapshot", "system", tokens);
            var userPrompt = RenderPrompt(document.PromptLibrary, "snapshot", "user", tokens);
            var startedUtc = DateTime.UtcNow;
            var completion = await SendCompletionAsync(selection.Provider, selection.Model, tuning, systemPrompt, userPrompt, true, cancellationToken);
            var result = DeserializeJson<SnapshotResponse>(completion.Text);
            trace.Steps.Add(CreateStepTrace(
                "snapshot",
                "Snapshot",
                selection,
                startedUtc,
                DateTime.UtcNow,
                systemPrompt,
                userPrompt,
                completion,
                JsonSerializer.Serialize(result, JsonOptions),
                ""));
            FinalizeTrace(trace, "completed");
            var activePath = TranscriptGraph.GetActivePath(document.Transcript);
            var turnIndex = activePath.FindIndex(turn => turn.Id == request.TurnId);
            if (turnIndex >= 0)
                activePath = activePath.Take(turnIndex + 1).ToList();

            var latestTurn = activePath.LastOrDefault();
            return new(
                result.Summary,
                context.EarlierPrivateIntentContinuity,
                BuildSnapshotAppearances(activePath),
                SessionCloner.Clone(latestTurn?.Scene ?? document.Transcript.RootScene),
                trace);
        }
        catch (Exception exception) when (exception is not TranscriptGenerationException)
        {
            FinalizeTrace(trace, "failed");
            trace.Data["error"] = exception.Message;
            throw new TranscriptGenerationException(exception.Message, trace);
        }
    }

    async Task<Dictionary<string, string>> RunAppearanceStepAsync(
        RpChatDocument document,
        ActiveTextModel selection,
        TurnPromptContext context,
        RpTurnTrace trace,
        CancellationToken cancellationToken)
    {
        var tuning = ResolveTuning(document.ModelTuning, "appearance");
        var tokens = promptContextBuilder.BuildTokens(context, "");
        var systemPrompt = RenderPrompt(document.PromptLibrary, "appearance", "system", tokens);
        var userPrompt = RenderPrompt(document.PromptLibrary, "appearance", "user", tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendCompletionAsync(selection.Provider, selection.Model, tuning, systemPrompt, userPrompt, true, cancellationToken);
        var result = DeserializeJson<AppearanceResponse>(completion.Text);
        trace.Steps.Add(CreateStepTrace(
            "appearance",
            "Appearance",
            selection,
            startedUtc,
            DateTime.UtcNow,
            systemPrompt,
            userPrompt,
            completion,
            JsonSerializer.Serialize(result, JsonOptions),
            ""));
        return result.Characters
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => ResolveCharacterId(document.Characters, pair.Key), pair => pair.Value, StringComparer.Ordinal);
    }

    async Task<(string Id, string Name)> RunSelectionStepAsync(
        RpChatDocument document,
        ActiveTextModel selection,
        TurnPromptContext context,
        GenerateTurnRequest request,
        RpTurnTrace trace,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RequestedActorCharacterId))
        {
            var explicitSelection = new SelectionResponse(request.RequestedActorName, "User override");
            var now = DateTime.UtcNow;
            trace.Steps.Add(CreateStepTrace(
                "selection",
                "Selection",
                selection,
                now,
                now,
                "User override",
                request.RequestedActorName,
                new("User override", 0, 0),
                JsonSerializer.Serialize(explicitSelection, JsonOptions),
                ""));
            return (request.RequestedActorCharacterId, request.RequestedActorName);
        }

        var tuning = ResolveTuning(document.ModelTuning, "selection");
        var tokens = promptContextBuilder.BuildTokens(context, "");
        var systemPrompt = RenderPrompt(document.PromptLibrary, "selection", "system", tokens);
        var userPrompt = RenderPrompt(document.PromptLibrary, "selection", "user", tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendCompletionAsync(selection.Provider, selection.Model, tuning, systemPrompt, userPrompt, true, cancellationToken);
        var result = DeserializeJson<SelectionResponse>(completion.Text);
        trace.Steps.Add(CreateStepTrace(
            "selection",
            "Selection",
            selection,
            startedUtc,
            DateTime.UtcNow,
            systemPrompt,
            userPrompt,
            completion,
            JsonSerializer.Serialize(result, JsonOptions),
            ""));
        var actorId = ResolveCharacterId(document.Characters, result.CharacterName);
        var actorName = document.Characters.FirstOrDefault(character => character.Id == actorId)?.Name ?? result.CharacterName;
        return (actorId, actorName);
    }

    async Task<PlanningResponse> RunPlanningStepAsync(
        RpChatDocument document,
        ActiveTextModel selection,
        TurnPromptContext context,
        (string Id, string Name) actor,
        RpTurnTrace trace,
        CancellationToken cancellationToken)
    {
        var tuning = ResolveTuning(document.ModelTuning, "planning");
        var tokens = promptContextBuilder.BuildTokens(context with { Actor = document.Characters.FirstOrDefault(character => character.Id == actor.Id) }, "");
        var systemPrompt = RenderPrompt(document.PromptLibrary, "planning", "system", tokens);
        var userPrompt = RenderPrompt(document.PromptLibrary, "planning", "user", tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendCompletionAsync(selection.Provider, selection.Model, tuning, systemPrompt, userPrompt, true, cancellationToken);
        var result = DeserializeJson<PlanningResponse>(completion.Text);
        trace.Steps.Add(CreateStepTrace(
            "planning",
            "Planning",
            selection,
            startedUtc,
            DateTime.UtcNow,
            systemPrompt,
            userPrompt,
            completion,
            JsonSerializer.Serialize(result, JsonOptions),
            ""));
        return result;
    }

    async Task<string> RunProseStepAsync(
        RpChatDocument document,
        ActiveTextModel selection,
        TurnPromptContext context,
        (string Id, string Name) actor,
        PlanningResponse plan,
        RpTurnTrace trace,
        CancellationToken cancellationToken)
    {
        var tuning = ResolveTuning(document.ModelTuning, "prose");
        var planningOutput = $"Beat: {plan.Beat}\nIntent: {plan.Intent}\nImmediate Goal: {plan.ImmediateGoal}\nWhy Now: {plan.WhyNow}\nChange Introduced: {plan.ChangeIntroduced}\nGuardrails: {plan.Guardrails}";
        var tokens = promptContextBuilder.BuildTokens(context with { Actor = document.Characters.FirstOrDefault(character => character.Id == actor.Id) }, planningOutput);
        var systemPrompt = RenderPrompt(document.PromptLibrary, "prose", "system", tokens);
        var userPrompt = RenderPrompt(document.PromptLibrary, "prose", "user", tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendCompletionAsync(selection.Provider, selection.Model, tuning, systemPrompt, userPrompt, false, cancellationToken);
        trace.Steps.Add(CreateStepTrace(
            "prose",
            "Prose",
            selection,
            startedUtc,
            DateTime.UtcNow,
            systemPrompt,
            userPrompt,
            completion,
            "",
            ""));
        return completion.Text.Trim();
    }

    static string RenderPrompt(PromptLibraryState library, string stepId, string field, IReadOnlyDictionary<string, string> tokens)
    {
        var defaults = PromptLibraryState.CreateDefault();
        var pair = library.Prompts.TryGetValue(stepId, out var configured)
            ? configured
            : defaults.Prompts[stepId];
        var template = field == "system" ? pair.System : pair.User;
        return PromptTemplateRenderer.Render(template, tokens);
    }

    static ModelTuningStepState ResolveTuning(ModelTuningState state, string stepId)
    {
        if (state.Values.TryGetValue(stepId, out var tuning))
            return tuning;

        var defaults = ModelTuningState.CreateDefault();
        return defaults.Values.TryGetValue(stepId, out var defaultTuning) ? defaultTuning : new();
    }

    async Task<TextCompletion> SendCompletionAsync(
        AiProvider provider,
        AiProviderModel model,
        ModelTuningStepState tuning,
        string systemPrompt,
        string userPrompt,
        bool expectJson,
        CancellationToken cancellationToken)
    {
        return provider.Type switch
        {
            "openai" or "grok" or "compatible" => await SendOpenAiStyleAsync(provider, model, tuning, systemPrompt, userPrompt, expectJson, cancellationToken),
            "claude" => await SendClaudeAsync(provider, model, tuning, systemPrompt, userPrompt, cancellationToken),
            _ => throw new InvalidOperationException($"Generating text failed because '{provider.Name}' is not supported for transcript generation yet.")
        };
    }

    async Task<TextCompletion> SendOpenAiStyleAsync(
        AiProvider provider,
        AiProviderModel model,
        ModelTuningStepState tuning,
        string systemPrompt,
        string userPrompt,
        bool expectJson,
        CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey, TimeSpan.FromMinutes(3));
        var body = new JsonObject
        {
            ["model"] = model.Id,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userPrompt }
            }
        };
        TextModelTuningCatalog.Apply(body, provider.Type, model.Id, tuning);
        if (expectJson)
            body["response_format"] = new JsonObject { ["type"] = "json_object" };

        using var response = await client.PostAsJsonAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "chat/completions"), body, JsonOptions, cancellationToken);
        var json = await ReadJsonAsync(response, $"Generating transcript text with '{model.Id}'", cancellationToken);
        var content = json["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException($"{provider.Name} did not return any text.");

        return new(
            content,
            json["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0,
            json["usage"]?["completion_tokens"]?.GetValue<int>() ?? 0);
    }

    async Task<TextCompletion> SendClaudeAsync(
        AiProvider provider,
        AiProviderModel model,
        ModelTuningStepState tuning,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        using var client = CreateClaudeClient(provider.ApiKey, TimeSpan.FromMinutes(3));
        var body = new JsonObject
        {
            ["model"] = model.Id,
            ["system"] = systemPrompt,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = userPrompt
                        }
                    }
                }
            },
            ["max_tokens"] = ParsePositiveInt(tuning.MaxTokens) ?? 1200
        };
        TextModelTuningCatalog.Apply(body, provider.Type, model.Id, tuning);

        using var response = await client.PostAsJsonAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "messages"), body, JsonOptions, cancellationToken);
        var json = await ReadJsonAsync(response, $"Generating transcript text with '{model.Id}'", cancellationToken);
        var content = json["content"]?.AsArray()
            .Select(item => item?["text"]?.GetValue<string>())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
        var text = string.Join("\n", content ?? []);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"{provider.Name} did not return any text.");

        return new(
            text,
            json["usage"]?["input_tokens"]?.GetValue<int>() ?? 0,
            json["usage"]?["output_tokens"]?.GetValue<int>() ?? 0);
    }

    static RpTurnTraceStep CreateStepTrace(
        string id,
        string label,
        ActiveTextModel selection,
        DateTime startedUtc,
        DateTime completedUtc,
        string systemPrompt,
        string userPrompt,
        TextCompletion completion,
        string structuredOutputJson,
        string error) => new()
    {
        Id = id,
        Label = label,
        Status = string.IsNullOrWhiteSpace(error) ? "completed" : "failed",
        StartedUtc = startedUtc,
        CompletedUtc = completedUtc,
        ProviderId = selection.Provider.Id,
        ProviderName = selection.Provider.Name,
        ModelId = selection.Model.Id,
        InputTokens = completion.InputTokens,
        OutputTokens = completion.OutputTokens,
        TotalTokens = completion.InputTokens + completion.OutputTokens,
        DurationSeconds = (completedUtc - startedUtc).TotalSeconds,
        SystemPrompt = systemPrompt,
        UserPrompt = userPrompt,
        RawOutput = completion.Text,
        StructuredOutputJson = structuredOutputJson,
        Error = error
    };

    static void FinalizeTrace(RpTurnTrace trace, string status)
    {
        trace.Status = status;
        trace.CompletedUtc = DateTime.UtcNow;
        trace.DurationSeconds = (trace.CompletedUtc - trace.StartedUtc).TotalSeconds;
        trace.InputTokens = trace.Steps.Sum(step => step.InputTokens);
        trace.OutputTokens = trace.Steps.Sum(step => step.OutputTokens);
        trace.TotalTokens = trace.Steps.Sum(step => step.TotalTokens);
        var actor = trace.Data["actorName"]?.GetValue<string>() ?? "Narrator";
        trace.Summary = $"{status[..1].ToUpperInvariant()}{status[1..]} · {actor} · {string.Join(" -> ", trace.Steps.Select(step => step.Label))}";
    }

    static ActiveTextModel ResolveTextModel(IReadOnlyList<AiProvider> providers) =>
        TextModelTuningCatalog.TryResolveActiveTextModel(providers)
        ?? throw new InvalidOperationException("Generating transcript text failed because no text-capable model is enabled.");

    static string ResolveCharacterId(IEnumerable<RpCharacter> characters, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var match = characters.FirstOrDefault(character => string.Equals(character.Name, name, StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? "";
    }

    static Dictionary<string, string> BuildSnapshotAppearances(IEnumerable<RpTranscriptTurn> turns)
    {
        var appearances = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var turn in turns)
        {
            foreach (var pair in turn.AppearanceByCharacterId)
                appearances[pair.Key] = pair.Value;
        }

        return appearances;
    }

    static T DeserializeJson<T>(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions)
                ?? throw new InvalidOperationException("The model returned an empty structured response.");
        }
        catch (JsonException)
        {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start >= 0 && end >= start)
            {
                var json = content[start..(end + 1)];
                return JsonSerializer.Deserialize<T>(json, JsonOptions)
                    ?? throw new InvalidOperationException("The model returned an empty structured response.");
            }

            throw;
        }
    }

    HttpClient CreateBearerClient(string apiKey, TimeSpan timeout)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = timeout;
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return client;
    }

    HttpClient CreateClaudeClient(string apiKey, TimeSpan timeout)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = timeout;
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        return client;
    }

    static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken) ?? new JsonObject();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = UserFacingErrorMessageBuilder.BuildExternalHttpFailure(operation, response.StatusCode, responseBody);
        throw new ExternalServiceFailureException(message, response.StatusCode, responseBody);
    }

    static string NormalizeEndpoint(AiProvider provider)
    {
        var endpoint = string.IsNullOrWhiteSpace(provider.Endpoint) ? DefaultEndpoint(provider.Type) : provider.Endpoint.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException($"Connecting to {provider.Name} failed because the endpoint was empty.");

        if (!endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Connecting to {provider.Name} failed because the endpoint must start with http:// or https://.");

        return endpoint.EndsWith('/') ? endpoint : $"{endpoint}/";
    }

    static string DefaultEndpoint(string providerType) => providerType switch
    {
        "openai" => "https://api.openai.com/v1/",
        "grok" => "https://api.x.ai/v1/",
        "claude" => "https://api.anthropic.com/v1/",
        _ => ""
    };

    static int? ParsePositiveInt(string value) => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;

    sealed record TextCompletion(string Text, int InputTokens, int OutputTokens);
    sealed class AppearanceResponse
    {
        public Dictionary<string, string> Characters { get; set; } = [];
    }

    sealed class SelectionResponse
    {
        public SelectionResponse()
        {
        }

        public SelectionResponse(string characterName, string reason)
        {
            CharacterName = characterName;
            Reason = reason;
        }

        public string CharacterName { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    sealed class PlanningResponse
    {
        public string Beat { get; set; } = "";
        public string Intent { get; set; } = "";
        public string ImmediateGoal { get; set; } = "";
        public string WhyNow { get; set; } = "";
        public string ChangeIntroduced { get; set; } = "";
        public string Guardrails { get; set; } = "";
        public string PrivateIntent { get; set; } = "";
    }

    sealed class SnapshotResponse
    {
        public string Summary { get; set; } = "";
    }
}
