using Bannerlord.PrivateWars.Patches;
using Xunit;

namespace Bannerlord.PrivateWars.Tests;

// A target that fails to resolve throws while patching in OnSubModuleLoad, which exits the game silently.
public class WarArmyNoActiveWarDispersalPatchTargetTests
{
    [Fact]
    public void DisbandArmyInternal_Resolves()
        => Assert.NotNull(PrivateWarHarmonyPatchTargets.DisbandArmyInternal());
}
