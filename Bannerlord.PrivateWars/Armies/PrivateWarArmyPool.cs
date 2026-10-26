using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.PrivateWars.Armies
{
    /// <summary>
    /// Vanilla's DefaultArmyManagementCalculationModel.CanLordCreateArmy for a feud army: the same
    /// leader and member gates, priority order, 70% call cap and 1000 siege-strength floor, but the
    /// call list is only the lords fighting on the leader's side of the feud, and the realm need not
    /// be at war.
    /// </summary>
    public static class PrivateWarArmyPool
    {
        private const float MinimumArmySiegeStrength = 1000f;

        public static MBList<MobileParty> Gather(MobileParty leaderParty, Func<MobileParty, bool> isOnSide)
        {
            var members = new MBList<MobileParty>();
            if (!PrivateWarLeadGate.CanLead(leaderParty) || leaderParty.MapFaction is not Kingdom kingdom) return members;

            var models = TaleWorlds.CampaignSystem.Campaign.Current.Models;
            var disbandBehaviour = TaleWorlds.CampaignSystem.Campaign.Current.GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
            var eligible = new List<(MobileParty Party, float Priority)>();
            foreach (WarPartyComponent component in kingdom.WarPartyComponents)
            {
                var party = component.MobileParty;
                var partyLeader = party.LeaderHero;
                if (!party.IsLordParty
                    || party.Army != null
                    || party == leaderParty
                    || partyLeader == null
                    || party.IsMainParty
                    || partyLeader == partyLeader.MapFaction.Leader
                    || party.Ai.DoNotMakeNewDecisions
                    || party.CurrentSettlement?.SiegeEvent != null
                    || party.IsDisbanding
                    || party.GetNumDaysForFoodToLast() <= models.ArmyManagementCalculationModel.MinimumNeededFoodInDaysToCallToArmy
                    || party.PartySizeRatio <= models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy
                    || !partyLeader.CanLeadParty()
                    || party.IsInRaftState
                    || party.MapEvent != null
                    || party.BesiegedSettlement != null
                    || disbandBehaviour?.IsPartyWaitingForDisband(party) == true
                    || DistanceHelper.GetDistanceBetweenMobilePartyToMobileParty(party, leaderParty, party.NavigationCapability, out _)
                        >= models.ArmyManagementCalculationModel.MaximumDistanceToCallToArmy
                    || !isOnSide(party))
                {
                    continue;
                }

                int influenceCost = models.ArmyManagementCalculationModel.CalculatePartyInfluenceCost(leaderParty, party);
                float unwoundedRatio = 1f - (float)party.Party.MemberRoster.TotalWounded / party.Party.MemberRoster.TotalManCount;
                eligible.Add((party, party.Party.EstimatedStrength / (influenceCost + 0.1f) * unwoundedRatio));
            }

            int cap = MathF.Ceiling(kingdom.WarPartyComponents.Count * 0.7f - kingdom.Armies.Sum(army => army.Parties.Count));
            members.AddRange(eligible.OrderBy(entry => entry.Priority).Take(Math.Max(0, cap)).Select(entry => entry.Party));

            if (SiegeStrength(leaderParty) + members.Sum(SiegeStrength) < MinimumArmySiegeStrength) members.Clear();
            return members;
        }

        public static float SiegeStrength(MobileParty party)
            => party.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
    }
}
