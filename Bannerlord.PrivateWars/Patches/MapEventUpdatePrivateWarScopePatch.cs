using System.Reflection;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace Bannerlord.PrivateWars.Patches
{
    // MapEvent.Update opens with "if the two leader parties' MapFactions are not at war, the event is
    // DiplomaticallyFinished". Two clans of one kingdom share a MapFaction, so every same-kingdom
    // private-war battle StartPartyEncounterBattlePatch starts is finished on its first tick with no
    // winner; the AI then re-engages on the next tick, forever, and the battle never scores.
    //
    // While Update runs for a battle whose two leaders belong to private-war enemy clans in one kingdom,
    // open a scope that KingdomIsAtWarWithPrivateWarPatch reads to report that kingdom at war with itself.
    // A fortification battle (wall assault, sally-out) has a settlement party as one leader; its clan is
    // the settlement's owner.
    internal static class PrivateWarBattleScope
    {
        public static IFaction? Faction;

        public static IFaction? Resolve(MapEvent mapEvent)
        {
            var attacker = mapEvent.AttackerSide?.LeaderParty;
            var defender = mapEvent.DefenderSide?.LeaderParty;
            if (attacker is null || defender is null) return null;
            if (attacker.MapFaction is null || attacker.MapFaction != defender.MapFaction) return null;

            return PrivateWarPatchHelper.AreEnemies(
                    PrivateWarPatchHelper.ClanOf(attacker), PrivateWarPatchHelper.ClanOf(defender))
                ? attacker.MapFaction
                : null;
        }
    }

    public class MapEventUpdateEnterPrivateWarScopePatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.MapEventUpdate();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(MapEventUpdateEnterPrivateWarScopePatch), nameof(EnterScope));

        public PatchType PatchType => PatchType.Prefix;

        private static void EnterScope(MapEvent __instance)
            => PrivateWarBattleScope.Faction = PrivateWarBattleScope.Resolve(__instance);
    }

    public class MapEventUpdateExitPrivateWarScopePatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.MapEventUpdate();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(MapEventUpdateExitPrivateWarScopePatch), nameof(ExitScope));

        public PatchType PatchType => PatchType.Postfix;

        private static void ExitScope() => PrivateWarBattleScope.Faction = null;
    }

    // Update finishes a won battle inline: FinishBattle -> FinalizeEventAux -> SiegeCompleted -> the capture
    // and every OnSettlementOwnerChanged handler. Inside the scope those see the kingdom at war with itself,
    // and PartyDiplomaticHandlerCampaignBehavior evicts the capturer's fresh garrison from its own castle
    // (a garrison with no CurrentSettlement then NREs in IsGarrisonStarving). Close the scope there.
    public class MapEventFinishBattleExitPrivateWarScopePatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.MapEventFinishBattle();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(MapEventFinishBattleExitPrivateWarScopePatch), nameof(ExitScope));

        public PatchType PatchType => PatchType.Prefix;

        private static void ExitScope() => PrivateWarBattleScope.Faction = null;
    }
}
