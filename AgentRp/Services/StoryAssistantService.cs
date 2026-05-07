using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Serialization;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed record StoryAssistantTurnRequest(string UserMessage);

public interface IStoryAssistantCallbacks
{
    Task AppendAssistantTextAsync(string delta, CancellationToken cancellationToken);
    Task RecordToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken);
    Task UpdateToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken);
    Task<StoryAssistantDecision> ReviewChangeAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken);
    Task<string> AskQuestionAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken);
    Task<SceneTransitionResult> GenerateSceneTransitionAsync(SceneTransitionRequest request, CancellationToken cancellationToken);
    Task SaveEntityAreaAsync(RoleplayStoreArea area, CancellationToken cancellationToken);
    Task SaveAssistantStateAsync(CancellationToken cancellationToken);
}

public interface IStoryAssistantService
{
    Task RunTurnAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        StoryAssistantTurnRequest request,
        IStoryAssistantCallbacks callbacks,
        CancellationToken cancellationToken = default);

    Task ClearRemoteStateAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
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
        StoryAssistantTurnRequest request,
        IStoryAssistantCallbacks callbacks,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var selection = TextModelTuningCatalog.TryResolveActiveReasoningModel(providers, document.ActiveModelSelections)
            ?? throw new InvalidOperationException("Starting the Story Assistant failed because no reasoning model is enabled.");
        if (!selection.Capabilities.CanGenerateText || !selection.Capabilities.Tools)
            throw new InvalidOperationException($"Starting the Story Assistant failed because reasoning model '{selection.Model.Id}' must support text and tools.");

        if (!ResponseChainMatches(document.StoryAssistant, selection))
        {
            await ClearRemoteStateAsync(document, providers, cancellationToken);
            ClearResponseChain(document.StoryAssistant);
            await callbacks.SaveAssistantStateAsync(cancellationToken);
        }

        document.StoryAssistant.RemoteThreadLost = false;
        document.StoryAssistant.RemoteThreadError = "";
        var inputs = new List<ModelAssistantInput> { new(ModelAssistantInputKind.UserMessage, request.UserMessage.Trim()) };
        for (var pass = 0; pass < 16; pass++)
        {
            var toolOutputs = new List<ModelAssistantInput>();
            await foreach (var update in generationClient.GenerateAssistantStreamingAsync(new(
                selection.Provider,
                selection.Model,
                selection.Capabilities,
                new(),
                Instructions(),
                document.StoryAssistant.LastResponseId,
                inputs,
                BuildTools(document),
                "Running Story Assistant"), cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (update.Kind == ModelAssistantStreamingUpdateKind.TextDelta)
                    await callbacks.AppendAssistantTextAsync(update.TextDelta, cancellationToken);
                else if (update.Kind == ModelAssistantStreamingUpdateKind.ToolCall)
                {
                    var output = await patchService.ExecuteAsync(document, update.ToolCallId, update.ToolName, update.ToolArgumentsJson, callbacks, cancellationToken);
                    toolOutputs.Add(new(ModelAssistantInputKind.FunctionCallOutput, output, update.ToolCallId));
                }
                else if (update.Kind == ModelAssistantStreamingUpdateKind.Completed)
                {
                    if (!string.IsNullOrWhiteSpace(update.ResponseId))
                        RecordResponse(document.StoryAssistant, selection, update.ResponseId);

                    await callbacks.SaveAssistantStateAsync(cancellationToken);
                }
            }

            if (toolOutputs.Count == 0)
                return;

            inputs = toolOutputs;
        }

        throw new InvalidOperationException("Running the Story Assistant stopped because too many tool rounds were requested in one turn.");
    }

    public async Task ClearRemoteStateAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
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

    static string Instructions() => """
You are the Story Entities Assistant for AgentRp. Help the user bootstrap and maintain story canon through concise collaboration.

Use tools for durable changes. Prefer partial updates: only send fields you intend to change. Never resend a whole existing entity unless creating it.
Ask focused questions when a choice materially changes story direction. Prefer 1-3 multiple-choice options; use an open-ended question when choices would over-constrain the user.
When editing relationships, treat them as directional. Use clear thinking like "how Character A sees Character B" and "how Character B sees Character A".
Before setting controlled character profile fields, call get_character_profile_options for the fields you need. If a character tool fails with nextStep.tool = get_character_profile_options, call it before retrying.
Before setting controlled chat direction fields, call get_chat_direction_options for the fields you need. If a chat direction tool fails with nextStep.tool = get_chat_direction_options, call it before retrying.
Use set_scene only for opening scenes, user-requested fast-forwards, location transitions, or explicit scene resets. The set_scene tool stages existing canon only; call get_story_entities first if any ids are uncertain, and create missing canon with existing entity tools or ask the user before setting the scene.
Do not use set_scene to resolve major plot outcomes, relationship changes, defeats, losses, off-screen decisions, or irreversible consequences unless the user explicitly requested those outcomes. If unsure whether a change is staging or a plot consequence, ask the user.
When using set_scene, provide state and intent only. Preserve narrator creative freedom; do not write the scene prose yourself.
Before making a broad or identity-level change, briefly explain the intent and then use a tool. The app will show every tool call to the user for audit.
""";
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
        try
        {
            return toolName switch
            {
                "get_story_entities" => await ReadToolAsync(toolCallId, toolName, "Read story entities", argumentsJson, callbacks, new { entities = BuildEntities(document) }, cancellationToken),
                "get_story_transcript" => await ReadToolAsync(toolCallId, toolName, "Read story transcript", argumentsJson, callbacks, new { transcript = BuildTranscript(document) }, cancellationToken),
                "get_character_profile_options" => await ReadProfileOptionsAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "get_chat_direction_options" => await ReadChatDirectionOptionsAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "ask_user" => await AskUserAsync(toolCallId, argumentsJson, callbacks, cancellationToken),
                "create_character" => await CreateCharacterAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "update_character" => await UpdateCharacterAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "update_chat_direction" => await UpdateChatDirectionAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "create_location" => await CreateLocationAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "update_location" => await UpdateLocationAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "create_item" => await CreateItemAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "update_item" => await UpdateItemAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "create_timeline_entry" => await CreateTimelineAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "update_timeline_entry" => await UpdateTimelineAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "update_character_relationship" => await UpdateRelationshipAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "set_scene" => await SetSceneAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                _ => Output("failed", new { reason = $"Unknown tool '{toolName}'." })
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CharacterProfileValidationException exception)
        {
            return Output("failed", new
            {
                reason = exception.Message,
                nextStep = new
                {
                    tool = "get_character_profile_options",
                    fields = exception.Fields,
                    instruction = "Call get_character_profile_options for the invalid field, then retry with valid ids and limits."
                }
            });
        }
        catch (ChatDirectionValidationException exception)
        {
            return Output("failed", new
            {
                reason = exception.Message,
                nextStep = new
                {
                    tool = "get_chat_direction_options",
                    fields = exception.Fields,
                    instruction = "Call get_chat_direction_options for the invalid field, then retry with valid ids, limits, and intensity values."
                }
            });
        }
        catch (StoryAssistantEntityLookupException exception)
        {
            return Output("failed", new
            {
                reason = exception.Message,
                nextStep = new
                {
                    tool = "get_story_entities",
                    instruction = "Call get_story_entities, choose the correct entity id from the result, then retry with that entityId."
                }
            });
        }
        catch (SceneTransitionValidationException exception)
        {
            return Output("failed", new
            {
                reason = exception.Message,
                nextStep = new
                {
                    tool = "get_story_entities",
                    instruction = "Call get_story_entities, choose existing canon ids, create missing canon with the appropriate entity tool or ask the user, then retry set_scene."
                }
            });
        }
        catch (Exception exception)
        {
            return Output("failed", new { reason = exception.Message });
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
            relationships = document.Characters.Select(character => new
            {
                character.Id,
                character.Name,
                relationships = character.ProfileRelationships.Select(relationship => new
                {
                    sourceCharacterId = character.Id,
                    targetCharacterId = relationship.CharacterId,
                    howSourceSeesTarget = relationship.NoteAtoB,
                    howTargetSeesSource = relationship.NoteBtoA,
                    publicDynamic = relationship.NoteExternal,
                    relationship.Bonds,
                    relationship.Dynamics
                })
            })
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
        character.Appearance,
        character.AppearanceProfile,
        AppearanceSummary = CharacterAppearanceFormatter.FormatBase(character, library),
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

    async Task<string> AskUserAsync(string callId, string argumentsJson, IStoryAssistantCallbacks callbacks, CancellationToken cancellationToken)
    {
        using var doc = Parse(argumentsJson);
        var root = doc.RootElement;
        var item = BaseItem(callId, "ask_user", "Question", argumentsJson);
        item.Kind = StoryAssistantItemKind.Question;
        item.Status = StoryAssistantItemStatus.Pending;
        item.Operation = StoryAssistantOperationKind.Question;
        item.Question.Prompt = String(root, "prompt");
        item.Question.AllowsFreeform = Bool(root, "allowsFreeform");
        item.Question.Choices = root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array
            ? choices.EnumerateArray().Select((choice, index) => new StoryAssistantQuestionChoice
            {
                Id = String(choice, "id", $"choice-{index + 1}"),
                Label = String(choice, "label")
            }).Where(choice => !string.IsNullOrWhiteSpace(choice.Label)).ToList()
            : [];
        var answer = await callbacks.AskQuestionAsync(item, cancellationToken);
        return Output("accepted", new { answer });
    }

    async Task<string> CreateCharacterAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var character = new RpCharacter { Id = NextId(document.Characters.Select(item => item.Id), "c"), Name = "New Character" };
        var updates = Updates(json.RootElement);
        CharacterProfileRules.ValidateCharacterPatch(updates, document.CharacterTraitLibrary);
        ApplyCharacter(character, updates);
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Create, $"Create {character.Name}", "character", character.Id, character.Name, args, new(), CharacterJsonObject(character, document.CharacterTraitLibrary), StoryAssistantChangeRisk.Low);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Characters, () => document.Characters.Insert(0, character), new(), token);
    }

    async Task<string> UpdateCharacterAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var id = RequiredEntityId(json.RootElement);
        var existing = document.Characters.FirstOrDefault(item => item.Id == id) ?? throw new StoryAssistantEntityLookupException($"No character with id '{id}' exists.");
        var before = Clone(existing);
        var after = Clone(existing);
        var updates = Updates(json.RootElement);
        CharacterProfileRules.ValidateCharacterPatch(updates, document.CharacterTraitLibrary);
        ApplyCharacter(after, updates);
        var risk = updates.TryGetProperty("name", out _) || updates.TryGetProperty("backstory", out _) ? StoryAssistantChangeRisk.Major : StoryAssistantChangeRisk.Low;
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Update, $"Update {after.Name}", "character", id, after.Name, args, CharacterJsonObject(before, document.CharacterTraitLibrary), CharacterJsonObject(after, document.CharacterTraitLibrary), risk);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Characters, () => Copy(after, existing), CharacterJsonObject(existing, document.CharacterTraitLibrary), token);
    }

    async Task<string> UpdateChatDirectionAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
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

    async Task<string> CreateLocationAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
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

    async Task<string> UpdateLocationAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
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

    async Task<string> CreateItemAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
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

    async Task<string> UpdateItemAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
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

    async Task<string> CreateTimelineAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
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

    async Task<string> UpdateTimelineAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
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

    async Task<string> UpdateRelationshipAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var sourceId = RequiredString(json.RootElement, "sourceCharacterId");
        var targetId = RequiredString(json.RootElement, "targetCharacterId");
        var source = document.Characters.FirstOrDefault(item => item.Id == sourceId) ?? throw new StoryAssistantEntityLookupException($"No source character with id '{sourceId}' exists.");
        var target = document.Characters.FirstOrDefault(item => item.Id == targetId) ?? throw new StoryAssistantEntityLookupException($"No target character with id '{targetId}' exists.");
        CharacterProfileRules.ValidateRelationshipPatch(json.RootElement, document.CharacterTraitLibrary);
        var before = Clone(source);
        var after = Clone(source);
        var relationship = after.ProfileRelationships.FirstOrDefault(item => item.CharacterId == targetId);
        if (relationship is null)
        {
            relationship = new() { CharacterId = targetId };
            after.ProfileRelationships.Add(relationship);
        }

        relationship.NoteAtoB = String(json.RootElement, "howSourceSeesTarget", relationship.NoteAtoB);
        relationship.NoteBtoA = String(json.RootElement, "howTargetSeesSource", relationship.NoteBtoA);
        relationship.NoteExternal = String(json.RootElement, "publicDynamic", relationship.NoteExternal);
        AddDistinct(relationship.Dynamics, String(json.RootElement, "privateTension"));
        AddDistinct(relationship.Bonds, String(json.RootElement, "relationshipType"));

        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Update, $"Update {source.Name} and {target.Name}", "relationship", source.Id, source.Name, args, RelationshipJsonObject(before), RelationshipJsonObject(after), StoryAssistantChangeRisk.Major);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Characters, () => Copy(after, source), RelationshipJsonObject(source), token);
    }

    async Task<string> SetSceneAsync(RpChatDocument document, string callId, string toolName, string args, IStoryAssistantCallbacks callbacks, CancellationToken token)
    {
        using var json = Parse(args);
        var root = json.RootElement;
        var request = new SceneTransitionRequest(
            RequiredString(root, "locationId"),
            StringList(root, "characterIds"),
            StringList(root, "itemIds"),
            String(root, "elapsedTime"),
            String(root, "transitionNote"),
            String(root, "reason"));
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

    async Task<string> ResolveMutationAsync(
        RpChatDocument document,
        StoryAssistantTranscriptItem item,
        IStoryAssistantCallbacks callbacks,
        RoleplayStoreArea area,
        Action apply,
        JsonObject currentEntity,
        CancellationToken cancellationToken)
    {
        item.Diffs = Diff(item.Before, item.After);
        await callbacks.RecordToolCallAsync(item, cancellationToken);
        var shouldReview = ShouldReview(document.StoryAssistant.ReviewMode, item.Risk);
        if (shouldReview)
        {
            item.Status = StoryAssistantItemStatus.NeedsReview;
            await callbacks.UpdateToolCallAsync(item, cancellationToken);
            var decision = await callbacks.ReviewChangeAsync(item, cancellationToken);
            item.DecisionReason = decision.Reason;
            if (decision.Kind == StoryAssistantDecisionKind.TryAgain)
            {
                item.Status = StoryAssistantItemStatus.RetryRequested;
                await callbacks.UpdateToolCallAsync(item, cancellationToken);
                return Output("retry_requested", new { currentEntity, userGuidance = decision.Reason });
            }

            if (decision.Kind == StoryAssistantDecisionKind.Reject)
            {
                item.Status = StoryAssistantItemStatus.Rejected;
                await callbacks.UpdateToolCallAsync(item, cancellationToken);
                return Output("rejected", new { currentEntity, rejectionReason = decision.Reason });
            }
        }

        apply();
        item.Status = shouldReview ? StoryAssistantItemStatus.Accepted : StoryAssistantItemStatus.Applied;
        await callbacks.SaveEntityAreaAsync(area, cancellationToken);
        await callbacks.UpdateToolCallAsync(item, cancellationToken);
        return Output("accepted", new { entityType = item.EntityType, entityId = item.EntityId, resultingEntity = item.After });
    }

    async Task<string> ResolveSceneTransitionAsync(
        RpChatDocument document,
        StoryAssistantTranscriptItem item,
        IStoryAssistantCallbacks callbacks,
        SceneTransitionRequest request,
        SceneTransitionPlan transition,
        CancellationToken cancellationToken)
    {
        item.Diffs = Diff(item.Before, item.After);
        await callbacks.RecordToolCallAsync(item, cancellationToken);
        var shouldReview = RequiresSceneReview(transition) || ShouldReview(document.StoryAssistant.ReviewMode, item.Risk);
        if (shouldReview)
        {
            item.Status = StoryAssistantItemStatus.NeedsReview;
            await callbacks.UpdateToolCallAsync(item, cancellationToken);
            var decision = await callbacks.ReviewChangeAsync(item, cancellationToken);
            item.DecisionReason = decision.Reason;
            if (decision.Kind == StoryAssistantDecisionKind.TryAgain)
            {
                item.Status = StoryAssistantItemStatus.RetryRequested;
                await callbacks.UpdateToolCallAsync(item, cancellationToken);
                return Output("retry_requested", new { currentScene = item.Before, userGuidance = decision.Reason });
            }

            if (decision.Kind == StoryAssistantDecisionKind.Reject)
            {
                item.Status = StoryAssistantItemStatus.Rejected;
                await callbacks.UpdateToolCallAsync(item, cancellationToken);
                return Output("rejected", new { currentScene = item.Before, rejectionReason = decision.Reason });
            }
        }

        try
        {
            var generated = await callbacks.GenerateSceneTransitionAsync(request, cancellationToken);
            item.After = SceneTransitionJsonObject(generated.Plan, document);
            item.Diffs = Diff(item.Before, item.After);
            item.Status = shouldReview ? StoryAssistantItemStatus.Accepted : StoryAssistantItemStatus.Applied;
            await callbacks.UpdateToolCallAsync(item, cancellationToken);
            return Output("accepted", new
            {
                entityType = item.EntityType,
                entityId = item.EntityId,
                resultingScene = item.After,
                narratorInstruction = generated.Plan.NarratorInstruction,
                narratorTurnId = generated.NarratorTurnId,
                narratorMessage = generated.NarratorMessage
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            item.Status = StoryAssistantItemStatus.Failed;
            item.DecisionReason = UserFacingErrorMessageBuilder.Build("Setting the scene failed.", exception);
            await callbacks.UpdateToolCallAsync(item, cancellationToken);
            return Output("failed", new { reason = item.DecisionReason, currentScene = item.Before });
        }
    }

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
        Set(updates, "appearance", value => target.Appearance = value);
        SetAppearanceProfile(updates, target.AppearanceProfile);
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

    static void SetAppearanceProfile(JsonElement root, CharacterAppearanceState target)
    {
        if (!root.TryGetProperty("appearanceProfile", out var value) || value.ValueKind != JsonValueKind.Object)
            return;

        Set(value, "hairColor", text => target.HairColor = text);
        SetList(value, "hairStyles", list => target.HairStyles = list);
        Set(value, "eyeColor", text => target.EyeColor = text);
        Set(value, "faceShape", text => target.FaceShape = text);
        Set(value, "skinTone", text => target.SkinTone = text);
        SetList(value, "complexion", list => target.Complexion = list);
        Set(value, "height", text => target.Height = text);
        Set(value, "build", text => target.Build = text);
        SetList(value, "bodyProportions", list => target.BodyProportions = list);
        SetList(value, "presentation", list => target.Presentation = list);
        Set(value, "attractiveness", text => target.Attractiveness = text);
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

    static void AddDistinct(List<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value, StringComparer.OrdinalIgnoreCase))
            values.Add(value);
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
    static JsonObject RelationshipJsonObject(RpCharacter character) => ToJsonObject(new
    {
        character.Id,
        character.Name,
        character.ProfileRelationships
    });

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
        isOpeningScene = transition.IsOpeningScene,
        isLocationTransition = transition.IsLocationTransition,
        isTimeSkip = transition.IsTimeSkip,
        locationId = transition.TargetScene.LocationId,
        locationName = ResolveLocationName(transition.TargetScene, document),
        characterIds = transition.TargetScene.InSceneCharacterIds,
        characters = ResolveNames(transition.TargetScene.InSceneCharacterIds, document.Characters.Select(character => (character.Id, character.Name))),
        itemIds = transition.TargetScene.InSceneItemIds,
        items = ResolveNames(transition.TargetScene.InSceneItemIds, document.Items.Select(item => (item.Id, item.Name))),
        addedCharacters = transition.AddedCharacters.Select(item => item.Name),
        removedCharacters = transition.RemovedCharacters.Select(item => item.Name),
        stayingCharacters = transition.StayingCharacters.Select(item => item.Name),
        addedItems = transition.AddedItems.Select(item => item.Name),
        removedItems = transition.RemovedItems.Select(item => item.Name),
        stayingItems = transition.StayingItems.Select(item => item.Name)
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
