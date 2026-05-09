using System.Text;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed class TranscriptPromptContextBuilder(SceneTransitionService? sceneTransitionService = null)
{
    readonly SceneTransitionService _sceneTransitionService = sceneTransitionService ?? new();

    public TurnPromptContext BuildTurnContext(
        RpChatDocument document,
        string parentTurnId,
        string guidance,
        string requestedTurnShape,
        RpCharacter? requestedActor,
        bool requestedNarrator = false,
        RpSceneFrame? sceneOverride = null,
        IReadOnlyDictionary<string, string>? appearanceOverrides = null)
    {
        TranscriptTurnNumbering.EnsureTurnNumbers(document.Transcript);
        var activePath = TranscriptGraph.GetActivePath(document.Transcript);
        if (!string.IsNullOrWhiteSpace(parentTurnId))
        {
            var parentIndex = activePath.FindIndex(turn => turn.Id == parentTurnId);
            if (parentIndex >= 0)
                activePath = activePath.Take(parentIndex + 1).ToList();
        }

        var snapshot = activePath.Count == 0
            ? null
            : document.Transcript.Snapshots
                .Where(candidate => activePath.Any(turn => turn.Id == candidate.TurnId))
                .OrderBy(candidate => candidate.CreatedUtc)
                .LastOrDefault();
        var snapshotTurnIndex = snapshot is null
            ? -1
            : activePath.FindIndex(turn => turn.Id == snapshot.TurnId);
        var transcriptTurns = snapshotTurnIndex >= 0
            ? activePath.Skip(snapshotTurnIndex + 1).ToList()
            : activePath;

        var scene = sceneOverride ?? TranscriptGraph.GetSceneForNextTurn(document.Transcript, parentTurnId);
        var presentCharacters = document.Characters.Where(character => scene.InSceneCharacterIds.Contains(character.Id)).ToList();
        var otherCharacters = document.Characters.Where(character => !scene.InSceneCharacterIds.Contains(character.Id)).ToList();
        var presentItems = document.Items.Where(item => scene.InSceneItemIds.Contains(item.Id)).ToList();
        var characterAppearances = BuildAppearanceMap(snapshot, activePath, snapshotTurnIndex);
        ApplyAppearanceOverrides(characterAppearances, appearanceOverrides);
        var traitLibrary = CharacterTraitLibraryService.NormalizeState(document.CharacterTraitLibrary);
        var actor = requestedNarrator ? null : requestedActor ?? presentCharacters.FirstOrDefault() ?? document.Characters.FirstOrDefault();
        var activeSpeakerName = ResolveActiveSpeakerName(activePath.LastOrDefault());
        var requestedShape = string.IsNullOrWhiteSpace(requestedTurnShape) ? "Brief" : requestedTurnShape.Trim();
        var currentLocation = document.Locations.FirstOrDefault(location => location.Id == scene.LocationId);
        var characterNames = document.Characters.ToDictionary(character => character.Id, character => character.Name, StringComparer.Ordinal);
        var actorText = FormatActor(actor, document.NarratorProfile, requestedNarrator);
        var locationText = FormatLocation(currentLocation, scene);
        var charactersText = FormatCharactersInScene(presentCharacters, actor);
        var relationshipsText = FormatRelationships(document.CharacterRelationships, presentCharacters, actor, characterNames);
        var otherCharactersText = FormatOtherKnownCharacters(otherCharacters);
        var objectsText = FormatObjectsInScene(presentItems, scene, presentCharacters);
        var chatDirection = ChatDirectionService.NormalizeState(document.ChatDirection);
        var storyContextText = FormatStoryContext(document);
        var contentGuidanceText = ChatDirectionService.BuildContentGuidance(chatDirection);
        var historySummaryText = FormatHistorySummary(document);
        var snapshotText = snapshot?.Summary ?? "";
        var characterAppearancesText = FormatCharacterAppearances(presentCharacters, characterAppearances, traitLibrary);
        var physicalSceneStateText = FormatPhysicalSceneState(scene, presentCharacters);
        var continuityPlanIntentsText = FormatContinuityPlanIntents(transcriptTurns, document.Characters);
        var transcriptText = FormatTranscriptWithTurnStart(
            FormatTranscriptWithEarlierSummary(FormatTranscript(document, transcriptTurns, snapshot?.Scene), snapshotText),
            transcriptTurns);
        var planningProseTranscriptText = FormatTranscriptWithEarlierSummary(
            FormatTranscript(
                document,
                transcriptTurns,
                snapshot?.Scene,
                requestedNarrator ? PrivateIntentTranscriptScope.All : PrivateIntentTranscriptScope.ActorOnly,
                actor?.Id ?? ""),
            snapshotText);
        planningProseTranscriptText = FormatTranscriptWithTurnStart(planningProseTranscriptText, transcriptTurns);
        var contextText = BuildContextText(
            actorText,
            locationText,
            charactersText,
            relationshipsText,
            otherCharactersText,
            objectsText,
            storyContextText,
            contentGuidanceText,
            historySummaryText,
            FormatTranscriptSection(transcriptText),
            FormatCharacterAppearancesSection(characterAppearancesText),
            FormatPhysicalSceneStateSection(physicalSceneStateText));
        var planningProseContextText = BuildContextText(
            actorText,
            locationText,
            charactersText,
            relationshipsText,
            otherCharactersText,
            objectsText,
            storyContextText,
            contentGuidanceText,
            historySummaryText,
            FormatTranscriptSection(planningProseTranscriptText),
            FormatCharacterAppearancesSection(characterAppearancesText),
            FormatPhysicalSceneStateSection(physicalSceneStateText),
            FormatContinuityPlanIntentsSection(continuityPlanIntentsText));

        return new(
            Actor: actor,
            ActiveSpeakerName: activeSpeakerName,
            ContextText: contextText,
            PlanningProseContextText: planningProseContextText,
            ActorText: actorText,
            LocationText: locationText,
            CharactersText: charactersText,
            RelationshipsText: relationshipsText,
            OtherKnownCharactersText: otherCharactersText,
            ObjectsText: objectsText,
            StoryContextText: storyContextText,
            ContentGuidanceText: contentGuidanceText,
            ExplicitContentLabel: ChatDirectionService.FormatIntensityLabel(chatDirection.ExplicitContent),
            ViolentContentLabel: ChatDirectionService.FormatIntensityLabel(chatDirection.ViolentContent),
            HistorySummaryText: historySummaryText,
            SnapshotText: snapshotText,
            TranscriptText: transcriptText,
            PlanningProseTranscriptText: planningProseTranscriptText,
            CharactersInSceneText: charactersText,
            CharacterAppearancesText: characterAppearancesText,
            PhysicalSceneStateText: physicalSceneStateText,
            ContinuityPlanIntentsText: continuityPlanIntentsText,
            AppearanceCharactersText: FormatAppearanceCharacters(presentCharacters, characterAppearances, traitLibrary),
            AppearanceTranscriptText: FormatTranscriptWithEarlierSummary(FormatTranscript(document, transcriptTurns, snapshot?.Scene), snapshotText),
            SelectionEligibleResponders: FormatEligibleResponders(presentCharacters, activeSpeakerName, characterAppearances, traitLibrary),
            SelectionLocationSection: string.IsNullOrWhiteSpace(locationText) ? "" : locationText + Environment.NewLine,
            SelectionCurrentAppearance: FormatSelectionAppearance(characterAppearancesText),
            Guidance: guidance.Trim(),
            GuidanceSection: string.IsNullOrWhiteSpace(guidance) ? "" : $"Use this guidance to compose the next message: {guidance.Trim()}",
            RequestedTurnShape: requestedShape,
            RequestedTurnShapeSection: BuildRequestedTurnShapeSection(document, requestedShape),
            PlanningTurnShapeDefinitions: FormatTurnShapeDefinitions(document, PromptLibraryStageIds.Planning),
            ProseInSceneNames: FormatProseInSceneNames(presentCharacters, actor),
            NarratorGuidance: requestedNarrator ? NarratorProfileService.BuildPromptGuidance(document.NarratorProfile) : "",
            TurnScopeRules: requestedNarrator ? BuildNarratorTurnScopeRules() : BuildTurnScopeRules(actor?.Name ?? "Narrator"));
    }

    public SnapshotPromptContext BuildSnapshotContext(RpChatDocument document, string turnId)
    {
        TranscriptTurnNumbering.EnsureTurnNumbers(document.Transcript);
        var activePath = TranscriptGraph.GetActivePath(document.Transcript);
        var turnIndex = activePath.FindIndex(turn => turn.Id == turnId);
        if (turnIndex >= 0)
            activePath = activePath.Take(turnIndex + 1).ToList();

        var latestSnapshot = activePath.Count == 0
            ? null
            : document.Transcript.Snapshots
                .Where(candidate => activePath.Any(turn => turn.Id == candidate.TurnId))
                .OrderBy(candidate => candidate.CreatedUtc)
                .LastOrDefault();
        var snapshotTurnIndex = latestSnapshot is null
            ? -1
            : activePath.FindIndex(turn => turn.Id == latestSnapshot.TurnId);
        var snapshotTurns = snapshotTurnIndex >= 0
            ? activePath.Skip(snapshotTurnIndex + 1).ToList()
            : activePath;
        var currentTurn = activePath.LastOrDefault();
        var scene = currentTurn?.Scene ?? document.Transcript.RootScene;
        var presentCharacters = document.Characters.Where(character => scene.InSceneCharacterIds.Contains(character.Id)).ToList();
        var characterAppearances = BuildAppearanceMap(latestSnapshot, activePath, snapshotTurnIndex);
        var traitLibrary = CharacterTraitLibraryService.NormalizeState(document.CharacterTraitLibrary);
        var currentLocation = document.Locations.FirstOrDefault(location => location.Id == scene.LocationId)?.Name;
        var physicalSceneStateText = FormatPhysicalSceneState(scene, presentCharacters);

        return new(
            ThreadTitle: document.Chat.Title,
            CurrentLocation: string.IsNullOrWhiteSpace(currentLocation) ? "None" : currentLocation,
            Characters: FormatCharacterReferences(document.Characters),
            Locations: FormatReferenceNames(document.Locations.Select(location => location.Name)),
            Items: FormatReferenceNames(document.Items.Select(item => item.Name)),
            History: FormatSnapshotHistory(document),
            Messages: FormatSnapshotMessages(snapshotTurns),
            TranscriptText: FormatTranscriptWithEarlierSummary(FormatTranscript(document, snapshotTurns, latestSnapshot?.Scene, includeTurnLabels: true), latestSnapshot?.Summary ?? ""),
            CharacterAppearancesText: FormatCharacterAppearances(presentCharacters, characterAppearances, traitLibrary),
            PhysicalSceneStateText: physicalSceneStateText);
    }

    public Dictionary<string, string> BuildTokens(TurnPromptContext context, string planningOutput, string stageId = "")
    {
        var actorName = context.Actor?.Name ?? "Narrator";
        var includePrivateIntentTranscript = stageId is PromptLibraryStageIds.Planning or PromptLibraryStageIds.Prose;
        var contextText = includePrivateIntentTranscript ? context.PlanningProseContextText : context.ContextText;
        var transcriptText = includePrivateIntentTranscript ? context.PlanningProseTranscriptText : context.TranscriptText;
        return new(StringComparer.Ordinal)
        {
            ["{content.explicitLabel}"] = context.ExplicitContentLabel,
            ["{content.violentLabel}"] = context.ViolentContentLabel,
            ["{sceneContinuity.characters}"] = context.AppearanceCharactersText,
            ["{sceneContinuity.transcript}"] = context.AppearanceTranscriptText,
            ["{sceneContinuity.sceneState}"] = context.PhysicalSceneStateText,
            ["{sceneContinuity.planIntents}"] = context.ContinuityPlanIntentsText,
            ["{appearance.characters}"] = context.AppearanceCharactersText,
            ["{appearance.transcript}"] = context.AppearanceTranscriptText,
            ["{selection.activeSpeakerName}"] = context.ActiveSpeakerName,
            ["{selection.guidanceSection}"] = string.IsNullOrWhiteSpace(context.Guidance) ? "" : $"**Guidance:** {context.Guidance}\n",
            ["{selection.eligibleResponders}"] = context.SelectionEligibleResponders,
            ["{selection.locationSection}"] = context.SelectionLocationSection,
            ["{selection.storyContext}"] = FormatOptionalSection(context.StoryContextText),
            ["{selection.contentGuidance}"] = FormatOptionalSection(context.ContentGuidanceText),
            ["{selection.recentTranscript}"] = FormatSelectionTranscript(context.TranscriptText),
            ["{selection.currentAppearance}"] = context.SelectionCurrentAppearance,
            ["{context}"] = contextText,
            ["{context.actor}"] = context.ActorText,
            ["{context.location}"] = context.LocationText,
            ["{context.charactersInScene}"] = context.CharactersInSceneText,
            ["{context.relationships}"] = context.RelationshipsText,
            ["{context.otherKnownCharacters}"] = context.OtherKnownCharactersText,
            ["{context.objectsInScene}"] = context.ObjectsText,
            ["{context.storyContext}"] = context.StoryContextText,
            ["{context.contentGuidance}"] = context.ContentGuidanceText,
            ["{context.historySummary}"] = context.HistorySummaryText,
            ["{context.snapshot}"] = context.SnapshotText,
            ["{context.transcript}"] = transcriptText,
            ["{context.characterAppearances}"] = context.CharacterAppearancesText,
            ["{context.earlierPrivateIntentContinuity}"] = "",
            ["{actor.name}"] = actorName,
            ["{speaker.name}"] = actorName,
            ["{guidance}"] = context.Guidance,
            ["{guidanceSection}"] = context.GuidanceSection,
            ["{requestedTurnShape}"] = context.RequestedTurnShape,
            ["{requestedTurnShapeSection}"] = context.RequestedTurnShapeSection,
            ["{planning.turnShapeDefinitions}"] = context.PlanningTurnShapeDefinitions,
            ["{turnScopeRules}"] = context.TurnScopeRules
        };
    }

    public Dictionary<string, string> BuildProseTokens(
        TurnPromptContext context,
        string planningOutput,
        string turnShape,
        string beat,
        string intent,
        string immediateGoal,
        string whyNow,
        string changeIntroduced,
        string privateIntent,
        string narrativeGuardrails,
        PromptLibraryState promptLibrary)
    {
        var tokens = BuildTokens(context, planningOutput, PromptLibraryStageIds.Prose);
        var isNarrator = context.Actor is null;
        var speaker = isNarrator ? "the narrator" : context.Actor!.Name;
        var turnShapeSystem = PromptLibraryService.BuildDefaultProseSystemTurnShape(turnShape);
        tokens["{speaker.name}"] = speaker;
        tokens["{prose.inSceneNames}"] = context.ProseInSceneNames;
        tokens["{prose.turnShapeSystem}"] = turnShapeSystem;
        tokens["{prose.turnShapeUser}"] = isNarrator
            ? PromptLibraryService.BuildDefaultNarratorProseTurnShape(turnShape)
            : PromptLibraryService.GetTurnShapeTemplate(promptLibrary, PromptLibraryStageIds.Prose, turnShape);
        tokens["{prose.narratorSystem}"] = isNarrator
            ? BuildNarratorProseRules(context.NarratorGuidance)
            : string.Empty;
        tokens["{prose.characterOnlySystem}"] = isNarrator
            ? string.Empty
            : $"{turnShapeSystem}\n{BuildProseRules(speaker)}";
        tokens["{planner.beat}"] = beat;
        tokens["{planner.intent}"] = intent;
        tokens["{planner.immediateGoal}"] = immediateGoal;
        tokens["{planner.changeIntroduced}"] = changeIntroduced;
        tokens["{planner.whyNow}"] = whyNow;
        tokens["{planner.privateIntent}"] = privateIntent;
        tokens["{planner.narrativeGuardrails}"] = string.IsNullOrWhiteSpace(narrativeGuardrails) ? "None" : narrativeGuardrails;
        tokens["{guidanceSection}"] = string.IsNullOrWhiteSpace(context.Guidance)
            ? string.Empty
            : $"**Guidance to follow strictly:**\n{context.Guidance.Trim()}\n";
        return tokens;
    }

    public Dictionary<string, string> BuildTokens(SnapshotPromptContext context) => new(StringComparer.Ordinal)
    {
        ["{snapshot.threadTitle}"] = context.ThreadTitle,
        ["{snapshot.currentLocation}"] = context.CurrentLocation,
        ["{snapshot.characters}"] = context.Characters,
        ["{snapshot.locations}"] = context.Locations,
        ["{snapshot.items}"] = context.Items,
        ["{snapshot.history}"] = context.History,
        ["{snapshot.messages}"] = context.Messages,
        ["{context.transcript}"] = context.TranscriptText,
        ["{context.characterAppearances}"] = context.CharacterAppearancesText,
        ["{context.physicalSceneState}"] = context.PhysicalSceneStateText,
        ["{context.earlierPrivateIntentContinuity}"] = ""
    };

    static Dictionary<string, string> BuildAppearanceMap(RpTranscriptSnapshot? snapshot, IReadOnlyList<RpTranscriptTurn> path, int snapshotTurnIndex)
    {
        var appearances = snapshot?.CharacterAppearances.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var startIndex = snapshotTurnIndex >= 0 ? snapshotTurnIndex + 1 : 0;
        foreach (var turn in path.Skip(startIndex))
        {
            foreach (var pair in turn.AppearanceByCharacterId)
                appearances[pair.Key] = pair.Value;
        }

        return appearances;
    }

    static void ApplyAppearanceOverrides(Dictionary<string, string> appearances, IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides is null)
            return;

        foreach (var pair in overrides)
            appearances[pair.Key] = pair.Value;
    }

    static string ResolveCharacterName(IEnumerable<RpCharacter> characters, string characterId) =>
        characters.FirstOrDefault(character => character.Id == characterId)?.Name ?? characterId;

    string FormatTranscript(
        RpChatDocument document,
        IEnumerable<RpTranscriptTurn> turns,
        RpSceneFrame? baselineScene = null,
        PrivateIntentTranscriptScope privateIntentScope = PrivateIntentTranscriptScope.None,
        string actorCharacterId = "",
        bool includeTurnLabels = false)
    {
        var blocks = new List<string>();
        var previousScene = baselineScene;
        foreach (var turn in turns.Where(turn => !string.IsNullOrWhiteSpace(turn.Body)))
        {
            var builder = new StringBuilder();
            if (previousScene is not null)
            {
                var delta = _sceneTransitionService.BuildDelta(document, previousScene, turn.Scene);
                var transition = _sceneTransitionService.FormatForTranscript(delta);
                if (!string.IsNullOrWhiteSpace(transition))
                    builder.AppendLine(transition.Trim());
            }

            if (builder.Length > 0)
                builder.AppendLine();

            AppendPrivateIntentLines(builder, document.Characters, turn, privateIntentScope, actorCharacterId);
            var speakerLine = $"{ResolveActiveSpeakerName(turn)}: {turn.Body.Trim()}";
            builder.Append(includeTurnLabels ? $"{TranscriptTurnNumbering.Format(turn)} - {speakerLine}" : speakerLine);
            blocks.Add(builder.ToString());
            previousScene = turn.Scene;
        }

        var content = string.Join("\n\n", blocks);
        return string.IsNullOrWhiteSpace(content) ? "(No transcript yet.)" : content;
    }

    static void AppendPrivateIntentLines(
        StringBuilder builder,
        IReadOnlyList<RpCharacter> characters,
        RpTranscriptTurn turn,
        PrivateIntentTranscriptScope scope,
        string actorCharacterId)
    {
        var intents = ResolvePrivateIntents(characters, turn, scope, actorCharacterId);
        foreach (var intent in intents)
            builder.AppendLine($"[{intent.CharacterName}'s private intent: {PromptInlineText(intent.Value)}]");
    }

    static IReadOnlyList<(string CharacterName, string Value)> ResolvePrivateIntents(
        IReadOnlyList<RpCharacter> characters,
        RpTranscriptTurn turn,
        PrivateIntentTranscriptScope scope,
        string actorCharacterId)
    {
        if (scope == PrivateIntentTranscriptScope.None || turn.PrivateIntentByCharacterId.Count == 0)
            return [];

        if (scope == PrivateIntentTranscriptScope.ActorOnly)
        {
            if (string.IsNullOrWhiteSpace(actorCharacterId) || turn.ActorCharacterId != actorCharacterId)
                return [];

            return turn.PrivateIntentByCharacterId.TryGetValue(actorCharacterId, out var privateIntent) && !string.IsNullOrWhiteSpace(privateIntent)
                ? [(ResolveCharacterName(characters, actorCharacterId), privateIntent.Trim())]
                : [];
        }

        return turn.PrivateIntentByCharacterId
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => (CharacterName: ResolveCharacterName(characters, pair.Key), Value: pair.Value.Trim()))
            .OrderBy(pair => pair.CharacterName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static string FormatSelectionTranscript(string transcriptText)
    {
        if (transcriptText == "(No transcript yet.)")
            return "- None";

        return string.Join(
            Environment.NewLine,
            transcriptText
                .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => $"- {CollapseWhitespace(line)}"));
    }

    static string FormatActor(
        RpCharacter? actor,
        NarratorProfileState narratorProfile,
        bool requestedNarrator)
    {
        if (actor is null)
        {
            if (!requestedNarrator)
                return "**Actor:** Narrator";

            return $"**Actor:** Narrator{Environment.NewLine}{NarratorProfileService.BuildPromptGuidance(narratorProfile)}";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"**Actor:** {actor.Name}");
        AppendList(builder, "Pronouns", actor.Pronouns);
        AppendField(builder, "Backstory", actor.Backstory);
        AppendField(builder, "Surface mask", actor.SurfaceMask);
        AppendField(builder, "Sentence style", actor.SentenceStyle);
        AppendField(builder, "Honesty style", actor.HonestyStyle);
        AppendField(builder, "Emotional leakage", actor.EmotionalLeakage);
        AppendField(builder, "Action fingerprint", actor.ActionFingerprint);
        AppendField(builder, "Stress pattern", actor.StressPattern);
        AppendList(builder, "Scene roles", actor.SceneRoles);
        AppendList(builder, "Drives", actor.Drives);
        AppendList(builder, "Soft spots", actor.SoftSpots);
        AppendList(builder, "Avoid patterns", actor.AvoidPatterns);
        AppendField(builder, "Notes", actor.Notes);
        return builder.ToString().TrimEnd();
    }

    static string FormatRelationships(
        IReadOnlyList<RpCharacterRelationship> relationships,
        IReadOnlyList<RpCharacter> sceneCharacters,
        RpCharacter? actor,
        IReadOnlyDictionary<string, string> characterNames)
    {
        var relevantCharacterIds = sceneCharacters.Select(character => character.Id).ToHashSet(StringComparer.Ordinal);
        if (actor is not null)
            relevantCharacterIds.Add(actor.Id);

        var lines = relationships
            .Where(relationship => relevantCharacterIds.Contains(relationship.CharacterAId) || relevantCharacterIds.Contains(relationship.CharacterBId))
            .OrderBy(relationship => characterNames.GetValueOrDefault(relationship.CharacterAId, ""), StringComparer.OrdinalIgnoreCase)
            .ThenBy(relationship => characterNames.GetValueOrDefault(relationship.CharacterBId, ""), StringComparer.OrdinalIgnoreCase)
            .Select(relationship => FormatRelationshipLine(relationship, actor, characterNames))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        return lines.Count == 0
            ? ""
            : $"**Relationships:**{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    static string FormatRelationshipLine(
        RpCharacterRelationship relationship,
        RpCharacter? actor,
        IReadOnlyDictionary<string, string> characterNames)
    {
        var characterAName = characterNames.GetValueOrDefault(relationship.CharacterAId, "");
        var characterBName = characterNames.GetValueOrDefault(relationship.CharacterBId, "");
        if (string.IsNullOrWhiteSpace(characterAName) || string.IsNullOrWhiteSpace(characterBName))
            return "";

        var actorView = actor is not null && RelationshipContains(relationship, actor.Id)
            ? CharacterRelationshipGraph.View(
                relationship,
                actor.Id,
                actor.Name,
                relationship.CharacterAId == actor.Id ? relationship.CharacterBId : relationship.CharacterAId,
                relationship.CharacterAId == actor.Id ? characterBName : characterAName)
            : null;
        var details = new List<string>();
        AppendRelationshipValues(details, relationship.Bonds);
        AppendRelationshipValues(details, relationship.Dynamics);

        var visibleNote = actorView is not null
            ? actorView.HowSourceSeesTarget
            : relationship.NoteExternal;
        if (!string.IsNullOrWhiteSpace(visibleNote))
            details.Add(PromptInlineText(visibleNote));

        return details.Count == 0
            ? $"- {characterAName} and {characterBName}"
            : $"- {characterAName} and {characterBName}: {string.Join("; ", details)}";
    }

    static bool RelationshipContains(RpCharacterRelationship relationship, string characterId) =>
        relationship.CharacterAId == characterId || relationship.CharacterBId == characterId;

    static void AppendRelationshipValues(List<string> details, IReadOnlyList<string> values)
    {
        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => PromptInlineText(value))
            .ToList();
        if (normalized.Count > 0)
            details.Add(string.Join(", ", normalized));
    }

    static string FormatLocation(RpLocation? location, RpSceneFrame scene)
    {
        var name = location?.Name ?? scene.LocationName;
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var builder = new StringBuilder();
        builder.AppendLine($"**Location:** {name}");
        if (location is not null)
        {
            AppendField(builder, "Summary", location.Summary);
            AppendField(builder, "Description", location.Description);
            AppendField(builder, "Atmosphere", location.Atmosphere);
            AppendField(builder, "Features", location.Features);
        }

        return builder.ToString().TrimEnd();
    }

    static string FormatCharactersInScene(
        IEnumerable<RpCharacter> characters,
        RpCharacter? actor)
    {
        var values = characters.ToList();
        if (values.Count == 0)
            return "**Characters in the scene:** None";

        var builder = new StringBuilder();
        builder.AppendLine("**Characters in the scene:**");
        foreach (var character in values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var role = actor?.Id == character.Id ? "current actor" : "in scene";
            builder.AppendLine().AppendLine($"**{character.Name}:** {string.Join(", ", [..character.Pronouns, role])}");
            AppendList(builder, "Pronouns", character.Pronouns);
            AppendField(builder, "Summary", character.Summary);
            AppendField(builder, "Voice", character.Voice);
            AppendField(builder, "Personality", character.Personality);
            AppendField(builder, "Core drive", character.CoreDrive);
            AppendField(builder, "Core fear", character.CoreFear);
            AppendField(builder, "Hidden truth", character.HiddenTruth);
            AppendList(builder, "Traits", character.Traits);
            AppendList(builder, "Limits", character.Limits);
        }

        return builder.ToString().TrimEnd();
    }

    static string FormatOtherKnownCharacters(IEnumerable<RpCharacter> characters)
    {
        var names = characters
            .Where(character => !string.IsNullOrWhiteSpace(character.Name))
            .OrderBy(character => character.Name, StringComparer.OrdinalIgnoreCase)
            .Select(FormatCharacterReference)
            .ToList();
        return names.Count == 0
            ? ""
            : $"**Other known characters:** {string.Join(", ", names)}";
    }

    static string FormatObjectsInScene(
        IEnumerable<RpItem> items,
        RpSceneFrame scene,
        IReadOnlyList<RpCharacter> characters)
    {
        var values = items.ToList();
        var sceneObjects = scene.SceneObjects
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) || !string.IsNullOrWhiteSpace(item.Summary))
            .ToList();
        if (values.Count == 0 && sceneObjects.Count == 0)
            return "";

        var builder = new StringBuilder();
        builder.AppendLine("**Objects in the scene:**");
        foreach (var item in values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- **{item.Name}:** {PromptInlineText(item.Summary, "No summary")}");
            AppendField(builder, "  Description", item.Description);
            AppendField(builder, "  History", item.History);
            AppendField(builder, "  Properties", item.Properties);
        }

        foreach (var item in sceneObjects.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var name = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
            builder.AppendLine($"- **{PromptInlineText(name, "Object")}:** {PromptInlineText(item.Summary, "No summary")}");
            AppendField(builder, "  Owner", ResolveCharacterName(characters, item.OwnerCharacterId));
            AppendField(builder, "  Holder", ResolveCharacterName(characters, item.HolderCharacterId));
            AppendField(builder, "  Held by", item.HeldBodyPart);
            AppendField(builder, "  Location", item.Location);
            AppendField(builder, "  State", item.State);
        }

        return builder.ToString().TrimEnd();
    }

    static string FormatPhysicalSceneState(RpSceneFrame scene, IReadOnlyList<RpCharacter> characters)
    {
        var characterNames = characters.ToDictionary(character => character.Id, character => character.Name, StringComparer.Ordinal);
        var lines = new List<string>();
        foreach (var state in scene.CharacterPhysicalStates.Where(state => !string.IsNullOrWhiteSpace(state.CharacterId)))
        {
            var name = characterNames.TryGetValue(state.CharacterId, out var characterName) ? characterName : state.CharacterId;
            var details = new List<string>();
            Add(details, "location", state.Location);
            Add(details, "posture", state.Posture);
            Add(details, "head", state.Head);
            Add(details, "left arm", state.LeftArm);
            Add(details, "right arm", state.RightArm);
            Add(details, "left hand", state.LeftHand);
            Add(details, "right hand", state.RightHand);
            Add(details, "left leg", state.LeftLeg);
            Add(details, "right leg", state.RightLeg);
            Add(details, "left foot", state.LeftFoot);
            Add(details, "right foot", state.RightFoot);
            Add(details, "contact", state.Contact);
            Add(details, "summary", state.Summary);
            if (details.Count > 0)
                lines.Add($"- {name}: {string.Join("; ", details)}");
        }

        foreach (var item in scene.SceneObjects.Where(item => !string.IsNullOrWhiteSpace(item.Name) || !string.IsNullOrWhiteSpace(item.Summary)))
        {
            var details = new List<string>();
            Add(details, "owner", ResolveCharacterName(characters, item.OwnerCharacterId));
            Add(details, "holder", ResolveCharacterName(characters, item.HolderCharacterId));
            Add(details, "held by", item.HeldBodyPart);
            Add(details, "location", item.Location);
            Add(details, "state", item.State);
            Add(details, "summary", item.Summary);
            lines.Add($"- {PromptInlineText(item.Name, item.Id)}: {string.Join("; ", details)}");
        }

        return lines.Count == 0 ? "No physical scene ledger yet." : string.Join(Environment.NewLine, lines);

        static void Add(List<string> values, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                values.Add($"{label}: {PromptInlineText(value)}");
        }
    }

    static string FormatContinuityPlanIntents(IEnumerable<RpTranscriptTurn> turns, IReadOnlyList<RpCharacter> characters)
    {
        var lines = new List<string>();
        foreach (var turn in turns)
        {
            foreach (var intent in turn.Plan.ContinuityIntents)
            {
                var character = string.IsNullOrWhiteSpace(intent.CharacterName)
                    ? ResolveCharacterName(characters, intent.CharacterId)
                    : intent.CharacterName;
                var details = new List<string>();
                Add(details, "kind", intent.Kind);
                Add(details, "character", character);
                Add(details, "body part", intent.BodyPart);
                Add(details, "object", string.IsNullOrWhiteSpace(intent.ObjectName) ? intent.ObjectId : intent.ObjectName);
                Add(details, "target", intent.Target);
                Add(details, "change", intent.Change);
                if (intent.ClearsStaleState)
                    details.Add("clears stale state");
                if (details.Count > 0)
                    lines.Add($"- {TranscriptTurnNumbering.Format(turn)}: {string.Join("; ", details)}");
            }
        }

        return lines.Count == 0 ? "None" : string.Join(Environment.NewLine, lines);

        static void Add(List<string> values, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                values.Add($"{label}: {PromptInlineText(value)}");
        }
    }

    static string FormatStoryContext(RpChatDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("**Story context:**");
        AppendField(builder, "Title", document.Chat.Title);
        AppendField(builder, "Chat location", document.Chat.Location);
        var direction = ChatDirectionService.BuildStoryContext(document.ChatDirection);
        if (!string.IsNullOrWhiteSpace(direction))
            builder.AppendLine(direction);
        if (document.Timeline.Count > 0)
        {
            builder.AppendLine("- Timeline:");
            foreach (var entry in document.Timeline.OrderBy(x => x.Date, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase))
                builder.AppendLine($"  - {PromptInlineText(entry.Date, "Unplaced")}: {entry.Title} - {PromptInlineText(entry.Description, "No description")}");
        }

        return builder.ToString().TrimEnd();
    }

    static string FormatHistorySummary(RpChatDocument document)
    {
        var entries = document.Timeline
            .OrderBy(x => x.Date, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(x => $"{x.Title}: {PromptInlineText(x.Description, "No description")}")
            .ToList();
        return entries.Count == 0 ? "" : $"**History summary:** {string.Join(" | ", entries)}";
    }

    static string FormatSnapshotHistory(RpChatDocument document)
    {
        var entries = document.Timeline
            .Take(3)
            .Select(x => $"{x.Title}: {PromptInlineText(x.Description, "No summary")}")
            .ToList();
        return entries.Count == 0 ? "None" : string.Join(" | ", entries);
    }

    static string FormatSnapshotMessages(IEnumerable<RpTranscriptTurn> turns)
    {
        var lines = turns.Select(turn =>
        {
            var speaker = ResolveActiveSpeakerName(turn);
            return $"- {TranscriptTurnNumbering.Format(turn)} - {speaker}: {CollapseWhitespace(turn.Body)}";
        });
        var content = string.Join(Environment.NewLine, lines);
        return string.IsNullOrWhiteSpace(content) ? "- None" : content;
    }

    static string FormatReferenceNames(IEnumerable<string> names)
    {
        var values = names.Where(name => !string.IsNullOrWhiteSpace(name)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        return values.Count == 0 ? "None" : string.Join(", ", values);
    }

    static string FormatCharacterReferences(IEnumerable<RpCharacter> characters)
    {
        var values = characters
            .Where(character => !string.IsNullOrWhiteSpace(character.Name))
            .OrderBy(character => character.Name, StringComparer.OrdinalIgnoreCase)
            .Select(FormatCharacterReference)
            .ToList();
        return values.Count == 0 ? "None" : string.Join(", ", values);
    }

    static string FormatCharacterReference(RpCharacter character)
    {
        var pronouns = CharacterProfileRules.FormatPronouns(character.Pronouns);
        return string.IsNullOrWhiteSpace(pronouns)
            ? character.Name
            : $"{character.Name} ({pronouns})";
    }

    static string FormatAppearanceCharacters(IEnumerable<RpCharacter> characters, IReadOnlyDictionary<string, string> appearanceMap, CharacterTraitLibraryState library)
    {
        var lines = characters.Select(character =>
        {
            var appearance = CharacterAppearanceFormatter.FormatWithSceneState(
                character,
                library,
                appearanceMap.TryGetValue(character.Id, out var current) ? current : "");
            return $"- **{character.Name}:** {PromptInlineText(appearance, "None")}";
        });
        var content = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(content) ? "- None" : content;
    }

    static string FormatCharacterAppearances(IEnumerable<RpCharacter> characters, IReadOnlyDictionary<string, string> appearanceMap, CharacterTraitLibraryState library)
    {
        var lines = characters
            .Select(character =>
            {
                var appearance = CharacterAppearanceFormatter.FormatWithSceneState(
                    character,
                    library,
                    appearanceMap.TryGetValue(character.Id, out var current) ? current : "");
                return !string.IsNullOrWhiteSpace(appearance)
                    ? $"- {character.Name}: {appearance}"
                    : null;
            })
            .Where(line => !string.IsNullOrWhiteSpace(line));
        var content = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(content) ? "No appearance details." : content;
    }

    static string FormatEligibleResponders(IEnumerable<RpCharacter> characters, string activeSpeakerName, IReadOnlyDictionary<string, string> appearances, CharacterTraitLibraryState library)
    {
        var eligible = characters
            .Where(character => !string.Equals(character.Name, activeSpeakerName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(character => character.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (eligible.Count == 0)
            eligible = characters.OrderBy(character => character.Name, StringComparer.OrdinalIgnoreCase).ToList();

        if (eligible.Count == 0)
            return "- None";

        return string.Join(Environment.NewLine, eligible.Select(character =>
        {
            var appearance = CharacterAppearanceFormatter.FormatWithSceneState(character, library, appearances.TryGetValue(character.Id, out var current) ? current : "");
            var pronouns = CharacterProfileRules.FormatPronouns(character.Pronouns);
            var pronounText = string.IsNullOrWhiteSpace(pronouns) ? "" : $" | Pronouns: {pronouns}";
            return $"- {character.Name}: {PromptInlineText(character.Summary)}{pronounText} | Appearance: {PromptInlineText(appearance, "None")}";
        }));
    }

    static string FormatSelectionAppearance(string characterAppearancesText) =>
        string.IsNullOrWhiteSpace(characterAppearancesText) ? "No appearance details have been captured for this scene yet." : characterAppearancesText;

    static string FormatProseInSceneNames(IEnumerable<RpCharacter> characters, RpCharacter? actor)
    {
        var names = characters
            .Where(character => actor is null || character.Id != actor.Id)
            .Select(character => character.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return names.Count == 0 ? "the scene" : string.Join(", ", names);
    }

    static string BuildRequestedTurnShapeSection(RpChatDocument document, string requestedTurnShape)
    {
        return PromptLibraryService.FormatRequestedTurnShape(document.PromptLibrary, PromptLibraryStageIds.Planning, requestedTurnShape);
    }

    static string FormatTurnShapeDefinitions(RpChatDocument document, string stepId)
    {
        var normalized = PromptLibraryService.NormalizeState(document.PromptLibrary);
        return normalized.TurnShapes.TryGetValue(stepId, out var shapes)
            ? PromptLibraryService.FormatTurnShapeDefinitions(shapes)
            : "";
    }

    static string BuildTurnScopeRules(string actorName) =>
        $"""
        Turn scope rules:
        - {actorName} only
        - Choose one immediate beat, not a sequence.
        - React to the last turn only if it truly requires a response.
        - If the recent transcript includes scene transition lines, naturally react when {actorName} would plausibly notice or be first to respond.
        - Otherwise introduce a small new beat that adds value.
        - The beat should change something: pressure, focus, distance, tone, or uncertainty.
        - Avoid empty turns that only restate rules or repeat the current tension.
        - Keep it grounded and playable.
        """;

    static string BuildNarratorTurnScopeRules() =>
        """
        Turn scope rules:
        - Narrator staging only.
        - Choose one immediate summary or staging beat, not a sequence of character turns.
        - You may bridge elapsed time, travel, mundane logistics, low-stakes positioning, and already-established or user-approved offscreen continuity.
        - Preserve tension as setup, atmosphere, or physical arrangement without making any character react.
        - Do not write dialogue, internal monologue, new emotional reactions, new conflict moves, confessions, reveals, jokes, threats, attacks, or decisions for characters.
        - Stop once the scene is staged for the next character response.
        """;

    static string BuildNarratorProseRules(string narratorGuidance)
    {
        var guidance = string.IsNullOrWhiteSpace(narratorGuidance)
            ? ""
            : $"{Environment.NewLine}{narratorGuidance.Trim()}";
        return
            $"""
            Narrator contract:
            - You are the narrator, not a character.
            - NEVER speak as, quote, roleplay, decide for, or take a turn as any character.
            - Do not write character dialogue, internal monologue, new emotional reactions, jokes, threats, confessions, reveals, attacks, or decisions.
            - Do not continue the whole scene or write the next dramatic exchange.
            - You may summarize transitional action, elapsed time, travel, mundane logistics, and already-established or user-approved offscreen continuity.
            - You may resolve low-stakes positioning needed to reach the requested scene, such as who enters, where characters stand, who sits where, what they carry, and what they are visibly wearing.
            - You may preserve obvious tension as atmosphere, physical arrangement, or visible setup, but do not make a character react to it.
            - Focus on location, atmosphere, current mode, character positions, clothing, visible state, present items, and how the transition into this scene happened.
            - {PromptLibraryService.NarratorWardrobeGuidance}
            - End with the scene staged so a character can react next.
            - Write narration only, with no speaker labels, no meta commentary, and no tool/schema language.
            {guidance}
            """;
    }

    static string BuildProseRules(string speaker) =>
        $"""
        Rules:
        - Write only as {speaker}
        - Stay inside the current moment
        - Do not fast-forward
        - Do not resolve the whole exchange
        - Do not add a second move after the beat lands
        - Do not restate the same beat in another form
        - Prefer implication over explanation
        - Prefer one strong signal over several similar ones
        - Do not add meta text, labels, or turn markers
        - Do not write unwrapped narration

        Format:
        - Actions and non-spoken beats in *asterisks*
        - Spoken dialogue in "double quotes"
        - You may combine action and dialogue in the same line
        """;

    static string JoinSections(params string[] sections) =>
        string.Join(Environment.NewLine + Environment.NewLine, sections.Where(section => !string.IsNullOrWhiteSpace(section))).Trim();

    static string BuildContextText(params string[] sections) => JoinSections(sections);

    static string FormatTranscriptSection(string transcriptText) =>
        $"**Transcript:**{Environment.NewLine}{transcriptText}";

    static string FormatTranscriptWithTurnStart(string transcriptText, IReadOnlyList<RpTranscriptTurn> turns)
    {
        var startLine = TranscriptTurnNumbering.FormatTranscriptStart(turns);
        return string.IsNullOrWhiteSpace(startLine)
            ? transcriptText
            : $"{startLine}{Environment.NewLine}{Environment.NewLine}{transcriptText}";
    }

    static string FormatTranscriptWithEarlierSummary(string transcriptText, string earlierSummary)
    {
        if (string.IsNullOrWhiteSpace(earlierSummary))
            return transcriptText;

        var summary = $"Earlier transcript summary: {PromptInlineText(earlierSummary)}";
        return transcriptText == "(No transcript yet.)"
            ? $"{summary}{Environment.NewLine}{Environment.NewLine}(No transcript since that summary.)"
            : $"{summary}{Environment.NewLine}{Environment.NewLine}{transcriptText}";
    }

    static string FormatCharacterAppearancesSection(string characterAppearancesText) =>
        $"**Character appearances:**{Environment.NewLine}{characterAppearancesText}";

    static string FormatPhysicalSceneStateSection(string physicalSceneStateText) =>
        $"**Physical scene state:**{Environment.NewLine}{physicalSceneStateText}";

    static string FormatContinuityPlanIntentsSection(string continuityPlanIntentsText) =>
        $"**Continuity plan intents since last snapshot:**{Environment.NewLine}{continuityPlanIntentsText}";

    static string FormatOptionalSection(string text) =>
        string.IsNullOrWhiteSpace(text) ? "" : text.TrimEnd() + Environment.NewLine;

    static string PromptInlineText(string? value, string fallback = "Unknown") =>
        string.IsNullOrWhiteSpace(value) ? fallback : CollapseWhitespace(value);

    static string CollapseWhitespace(string value) =>
        string.Join(" ", value.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    static string ResolveActiveSpeakerName(RpTranscriptTurn? turn) =>
        string.IsNullOrWhiteSpace(turn?.ActorName)
            ? string.IsNullOrWhiteSpace(turn?.AuthorName) ? "Narrator" : turn.AuthorName
            : turn.ActorName;

    static void AppendField(StringBuilder builder, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            builder.AppendLine($"- {label}: {PromptInlineText(value)}");
    }

    static void AppendList(StringBuilder builder, string label, IReadOnlyList<string> values)
    {
        var normalized = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => PromptInlineText(value)).ToList();
        if (normalized.Count > 0)
            builder.AppendLine($"- {label}: {string.Join(", ", normalized)}");
    }

}

public sealed record TurnPromptContext(
    RpCharacter? Actor,
    string ActiveSpeakerName,
    string ContextText,
    string PlanningProseContextText,
    string ActorText,
    string LocationText,
    string CharactersText,
    string RelationshipsText,
    string OtherKnownCharactersText,
    string ObjectsText,
    string StoryContextText,
    string ContentGuidanceText,
    string ExplicitContentLabel,
    string ViolentContentLabel,
    string HistorySummaryText,
    string SnapshotText,
    string TranscriptText,
    string PlanningProseTranscriptText,
    string CharactersInSceneText,
    string CharacterAppearancesText,
    string PhysicalSceneStateText,
    string ContinuityPlanIntentsText,
    string AppearanceCharactersText,
    string AppearanceTranscriptText,
    string SelectionEligibleResponders,
    string SelectionLocationSection,
    string SelectionCurrentAppearance,
    string Guidance,
    string GuidanceSection,
    string RequestedTurnShape,
    string RequestedTurnShapeSection,
    string PlanningTurnShapeDefinitions,
    string NarratorGuidance,
    string ProseInSceneNames,
    string TurnScopeRules);

public sealed record SnapshotPromptContext(
    string ThreadTitle,
    string CurrentLocation,
    string Characters,
    string Locations,
    string Items,
    string History,
    string Messages,
    string TranscriptText,
    string CharacterAppearancesText,
    string PhysicalSceneStateText);

enum PrivateIntentTranscriptScope
{
    None,
    ActorOnly,
    All
}
