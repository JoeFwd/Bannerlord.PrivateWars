using System.Linq;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>Reads a war's score inputs from current world state.</summary>
    public class PrivateWarObservationsReader
    {
        private readonly IPrivateWarSidesProvider _sides;

        public PrivateWarObservationsReader(IPrivateWarSidesProvider sides)
        {
            _sides = sides;
        }

        // Side membership is resolved through the feudal hierarchy so a clan fights with (or against) its
        // whole suzerain subtree. The main goal carries its own score term, so it is excluded from the
        // town/castle crossing counts.
        public PrivateWarObservations Read(PrivateWar war)
        {
            int defTownsByAtt = 0, defCastlesByAtt = 0, attTownsByDef = 0, attCastlesByDef = 0;
            bool attackerHoldsGoal = false;

            foreach (var entry in war.OriginalFiefOwners)
            {
                var settlement = Settlement.Find(entry.Key);
                if (settlement?.OwnerClan is null) continue;

                var currentSide = _sides.ResolveSide(settlement.OwnerClan.StringId, war);

                if (entry.Key == war.MainGoalSettlementId)
                {
                    attackerHoldsGoal = currentSide == WarSide.Attacker;
                    continue;
                }

                var originalSide = _sides.ResolveSide(entry.Value, war);
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

                var captorClan = PartyClan.OwnerClanOf(hero.PartyBelongedToAsPrisoner);
                if (captorClan is null) continue;

                if (_sides.ResolveSide(captorClan.StringId, war) == captorSide) count++;
            }

            return count;
        }
    }
}
