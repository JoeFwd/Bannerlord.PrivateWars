using System.Reflection;
using Bannerlord.PrivateWars.Armies;
using Bannerlord.PrivateWars.Campaign;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Bannerlord.PrivateWars.Patches
{
    public class KingdomCreateArmyPatch : IPatch
    {
        private static PrivateWarCampaignBehavior _campaignBehavior = null!;

        public KingdomCreateArmyPatch(PrivateWarCampaignBehavior campaignBehavior)
        {
            _campaignBehavior = campaignBehavior;
        }

        public MethodInfo TargetMethod => KingdomCreateArmyPatchTarget.Resolve();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(KingdomCreateArmyPatch), nameof(Prefix));

        public PatchType PatchType => PatchType.Prefix;

        private static bool Prefix(
            Hero armyLeader,
            Settlement targetSettlement,
            Army.ArmyTypes selectedArmyType,
            ref MBReadOnlyList<MobileParty> partiesToCallToArmy)
        {
            var action = _campaignBehavior.PreparePrivateWarArmyCreation(
                armyLeader,
                targetSettlement,
                selectedArmyType,
                out var replacementMembers);

            if (action == PrivateWarArmyCreationAction.ReplaceMembers)
                partiesToCallToArmy = replacementMembers!;

            return action != PrivateWarArmyCreationAction.Suppress;
        }
    }
}
