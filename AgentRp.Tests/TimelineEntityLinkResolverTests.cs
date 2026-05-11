using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Tests;

public sealed class TimelineEntityLinkResolverTests
{
    [Fact]
    public void ResolveCharacterIdsAcceptsIdsAndExactNames()
    {
        var ids = TimelineEntityLinkResolver.ResolveCharacterIds(Characters(), ["c2", "Lucia", "missing", "Gemma"]);

        Assert.Equal(["c2", "c1"], ids);
    }

    [Fact]
    public void ResolveLocationIdsAcceptsIdsAndExactNames()
    {
        var ids = TimelineEntityLinkResolver.ResolveLocationIds(Locations(), ["l2", "Devonshire Apartment 822", "missing"]);

        Assert.Equal(["l2", "l1"], ids);
    }

    [Fact]
    public void ResolveIdsDropsUnknownsAndDuplicates()
    {
        var ids = TimelineEntityLinkResolver.ResolveLocationIds(Locations(), ["Devonshire Apartment 822", "l1", "", "Unknown"]);

        Assert.Equal(["l1"], ids);
    }

    static List<RpCharacter> Characters() =>
    [
        new() { Id = "c1", Name = "Lucia" },
        new() { Id = "c2", Name = "Gemma" }
    ];

    static List<RpLocation> Locations() =>
    [
        new() { Id = "l1", Name = "Devonshire Apartment 822" },
        new() { Id = "l2", Name = "Blackstone Library" }
    ];
}
