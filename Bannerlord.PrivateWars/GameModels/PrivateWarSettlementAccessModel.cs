using Bannerlord.PrivateWars.DI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Bannerlord.PrivateWars.GameModels
{
    // Treat a same-kingdom private-war rival's town or castle as hostile (no entry) for the player.
    //
    // DefaultSettlementAccessModel.CanMainHeroEnterSettlement grants FullAccess to same-faction
    // settlements via DiplomacyHelper.IsSameFactionAndNotEliminated — two clans in the same kingdom
    // share a MapFaction, so the player walks unchallenged into the rival's town/castle. Override to
    // return NoAccess/HostileFaction when the settlement's owner clan is a registered private-war
    // enemy of the player's clan. All non-enemy settlements fall through to the wrapped model unchanged.
    public class PrivateWarSettlementAccessModel : SettlementAccessModel
    {
        private readonly SettlementAccessModel _inner;

        public PrivateWarSettlementAccessModel(SettlementAccessModel inner)
        {
            _inner = inner;
        }

        public override void CanMainHeroEnterSettlement(Settlement settlement, out AccessDetails accessDetails)
        {
            if (settlement.IsFortification
                && IsPrivateWarEnemy(settlement.OwnerClan))
            {
                accessDetails = new AccessDetails
                {
                    AccessLevel = AccessLevel.NoAccess,
                    AccessMethod = AccessMethod.ByRequest,
                    AccessLimitationReason = AccessLimitationReason.HostileFaction
                };
                return;
            }

            _inner.CanMainHeroEnterSettlement(settlement, out accessDetails);
        }

        public override void CanMainHeroEnterLordsHall(Settlement settlement, out AccessDetails accessDetails) =>
            _inner.CanMainHeroEnterLordsHall(settlement, out accessDetails);

        public override void CanMainHeroEnterDungeon(Settlement settlement, out AccessDetails accessDetails) =>
            _inner.CanMainHeroEnterDungeon(settlement, out accessDetails);

        public override bool CanMainHeroAccessLocation(Settlement settlement, string locationId,
            out bool disableOption, out TextObject disabledText) =>
            _inner.CanMainHeroAccessLocation(settlement, locationId, out disableOption, out disabledText);

        public override bool CanMainHeroDoSettlementAction(Settlement settlement, SettlementAction settlementAction,
            out bool disableOption, out TextObject disabledText) =>
            _inner.CanMainHeroDoSettlementAction(settlement, settlementAction, out disableOption, out disabledText);

        public override bool IsRequestMeetingOptionAvailable(Settlement settlement,
            out bool disableOption, out TextObject disabledText) =>
            _inner.IsRequestMeetingOptionAvailable(settlement, out disableOption, out disabledText);

        private static bool IsPrivateWarEnemy(Clan? ownerClan)
        {
            if (!PrivateWarsServices.IsInitialised || PrivateWarsServices.PrivateWarHostility is null) return false;
            var playerClan = Hero.MainHero?.Clan;
            if (playerClan is null || ownerClan is null) return false;
            return PrivateWarsServices.PrivateWarHostility.AreEnemies(playerClan.StringId, ownerClan.StringId);
        }
    }
}
