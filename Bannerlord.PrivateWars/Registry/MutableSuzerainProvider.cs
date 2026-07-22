using Bannerlord.PrivateWars.Domain.Port;

namespace Bannerlord.PrivateWars.Registry
{
    // Module-owned suzerain provider. The registry and campaign behaviour capture an ISuzerainProvider
    // at construction, but the host only supplies the real feudal hierarchy later (through
    // IPrivateWarsApi.SetSuzerainProvider). This indirection lets that late-bound provider reach the
    // already-built singletons: it delegates to an inner provider, defaulting to a null-returning one
    // so side resolution falls back to principal clans only until a host sets the real hierarchy.
    public class MutableSuzerainProvider : ISuzerainProvider
    {
        private ISuzerainProvider _inner = new NullSuzerainProvider();

        public string? GetSuzerain(string clanId) => _inner.GetSuzerain(clanId);

        public void SetProvider(ISuzerainProvider provider) => _inner = provider;

        private sealed class NullSuzerainProvider : ISuzerainProvider
        {
            public string? GetSuzerain(string clanId) => null;
        }
    }
}
