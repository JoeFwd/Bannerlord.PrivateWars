using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.Registry;
using Xunit;

namespace Bannerlord.PrivateWars.Tests;

public class PrivateWarHostilityCacheTests
{
    [Fact]
    public void RepeatedQueries_ResolveEachHierarchyLinkOnce()
    {
        var hierarchy = new VersionedHierarchy();
        hierarchy.Links["vassal"] = "liege";
        hierarchy.Links["liege"] = "attacker";
        var registry = Registry(hierarchy);

        for (var i = 0; i < 1000; i++)
        {
            Assert.True(registry.AreEnemies("vassal", "defender"));
            Assert.True(registry.AreAllies("attacker", "vassal"));
            Assert.False(registry.AreEnemies("attacker", "vassal"));
        }

        Assert.Equal(2, hierarchy.Calls);
    }

    [Fact]
    public void ResolveSideAndGetSuzerain_ShareTheHostilityCache()
    {
        var hierarchy = new VersionedHierarchy();
        hierarchy.Links["vassal"] = "liege";
        hierarchy.Links["liege"] = "attacker";
        var registry = Registry(hierarchy);
        var war = registry.Get("war")!;
        Assert.True(registry.AreEnemies("vassal", "defender"));

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(WarSide.Attacker, registry.ResolveSide("vassal", war));
            Assert.Equal("liege", registry.GetSuzerain("vassal"));
        }

        Assert.Equal(2, hierarchy.Calls);
    }

    [Fact]
    public void ResolveSide_DoesNotServeCachedSidesForAnUnstoredWarInstance()
    {
        var hierarchy = new VersionedHierarchy();
        hierarchy.Links["vassal"] = "attacker";
        var registry = Registry(hierarchy);
        Assert.Equal(WarSide.Attacker, registry.ResolveSide("vassal", registry.Get("war")!));

        var pending = War() with { AttackerPrincipalClanId = "other" };

        Assert.Null(registry.ResolveSide("vassal", pending));
        Assert.Equal(WarSide.Attacker, registry.ResolveSide("vassal", registry.Get("war")!));
    }

    [Fact]
    public void UninvolvedClans_CacheNullResults()
    {
        var hierarchy = new VersionedHierarchy();
        var registry = Registry(hierarchy);

        for (var i = 0; i < 1000; i++)
        {
            Assert.False(registry.AreEnemies("uninvolved", "defender"));
            Assert.False(registry.AreAllies("attacker", "uninvolved"));
        }

        Assert.Equal(1, hierarchy.Calls);
    }

    [Fact]
    public void HierarchyRevisionChange_InvalidatesPositiveAndNullResults()
    {
        var hierarchy = new VersionedHierarchy();
        hierarchy.Links["vassal"] = "attacker";
        var registry = Registry(hierarchy);
        Assert.True(registry.AreEnemies("vassal", "defender"));
        Assert.False(registry.AreEnemies("uninvolved", "defender"));

        hierarchy.Links["vassal"] = "defender";
        hierarchy.Links["uninvolved"] = "attacker";
        hierarchy.Revision++;

        Assert.False(registry.AreEnemies("vassal", "defender"));
        Assert.True(registry.AreAllies("vassal", "defender"));
        Assert.True(registry.AreEnemies("uninvolved", "defender"));
        Assert.Equal(4, hierarchy.Calls);
    }

    [Fact]
    public void SupporterUpdate_RecomputesSidesWithoutRescanningHierarchy()
    {
        var hierarchy = new VersionedHierarchy();
        hierarchy.Links["vassal"] = "supporter";
        var registry = Registry(hierarchy);
        Assert.False(registry.AreEnemies("vassal", "defender"));
        Assert.Equal(2, hierarchy.Calls);

        registry.Update(War() with { AttackerSupporters = new[] { "supporter" } });

        Assert.True(registry.AreEnemies("vassal", "defender"));
        Assert.True(registry.AreAllies("vassal", "attacker"));
        Assert.Equal(2, hierarchy.Calls);
    }

    [Fact]
    public void ReplacingWarWithSameId_RecomputesPrincipalSides()
    {
        var hierarchy = new VersionedHierarchy();
        hierarchy.Links["vassal"] = "attacker";
        var registry = Registry(hierarchy);
        Assert.True(registry.AreEnemies("vassal", "defender"));

        registry.Add(War() with { DefenderPrincipalClanId = "new_defender" });

        Assert.True(registry.AreAllies("vassal", "attacker"));
        Assert.False(registry.AreEnemies("vassal", "defender"));
        Assert.True(registry.AreEnemies("vassal", "new_defender"));
        Assert.Equal(2, hierarchy.Calls);
    }

    [Fact]
    public void ConcludedOrRemovedWar_NoLongerSuppliesHostility()
    {
        var registry = Registry(new VersionedHierarchy());
        Assert.True(registry.AreEnemies("attacker", "defender"));

        registry.Update(War() with { Status = PrivateWarStatus.Concluded });
        Assert.False(registry.AreEnemies("attacker", "defender"));

        registry.Update(War());
        Assert.True(registry.AreEnemies("attacker", "defender"));
        registry.Remove("war");
        Assert.False(registry.AreEnemies("attacker", "defender"));
    }

    [Fact]
    public void Initialisation_DiscardsCachedHierarchyEvenWhenRevisionIsUnchanged()
    {
        var hierarchy = new VersionedHierarchy();
        hierarchy.Links["vassal"] = "attacker";
        var registry = Registry(hierarchy);
        Assert.True(registry.AreEnemies("vassal", "defender"));

        hierarchy.Links["vassal"] = "defender";
        registry.Initialise(new[] { War() });

        Assert.False(registry.AreEnemies("vassal", "defender"));
        Assert.True(registry.AreAllies("vassal", "defender"));
        Assert.Equal(2, hierarchy.Calls);
        registry.Initialise(Array.Empty<PrivateWar>());
        Assert.False(registry.AreEnemies("attacker", "defender"));
    }

    [Fact]
    public void UnversionedProvider_ContinuesReadingLiveHierarchy()
    {
        var hierarchy = new Hierarchy();
        hierarchy.Links["vassal"] = "attacker";
        var registry = Registry(hierarchy);
        Assert.True(registry.AreEnemies("vassal", "defender"));

        hierarchy.Links["vassal"] = "defender";

        Assert.False(registry.AreEnemies("vassal", "defender"));
        Assert.True(registry.AreAllies("vassal", "defender"));
        Assert.Equal(3, hierarchy.Calls);
    }

    [Fact]
    public void ReplacingProviderWithEqualRevision_InvalidatesCachedSides()
    {
        var first = new VersionedHierarchy();
        first.Links["vassal"] = "attacker";
        var second = new VersionedHierarchy();
        second.Links["vassal"] = "defender";
        var proxy = new MutableSuzerainProvider();
        proxy.SetProvider(first);
        var registry = Registry(proxy);
        Assert.True(registry.AreEnemies("vassal", "defender"));

        proxy.SetProvider(second);

        Assert.False(registry.AreEnemies("vassal", "defender"));
        Assert.True(registry.AreAllies("vassal", "defender"));
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
    }

    [Fact]
    public void MutableProvider_ForwardsRevisionChangesAndUnversionedFallback()
    {
        var hierarchy = new VersionedHierarchy();
        hierarchy.Links["vassal"] = "attacker";
        var proxy = new MutableSuzerainProvider();
        Assert.Null(proxy.Revision);
        proxy.SetProvider(hierarchy);
        var registry = Registry(proxy);
        Assert.True(registry.AreEnemies("vassal", "defender"));

        hierarchy.Links["vassal"] = "defender";
        hierarchy.Revision++;
        Assert.False(registry.AreEnemies("vassal", "defender"));

        var unversioned = new Hierarchy();
        unversioned.Links["vassal"] = "attacker";
        proxy.SetProvider(unversioned);
        Assert.Null(proxy.Revision);
        Assert.True(registry.AreEnemies("vassal", "defender"));
        unversioned.Links["vassal"] = "defender";
        Assert.False(registry.AreEnemies("vassal", "defender"));
    }

    [Fact]
    public void ParallelQueries_ShareOneHierarchyCache()
    {
        var hierarchy = new VersionedHierarchy();
        hierarchy.Links["vassal"] = "attacker";
        var registry = Registry(hierarchy);

        Parallel.For(0, 1000, _ =>
        {
            Assert.True(registry.AreEnemies("vassal", "defender"));
            Assert.True(registry.AreAllies("vassal", "attacker"));
            Assert.False(registry.AreEnemies("uninvolved", "defender"));
        });

        Assert.Equal(2, hierarchy.Calls);
    }

    private static InMemoryPrivateWarRegistry Registry(ISuzerainProvider hierarchy)
    {
        var registry = new InMemoryPrivateWarRegistry(new WarSideResolver(), hierarchy);
        registry.Add(War());
        return registry;
    }

    private static PrivateWar War() => new(
        "war", "attacker", "defender", Array.Empty<string>(), Array.Empty<string>(),
        "claim", "title", "settlement", new Dictionary<string, string>(),
        0, 0, 0, 0, PrivateWarStatus.Active);

    private class Hierarchy : ISuzerainProvider
    {
        public Dictionary<string, string?> Links { get; } = new();
        public int Calls;

        public string? GetSuzerain(string clanId)
        {
            Interlocked.Increment(ref Calls);
            return Links.TryGetValue(clanId, out var suzerain) ? suzerain : null;
        }
    }

    private sealed class VersionedHierarchy : Hierarchy, IVersionedSuzerainProvider
    {
        public long? Revision { get; set; } = 0;
    }
}
