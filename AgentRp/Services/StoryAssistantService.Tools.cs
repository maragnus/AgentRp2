using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed partial class StoryAssistantService
{
    public static IReadOnlyList<ModelAssistantTool> BuildTools(RpChatDocument document) =>
    [
        Tool("get_story_entities", "Read the current JSON model for all story entities and character relationships.", ObjectSchema()),
        Tool("get_story_transcript", "Read the current active story chat transcript, including private intents.", ObjectSchema()),
        Tool("get_character_profile_options", "Read valid ids and limits for controlled character profile fields. Call this before setting controlled fields in create_character, update_character, or update_character_relationship.", CharacterProfileRules.ProfileOptionsSchema()),
        Tool("get_chat_direction_options", "Read valid ids, limits, intensity values, and current values for controlled chat direction fields. Call this before setting controlled fields in update_chat_direction.", ChatDirectionRules.OptionsSchema()),
        Tool("create_character", "Create a new character from provided fields. Before setting controlled profile fields, call get_character_profile_options for those fields.", CharacterEntityPatchSchema()),
        Tool("update_character", "Patch only the provided fields on an existing character. Before setting controlled profile fields, call get_character_profile_options for those fields.", CharacterEntityPatchSchema(needsId: true)),
        Tool("update_chat_direction", "Patch only the provided fields on this chat's direction. Before setting controlled direction fields, call get_chat_direction_options for those fields.", ChatDirectionRules.PatchSchema()),
        Tool("create_location", "Create a new location from provided canon fields. The location name is required.", LocationEntityPatchSchema(requiredField: "name")),
        Tool("update_location", "Patch only the provided fields on an existing location. Use entityId from get_story_entities; call get_story_entities first if the target id is uncertain.", LocationEntityPatchSchema(needsId: true)),
        Tool("create_item", "Create a new item from provided canon fields. The item name is required.", ItemEntityPatchSchema(requiredField: "name")),
        Tool("update_item", "Patch only the provided fields on an existing item. Use entityId from get_story_entities; call get_story_entities first if the target id is uncertain.", ItemEntityPatchSchema(needsId: true)),
        Tool("create_timeline_entry", "Create a new timeline entry from provided canon fields. The timeline title is required.", TimelineEntityPatchSchema(requiredField: "title")),
        Tool("update_timeline_entry", "Patch only the provided fields on an existing timeline entry. Use entityId from get_story_entities; call get_story_entities first if the target id is uncertain.", TimelineEntityPatchSchema(needsId: true)),
        Tool("update_character_relationship", "Patch the directional relationship between two characters with explicit source/target meaning. Before setting relationshipType or privateTension, call get_character_profile_options.", CharacterProfileRules.RelationshipSchema()),
        Tool("set_scene", "Set the starting scene or transition scene using existing canon ids, then ask the narrator to set the scene. Use only for opening scenes, user-requested fast-forwards, location transitions, or explicit scene resets. Do not resolve major plot outcomes through this tool.", SceneTransitionSchema()),
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

    static JsonObject SceneTransitionSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["locationId"] = new JsonObject { ["type"] = "string", ["description"] = "Existing location id from get_story_entities." },
            ["characterIds"] = IdArraySchema("Existing character ids that should be present in the resulting scene."),
            ["itemIds"] = IdArraySchema("Existing item ids that should be present in the resulting scene. Use an empty array if no items are present."),
            ["elapsedTime"] = new JsonObject { ["type"] = "string", ["description"] = "Optional skipped time, such as 'two hours later'." },
            ["transitionNote"] = new JsonObject { ["type"] = "string", ["description"] = "Optional user-approved staging or transition context. Do not include unapproved major outcomes." },
            ["reason"] = new JsonObject { ["type"] = "string", ["description"] = "Why the scene is being set now." }
        },
        ["required"] = new JsonArray { "locationId", "characterIds" },
        ["additionalProperties"] = false
    };

    static JsonObject IdArraySchema(string description) => new()
    {
        ["type"] = "array",
        ["description"] = description,
        ["items"] = new JsonObject { ["type"] = "string" },
        ["uniqueItems"] = true
    };
}
