using System.Reflection;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Interactions;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Patches
{
    public class RecruitmentEntryPatch : IPatch
    {
        private static PrivateWarInteractionPolicy _interactionPolicy = null!;

        public RecruitmentEntryPatch(PrivateWarInteractionPolicy interactionPolicy)
        {
            _interactionPolicy = interactionPolicy;
        }

        public MethodInfo TargetMethod => RecruitmentEntryPatchTarget.Resolve();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(RecruitmentEntryPatch), nameof(Prefix));

        public PatchType PatchType => PatchType.Prefix;

        private static bool Prefix(MobileParty mobileParty, Settlement settlement, Hero hero)
        {
            var arePrivateEnemies = PrivateWarPatchHelper.AreEnemies(
                mobileParty?.ActualClan,
                settlement?.OwnerClan);
            return _interactionPolicy.ShouldRunRecruitmentEntry(arePrivateEnemies);
        }
    }
}
