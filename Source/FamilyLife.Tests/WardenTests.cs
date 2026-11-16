using System.IO;
using System.Linq;
using System.Xml.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class WardenTests
    {
        [Fact]
        public void The_tree_is_guard_to_warden_to_three_specialties_and_there_is_no_persuader()
        {
            var d = WardenCareers.Definition();
            Assert.Empty(d.Level2Branches); Assert.Equal(new[] { "Confessor", "ContainmentKeeper", "Overseer" }, d.Specialties.Keys.OrderBy(x => x).ToArray());
            Assert.Equal(WardenCareers.Specialties.OrderBy(x => x), d.Specialties.Keys.OrderBy(x => x)); Assert.DoesNotContain("Persuader", d.Specialties.Keys); Assert.DoesNotContain("Persuader", WardenEvents.Roles);
            Assert.Equal(6, d.Level2.Skills["Social"]); Assert.Equal(120, d.Level2.GeneralXp);
        }

        [Theory]
        [InlineData("Confessor", "Intellectual", 6)]
        [InlineData("Overseer", "Melee", 6)]
        [InlineData("ContainmentKeeper", "Melee", 6)]
        [InlineData("ContainmentKeeper", "Intellectual", 8)]
        [InlineData("Confessor", "Social", 8)]
        public void Each_specialty_needs_its_skills_and_a_warden_first(string specialty, string skill, int need)
        {
            var d = WardenCareers.Definition(); var l = new CareerLedger(); l.Award(1, d.Id, specialty, "ward", "a", 400, 0);
            l.Evaluate(1, d, k => k == skill ? need - 1 : 20); Assert.False(l.IsSpecialist(1, d.Id, specialty));
            l.Evaluate(1, d, k => 20); Assert.True(l.IsSpecialist(1, d.Id, specialty));
            var low = new CareerLedger(); low.Award(2, d.Id, specialty, "ward", "a", 400, 0); low.Evaluate(2, d, k => k == "Social" ? 5 : 20); Assert.False(low.IsQualified(2, d.Id)); Assert.False(low.IsSpecialist(2, d.Id, specialty));          // Social 6 makes a Warden
        }

        [Theory]
        [InlineData("PrisonerAttemptConvert", "Warden", "Confessor", "ward.convert")]
        [InlineData("SuppressSlave", "Warden", "Overseer", "ward.oversee")]
        [InlineData("FeedHoldingPlatform", "Warden", "ContainmentKeeper", "ward.contain")]
        [InlineData("PrisonerAttemptRecruit", "Warden", null, "ward.warden")]
        [InlineData("DeliverFood", "Warden", null, "ward.warden")]
        [InlineData("AnythingElse", "Warden", null, "ward.warden")]          // any job under the Warden work type is warden work
        [InlineData("PrisonerChat", null, null, "ward.warden")]
        public void Warden_work_is_routed_by_what_it_is(string job, string workType, string specialty, string kind)
        {
            var t = WardenClassifier.Classify(job, workType); Assert.Equal(specialty, t.Specialty); Assert.Equal(kind, t.Kind); Assert.True(t.Points > 0);
        }

        [Fact]
        public void Work_that_is_not_warden_work_earns_nothing()
        {
            Assert.Null(WardenClassifier.Classify("Research", "Research")); Assert.Null(WardenClassifier.Classify(null, null)); Assert.Null(WardenClassifier.Classify("Cook", "Cooking"));
        }

        [Fact]
        public void The_specialty_jobs_are_worth_more_than_plain_warden_work_and_bounded()
        {
            Assert.True(WardenCareers.ConvertPoints > WardenCareers.WardenPoints); Assert.True(WardenCareers.OverseePoints > WardenCareers.WardenPoints); Assert.True(WardenCareers.ContainPoints > WardenCareers.WardenPoints);
            Assert.InRange(WardenCareers.ConvertPoints, 1, 20);
        }

        [Fact]
        public void Each_bonus_is_on_its_own_stat_for_its_own_specialty_only()
        {
            Assert.Equal(1.15f, WardenBonuses.Factor("Confessor", WardenBonuses.ConversionPower)); Assert.Equal(1.15f, WardenBonuses.Factor("Overseer", WardenBonuses.SuppressionPower)); Assert.Equal(1.10f, WardenBonuses.Factor("ContainmentKeeper", WardenBonuses.StudyEfficiency));
            foreach (var stat in new[] { WardenBonuses.ConversionPower, WardenBonuses.SuppressionPower, WardenBonuses.StudyEfficiency, "Other", null })
            {
                Assert.Equal(1f, WardenBonuses.Factor("Warden", stat)); Assert.Equal(1f, WardenBonuses.Factor("Guard", stat)); Assert.Equal(1f, WardenBonuses.Factor(null, stat));          // the plain Warden has no bonus
            }
            Assert.Equal(1f, WardenBonuses.Factor("Confessor", WardenBonuses.SuppressionPower)); Assert.Equal(1f, WardenBonuses.Factor("Overseer", WardenBonuses.StudyEfficiency)); Assert.Equal(1f, WardenBonuses.Factor("ContainmentKeeper", WardenBonuses.ConversionPower));
        }

        [Fact]
        public void Every_warden_role_has_five_positive_negative_and_relationship_events()
        {
            Assert.Equal(new[] { "Guard", "Warden", "Confessor", "Overseer", "ContainmentKeeper" }, WardenEvents.Roles);
            foreach (var role in WardenEvents.Roles) { var e = WardenEvents.All.Where(x => x.Role == role).ToList(); Assert.Equal(5, e.Count(x => x.Mood > 0)); Assert.Equal(5, e.Count(x => x.Mood < 0)); Assert.Equal(5, e.Count(x => x.Opinion != 0)); Assert.Equal(15, e.Select(x => x.Fact).Distinct().Count()); }
            Assert.Equal(75, WardenEvents.All.Length); Assert.Equal(75, WardenEvents.All.Select(x => x.Id).Distinct().Count()); Assert.Equal(75, WardenEvents.All.Select(x => x.Thought).Distinct().Count());
        }

        [Fact]
        public void Every_event_has_a_thought_every_role_a_name_and_every_string_the_code_uses_exists()
        {
            string root = RepoPaths.Root();
            var names = XDocument.Load(Path.Combine(root, "Packages/WilsWardens/Defs/WardenThoughts.xml")).Descendants("defName").Select(x => x.Value).ToList();
            Assert.Equal(WardenEvents.All.Length, names.Count); foreach (var e in WardenEvents.All) Assert.Contains(e.Thought, names);
            var keys = XDocument.Load(Path.Combine(root, "Packages/WilsWardens/Languages/English/Keyed/Wardens.xml")).Root.Elements().Select(x => x.Name.LocalName).ToHashSet();
            Assert.Contains("WFL_Wardens_Career", keys);
            foreach (var role in WardenEvents.Roles) Assert.Contains("wils.wardens." + role.ToLowerInvariant() + "_label", keys);
            foreach (var s in WardenCareers.Specialties) Assert.Contains("WFL_Wardens_" + s + "Bonus", keys);
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Source/Wardens"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Replace('\\', '/').Contains("/obj/")))
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), "\"(WFL_Wardens_[A-Za-z]+)\"")) Assert.True(keys.Contains(m.Groups[1].Value), Path.GetFileName(file) + " uses a missing key " + m.Groups[1].Value);
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Packages/WilsWardens"), "*.xml", SearchOption.AllDirectories)) XDocument.Load(file);
        }

        [Fact]
        public void The_stat_patch_reaches_the_three_stats_cannot_raise_an_error_and_the_module_has_no_patch_of_its_own()
        {
            string root = RepoPaths.Root(); var doc = XDocument.Load(Path.Combine(root, "Packages/WilsWardens/Patches/WardenStats.xml")); string text = doc.ToString();
            foreach (var stat in new[] { "ConversionPower", "SuppressionPower", "StudyEfficiency" }) Assert.Contains("defName=\"" + stat + "\"", text);
            Assert.Contains("FamilyLife.StatPart_WardenSpecialty", text); foreach (var op in doc.Root.Elements("Operation")) Assert.Equal("Always", op.Element("success")?.Value);
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Source/Wardens"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Replace('\\', '/').Contains("/obj/"))) Assert.DoesNotContain("HarmonyPatch", File.ReadAllText(file));
        }

        [Fact]
        public void The_three_modules_event_thoughts_never_collide()
        {
            var all = IntelligenceEvents.All.Select(e => e.Thought).Concat(MilitaryEvents.All.Select(e => e.Thought)).Concat(WardenEvents.All.Select(e => e.Thought)).ToList();
            Assert.Equal(all.Count, all.Distinct().Count());
        }
    }
}
