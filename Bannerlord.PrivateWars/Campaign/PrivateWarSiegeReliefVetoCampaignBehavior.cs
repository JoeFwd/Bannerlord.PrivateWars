using System.Collections.Generic;
using Bannerlord.PrivateWars.Domain.Port;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// Vanilla's defender scoring treats any besieged settlement of the party's own kingdom as one to
    /// relieve, so a private-war siege drew the whole kingdom - the besieger's own side included - into
    /// DefendSettlement on it (observed: every attacker-side party "defending" its own siege of
    /// Kenilworth). A same-kingdom siege is a feud, so only the besieger's private-war opponents relieve it.
    /// </summary>
    public class PrivateWarSiegeReliefVetoCampaignBehavior : CampaignBehaviorBase
    {
        private readonly IPrivateWarHostility _hostility;

        public PrivateWarSiegeReliefVetoCampaignBehavior(IPrivateWarHostility hostility)
        {
            _hostility = hostility;
        }

        public override void RegisterEvents()
            => CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnAiHourlyTick(MobileParty mobileParty, PartyThinkParams p)
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
                if (_hostility.AreEnemies(clan.StringId, besiegerClan.StringId)) continue;

                (vetoed ??= new List<AIBehaviorData>()).Add(behaviour);
            }

            if (vetoed is null) return;
            foreach (var behaviour in vetoed)
                p.SetBehaviorScore(behaviour, 0f);
        }
    }
}
