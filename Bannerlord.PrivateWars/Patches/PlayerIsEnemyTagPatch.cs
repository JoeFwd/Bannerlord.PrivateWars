using System.Reflection;
using Bannerlord.PrivateWars.Patches;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Tags;

namespace Bannerlord.PrivateWars.Patches
{
    // Make a same-kingdom private-war rival's lords greet the player as enemies.
    //
    // PlayerIsEnemyTag.IsApplicableTo decides whether an NPC uses the *enemy* conversation tree (and the
    // hostile-greeting lines) instead of the friendly one:
    //   return FactionManager.IsAtWarAgainstFaction(character.HeroObject.MapFaction, Hero.MainHero.MapFaction);
    // Both clans share a kingdom during a private war, so the MapFaction-grain check is always false
    // (design §2) and a rival lord greets the player as a friend. This postfix marks the tag applicable
    // when the speaking hero's clan is a registered private-war enemy of the player's clan, so the engine
    // routes them to the enemy dialogue tree.
    public class PlayerIsEnemyTagPatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.PlayerIsEnemyTag();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(PlayerIsEnemyTagPatch), nameof(Postfix));

        public PatchType PatchType => PatchType.Postfix;

        private static void Postfix(ref bool __result, CharacterObject character)
        {
            if (__result || character is null || !character.IsHero) return;
            if (!PrivateWarPatchHelper.AreEnemies(character.HeroObject?.Clan, Hero.MainHero?.Clan)) return;
            __result = true;
        }
    }
}
