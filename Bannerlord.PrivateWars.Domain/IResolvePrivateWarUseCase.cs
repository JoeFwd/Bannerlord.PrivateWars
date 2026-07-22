using System;
using System.Collections.Generic;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    public interface IResolvePrivateWarUseCase
    {
        ResolutionPlan Execute(
            PrivateWar war,
            PrivateWarOutcome outcome,
            IReadOnlyDictionary<string, string> currentOwners,
            Func<string, WarSide?> resolveSide);
    }
}
