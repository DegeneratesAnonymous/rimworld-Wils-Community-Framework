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
            FamilyFramework.Resets.Add(Id + ".reset", Id, () => WorksStories.Reset());
            FamilyFramework.Removal.Add(Id + ".removal", Id, c =>
            {
                var report = new CleanupReport(Id);
                report.Step("Clear in-memory records", () => { WorksStories.Reset(); return "careers and experience retained; this module adds no jobs or hediffs"; });
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
    }
}
