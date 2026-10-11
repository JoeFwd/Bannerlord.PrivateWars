using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain.Port
{
    /// <summary>
    /// Cached side and suzerain lookups, shared with the <see cref="IPrivateWarHostility"/> signal
    /// so the campaign AI and the hostility patches resolve each suzerain chain once.
    /// </summary>
    public interface IPrivateWarSidesProvider
    {
        /// <summary>Which side of the war a clan fights on, walking up its suzerain chain.</summary>
        WarSide? ResolveSide(string clanId, PrivateWar war);

        string? GetSuzerain(string clanId);
    }
}
