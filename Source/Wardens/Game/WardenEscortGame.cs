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
    /// <summary>
    /// A Warden leads a calm prisoner to the Interview Room (for persuasion) or the Visiting Room (when a relative is free to come), waits there, and the prisoner walks back to their cell. Everyone involved has an
    /// ordinary job that the game's needs and any order of the player end at once, and the prisoner always goes home afterwards. Only willing, calm prisoners; at most once a day each; never with hostiles about;
    /// and only along a path that stays inside enclosed rooms. No door, lock, faction, fate or need is ever touched.
    /// </summary>
    internal static class WardenEscortGame
    {
        private sealed class Escort { public int Warden, Prisoner, Visitor, Start, Map; public IntVec3 Cell, Home; public bool Visiting, Mode; }
        private static readonly Dictionary<int, Escort> running = new Dictionary<int, Escort>();          // by warden
        private static readonly Dictionary<int, int> taken = new Dictionary<int, int>();          // prisoner -> day last taken
        private static readonly Dictionary<int, int[]> daily = new Dictionary<int, int[]>();          // warden -> day, escorts that day
        private static readonly Dictionary<int, int> lastEnd = new Dictionary<int, int>();
        public static void Reset() { running.Clear(); taken.Clear(); daily.Clear(); lastEnd.Clear(); }

        public const int LeaderLead = 0, LeaderVisit = 1, PrisonerFollow = 2, Visitor = 3;

        private static int Day(int now) => now / ActivityTuning.TicksPerDay;

        private static bool Persuading(Pawn prisoner)
        {
            string mode = prisoner.guest?.ExclusiveInteractionMode?.defName; return mode == "AttemptRecruit" || mode == "ReduceResistance" || mode == "Convert";
        }

        private static EscortFacts FactsOf(Pawn p, int now)
        {
            var needs = p.needs;
            return new EscortFacts
            {
                Awake = p.Awake(), Downed = p.Downed, MentalState = p.InMentalState, Drafted = p.Drafted, Breaking = PrisonBreakUtility.IsPrisonBreaking(p), NeedsTending = HealthAIUtility.ShouldBeTendedNowByPlayer(p),
                Hungry = needs?.food != null && needs.food.CurLevelPercentage < .3f, Tired = needs?.rest != null && needs.rest.CurLevelPercentage < .3f, Mood = needs?.mood?.CurLevelPercentage ?? .5f,
                DoneToday = taken.TryGetValue(p.thingIDNumber, out int d) && d == Day(now)
            };
        }

        /// <summary>The whole walk stays under a roof inside enclosed rooms: every cell on the way is in a room that is not outdoors and does not touch the map edge.</summary>
        private static bool StaysIndoors(Pawn p, IntVec3 to)
        {
            var path = p.Map.pathFinder.FindPathNow(p.Position, to, p, null, PathEndMode.OnCell);
            try
            {
                if (path == null || !path.Found || path.NodesReversed.Count > 80) return false;
                foreach (var cell in path.NodesReversed)
                {
                    var room = cell.GetRoom(p.Map); if (room == null || room.PsychologicallyOutdoors || room.TouchesMapEdge) return false;
                }
                return true;
            }
            finally { path?.ReleaseToPool(); }
        }

        public static void Run(int now)
        {
            var c = FamilyComponent.Current; if (c == null || !c.Registry.Wardens().RoundsOn) return;
            running.Where(kv => now - kv.Value.Start > WardenEscorts.LimitTicks + 600).Select(kv => kv.Key).ToList().ForEach(k => running.Remove(k));
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || WardenRoundGame.HostilesAbout(map)) continue;
                var wardens = map.mapPawns.FreeColonistsSpawned.Where(p => WardenStories.ActiveRole(p) != null).OrderBy(p => p.thingIDNumber).ToList(); if (wardens.Count == 0) continue;
                if (running.Values.Count(e => e.Map == map.uniqueID) >= WardenEscorts.MaxOut(wardens.Count)) continue;
                IntVec3 interview = IntVec3.Invalid, visiting = IntVec3.Invalid;
                foreach (var room in map.regionGrid.AllRooms)
                {
                    var kind = WardenRoomGame.KindOf(room); if (kind != WardenRoom.InterviewRoom && kind != WardenRoom.VisitingRoom) continue;
                    var cell = WardenRoundGame.StandIn(room, room.Cells.FirstOrDefault()); if (!cell.IsValid) continue;
                    if (kind == WardenRoom.InterviewRoom && !interview.IsValid) interview = cell; else if (kind == WardenRoom.VisitingRoom && !visiting.IsValid) visiting = cell;
                }
                if (!interview.IsValid && !visiting.IsValid) continue;
                foreach (var warden in wardens)
                {
                    if (running.ContainsKey(warden.thingIDNumber) || !WardenRoundGame.Free(warden)) continue;
                    int day = Day(now), done = daily.TryGetValue(warden.thingIDNumber, out var d) && d[0] == day ? d[1] : 0;
                    if (!WardenEscorts.MayEscort(done) || (lastEnd.TryGetValue(warden.thingIDNumber, out int e) && now >= e && now - e < WardenRounds.CooldownTicks / 2)) continue;
                    if (TryStart(map, warden, interview, visiting, wardens, now)) break;          // one escort begun per look per map
                }
            }
        }

        private static bool TryStart(Map map, Pawn warden, IntVec3 interview, IntVec3 visiting, List<Pawn> wardens, int now)
        {
            foreach (var prisoner in map.mapPawns.PrisonersOfColony.Where(p => p.Spawned).OrderBy(p => taken.TryGetValue(p.thingIDNumber, out int t) ? t : 0).ThenBy(p => p.thingIDNumber))
            {
                if (running.Values.Any(e => e.Prisoner == prisoner.thingIDNumber) || !WardenEscorts.Willing(FactsOf(prisoner, now))) continue;
                var kin = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p != warden && p != prisoner && WardenRoundGame.Free(p) && WardenTreatmentGame.Kin(p, prisoner) && !running.Values.Any(e => e.Visitor == p.thingIDNumber));
                var kind = WardenEscorts.Choose(kin != null, Persuading(prisoner), interview.IsValid, visiting.IsValid); if (kind == WardenRoom.None) continue;
                var dest = kind == WardenRoom.VisitingRoom ? visiting : interview; var home = prisoner.Position;
                if (!warden.CanReach(dest, PathEndMode.OnCell, Danger.None) || !prisoner.CanReach(dest, PathEndMode.OnCell, Danger.None) || !StaysIndoors(prisoner, dest)) continue;
                if (kind == WardenRoom.VisitingRoom && !kin.CanReach(dest, PathEndMode.OnCell, Danger.None)) continue;
                var escort = new Escort { Warden = warden.thingIDNumber, Prisoner = prisoner.thingIDNumber, Visitor = kind == WardenRoom.VisitingRoom ? kin.thingIDNumber : 0, Start = now, Map = map.uniqueID, Cell = dest, Home = home, Visiting = kind == WardenRoom.VisitingRoom, Mode = true };
                running[escort.Warden] = escort; taken[escort.Prisoner] = Day(now);
                Begin(warden, escort, escort.Visiting ? LeaderVisit : LeaderLead, prisoner, warden, dest);
                Begin(prisoner, escort, PrisonerFollow, prisoner, warden, dest);
                FrameworkServices.Say(warden, prisoner, "WFL_Escort");
                if (escort.Visiting) Begin(kin, escort, Visitor, prisoner, warden, dest);
                FrameworkServices.AttachCompanion(FamilyComponent.Current, warden, "wils.military.military", WardenCareers.Module, 3000);          // a soldier escorts the escort
                return true;
            }
            return false;
        }

        private static void Begin(Pawn who, Escort e, int role, Pawn prisoner, Pawn warden, IntVec3 dest)
        {
            var job = JobMaker.MakeJob(WardenDefOf.WFL_WardenEscort, dest, prisoner, warden); job.count = role; job.locomotionUrgency = LocomotionUrgency.Walk; job.expiryInterval = -1;
            if (role == PrisonerFollow) job.targetQueueA = new List<LocalTargetInfo> { e.Home };          // the way home
            who.jobs.StartJob(job, JobCondition.InterruptForced, null, false, tag: JobTag.Misc);
        }

        /// <summary>Whether the escort this pawn belongs to should be called off: hostiles, a draft, a mental state, a need or the time.</summary>
        public static bool Abort(Pawn pawn, Job job, int started)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || pawn.Drafted || pawn.InMentalState || WardenRoundGame.HostilesAbout(pawn.Map)) return true;
            if (WardenEscorts.TimedOut(started, FrameworkServices.Now)) return true;
            if (job.count == PrisonerFollow) return !WardenEscorts.Willing(FactsOf(pawn, 0));
            if (job.targetC.Thing is Pawn leader && (leader.Dead || leader.Downed || leader.Drafted || leader.InMentalState)) return true;
            return FrameworkServices.NeedsReason(pawn) != null;
        }

        /// <summary>Whether the leader of this escort is still on it. A follower whose leader has finished or been interrupted goes home.</summary>
        public static bool LeaderGone(Job job) => !(job.targetC.Thing is Pawn leader) || leader.CurJobDef != WardenDefOf.WFL_WardenEscort || leader.CurJob?.count > LeaderVisit;

        public static bool Together(Pawn warden, Pawn prisoner, IntVec3 cell) => warden != null && prisoner != null && warden.Position.DistanceTo(cell) <= 5f && prisoner.Position.DistanceTo(cell) <= 5f;

        /// <summary>The leader finished the time in the room: the interview or visit did its work.</summary>
        public static void Held(Pawn warden, Job job)
        {
            try
            {
                var c = FamilyComponent.Current; var prisoner = job.targetB.Thing as Pawn; if (c == null || prisoner == null || prisoner.Dead) return;
                int now = FrameworkServices.Now; bool visit = job.count == LeaderVisit; running.TryGetValue(warden.thingIDNumber, out var escort);
                if (visit)
                {
                    FrameworkServices.GainMemory(prisoner, WardenEscorts.VisitThought, null);
                    if (escort != null && escort.Visitor != 0 && c.TryGetPawn(escort.Visitor) is Pawn visitor) FrameworkServices.GainMemory(visitor, WardenEscorts.VisitorThought, prisoner);
                }
                else
                {
                    FrameworkServices.GainMemory(prisoner, WardenEscorts.InterviewThought, warden);
                    FrameworkServices.Say(warden, prisoner, "WFL_Interview");
                    var guest = prisoner.guest; if (guest != null && (guest.ExclusiveInteractionMode?.defName ?? "") != "Convert") guest.resistance = Math.Max(0f, guest.resistance - WardenEscorts.Relief(guest.resistance));
                    if (prisoner.ideo != null && (guest?.ExclusiveInteractionMode?.defName ?? "") == "Convert") prisoner.ideo.OffsetCertainty(-WardenEscorts.CertaintyDrop(prisoner.ideo.Certainty));
                }
                WardenTreatmentGame.Escorted(prisoner, warden);
                int escortPoints = WardenEscorts.ReturnPoints;
                if (FrameworkServices.CompanionOf(warden) is Pawn guard)
                {
                    escortPoints = FrameworkServices.TeamPoints(escortPoints);          // an escort with a soldier earns more, and the soldier practises guard work
                    CareerService.Report(c, guard, "wils.military.military", null, "mil.escort", "escort|" + job.loadID, 4, now);
                    FrameworkServices.EndCompanion(warden, WardenCareers.Module);
                }
                CareerService.Report(c, warden, WardenCareers.Career, null, "ward.escort", "escort:" + job.loadID, escortPoints, now);
            }
            catch (Exception ex) { Log.Warning("[Wil's Role Modules: Wardens] An escort could not be recorded: " + ex.Message); }
        }

        public static void Ended(Pawn warden, Job job)
        {
            if (job.count > LeaderVisit) return; int now = FrameworkServices.Now, id = warden.thingIDNumber;
            running.Remove(id); lastEnd[id] = now; int day = Day(now);
            if (daily.TryGetValue(id, out var d) && d[0] == day) d[1]++; else daily[id] = new[] { day, 1 };
        }
    }

    /// <summary>One job for the three people on an escort. The leader (Warden) walks to the room and waits; the prisoner and any visitor walk there and wait until the leader is done, then the prisoner walks home. Anything amiss ends the wait and the prisoner goes home.</summary>
    public class JobDriver_WardenEscort : JobDriver
    {
        private int started, stayed; private bool held;
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
        public override string GetReport() => "WFL_Wardens_Escort".Translate();
        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref started, "escortStart"); Scribe_Values.Look(ref stayed, "escortStayed"); Scribe_Values.Look(ref held, "escortHeld"); }
        private bool Leader => job.count <= WardenEscortGame.LeaderVisit;
        private bool Aborted => WardenEscortGame.Abort(pawn, job, started);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            started = FrameworkServices.Now;
            AddFinishAction(condition => { if (Leader) { if (held) WardenEscortGame.Held(pawn, job); WardenEscortGame.Ended(pawn, job); } });
            if (Leader) this.FailOn(() => Aborted);
            Toil home = null;
            if (job.count == WardenEscortGame.PrisonerFollow) home = Toils_Goto.GotoCell(job.targetQueueA != null && job.targetQueueA.Count > 0 ? job.targetQueueA[0].Cell : pawn.Position, PathEndMode.OnCell);
            var go = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell); if (home != null) go.tickAction = () => { if (Aborted) JumpToToil(home); };
            yield return go;
            var wait = new Toil { defaultCompleteMode = ToilCompleteMode.Never, initAction = () => pawn.pather.StopDead() };
            wait.tickAction = () =>
            {
                stayed++;
                if (Leader)
                {
                    var prisoner = job.targetB.Thing as Pawn;
                    if (stayed >= WardenEscorts.StayTicks && WardenEscortGame.Together(pawn, prisoner, job.targetA.Cell)) { held = true; ReadyForNextToil(); }
                }
                else if (WardenEscortGame.LeaderGone(job) || Aborted) { if (home != null) ReadyForNextToil(); else EndJobWith(JobCondition.Succeeded); }
            };
            yield return wait;
            if (home != null) yield return home;
        }
    }
}
