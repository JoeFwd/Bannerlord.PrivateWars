using System;
using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.Registry;
using DomainWar = Bannerlord.PrivateWars.Domain.Model.PrivateWar;
using DomainStatus = Bannerlord.PrivateWars.Domain.Model.PrivateWarStatus;
using DomainOutcome = Bannerlord.PrivateWars.Domain.Model.PrivateWarOutcome;

namespace Bannerlord.PrivateWars.Api;

/// <summary>
/// Default <see cref="IPrivateWarsApi"/> implementation. Resolves the live domain ports and use
/// cases from the mechanism's runtime service provider and maps the internal domain records to the
/// public <see cref="PrivateWarInfo"/> DTO. When the runtime is not yet initialised (services
/// resolver returns null) queries degrade gracefully to empty/false, matching Cannons' CannonApi.
/// </summary>
internal sealed class PrivateWarsApi : IPrivateWarsApi
{
    private readonly Func<IPrivateWarRepository?> _repositoryResolver;
    private readonly Func<IPrivateWarHostility?> _hostilityResolver;
    private readonly Func<IDeclarePrivateWarUseCase?> _declareResolver;
    private readonly Func<IResolvePrivateWarByIdUseCase?> _behaviorResolver;
    private readonly Func<IAssessPrivateWarProsecutionUseCase?> _prosecutionResolver;
    private readonly Func<MutableSuzerainProvider?> _suzerainResolver;
    private readonly Func<MutablePrivateWarNameplateColorProvider?> _nameplateResolver;

    // The hostility signal and the resolving behaviour are created when the SubModule builds its
    // container, after this API is constructed. Subscribe lazily the first time each becomes
    // resolvable so HostilityChanged and WarResolved fire.
    private bool _hostilitySubscribed;
    private bool _warResolvedSubscribed;

    public PrivateWarsApi(
        Func<IPrivateWarRepository?> repositoryResolver,
        Func<IPrivateWarHostility?> hostilityResolver,
        Func<IDeclarePrivateWarUseCase?> declareResolver,
        Func<IResolvePrivateWarByIdUseCase?> behaviorResolver,
        Func<IAssessPrivateWarProsecutionUseCase?> prosecutionResolver,
        Func<MutableSuzerainProvider?> suzerainResolver,
        Func<MutablePrivateWarNameplateColorProvider?> nameplateResolver)
    {
        _repositoryResolver = repositoryResolver;
        _hostilityResolver = hostilityResolver;
        _declareResolver = declareResolver;
        _behaviorResolver = behaviorResolver;
        _prosecutionResolver = prosecutionResolver;
        _suzerainResolver = suzerainResolver;
        _nameplateResolver = nameplateResolver;
    }

    public void SetSuzerainProvider(ISuzerainProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        _suzerainResolver()?.SetProvider(provider);
    }

    public void SetNameplateColors(string? enemyArgbHex, string? allyArgbHex)
    {
        _nameplateResolver()?.SetColors(enemyArgbHex, allyArgbHex);
    }

    public IReadOnlyList<PrivateWarInfo> GetAllWars()
    {
        EnsureSignalsSubscribed();
        var repository = _repositoryResolver();
        if (repository == null)
            return Array.Empty<PrivateWarInfo>();

        return repository.GetAll().Select(Map).ToList();
    }

    public IReadOnlyList<PrivateWarInfo> GetWarsByClan(string clanId)
    {
        EnsureSignalsSubscribed();
        var repository = _repositoryResolver();
        if (repository == null)
            return Array.Empty<PrivateWarInfo>();

        return repository.GetByClan(clanId).Select(Map).ToList();
    }

    public bool AreEnemies(string clanA, string clanB)
    {
        var hostility = _hostilityResolver();
        return hostility != null && hostility.AreEnemies(clanA, clanB);
    }

    public bool AreAllies(string clanA, string clanB)
    {
        var hostility = _hostilityResolver();
        return hostility != null && hostility.AreAllies(clanA, clanB);
    }

    public PrivateWarInfo? DeclareWar(
        string attackerClanId,
        string defenderClanId,
        string casusBelliType,
        string titleId,
        string? mainGoalSettlementId,
        IReadOnlyDictionary<string, string> fiefSnapshot,
        float startDay,
        IReadOnlyCollection<string> attackerSupporters,
        IReadOnlyCollection<string> defenderSupporters)
    {
        EnsureSignalsSubscribed();
        var declare = _declareResolver();
        if (declare == null)
            return null;

        var casusBelli = ToCasusBelli(casusBelliType, titleId);
        var war = declare.Execute(
            attackerClanId,
            defenderClanId,
            casusBelli,
            mainGoalSettlementId,
            fiefSnapshot,
            startDay,
            attackerSupporters,
            defenderSupporters);

        if (war == null)
            return null;

        var info = Map(war);
        WarDeclared?.Invoke(info);
        return info;
    }

    public bool CanProsecute(
        string attackerClanId,
        string defenderClanId,
        string mainGoalSettlementId,
        IReadOnlyCollection<string> attackerSupporters,
        IReadOnlyCollection<string> defenderSupporters)
    {
        var prosecution = _prosecutionResolver();
        return prosecution != null
               && prosecution.CanProsecute(attackerClanId, defenderClanId, mainGoalSettlementId, attackerSupporters, defenderSupporters);
    }

    public PrivateWarInfo? ResolveWar(string warId, PrivateWarOutcome outcome)
    {
        // Resolution needs current fief owners and a side resolver assembled from live campaign state;
        // the campaign behaviour owns that assembly, so route through its resolution seam.
        EnsureSignalsSubscribed();
        var behavior = _behaviorResolver();
        if (behavior == null)
            return null;

        var resolved = behavior.ResolveWarById(warId, Map(outcome));
        return resolved == null ? null : Map(resolved);
    }

    public event Action<PrivateWarInfo>? WarDeclared;

    public event Action<PrivateWarInfo, PrivateWarOutcome>? WarResolved;

    public event Action? HostilityChanged;

    // Subscribe once to each live signal so HostilityChanged and WarResolved relay it. Both only
    // exist after the SubModule builds its container, so this is deferred until they first resolve.
    private void EnsureSignalsSubscribed()
    {
        if (!_hostilitySubscribed)
        {
            var hostility = _hostilityResolver();
            if (hostility != null)
            {
                hostility.Changed += () => HostilityChanged?.Invoke();
                _hostilitySubscribed = true;
            }
        }

        if (_warResolvedSubscribed) return;

        var behavior = _behaviorResolver();
        if (behavior == null) return;

        behavior.WarResolved += (war, outcome) => WarResolved?.Invoke(Map(war), Map(outcome));
        _warResolvedSubscribed = true;
    }

    private static PrivateWarOutcome Map(DomainOutcome outcome) =>
        outcome switch
        {
            DomainOutcome.AttackerVictory => PrivateWarOutcome.AttackerVictory,
            DomainOutcome.DefenderVictory => PrivateWarOutcome.DefenderVictory,
            DomainOutcome.WhitePeace => PrivateWarOutcome.WhitePeace,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };

    private static DomainOutcome Map(PrivateWarOutcome outcome) =>
        outcome switch
        {
            PrivateWarOutcome.AttackerVictory => DomainOutcome.AttackerVictory,
            PrivateWarOutcome.DefenderVictory => DomainOutcome.DefenderVictory,
            PrivateWarOutcome.WhitePeace => DomainOutcome.WhitePeace,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };

    private static ICasusBelli ToCasusBelli(string casusBelliType, string titleId)
    {
        if (casusBelliType == ClaimCasusBelli.ClaimType)
            return new ClaimCasusBelli(titleId);

        throw new ArgumentException($"Unknown casus belli type '{casusBelliType}'.", nameof(casusBelliType));
    }

    private static PrivateWarInfo Map(DomainWar war) =>
        new(
            war.Id,
            war.AttackerPrincipalClanId,
            war.DefenderPrincipalClanId,
            war.CasusBelliType,
            war.TitleId,
            war.MainGoalSettlementId,
            Map(war.Status));

    private static PrivateWarStatus Map(DomainStatus status) =>
        status switch
        {
            DomainStatus.Active => PrivateWarStatus.Active,
            DomainStatus.Concluded => PrivateWarStatus.Concluded,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
}
