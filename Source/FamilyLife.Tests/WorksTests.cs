using System.IO;
using System.Linq;
using System.Xml.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class WorksTests
    {
        [Fact]
        public void There_are_two_careers_each_with_its_three_specialties_and_no_surveyor_or_driller()
        {
            var b = WorksCareers.BuilderDefinition(); var m = WorksCareers.MinerDefinition();
            Assert.Equal(new[] { "Carpenter", "Engineer", "Mason" }, b.Specialties.Keys.OrderBy(x => x).ToArray()); Assert.Equal(new[] { "Prospector", "Quarryman", "Tunneller" }, m.Specialties.Keys.OrderBy(x => x).ToArray());
            Assert.Equal(WorksCareers.BuilderSpecialties.OrderBy(x => x), b.Specialties.Keys.OrderBy(x => x)); Assert.Equal(WorksCareers.MinerSpecialties.OrderBy(x => x), m.Specialties.Keys.OrderBy(x => x));
            Assert.NotEqual(b.Id, m.Id); Assert.Equal(WorksCareers.Module, b.ModuleId); Assert.Equal(WorksCareers.Module, m.ModuleId);
            Assert.DoesNotContain("Surveyor", WorksEvents.Roles); Assert.DoesNotContain("DeepDriller", WorksEvents.Roles); Assert.Empty(b.Level2Branches); Assert.Empty(m.Level2Branches);
            Assert.Equal(WorksCareers.Builder, WorksCareers.CareerOf("Mason")); Assert.Equal(WorksCareers.Miner, WorksCareers.CareerOf("Tunneller")); Assert.Null(WorksCareers.CareerOf("Smith"));
        }

        [Theory]
        [InlineData("Builder", "Mason", "Construction", 8)]
        [InlineData("Builder", "Carpenter", "Crafting", 4)]
        [InlineData("Builder", "Engineer", "Intellectual", 6)]
        [InlineData("Miner", "Prospector", "Intellectual", 4)]
        [InlineData("Miner", "Quarryman", "Mining", 8)]
        [InlineData("Miner", "Tunneller", "Mining", 10)]
        public void Each_specialty_needs_its_skills_and_a_journeyman_first(string career, string specialty, string skill, int need)
        {
            var d = career == "Builder" ? WorksCareers.BuilderDefinition() : WorksCareers.MinerDefinition(); string main = career == "Builder" ? "Construction" : "Mining";
            var l = new CareerLedger(); l.Award(1, d.Id, specialty, "w", "a", 400, 0);
            l.Evaluate(1, d, k => k == skill ? need - 1 : 20); Assert.False(l.IsSpecialist(1, d.Id, specialty));
            l.Evaluate(1, d, k => 20); Assert.True(l.IsSpecialist(1, d.Id, specialty));
            var low = new CareerLedger(); low.Award(2, d.Id, specialty, "w", "a", 400, 0); low.Evaluate(2, d, k => k == main ? 4 : 20); Assert.False(low.IsQualified(2, d.Id)); Assert.False(low.IsSpecialist(2, d.Id, specialty));          // skill 5 makes a Journeyman
        }

        [Fact]
        public void The_two_careers_are_independent_in_the_ledger()
        {
            var b = WorksCareers.BuilderDefinition(); var m = WorksCareers.MinerDefinition(); var l = new CareerLedger();
            l.Award(1, b.Id, "Mason", "w", "a", 400, 0); l.Evaluate(1, b, k => 20); l.Evaluate(1, m, k => 20);
            Assert.True(l.IsSpecialist(1, b.Id, "Mason")); Assert.False(l.IsQualified(1, m.Id)); Assert.False(l.IsSpecialist(1, m.Id, "Quarryman"));
        }

        [Fact]
        public void Every_role_has_fifteen_events_and_a_thought_for_each_with_the_right_sign()
        {
            Assert.Equal(10, WorksEvents.Roles.Length); Assert.Equal(150, WorksEvents.All.Length);
            var defs = XDocument.Load(Path.Combine(RepoPaths.Root(), "Packages/WilsWorks/Defs/WorksThoughts.xml")).Descendants("ThoughtDef").ToDictionary(x => x.Element("defName").Value);
            foreach (var e in WorksEvents.All)
            {
                Assert.True(defs.ContainsKey(e.Thought), e.Thought); var stage = defs[e.Thought].Descendants("li").First();
                if (e.Opinion != 0) Assert.Equal(e.Opinion, int.Parse(stage.Element("baseOpinionOffset").Value)); else Assert.Equal(e.Mood, int.Parse(stage.Element("baseMoodEffect").Value));
            }
            Assert.Equal(defs.Count, WorksEvents.All.Length);
        }

        [Fact]
        public void Works_thoughts_never_collide_with_the_other_role_modules()
        {
            var all = IntelligenceEvents.All.Select(e => e.Thought).Concat(MilitaryEvents.All.Select(e => e.Thought)).Concat(WardenEvents.All.Select(e => e.Thought)).Concat(WorksEvents.All.Select(e => e.Thought)).ToList();
            Assert.Equal(all.Count, all.Distinct().Count());
        }

        [Fact]
        public void The_module_registers_both_careers_and_uses_only_the_framework()
        {
            string code = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Works/Game/WorksModule.cs"));
            Assert.Contains("RegisterCareer(WorksCareers.BuilderDefinition())", code); Assert.Contains("RegisterCareer(WorksCareers.MinerDefinition())", code);
            foreach (var file in Directory.GetFiles(Path.Combine(RepoPaths.Root(), "Source/Works"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Replace('\\', '/').Contains("/obj/"))) Assert.DoesNotContain("HarmonyPatch", File.ReadAllText(file));
            string csproj = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Works/WilsWorks.csproj")); Assert.Contains("<AssemblyName>WilsWorks</AssemblyName>", csproj); Assert.DoesNotContain("WilsWardens", csproj);
        }
    }
}
