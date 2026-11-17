using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class ReleaseHoldTests
    {
        [Fact]
        public void The_condition_decides_when_a_marked_prisoner_may_go()
        {
            Assert.False(ReleaseRule.Due(ReleaseWhen.Healthy, true, false, false)); Assert.False(ReleaseRule.Due(ReleaseWhen.Healthy, false, true, true)); Assert.True(ReleaseRule.Due(ReleaseWhen.Healthy, false, false, true));
            Assert.False(ReleaseRule.Due(ReleaseWhen.CanWalk, false, true, false)); Assert.True(ReleaseRule.Due(ReleaseWhen.CanWalk, false, true, true)); Assert.False(ReleaseRule.Due(ReleaseWhen.None, false, false, true));
        }

        [Fact]
        public void Holds_are_set_cleared_and_saved()
        {
            var h = new ReleaseHold(); h.Set(5, ReleaseWhen.Healthy); h.Set(6, ReleaseWhen.CanWalk); h.Set(7, ReleaseWhen.None); h.Set(0, ReleaseWhen.Healthy);
            Assert.Equal(2, h.Count); var loaded = ReleaseHold.Decode(new StateReader(h.Encode()));
            Assert.Equal(ReleaseWhen.Healthy, loaded.When(5)); Assert.Equal(ReleaseWhen.CanWalk, loaded.When(6)); Assert.Equal(ReleaseWhen.None, loaded.When(99));
            loaded.Set(5, ReleaseWhen.None); Assert.Equal(new[] { 6 }, loaded.People.ToArray());
        }
    }
}
