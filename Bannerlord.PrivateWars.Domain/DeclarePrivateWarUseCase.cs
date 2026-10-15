using System;
using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Opens a new private war if it is admissible: the defendant must not be landless (a main goal
    /// must exist, design §18.D), and no active war may already press the same casus belli between
    /// the same two principals (one war per principal pair per CB, design §18.A). Returns null when
    /// rejected. The war records every snapshotted fief held by a clan on either side, so any of them
    /// can be fought over, scored, and reverted at resolution.
    /// </summary>
    public class DeclarePrivateWarUseCase : IDeclarePrivateWarUseCase
    {
        private readonly IPrivateWarRepository _repository;
        private readonly IPrivateWarSidesProvider _sides;

        public DeclarePrivateWarUseCase(IPrivateWarRepository repository, IPrivateWarSidesProvider sides)
        {
            _repository = repository;
            _sides = sides;
        }

        public PrivateWar? Execute(
            string attackerPrincipalClanId,
            string defenderPrincipalClanId,
            ICasusBelli casusBelli,
            string? mainGoalSettlementId,
            IReadOnlyDictionary<string, string> fiefSnapshot,
            float startDay,
            IReadOnlyCollection<string> attackerSupporters,
            IReadOnlyCollection<string> defenderSupporters,
            IReadOnlyCollection<string>? foreignSupporters = null)
        {
            if (mainGoalSettlementId is null) return null;

            bool duplicate = _repository
                .GetByPrincipalPair(attackerPrincipalClanId, defenderPrincipalClanId)
                .Any(w => w.Status == PrivateWarStatus.Active
                          && w.CasusBelliType == casusBelli.Type
                          && w.TitleId == casusBelli.TitleId);
            if (duplicate) return null;

            var war = new PrivateWar(
                Id: $"pw_{attackerPrincipalClanId}_{defenderPrincipalClanId}_{Guid.NewGuid():N}",
                AttackerPrincipalClanId: attackerPrincipalClanId,
                DefenderPrincipalClanId: defenderPrincipalClanId,
                AttackerSupporters: attackerSupporters,
                DefenderSupporters: defenderSupporters,
                CasusBelliType: casusBelli.Type,
                TitleId: casusBelli.TitleId,
                MainGoalSettlementId: mainGoalSettlementId,
                OriginalFiefOwners: fiefSnapshot,
                BattleScore: 0f,
                Score: 0f,
                StartDay: startDay,
                GoalLastTakenDay: startDay,
                Status: PrivateWarStatus.Active)
            {
                ForeignSupporters = foreignSupporters ?? Array.Empty<string>()
            };
            war = war with
            {
                OriginalFiefOwners = fiefSnapshot
                    .Where(entry => _sides.ResolveSide(entry.Value, war) is not null)
                    .ToDictionary(entry => entry.Key, entry => entry.Value)
            };

            _repository.Add(war);
            return war;
        }
    }
}
