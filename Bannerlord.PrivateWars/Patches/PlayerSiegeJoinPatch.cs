using System.Linq;
using System.Reflection;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Bannerlord.PrivateWars.Patches
{
    // Let the player who comes upon a same-kingdom private-war siege take a side. Every option of the
    // vanilla join_siege_event menu (join the siege, break in, assault the camp) is gated on
    // SiegeEvent.CanPartyJoinSide, which tests kingdom-grain IsAtWarWith and so refuses both sides.
    // Re-decide at clan grain, as CanPartyJoinBattlePatch does for the battle itself: the player may join
    // a side when the other side holds a private-war enemy of their clan and the requested side holds none.
    // shortcut: player only; AI lords reach private-war sieges through army calls, widen if passers-by must join.
    public class PlayerSiegeJoinPatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.SiegeDefenderJoin();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(PlayerSiegeJoinPatch), nameof(Postfix));

        public PatchType PatchType => PatchType.Postfix;

        private static void Postfix(ref bool __result, SiegeEvent __instance, PartyBase party, BattleSideEnum side)
        {
            if (__result || party != PartyBase.MainParty) return;

            var joinerClan = party.MobileParty?.ActualClan;
            var otherSide = side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;
            bool HasEnemy(BattleSideEnum s) => __instance.GetSiegeEventSide(s).GetInvolvedPartiesForEventType()
                .Any(x => PrivateWarPatchHelper.AreEnemies(joinerClan, PrivateWarPatchHelper.ClanOf(x)));

            if (HasEnemy(otherSide) && !HasEnemy(side)) __result = true;
        }
    }
}
