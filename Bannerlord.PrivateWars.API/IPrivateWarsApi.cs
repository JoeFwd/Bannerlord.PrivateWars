using System;
using System.Collections.Generic;
using Bannerlord.PrivateWars.Domain.Port;

namespace Bannerlord.PrivateWars.Api;

/// <summary>
/// The public consumer surface for the private-war mechanism. A host mod supplies the feudal
/// hierarchy and nameplate colours, queries the set of active wars and clan hostility, and can
/// declare or resolve wars. All parameters and results are primitives or API DTOs; no internal
/// domain type crosses this boundary.
/// </summary>
public interface IPrivateWarsApi
{
    /// <summary>
    /// Supplies the feudal suzerain link used to resolve war sides by walking up the chain to the
    /// nearest belligerent principal (call-to-arms). Must be set before wars are queried or resolved.
    /// </summary>
    void SetSuzerainProvider(ISuzerainProvider provider);

    /// <summary>
    /// Configures the nameplate tints applied to private-war enemies and co-belligerent allies.
    /// Each value is a raw ARGB hex string (AARRGGBB or RRGGBB, optional leading '#'); pass null to
    /// keep the mechanism's built-in default. The mechanism owns all validation and defaulting.
    /// </summary>
    void SetNameplateColors(string? enemyArgbHex, string? allyArgbHex);

    IReadOnlyList<PrivateWarInfo> GetAllWars();

    IReadOnlyList<PrivateWarInfo> GetWarsByClan(string clanId);

    bool AreEnemies(string clanA, string clanB);

    bool AreAllies(string clanA, string clanB);

    /// <summary>
    /// Declares a new private war. Mirrors the inputs of the internal declare use case:
    /// the two principal clans, the casus belli (its persisted type discriminator and the contested
    /// title), the frozen main-goal settlement, the declaration-time fief ownership snapshot used to
    /// revert captures on resolution (pass every candidate fief; the war keeps those held by either
    /// side, and any of them can be besieged), the campaign day of declaration, and the clans that joined
    /// each side from outside the principals' vassal subtrees. Supporters also named in
    /// <paramref name="foreignSupporters"/> come from outside the principals' kingdom and fight
    /// alone, without their vassals. Returns the resulting war, or null if the declaration was
    /// rejected.
    /// </summary>
    PrivateWarInfo? DeclareWar(
        string attackerClanId,
        string defenderClanId,
        string casusBelliType,
        string titleId,
        string? mainGoalSettlementId,
        IReadOnlyDictionary<string, string> fiefSnapshot,
        float startDay,
        IReadOnlyCollection<string> attackerSupporters,
        IReadOnlyCollection<string> defenderSupporters,
        IReadOnlyCollection<string>? foreignSupporters = null);

    /// <summary>
    /// Whether the clan fights on the attacker's side of the war, by its own pledge or through its
    /// suzerain chain. Answers for a concluded war too, from the sides it was declared with. False
    /// when no war with that id exists.
    /// </summary>
    bool IsOnAttackerSide(string warId, string clanId);

    /// <summary>
    /// Whether the attacker side of a war with these sides could besiege the main goal today: some
    /// party on it can lead a private-war army that beats the goal's walled defence. Check before
    /// <see cref="DeclareWar"/> so a clan does not declare a war it cannot fight. The attacker may be
    /// a clan that does not exist yet, in which case only its supporters are counted.
    /// </summary>
    bool CanProsecute(
        string attackerClanId,
        string defenderClanId,
        string mainGoalSettlementId,
        IReadOnlyCollection<string> attackerSupporters,
        IReadOnlyCollection<string> defenderSupporters);

    /// <summary>
    /// Resolves an existing private war under the given outcome. Returns the resolved war snapshot,
    /// or null if no war with that id exists.
    /// </summary>
    PrivateWarInfo? ResolveWar(string warId, PrivateWarOutcome outcome);

    event Action<PrivateWarInfo> WarDeclared;

    /// <summary>
    /// Raised for every war the mechanism concludes, whether through <see cref="ResolveWar"/> or on
    /// its own daily tick, carrying the outcome it was resolved under.
    /// </summary>
    event Action<PrivateWarInfo, PrivateWarOutcome> WarResolved;

    event Action HostilityChanged;
}
