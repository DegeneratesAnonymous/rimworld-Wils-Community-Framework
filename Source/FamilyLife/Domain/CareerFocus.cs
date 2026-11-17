using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    /// <summary>One bar for "how far along is this person in this career": the next step they are working toward, how far they are, and whether they have nothing left to earn.</summary>
    public sealed class CareerFocus
    {
        public float Share; public string Target; public bool Done;

        /// <summary>
        /// For a role that names a specialty, that specialty's step; otherwise the next step the person has not earned (the qualified-professional step first, then the specialties). A step is as far along as its
        /// slower half (general experience or specialty experience). <paramref name="rows"/> are the career's progress rows for the person.
        /// </summary>
        public static CareerFocus Of(IList<CareerProgressRow> rows, string roleSpecialty)
        {
            if (rows == null || rows.Count == 0) return new CareerFocus { Share = 0f, Done = false };
            CareerProgressRow row = !string.IsNullOrEmpty(roleSpecialty) ? rows.FirstOrDefault(r => r.Key == roleSpecialty) : null;
            if (row == null) row = rows.Where(r => !r.Earned).OrderBy(r => r.Key == null ? 0 : 1).ThenByDescending(r => r.Available ? 1 : 0).FirstOrDefault();
            if (row == null) return new CareerFocus { Share = 1f, Done = true };
            if (row.Earned) return new CareerFocus { Share = 1f, Done = true, Target = row.Key };
            float share = Math.Min(row.GeneralShare, row.SpecialtyNeeded > 0 ? row.SpecialtyShare : 1f);
            return new CareerFocus { Share = Math.Max(0f, Math.Min(1f, share)), Target = row.Key };
        }
    }

    /// <summary>Recent experience gains, so the player can tell who is learning right now. Session-only; recorded when experience is credited.</summary>
    public sealed class GainTracker
    {
        public const int WindowTicks = 90000;          // about a day and a half
        private readonly Dictionary<string, int[]> byKey = new Dictionary<string, int[]>();
        private static string Key(int person, string career) => person + "|" + career;

        public void Record(int person, string career, int amount, int now)
        {
            if (amount <= 0) return;
            if (byKey.TryGetValue(Key(person, career), out var g) && now - g[0] < WindowTicks) { g[0] = now; g[1] += amount; }
            else byKey[Key(person, career)] = new[] { now, amount };
        }

        /// <summary>The experience gained in the current streak, or 0 when nothing came in recently.</summary>
        public int Recent(int person, string career, int now) => byKey.TryGetValue(Key(person, career), out var g) && now - g[0] < WindowTicks ? g[1] : 0;
        public void Clear() => byKey.Clear();
    }
}
