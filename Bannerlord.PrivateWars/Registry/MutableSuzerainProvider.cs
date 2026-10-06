using Bannerlord.PrivateWars.Domain.Port;

namespace Bannerlord.PrivateWars.Registry
{
    // Module-owned suzerain provider. The registry and campaign behaviour capture an ISuzerainProvider
    // at construction, but the host only supplies the real feudal hierarchy later (through
    // IPrivateWarsApi.SetSuzerainProvider). This indirection lets that late-bound provider reach the
    // already-built singletons: it delegates to an inner provider, defaulting to a null-returning one
    // so side resolution falls back to principal clans only until a host sets the real hierarchy.
    public class MutableSuzerainProvider : IVersionedSuzerainProvider
    {
        private readonly object _sync = new();
        private ISuzerainProvider _inner = new NullSuzerainProvider();
        private long _revision;
        private long? _innerRevision;

        public long? Revision
        {
            get
            {
                lock (_sync)
                {
                    var revision = (_inner as IVersionedSuzerainProvider)?.Revision;
                    if (revision is null) return null;
                    if (_innerRevision != revision)
                    {
                        _innerRevision = revision;
                        _revision++;
                    }
                    return _revision;
                }
            }
        }

        public string? GetSuzerain(string clanId) => _inner.GetSuzerain(clanId);

        public void SetProvider(ISuzerainProvider provider)
        {
            lock (_sync)
            {
                _inner = provider;
                _innerRevision = null;
                _revision++;
            }
        }

        private sealed class NullSuzerainProvider : ISuzerainProvider
        {
            public string? GetSuzerain(string clanId) => null;
        }
    }
}
