using System.Collections.Generic;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class CareerFocusTests
    {
        private static CareerProgressRow Row(string key, bool earned, int gen, int genNeed, int spec = 0, int specNeed = 0, bool available = true) =>
            new CareerProgressRow { Key = key, Earned = earned, Available = available, GeneralXp = gen, GeneralNeeded = genNeed, SpecialtyXp = spec, SpecialtyNeeded = specNeed };

        [Fact]
        public void The_bar_follows_the_next_unearned_step_by_its_slower_half()
        {
            var rows = new List<CareerProgressRow> { Row(null, false, 50, 100), Row("Surgeon", false, 50, 100, 10, 40, false) };
            var f = CareerFocus.Of(rows, null); Assert.Null(f.Target); Assert.Equal(.5f, f.Share, 2); Assert.False(f.Done);
            var s = CareerFocus.Of(rows, "Surgeon"); Assert.Equal("Surgeon", s.Target); Assert.Equal(.25f, s.Share, 2);          // 10 of 40 specialty experience is the slower half
        }

        [Fact]
        public void A_person_with_everything_earned_is_done()
        {
            var rows = new List<CareerProgressRow> { Row(null, true, 100, 100), Row("Surgeon", true, 100, 100, 40, 40) };
            Assert.True(CareerFocus.Of(rows, null).Done); Assert.Equal(1f, CareerFocus.Of(rows, null).Share);
            Assert.False(CareerFocus.Of(new List<CareerProgressRow>(), null).Done);
        }

        [Fact]
        public void Gains_show_while_recent_and_add_up_within_a_streak()
        {
            var g = new GainTracker(); g.Record(1, "c", 5, 1000); g.Record(1, "c", 3, 2000);
            Assert.Equal(8, g.Recent(1, "c", 3000)); Assert.Equal(0, g.Recent(1, "c", 1000 + GainTracker.WindowTicks + 5000)); Assert.Equal(0, g.Recent(2, "c", 3000));
        }
    }
}
