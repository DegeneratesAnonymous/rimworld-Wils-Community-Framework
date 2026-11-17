using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>What this module reports to the Community overview: what is working and what is not.</summary>
    internal static class TreasuryHealth
    {
        public static IEnumerable<HealthNote> Check(FamilyComponent c, string module)
        {
            var reg = c.Registry; var list = new List<HealthNote>();
            var l = reg.Ledger(); var pay = reg.Payroll(); var book = reg.Assignments(); int funds = l.Balance(Ledger.CommunityId);
            int owed = l.Debts.Where(d => d.Active && d.Kind == DebtKind.Wage).Sum(d => d.Outstanding);
            var assigned = book.AllPeople().Distinct().Where(id => reg.GetPerson(id)?.InColony == true).SelectMany(id => book.RolesOf(id).Select(role => new KeyValuePair<int, string>(id, role))).ToList();
            var lines = PayrollBudget.ByModule(pay.Earned, assigned, role => CareerService.Professions.ById(role)?.ModuleId, pay.Policy); int days = PayrollBudget.RunwayDays(funds, lines);
            if (owed > 0) list.Add(HealthNote.Bad(module, "WFL_Health_Tr_Owed".Translate(owed), "WFL_Health_Tr_OwedHint".Translate()));
            else if (days >= 0 && days < 3) list.Add(HealthNote.Warn(module, "WFL_Health_Tr_Low".Translate(days), "WFL_Health_Tr_LowHint".Translate()));
            else if (days >= 0) list.Add(HealthNote.Good(module, "WFL_Health_Tr_Paid".Translate(days)));
            int perDay = lines.Sum(x => x.MaxPerDay); int income = PayrollBudget.IncomeSince(l.History, Ledger.CommunityId, TreasuryService.Today() - 15);
            if (perDay > 0 && income / 15 < perDay / 2) list.Add(HealthNote.Warn(module, "WFL_Health_Tr_Income".Translate(income / 15, perDay), "WFL_Health_Tr_IncomeHint".Translate()));
            else if (perDay > 0) list.Add(HealthNote.Good(module, "WFL_Health_Tr_IncomeOk".Translate(income / 15, perDay)));
            return list;
        }
    }
}
