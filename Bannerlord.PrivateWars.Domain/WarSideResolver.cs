using System;
using System.Collections.Generic;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Resolves which side of a war a clan fights on by walking UP its suzerain chain to the
    /// first belligerent principal (nearest-belligerent-ancestor, design §18.A). A clan attacking
    /// its own liege therefore keeps its own subtree on its side. Returns null for a clan whose
    /// chain reaches neither principal (uninvolved).
    /// </summary>
    public class WarSideResolver
    {
        public WarSide? ResolveSide(string clanId, PrivateWar war, Func<string, string?> getSuzerain)
        {
            string? current = clanId;
            var visited = new HashSet<string>();

            while (current is not null && visited.Add(current))
            {
                if (current == war.AttackerPrincipalClanId) return WarSide.Attacker;
                if (current == war.DefenderPrincipalClanId) return WarSide.Defender;
                current = getSuzerain(current);
            }

            return null;
        }
    }
}
