using System.Reflection;
using Bannerlord.PrivateWars.Patches;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Bannerlord.PrivateWars.Patches
{
    // Fix the player being placed on the wrong side when besieging a same-kingdom rival's settlement.
    //
    // PlayerEncounter.SetupFields contains:
    //   else if (defenderParty == PartyBase.MainParty || ... ||
    //            (defenderParty.IsSettlement &&
    //             (defenderParty.Settlement.MapFaction == MobileParty.MainParty.MapFaction || ...)))
    //   { PlayerSide = BattleSideEnum.Defender; }
    //
    // A same-kingdom settlement passes the MapFaction == check, forcing the ATTACKER player into the
    // Defender role. This postfix detects that specific misassignment and flips the sides back via
    // reflection (PlayerSide and OpponentSide both have private setters).
    public class PlayerEncounterSetupFieldsPatch : IPatch
    {
        // PlayerSide and OpponentSide have private setters; resolve the setters by property name so we
        // do not depend on the compiler-generated backing-field name.
        private static readonly MethodInfo PlayerSideSetter =
            AccessTools.PropertySetter(typeof(PlayerEncounter), nameof(PlayerEncounter.PlayerSide));

        private static readonly MethodInfo OpponentSideSetter =
            AccessTools.PropertySetter(typeof(PlayerEncounter), nameof(PlayerEncounter.OpponentSide));

        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.PlayerEncounterSetupFields();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(PlayerEncounterSetupFieldsPatch), nameof(Postfix));

        public PatchType PatchType => PatchType.Postfix;

        private static void Postfix(PlayerEncounter __instance, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (__instance.PlayerSide != BattleSideEnum.Defender) return;
            if (attackerParty != PartyBase.MainParty) return;
            if (defenderParty is null || !defenderParty.IsSettlement) return;
            if (!PrivateWarPatchHelper.AreEnemies(Hero.MainHero?.Clan, defenderParty.Settlement?.OwnerClan)) return;

            PlayerSideSetter?.Invoke(__instance, new object[] { BattleSideEnum.Attacker });
            OpponentSideSetter?.Invoke(__instance, new object[] { BattleSideEnum.Defender });
        }
    }
}
