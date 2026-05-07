using Microsoft.AspNetCore.Components;

namespace AgentRp.Services;

public static class MessageSpeechDialogHelper
{
    public static async Task ShowMissingVoiceAsync(
        DialogHelper dialogs,
        MessageSpeechAvailability availability,
        EventCallback<(string Type, string? Id)> onOpenEntities)
    {
        if (availability.Kind != MessageSpeechAvailabilityKind.MissingVoice)
            return;

        var body = availability.MissingNarrator
            ? "Select a narrator voice before reading narrator messages aloud."
            : $"Select a voice for {availability.MissingEntityName} or the narrator before reading this message aloud.";
        var result = await dialogs.ShowAsync(new DialogOptions(
            "Select a Voice",
            body,
            DialogKind.Confirm,
            Buttons: new DialogButtonOptions(
                ShowCancel: true,
                PrimaryText: "Open Editor",
                PrimaryIcon: "edit"),
            Icon: "volume",
            Subtitle: availability.MissingEntityName));

        if (result != DialogResult.Primary)
            return;

        var id = availability.MissingNarrator
            ? MessageSpeechService.NarratorVoiceKey
            : availability.MissingEntityId;
        await onOpenEntities.InvokeAsync(("characters", id));
    }
}
