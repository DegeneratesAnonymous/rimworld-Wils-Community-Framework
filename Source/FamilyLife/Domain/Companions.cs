using System;
using System.Collections.Generic;

namespace FamilyLife.Domain
{
    /// <summary>
    /// Who is working alongside whom on a joint duty: a leader (the one whose duty it is) and one companion from another module (a medic on a patrol, a soldier on a prisoner escort). A pair expires on its own,
    /// a person is in at most one pair, and nothing is saved. Pure.
    /// </summary>
    public sealed class CompanionBook
    {
        private sealed class Pair { public int Companion, Until; }
        private readonly Dictionary<int, Pair> byLeader = new Dictionary<int, Pair>();

        private bool Live(int leader, int now, out Pair pair)
        {
            if (byLeader.TryGetValue(leader, out pair)) { if (now < pair.Until) return true; byLeader.Remove(leader); }
            pair = null; return false;
        }

        public bool InPair(int person, int now) => Live(person, now, out _) || LeaderOf(person, now) != 0;

        /// <summary>Pairs a leader with a companion for <paramref name="ticks"/>. Refused when either is already in a pair, or they are the same person.</summary>
        public bool TryPair(int leader, int companion, int now, int ticks)
        {
            if (leader == 0 || companion == 0 || leader == companion || ticks <= 0 || InPair(leader, now) || InPair(companion, now)) return false;
            byLeader[leader] = new Pair { Companion = companion, Until = now + ticks }; return true;
        }

        public int CompanionOf(int leader, int now) => Live(leader, now, out var p) ? p.Companion : 0;

        public int LeaderOf(int companion, int now)
        {
            int found = 0; List<int> expired = null;
            foreach (var kv in byLeader)
            {
                if (now >= kv.Value.Until) { (expired ?? (expired = new List<int>())).Add(kv.Key); continue; }
                if (kv.Value.Companion == companion) found = kv.Key;
            }
            if (expired != null) foreach (var k in expired) byLeader.Remove(k);
            return found;
        }

        public void End(int leader) => byLeader.Remove(leader);
        public void Clear() => byLeader.Clear();
    }

    public static class TeamBonus
    {
        /// <summary>Points for a duty done with a companion: the base plus the bonus share (at least one extra point when there is a bonus). A zero or negative bonus changes nothing.</summary>
        public static int Apply(int points, float bonus) => points <= 0 || bonus <= 0f ? points : Math.Max(points + 1, (int)Math.Round(points * (1f + Math.Min(bonus, 1f))));
    }
}
