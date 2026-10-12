using System.Collections.Generic;
using System.Linq;

namespace Bannerlord.PrivateWars.Domain.Model
{
    /// <summary>
    /// The result of one resolved battle between the two sides of a private war, fed into
    /// <see cref="Bannerlord.PrivateWars.Domain.ApplyBattleOutcomeUseCase"/>. The battle
    /// bump is normalized by the *losing side's total strength* so a decisive engagement is worth
    /// far more than many skirmishes (design §18.B).
    /// </summary>
    public record BattleOutcome(
        WarSide Winner,
        float EnemyForceDefeated,
        float LosingSideTotalStrength)
    {
        /// <summary>
        /// Measures a battle against the losing war side's whole field strength: the force it lost in
        /// this battle plus every party of that side not in the battle. Normalizing by the battle's
        /// losing parties alone made every decisive win, however small, worth the full weight.
        /// </summary>
        public static BattleOutcome Measure(
            WarSide winner,
            float defeatedInBattle,
            IEnumerable<(WarSide Side, float Strength)> partiesNotInBattle)
        {
            var losingSide = winner == WarSide.Attacker ? WarSide.Defender : WarSide.Attacker;
            float elsewhere = partiesNotInBattle.Where(party => party.Side == losingSide).Sum(party => party.Strength);
            return new BattleOutcome(winner, defeatedInBattle, defeatedInBattle + elsewhere);
        }
    }
}
