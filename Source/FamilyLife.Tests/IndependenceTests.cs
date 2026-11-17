using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace FamilyLife.Tests
{
    /// <summary>The community mods depend on the framework (and Harmony) only. No feature may need another mod to work.</summary>
    public class IndependenceTests
    {
        [Fact]
        public void Every_about_file_depends_only_on_the_framework_and_harmony()
        {
            var files = Directory.GetFiles(Path.Combine(RepoPaths.Root(), "Packages"), "About.xml", SearchOption.AllDirectories).Concat(new[] { Path.Combine(RepoPaths.Root(), "About/About.xml") });
            foreach (var f in files)
            {
                var t = File.ReadAllText(f); var deps = Regex.Match(t, "<modDependencies>(.*?)</modDependencies>", RegexOptions.Singleline).Groups[1].Value;
                foreach (Match m in Regex.Matches(deps, "<packageId>([^<]+)</packageId>"))
                    Assert.True(m.Groups[1].Value.StartsWith("SpacemanWil.Wils") || m.Groups[1].Value == "brrainz.harmony" || m.Groups[1].Value.StartsWith("Ludeon.RimWorld"), f + " depends on " + m.Groups[1].Value);
                foreach (Match m in Regex.Matches(t, "<loadAfter>(.*?)</loadAfter>", RegexOptions.Singleline).Cast<Match>().SelectMany(x => Regex.Matches(x.Groups[1].Value, "<li>([^<]+)</li>").Cast<Match>()))
                    Assert.True(m.Groups[1].Value.StartsWith("Ludeon.RimWorld") || m.Groups[1].Value == "brrainz.harmony" || m.Groups[1].Value.StartsWith("SpacemanWil."), f + " loads after " + m.Groups[1].Value);
            }
        }

        // The commerce adapters that stop a shop from being paid twice predate this rule and only guard against double handling; nothing else may name another mod.
        private static readonly string[] Allowed = { "OptionalCommerceHooks.cs", "HospitalityInterop.cs", "BedLocks" };
        private static readonly string[] Forbidden =
        {
            "ColonyRoster", "VSE.", "PrisonerRealism", "CustomPrisonerInteractions", "PrisonCommons", "LivingBonds", "SearchAndRescue", "Storefront", "VendingMachines", "HospitalityCasino", "HospitalitySpa", "ResearchPaper", "YART",
            "VanillaSocialInteractions", "BubblesExpanded", "Jaxe.Bubbles", "RimCourt", "RimMarket", "Achtung"
        };

        [Fact]
        public void No_code_or_data_names_another_mod_except_the_commerce_guards()
        {
            var files = RepoPaths.Roots().SelectMany(r => Directory.GetFiles(r, "*.cs", SearchOption.AllDirectories)).Where(f => !Allowed.Any(a => f.Replace('\\', '/').EndsWith(a))).Concat(RepoPaths.DefAndPatchFiles());
            foreach (var f in files)
            {
                var text = File.ReadAllText(f);
                foreach (var word in Forbidden) Assert.False(text.Contains(word), Path.GetFileName(f) + " names another mod: " + word);
            }
        }
    }
}
