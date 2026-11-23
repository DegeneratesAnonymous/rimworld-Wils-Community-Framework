using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FamilyLife.Framework;

namespace FamilyLife.Domain
{
    /// <summary>One investor's share of one business. Real silver moves from the investor to the business when it opens, and real dividends move back; nothing is created.</summary>
    public sealed class Stake
    {
        public int Id, Investor, Business, Capital, ShareBp, OpenedDay, LastPayoutDay, Received;
        public bool Closed;
    }

    /// <summary>The numbers of the investor tier. All integers, all tunable here, none of it needs the game.</summary>
    public static class InvestmentRules
    {
        public const int MinCapital = 100, MaxPerStake = 500, KeepPersonal = 100, CooldownDays = 10;
        /// <summary>A new business is valued at least this, so a first small stake cannot buy most of it.</summary>
        public const int SeedValue = 200;
        /// <summary>The most of a business that outside investors can hold together, in basis points; the owners keep control.</summary>
        public const int MaxOutsideBp = 4000;
        /// <summary>The share of a business's spare silver (after wages and a reserve) paid out as dividends, every PeriodDays.</summary>
        public const int PayoutPercent = 50, PeriodDays = 5;
        /// <summary>What a business keeps back before it pays anything: a fifth of the outside capital, never under MinReserve.</summary>
        public const int ReservePercent = 20, MinReserve = 50, MinSurplus = 60;

        /// <summary>What the business is worth before a new stake: the silver it holds after what it owes (earlier investors' capital is already in that balance).</summary>
        public static int Value(int available, int owed) => Math.Max(SeedValue, available - owed);
        public static int ShareBp(int capital, int value) => capital <= 0 ? 0 : (int)((long)capital * 10000 / ((long)capital + Math.Max(1, value)));
        public static int Reserve(int outsideCapital) => Math.Max(MinReserve, outsideCapital * ReservePercent / 100);
        public static int Surplus(int available, int owed, int outsideCapital) => Math.Max(0, available - owed - Reserve(outsideCapital));
        public static int Pool(int surplus) => surplus * PayoutPercent / 100;
        public static int Dividend(int pool, int shareBp) => (int)((long)Math.Max(0, pool) * Math.Max(0, shareBp) / 10000);
        public static int Capacity(int heldBp) => Math.Max(0, MaxOutsideBp - Math.Max(0, heldBp));

        /// <summary>The most capital that fits in the room left, given the business's value.</summary>
        public static int MaxCapitalFor(int capacityBp, int value) => capacityBp >= 10000 ? int.MaxValue : (int)((long)capacityBp * Math.Max(1, value) / (10000 - capacityBp));

        /// <summary>What an investor is willing to put into one stake: half of what they hold above their own reserve, up to the cap.</summary>
        public static int Stake(int personalAvailable) => Math.Max(0, Math.Min(MaxPerStake, (personalAvailable - KeepPersonal) / 2));
    }

    public sealed class InvestmentBook : IModuleState
    {
        public const string StateId = "wils.treasury.investments";
        public int NextId = 1;
        public readonly List<Stake> Stakes = new List<Stake>();

        public IEnumerable<Stake> Live => Stakes.Where(s => !s.Closed);
        public int HeldBp(int business) => Live.Where(s => s.Business == business).Sum(s => s.ShareBp);
        public int OutsideCapital(int business) => Live.Where(s => s.Business == business).Sum(s => s.Capital);
        public Stake Latest(int investor) => Stakes.Where(s => s.Investor == investor).OrderByDescending(s => s.OpenedDay).FirstOrDefault();
        public bool Holds(int investor, int business) => Live.Any(s => s.Investor == investor && s.Business == business);

        /// <summary>The business worth backing: the one with the most spare silver that has room for another investor, and that this investor is not already in. Zero when none qualifies.</summary>
        public int Pick(Ledger ledger, int investor)
        {
            int best = 0, bestSurplus = InvestmentRules.MinSurplus - 1;
            foreach (var a in ledger.Accounts.Where(a => a.Kind == AccountKind.Business && !a.Closed).OrderBy(a => a.Owner))
            {
                if (Holds(investor, a.Owner) || InvestmentRules.Capacity(HeldBp(a.Owner)) <= 0) continue;
                int surplus = InvestmentRules.Surplus(ledger.Available(a.Id), ledger.Liabilities(a.Id), OutsideCapital(a.Owner));
                if (surplus > bestSurplus) { best = a.Owner; bestSurplus = surplus; }
            }
            return best;
        }

        /// <summary>Opens a stake. The silver moves from the investor's own account to the business. Returns null when it cannot (too little silver, no room, unknown business).</summary>
        public Stake Open(Ledger ledger, int investor, int business, int silver, int day)
        {
            var biz = ledger.Get(Ledger.IdFor(AccountKind.Business, business));
            if (biz == null || biz.Closed || Holds(investor, business)) return null;
            string person = Ledger.IdFor(AccountKind.Person, investor); ledger.Ensure(AccountKind.Person, investor);
            int value = InvestmentRules.Value(ledger.Available(biz.Id), ledger.Liabilities(biz.Id));
            int room = InvestmentRules.Capacity(HeldBp(business));
            silver = Math.Min(Math.Min(silver, ledger.Available(person)), InvestmentRules.MaxCapitalFor(room, value));
            if (silver < InvestmentRules.MinCapital) return null;
            int share = InvestmentRules.ShareBp(silver, value);
            if (share <= 0) return null;
            var stake = new Stake { Id = NextId, Investor = investor, Business = business, Capital = silver, ShareBp = share, OpenedDay = day, LastPayoutDay = day };
            var result = ledger.Transfer(person, biz.Id, silver, TxKind.Transfer, "invest:" + stake.Id, day, "investment");
            if (result != LedgerResult.Ok) return null;
            NextId++; Stakes.Add(stake);
            return stake;
        }

        /// <summary>Pays one business's investors their share of its spare silver once the period has passed. Returns what each investor received, by investor id.</summary>
        public Dictionary<int, int> PayDividends(Ledger ledger, int business, int day)
        {
            var paid = new Dictionary<int, int>();
            var stakes = Live.Where(s => s.Business == business).ToList();
            if (stakes.Count == 0 || day - stakes.Min(s => s.LastPayoutDay) < InvestmentRules.PeriodDays) return paid;
            string biz = Ledger.IdFor(AccountKind.Business, business); if (ledger.Get(biz) == null || ledger.Get(biz).Closed) return paid;
            int pool = InvestmentRules.Pool(InvestmentRules.Surplus(ledger.Available(biz), ledger.Liabilities(biz), stakes.Sum(s => s.Capital)));
            foreach (var s in stakes)
            {
                s.LastPayoutDay = day;
                int due = Math.Min(InvestmentRules.Dividend(pool, s.ShareBp), ledger.Available(biz));
                if (due <= 0) continue;
                string person = Ledger.IdFor(AccountKind.Person, s.Investor); ledger.Ensure(AccountKind.Person, s.Investor);
                string to = new BusinessBank(ledger).Successor(person);
                if (ledger.Transfer(biz, to, due, TxKind.Transfer, "dividend:" + s.Id + ":" + day, day, "dividend") != LedgerResult.Ok) continue;
                s.Received += due; paid[s.Investor] = (paid.TryGetValue(s.Investor, out var p) ? p : 0) + due;
            }
            return paid;
        }

        /// <summary>The business buys the stake back at the capital put in, if it can spare that without touching wages or its reserve. Dividends already received are kept.</summary>
        public bool Exit(Ledger ledger, Stake stake, int day)
        {
            if (stake == null || stake.Closed) return false;
            string biz = Ledger.IdFor(AccountKind.Business, stake.Business);
            if (ledger.Get(biz) == null || ledger.Get(biz).Closed) { stake.Closed = true; return true; }
            if (ledger.Available(biz) - ledger.Liabilities(biz) - InvestmentRules.MinReserve < stake.Capital) return false;
            string person = Ledger.IdFor(AccountKind.Person, stake.Investor); ledger.Ensure(AccountKind.Person, stake.Investor);
            if (ledger.Transfer(biz, new BusinessBank(ledger).Successor(person), stake.Capital, TxKind.Transfer, "exit:" + stake.Id, day, "stake bought back") != LedgerResult.Ok) return false;
            stake.Closed = true; return true;
        }

        public string Encode()
        {
            var w = new StateWriter();
            w.Int("next", NextId);
            w.Strings("stake", Stakes.Select(s => string.Join("|", s.Id, s.Investor, s.Business, s.Capital, s.ShareBp, s.OpenedDay, s.LastPayoutDay, s.Received, s.Closed ? 1 : 0)).ToList());
            return w.ToString();
        }

        public static InvestmentBook Decode(StateReader r)
        {
            var b = new InvestmentBook { NextId = Math.Max(1, r.Int("next", 1)) };
            foreach (var s in r.Strings("stake"))
            {
                var p = s.Split('|'); if (p.Length < 9) continue;
                b.Stakes.Add(new Stake { Id = I(p[0]), Investor = I(p[1]), Business = I(p[2]), Capital = I(p[3]), ShareBp = I(p[4]), OpenedDay = I(p[5]), LastPayoutDay = I(p[6]), Received = I(p[7]), Closed = I(p[8]) == 1 });
            }
            return b;
        }
        private static int I(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    public sealed class InvestmentFactory : IStateFactory<InvestmentBook>
    {
        public static readonly InvestmentFactory Instance = new InvestmentFactory();
        public int Version => 1;
        public InvestmentBook Create() => new InvestmentBook();
        public InvestmentBook Decode(StateReader reader, int version) => InvestmentBook.Decode(reader);
        public InvestmentBook FromLegacy(StateReader legacy) => new InvestmentBook();
    }

    public static class InvestmentAccess { public static InvestmentBook Investments(this FamilyRegistry r) => r.Shared.Of(InvestmentBook.StateId, InvestmentFactory.Instance); }
}
