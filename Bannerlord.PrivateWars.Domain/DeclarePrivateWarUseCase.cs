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
    /// rejected.
    /// </summary>
    public class DeclarePrivateWarUseCase : IDeclarePrivateWarUseCase
    {
        private readonly IPrivateWarRepository _repository;

        public DeclarePrivateWarUseCase(IPrivateWarRepository repository)
        {
            _repository = repository;
        }

        public PrivateWar? Execute(
            string attackerPrincipalClanId,
            string defenderPrincipalClanId,
            ICasusBelli casusBelli,
            string? mainGoalSettlementId,
            IReadOnlyDictionary<string, string> fiefSnapshot,
            float startDay)
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
                CasusBelliType: casusBelli.Type,
                TitleId: casusBelli.TitleId,
                MainGoalSettlementId: mainGoalSettlementId,
                OriginalFiefOwners: fiefSnapshot,
                BattleScore: 0f,
                Score: 0f,
                StartDay: startDay,
                GoalLastTakenDay: startDay,
                Status: PrivateWarStatus.Active);

            _repository.Add(war);
            return war;
        }
    }
}
