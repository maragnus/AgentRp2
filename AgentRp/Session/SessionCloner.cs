using System.Text.Json.Nodes;
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
        Location = value.Location,
        SceneCharacters = value.SceneCharacters.Select(Clone).ToList()
    };

    static RpChatSceneCharacter Clone(RpChatSceneCharacter value) => new()
    {
        Id = value.Id,
        Name = value.Name,
        ImageId = value.ImageId,
        Image = value.Image is null ? null : Clone(value.Image)
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
        Hue = value.Hue,
        Url = value.Url
    };

    public static AiProvider Clone(AiProvider value) => new()
    {
        Id = value.Id,
        Name = value.Name,
        Type = value.Type,
        Enabled = value.Enabled,
        ApiKey = value.ApiKey,
        ManagementApiKey = value.ManagementApiKey,
        Endpoint = value.Endpoint,
        AccountId = value.AccountId,
        ProjectId = value.ProjectId,
        TeamId = value.TeamId,
        LastMetricsRefreshUtc = value.LastMetricsRefreshUtc,
        LastMetricsError = value.LastMetricsError,
        Models = value.Models.Select(Clone).ToList(),
        Metrics = value.Metrics.Select(Clone).ToList()
    };

    public static AiProviderModel Clone(AiProviderModel value) => new()
    {
        Id = value.Id,
        DisplayName = value.DisplayName,
        Endpoint = value.Endpoint,
        Repository = value.Repository,
        CreatedUnix = value.CreatedUnix,
        Enabled = value.Enabled,
        Text = value.Text,
        Image = value.Image,
        ActiveText = value.ActiveText,
        Capabilities = value.Capabilities
    };

    public static AiProviderMetric Clone(AiProviderMetric value) => new()
    {
        Id = value.Id,
        Kind = value.Kind,
        Label = value.Label,
        Value = value.Value,
        Detail = value.Detail,
        RefreshedUtc = value.RefreshedUtc
    };

    public static RpChatDocument Clone(RpChatDocument value) => new()
    {
        Chat = Clone(value.Chat),
        Characters = value.Characters.Select(Clone).ToList(),
        Locations = value.Locations.Select(Clone).ToList(),
        Items = value.Items.Select(Clone).ToList(),
        Timeline = value.Timeline.Select(Clone).ToList(),
        Images = value.Images.Select(Clone).ToList(),
        Transcript = Clone(value.Transcript),
        StoryAssistant = Clone(value.StoryAssistant),
        NarratorProfile = Clone(value.NarratorProfile),
        PromptLibrary = Clone(value.PromptLibrary),
        CharacterTraitLibrary = Clone(value.CharacterTraitLibrary),
        ModelTuning = Clone(value.ModelTuning)
    };

    public static NarratorProfileState Clone(NarratorProfileState value) => new()
    {
        SchemaVersion = value.SchemaVersion,
        VoicePreset = value.VoicePreset,
        SetupDepth = value.SetupDepth,
        VisualDetail = value.VisualDetail,
        TransitionContext = value.TransitionContext,
        Foreshadowing = value.Foreshadowing,
        DirectionStrength = value.DirectionStrength,
        CustomGuidance = value.CustomGuidance
    };

    public static StoryAssistantState Clone(StoryAssistantState value) => new()
    {
        SchemaVersion = value.SchemaVersion,
        ReviewMode = value.ReviewMode,
        ConversationId = value.ConversationId,
        Items = value.Items.Select(Clone).ToList()
    };

    static StoryAssistantTranscriptItem Clone(StoryAssistantTranscriptItem value) => new()
    {
        Id = value.Id,
        Kind = value.Kind,
        Status = value.Status,
        CreatedUtc = value.CreatedUtc,
        UpdatedUtc = value.UpdatedUtc,
        Text = value.Text,
        Title = value.Title,
        ToolName = value.ToolName,
        ToolCallId = value.ToolCallId,
        EntityType = value.EntityType,
        EntityId = value.EntityId,
        EntityName = value.EntityName,
        ArgumentsJson = value.ArgumentsJson,
        ResultJson = value.ResultJson,
        Before = Clone(value.Before),
        After = Clone(value.After),
        Diffs = value.Diffs.Select(Clone).ToList(),
        Risk = value.Risk,
        DecisionReason = value.DecisionReason,
        Question = Clone(value.Question)
    };

    static StoryAssistantFieldDiff Clone(StoryAssistantFieldDiff value) => new()
    {
        Field = value.Field,
        Label = value.Label,
        Before = value.Before,
        After = value.After
    };

    static StoryAssistantQuestion Clone(StoryAssistantQuestion value) => new()
    {
        Prompt = value.Prompt,
        AllowsFreeform = value.AllowsFreeform,
        Choices = value.Choices.Select(Clone).ToList(),
        Answer = value.Answer
    };

    static StoryAssistantQuestionChoice Clone(StoryAssistantQuestionChoice value) => new()
    {
        Id = value.Id,
        Label = value.Label
    };

    public static RpTranscriptState Clone(RpTranscriptState value) => new()
    {
        SchemaVersion = value.SchemaVersion,
        RootScene = Clone(value.RootScene),
        Turns = value.Turns.Select(Clone).ToList(),
        Snapshots = value.Snapshots.Select(Clone).ToList(),
        ActiveLeafTurnId = value.ActiveLeafTurnId,
        BranchSelections = value.BranchSelections.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        Data = Clone(value.Data)
    };

    static RpTranscriptTurn Clone(RpTranscriptTurn value) => new()
    {
        Id = value.Id,
        ParentTurnId = value.ParentTurnId,
        CreatedUtc = value.CreatedUtc,
        UpdatedUtc = value.UpdatedUtc,
        Mode = value.Mode,
        AuthorCharacterId = value.AuthorCharacterId,
        AuthorName = value.AuthorName,
        ActorCharacterId = value.ActorCharacterId,
        ActorName = value.ActorName,
        Guidance = value.Guidance,
        Body = value.Body,
        Plan = Clone(value.Plan),
        AppearanceByCharacterId = value.AppearanceByCharacterId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        PrivateIntentByCharacterId = value.PrivateIntentByCharacterId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        SnapshotId = value.SnapshotId,
        Scene = Clone(value.Scene),
        Trace = value.Trace is null ? null : Clone(value.Trace),
        Data = Clone(value.Data)
    };

    static RpTranscriptSnapshot Clone(RpTranscriptSnapshot value) => new()
    {
        Id = value.Id,
        TurnId = value.TurnId,
        CreatedUtc = value.CreatedUtc,
        Summary = value.Summary,
        EarlierPrivateIntentContinuity = value.EarlierPrivateIntentContinuity,
        Facts = value.Facts.Select(Clone).ToList(),
        TimelineEntries = value.TimelineEntries.Select(Clone).ToList(),
        CharacterAppearances = value.CharacterAppearances.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        Scene = Clone(value.Scene),
        Trace = value.Trace is null ? null : Clone(value.Trace),
        Data = Clone(value.Data)
    };

    static RpTranscriptSnapshotFact Clone(RpTranscriptSnapshotFact value) => new()
    {
        Title = value.Title,
        Summary = value.Summary,
        Details = value.Details,
        CharacterNames = [.. value.CharacterNames],
        LocationNames = [.. value.LocationNames],
        ItemNames = [.. value.ItemNames]
    };

    static RpTranscriptSnapshotTimelineEntry Clone(RpTranscriptSnapshotTimelineEntry value) => new()
    {
        TimelineEntryId = value.TimelineEntryId,
        WhenText = value.WhenText,
        Title = value.Title,
        Summary = value.Summary,
        Details = value.Details,
        CharacterNames = [.. value.CharacterNames],
        LocationNames = [.. value.LocationNames],
        ItemNames = [.. value.ItemNames]
    };

    public static RpSceneFrame Clone(RpSceneFrame value) => new()
    {
        LocationId = value.LocationId,
        LocationName = value.LocationName,
        InSceneCharacterIds = [.. value.InSceneCharacterIds],
        InSceneItemIds = [.. value.InSceneItemIds],
        Data = Clone(value.Data)
    };

    public static RpTurnPlan Clone(RpTurnPlan value) => new()
    {
        TurnShape = value.TurnShape,
        Beat = value.Beat,
        Intent = value.Intent,
        ImmediateGoal = value.ImmediateGoal,
        WhyNow = value.WhyNow,
        ChangeIntroduced = value.ChangeIntroduced,
        Guardrails = value.Guardrails,
        Data = Clone(value.Data)
    };

    public static RpTurnTrace Clone(RpTurnTrace value) => new()
    {
        Summary = value.Summary,
        Status = value.Status,
        StartedUtc = value.StartedUtc,
        CompletedUtc = value.CompletedUtc,
        ProviderId = value.ProviderId,
        ProviderName = value.ProviderName,
        ModelId = value.ModelId,
        InputTokens = value.InputTokens,
        OutputTokens = value.OutputTokens,
        TotalTokens = value.TotalTokens,
        DurationSeconds = value.DurationSeconds,
        Steps = value.Steps.Select(Clone).ToList(),
        Data = Clone(value.Data)
    };

    static RpTurnTraceStep Clone(RpTurnTraceStep value) => new()
    {
        Id = value.Id,
        Label = value.Label,
        Status = value.Status,
        StartedUtc = value.StartedUtc,
        CompletedUtc = value.CompletedUtc,
        ProviderId = value.ProviderId,
        ProviderName = value.ProviderName,
        ModelId = value.ModelId,
        InputTokens = value.InputTokens,
        OutputTokens = value.OutputTokens,
        TotalTokens = value.TotalTokens,
        DurationSeconds = value.DurationSeconds,
        SystemPrompt = value.SystemPrompt,
        UserPrompt = value.UserPrompt,
        RawOutput = value.RawOutput,
        StructuredOutputJson = value.StructuredOutputJson,
        Error = value.Error,
        Data = Clone(value.Data)
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

    public static CharacterTraitLibraryState Clone(CharacterTraitLibraryState value) => new()
    {
        SchemaVersion = value.SchemaVersion,
        SceneRoles = value.SceneRoles.Select(Clone).ToList(),
        TraitCategories = value.TraitCategories.Select(Clone).ToList(),
        CoreDrives = value.CoreDrives.Select(Clone).ToList(),
        CoreFears = value.CoreFears.Select(Clone).ToList(),
        SurfaceMasks = value.SurfaceMasks.Select(Clone).ToList(),
        HiddenTruths = value.HiddenTruths.Select(Clone).ToList(),
        SentenceStyles = value.SentenceStyles.Select(Clone).ToList(),
        HonestyStyles = value.HonestyStyles.Select(Clone).ToList(),
        EmotionalLeakages = value.EmotionalLeakages.Select(Clone).ToList(),
        ActionFingerprints = value.ActionFingerprints.Select(Clone).ToList(),
        StressPatterns = value.StressPatterns.Select(Clone).ToList(),
        SoftSpots = value.SoftSpots.Select(Clone).ToList(),
        AvoidPatterns = value.AvoidPatterns.Select(Clone).ToList(),
        BondTypes = [.. value.BondTypes],
        Dynamics = [.. value.Dynamics]
    };

    static CharacterTraitGroupState Clone(CharacterTraitGroupState value) => new()
    {
        Name = value.Name,
        Color = value.Color,
        Items = value.Items.Select(Clone).ToList()
    };

    static CharacterOption Clone(CharacterOption value) => new(value.Id, value.Label, value.Hover);

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

    static JsonObject Clone(JsonObject value) => (JsonObject?)value.DeepClone() ?? new();
}
