using System;
using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Resolves which side of a war a clan fights on by walking UP its suzerain chain to the
    /// first belligerent principal (nearest-belligerent-ancestor, design §18.A). A clan attacking
    /// its own liege therefore keeps its own subtree on its side. A clan listed as a supporter of
    /// either side is belligerent in its own right, so the walk stops there too — except at a foreign
    /// supporter, which only counts for itself, so its vassals walk on past it. Returns null for a
    /// clan whose chain reaches neither principal nor supporter (uninvolved).
    /// </summary>
    public class WarSideResolver
    {
        public WarSide? ResolveSide(string clanId, PrivateWar war, Func<string, string?> getSuzerain)
        {
            string? current = clanId;
            var visited = new HashSet<string>();

            while (current is not null && visited.Add(current))
            {
                bool supports = current == clanId || !war.ForeignSupporters.Contains(current);
                if (current == war.AttackerPrincipalClanId || supports && war.AttackerSupporters.Contains(current)) return WarSide.Attacker;
                if (current == war.DefenderPrincipalClanId || supports && war.DefenderSupporters.Contains(current)) return WarSide.Defender;
                current = getSuzerain(current);
            }

            return null;
        }
    }
}
