using AgentRp.Models;

namespace AgentRp.Session;

static class SessionCloner
{
    public static RpChat Clone(RpChat value) => new()
    {
        Id = value.Id,
        Title = value.Title,
        Updated = value.Updated,
        Starred = value.Starred,
        Messages = value.Messages,
        Location = value.Location
    };

    public static RpCharacter Clone(RpCharacter value) => new()
    {
        Id = value.Id,
        Name = value.Name,
        ImageId = value.ImageId,
        InScene = value.InScene,
        Summary = value.Summary,
        Personality = value.Personality,
        Appearance = value.Appearance,
        Relationships = value.Relationships,
        Backstory = value.Backstory,
        Voice = value.Voice,
        Notes = value.Notes,
        SceneRoles = [.. value.SceneRoles],
        Traits = [.. value.Traits],
        Drives = [.. value.Drives],
        Limits = [.. value.Limits],
        CoreDrive = value.CoreDrive,
        CoreFear = value.CoreFear,
        SurfaceMask = value.SurfaceMask,
        HiddenTruth = value.HiddenTruth,
        SentenceStyle = value.SentenceStyle,
        HonestyStyle = value.HonestyStyle,
        EmotionalLeakage = value.EmotionalLeakage,
        ActionFingerprint = value.ActionFingerprint,
        StressPattern = value.StressPattern,
        SoftSpots = [.. value.SoftSpots],
        AvoidPatterns = [.. value.AvoidPatterns],
        ProfileRelationships = value.ProfileRelationships.Select(Clone).ToList()
    };

    static RpRelationship Clone(RpRelationship value) => new()
    {
        CharacterId = value.CharacterId,
        Bonds = [.. value.Bonds],
        Dynamics = [.. value.Dynamics],
        NoteAtoB = value.NoteAtoB,
        NoteBtoA = value.NoteBtoA,
        NoteExternal = value.NoteExternal
    };

    public static RpLocation Clone(RpLocation value) => new()
    {
        Id = value.Id,
        Name = value.Name,
        ImageId = value.ImageId,
        IsActive = value.IsActive,
        Summary = value.Summary,
        Description = value.Description,
        Atmosphere = value.Atmosphere,
        Features = value.Features
    };

    public static RpItem Clone(RpItem value) => new()
    {
        Id = value.Id,
        Name = value.Name,
        ImageId = value.ImageId,
        InScene = value.InScene,
        Summary = value.Summary,
        Description = value.Description,
        History = value.History,
        Properties = value.Properties
    };

    public static RpTimelineEntry Clone(RpTimelineEntry value) => new()
    {
        Id = value.Id,
        Title = value.Title,
        Date = value.Date,
        Description = value.Description,
        Characters = [.. value.Characters],
        Significance = value.Significance
    };

    public static GalleryImage Clone(GalleryImage value) => new()
    {
        Id = value.Id,
        Name = value.Name,
        Entity = value.Entity,
        EntityType = value.EntityType,
        Date = value.Date,
        Hue = value.Hue
    };

    public static AiProvider Clone(AiProvider value) => new()
    {
        Id = value.Id,
        Name = value.Name,
        Type = value.Type,
        Enabled = value.Enabled,
        ApiKey = value.ApiKey,
        Endpoint = value.Endpoint,
        Models = value.Models.Select(Clone).ToList()
    };

    public static AiProviderModel Clone(AiProviderModel value) => new()
    {
        Id = value.Id,
        Enabled = value.Enabled,
        Text = value.Text,
        Image = value.Image
    };

    public static RpMessage Clone(RpMessage value) => new()
    {
        Id = value.Id,
        Type = value.Type,
        Summary = value.Summary,
        Status = value.Status,
        Duration = value.Duration,
        Timestamp = value.Timestamp,
        Author = value.Author,
        Mode = value.Mode,
        Body = value.Body,
        Branch = value.Branch,
        CharacterCount = value.CharacterCount,
        Steps = value.Steps.Select(Clone).ToList()
    };

    static RpProcessStep Clone(RpProcessStep value) => new()
    {
        Id = value.Id,
        Label = value.Label,
        Icon = value.Icon,
        TokensIn = value.TokensIn,
        TokensOut = value.TokensOut,
        TotalTokens = value.TotalTokens,
        Duration = value.Duration,
        SystemPrompt = value.SystemPrompt,
        UserPrompt = value.UserPrompt,
        Output = value.Output
    };

    public static RpChatDocument Clone(RpChatDocument value) => new()
    {
        Chat = Clone(value.Chat),
        Characters = value.Characters.Select(Clone).ToList(),
        Locations = value.Locations.Select(Clone).ToList(),
        Items = value.Items.Select(Clone).ToList(),
        Timeline = value.Timeline.Select(Clone).ToList(),
        Images = value.Images.Select(Clone).ToList(),
        Messages = value.Messages.Select(Clone).ToList(),
        PromptLibrary = Clone(value.PromptLibrary),
        ModelTuning = Clone(value.ModelTuning)
    };

    public static PromptLibraryState Clone(PromptLibraryState value) => new()
    {
        Prompts = value.Prompts.ToDictionary(pair => pair.Key, pair => new PromptPairState { System = pair.Value.System, User = pair.Value.User }),
        TurnShapes = value.TurnShapes.ToDictionary(pair => pair.Key, pair => pair.Value.Select(Clone).ToList())
    };

    static ShapePromptState Clone(ShapePromptState value) => new()
    {
        Id = value.Id,
        Label = value.Label,
        Value = value.Value
    };

    public static ModelTuningState Clone(ModelTuningState value) => new()
    {
        Values = value.Values.ToDictionary(pair => pair.Key, pair => Clone(pair.Value))
    };

    static ModelTuningStepState Clone(ModelTuningStepState value) => new()
    {
        Temperature = value.Temperature,
        TopP = value.TopP,
        MaxTokens = value.MaxTokens,
        Seed = value.Seed,
        FrequencyPenalty = value.FrequencyPenalty,
        PresencePenalty = value.PresencePenalty,
        StopSequences = value.StopSequences
    };
}
