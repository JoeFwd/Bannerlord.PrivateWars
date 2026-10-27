using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PrivateWars.Util
{
    internal static class PartyClan
    {
        // The clan a party or settlement fights for: a mobile party's clan, else the settlement owner's.
        // Garrisons and militias never get an ActualClan; they fight for their home settlement's owner.
        public static Clan? OwnerClanOf(PartyBase? party)
            => party?.MobileParty?.ActualClan
               ?? (party?.MobileParty is { IsGarrison: true } or { IsMilitia: true } ? party.MobileParty.HomeSettlement?.OwnerClan : null)
               ?? party?.Settlement?.OwnerClan;
    }
}
