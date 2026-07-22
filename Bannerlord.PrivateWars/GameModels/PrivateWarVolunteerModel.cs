using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace Bannerlord.PrivateWars.GameModels
{
    public class PrivateWarVolunteerModel : DefaultVolunteerModel
    {
        private readonly PrivateWarInteractionPolicy _interactionPolicy;

        public PrivateWarVolunteerModel(PrivateWarInteractionPolicy interactionPolicy)
        {
            _interactionPolicy = interactionPolicy;
        }

        public override int MaximumIndexHeroCanRecruitFromHero(
            Hero buyerHero,
            Hero sellerHero,
            int useValueAsRelation = -101)
        {
            var vanillaMaximumIndex = base.MaximumIndexHeroCanRecruitFromHero(
                buyerHero,
                sellerHero,
                useValueAsRelation);
            var ownerClan = sellerHero.CurrentSettlement?.OwnerClan;
            var arePrivateEnemies = PrivateWarSiegeDefenderPolicy.AreEnemies(buyerHero.Clan, ownerClan);

            return _interactionPolicy.RestrictRecruitableIndex(
                arePrivateEnemies,
                vanillaMaximumIndex);
        }
    }
}
