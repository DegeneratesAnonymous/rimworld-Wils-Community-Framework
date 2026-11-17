using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class ModCompatTests
    {
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

    }
}
