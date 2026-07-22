using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Recomputes the war score from current observations and reports a terminal outcome when the
    /// score crosses a victory threshold: attacker wins at +100, defender at -100 (design §18.B).
    /// Pure — the campaign layer persists the new score and acts on the outcome.
    /// </summary>
    public class TickPrivateWarUseCase : ITickPrivateWarUseCase
    {
        private readonly PrivateWarScoreCalculator _calculator;

        public TickPrivateWarUseCase(PrivateWarScoreCalculator calculator)
        {
            _calculator = calculator;
        }

        public TickResult Execute(PrivateWar war, PrivateWarObservations observations, float currentDay)
        {
            float daysElapsed = currentDay - war.GoalLastTakenDay;
            float score = _calculator.Compute(observations, daysElapsed);

            PrivateWarOutcome? outcome =
                score >= PrivateWarScoreCalculator.ScoreLimit ? PrivateWarOutcome.AttackerVictory
                : score <= -PrivateWarScoreCalculator.ScoreLimit ? PrivateWarOutcome.DefenderVictory
                : (PrivateWarOutcome?)null;

            return new TickResult(score, outcome);
        }
    }
}
