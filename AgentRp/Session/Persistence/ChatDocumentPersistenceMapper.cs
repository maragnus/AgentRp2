using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

internal static class ChatDocumentPersistenceMapper
{
    public static RpChatDocument CreateEmpty(string chatId, RpChatRow? chat) => new()
    {
        Chat = chat is null ? new RpChat { Id = chatId } : ChatPersistenceMapper.ToModel(chat)
    };

    public static RpChatDocument ToModel(RpChatDocumentRow row, RpTranscriptState transcript)
    {
        var document = new RpChatDocument
        {
            Chat = ChatPersistenceMapper.ToModel(row.Chat),
            Characters = PersistenceJson.Deserialize(row.CharactersJson, new List<RpCharacter>()),
            CharacterRelationships = PersistenceJson.Deserialize(row.CharacterRelationshipsJson, new List<RpCharacterRelationship>()),
            Locations = PersistenceJson.Deserialize(row.LocationsJson, new List<RpLocation>()),
            Items = PersistenceJson.Deserialize(row.ItemsJson, new List<RpItem>()),
            Timeline = PersistenceJson.Deserialize(row.TimelineJson, new List<RpTimelineEntry>()),
            Images = PersistenceJson.Deserialize(row.ImagesJson, new List<GalleryImage>()),
            Transcript = transcript,
            StoryAssistant = PersistenceJson.Deserialize(row.StoryAssistantJson, new StoryAssistantState()),
            ChatDirection = PersistenceJson.Deserialize(row.ChatDirectionJson, ChatDirectionState.CreateDefault()),
            NarratorProfile = PersistenceJson.Deserialize(row.NarratorProfileJson, NarratorProfileState.CreateDefault()),
            PromptLibrary = PersistenceJson.Deserialize(row.PromptLibraryJson, PromptLibraryState.CreateDefault()),
            CharacterTraitLibrary = PersistenceJson.Deserialize(row.CharacterTraitLibraryJson, CharacterTraitLibraryState.CreateDefault()),
            ModelTuning = PersistenceJson.Deserialize(row.ModelTuningJson, ModelTuningState.CreateDefault())
        };

        TranscriptProjector.Apply(document);
        return document;
    }

    public static void Apply(RpChatDocument document, RpChatDocumentRow row, DateTime now)
    {
        row.CharactersJson = PersistenceJson.Serialize(document.Characters);
        row.CharacterRelationshipsJson = PersistenceJson.Serialize(document.CharacterRelationships);
        row.LocationsJson = PersistenceJson.Serialize(document.Locations);
        row.ItemsJson = PersistenceJson.Serialize(document.Items);
        row.TimelineJson = PersistenceJson.Serialize(document.Timeline);
        row.ImagesJson = PersistenceJson.Serialize(document.Images);
        row.MessagesJson = PersistenceJson.Serialize(TranscriptPersistenceMapper.ToShell(document.Transcript));
        row.StoryAssistantJson = PersistenceJson.Serialize(document.StoryAssistant);
        row.ChatDirectionJson = PersistenceJson.Serialize(ChatDirectionService.NormalizeState(document.ChatDirection));
        row.NarratorProfileJson = PersistenceJson.Serialize(NarratorProfileService.NormalizeState(document.NarratorProfile));
        row.PromptLibraryJson = PersistenceJson.Serialize(PromptLibraryService.CreateOverridesFromResolved(document.PromptLibrary));
        row.CharacterTraitLibraryJson = PersistenceJson.Serialize(document.CharacterTraitLibrary);
        row.ModelTuningJson = PersistenceJson.Serialize(document.ModelTuning);
        row.UpdatedUtc = now;
    }
}
