using System.Linq;
using System.Reflection;
using Bannerlord.PrivateWars.Patches;
using Bannerlord.PrivateWars.DI;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PrivateWars.Patches
{
    // Un-merge two same-kingdom parties into a battle. When two AI parties meet, the AI-vs-AI branch
    // of EncounterManager.StartPartyEncounter merges them (attackerParty.MapEventSide =
    // defenderParty.MapEventSide) whenever they share a MapFaction (design §2) instead of starting a
    // battle. StartBattleAction.Apply itself does not gate on MapFaction, so for a private-war pair we
    // route there directly and skip the vanilla merge.
    //
    // Scope is deliberately narrow: only a clean AI-vs-AI, same-MapFaction, private-war pair with no
    // player involvement is intercepted. Everything else (player encounters, cross-faction pairs that
    // already battle, parties the player's map event involves) falls through to vanilla untouched.
    public class StartPartyEncounterBattlePatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.StartPartyEncounter();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(StartPartyEncounterBattlePatch), nameof(RouteSameKingdomToBattle));

        public PatchType PatchType => PatchType.Prefix;

        private static bool RouteSameKingdomToBattle(PartyBase attackerParty, PartyBase defenderParty)
        {
            if (!PrivateWarsServices.IsInitialised || PrivateWarsServices.PrivateWarHostility is null) return true;
            if (attackerParty == PartyBase.MainParty || defenderParty == PartyBase.MainParty) return true;
            if (!attackerParty.IsActive || !defenderParty.IsActive) return true;

            // Cross-faction pairs already battle in vanilla; only same-MapFaction needs the fix.
            if (attackerParty.MapFaction != defenderParty.MapFaction) return true;

            // Leave anything the player's own battle touches to vanilla's player-encounter handling.
            var playerEvent = PartyBase.MainParty.MapEvent;
            if (playerEvent != null &&
                (playerEvent.InvolvedParties.Contains(attackerParty) || playerEvent.InvolvedParties.Contains(defenderParty)))
                return true;

            if (!PrivateWarPatchHelper.AreEnemies(attackerParty.MobileParty?.ActualClan, defenderParty.MobileParty?.ActualClan))
                return true;

            StartBattleAction.Apply(attackerParty, defenderParty);
            return false;
        }
    }
}
