using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FamilyLife
{
    /// <summary>The job def Military ships (in its own package's Defs). Resolved by the game when Military's assembly loads.</summary>
    [DefOf]
    public static class MilitaryDefOf
    {
        public static JobDef WFL_MilitaryDuty;
        static MilitaryDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(MilitaryDefOf)); }
    }

    /// <summary>
    /// Starts soldiers on their daily duty. Only serving soldiers who are idle in their Work (or Anything) hours, rested, and under their daily limit are given duty; never while the map has
    /// hostiles, at Red, or beyond the on-duty cap. Colonists who are not soldiers may volunteer to join a group duty, but only in their own free time. Nothing is ever forced on anyone: a duty
    /// is an ordinary job that the game's own needs and any order of the player interrupt.
    /// </summary>
    internal static class MilitaryDutyGame
    {
        private sealed class Session { public DutyKind Kind; public int Leader, Map, Start, Members, Volunteers; public IntVec3 Cell; }
        private static readonly List<Session> sessions = new List<Session>();
        private static readonly Dictionary<int, int> lastEnd = new Dictionary<int, int>();
        private static readonly Dictionary<int, int[]> daily = new Dictionary<int, int[]>();          // pawn -> day, blocks done that day
        private static readonly Dictionary<int, IntVec3> held = new Dictionary<int, IntVec3>();          // the cell each pawn on duty is using
        private static readonly Dictionary<int, int> heldPost = new Dictionary<int, int>();          // the post each lookout or guard is using
        private static readonly Dictionary<int, KeyValuePair<int, bool>> threat = new Dictionary<int, KeyValuePair<int, bool>>();
        private static readonly MilitaryCareerCache careerCache = new MilitaryCareerCache();
        private const int ThreatKeep = 60;

        public static void Reset() { sessions.Clear(); lastEnd.Clear(); daily.Clear(); held.Clear(); heldPost.Clear(); threat.Clear(); }

        private sealed class MilitaryCareerCache { public readonly CareerDefinition Def = MilitaryCareers.Definition(); }

        // ------------------------------------------------------------------ is this a good moment

        /// <summary>Whether hostiles are about on the map, looked at no more than once a second per map however often it is asked.</summary>
        public static bool ThreatNow(Map map)
        {
            if (map == null) return false;
            int now = Find.TickManager.TicksGame;
            if (threat.TryGetValue(map.uniqueID, out var t) && now >= t.Key && now - t.Key < ThreatKeep) return t.Value;
            bool v = MilitaryCombat.HostilesAbout(map); threat[map.uniqueID] = new KeyValuePair<int, bool>(now, v); return v;
        }

        /// <summary>Whether a duty should end now: the system is off, the alert is Red, the pawn has been drafted or has broken, or hostiles have appeared.</summary>
        public static bool ShouldStop(Pawn pawn)
        {
            var c = FamilyComponent.Current; if (c == null || pawn == null) return true;
            var state = c.Registry.Alert();
            return !state.DutiesOn || state.Effective == AlertLevel.Red || pawn.Drafted || pawn.InMentalState || ThreatNow(pawn.Map);
        }

        private static bool OnDuty(Pawn p) => p.CurJobDef == MilitaryDefOf.WFL_MilitaryDuty;

        /// <summary>A serving soldier with nothing to do: awake, able, not hungry or tired, in Work or Anything hours, and idle (not in the middle of a task, an order or a need).</summary>
        private static bool SoldierFree(Pawn p)
        {
            if (p == null || !p.Spawned || p.Dead || p.Downed || p.Drafted || p.InMentalState || p.IsPrisoner || !p.RaceProps.Humanlike || p.DevelopmentalStage.Baby() || !p.Awake()) return false;
            if (p.ageTracker.AgeBiologicalYears < MilitaryDuties.MinTrainingAge || p.WorkTagIsDisabled(WorkTags.Violent)) return false;
            var assignment = p.timetable?.CurrentAssignment;
            if (assignment == TimeAssignmentDefOf.Sleep || assignment == TimeAssignmentDefOf.Joy) return false;
            if (p.mindState?.lastJobTag != JobTag.Idle || p.CurJob?.playerForced == true) return false;
            if (FrameworkServices.NeedsReason(p) != null || HealthAIUtility.ShouldSeekMedicalRest(p)) return false;
            return !(p.needs?.food != null && p.needs.food.CurLevel < .2f) && !(p.needs?.rest != null && p.needs.rest.CurLevel < .15f);
        }

        private static bool Rested(Pawn p, int now)
        {
            int day = now / ActivityTuning.TicksPerDay; int blocks = daily.TryGetValue(p.thingIDNumber, out var d) && d[0] == day ? d[1] : 0;
            return MilitaryDuties.MayStart(now, lastEnd.TryGetValue(p.thingIDNumber, out int e) ? e : 0, blocks);
        }

        // ------------------------------------------------------------------ the roster

        public static void Run(int now)
        {
            var c = FamilyComponent.Current; if (c == null) return;
            var state = c.Registry.Alert(); if (!state.DutiesOn) return;
            var level = state.Effective; if (level == AlertLevel.Red) { sessions.Clear(); return; }
            sessions.RemoveAll(s => now - s.Start > MilitaryDuties.BlockTicks + 600);
            foreach (var map in Find.Maps) if (map.IsPlayerHome && !ThreatNow(map)) RunMap(c, state, map, level, now);
        }

        private static List<IntVec3> YardCells(Map map) => map.zoneManager.AllZones.OfType<Zone_TrainingYard>().SelectMany(z => z.Cells).Where(x => x.Standable(map)).ToList();

        private static void RunMap(FamilyComponent c, AlertState state, Map map, AlertLevel level, int now)
        {
            var serving = map.mapPawns.FreeColonistsSpawned.Where(MilitaryRoles.Serving).OrderBy(p => p.thingIDNumber).ToList(); if (serving.Count == 0) return;
            int onDuty = serving.Count(OnDuty), cap = MilitaryDuties.MaxOnDuty(serving.Count), started = 0;
            var yard = YardCells(map); var posts = c.Registry.Posts().All.Where(p => p.Map == map.uniqueID).ToList();
            foreach (var p in serving)
            {
                if (onDuty >= cap || started >= 2) break;
                if (OnDuty(p) || !SoldierFree(p) || !Rested(p, now)) continue;
                string role = MilitaryRoles.ActiveRole(p); var kind = MilitaryDuties.For(role, level); if (kind == DutyKind.None) continue;
                if (Start(c, map, p, role, kind, yard, posts, serving, now)) { onDuty++; started++; }
            }
            if (state.Volunteers && yard.Count > 0) JoinVolunteers(map, serving, yard, now);
        }

        private static bool Start(FamilyComponent c, Map map, Pawn p, string role, DutyKind kind, List<IntVec3> yard, List<MilitaryPost> posts, List<Pawn> serving, int now)
        {
            switch (kind)
            {
                case DutyKind.Lookout: case DutyKind.GateGuard: return GoToPost(p, kind, posts, now);
                case DutyKind.Patrol: return Patrol(map, p, posts, now);
                case DutyKind.Muster: return Group(map, p, role, kind, MusterCell(map, p, yard, posts), yard, now);
                case DutyKind.Spar: return Spar(map, p, serving, yard, now);
                case DutyKind.FirstAid: return Solo(map, p, kind, FreeCell(map, p, yard, IntVec3.Invalid), now);
                default: return Group(map, p, role, kind, IntVec3.Invalid, yard, now);
            }
        }

        // ------------------------------------------------------------------ places

        private static bool Taken(IntVec3 cell, Pawn except) { foreach (var kv in held) if (kv.Value == cell && kv.Key != except.thingIDNumber) return true; return false; }

        /// <summary>A free cell in the yard, nearest the wanted spot (or the pawn), that nobody else is using.</summary>
        private static IntVec3 FreeCell(Map map, Pawn p, List<IntVec3> yard, IntVec3 near)
        {
            if (yard.Count == 0) return IntVec3.Invalid;
            var from = near.IsValid ? near : p.Position;
            foreach (var cell in yard.OrderBy(x => x.DistanceToSquared(from)))
            {
                if (Taken(cell, p)) continue; var there = cell.GetFirstPawn(map); if (there != null && there != p) continue;
                if (p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) return cell;
            }
            return IntVec3.Invalid;
        }

        private static IntVec3 MusterCell(Map map, Pawn p, List<IntVec3> yard, List<MilitaryPost> posts)
        {
            var rally = posts.Where(x => x.Kind == PostKind.Rally).OrderBy(x => p.Position.DistanceToSquared(new IntVec3(x.X, 0, x.Z))).FirstOrDefault();
            if (rally != null) { var centre = new IntVec3(rally.X, 0, rally.Z); foreach (var cell in GenRadial.RadialCellsAround(centre, 4f, true)) if (cell.InBounds(map) && cell.Standable(map) && !Taken(cell, p) && p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) return cell; }
            return FreeCell(map, p, yard, IntVec3.Invalid);          // no Rally point: muster in the yard
        }

        // ------------------------------------------------------------------ starting a duty

        public static DutyKind KindOf(Job job) => MilitaryDuties.KindOf(job.count);
        public static int LengthOf(Job job) => MilitaryDuties.LengthOf(job.count);
        private static int Remaining(Session s, int now) => Math.Max(600, MilitaryDuties.BlockTicks - (now - s.Start));

        private static Job Make(DutyKind kind, IntVec3 cell, int length = MilitaryDuties.BlockTicks)
        {
            var job = JobMaker.MakeJob(MilitaryDefOf.WFL_MilitaryDuty, cell); job.count = MilitaryDuties.Pack(kind, length); job.locomotionUrgency = LocomotionUrgency.Walk; return job;
        }

        private static void Begin(Pawn p, Job job, IntVec3 cell) { if (cell.IsValid) held[p.thingIDNumber] = cell; p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, tag: JobTag.Misc); }

        private static bool Solo(Map map, Pawn p, DutyKind kind, IntVec3 cell, int now)
        {
            if (!cell.IsValid) return false; Begin(p, Make(kind, cell), cell); return true;
        }

        /// <summary>Joins a running session of the same kind, or starts one if this soldier leads it, or works alone.</summary>
        private static bool Group(Map map, Pawn p, string role, DutyKind kind, IntVec3 want, List<IntVec3> yard, int now)
        {
            var running = sessions.FirstOrDefault(s => s.Kind == kind && s.Map == map.uniqueID && s.Leader != p.thingIDNumber && s.Members < MilitaryDuties.MaxParticipants && now - s.Start < MilitaryDuties.BlockTicks - 600);
            if (running != null)
            {
                var cell = kind == DutyKind.Muster ? NearSession(map, p, running.Cell) : FreeCell(map, p, yard, running.Cell);
                if (!cell.IsValid) return false; running.Members++; Begin(p, Make(kind, cell, Remaining(running, now)), cell); return true;
            }
            var spot = want.IsValid ? want : FreeCell(map, p, yard, IntVec3.Invalid); if (!spot.IsValid) return false;
            if (MilitaryDuties.Leads(role, kind)) sessions.Add(new Session { Kind = kind, Leader = p.thingIDNumber, Map = map.uniqueID, Start = now, Cell = spot });
            Begin(p, Make(kind, spot), spot); return true;
        }

        private static IntVec3 NearSession(Map map, Pawn p, IntVec3 centre)
        {
            foreach (var cell in GenRadial.RadialCellsAround(centre, 4f, true)) if (cell.InBounds(map) && cell.Standable(map) && !Taken(cell, p) && p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) return cell;
            return IntVec3.Invalid;
        }

        private static bool Spar(Map map, Pawn p, List<Pawn> serving, List<IntVec3> yard, int now)
        {
            var mine = FreeCell(map, p, yard, IntVec3.Invalid); if (!mine.IsValid) return false;
            var partner = serving.FirstOrDefault(o => o != p && !OnDuty(o) && SoldierFree(o) && Rested(o, now) && o.skills?.GetSkill(SkillDefOf.Melee) != null && !o.skills.GetSkill(SkillDefOf.Melee).TotallyDisabled);
            if (partner == null) { Begin(p, Make(DutyKind.Spar, mine), mine); return true; }          // practises alone
            held[p.thingIDNumber] = mine; var theirs = FreeCell(map, partner, yard, mine);
            if (!theirs.IsValid) { held.Remove(p.thingIDNumber); Begin(p, Make(DutyKind.Spar, mine), mine); return true; }
            var a = Make(DutyKind.Spar, mine); a.targetB = partner; var b = Make(DutyKind.Spar, theirs); b.targetB = p;
            Begin(p, a, mine); Begin(partner, b, theirs); return true;
        }

        private static bool GoToPost(Pawn p, DutyKind kind, List<MilitaryPost> posts, int now)
        {
            var want = MilitaryDuties.PostFor(kind); if (want == null) return false;
            var post = posts.Where(x => x.Kind == want.Value && !heldPost.ContainsValue(x.Id)).OrderBy(x => p.Position.DistanceToSquared(new IntVec3(x.X, 0, x.Z))).FirstOrDefault(); if (post == null) return false;
            var cell = new IntVec3(post.X, 0, post.Z); if (!cell.Standable(p.Map) || !p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) return false;
            heldPost[p.thingIDNumber] = post.Id; Begin(p, Make(kind, cell), cell); return true;
        }

        private static bool Patrol(Map map, Pawn p, List<MilitaryPost> posts, int now)
        {
            var points = new List<IntVec3>();
            var from = p.Position;
            var pool = posts.Select(x => new IntVec3(x.X, 0, x.Z)).Where(x => x.Standable(map)).ToList();
            while (pool.Count > 0 && points.Count < 4) { var next = pool.OrderBy(x => from.DistanceToSquared(x)).First(); points.Add(next); pool.Remove(next); from = next; }
            if (points.Count < 2)
            {
                var home = map.areaManager.Home; int n = home?.TrueCount ?? 0; if (n < 3) return false;
                var cells = home.ActiveCells.ToList();
                for (int i = 0; i < 3; i++) { var cell = cells.RandomElement(); if (cell.Standable(map) && p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) points.Add(cell); }
                if (points.Count < 2) return false;
            }
            var job = Make(DutyKind.Patrol, points[0]); job.targetQueueA = points.Select(x => new LocalTargetInfo(x)).ToList();
            Begin(p, job, IntVec3.Invalid); return true;
        }

        // ------------------------------------------------------------------ volunteers

        private static bool Willing(Pawn p, HashSet<int> soldierIds)
        {
            if (p.skills != null && (p.skills.GetSkill(SkillDefOf.Melee).passion != Passion.None || p.skills.GetSkill(SkillDefOf.Shooting).passion != Passion.None)) return true;
            try
            {
                if (p.relations == null) return false;
                foreach (var r in p.relations.FamilyByBlood) if (soldierIds.Contains(r.thingIDNumber)) return true;
                var spouse = p.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse); return spouse != null && soldierIds.Contains(spouse.thingIDNumber);
            }
            catch (Exception) { return false; }
        }

        /// <summary>Colonists who are not soldiers and have a moment to spare may join a drill or range practice already under way, if they care to: a passion for fighting, or a soldier in the family.</summary>
        private static void JoinVolunteers(Map map, List<Pawn> serving, List<IntVec3> yard, int now)
        {
            var open = sessions.Where(s => s.Map == map.uniqueID && (s.Kind == DutyKind.Drill || s.Kind == DutyKind.RangePractice) && s.Volunteers < MilitaryDuties.MaxVolunteers && s.Members < MilitaryDuties.MaxParticipants && now - s.Start < MilitaryDuties.BlockTicks - 600).ToList();
            if (open.Count == 0) return;
            var ids = new HashSet<int>(serving.Select(x => x.thingIDNumber));
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (open.Count == 0) return;
                if (ids.Contains(p.thingIDNumber) || OnDuty(p) || !FrameworkServices.Ready(p) || !Rested(p, now)) continue;          // Ready: free time only, never work time
                if (p.ageTracker.AgeBiologicalYears < MilitaryDuties.MinTrainingAge || p.WorkTagIsDisabled(WorkTags.Violent) || !Willing(p, ids)) continue;
                var s = open[0]; var cell = FreeCell(map, p, yard, s.Cell); if (!cell.IsValid) continue;
                s.Volunteers++; s.Members++; if (s.Volunteers >= MilitaryDuties.MaxVolunteers || s.Members >= MilitaryDuties.MaxParticipants) open.RemoveAt(0);
                Begin(p, Make(s.Kind, cell, Remaining(s, now)), cell);
            }
        }

        // ------------------------------------------------------------------ doing it, and what it earns

        public static void Train(Pawn pawn, DutyKind kind)
        {
            string skill = MilitaryDuties.Skill(kind); if (skill == null || pawn.skills == null) return;
            var def = DefDatabase<SkillDef>.GetNamedSilentFail(skill); var sk = def == null ? null : pawn.skills.GetSkill(def);
            if (sk != null && !sk.TotallyDisabled) pawn.skills.Learn(def, MilitaryDuties.SkillXpPerMinute, false);
        }

        /// <summary>Called when a duty ends for any reason. Releases what it held, notes the rest, and credits the block if enough of it was done.</summary>
        public static void Finish(Pawn pawn, Job job, int ticks, bool completed)
        {
            try
            {
                int id = pawn.thingIDNumber, now = FrameworkServices.Now; held.Remove(id); heldPost.Remove(id);
                var kind = KindOf(job); if (!MilitaryDuties.Counts(completed, ticks)) return;
                lastEnd[id] = now; int day = now / ActivityTuning.TicksPerDay;
                if (daily.TryGetValue(id, out var d) && d[0] == day) d[1]++; else daily[id] = new[] { day, 1 };
                var c = FamilyComponent.Current; if (c == null || !MilitaryRoles.Serving(pawn)) return;          // volunteers train their skills only
                Credit(c, pawn, kind, job.loadID, now);
                var session = sessions.FirstOrDefault(s => s.Leader == id);
                string role = MilitaryRoles.ActiveRole(pawn);
                if (kind == DutyKind.Spar && job.targetB.Thing is Pawn partner) MilitaryStories.Together(pawn, partner, job.loadID);
                else if (session != null && session.Members > 0) { var other = pawn.Map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(x => x != pawn && OnDuty(x) && KindOf(x.CurJob) == kind); if (other != null) MilitaryStories.Together(pawn, other, job.loadID); }
                MilitaryStories.Duty(pawn, role, job.loadID);
            }
            catch (Exception ex) { Log.Warning("[Wil's Family Life] A military duty could not be recorded: " + ex.Message); }
        }

        /// <summary>
        /// The experience for a finished block. An assigned specialist earns toward their own specialty. Someone who has earned only a branch practises the specialty their skills
        /// and experience put them nearest to (this is how a specialty is earned before it can be assigned). A Recruit builds toward the branch of their better skill.
        /// </summary>
        private static void Credit(FamilyComponent c, Pawn pawn, DutyKind kind, int jobId, int now)
        {
            string role = MilitaryRoles.ActiveRole(pawn); if (role == null) return;
            var ledger = c.Registry.Careers(); int id = pawn.thingIDNumber; var skill = CareerService.SkillsOf(pawn); string credit;
            if (MilitaryCareers.Specialties.Contains(role)) credit = role;
            else
            {
                var branches = MilitaryCareers.Branches.Where(b => ledger.IsSpecialist(id, MilitaryCareers.Career, b)).ToList();
                credit = branches.Count > 0
                    ? MilitaryDuties.Practising(careerCache.Def, skill, s => ledger.SpecialtyXp(id, MilitaryCareers.Career, s), s => ledger.IsSpecialist(id, MilitaryCareers.Career, s), q => ledger.Holds(id, q), branches)
                    : MilitaryDuties.BranchToward(skill("Melee"), skill("Shooting"));
            }
            int points = MilitaryDuties.Points(role, kind);
            var session = sessions.FirstOrDefault(s => s.Leader == id);
            if (session != null && session.Members == 0 && MilitaryDuties.IsGroup(kind)) points = Math.Max(1, points / 2);          // leading nobody earns half
            CareerService.Report(c, pawn, MilitaryCareers.Career, credit, "mil.duty." + kind, "duty:" + kind + ":" + jobId, points, now);
        }
    }

    /// <summary>The daily duty as a job. It goes to a cell (or around a patrol route), does the duty for a block of time (training the pawn's skill as it goes), and credits what was done when it ends.</summary>
    public class JobDriver_MilitaryDuty : JobDriver
    {
        private int ticks; private bool completed;
        private DutyKind Kind => MilitaryDutyGame.KindOf(job);
        private Pawn Partner => job.targetB.Thing as Pawn;
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
        public override string GetReport() => ("WFL_Military_Duty_" + Kind).Translate();
        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref ticks, "dutyTicks"); Scribe_Values.Look(ref completed, "dutyDone"); }

        private Toil Hold(int length, bool counts)
        {
            int local = 0;
            var toil = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
            toil.initAction = () => { pawn.pather.StopDead(); };
            toil.tickAction = () =>
            {
                ticks++; local++;
                if (ticks % 60 == 0) MilitaryDutyGame.Train(pawn, Kind);
                var partner = Partner; if (Kind == DutyKind.Spar && partner != null && ticks % 30 == 0 && partner.Position.DistanceTo(pawn.Position) <= 4f) pawn.rotationTracker.FaceCell(partner.Position);
                if (Kind == DutyKind.Spar && partner != null && (partner.Dead || partner.Downed || partner.CurJobDef != MilitaryDefOf.WFL_MilitaryDuty) && ticks > 120) { EndJobWith(JobCondition.Incompletable); return; }
                if (local >= length) ReadyForNextToil();
            };
            return toil;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => MilitaryDutyGame.ShouldStop(pawn));
            AddFinishAction(condition => MilitaryDutyGame.Finish(pawn, job, ticks, completed));
            if (Kind == DutyKind.Patrol && job.targetQueueA != null && job.targetQueueA.Count > 0)
            {
                foreach (var waypoint in job.targetQueueA.ToList())
                {
                    yield return Toils_Goto.GotoCell(waypoint.Cell, PathEndMode.OnCell);
                    yield return Hold(240, true);
                }
                yield return new Toil { initAction = () => completed = true, defaultCompleteMode = ToilCompleteMode.Instant };
                yield break;
            }
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            yield return Hold(MilitaryDutyGame.LengthOf(job), true);
            yield return new Toil { initAction = () => completed = true, defaultCompleteMode = ToilCompleteMode.Instant };
        }
    }
}
