using System.Linq;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// Frees the captives a concluded private war put behind bars.
    /// </summary>
    public class WarPrisonerReleaseAdapter
    {
        private readonly IPrivateWarSidesProvider _sides;

        public WarPrisonerReleaseAdapter(IPrivateWarSidesProvider sides)
        {
            _sides = sides;
        }

        // Release every captive this war put behind bars now that it has concluded. During the war
        // PrivateWarPrisonerRetentionPatch deliberately blocks the engine's involuntary auto-release of
        // a same-kingdom captive (peace sweeps, kingdom changes, our own siege capture) so the prize of
        // capturing the goal sticks; escape attempts are never blocked. Once the war ends that retention
        // no longer makes sense - the principals are at peace - so we deliberately free anyone held by the
        // opposing side. Must run AFTER the status flips to Concluded: AreEnemies is Active-gated, so a
        // concluded war no longer reports the pair as enemies and the retention patch lets the release
        // through. ApplyByPeace routes through the unblocked deliberate-release path.
        public void Release(PrivateWar war)
        {
            foreach (var hero in Hero.AllAliveHeroes.Where(h => h.IsPrisoner).ToList())
            {
                if (hero == Hero.MainHero || hero.Clan is null) continue;

                var captorClan = PartyClan.OwnerClanOf(hero.PartyBelongedToAsPrisoner);
                if (captorClan is null) continue;

                var prisonerSide = _sides.ResolveSide(hero.Clan.StringId, war);
                var captorSide = _sides.ResolveSide(captorClan.StringId, war);
                if (prisonerSide is null || captorSide is null || prisonerSide == captorSide) continue;

                EndCaptivityAction.ApplyByPeace(hero);
            }
        }
    }
}
