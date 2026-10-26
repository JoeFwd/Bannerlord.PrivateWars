using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Armies;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Microsoft.Extensions.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// Debug-only trace of the private wars: a daily line per war and per side (each lord's theatre, lead
    /// blockers and front), feud army churn, goal hand-overs and ruler-relation changes. Changes nothing.
    /// </summary>
    public class PrivateWarDiagnosticsCampaignBehavior : CampaignBehaviorBase
    {
        private readonly IPrivateWarRepository _privateWars;
        private readonly PrivateWarObservationsReader _observations;
        private readonly PrivateWarAllegianceCampaignBehavior _allegiance;
        private readonly PrivateWarFronts _fronts;
        private readonly ILogger<PrivateWarDiagnosticsCampaignBehavior> _logger;

        public PrivateWarDiagnosticsCampaignBehavior(
            IPrivateWarRepository privateWars,
            PrivateWarObservationsReader observations,
            PrivateWarAllegianceCampaignBehavior allegiance,
            PrivateWarFronts fronts,
            ILogger<PrivateWarDiagnosticsCampaignBehavior> logger)
        {
            _privateWars = privateWars;
            _observations = observations;
            _allegiance = allegiance;
            _fronts = fronts;
            _logger = logger;
        }

        public override void RegisterEvents()
        {
            if (!_logger.IsEnabled(LogLevel.Debug)) return;

            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HeroRelationChanged.AddNonSerializedListener(this, OnHeroRelationChanged);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.OnPartyJoinedArmyEvent.AddNonSerializedListener(this, OnPartyJoinedArmy);
            CampaignEvents.OnPartyLeftArmyEvent.AddNonSerializedListener(this, OnPartyLeftArmy);
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, OnArmyDispersed);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnDailyTick()
        {
            foreach (var war in _privateWars.GetAll().Where(war => war.Status == PrivateWarStatus.Active))
            {
                var obs = _observations.Read(war);
                var goal = war.MainGoalSettlementId is null ? null : Settlement.Find(war.MainGoalSettlementId);
                var ownerSide = goal?.OwnerClan is null ? null : _allegiance.ResolveSide(goal.OwnerClan, war);
                var camp = goal?.SiegeEvent?.BesiegerCamp;
                _logger.LogDebug(
                    "[PW] {War} {Date}: score={Score:0.#} battle={Battle:0.#} attHoldsGoal={AttHolds} prisoners(def/att)={DefPrisoners}/{AttPrisoners} goal={Goal} owner={Owner}({OwnerSide}) wall={Wall} siege={Siege}",
                    war.Id, CampaignTime.Now, war.Score, obs.AccumulatedBattleScore, obs.AttackerHoldsMainGoal,
                    obs.DefenderClanPrisonersHeldByAttackerSide, obs.AttackerClanPrisonersHeldByDefenderSide,
                    war.MainGoalSettlementId, goal?.OwnerClan?.StringId, ownerSide?.ToString() ?? "third-party",
                    goal?.Town?.GetWallLevel() ?? 0,
                    camp?.LeaderParty is null ? "none" : $"{camp.LeaderParty.StringId}(prepDone={camp.IsPreparationComplete})");

                foreach (var side in new[] { WarSide.Attacker, WarSide.Defender })
                {
                    var principalId = side == WarSide.Attacker ? war.AttackerPrincipalClanId : war.DefenderPrincipalClanId;
                    if (Clan.FindFirst(c => c.StringId == principalId)?.Kingdom is { } kingdom) LogSide(war, side, kingdom);
                }
            }
        }

        private void LogSide(PrivateWar war, WarSide side, Kingdom kingdom)
        {
            var parties = new List<string>();
            foreach (WarPartyComponent component in kingdom.WarPartyComponents)
            {
                var party = component.MobileParty;
                var clan = party.ActualClan;
                if (clan is null || !party.IsActive || _allegiance.ResolveSide(clan, war) != side) continue;

                var front = _fronts.FindPrivateFront(party);
                parties.Add($"{party.StringId}[{_allegiance.GetTheatre(clan)} str={party.GetTotalLandStrengthWithFollowers():0} "
                            + $"{party.DefaultBehavior}->{party.TargetSettlement?.StringId ?? "-"} army={party.Army?.LeaderParty?.StringId ?? "-"} "
                            + $"front={(front is null ? "-" : $"{front.War.Id}:{string.Join(",", front.Targets.Select(t => t.StringId))}")} "
                            + $"blockers={PrivateWarLeadGate.Describe(party)}]");
            }

            _logger.LogDebug("[PW] {War} {Side} side parties: {Parties}", war.Id, side, string.Join("; ", parties));
        }

        private void OnPartyJoinedArmy(MobileParty party)
        {
            if (party.Army is not { } army || party == army.LeaderParty || !_fronts.IsActiveWarArmy(army)) return;
            _logger.LogDebug("[PW] war army {Leader} joined by {Party}", army.LeaderParty.StringId, party.StringId);
        }

        private void OnPartyLeftArmy(MobileParty party, Army army)
        {
            if (party == army.LeaderParty || !_fronts.IsActiveWarArmy(army)) return;
            _logger.LogDebug("[PW] war army {Leader} lost {Party}: cohesion={Cohesion:0} food={Food:0.#}d inBattle={InBattle} attached={Attached} behaviour={Behaviour}",
                army.LeaderParty.StringId, party.StringId, army.Cohesion, party.GetNumDaysForFoodToLast(),
                party.MapEvent != null, party.AttachedTo != null, party.DefaultBehavior);
        }

        private void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayersArmy)
        {
            if (army.LeaderParty is null || !_fronts.IsActiveWarArmy(army)) return;
            _logger.LogDebug("[PW] war army {Leader} dispersed: {Reason} cohesion={Cohesion:0} parties={Count}",
                army.LeaderParty.StringId, reason, army.Cohesion, army.Parties.Count);
        }

        // A war goal changing hands outside our own capture path (observed: Kenilworth went to an
        // uninvolved clan a day after the attacker took it). The stack trace names the engine path.
        private void OnSettlementOwnerChanged(
            Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (settlement is null) return;

            foreach (var war in _privateWars.GetAll())
            {
                if (war.Status != PrivateWarStatus.Active || war.MainGoalSettlementId != settlement.StringId) continue;

                _logger.LogDebug("[PW] {War} goal {Goal} owner {Old}({OldClan}) -> {New}({NewClan}) detail={Detail} capturer={Capturer} openToClaim={OpenToClaim}\n{Stack}",
                    war.Id, settlement.StringId, oldOwner?.StringId, oldOwner?.Clan?.StringId,
                    newOwner?.StringId, newOwner?.Clan?.StringId, detail, capturerHero?.StringId, openToClaim,
                    new System.Diagnostics.StackTrace(1, false));
            }
        }

        // Defection keys off the clan leader's relation with their ruler; trace every change to that pair.
        private void OnHeroRelationChanged(
            Hero hero, Hero other, int change, bool showNotification,
            ChangeRelationAction.ChangeRelationDetail detail, Hero originalHero, Hero originalOther)
        {
            if (!IsLeaderRulerPair(hero, other) && !IsLeaderRulerPair(other, hero)) return;

            var callers = new System.Diagnostics.StackTrace(1, false).GetFrames()?
                .Select(f => f.GetMethod())
                .Where(m => m?.DeclaringType != null && !m.DeclaringType.Name.Contains("ChangeRelationAction")
                                                      && !m.DeclaringType.Name.Contains("MbEvent")
                                                      && !m.DeclaringType.Name.Contains("CampaignEvent"))
                .Take(4)
                .Select(m => m!.DeclaringType!.Name + "." + m.Name);
            _logger.LogDebug("[PW] ruler relation {Hero}({Clan}) <-> {Other}({OtherClan}) change={Change} now={Now} detail={Detail} via={Via}",
                hero.StringId, hero.Clan?.StringId, other.StringId, other.Clan?.StringId, change,
                hero.GetRelation(other), detail, callers == null ? "" : string.Join(" < ", callers));
        }

        private static bool IsLeaderRulerPair(Hero leader, Hero ruler)
            => leader?.Clan is { } clan && clan.Leader == leader && clan.Kingdom?.Leader == ruler && ruler != leader;
    }
}
