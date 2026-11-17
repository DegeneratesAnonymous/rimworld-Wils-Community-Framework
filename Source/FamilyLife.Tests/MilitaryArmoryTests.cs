using System.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class MilitaryArmoryTests
    {
        private static WeaponFacts W(int id, bool ranged, int quality = 2, double value = 100) => new WeaponFacts { Id = id, Ranged = ranged, Quality = quality, Value = value };

        [Fact]
        public void The_unarmed_always_need_a_weapon_and_a_suitable_one_is_left_alone()
        {
            foreach (var t in new[] { PostKind.Melee, PostKind.Ranged, PostKind.Mixed }) Assert.True(ArmoryRules.NeedsWeapon(t, null));
            Assert.False(ArmoryRules.NeedsWeapon(PostKind.Melee, false)); Assert.False(ArmoryRules.NeedsWeapon(PostKind.Ranged, true));
            Assert.False(ArmoryRules.NeedsWeapon(PostKind.Mixed, true)); Assert.False(ArmoryRules.NeedsWeapon(PostKind.Mixed, false));          // Support and Recruits are content with anything
        }

        [Fact]
        public void A_weapon_of_the_wrong_kind_for_the_branch_is_a_gap()
        {
            Assert.True(ArmoryRules.NeedsWeapon(PostKind.Melee, true)); Assert.True(ArmoryRules.NeedsWeapon(PostKind.Ranged, false));
        }

        [Fact]
        public void A_soldier_may_take_a_weapon_of_their_own_kind_and_a_mixed_soldier_any()
        {
            Assert.True(ArmoryRules.Fits(PostKind.Melee, W(1, false))); Assert.False(ArmoryRules.Fits(PostKind.Melee, W(1, true)));
            Assert.True(ArmoryRules.Fits(PostKind.Ranged, W(1, true))); Assert.False(ArmoryRules.Fits(PostKind.Ranged, W(1, false)));
            Assert.True(ArmoryRules.Fits(PostKind.Mixed, W(1, true))); Assert.True(ArmoryRules.Fits(PostKind.Mixed, W(1, false)));
        }

        [Fact]
        public void The_best_is_the_highest_quality_then_value_then_the_lowest_id()
        {
            var weapons = new[] { W(1, true, 2, 300), W(2, true, 4, 100), W(3, true, 4, 250), W(4, true, 4, 250), W(5, false, 6, 999) };
            Assert.Equal(new[] { 3, 4, 2, 1 }, ArmoryRules.Rank(PostKind.Ranged, weapons).Select(w => w.Id).ToArray());          // quality first, then value, then id; the melee weapon is not for a Ranger
            Assert.Equal(3, ArmoryRules.Best(PostKind.Ranged, weapons).Id); Assert.Equal(5, ArmoryRules.Best(PostKind.Melee, weapons).Id);
            Assert.Equal(5, ArmoryRules.Best(PostKind.Mixed, weapons).Id);          // a Mixed soldier takes the best of everything
        }

        [Fact]
        public void A_weapon_with_no_quality_ranks_below_one_that_has_it_and_nothing_fits_nothing()
        {
            Assert.Equal(2, ArmoryRules.Best(PostKind.Ranged, new[] { W(1, true, -1, 9999), W(2, true, 0, 1) }).Id);
            Assert.Null(ArmoryRules.Best(PostKind.Melee, new[] { W(1, true) })); Assert.Null(ArmoryRules.Best(PostKind.Ranged, new WeaponFacts[0])); Assert.Null(ArmoryRules.Best(PostKind.Mixed, null)); Assert.Empty(ArmoryRules.Rank(PostKind.Mixed, new WeaponFacts[] { null }));
        }

        [Fact]
        public void Arming_is_retried_gently_and_never_in_a_rush()
        {
            Assert.True(ArmoryRules.MayTry(1000, 0)); Assert.False(ArmoryRules.MayTry(1000 + ArmoryRules.RetryTicks - 1, 1000)); Assert.True(ArmoryRules.MayTry(1000 + ArmoryRules.RetryTicks, 1000)); Assert.True(ArmoryRules.MayTry(10, 5000));
            Assert.InRange(ArmoryRules.PerLook, 1, 3);
        }

        [Fact]
        public void The_soldier_types_line_up_with_the_roles()
        {
            Assert.Equal(PostKind.Melee, PostPlanner.TypeOf("Soldier")); Assert.Equal(PostKind.Ranged, PostPlanner.TypeOf("Ranger"));
            foreach (var role in new[] { "FieldMedic", "Officer", "Recruit" }) Assert.Equal(PostKind.Mixed, PostPlanner.TypeOf(role));
        }
    }
}
