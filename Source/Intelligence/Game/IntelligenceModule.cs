using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using Verse;

namespace FamilyLife
{
    public sealed class IntelligenceMod : Mod
    {
        public IntelligenceMod(ModContentPack content) : base(content) { FamilyFramework.Install(new IntelligenceModule()); }
    }

    public sealed class IntelligenceModule : IFamilyModule
    {
        public string Id => IntelligenceCareers.Module;
        public void Register()
        {
            FamilyFramework.Catalog.Register(new ModuleInfo { Id = Id, Name = "Wil's Role Modules : Intelligence", ApiVersion = FamilyFramework.ApiVersion, Requires = new[] { ModuleCatalog.FrameworkId } });
            CareerService.RegisterCareer(IntelligenceCareers.Definition());
            CareerService.RegisterRole(new ProfessionRole { Id = IntelligenceCareers.Assistant, ModuleId = Id, Career = IntelligenceCareers.Career, Level = 1 });
            CareerService.RegisterRole(new ProfessionRole { Id = IntelligenceCareers.Researcher, ModuleId = Id, Career = IntelligenceCareers.Career, Level = 2 });
            foreach (var specialty in IntelligenceCareers.Specialties)
                CareerService.RegisterRole(new ProfessionRole { Id = IntelligenceCareers.Role(specialty), ModuleId = Id, Career = IntelligenceCareers.Career, Level = 3, Specialty = specialty, BonusKey = "WFL_Intelligence_" + specialty + "Bonus" });
            FamilyFramework.ResearchDone.Add(Id + ".research", Id, IntelligenceWork.Researched);
            FamilyFramework.LabourDone.Add(Id + ".labour", Id, IntelligenceWork.Labour);
            FamilyFramework.ProductMade.Add(Id + ".products", Id, IntelligenceWork.Made);
            FamilyFramework.SocialInteraction.Add(Id + ".relationships", Id, IntelligenceStories.Social);
            FamilyFramework.Resets.Add(Id + ".reset", Id, () => { IntelligenceWork.Reset(); IntelligenceStories.Reset(); });
            FamilyFramework.Removal.Add(Id + ".removal", Id, c =>
            {
                var report = new CleanupReport(Id);
                report.Step("Clear in-memory research tallies", () => { IntelligenceWork.Reset(); IntelligenceStories.Reset(); return "careers, experience and memories retained; this module adds no jobs or hediffs"; });
                return report;
            });
            CareerService.Registered(Id);
        }
    }

    /// <summary>Reads the game's research projects as the plain facts the domain routes on. Cached per project.</summary>
    public static class IntelligenceResearch
    {
        private static readonly Dictionary<string, ResearchFacts> cache = new Dictionary<string, ResearchFacts>();
        public static void Reset() => cache.Clear();

        public static ResearchFacts Facts(ResearchProjectDef project)
        {
            if (project == null) return null;
            if (cache.TryGetValue(project.defName, out var known)) return known;
            var f = new ResearchFacts { Anomaly = project.knowledgeCategory != null, Gene = IntelligenceClassifier.GeneProject(project.defName, project.label), Industrial = project.techLevel >= TechLevel.Industrial };
            return cache[project.defName] = f;
        }

        /// <summary>The project the colony is working on now, or null.</summary>
        public static ResearchProjectDef Current() => Find.ResearchManager?.GetProject();
    }

    public static class IntelligenceWork
    {
        private static readonly Dictionary<int, float> tally = new Dictionary<int, float>();
        private static int serial;
        public static void Reset() { tally.Clear(); IntelligenceResearch.Reset(); }

        private static void Credit(Pawn pawn, IntelligenceTrack track, string key, int points, int seed)
        {
            var c = FamilyComponent.Current; if (c == null || pawn == null || track == null) return;
            CareerService.Report(c, pawn, IntelligenceCareers.Career, track.Specialty, track.Kind, key, points, FrameworkServices.Now);
            IntelligenceStories.Worked(pawn, track.Specialty, seed);
        }

        /// <summary>
        /// Research work arrives a little at a time; experience is given once per measure of it, to the discipline of the project being worked on. The game reports the raw work rate of
        /// each tick (about the researcher's speed), and scales it by <see cref="ResearchManager.ResearchPointsPerWorkTick"/> into project points, so that is applied here: a chunk is real points.
        /// </summary>
        public static void Researched(Pawn pawn, float amount)
        {
            if (pawn == null || amount <= 0f || FamilyComponent.Current == null) return;
            tally.TryGetValue(pawn.thingIDNumber, out float sum); sum += amount * ResearchManager.ResearchPointsPerWorkTick;
            if (sum < IntelligenceCareers.ResearchPerChunk) { tally[pawn.thingIDNumber] = sum; return; }
            tally[pawn.thingIDNumber] = sum - IntelligenceCareers.ResearchPerChunk;
            var track = IntelligenceClassifier.Research(IntelligenceResearch.Facts(IntelligenceResearch.Current()));
            int n = ++serial;
            Credit(pawn, track, "research:" + FrameworkServices.Now + ":" + n, IntelligenceCareers.ResearchPoints, n);
        }

        /// <summary>A finished scan is the Analyst's; a finished study session is Dark Study's.</summary>
        public static void Labour(LabourReport r)
        {
            if (r?.Pawn == null) return;
            if (IntelligenceClassifier.IsScan(r.JobDef)) Credit(r.Pawn, new IntelligenceTrack { Specialty = "Analyst", Kind = "intel.scan" }, "scan:" + r.JobId, IntelligenceCareers.ScanPoints, r.JobId);
            else if (IntelligenceClassifier.IsStudy(r.JobDef)) Credit(r.Pawn, new IntelligenceTrack { Specialty = "DarkScholar", Kind = "intel.study" }, "study:" + r.JobId, IntelligenceCareers.StudyPoints, r.JobId);
        }

        /// <summary>A book written or a xenogerm made at a bench. A book's quality rises one band for an assigned Author; nothing else about vanilla work is changed.</summary>
        public static void Made(Pawn pawn, Thing bench, Thing product, int job)
        {
            if (pawn == null || product?.def == null) return;
            var track = IntelligenceClassifier.Product(product.def.defName, product.TryGetComp<CompBook>() != null);
            if (track == null) return;
            bool active = CareerService.IsAssignedSpecialist(pawn, IntelligenceCareers.Career, track.Specialty);
            if (active && track.Specialty == "Author")
            {
                var quality = product.TryGetComp<CompQuality>();
                if (quality != null) { var now = quality.Quality; var better = (QualityCategory)IntelligenceBonuses.ImprovedQuality((int)now, true); if (better != now) quality.SetQuality(better, ArtGenerationContext.Colony); }
            }
            if (job != 0) { Credit(pawn, track, "made:" + job, IntelligenceCareers.ProductPoints, job); }
            if (track.Specialty == "Author" && product.TryGetQuality(out QualityCategory q) && q <= QualityCategory.Poor) IntelligenceStories.Setback(pawn, "Author", 1, job);          // "threw out the draft"
        }
    }

    public static class IntelligenceStories
    {
        private static readonly Dictionary<string, int> last = new Dictionary<string, int>();
        public static void Reset() => last.Clear();

        /// <summary>The role whose events the pawn lives: their assigned specialty, else Researcher, else Assistant. Null if they hold no Intelligence role at all.</summary>
        public static string ActiveRole(Pawn pawn)
        {
            var book = FamilyComponent.Current?.Registry.Assignments(); if (book == null || pawn == null) return null;
            foreach (var specialty in IntelligenceCareers.Specialties) if (book.IsAssigned(pawn.thingIDNumber, IntelligenceCareers.Role(specialty))) return specialty;
            if (book.IsAssigned(pawn.thingIDNumber, IntelligenceCareers.Researcher)) return "Researcher";
            if (book.IsAssigned(pawn.thingIDNumber, IntelligenceCareers.Assistant)) return "Assistant";
            return null;
        }

        private static void Give(Pawn subject, Pawn other, string role, int category, int index)
        {
            string fact = IntelligenceEvents.Fact(role, category, index); if (fact == null) return;
            var e = IntelligenceEvents.Eligible(role, fact, other != null).FirstOrDefault(); if (e == null) return;
            int day = FrameworkServices.EconomyDay; string key = subject.thingIDNumber + ":" + e.Id + ":" + (other?.thingIDNumber ?? 0);
            if (last.TryGetValue(key, out int before) && day - before < 3) return;
            last[key] = day; FrameworkServices.GainMemory(subject, e.Thought, other);
        }

        private static readonly int[] Routine = { 0, 1, 4 };
        /// <summary>Finished work: routine success, an outstanding result, or pride in the calling, in turn.</summary>
        public static void Worked(Pawn pawn, string specialty, int seed)
        {
            string role = ActiveRole(pawn); if (role == null) return;
            Give(pawn, null, role, IntelligenceEvents.Positive, Routine[Math.Abs(seed) % Routine.Length]);
        }
        public static void Setback(Pawn pawn, string specialty, int index, int seed)
        {
            string role = ActiveRole(pawn); if (role == null) return;
            Give(pawn, null, role, IntelligenceEvents.Negative, index);
        }
        public static void Social(Pawn actor, Pawn other, InteractionDef interaction)
        {
            string role = ActiveRole(actor); if (role == null || ActiveRole(other) == null) return;
            int seed = (actor.thingIDNumber * 397) ^ other.thingIDNumber ^ (FrameworkServices.EconomyDay / 3);
            Give(actor, other, role, IntelligenceEvents.Social, Math.Abs(seed) % 5);
        }
    }

    /// <summary>Applies an assigned specialty's research or study bonus to the stat it is attached to (patched onto ResearchSpeed and StudyEfficiency).</summary>
    public sealed class StatPart_IntelligenceSpecialty : StatPart
    {
        private static string Assigned(Pawn pawn)
        {
            foreach (var specialty in IntelligenceCareers.Specialties) if (CareerService.IsAssignedSpecialist(pawn, IntelligenceCareers.Career, specialty)) return specialty;
            return null;
        }
        private float Factor(StatRequest req, out string who)
        {
            who = null; var pawn = req.Thing as Pawn; if (pawn == null || parentStat == null) return 1f;
            string specialty = Assigned(pawn); if (specialty == null) return 1f;
            who = specialty;
            if (parentStat.defName == "StudyEfficiency") return IntelligenceBonuses.StudyFactor(specialty);
            if (parentStat.defName == "ResearchSpeed") return IntelligenceBonuses.ResearchFactor(specialty, IntelligenceResearch.Facts(IntelligenceResearch.Current()));
            return 1f;
        }
        public override void TransformValue(StatRequest req, ref float val) { float f = Factor(req, out _); if (f != 1f) val *= f; }
        public override string ExplanationPart(StatRequest req) { float f = Factor(req, out string who); return f != 1f ? who + ": x" + f.ToString("0.##") : null; }
    }
}
