using System.Reflection;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.DI;
using Bannerlord.PrivateWars.Patches;
using Harmony.DependencyInjection.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Bannerlord.PrivateWars.Patches
{
    // Keep a private-war captive held (design §4.5 point 3 / plan §9 — the AI auto-end hazard).
    // PrisonerReleaseCampaignBehavior frees any prisoner whose MapFaction !IsAtWarWith the captor's
    // MapFaction; for a same-kingdom pair that test is always false, so the moment a fief changes
    // hands or the kingdom makes peace, the captive evaporates and the war's prisoner score with it.
    // All four of those release sites funnel through EndCaptivityAction.ApplyInternal, so a single
    // prefix there is the surgical fix: skip only the two involuntary "no longer at war" auto-releases
    // (ReleasedAfterPeace / ReleasedAfterBattle) of a hero whose clan is at private war with its captor.
    // Escape, ransom, deliberate release, death and real-war peace all fall through to vanilla.
    // The player's own captivity is a separate hazard (PlayerCaptivityCampaignBehavior, phase 4); the
    // main hero is left to vanilla here.
    public class PrivateWarPrisonerRetentionPatch : IPatch
    {
        private static readonly PrivateWarPrisonerRetentionPolicy Policy = new();

        public MethodInfo TargetMethod => PrivateWarHarmonyPatchTargets.PrisonerRelease();

        public MethodInfo? PatchMethod =>
            AccessTools.Method(typeof(PrivateWarPrisonerRetentionPatch), nameof(SkipPrivateWarRelease));

        public PatchType PatchType => PatchType.Prefix;

        private static bool SkipPrivateWarRelease(Hero prisoner, EndCaptivityDetail detail)
        {
            if (prisoner is null) return true;

            var reason = ToReleaseReason(detail);
            var captorParty = prisoner.PartyBelongedToAsPrisoner;
            var captorClan = captorParty?.MobileParty?.ActualClan ?? captorParty?.Settlement?.OwnerClan;
            bool arePrivateEnemies = PrivateWarPatchHelper.AreEnemies(prisoner.Clan, captorClan);

            return Policy.ShouldAllowRelease(prisoner == Hero.MainHero, reason, arePrivateEnemies);
        }

        private static PrivateWarPrisonerReleaseReason ToReleaseReason(EndCaptivityDetail detail)
        {
            return detail switch
            {
                EndCaptivityDetail.Ransom => PrivateWarPrisonerReleaseReason.Ransom,
                EndCaptivityDetail.ReleasedAfterPeace => PrivateWarPrisonerReleaseReason.AfterPeace,
                EndCaptivityDetail.ReleasedAfterBattle => PrivateWarPrisonerReleaseReason.AfterBattle,
                EndCaptivityDetail.ReleasedAfterEscape => PrivateWarPrisonerReleaseReason.Escape,
                EndCaptivityDetail.ReleasedByChoice => PrivateWarPrisonerReleaseReason.DeliberateRelease,
                EndCaptivityDetail.Death => PrivateWarPrisonerReleaseReason.Death,
                EndCaptivityDetail.ReleasedByCompensation => PrivateWarPrisonerReleaseReason.Compensation,
                _ => PrivateWarPrisonerReleaseReason.DeliberateRelease
            };
        }
    }
}
