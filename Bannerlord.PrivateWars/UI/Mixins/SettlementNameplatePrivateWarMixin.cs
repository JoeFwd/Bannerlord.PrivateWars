using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.DI;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Bannerlord.PrivateWars.UI.Mixins
{
    [ViewModelMixin]
    public sealed class SettlementNameplatePrivateWarMixin : BaseViewModelMixin<SettlementNameplateVM>
    {
        private const int NoPrivateWar = 0;
        private const int PrivateWarEnemy = 1;
        private const int PrivateWarAlly = 2;

        private const int BannerlordNeutralRelation = 0;
        private const int BannerlordSameFactionRelation = 1;
        private const int BannerlordEnemyRelation = 2;
        private const int BannerlordAllianceRelation = 3;

        private readonly PropertyChangedWithIntValueEventHandler _relationChangedHandler;
        private readonly Action _hostilityChangedHandler;

        public SettlementNameplatePrivateWarMixin(SettlementNameplateVM vm) : base(vm)
        {
            _relationChangedHandler = OnBaseRelationChanged;
            _hostilityChangedHandler = OnPrivateWarHostilityChanged;

            vm.PropertyChangedWithIntValue += _relationChangedHandler;
            if (PrivateWarsServices.IsInitialised && PrivateWarsServices.PrivateWarHostility is not null)
            {
                PrivateWarsServices.PrivateWarHostility.Changed += _hostilityChangedHandler;
            }
        }

        [DataSourceProperty]
        public Color SettlementCapsuleColor
        {
            get
            {
                SettlementNameplateVM? vm = ViewModel;
                var colors = PrivateWarsServices.PrivateWarNameplateColor;
                if (!PrivateWarsServices.IsInitialised || vm is null || colors is null)
                {
                    return ArgbToColor(PrivateWarNameplateColorUseCase.VanillaNeutralSettlementArgb);
                }

                int privateWarState = ResolvePrivateWarState();
                uint argb = colors.GetSettlementCapsuleArgbColor(
                    ResolveVanillaRelation(vm.Relation),
                    privateWarState == PrivateWarEnemy,
                    privateWarState == PrivateWarAlly);

                return ArgbToColor(argb);
            }
        }

        [DataSourceProperty]
        public int SettlementCapsuleRelationType
        {
            get
            {
                SettlementNameplateVM? vm = ViewModel;
                if (vm is null)
                {
                    return BannerlordNeutralRelation;
                }

                int privateWarState = ResolvePrivateWarState();
                if (privateWarState == PrivateWarEnemy)
                {
                    return BannerlordEnemyRelation;
                }

                if (privateWarState == PrivateWarAlly)
                {
                    return BannerlordSameFactionRelation;
                }

                return ToBannerlordRelation(ResolveVanillaRelation(vm.Relation));
            }
        }

        public override void OnFinalize()
        {
            if (ViewModel is not null)
            {
                ViewModel.PropertyChangedWithIntValue -= _relationChangedHandler;
            }

            if (PrivateWarsServices.IsInitialised && PrivateWarsServices.PrivateWarHostility is not null)
            {
                PrivateWarsServices.PrivateWarHostility.Changed -= _hostilityChangedHandler;
            }

            base.OnFinalize();
        }

        // SettlementNameplateVM.Relation is an int, so the base ViewModel raises it through
        // PropertyChangedWithIntValue only — the plain PropertyChanged event never carries it.
        private void OnBaseRelationChanged(object? sender, PropertyChangedWithIntValueEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(SettlementNameplateVM.Relation))
            {
                NotifyPrivateWarCapsuleProperties();
            }
        }

        private void OnPrivateWarHostilityChanged()
            => NotifyPrivateWarCapsuleProperties();

        private void NotifyPrivateWarCapsuleProperties()
        {
            OnPropertyChanged(nameof(SettlementCapsuleRelationType));
            OnPropertyChanged(nameof(SettlementCapsuleColor));
        }

        private int ResolvePrivateWarState()
        {
            SettlementNameplateVM? vm = ViewModel;
            if (!PrivateWarsServices.IsInitialised || vm is null)
            {
                return NoPrivateWar;
            }

            IFaction? settlementFaction = vm.Settlement?.MapFaction;
            IFaction? mainFaction = Hero.MainHero?.MapFaction;
            if (settlementFaction is null || mainFaction is null || settlementFaction != mainFaction)
            {
                return NoPrivateWar;
            }

            Clan? mainClan = Hero.MainHero?.Clan;
            Clan? ownerClan = ResolveOwnerClan(vm);
            if (mainClan is null || ownerClan is null)
            {
                return NoPrivateWar;
            }

            var hostility = PrivateWarsServices.PrivateWarHostility;
            if (hostility is null)
            {
                return NoPrivateWar;
            }

            if (hostility.AreEnemies(mainClan.StringId, ownerClan.StringId))
            {
                return PrivateWarEnemy;
            }

            if (hostility.AreAllies(mainClan.StringId, ownerClan.StringId))
            {
                return PrivateWarAlly;
            }

            return NoPrivateWar;
        }

        private static Clan? ResolveOwnerClan(SettlementNameplateVM vm)
            => vm.Settlement?.OwnerClan ?? vm.Settlement?.MapFaction as Clan;

        private static SettlementNameplateRelation ResolveVanillaRelation(int bannerlordRelation)
        {
            switch (bannerlordRelation)
            {
                case BannerlordSameFactionRelation:
                    return SettlementNameplateRelation.SameFaction;
                case BannerlordEnemyRelation:
                    return SettlementNameplateRelation.Enemy;
                case BannerlordAllianceRelation:
                    return SettlementNameplateRelation.Alliance;
                default:
                    return SettlementNameplateRelation.Neutral;
            }
        }

        private static int ToBannerlordRelation(SettlementNameplateRelation relation)
        {
            switch (relation)
            {
                case SettlementNameplateRelation.SameFaction:
                    return BannerlordSameFactionRelation;
                case SettlementNameplateRelation.Enemy:
                    return BannerlordEnemyRelation;
                case SettlementNameplateRelation.Alliance:
                    return BannerlordAllianceRelation;
                default:
                    return BannerlordNeutralRelation;
            }
        }

        private static Color ArgbToColor(uint argb)
        {
            byte a = (byte)(argb >> 24);
            byte r = (byte)(argb >> 16);
            byte g = (byte)(argb >> 8);
            byte b = (byte)argb;
            return Color.ConvertStringToColor(
                "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2") + a.ToString("X2"));
        }
    }
}
