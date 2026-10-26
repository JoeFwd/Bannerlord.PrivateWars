using Bannerlord.PrivateWars.Domain;
using Xunit;

namespace Bannerlord.PrivateWars.Tests
{
    public class PrivateWarAllegiancePolicyTests
    {
        private readonly PrivateWarAllegiancePolicy _policy = new();

        private static PrivateWarAllegianceInputs Vassal(
            bool realmAtWar = true,
            bool isPrincipal = false,
            bool isRuler = false,
            bool realmThreat = false,
            bool feudThreat = false,
            bool fiefHeldByFeud = false,
            int relationWithRuler = 0,
            int relationWithPrincipal = 0,
            int honor = 0,
            int calculating = 0,
            PrivateWarTheatre? current = null)
            => new(isPrincipal, isRuler, realmAtWar, realmThreat, feudThreat, fiefHeldByFeud,
                relationWithRuler, relationWithPrincipal, honor, calculating, current);

        [Fact]
        public void Realm_at_peace_leaves_the_feud()
        {
            Assert.Equal(PrivateWarTheatre.Private,
                _policy.Choose(Vassal(realmAtWar: false, honor: 2, relationWithRuler: 100, isRuler: true)));
        }

        [Fact]
        public void Principal_pursues_its_own_feud_against_an_ordinary_realm_war()
        {
            Assert.Equal(PrivateWarTheatre.Private, _policy.Choose(Vassal(isPrincipal: true)));
        }

        [Fact]
        public void Honourable_vassal_loyal_to_the_ruler_answers_the_crown()
        {
            Assert.Equal(PrivateWarTheatre.Kingdom,
                _policy.Choose(Vassal(honor: 2, relationWithRuler: 40)));
        }

        [Fact]
        public void Vassal_devoted_to_its_principal_follows_the_feud()
        {
            Assert.Equal(PrivateWarTheatre.Private,
                _policy.Choose(Vassal(relationWithPrincipal: 60, relationWithRuler: -10)));
        }

        [Fact]
        public void Realm_enemy_at_the_gates_outweighs_a_feud_for_a_vassal()
        {
            Assert.Equal(PrivateWarTheatre.Kingdom, _policy.Choose(Vassal(realmThreat: true)));
        }

        [Fact]
        public void Feud_enemy_at_the_gates_outweighs_the_realm_war()
        {
            Assert.Equal(PrivateWarTheatre.Private, _policy.Choose(Vassal(feudThreat: true, honor: 1)));
        }

        [Fact]
        public void Ruler_in_a_feud_answers_its_own_war_unless_the_feud_is_its_own()
        {
            Assert.Equal(PrivateWarTheatre.Kingdom, _policy.Choose(Vassal(isRuler: true)));
            Assert.Equal(PrivateWarTheatre.Private, _policy.Choose(Vassal(isRuler: true, isPrincipal: true)));
        }

        [Fact]
        public void Calculating_lord_discounts_loyalty_against_a_lost_fief()
        {
            var loyal = Vassal(relationWithRuler: 80, fiefHeldByFeud: true);
            Assert.Equal(PrivateWarTheatre.Kingdom, _policy.Choose(loyal));
            Assert.Equal(PrivateWarTheatre.Private, _policy.Choose(loyal with { Calculating = 2 }));
        }

        [Fact]
        public void Current_theatre_resists_a_marginal_swing()
        {
            var marginal = Vassal(relationWithRuler: 10);
            Assert.Equal(PrivateWarTheatre.Kingdom, _policy.Choose(marginal));
            Assert.Equal(PrivateWarTheatre.Private, _policy.Choose(marginal with { Current = PrivateWarTheatre.Private }));
        }
    }
}
