using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class NeedBoardTests
    {
        [Fact]
        public void A_need_is_posted_once_and_resolved_by_its_own_module()
        {
            var b = new NeedBoard();
            Assert.True(b.Post("wils.education", "wils.education.classroom", "WFL_Need_Classroom", "", 100, 1, 5, 6));
            Assert.False(b.Post("wils.education", "wils.education.classroom", "WFL_Need_Classroom", "now", 900, 1, 5, 6));          // already open: not new, keeps its first date
            Assert.Equal(100, b.Open[0].PostedTick); Assert.Equal("now", b.Open[0].Detail);
            Assert.False(b.Resolve("wils.care", "wils.education.classroom")); Assert.Single(b.Open);          // another module cannot close it
            Assert.True(b.Resolve("wils.education", "wils.education.classroom")); Assert.Empty(b.Open);
        }

        [Fact]
        public void A_dismissed_need_stays_quiet_until_it_has_been_met_and_comes_back()
        {
            var b = new NeedBoard(); b.Post("m", "m.k", "L", "", 1);
            Assert.True(b.Dismiss("m.k")); Assert.Empty(b.Open);
            Assert.False(b.Post("m", "m.k", "L", "", 2)); Assert.Empty(b.Open);          // still dismissed
            b.Resolve("m", "m.k");          // the need was met
            Assert.True(b.Post("m", "m.k", "L", "", 3)); Assert.Single(b.Open);          // and now it is a fresh need
        }

        [Fact]
        public void The_board_survives_a_save_and_load()
        {
            var b = new NeedBoard(); b.Post("wils.care", "wils.care.clinic", "WFL_Need_Clinic", "a, b", 50, 2, 7, 9); b.Post("m", "m.gone", "L", "", 1); b.Dismiss("m.gone");
            var loaded = NeedBoard.Decode(new StateReader(b.Encode()));
            Assert.Single(loaded.Open); Assert.Equal("a, b", loaded.Open[0].Detail); Assert.Equal(2, loaded.Open[0].Map); Assert.Equal(9, loaded.Open[0].Z); Assert.True(loaded.IsDismissed("m.gone"));
        }

        [Fact]
        public void A_need_without_a_place_has_none()
        {
            var b = new NeedBoard(); b.Post("m", "m.k", "L", "", 1); Assert.False(b.Open[0].HasPlace);
        }
    }
}
