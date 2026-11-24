using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class RoleLoadTests
    {
        [Fact]
        public void Capacity_grows_one_role_for_every_two_skill_levels_above_eight()
        {
            Assert.Equal(1, RoleLoad.Capacity(0)); Assert.Equal(1, RoleLoad.Capacity(8)); Assert.Equal(1, RoleLoad.Capacity(9));
            Assert.Equal(2, RoleLoad.Capacity(10)); Assert.Equal(3, RoleLoad.Capacity(12)); Assert.Equal(7, RoleLoad.Capacity(20));
        }

        [Fact]
        public void Each_other_profession_held_raises_what_a_new_role_asks_for()
        {
            Assert.Equal(0, RoleLoad.Penalty(0)); Assert.Equal(2, RoleLoad.Penalty(1)); Assert.Equal(6, RoleLoad.Penalty(3));
        }

        [Fact]
        public void The_load_reads_open_full_or_over()
        {
            Assert.Equal(RoleLoad.Level.Open, RoleLoad.Of(1, 3)); Assert.Equal(RoleLoad.Level.Full, RoleLoad.Of(3, 3)); Assert.Equal(RoleLoad.Level.Over, RoleLoad.Of(4, 3));
        }

        [Fact]
        public void A_new_qualification_needs_extra_skill_for_each_profession_already_held_and_old_ones_are_kept()
        {
            var def = new CareerDefinition { Id = "med" }; def.Level2 = new CareerRule { GeneralXp = 10 }.Skill("Medicine", 8);
            var ledger = new CareerLedger(); ledger.Award(1, "med", null, "k", "a", 50, 1);
            Assert.Empty(ledger.Evaluate(1, def, k => 9, 2));          // holds one other profession: 8 + 2 = 10 needed
            Assert.Equal(new[] { "med" }, ledger.Evaluate(1, def, k => 10, 2));
            Assert.Empty(ledger.Evaluate(1, def, k => 0, 6));          // already qualified: nothing is taken away or repeated
            var rows = new CareerLedger().Progress(2, def, k => 9, 2); Assert.Equal(10, rows[0].Skills[0].Value[1]);          // the page shows the raised need
        }
    }
}
