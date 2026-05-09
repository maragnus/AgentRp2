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
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class ChatCharacterRow
{
    public string ChatId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageId { get; set; } = "";
    public bool InScene { get; set; }
    public int SortOrder { get; set; }
    public string ProfileJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class ChatCharacterRelationshipRow
{
    public string ChatId { get; set; } = "";
    public string Id { get; set; } = "";
    public string CharacterAId { get; set; } = "";
    public string CharacterBId { get; set; } = "";
    public int SortOrder { get; set; }
    public string DetailsJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class ChatLocationRow
{
    public string ChatId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageId { get; set; } = "";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public string DetailsJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class ChatItemRow
{
    public string ChatId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageId { get; set; } = "";
    public bool InScene { get; set; }
    public int SortOrder { get; set; }
    public string DetailsJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class ChatTimelineEntryRow
{
    public string ChatId { get; set; } = "";
    public string Id { get; set; } = "";
    public string SnapshotId { get; set; } = "";
    public string Title { get; set; } = "";
    public string DateText { get; set; } = "";
    public int SortOrder { get; set; }
    public string DetailsJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class ChatTranscriptStateRow
{
    public string ChatId { get; set; } = "";
    public int SchemaVersion { get; set; } = 1;
    public string RootSceneJson { get; set; } = "{}";
    public string WorkingSceneJson { get; set; } = "{}";
    public string OptionsJson { get; set; } = "{}";
    public string BranchSelectionsJson { get; set; } = "{}";
    public string DataJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class ChatDirectionStateRow
{
    public string ChatId { get; set; } = "";
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class NarratorProfileStateRow
{
    public string ChatId { get; set; } = "";
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class PromptLibraryStateRow
{
    public string ChatId { get; set; } = "";
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class CharacterTraitLibraryStateRow
{
    public string ChatId { get; set; } = "";
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class ModelTuningStateRow
{
    public string ChatId { get; set; } = "";
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class ChatCurrentSceneCharacterRow
{
    public string ChatId { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class ChatCurrentSceneItemRow
{
    public string ChatId { get; set; } = "";
    public string ItemId { get; set; } = "";
    public int SortOrder { get; set; }
}
