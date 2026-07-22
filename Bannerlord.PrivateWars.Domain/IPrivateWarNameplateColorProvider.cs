namespace Bannerlord.PrivateWars.Domain
{
    // Supplies raw, unvalidated ARGB hex strings configured for private-war nameplate tints
    // (typically from a config file).  Values may be null, empty, or malformed — all validation
    // and defaulting is the use case's responsibility (PrivateWarNameplateColorUseCase), not this
    // provider's.
    public interface IPrivateWarNameplateColorProvider
    {
        string? GetConfiguredPrivateWarEnemyColorArgb();

        string? GetConfiguredPrivateWarAllyColorArgb();
    }
}
