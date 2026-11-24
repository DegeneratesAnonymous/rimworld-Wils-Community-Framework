using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class WealthPayTests
    {
        private static string P(int id) => Ledger.IdFor(AccountKind.Person, id);

        private static (Ledger, PayrollBook) Setup()
        {
            var l = TreasuryLedgerTests.Funded((AccountKind.Community, 0, 1000), (AccountKind.Person, 1, 0), (AccountKind.Person, 2, 0)); var b = new PayrollBook();
            b.Policy.Rates["a"] = 4; b.Policy.Rates["b"] = 12; b.Policy.DailyUnits["a"] = 1; b.Policy.DailyUnits["b"] = 1; b.Policy.PawnDailyCap = 100;
            b.Accrue(1, "a", 1, 3, "k1"); b.Accrue(2, "b", 1, 3, "k2"); return (l, b);
        }

        [Fact]
        public void With_no_share_the_fixed_rates_pay_exactly_as_before()
        {
            var (l, b) = Setup(); b.SettleDue(l, 0, 1000, 800); b.SettleDue(l, 7, 1000, 800);
            Assert.Equal(4, l.Balance(P(1))); Assert.Equal(12, l.Balance(P(2))); Assert.True(l.Conserved);
        }

        [Fact]
        public void A_share_of_the_wealth_sets_the_pool_and_the_work_shares_it_in_proportion_without_exceeding_it()
        {
            var (l, b) = Setup(); b.Policy.WealthSharePercent = 10;
            Assert.Equal(10, b.Policy.PoolPerPayday(800));          // 10% of 800 a year over 8 paydays
            b.SettleDue(l, 0, 1000, 800); var reports = b.SettleDue(l, 7, 1000, 800);
            Assert.Equal(2, l.Balance(P(1))); Assert.Equal(7, l.Balance(P(2)));          // 4 and 12 earned, scaled by 10/16 and rounded down
            Assert.True(reports.Single().TotalDue <= 10); Assert.True(l.Conserved);
        }

        [Fact]
        public void A_rich_colony_pays_more_but_never_more_than_four_times_the_fixed_rate()
        {
            var (l, b) = Setup(); b.Policy.WealthSharePercent = 10;
            b.SettleDue(l, 0, 1000, 40000); b.SettleDue(l, 7, 1000, 40000);
            Assert.Equal(16, l.Balance(P(1))); Assert.Equal(48, l.Balance(P(2)));
        }

        [Fact]
        public void The_share_is_bounded_and_saved_with_the_gap_setting()
        {
            var b = new PayrollBook(); b.Policy.WealthSharePercent = 999; Assert.Equal(WagePolicy.MaxWealthShare * 1000L / 100 / 8, b.Policy.PoolPerPayday(1000));
            b.Policy.WealthSharePercent = 12; b.Policy.GapMood = false;
            var loaded = PayrollBook.Decode(new StateReader(b.Encode())); Assert.Equal(12, loaded.Policy.WealthSharePercent); Assert.False(loaded.Policy.GapMood);
            Assert.True(PayrollBook.Decode(new StateReader(new PayrollBook().Encode())).Policy.GapMood);          // on by default, including in older saves
            Assert.Equal(0, new PayrollBook().Policy.PoolPerPayday(100000));
        }

        [Fact]
        public void Being_far_poorer_than_a_wealthy_colony_is_felt_in_three_steps()
        {
            Assert.Equal(3, WealthGap.Stage(40000, 4, 100));          // 1% of 10,000 each
            Assert.Equal(2, WealthGap.Stage(40000, 4, 400));          // 4%
            Assert.Equal(1, WealthGap.Stage(40000, 4, 800));          // 8%
            Assert.Equal(0, WealthGap.Stage(40000, 4, 2000));         // 20%: no feeling
        }

        [Fact]
        public void A_young_colony_or_a_lone_colonist_is_never_judged()
        {
            Assert.Equal(0, WealthGap.Stage(4000, 4, 0));          // 1,000 each: not wealthy yet
            Assert.Equal(0, WealthGap.Stage(100000, 1, 0));
            Assert.Equal(0, WealthGap.Stage(0, 4, 0));
            Assert.Equal(1500, WealthGap.PerColonist(3000, 2) + 0 * WealthGap.Average(0, 0));
        }
    }
}
