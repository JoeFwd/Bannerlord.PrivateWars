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
    /// Ends private wars against live campaign state: on a score outcome, an API call, or a principal
    /// leaving the shared kingdom. Also the live-state resolution seam behind IPrivateWarsApi.ResolveWar.
    /// </summary>
    public class WarResolutionAdapter : IResolvePrivateWarByIdUseCase
    {
        private readonly IPrivateWarRepository _privateWars;
        private readonly IResolvePrivateWarUseCase _resolvePrivateWar;
        private readonly IPrivateWarSidesProvider _sides;
        private readonly WarArmyLeaders _warArmyLeaders;
        private readonly ILogger<WarResolutionAdapter> _logger;

        /// <inheritdoc cref="IResolvePrivateWarByIdUseCase.WarResolved"/>
        public event Action<PrivateWar, PrivateWarOutcome>? WarResolved;

        public WarResolutionAdapter(
            IPrivateWarRepository privateWars,
            IResolvePrivateWarUseCase resolvePrivateWar,
            IPrivateWarSidesProvider sides,
            WarArmyLeaders warArmyLeaders,
            ILogger<WarResolutionAdapter> logger)
        {
            _privateWars = privateWars;
            _resolvePrivateWar = resolvePrivateWar;
            _sides = sides;
            _warArmyLeaders = warArmyLeaders;
            _logger = logger;
        }

        // Public resolution seam for the API (IPrivateWarsApi.ResolveWar). Looks up the war by id and
        // applies the terminal outcome against live campaign state, then returns the concluded snapshot,
        // or null if no war with that id exists.
        public PrivateWar? ResolveWarById(string warId, PrivateWarOutcome outcome)
        {
            var war = _privateWars.Get(warId);
            if (war is null) return null;

            Resolve(war, outcome);
            return _privateWars.Get(warId);
        }

        // Apply the terminal outcome to engine state: revert each captured fief to its pre-war owner.
        // On attacker victory the resolution leaves the main goal with the attacker as the prize, and
        // takes it off any ally or vassal of his who holds it. The war record is marked concluded and
        // WarResolved is raised BEFORE that hand-over: a consumer that awards the dignity itself (a
        // title-aware one) then still sees the loser holding the goal and treats the war as won. A plain
        // change of owner would otherwise reach it first as an administrative transfer, and the win
        // would read as already settled. The hand-over afterwards only covers what the consumer did not
        // move; it is a no-op once the goal is already the winner's.
        public void Resolve(PrivateWar war, PrivateWarOutcome outcome)
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
            _warArmyLeaders.ForgetWar(war.Id);
            ReleaseWarPrisoners(war);

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
                if (goal is not null && leader is not null && goal.OwnerClan != principal)
                    ChangeOwnerOfSettlementAction.ApplyByDefault(leader, goal);
            }
        }

        // A private war is an intra-kingdom feud: both principals share a MapFaction, and every
        // mechanism (the AreEnemies hostility signal, the same-kingdom siege drive, prisoner retention,
        // status-quo resolution) assumes it. When a principal defects so the two principals no longer
        // share a kingdom, that premise is void and a stale Active record would keep forcing
        // cross-kingdom hostility; conclude it. Subtree vassals need no handling - WarSideResolver
        // re-resolves their side from the current suzerain chain on every query.
        public void ConcludeWarsSplitByKingdomChange(Clan clan)
        {
            foreach (var war in _privateWars.GetByClan(clan.StringId))
            {
                if (war.Status != PrivateWarStatus.Active) continue;

                var attacker = Clan.All.FirstOrDefault(c => c.StringId == war.AttackerPrincipalClanId);
                var defender = Clan.All.FirstOrDefault(c => c.StringId == war.DefenderPrincipalClanId);
                if (attacker != null && defender != null && attacker.MapFaction == defender.MapFaction)
                    continue;

                _privateWars.Update(war with { Status = PrivateWarStatus.Concluded });
                ReleaseWarPrisoners(war);
                InfoPrinter.Display(
                    $"Private war concluded: {war.AttackerPrincipalClanId} vs {war.DefenderPrincipalClanId} - " +
                    "principals no longer share a kingdom.");
            }
        }

        // Release every captive this war put behind bars now that it has concluded. During the war
        // PrivateWarPrisonerRetentionPatch deliberately blocks the engine's involuntary auto-release of
        // a same-kingdom captive (peace sweeps, kingdom changes, our own siege capture) so the prize of
        // capturing the goal sticks; escape attempts are never blocked. Once the war ends that retention
        // no longer makes sense - the principals are at peace - so we deliberately free anyone held by the
        // opposing side. Must run AFTER the status flips to Concluded: AreEnemies is Active-gated, so a
        // concluded war no longer reports the pair as enemies and the retention patch lets the release
        // through. ApplyByPeace routes through the unblocked deliberate-release path.
        private void ReleaseWarPrisoners(PrivateWar war)
        {
            foreach (var hero in Hero.AllAliveHeroes.Where(h => h.IsPrisoner).ToList())
            {
                if (hero == Hero.MainHero || hero.Clan is null) continue;

                var captorClan = PartyClan.OwnerClanOf(hero.PartyBelongedToAsPrisoner);
                if (captorClan is null) continue;

                var prisonerSide = _sides.ResolveSide(hero.Clan.StringId, war);
                var captorSide = _sides.ResolveSide(captorClan.StringId, war);
                if (prisonerSide is null || captorSide is null || prisonerSide == captorSide) continue;

                EndCaptivityAction.ApplyByPeace(hero);
            }
        }
    }
}
