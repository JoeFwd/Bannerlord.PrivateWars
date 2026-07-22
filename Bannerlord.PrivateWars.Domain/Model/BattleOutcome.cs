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
        float LosingSideTotalStrength);
}
