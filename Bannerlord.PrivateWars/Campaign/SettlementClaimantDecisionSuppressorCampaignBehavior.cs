using System.Linq;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// Suppresses the one branch of Bannerlord's SettlementClaimantCampaignBehavior that would
    /// auction a contested fief out from under the war that is being fought over it.
    ///
    /// ChangeOwnerOfSettlementAction.ApplyBySiege marks every captured fortification open to claim;
    /// the stock behaviour turns that into Town.IsOwnerUnassigned whenever the new owner's kingdom
    /// holds more than one clan, and its daily tick then raises a SettlementClaimantDecision there.
    /// A private war is intra-kingdom by definition, so that fires on every synthetic capture, and
    /// the resulting election can hand the goal to the defender or to an uninvolved clan while the
    /// war is still active - which ResolvePrivateWarUseCase then reads as a third party holding the
    /// fief and declines to revert.
    ///
    /// IsOwnerUnassigned is the sole gate on decision creation, so clearing it for contested fiefs is
    /// enough. The stock behaviour stays registered and keeps full vanilla ownership of every other
    /// settlement.
    /// </summary>
    public sealed class SettlementClaimantDecisionSuppressorCampaignBehavior : CampaignBehaviorBase
    {
        private readonly IPrivateWarRepository _privateWars;

        public SettlementClaimantDecisionSuppressorCampaignBehavior(IPrivateWarRepository privateWars)
        {
            _privateWars = privateWars;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
        }

        public override void SyncData(IDataStore dataStore) { }

        // MbEvent prepends listeners, so a behaviour registered later runs first. That rules out clearing
        // the flag on OnSettlementOwnerChanged (this would run before the stock listener sets it), but on
        // the daily tick it means this runs before the stock listener reads the flag.
        private void OnDailyTickSettlement(Settlement settlement)
        {
            if (settlement.Town is null || !settlement.Town.IsOwnerUnassigned) return;
            if (!IsContested(settlement.StringId)) return;

            settlement.Town.IsOwnerUnassigned = false;
        }

        // Saves written before this behaviour existed can carry an already-flagged fief, or a decision
        // already queued against one. Sweep both at load, ahead of the first daily tick. This must run
        // after PrivateWarCampaignBehavior.OnGameLoaded has restored the war records, which the
        // registration order in SubModule guarantees.
        private void OnGameLoaded(CampaignGameStarter campaignGameStarter)
        {
            foreach (var settlement in Settlement.All)
            {
                if (settlement.Town is null || !IsContested(settlement.StringId)) continue;

                settlement.Town.IsOwnerUnassigned = false;

                var kingdom = settlement.OwnerClan?.Kingdom;
                if (kingdom is null) continue;

                foreach (var decision in kingdom.UnresolvedDecisions.ToList())
                {
                    if (decision is SettlementClaimantDecision claimant && claimant.Settlement == settlement)
                        kingdom.RemoveDecision(decision);
                }
            }
        }

        // The main goal plus every fief the war snapshotted at declaration: the resolution reverts
        // against that snapshot, so anything in it is still the war's to award.
        private bool IsContested(string settlementId)
            => _privateWars.GetAll().Any(war => war.Status == PrivateWarStatus.Active
                && (war.MainGoalSettlementId == settlementId
                    || war.OriginalFiefOwners.ContainsKey(settlementId)));
    }
}
