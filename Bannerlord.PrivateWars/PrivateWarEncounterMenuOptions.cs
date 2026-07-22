using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Patches;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using CampaignState = TaleWorlds.CampaignSystem.Campaign;

namespace Bannerlord.PrivateWars
{
    internal static class PrivateWarEncounterMenuOptions
    {
        private static readonly PrivateWarInteractionPolicy InteractionPolicy = new();

        public static void Register(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town_outside", "dadg_private_war_besiege", "{=WdIGdHuL}Besiege the town.", BesiegeCondition, BesiegeConsequence);
            starter.AddGameMenuOption("castle_outside", "dadg_private_war_besiege", "{=UzMYZgoE}Besiege the castle.", BesiegeCondition, BesiegeConsequence);
            starter.AddGameMenuOption("encounter", "dadg_private_war_continue_preparations", "{=FOoMM4AU}Continue siege preparations.", ContinueSiegeCondition, ContinueSiegeConsequence);
            starter.AddGameMenuOption("army_encounter", "dadg_private_war_attack_army", "{=0URijoc0}Attack army", ArmyAttackCondition, ArmyAttackConsequence);
        }

        private static bool BesiegeCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.BesiegeTown;
            var currentSettlement = Settlement.CurrentSettlement;
            if (currentSettlement is null) return false;

            CheckFortificationAttackableHonorably(args, currentSettlement);
            return InteractionPolicy.CanShowPrivateWarBesiegeOption(
                PrivateWarPatchHelper.AreEnemies(Hero.MainHero.Clan, currentSettlement.OwnerClan),
                PartyBase.MainParty.NumberOfHealthyMembers > 0,
                currentSettlement.IsUnderSiege);
        }

        private static void CheckFortificationAttackableHonorably(
            MenuCallbackArgs args,
            Settlement settlement)
        {
            if ((MobileParty.MainParty.Army is null ||
                 MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty) &&
                settlement.MapFaction?.NotAttackableByPlayerUntilTime.IsFuture == true)
            {
                args.IsEnabled = false;
                args.Tooltip = GameTexts.FindText("str_enemy_not_attackable_tooltip");
            }
        }

        private static void BesiegeConsequence(MenuCallbackArgs args)
        {
            var currentSettlement = Settlement.CurrentSettlement;
            if (PlayerEncounter.Current is not null)
                PlayerEncounter.Finish();
            CampaignState.Current.SiegeEventManager.StartSiegeEvent(currentSettlement, MobileParty.MainParty);
            PlayerSiege.StartPlayerSiege(BattleSideEnum.Attacker);
            PlayerSiege.StartSiegePreparation();
        }

        private static bool ContinueSiegeCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Continue;
            var encounteredParty = PlayerEncounter.EncounteredParty;
            var encounteredBattle = PlayerEncounter.EncounteredBattle;
            var settlement = encounteredParty?.Settlement;
            var hasVanillaSiegeShape =
                encounteredBattle is not null &&
                encounteredBattle.GetLeaderParty(PartyBase.MainParty.Side) == PartyBase.MainParty &&
                encounteredParty!.IsSettlement &&
                settlement!.IsFortification &&
                settlement.IsUnderSiege &&
                settlement.CurrentSiegeState == Settlement.SiegeState.OnTheWalls;

            return InteractionPolicy.CanShowPrivateWarContinueSiegeOption(
                PrivateWarPatchHelper.AreEnemies(Hero.MainHero.Clan, settlement?.OwnerClan),
                hasVanillaSiegeShape);
        }

        private static void ContinueSiegeConsequence(MenuCallbackArgs args)
        {
            if (PlayerEncounter.Battle is not null)
                PlayerEncounter.Finish();
            PlayerSiege.StartSiegePreparation();
        }

        private static bool ArmyAttackCondition(MenuCallbackArgs args)
        {
            MenuHelper.CheckEnemyAttackableHonorably(args);
            args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
            return InteractionPolicy.CanShowPrivateWarArmyAttackOption(
                PrivateWarPatchHelper.AreEnemies(
                    MobileParty.MainParty.ActualClan,
                    PlayerEncounter.EncounteredMobileParty?.ActualClan));
        }

        private static void ArmyAttackConsequence(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu("encounter");
        }
    }
}
