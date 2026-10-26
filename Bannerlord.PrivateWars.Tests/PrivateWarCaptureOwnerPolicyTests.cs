using System;
using System.Collections.Generic;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Model;
using Xunit;

namespace Bannerlord.PrivateWars.Tests
{
    public class PrivateWarCaptureOwnerPolicyTests
    {
        private static readonly PrivateWar War = new(
            "war", "claimant", "holder", new[] { "ally" }, Array.Empty<string>(), "claim", "title",
            "goal", new Dictionary<string, string> { ["holder_castle"] = "holder" },
            0f, 0f, 0f, 0f, PrivateWarStatus.Active);

        [Fact]
        public void Attacker_ally_capturing_the_goal_without_the_claimant_holds_it_in_the_interim()
        {
            Assert.Equal("ally",
                PrivateWarCaptureOwnerPolicy.ChooseOwnerClanId(War, "goal", WarSide.Attacker, "ally", claimantInArmy: false));
        }

        [Fact]
        public void Attacker_ally_leading_the_claimant_hands_the_goal_to_the_claimant()
        {
            Assert.Equal("claimant",
                PrivateWarCaptureOwnerPolicy.ChooseOwnerClanId(War, "goal", WarSide.Attacker, "ally", claimantInArmy: true));
        }

        [Fact]
        public void Claimant_capturing_the_goal_keeps_it()
        {
            Assert.Equal("claimant",
                PrivateWarCaptureOwnerPolicy.ChooseOwnerClanId(War, "goal", WarSide.Attacker, "claimant", claimantInArmy: true));
        }

        [Fact]
        public void Attacker_ally_capturing_a_non_goal_fief_keeps_it_even_with_the_claimant()
        {
            Assert.Equal("ally",
                PrivateWarCaptureOwnerPolicy.ChooseOwnerClanId(War, "holder_castle", WarSide.Attacker, "ally", claimantInArmy: true));
        }

        [Fact]
        public void Defender_side_capturing_the_goal_keeps_it()
        {
            Assert.Equal("defender_ally",
                PrivateWarCaptureOwnerPolicy.ChooseOwnerClanId(War, "goal", WarSide.Defender, "defender_ally", claimantInArmy: false));
        }
    }
}
