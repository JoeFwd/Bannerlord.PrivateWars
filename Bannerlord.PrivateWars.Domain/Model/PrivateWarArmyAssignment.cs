namespace Bannerlord.PrivateWars.Domain.Model
{
    public record PrivateWarArmyAssignment(
        string PrivateWarId,
        WarSide Side,
        string GoalSettlementId,
        string LeaderPartyId);
}
