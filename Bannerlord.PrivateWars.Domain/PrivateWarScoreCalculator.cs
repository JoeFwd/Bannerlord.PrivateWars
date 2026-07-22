using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// The signed, attacker-positive war score (design §18.B). State terms (main goal, settlement
    /// control, prisoners) are recomputed from the fed-in observations; the accumulated battle score
    /// rides in on the observations; fatigue is the uncapped forcing function whose direction is set
    /// by who holds the frozen main goal — guaranteeing the war terminates at ±100 even in a
    /// stalemate. The final total is clamped to [-100, +100].
    /// </summary>
    public class PrivateWarScoreCalculator
    {
        public const float MainGoalWeight = 50f;
        public const float TownWeight = 30f;
        public const float CastleWeight = 10f;
        public const float PrisonerWeight = 5f;

        // Points of fatigue drift per day toward the main-goal holder. ~50 days to swing a held-goal
        // stalemate from 0 to the +100 victory threshold; tunable.
        public const float FatigueRatePerDay = 1f;

        public const float ScoreLimit = 100f;

        public float Compute(PrivateWarObservations obs, float daysElapsed)
        {
            float state =
                (obs.AttackerHoldsMainGoal ? MainGoalWeight : 0f)
                + TownWeight * obs.DefenderSideTownsHeldByAttacker
                + CastleWeight * obs.DefenderSideCastlesHeldByAttacker
                - TownWeight * obs.AttackerSideTownsHeldByDefender
                - CastleWeight * obs.AttackerSideCastlesHeldByDefender
                + PrisonerWeight * obs.DefenderClanPrisonersHeldByAttackerSide
                - PrisonerWeight * obs.AttackerClanPrisonersHeldByDefenderSide;

            float fatigue = FatigueRatePerDay * daysElapsed * (obs.AttackerHoldsMainGoal ? 1f : -1f);

            float total = state + obs.AccumulatedBattleScore + fatigue;
            return total < -ScoreLimit ? -ScoreLimit : (total > ScoreLimit ? ScoreLimit : total);
        }
    }
}
