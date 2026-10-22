using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PrivateWars.Util
{
    internal static class PartyClan
    {
        // The clan a party or settlement fights for: a mobile party's clan, else the settlement owner's.
        public static Clan? OwnerClanOf(PartyBase? party)
            => party?.MobileParty?.ActualClan ?? party?.Settlement?.OwnerClan;
    }
}
