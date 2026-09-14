using System;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Resolves a private war identified by id under the given outcome against live world state,
    /// returning the concluded war snapshot or null if no war with that id exists. Unlike
    /// <see cref="IResolvePrivateWarUseCase"/> (a pure planner) this owns the live-state gathering, so
    /// it is implemented by the campaign behaviour. Exposed as a game-free port so the public API can
    /// route IPrivateWarsApi.ResolveWar through it without referencing any Bannerlord assembly.
    /// </summary>
    public interface IResolvePrivateWarByIdUseCase
    {
        /// <summary>
        /// Raised once a war has been concluded, carrying the resolved snapshot and the outcome it
        /// was resolved under.
        /// </summary>
        event Action<PrivateWar, PrivateWarOutcome> WarResolved;

        PrivateWar? ResolveWarById(string warId, PrivateWarOutcome outcome);
    }
}
