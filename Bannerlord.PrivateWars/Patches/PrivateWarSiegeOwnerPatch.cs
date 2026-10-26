using System.Linq;
using System.Reflection;
using Bannerlord.PrivateWars.DI;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Patches
{
    // KingdomManager.SiegeCompleted hands every won assault to the capturer's kingdom leader. In a
    // private war that is the shared king, which would take the fief out of the war. Redirect a capture
    // of a war fief by the side that does not hold it to the owner PrivateWarCaptureOwnerPolicy names:
    // the capturer as interim owner, or the claimant when it marches in the capturing army. An eliminated
    // or leaderless owner clan falls back to the capturer rather than leave the fief without an owner hero.
    public class PrivateWarSiegeOwnerPatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.ApplyBySiege();

        public MethodInfo? PatchMethod => AccessTools.Method(typeof(PrivateWarSiegeOwnerPatch), nameof(Redirect));

        public PatchType PatchType => PatchType.Prefix;

        private static void Redirect(ref Hero newOwner, Hero capturerHero, Settlement settlement)
        {
            var wars = PrivateWarsServices.PrivateWars;
            var sides = PrivateWarsServices.Sides;
            var capturerClanId = capturerHero?.Clan?.StringId;
            var ownerClanId = settlement.OwnerClan?.StringId;
            if (wars is null || sides is null || capturerClanId is null || ownerClanId is null) return;

            foreach (var war in wars.GetAll())
            {
                if (war.Status != PrivateWarStatus.Active
                    || !war.IsWarFief(settlement.StringId)) continue;
                var besiegerSide = sides.ResolveSide(capturerClanId, war);
                var ownerSide = sides.ResolveSide(ownerClanId, war);
                if (besiegerSide is null || ownerSide is null || besiegerSide == ownerSide) continue;

                var army = capturerHero!.PartyBelongedTo?.Army;
                bool claimantInArmy = army is not null
                    && army.Parties.Any(party => party.ActualClan?.StringId == war.AttackerPrincipalClanId);
                var newOwnerClanId = PrivateWarCaptureOwnerPolicy.ChooseOwnerClanId(
                    war, settlement.StringId, besiegerSide.Value, capturerClanId, claimantInArmy);
                var newOwnerClan = Clan.All.FirstOrDefault(clan => clan.StringId == newOwnerClanId);
                newOwner = newOwnerClan is { IsEliminated: false, Leader: not null } && newOwnerClanId != capturerClanId
                    ? newOwnerClan.Leader
                    : capturerHero;
                return;
            }
        }
    }
}
