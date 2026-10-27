using System.Collections.Generic;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.Util;
using Microsoft.Extensions.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// When a battle resolves between the two sides of an active private war, bumps that war's
    /// accumulated battle score (the sole mutator of BattleScore, design §18.B).
    /// </summary>
    public class PrivateWarBattleScoreCampaignBehavior : CampaignBehaviorBase
    {
        private readonly IPrivateWarRepository _privateWars;
        private readonly IApplyBattleOutcomeUseCase _applyBattleOutcome;
        private readonly IPrivateWarSidesProvider _sides;
        private readonly ILogger<PrivateWarBattleScoreCampaignBehavior> _logger;

        public PrivateWarBattleScoreCampaignBehavior(
            IPrivateWarRepository privateWars,
            IApplyBattleOutcomeUseCase applyBattleOutcome,
            IPrivateWarSidesProvider sides,
            ILogger<PrivateWarBattleScoreCampaignBehavior> logger)
        {
            _privateWars = privateWars;
            _applyBattleOutcome = applyBattleOutcome;
            _sides = sides;
            _logger = logger;
        }

        public override void RegisterEvents()
            => CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);

        public override void SyncData(IDataStore dataStore)
        {
        }

        // The bump is the share of the losing side's total strength destroyed, signed by the winner. Fires
        // at WaitingRemoval, before parties are finalized, so the sides' casualties and strength are intact.
        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent is null || mapEvent.WinningSide == BattleSideEnum.None) return;

            foreach (var war in _privateWars.GetAll())
            {
                if (war.Status != PrivateWarStatus.Active) continue;

                var attackerBattleSide = ResolveBattleSide(mapEvent.AttackerSide, war);
                var defenderBattleSide = ResolveBattleSide(mapEvent.DefenderSide, war);
                if (attackerBattleSide is null || defenderBattleSide is null) continue;
                if (attackerBattleSide == defenderBattleSide) continue;

                var winnerWarSide = mapEvent.WinningSide == BattleSideEnum.Attacker
                    ? attackerBattleSide.Value
                    : defenderBattleSide.Value;

                var losingSide = mapEvent.WinningSide == BattleSideEnum.Attacker
                    ? mapEvent.DefenderSide
                    : mapEvent.AttackerSide;

                var partiesNotInBattle = new List<(WarSide Side, float Strength)>();
                foreach (var party in MobileParty.AllLordParties)
                {
                    if (party.MapEvent == mapEvent || party.ActualClan is null) continue;
                    if (_sides.ResolveSide(party.ActualClan.StringId, war) is { } side)
                        partiesNotInBattle.Add((side, party.Party.EstimatedStrength));
                }

                var outcome = BattleOutcome.Measure(
                    winnerWarSide,
                    losingSide.CasualtyStrength + losingSide.RecalculateStrengthOfSide(),
                    partiesNotInBattle);
                var scored = _applyBattleOutcome.Execute(war, outcome);
                _logger.LogDebug("[PW] {War} {Type} battle won by {Winner}: defeated={Defeated:0} sideTotal={Total:0} battle {Before:0.#} -> {After:0.#}",
                    war.Id, mapEvent.EventType, winnerWarSide, outcome.EnemyForceDefeated, outcome.LosingSideTotalStrength,
                    war.BattleScore, scored.BattleScore);

                _privateWars.Update(scored);
            }
        }

        // Which side of this war a battle side fights on, or null if neither belligerent. A battle side
        // can hold parties from several clans; resolve from the first whose suzerain chain maps onto the war.
        private WarSide? ResolveBattleSide(MapEventSide battleSide, PrivateWar war)
        {
            foreach (var party in battleSide.Parties)
            {
                var clan = PartyClan.OwnerClanOf(party.Party);
                if (clan is null) continue;

                var side = _sides.ResolveSide(clan.StringId, war);
                if (side is not null) return side;
            }

            return null;
        }
    }
}
