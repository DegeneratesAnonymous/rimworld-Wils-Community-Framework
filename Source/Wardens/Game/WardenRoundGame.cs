using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using Verse;
using Verse.AI;

namespace FamilyLife
{
    [DefOf]
    public static class WardenDefOf
    {
        public static JobDef WFL_WardenRound;
        static WardenDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(WardenDefOf)); }
    }

    /// <summary>
    /// Sends idle Wardens on their rounds: a walk past the cells (an Overseer also looks in on the slave quarters), pausing at each. A round marks the prisoners seen as that warden's responsibility, earns experience,
    /// and lets an Overseer note whether slave families sleep together. It is an ordinary job, started only for the idle and rested, never with hostiles about, and any need or order of the player interrupts it.
    /// Nothing and no one is ever moved, locked or changed.
    /// </summary>
    internal static class WardenRoundGame
    {
        private static readonly Dictionary<long, int> seen = new Dictionary<long, int>();          // room -> tick last looked in on
        private static readonly Dictionary<int, int> lastEnd = new Dictionary<int, int>();
        private static readonly Dictionary<int, int[]> daily = new Dictionary<int, int[]>();
        public static void Reset() { seen.Clear(); lastEnd.Clear(); daily.Clear(); }

        private static bool HostilesAbout(Map map) { try { return GenHostility.AnyHostileActiveThreatToPlayer(map); } catch (Exception) { return false; } }
        internal static bool OnRound(Pawn p) => p.CurJobDef == WardenDefOf.WFL_WardenRound;

        public static bool ShouldStop(Pawn p)
        {
            var c = FamilyComponent.Current; return c == null || !c.Registry.Wardens().RoundsOn || p.Drafted || p.InMentalState || HostilesAbout(p.Map);
        }

        private static bool Free(Pawn p)
        {
            if (p == null || !p.Spawned || p.Dead || p.Downed || p.Drafted || p.InMentalState || p.IsPrisoner || p.IsSlave || !p.RaceProps.Humanlike || p.DevelopmentalStage.Baby() || !p.Awake()) return false;
            var assignment = p.timetable?.CurrentAssignment;
            if (assignment == TimeAssignmentDefOf.Sleep || assignment == TimeAssignmentDefOf.Joy) return false;
            if (p.mindState?.lastJobTag != JobTag.Idle || p.CurJob?.playerForced == true) return false;
            if (FrameworkServices.NeedsReason(p) != null || HealthAIUtility.ShouldSeekMedicalRest(p)) return false;
            return !(p.needs?.food != null && p.needs.food.CurLevel < .2f) && !(p.needs?.rest != null && p.needs.rest.CurLevel < .15f);
        }

        private static long Key(Map map, Room room) => ((long)map.uniqueID << 32) | (uint)room.ID;
        private static Room RoomOf(Pawn p) => (p.ownership?.OwnedBed?.GetRoom()) ?? p.GetRoom();
        private static IntVec3 StandIn(Room room, IntVec3 near) => room.Cells.Where(x => x.Standable(room.Map)).OrderBy(x => x.DistanceToSquared(near)).FirstOrDefault();

        private static List<RoundStop> Stops(Map map, bool includeSlaves)
        {
            var found = new Dictionary<long, RoundStop>();
            void Add(Pawn p)
            {
                var room = RoomOf(p); if (room == null || room.PsychologicallyOutdoors) return; long key = Key(map, room); if (found.ContainsKey(key)) return;
                var cell = StandIn(room, p.Position); if (!cell.IsValid) return;
                found[key] = new RoundStop { Room = key, X = cell.x, Z = cell.z, LastSeen = seen.TryGetValue(key, out int t) ? t : 0 };
            }
            foreach (var prisoner in map.mapPawns.PrisonersOfColony) if (prisoner.Spawned) Add(prisoner);
            if (includeSlaves) foreach (var slave in map.mapPawns.SlavesOfColonySpawned) Add(slave);
            return found.Values.ToList();
        }

        public static void Run(int now)
        {
            var c = FamilyComponent.Current; if (c == null || !c.Registry.Wardens().RoundsOn) return;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || HostilesAbout(map)) continue;
                var wardens = map.mapPawns.FreeColonistsSpawned.Where(p => WardenStories.ActiveRole(p) != null).OrderBy(p => p.thingIDNumber).ToList(); if (wardens.Count == 0) continue;
                int cap = WardenRounds.MaxOnRound(wardens.Count), on = wardens.Count(OnRound), started = 0;
                foreach (var p in wardens)
                {
                    if (on >= cap || started >= 1) break;
                    if (OnRound(p) || !Free(p) || !Rested(p, now)) continue;
                    bool overseer = WardenStories.ActiveRole(p) == "Overseer"; var stops = WardenRounds.Plan(Stops(map, overseer), p.Position.x, p.Position.z); if (stops.Count == 0) continue;
                    var job = JobMaker.MakeJob(WardenDefOf.WFL_WardenRound); job.targetQueueA = new List<LocalTargetInfo>(); job.locomotionUrgency = LocomotionUrgency.Walk;
                    foreach (var s in stops) job.targetQueueA.Add(new IntVec3(s.X, 0, s.Z));
                    job.count = stops.Count; p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, tag: JobTag.Misc); on++; started++;
                }
            }
        }

        private static bool Rested(Pawn p, int now)
        {
            int day = now / ActivityTuning.TicksPerDay; int rounds = daily.TryGetValue(p.thingIDNumber, out var d) && d[0] == day ? d[1] : 0;
            return WardenRounds.MayStart(now, lastEnd.TryGetValue(p.thingIDNumber, out int e) ? e : 0, rounds);
        }

        /// <summary>The pawn has reached a stop and looked around: whoever is in the room is now theirs to answer for.</summary>
        public static void Looked(Pawn warden, IntVec3 cell)
        {
            var c = FamilyComponent.Current; var room = cell.GetRoom(warden.Map); if (c == null || room == null) return; int now = FrameworkServices.Now; seen[Key(warden.Map, room)] = now;
            foreach (var prisoner in warden.Map.mapPawns.PrisonersOfColony) if (prisoner.Spawned && RoomOf(prisoner) == room) c.Registry.Wardens().Assign(prisoner.thingIDNumber, warden.thingIDNumber, now);
        }

        /// <summary>The Overseer's check: slave kin who sleep in the same room are together; in different rooms, apart. Observes only.</summary>
        private static void CheckFamilies(FamilyComponent c, Pawn warden)
        {
            var slaves = warden.Map.mapPawns.SlavesOfColonySpawned.ToList(); if (slaves.Count < 2) return;
            var byId = slaves.ToDictionary(s => s.thingIDNumber); var pairs = new List<KeyValuePair<int, int>>();
            foreach (var a in slaves) foreach (var b in slaves) if (a.thingIDNumber < b.thingIDNumber && WardenTreatmentGame.Kin(a, b)) pairs.Add(new KeyValuePair<int, int>(a.thingIDNumber, b.thingIDNumber));
            var check = SlaveFamilies.Evaluate(pairs, id => { var s = byId[id]; var r = RoomOf(s); return r == null ? 0 : Key(warden.Map, r); });
            if (check.Together + check.Apart == 0) return;
            c.Registry.Wardens().Families(warden.thingIDNumber, check.Together, check.Apart);
            foreach (int id in check.TogetherPeople) WardenTreatmentGame.Give(byId[id], SlaveFamilies.TogetherThought, null, 0);
            foreach (int id in check.ApartPeople) WardenTreatmentGame.Give(byId[id], SlaveFamilies.ApartThought, null, 0);
        }

        public static void Finish(Pawn pawn, Job job, int visited)
        {
            try
            {
                var c = FamilyComponent.Current; int now = FrameworkServices.Now, id = pawn.thingIDNumber; if (c == null || visited <= 0) return;
                lastEnd[id] = now; int day = now / ActivityTuning.TicksPerDay;
                if (daily.TryGetValue(id, out var d) && d[0] == day) d[1]++; else daily[id] = new[] { day, 1 };
                string role = WardenStories.ActiveRole(pawn);
                CareerService.Report(c, pawn, WardenCareers.Career, role == "Overseer" ? "Overseer" : null, "ward.round", "round:" + job.loadID, WardenRounds.RoundPoints, now);
                if (role == "Overseer") CheckFamilies(c, pawn);
            }
            catch (Exception e) { Log.Warning("[Wil's Role Modules: Wardens] A round could not be recorded: " + e.Message); }
        }
    }

    /// <summary>A round as a job: walk to each stop in turn, pause to look around, and credit what was seen when it ends.</summary>
    public class JobDriver_WardenRound : JobDriver
    {
        private int visited;
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
        public override string GetReport() => "WFL_Wardens_Round".Translate();
        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref visited, "roundVisited"); }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => WardenRoundGame.ShouldStop(pawn));
            AddFinishAction(condition => WardenRoundGame.Finish(pawn, job, visited));
            foreach (var stop in (job.targetQueueA ?? new List<LocalTargetInfo>()).ToList())
            {
                var cell = stop.Cell;
                yield return Toils_Goto.GotoCell(cell, PathEndMode.OnCell);
                int waited = 0;
                var look = new Toil { defaultCompleteMode = ToilCompleteMode.Never, initAction = () => pawn.pather.StopDead() };
                look.tickAction = () => { if (++waited >= WardenRounds.PauseTicks) ReadyForNextToil(); };
                yield return look;
                yield return new Toil { initAction = () => { visited++; WardenRoundGame.Looked(pawn, cell); }, defaultCompleteMode = ToilCompleteMode.Instant };
            }
        }
    }
}
