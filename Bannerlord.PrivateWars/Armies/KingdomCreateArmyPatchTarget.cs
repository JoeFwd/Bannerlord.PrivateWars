using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Bannerlord.PrivateWars.Armies
{
    public static class KingdomCreateArmyPatchTarget
    {
        public static MethodInfo Resolve()
            => typeof(Kingdom).GetMethod(
                   nameof(Kingdom.CreateArmy),
                   new[]
                   {
                       typeof(Hero),
                       typeof(Settlement),
                       typeof(Army.ArmyTypes),
                       typeof(MBReadOnlyList<MobileParty>)
                   })
               ?? throw new MissingMethodException(
                   typeof(Kingdom).FullName,
                   nameof(Kingdom.CreateArmy));
    }
}
