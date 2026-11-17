using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    /// <summary>One module's share of the wage bill.</summary>
    public sealed class BudgetLine { public string Module; public int Staff, Pending, MaxPerDay; }

    /// <summary>
    /// The colony's wage bill seen by module: who is paid for each module's professions, what they have earned that is not yet paid, and the most it could cost per day. Read-only; it never moves silver.
    /// Roles are matched to modules by the career registry (a module is just a name here), and roles with no module (vanilla labour) are listed under a null module.
    /// </summary>
    public static class PayrollBudget
    {
        public static List<BudgetLine> ByModule(IEnumerable<Earning> earned, IEnumerable<KeyValuePair<int, string>> assigned, Func<string, string> moduleOfRole, WagePolicy policy)
        {
            var lines = new Dictionary<string, BudgetLine>(); var none = "\u0000";
            BudgetLine Line(string module) { var key = module ?? none; if (!lines.TryGetValue(key, out var l)) lines[key] = l = new BudgetLine { Module = module }; return l; }

            foreach (var e in earned ?? Enumerable.Empty<Earning>()) Line(moduleOfRole(e.Role)).Pending += e.Silver;

            // staff, and the most each module's roles could cost in a day (one person is capped over all their roles)
            foreach (var person in (assigned ?? Enumerable.Empty<KeyValuePair<int, string>>()).GroupBy(a => a.Key))
            {
                var perModule = person.Select(a => new { Module = moduleOfRole(a.Value), Cost = policy.RateOf(a.Value) * policy.UnitsPerDay(a.Value) }).GroupBy(x => x.Module).ToList();
                int uncapped = perModule.Sum(g => g.Sum(x => x.Cost)), cap = policy.PawnDailyCap; double scale = uncapped > cap && uncapped > 0 ? (double)cap / uncapped : 1.0;
                foreach (var g in perModule) { var l = Line(g.Key); l.Staff++; l.MaxPerDay += (int)Math.Round(g.Sum(x => x.Cost) * scale); }
            }
            return lines.Values.OrderByDescending(l => l.MaxPerDay).ThenByDescending(l => l.Pending).ThenBy(l => l.Module, StringComparer.Ordinal).ToList();
        }

        /// <summary>How many days the funds would last if everyone earned the daily maximum, or -1 when there is no bill.</summary>
        public static int RunwayDays(int funds, IEnumerable<BudgetLine> lines)
        {
            int perDay = lines.Sum(l => l.MaxPerDay); return perDay <= 0 ? -1 : Math.Max(0, funds) / perDay;
        }
    }
}
