using System.Collections.Generic;

namespace Bannerlord.PrivateWars.Domain.Model
{
    /// <summary>
    /// A single manufactured clan-vs-clan war pressed under one casus belli. The two
    /// principals are the claimant (attacker) and the title-holder (defender); each fights
    /// with its whole vassal subtree, resolved dynamically from the feudal hierarchy (design
    /// §18.A). AttackerSupporters/DefenderSupporters are the clans that joined a side *without*
    /// being in either principal's subtree; they are stored because nothing in the hierarchy can
    /// re-derive them.
    ///
    /// MainGoalSettlementId is frozen at declaration (design §18.C). OriginalFiefOwners is the
    /// settlement-to-owner snapshot taken at declaration, used to revert captures on resolution
    /// (status quo ante, design §8). BattleScore is the one *accumulated* score term (capped,
    /// attacker-positive); every other score term is recomputed from current world state each
    /// tick. Score is the last persisted total (for save/load and UI).
    ///
    /// GoalLastTakenDay is the epoch the fatigue term drifts from: it starts at declaration and
    /// resets to the current day whenever the main goal changes hands, so retaking the goal wipes
    /// the accumulated fatigue and the contest restarts its drift toward the new holder.
    /// </summary>
    public record PrivateWar(
        string Id,
        string AttackerPrincipalClanId,
        string DefenderPrincipalClanId,
        IReadOnlyCollection<string> AttackerSupporters,
        IReadOnlyCollection<string> DefenderSupporters,
        string CasusBelliType,
        string TitleId,
        string MainGoalSettlementId,
        IReadOnlyDictionary<string, string> OriginalFiefOwners,
        float BattleScore,
        float Score,
        float StartDay,
        float GoalLastTakenDay,
        PrivateWarStatus Status);
}
