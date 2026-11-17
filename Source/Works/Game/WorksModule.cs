using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using Verse;

namespace FamilyLife
{
    public sealed class WorksMod : Mod
    {
        public WorksMod(ModContentPack content) : base(content) { FamilyFramework.Install(new WorksModule()); }
    }

    public sealed class WorksModule : IFamilyModule
    {
        public string Id => WorksCareers.Module;
        public void Register()
        {
            FamilyFramework.Catalog.Register(new ModuleInfo { Id = Id, Name = "Wil's Role Modules : Works", ApiVersion = FamilyFramework.ApiVersion, Requires = new[] { ModuleCatalog.FrameworkId } });
            CareerService.RegisterCareer(WorksCareers.BuilderDefinition());
            CareerService.RegisterCareer(WorksCareers.MinerDefinition());
            Role(WorksCareers.BuilderLabourer, WorksCareers.Builder, 1, null); Role(WorksCareers.BuilderJourneyman, WorksCareers.Builder, 2, null);
            Role(WorksCareers.MinerLabourer, WorksCareers.Miner, 1, null); Role(WorksCareers.MinerJourneyman, WorksCareers.Miner, 2, null);
            foreach (var specialty in WorksCareers.BuilderSpecialties) Role(specialty, WorksCareers.Builder, 3, specialty);
            foreach (var specialty in WorksCareers.MinerSpecialties) Role(specialty, WorksCareers.Miner, 3, specialty);
            FamilyFramework.Pages.Register(new CommunityPage<Func<UnityEngine.Rect, FamilyComponent, float>> { Id = Id + ".overview", ModuleId = Id, LabelKey = "WFL_Works_Page", Order = 170, Icon = "WilsFamilyLife/Pages/works", Render = WorksPage.Draw });
            FamilyFramework.LabourDone.Add(Id + ".labour", Id, WorksWork.Labour);
            FamilyFramework.SocialInteraction.Add(Id + ".relationships", Id, WorksStories.Social);
            FamilyFramework.Roster.Register(new WorkItem { Id = Id + ".rounds", ModuleId = Id, Interval = 500, Run = WorksRoundGame.Run });
            FamilyFramework.Roster.Register(new WorkItem { Id = Id + ".family", ModuleId = Id, Interval = 2500, Run = WorksFamilyGame.Run });
            FamilyFramework.Reconcilers.Add(Id + ".reconcile", Id, WorksFamilyGame.Reconcile);
            FamilyFramework.Resets.Add(Id + ".reset", Id, () => { WorksStories.Reset(); WorksRoomGame.Reset(); WorksRoundGame.Reset(); WorksFamilyGame.Reset(); });
            FamilyFramework.Removal.Add(Id + ".removal", Id, c =>
            {
                var report = new CleanupReport(Id);
                report.Step("Clear in-memory records", () => { WorksStories.Reset(); WorksRoomGame.Reset(); WorksRoundGame.Reset(); WorksFamilyGame.Reset(); return "careers and experience retained; the round job ends by itself and adds no hediffs"; });
                return report;
            });
            CareerService.Registered(Id);
        }

        private void Role(string name, string career, int level, string specialty) =>
            CareerService.RegisterRole(new ProfessionRole { Id = WorksCareers.Role(name), ModuleId = Id, Career = career, Level = level, Specialty = specialty, BonusKey = specialty == null ? null : "WFL_Works_" + specialty + "Bonus" });
    }

    public static class WorksStories
    {
        private static readonly Dictionary<string, int> last = new Dictionary<string, int>();
        public static void Reset() => last.Clear();

        /// <summary>The Works role whose events the pawn lives (the one they are assigned to, the most senior first), or null. A pawn who holds a Builder and a Miner role lives the higher-level one.</summary>
        public static string ActiveRole(Pawn pawn)
        {
            var book = FamilyComponent.Current?.Registry.Assignments(); if (book == null || pawn == null) return null;
            foreach (var specialty in WorksCareers.BuilderSpecialties.Concat(WorksCareers.MinerSpecialties)) if (book.IsAssigned(pawn.thingIDNumber, WorksCareers.Role(specialty))) return specialty;
            foreach (var name in new[] { WorksCareers.BuilderJourneyman, WorksCareers.MinerJourneyman, WorksCareers.BuilderLabourer, WorksCareers.MinerLabourer }) if (book.IsAssigned(pawn.thingIDNumber, WorksCareers.Role(name))) return name;
            return null;
        }

        private static void Give(Pawn subject, Pawn other, string role, int category, int index)
        {
            string fact = WorksEvents.Fact(role, category, index); if (fact == null) return;
            var e = WorksEvents.Eligible(role, fact, other != null).FirstOrDefault(); if (e == null) return;
            int day = FrameworkServices.EconomyDay; string key = subject.thingIDNumber + ":" + e.Id + ":" + (other?.thingIDNumber ?? 0);
            if (last.TryGetValue(key, out int before) && day - before < 3) return;
            last[key] = day; FrameworkServices.GainMemory(subject, e.Thought, other);
        }

        private static readonly int[] Routine = { 0, 1, 4 };
        /// <summary>Finished work: routine success, an outstanding result, or pride in the trade, in turn.</summary>
        public static void Worked(Pawn pawn, int seed)
        {
            string role = ActiveRole(pawn); if (role == null) return;
            Give(pawn, null, role, WorksEvents.Positive, Routine[Math.Abs(seed) % Routine.Length]);
        }

        public static void Social(Pawn actor, Pawn other, InteractionDef interaction)
        {
            string role = ActiveRole(actor); if (role == null || ActiveRole(other) == null) return;
            int seed = (actor.thingIDNumber * 397) ^ other.thingIDNumber ^ (FrameworkServices.EconomyDay / 3);
            Give(actor, other, role, WorksEvents.Social, Math.Abs(seed) % 5);
        }
    }

    public static class WorksWork
    {
        private static WorksJobFacts Facts(LabourReport r)
        {
            var f = new WorksJobFacts(); var job = r.Pawn.CurJob; var map = r.Pawn.Map; if (job == null) return f;
            try
            {
                var thing = job.targetA.Thing;
                if (r.JobDef == "FinishFrame" && thing is Frame frame)
                {
                    var built = frame.def.entityDefToBuild as ThingDef;
                    f.Stone = frame.Stuff != null && frame.Stuff.stuffProps?.categories != null && frame.Stuff.stuffProps.categories.Contains(StuffCategoryDefOf.Stony);
                    string cat = built?.designationCategory?.defName ?? ""; f.Furniture = cat == "Furniture"; f.Machine = cat == "Power" || cat == "Production" || cat == "Temperature";
                }
                else if (r.JobDef == "Mine")
                {
                    f.Ore = thing?.def?.building != null && thing.def.building.isResourceRock;
                    var cell = job.targetA.Cell; if (map != null && cell.InBounds(map)) f.UnderRock = map.roofGrid.RoofAt(cell)?.isThickRoof == true;
                }
            }
            catch (Exception) { }
            return f;
        }

        /// <summary>A finished construction, repair, smoothing or mining job: experience, once per job. Nothing about the work itself is changed.</summary>
        public static void Labour(LabourReport r)
        {
            var c = FamilyComponent.Current; if (c == null || r?.Pawn == null) return;
            var track = WorksClassifier.Classify(r.JobDef, Facts(r)); if (track == null) return;
            int points = WorksRooms.Points(track.Points, WorksRooms.Suits(track.Kind), WorksRoomGame.PlaceOf(r.Pawn.Map, r.Pawn.CurJob?.targetA.Cell ?? r.Pawn.Position));
            CareerService.Report(c, r.Pawn, track.Career, track.Specialty, track.Kind, track.Kind + ":" + r.JobId, points, FrameworkServices.Now);
            WorksStories.Worked(r.Pawn, r.JobId); WorksFamilyGame.Crew(r.Pawn);
            if (r.JobDef == "FinishFrame") WorksFamilyGame.Built(r.Pawn, r.Pawn.CurJob?.targetA.Cell ?? r.Pawn.Position);
        }
    }
}

namespace FamilyLife
{
    /// <summary>Applies an assigned specialty's bonus to the stat it is attached to: construction, smoothing, repair and mining.</summary>
    public sealed class StatPart_WorksSpecialty : StatPart
    {
        private float Factor(StatRequest req, out string who)
        {
            who = null; var pawn = req.Thing as Pawn; if (pawn == null || parentStat == null) return 1f;
            foreach (var specialty in WorksCareers.BuilderSpecialties.Concat(WorksCareers.MinerSpecialties))
            {
                float f = WorksBonuses.Factor(specialty, parentStat.defName);
                if (f != 1f && CareerService.IsAssignedSpecialist(pawn, WorksCareers.CareerOf(specialty), specialty)) { who = specialty; return f; }
            }
            return 1f;
        }
        public override void TransformValue(StatRequest req, ref float val) { float f = Factor(req, out _); if (f != 1f) val *= f; }
        public override string ExplanationPart(StatRequest req) { float f = Factor(req, out string who); return f != 1f ? who + ": x" + f.ToString("0.##") : null; }
    }
}
