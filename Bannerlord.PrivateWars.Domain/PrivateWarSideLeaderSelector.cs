using System;
using System.Collections.Generic;
using System.Linq;

namespace Bannerlord.PrivateWars.Domain
{
    public record PrivateWarLeaderCandidate(
        string PartyId,
        string ClanId,
        int ClanTier,
        float ClanRenown,
        bool IsLeadingWarArmy,
        bool IsBesiegingWarFief = false);

    /// <summary>
    /// Picks the one party per war side that prosecutes the goal; every other party of the side
    /// joins its army instead of besieging alone. The party already besieging the goal keeps the
    /// lead, then an army already marching on it; otherwise the principal leads when available, else the most senior vassal (clan tier,
    /// then renown).
    /// </summary>
    public class PrivateWarSideLeaderSelector
    {
        public string? Select(IEnumerable<PrivateWarLeaderCandidate> candidates, string principalClanId)
            => candidates
                .OrderByDescending(c => c.IsBesiegingWarFief)
                .ThenByDescending(c => c.IsLeadingWarArmy)
                .ThenByDescending(c => c.ClanId == principalClanId)
                .ThenByDescending(c => c.ClanTier)
                .ThenByDescending(c => c.ClanRenown)
                .ThenBy(c => c.PartyId, StringComparer.Ordinal)
                .FirstOrDefault()
                ?.PartyId;
    }
}
