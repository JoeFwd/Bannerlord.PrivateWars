using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.DI;
using Bannerlord.PrivateWars.Registry;
using Microsoft.Extensions.Logging;

namespace Bannerlord.PrivateWars.Api;

public static class PrivateWarsApiFactory
{
    /// <summary>
    /// Creates the private-war API.
    /// <para>
    /// Pass a <paramref name="loggerFactory"/> to redirect the mechanism's internal logging to your
    /// own logger. Must be called before the private-war SubModule finishes loading (e.g. from your
    /// own SubModule constructor).
    /// </para>
    /// </summary>
    public static IPrivateWarsApi Create(ILoggerFactory? loggerFactory = null)
    {
        if (loggerFactory != null)
            PrivateWarsRuntimeServices.SetExternalLoggerFactory(loggerFactory);

        return new PrivateWarsApi(
            Resolve<IPrivateWarRepository>,
            Resolve<IPrivateWarHostility>,
            Resolve<IDeclarePrivateWarUseCase>,
            Resolve<IResolvePrivateWarByIdUseCase>,
            Resolve<MutableSuzerainProvider>,
            Resolve<MutablePrivateWarNameplateColorProvider>);
    }

    // PrivateWarsRuntimeServices.Current is populated by the SubModule container. Before the SubModule
    // loads (or outside a running game) this resolves to null and the API degrades gracefully.
    private static T? Resolve<T>() where T : class
    {
        var serviceProvider = PrivateWarsRuntimeServices.Current;
        return serviceProvider?.GetService(typeof(T)) as T;
    }
}
