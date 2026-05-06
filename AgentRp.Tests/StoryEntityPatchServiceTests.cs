using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Serialization;
using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Tests;

public sealed class StoryEntityPatchServiceTests
{
    [Fact]
    public async Task UpdateCharacterAppliesOnlyProvidedFields()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"summary":"Sharper summary"}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Lucia", document.Characters[0].Name);
        Assert.Equal("Sharper summary", document.Characters[0].Summary);
        Assert.Equal("Keeps the old backstory.", document.Characters[0].Backstory);
        Assert.Contains(callbacks.ToolItems.Single().Diffs, diff => diff.Field == "summary");
    }

    [Fact]
    public async Task ReviewAllTryAgainDoesNotMutateEntity()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.ReviewAll;
        var callbacks = new TestCallbacks { Decision = new(StoryAssistantDecisionKind.TryAgain, "Keep the current motive.") };
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"summary":"Changed"}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("retry_requested", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Old summary", document.Characters[0].Summary);
        Assert.Equal(StoryAssistantItemStatus.RetryRequested, callbacks.ToolItems.Single().Status);
    }

    [Fact]
    public async Task ReviewMajorAutoAppliesLowRiskPatch()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.ReviewMajor;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"summary":"Low risk summary"}}""",
            callbacks,
            CancellationToken.None);

        Assert.Equal("Low risk summary", document.Characters[0].Summary);
        Assert.Equal(0, callbacks.ReviewCount);
        Assert.Equal(StoryAssistantItemStatus.Applied, callbacks.ToolItems.Single().Status);
    }

    [Fact]
    public async Task RejectDoesNotMutateEntity()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.ReviewAll;
        var callbacks = new TestCallbacks { Decision = new(StoryAssistantDecisionKind.Reject, "Leave Lucia alone.") };
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"summary":"Changed"}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("rejected", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Old summary", document.Characters[0].Summary);
        Assert.Equal(StoryAssistantItemStatus.Rejected, callbacks.ToolItems.Single().Status);
    }

    [Fact]
    public async Task ReviewMajorPausesTimelinePatch()
    {
        var document = CreateDocument();
        document.Timeline.Add(new() { Id = "t1", Title = "Old event" });
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.ReviewMajor;
        var callbacks = new TestCallbacks { Decision = new(StoryAssistantDecisionKind.Accept, "") };
        var service = new StoryEntityPatchService();

        await service.ExecuteAsync(
            document,
            "call-1",
            "update_timeline_entry",
            """{"entityId":"t1","updates":{"title":"New event"}}""",
            callbacks,
            CancellationToken.None);

        Assert.Equal("New event", document.Timeline[0].Title);
        Assert.Equal(1, callbacks.ReviewCount);
        Assert.Equal(StoryAssistantItemStatus.Accepted, callbacks.ToolItems.Single().Status);
    }

    [Fact]
    public async Task RelationshipPatchKeepsDirectionalMeaning()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        await service.ExecuteAsync(
            document,
            "call-1",
            "update_character_relationship",
            """{"sourceCharacterId":"c1","targetCharacterId":"c2","howSourceSeesTarget":"Lucia trusts Gemma with maps.","howTargetSeesSource":"Gemma thinks Lucia is reckless.","publicDynamic":"Friendly rivals","privateTension":"Unspoken tension","relationshipType":"Rival"}""",
            callbacks,
            CancellationToken.None);

        var relationship = document.Characters[0].ProfileRelationships.Single(item => item.CharacterId == "c2");
        Assert.Equal("Lucia trusts Gemma with maps.", relationship.NoteAtoB);
        Assert.Equal("Gemma thinks Lucia is reckless.", relationship.NoteBtoA);
        Assert.Equal("Friendly rivals", relationship.NoteExternal);
        Assert.Contains("Unspoken tension", relationship.Dynamics);
        Assert.Contains("Rival", relationship.Bonds);
    }

    [Fact]
    public async Task ReadStoryEntitiesIsMarkedReadOnly()
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "get_story_entities",
            "{}",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(StoryAssistantItemStatus.Read, callbacks.ToolItems.Single().Status);
        Assert.Equal(StoryAssistantOperationKind.Read, callbacks.ToolItems.Single().Operation);
        Assert.Equal("Read story entities", callbacks.ToolItems.Single().Title);
    }

    [Fact]
    public async Task ReadStoryEntitiesIncludesTraitLibraryContext()
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "get_story_entities",
            "{}",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        var library = json.RootElement.GetProperty("entities").GetProperty("characterTraitLibrary");
        Assert.Equal(CharacterProfileRules.MaxTraits, library.GetProperty("limits").GetProperty("maxTraits").GetInt32());
        Assert.Contains(library.GetProperty("controlledFields").EnumerateArray(), item => item.GetString() == "traits");
        Assert.Contains("get_character_profile_options", library.GetProperty("instruction").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCharacterProfileOptionsReturnsRequestedFields()
    {
        var document = CreateDocument();
        document.CharacterTraitLibrary.SceneRoles = [new("foil", "Foil", "Contrasts another character.")];
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "get_character_profile_options",
            """{"fields":["sceneRoles"]}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        var options = json.RootElement.GetProperty("characterProfileOptions").GetProperty("fields");
        var sceneRoleIds = options.GetProperty("sceneRoles").GetProperty("options").EnumerateArray().Select(item => item.GetProperty("id").GetString()).ToList();
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("foil", sceneRoleIds);
        Assert.False(options.TryGetProperty("traits", out _));
        Assert.Equal(StoryAssistantItemStatus.Read, callbacks.ToolItems.Single().Status);
    }

    [Fact]
    public async Task ReadStoryEntitiesOmitsLegacyRelationshipSummary()
    {
        var document = CreateDocument();
        document.Characters[0].Relationships = "Legacy summary that should not be sent to the assistant.";
        document.Characters[0].ProfileRelationships.Add(new()
        {
            CharacterId = "c2",
            NoteAtoB = "Lucia trusts Gemma.",
            NoteBtoA = "Gemma worries about Lucia."
        });
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "get_story_entities",
            "{}",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        var character = json.RootElement.GetProperty("entities").GetProperty("characters")[0];
        Assert.False(character.TryGetProperty("relationships", out _));
        Assert.False(character.TryGetProperty("profileRelationships", out _));
        var relationship = json.RootElement.GetProperty("entities").GetProperty("relationships")[0].GetProperty("relationships")[0];
        Assert.Equal("Lucia trusts Gemma.", relationship.GetProperty("howSourceSeesTarget").GetString());
    }

    [Fact]
    public async Task ReadStoryEntitiesOmitsCurrentSceneStateFlags()
    {
        var document = CreateDocument();
        document.Characters[0].InScene = true;
        document.Locations.Add(new() { Id = "l1", Name = "Garden", IsActive = true });
        document.Items.Add(new() { Id = "i1", Name = "Key", InScene = true });
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "get_story_entities",
            "{}",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        var entities = json.RootElement.GetProperty("entities");
        Assert.False(entities.GetProperty("characters")[0].TryGetProperty("inScene", out _));
        Assert.False(entities.GetProperty("locations")[0].TryGetProperty("isActive", out _));
        Assert.False(entities.GetProperty("items")[0].TryGetProperty("inScene", out _));
        Assert.True(document.Characters[0].InScene);
        Assert.True(document.Locations[0].IsActive);
        Assert.True(document.Items[0].InScene);
    }

    [Theory]
    [InlineData("create_location", """{"updates":{"summary":"No name."}}""", "location", "name")]
    [InlineData("create_item", """{"updates":{"summary":"No name."}}""", "item", "name")]
    [InlineData("create_timeline_entry", """{"updates":{"description":"No title."}}""", "timeline entry", "title")]
    public async Task CreateToolsRequireMeaningfulIdentityFields(string toolName, string args, string entityName, string requiredField)
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(document, "call-1", toolName, args, callbacks, CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains(requiredField, json.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Empty(callbacks.ToolItems);
        var count = entityName switch
        {
            "location" => document.Locations.Count,
            "item" => document.Items.Count,
            _ => document.Timeline.Count
        };
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CreateLocationPersistsProvidedFieldsAndUsesAssistantShape()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "create_location",
            """{"updates":{"name":"Glass Conservatory","summary":"A humid room of mirrors.","description":"Iron ribs and fogged panes.","isActive":true}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("unsupported field 'isActive'", json.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Empty(document.Locations);

        result = await service.ExecuteAsync(
            document,
            "call-2",
            "create_location",
            """{"updates":{"name":"Glass Conservatory","summary":"A humid room of mirrors.","description":"Iron ribs and fogged panes."}}""",
            callbacks,
            CancellationToken.None);

        using var accepted = JsonDocument.Parse(result);
        Assert.Equal("accepted", accepted.RootElement.GetProperty("status").GetString());
        Assert.Equal("Glass Conservatory", document.Locations.Single().Name);
        Assert.False(document.Locations.Single().IsActive);
        Assert.False(accepted.RootElement.GetProperty("resultingEntity").TryGetProperty("isActive", out _));
        var item = callbacks.ToolItems.Single();
        Assert.Equal(StoryAssistantOperationKind.Create, item.Operation);
        Assert.False(item.After.TryGetPropertyValue("isActive", out _));
    }

    [Theory]
    [InlineData("update_location", """{"entityId":"missing","updates":{"summary":"New"}}""")]
    [InlineData("update_item", """{"entityId":"missing","updates":{"summary":"New"}}""")]
    [InlineData("update_timeline_entry", """{"entityId":"missing","updates":{"description":"New"}}""")]
    public async Task InvalidUpdateIdsTellModelToReadEntities(string toolName, string args)
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(document, "call-1", toolName, args, callbacks, CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("get_story_entities", json.RootElement.GetProperty("nextStep").GetProperty("tool").GetString());
        Assert.Empty(callbacks.ToolItems);
    }

    [Fact]
    public async Task UpdateItemRejectsUnknownFieldsBeforeMutating()
    {
        var document = CreateDocument();
        document.Items.Add(new() { Id = "i1", Name = "Key", InScene = true });
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_item",
            """{"entityId":"i1","updates":{"inScene":false,"summary":"Changed"}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("unsupported field 'inScene'", json.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.True(document.Items[0].InScene);
        Assert.Equal("", document.Items[0].Summary);
    }

    [Fact]
    public async Task CharacterPatchAcceptsValidControlledValues()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"sceneRoles":["anchor"],"traits":["guarded","dry-wit"],"coreDrive":"protect-their-people","softSpots":["being-trusted"],"avoidPatterns":["no-random-cruelty"]}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(["anchor"], document.Characters[0].SceneRoles);
        Assert.Equal(["guarded", "dry-wit"], document.Characters[0].Traits);
        Assert.Equal("protect-their-people", document.Characters[0].CoreDrive);
    }

    [Fact]
    public async Task CharacterPatchDoesNotApplyLegacyRelationshipSummary()
    {
        var document = CreateDocument();
        document.Characters[0].Relationships = "Original legacy summary.";
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"relationships":"New legacy summary.","summary":"New summary"}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Original legacy summary.", document.Characters[0].Relationships);
        Assert.Equal("New summary", document.Characters[0].Summary);
    }

    [Fact]
    public async Task CharacterPatchRejectsInvalidControlledValueWithoutMutating()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"traits":["invented-trait"]}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("invalid value 'invented-trait'", json.RootElement.GetProperty("reason").GetString());
        var nextStep = json.RootElement.GetProperty("nextStep");
        Assert.Equal("get_character_profile_options", nextStep.GetProperty("tool").GetString());
        Assert.Contains(nextStep.GetProperty("fields").EnumerateArray(), item => item.GetString() == "traits");
        Assert.Empty(document.Characters[0].Traits);
        Assert.Empty(callbacks.ToolItems);
    }

    [Fact]
    public async Task CharacterPatchRejectsDuplicateControlledValues()
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"sceneRoles":["anchor","anchor"]}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("duplicate value 'anchor'", json.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task CharacterPatchRejectsOverLimitControlledValues()
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"sceneRoles":["anchor","mirror","witness"]}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("maximum is 2", json.RootElement.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("traits", """["guarded","dry-wit","observer","controlled","snarky","principled","charmer"]""", "maximum is 6")]
    [InlineData("softSpots", """["quiet-inclusion","practical-care","remembered-details","being-trusted"]""", "maximum is 3")]
    [InlineData("avoidPatterns", """["no-random-cruelty","no-instant-vulnerable","no-passive-in-danger","no-solve-every-conflict","no-escalate-every-jab","no-reveal-secrets-early"]""", "maximum is 5")]
    public async Task CharacterPatchRejectsWizardArrayLimits(string field, string valuesJson, string expectedReason)
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            $$$"""{"entityId":"c1","updates":{"{{{field}}}":{{{valuesJson}}}}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains(expectedReason, json.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task PartialPatchDoesNotValidateExistingLegacyControlledValues()
    {
        var document = CreateDocument();
        document.Characters[0].Traits.Add("legacy-trait");
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"summary":"New summary"}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(["legacy-trait"], document.Characters[0].Traits);
        Assert.Equal("New summary", document.Characters[0].Summary);
    }

    [Fact]
    public async Task RelationshipPatchRejectsInvalidLibraryValues()
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character_relationship",
            """{"sourceCharacterId":"c1","targetCharacterId":"c2","relationshipType":"rivals","privateTension":"custom dynamic"}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("relationship type contains invalid value 'rivals'", json.RootElement.GetProperty("reason").GetString());
        Assert.Empty(document.Characters[0].ProfileRelationships);
    }

    [Fact]
    public void DiffValueFormatsArraysAsMultilineBullets()
    {
        var node = JsonNode.Parse("""["guarded","dry-wit"]""");

        Assert.Equal("- guarded\n- dry-wit", StoryEntityPatchService.FormatDiffValue(node));
    }

    [Fact]
    public void DiffValueFormatsObjectsAsMultilineKeyValues()
    {
        var node = JsonNode.Parse("""{"foo":"bar","count":2}""");

        Assert.Equal("foo: bar\ncount: 2", StoryEntityPatchService.FormatDiffValue(node));
    }

    [Fact]
    public void DiffValueFormatsNestedValuesWithIndentation()
    {
        var node = JsonNode.Parse("""{"profile":{"traits":["guarded","dry-wit"]}}""");

        Assert.Equal("profile: \n  traits: \n    - guarded\n    - dry-wit", StoryEntityPatchService.FormatDiffValue(node));
    }

    static RpChatDocument CreateDocument() => new()
    {
        Chat = new() { Id = "ch1" },
        Characters =
        [
            new()
            {
                Id = "c1",
                Name = "Lucia",
                Summary = "Old summary",
                Backstory = "Keeps the old backstory."
            },
            new()
            {
                Id = "c2",
                Name = "Gemma"
            }
        ]
    };

    sealed class TestCallbacks : IStoryAssistantCallbacks
    {
        public StoryAssistantDecision Decision { get; set; } = new(StoryAssistantDecisionKind.Accept, "");
        public List<StoryAssistantTranscriptItem> ToolItems { get; } = [];
        public int ReviewCount { get; private set; }

        public Task AppendAssistantTextAsync(string delta, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken)
        {
            ToolItems.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoryAssistantDecision> ReviewChangeAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken)
        {
            ReviewCount++;
            return Task.FromResult(Decision);
        }

        public Task<string> AskQuestionAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken) => Task.FromResult("Answer");

        public Task SaveEntityAreaAsync(RoleplayStoreArea area, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveAssistantStateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
