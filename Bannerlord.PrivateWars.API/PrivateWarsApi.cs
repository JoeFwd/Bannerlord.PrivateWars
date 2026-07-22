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
    private readonly Func<MutableSuzerainProvider?> _suzerainResolver;
    private readonly Func<MutablePrivateWarNameplateColorProvider?> _nameplateResolver;

    // The hostility signal is created when the SubModule builds its container, after this API is
    // constructed. Subscribe to it lazily the first time it becomes resolvable so HostilityChanged fires.
    private bool _hostilitySubscribed;

    public PrivateWarsApi(
        Func<IPrivateWarRepository?> repositoryResolver,
        Func<IPrivateWarHostility?> hostilityResolver,
        Func<IDeclarePrivateWarUseCase?> declareResolver,
        Func<IResolvePrivateWarByIdUseCase?> behaviorResolver,
        Func<MutableSuzerainProvider?> suzerainResolver,
        Func<MutablePrivateWarNameplateColorProvider?> nameplateResolver)
    {
        _repositoryResolver = repositoryResolver;
        _hostilityResolver = hostilityResolver;
        _declareResolver = declareResolver;
        _behaviorResolver = behaviorResolver;
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
        EnsureHostilitySubscribed();
        var repository = _repositoryResolver();
        if (repository == null)
            return Array.Empty<PrivateWarInfo>();

        return repository.GetAll().Select(Map).ToList();
    }

    public IReadOnlyList<PrivateWarInfo> GetWarsByClan(string clanId)
    {
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
        float startDay)
    {
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
            startDay);

        if (war == null)
            return null;

        var info = Map(war);
        WarDeclared?.Invoke(info);
        return info;
    }

    public PrivateWarInfo? ResolveWar(string warId, PrivateWarOutcome outcome)
    {
        // Resolution needs current fief owners and a side resolver assembled from live campaign state;
        // the campaign behaviour owns that assembly, so route through its resolution seam.
        var behavior = _behaviorResolver();
        if (behavior == null)
            return null;

        var resolved = behavior.ResolveWarById(warId, Map(outcome));
        if (resolved == null)
            return null;

        var info = Map(resolved);
        WarResolved?.Invoke(info);
        return info;
    }

    public event Action<PrivateWarInfo>? WarDeclared;

    public event Action<PrivateWarInfo>? WarResolved;

    public event Action? HostilityChanged;

    // Subscribe once to the live hostility signal so HostilityChanged relays it. The signal only
    // exists after the SubModule builds its container, so this is deferred until it first resolves.
    private void EnsureHostilitySubscribed()
    {
        if (_hostilitySubscribed) return;

        var hostility = _hostilityResolver();
        if (hostility == null) return;

        hostility.Changed += () => HostilityChanged?.Invoke();
        _hostilitySubscribed = true;
    }

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
