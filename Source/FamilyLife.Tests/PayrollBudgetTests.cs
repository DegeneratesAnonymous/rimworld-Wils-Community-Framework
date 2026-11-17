using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class PayrollBudgetTests
    {
        private static string Module(string role) => role.StartsWith("mil.") ? "military" : role.StartsWith("care.") ? "care" : null;

        [Fact]
        public void The_bill_is_split_by_module_with_pending_wages_and_staff()
        {
            var policy = new WagePolicy(); policy.Rates["mil.a"] = 3; policy.Rates["care.a"] = 2; policy.DailyUnits["mil.a"] = 2; policy.DailyUnits["care.a"] = 2;
            var earned = new[] { new Earning { Pawn = 1, Role = "mil.a", Silver = 6, Day = 1 }, new Earning { Pawn = 2, Role = "care.a", Silver = 4, Day = 1 }, new Earning { Pawn = 3, Role = "vanilla.cooking", Silver = 1, Day = 1 } };
            var assigned = new[] { new KeyValuePair<int, string>(1, "mil.a"), new KeyValuePair<int, string>(2, "care.a") };
            var lines = PayrollBudget.ByModule(earned, assigned, Module, policy);
            var mil = lines.Single(l => l.Module == "military"); Assert.Equal(1, mil.Staff); Assert.Equal(6, mil.Pending); Assert.Equal(6, mil.MaxPerDay);
            var care = lines.Single(l => l.Module == "care"); Assert.Equal(4, care.MaxPerDay);
            Assert.Equal(1, lines.Single(l => l.Module == null).Pending);          // vanilla labour has no module
        }

        [Fact]
        public void One_person_is_capped_over_all_their_roles()
        {
            var policy = new WagePolicy { PawnDailyCap = 10 }; policy.Rates["mil.a"] = 6; policy.DailyUnits["mil.a"] = 2; policy.Rates["care.a"] = 6; policy.DailyUnits["care.a"] = 2;          // 12 + 12 would be 24
            var assigned = new[] { new KeyValuePair<int, string>(1, "mil.a"), new KeyValuePair<int, string>(1, "care.a") };
            var lines = PayrollBudget.ByModule(null, assigned, Module, policy);
            Assert.Equal(10, lines.Sum(l => l.MaxPerDay)); Assert.All(lines, l => Assert.Equal(1, l.Staff));          // the person counts once in each module, but their pay once
        }

        [Fact]
        public void Runway_is_funds_over_the_daily_maximum()
        {
            var lines = new[] { new BudgetLine { MaxPerDay = 6 }, new BudgetLine { MaxPerDay = 4 } };
            Assert.Equal(12, PayrollBudget.RunwayDays(125, lines)); Assert.Equal(-1, PayrollBudget.RunwayDays(100, new BudgetLine[0]));
        }
    }
}
