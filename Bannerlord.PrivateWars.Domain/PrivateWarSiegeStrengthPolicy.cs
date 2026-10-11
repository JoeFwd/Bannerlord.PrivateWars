using System;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Strength gate for a private-war siege bid, shaped like
    /// DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction but with lower ratios: a party only
    /// bids to besiege when it at least matches the walled defence, or 0.75x once it is already holding
    /// the siege. Vanilla's 2x/1.5x suits kingdom armies of 1000+; a lord's subtree rarely fields more
    /// than a few hundred, so at vanilla ratios a private goal could never be taken. Above the gate the
    /// bid scales with the strength ratio, reaching full weight at twice the required strength.
    /// </summary>
    public class PrivateWarSiegeStrengthPolicy
    {
        private const float StartSiegeRatio = 1f;
        private const float HoldSiegeRatio = 0.75f;
        private const float WallLevelDefenceBonus = 0.33f;

        /// <returns>0 when the bid must not be made, otherwise a weight in [0.5, 1].</returns>
        public float GetScoreFactor(float ourStrength, float defenceStrength, int wallLevel, bool isHoldingSiege)
        {
            float walledDefence = Math.Max(defenceStrength, 1f) * (1f + WallLevelDefenceBonus * wallLevel);
            float required = walledDefence * (isHoldingSiege ? HoldSiegeRatio : StartSiegeRatio);
            if (ourStrength < required) return 0f;

            return Math.Min(1f, ourStrength / (2f * required));
        }
    }
}
