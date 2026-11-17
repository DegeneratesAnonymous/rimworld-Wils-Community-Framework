using System.IO;
using System.Linq;
using System.Xml.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class IntelligenceTests
    {
        private static readonly string[] Seven = { "Analyst", "Author", "Chemist", "DarkScholar", "Diplomat", "Geneticist", "MechCommander" };

        [Fact]
        public void The_tree_is_assistant_to_researcher_to_seven_specialties_and_the_inventor_is_gone()
        {
            var d = IntelligenceCareers.Definition();
            Assert.Empty(d.Level2Branches); Assert.Equal(Seven, d.Specialties.Keys.OrderBy(x => x).ToArray());
            Assert.Equal(IntelligenceCareers.Specialties.OrderBy(x => x), d.Specialties.Keys.OrderBy(x => x));
            Assert.DoesNotContain("Inventor", d.Specialties.Keys); Assert.DoesNotContain("Inventor", IntelligenceEvents.Roles);
            Assert.Equal(6, d.Level2.Skills["Intellectual"]);
        }

        [Fact]
        public void A_specialty_needs_the_researcher_step_first_and_its_own_skills()
        {
            var d = IntelligenceCareers.Definition(); var l = new CareerLedger();
            // all the experience in the world, but Intellectual 5 is below the Researcher bar: no Researcher, so no specialty either
            l.Award(1, d.Id, "Geneticist", "intel", "a", 300, 0); l.Evaluate(1, d, k => k == "Intellectual" ? 5 : 20);
            Assert.False(l.IsQualified(1, d.Id)); Assert.False(l.IsSpecialist(1, d.Id, "Geneticist"));
            // skilled enough: Researcher is earned, and Geneticist with it
            l = new CareerLedger(); l.Award(2, d.Id, "Geneticist", "intel", "a", 300, 0); l.Evaluate(2, d, k => 20);
            Assert.True(l.IsQualified(2, d.Id)); Assert.True(l.IsSpecialist(2, d.Id, "Geneticist"));
            // medicine is part of the genetics bar (docs/CAREERS.md: Medicine 10 and Intellectual 10)
            l = new CareerLedger(); l.Award(3, d.Id, "Geneticist", "intel", "a", 300, 0); l.Evaluate(3, d, k => k == "Medicine" ? 9 : 20);
            Assert.True(l.IsQualified(3, d.Id)); Assert.False(l.IsSpecialist(3, d.Id, "Geneticist"));
        }

        [Theory]
        [InlineData("Author", "Artistic", 8)]
        [InlineData("Diplomat", "Social", 8)]
        [InlineData("MechCommander", "Crafting", 6)]
        [InlineData("Chemist", "Medicine", 4)]
        [InlineData("Analyst", "Social", 5)]
        public void Each_specialty_has_a_second_skill_that_it_will_not_do_without(string specialty, string skill, int need)
        {
            var d = IntelligenceCareers.Definition(); var l = new CareerLedger(); l.Award(1, d.Id, specialty, "intel", "a", 400, 0);
            l.Evaluate(1, d, k => k == skill ? need - 1 : 20); Assert.False(l.IsSpecialist(1, d.Id, specialty));
            l.Evaluate(1, d, k => 20); Assert.True(l.IsSpecialist(1, d.Id, specialty));
        }

        [Fact]
        public void Research_is_credited_to_the_discipline_of_the_project()
        {
            Assert.Equal("intel.research", IntelligenceClassifier.Research(new ResearchFacts()).Kind); Assert.Null(IntelligenceClassifier.Research(new ResearchFacts()).Specialty);
            Assert.Equal("Geneticist", IntelligenceClassifier.Research(new ResearchFacts { Gene = true }).Specialty);
            Assert.Equal("DarkScholar", IntelligenceClassifier.Research(new ResearchFacts { Anomaly = true }).Specialty);
            Assert.Equal("DarkScholar", IntelligenceClassifier.Research(new ResearchFacts { Anomaly = true, Gene = true }).Specialty);          // Anomaly is its own discipline
            Assert.Equal("intel.research", IntelligenceClassifier.Research(null).Kind);
        }

        [Theory]
        [InlineData("Novel", false, false, "Author")]
        [InlineData("Textbook", false, false, "Author")]
        [InlineData("Tome", false, false, "Author")]
        [InlineData("AnythingElse", true, false, "Author")]
        [InlineData("Xenogerm", false, false, "Geneticist")]
        [InlineData("Genepack", false, false, "Geneticist")]
        [InlineData("Flake", false, true, "Chemist")]
        [InlineData("MedicineHerbal", false, true, "Chemist")]
        [InlineData("Steel", false, false, null)]
        [InlineData(null, false, false, null)]
        [InlineData("Xenogerm", false, true, "Geneticist")]          // a xenogerm is the Geneticist's even though it is a chemical-looking item
        public void Products_are_routed_by_what_they_are(string def, bool book, bool chemical, string specialty) => Assert.Equal(specialty, IntelligenceClassifier.Product(def, book, chemical)?.Specialty);

        [Fact]
        public void Scans_studies_and_mech_work_are_recognised_by_job()
        {
            Assert.True(IntelligenceClassifier.IsScan("OperateScanner")); Assert.False(IntelligenceClassifier.IsScan("Research")); Assert.False(IntelligenceClassifier.IsScan(null));
            Assert.True(IntelligenceClassifier.IsStudy("StudyThing")); Assert.False(IntelligenceClassifier.IsStudy("OperateScanner")); Assert.False(IntelligenceClassifier.IsStudy(null));
            Assert.True(IntelligenceClassifier.IsMechWork("RepairMech")); Assert.True(IntelligenceClassifier.IsMechWork("MechCharge")); Assert.False(IntelligenceClassifier.IsMechWork("Research")); Assert.False(IntelligenceClassifier.IsMechWork(null)); Assert.False(IntelligenceClassifier.IsMechWork("Mechanic"));
            Assert.True(IntelligenceClassifier.GeneProject("Xenogermination", "x")); Assert.True(IntelligenceClassifier.GeneProject("X", "Gene extraction")); Assert.False(IntelligenceClassifier.GeneProject("Electricity", "electricity"));
        }

        [Fact]
        public void The_diplomat_earns_from_outsiders_only()
        {
            Assert.True(IntelligenceClassifier.IsOutsider(false, false)); Assert.True(IntelligenceClassifier.IsOutsider(false, true)); Assert.True(IntelligenceClassifier.IsOutsider(true, true));
            Assert.False(IntelligenceClassifier.IsOutsider(true, false));          // a fellow colonist is not an outsider
        }

        [Fact]
        public void Bonuses_belong_to_their_own_specialty_and_are_bounded()
        {
            var gene = new ResearchFacts { Gene = true }; var dark = new ResearchFacts { Anomaly = true }; var plain = new ResearchFacts();
            Assert.Equal(1.15f, IntelligenceBonuses.ResearchFactor("Geneticist", gene)); Assert.Equal(1f, IntelligenceBonuses.ResearchFactor("Geneticist", plain));
            Assert.Equal(1.15f, IntelligenceBonuses.ResearchFactor("DarkScholar", dark)); Assert.Equal(1f, IntelligenceBonuses.ResearchFactor("DarkScholar", plain));
            Assert.Equal(1.10f, IntelligenceBonuses.ResearchFactor("Analyst", plain)); Assert.Equal(1.10f, IntelligenceBonuses.ResearchFactor("Analyst", dark));
            foreach (var other in new[] { "Author", "Diplomat", "Chemist", "MechCommander", null }) Assert.Equal(1f, IntelligenceBonuses.ResearchFactor(other, dark));
            Assert.Equal(1f, IntelligenceBonuses.ResearchFactor("Geneticist", null));
            Assert.Equal(1.2f, IntelligenceBonuses.StudyFactor("DarkScholar")); Assert.Equal(1f, IntelligenceBonuses.StudyFactor("Analyst"));
            Assert.Equal(3, IntelligenceBonuses.ImprovedQuality(2, true)); Assert.Equal(4, IntelligenceBonuses.ImprovedQuality(4, true)); Assert.Equal(2, IntelligenceBonuses.ImprovedQuality(2, false));
        }

        [Fact]
        public void The_diplomat_and_the_mech_commander_change_only_their_own_stats()
        {
            Assert.Equal(1.10f, IntelligenceBonuses.SocialFactor("Diplomat", IntelligenceBonuses.NegotiationAbility)); Assert.Equal(1.05f, IntelligenceBonuses.SocialFactor("Diplomat", IntelligenceBonuses.SocialImpact));
            Assert.Equal(1f, IntelligenceBonuses.SocialFactor("Diplomat", "ResearchSpeed")); Assert.Equal(1f, IntelligenceBonuses.SocialFactor("Analyst", IntelligenceBonuses.NegotiationAbility)); Assert.Equal(1f, IntelligenceBonuses.SocialFactor(null, IntelligenceBonuses.SocialImpact));
            Assert.Equal(1f, IntelligenceBonuses.Offset("MechCommander", IntelligenceBonuses.MechBandwidth)); Assert.Equal(0f, IntelligenceBonuses.Offset("MechCommander", "ResearchSpeed")); Assert.Equal(0f, IntelligenceBonuses.Offset("Diplomat", IntelligenceBonuses.MechBandwidth)); Assert.Equal(0f, IntelligenceBonuses.Offset(null, IntelligenceBonuses.MechBandwidth));
        }

        [Fact]
        public void The_chemists_extra_unit_is_about_one_in_ten_and_always_the_same_for_the_same_job()
        {
            int hits = 0, n = 20000; for (int job = 1; job <= n; job++) if (IntelligenceBonuses.ExtraUnit(job, 123)) hits++;
            Assert.InRange(hits / (double)n, .08, .12);          // 10%, give or take
            for (int job = 1; job < 500; job++) Assert.Equal(IntelligenceBonuses.ExtraUnit(job, 7), IntelligenceBonuses.ExtraUnit(job, 7));
            Assert.Contains(Enumerable.Range(1, 200).Select(j => IntelligenceBonuses.ExtraUnit(j, 1)), x => x); Assert.Contains(Enumerable.Range(1, 200).Select(j => IntelligenceBonuses.ExtraUnit(j, 1)), x => !x);
            Assert.False(Enumerable.Range(1, 200).All(j => IntelligenceBonuses.ExtraUnit(j, 1) == IntelligenceBonuses.ExtraUnit(j, 2)));          // different pawns, different luck
            Assert.True(IntelligenceBonuses.ChemistExtraPercent <= 10);
        }

        // ------------------------------------------------------------------ rooms

        private static IntelligenceRoomFacts Room(bool enclosed = true, int shelves = 0, int benches = 0, int gene = 0, int tables = 0) => new IntelligenceRoomFacts { Enclosed = enclosed, Bookshelves = shelves, ResearchBenches = benches, GeneEquipment = gene, Tables = tables };

        [Fact]
        public void Rooms_are_recognised_from_what_is_in_them()
        {
            Assert.Equal(IntelligenceRoom.Library, IntelligenceRooms.Classify(Room(shelves: 2, tables: 1)));
            Assert.Equal(IntelligenceRoom.None, IntelligenceRooms.Classify(Room(shelves: 1, tables: 1)));          // one shelf is a shelf
            Assert.Equal(IntelligenceRoom.None, IntelligenceRooms.Classify(Room(shelves: 4, tables: 0)));          // nowhere to read
            Assert.Equal(IntelligenceRoom.ResearchLab, IntelligenceRooms.Classify(Room(benches: 1)));
            Assert.Equal(IntelligenceRoom.GeneLab, IntelligenceRooms.Classify(Room(gene: 1)));
            Assert.Equal(IntelligenceRoom.None, IntelligenceRooms.Classify(Room(enclosed: false, benches: 2, gene: 1, shelves: 5, tables: 2)));          // a room with no walls is no room
            Assert.Equal(IntelligenceRoom.None, IntelligenceRooms.Classify(null)); Assert.Equal(IntelligenceRoom.None, IntelligenceRooms.Classify(Room()));
        }

        [Fact]
        public void A_lab_with_a_bookshelf_is_still_a_lab_and_gene_equipment_beats_a_bench()
        {
            Assert.Equal(IntelligenceRoom.ResearchLab, IntelligenceRooms.Classify(Room(benches: 1, shelves: 6, tables: 3)));
            Assert.Equal(IntelligenceRoom.GeneLab, IntelligenceRooms.Classify(Room(benches: 2, gene: 1, shelves: 6, tables: 3)));
        }

        [Fact]
        public void Work_earns_more_only_in_the_room_that_suits_it()
        {
            Assert.Equal(IntelligenceRoom.ResearchLab, IntelligenceRooms.Suits("intel.research")); Assert.Equal(IntelligenceRoom.ResearchLab, IntelligenceRooms.Suits("intel.genetics")); Assert.Equal(IntelligenceRoom.ResearchLab, IntelligenceRooms.Suits("intel.darkstudy"));
            Assert.Equal(IntelligenceRoom.Library, IntelligenceRooms.Suits("intel.book")); Assert.Equal(IntelligenceRoom.GeneLab, IntelligenceRooms.Suits("intel.xenogerm"));
            foreach (var kind in new[] { "intel.scan", "intel.study", "intel.mech", "intel.talk", "intel.chemistry", null }) Assert.Equal(IntelligenceRoom.None, IntelligenceRooms.Suits(kind));
            Assert.Equal(5, IntelligenceRooms.Points(4, IntelligenceRoom.ResearchLab, IntelligenceRoom.ResearchLab)); Assert.Equal(15, IntelligenceRooms.Points(12, IntelligenceRoom.Library, IntelligenceRoom.Library));
            Assert.Equal(4, IntelligenceRooms.Points(4, IntelligenceRoom.ResearchLab, IntelligenceRoom.Library)); Assert.Equal(4, IntelligenceRooms.Points(4, IntelligenceRoom.ResearchLab, IntelligenceRoom.None));
            Assert.Equal(4, IntelligenceRooms.Points(4, IntelligenceRoom.None, IntelligenceRoom.None));          // work that suits no room gets nothing from being in one
            Assert.Equal(1, IntelligenceRooms.Points(0, IntelligenceRoom.Library, IntelligenceRoom.Library) - 0);          // never a loss
            for (int p = 1; p < 100; p++) Assert.InRange(IntelligenceRooms.Points(p, IntelligenceRoom.Library, IntelligenceRoom.Library), p + 1, (int)(p * 1.3f) + 2);
        }

        // ------------------------------------------------------------------ events and packaging

        [Fact]
        public void Every_intelligence_role_has_five_positive_negative_and_relationship_events()
        {
            Assert.Equal(new[] { "Assistant", "Researcher", "Analyst", "Geneticist", "DarkScholar", "Author", "MechCommander", "Diplomat", "Chemist" }, IntelligenceEvents.Roles);
            foreach (var role in IntelligenceEvents.Roles) { var e = IntelligenceEvents.All.Where(x => x.Role == role).ToList(); Assert.Equal(5, e.Count(x => x.Mood > 0)); Assert.Equal(5, e.Count(x => x.Mood < 0)); Assert.Equal(5, e.Count(x => x.Opinion != 0)); Assert.Equal(15, e.Select(x => x.Fact).Distinct().Count()); }
            Assert.Equal(IntelligenceEvents.All.Length, IntelligenceEvents.All.Select(x => x.Id).Distinct().Count());
            Assert.Equal(IntelligenceEvents.All.Length, IntelligenceEvents.All.Select(x => x.Thought).Distinct().Count());
        }

        [Fact]
        public void Every_event_has_a_thought_and_every_role_a_name_and_the_specialties_a_bonus_text()
        {
            string root = RepoPaths.Root();
            var names = XDocument.Load(Path.Combine(root, "Packages/WilsIntelligence/Defs/IntelligenceThoughts.xml")).Descendants("defName").Select(x => x.Value).ToList();
            Assert.Equal(IntelligenceEvents.All.Length, names.Count);
            foreach (var e in IntelligenceEvents.All) Assert.Contains(e.Thought, names);
            var keys = XDocument.Load(Path.Combine(root, "Packages/WilsIntelligence/Languages/English/Keyed/Intelligence.xml")).Root.Elements().Select(x => x.Name.LocalName).ToList();
            Assert.Contains("WFL_Intelligence_Career", keys);
            foreach (var role in IntelligenceEvents.Roles) Assert.Contains("wils.intelligence." + role.ToLowerInvariant() + "_label", keys);
            foreach (var s in IntelligenceCareers.Specialties) Assert.Contains("WFL_Intelligence_" + s + "Bonus", keys);
            Assert.DoesNotContain("wils.intelligence.inventor_label", keys); Assert.DoesNotContain("WFL_Intelligence_InventorBonus", keys);
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Packages/WilsIntelligence"), "*.xml", SearchOption.AllDirectories)) XDocument.Load(file);
        }

        [Fact]
        public void Every_string_key_the_intelligence_code_uses_exists()
        {
            string root = RepoPaths.Root();
            var keys = XDocument.Load(Path.Combine(root, "Packages/WilsIntelligence/Languages/English/Keyed/Intelligence.xml")).Root.Elements().Select(x => x.Name.LocalName).ToHashSet();
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Source/Intelligence"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Replace('\\', '/').Contains("/obj/")))
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), "\"(WFL_Intelligence_[A-Za-z]+)\""))
                    Assert.True(keys.Contains(m.Groups[1].Value), Path.GetFileName(file) + " uses a missing key " + m.Groups[1].Value);
        }

        [Fact]
        public void The_stat_patch_reaches_every_stat_a_specialty_changes_and_the_module_has_no_patch_of_its_own()
        {
            string root = RepoPaths.Root(); var doc = XDocument.Load(Path.Combine(root, "Packages/WilsIntelligence/Patches/IntelligenceStats.xml")); string text = doc.ToString();
            foreach (var stat in new[] { "ResearchSpeed", "StudyEfficiency", "NegotiationAbility", "SocialImpact", "MechBandwidth" }) Assert.Contains("defName=\"" + stat + "\"", text);
            Assert.Contains("FamilyLife.StatPart_IntelligenceSpecialty", text);
            foreach (var op in doc.Root.Elements("Operation")) Assert.Equal("Always", op.Element("success")?.Value);          // StudyEfficiency exists only with Anomaly, MechBandwidth only with Biotech
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Source/Intelligence"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains("/obj/"))) Assert.DoesNotContain("HarmonyPatch", File.ReadAllText(file));
        }

        [Fact]
        public void The_room_and_page_code_exists_and_the_page_is_registered()
        {
            string code = string.Join("\n", Directory.GetFiles(Path.Combine(RepoPaths.Root(), "Source/Intelligence"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Replace('\\', '/').Contains("/obj/")).Select(File.ReadAllText));
            Assert.Contains("class IntelligenceRoomGame", code); Assert.Contains("SciencesPage.Draw", code); Assert.Contains("IntelligenceWork.Talked", code);
            Assert.Contains("pawn.mechanitor == null", code);          // bandwidth means something only to a mechanitor
        }

        [Fact]
        public void Thought_names_of_the_two_modules_never_collide()
        {
            Assert.Empty(IntelligenceEvents.All.Select(e => e.Thought).Intersect(MilitaryEvents.All.Select(e => e.Thought)));
        }
    }
}
