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
    public static class WorksDefOf
    {
        public static JobDef WFL_WorksRound;
        static WorksDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(WorksDefOf)); }
    }

    /// <summary>
    /// Sends idle workers on maintenance rounds. A Builder is given the game's own repair job on the most damaged building; a Miner walks the dug-out rooms (an ordinary job that changes nothing). Only the idle and
    /// rested, a few times a day, never with hostiles about, and any need or order of the player ends it. The repair earns its experience through the normal job hook; a Miner's round earns its own.
    /// </summary>
    internal static class WorksRoundGame
    {
        private static readonly Dictionary<long, int> seen = new Dictionary<long, int>();
        private static readonly Dictionary<int, int> lastEnd = new Dictionary<int, int>();
        private static readonly Dictionary<int, int[]> daily = new Dictionary<int, int[]>();
        public static void Reset() { seen.Clear(); lastEnd.Clear(); daily.Clear(); }

        private static bool HostilesAbout(Map map) { try { return GenHostility.AnyHostileActiveThreatToPlayer(map); } catch (Exception) { return false; } }
        internal static bool OnRound(Pawn p) => p.CurJobDef == WorksDefOf.WFL_WorksRound || p.CurJobDef == JobDefOf.Repair && p.CurJob?.playerForced == false;

        public static bool ShouldStop(Pawn p)
        {
            var c = FamilyComponent.Current; return c == null || !c.Registry.Works().RoundsOn || p.Drafted || p.InMentalState || HostilesAbout(p.Map);
        }

        private static bool Free(Pawn p) => FrameworkServices.FreeForDuty(p, WorksCareers.Module);

        private static bool Rested(Pawn p, int now)
        {
            int day = now / ActivityTuning.TicksPerDay; int rounds = daily.TryGetValue(p.thingIDNumber, out var d) && d[0] == day ? d[1] : 0;
            return WorksRounds.MayStart(now, lastEnd.TryGetValue(p.thingIDNumber, out int e) ? e : 0, rounds);
        }

        private static long Key(Map map, Room room) => ((long)map.uniqueID << 32) | (uint)room.ID;

        public static void Run(int now)
        {
            var c = FamilyComponent.Current; if (c == null || !c.Registry.Works().RoundsOn) return;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || HostilesAbout(map)) continue;
                var workers = map.mapPawns.FreeColonistsSpawned.Where(p => WorksStories.ActiveRole(p) != null).OrderBy(p => p.thingIDNumber).ToList(); if (workers.Count == 0) continue;
                int cap = WorksRounds.MaxOnRound(workers.Count), on = workers.Count(OnRound);
                foreach (var p in workers)
                {
                    if (on >= cap) break;
                    if (OnRound(p) || !Free(p) || !Rested(p, now)) continue;
                    string role = WorksStories.ActiveRole(p); bool builder = role == WorksCareers.BuilderLabourer || role == WorksCareers.BuilderJourneyman || WorksCareers.CareerOf(role) == WorksCareers.Builder;
                    if (FrameworkServices.ColonyAlert == ColonyAlertLevel.Red && !builder) continue;          // at Red only repairs go on; routine walks wait
                    if (!FrameworkServices.TryClaimDuty(p, WorksCareers.Module)) continue;
                    if (builder ? StartRepair(map, p) : StartWalk(map, p)) { on++; break; }
                    FrameworkServices.ReleaseDuty(p, WorksCareers.Module);          // one round begun per look per map
                }
            }
        }

        private static bool StartRepair(Map map, Pawn p)
        {
            var stops = new List<WorkStop>(); var byId = new Dictionary<long, Thing>();
            foreach (var b in map.listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer))
            {
                if (b == null || !b.Spawned || b.HitPoints <= 0 || b.MaxHitPoints <= 0) continue; long id = b.thingIDNumber; byId[id] = b;
                stops.Add(new WorkStop { Id = id, X = b.Position.x, Z = b.Position.z, Fraction = (float)b.HitPoints / b.MaxHitPoints });
            }
            var pick = WorksRounds.ChooseRepair(stops, p.Position.x, p.Position.z); if (pick == null) return false;
            var building = byId[pick.Id]; if (!p.CanReserveAndReach(building, PathEndMode.Touch, Danger.Some)) return false;
            p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Repair, building), JobCondition.InterruptForced, null, false, tag: JobTag.Misc); Counted(p); return true;
        }

        private static bool StartWalk(Map map, Pawn p)
        {
            var stops = new List<WorkStop>(); var cells = new Dictionary<long, IntVec3>();
            foreach (var room in map.regionGrid.AllRooms)
            {
                if (WorksRoomGame.KindOf(room) != WorksPlace.Mine) continue; long key = Key(map, room);
                var cell = room.Cells.Where(x => x.Standable(map)).OrderBy(x => x.DistanceToSquared(p.Position)).FirstOrDefault(); if (!cell.IsValid) continue;
                cells[key] = cell; stops.Add(new WorkStop { Id = key, X = cell.x, Z = cell.z, LastSeen = seen.TryGetValue(key, out int t) ? t : 0 });
            }
            var plan = WorksRounds.Plan(stops, p.Position.x, p.Position.z).Where(s => p.CanReach(cells[s.Id], PathEndMode.OnCell, Danger.Some)).ToList(); if (plan.Count == 0) return false;
            var job = JobMaker.MakeJob(WorksDefOf.WFL_WorksRound); job.targetQueueA = new List<LocalTargetInfo>(); job.locomotionUrgency = LocomotionUrgency.Walk;
            foreach (var s in plan) job.targetQueueA.Add(cells[s.Id]);
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, tag: JobTag.Misc); return true;
        }

        public static void Looked(Pawn pawn, IntVec3 cell) { var room = cell.GetRoom(pawn.Map); if (room != null) seen[Key(pawn.Map, room)] = FrameworkServices.Now; }

        public static void Finish(Pawn pawn, Job job, int visited)
        {
            try
            {
                var c = FamilyComponent.Current; int now = FrameworkServices.Now, id = pawn.thingIDNumber; if (c == null || visited <= 0) return;
                lastEnd[id] = now; int day = now / ActivityTuning.TicksPerDay;
                if (daily.TryGetValue(id, out var d) && d[0] == day) d[1]++; else daily[id] = new[] { day, 1 };
                CareerService.Report(c, pawn, WorksCareers.Miner, null, "works.round", "round:" + job.loadID, WorksCareers.RoundPoints, now);
            }
            catch (Exception e) { Log.Warning("[Wil's Role Modules: Works] A round could not be recorded: " + e.Message); }
        }

        /// <summary>A Builder's repair was begun: count it as the day's round so they are not sent again at once.</summary>
        private static void Counted(Pawn pawn)
        {
            int now = FrameworkServices.Now, id = pawn.thingIDNumber; lastEnd[id] = now; int day = now / ActivityTuning.TicksPerDay;
            if (daily.TryGetValue(id, out var d) && d[0] == day) d[1]++; else daily[id] = new[] { day, 1 };
        }
    }

    /// <summary>A Miner's round as a job: walk to each dug-out room in turn, pause to look at the supports and the roof, and credit the walk when it ends. Nothing is changed.</summary>
    public class JobDriver_WorksRound : JobDriver
    {
        private int visited;
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
        public override string GetReport() => "WFL_Works_Round".Translate();
        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref visited, "roundVisited"); }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => WorksRoundGame.ShouldStop(pawn));
            AddFinishAction(condition => WorksRoundGame.Finish(pawn, job, visited));
            foreach (var stop in (job.targetQueueA ?? new List<LocalTargetInfo>()).ToList())
            {
                var cell = stop.Cell;
                yield return Toils_Goto.GotoCell(cell, PathEndMode.OnCell);
                int waited = 0;
                var look = new Toil { defaultCompleteMode = ToilCompleteMode.Never, initAction = () => pawn.pather.StopDead() };
                look.tickAction = () => { if (++waited >= WorksRounds.PauseTicks) ReadyForNextToil(); };
                yield return look;
                yield return new Toil { initAction = () => { visited++; WorksRoundGame.Looked(pawn, cell); }, defaultCompleteMode = ToilCompleteMode.Instant };
            }
        }
    }
}
