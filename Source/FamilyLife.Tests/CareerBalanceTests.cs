using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    /// <summary>
    /// Keeps the careers on one curve so progression reads the same everywhere: a qualified professional (tier 2) asks for a main skill of 6 or less and 120 experience;
    /// a specialty (tier 3) asks for 250 experience and 60 of its own, and a main skill that rises in steps of two (8, 10, 12) rather than anything in between.
    /// </summary>
    public class CareerBalanceTests
    {
        private static IEnumerable<CareerDefinition> All() => new[]
        {
            AgricultureCareers.Definition(), CraftingCareers.Definition(), CulinaryCareers.Definition(), IntelligenceCareers.Definition(),
            MilitaryCareers.Definition(), WardenCareers.Definition(), WorksCareers.BuilderDefinition(), WorksCareers.MinerDefinition()
        };

        private static int Main(CareerRule r) => r.Skills.Count == 0 ? 0 : r.Skills.Values.Max();

        [Fact]
        public void A_qualified_professional_is_one_level_for_everyone()
        {
            foreach (var def in All())
                foreach (var rule in def.Level2Branches.Count > 0 ? def.Level2Branches.Values : new[] { def.Level2 }.AsEnumerable())
                { Assert.InRange(Main(rule), 6, 6); Assert.Equal(120, rule.GeneralXp); }
        }

        [Fact]
        public void Specialties_ask_the_same_experience_and_a_skill_that_steps_up_by_two()
        {
            foreach (var def in All())
                foreach (var spec in def.Specialties)
                {
                    Assert.Equal(250, spec.Value.GeneralXp); Assert.Equal(60, spec.Value.SpecialtyXp);
                    int main = Main(spec.Value); Assert.True(main == 8 || main == 10 || main == 12, def.Id + ":" + spec.Key + " asks " + main);
                }
        }

        [Fact]
        public void A_specialty_is_never_easier_than_the_step_before_it()
        {
            foreach (var def in All())
            {
                int tier2 = def.Level2Branches.Count > 0 ? def.Level2Branches.Values.Min(Main) : Main(def.Level2);
                foreach (var spec in def.Specialties) Assert.True(Main(spec.Value) >= tier2, def.Id + ":" + spec.Key);
            }
        }
    }
}
