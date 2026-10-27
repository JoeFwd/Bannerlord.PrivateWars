using Bannerlord.PrivateWars.Domain;
using Xunit;

namespace Bannerlord.PrivateWars.Tests;

public class PrivateWarSallyOutPolicyTests
{
    private readonly PrivateWarSallyOutPolicy _policy = new();

    [Fact]
    public void StrongGarrison_Sallies()
        => Assert.True(_policy.ShouldSally(5474f, 1158f, reliefBattleActive: false, sallyMorale: 40f));

    // Observed live: 3152 men over the castle's size limit sat at 0 morale and were routed on round one.
    [Fact]
    public void ZeroMoraleGarrison_StaysInside()
        => Assert.False(_policy.ShouldSally(5474f, 1158f, reliefBattleActive: false, sallyMorale: 0f));
}
