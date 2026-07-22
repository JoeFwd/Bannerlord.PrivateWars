using System.Reflection;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Patches;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Patches
{
    // Show the "Take a hostile action" option on the village menu for same-kingdom private-war rivals.
    //
    // VillageHostileActionCampaignBehavior.game_menu_village_hostile_action_on_condition gates the
    // "village" menu "hostile_action" option. The method returns false when MapFaction is shared
    // (same kingdom), so the "Take a hostile action" button never appears. This postfix re-enables
    // it when the village's bound-town owner is a registered private-war enemy.
    public class VillageHostileActionConditionPatch : IPatch
    {
        private static readonly PrivateWarVillageActionPolicy Policy = new();

        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.VillageHostileActionCondition();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(VillageHostileActionConditionPatch), nameof(Postfix));

        public PatchType PatchType => PatchType.Postfix;

        private static void Postfix(ref bool __result, MenuCallbackArgs args)
        {
            if (__result) return;
            var cs = Settlement.CurrentSettlement;
            var village = cs?.Village;
            // village.Owner is the bound Town; its OwnerClan is the relevant clan for the war check.
            var ownerClan = village?.Owner?.Settlement?.OwnerClan;
            bool canLeadArmyAction = MobileParty.MainParty.Army is null
                                     || MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty;
            var decision = Policy.EvaluateHostileAction(
                __result,
                args.IsEnabled,
                cs?.IsVillage == true,
                village?.VillageState == Village.VillageStates.Normal,
                canLeadArmyAction,
                PrivateWarPatchHelper.AreEnemies(Hero.MainHero?.Clan, ownerClan));
            args.IsEnabled = decision.IsEnabled;
            if (!decision.IsVisible) return;

            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            __result = true;
        }
    }
}
