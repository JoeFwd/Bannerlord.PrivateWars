namespace Bannerlord.PrivateWars.Domain.Model
{
    /// <summary>
    /// A flattened view of a settlement fed into <see cref="MainGoalSelector"/>: its current
    /// de facto owner, whether it is a town (vs. castle), and its prosperity. OwnerClanId is
    /// null when the settlement is unowned.
    /// </summary>
    public record SettlementInfo(
        string Id,
        string? OwnerClanId,
        bool IsTown,
        float Prosperity);
}
