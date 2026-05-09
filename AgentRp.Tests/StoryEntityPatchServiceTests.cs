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
    public void BuildToolsExposesRenameStoryWithRequiredTitle()
    {
        var tool = StoryAssistantService.BuildTools(new()).Single(tool => tool.Name == "rename_story");

        using var json = JsonDocument.Parse(tool.Parameters.ToJsonString());
        var required = json.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToList();

        Assert.Contains("Rename the current story", tool.Description, StringComparison.Ordinal);
        Assert.Contains("title", required);
        Assert.True(json.RootElement.GetProperty("properties").TryGetProperty("reason", out _));
    }

    [Fact]
    public async Task RenameStoryAppliesReviewedStoryTitleMutation()
    {
        var document = CreateDocument();
        document.Chat.Title = "Old Story";
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var context = new StoryAssistantToolRunContext { HasReadTranscript = true };
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteToolAsync(
            document,
            "call-1",
            "rename_story",
            """{"title":"  Neon Rain  ","reason":"Fits the premise."}""",
            callbacks,
            context,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result.OutputJson);
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Neon Rain", document.Chat.Title);
        Assert.Equal(RoleplayStoreArea.ChatDirection, callbacks.SavedAreas.Single());
        var item = callbacks.ToolItems.Single();
        Assert.Equal("rename_story", item.ToolName);
        Assert.Equal("story", item.EntityType);
        Assert.Contains(item.Diffs, diff => diff.Field == "title" && diff.Before == "Old Story" && diff.After == "Neon Rain");
    }

    [Fact]
    public async Task RenameStoryRespectsReviewMode()
    {
        var document = CreateDocument();
        document.Chat.Title = "Old Story";
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.ReviewMajor;
        var callbacks = new TestCallbacks();
        var context = new StoryAssistantToolRunContext { HasReadTranscript = true };
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteToolAsync(
            document,
            "call-1",
            "rename_story",
            """{"title":"Neon Rain"}""",
            callbacks,
            context,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result.OutputJson);
        Assert.Equal("pending", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Old Story", document.Chat.Title);
        var workItem = callbacks.WorkItems.Single();
        Assert.Equal("story", workItem.EntityType);

        await service.ResolveWorkItemAsync(
            document,
            workItem,
            new(StoryAssistantWorkItemResolutionKind.Accept, "", ""),
            callbacks,
            CancellationToken.None);

        Assert.Equal("Neon Rain", document.Chat.Title);
        Assert.Equal(RoleplayStoreArea.ChatDirection, callbacks.SavedAreas.Single());
    }

    [Fact]
    public async Task GuardedMutationsRequireTranscriptReadInRunContext()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var context = new StoryAssistantToolRunContext();
        var service = new StoryEntityPatchService();

        var blocked = await service.ExecuteToolAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"summary":"Changed"}}""",
            callbacks,
            context,
            CancellationToken.None);

        using var blockedJson = JsonDocument.Parse(blocked.OutputJson);
        Assert.Equal("failed", blockedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("get_story_transcript", blockedJson.RootElement.GetProperty("nextStep").GetProperty("tool").GetString());
        Assert.Equal("Old summary", document.Characters[0].Summary);
        Assert.Empty(callbacks.ToolItems);

        await service.ExecuteToolAsync(
            document,
            "call-read",
            "get_story_transcript",
            "{}",
            callbacks,
            context,
            CancellationToken.None);

        Assert.True(context.HasReadTranscript);

        var allowed = await service.ExecuteToolAsync(
            document,
            "call-2",
            "update_character",
            """{"entityId":"c1","updates":{"summary":"Changed"}}""",
            callbacks,
            context,
            CancellationToken.None);

        using var allowedJson = JsonDocument.Parse(allowed.OutputJson);
        Assert.Equal("accepted", allowedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("Changed", document.Characters[0].Summary);
    }

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
        var reconciliation = json.RootElement.GetProperty("relationshipReconciliation");
        Assert.Equal("c1", reconciliation.GetProperty("characterId").GetString());
        Assert.Equal(1, reconciliation.GetProperty("incompleteCount").GetInt32());
        Assert.Contains("Inspect every relationship", reconciliation.GetProperty("instruction").GetString(), StringComparison.Ordinal);
        Assert.Contains("do not update the same relationshipId twice", reconciliation.GetProperty("instruction").GetString(), StringComparison.Ordinal);
        var relationship = reconciliation.GetProperty("relationships")[0];
        Assert.Equal("relationship-c1-c2", relationship.GetProperty("relationshipId").GetString());
        Assert.True(relationship.GetProperty("shouldUpdate").GetBoolean());
        Assert.Contains(callbacks.ToolItems.Single().Diffs, diff => diff.Field == "summary");
    }

    [Fact]
    public async Task CompleteRelationshipReconciliationDoesNotRequestDuplicateUpdate()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        document.CharacterRelationships.Add(new()
        {
            Id = "relationship-c1-c2",
            CharacterAId = "c1",
            CharacterBId = "c2",
            NoteAtoB = "Lucia trusts Gemma.",
            NoteBtoA = "Gemma trusts Lucia.",
            NoteExternal = "Trusted partners",
            Bonds = ["Ally"],
            Dynamics = ["Protective"]
        });
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c2","updates":{"summary":"Sharper Gemma summary"}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        var reconciliation = json.RootElement.GetProperty("relationshipReconciliation");
        var relationship = reconciliation.GetProperty("relationships")[0];

        Assert.Equal(0, reconciliation.GetProperty("incompleteCount").GetInt32());
        Assert.Equal("relationship-c1-c2", relationship.GetProperty("relationshipId").GetString());
        Assert.False(relationship.GetProperty("shouldUpdate").GetBoolean());
        Assert.True(relationship.GetProperty("isComplete").GetBoolean());
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
        Assert.Equal("pending", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Old summary", document.Characters[0].Summary);
        var workItem = callbacks.WorkItems.Single();
        Assert.Equal(StoryAssistantWorkItemStatus.Pending, workItem.Status);

        await service.ResolveWorkItemAsync(
            document,
            workItem,
            new(StoryAssistantWorkItemResolutionKind.TryAgain, "", "Keep the current motive."),
            callbacks,
            CancellationToken.None);

        Assert.Equal("Old summary", document.Characters[0].Summary);
        Assert.Equal(StoryAssistantWorkItemStatus.RetryRequested, workItem.Status);
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
        Assert.Equal("pending", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Old summary", document.Characters[0].Summary);
        var workItem = callbacks.WorkItems.Single();

        await service.ResolveWorkItemAsync(
            document,
            workItem,
            new(StoryAssistantWorkItemResolutionKind.Reject, "", "Leave Lucia alone."),
            callbacks,
            CancellationToken.None);

        Assert.Equal("Old summary", document.Characters[0].Summary);
        Assert.Equal(StoryAssistantWorkItemStatus.Rejected, workItem.Status);
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

        Assert.Equal("Old event", document.Timeline[0].Title);
        Assert.Equal(1, callbacks.ReviewCount);
        var workItem = callbacks.WorkItems.Single();

        await service.ResolveWorkItemAsync(
            document,
            workItem,
            new(StoryAssistantWorkItemResolutionKind.Accept, "", ""),
            callbacks,
            CancellationToken.None);

        Assert.Equal("New event", document.Timeline[0].Title);
        Assert.Equal(StoryAssistantWorkItemStatus.Completed, workItem.Status);
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
            """{"sourceCharacterId":"c1","targetCharacterId":"c2","howSourceSeesTarget":"Lucia trusts Gemma with maps.","howTargetSeesSource":"Gemma thinks Lucia is reckless.","publicDynamic":"Friendly rivals","privateTensions":["Unspoken tension"],"relationshipTypes":["Rival"]}""",
            callbacks,
            CancellationToken.None);

        var relationship = document.CharacterRelationships.Single(item => item.CharacterAId == "c1" && item.CharacterBId == "c2");
        Assert.Equal("Lucia trusts Gemma with maps.", relationship.NoteAtoB);
        Assert.Equal("Gemma thinks Lucia is reckless.", relationship.NoteBtoA);
        Assert.Equal("Friendly rivals", relationship.NoteExternal);
        Assert.Contains("Unspoken tension", relationship.Dynamics);
        Assert.Contains("Rival", relationship.Bonds);
    }

    [Fact]
    public async Task RelationshipPatchReviewUsesFlatRelationshipFields()
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        await service.ExecuteAsync(
            document,
            "call-1",
            "update_character_relationship",
            """{"sourceCharacterId":"c1","targetCharacterId":"c2","howSourceSeesTarget":"Lucia trusts Gemma with maps.","howTargetSeesSource":"Gemma thinks Lucia is reckless.","publicDynamic":"Friendly rivals","privateTensions":["Unspoken tension"],"relationshipTypes":["Rival"]}""",
            callbacks,
            CancellationToken.None);

        var workItem = callbacks.WorkItems.Single();

        Assert.False(workItem.Before.TryGetPropertyValue("profileRelationships", out _));
        Assert.False(workItem.After.TryGetPropertyValue("profileRelationships", out _));
        Assert.False(workItem.After.TryGetPropertyValue("relationships", out _));
        Assert.Contains(workItem.Diffs, diff => diff.Field == "howSourceSeesTarget" && diff.Label == "How Source Sees Target");
        Assert.Contains(workItem.Diffs, diff => diff.Field == "relationshipTypes" && diff.Label == "Relationship Types");
        Assert.Contains(workItem.Diffs, diff => diff.Field == "privateTensions" && diff.Label == "Private Tensions");
    }

    [Fact]
    public async Task RelationshipPatchRejectsIncompleteFieldsBeforeMutating()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character_relationship",
            """{"sourceCharacterId":"c1","targetCharacterId":"c2","howSourceSeesTarget":"Lucia trusts Gemma.","relationshipTypes":["Ally"]}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("every relationship field", json.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Equal("get_character_profile_options", json.RootElement.GetProperty("nextStep").GetProperty("tool").GetString());
        Assert.Empty(document.CharacterRelationships);
        Assert.Empty(callbacks.ToolItems);
    }

    [Fact]
    public async Task ReverseRelationshipPatchUpdatesSameCanonicalRecord()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        await service.ExecuteAsync(
            document,
            "call-1",
            "update_character_relationship",
            """{"sourceCharacterId":"c1","targetCharacterId":"c2","howSourceSeesTarget":"Lucia trusts Gemma.","howTargetSeesSource":"Gemma worries about Lucia.","publicDynamic":"Careful allies","relationshipTypes":["Ally"],"privateTensions":["Protective"]}""",
            callbacks,
            CancellationToken.None);

        await service.ExecuteAsync(
            document,
            "call-2",
            "update_character_relationship",
            """{"sourceCharacterId":"c2","targetCharacterId":"c1","howSourceSeesTarget":"Gemma trusts Lucia back.","howTargetSeesSource":"Lucia knows Gemma will challenge her.","publicDynamic":"Hard-won partners","relationshipTypes":["Rival"],"privateTensions":["Unspoken tension"]}""",
            callbacks,
            CancellationToken.None);

        var relationship = document.CharacterRelationships.Single();
        Assert.Equal("relationship-c1-c2", relationship.Id);
        Assert.Equal("Lucia knows Gemma will challenge her.", relationship.NoteAtoB);
        Assert.Equal("Gemma trusts Lucia back.", relationship.NoteBtoA);
        Assert.Equal("Hard-won partners", relationship.NoteExternal);
        Assert.Equal(["Rival"], relationship.Bonds);
        Assert.Equal(["Unspoken tension"], relationship.Dynamics);
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
        document.Characters[0].Pronouns = ["they/them"];
        document.Characters[0].Appearance = "Small crescent scar under one eye.";
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
        Assert.Contains(library.GetProperty("controlledFields").EnumerateArray(), item => item.GetString() == "pronouns");
        Assert.Contains("flat appearance fields", library.GetProperty("appearancePolicy").GetString(), StringComparison.Ordinal);
        var character = json.RootElement.GetProperty("entities").GetProperty("characters")[0];
        Assert.Contains(character.GetProperty("pronouns").EnumerateArray(), item => item.GetString() == "they/them");
        Assert.Equal("Small crescent scar under one eye.", character.GetProperty("extraAppearanceDetails").GetString());
        Assert.True(character.TryGetProperty("hairColor", out _));
        Assert.True(character.TryGetProperty("attractiveness", out _));
        Assert.False(character.TryGetProperty("appearance", out _));
        Assert.False(character.TryGetProperty("appearanceProfile", out _));
        Assert.False(character.TryGetProperty("appearanceSummary", out _));
        Assert.Contains("get_character_profile_options", library.GetProperty("instruction").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadStoryEntitiesIncludesEveryCharacterPairRelationshipCoverage()
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
        var relationship = json.RootElement.GetProperty("entities").GetProperty("relationships")[0];

        Assert.Equal("c1", relationship.GetProperty("sourceCharacterId").GetString());
        Assert.Equal("c2", relationship.GetProperty("targetCharacterId").GetString());
        Assert.Equal("relationship-c1-c2", relationship.GetProperty("relationshipId").GetString());
        Assert.False(relationship.GetProperty("isComplete").GetBoolean());
        Assert.Contains(relationship.GetProperty("missingFields").EnumerateArray(), item => item.GetString() == "howSourceSeesTarget");
        Assert.Contains(relationship.GetProperty("missingFields").EnumerateArray(), item => item.GetString() == "relationshipTypes");
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
        Assert.Contains("flat appearance fields", json.RootElement.GetProperty("characterProfileOptions").GetProperty("appearancePolicy").GetString(), StringComparison.Ordinal);
        Assert.Contains("foil", sceneRoleIds);
        Assert.False(options.TryGetProperty("traits", out _));
        Assert.Equal(StoryAssistantItemStatus.Read, callbacks.ToolItems.Single().Status);
    }

    [Fact]
    public async Task GetCharacterProfileOptionsRejectsLegacyAppearanceProfileField()
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "get_character_profile_options",
            """{"fields":["appearanceProfile"]}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("not a supported controlled profile field", json.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Empty(callbacks.ToolItems);
    }

    [Fact]
    public async Task ReadStoryEntitiesOmitsLegacyRelationshipSummary()
    {
        var document = CreateDocument();
        document.CharacterRelationships.Add(new()
        {
            Id = "relationship-c1-c2",
            CharacterAId = "c1",
            CharacterBId = "c2",
            NoteAtoB = "Lucia trusts Gemma.",
            NoteBtoA = "Gemma worries about Lucia.",
            Bonds = ["Ally"],
            Dynamics = ["Protective"]
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
        var relationship = json.RootElement.GetProperty("entities").GetProperty("relationships")[0];
        Assert.False(relationship.TryGetProperty("relationships", out _));
        Assert.Equal("c1", relationship.GetProperty("sourceCharacterId").GetString());
        Assert.Equal("Lucia", relationship.GetProperty("sourceCharacterName").GetString());
        Assert.Equal("c2", relationship.GetProperty("targetCharacterId").GetString());
        Assert.Equal("Gemma", relationship.GetProperty("targetCharacterName").GetString());
        Assert.Equal("Lucia trusts Gemma.", relationship.GetProperty("howSourceSeesTarget").GetString());
        Assert.Equal("Ally", relationship.GetProperty("relationshipTypes")[0].GetString());
        Assert.Equal("Protective", relationship.GetProperty("privateTensions")[0].GetString());
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
    public async Task CreateCharacterRequiresUsableCompleteProfile()
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "create_character",
            """{"updates":{"name":"Mira"}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("fuller profile", json.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Contains("summary", json.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Contains(json.RootElement.GetProperty("nextStep").GetProperty("fields").EnumerateArray(), item => item.GetString() == "hairColor");
        Assert.Empty(callbacks.ToolItems);
        Assert.Equal(2, document.Characters.Count);
    }

    [Fact]
    public async Task CreateCharacterAcceptsFlatCompleteAppearanceProfile()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "create_character",
            """{"updates":{"name":"Mira","summary":"A poised courier with dangerous friends.","personality":"Alert, charming, and decisive under pressure.","voice":"Quick, bright, and teasing.","traits":["charmer","observer"],"hairColor":"black","hairStyles":["short","wavy"],"eyeColor":"green","faceShape":"angular","skinTone":"tan","complexion":["sun-kissed"],"height":"average","build":"lean","bodyProportions":["balanced-proportions"],"presentation":["confident"],"attractiveness":"attractive","extraAppearanceDetails":"A thin silver ring on every finger."}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Mira", document.Characters[0].Name);
        Assert.Equal("black", document.Characters[0].AppearanceProfile.HairColor);
        Assert.Equal("A thin silver ring on every finger.", document.Characters[0].Appearance);
        Assert.False(json.RootElement.GetProperty("resultingEntity").TryGetProperty("appearanceProfile", out _));
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
            """{"entityId":"c1","updates":{"pronouns":["they/them","xe/xem"],"sceneRoles":["anchor"],"traits":["guarded","dry-wit"],"coreDrive":"protect-their-people","softSpots":["being-trusted"],"avoidPatterns":["no-random-cruelty"]}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(["anchor"], document.Characters[0].SceneRoles);
        Assert.Equal(["guarded", "dry-wit"], document.Characters[0].Traits);
        Assert.Equal(["they/them", "xe/xem"], document.Characters[0].Pronouns);
        Assert.Equal("protect-their-people", document.Characters[0].CoreDrive);
    }

    [Fact]
    public async Task CharacterPatchMapsExtraAppearanceDetailsToStoredAppearance()
    {
        var document = CreateDocument();
        CompleteAppearance(document.Characters[0]);
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"extraAppearanceDetails":"Small crescent scar under one eye."}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Small crescent scar under one eye.", document.Characters[0].Appearance);
        var item = callbacks.ToolItems.Single();
        Assert.Contains(item.Diffs, diff => diff.Field == "extraAppearanceDetails");
        Assert.DoesNotContain(item.Diffs, diff => diff.Field == "appearanceSummary");
    }

    [Fact]
    public async Task CharacterPatchRejectsAppearanceUpdateWhenResultWouldStayIncomplete()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"hairColor":"blonde"}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("complete visual profile", json.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Contains(json.RootElement.GetProperty("nextStep").GetProperty("fields").EnumerateArray(), item => item.GetString() == "eyeColor");
        Assert.Equal("", document.Characters[0].AppearanceProfile.HairColor);
        Assert.Empty(callbacks.ToolItems);
    }

    [Fact]
    public async Task CharacterPatchMapsFlatAppearanceFieldsToStoredAppearanceProfile()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"hairColor":"blonde","hairStyles":["long","wavy"],"eyeColor":"blue","faceShape":"heart-shaped","skinTone":"fair","complexion":["clear"],"height":"tall","build":"slender","bodyProportions":["long-legs"],"presentation":["graceful"],"attractiveness":"striking"}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("blonde", document.Characters[0].AppearanceProfile.HairColor);
        Assert.Equal(["long", "wavy"], document.Characters[0].AppearanceProfile.HairStyles);
        Assert.Equal("blue", document.Characters[0].AppearanceProfile.EyeColor);
        Assert.Contains(callbacks.ToolItems.Single().Diffs, diff => diff.Field == "hairColor" && diff.Label == "Hair Color");
        Assert.DoesNotContain(callbacks.ToolItems.Single().Diffs, diff => diff.Field == "appearanceProfile");
    }

    [Fact]
    public async Task CharacterPatchRejectsLegacyAppearanceField()
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"appearance":"Tall blonde."}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("not a supported Story Assistant field", json.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Equal("", document.Characters[0].Appearance);
        Assert.Empty(callbacks.ToolItems);
    }

    [Fact]
    public async Task CharacterPatchRejectsInvalidPronounWithoutMutating()
    {
        var document = CreateDocument();
        document.Characters[0].Pronouns = ["she/her"];
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"pronouns":["invented/pronoun"]}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("invalid value 'invented/pronoun'", json.RootElement.GetProperty("reason").GetString());
        Assert.Contains(json.RootElement.GetProperty("nextStep").GetProperty("fields").EnumerateArray(), item => item.GetString() == "pronouns");
        Assert.Equal(["she/her"], document.Characters[0].Pronouns);
        Assert.Empty(callbacks.ToolItems);
    }

    [Fact]
    public async Task CharacterPatchRejectsDuplicatePronouns()
    {
        var document = CreateDocument();
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "update_character",
            """{"entityId":"c1","updates":{"pronouns":["he/him","he/him"]}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("duplicate value 'he/him'", json.RootElement.GetProperty("reason").GetString());
        Assert.Empty(document.Characters[0].Pronouns);
    }

    [Fact]
    public async Task CharacterPatchDoesNotCreateLocalRelationshipState()
    {
        var document = CreateDocument();
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
        Assert.Equal("New summary", document.Characters[0].Summary);
        Assert.Empty(document.CharacterRelationships);
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
            """{"sourceCharacterId":"c1","targetCharacterId":"c2","howSourceSeesTarget":"Lucia pushes Gemma.","howTargetSeesSource":"Gemma pushes back.","publicDynamic":"Open rivals","relationshipTypes":["rivals"],"privateTensions":["custom dynamic"]}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("relationship types contains invalid value 'rivals'", json.RootElement.GetProperty("reason").GetString());
        Assert.Empty(document.CharacterRelationships);
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

    [Fact]
    public async Task SetSceneRequiresReviewAndCallsNarratorTransition()
    {
        var document = CreateDocument();
        document.StoryAssistant.ReviewMode = StoryAssistantReviewMode.AutoApprove;
        document.Locations.Add(new() { Id = "l1", Name = "Library" });
        document.Items.Add(new() { Id = "i1", Name = "Lantern" });
        var callbacks = new TestCallbacks();
        callbacks.SceneTransition = request => new(new SceneTransitionService().Build(document, request), "turn-1", "The library waits in lamplight.");
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "set_scene",
            """{"locationId":"l1","characterIds":["c1","c2"],"itemIds":["i1"],"narratorGuidance":{"purpose":"opening_scene","guidance":"Open in the library."}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("pending", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, callbacks.ReviewCount);
        Assert.Equal(0, callbacks.SceneTransitionCount);
        var workItem = callbacks.WorkItems.Single();
        Assert.Contains(workItem.Diffs, diff => diff.Field == "locationName");

        await service.ResolveWorkItemAsync(
            document,
            workItem,
            new(StoryAssistantWorkItemResolutionKind.Accept, "", ""),
            callbacks,
            CancellationToken.None);

        Assert.Equal(1, callbacks.SceneTransitionCount);
        Assert.Equal(StoryAssistantWorkItemStatus.Completed, workItem.Status);
        using var resolvedJson = JsonDocument.Parse(workItem.ResultJson);
        Assert.Equal("turn-1", resolvedJson.RootElement.GetProperty("narratorTurnId").GetString());
        Assert.Equal("The library waits in lamplight.", resolvedJson.RootElement.GetProperty("narratorMessage").GetString());
    }

    [Fact]
    public async Task RejectedSetSceneDoesNotCallNarratorTransition()
    {
        var document = CreateDocument();
        document.Locations.Add(new() { Id = "l1", Name = "Library" });
        var callbacks = new TestCallbacks { Decision = new(StoryAssistantDecisionKind.Reject, "Not yet.") };
        callbacks.SceneTransition = request => new(new SceneTransitionService().Build(document, request), "turn-1", "The library waits in lamplight.");
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "set_scene",
            """{"locationId":"l1","characterIds":["c1"],"itemIds":[],"narratorGuidance":{"purpose":"opening_scene","guidance":"Open in the library."}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("pending", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, callbacks.SceneTransitionCount);
        var workItem = callbacks.WorkItems.Single();

        await service.ResolveWorkItemAsync(
            document,
            workItem,
            new(StoryAssistantWorkItemResolutionKind.Reject, "", "Not yet."),
            callbacks,
            CancellationToken.None);

        Assert.Equal(0, callbacks.SceneTransitionCount);
        Assert.Equal(StoryAssistantWorkItemStatus.Rejected, workItem.Status);
    }

    [Fact]
    public async Task SetSceneUnknownIdsTellModelToReadEntities()
    {
        var document = CreateDocument();
        document.Locations.Add(new() { Id = "l1", Name = "Library" });
        var callbacks = new TestCallbacks();
        var service = new StoryEntityPatchService();

        var result = await service.ExecuteAsync(
            document,
            "call-1",
            "set_scene",
            """{"locationId":"l1","characterIds":["missing"],"itemIds":[],"narratorGuidance":{"purpose":"opening_scene","guidance":"Open in the library."}}""",
            callbacks,
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("get_story_entities", json.RootElement.GetProperty("nextStep").GetProperty("tool").GetString());
        Assert.Empty(callbacks.ToolItems);
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

    static void CompleteAppearance(RpCharacter character)
    {
        character.AppearanceProfile.HairColor = "brown";
        character.AppearanceProfile.HairStyles = ["short"];
        character.AppearanceProfile.EyeColor = "green";
        character.AppearanceProfile.FaceShape = "oval";
        character.AppearanceProfile.SkinTone = "light";
        character.AppearanceProfile.Complexion = ["clear"];
        character.AppearanceProfile.Height = "average";
        character.AppearanceProfile.Build = "lean";
        character.AppearanceProfile.BodyProportions = ["balanced-proportions"];
        character.AppearanceProfile.Presentation = ["confident"];
        character.AppearanceProfile.Attractiveness = "attractive";
    }

    sealed class TestCallbacks : IStoryAssistantCallbacks
    {
        public StoryAssistantDecision Decision { get; set; } = new(StoryAssistantDecisionKind.Accept, "");
        public List<StoryAssistantTranscriptItem> ToolItems { get; } = [];
        public List<StoryAssistantWorkItem> WorkItems { get; } = [];
        public List<RoleplayStoreArea> SavedAreas { get; } = [];
        public Func<SetSceneRequest, SceneTransitionResult>? SceneTransition { get; set; }
        public int ReviewCount { get; private set; }
        public int SceneTransitionCount { get; private set; }

        public Task AppendAssistantTextAsync(string delta, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken)
        {
            ToolItems.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateToolCallAsync(StoryAssistantTranscriptItem item, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordWorkItemAsync(StoryAssistantWorkItem workItem, CancellationToken cancellationToken)
        {
            WorkItems.Add(workItem);
            ReviewCount++;
            return Task.CompletedTask;
        }

        public Task UpdateWorkItemAsync(StoryAssistantWorkItem workItem, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SceneTransitionResult> SetSceneAsync(SetSceneRequest request, CancellationToken cancellationToken)
        {
            SceneTransitionCount++;
            if (SceneTransition is null)
                throw new NotSupportedException();

            return Task.FromResult(SceneTransition(request));
        }

        public Task SaveEntityAreaAsync(RoleplayStoreArea area, CancellationToken cancellationToken)
        {
            SavedAreas.Add(area);
            return Task.CompletedTask;
        }

        public Task SaveAssistantStateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
