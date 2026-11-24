using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Framework;

namespace FamilyLife.Domain
{
    public static class AgricultureCareers
    {
        public const string Module="wils.agriculture", Career="wils.agriculture.agriculture";
        public const string Forager="wils.agriculture.forager", Tracker="wils.agriculture.tracker", Handler="wils.agriculture.handler";
        public static string Role(string name)=>Module+"."+name.ToLowerInvariant();
        public static CareerDefinition Definition()=>new CareerDefinition
        {
            Id=Career,ModuleId=Module,LabelKey="WFL_Agriculture_Career",
            Level2Branches=new Dictionary<string,CareerRule>
            {
                {"Farmer",new CareerRule{GeneralXp=120,SpecialtyXp=60}.Skill("Plants",6)},
                {"Hunter",new CareerRule{GeneralXp=120,SpecialtyXp=60}.Skill("Shooting",6).Skill("Animals",4)},
                {"Rancher",new CareerRule{GeneralXp=120,SpecialtyXp=60}.Skill("Animals",6)}
            },
            Specialties=new Dictionary<string,CareerRule>
            {
                {"Horticulturist",new CareerRule{GeneralXp=250,SpecialtyXp=60}.Skill("Plants",10).After("Farmer")},
                {"Herbalist",new CareerRule{GeneralXp=250,SpecialtyXp=60}.Skill("Plants",10).Skill("Medicine",5).After("Farmer")},
                {"Arborist",new CareerRule{GeneralXp=250,SpecialtyXp=60}.Skill("Plants",10).After("Farmer")},
                {"Stalker",new CareerRule{GeneralXp=250,SpecialtyXp=60}.Skill("Shooting",10).Skill("Animals",6).After("Hunter")},
                {"Gamekeeper",new CareerRule{GeneralXp=250,SpecialtyXp=60}.Skill("Animals",10).Skill("Plants",6).After("Hunter")},
                {"BigGameHunter",new CareerRule{GeneralXp=250,SpecialtyXp=60}.Skill("Shooting",10).Skill("Animals",8).After("Hunter")},
                {"Shepherd",new CareerRule{GeneralXp=250,SpecialtyXp=60}.Skill("Animals",10).After("Rancher")},
                {"Breeder",new CareerRule{GeneralXp=250,SpecialtyXp=60}.Skill("Animals",10).Skill("Medicine",4).After("Rancher")},
                {"Beastmaster",new CareerRule{GeneralXp=250,SpecialtyXp=60}.Skill("Animals",10).After("Rancher")}
            }
        };
    }

    public enum GameSize { Small, Medium, Large }
    public enum HunterCapability { Tracker, Hunter, Stalker, Gamekeeper, BigGameHunter }

    public sealed class HuntTargetFacts
    {
        public int Id,Population,Meat,RevengePercent,Distance;
        public string Species;
        public GameSize Size;
        public bool Predator,Herd,Adult=true,Pregnant,Reachable=true,AlreadyDesignated;
    }

    public sealed class HuntingPolicy : IModuleState
    {
        public const string StateId="wils.agriculture.hunting";
        public string Id=>StateId;
        public bool Automatic;
        public int MeatTarget=250,MaxDistance=60,MaxRevengePercent=5,MinimumPopulation=2,TargetsPerHunter=1;
        public bool AllowMedium=true,AllowLarge,AllowPredators,ProtectYoung=true,ProtectPregnant=true;
        public readonly List<int> AutomaticTargets=new List<int>();

        public string Encode()=>new StateWriter().Bool("auto",Automatic).Int("meat",MeatTarget).Int("distance",MaxDistance).Int("revenge",MaxRevengePercent)
            .Int("population",MinimumPopulation).Int("perHunter",TargetsPerHunter).Bool("medium",AllowMedium).Bool("large",AllowLarge).Bool("predators",AllowPredators)
            .Bool("young",ProtectYoung).Bool("pregnant",ProtectPregnant).Ints("targets",AutomaticTargets).ToString();
        public static HuntingPolicy Decode(StateReader r)
        {
            var p=new HuntingPolicy{Automatic=r.Bool("auto"),MeatTarget=r.Int("meat",250),MaxDistance=r.Int("distance",60),MaxRevengePercent=r.Int("revenge",5),
                MinimumPopulation=r.Int("population",2),TargetsPerHunter=r.Int("perHunter",1),AllowMedium=r.Bool("medium"),AllowLarge=r.Bool("large"),AllowPredators=r.Bool("predators"),
                ProtectYoung=r.Bool("young"),ProtectPregnant=r.Bool("pregnant")};
            p.AutomaticTargets.AddRange(r.Ints("targets").Distinct());return p;
        }
    }
    public sealed class HuntingPolicyFactory:IStateFactory<HuntingPolicy>
    {
        public static readonly HuntingPolicyFactory Instance=new HuntingPolicyFactory();public int Version=>1;
        public HuntingPolicy Create()=>new HuntingPolicy();public HuntingPolicy Decode(StateReader r,int version)=>HuntingPolicy.Decode(r);public HuntingPolicy FromLegacy(StateReader r)=>Create();
    }
    public static class AgricultureStateAccess
    {
        public static Domain.HuntingPolicy HuntingPolicy(this FamilyRegistry r)=>r.Shared.Of(Domain.HuntingPolicy.StateId,HuntingPolicyFactory.Instance);
    }

    public static class HuntingRules
    {
        public static bool Eligible(HuntTargetFacts t,HuntingPolicy p,HunterCapability capability)
        {
            if(t==null||p==null||!p.Automatic||!t.Reachable||t.AlreadyDesignated)return false;
            if(p.ProtectYoung&&!t.Adult||p.ProtectPregnant&&t.Pregnant)return false;
            if(t.Population<=p.MinimumPopulation)return false;
            bool fullHunter=capability!=HunterCapability.Tracker;
            if(t.Size==GameSize.Medium&&(!fullHunter||!p.AllowMedium))return false;
            if(t.Size==GameSize.Large&&(capability!=HunterCapability.BigGameHunter||!p.AllowLarge))return false;
            if(t.Predator&&(capability!=HunterCapability.BigGameHunter||!p.AllowPredators))return false;
            int revenge=capability==HunterCapability.Tracker?Math.Min(2,p.MaxRevengePercent):p.MaxRevengePercent;
            if(t.RevengePercent>revenge)return false;
            int distance=capability==HunterCapability.Stalker?p.MaxDistance+25:capability==HunterCapability.Tracker?Math.Min(45,p.MaxDistance):p.MaxDistance;
            return t.Distance<=distance;
        }

        public static int Score(HuntTargetFacts t,HunterCapability capability)
        {
            int score=t.Meat*3-t.Distance-t.RevengePercent*5;
            if(capability==HunterCapability.Gamekeeper)score+=Math.Max(0,t.Population-2)*8;
            if(capability==HunterCapability.Stalker)score+=t.Distance/2;
            if(capability==HunterCapability.BigGameHunter&&t.Size==GameSize.Large)score+=100;
            return score;
        }

        public static HuntTargetFacts Choose(IEnumerable<HuntTargetFacts> targets,HuntingPolicy policy,IEnumerable<HunterCapability> capabilities)
        {
            var caps=(capabilities??Enumerable.Empty<HunterCapability>()).Distinct().ToList();
            return (targets??Enumerable.Empty<HuntTargetFacts>()).SelectMany(t=>caps.Where(c=>Eligible(t,policy,c)).Select(c=>new{Target=t,Score=Score(t,c)}))
                .OrderByDescending(x=>x.Score).ThenBy(x=>x.Target.Id).Select(x=>x.Target).FirstOrDefault();
        }
    }
}
