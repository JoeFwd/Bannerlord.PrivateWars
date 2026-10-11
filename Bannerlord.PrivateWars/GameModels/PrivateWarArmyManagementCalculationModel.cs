using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Armies;
using Bannerlord.PrivateWars.DI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.PrivateWars.GameModels
{
    // Keeps a private-war enemy out of the belligerent's army candidate pool (design §4.1).
    //
    // The base builds its call list from leaderParty.MapFaction.WarPartyComponents — the whole
    // kingdom — so a same-kingdom feud enemy (C) is auto-invited into the attacker's (A's) army,
    // which is nonsensical while the two are at private war. Drop any candidate whose clan is at
    // private war with the army leader's clan; everyone else is returned unchanged (stock answer
    // when no war, design's pair-scoped contract).
    //
    // This is the army-leader's "who do I invite" decision, not a member's "should I quit" decision
    // (the latter stays a scored AI vote in AiArmyMemberBehavior / PrivateWarCampaignBehavior).
    public class PrivateWarArmyManagementCalculationModel : DefaultArmyManagementCalculationModel
    {
        private readonly PrivateWarArmyDecisionAdapter _armyDecisionAdapter;

        public PrivateWarArmyManagementCalculationModel(PrivateWarArmyDecisionAdapter armyDecisionAdapter)
        {
            _armyDecisionAdapter = armyDecisionAdapter;
        }

        public override bool CanLordCreateArmy(
            MobileParty leaderParty,
            out MBList<MobileParty> possibleArmyMembers)
        {
            var canCreateArmy = base.CanLordCreateArmy(leaderParty, out possibleArmyMembers);

            var leaderClan = leaderParty.LeaderHero?.Clan;
            if (leaderClan is null || !PrivateWarsServices.IsInitialised || PrivateWarsServices.PrivateWarHostility is null)
                return canCreateArmy;

            var candidates = possibleArmyMembers.Select(party => new PrivateWarArmyCandidate(
                party.StringId,
                party.ActualClan?.StringId ?? string.Empty,
                CanLeadArmy: false,
                CanJoinArmy: true,
                SiegeStrength: party.Party.GetCustomStrength(
                    BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege),
                MemberDesirability: 0f)).ToList();
            var kingdom = leaderParty.MapFaction as Kingdom;
            var decision = _armyDecisionAdapter.FilterOrdinaryMembers(
                canCreateArmy,
                leaderClan.StringId,
                leaderParty.Party.GetCustomStrength(
                    BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege),
                kingdom?.Settlements.Count > 0,
                candidates,
                PrivateWarsServices.PrivateWarHostility.AreEnemies,
                PrivateWarsServices.PrivateWarHostility.IsBelligerent);

            if (!decision.CanCreateArmy)
            {
                possibleArmyMembers.Clear();
                return false;
            }

            var allowedPartyIds = new HashSet<string>(decision.MemberPartyIds);
            possibleArmyMembers.RemoveAll(party => !allowedPartyIds.Contains(party.StringId));

            return true;
        }
    }
}
