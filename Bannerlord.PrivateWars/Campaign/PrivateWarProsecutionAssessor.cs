using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Armies;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Microsoft.Extensions.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// Whether a would-be attacker could take the goal at all: some attacker-side lord able to raise an
    /// army whose strength vanilla's own siege scoring finds worth a besiege (its 2x-defence gate
    /// included). Priced on a war record that is never registered: the side resolver resolves an
    /// unstored war fresh.
    /// </summary>
    public class PrivateWarProsecutionAssessor : IAssessPrivateWarProsecutionUseCase
    {
        private readonly IPrivateWarSidesProvider _sides;
        private readonly ILogger<PrivateWarProsecutionAssessor> _logger;

        public PrivateWarProsecutionAssessor(IPrivateWarSidesProvider sides, ILogger<PrivateWarProsecutionAssessor> logger)
        {
            _sides = sides;
            _logger = logger;
        }

        public bool CanProsecute(
            string attackerClanId,
            string defenderClanId,
            string mainGoalSettlementId,
            IReadOnlyCollection<string> attackerSupporters,
            IReadOnlyCollection<string> defenderSupporters)
        {
            var goal = Settlement.Find(mainGoalSettlementId);
            if (goal is null || !goal.IsFortification || goal.MapFaction is not Kingdom kingdom) return false;

            var war = new PrivateWar(
                Id: $"pw_assessment_{attackerClanId}_{defenderClanId}",
                AttackerPrincipalClanId: attackerClanId,
                DefenderPrincipalClanId: defenderClanId,
                AttackerSupporters: attackerSupporters,
                DefenderSupporters: defenderSupporters,
                CasusBelliType: string.Empty,
                TitleId: string.Empty,
                MainGoalSettlementId: mainGoalSettlementId,
                OriginalFiefOwners: new Dictionary<string, string>(),
                BattleScore: 0f,
                Score: 0f,
                StartDay: 0f,
                GoalLastTakenDay: 0f,
                Status: PrivateWarStatus.Active);

            var targetScore = TaleWorlds.CampaignSystem.Campaign.Current.Models.TargetScoreCalculatingModel;
            float bestScore = 0f, strongestArmy = 0f;
            int sideParties = 0, leaders = 0;
            foreach (WarPartyComponent component in kingdom.WarPartyComponents)
            {
                var party = component.MobileParty;
                var clan = party.ActualClan;
                if (clan is null || !party.IsActive || party.IsMainParty || clan == Clan.PlayerClan) continue;
                if (_sides.ResolveSide(clan.StringId, war) != WarSide.Attacker) continue;
                sideParties++;
                if (!PrivateWarLeadGate.CanLead(party)) continue;
                leaders++;

                var members = PrivateWarArmyPool.Gather(party, member =>
                    member.ActualClan is { } memberClan && _sides.ResolveSide(memberClan.StringId, war) == WarSide.Attacker);
                float strength = PrivateWarArmyPool.SiegeStrength(party) + members.Sum(PrivateWarArmyPool.SiegeStrength);
                strongestArmy = System.Math.Max(strongestArmy, strength);
                bestScore = System.Math.Max(bestScore, targetScore.GetTargetScoreForFaction(goal, Army.ArmyTypes.Besieger, party, strength));
            }

            _logger.LogDebug("[PW] prosecution check {Attacker} vs {Defender} on {Goal}: sideParties={SideParties} leaders={Leaders} strongestArmy={Strength:0} "
                             + "defence={Defence:0} bestScore={Score:0.##} -> {Result}",
                attackerClanId, defenderClanId, mainGoalSettlementId, sideParties, leaders, strongestArmy,
                goal.Parties.Where(p => p.IsGarrison || p.IsMilitia || p.Aggressiveness > 0.01f).Sum(p => p.Party.EstimatedStrength),
                bestScore, bestScore > 0f);
            return bestScore > 0f;
        }
    }
}
