namespace Bannerlord.PrivateWars.Domain.Model
{
    /// <summary>
    /// On attacker victory, the claimed dignity and its seat transfer to the attacker principal.
    /// Applied by the integration layer via the existing title-assignment path (design §8/§18.B).
    /// HandOverSettlement is set when another clan of the attacker's side holds the seat: the
    /// integration layer then moves it to the principal, who fought the war for it.
    /// </summary>
    public record PrizeAward(
        string SettlementId,
        string TitleId,
        string NewHolderClanId,
        bool HandOverSettlement);
}
