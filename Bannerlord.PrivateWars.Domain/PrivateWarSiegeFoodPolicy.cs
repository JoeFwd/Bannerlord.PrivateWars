namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Food factor for a private-war siege bid, mirroring AiMilitaryBehavior.GetFoodScoreForActionType.
    /// Vanilla scales every siege bid down as the army runs short of food so its buy-food settlement
    /// visits can win the behaviour vote; a bid that ignored food kept a hungry war army marching on,
    /// or camped at, the goal until its members starved and left.
    /// </summary>
    public class PrivateWarSiegeFoodPolicy
    {
        /// <param name="foodDays">Average days of food across the army (or the party alone).</param>
        /// <param name="neededFoodDays">Vanilla's NeededFoodsInDaysThresholdForSiege.</param>
        /// <param name="willGatherArmy">A bid to raise an army, which vanilla refuses outright when short.</param>
        /// <returns>0 when the bid must not be made, otherwise a weight in [0.1, 1].</returns>
        public float GetScoreFactor(int foodDays, float neededFoodDays, bool willGatherArmy)
        {
            if (foodDays >= neededFoodDays) return 1f;
            if (willGatherArmy) return 0f;

            return 0.1f + 0.9f * foodDays / neededFoodDays;
        }
    }
}
