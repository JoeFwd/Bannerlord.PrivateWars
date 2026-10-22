using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Party that raised each war side's war army, keyed by war id and side. A gathering Besieger
    /// army points AiBehaviorObject at a gathering fortress, never a war fief, so without this its
    /// leader lost the designation the moment it formed and the next lord raised another army.
    /// Not persisted: after a load, an army is recognised again once it marches on a war fief.
    /// </summary>
    public class WarArmyLeaders
    {
        private readonly Dictionary<(string WarId, WarSide Side), string> _leaders = new();

        public void Record(string warId, WarSide side, string partyId) => _leaders[(warId, side)] = partyId;

        public bool IsRaisedBy(string warId, WarSide side, string partyId)
            => _leaders.TryGetValue((warId, side), out var raisedBy) && raisedBy == partyId;

        public bool IsLeader(string partyId) => _leaders.ContainsValue(partyId);

        public bool LeadsAnyOf(string partyId, ICollection<string> warIds)
            => _leaders.Any(entry => entry.Value == partyId && warIds.Contains(entry.Key.WarId));

        public void ForgetWar(string warId)
        {
            foreach (var key in _leaders.Keys.Where(k => k.WarId == warId).ToList())
                _leaders.Remove(key);
        }

        public void ForgetLeader(string partyId)
        {
            foreach (var key in _leaders.Where(e => e.Value == partyId).Select(e => e.Key).ToList())
                _leaders.Remove(key);
        }
    }
}
