using System.Linq;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.Util;
using TaleWorlds.CampaignSystem;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// Concludes the private wars a kingdom change has split across two kingdoms.
    /// </summary>
    public class KingdomSplitWarAdapter
    {
        private readonly IPrivateWarRepository _privateWars;
        private readonly WarPrisonerReleaseAdapter _prisonerRelease;

        public KingdomSplitWarAdapter(IPrivateWarRepository privateWars, WarPrisonerReleaseAdapter prisonerRelease)
        {
            _privateWars = privateWars;
            _prisonerRelease = prisonerRelease;
        }

        // A private war is an intra-kingdom feud: both principals share a MapFaction, and every
        // mechanism (the AreEnemies hostility signal, the same-kingdom siege drive, prisoner retention,
        // status-quo resolution) assumes it. When a principal defects so the two principals no longer
        // share a kingdom, that premise is void and a stale Active record would keep forcing
        // cross-kingdom hostility; conclude it. Subtree vassals need no handling - WarSideResolver
        // re-resolves their side from the current suzerain chain on every query.
        public void ConcludeWarsSplitBy(Clan clan)
        {
            foreach (var war in _privateWars.GetByClan(clan.StringId))
            {
                if (war.Status != PrivateWarStatus.Active) continue;

                var attacker = Clan.All.FirstOrDefault(c => c.StringId == war.AttackerPrincipalClanId);
                var defender = Clan.All.FirstOrDefault(c => c.StringId == war.DefenderPrincipalClanId);
                if (attacker != null && defender != null && attacker.MapFaction == defender.MapFaction)
                    continue;

                _privateWars.Update(war with { Status = PrivateWarStatus.Concluded });
                _prisonerRelease.Release(war);
                InfoPrinter.Display(
                    $"Private war concluded: {war.AttackerPrincipalClanId} vs {war.DefenderPrincipalClanId} - " +
                    "principals no longer share a kingdom.");
            }
        }
    }
}
