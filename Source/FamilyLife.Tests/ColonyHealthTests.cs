using System.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class ColonyHealthTests
    {
        [Fact]
        public void Notes_are_ordered_worst_first_and_the_worst_decides_the_summary()
        {
            var notes = new[] { HealthNote.Good("b", "ok"), HealthNote.Bad("a", "broken"), HealthNote.Warn("c", "watch") };
            var ordered = HealthRules.Order(notes); Assert.Equal(HealthLevel.Bad, ordered[0].Level); Assert.Equal(HealthLevel.Good, ordered.Last().Level);
            Assert.Equal(HealthLevel.Bad, HealthRules.Worst(notes)); Assert.Equal(HealthLevel.Good, HealthRules.Worst(new HealthNote[0]));
        }

        [Fact]
        public void A_module_is_unstaffed_or_idle_only_when_that_is_true_and_known()
        {
            Assert.Equal(StaffReason.Unstaffed, HealthRules.Staffing(0, 0, true));
            Assert.Equal(StaffReason.Idle, HealthRules.Staffing(3, 0, true));
            Assert.Equal(StaffReason.Fine, HealthRules.Staffing(3, 0, false));          // just after a load nobody has been seen working yet
            Assert.Equal(StaffReason.Fine, HealthRules.Staffing(3, 1, true));
        }

        [Fact]
        public void A_need_turns_serious_after_a_few_days()
        {
            Assert.Equal(HealthLevel.Warn, HealthRules.NeedLevel(1000)); Assert.Equal(HealthLevel.Bad, HealthRules.NeedLevel(400000));
        }
    }
}
