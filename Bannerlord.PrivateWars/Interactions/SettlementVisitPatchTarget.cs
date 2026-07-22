using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Interactions
{
    public static class SettlementVisitPatchTarget
    {
        private const string MethodName = "IsSettlementSuitableForVisitingCondition";

        public static MethodInfo Resolve()
            => typeof(AiVisitSettlementBehavior).GetMethod(
                   MethodName,
                   BindingFlags.Static | BindingFlags.NonPublic,
                   null,
                   new[] { typeof(MobileParty), typeof(Settlement) },
                   null)
               ?? throw new MissingMethodException(
                   typeof(AiVisitSettlementBehavior).FullName,
                   MethodName);
    }
}
