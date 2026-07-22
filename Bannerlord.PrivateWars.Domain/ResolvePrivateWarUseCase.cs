using System;
using System.Collections.Generic;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Produces the status-quo-ante resolution (design §8): every snapshotted fief that changed hands
    /// reverts to its pre-war owner, EXCEPT (a) fiefs now held by a third party to this war — a clan
    /// on neither side — which are not ours to move (design §18.E shared-goal case), and (b) on
    /// attacker victory the main goal, which is handed to the attacker as the prize instead. Pure;
    /// the integration layer applies the plan against engine state.
    /// </summary>
    public class ResolvePrivateWarUseCase : IResolvePrivateWarUseCase
    {
        public ResolutionPlan Execute(
            PrivateWar war,
            PrivateWarOutcome outcome,
            IReadOnlyDictionary<string, string> currentOwners,
            Func<string, WarSide?> resolveSide)
        {
            var reverts = new List<RevertInstruction>();
            bool attackerWon = outcome == PrivateWarOutcome.AttackerVictory;

            foreach (var snapshot in war.OriginalFiefOwners)
            {
                string settlementId = snapshot.Key;
                string originalOwner = snapshot.Value;

                string currentOwner = currentOwners.TryGetValue(settlementId, out var owner)
                    ? owner
                    : originalOwner;

                if (currentOwner == originalOwner) continue;                 // never left its owner
                if (resolveSide(currentOwner) is null) continue;             // third party holds it now (§18.E)
                if (attackerWon && settlementId == war.MainGoalSettlementId) continue; // becomes the prize

                reverts.Add(new RevertInstruction(settlementId, originalOwner));
            }

            PrizeAward? prize = attackerWon
                ? new PrizeAward(war.MainGoalSettlementId, war.TitleId, war.AttackerPrincipalClanId)
                : null;

            return new ResolutionPlan(reverts, prize, AttackerClaimLost: !attackerWon);
        }
    }
}
