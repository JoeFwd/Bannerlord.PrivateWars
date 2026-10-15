using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Whether a side should march on the war's goal. It never besieges a goal its own side holds, and
    /// always one the opposing side holds. A goal held by a clan on neither side is besieged only when
    /// the party's realm is at war with the holder's: a foreign enemy who took it is fair game, an
    /// uninvolved clan of the same realm is not.
    /// </summary>
    public static class PrivateWarGoalPursuitPolicy
    {
        public static bool ShouldBesiegeGoal(WarSide mySide, WarSide? ownerSide, bool atWarWithOwner)
            => ownerSide is null ? atWarWithOwner : ownerSide != mySide;
    }
}
