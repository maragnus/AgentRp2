using System.Text.Json;
using AgentRp.Models;
using AgentRp.Serialization;
using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Tests;

public sealed class CharacterProfileRulesTests
{
    [Fact]
    public void CharacterToolSchemaKeepsTraitLibraryOutOfMutationTools()
    {
        var document = new RpChatDocument
        {
            CharacterTraitLibrary = CharacterTraitLibraryService.CreateDefaultState()
        };
        var tool = StoryAssistantService.BuildTools(document).Single(tool => tool.Name == "update_character");

        using var json = JsonDocument.Parse(tool.Parameters.ToJsonString());
        var updates = json.RootElement.GetProperty("properties").GetProperty("updates").GetProperty("properties");
        var traits = updates.GetProperty("traits");
        var sceneRoles = updates.GetProperty("sceneRoles");
        var pronouns = updates.GetProperty("pronouns");
        var extraAppearanceDetails = updates.GetProperty(CharacterProfileRules.ExtraAppearanceDetailsField);
        var hairColor = updates.GetProperty("hairColor");
        var hairStyles = updates.GetProperty("hairStyles");
        var schemaText = tool.Parameters.ToJsonString();

        Assert.True(traits.GetProperty("uniqueItems").GetBoolean());
        Assert.Equal(CharacterProfileRules.MaxTraits, traits.GetProperty("maxItems").GetInt32());
        Assert.True(sceneRoles.GetProperty("uniqueItems").GetBoolean());
        Assert.Equal(CharacterProfileRules.MaxSceneRoles, sceneRoles.GetProperty("maxItems").GetInt32());
        Assert.True(pronouns.GetProperty("uniqueItems").GetBoolean());
        Assert.Equal(CharacterProfileRules.MaxPronouns, pronouns.GetProperty("maxItems").GetInt32());
        Assert.Equal(
            CharacterProfileRules.PronounOptions.Select(option => option.Id),
            pronouns.GetProperty("items").GetProperty("enum").EnumerateArray().Select(item => item.GetString() ?? ""));
        Assert.False(traits.GetProperty("items").TryGetProperty("enum", out _));
        Assert.DoesNotContain("guarded", schemaText, StringComparison.Ordinal);
        Assert.False(updates.TryGetProperty("relationships", out _));
        Assert.False(updates.TryGetProperty("appearance", out _));
        Assert.False(updates.TryGetProperty("appearanceProfile", out _));
        Assert.Contains("Extra visible appearance details", extraAppearanceDetails.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("complete visual profile", hairColor.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Equal(CharacterProfileRules.MaxHairStyles, hairStyles.GetProperty("maxItems").GetInt32());
        Assert.Contains("unknown or stale", schemaText, StringComparison.Ordinal);
    }

    [Fact]
    public void RelationshipToolSchemaUsesFlatRelationshipFields()
    {
        var tool = StoryAssistantService.BuildTools(new()).Single(tool => tool.Name == "update_character_relationship");

        using var json = JsonDocument.Parse(tool.Parameters.ToJsonString());
        var properties = json.RootElement.GetProperty("properties");

        Assert.True(properties.TryGetProperty("howSourceSeesTarget", out _));
        Assert.True(properties.TryGetProperty("howTargetSeesSource", out _));
        Assert.True(properties.TryGetProperty("publicDynamic", out _));
        Assert.True(properties.TryGetProperty("relationshipTypes", out var relationshipTypes));
        Assert.True(properties.TryGetProperty("privateTensions", out var privateTensions));
        var required = json.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToList();
        Assert.Contains("howSourceSeesTarget", required);
        Assert.Contains("howTargetSeesSource", required);
        Assert.Contains("publicDynamic", required);
        Assert.Contains("relationshipTypes", required);
        Assert.Contains("privateTensions", required);
        Assert.Equal("array", relationshipTypes.GetProperty("type").GetString());
        Assert.Equal("array", privateTensions.GetProperty("type").GetString());
        Assert.False(properties.TryGetProperty("profileRelationships", out _));
        Assert.False(properties.TryGetProperty("relationshipType", out _));
        Assert.False(properties.TryGetProperty("privateTension", out _));
    }

    [Fact]
    public void RelationshipPatchRequiresEveryRelationshipField()
    {
        using var json = JsonDocument.Parse("""{"sourceCharacterId":"c1","targetCharacterId":"c2","howSourceSeesTarget":"Trusts her.","relationshipTypes":["Ally"]}""");

        var exception = Assert.Throws<CharacterProfileValidationException>(() =>
            CharacterProfileRules.ValidateRelationshipPatch(json.RootElement, CharacterTraitLibraryService.CreateDefaultState()));

        Assert.Contains("every relationship field", exception.Message, StringComparison.Ordinal);
        Assert.Contains("howTargetSeesSource", exception.Fields);
        Assert.Contains("publicDynamic", exception.Fields);
        Assert.Contains("privateTensions", exception.Fields);
    }

    [Fact]
    public void ProfileOptionsToolReturnsCustomizedTraitLibraryValues()
    {
        var document = new RpChatDocument
        {
            CharacterTraitLibrary = CharacterTraitLibraryService.CreateDefaultState()
        };
        document.CharacterTraitLibrary.SceneRoles = [new("foil", "Foil", "Contrasts another character.")];
        var options = CharacterProfileRules.ProfileOptions(document.CharacterTraitLibrary, ["sceneRoles"]);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(options, AppJsonSerializerOptions.Web));
        Assert.Contains("flat appearance fields", json.RootElement.GetProperty("appearancePolicy").GetString(), StringComparison.Ordinal);
        var sceneRoleEnum = json.RootElement
            .GetProperty("fields")
            .GetProperty("sceneRoles")
            .GetProperty("options")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .ToList();

        Assert.Contains("foil", sceneRoleEnum);
        Assert.DoesNotContain("anchor", sceneRoleEnum);
    }

    [Fact]
    public void ProfileOptionsForAppearanceFieldReturnsCompleteAppearanceGroup()
    {
        var options = CharacterProfileRules.ProfileOptions(CharacterTraitLibraryService.CreateDefaultState(), ["hairColor"]);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(options, AppJsonSerializerOptions.Web));
        var fields = json.RootElement.GetProperty("fields");

        Assert.True(fields.TryGetProperty("hairColor", out _));
        Assert.True(fields.TryGetProperty("eyeColor", out _));
        Assert.True(fields.TryGetProperty("bodyProportions", out _));
        Assert.True(fields.TryGetProperty("attractiveness", out _));
    }

    [Fact]
    public void ProfileOptionsForRelationshipFieldsUseFlatNames()
    {
        var options = CharacterProfileRules.ProfileOptions(CharacterTraitLibraryService.CreateDefaultState(), ["relationshipTypes", "privateTensions"]);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(options, AppJsonSerializerOptions.Web));
        var fields = json.RootElement.GetProperty("fields");

        Assert.True(fields.TryGetProperty("relationshipTypes", out _));
        Assert.True(fields.TryGetProperty("privateTensions", out _));
        Assert.False(fields.TryGetProperty("relationshipType", out _));
        Assert.False(fields.TryGetProperty("privateTension", out _));
    }

    [Fact]
    public void ProfileOptionsSchemaUsesOnlyFieldNameEnums()
    {
        var tool = StoryAssistantService.BuildTools(new()).Single(tool => tool.Name == "get_character_profile_options");

        using var json = JsonDocument.Parse(tool.Parameters.ToJsonString());
        var fieldEnum = json.RootElement
            .GetProperty("properties")
            .GetProperty("fields")
            .GetProperty("items")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToList();

        Assert.Contains("traits", fieldEnum);
        Assert.Contains("pronouns", fieldEnum);
        Assert.Contains("coreDrive", fieldEnum);
        Assert.Contains("hairColor", fieldEnum);
        Assert.Contains("attractiveness", fieldEnum);
        Assert.DoesNotContain("appearanceProfile", fieldEnum);
        Assert.DoesNotContain("protect-their-people", fieldEnum);
    }

    [Fact]
    public void LocationItemAndTimelineSchemasUseExplicitPatchFields()
    {
        var tools = StoryAssistantService.BuildTools(new()).ToDictionary(tool => tool.Name);

        AssertCreateSchemaRequires(tools["create_location"], "name");
        AssertCreateSchemaRequires(tools["create_item"], "name");
        AssertCreateSchemaRequires(tools["create_timeline_entry"], "title");
        AssertUpdateSchemaRequiresEntityId(tools["update_location"]);
        AssertUpdateSchemaRequiresEntityId(tools["update_item"]);
        AssertUpdateSchemaRequiresEntityId(tools["update_timeline_entry"]);
        Assert.DoesNotContain("isActive", tools["create_location"].Parameters.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inScene", tools["create_item"].Parameters.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("get_story_entities", tools["update_location"].Description, StringComparison.Ordinal);
        Assert.Contains("get_story_entities", tools["update_item"].Description, StringComparison.Ordinal);
        Assert.Contains("get_story_entities", tools["update_timeline_entry"].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void AskUserToolSchemaSupportsChoiceModesAndDescriptions()
    {
        var tool = StoryAssistantService.BuildTools(new()).Single(tool => tool.Name == "ask_user");

        using var json = JsonDocument.Parse(tool.Parameters.ToJsonString());
        var properties = json.RootElement.GetProperty("properties");
        var choiceProperties = properties
            .GetProperty("choices")
            .GetProperty("items")
            .GetProperty("properties");

        Assert.Contains("onboarding interviews", tool.Description, StringComparison.Ordinal);
        Assert.Contains(properties.GetProperty("selectionMode").GetProperty("enum").EnumerateArray(), item => item.GetString() == "multiple");
        Assert.Equal("integer", properties.GetProperty("minSelections").GetProperty("type").GetString());
        Assert.Equal("integer", properties.GetProperty("maxSelections").GetProperty("type").GetString());
        Assert.True(choiceProperties.TryGetProperty("description", out _));
    }

    static void AssertCreateSchemaRequires(ModelAssistantTool tool, string requiredField)
    {
        using var json = JsonDocument.Parse(tool.Parameters.ToJsonString());
        var updates = json.RootElement.GetProperty("properties").GetProperty("updates");
        Assert.False(updates.GetProperty("additionalProperties").GetBoolean());
        Assert.Contains(updates.GetProperty("required").EnumerateArray(), item => item.GetString() == requiredField);
    }

    static void AssertUpdateSchemaRequiresEntityId(ModelAssistantTool tool)
    {
        using var json = JsonDocument.Parse(tool.Parameters.ToJsonString());
        Assert.Contains(json.RootElement.GetProperty("required").EnumerateArray(), item => item.GetString() == "entityId");
        Assert.False(json.RootElement.GetProperty("properties").GetProperty("updates").GetProperty("additionalProperties").GetBoolean());
    }
}
