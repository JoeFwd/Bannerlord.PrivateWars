namespace Bannerlord.PrivateWars.Domain
{
    public enum PrivateWarPrisonerReleaseReason
    {
        Ransom,
        AfterPeace,
        AfterBattle,
        Escape,
        DeliberateRelease,
        Death,
        Compensation
    }

    public sealed class PrivateWarPrisonerRetentionPolicy
    {
        public bool ShouldAllowRelease(
            bool isMainHero,
            PrivateWarPrisonerReleaseReason reason,
            bool arePrivateEnemies)
        {
            if (isMainHero || !arePrivateEnemies) return true;

            return reason != PrivateWarPrisonerReleaseReason.AfterPeace
                   && reason != PrivateWarPrisonerReleaseReason.AfterBattle;
        }
    }
}
