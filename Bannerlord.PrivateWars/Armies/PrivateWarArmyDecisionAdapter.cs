using System;
using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Armies
{
    public record PrivateWarArmyCandidate(
        string PartyId,
        string ClanId,
        bool CanLeadArmy,
        bool CanJoinArmy,
        float SiegeStrength,
        float MemberDesirability);

    public record ArmyFormationDecision(
        bool CanCreateArmy,
        IReadOnlyList<string> MemberPartyIds);

    public enum PrivateWarArmyCreationAction
    {
        ContinueOriginal,
        ReplaceMembers,
        Suppress
    }

    public record PrivateWarArmyCreationDecision(
        PrivateWarArmyCreationAction Action,
        IReadOnlyList<string> MemberPartyIds);

    public class PrivateWarArmyDecisionAdapter
    {
        private readonly PrivateWarArmyPolicy _policy;

        public PrivateWarArmyDecisionAdapter(PrivateWarArmyPolicy policy)
        {
            _policy = policy;
        }

        public PrivateWarArmyPlan? CreatePlan(
            PrivateWar war,
            WarSide side,
            string leaderPartyId,
            IReadOnlyList<PrivateWarArmyCandidate> candidates,
            IReadOnlyList<PrivateWarArmyAssignment> assignments,
            Func<string, string?> getSuzerain,
            bool kingdomHasSettlements,
            int maximumMemberCount)
        {
            var plan = _policy.CreatePlan(
                war,
                side,
                leaderPartyId,
                candidates.Select(candidate => new PrivateWarArmyParty(
                    candidate.PartyId,
                    candidate.ClanId,
                    candidate.CanLeadArmy,
                    candidate.CanJoinArmy,
                    candidate.MemberDesirability)).ToList(),
                assignments,
                getSuzerain,
                maximumMemberCount);
            if (plan is null) return null;

            var leader = candidates.First(candidate => candidate.PartyId == plan.LeaderPartyId);
            var members = candidates
                .Where(candidate => plan.MemberPartyIds.Contains(candidate.PartyId))
                .ToList();
            if (members.Count == 0
                || kingdomHasSettlements
                && leader.SiegeStrength + members.Sum(member => member.SiegeStrength) < 1000f)
            {
                return null;
            }

            return plan;
        }

        public ArmyFormationDecision FilterOrdinaryMembers(
            bool vanillaCanCreateArmy,
            string leaderClanId,
            float leaderSiegeStrength,
            bool kingdomHasSettlements,
            IReadOnlyList<PrivateWarArmyCandidate> candidates,
            Func<string, string, bool> areEnemies)
        {
            if (!vanillaCanCreateArmy)
                return new ArmyFormationDecision(false, Array.Empty<string>());

            var members = candidates
                .Where(candidate => candidate.CanJoinArmy)
                .Where(candidate => !areEnemies(leaderClanId, candidate.ClanId))
                .ToList();
            if (members.Count == 0
                || kingdomHasSettlements
                && leaderSiegeStrength + members.Sum(member => member.SiegeStrength) < 1000f)
            {
                return new ArmyFormationDecision(false, Array.Empty<string>());
            }

            return new ArmyFormationDecision(
                true, members.Select(member => member.PartyId).ToList());
        }

        public PrivateWarArmyCreationDecision DecidePrivateCreation(
            bool isBesieger,
            bool hasActivePrivateWar,
            string leaderPartyId,
            string targetSettlementId,
            string privateWarLeaderPartyId,
            string frozenGoalSettlementId,
            Func<IReadOnlyList<string>?> revalidateMembers)
        {
            if (isBesieger
                && hasActivePrivateWar
                && leaderPartyId == privateWarLeaderPartyId
                && targetSettlementId == frozenGoalSettlementId)
            {
                var memberPartyIds = revalidateMembers();
                if (memberPartyIds != null)
                {
                    return new PrivateWarArmyCreationDecision(
                        PrivateWarArmyCreationAction.ReplaceMembers,
                        memberPartyIds);
                }

                return new PrivateWarArmyCreationDecision(
                    PrivateWarArmyCreationAction.Suppress,
                    Array.Empty<string>());
            }

            return new PrivateWarArmyCreationDecision(
                PrivateWarArmyCreationAction.ContinueOriginal,
                Array.Empty<string>());
        }
    }
}
