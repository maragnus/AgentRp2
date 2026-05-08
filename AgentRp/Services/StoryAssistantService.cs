using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Serialization;
using AgentRp.Session;

namespace AgentRp.Services;

public enum StoryAssistantTurnRequestKind
{
    UserMessage,
    WorkItemResume
}

public sealed record StoryAssistantTurnRequest(
    StoryAssistantTurnRequestKind Kind,
    string ModelInput,
    string DisplayMessage,
    string ToolCallId = "",
    string PreviousResponseId = "")
{
    public static StoryAssistantTurnRequest Start(string modelInput, string displayMessage) =>
        new(StoryAssistantTurnRequestKind.UserMessage, modelInput, displayMessage);

    public static StoryAssistantTurnRequest Resume(StoryAssistantWorkItem workItem) =>
        new(StoryAssistantTurnRequestKind.WorkItemResume, workItem.ResultJson, workItem.Title, workItem.ToolCallId, workItem.AwaitingResponseId);

    public string UserMessage => ModelInput;
}

public interface IStoryAssistantCallbacks
{
    Task AppendAssistantTextAsync(string delta, CancellationToken cancellationToken);
    Task RecordToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken);
    Task UpdateToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken);
    Task RecordWorkItemAsync(StoryAssistantWorkItem workItem, CancellationToken cancellationToken);
    Task UpdateWorkItemAsync(StoryAssistantWorkItem workItem, CancellationToken cancellationToken);
    Task<SceneTransitionResult> SetSceneAsync(SetSceneRequest request, CancellationToken cancellationToken);
    Task SaveEntityAreaAsync(RoleplayStoreArea area, CancellationToken cancellationToken);
    Task SaveAssistantStateAsync(CancellationToken cancellationToken);
}

public enum StoryAssistantToolExecutionStatus
{
    Completed,
    Pending
}

public sealed record StoryAssistantToolExecutionResult(
    StoryAssistantToolExecutionStatus Status,
    string OutputJson,
    StoryAssistantWorkItem? WorkItem)
{
    public static StoryAssistantToolExecutionResult Completed(string outputJson) =>
        new(StoryAssistantToolExecutionStatus.Completed, outputJson, null);

    public static StoryAssistantToolExecutionResult Pending(StoryAssistantWorkItem workItem) =>
        new(StoryAssistantToolExecutionStatus.Pending, Output("pending", new { workItemId = workItem.Id, toolCallId = workItem.ToolCallId }), workItem);

    static string Output(string status, object value)
    {
        var node = JsonSerializer.SerializeToNode(value, AppJsonSerializerOptions.Web)?.AsObject() ?? new();
        node["status"] = status;
        return node.ToJsonString(AppJsonSerializerOptions.Web);
    }
}

public interface IStoryAssistantService
{
    Task RunTurnAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        StoryAssistantTurnRequest request,
        IStoryAssistantCallbacks callbacks,
        CancellationToken cancellationToken = default);

    Task ClearRemoteStateAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        CancellationToken cancellationToken = default);

    Task ResolveWorkItemAsync(
        RpChatDocument document,
        StoryAssistantWorkItem workItem,
        StoryAssistantWorkItemResolution resolution,
        IStoryAssistantCallbacks callbacks,
        CancellationToken cancellationToken = default);
}

public sealed partial class StoryAssistantService(
    IModelGenerationClient generationClient,
    IModelCapabilityCatalog capabilityCatalog,
    StoryEntityPatchService patchService) : IStoryAssistantService
{
    public async Task RunTurnAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        StoryAssistantTurnRequest request,
        IStoryAssistantCallbacks callbacks,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var selection = TextModelTuningCatalog.TryResolveActiveReasoningModel(providers, modelSelections)
            ?? throw new InvalidOperationException("Starting the Story Assistant failed because no reasoning model is enabled.");
        if (!selection.Capabilities.CanGenerateText || !selection.Capabilities.Tools)
            throw new InvalidOperationException($"Starting the Story Assistant failed because reasoning model '{selection.Model.Id}' must support text and tools.");

        if (request.Kind == StoryAssistantTurnRequestKind.WorkItemResume && string.IsNullOrWhiteSpace(request.PreviousResponseId))
            throw new InvalidOperationException("Resuming Story Assistant failed because the saved action is missing its Responses continuation.");

        if (request.Kind == StoryAssistantTurnRequestKind.WorkItemResume && !ResponseChainMatches(document.StoryAssistant, selection))
            throw new InvalidOperationException($"Resuming Story Assistant failed because the saved action belongs to '{document.StoryAssistant.ResponseModelId}', but the active reasoning model is '{selection.Model.Id}'.");

        if (request.Kind == StoryAssistantTurnRequestKind.UserMessage && !ResponseChainMatches(document.StoryAssistant, selection))
        {
            await ClearRemoteStateAsync(document, providers, modelSelections, cancellationToken);
            ClearResponseChain(document.StoryAssistant);
            await callbacks.SaveAssistantStateAsync(cancellationToken);
        }

        document.StoryAssistant.RemoteThreadLost = false;
        document.StoryAssistant.RemoteThreadError = "";
        var previousResponseId = request.Kind == StoryAssistantTurnRequestKind.WorkItemResume
            ? request.PreviousResponseId
            : document.StoryAssistant.LastResponseId;
        var inputs = request.Kind == StoryAssistantTurnRequestKind.WorkItemResume
            ? new List<ModelAssistantInput> { new(ModelAssistantInputKind.FunctionCallOutput, request.ModelInput, request.ToolCallId) }
            : new List<ModelAssistantInput> { new(ModelAssistantInputKind.UserMessage, request.ModelInput.Trim()) };
        var instructions = Instructions(document.PromptLibrary);
        for (var pass = 0; pass < 16; pass++)
        {
            var toolOutputs = new List<ModelAssistantInput>();
            StoryAssistantWorkItem? pendingWorkItem = null;
            await foreach (var update in generationClient.GenerateAssistantStreamingAsync(new(
                selection.Provider,
                selection.Model,
                selection.Capabilities,
                new(),
                instructions,
                previousResponseId,
                inputs,
                BuildTools(document),
                "Running Story Assistant"), cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (update.Kind == ModelAssistantStreamingUpdateKind.TextDelta)
                    await callbacks.AppendAssistantTextAsync(update.TextDelta, cancellationToken);
                else if (update.Kind == ModelAssistantStreamingUpdateKind.ToolCall)
                {
                    var execution = await patchService.ExecuteToolAsync(document, update.ToolCallId, update.ToolName, update.ToolArgumentsJson, callbacks, cancellationToken);
                    if (execution.Status == StoryAssistantToolExecutionStatus.Pending)
                        pendingWorkItem = execution.WorkItem;
                    else
                        toolOutputs.Add(new(ModelAssistantInputKind.FunctionCallOutput, execution.OutputJson, update.ToolCallId));
                }
                else if (update.Kind == ModelAssistantStreamingUpdateKind.Completed)
                {
                    if (!string.IsNullOrWhiteSpace(update.ResponseId))
                    {
                        RecordResponse(document.StoryAssistant, selection, update.ResponseId);
                        if (pendingWorkItem is not null)
                        {
                            pendingWorkItem.AwaitingResponseId = update.ResponseId;
                            pendingWorkItem.ResponseProviderId = selection.Provider.Id;
                            pendingWorkItem.ResponseModelId = selection.Model.Id;
                            pendingWorkItem.UpdatedUtc = DateTime.UtcNow;
                            await callbacks.UpdateWorkItemAsync(pendingWorkItem, cancellationToken);
                        }
                    }

                    await callbacks.SaveAssistantStateAsync(cancellationToken);
                }
            }

            if (pendingWorkItem is not null)
                return;

            if (toolOutputs.Count == 0)
                return;

            inputs = toolOutputs;
            previousResponseId = document.StoryAssistant.LastResponseId;
        }

        throw new InvalidOperationException("Running the Story Assistant stopped because too many tool rounds were requested in one turn.");
    }

    public async Task ClearRemoteStateAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var responseIds = document.StoryAssistant.ResponseIds
            .Append(document.StoryAssistant.LastResponseId)
            .Where(responseId => !string.IsNullOrWhiteSpace(responseId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (responseIds.Count == 0)
            return;

        var provider = providers.FirstOrDefault(provider => provider.Id == document.StoryAssistant.ResponseProviderId);
        var model = provider?.Models.FirstOrDefault(model => model.Id == document.StoryAssistant.ResponseModelId);
        if (provider is null || model is null)
            return;

        await generationClient.DeleteAssistantResponsesAsync(provider, model, responseIds, cancellationToken);
    }

    public Task ResolveWorkItemAsync(
        RpChatDocument document,
        StoryAssistantWorkItem workItem,
        StoryAssistantWorkItemResolution resolution,
        IStoryAssistantCallbacks callbacks,
        CancellationToken cancellationToken = default) =>
        patchService.ResolveWorkItemAsync(document, workItem, resolution, callbacks, cancellationToken);

    void ApplyCapabilities(IReadOnlyList<AiProvider> providers)
    {
        foreach (var provider in providers)
            capabilityCatalog.ApplyResolvedCapabilities(provider);
    }

    static bool ResponseChainMatches(StoryAssistantState state, ActiveModelSelection selection) =>
        string.IsNullOrWhiteSpace(state.LastResponseId)
        || (string.Equals(state.ResponseProviderId, selection.Provider.Id, StringComparison.Ordinal)
            && string.Equals(state.ResponseModelId, selection.Model.Id, StringComparison.Ordinal));

    static void RecordResponse(StoryAssistantState state, ActiveModelSelection selection, string responseId)
    {
        state.LastResponseId = responseId;
        state.ResponseProviderId = selection.Provider.Id;
        state.ResponseModelId = selection.Model.Id;
        state.RemoteThreadLost = false;
        state.RemoteThreadError = "";
        if (!state.ResponseIds.Contains(responseId, StringComparer.Ordinal))
            state.ResponseIds.Add(responseId);
    }

    public static void ClearResponseChain(StoryAssistantState state)
    {
        state.LastResponseId = "";
        state.ResponseIds.Clear();
        state.ResponseProviderId = "";
        state.ResponseModelId = "";
        state.RemoteThreadLost = false;
        state.RemoteThreadError = "";
    }

    static string Instructions(PromptLibraryState promptLibrary) =>
        PromptLibraryService.RenderStage(promptLibrary, PromptLibraryStageIds.StoryAssistantBase, EmptyPromptValues).SystemPrompt;

    static readonly IReadOnlyDictionary<string, string> EmptyPromptValues = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed class StoryEntityPatchService(SceneTransitionService? sceneTransitionService = null)
{
    static readonly string[] LocationFields = ["name", "summary", "description", "atmosphere", "features"];
    static readonly string[] ItemFields = ["name", "summary", "description", "history", "properties"];
    static readonly string[] TimelineFields = ["title", "date", "description", "significance", "characters"];

    public async Task<string> ExecuteAsync(
        RpChatDocument document,
        string toolCallId,
        string toolName,
        string argumentsJson,
        IStoryAssistantCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteToolAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
        return result.OutputJson;
    }

    public async Task<StoryAssistantToolExecutionResult> ExecuteToolAsync(
        RpChatDocument document,
        string toolCallId,
        string toolName,
        string argumentsJson,
        IStoryAssistantCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (toolName)
            {
                case "get_story_entities":
                    return StoryAssistantToolExecutionResult.Completed(await ReadToolAsync(toolCallId, toolName, "Read story entities", argumentsJson, callbacks, new { entities = BuildEntities(document) }, cancellationToken));
                case "get_story_transcript":
                    return StoryAssistantToolExecutionResult.Completed(await ReadToolAsync(toolCallId, toolName, "Read story transcript", argumentsJson, callbacks, new { transcript = BuildTranscript(document) }, cancellationToken));
                case "get_character_profile_options":
                    return StoryAssistantToolExecutionResult.Completed(await ReadProfileOptionsAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken));
                case "get_chat_direction_options":
                    return StoryAssistantToolExecutionResult.Completed(await ReadChatDirectionOptionsAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken));
                case "ask_user":
                    return await AskUserAsync(toolCallId, argumentsJson, callbacks, cancellationToken);
                case "create_character":
                    return await CreateCharacterAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
                case "update_character":
                    return await UpdateCharacterAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
                case "update_chat_direction":
                    return await UpdateChatDirectionAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
                case "create_location":
                    return await CreateLocationAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
                case "update_location":
                    return await UpdateLocationAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
                case "create_item":
                    return await CreateItemAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
                case "update_item":
                    return await UpdateItemAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
                case "create_timeline_entry":
                    return await CreateTimelineAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
                case "update_timeline_entry":
                    return await UpdateTimelineAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
                case "update_character_relationship":
                    return await UpdateRelationshipAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
                case "set_scene":
                    return await SetSceneAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken);
                default:
                    return StoryAssistantToolExecutionResult.Completed(Output("failed", new { reason = $"Unknown tool '{toolName}'." }));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CharacterProfileValidationException exception)
        {
            return StoryAssistantToolExecutionResult.Completed(Output("failed", new
            {
                reason = exception.Message,
                nextStep = CharacterProfileNextStep(toolName, exception)
            }));
        }
        catch (ChatDirectionValidationException exception)
        {
            return StoryAssistantToolExecutionResult.Completed(Output("failed", new
            {
                reason = exception.Message,
                nextStep = new
                {
                    tool = "get_chat_direction_options",
                    fields = exception.Fields,
                    instruction = "Call get_chat_direction_options for the invalid field, then retry with valid ids, limits, and intensity values."
                }
            }));
        }
        catch (StoryAssistantEntityLookupException exception)
        {
            return StoryAssistantToolExecutionResult.Completed(Output("failed", new
            {
                reason = exception.Message,
                nextStep = new
                {
                    tool = "get_story_entities",
                    instruction = "Call get_story_entities, choose the correct entity id from the result, then retry with that entityId."
                }
            }));
        }
        catch (SceneTransitionValidationException exception)
        {
            return StoryAssistantToolExecutionResult.Completed(Output("failed", new
            {
                reason = exception.Message,
                nextStep = new
                {
                    tool = "get_story_entities",
                    instruction = "Call get_story_entities, choose existing canon ids, create missing canon with the appropriate entity tool or ask the user, then retry set_scene."
                }
            }));
        }
        catch (Exception exception)
        {
            return StoryAssistantToolExecutionResult.Completed(Output("failed", new { reason = exception.Message }));
        }
    }

    static async Task<string> ReadToolAsync(string callId, string toolName, string title, string argumentsJson, IStoryAssistantCallbacks callbacks, object payload, CancellationToken cancellationToken)
    {
        var item = BaseItem(callId, toolName, title, argumentsJson);
        item.Status = StoryAssistantItemStatus.Read;
        item.Operation = StoryAssistantOperationKind.Read;
        item.ResultJson = Output("accepted", payload);
        await callbacks.RecordToolCallAsync(item, cancellationToken);
        return item.ResultJson;
    }

    static object CharacterProfileNextStep(string toolName, CharacterProfileValidationException exception)
    {
        var controlledFields = exception.Fields
            .Where(field => CharacterProfileRules.ControlledCharacterFields.Contains(field, StringComparer.Ordinal))
            .ToList();

        if (toolName == "update_character_relationship")
            return new
            {
                tool = controlledFields.Count > 0 ? "get_character_profile_options" : toolName,
                fields = exception.Fields,
                controlledFields,
                instruction = controlledFields.Count > 0
                    ? "Read valid relationshipTypes and privateTensions options, then retry update_character_relationship with every relationship field populated."
                    : "Retry update_character_relationship with every required relationship field populated."
            };

        if (controlledFields.Count > 0)
            return new
            {
                tool = "get_character_profile_options",
                fields = controlledFields,
                instruction = "Read valid controlled profile options, then retry with a complete and useful character profile."
            };

        return new
        {
            tool = toolName,
            fields = exception.Fields,
            instruction = "Retry with the missing character fields filled in."
        };
    }

    static object BuildEntities(RpChatDocument document)
    {
        var library = CharacterTraitLibraryService.NormalizeState(document.CharacterTraitLibrary);
        return new
        {
            characters = document.Characters.Select(character => CharacterShape(character, library)),
            locations = document.Locations.Select(LocationShape),
            items = document.Items.Select(ItemShape),
            timeline = document.Timeline.Select(TimelineShape),
            chatDirection = ChatDirectionRules.Context(document.ChatDirection),
            characterTraitLibrary = CharacterProfileRules.Context(library),
            relationships = CharacterRelationshipRules.Coverage(document)
        };
    }

    static object BuildTranscript(RpChatDocument document) =>
        TranscriptGraph.GetActivePath(document.Transcript).Select(turn => new
        {
            turn.Id,
            turn.AuthorName,
            turn.ActorName,
            turn.Body,
            turn.Guidance,
            turn.PrivateIntentByCharacterId,
            turn.CreatedUtc
        });

    static object CharacterShape(RpCharacter character, CharacterTraitLibraryState library) => new
    {
        character.Id,
        character.Name,
        character.ImageId,
        character.Summary,
        character.Personality,
        extraAppearanceDetails = character.Appearance,
        character.AppearanceProfile.HairColor,
        character.AppearanceProfile.HairStyles,
        character.AppearanceProfile.EyeColor,
        character.AppearanceProfile.FaceShape,
        character.AppearanceProfile.SkinTone,
        character.AppearanceProfile.Complexion,
        character.AppearanceProfile.Height,
        character.AppearanceProfile.Build,
        character.AppearanceProfile.BodyProportions,
        character.AppearanceProfile.Presentation,
        character.AppearanceProfile.Attractiveness,
        character.Backstory,
        character.Voice,
        character.Notes,
        character.Pronouns,
        character.SceneRoles,
        character.Traits,
        character.Drives,
        character.Limits,
        character.CoreDrive,
        character.CoreFear,
        character.SurfaceMask,
        character.HiddenTruth,
        character.SentenceStyle,
        character.HonestyStyle,
        character.EmotionalLeakage,
        character.ActionFingerprint,
        character.StressPattern,
        character.SoftSpots,
        character.AvoidPatterns
    };

    static object RelationshipShape(CharacterRelationshipView relationship) =>
        RelationshipShape(relationship, relationship.SourceCharacterId, relationship.SourceCharacterName, relationship.TargetCharacterId, relationship.TargetCharacterName);

    static object RelationshipShape(CharacterRelationshipView? relationship, string sourceId, string sourceName, string targetId, string targetName) => new
    {
        sourceCharacterId = sourceId,
        sourceCharacterName = sourceName,
        targetCharacterId = targetId,
        targetCharacterName = targetName,
        howSourceSeesTarget = relationship?.HowSourceSeesTarget ?? "",
        howTargetSeesSource = relationship?.HowTargetSeesSource ?? "",
        publicDynamic = relationship?.PublicDynamic ?? "",
        relationshipTypes = relationship?.RelationshipTypes ?? [],
        privateTensions = relationship?.PrivateTensions ?? []
    };

    static object LocationShape(RpLocation location) => new
    {
        location.Id,
        location.Name,
        location.ImageId,
        location.Summary,
        location.Description,
        location.Atmosphere,
        location.Features
    };

    static object ItemShape(RpItem item) => new
    {
        item.Id,
        item.Name,
        item.ImageId,
        item.Summary,
        item.Description,
        item.History,
        item.Properties
    };

    static object TimelineShape(RpTimelineEntry entry) => new
    {
        entry.Id,
        entry.Title,
        entry.Date,
        entry.Description,
        entry.Characters,
        entry.Significance
    };

    static async Task<string> ReadProfileOptionsAsync(RpChatDocument document, string callId, string toolName, string argumentsJson, IStoryAssistantCallbacks callbacks, CancellationToken cancellationToken)
    {
        using var json = Parse(argumentsJson);
        var fields = json.RootElement.TryGetProperty("fields", out var fieldArray) && fieldArray.ValueKind == JsonValueKind.Array
            ? fieldArray.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? "").Where(item => !string.IsNullOrWhiteSpace(item)).ToList()
            : [];
        return await ReadToolAsync(callId, toolName, "Read character profile options", argumentsJson, callbacks, new { characterProfileOptions = CharacterProfileRules.ProfileOptions(document.CharacterTraitLibrary, fields) }, cancellationToken);
    }

    static async Task<string> ReadChatDirectionOptionsAsync(RpChatDocument document, string callId, string toolName, string argumentsJson, IStoryAssistantCallbacks callbacks, CancellationToken cancellationToken)
    {
        using var json = Parse(argumentsJson);
        var fields = json.RootElement.TryGetProperty("fields", out var fieldArray) && fieldArray.ValueKind == JsonValueKind.Array
            ? fieldArray.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? "").Where(item => !string.IsNullOrWhiteSpace(item)).ToList()
            : [];
        return await ReadToolAsync(callId, toolName, "Read chat direction options", argumentsJson, callbacks, new { chatDirectionOptions = ChatDirectionRules.Options(document.ChatDirection, fields) }, cancellationToken);
    }

    async Task<StoryAssistantToolExecutionResult> AskUserAsync(string callId, string argumentsJson, IStoryAssistantCallbacks callbacks, CancellationToken cancellationToken)
    {
        using var doc = Parse(argumentsJson);
        var root = doc.RootElement;
        var workItem = BaseWorkItem(callId, "ask_user", "Question", argumentsJson, StoryAssistantWorkItemKind.Question);
        workItem.Operation = StoryAssistantOperationKind.Question;
        workItem.Question.Prompt = String(root, "prompt");
        workItem.Question.AllowsFreeform = Bool(root, "allowsFreeform");
        workItem.Question.SelectionMode = String(root, "selectionMode").Equals("multiple", StringComparison.OrdinalIgnoreCase)
            ? StoryAssistantQuestionSelectionMode.Multiple
            : StoryAssistantQuestionSelectionMode.Single;
        workItem.Question.MinSelections = Math.Max(0, Int(root, "minSelections", workItem.Question.SelectionMode == StoryAssistantQuestionSelectionMode.Multiple ? 0 : 1));
        workItem.Question.MaxSelections = Math.Max(1, Int(root, "maxSelections", workItem.Question.SelectionMode == StoryAssistantQuestionSelectionMode.Multiple ? 3 : 1));
        if (workItem.Question.SelectionMode == StoryAssistantQuestionSelectionMode.Single)
        {
            workItem.Question.MinSelections = 1;
            workItem.Question.MaxSelections = 1;
        }

        workItem.Question.Choices = root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array
            ? choices.EnumerateArray().Select((choice, index) => new StoryAssistantQuestionChoice
            {
                Id = String(choice, "id", $"choice-{index + 1}"),
                Label = String(choice, "label"),
                Description = String(choice, "description")
            }).Where(choice => !string.IsNullOrWhiteSpace(choice.Label)).ToList()
            : [];
        await callbacks.RecordWorkItemAsync(workItem, cancellationToken);
        return StoryAssistantToolExecutionResult.Pending(workItem);
    }

    async Task<StoryAssistantToolExecutionResult> CreateCharacterAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var character = new RpCharacter { Id = NextId(document.Characters.Select(item => item.Id), "c"), Name = "New Character" };
        var updates = Updates(json.RootElement);
        CharacterProfileRules.ValidateCharacterPatch(updates, document.CharacterTraitLibrary);
        ApplyCharacter(character, updates);
        CharacterProfileRules.ValidateCreatedCharacter(character);
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Create, $"Create {character.Name}", "character", character.Id, character.Name, args, new(), CharacterJsonObject(character, document.CharacterTraitLibrary), StoryAssistantChangeRisk.Low);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Characters, () => document.Characters.Insert(0, character), new(), token);
    }

    async Task<StoryAssistantToolExecutionResult> UpdateCharacterAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var id = RequiredEntityId(json.RootElement);
        var existing = document.Characters.FirstOrDefault(item => item.Id == id) ?? throw new StoryAssistantEntityLookupException($"No character with id '{id}' exists.");
        var before = Clone(existing);
        var after = Clone(existing);
        var updates = Updates(json.RootElement);
        CharacterProfileRules.ValidateCharacterPatch(updates, document.CharacterTraitLibrary);
        ApplyCharacter(after, updates);
        if (CharacterProfileRules.HasAppearancePatch(updates))
            CharacterProfileRules.ValidateCompleteAppearance(after);
        var risk = updates.TryGetProperty("name", out _) || updates.TryGetProperty("backstory", out _) ? StoryAssistantChangeRisk.Major : StoryAssistantChangeRisk.Low;
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Update, $"Update {after.Name}", "character", id, after.Name, args, CharacterJsonObject(before, document.CharacterTraitLibrary), CharacterJsonObject(after, document.CharacterTraitLibrary), risk);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Characters, () => Copy(after, existing), CharacterJsonObject(existing, document.CharacterTraitLibrary), token);
    }

    async Task<StoryAssistantToolExecutionResult> UpdateChatDirectionAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var before = SessionCloner.Clone(document.ChatDirection);
        var after = SessionCloner.Clone(document.ChatDirection);
        var updates = Updates(json.RootElement);
        ChatDirectionRules.ValidatePatch(updates);
        ChatDirectionRules.Apply(after, updates);
        after = ChatDirectionService.NormalizeState(after);
        var risk = ChatDirectionRules.Risk(updates);
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Update, "Update chat direction", "chatDirection", document.Chat.Id, "Chat Direction", args, ChatDirectionRules.JsonObject(before), ChatDirectionRules.JsonObject(after), risk);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.ChatDirection, () => document.ChatDirection = after, ChatDirectionRules.JsonObject(document.ChatDirection), token);
    }

    async Task<StoryAssistantToolExecutionResult> CreateLocationAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var updates = Updates(json.RootElement);
        ValidatePatch(updates, LocationFields, "location");
        RequirePatchString(updates, "name", "Creating a location");
        var location = new RpLocation { Id = NextId(document.Locations.Select(item => item.Id), "l") };
        ApplyLocation(location, updates);
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Create, $"Create {location.Name}", "location", location.Id, location.Name, args, new(), LocationJsonObject(location), StoryAssistantChangeRisk.Low);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Locations, () => document.Locations.Add(location), new(), token);
    }

    async Task<StoryAssistantToolExecutionResult> UpdateLocationAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var id = RequiredEntityId(json.RootElement);
        var existing = document.Locations.FirstOrDefault(item => item.Id == id) ?? throw new StoryAssistantEntityLookupException($"No location with id '{id}' exists.");
        var before = Clone(existing);
        var after = Clone(existing);
        var updates = Updates(json.RootElement);
        ValidatePatch(updates, LocationFields, "location");
        ApplyLocation(after, updates);
        var risk = updates.TryGetProperty("name", out _) ? StoryAssistantChangeRisk.Major : StoryAssistantChangeRisk.Low;
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Update, $"Update {after.Name}", "location", id, after.Name, args, LocationJsonObject(before), LocationJsonObject(after), risk);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Locations, () => Copy(after, existing), LocationJsonObject(existing), token);
    }

    async Task<StoryAssistantToolExecutionResult> CreateItemAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var updates = Updates(json.RootElement);
        ValidatePatch(updates, ItemFields, "item");
        RequirePatchString(updates, "name", "Creating an item");
        var itemEntity = new RpItem { Id = NextId(document.Items.Select(item => item.Id), "i") };
        ApplyItem(itemEntity, updates);
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Create, $"Create {itemEntity.Name}", "item", itemEntity.Id, itemEntity.Name, args, new(), ItemJsonObject(itemEntity), StoryAssistantChangeRisk.Low);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Items, () => document.Items.Add(itemEntity), new(), token);
    }

    async Task<StoryAssistantToolExecutionResult> UpdateItemAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var id = RequiredEntityId(json.RootElement);
        var existing = document.Items.FirstOrDefault(item => item.Id == id) ?? throw new StoryAssistantEntityLookupException($"No item with id '{id}' exists.");
        var before = Clone(existing);
        var after = Clone(existing);
        var updates = Updates(json.RootElement);
        ValidatePatch(updates, ItemFields, "item");
        ApplyItem(after, updates);
        var risk = updates.TryGetProperty("name", out _) ? StoryAssistantChangeRisk.Major : StoryAssistantChangeRisk.Low;
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Update, $"Update {after.Name}", "item", id, after.Name, args, ItemJsonObject(before), ItemJsonObject(after), risk);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Items, () => Copy(after, existing), ItemJsonObject(existing), token);
    }

    async Task<StoryAssistantToolExecutionResult> CreateTimelineAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var updates = Updates(json.RootElement);
        ValidatePatch(updates, TimelineFields, "timeline entry");
        RequirePatchString(updates, "title", "Creating a timeline entry");
        var entry = new RpTimelineEntry { Id = NextId(document.Timeline.Select(item => item.Id), "t") };
        ApplyTimeline(entry, updates);
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Create, $"Create {entry.Title}", "timeline", entry.Id, entry.Title, args, new(), TimelineJsonObject(entry), StoryAssistantChangeRisk.Major);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Timeline, () => document.Timeline.Add(entry), new(), token);
    }

    async Task<StoryAssistantToolExecutionResult> UpdateTimelineAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var id = RequiredEntityId(json.RootElement);
        var existing = document.Timeline.FirstOrDefault(item => item.Id == id) ?? throw new StoryAssistantEntityLookupException($"No timeline entry with id '{id}' exists.");
        var before = Clone(existing);
        var after = Clone(existing);
        var updates = Updates(json.RootElement);
        ValidatePatch(updates, TimelineFields, "timeline entry");
        ApplyTimeline(after, updates);
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Update, $"Update {after.Title}", "timeline", id, after.Title, args, TimelineJsonObject(before), TimelineJsonObject(after), StoryAssistantChangeRisk.Major);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Timeline, () => Copy(after, existing), TimelineJsonObject(existing), token);
    }

    async Task<StoryAssistantToolExecutionResult> UpdateRelationshipAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var sourceId = RequiredString(json.RootElement, "sourceCharacterId");
        var targetId = RequiredString(json.RootElement, "targetCharacterId");
        var source = document.Characters.FirstOrDefault(item => item.Id == sourceId) ?? throw new StoryAssistantEntityLookupException($"No source character with id '{sourceId}' exists.");
        var target = document.Characters.FirstOrDefault(item => item.Id == targetId) ?? throw new StoryAssistantEntityLookupException($"No target character with id '{targetId}' exists.");
        CharacterProfileRules.ValidateRelationshipPatch(json.RootElement, document.CharacterTraitLibrary);
        var preview = SessionCloner.Clone(document);
        var before = RelationshipJsonObject(document, source.Id, source.Name, target.Id, target.Name);
        ApplyRelationship(preview, json.RootElement);
        var after = RelationshipJsonObject(preview, source.Id, source.Name, target.Id, target.Name);

        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Update, $"Update {source.Name} and {target.Name}", "relationship", source.Id, source.Name, args, before, after, StoryAssistantChangeRisk.Major);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Characters, () => ApplyRelationship(document, json.RootElement), RelationshipJsonObject(document, source.Id, source.Name, target.Id, target.Name), token);
    }

    async Task<StoryAssistantToolExecutionResult> SetSceneAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var root = json.RootElement;
        var request = BuildSetSceneRequest(root);
        var transition = (sceneTransitionService ?? new SceneTransitionService()).Build(document, request);
        var item = MutationItem(
            callId,
            toolName,
            StoryAssistantOperationKind.Update,
            transition.IsOpeningScene ? $"Set opening scene at {transition.TargetScene.LocationName}" : $"Set scene at {transition.TargetScene.LocationName}",
            "scene",
            document.Chat.Id,
            transition.TargetScene.LocationName,
            args,
            SceneJsonObject(transition.PreviousScene, document),
            SceneTransitionJsonObject(transition, document),
            SceneTransitionRisk(transition));
        return await ResolveSceneTransitionAsync(document, item, callbacks, request, transition, token);
    }

    static SetSceneRequest BuildSetSceneRequest(JsonElement root) =>
        new(
            RequiredString(root, "locationId"),
            StringList(root, "characterIds"),
            StringList(root, "itemIds"),
            ParseNarratorGuidance(root));

    async Task<StoryAssistantToolExecutionResult> ResolveMutationAsync(
        RpChatDocument document,
        StoryAssistantTranscriptItem item,
        IStoryAssistantCallbacks callbacks,
        RoleplayStoreArea area,
        Action apply,
        JsonObject currentEntity,
        CancellationToken cancellationToken)
    {
        item.Diffs = Diff(item.Before, item.After);
        var shouldReview = ShouldReview(document.StoryAssistant.ReviewMode, item.Risk);
        if (shouldReview)
        {
            var workItem = WorkItemFromTranscriptItem(item, StoryAssistantWorkItemKind.MutationReview, area);
            await callbacks.RecordWorkItemAsync(workItem, cancellationToken);
            return StoryAssistantToolExecutionResult.Pending(workItem);
        }

        item.Status = StoryAssistantItemStatus.Applied;
        await callbacks.RecordToolCallAsync(item, cancellationToken);
        apply();
        await callbacks.SaveEntityAreaAsync(area, cancellationToken);
        await callbacks.UpdateToolCallAsync(item, cancellationToken);
        return StoryAssistantToolExecutionResult.Completed(AcceptedMutationOutput(document, item));
    }

    static string AcceptedMutationOutput(RpChatDocument document, StoryAssistantTranscriptItem item)
    {
        var payload = new JsonObject
        {
            ["entityType"] = item.EntityType,
            ["entityId"] = item.EntityId,
            ["resultingEntity"] = item.After.DeepClone()
        };

        if (item.ToolName is "create_character" or "update_character")
            payload["relationshipReconciliation"] = ToJsonObject(CharacterRelationshipRules.ReconciliationFor(document, item.EntityId));

        payload["status"] = "accepted";
        return payload.ToJsonString(AppJsonSerializerOptions.Web);
    }

    static string AcceptedMutationOutput(RpChatDocument document, StoryAssistantWorkItem workItem)
    {
        var payload = new JsonObject
        {
            ["entityType"] = workItem.EntityType,
            ["entityId"] = workItem.EntityId,
            ["resultingEntity"] = workItem.After.DeepClone()
        };

        if (workItem.ToolName is "create_character" or "update_character")
            payload["relationshipReconciliation"] = ToJsonObject(CharacterRelationshipRules.ReconciliationFor(document, workItem.EntityId));

        payload["status"] = "accepted";
        return payload.ToJsonString(AppJsonSerializerOptions.Web);
    }

    async Task<StoryAssistantToolExecutionResult> ResolveSceneTransitionAsync(
        RpChatDocument document,
        StoryAssistantTranscriptItem item,
        IStoryAssistantCallbacks callbacks,
        SetSceneRequest request,
        SceneTransitionPlan transition,
        CancellationToken cancellationToken)
    {
        item.Diffs = Diff(item.Before, item.After);
        var shouldReview = RequiresSceneReview(transition) || ShouldReview(document.StoryAssistant.ReviewMode, item.Risk);
        if (shouldReview)
        {
            var workItem = WorkItemFromTranscriptItem(item, StoryAssistantWorkItemKind.SceneReview, RoleplayStoreArea.Transcript);
            await callbacks.RecordWorkItemAsync(workItem, cancellationToken);
            return StoryAssistantToolExecutionResult.Pending(workItem);
        }

        try
        {
            item.Status = StoryAssistantItemStatus.Applied;
            await callbacks.RecordToolCallAsync(item, cancellationToken);
            var generated = await callbacks.SetSceneAsync(request, cancellationToken);
            item.After = SceneTransitionJsonObject(generated.Plan, document);
            item.Diffs = Diff(item.Before, item.After);
            await callbacks.UpdateToolCallAsync(item, cancellationToken);
            return StoryAssistantToolExecutionResult.Completed(Output("accepted", new
            {
                entityType = item.EntityType,
                entityId = item.EntityId,
                resultingScene = item.After,
                narratorInstruction = generated.Plan.NarratorInstruction,
                narratorTurnId = generated.NarratorTurnId,
                narratorMessage = generated.NarratorMessage
            }));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            item.Status = StoryAssistantItemStatus.Failed;
            item.DecisionReason = UserFacingErrorMessageBuilder.Build("Setting the scene failed.", exception);
            await callbacks.UpdateToolCallAsync(item, cancellationToken);
            return StoryAssistantToolExecutionResult.Completed(Output("failed", new { reason = item.DecisionReason, currentScene = item.Before }));
        }
    }

    public async Task ResolveWorkItemAsync(
        RpChatDocument document,
        StoryAssistantWorkItem workItem,
        StoryAssistantWorkItemResolution resolution,
        IStoryAssistantCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        if (workItem.Status != StoryAssistantWorkItemStatus.Pending)
            return;

        try
        {
            if (workItem.Kind == StoryAssistantWorkItemKind.Question)
            {
                var answer = resolution.Answer.Trim();
                workItem.Question.Answer = answer;
                workItem.Status = StoryAssistantWorkItemStatus.Completed;
                workItem.ResultJson = Output("accepted", new { answer });
                workItem.UpdatedUtc = DateTime.UtcNow;
                await callbacks.UpdateWorkItemAsync(workItem, cancellationToken);
                return;
            }

            if (resolution.Kind == StoryAssistantWorkItemResolutionKind.TryAgain)
            {
                workItem.Status = StoryAssistantWorkItemStatus.RetryRequested;
                workItem.DecisionReason = resolution.Reason.Trim();
                workItem.ResultJson = Output("retry_requested", new { currentEntity = CurrentWorkItemState(document, workItem), userGuidance = workItem.DecisionReason });
                workItem.UpdatedUtc = DateTime.UtcNow;
                await callbacks.UpdateWorkItemAsync(workItem, cancellationToken);
                return;
            }

            if (resolution.Kind == StoryAssistantWorkItemResolutionKind.Reject)
            {
                workItem.Status = StoryAssistantWorkItemStatus.Rejected;
                workItem.DecisionReason = resolution.Reason.Trim();
                workItem.ResultJson = Output("rejected", new { currentEntity = CurrentWorkItemState(document, workItem), rejectionReason = workItem.DecisionReason });
                workItem.UpdatedUtc = DateTime.UtcNow;
                await callbacks.UpdateWorkItemAsync(workItem, cancellationToken);
                return;
            }

            var current = CurrentWorkItemState(document, workItem);
            if (!JsonNode.DeepEquals(current, workItem.Before))
            {
                workItem.Status = StoryAssistantWorkItemStatus.Conflict;
                workItem.DecisionReason = "The story changed after this assistant action was proposed.";
                workItem.ResultJson = Output("conflict", new
                {
                    reason = workItem.DecisionReason,
                    expected = workItem.Before,
                    current
                });
                workItem.UpdatedUtc = DateTime.UtcNow;
                await callbacks.UpdateWorkItemAsync(workItem, cancellationToken);
                return;
            }

            if (workItem.Kind == StoryAssistantWorkItemKind.SceneReview)
            {
                await ResolveAcceptedSceneWorkItemAsync(document, workItem, callbacks, cancellationToken);
                return;
            }

            ApplyAcceptedMutation(document, workItem);
            workItem.Status = StoryAssistantWorkItemStatus.Completed;
            workItem.ResultJson = AcceptedMutationOutput(document, workItem);
            workItem.UpdatedUtc = DateTime.UtcNow;
            await callbacks.SaveEntityAreaAsync(AreaFor(workItem), cancellationToken);
            await callbacks.UpdateWorkItemAsync(workItem, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            workItem.Status = StoryAssistantWorkItemStatus.Failed;
            workItem.DecisionReason = UserFacingErrorMessageBuilder.Build("Resolving Story Assistant action failed.", exception);
            workItem.ResultJson = Output("failed", new { reason = workItem.DecisionReason, currentEntity = CurrentWorkItemState(document, workItem) });
            workItem.UpdatedUtc = DateTime.UtcNow;
            await callbacks.UpdateWorkItemAsync(workItem, cancellationToken);
        }
    }

    async Task ResolveAcceptedSceneWorkItemAsync(
        RpChatDocument document,
        StoryAssistantWorkItem workItem,
        IStoryAssistantCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        var request = SceneRequest(workItem.ArgumentsJson);
        var generated = await callbacks.SetSceneAsync(request, cancellationToken);
        workItem.After = SceneTransitionJsonObject(generated.Plan, document);
        workItem.Diffs = Diff(workItem.Before, workItem.After);
        workItem.Status = StoryAssistantWorkItemStatus.Completed;
        workItem.ResultJson = Output("accepted", new
        {
            entityType = workItem.EntityType,
            entityId = workItem.EntityId,
            resultingScene = workItem.After,
            narratorInstruction = generated.Plan.NarratorInstruction,
            narratorTurnId = generated.NarratorTurnId,
            narratorMessage = generated.NarratorMessage
        });
        workItem.UpdatedUtc = DateTime.UtcNow;
        await callbacks.UpdateWorkItemAsync(workItem, cancellationToken);
    }

    JsonObject CurrentWorkItemState(RpChatDocument document, StoryAssistantWorkItem workItem) => workItem.EntityType switch
    {
        "character" => document.Characters.FirstOrDefault(item => item.Id == workItem.EntityId) is { } character ? CharacterJsonObject(character, document.CharacterTraitLibrary) : new(),
        "relationship" => CurrentRelationshipState(document, workItem),
        "chatDirection" => ChatDirectionRules.JsonObject(document.ChatDirection),
        "location" => document.Locations.FirstOrDefault(item => item.Id == workItem.EntityId) is { } location ? LocationJsonObject(location) : new(),
        "item" => document.Items.FirstOrDefault(item => item.Id == workItem.EntityId) is { } item ? ItemJsonObject(item) : new(),
        "timeline" => document.Timeline.FirstOrDefault(item => item.Id == workItem.EntityId) is { } entry ? TimelineJsonObject(entry) : new(),
        "scene" => SceneJsonObject(TranscriptGraph.GetActiveScene(document.Transcript), document),
        _ => new()
    };

    static JsonObject CurrentRelationshipState(RpChatDocument document, StoryAssistantWorkItem workItem)
    {
        using var json = Parse(workItem.ArgumentsJson);
        var root = json.RootElement;
        var sourceId = String(root, "sourceCharacterId", workItem.EntityId);
        var targetId = String(root, "targetCharacterId");
        var source = document.Characters.FirstOrDefault(item => item.Id == sourceId);
        if (source is null || string.IsNullOrWhiteSpace(targetId))
            return new();

        var target = document.Characters.FirstOrDefault(item => item.Id == targetId);
        return target is null
            ? new()
            : RelationshipJsonObject(document, source.Id, source.Name, target.Id, target.Name);
    }

    void ApplyAcceptedMutation(RpChatDocument document, StoryAssistantWorkItem workItem)
    {
        using var json = Parse(workItem.ArgumentsJson);
        var root = json.RootElement;
        var updates = Updates(root);
        switch (workItem.ToolName)
        {
            case "create_character":
                CharacterProfileRules.ValidateCharacterPatch(updates, document.CharacterTraitLibrary);
                var character = new RpCharacter { Id = workItem.EntityId, Name = "New Character" };
                ApplyCharacter(character, updates);
                CharacterProfileRules.ValidateCreatedCharacter(character);
                document.Characters.Insert(0, character);
                break;
            case "update_character":
                CharacterProfileRules.ValidateCharacterPatch(updates, document.CharacterTraitLibrary);
                var target = document.Characters.First(item => item.Id == workItem.EntityId);
                ApplyCharacter(target, updates);
                if (CharacterProfileRules.HasAppearancePatch(updates))
                    CharacterProfileRules.ValidateCompleteAppearance(target);
                break;
            case "update_chat_direction":
                ChatDirectionRules.ValidatePatch(updates);
                ChatDirectionRules.Apply(document.ChatDirection, updates);
                document.ChatDirection = ChatDirectionService.NormalizeState(document.ChatDirection);
                break;
            case "create_location":
                ValidatePatch(updates, LocationFields, "location");
                document.Locations.Add(Deserialize<RpLocation>(workItem.After));
                break;
            case "update_location":
                ValidatePatch(updates, LocationFields, "location");
                ApplyLocation(document.Locations.First(item => item.Id == workItem.EntityId), updates);
                break;
            case "create_item":
                ValidatePatch(updates, ItemFields, "item");
                document.Items.Add(Deserialize<RpItem>(workItem.After));
                break;
            case "update_item":
                ValidatePatch(updates, ItemFields, "item");
                ApplyItem(document.Items.First(item => item.Id == workItem.EntityId), updates);
                break;
            case "create_timeline_entry":
                ValidatePatch(updates, TimelineFields, "timeline entry");
                document.Timeline.Add(Deserialize<RpTimelineEntry>(workItem.After));
                break;
            case "update_timeline_entry":
                ValidatePatch(updates, TimelineFields, "timeline entry");
                ApplyTimeline(document.Timeline.First(item => item.Id == workItem.EntityId), updates);
                break;
            case "update_character_relationship":
                CharacterProfileRules.ValidateRelationshipPatch(root, document.CharacterTraitLibrary);
                ApplyRelationship(document, root);
                break;
            default:
                throw new InvalidOperationException($"Resolving Story Assistant action failed because '{workItem.ToolName}' is not a supported durable mutation.");
        }
    }

    static void ApplyRelationship(RpChatDocument document, JsonElement root)
    {
        var sourceId = RequiredString(root, "sourceCharacterId");
        var targetId = RequiredString(root, "targetCharacterId");
        CharacterRelationshipGraph.ApplyPatch(document, sourceId, targetId, root);
    }

    static SetSceneRequest SceneRequest(string args)
    {
        using var json = Parse(args);
        return BuildSetSceneRequest(json.RootElement);
    }

    static RoleplayStoreArea AreaFor(StoryAssistantWorkItem workItem) =>
        Enum.TryParse<RoleplayStoreArea>(workItem.EntityArea, out var area) ? area : RoleplayStoreArea.StoryAssistant;

    static T Deserialize<T>(JsonObject value) =>
        JsonSerializer.Deserialize<T>(value.ToJsonString(AppJsonSerializerOptions.Web), AppJsonSerializerOptions.Web)!;

    static StoryAssistantTranscriptItem BaseItem(string callId, string toolName, string title, string args)
    {
        var now = DateTime.UtcNow;
        return new()
        {
            Id = $"assistant-item-{Guid.NewGuid():N}",
            Kind = StoryAssistantItemKind.ToolCall,
            Status = StoryAssistantItemStatus.Pending,
            CreatedUtc = now,
            UpdatedUtc = now,
            ToolCallId = callId,
            ToolName = toolName,
            Title = title,
            ArgumentsJson = args
        };
    }

    static StoryAssistantWorkItem BaseWorkItem(string callId, string toolName, string title, string args, StoryAssistantWorkItemKind kind)
    {
        var now = DateTime.UtcNow;
        return new()
        {
            Id = $"assistant-work-{Guid.NewGuid():N}",
            TranscriptItemId = $"assistant-item-{Guid.NewGuid():N}",
            Kind = kind,
            Status = StoryAssistantWorkItemStatus.Pending,
            CreatedUtc = now,
            UpdatedUtc = now,
            ToolCallId = callId,
            ToolName = toolName,
            Title = title,
            ArgumentsJson = args
        };
    }

    static StoryAssistantWorkItem WorkItemFromTranscriptItem(StoryAssistantTranscriptItem item, StoryAssistantWorkItemKind kind, RoleplayStoreArea area) => new()
    {
        Id = $"assistant-work-{Guid.NewGuid():N}",
        TranscriptItemId = item.Id,
        Kind = kind,
        Status = StoryAssistantWorkItemStatus.Pending,
        CreatedUtc = item.CreatedUtc,
        UpdatedUtc = DateTime.UtcNow,
        Title = item.Title,
        ToolName = item.ToolName,
        ToolCallId = item.ToolCallId,
        EntityArea = area.ToString(),
        Operation = item.Operation,
        EntityType = item.EntityType,
        EntityId = item.EntityId,
        EntityName = item.EntityName,
        ArgumentsJson = item.ArgumentsJson,
        Before = item.Before,
        After = item.After,
        Diffs = item.Diffs,
        Risk = item.Risk,
        Question = item.Question,
        Diagnostics = item.Diagnostics
    };

    static StoryAssistantTranscriptItem MutationItem(string callId, string toolName, StoryAssistantOperationKind operation, string title, string entityType, string entityId, string entityName, string args, JsonObject before, JsonObject after, StoryAssistantChangeRisk risk)
    {
        var item = BaseItem(callId, toolName, title, args);
        item.Operation = operation;
        item.EntityType = entityType;
        item.EntityId = entityId;
        item.EntityName = entityName;
        item.Before = before;
        item.After = after;
        item.Risk = risk;
        return item;
    }

    static bool ShouldReview(StoryAssistantReviewMode mode, StoryAssistantChangeRisk risk) => mode switch
    {
        StoryAssistantReviewMode.ReviewAll => true,
        StoryAssistantReviewMode.ReviewMajor => risk is StoryAssistantChangeRisk.Major or StoryAssistantChangeRisk.Destructive or StoryAssistantChangeRisk.Blocked,
        StoryAssistantReviewMode.AutoApprove => risk is StoryAssistantChangeRisk.Destructive or StoryAssistantChangeRisk.Blocked,
        _ => true
    };

    static JsonDocument Parse(string json) => JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
    static JsonElement Updates(JsonElement root) => root.TryGetProperty("updates", out var updates) && updates.ValueKind == JsonValueKind.Object ? updates : root;
    static string RequiredString(JsonElement root, string name) => String(root, name) is { Length: > 0 } value ? value : throw new InvalidOperationException($"The tool call was missing '{name}'.");
    static string RequiredEntityId(JsonElement root) => String(root, "entityId") is { Length: > 0 } value ? value : throw new StoryAssistantEntityLookupException("The tool call was missing 'entityId'.");
    static string String(JsonElement root, string name, string fallback = "") => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : fallback;
    static bool Bool(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    static int Int(JsonElement root, string name, int fallback) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : fallback;
    static SceneNarratorGuidance ParseNarratorGuidance(JsonElement root)
    {
        if (!root.TryGetProperty("narratorGuidance", out var guidanceElement) || guidanceElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("The tool call was missing 'narratorGuidance'.");

        var purpose = RequiredString(guidanceElement, "purpose");
        return new(ParseNarratorPurpose(purpose), RequiredString(guidanceElement, "guidance"));
    }

    static SceneNarratorGuidancePurpose ParseNarratorPurpose(string value) => value.Trim() switch
    {
        "opening_scene" => SceneNarratorGuidancePurpose.OpeningScene,
        "location_transition" => SceneNarratorGuidancePurpose.LocationTransition,
        "time_skip" => SceneNarratorGuidancePurpose.TimeSkip,
        "scene_reset" => SceneNarratorGuidancePurpose.SceneReset,
        _ => throw new InvalidOperationException($"The tool call had unsupported narrator guidance purpose '{value}'.")
    };

    static List<string> StringList(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
        ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? "").Where(item => !string.IsNullOrWhiteSpace(item)).ToList()
        : [];

    static void ValidatePatch(JsonElement updates, IReadOnlyCollection<string> allowedFields, string entityName)
    {
        if (updates.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"The {entityName} patch must provide an updates object.");

        foreach (var property in updates.EnumerateObject())
            if (!allowedFields.Contains(property.Name, StringComparer.Ordinal))
                throw new InvalidOperationException($"The {entityName} patch contains unsupported field '{property.Name}'. Call get_story_entities if you need the current entity shape, then retry with only supported fields.");
    }

    static void RequirePatchString(JsonElement updates, string field, string action)
    {
        if (String(updates, field) is { Length: > 0 })
            return;

        throw new InvalidOperationException($"{action} failed because updates.{field} is required.");
    }

    static void ApplyCharacter(RpCharacter target, JsonElement updates)
    {
        Set(updates, "name", value => target.Name = value);
        Set(updates, "summary", value => target.Summary = value);
        Set(updates, "personality", value => target.Personality = value);
        Set(updates, CharacterProfileRules.ExtraAppearanceDetailsField, value => target.Appearance = value);
        SetAppearanceFields(updates, target.AppearanceProfile);
        Set(updates, "backstory", value => target.Backstory = value);
        Set(updates, "voice", value => target.Voice = value);
        Set(updates, "notes", value => target.Notes = value);
        SetList(updates, "pronouns", value => target.Pronouns = value);
        Set(updates, "coreDrive", value => target.CoreDrive = value);
        Set(updates, "coreFear", value => target.CoreFear = value);
        Set(updates, "surfaceMask", value => target.SurfaceMask = value);
        Set(updates, "hiddenTruth", value => target.HiddenTruth = value);
        Set(updates, "sentenceStyle", value => target.SentenceStyle = value);
        Set(updates, "honestyStyle", value => target.HonestyStyle = value);
        Set(updates, "emotionalLeakage", value => target.EmotionalLeakage = value);
        Set(updates, "actionFingerprint", value => target.ActionFingerprint = value);
        Set(updates, "stressPattern", value => target.StressPattern = value);
        SetList(updates, "sceneRoles", value => target.SceneRoles = value);
        SetList(updates, "traits", value => target.Traits = value);
        SetList(updates, "drives", value => target.Drives = value);
        SetList(updates, "limits", value => target.Limits = value);
        SetList(updates, "softSpots", value => target.SoftSpots = value);
        SetList(updates, "avoidPatterns", value => target.AvoidPatterns = value);
    }

    static void SetAppearanceFields(JsonElement root, CharacterAppearanceState target)
    {
        Set(root, "hairColor", text => target.HairColor = text);
        SetList(root, "hairStyles", list => target.HairStyles = list);
        Set(root, "eyeColor", text => target.EyeColor = text);
        Set(root, "faceShape", text => target.FaceShape = text);
        Set(root, "skinTone", text => target.SkinTone = text);
        SetList(root, "complexion", list => target.Complexion = list);
        Set(root, "height", text => target.Height = text);
        Set(root, "build", text => target.Build = text);
        SetList(root, "bodyProportions", list => target.BodyProportions = list);
        SetList(root, "presentation", list => target.Presentation = list);
        Set(root, "attractiveness", text => target.Attractiveness = text);
    }

    static void ApplyLocation(RpLocation target, JsonElement updates)
    {
        Set(updates, "name", value => target.Name = value);
        Set(updates, "summary", value => target.Summary = value);
        Set(updates, "description", value => target.Description = value);
        Set(updates, "atmosphere", value => target.Atmosphere = value);
        Set(updates, "features", value => target.Features = value);
    }

    static void ApplyItem(RpItem target, JsonElement updates)
    {
        Set(updates, "name", value => target.Name = value);
        Set(updates, "summary", value => target.Summary = value);
        Set(updates, "description", value => target.Description = value);
        Set(updates, "history", value => target.History = value);
        Set(updates, "properties", value => target.Properties = value);
    }

    static void ApplyTimeline(RpTimelineEntry target, JsonElement updates)
    {
        Set(updates, "title", value => target.Title = value);
        Set(updates, "date", value => target.Date = value);
        Set(updates, "description", value => target.Description = value);
        Set(updates, "significance", value => target.Significance = value);
        SetList(updates, "characters", value => target.Characters = value);
    }

    static void Set(JsonElement root, string name, Action<string> setter)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            setter(value.GetString() ?? "");
    }

    static void SetList(JsonElement root, string name, Action<List<string>> setter)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
            setter(value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? "").Where(item => !string.IsNullOrWhiteSpace(item)).ToList());
    }

    static List<StoryAssistantFieldDiff> Diff(JsonObject before, JsonObject after)
    {
        var fields = before.Select(pair => pair.Key).Concat(after.Select(pair => pair.Key)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal);
        var diffs = new List<StoryAssistantFieldDiff>();
        foreach (var field in fields)
        {
            var beforeText = before.TryGetPropertyValue(field, out var beforeValue) ? FormatDiffValue(beforeValue) : "";
            var afterText = after.TryGetPropertyValue(field, out var afterValue) ? FormatDiffValue(afterValue) : "";
            if (beforeText != afterText)
                diffs.Add(new() { Field = field, Label = LabelFor(field), Before = beforeText, After = afterText });
        }

        return diffs;
    }

    static string LabelFor(string field) => field switch
    {
        "noteAtoB" => "How source sees target",
        "noteBtoA" => "How target sees source",
        "noteExternal" => "Public dynamic",
        "howSourceSeesTarget" => "How Source Sees Target",
        "howTargetSeesSource" => "How Target Sees Source",
        "publicDynamic" => "Public Dynamic",
        "relationshipTypes" => "Relationship Types",
        "privateTensions" => "Private Tensions",
        "sourceCharacterName" => "Source Character",
        "targetCharacterName" => "Target Character",
        "extraAppearanceDetails" => "Extra Appearance Details",
        "hairColor" => "Hair Color",
        "hairStyles" => "Hair Styles",
        "eyeColor" => "Eye Color",
        "faceShape" => "Face Shape",
        "skinTone" => "Skin Tone",
        "complexion" => "Complexion",
        "height" => "Height",
        "build" => "Build",
        "bodyProportions" => "Body Proportions",
        "presentation" => "Presentation",
        "attractiveness" => "Attractiveness",
        _ => string.Concat(field.Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {ch}" : ch.ToString()))
    };

    public static string FormatDiffValue(JsonNode? node) => FormatDiffValue(node, 0);

    static string FormatDiffValue(JsonNode? node, int indent) => node switch
    {
        null => "",
        JsonValue value => JsonValueText(value),
        JsonArray array => ArrayText(array, indent),
        JsonObject obj => ObjectText(obj, indent),
        _ => node.ToJsonString(AppJsonSerializerOptions.Web)
    };

    static string ArrayText(JsonArray array, int indent)
    {
        var prefix = new string(' ', indent);
        return string.Join('\n', array.Select(item => $"{prefix}- {NestedValueText(item, indent + 2)}"));
    }

    static string ObjectText(JsonObject obj, int indent)
    {
        var prefix = new string(' ', indent);
        return string.Join('\n', obj.Select(pair => $"{prefix}{pair.Key}: {NestedValueText(pair.Value, indent + 2)}"));
    }

    static string NestedValueText(JsonNode? node, int indent)
    {
        if (node is JsonArray or JsonObject)
            return $"\n{FormatDiffValue(node, indent)}";

        return FormatDiffValue(node, indent);
    }

    static string JsonValueText(JsonValue value)
    {
        if (value.TryGetValue<JsonElement>(out var element))
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "",
                JsonValueKind.Null => "",
                _ => value.ToJsonString(AppJsonSerializerOptions.Web).Trim('"')
            };

        if (value.TryGetValue<string>(out var text))
            return text;

        if (value.TryGetValue<bool>(out var boolean))
            return boolean.ToString();

        if (value.TryGetValue<int>(out var integer))
            return integer.ToString();

        if (value.TryGetValue<long>(out var longInteger))
            return longInteger.ToString();

        if (value.TryGetValue<double>(out var number))
            return number.ToString();

        return value.ToJsonString(AppJsonSerializerOptions.Web).Trim('"');
    }

    public static JsonObject ToJsonObject<T>(T value) =>
        JsonSerializer.SerializeToNode(value, AppJsonSerializerOptions.Web)?.AsObject() ?? new();

    static JsonObject CharacterJsonObject(RpCharacter character, CharacterTraitLibraryState library) => ToJsonObject(CharacterShape(character, CharacterTraitLibraryService.NormalizeState(library)));

    static JsonObject RelationshipJsonObject(RpChatDocument document, string sourceId, string sourceName, string targetId, string targetName) =>
        ToJsonObject(RelationshipShape(CharacterRelationshipGraph.View(document, sourceId, sourceName, targetId, targetName), sourceId, sourceName, targetId, targetName));

    static JsonObject LocationJsonObject(RpLocation location) => ToJsonObject(LocationShape(location));
    static JsonObject ItemJsonObject(RpItem item) => ToJsonObject(ItemShape(item));
    static JsonObject TimelineJsonObject(RpTimelineEntry entry) => ToJsonObject(TimelineShape(entry));
    static JsonObject SceneJsonObject(RpSceneFrame scene, RpChatDocument document) => ToJsonObject(new
    {
        locationId = scene.LocationId,
        locationName = ResolveLocationName(scene, document),
        characterIds = scene.InSceneCharacterIds,
        characters = ResolveNames(scene.InSceneCharacterIds, document.Characters.Select(character => (character.Id, character.Name))),
        itemIds = scene.InSceneItemIds,
        items = ResolveNames(scene.InSceneItemIds, document.Items.Select(item => (item.Id, item.Name)))
    });

    static JsonObject SceneTransitionJsonObject(SceneTransitionPlan transition, RpChatDocument document) => ToJsonObject(new
    {
        locationId = transition.TargetScene.LocationId,
        locationName = ResolveLocationName(transition.TargetScene, document),
        characterIds = transition.TargetScene.InSceneCharacterIds,
        characters = ResolveNames(transition.TargetScene.InSceneCharacterIds, document.Characters.Select(character => (character.Id, character.Name))),
        itemIds = transition.TargetScene.InSceneItemIds,
        items = ResolveNames(transition.TargetScene.InSceneItemIds, document.Items.Select(item => (item.Id, item.Name)))
    });

    static string ResolveLocationName(RpSceneFrame scene, RpChatDocument document) =>
        document.Locations.FirstOrDefault(location => location.Id == scene.LocationId)?.Name ?? scene.LocationName;

    static List<string> ResolveNames(IEnumerable<string> ids, IEnumerable<(string Id, string Name)> entities)
    {
        var byId = entities.ToDictionary(pair => pair.Id, pair => pair.Name, StringComparer.Ordinal);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    static StoryAssistantChangeRisk SceneTransitionRisk(SceneTransitionPlan transition) =>
        RequiresSceneReview(transition) ? StoryAssistantChangeRisk.Major : StoryAssistantChangeRisk.Low;

    static bool RequiresSceneReview(SceneTransitionPlan transition) =>
        transition.IsOpeningScene
        || transition.IsLocationTransition
        || transition.IsTimeSkip
        || transition.IsSceneReset
        || transition.RemovedCharacters.Count > 0
        || transition.RemovedItems.Count > 0;

    static string Output(string status, object value)
    {
        var node = JsonSerializer.SerializeToNode(value, AppJsonSerializerOptions.Web)?.AsObject() ?? new();
        node["status"] = status;
        return node.ToJsonString(AppJsonSerializerOptions.Web);
    }

    static T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, AppJsonSerializerOptions.Web), AppJsonSerializerOptions.Web)!;

    static void Copy<T>(T source, T target)
    {
        var json = JsonSerializer.Serialize(source, AppJsonSerializerOptions.Web);
        var clone = JsonSerializer.Deserialize<T>(json, AppJsonSerializerOptions.Web)!;
        foreach (var property in typeof(T).GetProperties().Where(property => property.CanRead && property.CanWrite))
            property.SetValue(target, property.GetValue(clone));
    }

    static string NextId(IEnumerable<string> ids, string prefix)
    {
        var next = ids
            .Where(id => id.Length > prefix.Length && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(id[prefix.Length..], out _))
            .Select(id => int.Parse(id[prefix.Length..]))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{prefix}{next}";
    }
}

public sealed class StoryAssistantEntityLookupException(string message) : InvalidOperationException(message);
