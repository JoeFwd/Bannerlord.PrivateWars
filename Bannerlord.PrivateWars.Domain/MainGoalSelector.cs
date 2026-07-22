using System;
using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Picks the war's main goal — the settlement whose control drives fatigue — and freezes it at
    /// declaration (design §18.C): the highest-prosperity de jure settlement of the claimed title
    /// that the defendant currently holds. Returns null when the defendant holds none of the
    /// claimed title's settlements; with no main goal the casus belli cannot be pressed (§18.D).
    /// </summary>
    public class MainGoalSelector
    {
        public string? Select(
            IEnumerable<SettlementInfo> titleDeJureSettlements,
            string defenderClanId)
        {
            var heldByDefender = titleDeJureSettlements
                .Where(s => s.OwnerClanId == defenderClanId)
                .OrderByDescending(s => s.Prosperity)
                .ThenBy(s => s.Id, StringComparer.Ordinal)
                .ToList();

            return heldByDefender.Count > 0 ? heldByDefender[0].Id : null;
        }
    }
}
