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

        public bool ShouldSally(
            float settlementStrength,
            float besiegerStrength,
            bool reliefBattleActive)
            => settlementStrength > besiegerStrength * (reliefBattleActive ? 1.5f : 2f);
    }
}
