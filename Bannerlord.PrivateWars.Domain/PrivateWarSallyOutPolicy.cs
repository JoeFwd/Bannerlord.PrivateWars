namespace Bannerlord.PrivateWars.Domain
{
    public enum SallyOutPartySide
    {
        None,
        Besieger,
        Settlement
    }

    public sealed class PrivateWarSallyOutPolicy
    {
        public SallyOutPartySide ClassifyNearbyParty(
            bool vanillaEnemy,
            bool privateEnemy,
            bool samePrivateWarSide,
            bool isPrivateWarSiege,
            bool sameMapFaction,
            bool matchesNavalContext)
        {
            if (isPrivateWarSiege)
            {
                if (vanillaEnemy || privateEnemy) return SallyOutPartySide.Besieger;
                return samePrivateWarSide && matchesNavalContext
                    ? SallyOutPartySide.Settlement
                    : SallyOutPartySide.None;
            }

            if (vanillaEnemy) return SallyOutPartySide.Besieger;
            return sameMapFaction && matchesNavalContext
                ? SallyOutPartySide.Settlement
                : SallyOutPartySide.None;
        }

        // MapEvent.CalculateWinner routs a side whose mobile leader has ~0 morale before a blow is struck,
        // and a lost sally hands the besieger the fortification. A garrison far over its size limit
        // sits at 0 morale however strong it is, so it must stay behind its walls.
        public bool ShouldSally(
            float settlementStrength,
            float besiegerStrength,
            bool reliefBattleActive,
            float sallyMorale)
            => sallyMorale >= 1f
               && settlementStrength > besiegerStrength * (reliefBattleActive ? 1.5f : 2f);
    }
}
