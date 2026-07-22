namespace Bannerlord.PrivateWars.Api;

/// <summary>
/// The public, consumer-facing snapshot of a single private war. A minimal projection of the
/// internal <c>PrivateWar</c> domain record: only the fields a consuming mod needs to identify a
/// war, its two principals, its casus belli, the contested title, the frozen main-goal settlement,
/// and its lifecycle status. Internal accounting (scores, fief snapshots, epochs) is not exposed.
/// </summary>
public record PrivateWarInfo(
    string Id,
    string AttackerClanId,
    string DefenderClanId,
    string CasusBelliType,
    string TitleId,
    string MainGoalSettlementId,
    PrivateWarStatus Status);
