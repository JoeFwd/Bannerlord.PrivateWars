using Bannerlord.PrivateWars.Armies;
using Bannerlord.PrivateWars.Campaign;
using Bannerlord.PrivateWars.DI;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Bannerlord.PrivateWars.GameModels
{
    // Who an army leader calls (design §4.1). A lord prosecuting a feud calls only the lords fighting on
    // the same front, through vanilla's own gates (PrivateWarArmyPool). Any other army is the realm's: it
    // never calls a feud enemy of its leader, nor a lord busy prosecuting a feud.
    public class PrivateWarArmyManagementCalculationModel : DefaultArmyManagementCalculationModel
    {
        private readonly PrivateWarFronts _fronts;

        public PrivateWarArmyManagementCalculationModel(PrivateWarFronts fronts)
        {
            _fronts = fronts;
        }

        public override bool CanLordCreateArmy(MobileParty leaderParty, out MBList<MobileParty> possibleArmyMembers)
        {
            var front = _fronts.FindPrivateFront(leaderParty);
            if (front is not null)
            {
                possibleArmyMembers = PrivateWarArmyPool.Gather(leaderParty, party => _fronts.IsSameFront(front, party));
                return possibleArmyMembers.Count > 0;
            }

            var canCreateArmy = base.CanLordCreateArmy(leaderParty, out possibleArmyMembers);
            var leaderClan = leaderParty.LeaderHero?.Clan;
            var hostility = PrivateWarsServices.PrivateWarHostility;
            if (!canCreateArmy || leaderClan is null || !PrivateWarsServices.IsInitialised || hostility is null) return canCreateArmy;

            possibleArmyMembers.RemoveAll(party =>
                party.ActualClan is { } clan && hostility.AreEnemies(leaderClan.StringId, clan.StringId)
                || _fronts.FindPrivateFront(party) is not null);
            // shortcut: vanilla's 1000 siege-strength floor is not re-checked after removal; re-check it if realm armies too weak to siege show up.
            return possibleArmyMembers.Count > 0;
        }
    }
}
