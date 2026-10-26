using Bannerlord.PrivateWars.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PrivateWars.Patches
{
    // Shared, null-safe gateway the §4.2 hostility patches use to consult the private-war registry.
    // Two clans are enemies when they sit on opposite sides of an active private war. Delegates to the
    // single rule definition (PrivateWarSiegeDefenderPolicy.AreEnemies) so the patch layer and the
    // game-model layer cannot drift apart; falls back to vanilla (false) when the registry is unavailable.
    internal static class PrivateWarPatchHelper
    {
        public static bool AreEnemies(Clan? a, Clan? b)
            => PrivateWarSiegeDefenderPolicy.AreEnemies(a, b);

        // A settlement party (wall defender) fights for its owner's clan.
        public static Clan? ClanOf(PartyBase? party) => party?.MobileParty?.ActualClan ?? party?.Settlement?.OwnerClan;
    }
}
