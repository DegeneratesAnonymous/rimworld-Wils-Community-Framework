using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class ModCompatTests
    {
        [Fact]
        public void Expertise_counts_only_for_the_career_it_belongs_to()
        {
            Assert.Equal("wils.care.practice", ExpertiseMap.CareerOf("Surgeon")); Assert.Null(ExpertiseMap.CareerOf("Nothing")); Assert.Null(ExpertiseMap.CareerOf(null));
            Assert.Equal(8f, ExpertiseMap.FitFor("wils.care.practice", new[] { "Surgeon", "BattlefieldMedic", "Sharp" }));
            Assert.Equal(0f, ExpertiseMap.FitFor("wils.works.miner", new[] { "Surgeon" })); Assert.Equal(0f, ExpertiseMap.FitFor(null, new[] { "Surgeon" }));
        }

        [Fact]
        public void Field_rescue_counts_for_care_but_capture_and_mech_work_do_not()
        {
            var src = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoRoot(), "Source/Care/Game/CareFieldRescue.cs"));
            Assert.Contains("StartsWith(\"SAR_\"", src); Assert.Contains("Contains(\"Capture\")", src); Assert.Contains("Contains(\"Mech\")", src);
        }

        [Fact]
        public void Trade_and_plain_income_into_the_account_are_counted_since_a_day()
        {
            var h = new[]
            {
                new Transaction { Day = 10, To = "c:1", Amount = 50, Kind = TxKind.Trade }, new Transaction { Day = 12, To = "c:1", Amount = 20, Kind = TxKind.Income },
                new Transaction { Day = 12, To = "c:1", Amount = 99, Kind = TxKind.Tax }, new Transaction { Day = 1, To = "c:1", Amount = 77, Kind = TxKind.Trade }, new Transaction { Day = 12, To = "x", Amount = 5, Kind = TxKind.Trade },
            };
            Assert.Equal(70, PayrollBudget.IncomeSince(h, "c:1", 5)); Assert.Equal(0, PayrollBudget.IncomeSince(null, "c:1", 5));
        }

        private static string RepoRoot() => RepoPaths.Root();
    }
}
