using System.Runtime.CompilerServices;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Tests;

public sealed class CharacterTraitLibraryServiceTests
{
    [Fact]
    public void DefaultsIncludeEveryCharacterWizardCollection()
    {
        var defaults = CharacterTraitLibraryService.CreateDefaultState();

        AssertIds(defaults.SceneRoles, "instigator", "anchor", "button-pusher", "mediator");
        Assert.Equal(["Conflict", "Emotional Style", "Social Style", "Attachment", "Humor", "Agency", "Moral Posture", "Vulnerability"], defaults.TraitCategories.Select(group => group.Name));
        AssertIds(defaults.TraitCategories.First(group => group.Name == "Conflict").Items, "deadpan-deflector", "bratty-provoker", "boundary-setter");
        AssertIds(defaults.CoreDrives, "prove-worth", "protect-their-people", "preserve-independence");
        AssertIds(defaults.CoreFears, "being-abandoned", "being-unlovable", "depending-on-someone");
        AssertIds(defaults.SurfaceMasks, "smug-untouchable", "helpful-capable", "mysterious-withholding");
        AssertIds(defaults.HiddenTruths, "needs-reassurance", "wants-to-be-chosen", "still-hopes");
        AssertIds(defaults.SentenceStyles, "terse", "formal", "fragmented");
        AssertIds(defaults.HonestyStyles, "direct", "layered", "accidentally-honest");
        AssertIds(defaults.EmotionalLeakages, "gets-quieter", "gets-warmer", "gets-physical");
        AssertIds(defaults.ActionFingerprints, "lounger", "touch-connector", "restless-spark");
        AssertIds(defaults.StressPatterns, "sharper-under-pressure", "helpful-under-pressure", "protective-under-pressure");
        AssertIds(defaults.SoftSpots, "quiet-inclusion", "being-trusted", "protected-vulnerability");
        AssertIds(defaults.AvoidPatterns, "no-random-cruelty", "no-act-on-unknown-info", "no-flatten-into-one-trait");
        Assert.Contains("Close Friend", defaults.BondTypes);
        Assert.Contains("Acquaintance", defaults.BondTypes);
        Assert.Contains("Power struggle", defaults.Dynamics);
        Assert.Contains("Complicated history", defaults.Dynamics);
    }

    [Fact]
    public void NormalizePreservesCustomValuesAndFillsMissingDefaults()
    {
        var partial = new CharacterTraitLibraryState
        {
            CoreDrives = [new("custom-drive", "Custom Drive", "Custom hover.")],
            TraitCategories =
            [
                new()
                {
                    Name = "Conflict",
                    Color = "custom",
                    Items = [new("custom-trait", "Custom Trait", "Custom hover.")]
                }
            ],
            BondTypes = ["Custom Bond"]
        };

        var normalized = CharacterTraitLibraryService.NormalizeState(partial);

        Assert.Equal(["custom-drive"], normalized.CoreDrives.Select(option => option.Id));
        Assert.Equal("custom", normalized.TraitCategories.First(group => group.Name == "Conflict").Color);
        Assert.Equal(["custom-trait"], normalized.TraitCategories.First(group => group.Name == "Conflict").Items.Select(option => option.Id));
        Assert.Contains(normalized.TraitCategories, group => group.Name == "Emotional Style");
        Assert.NotEmpty(normalized.CoreFears);
        Assert.Equal(["Custom Bond"], normalized.BondTypes);
        Assert.NotEmpty(normalized.Dynamics);
    }

    [Fact]
    public void ValidateRejectsInvalidOptionsAndGroups()
    {
        var duplicate = CharacterTraitLibraryService.CreateDefaultState();
        duplicate.CoreDrives.Add(duplicate.CoreDrives[0]);
        var duplicateException = Assert.Throws<InvalidOperationException>(() => CharacterTraitLibraryService.ValidateState(duplicate));
        Assert.Contains("duplicate option id", duplicateException.Message, StringComparison.Ordinal);

        var emptyOption = CharacterTraitLibraryService.CreateDefaultState();
        emptyOption.CoreFears[0] = new("", "Empty", "");
        var emptyOptionException = Assert.Throws<InvalidOperationException>(() => CharacterTraitLibraryService.ValidateState(emptyOption));
        Assert.Contains("empty id", emptyOptionException.Message, StringComparison.Ordinal);

        var emptyGroup = CharacterTraitLibraryService.CreateDefaultState();
        emptyGroup.TraitCategories.Add(new());
        var emptyGroupException = Assert.Throws<InvalidOperationException>(() => CharacterTraitLibraryService.ValidateState(emptyGroup));
        Assert.Contains("trait group name was empty", emptyGroupException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CharacterWizardReadsTraitLibraryInsteadOfStaticOptionArrays()
    {
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "AgentRp", "Components", "Entities", "CharacterWizardModal.razor"));

        Assert.Contains("Session.Chat.CharacterTraitLibrary.State", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static readonly CharacterOption[] CoreDrives", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static readonly CharacterOption[] EmotionalLeakages", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static readonly string[] BondTypes", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static readonly string[] Dynamics", source, StringComparison.Ordinal);
    }

    static string FindRepoRoot([CallerFilePath] string sourcePath = "")
    {
        foreach (var start in new[] { Path.GetDirectoryName(sourcePath) ?? "", Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AgentRp", "Components", "Entities", "CharacterWizardModal.razor")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not find the AgentRp repository root.");
    }

    static void AssertIds(IReadOnlyList<CharacterOption> options, params string[] ids)
    {
        foreach (var id in ids)
            Assert.Contains(options, option => option.Id == id);
    }
}
