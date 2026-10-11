using System;
using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Armies;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.Spi;
using Bannerlord.PrivateWars.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Helpers;
using Microsoft.Extensions.Logging;

namespace Bannerlord.PrivateWars.Campaign
{
    /**
     * <summary>
     *  Owns the registry lifecycle and the war's prosecution drive. Persists active and concluded
     *  wars through Bannerlord saves and restores them on load; and, because the engine never plans
     *  a same-kingdom siege on its own (design §5, risk #1), injects the war goal as a strength-gated
     *  besiege candidate into the side leader's AI think so it competes in the engine's normal behaviour
     *  vote — losing, by design, to higher priorities such as defending the clan's own threatened
     *  fief. The retention model keeps an in-progress siege sticky. Each day it also advances every
     *  active war's score from current world state and, once a side crosses ±100, resolves it:
     *  reverting captured fiefs to their pre-war owners (status quo ante) and leaving the attacker
     *  its goal as the prize.
     * </summary>
     */
    public class PrivateWarCampaignBehavior : CampaignBehaviorBase, IResolvePrivateWarByIdUseCase
    {
        // Score injected into the AI behaviour vote for a private-war attacker's besiege of the goal.
        // AiPartyThinkBehavior applies the maximum-scoring candidate, so this must beat ordinary /
        // opportunistic attacks (~3-15) yet stay below an active defence of the clan's own fief
        // (~15-30; defence's base factor is 1.28 vs a siege's 0.8) so defending one's title wins.
        // Only the side's designated leader bids, scaled by PrivateWarSiegeStrengthPolicy (x0.5-1).
        // Tuning value — revisit via AI-vs-AI playtest.
        private const float PrivateWarSiegeSelectionScore = 12f;

        // Once a private-war besieger has established its camp on the goal, this dominant score keeps it
        // there through the engine's periodic AI re-think until preparations complete and DADG captures
        // the fief. A same-kingdom siege is not a vanilla war, so without this the besieger flips to
        // GoToSettlement and abandons the camp before it matures (observed in playtest). It must beat
        // ordinary wander/escort/opportunistic scores (~3-20). Highest-priority tuning knob — revisit via
        // AI-vs-AI playtest (notably whether an active defence of the clan's own fief should still
        // outrank holding the siege; at this value it does not).
        private const float PrivateWarSiegeHoldScore = 50f;

        private readonly IPrivateWarRepository _privateWars;
        private readonly ITickPrivateWarUseCase _tickPrivateWar;
        private readonly IResolvePrivateWarUseCase _resolvePrivateWar;
        private readonly IApplyBattleOutcomeUseCase _applyBattleOutcome;
        private readonly IPrivateWarSidesProvider _sides;
        private readonly PrivateWarArmyDecisionAdapter _armyDecisionAdapter;
        private readonly PrivateWarSyntheticCapturePolicy _syntheticCapturePolicy;
        private readonly PrivateWarSiegeStrengthPolicy _siegeStrengthPolicy;
        private readonly PrivateWarSideLeaderSelector _sideLeaderSelector;
        private readonly ILogger<PrivateWarCampaignBehavior> _logger;

        private List<string> _serialisedWars = new();

        // Party that raised each war side's goal army, keyed by war id and side. A gathering Besieger
        // army points AiBehaviorObject at a gathering fortress, never the goal, so without this its
        // leader lost the designation the moment it formed and the next lord raised another army.
        // Not persisted: after a load, an army is recognised again once it marches on the goal.
        private readonly Dictionary<(string WarId, WarSide Side), string> _goalArmyLeaders = new();

        /// <inheritdoc cref="IResolvePrivateWarByIdUseCase.WarResolved"/>
        public event Action<PrivateWar, PrivateWarOutcome>? WarResolved;

        public PrivateWarCampaignBehavior(
            IPrivateWarRepository privateWars,
            ITickPrivateWarUseCase tickPrivateWar,
            IResolvePrivateWarUseCase resolvePrivateWar,
            IApplyBattleOutcomeUseCase applyBattleOutcome,
            IPrivateWarSidesProvider sides,
            PrivateWarArmyDecisionAdapter armyDecisionAdapter,
            PrivateWarSyntheticCapturePolicy syntheticCapturePolicy,
            PrivateWarSiegeStrengthPolicy siegeStrengthPolicy,
            PrivateWarSideLeaderSelector sideLeaderSelector,
            ILogger<PrivateWarCampaignBehavior> logger)
        {
            _logger = logger;
            _siegeStrengthPolicy = siegeStrengthPolicy;
            _sideLeaderSelector = sideLeaderSelector;
            _privateWars = privateWars;
            _tickPrivateWar = tickPrivateWar;
            _resolvePrivateWar = resolvePrivateWar;
            _applyBattleOutcome = applyBattleOutcome;
            _sides = sides;
            _armyDecisionAdapter = armyDecisionAdapter;
            _syntheticCapturePolicy = syntheticCapturePolicy;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                _serialisedWars = PrivateWarStateSerialiser.SerialiseWars(_privateWars.Snapshot());
            }

            dataStore.SyncData("DadgPrivateWars", ref _serialisedWars);

            _serialisedWars ??= new List<string>();
        }

        private void OnGameLoaded(CampaignGameStarter campaignGameStarter)
        {
            _privateWars.Initialise(PrivateWarStateSerialiser.DeserialiseWars(_serialisedWars));
        }

        // DADG owns prosecution. The stock siege planner never enumerates a same-kingdom settlement
        // (design §5, risk #1), so the goal never enters the engine's behaviour vote on its own. For
        // each attacker principal's eligible party, inject the goal as a scored besiege candidate; it
        // then flows through the engine's own max-score selection in AiPartyThinkBehavior, so a
        // higher-priority objective (notably defending the clan's own besieged fief) naturally wins.
        // Only the side's designated leader bids (principal, else the most senior vassal); the rest of
        // the side joins its army when called instead of trickling in alone. Like vanilla's
        // AiMilitaryBehavior, the leader bids twice — once as a gathered army, once with its current
        // strength — and each bid is strength-gated against the walled defence, so an outnumbered
        // party gathers an army or stays home rather than besieging alone.
        private void OnAiHourlyTick(MobileParty mobileParty, PartyThinkParams p)
        {
            VetoSameKingdomSiegeRelief(mobileParty, p);

            var objective = FindPrivateWarGoalFor(mobileParty);
            if (objective is null || !IsDesignatedSideLeader(mobileParty, objective)) return;

            float defence = EstimateGoalDefence(objective);
            int wallLevel = objective.Goal.Town?.GetWallLevel() ?? 0;

            if (mobileParty.Army == null)
            {
                var armyMembers = GetPrivateWarArmyMembers(mobileParty, objective);
                if (armyMembers != null)
                {
                    float armyStrength = mobileParty.GetTotalLandStrengthWithFollowers()
                                         + armyMembers.Sum(member => member.Party.GetCustomStrength(
                                             BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege));
                    AddSiegeBid(p, objective.Goal, willGatherArmy: true, PrivateWarSiegeSelectionScore
                        * _siegeStrengthPolicy.GetScoreFactor(armyStrength, defence, wallLevel, isHoldingSiege: false));
                }
            }

            // While en route, use the low selection score so a higher-priority objective (notably
            // defending the clan's own besieged fief) still wins. Once the party has actually established
            // the siege on the goal, switch to the dominant hold score so the engine's periodic re-think
            // does not abandon the camp before preparations complete — unless relief has made the siege
            // unwinnable, in which case the gate drops the bid and the camp is abandoned.
            bool isHoldingSiege = mobileParty.BesiegedSettlement == objective.Goal;
            float ourStrength = isHoldingSiege && mobileParty.BesiegerCamp != null
                ? mobileParty.BesiegerCamp.GetInvolvedPartiesForEventType().Sum(party => party.EstimatedStrength)
                : mobileParty.GetTotalLandStrengthWithFollowers();
            float baseScore = isHoldingSiege ? PrivateWarSiegeHoldScore : PrivateWarSiegeSelectionScore;
            AddSiegeBid(p, objective.Goal, willGatherArmy: false, baseScore
                * _siegeStrengthPolicy.GetScoreFactor(ourStrength, defence, wallLevel, isHoldingSiege));
        }

        // Vanilla's defender scoring treats any besieged settlement of the party's own kingdom as one to
        // relieve, so a private-war siege drew the whole kingdom - the besieger's own side included - into
        // DefendSettlement on it (observed: every attacker-side party "defending" its own siege of
        // Kenilworth). A same-kingdom siege is a feud, so only the besieger's private-war opponents relieve it.
        private void VetoSameKingdomSiegeRelief(MobileParty mobileParty, PartyThinkParams p)
        {
            var clan = mobileParty.ActualClan;
            if (clan is null) return;

            List<AIBehaviorData>? vetoed = null;
            foreach (var (behaviour, score) in p.AIBehaviorScores)
            {
                if (behaviour.AiBehavior != AiBehavior.DefendSettlement || score <= 0f) continue;
                if (behaviour.Party is not Settlement settlement) continue;

                var besieger = settlement.SiegeEvent?.BesiegerCamp?.LeaderParty;
                var besiegerClan = besieger?.ActualClan;
                if (besiegerClan is null || besieger!.MapFaction != mobileParty.MapFaction) continue;
                if (IsPrivateWarOpponent(clan.StringId, besiegerClan.StringId)) continue;

                (vetoed ??= new List<AIBehaviorData>()).Add(behaviour);
            }

            if (vetoed is null) return;
            foreach (var behaviour in vetoed)
                p.SetBehaviorScore(behaviour, 0f);
        }

        private bool IsPrivateWarOpponent(string clanId, string otherClanId)
            => _privateWars.Snapshot().Any(war =>
            {
                if (war.Status != PrivateWarStatus.Active) return false;
                var side = ResolveSide(clanId, war);
                var otherSide = ResolveSide(otherClanId, war);
                return side is not null && otherSide is not null && side != otherSide;
            });

        private static void AddSiegeBid(PartyThinkParams p, Settlement goal, bool willGatherArmy, float score)
        {
            if (score <= 0f) return;

            var behaviour = new AIBehaviorData(
                goal, AiBehavior.BesiegeSettlement, MobileParty.NavigationType.Default,
                willGatherArmy, isFromPort: false, isTargetingPort: false);
            p.AddBehaviorScore((behaviour, score));
        }

        private bool IsDesignatedSideLeader(MobileParty mobileParty, PrivateWarGoal objective)
        {
            if (mobileParty.MapFaction is not Kingdom kingdom) return false;

            var candidates = new List<PrivateWarLeaderCandidate>();
            foreach (WarPartyComponent component in kingdom.WarPartyComponents)
            {
                var party = component.MobileParty;
                var clan = party.ActualClan;
                if (clan is null || !party.IsActive || party.IsMainParty || clan == Clan.PlayerClan) continue;
                if (ResolveSide(clan.StringId, objective.War) != objective.Side) continue;

                // Only a besieging army counts: an army *defending* the goal also has it as its behaviour
                // object, and ranking it first stole the lead (and the hold bid) from the real besieger.
                bool isBesiegingGoal = party.BesiegedSettlement == objective.Goal
                                       && party.BesiegerCamp?.LeaderParty == party;
                bool isLeadingGoalArmy = party.Army?.LeaderParty == party
                                         && party.Army.ArmyType == Army.ArmyTypes.Besieger
                                         && (party.Army.AiBehaviorObject == objective.Goal
                                             || _goalArmyLeaders.TryGetValue((objective.War.Id, objective.Side), out var raisedBy)
                                             && raisedBy == party.StringId);
                if (!isBesiegingGoal && !isLeadingGoalArmy && !CanLeadPrivateWarArmy(party)) continue;

                candidates.Add(new PrivateWarLeaderCandidate(
                    party.StringId, clan.StringId, clan.Tier, clan.Renown, isLeadingGoalArmy, isBesiegingGoal));
            }

            var principalClanId = objective.Side == WarSide.Attacker
                ? objective.War.AttackerPrincipalClanId
                : objective.War.DefenderPrincipalClanId;
            return _sideLeaderSelector.Select(candidates, principalClanId) == mobileParty.StringId;
        }

        // Walled-defence input for the strength gate: the garrison, militia and enemy-side lords inside
        // the goal, plus enemy-side lords within half a day's march who would come to its relief. Sides
        // are resolved through the feud, not MapFaction (both sides share a kingdom).
        private float EstimateGoalDefence(PrivateWarGoal objective)
        {
            var goal = objective.Goal;
            float defence = 0f;
            foreach (var party in goal.Parties)
            {
                if (party.IsGarrison || party.IsMilitia || IsOnOpposingSide(party, objective))
                    defence += party.Party.EstimatedStrength;
            }

            float reliefRadius = TaleWorlds.CampaignSystem.Campaign.Current.EstimatedAverageLordPartySpeed
                                 * CampaignTime.HoursInDay * 0.5f;
            var search = MobileParty.StartFindingLocatablesAroundPosition(goal.GatePosition.ToVec2(), reliefRadius);
            for (var party = MobileParty.FindNextLocatable(ref search); party != null; party = MobileParty.FindNextLocatable(ref search))
            {
                if (party.CurrentSettlement != goal && party.IsLordParty && party.AttachedTo == null
                    && IsOnOpposingSide(party, objective))
                {
                    defence += party.GetTotalLandStrengthWithFollowers(includeNonAttachedArmyMembers: false);
                }
            }

            return defence;
        }

        private bool IsOnOpposingSide(MobileParty party, PrivateWarGoal objective)
        {
            var clan = party.ActualClan;
            var side = clan is null ? null : ResolveSide(clan.StringId, objective.War);
            return side is not null && side != objective.Side;
        }

        private MBList<MobileParty>? GetPrivateWarArmyMembers(
            MobileParty leaderParty,
            PrivateWarGoal objective)
        {
            if (!CanLeadPrivateWarArmy(leaderParty)) return null;
            if (leaderParty.MapFaction is not Kingdom kingdom)
                return null;

            var eligibleParties = new List<(MobileParty Party, float Priority)>();
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
                    || party.GetNumDaysForFoodToLast()
                        <= TaleWorlds.CampaignSystem.Campaign.Current.Models.ArmyManagementCalculationModel.MinimumNeededFoodInDaysToCallToArmy
                    || party.PartySizeRatio
                        <= TaleWorlds.CampaignSystem.Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy
                    || !partyLeader.CanLeadParty()
                    || party.IsInRaftState
                    || party.MapEvent != null
                    || party.BesiegedSettlement != null)
                {
                    continue;
                }

                var disbandBehavior = TaleWorlds.CampaignSystem.Campaign.Current
                    .GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
                if (disbandBehavior != null && disbandBehavior.IsPartyWaitingForDisband(party))
                    continue;

                var maximumDistance = TaleWorlds.CampaignSystem.Campaign.Current.Models
                    .ArmyManagementCalculationModel.MaximumDistanceToCallToArmy;
                if (DistanceHelper.GetDistanceBetweenMobilePartyToMobileParty(
                        party, leaderParty, party.NavigationCapability, out _) >= maximumDistance)
                {
                    continue;
                }

                int influenceCost = TaleWorlds.CampaignSystem.Campaign.Current.Models.ArmyManagementCalculationModel
                    .CalculatePartyInfluenceCost(leaderParty, party);
                float unwoundedRatio = 1f - (float)party.Party.MemberRoster.TotalWounded
                    / party.Party.MemberRoster.TotalManCount;
                float priority = party.Party.EstimatedStrength / (influenceCost + 0.1f) * unwoundedRatio;
                eligibleParties.Add((party, priority));
            }

            int maximumMemberCount = MathF.Ceiling(
                kingdom.WarPartyComponents.Count * 0.7f
                - kingdom.Armies.Sum(army => army.Parties.Count));
            if (maximumMemberCount <= 0) return null;

            var candidates = new List<PrivateWarArmyCandidate>
            {
                ToArmyCandidate(
                    leaderParty,
                    canLeadArmy: true,
                    canJoinArmy: false,
                    memberDesirability: 0f)
            };
            candidates.AddRange(eligibleParties.Select(candidate => ToArmyCandidate(
                candidate.Party,
                canLeadArmy: CanLeadPrivateWarArmy(candidate.Party),
                canJoinArmy: true,
                memberDesirability: candidate.Priority)));

            var assignments = kingdom.Armies
                .Where(army => army.AiBehaviorObject == objective.Goal)
                .Where(army => army.LeaderParty.ActualClan != null)
                .Where(army => ResolveSide(
                    army.LeaderParty.ActualClan.StringId, objective.War) == objective.Side)
                .Select(army => new PrivateWarArmyAssignment(
                    objective.War.Id,
                    objective.Side,
                    objective.War.MainGoalSettlementId,
                    army.LeaderParty.StringId))
                .ToList();

            var plan = _armyDecisionAdapter.CreatePlan(
                objective.War,
                objective.Side,
                leaderParty.StringId,
                candidates,
                assignments,
                _sides.GetSuzerain,
                maximumMemberCount);
            if (plan is null) return null;

            var membersById = eligibleParties.ToDictionary(candidate => candidate.Party.StringId, candidate => candidate.Party);
            return new MBList<MobileParty>(
                plan.MemberPartyIds.Select(partyId => membersById[partyId]));
        }

        public PrivateWarArmyCreationAction PreparePrivateWarArmyCreation(
            Hero armyLeader,
            Settlement targetSettlement,
            Army.ArmyTypes selectedArmyType,
            out MBReadOnlyList<MobileParty>? replacementMembers)
        {
            replacementMembers = null;

            var leaderParty = armyLeader?.PartyBelongedTo;
            var objective = leaderParty is null ? null : FindPrivateWarGoalFor(leaderParty);
            MBList<MobileParty>? freshMembers = null;
            var decision = _armyDecisionAdapter.DecidePrivateCreation(
                selectedArmyType == Army.ArmyTypes.Besieger,
                objective != null,
                leaderParty?.StringId ?? string.Empty,
                targetSettlement?.StringId ?? string.Empty,
                objective != null && IsDesignatedSideLeader(leaderParty!, objective)
                    ? leaderParty!.StringId
                    : string.Empty,
                objective?.War.MainGoalSettlementId ?? string.Empty,
                () =>
                {
                    freshMembers = GetPrivateWarArmyMembers(leaderParty!, objective!);
                    return freshMembers?.Select(member => member.StringId).ToList();
                });

            if (decision.Action == PrivateWarArmyCreationAction.ReplaceMembers)
            {
                replacementMembers = freshMembers;
                _goalArmyLeaders[(objective!.War.Id, objective.Side)] = leaderParty!.StringId;
            }
            if (decision.Action != PrivateWarArmyCreationAction.ContinueOriginal)
                _logger.LogDebug("[PW] {War} army creation {Action}: leader={Leader} target={Target} members=[{Members}]",
                    objective?.War.Id, decision.Action, leaderParty?.StringId, targetSettlement?.StringId,
                    string.Join(",", decision.MemberPartyIds));

            return decision.Action;
        }

        private static PrivateWarArmyCandidate ToArmyCandidate(
            MobileParty party,
            bool canLeadArmy,
            bool canJoinArmy,
            float memberDesirability)
            => new(
                party.StringId,
                party.ActualClan?.StringId ?? string.Empty,
                canLeadArmy,
                canJoinArmy,
                party.Party.GetCustomStrength(
                    BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege),
                memberDesirability);

        private static bool CanLeadPrivateWarArmy(MobileParty party)
        {
            var leaderHero = party.LeaderHero;
            var clan = leaderHero?.Clan;
            if (leaderHero is null
                || clan is null
                || party.Army != null
                || party.MapFaction is not Kingdom
                || party.IsCurrentlyAtSea
                || clan.Influence <= 100f
                || clan.IsUnderMercenaryService
                || party.GetNumDaysForFoodToLast()
                    <= TaleWorlds.CampaignSystem.Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege
                || party.PartySizeRatio
                    <= TaleWorlds.CampaignSystem.Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy)
            {
                return false;
            }

            return clan.Leader == leaderHero
                || clan.Leader?.PartyBelongedTo is null
                && clan.WarPartyComponents != null
                && clan.WarPartyComponents.FirstOrDefault() == party.WarPartyComponent;
        }

        // Capture must be immediate: the moment a private-war besieger's siege preparations complete the
        // fief changes hands, not on the next daily tick. Siege preparations advance on the engine's
        // hourly cadence, so check every game-hour - this fires the capture within the same hour the
        // engine itself would consider the siege assault-ready.
        private void OnHourlyTick()
        {
            TryCaptureCompletedSieges();
        }

        // Once a day, advance every active war: recompute its signed score from current world state and,
        // when a side crosses the +/-100 threshold, resolve it. The score calculator's fatigue term
        // drifts toward whoever holds the main goal, so the war terminates even in a stalemate.
        private void OnDailyTick()
        {
            float currentDay = (float)CampaignTime.Now.ToDays;

            foreach (var war in _privateWars.GetAll())
            {
                if (war.Status != PrivateWarStatus.Active) continue;

                ReleaseBelligerentsFromForeignArmies(war);
                var observations = BuildObservations(war);
                var result = _tickPrivateWar.Execute(war, observations, currentDay);
                var scored = war with { Score = result.Score };
                LogWarDiagnosis(scored, observations);

                if (result.Outcome is { } outcome)
                    ResolveWar(scored, outcome);
                else
                    _privateWars.Update(scored);
            }
        }

        // Daily trace of why a war is or is not being prosecuted: score inputs, who holds the goal, and for
        // the side that should besiege it, every party's eligibility, the designated leader, its army plan
        // and the strength-gate factors that feed the AI bid.
        private void LogWarDiagnosis(PrivateWar war, PrivateWarObservations obs)
        {
            if (!_logger.IsEnabled(LogLevel.Debug)) return;

            var goal = war.MainGoalSettlementId is null ? null : Settlement.Find(war.MainGoalSettlementId);
            var ownerSide = goal?.OwnerClan is null ? null : ResolveSide(goal.OwnerClan.StringId, war);
            var camp = goal?.SiegeEvent?.BesiegerCamp;
            var siegeClan = camp?.LeaderParty?.ActualClan;
            string siege = camp?.LeaderParty is null
                ? "none"
                : $"{camp.LeaderParty.StringId}(side={(siegeClan is null ? null : ResolveSide(siegeClan.StringId, war))?.ToString() ?? "-"}, prepDone={camp.IsPreparationComplete})";
            _logger.LogDebug(
                "[PW] {War} {Date}: score={Score:0.#} battle={Battle:0.#} attHoldsGoal={AttHolds} prisoners(def/att)={DefPrisoners}/{AttPrisoners} goal={Goal} owner={Owner}({OwnerSide}) wall={Wall} siege={Siege}",
                war.Id, CampaignTime.Now, war.Score, obs.AccumulatedBattleScore, obs.AttackerHoldsMainGoal,
                obs.DefenderClanPrisonersHeldByAttackerSide, obs.AttackerClanPrisonersHeldByDefenderSide,
                war.MainGoalSettlementId, goal?.OwnerClan?.StringId, ownerSide?.ToString() ?? "third-party",
                goal?.Town?.GetWallLevel() ?? 0, siege);
            if (goal is null || ownerSide is null) return;

            var side = ownerSide == WarSide.Attacker ? WarSide.Defender : WarSide.Attacker;
            var objective = new PrivateWarGoal(war, side, goal);
            var principalId = side == WarSide.Attacker ? war.AttackerPrincipalClanId : war.DefenderPrincipalClanId;
            var kingdom = Clan.All.FirstOrDefault(c => c.StringId == principalId)?.Kingdom;
            if (kingdom is null) return;

            MobileParty? leader = null;
            var parties = new List<string>();
            foreach (WarPartyComponent component in kingdom.WarPartyComponents)
            {
                var party = component.MobileParty;
                var clan = party.ActualClan;
                if (clan is null || !party.IsActive || ResolveSide(clan.StringId, war) != side) continue;

                if (leader is null && IsDesignatedSideLeader(party, objective)) leader = party;
                parties.Add($"{party.StringId}[str={party.GetTotalLandStrengthWithFollowers():0} {party.DefaultBehavior}->{party.TargetSettlement?.StringId ?? "-"} army={party.Army?.LeaderParty?.StringId ?? "-"} blockers={DescribeLeadBlockers(party)}]");
            }

            float defence = EstimateGoalDefence(objective);
            int wallLevel = goal.Town?.GetWallLevel() ?? 0;
            _logger.LogDebug("[PW] {War} {Side} side parties: {Parties}", war.Id, side, string.Join("; ", parties));
            if (leader is null)
            {
                _logger.LogDebug("[PW] {War} {Side}: no eligible leader, no siege bid (defence={Defence:0})", war.Id, side, defence);
                return;
            }

            var members = leader.Army == null ? GetPrivateWarArmyMembers(leader, objective) : null;
            float solo = leader.GetTotalLandStrengthWithFollowers();
            float armyStrength = solo + (members?.Sum(member => member.Party.GetCustomStrength(
                BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege)) ?? 0f);
            _logger.LogDebug(
                "[PW] {War} {Side}: leader={Leader} inBattle={InBattle} members=[{Members}] armyStr={ArmyStr:0} soloStr={Solo:0} defence={Defence:0} wall={Wall} armyFactor={ArmyFactor:0.##} soloFactor={SoloFactor:0.##}",
                war.Id, side, leader.StringId, leader.MapEvent != null,
                members is null ? "-" : string.Join(",", members.Select(member => member.StringId)),
                armyStrength, solo, defence, wallLevel,
                members is null ? 0f : _siegeStrengthPolicy.GetScoreFactor(armyStrength, defence, wallLevel, false),
                _siegeStrengthPolicy.GetScoreFactor(solo, defence, wallLevel, leader.BesiegedSettlement == goal));
        }

        // Mirrors CanLeadPrivateWarArmy, naming each failed condition for the daily trace.
        private static string DescribeLeadBlockers(MobileParty party)
        {
            var models = TaleWorlds.CampaignSystem.Campaign.Current.Models;
            var clan = party.LeaderHero?.Clan;
            var blockers = new List<string>();
            if (party.LeaderHero is null || clan is null) blockers.Add("noLeader");
            if (party.Army != null) blockers.Add("inArmy");
            if (party.MapFaction is not Kingdom) blockers.Add("noKingdom");
            if (party.IsCurrentlyAtSea) blockers.Add("atSea");
            if (clan != null && clan.Influence <= 100f) blockers.Add($"influence={clan.Influence:0}");
            if (clan != null && clan.IsUnderMercenaryService) blockers.Add("mercenary");
            if (party.GetNumDaysForFoodToLast() <= models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege)
                blockers.Add($"food={party.GetNumDaysForFoodToLast()}d");
            if (party.PartySizeRatio <= models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy)
                blockers.Add($"sizeRatio={party.PartySizeRatio:0.##}");
            if (party.MapEvent != null) blockers.Add("inBattle");
            if (blockers.Count == 0 && !CanLeadPrivateWarArmy(party)) blockers.Add("notClanLeader");
            return blockers.Count == 0 ? "none" : string.Join("|", blockers);
        }

        // A belligerent already called into an ordinary kingdom army when its war broke out is held there,
        // never eligible to lead or join its side's goal army. Pull it out unless its own side leads the
        // army against the war goal. Player-led armies are left alone.
        private void ReleaseBelligerentsFromForeignArmies(PrivateWar war)
        {
            foreach (var party in MobileParty.AllLordParties.ToList())
            {
                var army = party.Army;
                var clan = party.ActualClan;
                if (army is null || army.LeaderParty == party || army.LeaderParty is null
                    || army.LeaderParty.IsMainParty || clan is null || party.MapEvent != null)
                {
                    continue;
                }

                var side = ResolveSide(clan.StringId, war);
                if (side is null) continue;

                var leaderClan = army.LeaderParty.ActualClan;
                bool isOwnGoalArmy = leaderClan != null
                                     && ResolveSide(leaderClan.StringId, war) == side
                                     && (army.AiBehaviorObject as Settlement)?.StringId == war.MainGoalSettlementId;
                if (!isOwnGoalArmy)
                    party.Army = null;
            }
        }

        // DADG drives the siege to a transfer of possession - NOT to the end of the war. A same-kingdom
        // siege has no vanilla path to an assault (the assault/defender logic is gated on
        // MapFaction.IsAtWarWith, which is false within one kingdom), so once a besieger has completed its
        // preparations on the goal, capture the fief directly: mirror the engine's own post-assault
        // sequence (detach the besiegers, clear the routed settlement defenders, transfer ownership by
        // siege). ApplyBySiege owns 1.4's garrison destruction and replacement; DADG must not retain or
        // destroy that engine-owned party itself.
        // Possession can change hands repeatedly - the attacker takes the goal, the defender later
        // reclaims it - and the war itself is resolved only when the score crosses +/-100 on the daily
        // tick, so the contest has time to swing. The transfer goes to whichever side is besieging, as
        // long as it is not the side that already holds the goal. RemoveAllSiegeParties only asserts
        // against a live assault MapEvent, which an AI same-kingdom siege never creates, so it is safe.
        private void TryCaptureCompletedSieges()
        {
            foreach (var war in _privateWars.GetAll().ToList())
            {
                if (war.Status != PrivateWarStatus.Active || war.MainGoalSettlementId is null) continue;

                var goal = Settlement.Find(war.MainGoalSettlementId);
                var camp = goal?.SiegeEvent?.BesiegerCamp;
                if (goal is null || camp is null) continue;

                var leaderParty = camp.LeaderParty;
                var leaderHero = leaderParty?.LeaderHero;
                var besiegerClan = leaderParty?.ActualClan;
                var besiegerSide = besiegerClan is null ? null : ResolveSide(besiegerClan.StringId, war);
                var ownerSide = goal.OwnerClan is null ? null : ResolveSide(goal.OwnerClan.StringId, war);
                bool hasUsableSiegeLeader = leaderParty?.IsActive == true
                    && leaderHero?.PartyBelongedTo == leaderParty;
                var losingSide = _syntheticCapturePolicy.GetLosingSide(
                    war,
                    goal.StringId,
                    camp.IsPreparationComplete,
                    leaderParty?.IsMainParty == true,
                    hasUsableSiegeLeader,
                    besiegerSide,
                    ownerSide);
                if (losingSide is null || leaderParty is null || leaderHero is null) continue;

                // Owner-change callbacks can mutate the settlement's party list. Snapshot the losing
                // leaders first, then operate only on this immutable capture set after the transfer.
                var losingHeroes = goal.Parties
                    .Where(inside => inside.MapEvent is null
                                     && inside.ActualClan is not null
                                     && ResolveSide(inside.ActualClan.StringId, war) == losingSide
                                     && inside.LeaderHero is not null)
                    .Select(inside => inside.LeaderHero)
                    .Distinct()
                    .ToList();
                var capturerParty = leaderParty.Party;

                camp.RemoveAllSiegeParties();

                // Settlement.Party is the static defender roster used by the siege. It is not
                // Town.GarrisonParty; ApplyBySiege destroys and replaces that mobile garrison in 1.4.
                goal.Party.MemberRoster.Clear();
                ChangeOwnerOfSettlementAction.ApplyBySiege(leaderHero, leaderHero, goal);

                // ApplyBySiege is synchronous. Do not capture/update the fatigue epoch unless the
                // requested ownership transfer actually completed.
                if (goal.OwnerClan != leaderHero.Clan)
                {
                    _logger.LogWarning("[PW] {War} capture of {Goal} by {Leader} did not transfer ownership (owner={Owner})",
                        war.Id, goal.StringId, leaderHero.StringId, goal.OwnerClan?.StringId);
                    continue;
                }
                _logger.LogDebug("[PW] {War} {Goal} captured by {Leader} ({Clan}); prisoners=[{Losers}]",
                    war.Id, goal.StringId, leaderHero.StringId, leaderHero.Clan?.StringId,
                    string.Join(",", losingHeroes.Select(hero => hero.StringId)));

                // Classic-war parity: a same-kingdom capture changes neither MapFaction nor stages an assault
                // MapEvent, so the engine's own post-assault prisoner-taking (MapEvent.LootDefeatedPartyMembers
                // -> TakePrisonerAction.Apply) never runs and the losing side's lords are left sitting in the
                // fief they just lost as if it were friendly. Mirror what a real assault does to defeated
                // defenders: take their leaders prisoner via the same engine action, capturing them for the
                // besieger. PrivateWarPrisonerRetentionPatch keeps them held despite the shared kingdom.
                foreach (var losingHero in losingHeroes)
                {
                    if (!losingHero.IsPrisoner)
                        TakePrisonerAction.Apply(capturerParty, losingHero);
                }

                // The goal just changed hands, so reset the fatigue epoch: the accumulated drift toward
                // the previous holder is wiped and the contest restarts its climb toward the new holder.
                _privateWars.Update(war with { GoalLastTakenDay = (float)CampaignTime.Now.ToDays });
            }
        }

        // A private war is an intra-kingdom feud: both principals share a MapFaction, and every
        // mechanism (the AreEnemies hostility signal, the same-kingdom siege drive, prisoner retention,
        // status-quo resolution) assumes it. When a principal defects so the two principals no longer
        // share a kingdom, that premise is void and a stale Active record would keep forcing
        // cross-kingdom hostility; conclude it. Subtree vassals need no handling - WarSideResolver
        // re-resolves their side from the current suzerain chain on every query.
        // Diagnostic: a war goal changing hands outside our own capture path (observed: Kenilworth went
        // to an uninvolved clan a day after the attacker took it). The stack trace names the engine path.
        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (!_logger.IsEnabled(LogLevel.Debug) || settlement is null) return;

            foreach (var war in _privateWars.Snapshot())
            {
                if (war.Status != PrivateWarStatus.Active || war.MainGoalSettlementId != settlement.StringId) continue;

                _logger.LogDebug("[PW] {War} goal {Goal} owner {Old}({OldClan}) -> {New}({NewClan}) detail={Detail} capturer={Capturer} openToClaim={OpenToClaim}\n{Stack}",
                    war.Id, settlement.StringId, oldOwner?.StringId, oldOwner?.Clan?.StringId,
                    newOwner?.StringId, newOwner?.Clan?.StringId, detail, capturerHero?.StringId, openToClaim,
                    new System.Diagnostics.StackTrace(1, false));
            }
        }

        private void OnClanChangedKingdom(
            Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            foreach (var war in _privateWars.GetByClan(clan.StringId))
            {
                if (war.Status != PrivateWarStatus.Active) continue;

                var attacker = Clan.All.FirstOrDefault(c => c.StringId == war.AttackerPrincipalClanId);
                var defender = Clan.All.FirstOrDefault(c => c.StringId == war.DefenderPrincipalClanId);
                if (attacker != null && defender != null && attacker.MapFaction == defender.MapFaction)
                    continue;

                _privateWars.Update(war with { Status = PrivateWarStatus.Concluded });
                ReleaseWarPrisoners(war);
                InfoPrinter.Display(
                    $"Private war concluded: {war.AttackerPrincipalClanId} vs {war.DefenderPrincipalClanId} - " +
                    "principals no longer share a kingdom.");
            }
        }

        // When a battle resolves between the two sides of an active private war, bump that war's
        // accumulated battle score (the sole mutator of BattleScore, design §18.B). The bump is the
        // share of the losing side's total strength destroyed, signed by the winner. Fires at
        // WaitingRemoval, before parties are finalized, so the sides' casualties and strength are intact.
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

                var outcome = new BattleOutcome(
                    winnerWarSide,
                    losingSide.CasualtyStrength,
                    losingSide.CasualtyStrength + losingSide.RecalculateStrengthOfSide());

                _privateWars.Update(_applyBattleOutcome.Execute(war, outcome));
            }
        }

        // Which side of this war a battle side fights on, or null if neither belligerent. A battle side
        // can hold parties from several clans; resolve from the first whose suzerain chain maps onto the war.
        private WarSide? ResolveBattleSide(MapEventSide battleSide, PrivateWar war)
        {
            foreach (var party in battleSide.Parties)
            {
                var clan = party.Party.MobileParty?.ActualClan ?? party.Party.Settlement?.OwnerClan;
                if (clan is null) continue;

                var side = ResolveSide(clan.StringId, war);
                if (side is not null) return side;
            }

            return null;
        }

        // Assemble the score inputs from current world state. Side membership is resolved through the
        // feudal hierarchy so a clan fights with (or against) its whole suzerain subtree. The main goal
        // carries its own score term, so it is excluded from the town/castle crossing counts.
        private PrivateWarObservations BuildObservations(PrivateWar war)
        {
            int defTownsByAtt = 0, defCastlesByAtt = 0, attTownsByDef = 0, attCastlesByDef = 0;
            bool attackerHoldsGoal = false;

            foreach (var entry in war.OriginalFiefOwners)
            {
                var settlement = Settlement.Find(entry.Key);
                if (settlement?.OwnerClan is null) continue;

                var currentSide = ResolveSide(settlement.OwnerClan.StringId, war);

                if (entry.Key == war.MainGoalSettlementId)
                {
                    attackerHoldsGoal = currentSide == WarSide.Attacker;
                    continue;
                }

                var originalSide = ResolveSide(entry.Value, war);
                if (currentSide is null || originalSide is null || currentSide == originalSide) continue;

                if (originalSide == WarSide.Defender && currentSide == WarSide.Attacker)
                {
                    if (settlement.IsTown) defTownsByAtt++;
                    else if (settlement.IsCastle) defCastlesByAtt++;
                }
                else if (originalSide == WarSide.Attacker && currentSide == WarSide.Defender)
                {
                    if (settlement.IsTown) attTownsByDef++;
                    else if (settlement.IsCastle) attCastlesByDef++;
                }
            }

            int defenderPrisoners = CountPrisoners(war.DefenderPrincipalClanId, WarSide.Attacker, war);
            int attackerPrisoners = CountPrisoners(war.AttackerPrincipalClanId, WarSide.Defender, war);

            return new PrivateWarObservations(
                attackerHoldsGoal,
                defTownsByAtt, defCastlesByAtt,
                attTownsByDef, attCastlesByDef,
                defenderPrisoners, attackerPrisoners,
                war.BattleScore);
        }

        // Captives of the given principal's own clan currently held by a party or settlement belonging
        // to the opposing side (design §18.B). Only the principal clan's heroes count, not the whole side.
        private int CountPrisoners(string capturedPrincipalClanId, WarSide captorSide, PrivateWar war)
        {
            var clan = Clan.All.FirstOrDefault(c => c.StringId == capturedPrincipalClanId);
            if (clan is null) return 0;

            int count = 0;
            foreach (var hero in clan.Heroes)
            {
                if (!hero.IsPrisoner) continue;

                var captor = hero.PartyBelongedToAsPrisoner;
                var captorClan = captor?.MobileParty?.ActualClan ?? captor?.Settlement?.OwnerClan;
                if (captorClan is null) continue;

                if (ResolveSide(captorClan.StringId, war) == captorSide) count++;
            }

            return count;
        }

        // Public resolution seam for the API (IPrivateWarsApi.ResolveWar). Looks up the war by id and
        // applies the terminal outcome against live campaign state, then returns the concluded snapshot,
        // or null if no war with that id exists.
        public PrivateWar? ResolveWarById(string warId, PrivateWarOutcome outcome)
        {
            var war = _privateWars.Get(warId);
            if (war is null) return null;

            ResolveWar(war, outcome);
            return _privateWars.Get(warId);
        }

        // Apply the terminal outcome to engine state: revert each captured fief to its pre-war owner.
        // On attacker victory the resolution leaves the main goal with the attacker as the prize; the
        // dignity transfer already rode in on the capture (FeudalTitleCampaignBehavior.OnSettlementOwnerChanged),
        // so it is not repeated here. The war record is marked concluded.
        private void ResolveWar(PrivateWar war, PrivateWarOutcome outcome)
        {
            var currentOwners = new Dictionary<string, string>();
            foreach (var settlementId in war.OriginalFiefOwners.Keys)
            {
                var ownerClan = Settlement.Find(settlementId)?.OwnerClan;
                if (ownerClan != null) currentOwners[settlementId] = ownerClan.StringId;
            }

            var plan = _resolvePrivateWar.Execute(war, outcome, currentOwners, clanId => ResolveSide(clanId, war));
            _logger.LogDebug("[PW] {War} resolved {Outcome} at score={Score:0.#}; reverts=[{Reverts}]",
                war.Id, outcome, war.Score,
                string.Join(",", plan.Reverts.Select(revert => $"{revert.SettlementId}->{revert.RevertToClanId}")));

            foreach (var revert in plan.Reverts)
            {
                var settlement = Settlement.Find(revert.SettlementId);
                var newOwner = Clan.All.FirstOrDefault(c => c.StringId == revert.RevertToClanId);
                if (settlement is null || newOwner is null || newOwner.IsEliminated) continue;
                if (settlement.OwnerClan == newOwner) continue;

                var hero = newOwner.Leader ?? newOwner.Heroes.FirstOrDefault();
                if (hero is null) continue;
                ChangeOwnerOfSettlementAction.ApplyByDefault(hero, settlement);
            }

            var concludedWar = war with { Status = PrivateWarStatus.Concluded };
            _privateWars.Update(concludedWar);
            ReleaseWarPrisoners(war);

            InfoPrinter.Display(
                $"Private war concluded: {war.AttackerPrincipalClanId} vs {war.DefenderPrincipalClanId} - {outcome}.");

            WarResolved?.Invoke(concludedWar, outcome);
        }

        // Which side of this war a clan fights on, walking up its suzerain chain to the nearest principal.
        private WarSide? ResolveSide(string clanId, PrivateWar war)
            => _sides.ResolveSide(clanId, war);

        // Release every captive this war put behind bars now that it has concluded. During the war
        // PrivateWarPrisonerRetentionPatch deliberately blocks the engine's involuntary auto-release of
        // a same-kingdom captive (peace sweeps, kingdom changes, our own siege capture) so the prize of
        // capturing the goal sticks; escape attempts are never blocked. Once the war ends that retention
        // no longer makes sense - the principals are at peace - so we deliberately free anyone held by the
        // opposing side. Must run AFTER the status flips to Concluded: AreEnemies is Active-gated, so a
        // concluded war no longer reports the pair as enemies and the retention patch lets the release
        // through. ApplyByPeace routes through the unblocked deliberate-release path.
        private void ReleaseWarPrisoners(PrivateWar war)
        {
            foreach (var hero in Hero.AllAliveHeroes.Where(h => h.IsPrisoner).ToList())
            {
                if (hero == Hero.MainHero || hero.Clan is null) continue;

                var captorParty = hero.PartyBelongedToAsPrisoner;
                var captorClan = captorParty?.MobileParty?.ActualClan ?? captorParty?.Settlement?.OwnerClan;
                if (captorClan is null) continue;

                var prisonerSide = ResolveSide(hero.Clan.StringId, war);
                var captorSide = ResolveSide(captorClan.StringId, war);
                if (prisonerSide is null || captorSide is null || prisonerSide == captorSide) continue;

                EndCaptivityAction.ApplyByPeace(hero);
            }
        }

        // The goal this party should besiege, or null if it is not an eligible party for any active
        // private war. The drive is symmetric: a party besieges the goal whenever the ENEMY side
        // currently holds it - so the attacker marches to take the goal, and once it falls the defender
        // marches to reclaim it, both through this one path. A side never besieges a fief its own side
        // already holds. The player drives their own clan, and a party mid-battle is left to the engine.
        private PrivateWarGoal? FindPrivateWarGoalFor(MobileParty mobileParty)
        {
            if (mobileParty is null || !mobileParty.IsActive || mobileParty.IsMainParty) return null;
            if (mobileParty.MapEvent != null) return null;

            var clan = mobileParty.ActualClan;
            if (clan is null || clan == Clan.PlayerClan) return null;

            foreach (var war in _privateWars.Snapshot())
            {
                if (war.Status != PrivateWarStatus.Active || war.MainGoalSettlementId is null) continue;

                var mySide = ResolveSide(clan.StringId, war);
                if (mySide is null) continue;

                var goal = Settlement.Find(war.MainGoalSettlementId);
                if (goal is null || !goal.IsFortification || goal.OwnerClan is null) continue;

                // Besiege only when the opposing side holds the goal (attacker to take, defender to
                // reclaim); skip when my own side already holds it.
                var ownerSide = ResolveSide(goal.OwnerClan.StringId, war);
                if (ownerSide is null || ownerSide == mySide) continue;
                return new PrivateWarGoal(war, mySide.Value, goal);
            }

            return null;
        }

        private sealed record PrivateWarGoal(
            PrivateWar War,
            WarSide Side,
            Settlement Goal);
    }
}
