using System.Reflection;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Patches;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Patches
{
    // Show the "Raid the village" button inside the village_hostile_action submenu for private-war rivals.
    //
    // VillageHostileActionCampaignBehavior.game_menu_village_hostile_action_raid_village_on_condition returns
    // !DiplomacyHelper.IsSameFactionAndNotEliminated(Hero.MainHero.MapFaction, Settlement.CurrentSettlement.MapFaction).
    // Two clans in the same kingdom share a MapFaction, so IsSameFactionAndNotEliminated is true → the
    // "Raid the village" button is hidden. This postfix re-enables it when the settlement's owner is a
    // registered private-war enemy.
    public class VillageRaidConditionPatch : IPatch
    {
        private static readonly PrivateWarVillageActionPolicy Policy = new();

        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.VillageRaidCondition();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(VillageRaidConditionPatch), nameof(Postfix));

        public PatchType PatchType => PatchType.Postfix;

        private static void Postfix(ref bool __result, MenuCallbackArgs args)
        {
            if (__result) return;
            var cs = Settlement.CurrentSettlement;
            var ownerClan = cs?.Village?.Owner?.Settlement?.OwnerClan;
            var decision = Policy.EvaluateRaid(
                __result,
                args.IsEnabled,
                cs?.IsVillage == true,
                PrivateWarPatchHelper.AreEnemies(Hero.MainHero?.Clan, ownerClan));
            args.IsEnabled = decision.IsEnabled;
            if (!decision.IsVisible) return;

            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            __result = true;
        }
    }
}
