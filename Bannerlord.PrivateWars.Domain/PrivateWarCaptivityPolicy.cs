namespace Bannerlord.PrivateWars.Domain
{
    public sealed class PrivateWarCaptivityPolicy
    {
        public bool ShouldReleaseForNoMoreEnemies(
            bool vanillaAtWar,
            bool privateEnemies,
            bool sameFaction,
            bool crimeModerate,
            bool crimeSevere)
            => !vanillaAtWar
               && !privateEnemies
               && (sameFaction || (!crimeModerate && !crimeSevere));
    }
}
