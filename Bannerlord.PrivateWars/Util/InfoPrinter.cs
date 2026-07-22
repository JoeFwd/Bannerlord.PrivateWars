using TaleWorlds.Library;

namespace Bannerlord.PrivateWars.Util
{

    /**
     * <summary>
     *  A helper class for printing information to the game.
     * </summary>
     */
    public static class InfoPrinter
    {
        /**
         * <summary>
         *  Displays a message in the game.
         * </summary>
         * <param name="message">
         *  The message to display.
         * </param>
         */
        public static void Display(string message)
        {
            InformationManager.DisplayMessage(new InformationMessage(message, new Color(134, 114, 250)));
        }
    }
}
