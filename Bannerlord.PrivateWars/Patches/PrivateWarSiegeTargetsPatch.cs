using System;
using System.Reflection;
using Bannerlord.PrivateWars.Campaign;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.Patches
{
    // The private war as a "virtual kingdom": vanilla's siege planner only walks the settlements of
    // factions its realm is at war with, which never includes a same-kingdom feud enemy. For a lord
    // prosecuting its feud (PrivateWarFronts), feed the enemy side's war fiefs through vanilla's own
    // per-settlement scoring instead - its strength gate (2x the defence) is what keeps an outnumbered
    // army from besieging - and skip the realm's sieges and raids. Defence is left to vanilla.
    public class PrivateWarSiegeTargetsPatch : IPatch
    {
        private static PrivateWarFronts _fronts = null!;
        private static Func<AiMilitaryBehavior, PartyThinkParams, Army.ArmyTypes, float> _foodScore = null!;
        private static Func<AiMilitaryBehavior, MobileParty, Army.ArmyTypes, float> _partySizeScore = null!;
        private static Func<AiMilitaryBehavior, Settlement, MobileParty, Army.ArmyTypes, bool, bool> _isSuitable = null!;
        private static Action<AiMilitaryBehavior, Settlement, Army.ArmyTypes, AiBehavior, PartyThinkParams, float, float, float, float> _scoreSettlement = null!;

        public PrivateWarSiegeTargetsPatch(PrivateWarFronts fronts)
        {
            _fronts = fronts;
            _foodScore = AccessTools.MethodDelegate<Func<AiMilitaryBehavior, PartyThinkParams, Army.ArmyTypes, float>>(
                PrivateWarHarmonyPatchTargets.GetFoodScoreForActionType());
            _partySizeScore = AccessTools.MethodDelegate<Func<AiMilitaryBehavior, MobileParty, Army.ArmyTypes, float>>(
                PrivateWarHarmonyPatchTargets.GetPartySizeScore());
            _isSuitable = AccessTools.MethodDelegate<Func<AiMilitaryBehavior, Settlement, MobileParty, Army.ArmyTypes, bool, bool>>(
                PrivateWarHarmonyPatchTargets.CheckIfSettlementIsSuitableForMilitaryAction());
            _scoreSettlement = AccessTools.MethodDelegate<Action<AiMilitaryBehavior, Settlement, Army.ArmyTypes, AiBehavior, PartyThinkParams, float, float, float, float>>(
                PrivateWarHarmonyPatchTargets.CalculateMilitaryBehaviorForSettlement());
        }

        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.FindBestTargetAndItsValueForFaction();

        public MethodInfo? PatchMethod => AccessTools.Method(typeof(PrivateWarSiegeTargetsPatch), nameof(Prefix));

        public PatchType PatchType => PatchType.Prefix;

        private static bool Prefix(AiMilitaryBehavior __instance, Army.ArmyTypes missionType, PartyThinkParams p, float ourStrength)
        {
            if (missionType == Army.ArmyTypes.Defender) return true;

            var front = _fronts.FindPrivateFront(p.MobilePartyOf);
            if (front is null) return true;

            if (missionType == Army.ArmyTypes.Besieger) ScoreFront(__instance, p, ourStrength, front);
            return false;
        }

        // Vanilla's FindBestTargetAndItsValueForFaction preamble for a besieger, over the front's targets.
        private static void ScoreFront(AiMilitaryBehavior behaviour, PartyThinkParams p, float ourStrength, PrivateWarFront front)
        {
            var party = p.MobilePartyOf;
            if (party.Army != null && party.Army.LeaderParty != party) return;

            float cohesion = party.Army is { Cohesion: < 40f } army ? army.Cohesion / 40f : 1f;
            if (cohesion <= 0.25f) return;

            float food = _foodScore(behaviour, p, Army.ArmyTypes.Besieger);
            if (food <= 0f) return;

            float partySize = _partySizeScore(behaviour, party, Army.ArmyTypes.Besieger);
            foreach (var target in front.Targets)
            {
                if (_isSuitable(behaviour, target, party, Army.ArmyTypes.Besieger, p.WillGatherAnArmy))
                    _scoreSettlement(behaviour, target, Army.ArmyTypes.Besieger, AiBehavior.BesiegeSettlement, p, ourStrength, partySize, cohesion, food);
            }
        }
    }
}
