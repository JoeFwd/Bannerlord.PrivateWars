using System;
using System.Collections.Generic;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Spi;
using Xunit;

namespace Bannerlord.PrivateWars.Tests;

public class PrivateWarStateSerialiserTests
{
    [Fact]
    public void CurrentRecord_RoundTrips()
    {
        var original = new PrivateWar(
            "war-1", "attacker", "defender",
            new[] { "attacker-ally-1", "attacker-ally-2" }, new[] { "defender-ally" },
            "claim", "title", "goal",
            new Dictionary<string, string>
            {
                ["town"] = "defender",
                ["castle"] = "attacker"
            },
            12.5f, -7.25f, 100.5f, 110.75f, PrivateWarStatus.Active);

        var serialised = PrivateWarStateSerialiser.SerialiseWars(new[] { original });
        var loaded = Assert.Single(PrivateWarStateSerialiser.DeserialiseWars(serialised));

        AssertWarEqual(original, loaded);
    }

    [Fact]
    public void LegacyElevenFieldRecord_UsesStartDayForGoalLastTakenDay()
    {
        const string legacy =
            "war-legacy|attacker|defender|claim|title|goal|12.5|-7.25|100.5|Active|town:defender";

        var loaded = Assert.Single(PrivateWarStateSerialiser.DeserialiseWars(new List<string> { legacy }));

        Assert.Equal(100.5f, loaded.StartDay);
        Assert.Equal(100.5f, loaded.GoalLastTakenDay);
        Assert.Equal("defender", loaded.OriginalFiefOwners["town"]);
    }

    [Fact]
    public void NonNumericOptionalGoalLastTakenDay_PreservesStartDayFallback()
    {
        const string malformedOptional =
            "war-current|attacker|defender|claim|title|goal|12.5|-7.25|100.5|Active|town:defender|not-a-day";

        var loaded = Assert.Single(
            PrivateWarStateSerialiser.DeserialiseWars(new List<string> { malformedOptional }));

        Assert.Equal(100.5f, loaded.StartDay);
        Assert.Equal(100.5f, loaded.GoalLastTakenDay);
    }

    [Fact]
    public void RecordWithoutSupporterFields_LoadsWithNoSupporters()
    {
        const string preSupporters =
            "war-current|attacker|defender|claim|title|goal|12.5|-7.25|100.5|Active|town:defender|110.75";

        var loaded = Assert.Single(
            PrivateWarStateSerialiser.DeserialiseWars(new List<string> { preSupporters }));

        Assert.Empty(loaded.AttackerSupporters);
        Assert.Empty(loaded.DefenderSupporters);
    }

    [Fact]
    public void MalformedRecord_DoesNotPoisonFollowingValidRecord()
    {
        const string valid =
            "war-valid|attacker|defender|claim|title|goal|12.5|-7.25|100.5|Active|town:defender|110.75";
        List<PrivateWar>? loaded = null;

        var exception = Record.Exception(() => loaded = PrivateWarStateSerialiser.DeserialiseWars(
            new List<string> { null!, "not|enough|fields", valid }));

        Assert.Null(exception);
        Assert.Equal("war-valid", Assert.Single(loaded!).Id);
    }

    private static void AssertWarEqual(PrivateWar expected, PrivateWar actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.AttackerPrincipalClanId, actual.AttackerPrincipalClanId);
        Assert.Equal(expected.DefenderPrincipalClanId, actual.DefenderPrincipalClanId);
        Assert.Equal(expected.AttackerSupporters, actual.AttackerSupporters);
        Assert.Equal(expected.DefenderSupporters, actual.DefenderSupporters);
        Assert.Equal(expected.CasusBelliType, actual.CasusBelliType);
        Assert.Equal(expected.TitleId, actual.TitleId);
        Assert.Equal(expected.MainGoalSettlementId, actual.MainGoalSettlementId);
        Assert.Equal(expected.OriginalFiefOwners, actual.OriginalFiefOwners);
        Assert.Equal(expected.BattleScore, actual.BattleScore);
        Assert.Equal(expected.Score, actual.Score);
        Assert.Equal(expected.StartDay, actual.StartDay);
        Assert.Equal(expected.GoalLastTakenDay, actual.GoalLastTakenDay);
        Assert.Equal(expected.Status, actual.Status);
    }
}
