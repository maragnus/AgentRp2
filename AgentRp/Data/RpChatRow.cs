namespace AgentRp.Data;

public sealed class RpChatRow
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Updated { get; set; } = "";
    public DateTime? LastMessageUtc { get; set; }
    public int LastGeneratedTurnNumber { get; set; }
    public bool Starred { get; set; }
    public int Messages { get; set; }
    public string Location { get; set; } = "";
    public string ActiveLeafTurnId { get; set; } = "";
    public int ActiveTurnCount { get; set; }
    public string ActiveLocationId { get; set; } = "";
    public string ActiveLocationName { get; set; } = "";
    public int SnapshotCount { get; set; }
    public string ActiveLocationJson { get; set; } = "";
    public string SceneCharactersJson { get; set; } = "[]";
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public RpChatDocumentRow? Document { get; set; }
}

public sealed class RpChatDocumentRow
{
    public string ChatId { get; set; } = "";
    public string CharactersJson { get; set; } = "[]";
    public string CharacterRelationshipsJson { get; set; } = "[]";
    public string LocationsJson { get; set; } = "[]";
    public string ItemsJson { get; set; } = "[]";
    public string TimelineJson { get; set; } = "[]";
    public string ImagesJson { get; set; } = "[]";
    public string MessagesJson { get; set; } = "[]";
    public string StoryAssistantJson { get; set; } = "";
    public string ChatDirectionJson { get; set; } = "";
    public string NarratorProfileJson { get; set; } = "";
    public string PromptLibraryJson { get; set; } = "";
    public string CharacterTraitLibraryJson { get; set; } = "";
    public string ModelTuningJson { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public RpChatRow Chat { get; set; } = null!;
}

public sealed class ChatCurrentSceneCharacterRow
{
    public string ChatId { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public int SortOrder { get; set; }
}
