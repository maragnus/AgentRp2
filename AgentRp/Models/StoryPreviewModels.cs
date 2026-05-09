namespace AgentRp.Models;

public sealed class StoryPreview
{
    public string ChatId { get; set; } = "";
    public string Title { get; set; } = "";
    public bool Starred { get; set; }
    public int VisibleTurnCount { get; set; }
    public int LastGeneratedTurnNumber { get; set; }
    public DateTime? LastMessageUtc { get; set; }
    public string Updated { get; set; } = "";
    public StoryPreviewLocation? ActiveLocation { get; set; }
    public List<StoryPreviewCharacter> SceneCharacters { get; set; } = [];
}

public sealed class StoryPreviewLocation
{
    public string LocationId { get; set; } = "";
    public string Name { get; set; } = "";
    public StoryPreviewAvatar? Avatar { get; set; }
}

public sealed class StoryPreviewCharacter
{
    public string CharacterId { get; set; } = "";
    public string Name { get; set; } = "";
    public StoryPreviewAvatar? Avatar { get; set; }
}

public sealed class StoryPreviewAvatar
{
    public string ImageId { get; set; } = "";
    public string Url { get; set; } = "";
    public int FocusXPercent { get; set; } = 50;
    public int FocusYPercent { get; set; } = 50;
    public int ZoomPercent { get; set; } = 100;
}
