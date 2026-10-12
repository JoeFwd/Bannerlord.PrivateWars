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
    // While Update runs for a battle whose two leaders are mobile parties of private-war enemy clans in
    // one kingdom, open a scope that KingdomIsAtWarWithPrivateWarPatch reads to report that kingdom at war
    // with itself. Fortification battles (leader is a settlement party) are deliberately left out: the
    // same-kingdom siege flow captures synthetically (PrivateWarCampaignBehavior.TryCaptureCompletedSieges)
    // and relies on no assault MapEvent ever resolving.
    internal static class PrivateWarBattleScope
    {
        public static IFaction? Faction;

        public static IFaction? Resolve(MapEvent mapEvent)
        {
            var attacker = mapEvent.AttackerSide?.LeaderParty;
            var defender = mapEvent.DefenderSide?.LeaderParty;
            if (attacker?.MobileParty is null || defender?.MobileParty is null) return null;
            if (attacker.MapFaction is null || attacker.MapFaction != defender.MapFaction) return null;

            return PrivateWarPatchHelper.AreEnemies(attacker.MobileParty.ActualClan, defender.MobileParty.ActualClan)
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
}
