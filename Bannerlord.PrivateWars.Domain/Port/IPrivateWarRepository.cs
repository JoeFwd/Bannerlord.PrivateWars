using System.Collections.Generic;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain.Port
{
    public interface IPrivateWarRepository
    {
        PrivateWar? Get(string id);

        // Active and concluded wars for the exact attacker->defender principal ordering.
        IReadOnlyList<PrivateWar> GetByPrincipalPair(string attackerPrincipalClanId, string defenderPrincipalClanId);

        // Wars in which the given clan is the defending principal (multi-attacker re-eval, design §18.E).
        IReadOnlyList<PrivateWar> GetByDefender(string defenderPrincipalClanId);

        // Wars pressing a claim on the given title (same-CB collision, design §18.E).
        IReadOnlyList<PrivateWar> GetByTitle(string titleId);

        // Wars in which the given clan is either principal.
        IReadOnlyList<PrivateWar> GetByClan(string principalClanId);

        IReadOnlyList<PrivateWar> GetAll();
        void Add(PrivateWar war);
        void Update(PrivateWar war);
        void Remove(string id);

        // Lifecycle hooks for save/load support.
        void Initialise(IEnumerable<PrivateWar> wars);
        IReadOnlyList<PrivateWar> Snapshot();
    }
}
