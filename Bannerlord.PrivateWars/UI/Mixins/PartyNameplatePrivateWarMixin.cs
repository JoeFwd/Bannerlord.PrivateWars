using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.DI;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Bannerlord.PrivateWars.UI.Mixins
{
    [ViewModelMixin(nameof(PartyNameplateVM.InitializeWith))]
    public sealed class PartyNameplatePrivateWarMixin : BaseViewModelMixin<PartyNameplateVM>
    {
        private readonly PropertyChangedWithValueEventHandler _propertyChangedHandler;
        private readonly Action _hostilityChangedHandler;
        private IPrivateWarHostility? _subscribedHostility;

        public PartyNameplatePrivateWarMixin(PartyNameplateVM vm) : base(vm)
        {
            _propertyChangedHandler = OnBasePropertyChanged;
            _hostilityChangedHandler = OnPrivateWarHostilityChanged;
        }

        [DataSourceProperty]
        public string PrivateWarFontColor
        {
            get
            {
                PartyNameplateVM? vm = ViewModel;
                string neutralColor = vm?.FactionColor ?? "#FFFFFFFF";
                if (!PrivateWarsServices.IsInitialised || vm is null)
                {
                    return neutralColor;
                }

                MobileParty? party = vm.Party;
                IFaction? mainFaction = Hero.MainHero?.MapFaction;
                if (party is null || mainFaction is null || party.MapFaction != mainFaction)
                {
                    return neutralColor;
                }

                Clan? mainClan = Hero.MainHero?.Clan;
                Clan? ownerClan = ResolveOwnerClan(vm);
                if (mainClan is null || ownerClan is null)
                {
                    return neutralColor;
                }

                var hostility = PrivateWarsServices.PrivateWarHostility;
                var colors = PrivateWarsServices.PrivateWarNameplateColor;
                if (hostility is null || colors is null)
                {
                    return neutralColor;
                }

                if (hostility.AreEnemies(mainClan.StringId, ownerClan.StringId))
                {
                    return ArgbToFactionColorString(colors.GetPrivateWarEnemyArgbColor());
                }

                if (hostility.AreAllies(mainClan.StringId, ownerClan.StringId))
                {
                    return ArgbToFactionColorString(colors.GetPrivateWarAllyArgbColor());
                }

                return neutralColor;
            }
        }

        public override void OnRefresh()
        {
            if (ViewModel is not null)
            {
                ViewModel.PropertyChangedWithValue -= _propertyChangedHandler;
                ViewModel.PropertyChangedWithValue += _propertyChangedHandler;
            }

            if (_subscribedHostility is not null)
            {
                _subscribedHostility.Changed -= _hostilityChangedHandler;
                _subscribedHostility = null;
            }

            if (PrivateWarsServices.IsInitialised)
            {
                _subscribedHostility = PrivateWarsServices.PrivateWarHostility;
                if (_subscribedHostility is not null)
                {
                    _subscribedHostility.Changed += _hostilityChangedHandler;
                }
            }

            OnPropertyChanged(nameof(PrivateWarFontColor));
            base.OnRefresh();
        }

        public override void OnFinalize()
        {
            if (ViewModel is not null)
            {
                ViewModel.PropertyChangedWithValue -= _propertyChangedHandler;
            }

            if (_subscribedHostility is not null)
            {
                _subscribedHostility.Changed -= _hostilityChangedHandler;
                _subscribedHostility = null;
            }

            base.OnFinalize();
        }

        private void OnBasePropertyChanged(object? sender, PropertyChangedWithValueEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(NameplateVM.FactionColor))
            {
                OnPropertyChanged(nameof(PrivateWarFontColor));
            }
        }

        private void OnPrivateWarHostilityChanged()
        {
            OnPropertyChanged(nameof(PrivateWarFontColor));
        }

        private static Clan? ResolveOwnerClan(PartyNameplateVM vm)
        {
            MobileParty? party = vm.Party;
            return party?.ActualClan ?? party?.LeaderHero?.Clan ?? party?.HomeSettlement?.OwnerClan;
        }

        private static string ArgbToFactionColorString(uint argb)
        {
            byte a = (byte)(argb >> 24);
            byte r = (byte)(argb >> 16);
            byte g = (byte)(argb >> 8);
            byte b = (byte)argb;
            return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2") + a.ToString("X2");
        }
    }
}
