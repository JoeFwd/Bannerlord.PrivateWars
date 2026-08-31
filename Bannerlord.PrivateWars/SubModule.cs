using System;
using System.Linq;
using Bannerlord.PrivateWars.Campaign;
using Bannerlord.PrivateWars.DI;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.GameModels;
using Bannerlord.PrivateWars.Patches;
using Bannerlord.UIExtenderEx;
using Harmony.DependencyInjection;
using Harmony.DependencyInjection.Patches;
using Microsoft.Extensions.DependencyInjection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using CampaignState = TaleWorlds.CampaignSystem.Campaign;

namespace Bannerlord.PrivateWars
{
    /// <summary>
    /// Self-wires the private-war mechanism: builds its own DI container, applies its Harmony patches,
    /// registers its nameplate UI mixins, and on campaign start installs the game models, campaign
    /// behaviours, and encounter menus while replacing the two vanilla behaviours it must intercept.
    /// A consuming mod supplies the feudal hierarchy and nameplate tints through IPrivateWarsApi; no
    /// consumer wiring is required for the mechanism to run.
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        private IServiceProvider _serviceProvider = null!;
        private UIExtender? _uiExtender;

        // Guard: the two village condition postfixes are deferred until InitializeGameStarter, where
        // GameTexts is guaranteed initialised, and applied exactly once.
        private static bool _villageMenuPatchesApplied;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            _serviceProvider = new PrivateWarsServiceContainer().Build();
            PrivateWarsRuntimeServices.Set(_serviceProvider);

            _serviceProvider.GetRequiredService<IHarmonyPatcher>().ApplyPatches();

            // Register the nameplate mixins and prefab extensions in this assembly by attribute.
            _uiExtender = UIExtender.Create("Bannerlord.PrivateWars");
            _uiExtender.Register(typeof(SubModule).Assembly);
            _uiExtender.Enable();
        }

        protected override void InitializeGameStarter(Game game, IGameStarter starterObject)
        {
            base.InitializeGameStarter(game, starterObject);

            // Deferred until GameTexts is initialised; the guard applies both village postfixes once.
            if (!_villageMenuPatchesApplied)
            {
                _villageMenuPatchesApplied = true;
                var menuHarmony = new HarmonyLib.Harmony("com.private-wars.menus");
                IPatch[] menuPatches =
                [
                    new VillageHostileActionConditionPatch(),
                    new VillageRaidConditionPatch(),
                ];
                foreach (var p in menuPatches)
                    menuHarmony.Patch(p.TargetMethod, postfix: new HarmonyLib.HarmonyMethod(p.PatchMethod));
            }

            if (game.GameType is not CampaignState || starterObject is not CampaignGameStarter campaignGameStarter) return;

            ReplaceVanillaBehaviors(campaignGameStarter);
            PrivateWarEncounterMenuOptions.Register(campaignGameStarter);

            // Private-war siege-retention scoring (drive is explicit; see PrivateWarTargetScoreCalculatingModel)
            campaignGameStarter.AddModel(_serviceProvider.GetRequiredService<PrivateWarTargetScoreCalculatingModel>());
            // Keep a private-war enemy out of the belligerent's army candidate pool (design §4.1)
            campaignGameStarter.AddModel(_serviceProvider.GetRequiredService<PrivateWarArmyManagementCalculationModel>());
            // Block player entry into a same-kingdom private-war rival's town/castle (design §4.1)
            var settlementAccessModel = new PrivateWarSettlementAccessModel(
                campaignGameStarter.Models.OfType<SettlementAccessModel>().Last());
            campaignGameStarter.AddModel(settlementAccessModel);
            // Re-include garrison/militia/feud-lord defenders in a same-kingdom siege assault (design §4.1, Gate 1)
            campaignGameStarter.AddModel(_serviceProvider.GetRequiredService<PrivateWarEncounterModel>());
            // Deny private-war rivals the recruit slots that same-faction vanilla grants (design §4.1)
            campaignGameStarter.AddModel(_serviceProvider.GetRequiredService<PrivateWarVolunteerModel>());

            campaignGameStarter.AddBehavior(_serviceProvider.GetRequiredService<PrivateWarCampaignBehavior>());

            // Must follow PrivateWarCampaignBehavior: both listen on OnGameLoadedEvent, and the
            // claimant sweep needs the war records that behaviour restores there.
            campaignGameStarter.AddBehavior(new SettlementClaimantDecisionSuppressorCampaignBehavior(
                _serviceProvider.GetRequiredService<IPrivateWarRepository>()));

            // Populate the static locator read by the patches, game models, and nameplate mixins.
            PrivateWarsServices.Initialise(
                _serviceProvider.GetRequiredService<IPrivateWarRepository>(),
                _serviceProvider.GetRequiredService<IPrivateWarHostility>(),
                _serviceProvider.GetRequiredService<IPrivateWarNameplateColorUseCase>());
        }

        private void ReplaceVanillaBehaviors(CampaignGameStarter starter)
        {
            var vanillaCaptivity = starter.CampaignBehaviors
                .FirstOrDefault(behavior => behavior.GetType() == typeof(PlayerCaptivityCampaignBehavior))
                as PlayerCaptivityCampaignBehavior;
            if (vanillaCaptivity != null)
            {
                // Preserve the vanilla instance's menus, events, and save identity. The router is first
                // for ICaptivityCampaignBehavior lookup and delegates every ordinary capture back.
                starter.RemoveBehavior(vanillaCaptivity);
                starter.AddBehavior(new DadgPlayerCaptivityCampaignBehavior(
                    vanillaCaptivity,
                    _serviceProvider.GetRequiredService<PrivateWarCaptivityPolicy>()));
                starter.AddBehavior(vanillaCaptivity);
            }

            // The stock behaviour has no save state. Replace its private implementation with the same
            // public event/action flow plus explicit clan-side strength classification.
            var vanillaSallyOut = starter.CampaignBehaviors
                .FirstOrDefault(behavior => behavior.GetType() == typeof(SallyOutsCampaignBehavior))
                as SallyOutsCampaignBehavior;
            if (vanillaSallyOut != null)
                starter.RemoveBehavior(vanillaSallyOut);
            starter.AddBehavior(new DadgSallyOutCampaignBehavior(
                _serviceProvider.GetRequiredService<PrivateWarSallyOutPolicy>()));
        }
    }
}
