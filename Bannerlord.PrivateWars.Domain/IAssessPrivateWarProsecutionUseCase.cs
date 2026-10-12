using System.Collections.Generic;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Whether the attacker side of a prospective war could put its main goal under siege today: some
    /// party on that side can lead a private-war army, and that army beats the goal's walled defence.
    /// A war it could not prosecute would sit declared and unfought until fatigue concluded it. Needs
    /// live party state, so it is implemented by the campaign behaviour and exposed as a game-free port
    /// for IPrivateWarsApi.CanProsecute.
    /// </summary>
    public interface IAssessPrivateWarProsecutionUseCase
    {
        bool CanProsecute(
            string attackerClanId,
            string defenderClanId,
            string mainGoalSettlementId,
            IReadOnlyCollection<string> attackerSupporters,
            IReadOnlyCollection<string> defenderSupporters);
    }
}
