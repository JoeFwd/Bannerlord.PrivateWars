using System;
using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Bannerlord.PrivateWars.Domain.Port;

namespace Bannerlord.PrivateWars.Registry
{
    /// <summary>
    /// In-memory store of every private war, doubling as the patch-facing hostility signal.
    /// Mirrors DellarteDellaGuerra's InMemoryLevyRegistry.
    /// <para>
    /// Versioned hierarchies cache each clan's side and suzerain, including uninvolved clans.
    /// Campaign AI workers share this registry, so reads, cache fills and mutations are locked.
    /// </para>
    /// </summary>
    public class InMemoryPrivateWarRegistry : IPrivateWarRepository, IPrivateWarHostility, IPrivateWarSidesProvider
    {
        private readonly Dictionary<string, PrivateWar> _warsById = new();
        private readonly WarSideResolver _sideResolver;
        private readonly ISuzerainProvider _hierarchy;
        private readonly object _sync = new();
        private readonly Dictionary<string, Dictionary<string, WarSide?>> _sidesByWar = new();
        private readonly Dictionary<string, string?> _suzerains = new();
        private long? _hierarchyRevision;

        public InMemoryPrivateWarRegistry(WarSideResolver sideResolver, ISuzerainProvider hierarchy)
        {
            _sideResolver = sideResolver;
            _hierarchy = hierarchy;
            if (hierarchy is IReplaceableSuzerainProvider replaceable)
                replaceable.ProviderChanged += () =>
                {
                    lock (_sync)
                    {
                        _sidesByWar.Clear();
                        _suzerains.Clear();
                        _hierarchyRevision = null;
                    }
                };
        }

        public event Action? Changed;

        public PrivateWar? Get(string id)
        {
            lock (_sync) return _warsById.TryGetValue(id, out var war) ? war : null;
        }

        public IReadOnlyList<PrivateWar> GetByPrincipalPair(string attackerPrincipalClanId, string defenderPrincipalClanId)
        {
            lock (_sync) return _warsById.Values
                .Where(w => w.AttackerPrincipalClanId == attackerPrincipalClanId
                            && w.DefenderPrincipalClanId == defenderPrincipalClanId)
                .ToList();
        }

        public IReadOnlyList<PrivateWar> GetByDefender(string defenderPrincipalClanId)
        {
            lock (_sync) return _warsById.Values.Where(w => w.DefenderPrincipalClanId == defenderPrincipalClanId).ToList();
        }

        public IReadOnlyList<PrivateWar> GetByTitle(string titleId)
        {
            lock (_sync) return _warsById.Values.Where(w => w.TitleId == titleId).ToList();
        }

        public IReadOnlyList<PrivateWar> GetByClan(string principalClanId)
        {
            lock (_sync) return _warsById.Values
                .Where(w => w.AttackerPrincipalClanId == principalClanId
                            || w.DefenderPrincipalClanId == principalClanId)
                .ToList();
        }

        public IReadOnlyList<PrivateWar> GetAll()
        {
            lock (_sync) return _warsById.Values.ToList();
        }

        public void Add(PrivateWar war)
        {
            lock (_sync)
            {
                _warsById[war.Id] = war;
                _sidesByWar.Remove(war.Id);
            }
            Changed?.Invoke();
        }

        public void Update(PrivateWar war)
        {
            lock (_sync)
            {
                // The daily score update keeps the war's sides; only a change of who fights drops them.
                if (!_warsById.TryGetValue(war.Id, out var stored) || !HaveSameSides(stored, war))
                    _sidesByWar.Remove(war.Id);
                _warsById[war.Id] = war;
            }
            Changed?.Invoke();
        }

        public void Remove(string id)
        {
            lock (_sync)
            {
                _warsById.Remove(id);
                _sidesByWar.Remove(id);
            }
            Changed?.Invoke();
        }

        public void Initialise(IEnumerable<PrivateWar> wars)
        {
            lock (_sync)
            {
                _warsById.Clear();
                foreach (var war in wars)
                    _warsById[war.Id] = war;
                _sidesByWar.Clear();
                _suzerains.Clear();
                _hierarchyRevision = null;
            }

            Changed?.Invoke();
        }

        public IReadOnlyList<PrivateWar> Snapshot() => GetAll();

        public bool AreEnemies(string clanIdA, string clanIdB)
        {
            if (clanIdA == clanIdB) return false;
            lock (_sync)
            {
                bool cache = RefreshHierarchy();
                foreach (var war in _warsById.Values)
                {
                    if (war.Status != PrivateWarStatus.Active) continue;

                    var sideA = ResolveSide(clanIdA, war, cache);
                    if (sideA is null) continue;

                    var sideB = ResolveSide(clanIdB, war, cache);
                    if (sideB is null) continue;

                    if (sideA != sideB) return true;
                }
                return false;
            }
        }

        public bool AreAllies(string clanIdA, string clanIdB)
        {
            if (clanIdA == clanIdB) return false;

            lock (_sync)
            {
                bool cache = RefreshHierarchy();
                foreach (var war in _warsById.Values)
                {
                    if (war.Status != PrivateWarStatus.Active) continue;

                    var sideA = ResolveSide(clanIdA, war, cache);
                    if (sideA is null) continue;

                    var sideB = ResolveSide(clanIdB, war, cache);
                    if (sideB is null) continue;

                    if (sideA == sideB) return true;
                }
                return false;
            }
        }

        public bool IsBelligerent(string clanId)
        {
            lock (_sync)
            {
                bool cache = RefreshHierarchy();
                foreach (var war in _warsById.Values)
                {
                    if (war.Status == PrivateWarStatus.Active && ResolveSide(clanId, war, cache) is not null)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Which side of the war a clan fights on, from the shared side cache. A war instance this
        /// registry does not currently store (a record about to be updated, or one just removed) is
        /// resolved fresh, still through the cached suzerain chain.
        /// </summary>
        public WarSide? ResolveSide(string clanId, PrivateWar war)
        {
            lock (_sync)
            {
                bool cache = RefreshHierarchy();
                if (_warsById.TryGetValue(war.Id, out var stored) && ReferenceEquals(stored, war))
                    return ResolveSide(clanId, war, cache);
                return _sideResolver.ResolveSide(clanId, war, cache ? GetCachedSuzerain : _hierarchy.GetSuzerain);
            }
        }

        public string? GetSuzerain(string clanId)
        {
            lock (_sync) return RefreshHierarchy() ? GetCachedSuzerain(clanId) : _hierarchy.GetSuzerain(clanId);
        }

        private bool RefreshHierarchy()
        {
            var revision = (_hierarchy as IVersionedSuzerainProvider)?.Revision;
            if (revision != _hierarchyRevision)
            {
                _sidesByWar.Clear();
                _suzerains.Clear();
                _hierarchyRevision = revision;
            }
            return revision.HasValue;
        }

        private WarSide? ResolveSide(string clanId, PrivateWar war, bool cache)
        {
            if (!cache) return _sideResolver.ResolveSide(clanId, war, _hierarchy.GetSuzerain);
            if (!_sidesByWar.TryGetValue(war.Id, out var sides))
            {
                sides = new Dictionary<string, WarSide?>();
                _sidesByWar[war.Id] = sides;
            }
            if (!sides.TryGetValue(clanId, out var side))
            {
                side = _sideResolver.ResolveSide(clanId, war, GetCachedSuzerain);
                sides[clanId] = side;
            }
            return side;
        }

        // Exactly the fields WarSideResolver reads.
        private static bool HaveSameSides(PrivateWar a, PrivateWar b)
            => a.AttackerPrincipalClanId == b.AttackerPrincipalClanId
               && a.DefenderPrincipalClanId == b.DefenderPrincipalClanId
               && HaveSameClans(a.AttackerSupporters, b.AttackerSupporters)
               && HaveSameClans(a.DefenderSupporters, b.DefenderSupporters)
               && HaveSameClans(a.ForeignSupporters, b.ForeignSupporters);

        private static bool HaveSameClans(IReadOnlyCollection<string> a, IReadOnlyCollection<string> b)
            => ReferenceEquals(a, b) || a.All(b.Contains) && b.All(a.Contains);

        private string? GetCachedSuzerain(string clanId)
        {
            if (!_suzerains.TryGetValue(clanId, out var suzerain))
            {
                suzerain = _hierarchy.GetSuzerain(clanId);
                _suzerains[clanId] = suzerain;
            }
            return suzerain;
        }
    }
}
