using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class QuickSetupTests
    {
        private static SetupCandidate Who(int id, params (string career, float fit)[] fits) { var c = new SetupCandidate { Person = id }; foreach (var f in fits) c.Fit[f.career] = f.fit; return c; }

        [Fact]
        public void Everyone_is_suggested_at_most_one_career_they_suit()
        {
            var people = new[] { Who(1, ("med", 12f), ("mil", 5f)), Who(2, ("mil", 11f), ("med", 3f)), Who(3, ("med", 2f)) };
            var plan = QuickSetupPlanner.Plan(people, new[] { "med", "mil" }, SetupPreset.Balanced);
            Assert.Equal(plan.Count, plan.Select(s => s.Person).Distinct().Count());
            Assert.Equal("med", plan.Single(s => s.Person == 1).Career); Assert.Equal("mil", plan.Single(s => s.Person == 2).Career);
            Assert.DoesNotContain(plan, s => s.Person == 3);          // fit 2 is below the bar: nobody is pushed into a profession they do not suit
        }

        [Fact]
        public void No_career_takes_more_than_its_share_but_each_gets_someone()
        {
            var people = Enumerable.Range(1, 8).Select(i => Who(i, ("a", 15f - i * .1f), ("b", 6f))).ToList();
            var plan = QuickSetupPlanner.Plan(people, new[] { "a", "b" }, SetupPreset.Balanced);          // 25% of 8 = 2 places each
            Assert.True(plan.Count(s => s.Career == "a") <= 2); Assert.True(plan.Any(s => s.Career == "b"));
        }

        [Fact]
        public void A_career_that_is_already_staffed_counts_toward_its_share()
        {
            var people = new[] { Who(1, ("a", 12f)), Who(2, ("a", 11f)) };
            var plan = QuickSetupPlanner.Plan(people, new[] { "a" }, SetupPreset.Light, new Dictionary<string, int> { { "a", 1 } });          // cap is 1 and one is already there
            Assert.Empty(plan);
        }

        [Fact]
        public void Fit_is_the_best_step_average_and_passion_counts()
        {
            var rules = new List<IDictionary<string, int>> { new Dictionary<string, int> { { "Medicine", 6 } }, new Dictionary<string, int> { { "Shooting", 5 }, { "Melee", 5 } } };
            float fit = QuickSetupPlanner.Fit(rules, s => s == "Medicine" ? 8 : 4, s => s == "Medicine" ? 1 : 0);
            Assert.Equal(10f, fit);          // (8 + 1 passion * 2) beats the average of the combat skills (4)
            Assert.Equal(0f, QuickSetupPlanner.Fit(new List<IDictionary<string, int>>(), s => 20, s => 2));
        }
    }
}
