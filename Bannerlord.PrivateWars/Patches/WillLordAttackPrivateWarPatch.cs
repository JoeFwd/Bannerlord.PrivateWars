using System.Reflection;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;
using CampaignState = TaleWorlds.CampaignSystem.Campaign;

namespace Bannerlord.PrivateWars.Patches
{
    // Let a same-kingdom private-war rival open a hostile field encounter against the player.
    //
    // HeroHelper.WillLordAttack decides whether the lord the player just ran into uses the *attacking*
    // conversation branch (which escalates to battle) instead of a friendly meeting. Its gate is
    //   FactionManager.IsAtWarAgainstFaction(partyBase.MapFaction, Hero.MainHero.MapFaction)
    // which is always false between two clans of one kingdom (design §2). With it false the meeting stays
    // friendly and dismissable, the rival immediately re-collides, and you get an endless meeting loop —
    // the exact reason DadgEncounterGameMenuModel used to force the battle outright.
    //
    // This postfix re-applies vanilla's own guards (player is the defender, a real conversation lord, not
    // a prisoner/captured context, attack cooldown elapsed) but swaps the war check for the private-war
    // registry. The whole vanilla attacking flow ("Yield or fight!" → battle) then runs unchanged, so the
    // field encounter becomes a real enemy dialog rather than a forced battle. PlayerIsEnemyTagPatch
    // supplies the hostile greeting lines on top of this.
    public class WillLordAttackPrivateWarPatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.WillLordAttack();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(WillLordAttackPrivateWarPatch), nameof(Postfix));

        public PatchType PatchType => PatchType.Postfix;

        private static void Postfix(ref bool __result)
        {
            if (__result) return;

            var encounter = PlayerEncounter.Current;
            if (encounter is null || encounter.PlayerSide != BattleSideEnum.Defender) return;

            var encountered = PlayerEncounter.EncounteredMobileParty;
            if (encountered != null && encountered.Ai.DoNotAttackMainPartyUntil.IsFuture) return;

            var rival = Hero.OneToOneConversationHero;
            if (rival is null || rival.IsPrisoner) return;

            var context = CampaignState.Current.CurrentConversationContext;
            if (context == ConversationContext.CapturedLord || context == ConversationContext.FreeOrCapturePrisonerHero) return;

            if (!PrivateWarPatchHelper.AreEnemies(rival.Clan, Hero.MainHero?.Clan)) return;

            __result = true;
        }
    }
}
