using AgentRp.Components.Entities;
using AgentRp.Models;
using AgentRp.Session;
using AgentRp.Services;

namespace AgentRp.Tests;

public sealed class CharacterWizardDraftStateTests
{
    [Fact]
    public void FromCopiesOnlySelectedCharacterRelationships()
    {
        var document = CreateDocument();
        var draft = CharacterWizardDraftState.From(document.Characters[0], document);

        Assert.Equal(2, draft.Relationships.Count);
        Assert.Contains(draft.Relationships, relationship => relationship.Id == CharacterRelationshipGraph.RelationshipIdFor("a", "b"));
        Assert.Contains(draft.Relationships, relationship => relationship.Id == CharacterRelationshipGraph.RelationshipIdFor("a", "c"));
    }

    [Fact]
    public void DraftRelationshipEditsDoNotMutateLiveDocumentUntilApplied()
    {
        var document = CreateDocument();
        var draft = CharacterWizardDraftState.From(document.Characters[0], document);

        draft.Relationships[0].NoteAtoB = "draft note";

        Assert.Equal("saved note", document.CharacterRelationships[0].NoteAtoB);
    }

    [Fact]
    public void ApplyReplacesSelectedCharacterRelationshipsAndPreservesUnrelatedRows()
    {
        var document = CreateDocument();
        var target = document.Characters[0];
        var draft = CharacterWizardDraftState.From(target, document);

        draft.Character.Name = "A changed";
        draft.Relationships[0].NoteAtoB = "draft note";
        draft.Relationships.RemoveAll(relationship => relationship.Id == CharacterRelationshipGraph.RelationshipIdFor("a", "c"));
        draft.Relationships.Add(new()
        {
            Id = CharacterRelationshipGraph.RelationshipIdFor("a", "d"),
            CharacterAId = "a",
            CharacterBId = "d",
            NoteAtoB = "new row"
        });

        draft.ApplyTo(target, document);

        Assert.Equal("A changed", target.Name);
        Assert.DoesNotContain(document.CharacterRelationships, relationship => relationship.Id == CharacterRelationshipGraph.RelationshipIdFor("a", "c"));
        Assert.Contains(document.CharacterRelationships, relationship => relationship.Id == CharacterRelationshipGraph.RelationshipIdFor("b", "c"));
        Assert.Contains(document.CharacterRelationships, relationship => relationship.Id == CharacterRelationshipGraph.RelationshipIdFor("a", "d"));
        Assert.Equal("draft note", document.CharacterRelationships.First(relationship => relationship.Id == CharacterRelationshipGraph.RelationshipIdFor("a", "b")).NoteAtoB);
    }

    static RpChatDocument CreateDocument()
    {
        var document = new RpChatDocument
        {
            Characters =
            [
                new() { Id = "a", Name = "A" },
                new() { Id = "b", Name = "B" },
                new() { Id = "c", Name = "C" },
                new() { Id = "d", Name = "D" }
            ]
        };

        document.CharacterRelationships =
        [
            new()
            {
                Id = CharacterRelationshipGraph.RelationshipIdFor("a", "b"),
                CharacterAId = "a",
                CharacterBId = "b",
                NoteAtoB = "saved note"
            },
            new()
            {
                Id = CharacterRelationshipGraph.RelationshipIdFor("b", "c"),
                CharacterAId = "b",
                CharacterBId = "c",
                NoteAtoB = "unrelated"
            },
            new()
            {
                Id = CharacterRelationshipGraph.RelationshipIdFor("a", "c"),
                CharacterAId = "a",
                CharacterBId = "c",
                NoteAtoB = "removed when absent from draft"
            }
        ];
        return document;
    }
}
