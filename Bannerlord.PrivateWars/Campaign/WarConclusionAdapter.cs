using System;
using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using Microsoft.Extensions.Logging;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// Applies a private war's terminal outcome to live campaign state: fief reverts, the concluded
    /// record, prisoner release and the prize hand-over.
    /// </summary>
    public class WarConclusionAdapter
    {
        private readonly IPrivateWarRepository _privateWars;
        private readonly IResolvePrivateWarUseCase _resolvePrivateWar;
        private readonly IPrivateWarSidesProvider _sides;
        private readonly WarPrisonerReleaseAdapter _prisonerRelease;
        private readonly ILogger<WarConclusionAdapter> _logger;

        /// <inheritdoc cref="IResolvePrivateWarByIdUseCase.WarResolved"/>
        public event Action<PrivateWar, PrivateWarOutcome>? WarResolved;

        public WarConclusionAdapter(
            IPrivateWarRepository privateWars,
            IResolvePrivateWarUseCase resolvePrivateWar,
            IPrivateWarSidesProvider sides,
            WarPrisonerReleaseAdapter prisonerRelease,
            ILogger<WarConclusionAdapter> logger)
        {
            _privateWars = privateWars;
            _resolvePrivateWar = resolvePrivateWar;
            _sides = sides;
            _prisonerRelease = prisonerRelease;
            _logger = logger;
        }

        // Apply the terminal outcome to engine state: revert each captured fief to its pre-war owner.
        // On attacker victory the resolution leaves the main goal with the attacker as the prize, and
        // takes it off any ally or vassal of his who holds it. The war record is marked concluded and
        // WarResolved is raised BEFORE that hand-over: a consumer that awards the dignity itself (a
        // title-aware one) then still sees the loser holding the goal and treats the war as won. A plain
        // change of owner would otherwise reach it first as an administrative transfer, and the win
        // would read as already settled. The hand-over afterwards only covers what the consumer did not
        // move; it is a no-op once the goal is already the winner's.
        public void Conclude(PrivateWar war, PrivateWarOutcome outcome)
        {
            // A village's owner is its bound castle's or town's, so it changes hands and reverts with it;
            // left out of the current owners, it reads as never having moved.
            var currentOwners = new Dictionary<string, string>();
            foreach (var settlementId in war.OriginalFiefOwners.Keys)
            {
                var settlement = Settlement.Find(settlementId);
                if (settlement is null || settlement.IsVillage) continue;
                var ownerClan = settlement.OwnerClan;
                if (ownerClan != null) currentOwners[settlementId] = ownerClan.StringId;
            }

            var plan = _resolvePrivateWar.Execute(war, outcome, currentOwners, clanId => _sides.ResolveSide(clanId, war));
            _logger.LogDebug("[PW] {War} resolved {Outcome} at score={Score:0.#}; reverts=[{Reverts}]",
                war.Id, outcome, war.Score,
                string.Join(",", plan.Reverts.Select(revert => $"{revert.SettlementId}->{revert.RevertToClanId}")));

            foreach (var revert in plan.Reverts)
            {
                var settlement = Settlement.Find(revert.SettlementId);
                var newOwner = Clan.All.FirstOrDefault(c => c.StringId == revert.RevertToClanId);
                if (settlement is null || newOwner is null || newOwner.IsEliminated) continue;
                if (settlement.OwnerClan == newOwner) continue;

                var hero = newOwner.Leader ?? newOwner.Heroes.FirstOrDefault();
                if (hero is null) continue;
                ChangeOwnerOfSettlementAction.ApplyByDefault(hero, settlement);
            }

            var concludedWar = war with { Status = PrivateWarStatus.Concluded };
            _privateWars.Update(concludedWar);
            _prisonerRelease.Release(war);

            InfoPrinter.Display(
                $"Private war concluded: {war.AttackerPrincipalClanId} vs {war.DefenderPrincipalClanId} - {outcome}.");

            WarResolved?.Invoke(concludedWar, outcome);

            if (plan.Prize is { HandOverSettlement: true } prize)
            {
                var goal = Settlement.Find(prize.SettlementId);
                var principal = Clan.All.FirstOrDefault(c => c.StringId == prize.NewHolderClanId);
                var leader = principal is null || principal.IsEliminated
                    ? null
                    : principal.Leader ?? principal.Heroes.FirstOrDefault();
                if (goal is not null && leader is not null)
                {
                    string previousOwner = goal.OwnerClan?.StringId ?? "none";
                    if (goal.OwnerClan == principal)
                    {
                        _logger.LogInformation("[PW] {War} prize: goal {Settlement} (title {Title}) skipped, already held by principal {Principal}",
                            war.Id, prize.SettlementId, prize.TitleId, prize.NewHolderClanId);
                    }
                    else
                    {
                        ChangeOwnerOfSettlementAction.ApplyByDefault(leader, goal);
                        _logger.LogInformation("[PW] {War} prize: goal {Settlement} (title {Title}) owner {PreviousOwner} -> principal {Principal}",
                            war.Id, prize.SettlementId, prize.TitleId, previousOwner, prize.NewHolderClanId);
                    }
                }
            }
        }
    }
}
