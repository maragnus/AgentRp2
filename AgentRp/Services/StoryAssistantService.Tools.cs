using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed partial class StoryAssistantService
{
    public static IReadOnlyList<ModelAssistantTool> BuildTools(RpChatDocument document) =>
    [
        Tool("get_story_entities", "Read the current JSON model for all story entities and character relationships. Use this only when entity details or ids are unknown or stale.", ObjectSchema()),
        Tool("get_story_transcript", "Read the current active story chat transcript, including private intents. Use this only when transcript context is unknown or stale.", ObjectSchema()),
        Tool("get_character_profile_options", "Read valid ids and limits for controlled character profile fields. Use this only when the relevant options are unknown or stale.", CharacterProfileRules.ProfileOptionsSchema()),
        Tool("get_chat_direction_options", "Read valid ids, limits, intensity values, and current values for controlled chat direction fields. Use this only when the relevant options are unknown or stale.", ChatDirectionRules.OptionsSchema()),
        Tool("rename_story", "Rename the current story. Use known transcript context unless the transcript is unknown or stale.", StoryRenameSchema()),
        Tool("create_character", "Create a new character from provided fields. Use already-read character profile options unless relevant options are unknown or stale.", CharacterEntityPatchSchema()),
        Tool("update_character", "Update fields on an existing character. Eagerly complete missing profile details when useful; use already-read character profile options unless relevant options are unknown or stale. After any successful character update, inspect relationshipReconciliation and update only incomplete or contradicted canonical relationshipIds.", CharacterEntityPatchSchema(needsId: true)),
        Tool("update_chat_direction", "Update fields on this chat's direction. Use already-read chat direction options unless relevant options are unknown or stale.", ChatDirectionRules.PatchSchema()),
        Tool("create_location", "Create a new location from provided canon fields. The location name is required.", LocationEntityPatchSchema(requiredField: "name")),
        Tool("update_location", "Update fields on an existing location. Use known entity ids; call get_story_entities only if the target id is uncertain or stale.", LocationEntityPatchSchema(needsId: true)),
        Tool("create_item", "Create a new item from provided canon fields. The item name is required.", ItemEntityPatchSchema(requiredField: "name")),
        Tool("update_item", "Update fields on an existing item. Use known entity ids; call get_story_entities only if the target id is uncertain or stale.", ItemEntityPatchSchema(needsId: true)),
        Tool("create_timeline_entry", "Create a new timeline entry from provided canon fields. The timeline title is required.", TimelineEntityPatchSchema(requiredField: "title")),
        Tool("update_timeline_entry", "Update fields on an existing timeline entry. Use known entity ids; call get_story_entities only if the target id is uncertain or stale.", TimelineEntityPatchSchema(needsId: true)),
        Tool("update_character_relationship", "Update the relationship between two characters. This is a bidirectional update and updates both characters together with one call. Every relationship field is required and must be non-empty. Use already-read relationship options unless they are unknown or stale.", CharacterProfileRules.RelationshipSchema()),
        Tool("set_scene", "Set the starting scene or transition scene using existing canon ids, then run the normal narrator pipeline with required scene guidance. Use only for opening scenes, user-requested fast-forwards, location transitions, or explicit scene resets. Do not resolve major plot outcomes through this tool.", SceneTransitionSchema()),
        Tool("ask_user", "Ask the user one focused question and wait for their answer. Use this for onboarding interviews instead of writing prose questions. Supports single choice, multi-select, and optional freeform answers.", QuestionSchema())
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
            ["characterIds"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Character ids from get_story_entities that are involved in this event. Exact character names are accepted only when ids are unavailable.",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["uniqueItems"] = true
            },
            ["locationIds"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Location ids from get_story_entities that are involved in this event. Exact location names are accepted only when ids are unavailable.",
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
                ["description"] = "Fields to set or replace. Existing unchanged values do not need to be resent.",
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

    static JsonObject StoryRenameSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["title"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "New story title."
            },
            ["reason"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Why this title fits the story."
            }
        },
        ["required"] = new JsonArray { "title" },
        ["additionalProperties"] = false
    };

    static JsonObject QuestionSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["prompt"] = new JsonObject { ["type"] = "string", ["description"] = "The focused question to ask the user. Ask one decision at a time." },
            ["allowsFreeform"] = new JsonObject { ["type"] = "boolean", ["description"] = "Whether the user may write a custom answer instead of selecting choices." },
            ["selectionMode"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "single", "multiple" },
                ["description"] = "Use 'single' for one choice and 'multiple' when the user may pick more than one option."
            },
            ["minSelections"] = new JsonObject { ["type"] = "integer", ["description"] = "Minimum choices for multiple selection. Usually 0 or 1." },
            ["maxSelections"] = new JsonObject { ["type"] = "integer", ["description"] = "Maximum choices for multiple selection, such as 2 for pick 1-2." },
            ["choices"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Selectable options. Include descriptions when the option label alone is not enough.",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["id"] = new JsonObject { ["type"] = "string" },
                        ["label"] = new JsonObject { ["type"] = "string" },
                        ["description"] = new JsonObject { ["type"] = "string" }
                    },
                    ["required"] = new JsonArray { "label" },
                    ["additionalProperties"] = false
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
            ["narratorGuidance"] = new JsonObject
            {
                ["type"] = "object",
                ["description"] = "Required guidance for the narrator. Guide setting the scene, not controlling characters.",
                ["properties"] = new JsonObject
                {
                    ["purpose"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray { "opening_scene", "location_transition", "time_skip", "scene_reset" },
                        ["description"] = "What kind of scene-setting job the narrator is handling."
                    },
                    ["guidance"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "What the narrator should establish about how the scene starts or how the story arrived here. Do not write prose, dialogue, private thoughts, reactions, decisions, attacks, reveals, or other character turns."
                    }
                },
                ["required"] = new JsonArray { "purpose", "guidance" },
                ["additionalProperties"] = false
            }
        },
        ["required"] = new JsonArray { "locationId", "characterIds", "itemIds", "narratorGuidance" },
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
