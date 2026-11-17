using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class CrossTrainingTests
    {
        private static readonly CrossTrainingRule[] Rules =
        {
            new CrossTrainingRule { From = "mil", KindPrefix = "mil.duty.FirstAid", To = "care", Share = .5f },
            new CrossTrainingRule { From = "mil", KindPrefix = "mil.duty.Patrol", To = "ward", Share = .25f },
            new CrossTrainingRule { From = "ward", To = "mil", Share = .25f },
        };

        [Fact]
        public void An_activity_credits_the_related_career_a_share_of_its_points()
        {
            var c = CrossTraining.Credits(Rules, "mil", "mil.duty.FirstAid", 12);
            Assert.Single(c); Assert.Equal("care", c[0].Key); Assert.Equal(6, c[0].Value);
        }

        [Fact]
        public void Only_the_matching_kind_and_career_earn_a_credit()
        {
            Assert.Empty(CrossTraining.Credits(Rules, "mil", "mil.duty.Drill", 12));          // drill is not first aid or patrol
            Assert.Single(CrossTraining.Credits(Rules, "ward", "ward.round", 8));          // no kind named: any warden work counts
            Assert.Empty(CrossTraining.Credits(Rules, "care", "care.tend", 8));
        }

        [Fact]
        public void A_credit_is_at_least_one_point_never_more_than_half_and_never_to_itself()
        {
            Assert.Equal(1, CrossTraining.Credits(Rules, "ward", "ward.round", 1)[0].Value);
            var greedy = new[] { new CrossTrainingRule { From = "a", To = "b", Share = 5f }, new CrossTrainingRule { From = "a", To = "a", Share = .5f } };
            var c = CrossTraining.Credits(greedy, "a", "x", 10); Assert.Single(c); Assert.Equal(5, c[0].Value);          // clamped to half, and no credit back to its own career
        }

        [Fact]
        public void Two_rules_for_one_target_do_not_add_up()
        {
            var two = new[] { new CrossTrainingRule { From = "a", To = "b", Share = .2f }, new CrossTrainingRule { From = "a", To = "b", Share = .4f } };
            Assert.Equal(4, CrossTraining.Credits(two, "a", "x", 10).Single().Value);
        }
    }
}
