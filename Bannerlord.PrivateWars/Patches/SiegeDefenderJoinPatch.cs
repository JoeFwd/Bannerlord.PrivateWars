using System.Reflection;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Bannerlord.PrivateWars.Patches
{
    // Fix the defender side being empty in a same-kingdom private-war siege assault.
    //
    // SiegeEvent.CanPartyJoinSide checks whether the party's MapFaction is at war with every
    // party on the opposite side:
    //   flag = GetSiegeEventSide(attackerSide).All(x => x.MapFaction.IsAtWarWith(party.MapFaction))
    // In a same-kingdom private war the attacker shares the defender's MapFaction, so
    // IsAtWarWith is always false → flag is false → the inside-settlement parties are never
    // admitted to the Defender MapEventSide → the assault starts with 0 defenders on the wall.
    // This postfix admits two kinds of defender, even though they share a MapFaction:
    //   - the settlement's own garrison/militia, when the besieger is a private-war enemy of
    //     the settlement owner; and
    //   - a lord party inside the town that is itself a private-war enemy of the besieger
    //     (a belligerent on the owner's side of the feud). Neutral same-kingdom parties are
    //     left to vanilla, which ejects them from the siege.
    public class SiegeDefenderJoinPatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.SiegeDefenderJoin();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(SiegeDefenderJoinPatch), nameof(Postfix));

        public PatchType PatchType => PatchType.Postfix;

        private static void Postfix(ref bool __result, SiegeEvent __instance, PartyBase party, BattleSideEnum side)
        {
            if (__result) return;                        // vanilla already admitted the party
            if (side != BattleSideEnum.Defender) return; // only fix the empty-defender gap

            var besiegerClan = __instance.BesiegerCamp?.LeaderParty?.ActualClan;

            // The shared private-war defender rule: the owner's garrison/militia, or a feud-belligerent
            // lord party inside the town. The same policy backs DadgEncounterModel's candidacy seam, so
            // the assault side membership and the siege overlay never disagree.
            // CanPartyJoinSide gates the wall assault (a Siege battle), where militia DO defend — pass
            // Siege so the SallyOut-only militia exclusion in IsDefender does not apply here.
            if (PrivateWarSiegeDefenderPolicy.IsDefender(party.MobileParty, besiegerClan,
                    __instance.BesiegedSettlement, MapEvent.BattleTypes.Siege))
                __result = true;
        }
    }
}
