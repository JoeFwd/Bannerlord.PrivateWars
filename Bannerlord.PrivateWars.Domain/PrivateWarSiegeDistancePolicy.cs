using System;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Travel-distance factor for a private-war siege bid, so a side prefers the enemy fiefs nearest
    /// to it. Vanilla scores a besiege target by frontier adjacency against a rival realm, which is
    /// always zero inside one kingdom; this follows its raid scoring instead, falling off linearly
    /// over five average town-to-town distances. Never zero, so a war whose only targets are far
    /// away still has a (weak) bid rather than stalling.
    /// </summary>
    public class PrivateWarSiegeDistancePolicy
    {
        private const float MaximumDistanceInTownSpacings = 5f;
        private const float MinimumFactor = 0.1f;

        public float GetScoreFactor(float distance, float averageTownDistance)
            => Math.Max(MinimumFactor, 1f - distance / (averageTownDistance * MaximumDistanceInTownSpacings));
    }
}
