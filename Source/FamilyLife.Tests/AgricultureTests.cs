using System.IO;
using System.Linq;
using System.Xml.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class AgricultureTests
    {
        [Fact] public void Career_has_three_entry_routes_and_nine_specialties()
        {
            var d=AgricultureCareers.Definition();
            Assert.Equal(new[]{"Farmer","Hunter","Rancher"},d.Level2Branches.Keys.OrderBy(x=>x).ToArray());
            Assert.Equal(9,d.Specialties.Count);Assert.Contains("Farmer",d.Specialties["Horticulturist"].RequiresAny);Assert.Contains("Hunter",d.Specialties["BigGameHunter"].RequiresAny);Assert.Contains("Rancher",d.Specialties["Beastmaster"].RequiresAny);
        }

        [Fact] public void Tracker_auto_choice_is_conservative_but_rules_do_not_govern_manual_orders()
        {
            var p=new HuntingPolicy{Automatic=true,AllowMedium=true,AllowLarge=true,AllowPredators=true,MaxRevengePercent=50,MinimumPopulation=1};
            var small=new HuntTargetFacts{Id=1,Size=GameSize.Small,Population=5,Distance=20,Meat=10};
            var medium=new HuntTargetFacts{Id=2,Size=GameSize.Medium,Population=5,Distance=20,Meat=30};
            var dangerous=new HuntTargetFacts{Id=3,Size=GameSize.Large,Population=5,Distance=20,Meat=100,Predator=true,RevengePercent=10};
            Assert.True(HuntingRules.Eligible(small,p,HunterCapability.Tracker));Assert.False(HuntingRules.Eligible(medium,p,HunterCapability.Tracker));Assert.False(HuntingRules.Eligible(dangerous,p,HunterCapability.Tracker));
            Assert.True(HuntingRules.Eligible(medium,p,HunterCapability.Hunter));Assert.True(HuntingRules.Eligible(dangerous,p,HunterCapability.BigGameHunter));
            // These rules are called only by automatic selection. Existing/player designations are deliberately outside this API.
            Assert.False(HuntingRules.Eligible(new HuntTargetFacts{Id=4,Size=GameSize.Small,Population=5,AlreadyDesignated=true},p,HunterCapability.Tracker));
        }

        [Fact] public void Hunting_policy_round_trips_its_management_and_owned_targets()
        {
            var p=new HuntingPolicy{Automatic=true,MeatTarget=500,MaxDistance=80,MaxRevengePercent=12,MinimumPopulation=4,TargetsPerHunter=2,AllowLarge=true,AllowPredators=true};p.AutomaticTargets.AddRange(new[]{8,3});
            var back=HuntingPolicy.Decode(new StateReader(p.Encode()));
            Assert.True(back.Automatic);Assert.Equal(500,back.MeatTarget);Assert.Equal(80,back.MaxDistance);Assert.Equal(12,back.MaxRevengePercent);Assert.Equal(4,back.MinimumPopulation);Assert.Equal(2,back.TargetsPerHunter);Assert.True(back.AllowLarge);Assert.True(back.AllowPredators);Assert.Equal(new[]{8,3},back.AutomaticTargets);
        }

        [Fact] public void Agriculture_package_xml_is_well_formed_and_named()
        {
            string root=Path.Combine(RepoPaths.Root(),"Packages","WilsAgriculture");
            foreach(var file in Directory.GetFiles(root,"*.xml",SearchOption.AllDirectories))XDocument.Load(file);
            string about=File.ReadAllText(Path.Combine(root,"About","About.xml"));Assert.Contains("<name>Wil's Role Modules : Agriculture</name>",about);Assert.Contains("<packageId>SpacemanWil.WilsAgriculture</packageId>",about);
        }

        [Fact] public void Every_agriculture_role_has_five_positive_negative_and_relationship_events()
        {
            foreach(var role in AgricultureEvents.Roles){var events=AgricultureEvents.All.Where(e=>e.Role==role).ToList();Assert.Equal(5,events.Count(e=>e.Mood>0));Assert.Equal(5,events.Count(e=>e.Mood<0));Assert.Equal(5,events.Count(e=>e.Opinion!=0));}
            Assert.Equal(AgricultureEvents.All.Length,AgricultureEvents.All.Select(e=>e.Id).Distinct().Count());
            string file=Path.Combine(RepoPaths.Root(),"Packages","WilsAgriculture","Defs","AgricultureThoughts.xml");var defs=XDocument.Load(file).Descendants("defName").Select(x=>x.Value).ToList();foreach(var e in AgricultureEvents.All)Assert.Contains(e.Thought,defs);
        }
    }
}
