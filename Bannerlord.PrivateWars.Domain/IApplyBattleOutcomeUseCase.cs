using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    public interface IApplyBattleOutcomeUseCase
    {
        PrivateWar Execute(PrivateWar war, BattleOutcome outcome);
    }
}
