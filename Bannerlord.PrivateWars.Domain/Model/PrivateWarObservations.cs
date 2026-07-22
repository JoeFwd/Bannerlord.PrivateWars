namespace Bannerlord.PrivateWars.Domain.Model
{
    /// <summary>
    /// Everything <see cref="PrivateWarScoreCalculator"/> needs to compute the signed war score,
    /// assembled by the campaign layer each tick from current world state (settlement control,
    /// captivity) plus the war record's accumulated battle score. Counts span the whole side
    /// (principal + sub-vassals). Prisoner counts include only captives of the *opposing
    /// principal's clan* (design §18.B).
    /// </summary>
    public record PrivateWarObservations(
        bool AttackerHoldsMainGoal,
        int DefenderSideTownsHeldByAttacker,
        int DefenderSideCastlesHeldByAttacker,
        int AttackerSideTownsHeldByDefender,
        int AttackerSideCastlesHeldByDefender,
        int DefenderClanPrisonersHeldByAttackerSide,
        int AttackerClanPrisonersHeldByDefenderSide,
        float AccumulatedBattleScore);
}
