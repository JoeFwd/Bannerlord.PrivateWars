using System.Reflection;
using Bannerlord.PrivateWars.Patches;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PrivateWars.Patches
{
    // Twin of the IsEnemy patch. DefaultMobilePartyAIModel.CalculateStanceScore is a private helper
    // that returns +1 for a faction-war pair, -1 for a same-faction (ally) pair, 0 otherwise. A
    // same-kingdom private-war pair would score -1 (treated as an ally to escort/avoid). Force it to
    // the enemy score (+1) so the engage/strength-accumulation maths sees the opponent as hostile.
    public class MobilePartyAiStanceScorePatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.MobilePartyAiStanceScore();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(MobilePartyAiStanceScorePatch), nameof(ForcePrivateWarStance));

        public PatchType PatchType => PatchType.Postfix;

        private static void ForcePrivateWarStance(ref float __result, MobileParty mobileParty, MobileParty otherParty)
        {
            if (PrivateWarPatchHelper.AreEnemies(mobileParty?.ActualClan, otherParty?.ActualClan))
                __result = 1f;
        }
    }
}
