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

namespace Bannerlord.PrivateWars.Campaign
{
    /**
     * <summary>
     *  Owns the registry lifecycle and the war's prosecution drive. Persists active and concluded
     *  wars through Bannerlord saves and restores them on load; and, because the engine never plans
     *  a same-kingdom siege on its own (design §5, risk #1), injects the war goal as a scored besiege
     *  candidate into each attacker party's AI think so it competes in the engine's normal behaviour
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
        // It also competes with AiArmyMemberBehavior's escort-the-leader score (10 min - 20 max) for an
        // attached member, so at 12f the besieger stays in a healthy army (escort ~20 wins) and only
        // quits to prosecute when escorting is weak — keeping the quit a scored decision, not a forced one.
        // Flat tuning value — revisit via AI-vs-AI playtest.
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
        private readonly WarSideResolver _sideResolver;
        private readonly ISuzerainProvider _hierarchy;
        private readonly PrivateWarArmyDecisionAdapter _armyDecisionAdapter;
        private readonly PrivateWarSyntheticCapturePolicy _syntheticCapturePolicy;

        private List<string> _serialisedWars = new();

        public PrivateWarCampaignBehavior(
            IPrivateWarRepository privateWars,
            ITickPrivateWarUseCase tickPrivateWar,
            IResolvePrivateWarUseCase resolvePrivateWar,
            IApplyBattleOutcomeUseCase applyBattleOutcome,
            WarSideResolver sideResolver,
            ISuzerainProvider hierarchy,
            PrivateWarArmyDecisionAdapter armyDecisionAdapter,
            PrivateWarSyntheticCapturePolicy syntheticCapturePolicy)
        {
            _privateWars = privateWars;
            _tickPrivateWar = tickPrivateWar;
            _resolvePrivateWar = resolvePrivateWar;
            _applyBattleOutcome = applyBattleOutcome;
            _sideResolver = sideResolver;
            _hierarchy = hierarchy;
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
        private void OnAiHourlyTick(MobileParty mobileParty, PartyThinkParams p)
        {
            var objective = FindPrivateWarGoalFor(mobileParty);
            if (objective is null) return;

            var armyMembers = GetPrivateWarArmyMembers(mobileParty, objective);

            // While en route, use the low selection score so a higher-priority objective (notably
            // defending the clan's own besieged fief) still wins. Once the party has actually established
            // the siege on the goal, switch to the dominant hold score so the engine's periodic re-think
            // does not abandon the camp before preparations complete.
            float score = mobileParty.BesiegedSettlement == objective.Goal
                ? PrivateWarSiegeHoldScore
                : PrivateWarSiegeSelectionScore;

            var behaviour = new AIBehaviorData(
                objective.Goal, AiBehavior.BesiegeSettlement, MobileParty.NavigationType.Default,
                willGatherArmy: armyMembers != null, isFromPort: false, isTargetingPort: false);
            p.AddBehaviorScore((behaviour, score));
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
                _hierarchy.GetSuzerain,
                kingdom.Settlements.Count > 0,
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
                objective is null ? string.Empty : leaderParty!.StringId,
                objective?.War.MainGoalSettlementId ?? string.Empty,
                () =>
                {
                    freshMembers = GetPrivateWarArmyMembers(leaderParty!, objective!);
                    return freshMembers?.Select(member => member.StringId).ToList();
                });

            if (decision.Action == PrivateWarArmyCreationAction.ReplaceMembers)
                replacementMembers = freshMembers;

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

                var observations = BuildObservations(war);
                var result = _tickPrivateWar.Execute(war, observations, currentDay);
                var scored = war with { Score = result.Score };

                if (result.Outcome is { } outcome)
                    ResolveWar(scored, outcome);
                else
                    _privateWars.Update(scored);
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
                if (goal.OwnerClan != leaderHero.Clan) continue;

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

            _privateWars.Update(war with { Status = PrivateWarStatus.Concluded });
            ReleaseWarPrisoners(war);

            InfoPrinter.Display(
                $"Private war concluded: {war.AttackerPrincipalClanId} vs {war.DefenderPrincipalClanId} - {outcome}.");
        }

        // Which side of this war a clan fights on, walking up its suzerain chain to the nearest principal.
        private WarSide? ResolveSide(string clanId, PrivateWar war)
            => _sideResolver.ResolveSide(clanId, war, _hierarchy.GetSuzerain);

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
        // An attached army member still gets the goal injected so that quitting the army to prosecute (or
        // defend) the feud is a *scored* AI decision: the besiege candidate competes with
        // AiArmyMemberBehavior's escort-the-leader score (10-20) in the same vote.
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
