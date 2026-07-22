namespace Bannerlord.PrivateWars.Domain.Model
{
    /// <summary>
    /// On attacker victory, the claimed dignity and its seat transfer to the attacker principal.
    /// Applied by the integration layer via the existing title-assignment path (design §8/§18.B).
    /// </summary>
    public record PrizeAward(
        string SettlementId,
        string TitleId,
        string NewHolderClanId);
}
