using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

static class SessionCloner
{
    public static RpChat Clone(RpChat value) => new()
    {
        Id = value.Id,
        Title = value.Title,
        Updated = value.Updated,
        LastMessageUtc = value.LastMessageUtc,
        LastGeneratedTurnNumber = value.LastGeneratedTurnNumber,
        Starred = value.Starred,
        Messages = value.Messages,
        Location = value.Location,
        ActiveLocation = value.ActiveLocation is null ? null : Clone(value.ActiveLocation),
        SceneCharacters = value.SceneCharacters.Select(Clone).ToList()
    };

    static RpChatSceneLocation Clone(RpChatSceneLocation value) => new()
    {
        Id = value.Id,
        Name = value.Name,
        ImageId = value.ImageId,
        Image = value.Image is null ? null : Clone(value.Image)
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
        AppearanceProfile = Clone(value.AppearanceProfile),
        Backstory = value.Backstory,
        Voice = value.Voice,
        Notes = value.Notes,
        Pronouns = [.. value.Pronouns],
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
        VoiceSelections = value.VoiceSelections.ToDictionary(pair => pair.Key, pair => Clone(pair.Value), StringComparer.Ordinal)
    };

    static CharacterAppearanceState Clone(CharacterAppearanceState value) => new()
    {
        HairColor = value.HairColor,
        HairStyles = [.. value.HairStyles],
        EyeColor = value.EyeColor,
        FaceShape = value.FaceShape,
        SkinTone = value.SkinTone,
        Complexion = [.. value.Complexion],
        Height = value.Height,
        Build = value.Build,
        BodyProportions = [.. value.BodyProportions],
        Presentation = [.. value.Presentation],
        Attractiveness = value.Attractiveness
    };

    static CharacterVoiceSelection Clone(CharacterVoiceSelection value) => new()
    {
        VoiceId = value.VoiceId,
        VoiceName = value.VoiceName,
        UpdatedUtc = value.UpdatedUtc
    };

    public static RpCharacterRelationship Clone(RpCharacterRelationship value) => new()
    {
        Id = value.Id,
        CharacterAId = value.CharacterAId,
        CharacterBId = value.CharacterBId,
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
        SnapshotId = value.SnapshotId,
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
        Url = value.Url,
        AvatarFocusXPercent = value.AvatarFocusXPercent,
        AvatarFocusYPercent = value.AvatarFocusYPercent,
        AvatarZoomPercent = value.AvatarZoomPercent
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
        Roles = [.. value.Roles],
        LastVoiceRefreshUtc = value.LastVoiceRefreshUtc,
        LastVoiceRefreshError = value.LastVoiceRefreshError,
        Voices = value.Voices.Select(Clone).ToList(),
        Capabilities = value.Capabilities
    };

    public static AiProviderVoice Clone(AiProviderVoice value) => new()
    {
        Id = value.Id,
        DisplayName = value.DisplayName,
        Description = value.Description,
        PreviewUrl = value.PreviewUrl,
        Labels = value.Labels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        Source = value.Source,
        IsCatalogVoice = value.IsCatalogVoice,
        IsBookmarked = value.IsBookmarked,
        IsAvailable = value.IsAvailable,
        UpdatedUtc = value.UpdatedUtc
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
        CharacterRelationships = value.CharacterRelationships.Select(Clone).ToList(),
        Locations = value.Locations.Select(Clone).ToList(),
        Items = value.Items.Select(Clone).ToList(),
        Timeline = value.Timeline.Select(Clone).ToList(),
        Images = value.Images.Select(Clone).ToList(),
        Transcript = Clone(value.Transcript),
        StoryAssistant = Clone(value.StoryAssistant),
        ChatDirection = Clone(value.ChatDirection),
        NarratorProfile = Clone(value.NarratorProfile),
        PromptLibrary = Clone(value.PromptLibrary),
        CharacterTraitLibrary = Clone(value.CharacterTraitLibrary),
        ModelTuning = Clone(value.ModelTuning)
    };

    public static ActiveModelSelectionsState Clone(ActiveModelSelectionsState value) => new()
    {
        Values = value.Values.ToDictionary(pair => pair.Key, pair => Clone(pair.Value))
    };

    static ActiveModelSelectionState Clone(ActiveModelSelectionState value) => new()
    {
        ProviderId = value.ProviderId,
        ModelId = value.ModelId
    };

    public static ChatDirectionState Clone(ChatDirectionState value) => new()
    {
        SchemaVersion = value.SchemaVersion,
        Genres = [.. value.Genres],
        Tones = [.. value.Tones],
        Themes = [.. value.Themes],
        Pacing = [.. value.Pacing],
        StoryFocus = [.. value.StoryFocus],
        Boundaries = [.. value.Boundaries],
        ExplicitContent = value.ExplicitContent,
        ViolentContent = value.ViolentContent,
        Setting = value.Setting,
        Premise = value.Premise,
        CustomGuidance = value.CustomGuidance
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
        CustomGuidance = value.CustomGuidance,
        VoiceSelections = value.VoiceSelections.ToDictionary(pair => pair.Key, pair => Clone(pair.Value), StringComparer.Ordinal)
    };

    public static StoryAssistantState Clone(StoryAssistantState value) => new()
    {
        SchemaVersion = value.SchemaVersion,
        ReviewMode = value.ReviewMode,
        ActiveChatId = value.ActiveChatId,
        Chats = value.Chats.Select(Clone).ToList()
    };

    static StoryAssistantChat Clone(StoryAssistantChat value) => new()
    {
        Id = value.Id,
        Title = value.Title,
        CreatedUtc = value.CreatedUtc,
        UpdatedUtc = value.UpdatedUtc,
        LastResponseId = value.LastResponseId,
        ResponseIds = value.ResponseIds.ToList(),
        ResponseProviderId = value.ResponseProviderId,
        ResponseModelId = value.ResponseModelId,
        RemoteThreadLost = value.RemoteThreadLost,
        RemoteThreadError = value.RemoteThreadError,
        Items = value.Items.Select(Clone).ToList(),
        WorkItems = value.WorkItems.Select(Clone).ToList()
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
        WorkItemId = value.WorkItemId,
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
        Retry = Clone(value.Retry),
        Diagnostics = Clone(value.Diagnostics),
        Question = Clone(value.Question)
    };

    static StoryAssistantWorkItem Clone(StoryAssistantWorkItem value) => new()
    {
        Id = value.Id,
        TranscriptItemId = value.TranscriptItemId,
        Kind = value.Kind,
        Status = value.Status,
        CreatedUtc = value.CreatedUtc,
        UpdatedUtc = value.UpdatedUtc,
        Title = value.Title,
        ToolName = value.ToolName,
        ToolCallId = value.ToolCallId,
        AwaitingResponseId = value.AwaitingResponseId,
        ResponseProviderId = value.ResponseProviderId,
        ResponseModelId = value.ResponseModelId,
        EntityArea = value.EntityArea,
        Operation = value.Operation,
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
        Question = Clone(value.Question),
        Diagnostics = Clone(value.Diagnostics)
    };

    static StoryAssistantRetryContext Clone(StoryAssistantRetryContext value) => new()
    {
        DisplayMessage = value.DisplayMessage,
        ModelInput = value.ModelInput,
        IsRetry = value.IsRetry
    };

    static StoryAssistantDiagnostics Clone(StoryAssistantDiagnostics value) => new()
    {
        Outcome = value.Outcome,
        Reason = value.Reason,
        ProviderId = value.ProviderId,
        ProviderName = value.ProviderName,
        ModelId = value.ModelId,
        ModelName = value.ModelName,
        PreviousResponseId = value.PreviousResponseId,
        ResponseId = value.ResponseId,
        RequestDisplay = value.RequestDisplay,
        LastStreamEvent = value.LastStreamEvent,
        ToolRoundCount = value.ToolRoundCount,
        Error = value.Error,
        RecordedUtc = value.RecordedUtc
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
        SelectionMode = value.SelectionMode,
        MinSelections = value.MinSelections,
        MaxSelections = value.MaxSelections,
        Choices = value.Choices.Select(Clone).ToList(),
        Answer = value.Answer
    };

    static StoryAssistantQuestionChoice Clone(StoryAssistantQuestionChoice value) => new()
    {
        Id = value.Id,
        Label = value.Label,
        Description = value.Description
    };

    public static RpTranscriptState Clone(RpTranscriptState value) => new()
    {
        SchemaVersion = value.SchemaVersion,
        RootScene = Clone(value.RootScene),
        WorkingScene = Clone(value.WorkingScene),
        Options = Clone(value.Options),
        Turns = value.Turns.Select(Clone).ToList(),
        Snapshots = value.Snapshots.Select(Clone).ToList(),
        ActiveLeafTurnId = value.ActiveLeafTurnId,
        BranchSelections = value.BranchSelections.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        Data = Clone(value.Data)
    };

    static RpWorkingSceneState Clone(RpWorkingSceneState value) => new()
    {
        IsActive = value.IsActive,
        ParentTurnId = value.ParentTurnId,
        Scene = Clone(value.Scene)
    };

    static RpTranscriptOptionsState Clone(RpTranscriptOptionsState value) => new()
    {
        InjectAudioTags = value.InjectAudioTags,
        HideAudioTags = value.HideAudioTags,
        ShowAppearanceBlocks = value.ShowAppearanceBlocks,
        ShowProcessTraces = value.ShowProcessTraces,
        AutoSpeakNewMessages = value.AutoSpeakNewMessages,
        SpeakActionsInNarratorVoice = value.SpeakActionsInNarratorVoice,
        TurnShape = value.TurnShape,
        TurnShapeLocked = value.TurnShapeLocked
    };

    static RpTranscriptTurn Clone(RpTranscriptTurn value) => new()
    {
        Id = value.Id,
        ParentTurnId = value.ParentTurnId,
        TurnNumber = value.TurnNumber,
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
        Speech = Clone(value.Speech),
        Scene = Clone(value.Scene),
        Trace = value.Trace is null ? null : Clone(value.Trace),
        Data = Clone(value.Data)
    };

    static RpMessageSpeechState Clone(RpMessageSpeechState value) => new()
    {
        VoiceMessageId = value.VoiceMessageId,
        GeneratedUtc = value.GeneratedUtc,
        SourceHash = value.SourceHash,
        ProviderId = value.ProviderId,
        ProviderName = value.ProviderName,
        ProviderType = value.ProviderType,
        ModelId = value.ModelId,
        VoiceIds = value.VoiceIds.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
    };

    static RpTranscriptSnapshot Clone(RpTranscriptSnapshot value) => new()
    {
        Id = value.Id,
        TurnId = value.TurnId,
        CreatedUtc = value.CreatedUtc,
        Summary = value.Summary,
        Speech = Clone(value.Speech),
        PrivateIntentByCharacterId = value.PrivateIntentByCharacterId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        CharacterAppearances = value.CharacterAppearances.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        Scene = Clone(value.Scene),
        Trace = value.Trace is null ? null : Clone(value.Trace),
        Data = Clone(value.Data)
    };

    static RpTranscriptSnapshotTimelineEntry Clone(RpTranscriptSnapshotTimelineEntry value) => new()
    {
        TurnNumber = value.TurnNumber,
        Title = value.Title,
        Description = value.Description,
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
        CharacterPhysicalStates = value.CharacterPhysicalStates.Select(Clone).ToList(),
        SceneObjects = value.SceneObjects.Select(Clone).ToList(),
        Data = Clone(value.Data)
    };

    static RpCharacterPhysicalState Clone(RpCharacterPhysicalState value) => new()
    {
        CharacterId = value.CharacterId,
        Location = value.Location,
        Posture = value.Posture,
        Head = value.Head,
        LeftArm = value.LeftArm,
        RightArm = value.RightArm,
        LeftHand = value.LeftHand,
        RightHand = value.RightHand,
        LeftLeg = value.LeftLeg,
        RightLeg = value.RightLeg,
        LeftFoot = value.LeftFoot,
        RightFoot = value.RightFoot,
        Contact = value.Contact,
        Summary = value.Summary
    };

    static RpSceneObjectState Clone(RpSceneObjectState value) => new()
    {
        Id = value.Id,
        Name = value.Name,
        OwnerCharacterId = value.OwnerCharacterId,
        HolderCharacterId = value.HolderCharacterId,
        HeldBodyPart = value.HeldBodyPart,
        Location = value.Location,
        State = value.State,
        Summary = value.Summary
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
        ContinuityIntents = value.ContinuityIntents.Select(Clone).ToList(),
        Data = Clone(value.Data)
    };

    static RpPhysicalContinuityIntent Clone(RpPhysicalContinuityIntent value) => new()
    {
        Kind = value.Kind,
        CharacterName = value.CharacterName,
        CharacterId = value.CharacterId,
        BodyPart = value.BodyPart,
        ObjectName = value.ObjectName,
        ObjectId = value.ObjectId,
        Target = value.Target,
        Change = value.Change,
        ClearsStaleState = value.ClearsStaleState
    };

    public static RpTurnTrace Clone(RpTurnTrace value) => new()
    {
        Summary = value.Summary,
        Status = value.Status,
        StartedUtc = value.StartedUtc,
        CompletedUtc = value.CompletedUtc,
        ProviderId = value.ProviderId,
        ProviderName = value.ProviderName,
        ProviderType = value.ProviderType,
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
        ProviderType = value.ProviderType,
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

    public static PromptLibraryState Clone(PromptLibraryState value)
    {
        var overrides = PromptLibraryService.CreateOverridesFromResolved(value);
        return new()
        {
            PromptOverrides = overrides.PromptOverrides.ToDictionary(pair => pair.Key, pair => new PromptPairOverrideState { System = pair.Value.System, User = pair.Value.User }),
            TurnShapeOverrides = overrides.TurnShapeOverrides.ToDictionary(pair => pair.Key, pair => pair.Value.Select(Clone).ToList())
        };
    }

    static ShapePromptOverrideState Clone(ShapePromptOverrideState value) => new()
    {
        Id = value.Id,
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
        HairColors = value.HairColors.Select(Clone).ToList(),
        HairStyles = value.HairStyles.Select(Clone).ToList(),
        EyeColors = value.EyeColors.Select(Clone).ToList(),
        FaceShapes = value.FaceShapes.Select(Clone).ToList(),
        SkinTones = value.SkinTones.Select(Clone).ToList(),
        Complexions = value.Complexions.Select(Clone).ToList(),
        Heights = value.Heights.Select(Clone).ToList(),
        Builds = value.Builds.Select(Clone).ToList(),
        BodyProportions = value.BodyProportions.Select(Clone).ToList(),
        Presentations = value.Presentations.Select(Clone).ToList(),
        AttractivenessLevels = value.AttractivenessLevels.Select(Clone).ToList(),
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
