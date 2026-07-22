using System;
using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    public class PrivateWarArmyPolicy
    {
        public PrivateWarArmyPlan? CreatePlan(
            PrivateWar war,
            WarSide side,
            string candidateLeaderPartyId,
            IReadOnlyList<PrivateWarArmyParty> parties,
            IReadOnlyList<PrivateWarArmyAssignment> assignments,
            Func<string, string?> getSuzerain,
            int maximumMemberCount)
        {
            var candidate = parties.FirstOrDefault(p => p.PartyId == candidateLeaderPartyId);
            if (candidate is null || !candidate.CanLeadArmy) return null;

            var currentAssignment = assignments.FirstOrDefault(a =>
                a.PrivateWarId == war.Id
                && a.Side == side
                && a.GoalSettlementId == war.MainGoalSettlementId);
            if (currentAssignment is not null
                && currentAssignment.LeaderPartyId != candidateLeaderPartyId)
            {
                return null;
            }
            var isCurrentLeader = currentAssignment?.LeaderPartyId == candidateLeaderPartyId;

            var principalClanId = side == WarSide.Attacker
                ? war.AttackerPrincipalClanId
                : war.DefenderPrincipalClanId;
            var sideResolver = new WarSideResolver();
            var hasEligiblePrincipalLeader = parties.Any(p =>
                p.CanLeadArmy && p.ClanId == principalClanId);

            if (!isCurrentLeader
                && hasEligiblePrincipalLeader
                && candidate.ClanId != principalClanId)
            {
                return null;
            }
            if (sideResolver.ResolveSide(candidate.ClanId, war, getSuzerain) != side) return null;

            var memberPartyIds = parties
                .Where(p => p.PartyId != candidate.PartyId)
                .Where(p => p.CanJoinArmy)
                .Where(p => sideResolver.ResolveSide(p.ClanId, war, getSuzerain) == side)
                .OrderByDescending(p => p.MemberDesirability)
                .Take(maximumMemberCount)
                .Select(p => p.PartyId)
                .ToList();

            return new PrivateWarArmyPlan(
                war.MainGoalSettlementId,
                candidate.PartyId,
                memberPartyIds);
        }
    }
}
