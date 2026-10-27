using System.Linq;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.DI;
using Bannerlord.PrivateWars.Patches;
using Microsoft.Extensions.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using CampaignState = TaleWorlds.CampaignSystem.Campaign;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// Bannerlord 1.4.6 sally-out behavior expressed through public campaign events and actions.
    /// Ordinary sieges retain vanilla faction classification; private sieges classify nearby parties
    /// by their clan side before accumulating strength.
    /// </summary>
    public sealed class DadgSallyOutCampaignBehavior : CampaignBehaviorBase
    {
        private readonly PrivateWarSallyOutPolicy _policy;
        private readonly ILogger<DadgSallyOutCampaignBehavior> _logger;

        public DadgSallyOutCampaignBehavior(PrivateWarSallyOutPolicy policy, ILogger<DadgSallyOutCampaignBehavior> logger)
        {
            _policy = policy;
            _logger = logger;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickSettlementEvent.AddNonSerializedListener(this, HourlyTickSettlement);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (defenderParty.SiegeEvent != null)
                CheckForSettlementSallyOut(defenderParty.SiegeEvent.BesiegedSettlement);
        }

        public void HourlyTickSettlement(Settlement settlement)
        {
            CheckForSettlementSallyOut(settlement);
        }

        private void CheckForSettlementSallyOut(Settlement settlement)
        {
            if (!settlement.IsFortification ||
                settlement.SiegeEvent == null ||
                settlement.Party.MapEvent != null ||
                settlement.Town.GarrisonParty == null ||
                settlement.Town.GarrisonParty.MapEvent != null)
                return;

            var leaderMapEvent = settlement.SiegeEvent.BesiegerCamp.LeaderParty.MapEvent;
            var activeOutsideBattle = leaderMapEvent != null &&
                                      (leaderMapEvent.IsSiegeOutside || leaderMapEvent.IsBlockade);
            if (!activeOutsideBattle && MathF.Floor(CampaignTime.Now.ToHours) % 4 != 0)
                return;

            if (Hero.MainHero.CurrentSettlement == settlement &&
                CampaignState.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(
                    settlement.SiegeEvent, BattleSideEnum.Defender) == Hero.MainHero)
                return;

            CheckSallyOut(settlement, checkForNavalSallyOut: false, out var salliedOut);
            if (!salliedOut && settlement.HasPort && settlement.SiegeEvent.IsBlockadeActive)
                CheckSallyOut(settlement, checkForNavalSallyOut: true, out _);
        }

        private void CheckSallyOut(Settlement settlement, bool checkForNavalSallyOut, out bool salliedOut)
        {
            salliedOut = false;
            var leaderParty = settlement.SiegeEvent.BesiegerCamp.LeaderParty;
            var siegeOutside = false;
            var blockade = false;
            if (leaderParty.MapEvent != null)
            {
                siegeOutside = leaderParty.MapEvent.IsSiegeOutside;
                blockade = leaderParty.MapEvent.IsBlockade;
            }

            if ((blockade && !checkForNavalSallyOut) || (siegeOutside && checkForNavalSallyOut))
                return;

            var besiegerStrength = 0f;
            var nearbySettlementStrength = 0f;
            var sallyParties = settlement.GetInvolvedPartiesForEventType(checkForNavalSallyOut
                ? MapEvent.BattleTypes.BlockadeSallyOutBattle
                : MapEvent.BattleTypes.SallyOut);
            var sallyContext = checkForNavalSallyOut
                ? MapEvent.PowerCalculationContext.SeaBattle
                : MapEvent.PowerCalculationContext.PlainBattle;
            var settlementStrength = sallyParties.Sum(party => party.GetCustomStrength(BattleSideEnum.Attacker, sallyContext));
            var sallyMorale = SideMorale(sallyParties, sallyContext);

            var ownerClan = settlement.OwnerClan;
            var privateWarSiege = PrivateWarSiegeDefenderPolicy.AreEnemies(
                leaderParty.ActualClan, ownerClan);
            var search = MobileParty.StartFindingLocatablesAroundPosition(
                leaderParty.Position.ToVec2(),
                CampaignState.Current.Models.EncounterModel.GetEncounterJoiningRadius);

            for (var nearby = MobileParty.FindNextLocatable(ref search);
                 nearby != null;
                 nearby = MobileParty.FindNextLocatable(ref search))
            {
                if (nearby.CurrentSettlement != null || nearby.Aggressiveness <= 0f) continue;

                var aggressiveness = nearby.Aggressiveness > 0.5f
                    ? 1f
                    : nearby.Aggressiveness * 2f;
                var side = _policy.ClassifyNearbyParty(
                    nearby.MapFaction.IsAtWarWith(settlement.Party.MapFaction),
                    PrivateWarSiegeDefenderPolicy.AreEnemies(nearby.ActualClan, ownerClan),
                    IsOnSettlementPrivateWarSide(nearby.ActualClan, ownerClan),
                    privateWarSiege,
                    nearby.MapFaction == settlement.MapFaction,
                    checkForNavalSallyOut == nearby.IsCurrentlyAtSea);

                if (side == SallyOutPartySide.Besieger)
                {
                    besiegerStrength += aggressiveness * nearby.Party.GetCustomStrength(
                        BattleSideEnum.Defender,
                        checkForNavalSallyOut
                            ? MapEvent.PowerCalculationContext.SeaBattle
                            : MapEvent.PowerCalculationContext.PlainBattle);
                }
                else if (side == SallyOutPartySide.Settlement)
                {
                    nearbySettlementStrength += aggressiveness * nearby.Party.GetCustomStrength(
                        BattleSideEnum.Attacker,
                        checkForNavalSallyOut
                            ? MapEvent.PowerCalculationContext.SeaBattle
                            : MapEvent.PowerCalculationContext.PlainBattle);
                }
            }

            if (!_policy.ShouldSally(
                    settlementStrength + nearbySettlementStrength,
                    besiegerStrength,
                    siegeOutside || blockade,
                    sallyMorale))
                return;

            _logger.LogDebug("[PW] {Settlement} sallies: strength={Strength:0} besieger={Besieger:0} morale={Morale:0.#} naval={Naval}",
                settlement.StringId, settlementStrength + nearbySettlementStrength, besiegerStrength, sallyMorale, checkForNavalSallyOut);

            if (siegeOutside || blockade)
            {
                foreach (var party in settlement.GetInvolvedPartiesForEventType(
                             checkForNavalSallyOut
                                 ? MapEvent.BattleTypes.BlockadeSallyOutBattle
                                 : MapEvent.BattleTypes.SallyOut))
                {
                    if (!party.IsMobile || party.NumberOfHealthyMembers <= 0 ||
                        party.MobileParty.IsMainParty || party.MapEventSide != null)
                        continue;

                    if (leaderParty.MapEvent == null) break;
                    party.MapEventSide = leaderParty.MapEvent.AttackerSide;
                }
            }
            else
            {
                if (checkForNavalSallyOut)
                    settlement.Town.GarrisonParty.SetTargetSettlement(settlement, isTargetingPort: true);
                EncounterManager.StartPartyEncounter(settlement.Town.GarrisonParty.Party, leaderParty.Party);
            }

            salliedOut = true;
        }

        // MapEventSide.GetSideMorale: strength-weighted morale of the side's mobile parties.
        // shortcut: counts only the sallying parties, not a relief army already fighting outside.
        private static float SideMorale(System.Collections.Generic.IEnumerable<PartyBase> parties, MapEvent.PowerCalculationContext context)
        {
            float strength = 0f, weighted = 0f;
            foreach (var party in parties)
            {
                if (!party.IsMobile) continue;
                var partyStrength = party.GetCustomStrength(BattleSideEnum.Attacker, context);
                strength += partyStrength;
                weighted += partyStrength * party.MobileParty.Morale;
            }

            return strength > 0f ? weighted / strength : 0f;
        }

        private static bool IsOnSettlementPrivateWarSide(Clan candidate, Clan owner)
        {
            if (candidate == null || owner == null) return false;
            if (candidate == owner) return true;
            return PrivateWarsServices.IsInitialised &&
                   PrivateWarsServices.PrivateWarHostility?.AreAllies(candidate.StringId, owner.StringId) == true;
        }
    }
}
