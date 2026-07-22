using Bannerlord.PrivateWars.DI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Patches
{
    // Single source of truth for "who defends a fortification in a same-kingdom private-war siege".
    //
    // The engine reaches the defender population through three independent, faction-war-gated seams that
    // must all admit the same set, or a surface drifts out of agreement:
    //   - EncounterModel.GetDefenderPartiesOfSettlement  (battle candidacy + siege-overlay presence)
    //   - EncounterModel.GetNextDefenderPartyOfSettlement (siege-overlay defender/attacker bucketing)
    //   - SiegeEvent.CanPartyJoinSide                     (assault MapEventSide membership)
    // The first two are model overrides (DadgEncounterModel); the third is a Harmony postfix
    // (SiegeDefenderJoinPatch). The shared rule lives here so all three consult one definition.
    //
    // This sits in the TaleWorlds-coupled layer, NOT the pure domain: it inspects engine party roles
    // (IsGarrison / IsMilitia / ActualClan / MapFaction). The pure predicate it composes over — "are
    // these two clans on opposite sides of an active private war" — already lives in the domain
    // (PrivateWarsServices.PrivateWarHostility, keyed on string ids).
    public static class PrivateWarSiegeDefenderPolicy
    {
        // Two clans are enemies when an active private war puts them on opposite sides. Null-safe and
        // init-safe: returns false (i.e. vanilla behaviour) when the registry is not available.
        public static bool AreEnemies(Clan? a, Clan? b)
        {
            if (!PrivateWarsServices.IsInitialised || PrivateWarsServices.PrivateWarHostility is null) return false;
            if (a is null || b is null) return false;
            return PrivateWarsServices.PrivateWarHostility.AreEnemies(a.StringId, b.StringId);
        }

        // A party stationed at or inside the besieged fortification counts as a defender when either:
        //   - it is the owner's own garrison or militia and the besieger is a private-war enemy of the
        //     settlement owner; or
        //   - it is a lord party that is itself a private-war enemy of the besieger (a belligerent on the
        //     owner's side of the feud).
        // Neutral same-kingdom parties are not defenders — vanilla ejects them from the siege.
        //
        // Militia mirror vanilla Town.GetDefenderParties exactly: they hold the walls in an assault but do
        // NOT sally out (battleType == SallyOut) — militia are static defenders, not field troops. The
        // garrison and any feud-belligerent lord parties sally normally. (BlockadeSallyOutBattle still
        // includes militia, matching vanilla, which gates only on SallyOut.)
        public static bool IsDefender(MobileParty? candidate, Clan? besiegerClan, Settlement? besieged,
            MapEvent.BattleTypes mapEventType)
        {
            if (candidate is null || besieged is null || !besieged.IsFortification) return false;

            if (candidate.IsGarrison || candidate.IsMilitia)
            {
                if (candidate.IsMilitia && mapEventType == MapEvent.BattleTypes.SallyOut)
                    return false;
                return candidate.MapFaction == besieged.MapFaction
                       && AreEnemies(besiegerClan, besieged.OwnerClan);
            }

            return AreEnemies(besiegerClan, candidate.ActualClan);
        }
    }
}
