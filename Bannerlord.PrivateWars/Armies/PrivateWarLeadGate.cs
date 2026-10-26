using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PrivateWars.Armies
{
    /// <summary>
    /// Vanilla's CanLordCreateArmy leader conditions, minus "the realm is at war": a feud is fought
    /// whether or not the crown is.
    /// </summary>
    public static class PrivateWarLeadGate
    {
        [Flags]
        private enum LeadBlockers
        {
            None = 0,
            NoLeader = 1 << 0,
            InArmy = 1 << 1,
            NoKingdom = 1 << 2,
            AtSea = 1 << 3,
            LowInfluence = 1 << 4,
            Mercenary = 1 << 5,
            LowFood = 1 << 6,
            SmallParty = 1 << 7,
            NotClanLeader = 1 << 8
        }

        public static bool CanLead(MobileParty party) => GetLeadBlockers(party, stopAtFirst: true) == LeadBlockers.None;

        // Names every failed lead condition for the daily trace, plus inBattle, which only delays a lead.
        public static string Describe(MobileParty party)
        {
            var found = GetLeadBlockers(party, stopAtFirst: false);
            var clan = party.LeaderHero?.Clan;
            var blockers = new List<string>();
            if (found.HasFlag(LeadBlockers.NoLeader)) blockers.Add("noLeader");
            if (found.HasFlag(LeadBlockers.InArmy)) blockers.Add("inArmy");
            if (found.HasFlag(LeadBlockers.NoKingdom)) blockers.Add("noKingdom");
            if (found.HasFlag(LeadBlockers.AtSea)) blockers.Add("atSea");
            if (found.HasFlag(LeadBlockers.LowInfluence)) blockers.Add($"influence={clan!.Influence:0}");
            if (found.HasFlag(LeadBlockers.Mercenary)) blockers.Add("mercenary");
            if (found.HasFlag(LeadBlockers.LowFood)) blockers.Add($"food={party.GetNumDaysForFoodToLast()}d");
            if (found.HasFlag(LeadBlockers.SmallParty)) blockers.Add($"sizeRatio={party.PartySizeRatio:0.##}");
            if (party.MapEvent != null) blockers.Add("inBattle");
            if (found.HasFlag(LeadBlockers.NotClanLeader)) blockers.Add("notClanLeader");
            return blockers.Count == 0 ? "none" : string.Join("|", blockers);
        }

        // The single source of the lead conditions, shared by the AI gate (stops at the first, in the
        // cheap-first order) and the daily trace (collects them all).
        private static LeadBlockers GetLeadBlockers(MobileParty party, bool stopAtFirst)
        {
            var models = TaleWorlds.CampaignSystem.Campaign.Current.Models;
            var leaderHero = party.LeaderHero;
            var clan = leaderHero?.Clan;
            var blockers = LeadBlockers.None;
            bool Block(LeadBlockers blocker)
            {
                blockers |= blocker;
                return stopAtFirst;
            }

            if (clan is null && Block(LeadBlockers.NoLeader)) return blockers;
            if (party.Army != null && Block(LeadBlockers.InArmy)) return blockers;
            if (party.MapFaction is not Kingdom && Block(LeadBlockers.NoKingdom)) return blockers;
            if (party.IsCurrentlyAtSea && Block(LeadBlockers.AtSea)) return blockers;
            if (clan is not null && clan.Influence <= 100f && Block(LeadBlockers.LowInfluence)) return blockers;
            if (clan is not null && clan.IsUnderMercenaryService && Block(LeadBlockers.Mercenary)) return blockers;
            if (party.GetNumDaysForFoodToLast() <= models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege
                && Block(LeadBlockers.LowFood)) return blockers;
            if (party.PartySizeRatio <= models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy
                && Block(LeadBlockers.SmallParty)) return blockers;

            bool isClanLead = clan is not null
                && (clan.Leader == leaderHero
                    || clan.Leader?.PartyBelongedTo is null
                    && clan.WarPartyComponents != null
                    && clan.WarPartyComponents.FirstOrDefault() == party.WarPartyComponent);
            if (clan is not null && !isClanLead) Block(LeadBlockers.NotClanLeader);
            return blockers;
        }
    }
}
