using System.Reflection;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Interactions;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Patches
{
    public class SettlementVisitPatch : IPatch
    {
        private static PrivateWarInteractionPolicy _interactionPolicy = null!;

        public SettlementVisitPatch(PrivateWarInteractionPolicy interactionPolicy)
        {
            _interactionPolicy = interactionPolicy;
        }

        public MethodInfo TargetMethod => SettlementVisitPatchTarget.Resolve();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(SettlementVisitPatch), nameof(Postfix));

        public PatchType PatchType => PatchType.Postfix;

        private static void Postfix(
            MobileParty mobileParty,
            Settlement settlement,
            ref bool __result)
        {
            var arePrivateEnemies = PrivateWarPatchHelper.AreEnemies(
                mobileParty?.ActualClan,
                settlement?.OwnerClan);
            __result = _interactionPolicy.AllowSettlementVisit(__result, arePrivateEnemies);
        }
    }
}
