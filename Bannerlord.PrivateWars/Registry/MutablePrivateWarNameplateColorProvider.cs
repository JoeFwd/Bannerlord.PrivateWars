using Bannerlord.PrivateWars.Domain;

namespace Bannerlord.PrivateWars.Registry
{
    // Module-owned nameplate-tint provider. Holds the raw enemy/ally ARGB hex strings supplied by a
    // consumer through IPrivateWarsApi.SetNameplateColors. While unset both values are null, so the
    // domain use case (PrivateWarNameplateColorUseCase) yields its built-in defaults — preserving the
    // default orange/purple tints until a host overrides them, and validating any override it is given.
    public class MutablePrivateWarNameplateColorProvider : IPrivateWarNameplateColorProvider
    {
        private string? _enemyArgb;
        private string? _allyArgb;

        public string? GetConfiguredPrivateWarEnemyColorArgb() => _enemyArgb;

        public string? GetConfiguredPrivateWarAllyColorArgb() => _allyArgb;

        public void SetColors(string? enemyArgb, string? allyArgb)
        {
            _enemyArgb = enemyArgb;
            _allyArgb = allyArgb;
        }
    }
}
