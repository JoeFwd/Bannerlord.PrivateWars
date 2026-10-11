using Bannerlord.PrivateWars.Domain;
using Xunit;

namespace Bannerlord.PrivateWars.Tests;

public class PrivateWarSiegeProsecutionTests
{
    private readonly PrivateWarSiegeStrengthPolicy _strength = new();
    private readonly PrivateWarSideLeaderSelector _selector = new();

    [Fact]
    public void StrengthGate_RejectsOutnumberedBesieger()
        => Assert.Equal(0f, _strength.GetScoreFactor(150f, 300f, wallLevel: 0, isHoldingSiege: false));

    [Fact]
    public void StrengthGate_WallsRaiseTheBar()
    {
        Assert.True(_strength.GetScoreFactor(450f, 300f, wallLevel: 0, isHoldingSiege: false) > 0f);
        Assert.Equal(0f, _strength.GetScoreFactor(450f, 300f, wallLevel: 2, isHoldingSiege: false));
    }

    [Fact]
    public void StrengthGate_HoldingSiegeUsesLowerRatio()
    {
        Assert.Equal(0f, _strength.GetScoreFactor(250f, 300f, wallLevel: 0, isHoldingSiege: false));
        Assert.True(_strength.GetScoreFactor(250f, 300f, wallLevel: 0, isHoldingSiege: true) > 0f);
    }

    [Fact]
    public void StrengthGate_ScalesFromHalfToFull()
    {
        Assert.Equal(0.5f, _strength.GetScoreFactor(300f, 300f, wallLevel: 0, isHoldingSiege: false));
        Assert.Equal(1f, _strength.GetScoreFactor(5000f, 300f, wallLevel: 0, isHoldingSiege: false));
    }

    [Fact]
    public void Selector_PrefersPrincipalWhenAvailable()
    {
        var leader = _selector.Select(new[]
        {
            new PrivateWarLeaderCandidate("vassal", "V", 6, 5000f, false),
            new PrivateWarLeaderCandidate("principal", "P", 3, 100f, false)
        }, "P");

        Assert.Equal("principal", leader);
    }

    [Fact]
    public void Selector_FallsBackToMostSeniorVassal()
    {
        var leader = _selector.Select(new[]
        {
            new PrivateWarLeaderCandidate("junior", "J", 2, 900f, false),
            new PrivateWarLeaderCandidate("senior", "S", 4, 100f, false),
            new PrivateWarLeaderCandidate("senior_less_renown", "S2", 4, 50f, false)
        }, "P");

        Assert.Equal("senior", leader);
    }

    [Fact]
    public void Selector_KeepsExistingGoalArmyLeader()
    {
        var leader = _selector.Select(new[]
        {
            new PrivateWarLeaderCandidate("principal", "P", 6, 5000f, false),
            new PrivateWarLeaderCandidate("vassal_army", "V", 1, 0f, true)
        }, "P");

        Assert.Equal("vassal_army", leader);
    }

    [Fact]
    public void Selector_ActualBesiegerOutranksGoalArmyAndPrincipal()
    {
        var leader = _selector.Select(new[]
        {
            new PrivateWarLeaderCandidate("principal", "P", 6, 5000f, false),
            new PrivateWarLeaderCandidate("vassal_army", "V", 4, 900f, true),
            new PrivateWarLeaderCandidate("besieger", "B", 1, 0f, false, true)
        }, "P");

        Assert.Equal("besieger", leader);
    }

    [Fact]
    public void Selector_NoCandidates_ReturnsNull()
        => Assert.Null(_selector.Select(System.Array.Empty<PrivateWarLeaderCandidate>(), "P"));
}
