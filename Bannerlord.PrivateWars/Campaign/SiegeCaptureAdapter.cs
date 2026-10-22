using System.Linq;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using Microsoft.Extensions.Logging;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// DADG drives the siege to a transfer of possession - NOT to the end of the war. A same-kingdom
    /// siege has no vanilla path to an assault (the assault/defender logic is gated on
    /// MapFaction.IsAtWarWith, which is false within one kingdom), so once a besieger has completed its
    /// preparations on a war fief, capture the fief directly: mirror the engine's own post-assault
    /// sequence (detach the besiegers, clear the routed settlement defenders, transfer ownership by
    /// siege). ApplyBySiege owns 1.4's garrison destruction and replacement; DADG must not retain or
    /// destroy that engine-owned party itself.
    /// <para>
    /// Possession can change hands repeatedly - the attacker takes the goal, the defender later
    /// reclaims it - and the war itself is resolved only when the score crosses +/-100 on the daily
    /// tick, so the contest has time to swing. The transfer goes to whichever side is besieging, as
    /// long as it is not the side that already holds the fief. RemoveAllSiegeParties only asserts
    /// against a live assault MapEvent, which an AI same-kingdom siege never creates, so it is safe.
    /// </para>
    /// </summary>
    public class SiegeCaptureAdapter
    {
        private readonly IPrivateWarRepository _privateWars;
        private readonly IPrivateWarSidesProvider _sides;
        private readonly PrivateWarSyntheticCapturePolicy _syntheticCapturePolicy;
        private readonly ILogger<SiegeCaptureAdapter> _logger;

        public SiegeCaptureAdapter(
            IPrivateWarRepository privateWars,
            IPrivateWarSidesProvider sides,
            PrivateWarSyntheticCapturePolicy syntheticCapturePolicy,
            ILogger<SiegeCaptureAdapter> logger)
        {
            _privateWars = privateWars;
            _sides = sides;
            _syntheticCapturePolicy = syntheticCapturePolicy;
            _logger = logger;
        }

        public void CaptureCompletedSieges()
        {
            foreach (var war in _privateWars.GetAll().ToList())
            {
                if (war.Status != PrivateWarStatus.Active || war.MainGoalSettlementId is null) continue;

                foreach (var settlementId in PrivateWarSyntheticCapturePolicy.WarFiefIds(war))
                    TryCaptureCompletedSiege(war, settlementId);
            }
        }

        private void TryCaptureCompletedSiege(PrivateWar war, string settlementId)
        {
            var fief = Settlement.Find(settlementId);
            var camp = fief?.SiegeEvent?.BesiegerCamp;
            if (fief is null || camp is null) return;

            var leaderParty = camp.LeaderParty;
            var leaderHero = leaderParty?.LeaderHero;
            var besiegerClan = leaderParty?.ActualClan;
            var besiegerSide = besiegerClan is null ? null : _sides.ResolveSide(besiegerClan.StringId, war);
            var ownerSide = fief.OwnerClan is null ? null : _sides.ResolveSide(fief.OwnerClan.StringId, war);
            bool hasUsableSiegeLeader = leaderParty?.IsActive == true
                && leaderHero?.PartyBelongedTo == leaderParty;
            var losingSide = _syntheticCapturePolicy.GetLosingSide(
                war,
                fief.StringId,
                camp.IsPreparationComplete,
                leaderParty?.IsMainParty == true,
                hasUsableSiegeLeader,
                besiegerSide,
                ownerSide);
            if (losingSide is null || leaderParty is null || leaderHero is null) return;

            // Owner-change callbacks can mutate the settlement's party list. Snapshot the losing
            // leaders first, then operate only on this immutable capture set after the transfer.
            var losingHeroes = fief.Parties
                .Where(inside => inside.MapEvent is null
                                 && inside.ActualClan is not null
                                 && _sides.ResolveSide(inside.ActualClan.StringId, war) == losingSide
                                 && inside.LeaderHero is not null)
                .Select(inside => inside.LeaderHero)
                .Distinct()
                .ToList();
            var capturerParty = leaderParty.Party;

            camp.RemoveAllSiegeParties();

            // Settlement.Party is the static defender roster used by the siege. It is not
            // Town.GarrisonParty; ApplyBySiege destroys and replaces that mobile garrison in 1.4.
            fief.Party.MemberRoster.Clear();
            ChangeOwnerOfSettlementAction.ApplyBySiege(leaderHero, leaderHero, fief);

            // ApplyBySiege is synchronous (OnSettlementOwnerChanged has already reset the fatigue
            // epoch). Do not take prisoners unless the requested ownership transfer actually completed.
            if (fief.OwnerClan != leaderHero.Clan)
            {
                _logger.LogWarning("[PW] {War} capture of {Fief} by {Leader} did not transfer ownership (owner={Owner})",
                    war.Id, fief.StringId, leaderHero.StringId, fief.OwnerClan?.StringId);
                return;
            }
            _logger.LogDebug("[PW] {War} {Fief} captured by {Leader} ({Clan}); prisoners=[{Losers}]",
                war.Id, fief.StringId, leaderHero.StringId, leaderHero.Clan?.StringId,
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
        }
    }
}
