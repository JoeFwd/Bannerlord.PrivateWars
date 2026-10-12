using System.Reflection;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Bannerlord.PrivateWars.Patches
{
    // Reports a kingdom at war with itself, but only inside the PrivateWarBattleScope opened by
    // MapEventUpdateEnterPrivateWarScopePatch for a same-kingdom private-war battle. Outside that scope,
    // or for any other pair of factions, vanilla answers.
    public class KingdomIsAtWarWithPrivateWarPatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.KingdomIsAtWarWith();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(KingdomIsAtWarWithPrivateWarPatch), nameof(AtWarWithinPrivateWarBattle));

        public PatchType PatchType => PatchType.Prefix;

        private static bool AtWarWithinPrivateWarBattle(Kingdom __instance, IFaction other, ref bool __result)
        {
            var scope = PrivateWarBattleScope.Faction;
            if (scope is null || scope != __instance || other != __instance) return true;

            __result = true;
            return false;
        }
    }
}
