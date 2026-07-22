namespace Bannerlord.PrivateWars.Api;

/// <summary>
/// The lifecycle status of a private war, as exposed to consumers. Mirrors the internal domain
/// status; the API implementation maps between the two so the internal enum never leaks across
/// the package boundary.
/// </summary>
public enum PrivateWarStatus
{
    Active,
    Concluded
}
