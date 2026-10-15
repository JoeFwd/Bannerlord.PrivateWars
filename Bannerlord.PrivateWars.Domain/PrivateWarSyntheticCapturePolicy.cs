using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    public sealed class PrivateWarSyntheticCapturePolicy
    {
        // Any fief the war is fought over - the goal, or one either side held at declaration - falls
        // to an opposing besieger once its preparations complete.
        public WarSide? GetLosingSide(
            PrivateWar war,
            string settlementId,
            bool preparationComplete,
            bool isPlayerLed,
            bool hasUsableSiegeLeader,
            WarSide? besiegerSide,
            WarSide? ownerSide)
        {
            if (war.Status != PrivateWarStatus.Active
                || !IsWarFief(war, settlementId)
                || !preparationComplete
                || isPlayerLed
                || !hasUsableSiegeLeader
                || besiegerSide is null
                || ownerSide is null
                || besiegerSide == ownerSide)
            {
                return null;
            }

            return ownerSide;
        }

        public static bool IsWarFief(PrivateWar war, string settlementId)
            => war.MainGoalSettlementId == settlementId || war.OriginalFiefOwners.ContainsKey(settlementId);
    }
}
