namespace Bannerlord.PrivateWars.Domain.Model
{
    public record PrivateWarArmyParty(
        string PartyId,
        string ClanId,
        bool CanLeadArmy,
        bool CanJoinArmy,
        float MemberDesirability);
}
