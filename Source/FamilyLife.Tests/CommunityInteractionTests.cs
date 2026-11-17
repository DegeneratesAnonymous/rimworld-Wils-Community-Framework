using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace FamilyLife.Tests
{
    public class CommunityInteractionTests
    {
        [Fact]
        public void There_are_at_least_fifteen_interactions_each_with_text_for_the_bubble_and_no_side_effects()
        {
            var defs = XDocument.Load(Path.Combine(RepoPaths.Root(), "Defs/CommunityInteractions.xml")).Descendants("InteractionDef").ToList();
            Assert.True(defs.Count >= 15, "Interaction Bubbles needs at least 15 interactions; found " + defs.Count);
            Assert.Equal(defs.Count, defs.Select(d => d.Element("defName").Value).Distinct().Count());
            foreach (var d in defs)
            {
                Assert.StartsWith("WFL_", d.Element("defName").Value);
                Assert.True(d.Descendants("li").Count(l => l.Value.StartsWith("r_logentry->")) >= 2, d.Element("defName").Value + " needs at least two phrases");
                Assert.Null(d.Element("initiatorThought")); Assert.Null(d.Element("recipientThought")); Assert.Null(d.Element("socialFightBaseChance"));          // they change no opinions and cause no fights
            }
        }

        [Fact]
        public void Every_interaction_the_code_plays_exists()
        {
            var names = XDocument.Load(Path.Combine(RepoPaths.Root(), "Defs/CommunityInteractions.xml")).Descendants("defName").Select(x => x.Value).ToHashSet();
            var source = string.Concat(RepoPaths.Roots().SelectMany(r => Directory.GetFiles(r, "*.cs", SearchOption.AllDirectories)).Select(File.ReadAllText));
            var played = System.Text.RegularExpressions.Regex.Matches(source, @"\bSay\([^,()]+,[^,()]+,\s*""(WFL_\w+)""\)").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.NotEmpty(played);
            foreach (var name in played) Assert.Contains(name, names);
        }
    }
}
