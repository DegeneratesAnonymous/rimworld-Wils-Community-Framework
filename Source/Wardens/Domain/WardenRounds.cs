using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    /// <summary>One place a round can stop: a room with prisoners in it (or, for an Overseer, a room where slaves sleep).</summary>
    public sealed class RoundStop
    {
        public long Room;
        public int X, Z;
        /// <summary>When this room was last visited on a round (0 if never), so the least recently seen are seen first.</summary>
        public int LastSeen;
        public double DistanceTo(int x, int z) => Math.Sqrt((double)(X - x) * (X - x) + (double)(Z - z) * (Z - z));
    }

    /// <summary>
    /// A Warden's round: an idle Warden walks the cells (an Overseer, the slave quarters) looking in on whoever is there. Gentle by construction, in the same way as Military's duties: only the idle, rested
    /// and not needed elsewhere; capped; never with hostiles about; anything the player orders or a need interrupts it. A round is the way a Warden becomes responsible for a prisoner without any work being ordered.
    /// </summary>
    public static class WardenRounds
    {
        public const int PauseTicks = 200, MaxStops = 6, CooldownTicks = 4000, MaxPerDay = 3, MaxPerLook = 2;
        /// <summary>Experience for a finished round (at least one stop made). The Overseer's rounds count toward their specialty.</summary>
        public const int RoundPoints = 4;
        /// <summary>The most of the wardens who may be on a round at once (at least one).</summary>
        public const float MaxAtOnceShare = .5f;

        public static int MaxOnRound(int wardens) => wardens <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(wardens * MaxAtOnceShare));
        public static bool MayStart(int now, int lastEnd, int roundsToday) => roundsToday < MaxPerDay && (lastEnd <= 0 || now < lastEnd || now - lastEnd >= CooldownTicks);

        /// <summary>
        /// The stops for one round: the rooms seen least recently first (ties by id), at most <see cref="MaxStops"/>, then walked nearest-first from where the warden stands. Deterministic.
        /// </summary>
        public static List<RoundStop> Plan(IEnumerable<RoundStop> stops, int x, int z)
        {
            var chosen = (stops ?? Enumerable.Empty<RoundStop>()).Where(s => s != null).GroupBy(s => s.Room).Select(g => g.OrderBy(s => s.LastSeen).First()).OrderBy(s => s.LastSeen).ThenBy(s => s.Room).Take(MaxStops).ToList();
            var ordered = new List<RoundStop>(); int px = x, pz = z;
            while (chosen.Count > 0) { var next = chosen.OrderBy(s => s.DistanceTo(px, pz)).ThenBy(s => s.Room).First(); chosen.Remove(next); ordered.Add(next); px = next.X; pz = next.Z; }
            return ordered;
        }
    }

    /// <summary>The result of an Overseer's look at slave families.</summary>
    public sealed class FamilyCheck { public int Together, Apart; public List<int> TogetherPeople = new List<int>(), ApartPeople = new List<int>(); }

    public static class SlaveFamilies
    {
        public const string TogetherThought = "WFL_W_SlaveFamilyTogether", ApartThought = "WFL_W_SlaveFamilyApart";

        /// <summary>
        /// For each pair of slave kin: together if they sleep in the same room, apart if in different rooms. A pair where either slave's room is unknown (0) is not counted, and a pair is counted
        /// once however it is listed. Observes only.
        /// </summary>
        public static FamilyCheck Evaluate(IEnumerable<KeyValuePair<int, int>> kinPairs, Func<int, long> roomOf)
        {
            var result = new FamilyCheck(); var seen = new HashSet<long>();
            foreach (var pair in kinPairs ?? Enumerable.Empty<KeyValuePair<int, int>>())
            {
                int a = Math.Min(pair.Key, pair.Value), b = Math.Max(pair.Key, pair.Value); if (a == b || a <= 0 || !seen.Add(((long)a << 32) | (uint)b)) continue;
                long ra = roomOf(a), rb = roomOf(b); if (ra == 0 || rb == 0) continue;
                if (ra == rb) { result.Together++; result.TogetherPeople.Add(a); result.TogetherPeople.Add(b); } else { result.Apart++; result.ApartPeople.Add(a); result.ApartPeople.Add(b); }
            }
            result.TogetherPeople = result.TogetherPeople.Distinct().ToList(); result.ApartPeople = result.ApartPeople.Distinct().ToList(); return result;
        }

        /// <summary>The Overseer's own reaction: pleased if some families are together and none apart, troubled if any are apart, nothing if there were none to see.</summary>
        public static int OverseerEvent(FamilyCheck c) => c.Apart > 0 ? -1 : c.Together > 0 ? 1 : 0;
    }
}
