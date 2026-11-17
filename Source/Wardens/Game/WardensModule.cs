using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using Verse;

namespace FamilyLife
{
    public sealed class WardensMod : Mod
    {
        public WardensMod(ModContentPack content) : base(content) { FamilyFramework.Install(new WardensModule()); }
    }

    public sealed class WardensModule : IFamilyModule
    {
        public string Id => WardenCareers.Module;
        public void Register()
        {
            FamilyFramework.Catalog.Register(new ModuleInfo { Id = Id, Name = "Wil's Role Modules : Wardens", ApiVersion = FamilyFramework.ApiVersion, Requires = new[] { ModuleCatalog.FrameworkId } });
            CareerService.RegisterCareer(WardenCareers.Definition());
            CareerService.RegisterRole(new ProfessionRole { Id = WardenCareers.Guard, ModuleId = Id, Career = WardenCareers.Career, Level = 1 });
            CareerService.RegisterRole(new ProfessionRole { Id = WardenCareers.Warden, ModuleId = Id, Career = WardenCareers.Career, Level = 2 });
            foreach (var specialty in WardenCareers.Specialties)
                CareerService.RegisterRole(new ProfessionRole { Id = WardenCareers.Role(specialty), ModuleId = Id, Career = WardenCareers.Career, Level = 3, Specialty = specialty, BonusKey = "WFL_Wardens_" + specialty + "Bonus" });
            FamilyFramework.Pages.Register(new CommunityPage<Func<UnityEngine.Rect, FamilyComponent, float>> { Id = Id + ".overview", ModuleId = Id, LabelKey = "WFL_Wardens_Page", Order = 165, Icon = "WilsFamilyLife/Pages/prisoners", Render = WardensPage.Draw });
            FamilyFramework.LabourDone.Add(Id + ".labour", Id, WardenWork.Labour);
            FamilyFramework.LabourDone.Add(Id + ".responsible", Id, WardenTreatmentGame.Labour);
            FamilyFramework.Roster.Register(new WorkItem { Id = Id + ".treatment", ModuleId = Id, Interval = 2500, Run = WardenTreatmentGame.Run });
            FamilyFramework.Roster.Register(new WorkItem { Id = Id + ".rounds", ModuleId = Id, Interval = 500, Run = WardenRoundGame.Run });
            FamilyFramework.Roster.Register(new WorkItem { Id = Id + ".escorts", ModuleId = Id, Interval = 600, Run = WardenEscortGame.Run });
            FamilyFramework.Reconcilers.Add(Id + ".reconcile", Id, WardenTreatmentGame.Reconcile);
            FamilyFramework.SocialInteraction.Add(Id + ".relationships", Id, WardenStories.Social);
            FamilyFramework.Resets.Add(Id + ".reset", Id, () => { WardenStories.Reset(); WardenTreatmentGame.Reset(); WardenRoomGame.Reset(); WardenRoundGame.Reset(); WardenEscortGame.Reset(); });
            FamilyFramework.Removal.Add(Id + ".removal", Id, c =>
            {
                var report = new CleanupReport(Id);
                report.Step("Clear in-memory records", () => { WardenStories.Reset(); WardenTreatmentGame.Reset(); WardenRoomGame.Reset(); WardenRoundGame.Reset(); WardenEscortGame.Reset(); return "careers, experience, the treatment record and memories retained; the round job ends by itself and adds no hediffs"; });
                return report;
            });
            FamilyFramework.CrossTraining.Add(Id + ".cross.military", Id, new CrossTrainingRule { From = WardenCareers.Career, To = "wils.military.military", KindPrefix = null, Share = .25f });
            FamilyFramework.HealthChecks.Add(Id + ".health", Id, c => WardensHealth.Check(c, Id));
            FamilyFramework.Roster.Register(new WorkItem { Id = Id + ".release", ModuleId = Id, Interval = 600, Run = WardenReleaseGame.Run });
            CareerService.Registered(Id);
        }
    }

    public static class WardenWork
    {
        /// <summary>A finished job under the Warden work type, or a conversion, slave or containment job: experience, once per job. Nothing about the work itself is changed.</summary>
        public static void Labour(LabourReport r)
        {
            var c = FamilyComponent.Current; if (c == null || r?.Pawn == null) return;
            var track = WardenClassifier.Classify(r.JobDef, r.WorkType); if (track == null) return;
            CareerService.Report(c, r.Pawn, WardenCareers.Career, track.Specialty, track.Kind, track.Kind + ":" + r.JobId, track.Points, FrameworkServices.Now);
            WardenStories.Worked(r.Pawn, r.JobId);
        }
    }

    public static class WardenStories
    {
        private static readonly Dictionary<string, int> last = new Dictionary<string, int>();
        public static void Reset() => last.Clear();

        /// <summary>The role whose events the pawn lives: their assigned specialty, else Warden, else Guard. Null if they hold no Warden role at all.</summary>
        public static string ActiveRole(Pawn pawn)
        {
            var book = FamilyComponent.Current?.Registry.Assignments(); if (book == null || pawn == null) return null;
            foreach (var specialty in WardenCareers.Specialties) if (book.IsAssigned(pawn.thingIDNumber, WardenCareers.Role(specialty))) return specialty;
            if (book.IsAssigned(pawn.thingIDNumber, WardenCareers.Warden)) return "Warden";
            return book.IsAssigned(pawn.thingIDNumber, WardenCareers.Guard) ? "Guard" : null;
        }

        private static void Give(Pawn subject, Pawn other, string role, int category, int index)
        {
            string fact = WardenEvents.Fact(role, category, index); if (fact == null) return;
            var e = WardenEvents.Eligible(role, fact, other != null).FirstOrDefault(); if (e == null) return;
            int day = FrameworkServices.EconomyDay; string key = subject.thingIDNumber + ":" + e.Id + ":" + (other?.thingIDNumber ?? 0);
            if (last.TryGetValue(key, out int before) && day - before < 3) return;
            last[key] = day; FrameworkServices.GainMemory(subject, e.Thought, other);
        }

        private static readonly int[] Routine = { 0, 1, 4 };
        /// <summary>Finished warden work: routine success, an outstanding result, or pride in the calling, in turn.</summary>
        public static void Worked(Pawn pawn, int seed)
        {
            string role = ActiveRole(pawn); if (role == null) return;
            Give(pawn, null, role, WardenEvents.Positive, Routine[Math.Abs(seed) % Routine.Length]);
        }

        public static void Social(Pawn actor, Pawn other, InteractionDef interaction)
        {
            string role = ActiveRole(actor); if (role == null || ActiveRole(other) == null) return;
            int seed = (actor.thingIDNumber * 397) ^ other.thingIDNumber ^ (FrameworkServices.EconomyDay / 3);
            Give(actor, other, role, WardenEvents.Social, Math.Abs(seed) % 5);
        }
    }

    /// <summary>Applies an assigned specialty's bonus to the stat it is attached to: conversion power (Confessor), suppression power (Overseer) and study efficiency (Containment Keeper).</summary>
    public sealed class StatPart_WardenSpecialty : StatPart
    {
        private float Factor(StatRequest req, out string who)
        {
            who = null; var pawn = req.Thing as Pawn; if (pawn == null || parentStat == null) return 1f;
            foreach (var specialty in WardenCareers.Specialties)
            {
                float f = WardenBonuses.Factor(specialty, parentStat.defName);
                if (f != 1f && CareerService.IsAssignedSpecialist(pawn, WardenCareers.Career, specialty)) { who = specialty; return f; }
            }
            return 1f;
        }
        public override void TransformValue(StatRequest req, ref float val) { float f = Factor(req, out _); if (f != 1f) val *= f; }
        public override string ExplanationPart(StatRequest req) { float f = Factor(req, out string who); return f != 1f ? who + ": x" + f.ToString("0.##") : null; }
    }
}
