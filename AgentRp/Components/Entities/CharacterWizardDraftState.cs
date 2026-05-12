using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Components.Entities;

public sealed class CharacterWizardDraftState
{
    public RpCharacter Character { get; set; } = new();
    public List<RpCharacterRelationship> Relationships { get; set; } = [];

    public static CharacterWizardDraftState From(RpCharacter character, RpChatDocument document) => new()
    {
        Character = SessionCloner.Clone(character),
        Relationships = document.CharacterRelationships
            .Where(relationship => CharacterRelationshipGraph.ContainsCharacter(relationship, character.Id))
            .Select(SessionCloner.Clone)
            .ToList()
    };

    public void ApplyTo(RpCharacter character, RpChatDocument document)
    {
        CopyCharacter(character, Character);
        document.CharacterRelationships.RemoveAll(relationship =>
            CharacterRelationshipGraph.ContainsCharacter(relationship, Character.Id));
        document.CharacterRelationships.AddRange(Relationships.Select(SessionCloner.Clone));
    }

    static void CopyCharacter(RpCharacter target, RpCharacter source)
    {
        var clone = SessionCloner.Clone(source);
        target.Id = clone.Id;
        target.Name = clone.Name;
        target.UpdatedUtc = clone.UpdatedUtc;
        target.ImageId = clone.ImageId;
        target.InScene = clone.InScene;
        target.Summary = clone.Summary;
        target.Personality = clone.Personality;
        target.Appearance = clone.Appearance;
        target.AppearanceProfile = clone.AppearanceProfile;
        target.Backstory = clone.Backstory;
        target.Voice = clone.Voice;
        target.Notes = clone.Notes;
        target.Pronouns = clone.Pronouns;
        target.SceneRoles = clone.SceneRoles;
        target.Traits = clone.Traits;
        target.Drives = clone.Drives;
        target.Limits = clone.Limits;
        target.CoreDrive = clone.CoreDrive;
        target.CoreFear = clone.CoreFear;
        target.SurfaceMask = clone.SurfaceMask;
        target.HiddenTruth = clone.HiddenTruth;
        target.SentenceStyle = clone.SentenceStyle;
        target.HonestyStyle = clone.HonestyStyle;
        target.EmotionalLeakage = clone.EmotionalLeakage;
        target.ActionFingerprint = clone.ActionFingerprint;
        target.StressPattern = clone.StressPattern;
        target.SoftSpots = clone.SoftSpots;
        target.AvoidPatterns = clone.AvoidPatterns;
        target.VoiceSelections = clone.VoiceSelections;
    }
}
