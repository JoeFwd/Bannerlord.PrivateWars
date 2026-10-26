using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>A side of a private war seen as a "virtual kingdom": the war fiefs its enemy holds.</summary>
    public sealed record PrivateWarFront(PrivateWar War, WarSide Side, IReadOnlyList<Settlement> Targets);

    /// <summary>
    /// Which feud, if any, a party prosecutes instead of its realm's wars: its clan answers the feud
    /// (<see cref="PrivateWarAllegianceCampaignBehavior"/>) and its side has a war fief to besiege. A
    /// feud with nothing left to besiege leaves the lord to the crown.
    /// </summary>
    public class PrivateWarFronts
    {
        private readonly PrivateWarAllegianceCampaignBehavior _allegiance;

        public PrivateWarFronts(PrivateWarAllegianceCampaignBehavior allegiance)
        {
            _allegiance = allegiance;
        }

        public PrivateWarFront? FindPrivateFront(MobileParty? party)
        {
            var clan = party?.ActualClan;
            if (party is null || !party.IsActive || party.IsMainParty || clan is null || clan == Clan.PlayerClan) return null;

            foreach (var (war, side) in _allegiance.SidesOf(clan))
            {
                if (_allegiance.GetTheatre(clan) != PrivateWarTheatre.Private) return null;

                var targets = FindTargets(party, war, side);
                if (targets.Count > 0) return new PrivateWarFront(war, side, targets);
            }

            return null;
        }

        public bool IsSameFront(PrivateWarFront front, MobileParty party)
            => FindPrivateFront(party) is { } other && other.War.Id == front.War.Id && other.Side == front.Side;

        public bool IsActiveWarArmy(Army army) => FindPrivateFront(army.LeaderParty) is not null;

        public WarSide? ResolveSide(Clan clan, PrivateWar war) => _allegiance.ResolveSide(clan, war);

        // The war fiefs this side should besiege: every fortification snapshotted at declaration that the
        // opposing side now holds within the party's realm, plus the goal. The drive is symmetric - each
        // side besieges what the other holds, so a fief taken can be retaken - and a side never besieges a
        // fief its own side holds. The goal alone is also besieged when a realm at war with the party's
        // holds it (a foreign enemy who took it); other fiefs held across a realm border are left to vanilla.
        public List<Settlement> FindTargets(MobileParty mobileParty, PrivateWar war, WarSide side)
        {
            var targets = new List<Settlement>();
            foreach (var settlementId in war.WarFiefIds())
            {
                var settlement = Settlement.Find(settlementId);
                if (settlement is null || !settlement.IsFortification || settlement.OwnerClan is null) continue;

                var ownerSide = ResolveSide(settlement.OwnerClan, war);
                if (settlementId == war.MainGoalSettlementId)
                {
                    bool atWarWithOwner = FactionManager.IsAtWarAgainstFaction(mobileParty.MapFaction, settlement.OwnerClan.MapFaction);
                    if (!PrivateWarGoalPursuitPolicy.ShouldBesiegeGoal(side, ownerSide, atWarWithOwner)) continue;
                }
                else if (ownerSide is null || ownerSide == side || settlement.MapFaction != mobileParty.MapFaction)
                {
                    continue;
                }

                targets.Add(settlement);
            }

            return targets;
        }

        public bool IsEnemyHeld(Settlement settlement, PrivateWarFront front)
            => settlement.OwnerClan is { } owner && ResolveSide(owner, front.War) is { } side && side != front.Side;
    }
}
