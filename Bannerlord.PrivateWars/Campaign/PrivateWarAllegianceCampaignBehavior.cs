using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Microsoft.Extensions.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// Decides, per belligerent clan, whether its lords answer the crown's war or pursue their feud
    /// (<see cref="PrivateWarAllegiancePolicy"/>). Re-judged once a day, with the previous answer as the
    /// hysteresis anchor; a clan seen for the first time is judged on demand.
    /// </summary>
    public class PrivateWarAllegianceCampaignBehavior : CampaignBehaviorBase
    {
        private readonly IPrivateWarRepository _privateWars;
        private readonly IPrivateWarSidesProvider _sides;
        private readonly PrivateWarAllegiancePolicy _policy;
        private readonly ILogger<PrivateWarAllegianceCampaignBehavior> _logger;
        private readonly Dictionary<string, PrivateWarTheatre> _theatres = new();

        public PrivateWarAllegianceCampaignBehavior(
            IPrivateWarRepository privateWars,
            IPrivateWarSidesProvider sides,
            PrivateWarAllegiancePolicy policy,
            ILogger<PrivateWarAllegianceCampaignBehavior> logger)
        {
            _privateWars = privateWars;
            _sides = sides;
            _policy = policy;
            _logger = logger;
        }

        // A DI singleton outlives the campaign, so each new campaign starts with no cached theatres.
        public override void RegisterEvents()
        {
            _theatres.Clear();
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        // shortcut: theatres are not saved, so a load re-judges every clan once without hysteresis;
        // persist _theatres if lords visibly flip theatre right after loading.
        public override void SyncData(IDataStore dataStore)
        {
        }

        public PrivateWarTheatre GetTheatre(Clan clan)
        {
            if (!_theatres.TryGetValue(clan.StringId, out var theatre))
                _theatres[clan.StringId] = theatre = Judge(clan, current: null);
            return theatre;
        }

        // Every active war the clan fights in, with its side; wars it leads come first.
        public IEnumerable<(PrivateWar War, WarSide Side)> SidesOf(Clan clan)
            => _privateWars.GetAll()
                .Where(war => war.Status == PrivateWarStatus.Active && war.MainGoalSettlementId is not null)
                .Select(war => (War: war, Side: _sides.ResolveSide(clan.StringId, war)))
                .Where(entry => entry.Side is not null)
                .Select(entry => (entry.War, entry.Side!.Value))
                .OrderByDescending(entry => IsPrincipal(clan, entry.War));

        public WarSide? ResolveSide(Clan clan, PrivateWar war) => _sides.ResolveSide(clan.StringId, war);

        private void OnDailyTick()
        {
            foreach (var clanId in _theatres.Keys.ToList())
            {
                var clan = Clan.FindFirst(c => c.StringId == clanId);
                if (clan is null) _theatres.Remove(clanId);
                else _theatres[clanId] = Judge(clan, _theatres[clanId]);
            }
        }

        private PrivateWarTheatre Judge(Clan clan, PrivateWarTheatre? current)
        {
            var (war, side) = SidesOf(clan).FirstOrDefault();
            if (war is null || clan.Kingdom is not { } kingdom) return PrivateWarTheatre.Private;

            var leader = clan.Leader;
            var ruler = kingdom.Leader;
            var principalId = side == WarSide.Attacker ? war.AttackerPrincipalClanId : war.DefenderPrincipalClanId;
            var principal = Clan.FindFirst(c => c.StringId == principalId)?.Leader;
            var inputs = new PrivateWarAllegianceInputs(
                IsPrincipal: IsPrincipal(clan, war),
                IsRuler: ruler is not null && ruler == leader,
                RealmAtWar: kingdom.FactionsAtWarWith.Any(faction => faction.Fiefs.Any()),
                OwnFiefThreatenedByRealmEnemy: clan.Fiefs.Any(fief => Besieger(fief.Settlement)?.MapFaction.IsAtWarWith(kingdom) == true),
                OwnFiefThreatenedByFeudEnemy: clan.Fiefs.Any(fief => IsEnemyOf(Besieger(fief.Settlement)?.ActualClan, war, side)),
                OwnFiefHeldByFeudEnemy: war.OriginalFiefOwners.Any(entry =>
                    entry.Value == clan.StringId && IsEnemyOf(Settlement.Find(entry.Key)?.OwnerClan, war, side)),
                RelationWithRuler: leader is null || ruler is null ? 0 : leader.GetRelation(ruler),
                RelationWithPrincipal: leader is null || principal is null ? 0 : leader.GetRelation(principal),
                Honor: leader?.GetTraitLevel(DefaultTraits.Honor) ?? 0,
                Calculating: leader?.GetTraitLevel(DefaultTraits.Calculating) ?? 0,
                Current: current);

            var theatre = _policy.Choose(inputs);
            if (theatre != current)
                _logger.LogDebug("[PW] {Clan} answers {Theatre} ({War} {Side}): {Inputs}", clan.StringId, theatre, war.Id, side, inputs);
            return theatre;
        }

        private static bool IsPrincipal(Clan clan, PrivateWar war)
            => war.AttackerPrincipalClanId == clan.StringId || war.DefenderPrincipalClanId == clan.StringId;

        private static TaleWorlds.CampaignSystem.Party.MobileParty? Besieger(Settlement settlement)
            => settlement.SiegeEvent?.BesiegerCamp?.LeaderParty;

        private bool IsEnemyOf(Clan? clan, PrivateWar war, WarSide side)
            => clan is not null && ResolveSide(clan, war) is { } other && other != side;
    }
}
