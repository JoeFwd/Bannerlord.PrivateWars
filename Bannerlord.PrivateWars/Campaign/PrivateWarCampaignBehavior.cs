using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.Spi;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Microsoft.Extensions.Logging;

namespace Bannerlord.PrivateWars.Campaign
{
    /**
     * <summary>
     *  Owns the registry lifecycle. Persists active and concluded wars through Bannerlord saves and
     *  restores them on load; each day advances every active war's score from current world state and,
     *  once a side crosses ±100, resolves it: reverting captured fiefs to their pre-war owners (status
     *  quo ante) and leaving the attacker its goal as the prize. Prosecution itself is vanilla's: the
     *  feud's fiefs are fed to its siege planner (PrivateWarSiegeTargetsPatch) and its army call list
     *  (PrivateWarArmyManagementCalculationModel).
     * </summary>
     */
    public class PrivateWarCampaignBehavior : CampaignBehaviorBase
    {
        private readonly IPrivateWarRepository _privateWars;
        private readonly ITickPrivateWarUseCase _tickPrivateWar;
        private readonly IPrivateWarSidesProvider _sides;
        private readonly IPrivateWarHostility _hostility;
        private readonly PrivateWarObservationsReader _observations;
        private readonly PrivateWarFronts _fronts;
        private readonly WarConclusionAdapter _warConclusion;
        private readonly KingdomSplitWarAdapter _kingdomSplit;
        private readonly ILogger<PrivateWarCampaignBehavior> _logger;

        private List<string> _serialisedWars = new();

        public PrivateWarCampaignBehavior(
            IPrivateWarRepository privateWars,
            ITickPrivateWarUseCase tickPrivateWar,
            IPrivateWarSidesProvider sides,
            IPrivateWarHostility hostility,
            PrivateWarObservationsReader observations,
            PrivateWarFronts fronts,
            WarConclusionAdapter warConclusion,
            KingdomSplitWarAdapter kingdomSplit,
            ILogger<PrivateWarCampaignBehavior> logger)
        {
            _privateWars = privateWars;
            _tickPrivateWar = tickPrivateWar;
            _sides = sides;
            _hostility = hostility;
            _observations = observations;
            _fronts = fronts;
            _warConclusion = warConclusion;
            _kingdomSplit = kingdomSplit;
            _logger = logger;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                _serialisedWars = PrivateWarStateSerialiser.SerialiseWars(_privateWars.GetAll());
            }

            dataStore.SyncData("DadgPrivateWars", ref _serialisedWars);

            _serialisedWars ??= new List<string>();
        }

        private void OnGameLoaded(CampaignGameStarter campaignGameStarter)
        {
            _privateWars.Initialise(PrivateWarStateSerialiser.DeserialiseWars(_serialisedWars));
        }

        // Once a day, advance every active war: recompute its signed score from current world state and,
        // when a side crosses the +/-100 threshold, resolve it. The score calculator's fatigue term
        // drifts toward whoever holds the main goal, so the war terminates even in a stalemate.
        private void OnDailyTick()
        {
            ReleaseFeudLordsFromForeignArmies();
            float currentDay = (float)CampaignTime.Now.ToDays;

            foreach (var war in _privateWars.GetAll())
            {
                if (war.Status != PrivateWarStatus.Active) continue;

                var result = _tickPrivateWar.Execute(war, _observations.Read(war), currentDay);
                var scored = war with { Score = result.Score };

                if (result.Outcome is { } outcome)
                    _warConclusion.Conclude(scored, outcome);
                else
                    _privateWars.Update(scored);
            }
        }

        // A lord already in a realm army when it took up its feud, or one serving under a feud enemy, is
        // pulled out so it can follow its front; vanilla's army model then only calls it to its own side.
        // Player-led armies are left alone.
        private void ReleaseFeudLordsFromForeignArmies()
        {
            foreach (var party in MobileParty.AllLordParties.ToList())
            {
                var army = party.Army;
                var clan = party.ActualClan;
                var leader = army?.LeaderParty;
                if (leader is null || leader == party || leader.IsMainParty || clan is null || party.MapEvent != null) continue;

                bool servesFeudEnemy = leader.ActualClan is { } leaderClan && _hostility.AreEnemies(clan.StringId, leaderClan.StringId);
                bool leftItsFront = _fronts.FindPrivateFront(party) is { } front && !_fronts.IsSameFront(front, leader);
                if (servesFeudEnemy || leftItsFront)
                    party.Army = null;
            }
        }

        // A war goal changing sides - by our capture path or any engine path - resets the fatigue epoch:
        // the accumulated drift toward the previous holder is wiped and the contest restarts its climb
        // toward the new holder. A move between clans of the same side (a kingdom grant, a gift between
        // allies) leaves it alone.
        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (settlement is null) return;

            var oldClanId = oldOwner?.Clan?.StringId;
            var newClanId = newOwner?.Clan?.StringId;

            foreach (var war in _privateWars.GetAll())
            {
                if (war.Status != PrivateWarStatus.Active || war.MainGoalSettlementId != settlement.StringId) continue;

                var oldSide = oldClanId is null ? null : _sides.ResolveSide(oldClanId, war);
                var newSide = newClanId is null ? null : _sides.ResolveSide(newClanId, war);
                if (oldSide == newSide) continue;

                _privateWars.Update(war with { GoalLastTakenDay = (float)CampaignTime.Now.ToDays });
            }
        }

        private void OnClanChangedKingdom(
            Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            _logger.LogDebug("[PW] realm change {Clan}: {Old} -> {New} detail={Detail} relationWithOldRuler={Relation}\n{Stack}",
                clan.StringId, oldKingdom?.StringId, newKingdom?.StringId, detail,
                oldKingdom?.Leader is { } oldRuler && clan.Leader != null ? clan.Leader.GetRelation(oldRuler) : (int?)null,
                new System.Diagnostics.StackTrace(1, false));

            _kingdomSplit.ConcludeWarsSplitBy(clan);
        }
    }
}
