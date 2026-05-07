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
}

public sealed class StoryAssistantService(
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

        if (string.IsNullOrWhiteSpace(document.StoryAssistant.ConversationId))
        {
            document.StoryAssistant.ConversationId = await generationClient.CreateAssistantConversationAsync(selection.Provider, selection.Model, cancellationToken);
            await callbacks.SaveAssistantStateAsync(cancellationToken);
        }

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
                document.StoryAssistant.ConversationId,
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
                    if (!string.IsNullOrWhiteSpace(update.ConversationId) && update.ConversationId != document.StoryAssistant.ConversationId)
                    {
                        document.StoryAssistant.ConversationId = update.ConversationId;
                        await callbacks.SaveAssistantStateAsync(cancellationToken);
                    }
                }
            }

            if (toolOutputs.Count == 0)
                return;

            inputs = toolOutputs;
        }

        throw new InvalidOperationException("Running the Story Assistant stopped because too many tool rounds were requested in one turn.");
    }

    void ApplyCapabilities(IReadOnlyList<AiProvider> providers)
    {
        foreach (var provider in providers)
            capabilityCatalog.ApplyResolvedCapabilities(provider);
    }

    static string Instructions() => """
You are the Story Entities Assistant for AgentRp. Help the user bootstrap and maintain story canon through concise collaboration.

Use tools for durable changes. Prefer partial updates: only send fields you intend to change. Never resend a whole existing entity unless creating it.
Ask focused questions when a choice materially changes story direction. Prefer 1-3 multiple-choice options; use an open-ended question when choices would over-constrain the user.
When editing relationships, treat them as directional. Use clear thinking like "how Character A sees Character B" and "how Character B sees Character A".
Before setting controlled character profile fields, call get_character_profile_options for the fields you need. If a character tool fails with nextStep.tool = get_character_profile_options, call it before retrying.
Before making a broad or identity-level change, briefly explain the intent and then use a tool. The app will show every tool call to the user for audit.
""";

    public static IReadOnlyList<ModelAssistantTool> BuildTools(RpChatDocument document) =>
    [
        Tool("get_story_entities", "Read the current JSON model for all story entities and character relationships.", ObjectSchema()),
        Tool("get_story_transcript", "Read the current active story chat transcript, including private intents.", ObjectSchema()),
        Tool("get_character_profile_options", "Read valid ids and limits for controlled character profile fields. Call this before setting controlled fields in create_character, update_character, or update_character_relationship.", CharacterProfileRules.ProfileOptionsSchema()),
        Tool("create_character", "Create a new character from provided fields. Before setting controlled profile fields, call get_character_profile_options for those fields.", CharacterEntityPatchSchema()),
        Tool("update_character", "Patch only the provided fields on an existing character. Before setting controlled profile fields, call get_character_profile_options for those fields.", CharacterEntityPatchSchema(needsId: true)),
        Tool("create_location", "Create a new location from provided canon fields. The location name is required.", LocationEntityPatchSchema(requiredField: "name")),
        Tool("update_location", "Patch only the provided fields on an existing location. Use entityId from get_story_entities; call get_story_entities first if the target id is uncertain.", LocationEntityPatchSchema(needsId: true)),
        Tool("create_item", "Create a new item from provided canon fields. The item name is required.", ItemEntityPatchSchema(requiredField: "name")),
        Tool("update_item", "Patch only the provided fields on an existing item. Use entityId from get_story_entities; call get_story_entities first if the target id is uncertain.", ItemEntityPatchSchema(needsId: true)),
        Tool("create_timeline_entry", "Create a new timeline entry from provided canon fields. The timeline title is required.", TimelineEntityPatchSchema(requiredField: "title")),
        Tool("update_timeline_entry", "Patch only the provided fields on an existing timeline entry. Use entityId from get_story_entities; call get_story_entities first if the target id is uncertain.", TimelineEntityPatchSchema(needsId: true)),
        Tool("update_character_relationship", "Patch the directional relationship between two characters with explicit source/target meaning. Before setting relationshipType or privateTension, call get_character_profile_options.", CharacterProfileRules.RelationshipSchema()),
        Tool("ask_user", "Ask the user a multiple-choice or open-ended question and wait for their answer.", QuestionSchema())
    ];

    static ModelAssistantTool Tool(string name, string description, JsonObject schema) => new(name, description, schema);

    static JsonObject ObjectSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
        ["additionalProperties"] = false
    };

    static JsonObject LocationEntityPatchSchema(bool needsId = false, string requiredField = "") =>
        EntityPatchSchema(new()
        {
            ["name"] = StringSchema("Location name. Required when creating a location."),
            ["summary"] = StringSchema("Short scannable location summary."),
            ["description"] = StringSchema("Physical description and story-relevant details."),
            ["atmosphere"] = StringSchema("Mood, energy, or emotional tone of the place."),
            ["features"] = StringSchema("Notable features, landmarks, rooms, exits, hazards, or resources.")
        }, needsId, requiredField);

    static JsonObject ItemEntityPatchSchema(bool needsId = false, string requiredField = "") =>
        EntityPatchSchema(new()
        {
            ["name"] = StringSchema("Item name. Required when creating an item."),
            ["summary"] = StringSchema("Short scannable item summary."),
            ["description"] = StringSchema("Appearance and concrete details."),
            ["history"] = StringSchema("Backstory, ownership, provenance, or emotional baggage."),
            ["properties"] = StringSchema("Useful properties, powers, constraints, contents, or current known facts.")
        }, needsId, requiredField);

    static JsonObject TimelineEntityPatchSchema(bool needsId = false, string requiredField = "") =>
        EntityPatchSchema(new()
        {
            ["title"] = StringSchema("Timeline entry title. Required when creating a timeline entry."),
            ["date"] = StringSchema("In-world date, relative date, era, or sequence marker."),
            ["description"] = StringSchema("What happened."),
            ["significance"] = StringSchema("Why this event matters to canon or future scenes."),
            ["characters"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Character names or ids from get_story_entities that are involved in this event.",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["uniqueItems"] = true
            }
        }, needsId, requiredField);

    static JsonObject EntityPatchSchema(JsonObject updateProperties, bool needsId, string requiredField)
    {
        var properties = new JsonObject
        {
            ["updates"] = new JsonObject
            {
                ["type"] = "object",
                ["description"] = "Only the fields to set or replace. Do not resend unchanged fields.",
                ["properties"] = updateProperties,
                ["additionalProperties"] = false
            },
            ["reason"] = new JsonObject { ["type"] = "string" }
        };
        var required = new JsonArray { "updates" };
        if (!string.IsNullOrWhiteSpace(requiredField) && properties["updates"] is JsonObject updates)
            updates["required"] = new JsonArray { requiredField };

        if (needsId)
        {
            properties["entityId"] = new JsonObject { ["type"] = "string", ["description"] = "Existing entity id from get_story_entities." };
            required.Add("entityId");
        }

        return new()
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    static JsonObject StringSchema(string description) => new()
    {
        ["type"] = "string",
        ["description"] = description
    };

    static JsonObject CharacterEntityPatchSchema(bool needsId = false)
    {
        var properties = new JsonObject
        {
            ["updates"] = CharacterProfileRules.CharacterPatchSchema(),
            ["reason"] = new JsonObject { ["type"] = "string" }
        };
        var required = new JsonArray { "updates" };
        if (needsId)
        {
            properties["entityId"] = new JsonObject { ["type"] = "string" };
            required.Add("entityId");
        }

        return new()
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    static JsonObject QuestionSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["prompt"] = new JsonObject { ["type"] = "string" },
            ["allowsFreeform"] = new JsonObject { ["type"] = "boolean" },
            ["choices"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["id"] = new JsonObject { ["type"] = "string" },
                        ["label"] = new JsonObject { ["type"] = "string" }
                    }
                }
            }
        },
        ["required"] = new JsonArray { "prompt" },
        ["additionalProperties"] = false
    };
}

public sealed class StoryEntityPatchService
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
                "ask_user" => await AskUserAsync(toolCallId, argumentsJson, callbacks, cancellationToken),
                "create_character" => await CreateCharacterAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "update_character" => await UpdateCharacterAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "create_location" => await CreateLocationAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "update_location" => await UpdateLocationAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "create_item" => await CreateItemAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "update_item" => await UpdateItemAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "create_timeline_entry" => await CreateTimelineAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "update_timeline_entry" => await UpdateTimelineAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
                "update_character_relationship" => await UpdateRelationshipAsync(document, toolCallId, toolName, argumentsJson, callbacks, cancellationToken),
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

    static object BuildEntities(RpChatDocument document) => new
    {
        characters = document.Characters.Select(CharacterShape),
        locations = document.Locations.Select(LocationShape),
        items = document.Items.Select(ItemShape),
        timeline = document.Timeline.Select(TimelineShape),
        characterTraitLibrary = CharacterProfileRules.Context(document.CharacterTraitLibrary),
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

    static object CharacterShape(RpCharacter character) => new
    {
        character.Id,
        character.Name,
        character.ImageId,
        character.Summary,
        character.Personality,
        character.Appearance,
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
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Create, $"Create {character.Name}", "character", character.Id, character.Name, args, new(), CharacterJsonObject(character), StoryAssistantChangeRisk.Low);
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
        var item = MutationItem(callId, toolName, StoryAssistantOperationKind.Update, $"Update {after.Name}", "character", id, after.Name, args, CharacterJsonObject(before), CharacterJsonObject(after), risk);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.Characters, () => Copy(after, existing), CharacterJsonObject(existing), token);
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

    static JsonObject ToJsonObject<T>(T value) =>
        JsonSerializer.SerializeToNode(value, AppJsonSerializerOptions.Web)?.AsObject() ?? new();

    static JsonObject CharacterJsonObject(RpCharacter character) => ToJsonObject(CharacterShape(character));
    static JsonObject RelationshipJsonObject(RpCharacter character) => ToJsonObject(new
    {
        character.Id,
        character.Name,
        character.ProfileRelationships
    });

    static JsonObject LocationJsonObject(RpLocation location) => ToJsonObject(LocationShape(location));
    static JsonObject ItemJsonObject(RpItem item) => ToJsonObject(ItemShape(item));
    static JsonObject TimelineJsonObject(RpTimelineEntry entry) => ToJsonObject(TimelineShape(entry));

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
