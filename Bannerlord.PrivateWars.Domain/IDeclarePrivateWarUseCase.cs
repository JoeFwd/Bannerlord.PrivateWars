using System.Collections.Generic;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    public interface IDeclarePrivateWarUseCase
    {
        PrivateWar? Execute(
            string attackerPrincipalClanId,
            string defenderPrincipalClanId,
            ICasusBelli casusBelli,
            string? mainGoalSettlementId,
            IReadOnlyDictionary<string, string> fiefSnapshot,
            float startDay);
    }
}
