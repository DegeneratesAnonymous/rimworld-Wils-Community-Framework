using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// The Treasury page of the Community window, in six subpages: Overview, Accounts, Payroll, Taxation, Debts and Transactions. It only reads the ledger and calls its operations; the page can be
    /// closed, broken or absent without any account changing.
    /// </summary>
    internal static class TreasuryPage
    {
        private static int sub;
        private static string transferFrom, transferTo; private static string amountText = "10"; private static int amount = 10;
        private static readonly string[] Tabs = { "WFL_Tr_Overview", "WFL_Tr_Accounts", "WFL_Tr_Payroll", "WFL_Tr_Taxation", "WFL_Tr_Debts", "WFL_Tr_Transactions", "WFL_Tr_Budget" };

        /// <summary>The number shown on the page's navigation entry: wages owed plus overdue tax, so something needing the player's eye is visible without opening it.</summary>
        public static int Attention()
        {
            var c = FamilyComponent.Current; if (c == null) return 0;
            var l = c.Registry.Ledger(); int today = TreasuryService.Today();
            return l.Debts.Count(d => d.Active && d.Kind == DebtKind.Wage) + l.Debts.Count(d => d.Active && d.Kind == DebtKind.Tax && d.DueDay < today - 7);
        }

        private static string NameOfPawn(FamilyComponent c, int id) => c.TryGetPawn(id)?.LabelShort ?? c.Registry.GetPerson(id)?.Name ?? "#" + id;
        public static string NameOf(FamilyComponent c, string account)
        {
            var a = c.Registry.Ledger().Get(account); if (a == null) return account ?? "WFL_Tr_Outside".Translate().ToString();
            switch (a.Kind)
            {
                case AccountKind.Community: return "WFL_Tr_Community".Translate();
                case AccountKind.Person: return NameOfPawn(c, a.Owner);
                case AccountKind.Household: { var h = c.Registry.GetHousehold(a.Owner); var first = h?.ResidentIds.FirstOrDefault(); return "WFL_Tr_Household".Translate(first.HasValue ? NameOfPawn(c, first.Value) : "#" + a.Owner); }
                default: return "WFL_Tr_Business".Translate(a.Owner);
            }
        }

        private static float Line(Rect r, float y, string text, bool dim = false)
        {
            float h = Text.CalcHeight(text, r.width - 8f); if (dim) GUI.color = Color.gray;
            Widgets.Label(new Rect(4f, y, r.width - 8f, h), text); GUI.color = Color.white; return y + h + 2f;
        }
        private static float Heading(Rect r, float y, string text) { GUI.color = new Color(0.90f, 0.74f, 0.28f); Widgets.Label(new Rect(0f, y, r.width, 24f), text); GUI.color = Color.white; return y + 26f; }

        public static float Draw(Rect r, FamilyComponent c)
        {
            float y = CommunityPageLayout.Tabs(r, 0f, Tabs.Select(t => t.Translate().ToString()).ToList(), sub, i => sub = i);
            switch (sub)
            {
                case 0: return Overview(r, c, y);
                case 1: return Accounts(r, c, y);
                case 2: return Payroll(r, c, y);
                case 3: return Taxation(r, c, y);
                case 4: return Debts(r, c, y);
                case 6: return Budget(r, c, y);
                default: return Transactions(r, c, y);
            }
        }

        private static string Names(FamilyComponent c, string role)
        {
            var list = TreasuryService.Holders(c.Registry, role).Select(id => NameOfPawn(c, id)).ToList();
            return list.Count == 0 ? "WFL_Tr_None".Translate().ToString() : string.Join(", ", list);
        }

        private static float Overview(Rect r, FamilyComponent c, float y)
        {
            var reg = c.Registry; var l = reg.Ledger(); var pay = reg.Payroll(); int today = TreasuryService.Today(); var map = Find.AnyPlayerHomeMap;
            y = CommunityStaffing.Draw(r, y, c, "wils.treasury");
            int payday = PayCalendar.NextPayday(today + 1) - today;
            int columns = r.width >= 600f ? 3 : 1;
            float metricWidth = (r.width - (columns - 1) * 12f) / columns;
            var metricLabels = new[] { "WFL_UX_Funds", "WFL_UX_Pending", "WFL_UX_Payday" };
            var metricValues = new[] { l.Balance(Ledger.CommunityId).ToString(), pay.PendingTotal.ToString(), payday.ToString() };
            for (int m = 0; m < 3; m++) {
                GUI.BeginGroup(new Rect(m % columns * (metricWidth + 12f), y + m / columns * 90f, metricWidth, 90f));
                try { CommunityStaffing.Metric(new Rect(0f, 0f, metricWidth, 90f), 0f, metricLabels[m].Translate(), metricValues[m]); } finally { GUI.EndGroup(); }
            }
            y += columns == 3 ? 90f : 270f;
            y = Line(r, y, "WFL_UX_PayHint".Translate(), true);
            y = Line(r, y, "WFL_Tr_CommunityFunds".Translate(l.Balance(Ledger.CommunityId)));
            int physical = TreasuryService.PhysicalSilver(map);
            y = Line(r, y, "WFL_Tr_Claims".Translate(l.TotalClaims, physical));
            float communityShare = l.TotalClaims <= 0 ? 0f : Mathf.Clamp01(l.Balance(Ledger.CommunityId) / (float)l.TotalClaims);
            CommunityWidgets.Bar(new Rect(4f, y, r.width - 8f, 22f), communityShare, CommunityWidgets.Gold, "WFL_UX_Ownership".Translate(l.Balance(Ledger.CommunityId), l.TotalClaims));
            y += 30f;
            y = Line(r, y, "WFL_UX_OwnershipHint".Translate(), true);
            y = Line(r, y, "WFL_Tr_Owned".Translate(l.TotalClaims, l.TotalAvailable, l.Unbacked), l.Unbacked > 0);
            if (l.Unbacked > 0) y = Line(r, y, "WFL_Tr_Bearer".Translate(l.CommunityShare, l.SpilledToOwners), true);
            int owedWages = l.Debts.Where(d => d.Active && d.Kind == DebtKind.Wage).Sum(d => d.Outstanding);
            if (owedWages > 0) y = Line(r, y, "WFL_Tr_WagesOwedTotal".Translate(owedWages, pay.WorkersMissing(2).Count), true);
            if (l.Unbacked > 0) y = Line(r, y, "WFL_Tr_Discrepancy".Translate(l.Unbacked, l.TotalAvailable), true);
            else if (physical > l.TotalClaims + 3) y = Line(r, y, "WFL_Tr_Surplus".Translate(physical - l.TotalClaims));
            var personal = l.Accounts.Where(a => a.Kind != AccountKind.Community && !a.Closed).ToList();
            y = Line(r, y, "WFL_Tr_Wealth".Translate(personal.Sum(a => a.Balance), personal.Count));
            var zone = map?.zoneManager.AllZones.OfType<Zone_Treasury>().OrderBy(z => z.ID).FirstOrDefault();
            y = Line(r, y, "WFL_Tr_ZoneStatus".Translate(zone == null ? "WFL_Tr_NoZone".Translate().ToString() : zone.GetInspectString().Replace('\n', ' ')));
            y = Line(r, y, "WFL_Tr_Treasurer".Translate(Names(c, TreasuryRoles.Treasurer)));
            y = Line(r, y, "WFL_Tr_Collector".Translate(Names(c, TreasuryRoles.TaxCollector)));
            int days = PayCalendar.NextPayday(today + 1) - today;
            y = Line(r, y, "WFL_Tr_NextPayday".Translate(days, pay.PendingTotal));
            var open = l.Debts.Where(d => d.Active).ToList();
            y = Line(r, y, "WFL_Tr_DebtsTotal".Translate(open.Sum(d => d.Outstanding), open.Count));
            y = Line(r, y, "WFL_Tr_IncomeShare".Translate(reg.Treasury().CommunityPercent));
            var st = reg.Treasury(); int pct = st.CommunityPercent;
            float v = Widgets.HorizontalSlider(new Rect(4f, y, 240f, 20f), pct, 0f, 100f, true, null, null, null, 5f); if ((int)v != pct) st.CommunityPercent = (int)v;
            y += 26f;
            y = Heading(r, y + 6f, "WFL_Tr_Valuables".Translate());
            var names = st.Valuables.All.Select(val => { var d = DefDatabase<ThingDef>.GetNamedSilentFail(val.DefName); return (d != null ? d.LabelCap.ToString() : val.DefName) + (val.Liquid ? " (" + "WFL_Tr_Liquid".Translate() + ")" : ""); }).ToList();
            y = Line(r, y, string.Join(", ", names));
            if (Widgets.ButtonText(new Rect(4f, y, 260f, 26f), "WFL_Tr_ChooseValuables".Translate())) Find.WindowStack.Add(new Dialog_Valuables(st));
            y += 30f;
            y = Heading(r, y + 6f, "WFL_Tr_Transactions".Translate());
            foreach (var t in l.History.Reverse().Take(6)) y = Line(r, y, TxText(c, t), true);
            return y;
        }

        private static string TxText(FamilyComponent c, Transaction t) => "WFL_Tr_TxLine".Translate(t.Day % PayCalendar.DaysPerYear + 1, t.Kind, t.Amount, t.From == null ? "WFL_Tr_Outside".Translate().ToString() : NameOf(c, t.From), t.To == null ? "WFL_Tr_Outside".Translate().ToString() : NameOf(c, t.To));

        private static float Accounts(Rect r, FamilyComponent c, float y)
        {
            var l = c.Registry.Ledger();
            foreach (var a in l.Accounts.Where(x => !x.Closed).OrderBy(x => x.Kind).ThenByDescending(x => x.Balance)) { y = Line(r, y, "WFL_Tr_AccountLine".Translate(NameOf(c, a.Id), a.Balance)); }
            y += 8f; y = Heading(r, y, "WFL_Tr_Transfer".Translate());
            var open = l.Accounts.Where(x => !x.Closed).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            if (Widgets.ButtonText(new Rect(4f, y, 200f, 24f), "WFL_Tr_TransferFrom".Translate() + ": " + (transferFrom == null ? "-" : NameOf(c, transferFrom)))) Find.WindowStack.Add(new FloatMenu(open.Select(a => { var id = a.Id; return new FloatMenuOption(NameOf(c, id), () => transferFrom = id); }).ToList()));
            if (Widgets.ButtonText(new Rect(212f, y, 200f, 24f), "WFL_Tr_TransferTo".Translate() + ": " + (transferTo == null ? "-" : NameOf(c, transferTo)))) Find.WindowStack.Add(new FloatMenu(open.Select(a => { var id = a.Id; return new FloatMenuOption(NameOf(c, id), () => transferTo = id); }).ToList()));
            Widgets.TextFieldNumeric(new Rect(420f, y, 70f, 24f), ref amount, ref amountText, 1f, 1000000f);
            y += 30f;
            if (transferFrom != null && transferTo != null && Widgets.ButtonText(new Rect(4f, y, 220f, 26f), "WFL_Tr_TransferDo".Translate(amount)))
            {
                var res = l.Transfer(transferFrom, transferTo, amount, TxKind.Transfer, "manual:" + Find.TickManager.TicksGame + ":" + transferFrom + ":" + transferTo, TreasuryService.Today(), "player transfer");
                if (res != LedgerResult.Ok) Messages.Message("WFL_Tr_TransferFailed".Translate(res), MessageTypeDefOf.RejectInput, false);
            }
            return y + 32f;
        }

        /// <summary>The wage bill by module: who is paid for each module's professions, what is owed, and how long the community funds would last. Read-only.</summary>
        private static float Budget(Rect r, FamilyComponent c, float y)
        {
            var reg = c.Registry; var pay = reg.Payroll(); var book = reg.Assignments(); var l = reg.Ledger();
            Func<string, string> moduleOf = role => CareerService.Professions.ById(role)?.ModuleId;
            var assigned = book.AllPeople().Distinct().Where(id => reg.GetPerson(id)?.InColony == true).SelectMany(id => book.RolesOf(id).Select(role => new KeyValuePair<int, string>(id, role))).Where(a => { var m = moduleOf(a.Value); return m == null || FamilyFramework.Catalog.IsEnabled(m); }).ToList();
            var lines = PayrollBudget.ByModule(pay.Earned, assigned, moduleOf, pay.Policy);
            int funds = l.Balance(Ledger.CommunityId), perDay = lines.Sum(x => x.MaxPerDay), pending = lines.Sum(x => x.Pending), owed = l.Debts.Where(d => d.Active && d.Kind == DebtKind.Wage).Sum(d => d.Outstanding);
            y = Line(r, y, "WFL_Tr_BudgetFunds".Translate(funds, perDay));
            y = Line(r, y, "WFL_Tr_BudgetIncome".Translate(PayrollBudget.IncomeSince(l.History, Ledger.CommunityId, TreasuryService.Today() - 15), 15));
            int days = PayrollBudget.RunwayDays(funds, lines);
            if (days >= 0) y = Line(r, y, "WFL_Tr_BudgetRunway".Translate(days), days < 3);
            if (owed > 0) y = Line(r, y, "WFL_Tr_BudgetOwed".Translate(owed));
            y = CommunityPageLayout.Help(r, y, "WFL_Tr_BudgetHelp".Translate()) + 4f;
            float w = r.width, nameW = Mathf.Max(140f, w * .34f), colW = (w - nameW) / 3f; Text.Font = GameFont.Tiny; GUI.color = new Color(0.90f, 0.74f, 0.28f);
            Widgets.Label(new Rect(4f, y, nameW, 18f), "WFL_Tr_BudgetModule".Translate()); Widgets.Label(new Rect(nameW, y, colW, 18f), "WFL_Tr_BudgetStaff".Translate()); Widgets.Label(new Rect(nameW + colW, y, colW, 18f), "WFL_Tr_BudgetPending".Translate()); Widgets.Label(new Rect(nameW + colW * 2f, y, colW, 18f), "WFL_Tr_BudgetMax".Translate());
            GUI.color = Color.white; Text.Font = GameFont.Small; y += 22f; int i = 0;
            foreach (var line in lines)
            {
                var row = new Rect(0f, y, w, 24f); if (i++ % 2 == 0) Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, .04f));
                string name = line.Module == null ? "WFL_Tr_BudgetOrdinary".Translate().ToString() : (FamilyFramework.Catalog.Modules.FirstOrDefault(m => m.Id == line.Module)?.Name ?? line.Module).Replace("Wil's Role Modules : ", "");
                Text.Anchor = TextAnchor.MiddleLeft; Widgets.Label(new Rect(4f, y, nameW, 24f), name);
                Widgets.Label(new Rect(nameW, y, colW, 24f), line.Staff.ToString()); Widgets.Label(new Rect(nameW + colW, y, colW, 24f), line.Pending.ToString()); Widgets.Label(new Rect(nameW + colW * 2f, y, colW, 24f), line.MaxPerDay.ToString());
                Text.Anchor = TextAnchor.UpperLeft; y += 24f;
            }
            if (lines.Count == 0) y = Line(r, y, "WFL_Tr_BudgetNone".Translate(), true);
            return y + 8f;
        }

        private static float Payroll(Rect r, FamilyComponent c, float y)
        {
            var reg = c.Registry; var pay = reg.Payroll(); var pol = pay.Policy;
            y = CommunityPageLayout.Help(r, y, "WFL_Tr_PayrollHelp".Translate()) + 4f;
            y = WealthPay(r, c, y);
            y = Heading(r, y + 4f, "WFL_Tr_FundingTitle".Translate());
            foreach (PayrollFunding f in Enum.GetValues(typeof(PayrollFunding))) { if (Widgets.RadioButtonLabeled(new Rect(4f, y, r.width - 16f, 24f), ("WFL_Tr_Funding_" + f).Translate(), pol.Funding == f)) pol.Funding = f; y += 26f; }
            y = CommunityPageLayout.Help(r, y, "WFL_Tr_FundingNote".Translate());
            var roles = CareerService.Professions.All.Where(x => FamilyFramework.Catalog.IsEnabled(x.ModuleId)).OrderBy(x => x.Career, StringComparer.Ordinal).ThenBy(x => x.Level).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
            if (pol.Funding == PayrollFunding.EssentialFirst)
                foreach (var role in roles) { bool ess = pol.Essential.Contains(role.Id); Widgets.CheckboxLabeled(new Rect(24f, y, 360f, 24f), "WFL_Tr_Essential".Translate() + ": " + CareerService.TitleOf(role, reg), ref ess); if (ess) pol.Essential.Add(role.Id); else pol.Essential.Remove(role.Id); y += 26f; }
            if (pol.Funding == PayrollFunding.Manual)
            {
                foreach (var role in roles) if (!pol.Order.Contains(role.Id)) pol.Order.Add(role.Id);
                for (int i = 0; i < pol.Order.Count; i++)
                {
                    var rl = roles.FirstOrDefault(x => x.Id == pol.Order[i]); Widgets.Label(new Rect(24f, y, 300f, 24f), (i + 1) + ". " + (rl == null ? pol.Order[i] : CareerService.TitleOf(rl, reg)));
                    if (i > 0 && Widgets.ButtonText(new Rect(330f, y, 30f, 22f), "â–²")) { var t = pol.Order[i]; pol.Order[i] = pol.Order[i - 1]; pol.Order[i - 1] = t; }
                    if (i < pol.Order.Count - 1 && Widgets.ButtonText(new Rect(364f, y, 30f, 22f), "â–¼")) { var t = pol.Order[i]; pol.Order[i] = pol.Order[i + 1]; pol.Order[i + 1] = t; }
                    y += 26f;
                }
            }
            if (pay.WorkersMissing(2).Count > 0)
            {
                if (Widgets.ButtonText(new Rect(4f, y, 320f, 24f), "WFL_Tr_Renegotiate".Translate())) foreach (var k in pol.Rates.Keys.ToList()) pol.Rates[k] = Math.Max(1, pol.Rates[k] * 3 / 4);
                y = CommunityPageLayout.Help(r, y + 26f, "WFL_Tr_RenegotiateNote".Translate());
            }
            foreach (var role in CareerService.Professions.All.Where(x => FamilyFramework.Catalog.IsEnabled(x.ModuleId)).OrderBy(x => x.Career, StringComparer.Ordinal).ThenBy(x => x.Level).ThenBy(x => x.Id, StringComparer.Ordinal))
                y = RateRow(r, y, role.Id, CareerService.TitleOf(role, reg));
            y = Heading(r, y + 6f, "WFL_Tr_VanillaWork".Translate());
            foreach (var cat in Labour.Categories) y = RateRow(r, y, Labour.Role(cat), cat, WagePolicy.OrdinaryRate);
            int ocap = pol.OrdinaryDailyCap; string obuf = ocap.ToString();
            Widgets.Label(new Rect(4f, y, 300f, 24f), "WFL_Tr_OrdinaryCap".Translate()); Widgets.TextFieldNumeric(new Rect(310f, y, 70f, 24f), ref ocap, ref obuf, 0f, 100000f); pol.OrdinaryDailyCap = ocap; y += 28f;
            int cap = pol.PawnDailyCap; string buf = cap.ToString();
            Widgets.Label(new Rect(4f, y, 300f, 24f), "WFL_Tr_DailyCap".Translate()); Widgets.TextFieldNumeric(new Rect(310f, y, 70f, 24f), ref cap, ref buf, 0f, 100000f); pol.PawnDailyCap = cap; y += 32f;
            y = Heading(r, y, "WFL_Tr_PastPayments".Translate());
            foreach (var rep in pay.Reports.AsEnumerable().Reverse()) y = Line(r, y, "WFL_Tr_PaydayLine".Translate(rep.Payday % PayCalendar.DaysPerYear + 1, rep.TotalDue, rep.TotalPaid, rep.Shortfall > 0 ? (string)"WFL_Tr_ShortBy".Translate(rep.Shortfall) : ""), true);
            return y;
        }

        /// <summary>Pay linked to the colony's wealth, and how far colonists' own money is from it. The share is the player's choice; 0 keeps the fixed rates below.</summary>
        private static float WealthPay(Rect r, FamilyComponent c, float y)
        {
            var reg = c.Registry; var pol = reg.Payroll().Policy; var l = reg.Ledger();
            y = Heading(r, y + 4f, "WFL_Tr_WealthPayTitle".Translate());
            long wealth = TreasuryService.ColonyWealth(); var people = TreasuryService.GapColonists(c); int n = people.Count;
            y = Line(r, y, "WFL_Tr_WealthPayShare".Translate(pol.WealthSharePercent));
            pol.WealthSharePercent = (int)Widgets.HorizontalSlider(new Rect(4f, y, 280f, 20f), pol.WealthSharePercent, 0f, WagePolicy.MaxWealthShare, true, null, null, null, 1f); y += 26f;
            y = Line(r, y, pol.WealthSharePercent <= 0 ? (string)"WFL_Tr_WealthPayOff".Translate() : (string)"WFL_Tr_WealthPayOn".Translate(wealth, pol.PoolPerPayday(wealth)), true);
            long per = WealthGap.PerColonist(wealth, n), held = n == 0 ? 0 : people.Sum(p => (long)l.Balance(Ledger.IdFor(AccountKind.Person, p.thingIDNumber))) / n;
            y = Line(r, y, "WFL_Tr_WealthGapLine".Translate(held, per, per > 0 ? Mathf.RoundToInt(held * 100f / per) : 0), true);
            bool gap = pol.GapMood; Widgets.CheckboxLabeled(new Rect(4f, y, 420f, 24f), "WFL_Tr_GapMood".Translate(), ref gap); pol.GapMood = gap; y += 28f;
            return CommunityPageLayout.Help(r, y, "WFL_Tr_GapMoodNote".Translate());
        }

        private static readonly Dictionary<string, string> rateBuffers = new Dictionary<string, string>();
        private static float RateRow(Rect r, float y, string role, string label, int fallback = -1)
        {
            var pol = FamilyComponent.Current.Registry.Payroll().Policy;
            int rate = pol.Rates.TryGetValue(role, out var v) ? v : (fallback >= 0 ? fallback : WagePolicy.DefaultRate);
            if (!rateBuffers.TryGetValue(role, out var buf)) buf = rate.ToString();
            Widgets.Label(new Rect(4f, y, 300f, 24f), "WFL_Tr_RatePerUnit".Translate(label));
            Widgets.TextFieldNumeric(new Rect(310f, y, 70f, 24f), ref rate, ref buf, 0f, 100000f); rateBuffers[role] = buf; pol.Rates[role] = rate;
            return y + 26f;
        }

        private static float Taxation(Rect r, FamilyComponent c, float y)
        {
            var reg = c.Registry; var l = reg.Ledger(); var tax = reg.Tax(); int today = TreasuryService.Today();
            if (!TaxAuthority.Unlocked(TreasuryService.ActiveCollectors(reg))) return Line(r, y, "WFL_Tr_TaxLocked".Translate(), true);
            y = Line(r, y, "WFL_Tr_Collector".Translate(Names(c, TreasuryRoles.TaxCollector)));
            y = Line(r, y, "WFL_Tr_TaxRate".Translate(tax.RatePercent));
            float v = Widgets.HorizontalSlider(new Rect(4f, y, 240f, 20f), tax.RatePercent, 0f, 40f, true, null, null, null, 1f); tax.RatePercent = (int)v; y += 26f;
            bool auto = tax.AutoRemit; Widgets.CheckboxLabeled(new Rect(4f, y, 360f, 24f), "Collect automatically without visits", ref auto); tax.AutoRemit = auto; y += 28f;
            y = Line(r, y, "WFL_Tr_Legitimacy".Translate(tax.Legitimacy));
            y = Heading(r, y + 6f, "WFL_Tr_Taxation".Translate());
            foreach (var d in l.Debts.Where(x => x.Active && x.Kind == DebtKind.Tax).OrderBy(x => x.DueDay)) y = Line(r, y, "WFL_Tr_TaxLine".Translate(NameOf(c, d.Debtor), d.Outstanding, d.DueDay % PayCalendar.DaysPerYear + 1) + (d.DueDay < today ? "  !" : ""));
            return y;
        }

        private static float Debts(Rect r, FamilyComponent c, float y)
        {
            var l = c.Registry.Ledger();
            foreach (var d in l.Debts.Where(x => x.Active).OrderBy(x => x.DueDay).ToList())
            {
                y = Line(r, y, "WFL_Tr_DebtLine".Translate(NameOf(c, d.Debtor), NameOf(c, d.Creditor), d.Outstanding, d.Original, d.Kind + (d.Status == DebtStatus.Disputed ? ", disputed" : "")));
                if (Widgets.ButtonText(new Rect(r.width - 250f, y - 24f, 80f, 22f), "WFL_Tr_Forgive".Translate())) l.Forgive(d.Id);
                if (Widgets.ButtonText(new Rect(r.width - 166f, y - 24f, 80f, 22f), (d.Status == DebtStatus.Disputed ? "WFL_Tr_Resolve" : "WFL_Tr_Dispute").Translate())) l.Dispute(d.Id, d.Status != DebtStatus.Disputed);
            }
            return y;
        }

        private static float Transactions(Rect r, FamilyComponent c, float y)
        {
            foreach (var t in c.Registry.Ledger().History.Reverse().Take(80)) y = Line(r, y, TxText(c, t) + (t.Tag != null ? "  [" + t.Tag + "]" : ""), t.Kind != TxKind.Misconduct);
            return y;
        }
    }
}

