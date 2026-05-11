using System.Text;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed partial class TranscriptPromptContextBuilder
{
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
        var physicalSceneStateText = FormatPhysicalSceneState(scene, presentCharacters);
        var relationshipRefreshText = FormatSnapshotRelationshipRefresh(document, traitLibrary);
        var transcriptText = FormatSnapshotMessages(document, snapshotTurns, latestSnapshot?.Scene);

        return new(
            ThreadTitle: document.Chat.Title,
            CurrentLocation: FormatSnapshotCurrentLocation(document.Locations, scene),
            Characters: FormatCharacterReferences(document.Characters),
            CharacterDetails: FormatSnapshotCharacterDetails(document.Characters, BuildSnapshotCharacterIds(snapshotTurns, scene)),
            Locations: FormatLocationReferences(document.Locations),
            LocationDetails: FormatSnapshotLocationDetails(document.Locations, BuildSnapshotLocationIds(snapshotTurns, scene), scene),
            Items: FormatItemReferences(document.Items),
            History: FormatSnapshotHistory(document),
            Messages: transcriptText,
            TranscriptText: FormatTranscriptWithEarlierSummary(transcriptText == "- None" ? "(No transcript yet.)" : transcriptText, latestSnapshot?.Summary ?? ""),
            CharacterAppearancesText: FormatCharacterAppearances(presentCharacters, characterAppearances, traitLibrary),
            PhysicalSceneStateText: physicalSceneStateText,
            RelationshipRefreshText: relationshipRefreshText);
    }

    static HashSet<string> BuildSnapshotCharacterIds(IEnumerable<RpTranscriptTurn> turns, RpSceneFrame finalScene)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        AddSceneCharacterIds(ids, finalScene);
        foreach (var turn in turns)
        {
            Add(ids, turn.AuthorCharacterId);
            Add(ids, turn.ActorCharacterId);
            AddSceneCharacterIds(ids, turn.Scene);
            foreach (var characterId in turn.PrivateIntentByCharacterId.Keys)
                Add(ids, characterId);
        }

        return ids;

        static void AddSceneCharacterIds(HashSet<string> ids, RpSceneFrame scene)
        {
            foreach (var characterId in scene.InSceneCharacterIds)
                Add(ids, characterId);
            foreach (var state in scene.CharacterPhysicalStates)
                Add(ids, state.CharacterId);
            foreach (var item in scene.SceneObjects)
            {
                Add(ids, item.OwnerCharacterId);
                Add(ids, item.HolderCharacterId);
            }
        }
    }

    static HashSet<string> BuildSnapshotLocationIds(IEnumerable<RpTranscriptTurn> turns, RpSceneFrame finalScene)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Add(ids, finalScene.LocationId);
        foreach (var turn in turns)
            Add(ids, turn.Scene.LocationId);
        return ids;
    }

    static string FormatSnapshotCharacterDetails(IEnumerable<RpCharacter> characters, IReadOnlySet<string> characterIds)
    {
        var values = characters
            .Where(character => characterIds.Contains(character.Id))
            .OrderBy(character => character.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (values.Count == 0)
            return "None";

        var builder = new StringBuilder();
        foreach (var character in values)
        {
            if (builder.Length > 0)
                builder.AppendLine();
            builder.AppendLine($"**{character.Name}** (id: {character.Id})");
            AppendCharacterCorePromptDetails(builder, character);
            AppendField(builder, "Backstory", character.Backstory);
            AppendField(builder, "Surface mask", character.SurfaceMask);
            AppendField(builder, "Sentence style", character.SentenceStyle);
            AppendField(builder, "Honesty style", character.HonestyStyle);
            AppendField(builder, "Emotional leakage", character.EmotionalLeakage);
            AppendField(builder, "Action fingerprint", character.ActionFingerprint);
            AppendField(builder, "Stress pattern", character.StressPattern);
            AppendList(builder, "Scene roles", character.SceneRoles);
            AppendList(builder, "Drives", character.Drives);
            AppendList(builder, "Soft spots", character.SoftSpots);
            AppendList(builder, "Avoid patterns", character.AvoidPatterns);
            AppendField(builder, "Notes", character.Notes);
        }

        return builder.ToString().TrimEnd();
    }

    static void AppendCharacterCorePromptDetails(StringBuilder builder, RpCharacter character)
    {
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

    static string FormatSnapshotLocationDetails(IEnumerable<RpLocation> locations, IReadOnlySet<string> locationIds, RpSceneFrame finalScene)
    {
        var values = locations
            .Where(location => locationIds.Contains(location.Id))
            .OrderBy(location => location.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (values.Count == 0)
            return string.IsNullOrWhiteSpace(finalScene.LocationName) ? "None" : $"**{finalScene.LocationName}** (id: none)";

        var builder = new StringBuilder();
        foreach (var location in values)
        {
            if (builder.Length > 0)
                builder.AppendLine();
            builder.AppendLine($"**{location.Name}** (id: {location.Id})");
            AppendField(builder, "Summary", location.Summary);
            AppendField(builder, "Description", location.Description);
            AppendField(builder, "Atmosphere", location.Atmosphere);
            AppendField(builder, "Features", location.Features);
        }

        return builder.ToString().TrimEnd();
    }

    string FormatSnapshotMessages(RpChatDocument document, IReadOnlyList<RpTranscriptTurn> turns, RpSceneFrame? baselineScene)
    {
        var transcript = FormatTranscript(
            document,
            turns,
            baselineScene,
            PrivateIntentTranscriptScope.All,
            includeTurnLabels: true);
        return transcript == "(No transcript yet.)" ? "- None" : transcript;
    }

    static string FormatSnapshotHistory(RpChatDocument document)
    {
        var entries = document.Timeline
            .Take(3)
            .Select(x => $"{x.Title}: {PromptInlineText(x.Description, "No summary")}")
            .ToList();
        return entries.Count == 0 ? "None" : string.Join(" | ", entries);
    }

    static string FormatSnapshotCurrentLocation(IEnumerable<RpLocation> locations, RpSceneFrame scene)
    {
        var location = locations.FirstOrDefault(location => string.Equals(location.Id, scene.LocationId, StringComparison.Ordinal));
        if (location is not null)
            return FormatEntityReference(location.Name, location.Id);

        var name = string.IsNullOrWhiteSpace(scene.LocationName) ? "" : scene.LocationName;
        return string.IsNullOrWhiteSpace(name) ? "None" : FormatEntityReference(name, scene.LocationId);
    }

    static string FormatLocationReferences(IEnumerable<RpLocation> locations) =>
        FormatEntityReferences(locations.Select(location => (location.Name, location.Id)));

    static string FormatItemReferences(IEnumerable<RpItem> items) =>
        FormatEntityReferences(items.Select(item => (item.Name, item.Id)));

    static string FormatEntityReferences(IEnumerable<(string Name, string Id)> entities)
    {
        var values = entities
            .Where(entity => !string.IsNullOrWhiteSpace(entity.Name))
            .OrderBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entity => FormatEntityReference(entity.Name, entity.Id))
            .ToList();
        return values.Count == 0 ? "None" : string.Join(", ", values);
    }

    static string FormatEntityReference(string name, string id) =>
        string.IsNullOrWhiteSpace(id) ? $"{name} (id: none)" : $"{name} (id: {id})";

    static string FormatCharacterReferences(IEnumerable<RpCharacter> characters)
    {
        var values = characters
            .Where(character => !string.IsNullOrWhiteSpace(character.Name))
            .OrderBy(character => character.Name, StringComparer.OrdinalIgnoreCase)
            .Select(FormatCharacterReference)
            .ToList();
        return values.Count == 0 ? "None" : string.Join(", ", values);
    }

    static string FormatSnapshotRelationshipRefresh(RpChatDocument document, CharacterTraitLibraryState traitLibrary)
    {
        var characterNames = document.Characters.ToDictionary(character => character.Id, character => character.Name, StringComparer.Ordinal);
        var relationshipBlocks = document.CharacterRelationships
            .OrderBy(relationship => characterNames.GetValueOrDefault(relationship.CharacterAId, ""), StringComparer.OrdinalIgnoreCase)
            .ThenBy(relationship => characterNames.GetValueOrDefault(relationship.CharacterBId, ""), StringComparer.OrdinalIgnoreCase)
            .Select(relationship => FormatSnapshotRelationshipBlock(relationship, characterNames))
            .Where(block => !string.IsNullOrWhiteSpace(block))
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("**Relationship canon to refresh:**");
        builder.AppendLine("- Review each relationship below for new evidence about awareness, meeting status, recognition, emotional stance, public dynamic, bond type (`relationshipTypes`), or shared dynamic (`privateTensions`).");
        builder.AppendLine("- When the transcript gives a clearer current version of a relationship, return a complete relationship update for that pair using the existing controlled values and updated prose.");
        builder.AppendLine("- Use the relationshipId, sourceCharacterId, and targetCharacterId exactly as shown in the row.");
        builder.AppendLine("- Use only the provided relationshipTypes and privateTensions values.");
        builder.AppendLine("- Prefer useful canon updates that improve future scene consistency.");
        builder.AppendLine("- Carry forward existing relationship details that are still true, and incorporate the new transcript evidence into the complete updated row.");
        builder.AppendLine("- Each relationship update should include relationshipId, sourceCharacterId, targetCharacterId, relationshipTypes, privateTensions, howSourceSeesTarget, howTargetSeesSource, publicDynamic, reason, and evidenceTurnNumbers.");
        builder.AppendLine();
        builder.AppendLine($"**relationshipTypes:** {FormatSnapshotControlledValues(traitLibrary.BondTypes)}");
        builder.AppendLine($"**privateTensions:** {FormatSnapshotControlledValues(traitLibrary.Dynamics)}");
        builder.AppendLine();
        builder.AppendLine("**Relationships:**");
        builder.AppendLine(relationshipBlocks.Count == 0 ? "None" : string.Join($"{Environment.NewLine}{Environment.NewLine}", relationshipBlocks));
        return builder.ToString().TrimEnd();
    }

    static string FormatSnapshotRelationshipBlock(RpCharacterRelationship relationship, IReadOnlyDictionary<string, string> characterNames)
    {
        var sourceName = characterNames.GetValueOrDefault(relationship.CharacterAId, "");
        var targetName = characterNames.GetValueOrDefault(relationship.CharacterBId, "");
        if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(targetName))
            return "";

        var builder = new StringBuilder();
        builder.AppendLine($"Relationship: {sourceName} / {targetName} (relationshipId: {relationship.Id})");
        builder.AppendLine($"Source: {sourceName} (sourceCharacterId: {relationship.CharacterAId})");
        builder.AppendLine($"Target: {targetName} (targetCharacterId: {relationship.CharacterBId})");
        builder.AppendLine($"relationshipTypes: {FormatSnapshotControlledValues(relationship.Bonds)}");
        builder.AppendLine($"privateTensions: {FormatSnapshotControlledValues(relationship.Dynamics)}");
        builder.AppendLine($"howSourceSeesTarget (how {sourceName} sees {targetName}): {PromptInlineText(relationship.NoteAtoB, "Unknown")}");
        builder.AppendLine($"howTargetSeesSource (how {targetName} sees {sourceName}): {PromptInlineText(relationship.NoteBtoA, "Unknown")}");
        builder.Append($"publicDynamic (outsider view): {PromptInlineText(relationship.NoteExternal, "Unknown")}");
        return builder.ToString();
    }

    static string FormatSnapshotControlledValues(IReadOnlyList<string> values)
    {
        var normalized = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => PromptInlineText(value)).ToList();
        return normalized.Count == 0 ? "None" : string.Join(", ", normalized);
    }

    static void Add(HashSet<string> ids, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            ids.Add(value);
    }
}
