using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Port;

namespace Bannerlord.PrivateWars.DI
{
    /**
     * <summary>
     *  Static service locator for private-war collaborators that are consumed by objects the game's
     *  own machinery creates rather than the DI container: Harmony patches, campaign game models, and
     *  Gauntlet nameplate mixins. Populated once by the SubModule from the runtime container.
     * </summary>
     * <remarks>
     *  Every consumer must check <see cref="IsInitialised"/> and degrade gracefully (behave as if no
     *  private war exists) when the locator has not been initialised (no campaign, unit tests). This
     *  mirrors DADG's former <c>FeudalServices</c>/<c>FeudalUiServices</c> private-war members, which
     *  this module now owns.
     * </remarks>
     */
    public static class PrivateWarsServices
    {
        public static IPrivateWarRepository? PrivateWars { get; private set; }
        public static IPrivateWarHostility? PrivateWarHostility { get; private set; }
        public static IPrivateWarNameplateColorUseCase? PrivateWarNameplateColor { get; private set; }

        public static bool IsInitialised { get; private set; }

        public static void Initialise(
            IPrivateWarRepository privateWars,
            IPrivateWarHostility privateWarHostility,
            IPrivateWarNameplateColorUseCase privateWarNameplateColor)
        {
            PrivateWars = privateWars;
            PrivateWarHostility = privateWarHostility;
            PrivateWarNameplateColor = privateWarNameplateColor;
            IsInitialised = true;
        }

        public static void Reset()
        {
            PrivateWars = null;
            PrivateWarHostility = null;
            PrivateWarNameplateColor = null;
            IsInitialised = false;
        }
    }
}
