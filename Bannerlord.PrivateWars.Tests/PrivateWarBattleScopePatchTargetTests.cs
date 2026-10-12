using Bannerlord.PrivateWars.Patches;
using Xunit;

namespace Bannerlord.PrivateWars.Tests;

// A target that fails to resolve throws while patching in OnSubModuleLoad, which exits the game silently.
public class PrivateWarBattleScopePatchTargetTests
{
    [Fact]
    public void MapEventUpdate_Resolves()
        => Assert.NotNull(PrivateWarHarmonyPatchTargets.MapEventUpdate());

    [Fact]
    public void KingdomIsAtWarWith_Resolves()
        => Assert.NotNull(PrivateWarHarmonyPatchTargets.KingdomIsAtWarWith());
}
