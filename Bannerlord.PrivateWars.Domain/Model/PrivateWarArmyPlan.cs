using System.Collections.Generic;

namespace Bannerlord.PrivateWars.Domain.Model
{
    public record PrivateWarArmyPlan(
        string GoalSettlementId,
        string LeaderPartyId,
        IReadOnlyList<string> MemberPartyIds);
}
