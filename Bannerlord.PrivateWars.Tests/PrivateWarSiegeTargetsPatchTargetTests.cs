using Bannerlord.PrivateWars.Patches;
using Xunit;

namespace Bannerlord.PrivateWars.Tests;

// A target that fails to resolve throws while patching in OnSubModuleLoad, which exits the game silently.
public class PrivateWarSiegeTargetsPatchTargetTests
{
    [Fact]
    public void FindBestTargetAndItsValueForFaction_Resolves()
        => Assert.NotNull(PrivateWarHarmonyPatchTargets.FindBestTargetAndItsValueForFaction());

    [Fact]
    public void CalculateDistanceScoreForBesieging_Resolves()
        => Assert.NotNull(PrivateWarHarmonyPatchTargets.CalculateDistanceScoreForBesieging());

    // Binds the four private AiMilitaryBehavior methods the prefix calls; a signature drift throws here.
    [Fact]
    public void ScoringDelegates_Bind()
        => Assert.NotNull(new PrivateWarSiegeTargetsPatch(null!).TargetMethod);
}
