using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    /// <summary>A weapon in an Armory, as the game side reads it.</summary>
    public sealed class WeaponFacts
    {
        public int Id;
        public bool Ranged;
        /// <summary>The weapon's quality as a number (0 awful to 6 legendary), or -1 for a weapon with no quality.</summary>
        public int Quality = -1;
        public double Value;
    }

    /// <summary>
    /// Arming the unarmed. At Yellow and Red a serving soldier who has no suitable weapon takes the best suited one from an Armory and keeps it. Suitable means: a Soldier (melee) holds a melee weapon, a
    /// Ranger a ranged one, and a Mixed soldier (Support, a Recruit) anything. Nobody who already holds a suitable weapon is touched.
    /// </summary>
    public static class ArmoryRules
    {
        /// <summary>Whether what a soldier holds (null: nothing; otherwise whether it is a ranged weapon) suits their type.</summary>
        public static bool Suits(PostKind soldier, bool? heldRanged)
        {
            if (heldRanged == null) return false;
            switch (soldier)
            {
                case PostKind.Melee: return !heldRanged.Value;
                case PostKind.Ranged: return heldRanged.Value;
                default: return true;
            }
        }

        public static bool NeedsWeapon(PostKind soldier, bool? heldRanged) => !Suits(soldier, heldRanged);

        /// <summary>Whether a soldier of this type may take this weapon: a Soldier a melee one, a Ranger a ranged one, anyone else any.</summary>
        public static bool Fits(PostKind soldier, WeaponFacts w) => soldier == PostKind.Melee ? !w.Ranged : soldier == PostKind.Ranged ? w.Ranged : true;

        /// <summary>The weapons a soldier could take, best first: highest quality (a weapon with no quality ranks below any that has one), then highest value, then lowest id.</summary>
        public static List<WeaponFacts> Rank(PostKind soldier, IEnumerable<WeaponFacts> weapons)
            => (weapons ?? Enumerable.Empty<WeaponFacts>()).Where(w => w != null && Fits(soldier, w)).OrderByDescending(w => w.Quality).ThenByDescending(w => w.Value).ThenBy(w => w.Id).ToList();

        public static WeaponFacts Best(PostKind soldier, IEnumerable<WeaponFacts> weapons) => Rank(soldier, weapons).FirstOrDefault();

        /// <summary>How long a soldier is left alone after an attempt to arm them, so a weapon that cannot be fetched is not tried every look (about two game hours).</summary>
        public const int RetryTicks = 5000;
        public static bool MayTry(int now, int lastTry) => lastTry <= 0 || now < lastTry || now - lastTry >= RetryTicks;

        /// <summary>The most soldiers sent to the Armory in one look, so a colony of soldiers does not all walk there at once.</summary>
        public const int PerLook = 2;
    }
}
