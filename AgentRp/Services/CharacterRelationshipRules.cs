using System.Text.Json;
using AgentRp.Session;

namespace AgentRp.Services;

public static class CharacterRelationshipRules
{
    public static readonly string[] RequiredPatchFields =
    [
        "sourceCharacterId",
        "targetCharacterId",
        "howSourceSeesTarget",
        "howTargetSeesSource",
        "publicDynamic",
        "relationshipTypes",
        "privateTensions"
    ];

    public static readonly string[] ContentFields =
    [
        "howSourceSeesTarget",
        "howTargetSeesSource",
        "publicDynamic",
        "relationshipTypes",
        "privateTensions"
    ];

    public static IReadOnlyList<string> MissingPatchFields(JsonElement root) =>
        ContentFields.Where(field => IsMissing(root, field)).ToList();

    public static IReadOnlyList<string> MissingFields(CharacterRelationshipView? relationship)
    {
        if (relationship is null)
            return ContentFields;

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(relationship.HowSourceSeesTarget))
            missing.Add("howSourceSeesTarget");
        if (string.IsNullOrWhiteSpace(relationship.HowTargetSeesSource))
            missing.Add("howTargetSeesSource");
        if (string.IsNullOrWhiteSpace(relationship.PublicDynamic))
            missing.Add("publicDynamic");
        if (relationship.RelationshipTypes.Count == 0)
            missing.Add("relationshipTypes");
        if (relationship.PrivateTensions.Count == 0)
            missing.Add("privateTensions");

        return missing;
    }

    public static object Coverage(RpChatDocument document) =>
        AllPairViews(document).Select(pair =>
        {
            var missingFields = MissingFields(pair.Relationship);
            return new
            {
                pair.RelationshipId,
                pair.SourceCharacterId,
                pair.SourceCharacterName,
                pair.TargetCharacterId,
                pair.TargetCharacterName,
                howSourceSeesTarget = pair.Relationship?.HowSourceSeesTarget ?? "",
                howTargetSeesSource = pair.Relationship?.HowTargetSeesSource ?? "",
                publicDynamic = pair.Relationship?.PublicDynamic ?? "",
                relationshipTypes = pair.Relationship?.RelationshipTypes ?? [],
                privateTensions = pair.Relationship?.PrivateTensions ?? [],
                missingFields,
                isComplete = missingFields.Count == 0
            };
        }).ToList();

    public static object ReconciliationFor(RpChatDocument document, string characterId)
    {
        var relationships = AllPairViews(document)
            .Where(pair => pair.SourceCharacterId == characterId || pair.TargetCharacterId == characterId)
            .Select(pair =>
            {
                var missingFields = MissingFields(pair.Relationship);
                return new
                {
                    pair.RelationshipId,
                    pair.SourceCharacterId,
                    pair.SourceCharacterName,
                    pair.TargetCharacterId,
                    pair.TargetCharacterName,
                    shouldUpdate = missingFields.Count > 0,
                    missingFields,
                    isComplete = missingFields.Count == 0
                };
            })
            .ToList();

        return new
        {
            characterId,
            instruction = "Inspect every relationship involving this character. Call update_character_relationship only for rows with shouldUpdate true or rows whose existing content is contradicted by the character update. Each relationshipId is one canonical pair; do not update the same relationshipId twice or call the reverse pair as a separate update.",
            relationships,
            incompleteCount = relationships.Count(item => !item.isComplete)
        };
    }

    static IReadOnlyList<CharacterRelationshipPairView> AllPairViews(RpChatDocument document)
    {
        var characters = document.Characters.OrderBy(character => character.Id, StringComparer.Ordinal).ToList();
        var pairs = new List<CharacterRelationshipPairView>();
        for (var sourceIndex = 0; sourceIndex < characters.Count; sourceIndex++)
        for (var targetIndex = sourceIndex + 1; targetIndex < characters.Count; targetIndex++)
        {
            var source = characters[sourceIndex];
            var target = characters[targetIndex];
            pairs.Add(new(
                CharacterRelationshipGraph.RelationshipIdFor(source.Id, target.Id),
                source.Id,
                source.Name,
                target.Id,
                target.Name,
                CharacterRelationshipGraph.View(document, source.Id, source.Name, target.Id, target.Name)));
        }

        return pairs;
    }

    static bool IsMissing(JsonElement root, string field)
    {
        if (!root.TryGetProperty(field, out var value))
            return true;

        return value.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Array => !value.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())),
            _ => true
        };
    }

    sealed record CharacterRelationshipPairView(
        string RelationshipId,
        string SourceCharacterId,
        string SourceCharacterName,
        string TargetCharacterId,
        string TargetCharacterName,
        CharacterRelationshipView? Relationship);
}
