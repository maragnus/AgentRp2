using System.Text.Json;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public static class CharacterRelationshipGraph
{
    public static RpCharacterRelationship? Find(RpChatDocument document, string sourceCharacterId, string targetCharacterId)
        => Find(document.CharacterRelationships, sourceCharacterId, targetCharacterId);

    public static RpCharacterRelationship? Find(IEnumerable<RpCharacterRelationship> relationships, string sourceCharacterId, string targetCharacterId)
    {
        var pair = CanonicalPair(sourceCharacterId, targetCharacterId);
        return relationships.FirstOrDefault(relationship =>
            relationship.CharacterAId == pair.CharacterAId && relationship.CharacterBId == pair.CharacterBId);
    }

    public static RpCharacterRelationship GetOrCreate(RpChatDocument document, string sourceCharacterId, string targetCharacterId)
        => GetOrCreate(document.CharacterRelationships, sourceCharacterId, targetCharacterId);

    public static RpCharacterRelationship GetOrCreate(List<RpCharacterRelationship> relationships, string sourceCharacterId, string targetCharacterId)
    {
        var pair = CanonicalPair(sourceCharacterId, targetCharacterId);
        var existing = Find(relationships, sourceCharacterId, targetCharacterId);
        if (existing is not null)
            return existing;

        var relationship = new RpCharacterRelationship
        {
            Id = RelationshipId(pair.CharacterAId, pair.CharacterBId),
            CharacterAId = pair.CharacterAId,
            CharacterBId = pair.CharacterBId
        };
        relationships.Add(relationship);
        return relationship;
    }

    public static bool Remove(RpChatDocument document, string sourceCharacterId, string targetCharacterId)
        => Remove(document.CharacterRelationships, sourceCharacterId, targetCharacterId);

    public static bool Remove(List<RpCharacterRelationship> relationships, string sourceCharacterId, string targetCharacterId)
    {
        var relationship = Find(relationships, sourceCharacterId, targetCharacterId);
        return relationship is not null && relationships.Remove(relationship);
    }

    public static string RelationshipIdFor(string firstCharacterId, string secondCharacterId)
    {
        var pair = CanonicalPair(firstCharacterId, secondCharacterId);
        return RelationshipId(pair.CharacterAId, pair.CharacterBId);
    }

    public static void RemoveCharacter(RpChatDocument document, string characterId) =>
        document.CharacterRelationships.RemoveAll(relationship => Contains(relationship, characterId));

    public static bool ContainsCharacter(RpCharacterRelationship relationship, string characterId) =>
        Contains(relationship, characterId);

    public static CharacterRelationshipView View(
        RpCharacterRelationship relationship,
        string sourceCharacterId,
        string sourceCharacterName,
        string targetCharacterId,
        string targetCharacterName)
    {
        var sourceIsA = relationship.CharacterAId == sourceCharacterId;
        return new(
            relationship,
            sourceCharacterId,
            sourceCharacterName,
            targetCharacterId,
            targetCharacterName,
            sourceIsA);
    }

    public static CharacterRelationshipView? View(
        RpChatDocument document,
        string sourceCharacterId,
        string sourceCharacterName,
        string targetCharacterId,
        string targetCharacterName)
    {
        var relationship = Find(document, sourceCharacterId, targetCharacterId);
        return relationship is null
            ? null
            : View(relationship, sourceCharacterId, sourceCharacterName, targetCharacterId, targetCharacterName);
    }

    public static CharacterRelationshipView GetOrCreateView(
        RpChatDocument document,
        string sourceCharacterId,
        string sourceCharacterName,
        string targetCharacterId,
        string targetCharacterName) =>
        View(GetOrCreate(document, sourceCharacterId, targetCharacterId), sourceCharacterId, sourceCharacterName, targetCharacterId, targetCharacterName);

    public static void ApplyPatch(RpChatDocument document, string sourceCharacterId, string targetCharacterId, JsonElement root)
    {
        var source = document.Characters.First(item => item.Id == sourceCharacterId);
        var target = document.Characters.First(item => item.Id == targetCharacterId);
        var view = GetOrCreateView(document, sourceCharacterId, source.Name, targetCharacterId, target.Name);

        if (root.TryGetProperty("howSourceSeesTarget", out var sourceNote) && sourceNote.ValueKind == JsonValueKind.String)
            view.HowSourceSeesTarget = sourceNote.GetString() ?? "";

        if (root.TryGetProperty("howTargetSeesSource", out var targetNote) && targetNote.ValueKind == JsonValueKind.String)
            view.HowTargetSeesSource = targetNote.GetString() ?? "";

        if (root.TryGetProperty("publicDynamic", out var publicDynamic) && publicDynamic.ValueKind == JsonValueKind.String)
            view.PublicDynamic = publicDynamic.GetString() ?? "";

        if (root.TryGetProperty("privateTensions", out var privateTensions) && privateTensions.ValueKind == JsonValueKind.Array)
            view.PrivateTensions = StringList(privateTensions);

        if (root.TryGetProperty("relationshipTypes", out var relationshipTypes) && relationshipTypes.ValueKind == JsonValueKind.Array)
            view.RelationshipTypes = StringList(relationshipTypes);
    }

    public static IEnumerable<CharacterRelationshipView> ViewsForCharacter(
        RpChatDocument document,
        string sourceCharacterId,
        IReadOnlyDictionary<string, string> characterNames) =>
        ViewsForCharacter(document.CharacterRelationships, sourceCharacterId, characterNames);

    public static IEnumerable<CharacterRelationshipView> ViewsForCharacter(
        IEnumerable<RpCharacterRelationship> relationships,
        string sourceCharacterId,
        IReadOnlyDictionary<string, string> characterNames) =>
        relationships
            .Where(relationship => Contains(relationship, sourceCharacterId))
            .Select(relationship =>
            {
                var targetId = relationship.CharacterAId == sourceCharacterId
                    ? relationship.CharacterBId
                    : relationship.CharacterAId;
                return View(
                    relationship,
                    sourceCharacterId,
                    characterNames.GetValueOrDefault(sourceCharacterId, ""),
                    targetId,
                    characterNames.GetValueOrDefault(targetId, ""));
            });

    static (string CharacterAId, string CharacterBId) CanonicalPair(string firstCharacterId, string secondCharacterId)
    {
        if (string.IsNullOrWhiteSpace(firstCharacterId) || string.IsNullOrWhiteSpace(secondCharacterId))
            throw new InvalidOperationException("A relationship needs two character ids.");

        if (firstCharacterId == secondCharacterId)
            throw new InvalidOperationException("A character relationship needs two different characters.");

        return string.CompareOrdinal(firstCharacterId, secondCharacterId) <= 0
            ? (firstCharacterId, secondCharacterId)
            : (secondCharacterId, firstCharacterId);
    }

    static string RelationshipId(string characterAId, string characterBId) => $"relationship-{characterAId}-{characterBId}";

    static bool Contains(RpCharacterRelationship relationship, string characterId) =>
        relationship.CharacterAId == characterId || relationship.CharacterBId == characterId;

    static List<string> StringList(JsonElement value) =>
        value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
}

public sealed class CharacterRelationshipView(
    RpCharacterRelationship relationship,
    string sourceCharacterId,
    string sourceCharacterName,
    string targetCharacterId,
    string targetCharacterName,
    bool sourceIsA)
{
    public RpCharacterRelationship Relationship { get; } = relationship;
    public string SourceCharacterId { get; } = sourceCharacterId;
    public string SourceCharacterName { get; } = sourceCharacterName;
    public string TargetCharacterId { get; } = targetCharacterId;
    public string TargetCharacterName { get; } = targetCharacterName;
    public List<string> Bonds => Relationship.Bonds;
    public List<string> Dynamics => Relationship.Dynamics;

    public string HowSourceSeesTarget
    {
        get => sourceIsA ? Relationship.NoteAtoB : Relationship.NoteBtoA;
        set
        {
            if (sourceIsA)
                Relationship.NoteAtoB = value;
            else
                Relationship.NoteBtoA = value;
        }
    }

    public string HowTargetSeesSource
    {
        get => sourceIsA ? Relationship.NoteBtoA : Relationship.NoteAtoB;
        set
        {
            if (sourceIsA)
                Relationship.NoteBtoA = value;
            else
                Relationship.NoteAtoB = value;
        }
    }

    public string PublicDynamic
    {
        get => Relationship.NoteExternal;
        set => Relationship.NoteExternal = value;
    }

    public List<string> RelationshipTypes
    {
        get => Relationship.Bonds;
        set => Relationship.Bonds = value;
    }

    public List<string> PrivateTensions
    {
        get => Relationship.Dynamics;
        set => Relationship.Dynamics = value;
    }
}
