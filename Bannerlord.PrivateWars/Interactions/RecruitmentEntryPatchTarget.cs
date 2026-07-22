using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Interactions
{
    public static class RecruitmentEntryPatchTarget
    {
        public static MethodInfo Resolve()
            => typeof(RecruitmentCampaignBehavior).GetMethod(
                   nameof(RecruitmentCampaignBehavior.OnBeforeSettlementEntered),
                   BindingFlags.Instance | BindingFlags.Public,
                   null,
                   new[] { typeof(MobileParty), typeof(Settlement), typeof(Hero) },
                   null)
               ?? throw new MissingMethodException(
                   typeof(RecruitmentCampaignBehavior).FullName,
                   nameof(RecruitmentCampaignBehavior.OnBeforeSettlementEntered));
    }
}
