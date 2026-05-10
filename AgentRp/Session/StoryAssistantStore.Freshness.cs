using AgentRp.Models;

namespace AgentRp.Session;

public sealed partial class StoryAssistantStore
{
    static string BuildModelInputWithFreshnessNote(RpChatDocument document, StoryAssistantChat chat, string modelInput)
    {
        var note = BuildFreshnessNote(document, chat);
        return string.IsNullOrWhiteSpace(note)
            ? modelInput
            : $"{note}{Environment.NewLine}{Environment.NewLine}{modelInput}";
    }

    static string BuildFreshnessNote(RpChatDocument document, StoryAssistantChat chat)
    {
        var updates = new List<string>();
        AddTranscriptUpdates(document, chat, updates);
        AddStoryEntityUpdates(document, chat, updates);
        AddOptionsUpdates(document, chat, updates);
        return updates.Count == 0
            ? ""
            : $"NOTE: Updates since you last checked: {string.Join("; ", updates)}.";
    }

    static void AddTranscriptUpdates(RpChatDocument document, StoryAssistantChat chat, List<string> updates)
    {
        if (chat.LastTranscriptReadUtc == default)
            return;

        var activeTurns = TranscriptGraph.GetActivePath(document.Transcript).ToList();
        var addedCount = activeTurns.Count(turn => turn.CreatedUtc > chat.LastTranscriptReadUtc);
        var changedCount = activeTurns.Count(turn =>
            turn.CreatedUtc <= chat.LastTranscriptReadUtc
            && EffectiveUpdatedUtc(turn) > chat.LastTranscriptReadUtc);
        if (addedCount > 0)
            updates.Add($"added {addedCount} {Pluralize(addedCount, "message", "messages")}");

        if (changedCount > 0)
            updates.Add($"changed {changedCount} {Pluralize(changedCount, "message", "messages")}");
    }

    static void AddStoryEntityUpdates(RpChatDocument document, StoryAssistantChat chat, List<string> updates)
    {
        if (chat.LastStoryEntitiesReadUtc == default)
            return;

        var names = new List<string>();
        names.AddRange(document.Characters.Where(item => item.UpdatedUtc > chat.LastStoryEntitiesReadUtc).Select(item => item.Name));
        names.AddRange(document.Locations.Where(item => item.UpdatedUtc > chat.LastStoryEntitiesReadUtc).Select(item => item.Name));
        names.AddRange(document.Items.Where(item => item.UpdatedUtc > chat.LastStoryEntitiesReadUtc).Select(item => item.Name));
        names.AddRange(document.Timeline.Where(item => item.UpdatedUtc > chat.LastStoryEntitiesReadUtc).Select(item => item.Title));
        names.AddRange(document.CharacterRelationships
            .Where(item => item.UpdatedUtc > chat.LastStoryEntitiesReadUtc)
            .Select(item => RelationshipName(document, item)));

        if (document.ChatDirection.UpdatedUtc > chat.LastStoryEntitiesReadUtc)
            names.Add("story direction");

        names = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (names.Count > 0)
            updates.Add($"changed {FormatNameList(names)}");
    }

    static void AddOptionsUpdates(RpChatDocument document, StoryAssistantChat chat, List<string> updates)
    {
        if (chat.LastCharacterProfileOptionsReadUtc != default
            && document.CharacterTraitLibrary.UpdatedUtc > chat.LastCharacterProfileOptionsReadUtc)
            updates.Add("changed character profile options");

        if (chat.LastChatDirectionOptionsReadUtc != default
            && document.ChatDirection.UpdatedUtc > chat.LastChatDirectionOptionsReadUtc)
            updates.Add("changed chat direction options");
    }

    static string RelationshipName(RpChatDocument document, RpCharacterRelationship relationship)
    {
        var first = document.Characters.FirstOrDefault(item => item.Id == relationship.CharacterAId)?.Name ?? relationship.CharacterAId;
        var second = document.Characters.FirstOrDefault(item => item.Id == relationship.CharacterBId)?.Name ?? relationship.CharacterBId;
        return string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)
            ? relationship.Id
            : $"{first}/{second} relationship";
    }

    static string FormatNameList(IReadOnlyList<string> names)
    {
        var visible = names.Take(8).ToList();
        if (names.Count > visible.Count)
            visible.Add($"{names.Count - visible.Count} more");

        return visible.Count switch
        {
            0 => "",
            1 => visible[0],
            2 => $"{visible[0]} and {visible[1]}",
            _ => $"{string.Join(", ", visible.Take(visible.Count - 1))}, and {visible[^1]}"
        };
    }

    static string Pluralize(int count, string singular, string plural) =>
        count == 1 ? singular : plural;

    static DateTime EffectiveUpdatedUtc(RpTranscriptTurn turn) =>
        turn.UpdatedUtc == default ? turn.CreatedUtc : turn.UpdatedUtc;

    static void MarkFreshnessFromRecordedToolCall(StoryAssistantChat chat, StoryAssistantTranscriptItem item)
    {
        if (item.Status != StoryAssistantItemStatus.Read)
            return;

        var now = DateTime.UtcNow;
        switch (item.ToolName)
        {
            case "get_story_transcript":
                chat.LastTranscriptReadUtc = now;
                break;
            case "get_story_entities":
                chat.LastStoryEntitiesReadUtc = now;
                break;
            case "get_character_profile_options":
                chat.LastCharacterProfileOptionsReadUtc = now;
                break;
            case "get_chat_direction_options":
                chat.LastChatDirectionOptionsReadUtc = now;
                break;
        }
    }

    static void MarkFreshnessFromUpdatedToolCall(StoryAssistantChat chat, StoryAssistantTranscriptItem item)
    {
        if (item.Status != StoryAssistantItemStatus.Applied && item.Status != StoryAssistantItemStatus.Accepted)
            return;

        if (item.Operation is StoryAssistantOperationKind.Create or StoryAssistantOperationKind.Update
            && IsStoryEntitiesSource(item.EntityType))
            chat.LastStoryEntitiesReadUtc = DateTime.UtcNow;
    }

    static void MarkFreshnessFromWorkItem(StoryAssistantChat chat, StoryAssistantWorkItem workItem)
    {
        if (workItem.Status != StoryAssistantWorkItemStatus.Completed)
            return;

        if (workItem.Operation is StoryAssistantOperationKind.Create or StoryAssistantOperationKind.Update
            && IsStoryEntitiesSource(workItem.EntityType))
            chat.LastStoryEntitiesReadUtc = DateTime.UtcNow;
    }

    static bool IsStoryEntitiesSource(string entityType) =>
        entityType is "character" or "relationship" or "chatDirection" or "location" or "item" or "timeline";

    static void ClearFreshnessReceipts(StoryAssistantChat chat)
    {
        chat.LastTranscriptReadUtc = default;
        chat.LastStoryEntitiesReadUtc = default;
        chat.LastCharacterProfileOptionsReadUtc = default;
        chat.LastChatDirectionOptionsReadUtc = default;
    }
}
