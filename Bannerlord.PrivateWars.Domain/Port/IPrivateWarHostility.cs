using System;

namespace Bannerlord.PrivateWars.Domain.Port
{
    /// <summary>
    /// The patch-facing hostility signal: two clans are enemies when they sit on opposite sides
    /// of some active private war. This is the hot path consulted by every §4 model override and
    /// Harmony patch, so the registry implementation must answer it in O(1) (design §18.A).
    /// </summary>
    public interface IPrivateWarHostility
    {
        bool AreEnemies(string clanIdA, string clanIdB);

        /// <summary>
        /// Raised whenever the set of active private wars changes (a war is added, updated, removed,
        /// or the registry is re-initialised from a save). A private war changes no faction relation,
        /// so map nameplates that tint by belligerency have no base-VM signal to refresh on; they
        /// subscribe here to re-read their tint the moment a war starts or ends.
        /// </summary>
        event Action Changed;

        /// <summary>
        /// The co-belligerent signal: two distinct clans are allies when they sit on the SAME side
        /// of some active private war (both pulled in via the suzerain chain). A clan is never its
        /// own ally. Same O(1) hot-path contract as <see cref="AreEnemies"/>.
        /// </summary>
        bool AreAllies(string clanIdA, string clanIdB);

        /// <summary>
        /// True when the clan sits on either side of some active private war.
        /// </summary>
        bool IsBelligerent(string clanId);
    }
}
