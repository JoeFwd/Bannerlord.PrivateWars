using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    public sealed class PrivateWarSyntheticCapturePolicy
    {
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
                || war.MainGoalSettlementId != settlementId
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
    }
}
