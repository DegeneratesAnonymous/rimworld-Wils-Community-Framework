using System;
using System.Collections.Generic;

namespace FamilyLife.Domain
{
    /// <summary>What a household's own store should hold: only what the family will actually use. Pure arithmetic, so it is tested without the game.</summary>
    public static class StockLimits
    {
        /// <summary>Days a perishable lasts at a temperature. A rot rate of zero (freezing) means it never spoils.</summary>
        public static float UsableDays(float daysToRotStart, float rotRate) =>
            rotRate <= 0.0001f ? float.PositiveInfinity : Math.Max(0f, daysToRotStart) / rotRate;

        /// <summary>Nutrition of perishable food worth keeping: what the household eats before it spoils, never more than its reserve. Zero when it would spoil within half a day.</summary>
        public static float PerishableFoodCap(int members, float reserveDays, float usableDays) =>
            usableDays < .5f ? 0f : HouseholdPolicy.FoodTarget(members, Math.Min(reserveDays, usableDays));

        /// <summary>Nutrition of food that keeps (dried, packaged, frozen): the household's reserve.</summary>
        public static float DurableFoodCap(int members, float reserveDays) => HouseholdPolicy.FoodTarget(members, reserveDays);

        /// <summary>One dose each, and never fewer than the household's own reserve setting.</summary>
        public static int MedicineCap(int members, int reserve) => Math.Max(Math.Max(0, members), Math.Max(0, reserve));

        /// <summary>One spare piece for each piece a member wears (at most four per person, at least one).</summary>
        public static int SpareApparelCap(IEnumerable<int> wornPiecesPerMember)
        {
            int n = 0; if (wornPiecesPerMember == null) return 0;
            foreach (var worn in wornPiecesPerMember) n += Math.Max(1, Math.Min(4, worn));
            return n;
        }

        /// <summary>One book at a time.</summary>
        public const int BookCap = 1;

        /// <summary>Three doses per person who depends on the drug; nobody else needs any.</summary>
        public static int DrugCap(int dependentMembers) => 3 * Math.Max(0, dependentMembers);

        /// <summary>Fuel for the days of reserve, only for what in the house burns it; nothing when nothing does.</summary>
        public static int FuelCap(IEnumerable<float> consumptionPerDay, float reserveDays)
        {
            float total = 0f; if (consumptionPerDay == null) return 0;
            foreach (var rate in consumptionPerDay) total += Math.Max(0f, rate);
            if (total <= 0f) return 0;
            return Math.Max(1, (int)Math.Ceiling(total * Math.Max(.5f, Math.Min(3f, reserveDays))));
        }

        /// <summary>A store takes more only while it holds less than the cap.</summary>
        public static bool Open(float held, float cap) => cap > 0f && held < cap;
    }
}
