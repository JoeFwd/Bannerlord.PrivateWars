using System.Reflection;
using Bannerlord.PrivateWars.Campaign;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Bannerlord.PrivateWars.Patches
{
    // Army.CheckArmyDispersion disbands an army (25% per hour) when its kingdom has no war against a
    // fief-holding faction. A private war is not a kingdom war, so a war army raised while its realm was
    // at peace was dispersed within the hour, re-raised, dispersed again - each raise spending the
    // leader's influence (observed: York's army against St Leger churned 7 times in 4 days, leaving its
    // lords too broke to call any army once the realm's war resumed). Keep a war army of an active war.
    public class WarArmyNoActiveWarDispersalPatch : IPatch
    {
        private static PrivateWarCampaignBehavior _campaignBehavior = null!;

        public WarArmyNoActiveWarDispersalPatch(PrivateWarCampaignBehavior campaignBehavior)
        {
            _campaignBehavior = campaignBehavior;
        }

        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.DisbandArmyInternal();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(WarArmyNoActiveWarDispersalPatch), nameof(Prefix));

        public PatchType PatchType => PatchType.Prefix;

        private static bool Prefix(Army army, Army.ArmyDispersionReason reason) =>
            reason != Army.ArmyDispersionReason.NoActiveWar || !_campaignBehavior.IsActiveWarArmy(army);
    }
}
