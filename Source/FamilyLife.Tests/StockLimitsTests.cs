using System.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class StockLimitsTests
    {
        [Fact] public void Freezing_food_never_spoils() => Assert.True(float.IsPositiveInfinity(StockLimits.UsableDays(4f, 0f)));
        [Fact] public void Warm_rooms_shorten_the_life_of_food() => Assert.Equal(2f, StockLimits.UsableDays(4f, 2f), 3);

        [Fact] public void Perishables_are_capped_by_how_long_they_last()
        {
            // 4 people, 3 days of reserve, but meals last only 1 day: keep one day's worth.
            Assert.Equal(HouseholdPolicy.FoodTarget(4, 1f), StockLimits.PerishableFoodCap(4, 3f, 1f), 3);
            // Cold storage lets the full reserve be kept.
            Assert.Equal(HouseholdPolicy.FoodTarget(4, 3f), StockLimits.PerishableFoodCap(4, 3f, float.PositiveInfinity), 3);
            // Food that would spoil within half a day is not worth keeping.
            Assert.Equal(0f, StockLimits.PerishableFoodCap(4, 3f, .3f));
        }

        [Fact] public void Medicine_is_one_per_person_but_not_below_the_household_reserve()
        {
            Assert.Equal(5, StockLimits.MedicineCap(5, 2));
            Assert.Equal(2, StockLimits.MedicineCap(1, 2));
        }

        [Fact] public void One_spare_piece_per_worn_piece()
        {
            Assert.Equal(2 + 4 + 1, StockLimits.SpareApparelCap(new[] { 2, 7, 0 }));
            Assert.Equal(0, StockLimits.SpareApparelCap(null));
        }

        [Fact] public void Only_one_book_and_drugs_only_for_those_who_need_them()
        {
            Assert.Equal(1, StockLimits.BookCap);
            Assert.Equal(0, StockLimits.DrugCap(0));
            Assert.Equal(6, StockLimits.DrugCap(2));
        }

        [Fact] public void Fuel_is_only_wanted_when_something_burns_it()
        {
            Assert.Equal(0, StockLimits.FuelCap(Enumerable.Empty<float>(), 2f));
            Assert.Equal(6, StockLimits.FuelCap(new[] { 1.5f, 1.5f }, 2f));
        }

        [Fact] public void A_store_takes_more_only_below_its_cap()
        {
            Assert.True(StockLimits.Open(3, 4)); Assert.False(StockLimits.Open(4, 4)); Assert.False(StockLimits.Open(0, 0));
        }
    }
}
