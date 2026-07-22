using System.Linq;
using System.Reflection;
using Bannerlord.PrivateWars.Patches;
using Bannerlord.PrivateWars.DI;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Bannerlord.PrivateWars.Patches
{
    // Let private-war reinforcements join the correct side of an ongoing battle. Vanilla
    // CanPartyJoinBattle returns true only when the requested side holds no party at war with the
    // joiner AND the other side is entirely at war with the joiner. Two private-war sides share a
    // MapFaction (design §2), so both IsAtWarWith tests collapse to false and no reinforcement may
    // join either side. We re-decide at clan grain: a party may join a side when the other side
    // holds a private-war enemy of its clan and the requested side holds none.
    public class CanPartyJoinBattlePatch : IPatch
    {
        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.CanPartyJoinBattle();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(CanPartyJoinBattlePatch), nameof(AllowPrivateWarJoin));

        public PatchType PatchType => PatchType.Postfix;

        private static void AllowPrivateWarJoin(MapEvent __instance, ref bool __result, PartyBase party, BattleSideEnum side)
        {
            if (__result) return;
            if (!PrivateWarsServices.IsInitialised || PrivateWarsServices.PrivateWarHostility is null) return;

            var joinerClan = party?.MobileParty?.ActualClan;
            if (joinerClan is null) return;

            var requestedSide = side == BattleSideEnum.Attacker ? __instance.AttackerSide : __instance.DefenderSide;
            var otherSide = side == BattleSideEnum.Attacker ? __instance.DefenderSide : __instance.AttackerSide;

            bool otherSideHasEnemy = otherSide.Parties.Any(x =>
                x.Party.IsActive && PrivateWarPatchHelper.AreEnemies(joinerClan, x.Party.MobileParty?.ActualClan));
            if (!otherSideHasEnemy) return;

            bool requestedSideHasEnemy = requestedSide.Parties.Any(x =>
                x.Party.IsActive && PrivateWarPatchHelper.AreEnemies(joinerClan, x.Party.MobileParty?.ActualClan));
            if (requestedSideHasEnemy) return;

            __result = true;
        }
    }
}
