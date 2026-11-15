using System;
using System.Collections.Generic;

namespace FamilyLife.Domain
{
    /// <summary>
    /// The Intelligence career: Assistant (anyone) -> Researcher (qualified) -> a specialty. Inventor and Analyst grow out of ordinary research and scanning; Geneticist, Dark Study and Writing
    /// each have their own work behind them (xenogerms, Anomaly study, books). Owner: Intelligence.
    /// </summary>
    public static class IntelligenceCareers
    {
        public const string Module = "wils.intelligence", Career = "wils.intelligence.intelligence";
        public const string Assistant = "wils.intelligence.assistant", Researcher = "wils.intelligence.researcher";
        public static string Role(string name) => Module + "." + name.ToLowerInvariant();

        /// <summary>Dark Study's role is called Dark Scholar; its specialty key is the one used in saves and rules.</summary>
        public static readonly string[] Specialties = { "Inventor", "Analyst", "Geneticist", "DarkScholar", "Author" };

        public static CareerDefinition Definition() => new CareerDefinition
        {
            Id = Career, ModuleId = Module, LabelKey = "WFL_Intelligence_Career",
            Level2 = new CareerRule { GeneralXp = 120 }.Skill("Intellectual", 6),
            Specialties = new Dictionary<string, CareerRule>
            {
                { "Inventor", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 10).Skill("Crafting", 5) },
                { "Analyst", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 8).Skill("Social", 5) },
                { "Geneticist", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 10).Skill("Medicine", 10) },
                { "DarkScholar", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 10) },
                { "Author", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 8).Skill("Artistic", 8) }
            }
        };

        /// <summary>Experience for a measure of research (one chunk is <see cref="ResearchPerChunk"/> project points), one scan, one study session, one product.</summary>
        public const int ResearchPoints = 4, ScanPoints = 6, StudyPoints = 8, ProductPoints = 12;
        public const float ResearchPerChunk = 50f;          // project points; a researcher at speed 1 makes about 165 a day over an eight-hour shift, so a chunk is about a quarter of a day's work
    }

    // ------------------------------------------------------------------ what was worked on

    /// <summary>What a project is about, as the game side reads it. Pure here so the routing is tested.</summary>
    public sealed class ResearchFacts
    {
        /// <summary>An Anomaly knowledge project (it has a knowledge category).</summary>
        public bool Anomaly;
        /// <summary>The project is about genes or xenogerms (matched by name; modded projects may fall outside it).</summary>
        public bool Gene;
        /// <summary>The project is at the industrial tech level or later.</summary>
        public bool Industrial;
    }

    public sealed class IntelligenceTrack { public string Specialty, Kind; }

    public static class IntelligenceClassifier
    {
        /// <summary>Anomaly first (it is its own discipline), then genes, then the industrial and later projects an inventor builds on. Anything else is ordinary research: the Researcher's own.</summary>
        public static IntelligenceTrack Research(ResearchFacts f)
        {
            if (f == null) return new IntelligenceTrack { Kind = "intel.research" };
            if (f.Anomaly) return new IntelligenceTrack { Specialty = "DarkScholar", Kind = "intel.darkstudy" };
            if (f.Gene) return new IntelligenceTrack { Specialty = "Geneticist", Kind = "intel.genetics" };
            if (f.Industrial) return new IntelligenceTrack { Specialty = "Inventor", Kind = "intel.invention" };
            return new IntelligenceTrack { Kind = "intel.research" };
        }

        public const string ScanJob = "OperateScanner";
        public static bool IsScan(string jobDef) => jobDef != null && jobDef.IndexOf("Scanner", StringComparison.OrdinalIgnoreCase) >= 0;
        /// <summary>A study of something Anomaly gives to study (matched by job name; with Anomaly absent nothing is ever studied and the specialty simply has no work).</summary>
        public static bool IsStudy(string jobDef) => jobDef != null && jobDef.IndexOf("Study", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool Has(string value, params string[] words)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (var w in words) if (value.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        /// <summary>A written work (an Anomaly book) is the Author's; a xenogerm is the Geneticist's. Anything else is nobody's.</summary>
        public static IntelligenceTrack Product(string defName, bool isBook)
        {
            if (isBook || Has(defName, "Novel", "Textbook", "Tome", "Schematic", "Book")) return new IntelligenceTrack { Specialty = "Author", Kind = "intel.book" };
            if (Has(defName, "Xenogerm", "Genepack")) return new IntelligenceTrack { Specialty = "Geneticist", Kind = "intel.xenogerm" };
            return null;
        }
        public static bool GeneProject(string defName, string label) => Has(defName, "Gene", "Xeno", "Germ") || Has(label, "gene", "xeno", "germ");
    }

    // ------------------------------------------------------------------ what an assignment is worth

    /// <summary>
    /// A specialty is worth something only while the pawn is qualified and assigned to that exact specialty (docs/SPECIALTY-BONUSES.md). All values are bounded multipliers or one quality band.
    ///   Inventor    +15% research speed on industrial and later projects.        Geneticist  +15% on gene projects.
    ///   Dark Scholar +15% on Anomaly projects and +20% study efficiency.           Analyst     +10% on any project (the generalist).
    ///   Author      the quality of a book rises one band, up to Excellent.
    /// </summary>
    public static class IntelligenceBonuses
    {
        public const float Inventor = 1.15f, Geneticist = 1.15f, DarkProject = 1.15f, DarkStudy = 1.20f, Analyst = 1.10f;

        public static float ResearchFactor(string specialty, ResearchFacts f)
        {
            if (specialty == null) return 1f;
            switch (specialty)
            {
                case "Inventor": return f != null && f.Industrial && !f.Anomaly ? Inventor : 1f;
                case "Geneticist": return f != null && f.Gene ? Geneticist : 1f;
                case "DarkScholar": return f != null && f.Anomaly ? DarkProject : 1f;
                case "Analyst": return Analyst;
                default: return 1f;
            }
        }
        public static float StudyFactor(string specialty) => specialty == "DarkScholar" ? DarkStudy : 1f;
        public static int ImprovedQuality(int quality, bool assigned) => assigned && quality < 4 ? quality + 1 : quality;
    }
}
