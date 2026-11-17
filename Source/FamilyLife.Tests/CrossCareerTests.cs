using System.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    /// <summary>The framework rule that lets a role require another career's qualification by its ledger id, so two modules can make a hybrid without referencing each other.</summary>
    public class CrossCareerTests
    {
        private static CareerDefinition Hybrid() => new CareerDefinition
        {
            Id = "mod.a.career", ModuleId = "mod.a", Level2 = new CareerRule { GeneralXp = 10 },
            Specialties = new System.Collections.Generic.Dictionary<string, CareerRule>
            {
                { "Plain", new CareerRule { GeneralXp = 20, SpecialtyXp = 5 } },
                { "Both", new CareerRule { GeneralXp = 20, SpecialtyXp = 5 }.Needs("mod.b.career") },
                { "Deep", new CareerRule { GeneralXp = 20, SpecialtyXp = 5 }.Needs("mod.b.career:Surgeon", "mod.c.career") }
            }
        };

        [Fact]
        public void A_requirement_on_another_career_blocks_until_it_is_held()
        {
            var d = Hybrid(); var l = new CareerLedger(); l.Award(1, d.Id, "Both", "k", "a", 100, 0); l.Evaluate(1, d, k => 0);
            Assert.True(l.IsQualified(1, d.Id)); Assert.False(l.IsSpecialist(1, d.Id, "Both"));
            l.Grandfather(1, "mod.b.career"); l.Evaluate(1, d, k => 0); Assert.True(l.IsSpecialist(1, d.Id, "Both"));
        }

        [Fact]
        public void An_absent_module_leaves_the_role_unavailable_and_unrelated_roles_alone()
        {
            var d = Hybrid(); var l = new CareerLedger(); l.Award(1, d.Id, "Plain", "k", "a", 100, 0); l.Award(1, d.Id, "Both", "k", "b", 100, 0); l.Evaluate(1, d, k => 0);
            Assert.True(l.IsSpecialist(1, d.Id, "Plain")); Assert.False(l.IsSpecialist(1, d.Id, "Both"));
        }

        [Fact]
        public void Every_listed_requirement_is_needed_and_a_specialty_of_the_other_career_can_be_named()
        {
            var d = Hybrid(); var l = new CareerLedger(); l.Award(1, d.Id, "Deep", "k", "a", 100, 0); l.Grandfather(1, "mod.b.career"); l.Evaluate(1, d, k => 0);
            Assert.False(l.IsSpecialist(1, d.Id, "Deep"));          // needs the Surgeon specialty and the other career too
            l.Grandfather(1, "mod.c.career"); l.Evaluate(1, d, k => 0); Assert.False(l.IsSpecialist(1, d.Id, "Deep"));
            Assert.True(l.Holds(1, "mod.b.career")); Assert.False(l.Holds(1, "mod.b.career:Surgeon")); Assert.False(l.Holds(2, "mod.b.career"));
        }

        [Fact]
        public void Progress_and_missing_say_what_is_still_needed()
        {
            var d = Hybrid(); var l = new CareerLedger(); l.Award(1, d.Id, "Both", "k", "a", 100, 0); l.Evaluate(1, d, k => 0);
            var row = l.Progress(1, d, k => 0).Single(r => r.Key == "Both"); Assert.False(row.Available);
            Assert.Contains("qualification mod.b.career", l.Missing(1, d.Id, "Both", d.Specialties["Both"], k => 0));
            l.Grandfather(1, "mod.b.career");
            Assert.True(l.Progress(1, d, k => 0).Single(r => r.Key == "Both").Available);
            Assert.DoesNotContain(l.Missing(1, d.Id, "Both", d.Specialties["Both"], k => 0), m => m.StartsWith("qualification"));
        }

        [Fact]
        public void A_rule_with_no_cross_requirement_behaves_exactly_as_before()
        {
            Assert.Empty(new CareerRule { GeneralXp = 1 }.RequiresAll); Assert.Empty(new CareerRule().Needs().RequiresAll); Assert.Empty(new CareerRule().Needs(null, "").RequiresAll);
        }
    }
}
