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
    /// <c>AreEnemies</c> resolves each clan's side per active war by walking the suzerain chain
    /// (call-to-arms, design §18.A). This implementation recomputes sides on every call; the
    /// cached side-set optimisation (design §15 risk #4) is deferred to phase 3.
    /// </para>
    /// </summary>
    public class InMemoryPrivateWarRegistry : IPrivateWarRepository, IPrivateWarHostility
    {
        private readonly Dictionary<string, PrivateWar> _warsById = new();
        private readonly WarSideResolver _sideResolver;
        private readonly ISuzerainProvider _hierarchy;

        public InMemoryPrivateWarRegistry(WarSideResolver sideResolver, ISuzerainProvider hierarchy)
        {
            _sideResolver = sideResolver;
            _hierarchy = hierarchy;
        }

        public event Action? Changed;

        public PrivateWar? Get(string id)
            => _warsById.TryGetValue(id, out var war) ? war : null;

        public IReadOnlyList<PrivateWar> GetByPrincipalPair(string attackerPrincipalClanId, string defenderPrincipalClanId)
            => _warsById.Values
                .Where(w => w.AttackerPrincipalClanId == attackerPrincipalClanId
                            && w.DefenderPrincipalClanId == defenderPrincipalClanId)
                .ToList();

        public IReadOnlyList<PrivateWar> GetByDefender(string defenderPrincipalClanId)
            => _warsById.Values.Where(w => w.DefenderPrincipalClanId == defenderPrincipalClanId).ToList();

        public IReadOnlyList<PrivateWar> GetByTitle(string titleId)
            => _warsById.Values.Where(w => w.TitleId == titleId).ToList();

        public IReadOnlyList<PrivateWar> GetByClan(string principalClanId)
            => _warsById.Values
                .Where(w => w.AttackerPrincipalClanId == principalClanId
                            || w.DefenderPrincipalClanId == principalClanId)
                .ToList();

        public IReadOnlyList<PrivateWar> GetAll() => _warsById.Values.ToList();

        public void Add(PrivateWar war)
        {
            _warsById[war.Id] = war;
            Changed?.Invoke();
        }

        public void Update(PrivateWar war)
        {
            _warsById[war.Id] = war;
            Changed?.Invoke();
        }

        public void Remove(string id)
        {
            _warsById.Remove(id);
            Changed?.Invoke();
        }

        public void Initialise(IEnumerable<PrivateWar> wars)
        {
            _warsById.Clear();
            foreach (var war in wars)
                _warsById[war.Id] = war;

            Changed?.Invoke();
        }

        public IReadOnlyList<PrivateWar> Snapshot() => _warsById.Values.ToList();

        public bool AreEnemies(string clanIdA, string clanIdB)
        {
            foreach (var war in _warsById.Values)
            {
                if (war.Status != PrivateWarStatus.Active) continue;

                var sideA = _sideResolver.ResolveSide(clanIdA, war, _hierarchy.GetSuzerain);
                if (sideA is null) continue;

                var sideB = _sideResolver.ResolveSide(clanIdB, war, _hierarchy.GetSuzerain);
                if (sideB is null) continue;

                if (sideA != sideB) return true;
            }

            return false;
        }

        public bool AreAllies(string clanIdA, string clanIdB)
        {
            if (clanIdA == clanIdB) return false;

            foreach (var war in _warsById.Values)
            {
                if (war.Status != PrivateWarStatus.Active) continue;

                var sideA = _sideResolver.ResolveSide(clanIdA, war, _hierarchy.GetSuzerain);
                if (sideA is null) continue;

                var sideB = _sideResolver.ResolveSide(clanIdB, war, _hierarchy.GetSuzerain);
                if (sideB is null) continue;

                if (sideA == sideB) return true;
            }

            return false;
        }
    }
}
