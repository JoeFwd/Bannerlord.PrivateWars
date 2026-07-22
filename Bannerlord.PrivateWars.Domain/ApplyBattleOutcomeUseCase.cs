using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// The sole mutator of a war's accumulated battle score (design §18.B). Each resolved
    /// between-sides battle adds a normalized bump — the share of the losing side's *total* strength
    /// destroyed — signed by the winner and accumulated, then capped so battles stay subordinate to
    /// the main goal and fatigue. Pure; the campaign layer persists the returned record.
    /// </summary>
    public class ApplyBattleOutcomeUseCase : IApplyBattleOutcomeUseCase
    {
        // Points awarded for annihilating a whole enemy side in a single battle, and the ceiling on
        // total accumulated battle score. Both tunable; start equal so one annihilation caps it.
        public const float BattleWeight = 50f;
        public const float BattleScoreCap = 50f;

        public PrivateWar Execute(PrivateWar war, BattleOutcome outcome)
        {
            if (outcome.LosingSideTotalStrength <= 0f) return war;

            float fraction = outcome.EnemyForceDefeated / outcome.LosingSideTotalStrength;
            if (fraction < 0f) fraction = 0f;
            else if (fraction > 1f) fraction = 1f;

            float signed = BattleWeight * fraction * (outcome.Winner == WarSide.Attacker ? 1f : -1f);

            float accumulated = war.BattleScore + signed;
            if (accumulated < -BattleScoreCap) accumulated = -BattleScoreCap;
            else if (accumulated > BattleScoreCap) accumulated = BattleScoreCap;

            return war with { BattleScore = accumulated };
        }
    }
}
