using System.Reflection;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using CampaignState = TaleWorlds.CampaignSystem.Campaign;

namespace Bannerlord.PrivateWars.Patches
{
    // Give the player the "Yield or fight!" option against a same-kingdom private-war rival.
    //
    // LordConversationsCampaignBehavior.conversation_player_can_attack_hero_on_condition is the gate for
    // the player's hostile demand option in a field meeting. It requires
    //   FactionManager.IsAtWarAgainstFaction(rival.MapFaction, Hero.MainHero.MapFaction)
    // which is always false between two clans of one kingdom (design §2), so when the player initiates the
    // encounter (player is the attacker, so WillLordAttack stays false) there is no way to escalate to a
    // fight and the meeting can only be dismissed.
    //
    // This postfix re-applies vanilla's own structural guards (party-encounter context, the encountered
    // party's leader is the conversation lord) but swaps the war check for the private-war registry, so
    // the existing "Surrender or die" → battle dialog runs unchanged for a private-war rival.
    public class PlayerCanAttackPrivateWarRivalPatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.PlayerCanAttackRival();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(PlayerCanAttackPrivateWarRivalPatch), nameof(Postfix));

        public PatchType PatchType => PatchType.Postfix;

        private static void Postfix(ref bool __result)
        {
            if (__result) return;

            var rival = Hero.OneToOneConversationHero;
            if (rival is null) return;
            if (CampaignState.Current.CurrentConversationContext != ConversationContext.PartyEncounter) return;

            var encountered = PlayerEncounter.EncounteredMobileParty;
            if (encountered is null || encountered.LeaderHero != rival) return;

            if (!PrivateWarPatchHelper.AreEnemies(rival.Clan, Hero.MainHero?.Clan)) return;

            __result = true;
        }
    }
}
