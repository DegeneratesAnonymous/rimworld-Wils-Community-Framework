using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class DutyClaimTests
    {
        [Fact]
        public void A_claimed_person_is_not_available_to_another_module_until_it_ends()
        {
            var b = new DutyClaimBook();
            Assert.True(b.TryClaim(7, "wils.military", 100, 1500));
            Assert.False(b.TryClaim(7, "wils.works", 200, 1500));          // someone else has them
            Assert.True(b.HeldByOther(7, "wils.works", 200)); Assert.False(b.HeldByOther(7, "wils.military", 200));
            Assert.True(b.TryClaim(7, "wils.works", 1600, 1500));          // the first claim has run out
        }

        [Fact]
        public void A_module_can_renew_its_own_claim_and_release_only_its_own()
        {
            var b = new DutyClaimBook();
            b.TryClaim(7, "a", 0, 100); Assert.True(b.TryClaim(7, "a", 50, 100)); Assert.True(b.HeldByOther(7, "b", 120));          // renewed to 150
            b.Release(7, "b"); Assert.True(b.HeldByOther(7, "b", 120));          // another module cannot end it
            b.Release(7, "a"); Assert.False(b.HeldByOther(7, "b", 120));
        }

        [Fact]
        public void Nothing_is_claimed_for_nobody_or_for_no_time()
        {
            var b = new DutyClaimBook();
            Assert.False(b.TryClaim(0, "a", 0, 100)); Assert.False(b.TryClaim(7, "", 0, 100)); Assert.False(b.TryClaim(7, "a", 0, 0)); Assert.Equal(0, b.Count);
        }
    }
}
