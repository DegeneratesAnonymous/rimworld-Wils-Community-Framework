using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class PrimaryRoleTests
    {
        private static AssignmentBook Book()
        {
            var cat = new ProfessionCatalog(); var ledger = new CareerLedger(); var b = new AssignmentBook();
            foreach (var id in new[] { "m.a", "m.b" }) cat.Register(new ProfessionRole { Id = id, ModuleId = "m", Career = "m.c", Level = 1 });
            b.Assign(1, cat.ById("m.a"), ledger, 0); b.Assign(1, cat.ById("m.b"), ledger, 0); return b;
        }

        [Fact]
        public void The_main_profession_defaults_to_the_first_and_can_be_chosen()
        {
            var b = Book(); Assert.Equal("m.a", b.PrimaryOf(1));
            Assert.True(b.SetPrimary(1, "m.b")); Assert.Equal("m.b", b.PrimaryOf(1)); Assert.False(b.SetPrimary(1, "m.zzz")); Assert.Null(b.PrimaryOf(2));
        }

        [Fact]
        public void Losing_the_main_profession_falls_back_and_the_choice_survives_a_save()
        {
            var b = Book(); b.SetPrimary(1, "m.b");
            var loaded = AssignmentBook.Decode(new StateReader(b.Encode())); Assert.Equal("m.b", loaded.PrimaryOf(1));
            b.Unassign(1, "m.b"); Assert.Equal("m.a", b.PrimaryOf(1));
            b.Vacate(1); Assert.Null(b.PrimaryOf(1));
        }
    }
}
