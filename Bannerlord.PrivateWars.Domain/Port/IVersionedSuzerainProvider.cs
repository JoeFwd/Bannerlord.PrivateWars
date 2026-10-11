namespace Bannerlord.PrivateWars.Domain.Port
{
    /// <summary>
    /// Opts a hierarchy into cached war-side resolution. The revision must change whenever
    /// any suzerain link can change. Null disables caching for a dynamic, unversioned provider.
    /// </summary>
    public interface IVersionedSuzerainProvider : ISuzerainProvider
    {
        long? Revision { get; }
    }
}
