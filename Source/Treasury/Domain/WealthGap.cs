using System;

namespace FamilyLife.Domain
{
    /// <summary>
    /// How far a colonist's own money is from the colony's wealth. The colony's wealth per colonist is the yardstick: someone who holds a small fraction of it while the colony is rich feels left out. A young colony
    /// (little wealth per colonist) is never judged this way, and the effect is small and only a mood, never a change to any balance.
    /// </summary>
    public static class WealthGap
    {
        /// <summary>Wealth per colonist below which nobody is compared (an early colony is poor for everyone).</summary>
        public const long MinPerColonist = 1500;
        /// <summary>Holdings under these shares of the wealth per colonist are stage 3 (under 2%), 2 (under 5%) and 1 (under 10%).</summary>
        public const double Huge = .02, Large = .05, Some = .10;

        public static long PerColonist(long colonyWealth, int colonists) => colonists <= 0 || colonyWealth <= 0 ? 0 : colonyWealth / colonists;

        /// <summary>0 for no feeling, 1 to 3 for a growing one. Fewer than two colonists, or a colony that is not yet wealthy, gives 0.</summary>
        public static int Stage(long colonyWealth, int colonists, long personalHoldings)
        {
            if (colonists < 2) return 0;
            long per = PerColonist(colonyWealth, colonists); if (per < MinPerColonist) return 0;
            double share = Math.Max(0, personalHoldings) / (double)per;
            return share < Huge ? 3 : share < Large ? 2 : share < Some ? 1 : 0;
        }

        /// <summary>The average of what colonists hold, for showing next to the wealth per colonist.</summary>
        public static long Average(long total, int colonists) => colonists <= 0 ? 0 : total / colonists;
    }
}
