using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// What dangerous and shared work does to a worker's family: kin worry when a worker is hurt on the job, grieve one lost to the work, are relieved after a safe stretch; a Builder's kin feel it when they
    /// finish a place the kin live in; relatives on the same site share a memory and a bond. Memories only: it never changes anyone's health, work or home.
    /// </summary>
    internal static class WorksFamilyGame
    {
        private static readonly Dictionary<int, Pawn> working = new Dictionary<int, Pawn>();          // workers seen on a Works job at the last look
        private static readonly HashSet<int> workedSince = new HashSet<int>();
        private static readonly Dictionary<string, int> last = new Dictionary<string, int>();
        private const int Interval = 2500;
        public static void Reset() { working.Clear(); workedSince.Clear(); last.Clear(); }

        internal static bool IsWorksJob(Pawn p)
        {
            string job = p?.CurJobDef?.defName; if (job == null) return false;
            return job == "FinishFrame" || job == "Repair" || job == "FixBrokenDownBuilding" || job == "SmoothFloor" || job == "SmoothWall" || job == "Mine" || job == "OperateDeepDrill" || job == "WFL_WorksRound";
        }

        private static bool Kin(Pawn a, Pawn b)
        {
            if (a?.relations == null || b == null || a == b) return false;
            try
            {
                foreach (var relative in a.relations.FamilyByBlood) if (relative == b) return true;
                return a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, b) || a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b) || a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b);
            }
            catch (Exception) { return false; }
        }

        private static IEnumerable<Pawn> Colonists() => Find.Maps.Where(m => m.IsPlayerHome).SelectMany(m => m.mapPawns.FreeColonistsSpawned);

        /// <summary>Gives a thought, at most once in three days per person, thought and other person.</summary>
        private static void Give(Pawn subject, string thought, Pawn other)
        {
            if (subject == null || thought == null) return;
            int day = FrameworkServices.EconomyDay; string key = subject.thingIDNumber + ":" + thought + ":" + (other?.thingIDNumber ?? 0);
            if (last.TryGetValue(key, out int before) && day - before < 3) return;
            last[key] = day; FrameworkServices.GainMemory(subject, thought, other);
        }

        private static float NewInjury(Pawn p)
        {
            float total = 0f; var hediffs = p.health?.hediffSet?.hediffs; if (hediffs == null) return 0f;
            foreach (var h in hediffs) if (h is Hediff_Injury && h.ageTicks < Interval + 100) total += h.Severity;
            return total;
        }

        /// <summary>Every 2500 ticks: who was hurt or lost on the job, and whose kin are due some relief.</summary>
        public static void Run(int now)
        {
            var c = FamilyComponent.Current; if (c == null) return; var ledger = c.Registry.Works(); var seenNow = new Dictionary<int, Pawn>();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue; bool hostiles = SafeHostiles(map);
                foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(x => WorksStories.ActiveRole(x) != null).ToList())
                {
                    int id = p.thingIDNumber; bool onWork = IsWorksJob(p), wasWorking = working.ContainsKey(id);
                    if (onWork) { seenNow[id] = p; workedSince.Add(id); }
                    if (ledger.LastHarm(id) == 0 && ledger.LastRelief(id) == 0) { ledger.Relieved(id, now); continue; }          // a baseline, so the first safe stretch is counted from here
                    if ((onWork || wasWorking) && WorksFamily.Worrying(NewInjury(p)))
                    {
                        ledger.Harmed(id, now); foreach (var kin in Colonists().Where(k => Kin(k, p))) Give(kin, WorksFamily.Worried, p);
                    }
                    else if (WorksFamily.ReliefDue(now, ledger.LastHarm(id), ledger.LastRelief(id), workedSince.Contains(id)) && Colonists().Any(k => Kin(k, p)))
                    {
                        ledger.Relieved(id, now); workedSince.Remove(id); foreach (var kin in Colonists().Where(k => Kin(k, p))) Give(kin, WorksFamily.Relieved, p);
                    }
                }
                foreach (var kv in working.Where(w => w.Value != null && w.Value.Dead && w.Value.Map == map).ToList())
                {
                    if (WorksFamily.LostToTheWork(true, hostiles)) foreach (var kin in Colonists().Where(k => Kin(k, kv.Value))) Give(kin, WorksFamily.Lost, kv.Value);
                    working.Remove(kv.Key);
                }
            }
            foreach (var kv in seenNow) working[kv.Key] = kv.Value;
            foreach (int id in working.Where(w => w.Value == null || w.Value.Discarded || !seenNow.ContainsKey(w.Key) && !w.Value.Dead).Select(w => w.Key).ToList()) working.Remove(id);
        }

        private static bool SafeHostiles(Map map) { try { return GenHostility.AnyHostileActiveThreatToPlayer(map); } catch (Exception) { return false; } }

        /// <summary>A Builder finished a frame: kin who live in that room are told it was built for them.</summary>
        public static void Built(Pawn builder, IntVec3 cell)
        {
            var map = builder.Map; if (map == null || !cell.InBounds(map)) return; var room = cell.GetRoom(map); if (room == null || room.PsychologicallyOutdoors) return;
            foreach (var kin in Colonists().Where(k => k != builder && Kin(k, builder)))
            { var bed = kin.ownership?.OwnedBed; if (bed != null && bed.GetRoom() == room) Give(kin, WorksFamily.BuiltForUs, builder); }
        }

        /// <summary>A worker finished a job: any kin working close by on the same site shares the memory and a small bond.</summary>
        public static void Crew(Pawn worker)
        {
            if (worker.Map == null) return;
            foreach (var kin in worker.Map.mapPawns.FreeColonistsSpawned.Where(k => k != worker && Kin(k, worker) && WorksStories.ActiveRole(k) != null))
            {
                if (!WorksFamily.Alongside(true, IsWorksJob(kin), kin.Position.DistanceTo(worker.Position))) continue;
                Give(worker, WorksFamily.CrewMood, null); Give(kin, WorksFamily.CrewMood, null); Give(worker, WorksFamily.CrewBond, kin); Give(kin, WorksFamily.CrewBond, worker);
            }
        }

        /// <summary>After a load: forget workers who are gone.</summary>
        public static void Reconcile(FamilyComponent c) => c.Registry.Works().Prune(id => c.TryGetPawn(id) != null);
    }
}
