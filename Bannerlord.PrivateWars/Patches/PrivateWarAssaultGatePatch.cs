using System;
using System.Reflection;
using Bannerlord.PrivateWars.DI;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Patches
{
    // A private-war besieger whose camp is ready (BesiegerCamp.IsReadyToBesiege) switches to
    // AiBehavior.AssaultSettlement and walks up to the walls, but EncounterManager.StartSettlementEncounter
    // only starts the wall assault when FactionManager.IsAtWarAgainstFaction(attacker, settlement) holds -
    // false for two clans of one kingdom. Arm a one-shot answer for exactly that call: the prefix arms it
    // for an AI assault on a private-war enemy's fief; the next same-kingdom war check consumes it. That
    // check is the assault gate - the branches before it short-circuit on ShortTermBehavior or on the
    // settlement's MapEvent before asking. Consuming it at once keeps the rest of the encounter (defender
    // admission via SiegeEvent.CanPartyJoinSide) on its own private-war rules, not on "the kingdom is
    // at war with itself".
    internal static class PrivateWarAssaultGate
    {
        [ThreadStatic] public static IFaction? Armed;
    }

    public class PrivateWarAssaultGateArmPatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.StartSettlementEncounter();

        public MethodInfo? PatchMethod => AccessTools.Method(typeof(PrivateWarAssaultGateArmPatch), nameof(Arm));

        public PatchType PatchType => PatchType.Prefix;

        private static void Arm(MobileParty attackerParty, Settlement settlement)
        {
            PrivateWarAssaultGate.Armed =
                attackerParty != MobileParty.MainParty
                && attackerParty.ShortTermBehavior == AiBehavior.AssaultSettlement
                && attackerParty.ShortTermTargetSettlement == settlement
                && settlement.Party.MapEvent == null
                && attackerParty.MapFaction == settlement.MapFaction
                && PrivateWarPatchHelper.AreEnemies(attackerParty.ActualClan, settlement.OwnerClan)
                    ? attackerParty.MapFaction
                    : null;
        }
    }

    public class PrivateWarAssaultGateDisarmPatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.StartSettlementEncounter();

        public MethodInfo? PatchMethod => AccessTools.Method(typeof(PrivateWarAssaultGateDisarmPatch), nameof(Disarm));

        public PatchType PatchType => PatchType.Postfix;

        private static void Disarm() => PrivateWarAssaultGate.Armed = null;
    }

    public class PrivateWarAssaultGateAnswerPatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.FactionManagerIsAtWarAgainstFaction();

        public MethodInfo? PatchMethod => AccessTools.Method(typeof(PrivateWarAssaultGateAnswerPatch), nameof(Answer));

        public PatchType PatchType => PatchType.Prefix;

        private static bool Answer(IFaction faction1, IFaction faction2, ref bool __result)
        {
            var armed = PrivateWarAssaultGate.Armed;
            if (armed is null || faction1 != armed || faction2 != armed) return true;

            PrivateWarAssaultGate.Armed = null;
            __result = true;
            return false;
        }
    }
}
