using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class InvestmentTests
    {
        private static Ledger Setup(int investorSilver, int businessSilver)
        {
            var l = new Ledger();
            l.Ensure(AccountKind.Person, 7); l.Ensure(AccountKind.Business, 1);
            if (investorSilver > 0) l.Receive(Ledger.IdFor(AccountKind.Person, 7), investorSilver, TxKind.Income, "seed-p", 1, "test");
            if (businessSilver > 0) l.Receive(Ledger.IdFor(AccountKind.Business, 1), businessSilver, TxKind.Income, "seed-b", 1, "test");
            return l;
        }

        [Fact] public void Opening_a_stake_moves_real_silver_and_conserves_the_ledger()
        {
            var l = Setup(600, 400); var book = new InvestmentBook();
            var s = book.Open(l, 7, 1, 200, 2);
            Assert.NotNull(s);
            Assert.Equal(400, l.Available(Ledger.IdFor(AccountKind.Person, 7)));
            Assert.Equal(600, l.Available(Ledger.IdFor(AccountKind.Business, 1)));
            Assert.Equal(InvestmentRules.ShareBp(200, 400), s.ShareBp);
            Assert.True(l.Conserved);
        }

        [Fact] public void A_stake_below_the_minimum_or_beyond_the_investors_means_is_refused()
        {
            var l = Setup(600, 400); var book = new InvestmentBook();
            Assert.Null(book.Open(l, 7, 1, 50, 2));
            var poor = Setup(60, 400);
            Assert.Null(new InvestmentBook().Open(poor, 7, 1, 300, 2));
        }

        [Fact] public void Outside_investors_can_never_hold_more_than_the_cap()
        {
            var l = Setup(5000, 300); var book = new InvestmentBook();
            book.Open(l, 7, 1, 5000, 2);
            Assert.True(book.HeldBp(1) <= InvestmentRules.MaxOutsideBp);
        }

        [Fact] public void Dividends_come_from_spare_silver_only_after_the_period_and_never_create_money()
        {
            var l = Setup(600, 400); var book = new InvestmentBook();
            var s = book.Open(l, 7, 1, 300, 2);
            l.Receive(Ledger.IdFor(AccountKind.Business, 1), 600, TxKind.Income, "profit", 3, "test");
            Assert.Empty(book.PayDividends(l, 1, 4));                       // too soon
            var paid = book.PayDividends(l, 1, 2 + InvestmentRules.PeriodDays);
            Assert.True(paid[7] > 0);
            Assert.Equal(paid[7], s.Received);
            Assert.True(l.Available(Ledger.IdFor(AccountKind.Business, 1)) >= InvestmentRules.MinReserve);   // the reserve is kept
            Assert.True(l.Conserved);
        }

        [Fact] public void A_business_with_nothing_spare_pays_nothing()
        {
            var l = Setup(600, 40); var book = new InvestmentBook();
            book.Stakes.Add(new Stake { Id = 1, Investor = 7, Business = 1, Capital = 100, ShareBp = 2000, OpenedDay = 2, LastPayoutDay = 2 });
            Assert.Empty(book.PayDividends(l, 1, 2 + InvestmentRules.PeriodDays));
        }

        [Fact] public void An_investor_picks_the_business_with_the_most_spare_silver_and_not_one_they_already_back()
        {
            var l = Setup(600, 900); l.Ensure(AccountKind.Business, 2);
            l.Receive(Ledger.IdFor(AccountKind.Business, 2), 300, TxKind.Income, "seed-b2", 1, "test");
            var book = new InvestmentBook();
            Assert.Equal(1, book.Pick(l, 7));
            book.Open(l, 7, 1, 150, 2);
            Assert.Equal(2, book.Pick(l, 7));
        }

        [Fact] public void A_stake_is_bought_back_only_when_the_business_can_spare_it()
        {
            var l = Setup(600, 300); var book = new InvestmentBook();
            var s = book.Open(l, 7, 1, 200, 2);
            Assert.True(book.Exit(l, s, 20));
            Assert.True(s.Closed); Assert.Equal(0, book.HeldBp(1));
            var poor = Setup(600, 0); var b2 = new InvestmentBook();
            var stake = new Stake { Id = 1, Investor = 7, Business = 1, Capital = 400, ShareBp = 2000 }; b2.Stakes.Add(stake);
            Assert.False(b2.Exit(poor, stake, 20));
        }

        [Fact] public void Stakes_survive_a_save_and_load()
        {
            var l = Setup(600, 400); var book = new InvestmentBook(); book.Open(l, 7, 1, 200, 2);
            var back = InvestmentBook.Decode(new StateReader(book.Encode()));
            Assert.Single(back.Stakes);
            Assert.Equal(book.Stakes[0].Capital, back.Stakes[0].Capital);
            Assert.Equal(book.NextId, back.NextId);
        }

        [Fact] public void The_players_limits_are_respected_and_survive_a_save()
        {
            var l = Setup(5000, 1500); var book = new InvestmentBook { MaxStake = 150, MaxOutsideBp = 1000 };
            var s = book.Open(l, 7, 1, 1000, 2);
            Assert.NotNull(s); Assert.True(s.Capital <= 150); Assert.True(book.HeldBp(1) <= 1000);
            book.ClosedBusinesses.Add(2); book.InvestorsMayInvest = false;
            var back = InvestmentBook.Decode(new StateReader(book.Encode()));
            Assert.Equal(150, back.MaxStake); Assert.Equal(1000, back.MaxOutsideBp); Assert.Contains(2, back.ClosedBusinesses); Assert.False(back.InvestorsMayInvest);
        }

        [Fact] public void A_business_closed_to_investors_is_never_picked_or_opened()
        {
            var l = Setup(600, 900); var book = new InvestmentBook(); book.ClosedBusinesses.Add(1);
            Assert.Equal(0, book.Pick(l, 7)); Assert.Null(book.Open(l, 7, 1, 200, 2));
        }
    }
}
