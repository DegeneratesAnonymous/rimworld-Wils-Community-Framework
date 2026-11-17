using System;
using System.Collections.Generic;

namespace FamilyLife.Domain
{
    /// <summary>
    /// The Intelligence career: Assistant (anyone) -> Researcher (qualified) -> a specialty. Analyst is the research generalist; Geneticist, Dark Scholar (Dark Study), Author, Mech Commander,
    /// Diplomat and Chemist each have their own work behind them (xenogerms, Anomaly study, books, mechanoids, talking with outsiders, drugs and medicine). Owner: Intelligence.
    /// </summary>
    public static class IntelligenceCareers
    {
        public const string Module = "wils.intelligence", Career = "wils.intelligence.intelligence";
        public const string Assistant = "wils.intelligence.assistant", Researcher = "wils.intelligence.researcher";
        public static string Role(string name) => Module + "." + name.ToLowerInvariant();

        /// <summary>Dark Study's role is called Dark Scholar; its specialty key is the one used in saves and rules.</summary>
        public static readonly string[] Specialties = { "Analyst", "Geneticist", "DarkScholar", "Author", "MechCommander", "Diplomat", "Chemist" };

        public static CareerDefinition Definition() => new CareerDefinition
        {
            Id = Career, ModuleId = Module, LabelKey = "WFL_Intelligence_Career",
            Level2 = new CareerRule { GeneralXp = 120 }.Skill("Intellectual", 6),
            Specialties = new Dictionary<string, CareerRule>
            {
                { "Analyst", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 8).Skill("Social", 5) },
                { "Geneticist", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 10).Skill("Medicine", 10) },
                { "DarkScholar", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 10) },
                { "Author", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 8).Skill("Artistic", 8) },
                { "MechCommander", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 8).Skill("Crafting", 6) },
                { "Diplomat", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 6).Skill("Social", 8) },
                { "Chemist", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 8).Skill("Medicine", 4) }
            }
        };

        /// <summary>Experience for a measure of research (one chunk is <see cref="ResearchPerChunk"/> project points), one scan, one study session, one product, one stretch of mech work, one talk.</summary>
        public const int ResearchPoints = 4, ScanPoints = 6, StudyPoints = 8, ProductPoints = 12, MechPoints = 6, TalkPoints = 4;
        public const float ResearchPerChunk = 50f;          // project points; a researcher at speed 1 makes about 165 a day over an eight-hour shift, so a chunk is about a quarter of a day's work
        /// <summary>A work done in a room that suits it (<see cref="IntelligenceRooms"/>) earns this much more experience.</summary>
        public const float RoomExperience = 1.25f;
    }

    // ------------------------------------------------------------------ what was worked on

    /// <summary>What a project is about, as the game side reads it. Pure here so the routing is tested.</summary>
    public sealed class ResearchFacts
    {
        /// <summary>An Anomaly knowledge project (it has a knowledge category).</summary>
        public bool Anomaly;
        /// <summary>The project is about genes or xenogerms (matched by name; modded projects may fall outside it).</summary>
        public bool Gene;
    }

    public sealed class IntelligenceTrack { public string Specialty, Kind; }

    public static class IntelligenceClassifier
    {
        /// <summary>Anomaly first (it is its own discipline), then genes. Anything else is ordinary research: the Researcher's own.</summary>
        public static IntelligenceTrack Research(ResearchFacts f)
        {
            if (f == null) return new IntelligenceTrack { Kind = "intel.research" };
            if (f.Anomaly) return new IntelligenceTrack { Specialty = "DarkScholar", Kind = "intel.darkstudy" };
            if (f.Gene) return new IntelligenceTrack { Specialty = "Geneticist", Kind = "intel.genetics" };
            return new IntelligenceTrack { Kind = "intel.research" };
        }

        public const string ScanJob = "OperateScanner";
        public static bool IsScan(string jobDef) => jobDef != null && jobDef.IndexOf("Scanner", StringComparison.OrdinalIgnoreCase) >= 0;
        /// <summary>A study of something Anomaly gives to study (matched by job name; with Anomaly absent nothing is ever studied and the specialty simply has no work).</summary>
        public static bool IsStudy(string jobDef) => jobDef != null && jobDef.IndexOf("Study", StringComparison.OrdinalIgnoreCase) >= 0;
        /// <summary>Work on a mechanoid (repair, recharge, gestation and the like), matched by job name. With Biotech absent there are no such jobs and the specialty simply has no work.</summary>
        public static bool IsMechWork(string jobDef) => jobDef != null && jobDef.IndexOf("Mech", StringComparison.OrdinalIgnoreCase) >= 0 && jobDef.IndexOf("Mechanic", StringComparison.OrdinalIgnoreCase) < 0;

        private static bool Has(string value, params string[] words)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (var w in words) if (value.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>
        /// A written work (an Anomaly book) is the Author's; a xenogerm is the Geneticist's; a drug or a medicine is the Chemist's. Anything else is nobody's.
        /// </summary>
        public static IntelligenceTrack Product(string defName, bool isBook, bool isChemical = false)
        {
            if (isBook || Has(defName, "Novel", "Textbook", "Tome", "Schematic", "Book")) return new IntelligenceTrack { Specialty = "Author", Kind = "intel.book" };
            if (Has(defName, "Xenogerm", "Genepack")) return new IntelligenceTrack { Specialty = "Geneticist", Kind = "intel.xenogerm" };
            if (isChemical) return new IntelligenceTrack { Specialty = "Chemist", Kind = "intel.chemistry" };
            return null;
        }
        public static bool GeneProject(string defName, string label) => Has(defName, "Gene", "Xeno", "Germ") || Has(label, "gene", "xeno", "germ");

        /// <summary>Whether someone is a person from outside the colony to talk with: a guest, a visitor or a prisoner. The Diplomat earns by talking with them.</summary>
        public static bool IsOutsider(bool colonist, bool prisonerOfColony) => !colonist || prisonerOfColony;
    }

    // ------------------------------------------------------------------ what an assignment is worth

    /// <summary>
    /// A specialty is worth something only while the pawn is qualified and assigned to that exact specialty (docs/SPECIALTY-BONUSES.md). All values are bounded.
    ///   Geneticist  +15% research speed on gene projects.        Dark Scholar +15% on Anomaly projects and +20% study efficiency.   Analyst +10% on any project (the generalist).
    ///   Author      the quality of a book rises one band, up to Excellent.      Diplomat +10% negotiation ability and +5% social impact.
    ///   Mech Commander  +1 mech bandwidth (only for a mechanitor).      Chemist  a 10% chance of one extra unit when a drug or medicine bill finishes.
    /// </summary>
    public static class IntelligenceBonuses
    {
        public const float Geneticist = 1.15f, DarkProject = 1.15f, DarkStudy = 1.20f, Analyst = 1.10f, Negotiation = 1.10f, Impact = 1.05f;
        public const float Bandwidth = 1f;
        public const int ChemistExtraPercent = 10;

        public static float ResearchFactor(string specialty, ResearchFacts f)
        {
            if (specialty == null) return 1f;
            switch (specialty)
            {
                case "Geneticist": return f != null && f.Gene ? Geneticist : 1f;
                case "DarkScholar": return f != null && f.Anomaly ? DarkProject : 1f;
                case "Analyst": return Analyst;
                default: return 1f;
            }
        }
        public static float StudyFactor(string specialty) => specialty == "DarkScholar" ? DarkStudy : 1f;

        public const string NegotiationAbility = "NegotiationAbility", SocialImpact = "SocialImpact", MechBandwidth = "MechBandwidth";
        /// <summary>The multiplier a specialty applies to a social stat.</summary>
        public static float SocialFactor(string specialty, string stat) => specialty == "Diplomat" ? (stat == NegotiationAbility ? Negotiation : stat == SocialImpact ? Impact : 1f) : 1f;
        /// <summary>The amount a specialty adds to a stat: mech bandwidth for a Mech Commander.</summary>
        public static float Offset(string specialty, string stat) => specialty == "MechCommander" && stat == MechBandwidth ? Bandwidth : 0f;

        public static int ImprovedQuality(int quality, bool assigned) => assigned && quality < 4 ? quality + 1 : quality;

        /// <summary>
        /// Whether a finished drug or medicine bill yields one more unit. Decided from the job and the pawn alone, so it is the same however often it is asked and needs no random numbers.
        /// </summary>
        public static bool ExtraUnit(int job, int pawn) => (int)(((uint)unchecked(job * 2654435761u + (uint)pawn * 40503u)) % 100u) < ChemistExtraPercent;
    }

    // ------------------------------------------------------------------ rooms

    /// <summary>The kinds of room that suit Intelligence work. They are recognised from what is in them, never painted.</summary>
    public enum IntelligenceRoom : byte { None = 0, Library = 1, ResearchLab = 2, GeneLab = 3 }

    /// <summary>What a room holds, counted by the game side.</summary>
    public sealed class IntelligenceRoomFacts
    {
        public bool Enclosed;
        public int Bookshelves, ResearchBenches, GeneEquipment, Tables;
    }

    public static class IntelligenceRooms
    {
        public const int MinShelves = 2;

        /// <summary>
        /// An enclosed room is a Gene Lab if it holds gene equipment (an extractor, assembler or xenogerm forge), a Research Lab if it holds a research bench, a Library if it has at least
        /// <see cref="MinShelves"/> bookshelves and a table to read at. Gene equipment wins over a bench, which wins over shelves, so a lab with a bookshelf in the corner is still a lab.
        /// </summary>
        public static IntelligenceRoom Classify(IntelligenceRoomFacts f)
        {
            if (f == null || !f.Enclosed) return IntelligenceRoom.None;
            if (f.GeneEquipment > 0) return IntelligenceRoom.GeneLab;
            if (f.ResearchBenches > 0) return IntelligenceRoom.ResearchLab;
            if (f.Bookshelves >= MinShelves && f.Tables > 0) return IntelligenceRoom.Library;
            return IntelligenceRoom.None;
        }

        /// <summary>The room a piece of work suits: research at a bench, a book, a xenogerm.</summary>
        public static IntelligenceRoom Suits(string kind)
        {
            switch (kind)
            {
                case "intel.research": case "intel.darkstudy": case "intel.genetics": return IntelligenceRoom.ResearchLab;
                case "intel.book": return IntelligenceRoom.Library;
                case "intel.xenogerm": return IntelligenceRoom.GeneLab;
                default: return IntelligenceRoom.None;
            }
        }

        /// <summary>The experience a piece of work earns, raised by <see cref="IntelligenceCareers.RoomExperience"/> when it was done in a room that suits it (at least 1 more point).</summary>
        public static int Points(int points, IntelligenceRoom suits, IntelligenceRoom where)
            => suits != IntelligenceRoom.None && suits == where ? Math.Max(points + 1, (int)Math.Round(points * IntelligenceCareers.RoomExperience)) : points;
    }
}
