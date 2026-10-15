using System;
using System.Reflection;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation.Tags;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Bannerlord.PrivateWars.Patches
{
    public static class PrivateWarHarmonyPatchTargets
    {
        public static MethodInfo VillageHostileActionCondition() => ResolveExact(
            typeof(VillageHostileActionCampaignBehavior),
            "game_menu_village_hostile_action_on_condition",
            BindingFlags.Static | BindingFlags.NonPublic,
            typeof(bool),
            typeof(MenuCallbackArgs));

        public static MethodInfo VillageRaidCondition() => ResolveExact(
            typeof(VillageHostileActionCampaignBehavior),
            "game_menu_village_hostile_action_raid_village_on_condition",
            BindingFlags.Instance | BindingFlags.NonPublic,
            typeof(bool),
            typeof(MenuCallbackArgs));

        public static MethodInfo MobilePartyAiIsEnemy() => ResolveExact(
            typeof(DefaultMobilePartyAIModel),
            "IsEnemy",
            BindingFlags.Instance | BindingFlags.NonPublic,
            typeof(bool),
            typeof(PartyBase),
            typeof(MobileParty));

        public static MethodInfo MobilePartyAiStanceScore() => ResolveExact(
            typeof(DefaultMobilePartyAIModel),
            "CalculateStanceScore",
            BindingFlags.Instance | BindingFlags.NonPublic,
            typeof(float),
            typeof(MobileParty),
            typeof(MobileParty));

        public static MethodInfo StartPartyEncounter() => ResolveExact(
            typeof(EncounterManager),
            nameof(EncounterManager.StartPartyEncounter),
            BindingFlags.Static | BindingFlags.Public,
            typeof(void),
            typeof(PartyBase),
            typeof(PartyBase));

        public static MethodInfo MapEventUpdate() => ResolveExact(
            typeof(MapEvent),
            "Update",
            BindingFlags.Instance | BindingFlags.NonPublic,
            typeof(void));

        public static MethodInfo KingdomIsAtWarWith() => ResolveExact(
            typeof(Kingdom),
            nameof(Kingdom.IsAtWarWith),
            BindingFlags.Instance | BindingFlags.Public,
            typeof(bool),
            typeof(IFaction));

        public static MethodInfo CanPartyJoinBattle() => ResolveExact(
            typeof(MapEvent),
            nameof(MapEvent.CanPartyJoinBattle),
            BindingFlags.Instance | BindingFlags.Public,
            typeof(bool),
            typeof(PartyBase),
            typeof(BattleSideEnum));

        public static MethodInfo PrisonerRelease() => ResolveExact(
            typeof(EndCaptivityAction),
            "ApplyInternal",
            BindingFlags.Static | BindingFlags.NonPublic,
            typeof(void),
            typeof(Hero),
            typeof(EndCaptivityDetail),
            typeof(Hero),
            typeof(bool));

        public static MethodInfo PlayerEncounterSetupFields() => ResolveExact(
            typeof(PlayerEncounter),
            nameof(PlayerEncounter.SetupFields),
            BindingFlags.Instance | BindingFlags.Public,
            typeof(void),
            typeof(PartyBase),
            typeof(PartyBase));

        public static MethodInfo SiegeDefenderJoin() => ResolveExact(
            typeof(SiegeEvent),
            nameof(SiegeEvent.CanPartyJoinSide),
            BindingFlags.Instance | BindingFlags.Public,
            typeof(bool),
            typeof(PartyBase),
            typeof(BattleSideEnum));

        public static MethodInfo PlayerIsEnemyTag() => ResolveExact(
            typeof(TaleWorlds.CampaignSystem.Conversation.Tags.PlayerIsEnemyTag),
            nameof(TaleWorlds.CampaignSystem.Conversation.Tags.PlayerIsEnemyTag.IsApplicableTo),
            BindingFlags.Instance | BindingFlags.Public,
            typeof(bool),
            typeof(CharacterObject));

        public static MethodInfo WillLordAttack() => ResolveExact(
            typeof(HeroHelper),
            nameof(HeroHelper.WillLordAttack),
            BindingFlags.Static | BindingFlags.Public,
            typeof(bool));

        public static MethodInfo PlayerCanAttackRival() => ResolveExact(
            typeof(LordConversationsCampaignBehavior),
            "conversation_player_can_attack_hero_on_condition",
            BindingFlags.Instance | BindingFlags.Public,
            typeof(bool));

        // ApplyByNoActiveWar is a one-line forwarder small enough for the JIT to inline into its caller,
        // which would bypass a prefix on it; the private ApplyInternal it forwards to is too big to inline.
        public static MethodInfo DisbandArmyInternal() => ResolveExact(
            typeof(DisbandArmyAction),
            "ApplyInternal",
            BindingFlags.Static | BindingFlags.NonPublic,
            typeof(void),
            typeof(Army),
            typeof(Army.ArmyDispersionReason));

        private static MethodInfo ResolveExact(
            Type declaringType,
            string name,
            BindingFlags bindingFlags,
            Type returnType,
            params Type[] parameterTypes)
        {
            var method = declaringType.GetMethod(name, bindingFlags, null, parameterTypes, null);
            if (method is null || method.ReturnType != returnType)
                throw new MissingMethodException(
                    declaringType.FullName,
                    $"{name}({string.Join(", ", Array.ConvertAll(parameterTypes, type => type.FullName))}) -> {returnType.FullName}");
            return method;
        }
    }
}
