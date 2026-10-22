using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Xunit;

namespace Bannerlord.PrivateWars.Tests
{
    public class WarArmyLeadersTests
    {
        [Fact]
        public void Recorded_leader_is_raised_by_its_war_side_only()
        {
            var leaders = new WarArmyLeaders();
            leaders.Record("war_a", WarSide.Attacker, "party_1");

            Assert.True(leaders.IsRaisedBy("war_a", WarSide.Attacker, "party_1"));
            Assert.False(leaders.IsRaisedBy("war_a", WarSide.Defender, "party_1"));
            Assert.False(leaders.IsRaisedBy("war_b", WarSide.Attacker, "party_1"));
            Assert.True(leaders.LeadsAnyOf("party_1", new[] { "war_b", "war_a" }));
            Assert.False(leaders.LeadsAnyOf("party_1", new[] { "war_b" }));
        }

        [Fact]
        public void Forgetting_a_war_keeps_the_leaders_other_wars()
        {
            var leaders = new WarArmyLeaders();
            leaders.Record("war_a", WarSide.Attacker, "party_1");
            leaders.Record("war_b", WarSide.Defender, "party_1");

            leaders.ForgetWar("war_a");

            Assert.False(leaders.IsRaisedBy("war_a", WarSide.Attacker, "party_1"));
            Assert.True(leaders.IsLeader("party_1"));

            leaders.ForgetLeader("party_1");

            Assert.False(leaders.IsLeader("party_1"));
        }
    }
}
