namespace Bannerlord.PrivateWars.Api;

/// <summary>
/// The outcome a consumer requests when resolving a private war. Mirrors the internal domain
/// outcome; the API implementation maps between the two so the internal enum never leaks across
/// the package boundary.
/// </summary>
public enum PrivateWarOutcome
{
    AttackerVictory,
    DefenderVictory,
    WhitePeace
}
