using System;

namespace Bannerlord.PrivateWars.Domain.Port
{
    /// <summary>
    /// A hierarchy whose underlying provider can be swapped. The replacement may report the same
    /// revision as the one it replaces, so caches must drop everything on <see cref="ProviderChanged"/>.
    /// </summary>
    public interface IReplaceableSuzerainProvider : IVersionedSuzerainProvider
    {
        event Action ProviderChanged;
    }
}
