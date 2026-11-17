using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FamilyLife
{
    /// <summary>
    /// The small, deliberate surface the framework offers to optional modules for things that need the live game: the clock, who is free to be called away, moods, a conversation,
    /// a meeting, the developer-test switch. Each member forwards to framework internals that stay internal. Nothing here is a place to add convenience for a single module: if only
    /// one module needs something, it belongs in that module. Owner: the framework.
    /// </summary>
    public static class FrameworkServices
    {
        // ---- time and presence

        /// <summary>The current game tick.</summary>
        public static int Now => Activities.Now;
        /// <summary>The shared absolute economic day, using the primary colony's longitude and the game's calendar. All business/public paydays use this same clock.</summary>
        public static int EconomyDay
        {
            get
            {
                var map=Find.AnyPlayerHomeMap??Find.CurrentMap;
                float longitude=map!=null?Find.WorldGrid.LongLatOf(map.Tile).x:0f;
                long absolute=Find.TickManager.TicksAbs;
                return GenDate.Year(absolute,longitude)*60+GenDate.DayOfYear(absolute,longitude);
            }
        }

        /// <summary>
        /// A module reports something the colony needs ("no classroom"). Shown on the Today page with where it is; the player is told once when it first appears. Post it each time the need is seen; resolve it
        /// (<see cref="ResolveNeed"/>) when it is met. <paramref name="key"/> must start with the module id and is unique per need; <paramref name="labelKey"/> is a translation key in the module's own language file.
        /// </summary>
        public static void PostNeed(string moduleId, string key, string labelKey, string detail = "", Map map = null, IntVec3 cell = default(IntVec3))
        {
            var c = FamilyComponent.Current; if (c == null || string.IsNullOrEmpty(moduleId) || string.IsNullOrEmpty(key) || !key.StartsWith(moduleId + ".", StringComparison.Ordinal)) return;
            bool isNew = c.Registry.Needs().Post(moduleId, key, labelKey, detail ?? "", Now, map?.uniqueID ?? -1, cell.x, cell.z);
            if (isNew) Messages.Message("WFL_Need_Posted".Translate(labelKey.Translate()), MessageTypeDefOf.SilentInput, false);
        }

        /// <summary>The need is met (or gone). Only the module that posted it closes it.</summary>
        public static void ResolveNeed(string moduleId, string key) { var c = FamilyComponent.Current; if (c != null) c.Registry.Needs().Resolve(moduleId, key); }

        private static readonly Dictionary<long, int> saidAt = new Dictionary<long, int>();
        /// <summary>
        /// Plays one of the community's everyday interactions between two pawns (<paramref name="defName"/> is a WFL_ interaction, e.g. WFL_Lesson). It goes through the game's own interaction tracker when they are close, so it is
        /// logged and shows as a speech bubble and other social mods see it; otherwise it is logged directly. At most once per pair per half hour, and never an error.
        /// </summary>
        public static void Say(Pawn from, Pawn to, string defName)
        {
            try
            {
                if (from == null || to == null || from == to || from.Dead || to.Dead || from.interactions == null || !from.RaceProps.Humanlike) return;
                var def = DefDatabase<InteractionDef>.GetNamedSilentFail(defName); if (def == null) return;
                long key = ((long)from.thingIDNumber << 32) ^ (uint)to.thingIDNumber ^ (uint)defName.GetHashCode();
                if (saidAt.TryGetValue(key, out int last) && Now >= last && Now - last < 1250) return; saidAt[key] = Now;
                if (saidAt.Count > 400) saidAt.Clear();
                if (from.Spawned && to.Spawned && from.Map == to.Map && from.Position.InHorDistOf(to.Position, 8f) && from.interactions.TryInteractWith(to, def)) return;
                Find.PlayLog.Add(new PlayLogEntry_Interaction(def, from, to, null));
            }
            catch (Exception ex) { Log.WarningOnce("[Wil's Family Life] An interaction could not be played: " + ex.Message, 7710213); }
        }

        private static readonly CompanionBook companions = new CompanionBook();
        /// <summary>
        /// Joint duty: asks a free colleague holding a role in another career (by career id) to come along with <paramref name="leader"/> for a while. The colleague is claimed for <paramref name="claimModule"/>, follows the
        /// leader, and is returned (or null when nobody suitable is free, or the player has turned team duties off). The leader's module then gives both a bonus when the duty finishes.
        /// </summary>
        public static Pawn AttachCompanion(FamilyComponent c, Pawn leader, string companionCareer, string claimModule, int ticks, float maxDistance = 40f)
        {
            if (c == null || leader == null || !leader.Spawned || leader.Map == null || (FamilyLifeMod.Settings?.teamBonus ?? .25f) <= 0f || companions.InPair(leader.thingIDNumber, Now)) return null;
            var book = c.Registry.Assignments(); Pawn best = null; float bestD = float.MaxValue;
            foreach (var role in CareerService.Professions.All.Where(r => r.Career == companionCareer && FamilyFramework.Catalog.IsEnabled(r.ModuleId)))
                foreach (int id in book.Holders(role.Id).Distinct())
                {
                    var p = c.TryGetPawn(id); if (p == null || p == leader || p.Map != leader.Map || !FreeForDuty(p, claimModule) || companions.InPair(id, Now)) continue;
                    float d = p.Position.DistanceTo(leader.Position); if (d > maxDistance || d >= bestD || !p.CanReach(leader, PathEndMode.Touch, Danger.Deadly)) continue;
                    best = p; bestD = d;
                }
            if (best == null || !TryClaimDuty(best, claimModule, ticks) || !companions.TryPair(leader.thingIDNumber, best.thingIDNumber, Now, ticks)) return null;
            var job = JobMaker.MakeJob(JobDefOf.FollowClose, leader); job.followRadius = 5f; job.expiryInterval = ticks; job.checkOverrideOnExpire = true;
            best.jobs.StartJob(job, JobCondition.InterruptForced, null, false, tag: JobTag.Misc);
            Say(leader, best, "WFL_TeamUp");
            return best;
        }

        /// <summary>The colleague working with this leader on a joint duty right now, or null.</summary>
        public static Pawn CompanionOf(Pawn leader) { if (leader == null) return null; int id = companions.CompanionOf(leader.thingIDNumber, Now); return id == 0 ? null : FamilyComponent.Current?.TryGetPawn(id); }
        /// <summary>The leader this colleague is working with, or null.</summary>
        public static Pawn LeaderOf(Pawn companion) { if (companion == null) return null; int id = companions.LeaderOf(companion.thingIDNumber, Now); return id == 0 ? null : FamilyComponent.Current?.TryGetPawn(id); }
        /// <summary>The joint duty is finished: frees the colleague (and their claim) at once.</summary>
        public static void EndCompanion(Pawn leader, string claimModule)
        {
            var other = CompanionOf(leader); companions.End(leader.thingIDNumber);
            if (other != null) { ReleaseDuty(other, claimModule); if (other.CurJobDef == JobDefOf.FollowClose) other.jobs.EndCurrentJob(JobCondition.Succeeded); }
        }
        /// <summary>Points for a duty done as a team: the player's team bonus added on.</summary>
        public static int TeamPoints(int points) => TeamBonus.Apply(points, FamilyLifeMod.Settings?.teamBonus ?? .25f);

        private static readonly Dictionary<string, KeyValuePair<string, Action>> needActions = new Dictionary<string, KeyValuePair<string, Action>>();
        /// <summary>What the player can do about a need: a button on the Today page that starts the fix (opens the right build tool, say). <paramref name="labelKey"/> names the button; registered once at start-up.</summary>
        public static void RegisterNeedAction(string moduleId, string needKey, string labelKey, Action action)
        {
            if (string.IsNullOrEmpty(needKey) || !needKey.StartsWith(moduleId + ".", StringComparison.Ordinal) || action == null) return;
            needActions[needKey] = new KeyValuePair<string, Action>(labelKey, action);
        }
        internal static bool TryNeedAction(string needKey, out string labelKey, out Action action)
        {
            if (needActions.TryGetValue(needKey, out var kv)) { labelKey = kv.Key; action = kv.Value; return true; }
            labelKey = null; action = null; return false;
        }

        private static readonly ColonyAlertBoard alertBoard = new ColonyAlertBoard();
        /// <summary>How alert the colony is as a whole (the highest level any running module has published). Modules read this to adapt: pause routine work at Red, stand by, and so on.</summary>
        public static ColonyAlertLevel ColonyAlert => FamilyLifeMod.Settings != null && !FamilyLifeMod.Settings.alertEffects ? ColonyAlertLevel.Normal : alertBoard.Level(id => FamilyFramework.Catalog.IsEnabled(id));
        /// <summary>The alert as published, whether or not the player has turned its effects on other modules off (for display).</summary>
        public static ColonyAlertLevel ColonyAlertPublished => alertBoard.Level(id => FamilyFramework.Catalog.IsEnabled(id));
        /// <summary>The module that raised the colony to its current level, or null at Normal.</summary>
        public static string ColonyAlertRaisedBy => alertBoard.RaisedBy(id => FamilyFramework.Catalog.IsEnabled(id));
        /// <summary>A module publishes its own alert level (republished each pass; a stopped module is ignored).</summary>
        public static void PublishColonyAlert(string moduleId, ColonyAlertLevel level) => alertBoard.Publish(moduleId, level);

        private static readonly DutyClaimBook dutyClaims = new DutyClaimBook();
        /// <summary>Takes a pawn for a module's duty for a while. False when another module already has them: leave that pawn alone. A pawn can hold roles in several modules; this keeps them from being sent to two duties at once.</summary>
        public static bool TryClaimDuty(Pawn p, string moduleId, int ticks = 1500) => p != null && dutyClaims.TryClaim(p.thingIDNumber, moduleId, Now, ticks);
        /// <summary>True when another module has this pawn on a duty.</summary>
        public static bool ClaimedByOther(Pawn p, string moduleId) => p != null && dutyClaims.HeldByOther(p.thingIDNumber, moduleId, Now);
        /// <summary>The module that has this pawn on a duty now, or null.</summary>
        public static string DutyHolder(Pawn p) => p == null ? null : dutyClaims.HolderOf(p.thingIDNumber, Now);
        /// <summary>Ends the module's claim on a pawn (a duty finished or was never started).</summary>
        public static void ReleaseDuty(Pawn p, string moduleId) { if (p != null) dutyClaims.Release(p.thingIDNumber, moduleId); }

        /// <summary>
        /// The one check for "has nothing to do and can be given a duty": awake, able, free of a mental state, not asleep or on recreation by the timetable, last job idle and not ordered by the player, no urgent
        /// need, not resting for health, fed and rested enough, and not already on another module's duty. Modules add their own extra conditions after this.
        /// </summary>
        public static bool FreeForDuty(Pawn p, string moduleId)
        {
            if (p == null || !p.Spawned || p.Dead || p.Downed || p.Drafted || p.InMentalState || p.IsPrisoner || p.IsSlave || !p.RaceProps.Humanlike || p.DevelopmentalStage.Baby() || !p.Awake()) return false;
            if (ClaimedByOther(p, moduleId)) return false;
            var assignment = p.timetable?.CurrentAssignment;
            if (assignment == TimeAssignmentDefOf.Sleep || assignment == TimeAssignmentDefOf.Joy) return false;
            if (p.mindState?.lastJobTag != JobTag.Idle || p.CurJob?.playerForced == true) return false;
            if (NeedsReason(p) != null || HealthAIUtility.ShouldSeekMedicalRest(p)) return false;
            return !(p.needs?.food != null && p.needs.food.CurLevel < .2f) && !(p.needs?.rest != null && p.needs.rest.CurLevel < .15f);
        }

        /// <summary>The pawn the player is currently playing as (Perspective Shift), or 0.</summary>
        public static int AvatarId => PerspectiveShiftBridge.AvatarId;
        public static bool IsAvatar(Pawn p) => PerspectiveShiftBridge.IsAvatar(p);

        /// <summary>Not drafted, hurt or in a break; and, outside a developer test, not being played or selected.</summary>
        public static bool IsFree(FamilyComponent c, int pawnId) => Governance.Free(c, pawnId);
        public static bool FreeFor(FamilyComponent c, int pawnId) => OfficeDriver.FreeFor(c, pawnId);
        /// <summary>Idle enough to be asked to do something now (or simply undrafted and awake during a developer test).</summary>
        public static bool Ready(Pawn p) => OfficeDriver.Ready(p);
        /// <summary>Awake and able, for when the player asks for something and the pawn need not be idle.</summary>
        public static bool Available(Pawn p) => OfficeDriver.Available(p);
        /// <summary>Why this person should stop (a need, or an event they have been called to), or null.</summary>
        public static string NeedsReason(Pawn p) => OfficeDriver.NeedsReason(p);

        // ---- moods

        /// <summary>Gives a pawn a memory by thought def name (a missing def is ignored with a warning, never an error).</summary>
        public static void GainMemory(Pawn pawn, string thoughtName, Pawn other) => Activities.GainMemory(pawn, thoughtName, other);

        // ---- conversations and meetings

        /// <summary>One cooperative exchange between a post holder and another person, on a topic. Relationship changes go through the shared, daily-limited path.</summary>
        public static ExchangeResult RunConversation(Pawn leader, Pawn other, OfficeKind kind, SkillDef subject, ExchangeTopic topic, int seed, float facility = 0f) => Conversations.Run(leader, other, kind, subject, topic, seed, facility);

        /// <summary>Sends <paramref name="actor"/> to <paramref name="other"/> to hold a meeting the module registered. False if either is not free.</summary>
        public static bool StartMeeting(Pawn actor, Pawn other, JobDef def, int id, bool byPlayer = false, bool recipientConsented = false) => GovernanceMeetings.Start(actor, other, def, id, byPlayer, recipientConsented);
        /// <summary>A number that has not been used before, handed over only when physical work was really finished.</summary>
        public static int NextToken() => GovernanceMeetings.NextToken();

        /// <summary>The person served by one of their own house's post-holders feels it. Relationship changes are made in the exchanges themselves, never here.</summary>
        public static void Served(FamilyRegistry r, OfficeKind kind, Pawn holder, Pawn served, int now) => OfficeDriver.Served(r, kind, holder, served, now);

        // ---- planned events (the framework owns the notice, the saved pending state and the authority checks)

        /// <summary>Plans a registered event for tomorrow and says so with a notice that never blocks. Does nothing if the post is vacant or its holder is not on a map.</summary>
        public static void PlanEvent(int savedKind) => OfficeEvents.Plan(savedKind);
        /// <summary>Begins a registered event now: the framework checks the post and the holder, then the owning module's behaviour runs.</summary>
        public static bool BeginPlannedEvent(int savedKind) => OfficeEvents.Begin(savedKind);
        /// <summary>Removes the notices of the given planned events from the letter stack. Returns a line saying so, if any were there.</summary>
        public static IEnumerable<string> RemoveNotices(params int[] savedKinds) => OfficeEvents.RemoveNotices(savedKinds);
        /// <summary>The in-game year and whether it is the quadrum the yearly events fall in, at a map's longitude.</summary>
        public static int Year(Map m) => OfficeEvents.Year(m);
        public static bool IsAprimay(Map m) => OfficeEvents.IsAprimay(m);

        // ---- the post's work in general

        /// <summary>Furniture or rooms that belong to a school, a library or a study, not a dining hall. Judged by name, because mods name them so.</summary>
        public static bool LooksLikeSchool(ThingDef def) => HallDriver.LooksLikeSchool(def);

        /// <summary>0 to 1: a skill level scaled so 20 is full. A pawn who cannot do the skill is 0.</summary>
        public static float SkillFraction(Pawn p, SkillDef def) { var s = p?.skills?.GetSkill(def); return s == null || s.TotallyDisabled ? 0f : Math.Min(1f, s.Level / 20f); }

        /// <summary>A post's fitness for a Social/Intellect style post (used by posts that have not declared their own).</summary>
        public static float SocialFitness(Pawn p) => p == null || p.skills == null || p.Dead || p.WorkTagIsDisabled(WorkTags.Social) ? 0f : .6f * SkillFraction(p, SkillDefOf.Intellectual) + .4f * SkillFraction(p, SkillDefOf.Social);

        // ---- developer tests

        /// <summary>Set by a module's developer action so a test is not held up by who happens to be busy.</summary>
        public static int ForceUntil { get => OfficeDriver.ForceUntil; set => OfficeDriver.ForceUntil = value; }
        public static bool Forced => OfficeDriver.Forced;
        /// <summary>Written to the log during a developer test only, so a flow that does not start says why.</summary>
        public static void Trace(string text) => OfficeDriver.Trace(text);
        /// <summary>Runs the posts' work now, rather than at the next check.</summary>
        public static void WorkNow(FamilyComponent c, int now, OfficeKind? only = null) => OfficeDriver.WorkNow(c, now, only);
        /// <summary>What stands between a post and its work, in plain lines. Each module contributes its own section.</summary>
        public static List<string> Diagnose(FamilyComponent c, int now, OfficeKind? only = null) => OfficeDriver.Diagnose(c, now, only);
        /// <summary>Shown to the person when a developer action finishes.</summary>
        public static FamilySettings Settings => FamilyLifeMod.Settings;
    }

    /// <summary>
    /// Whether a pawn should stop what a class or an appointment has them doing (a real need, an emergency, danger), and their skill and tiredness. Read on a short timer and cached,
    /// so a job may ask every tick. Public because every module's jobs use the same answer; the cache and the rest stay internal.
    /// </summary>
    public static class PawnReadinessApi
    {
        public static string StopReason(Pawn p, int now) => PawnReadiness.StopReason(p, now);
        public static bool Excusable(string reason) => PawnReadiness.Excusable(reason);
        public static int Level(Pawn p, SkillDef d) => PawnReadiness.Level(p, d);
        public static float Fatigue(Pawn p) => PawnReadiness.Fatigue(p);
        public static bool Danger(Map map) => PawnReadiness.Danger(map);
    }
}
