using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using CampaignState = TaleWorlds.CampaignSystem.Campaign;

namespace Bannerlord.PrivateWars.Campaign
{
    /// <summary>
    /// Routes ordinary captivity to Bannerlord's untouched behavior and owns only the active
    /// private-war captivity path that Bannerlord cannot represent at faction grain.
    /// </summary>
    public sealed class DadgPlayerCaptivityCampaignBehavior : CampaignBehaviorBase, ICaptivityCampaignBehavior
    {
        private readonly PlayerCaptivityCampaignBehavior _vanilla;
        private readonly PrivateWarCaptivityPolicy _policy;

        public DadgPlayerCaptivityCampaignBehavior(
            PlayerCaptivityCampaignBehavior vanilla,
            PrivateWarCaptivityPolicy policy)
        {
            _vanilla = vanilla;
            _policy = policy;
        }

        public override void RegisterEvents()
        {
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        void ICaptivityCampaignBehavior.CheckCaptivityChange(float dt)
        {
            if (!IsPrivateWarCaptivity())
            {
                _vanilla.CheckCaptivityChange(dt);
                return;
            }

            CheckPrivateWarCaptivityChange();
        }

        private static bool IsPrivateWarCaptivity()
        {
            var captor = PlayerCaptivity.CaptorParty;
            var captorClan = captor?.MobileParty?.ActualClan ?? captor?.Settlement?.OwnerClan;
            return PrivateWarSiegeDefenderPolicy.AreEnemies(Hero.MainHero.Clan, captorClan);
        }

        // Bannerlord exposes the behavior interface but not a hook around its no-more-enemies branch.
        // This is the 1.4.6 private-war path, with the single release decision delegated to the domain.
        // The original behavior remains registered for its menus, events, save state and every ordinary
        // captivity update.
        private void CheckPrivateWarCaptivityChange()
        {
            var captor = PlayerCaptivity.CaptorParty;
            if (captor.IsMobile && !captor.MobileParty.IsActive)
            {
                GameMenu.SwitchToMenu("menu_captivity_end_by_party_removed");
                return;
            }

            if (captor.IsMobile && captor.MapFaction == Hero.MainHero.Clan)
            {
                GameMenu.SwitchToMenu("menu_captivity_end_by_ally_party_saved");
                return;
            }

            var captorFaction = captor.MapFaction;
            var playerFaction = MobileParty.MainParty.MapFaction;
            var privateEnemies = IsPrivateWarCaptivity();
            var sameFaction = captorFaction == playerFaction;
            if (_policy.ShouldReleaseForNoMoreEnemies(
                    FactionManager.IsAtWarAgainstFaction(captorFaction, playerFaction),
                    privateEnemies,
                    sameFaction,
                    !sameFaction && CampaignState.Current.Models.CrimeModel.IsPlayerCrimeRatingModerate(captorFaction),
                    !sameFaction && CampaignState.Current.Models.CrimeModel.IsPlayerCrimeRatingSevere(captorFaction)))
            {
                GameMenu.SwitchToMenu("menu_captivity_end_no_more_enemies");
                return;
            }

            if (captor.IsMobile && captor.MobileParty.CurrentSettlement != null &&
                captor.MobileParty.CurrentSettlement.IsTown &&
                captorFaction == captor.MobileParty.CurrentSettlement.MapFaction)
            {
                PlayerCaptivity.LastCheckTime = CampaignTime.Now;
                if (Game.Current.GameStateManager.ActiveState is MapState)
                    CampaignState.Current.LastTimeControlMode = CampaignState.Current.TimeControlMode;

                PlayerCaptivity.CaptorParty = captor.MobileParty.CurrentSettlement.Party;
                GameMenu.SwitchToMenu("menu_captivity_transfer_to_town");
                return;
            }

            if ((captor.IsSettlement && captor.Settlement.IsVillage) ||
                (captor.IsMobile && (captor.MobileParty.IsVillager || captor.MobileParty.IsCaravan)))
            {
                GameMenu.SwitchToMenu("menu_captivity_end_no_more_enemies");
                return;
            }

            var hoursToWait = (0.4f + CampaignState.Current.PlayerProgress * 0.4f) * CampaignTime.HoursInDay;
            hoursToWait *= Hero.MainHero.PartyBelongedToAsPrisoner.IsSettlement
                ? 2f
                : Hero.MainHero.PartyBelongedToAsPrisoner.IsMobile &&
                  Hero.MainHero.PartyBelongedToAsPrisoner.LeaderHero != null
                    ? 1.5f
                    : 1f;

            if (!HasElapsed(PlayerCaptivity.LastCheckTime, hoursToWait)) return;

            PlayerCaptivity.LastCheckTime = CampaignTime.Now;
            if (CampaignState.Current.PlayerCaptivity.CountOfOffers == 0)
            {
                CampaignState.Current.PlayerCaptivity.SetRansomAmount();
            }
            else
            {
                CampaignState.Current.PlayerCaptivity.CurrentRansomAmount = MathF.Max(
                    (int)(CampaignState.Current.PlayerCaptivity.CurrentRansomAmount * 0.8f -
                          CampaignState.Current.PlayerCaptivity.CountOfOffers * 0.05f),
                    1);
            }

            var randomFloat = MBRandom.RandomFloat;
            var battleEscapeBonus = 0f;
            if (captor.IsMobile && captor.MapEvent != null)
            {
                var captorSideCount = 0;
                var opposingSideCount = 0;
                foreach (PartyBase involvedParty in captor.MapEvent.InvolvedParties)
                {
                    if (involvedParty.Side == captor.Side)
                        captorSideCount += involvedParty.MemberRoster.TotalManCount;
                    else
                        opposingSideCount += involvedParty.MemberRoster.TotalManCount;
                }

                if (captorSideCount < opposingSideCount * 3f + 1f)
                {
                    battleEscapeBonus = 1f - captorSideCount / (opposingSideCount * 3f + 1f);
                    battleEscapeBonus /= 2f;
                }
            }

            var escapeChance = (CampaignState.Current.PlayerCaptivity.CountOfOffers + 1f) / 8f;
            if (battleEscapeBonus > 0f)
                escapeChance = MathF.Pow(escapeChance, 1f - battleEscapeBonus);

            if (Hero.MainHero.PartyBelongedToAsPrisoner != null)
            {
                if (Hero.MainHero.PartyBelongedToAsPrisoner.IsMobile &&
                    Hero.MainHero.PartyBelongedToAsPrisoner.LeaderHero != null)
                {
                    escapeChance *= MathF.Sqrt(escapeChance);
                }
                else if (Hero.MainHero.PartyBelongedToAsPrisoner.IsSettlement)
                {
                    escapeChance = !Hero.MainHero.PartyBelongedToAsPrisoner.Settlement.IsHideout
                        ? escapeChance * escapeChance
                        : escapeChance * MathF.Sqrt(escapeChance);
                }

                if (Hero.MainHero.PartyBelongedToAsPrisoner.IsMobile &&
                    !Hero.MainHero.PartyBelongedToAsPrisoner.MobileParty.IsCurrentlyAtSea &&
                    Hero.MainHero.GetPerkValue(DefaultPerks.Roguery.FleetFooted))
                {
                    escapeChance *= 1f + DefaultPerks.Roguery.FleetFooted.SecondaryBonus;
                }
            }

            if (randomFloat < escapeChance)
            {
                if (captor.IsMobile && captor.MapEvent != null)
                    GameMenu.SwitchToMenu("menu_escape_captivity_during_battle");
                else if (Hero.MainHero.CurrentSettlement == null)
                    GameMenu.SwitchToMenu("menu_captivity_end_wilderness_escape");
                else
                    GameMenu.SwitchToMenu("menu_captivity_end_prison_escape");
                return;
            }

            CampaignState.Current.PlayerCaptivity.CountOfOffers++;
            if (randomFloat < 0.5f &&
                CampaignState.Current.PlayerCaptivity.CurrentRansomAmount <= Hero.MainHero.Gold &&
                Hero.MainHero.PartyBelongedToAsPrisoner?.MapEvent == null)
            {
                GameMenu.SwitchToMenu(Hero.MainHero.CurrentSettlement != null
                    ? "menu_captivity_end_propose_ransom_in_prison"
                    : "menu_captivity_end_propose_ransom_wilderness");
            }
        }

        private static bool HasElapsed(CampaignTime beginTime, float hoursToWait)
            => hoursToWait * (0.5 + PlayerCaptivity.RandomNumber) < beginTime.ElapsedHoursUntilNow;
    }
}
