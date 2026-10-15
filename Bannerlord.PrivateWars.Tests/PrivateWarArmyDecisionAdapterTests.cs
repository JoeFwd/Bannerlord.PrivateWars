using System;
using System.Collections.Generic;
using Bannerlord.PrivateWars.Armies;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Xunit;

namespace Bannerlord.PrivateWars.Tests;

public class PrivateWarArmyDecisionAdapterTests
{
    [Fact]
    public void FilterOrdinaryMembers_ExcludesPrivateEnemyAndFailsWeakenedFormation()
    {
        var adapter = new PrivateWarArmyDecisionAdapter(new PrivateWarArmyPolicy());
        var candidates = new[]
        {
            new PrivateWarArmyCandidate("ally", "A_vassal", false, true, 400f, 1f),
            new PrivateWarArmyCandidate("private_enemy", "D", false, true, 600f, 1f)
        };

        var decision = adapter.FilterOrdinaryMembers(
            vanillaCanCreateArmy: true,
            leaderClanId: "A",
            leaderSiegeStrength: 500f,
            kingdomHasSettlements: true,
            candidates,
            (left, right) => left == "A" && right == "D",
            _ => false);

        Assert.False(decision.CanCreateArmy);
        Assert.Empty(decision.MemberPartyIds);
    }

    [Fact]
    public void FilterOrdinaryMembers_RetainsAuthorizedMembersWhenFormationRemainsStrongEnough()
    {
        var adapter = new PrivateWarArmyDecisionAdapter(new PrivateWarArmyPolicy());
        var candidates = new[]
        {
            new PrivateWarArmyCandidate("ally", "A_vassal", false, true, 600f, 1f),
            new PrivateWarArmyCandidate("private_enemy", "D", false, true, 600f, 1f)
        };

        var decision = adapter.FilterOrdinaryMembers(
            vanillaCanCreateArmy: true,
            leaderClanId: "A",
            leaderSiegeStrength: 500f,
            kingdomHasSettlements: true,
            candidates,
            (left, right) => left == "A" && right == "D",
            _ => false);

        Assert.True(decision.CanCreateArmy);
        Assert.Equal(new[] { "ally" }, decision.MemberPartyIds);
    }

    [Fact]
    public void FilterOrdinaryMembers_KeepsPrivateWarBelligerentsOutOfOrdinaryArmies()
    {
        var adapter = new PrivateWarArmyDecisionAdapter(new PrivateWarArmyPolicy());
        var candidates = new[]
        {
            new PrivateWarArmyCandidate("neutral", "N", false, true, 1200f, 1f),
            new PrivateWarArmyCandidate("belligerent", "B", false, true, 1200f, 1f)
        };

        var decision = adapter.FilterOrdinaryMembers(
            vanillaCanCreateArmy: true,
            leaderClanId: "K",
            leaderSiegeStrength: 500f,
            kingdomHasSettlements: true,
            candidates,
            (_, _) => false,
            clanId => clanId == "B");

        Assert.True(decision.CanCreateArmy);
        Assert.Equal(new[] { "neutral" }, decision.MemberPartyIds);
    }

    [Fact]
    public void CreatePlan_AcceptsPrivateArmyBelowVanillaStrengthFloor()
    {
        var adapter = new PrivateWarArmyDecisionAdapter(new PrivateWarArmyPolicy());
        var candidates = new[]
        {
            new PrivateWarArmyCandidate("leader", "A", true, false, 500f, 0f),
            new PrivateWarArmyCandidate("same_side", "A_vassal", true, true, 400f, 1f)
        };

        var plan = adapter.CreatePlan(
            War(), WarSide.Attacker, "leader", candidates,
            Array.Empty<PrivateWarArmyAssignment>(),
            clan => clan == "A_vassal" ? "A" : null,
            maximumMemberCount: 1);

        Assert.Equal(new[] { "same_side" }, plan?.MemberPartyIds);
    }

    [Fact]
    public void CreatePlan_SelectsHighestDesirabilityMemberBeforeCapacityAndStrengthChecks()
    {
        var adapter = new PrivateWarArmyDecisionAdapter(new PrivateWarArmyPolicy());
        var candidates = new[]
        {
            new PrivateWarArmyCandidate("leader", "A", true, false, 600f, 0f),
            new PrivateWarArmyCandidate("first", "A_vassal", true, true, 300f, 2f),
            new PrivateWarArmyCandidate("second", "A_subvassal", true, true, 900f, 8f)
        };

        var plan = adapter.CreatePlan(
            War(), WarSide.Attacker, "leader", candidates,
            Array.Empty<PrivateWarArmyAssignment>(),
            clan => clan switch
            {
                "A_vassal" => "A",
                "A_subvassal" => "A_vassal",
                _ => null
            },
            maximumMemberCount: 1);

        Assert.Equal(new[] { "second" }, plan?.MemberPartyIds);
    }

    [Theory]
    [InlineData(false, true, "leader", "goal", "leader", "goal")]
    [InlineData(true, false, "leader", "goal", "leader", "goal")]
    [InlineData(true, true, "leader", "goal", "leader", "other_goal")]
    public void DecidePrivateCreation_LeavesUnrelatedCreationUnchanged(
        bool isBesieger,
        bool hasActivePrivateWar,
        string leaderPartyId,
        string targetSettlementId,
        string privateWarLeaderPartyId,
        string warTargetSettlementId)
    {
        var adapter = new PrivateWarArmyDecisionAdapter(new PrivateWarArmyPolicy());
        var revalidationCalls = 0;

        var decision = adapter.DecidePrivateCreation(
            isBesieger,
            hasActivePrivateWar,
            leaderPartyId,
            targetSettlementId,
            privateWarLeaderPartyId,
            warTargetSettlementId,
            () =>
            {
                revalidationCalls++;
                return new[] { "private_member" };
            });

        Assert.Equal(PrivateWarArmyCreationAction.ContinueOriginal, decision.Action);
        Assert.Equal(0, revalidationCalls);
    }

    [Fact]
    public void DecidePrivateCreation_RevalidatesMatchingCreationAndReplacesMembers()
    {
        var adapter = new PrivateWarArmyDecisionAdapter(new PrivateWarArmyPolicy());
        var authorizedMembers = new[] { "fresh_member" };
        var revalidationCalls = 0;

        var decision = adapter.DecidePrivateCreation(
            isBesieger: true,
            hasActivePrivateWar: true,
            leaderPartyId: "leader",
            targetSettlementId: "goal",
            privateWarLeaderPartyId: "leader",
            warTargetSettlementId: "goal",
            revalidateMembers: () =>
            {
                revalidationCalls++;
                return authorizedMembers;
            });

        Assert.Equal(PrivateWarArmyCreationAction.ReplaceMembers, decision.Action);
        Assert.Same(authorizedMembers, decision.MemberPartyIds);
        Assert.Equal(1, revalidationCalls);
    }

    [Fact]
    public void DecidePrivateCreation_SuppressesWarArmyFromNonDesignatedLeader()
    {
        var adapter = new PrivateWarArmyDecisionAdapter(new PrivateWarArmyPolicy());
        var revalidationCalls = 0;

        var decision = adapter.DecidePrivateCreation(
            isBesieger: true,
            hasActivePrivateWar: true,
            leaderPartyId: "leader",
            targetSettlementId: "goal",
            privateWarLeaderPartyId: "other",
            warTargetSettlementId: "goal",
            revalidateMembers: () =>
            {
                revalidationCalls++;
                return new[] { "member" };
            });

        Assert.Equal(PrivateWarArmyCreationAction.Suppress, decision.Action);
        Assert.Equal(0, revalidationCalls);
    }

    [Fact]
    public void DecidePrivateCreation_SuppressesMatchingCreationWhenRevalidationFails()
    {
        var adapter = new PrivateWarArmyDecisionAdapter(new PrivateWarArmyPolicy());

        var decision = adapter.DecidePrivateCreation(
            isBesieger: true,
            hasActivePrivateWar: true,
            leaderPartyId: "leader",
            targetSettlementId: "goal",
            privateWarLeaderPartyId: "leader",
            warTargetSettlementId: "goal",
            revalidateMembers: () => null);

        Assert.Equal(PrivateWarArmyCreationAction.Suppress, decision.Action);
        Assert.Empty(decision.MemberPartyIds);
    }

    private static PrivateWar War()
        => new(
            "war", "A", "D", Array.Empty<string>(), Array.Empty<string>(), "claim", "title", "goal",
            new Dictionary<string, string>(), 0f, 0f, 0f, 0f,
            PrivateWarStatus.Active);
}
