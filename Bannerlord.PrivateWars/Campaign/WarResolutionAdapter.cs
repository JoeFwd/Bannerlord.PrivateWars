using System;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// The live-state resolution seam behind IPrivateWarsApi.ResolveWar.
    /// </summary>
    public class WarResolutionAdapter : IResolvePrivateWarByIdUseCase
    {
        private readonly IPrivateWarRepository _privateWars;
        private readonly WarConclusionAdapter _warConclusion;

        // Relayed so subscribers hear every conclusion, the daily scoring ones included, at the point
        // the conclusion adapter raises it (before the prize hand-over).
        /// <inheritdoc cref="IResolvePrivateWarByIdUseCase.WarResolved"/>
        public event Action<PrivateWar, PrivateWarOutcome>? WarResolved
        {
            add => _warConclusion.WarResolved += value;
            remove => _warConclusion.WarResolved -= value;
        }

        public WarResolutionAdapter(IPrivateWarRepository privateWars, WarConclusionAdapter warConclusion)
        {
            _privateWars = privateWars;
            _warConclusion = warConclusion;
        }

        // Looks up the war by id and applies the terminal outcome against live campaign state, then
        // returns the concluded snapshot, or null if no war with that id exists.
        public PrivateWar? ResolveWarById(string warId, PrivateWarOutcome outcome)
        {
            var war = _privateWars.Get(warId);
            if (war is null) return null;

            _warConclusion.Conclude(war, outcome);
            return _privateWars.Get(warId);
        }
    }
}
