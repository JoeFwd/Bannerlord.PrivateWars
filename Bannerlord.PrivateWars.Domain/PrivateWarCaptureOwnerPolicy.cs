using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Which clan owns a fief taken by a private-war siege. The capturer is the interim owner while the
    /// war lasts - it still scores for its side, and the war's conclusion hands the goal to the claimant.
    /// The one exception is the main goal taken by an attacker army the claimant marches in without
    /// leading it: there is no reason to make the claimant wait, so the goal goes to it straight away.
    /// The title never moves with an interim owner, since a siege only occupies a held seat.
    /// </summary>
    public static class PrivateWarCaptureOwnerPolicy
    {
        public static string ChooseOwnerClanId(
            PrivateWar war,
            string settlementId,
            WarSide besiegerSide,
            string besiegerClanId,
            bool claimantInArmy)
            => claimantInArmy && besiegerSide == WarSide.Attacker && settlementId == war.MainGoalSettlementId
                ? war.AttackerPrincipalClanId
                : besiegerClanId;
    }
}
