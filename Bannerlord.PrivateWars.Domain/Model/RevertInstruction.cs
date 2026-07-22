namespace Bannerlord.PrivateWars.Domain.Model
{
    /// <summary>
    /// Restore one captured settlement to its pre-war owner (status quo ante, design §8).
    /// </summary>
    public record RevertInstruction(
        string SettlementId,
        string RevertToClanId);
}
