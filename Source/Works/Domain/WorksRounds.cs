using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Framework;

namespace FamilyLife.Domain
{
    /// <summary>One damaged building a Builder might repair, or one dug-out room a Miner might look in on.</summary>
    public sealed class WorkStop
    {
        public long Id;
        public int X, Z;
        /// <summary>Hit points as a fraction of the most (1 is undamaged). For rooms, when it was last visited (0 if never) in LastSeen.</summary>
        public float Fraction = 1f;
        public int LastSeen;
        public double DistanceTo(int x, int z) => Math.Sqrt((double)(X - x) * (X - x) + (double)(Z - z) * (Z - z));
    }

    /// <summary>
    /// Maintenance rounds, in the same gentle form as Military duties and Warden rounds: only the idle and rested, capped, never with hostiles about, ended by any need or order. A Builder's round is one
    /// of the game's own repair jobs on the most damaged building; a Miner's is a walk through the dug-out rooms, looking at supports and roofs. Nothing is changed by a Miner's round. Owner: Works.
    /// </summary>
    public static class WorksRounds
    {
        public const int PauseTicks = 200, MaxStops = 4, CooldownTicks = 4000, MaxPerDay = 3;
        /// <summary>A building is worth repairing once it has lost a quarter of its hit points.</summary>
        public const float RepairBelow = .75f;
        public const float MaxAtOnceShare = .5f;

        public static int MaxOnRound(int workers) => workers <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(workers * MaxAtOnceShare));
        public static bool MayStart(int now, int lastEnd, int roundsToday) => roundsToday < MaxPerDay && (lastEnd <= 0 || now < lastEnd || now - lastEnd >= CooldownTicks);

        /// <summary>The building to repair: the most damaged one below the threshold, the nearest if equally damaged, never one that is whole or unknown. Null if nothing needs it.</summary>
        public static WorkStop ChooseRepair(IEnumerable<WorkStop> candidates, int x, int z)
        {
            return (candidates ?? Enumerable.Empty<WorkStop>()).Where(c => c != null && c.Fraction > 0f && c.Fraction < RepairBelow).OrderBy(c => c.Fraction).ThenBy(c => c.DistanceTo(x, z)).ThenBy(c => c.Id).FirstOrDefault();
        }

        /// <summary>The rooms for one walk: those seen least recently first, at most <see cref="MaxStops"/>, each once, then walked nearest-first from where the worker stands. Deterministic.</summary>
        public static List<WorkStop> Plan(IEnumerable<WorkStop> stops, int x, int z)
        {
            var chosen = (stops ?? Enumerable.Empty<WorkStop>()).Where(s => s != null).GroupBy(s => s.Id).Select(g => g.OrderBy(s => s.LastSeen).First()).OrderBy(s => s.LastSeen).ThenBy(s => s.Id).Take(MaxStops).ToList();
            var ordered = new List<WorkStop>(); int px = x, pz = z;
            while (chosen.Count > 0) { var next = chosen.OrderBy(s => s.DistanceTo(px, pz)).ThenBy(s => s.Id).First(); chosen.Remove(next); ordered.Add(next); px = next.X; pz = next.Z; }
            return ordered;
        }
    }

    /// <summary>What the colony remembers for Works (saved as <c>wils.works.record</c>): whether workers make their rounds on their own, and for each worker when they were last hurt on the job and when their kin last felt relieved. Bounded.</summary>
    public sealed class WorksLedger : IModuleState
    {
        public const string StateId = "wils.works.record";
        public const int MaxWorkers = 200;
        public string Id => StateId;
        /// <summary>On by default (holding a Works role is the consent); the player can turn it off.</summary>
        public bool RoundsOn = true;
        private readonly Dictionary<int, int> harm = new Dictionary<int, int>(), relief = new Dictionary<int, int>();

        public int LastHarm(int worker) => harm.TryGetValue(worker, out int t) ? t : 0;
        public int LastRelief(int worker) => relief.TryGetValue(worker, out int t) ? t : 0;
        public void Harmed(int worker, int now) { if (worker > 0 && (harm.ContainsKey(worker) || harm.Count < MaxWorkers)) harm[worker] = Math.Max(1, now); }
        public void Relieved(int worker, int now) { if (worker > 0 && (relief.ContainsKey(worker) || relief.Count < MaxWorkers)) relief[worker] = Math.Max(1, now); }
        public void Prune(Func<int, bool> alive) { foreach (int id in harm.Keys.Where(k => !alive(k)).ToList()) harm.Remove(id); foreach (int id in relief.Keys.Where(k => !alive(k)).ToList()) relief.Remove(id); }

        public string Encode()
        {
            var h = harm.Keys.OrderBy(x => x).ToList(); var r = relief.Keys.OrderBy(x => x).ToList();
            return new StateWriter().Bool("norounds", !RoundsOn).Ints("h", h).Ints("ht", h.Select(i => harm[i]).ToList()).Ints("r", r).Ints("rt", r.Select(i => relief[i]).ToList()).ToString();
        }

        public static WorksLedger Decode(StateReader rd)
        {
            var l = new WorksLedger { RoundsOn = !rd.Bool("norounds") };
            var h = rd.Ints("h"); var ht = rd.Ints("ht"); for (int i = 0; i < h.Count && i < ht.Count && l.harm.Count < MaxWorkers; i++) if (h[i] > 0 && ht[i] > 0) l.harm[h[i]] = ht[i];
            var r = rd.Ints("r"); var rt = rd.Ints("rt"); for (int i = 0; i < r.Count && i < rt.Count && l.relief.Count < MaxWorkers; i++) if (r[i] > 0 && rt[i] > 0) l.relief[r[i]] = rt[i];
            return l;
        }
    }

    public sealed class WorksLedgerFactory : IStateFactory<WorksLedger>
    {
        public static readonly WorksLedgerFactory Instance = new WorksLedgerFactory();
        public int Version => 1;
        public WorksLedger Create() => new WorksLedger();
        public WorksLedger Decode(StateReader reader, int version) => WorksLedger.Decode(reader);
        public WorksLedger FromLegacy(StateReader legacy) => Create();
    }

    public static class WorksStateAccess
    {
        public static WorksLedger Works(this FamilyRegistry r) => r.Shared.Of(WorksLedger.StateId, WorksLedgerFactory.Instance);
    }
}
