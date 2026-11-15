using System.IO;
using System.Linq;
using System.Xml.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class IntelligenceTests
    {
        [Fact]
        public void The_tree_is_assistant_to_researcher_to_five_specialties()
        {
            var d = IntelligenceCareers.Definition();
            Assert.Empty(d.Level2Branches); Assert.Equal(new[] { "Analyst", "Author", "DarkScholar", "Geneticist", "Inventor" }, d.Specialties.Keys.OrderBy(x => x).ToArray());
            Assert.Equal(IntelligenceCareers.Specialties.OrderBy(x => x), d.Specialties.Keys.OrderBy(x => x));
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

        [Fact]
        public void Writing_needs_both_the_mind_and_the_craft()
        {
            var d = IntelligenceCareers.Definition(); var l = new CareerLedger(); l.Award(1, d.Id, "Author", "intel", "a", 300, 0);
            l.Evaluate(1, d, k => k == "Artistic" ? 7 : 20); Assert.False(l.IsSpecialist(1, d.Id, "Author"));
            l.Evaluate(1, d, k => 20); Assert.True(l.IsSpecialist(1, d.Id, "Author"));
        }

        [Theory]
        [InlineData(false, false, false, null, "intel.research")]
        [InlineData(false, false, true, "Inventor", "intel.invention")]
        [InlineData(false, true, true, "Geneticist", "intel.genetics")]
        [InlineData(true, false, true, "DarkScholar", "intel.darkstudy")]
        [InlineData(true, true, false, "DarkScholar", "intel.darkstudy")]
        public void Research_is_credited_to_the_discipline_of_the_project(bool anomaly, bool gene, bool industrial, string specialty, string kind)
        {
            var t = IntelligenceClassifier.Research(new ResearchFacts { Anomaly = anomaly, Gene = gene, Industrial = industrial });
            Assert.Equal(specialty, t.Specialty); Assert.Equal(kind, t.Kind);
            Assert.Equal("intel.research", IntelligenceClassifier.Research(null).Kind);
        }

        [Theory]
        [InlineData("Novel", false, "Author")]
        [InlineData("Textbook", false, "Author")]
        [InlineData("Tome", false, "Author")]
        [InlineData("AnythingElse", true, "Author")]
        [InlineData("Xenogerm", false, "Geneticist")]
        [InlineData("Genepack", false, "Geneticist")]
        [InlineData("Steel", false, null)]
        [InlineData(null, false, null)]
        public void Products_are_routed_by_what_they_are(string def, bool book, string specialty) => Assert.Equal(specialty, IntelligenceClassifier.Product(def, book)?.Specialty);

        [Fact]
        public void Scans_and_studies_are_recognised_by_job()
        {
            Assert.True(IntelligenceClassifier.IsScan("OperateScanner")); Assert.False(IntelligenceClassifier.IsScan("Research")); Assert.False(IntelligenceClassifier.IsScan(null));
            Assert.True(IntelligenceClassifier.IsStudy("StudyThing")); Assert.False(IntelligenceClassifier.IsStudy("OperateScanner")); Assert.False(IntelligenceClassifier.IsStudy(null));
            Assert.True(IntelligenceClassifier.GeneProject("Xenogermination", "x")); Assert.True(IntelligenceClassifier.GeneProject("X", "Gene extraction")); Assert.False(IntelligenceClassifier.GeneProject("Electricity", "electricity"));
        }

        [Fact]
        public void Bonuses_belong_to_their_own_specialty_and_are_bounded()
        {
            var industrial = new ResearchFacts { Industrial = true }; var gene = new ResearchFacts { Gene = true, Industrial = true }; var dark = new ResearchFacts { Anomaly = true }; var plain = new ResearchFacts();
            Assert.Equal(1.15f, IntelligenceBonuses.ResearchFactor("Inventor", industrial)); Assert.Equal(1f, IntelligenceBonuses.ResearchFactor("Inventor", plain)); Assert.Equal(1f, IntelligenceBonuses.ResearchFactor("Inventor", dark));
            Assert.Equal(1.15f, IntelligenceBonuses.ResearchFactor("Geneticist", gene)); Assert.Equal(1f, IntelligenceBonuses.ResearchFactor("Geneticist", industrial));
            Assert.Equal(1.15f, IntelligenceBonuses.ResearchFactor("DarkScholar", dark)); Assert.Equal(1f, IntelligenceBonuses.ResearchFactor("DarkScholar", plain));
            Assert.Equal(1.10f, IntelligenceBonuses.ResearchFactor("Analyst", plain)); Assert.Equal(1.10f, IntelligenceBonuses.ResearchFactor("Analyst", dark));
            Assert.Equal(1f, IntelligenceBonuses.ResearchFactor("Author", dark)); Assert.Equal(1f, IntelligenceBonuses.ResearchFactor(null, dark)); Assert.Equal(1f, IntelligenceBonuses.ResearchFactor("Inventor", null));
            Assert.Equal(1.2f, IntelligenceBonuses.StudyFactor("DarkScholar")); Assert.Equal(1f, IntelligenceBonuses.StudyFactor("Inventor"));
            Assert.Equal(3, IntelligenceBonuses.ImprovedQuality(2, true)); Assert.Equal(4, IntelligenceBonuses.ImprovedQuality(4, true)); Assert.Equal(2, IntelligenceBonuses.ImprovedQuality(2, false));
        }

        [Fact]
        public void Every_intelligence_role_has_five_positive_negative_and_relationship_events()
        {
            Assert.Equal(new[] { "Assistant", "Researcher", "Inventor", "Analyst", "Geneticist", "DarkScholar", "Author" }, IntelligenceEvents.Roles);
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
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Packages/WilsIntelligence"), "*.xml", SearchOption.AllDirectories)) XDocument.Load(file);
        }

        [Fact]
        public void The_stat_patch_reaches_research_and_study_and_the_module_has_no_patch_of_its_own()
        {
            string root = RepoPaths.Root(); var doc = XDocument.Load(Path.Combine(root, "Packages/WilsIntelligence/Patches/IntelligenceStats.xml")); string text = doc.ToString();
            Assert.Contains("defName=\"ResearchSpeed\"", text); Assert.Contains("defName=\"StudyEfficiency\"", text); Assert.Contains("FamilyLife.StatPart_IntelligenceSpecialty", text);
            foreach (var op in doc.Root.Elements("Operation")) Assert.Equal("Always", op.Element("success")?.Value);          // StudyEfficiency exists only with Anomaly
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Source/Intelligence"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains("/obj/"))) Assert.DoesNotContain("HarmonyPatch", File.ReadAllText(file));
        }

        [Fact]
        public void Thought_names_of_the_two_modules_never_collide()
        {
            Assert.Empty(IntelligenceEvents.All.Select(e => e.Thought).Intersect(MilitaryEvents.All.Select(e => e.Thought)));
        }
    }
}
