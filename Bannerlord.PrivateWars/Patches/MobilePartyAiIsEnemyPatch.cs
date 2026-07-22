using System.Reflection;
using Bannerlord.PrivateWars.Patches;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PrivateWars.Patches
{
    // The central AI hostility signal. DefaultMobilePartyAIModel.IsEnemy is a private helper that
    // returns FactionManager.IsAtWarAgainstFaction(party, mobileParty) — always false for two clans
    // in the same kingdom (design §2). Flip it to true when the two parties' clans are at private
    // war, so the strategic AI treats the opponent as a target.
    public class MobilePartyAiIsEnemyPatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.MobilePartyAiIsEnemy();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(MobilePartyAiIsEnemyPatch), nameof(ForcePrivateWarEnemy));

        public PatchType PatchType => PatchType.Postfix;

        private static void ForcePrivateWarEnemy(ref bool __result, PartyBase party, MobileParty mobileParty)
        {
            if (__result) return;
            if (PrivateWarPatchHelper.AreEnemies(party?.MobileParty?.ActualClan, mobileParty?.ActualClan))
                __result = true;
        }
    }
}
