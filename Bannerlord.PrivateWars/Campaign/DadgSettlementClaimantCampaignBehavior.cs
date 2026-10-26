using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// Replaces vanilla SettlementClaimantCampaignBehavior so the king keeps every fief that
    /// ChangeOwnerOfSettlementAction opens to claim (siege, a clan leaving, a clan destroyed), and with it
    /// any title that follows the seat. Vanilla flags such a fief Town.IsOwnerUnassigned and its daily tick
    /// auctions it through a SettlementClaimantDecision; this copy never sets the flag and has no daily
    /// tick, and keeps the rest of the stock owner-change duties verbatim. Explicit votes stay vanilla: a
    /// relinquished fief (KingdomManager.RelinquishSettlementOwnership) and an annexation that passed its
    /// preliminary vote raise their decision directly.
    /// shortcut: kings accumulate open fiefs until a delegation behaviour hands them on.
    /// </summary>
    public sealed class DadgSettlementClaimantCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore) { }

        // Saves made under the vanilla behaviour can carry a flagged fief and the auction queued for it.
        private void OnGameLoaded(CampaignGameStarter campaignGameStarter)
        {
            foreach (var settlement in Settlement.All)
            {
                if (settlement.Town is null || !settlement.Town.IsOwnerUnassigned) continue;

                settlement.Town.IsOwnerUnassigned = false;
                RemoveClaimantDecisions(settlement, includePreliminary: false);
            }
        }

        // Vanilla's OnSettlementOwnerChanged minus the IsOwnerUnassigned branch.
        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
            Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (settlement.IsVillage && settlement.Party.MapEvent != null
                && !FactionManager.IsAtWarAgainstFaction(settlement.Party.MapEvent.AttackerSide.LeaderParty.MapFaction, newOwner.MapFaction))
            {
                settlement.Party.MapEvent.FinalizeEvent();
            }

            RemoveClaimantDecisions(settlement, includePreliminary: true);

            if (oldOwner.Clan != Clan.PlayerClan || (newOwner != null && newOwner.Clan == Clan.PlayerClan)) return;

            foreach (var element in settlement.Stash)
                settlement.ItemRoster.AddToCounts(element.EquipmentElement.Item, element.Amount);
            settlement.Stash.Clear();
        }

        private static void RemoveClaimantDecisions(Settlement settlement, bool includePreliminary)
        {
            foreach (var kingdom in Kingdom.All)
            {
                foreach (var decision in kingdom.UnresolvedDecisions.ToList())
                {
                    if ((decision is SettlementClaimantDecision claimant && claimant.Settlement == settlement)
                        || (includePreliminary && decision is SettlementClaimantPreliminaryDecision preliminary
                            && preliminary.Settlement == settlement))
                        kingdom.RemoveDecision(decision);
                }
            }
        }
    }
}
