namespace Bannerlord.PrivateWars.Domain
{
    public readonly struct PrivateWarVillageActionDecision
    {
        public PrivateWarVillageActionDecision(bool isVisible, bool isEnabled)
        {
            IsVisible = isVisible;
            IsEnabled = isEnabled;
        }

        public bool IsVisible { get; }
        public bool IsEnabled { get; }
    }

    public sealed class PrivateWarVillageActionPolicy
    {
        public PrivateWarVillageActionDecision EvaluateHostileAction(
            bool vanillaVisible,
            bool vanillaEnabled,
            bool isVillage,
            bool isNormalVillage,
            bool canLeadArmyAction,
            bool arePrivateEnemies)
        {
            bool privateWarVisible = isVillage
                                     && isNormalVillage
                                     && canLeadArmyAction
                                     && arePrivateEnemies;

            return new PrivateWarVillageActionDecision(
                vanillaVisible || privateWarVisible,
                vanillaEnabled);
        }

        public PrivateWarVillageActionDecision EvaluateRaid(
            bool vanillaVisible,
            bool vanillaEnabled,
            bool isVillage,
            bool arePrivateEnemies)
        {
            return new PrivateWarVillageActionDecision(
                vanillaVisible || (isVillage && arePrivateEnemies),
                vanillaEnabled);
        }
    }
}
