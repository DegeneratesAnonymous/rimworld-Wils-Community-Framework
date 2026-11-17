using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class CompanionTests
    {
        [Fact]
        public void A_pair_lasts_until_it_ends_or_expires_and_each_person_is_in_one_pair()
        {
            var b = new CompanionBook();
            Assert.True(b.TryPair(1, 2, 100, 500));
            Assert.Equal(2, b.CompanionOf(1, 200)); Assert.Equal(1, b.LeaderOf(2, 200));
            Assert.False(b.TryPair(3, 2, 200, 500)); Assert.False(b.TryPair(2, 3, 200, 500)); Assert.False(b.TryPair(1, 3, 200, 500));          // already paired
            Assert.Equal(0, b.CompanionOf(1, 700)); Assert.True(b.TryPair(3, 2, 700, 500));          // expired, free again
        }

        [Fact]
        public void Ending_a_pair_frees_both_and_nobody_pairs_with_themselves()
        {
            var b = new CompanionBook(); b.TryPair(1, 2, 0, 500); b.End(1);
            Assert.Equal(0, b.CompanionOf(1, 10)); Assert.False(b.InPair(2, 10));
            Assert.False(b.TryPair(4, 4, 0, 500)); Assert.False(b.TryPair(0, 4, 0, 500));
        }

        [Fact]
        public void The_team_bonus_adds_a_share_and_at_least_one_point()
        {
            Assert.Equal(15, TeamBonus.Apply(12, .25f)); Assert.Equal(5, TeamBonus.Apply(4, .1f));          // 4.4 rounds to 4, but a bonus is at least one more
            Assert.Equal(12, TeamBonus.Apply(12, 0f)); Assert.Equal(0, TeamBonus.Apply(0, .5f)); Assert.Equal(24, TeamBonus.Apply(12, 9f));          // capped at doubling
        }
    }
}
