using System;
using Microsoft.Extensions.Logging;

namespace Bannerlord.PrivateWars.DI;

/// <summary>
/// Holds the private-war mechanism's runtime service provider, set by the SubModule when the
/// game loads and read by <see cref="Bannerlord.PrivateWars.Api.PrivateWarsApiFactory"/> to
/// resolve the live services behind the public API. Mirrors Cannons' <c>CannonsRuntimeServices</c>.
/// </summary>
public static class PrivateWarsRuntimeServices
{
    public static IServiceProvider? Current { get; private set; }

    public static void Set(IServiceProvider serviceProvider)
    {
        Current = serviceProvider;
    }

    private static ILoggerFactory? _externalLoggerFactory;

    /// <summary>
    /// Redirects the mechanism's internal logging to a consumer-supplied factory. Mirrors
    /// <c>CannonsServiceContainer.SetExternalLoggerFactory</c>.
    /// </summary>
    public static void SetExternalLoggerFactory(ILoggerFactory loggerFactory)
        => _externalLoggerFactory = loggerFactory;

    /// <summary>
    /// The external logger factory, if a consumer supplied one via
    /// <see cref="PrivateWarsApiFactory.Create"/>; otherwise null.
    /// </summary>
    public static ILoggerFactory? ExternalLoggerFactory => _externalLoggerFactory;
}
