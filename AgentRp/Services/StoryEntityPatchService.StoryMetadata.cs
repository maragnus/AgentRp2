using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed partial class StoryEntityPatchService
{
    static readonly HashSet<string> TranscriptGuardedTools = new(StringComparer.Ordinal)
    {
        "rename_story",
        "create_character",
        "update_character",
        "update_chat_direction",
        "create_location",
        "update_location",
        "create_item",
        "update_item",
        "create_timeline_entry",
        "update_timeline_entry",
        "update_character_relationship",
        "set_scene"
    };

    static bool MustReadTranscriptFirst(string toolName, StoryAssistantToolRunContext? context) =>
        context is { HasReadTranscript: false } && TranscriptGuardedTools.Contains(toolName);

    static string TranscriptRequiredOutput(string toolName) => Output("failed", new
    {
        reason = $"The Story Assistant must read the current transcript before calling '{toolName}' so canon changes align with the narrative.",
        nextStep = new
        {
            tool = "get_story_transcript",
            instruction = "Call get_story_transcript, use the current narrative context to check the intended change, then retry the blocked tool call."
        }
    });

    async Task<StoryAssistantToolExecutionResult> RenameStoryAsync(
        RpChatDocument document,
        string callId,
        string toolName,
        string args,
        IStoryAssistantCallbacks callbacks,
        CancellationToken token)
    {
        using var json = Parse(args);
        var title = NormalizeStoryTitle(RequiredString(json.RootElement, "title"));
        var before = StoryTitleJsonObject(document.Chat.Title);
        var after = StoryTitleJsonObject(title);
        var item = MutationItem(
            callId,
            toolName,
            StoryAssistantOperationKind.Update,
            $"Rename story to {title}",
            "story",
            document.Chat.Id,
            title,
            args,
            before,
            after,
            StoryAssistantChangeRisk.Major);
        return await ResolveMutationAsync(document, item, callbacks, RoleplayStoreArea.ChatDirection, () => document.Chat.Title = title, before, token);
    }

    static string NormalizeStoryTitle(string title)
    {
        var clean = string.Join(' ', title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(clean))
            throw new InvalidOperationException("Renaming the story failed because title is required.");

        return clean.Length <= 500 ? clean : clean[..500];
    }

    static JsonObject StoryTitleJsonObject(string title) => ToJsonObject(new { title });
}
