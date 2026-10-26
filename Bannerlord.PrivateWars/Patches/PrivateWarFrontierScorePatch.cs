using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bannerlord.PrivateWars.Campaign;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Patches
{
    // Vanilla's besieging "distance" score is a frontier test: the share of the target's neighbouring
    // fortifications (and, at a quarter weight, their neighbours) held by the besieger's faction rather
    // than the target's. Inside one kingdom every neighbour shares both factions, so a feud target always
    // scored 0. Re-run the same two rings through the feud's sides: the enemy side's fiefs are the target's,
    // the realm's other fiefs and our side's are ours.
    public class PrivateWarFrontierScorePatch : IPatch
    {
        private static PrivateWarFronts _fronts = null!;

        public PrivateWarFrontierScorePatch(PrivateWarFronts fronts)
        {
            _fronts = fronts;
        }

        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.CalculateDistanceScoreForBesieging();

        public MethodInfo? PatchMethod => AccessTools.Method(typeof(PrivateWarFrontierScorePatch), nameof(Postfix));

        public PatchType PatchType => PatchType.Postfix;

        private static void Postfix(Settlement targetSettlement, MobileParty mobileParty, ref float bestDistanceScore)
        {
            if (targetSettlement.MapFaction != mobileParty.MapFaction) return;

            var front = _fronts.FindPrivateFront(mobileParty);
            if (front is null || !front.Targets.Contains(targetSettlement)) return;

            var checkedNeighbours = new HashSet<Settlement> { targetSettlement };
            var ring = targetSettlement.Town.GetNeighborFortifications(MobileParty.NavigationType.All);
            var nextRing = new List<Settlement>();
            foreach (var neighbour in ring)
            {
                checkedNeighbours.Add(neighbour);
                foreach (var next in neighbour.Town.GetNeighborFortifications(MobileParty.NavigationType.All))
                    nextRing.Add(next);
            }

            float score = RingShare(ring, mobileParty, front, null) + 0.25f * RingShare(nextRing, mobileParty, front, checkedNeighbours);
            bestDistanceScore = score < 0.1f ? 0f : score;
        }

        // Vanilla's ratio: neighbours not held by the target's side, and how many of those are ours.
        private static float RingShare(IEnumerable<Settlement> ring, MobileParty party, PrivateWarFront front, HashSet<Settlement>? skip)
        {
            float notTheirs = 0.01f, ours = 0.0001f;
            foreach (var settlement in ring)
            {
                if (skip is not null && !skip.Add(settlement)) continue;
                if (_fronts.IsEnemyHeld(settlement, front)) continue;

                notTheirs += 1f;
                if (settlement.MapFaction == party.MapFaction
                    || settlement.OwnerClan is { } owner && _fronts.ResolveSide(owner, front.War) == front.Side)
                    ours += 1f;
            }

            return ours / notTheirs;
        }
    }
}
