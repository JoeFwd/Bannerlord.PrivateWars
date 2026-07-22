using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    public interface ITickPrivateWarUseCase
    {
        TickResult Execute(PrivateWar war, PrivateWarObservations observations, float currentDay);
    }
}
