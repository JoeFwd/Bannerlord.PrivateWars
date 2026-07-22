using Bannerlord.PrivateWars.DI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Bannerlord.PrivateWars.GameModels
{
    // Retention signal for a DADG-driven private-war siege.
    //
    // The base method returns 0 for a besieger/raider whose target shares its MapFaction
    // (FactionManager.IsAtWarAgainstFaction short-circuits f1==f2 — design §2), so a same-kingdom
    // feud target scores 0 by default. This is NOT the lever that *starts* a private siege: the
    // stock siege planner (AiMilitaryBehavior.FindBestTargetAndItsValueForFaction) only ever
    // enumerates settlements of factions in FactionsAtWarWith, which never contains the attacker's
    // own kingdom, so the goal is never even a candidate (design §15 risk #1 — confirmed against the
    // 1.3.1 decompile). DADG injects the goal as a scored candidate instead (PrivateWarCampaignBehavior
    // .OnAiHourlyTick). This override is the retention half: it keeps the engine from *abandoning* that
    // siege on the next AI think by returning a positive score when the party's clan is at private war
    // with the target's owner.
    public class PrivateWarTargetScoreCalculatingModel : DefaultTargetScoreCalculatingModel
    {
        // Strong, flat retention score. The consuming planner multiplies this by distance/cohesion/
        // party-size/food factors (all <= 1), so the raw value must dominate the abandon threshold.
        // Tuning value; revisit once GABS AI-vs-AI verification is possible.
        private const float PrivateWarSiegeRetentionScore = 20f;

        public override float GetTargetScoreForFaction(
            Settlement targetSettlement, Army.ArmyTypes missionType, MobileParty mobileParty, float ourStrength)
        {
            float baseScore = base.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, ourStrength);
            if (baseScore > 0f || !PrivateWarsServices.IsInitialised || PrivateWarsServices.PrivateWarHostility is null)
                return baseScore;

            if (missionType != Army.ArmyTypes.Besieger && missionType != Army.ArmyTypes.Raider)
                return baseScore;

            var attackerClan = mobileParty?.ActualClan;
            var ownerClan = targetSettlement?.OwnerClan;
            if (attackerClan is null || ownerClan is null)
                return baseScore;

            return PrivateWarsServices.PrivateWarHostility.AreEnemies(attackerClan.StringId, ownerClan.StringId)
                ? PrivateWarSiegeRetentionScore
                : baseScore;
        }
    }
}
