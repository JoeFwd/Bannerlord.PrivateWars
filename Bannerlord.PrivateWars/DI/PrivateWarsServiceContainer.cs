using System;
using Bannerlord.PrivateWars.Armies;
using Bannerlord.PrivateWars.Campaign;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.GameModels;
using Bannerlord.PrivateWars.Patches;
using Bannerlord.PrivateWars.Registry;
using Harmony.DependencyInjection;
using Harmony.DependencyInjection.Patches;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bannerlord.PrivateWars.DI
{
    /// <summary>
    /// Composition root for the private-war mechanism, mirroring Cannons' CannonsServiceContainer.
    /// Registers the domain use cases, the in-memory registry (repository + hostility signal), the
    /// game-facing policies, adapters, campaign behaviour, game models, and every Harmony patch. The
    /// host-supplied feudal hierarchy and nameplate tints arrive later through IPrivateWarsApi, so the
    /// suzerain provider and nameplate colour provider are registered as mutable indirections whose
    /// backing values the API updates.
    /// </summary>
    public class PrivateWarsServiceContainer
    {
        public IServiceProvider Build()
        {
            var services = new ServiceCollection();
            services.AddSingleton(PrivateWarsRuntimeServices.ExternalLoggerFactory ?? (ILoggerFactory)NullLoggerFactory.Instance);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            // Feudal hierarchy: a mutable indirection the host fills via IPrivateWarsApi.SetSuzerainProvider.
            // Until then it yields no suzerain, so side resolution falls back to principal clans only.
            services.AddSingleton<MutableSuzerainProvider>();
            services.AddSingleton<ISuzerainProvider>(sp => sp.GetRequiredService<MutableSuzerainProvider>());

            // Nameplate tints: a mutable indirection the host fills via IPrivateWarsApi.SetNameplateColors.
            // Both values start null, so the use case yields its built-in default tints.
            services.AddSingleton<MutablePrivateWarNameplateColorProvider>();
            services.AddSingleton<IPrivateWarNameplateColorProvider>(sp => sp.GetRequiredService<MutablePrivateWarNameplateColorProvider>());
            services.AddSingleton<IPrivateWarNameplateColorUseCase, PrivateWarNameplateColorUseCase>();

            // Infrastructure: the registry doubles as repository and the patch-facing hostility signal.
            services.AddSingleton<WarSideResolver>();
            services.AddSingleton<PrivateWarScoreCalculator>();
            services.AddSingleton<InMemoryPrivateWarRegistry>();
            services.AddSingleton<IPrivateWarRepository>(sp => sp.GetRequiredService<InMemoryPrivateWarRegistry>());
            services.AddSingleton<IPrivateWarHostility>(sp => sp.GetRequiredService<InMemoryPrivateWarRegistry>());
            services.AddSingleton<IPrivateWarSidesProvider>(sp => sp.GetRequiredService<InMemoryPrivateWarRegistry>());

            // Domain use cases
            services.AddSingleton<IDeclarePrivateWarUseCase, DeclarePrivateWarUseCase>();
            services.AddSingleton<ITickPrivateWarUseCase, TickPrivateWarUseCase>();
            services.AddSingleton<IApplyBattleOutcomeUseCase, ApplyBattleOutcomeUseCase>();
            services.AddSingleton<IResolvePrivateWarUseCase, ResolvePrivateWarUseCase>();

            // Policies and adapters
            services.AddSingleton<PrivateWarInteractionPolicy>();
            services.AddSingleton<PrivateWarCaptivityPolicy>();
            services.AddSingleton<PrivateWarSallyOutPolicy>();
            services.AddSingleton<PrivateWarAllegiancePolicy>();
            services.AddSingleton<PrivateWarObservationsReader>();
            services.AddSingleton<PrivateWarFronts>();
            services.AddSingleton<WarPrisonerReleaseAdapter>();
            services.AddSingleton<WarConclusionAdapter>();
            services.AddSingleton<KingdomSplitWarAdapter>();

            // The live-state resolution seam behind IPrivateWarsApi.ResolveWar, exposed as a game-free
            // port so the API can route through it.
            services.AddSingleton<WarResolutionAdapter>();
            services.AddSingleton<IResolvePrivateWarByIdUseCase>(sp => sp.GetRequiredService<WarResolutionAdapter>());

            // Campaign behaviours: persistence lifecycle, lords' feud-or-crown choice, battle score,
            // same-kingdom siege relief veto and the debug trace.
            services.AddSingleton<PrivateWarCampaignBehavior>();
            services.AddSingleton<PrivateWarAllegianceCampaignBehavior>();
            services.AddSingleton<PrivateWarBattleScoreCampaignBehavior>();
            services.AddSingleton<PrivateWarSiegeReliefVetoCampaignBehavior>();
            services.AddSingleton<PrivateWarDiagnosticsCampaignBehavior>();
            services.AddSingleton<IAssessPrivateWarProsecutionUseCase, PrivateWarProsecutionAssessor>();

            // Game models (resolved lazily in the SubModule)
            services.AddTransient<PrivateWarArmyManagementCalculationModel>();
            services.AddTransient<PrivateWarEncounterModel>();
            services.AddTransient<PrivateWarVolunteerModel>();

            RegisterPatches(services);
            services.AddHarmonyPatching();

            return services.BuildServiceProvider();
        }

        private static void RegisterPatches(IServiceCollection services)
        {
            // Same-kingdom hostility signal
            services.AddSingleton<IPatch, MobilePartyAiIsEnemyPatch>();
            services.AddSingleton<IPatch, MobilePartyAiStanceScorePatch>();
            services.AddSingleton<IPatch, StartPartyEncounterBattlePatch>();
            services.AddSingleton<IPatch, MapEventUpdateEnterPrivateWarScopePatch>();
            services.AddSingleton<IPatch, MapEventUpdateExitPrivateWarScopePatch>();
            services.AddSingleton<IPatch, MapEventFinishBattleExitPrivateWarScopePatch>();
            services.AddSingleton<IPatch, KingdomIsAtWarWithPrivateWarPatch>();
            services.AddSingleton<IPatch, CanPartyJoinBattlePatch>();
            // Real wall assaults for AI besiegers, won fiefs to the war's interim owner
            services.AddSingleton<IPatch, PrivateWarAssaultGateArmPatch>();
            services.AddSingleton<IPatch, PrivateWarAssaultGateDisarmPatch>();
            services.AddSingleton<IPatch, PrivateWarAssaultGateAnswerPatch>();
            services.AddSingleton<IPatch, PrivateWarSiegeOwnerPatch>();
            // The feud as a virtual kingdom for vanilla's siege planner
            services.AddSingleton<IPatch, PrivateWarSiegeTargetsPatch>();
            services.AddSingleton<IPatch, PrivateWarFrontierScorePatch>();
            services.AddSingleton<IPatch, WarArmyNoActiveWarDispersalPatch>();
            services.AddSingleton<IPatch, PrivateWarPrisonerRetentionPatch>();
            services.AddSingleton<IPatch, RecruitmentEntryPatch>();
            services.AddSingleton<IPatch, SettlementVisitPatch>();
            // Player-facing encounter setup and siege defender join (safe to apply early)
            services.AddSingleton<IPatch, PlayerEncounterSetupFieldsPatch>();
            services.AddSingleton<IPatch, SiegeDefenderJoinPatch>();
            services.AddSingleton<IPatch, PlayerSiegeJoinPatch>();
            // Field-encounter dialog: open the enemy conversation for a same-kingdom rival
            services.AddSingleton<IPatch, PlayerIsEnemyTagPatch>();
            services.AddSingleton<IPatch, WillLordAttackPrivateWarPatch>();
            services.AddSingleton<IPatch, PlayerCanAttackPrivateWarRivalPatch>();
        }
    }
}
