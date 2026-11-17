using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class SquadTests
    {
        [Fact]
        public void Squads_nest_and_their_members_include_the_squads_inside()
        {
            var b = new SquadBook(); var a = b.Create("Alpha"); var a1 = b.Create("Alpha one", a.Id);
            b.Add(a.Id, 1); b.Add(a1.Id, 2); b.Add(a1.Id, 1);
            Assert.Equal(new[] { 1, 2 }, b.Effective(a.Id).OrderBy(x => x)); Assert.Equal(new[] { 1, 2 }, b.Effective(a1.Id).OrderBy(x => x));
            Assert.Equal(2, b.SquadsOf(1).Count()); Assert.False(b.Add(a.Id, 1));
        }

        [Fact]
        public void Nesting_names_and_counts_are_limited()
        {
            var b = new SquadBook(); Assert.Null(b.Create("   ")); Assert.Null(b.Create("x", 99));
            int parent = 0; for (int i = 0; i < SquadBook.MaxDepth; i++) { var s = b.Create("L" + i, parent); Assert.NotNull(s); parent = s.Id; }
            Assert.Null(b.Create("too deep", parent));
            Assert.Equal(SquadBook.MaxName, SquadBook.CleanName(new string('a', 100)).Length);
        }

        [Fact]
        public void Deleting_a_squad_moves_its_children_up_and_forgetting_a_person_removes_them_everywhere()
        {
            var b = new SquadBook(); var a = b.Create("A"); var m = b.Create("M", a.Id); var c = b.Create("C", m.Id); b.Add(c.Id, 7); b.Add(a.Id, 7);
            Assert.True(b.Delete(m.Id)); Assert.Equal(a.Id, b.Get(c.Id).ParentId); Assert.False(b.Delete(m.Id));
            Assert.Equal(2, b.Forget(7)); Assert.Empty(b.SquadsOf(7));
        }

        [Fact]
        public void Squads_survive_a_save_and_load_with_ids_kept()
        {
            var b = new SquadBook(); var a = b.Create("Alpha, the \"first\""); var a1 = b.Create("Sub", a.Id); b.Add(a1.Id, 5); b.Add(a1.Id, 6); b.Delete(b.Create("gone").Id);
            var loaded = SquadBook.Decode(new StateReader(b.Encode()));
            Assert.Equal(2, loaded.All.Count); Assert.Equal("Alpha, the \"first\"", loaded.Get(a.Id).Name); Assert.Equal(a.Id, loaded.Get(a1.Id).ParentId); Assert.Equal(new[] { 5, 6 }, loaded.Get(a1.Id).Members);
            Assert.True(loaded.Create("next").Id > a1.Id);          // ids keep counting up
        }
    }
}
