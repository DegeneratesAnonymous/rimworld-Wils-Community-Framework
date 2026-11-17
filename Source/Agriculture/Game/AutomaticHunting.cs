using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using Verse;
using Verse.AI;

namespace FamilyLife
{
    public static class AutomaticHunting
    {
        private static int lastMap;
        public static void Reset(){lastMap=0;}
        public static bool WorkerAllowed(Pawn pawn,Thing target)
        {
            var c=FamilyComponent.Current;if(c==null||pawn==null||target==null)return true;var p=c.Registry.HuntingPolicy();
            if(!p.AutomaticTargets.Contains(target.thingIDNumber))return true;
            var a=c.Registry.Assignments();return a.IsAssigned(pawn.thingIDNumber,AgricultureCareers.Tracker)||CareerService.IsAssignedSpecialist(pawn,AgricultureCareers.Career,"Hunter")||
                CareerService.IsAssignedSpecialist(pawn,AgricultureCareers.Career,"Stalker")||CareerService.IsAssignedSpecialist(pawn,AgricultureCareers.Career,"Gamekeeper")||CareerService.IsAssignedSpecialist(pawn,AgricultureCareers.Career,"BigGameHunter");
        }
        public static void Reconcile(FamilyComponent c)
        {
            if(c==null)return;var p=c.Registry.HuntingPolicy();
            p.AutomaticTargets.RemoveAll(id=>FindThing(id)==null||!HasHuntDesignation(FindThing(id)));
        }
        public static void Run(int now)
        {
            var c=FamilyComponent.Current;if(c==null||Find.Maps.Count==0)return;var policy=c.Registry.HuntingPolicy();
            var map=Find.Maps.OrderBy(m=>m.uniqueID).FirstOrDefault(m=>m.uniqueID>lastMap)??Find.Maps.OrderBy(m=>m.uniqueID).First();lastMap=map.uniqueID;
            Clean(policy,map);
            if(!policy.Automatic||MeatOnMap(map)>=policy.MeatTarget){RemoveForMap(policy,map);return;}
            var hunters=map.mapPawns.FreeColonistsSpawned.Where(p=>Capabilities(c,p).Any()).ToList();if(hunters.Count==0){RemoveForMap(policy,map);return;}
            ReleaseInvalid(policy,map,hunters);
            int room=Math.Max(0,hunters.Count*Math.Max(1,policy.TargetsPerHunter)-policy.AutomaticTargets.Count(id=>FindThing(id)?.Map==map));
            for(int n=0;n<room;n++)
            {
                var facts=Facts(map,hunters,policy);var target=HuntingRules.Choose(facts,policy,hunters.SelectMany(p=>Capabilities(c,p)));if(target==null)break;
                var animal=FindThing(target.Id) as Pawn;if(animal==null||HasHuntDesignation(animal))break;
                map.designationManager.AddDesignation(new Designation(animal,DesignationDefOf.Hunt));policy.AutomaticTargets.Add(animal.thingIDNumber);
            }
        }
        public static int RemoveAll(FamilyComponent c)
        {
            if(c==null)return 0;var p=c.Registry.HuntingPolicy();int count=p.AutomaticTargets.Count;
            foreach(int id in p.AutomaticTargets.ToList()){var t=FindThing(id);var d=t==null?null:t.Map?.designationManager.DesignationOn(t,DesignationDefOf.Hunt);if(d!=null)t.Map.designationManager.RemoveDesignation(d);}
            p.AutomaticTargets.Clear();return count;
        }
        private static void RemoveForMap(HuntingPolicy p,Map map)
        {
            foreach(int id in p.AutomaticTargets.Where(id=>FindThing(id)?.Map==map).ToList()){var t=FindThing(id);var d=t?.Map?.designationManager.DesignationOn(t,DesignationDefOf.Hunt);if(d!=null)t.Map.designationManager.RemoveDesignation(d);p.AutomaticTargets.Remove(id);}
        }
        private static void Clean(HuntingPolicy p,Map map)
        {
            foreach(int id in p.AutomaticTargets.ToList()){var t=FindThing(id);if(t==null||!HasHuntDesignation(t)){p.AutomaticTargets.Remove(id);continue;}if(t.Map!=map)continue;}
        }
        private static void ReleaseInvalid(HuntingPolicy p,Map map,List<Pawn> hunters)
        {
            var caps=hunters.SelectMany(x=>Capabilities(FamilyComponent.Current,x)).Distinct().ToList();var byId=Facts(map,hunters,p).ToDictionary(x=>x.Id);
            foreach(int id in p.AutomaticTargets.Where(id=>FindThing(id)?.Map==map).ToList())
            {
                if(!byId.TryGetValue(id,out var f)){Release(p,id);continue;}
                f.AlreadyDesignated=false;f.Population++;          // Facts already subtracts this designation; eligibility asks whether one more animal may be taken.
                if(!caps.Any(cap=>HuntingRules.Eligible(f,p,cap)))Release(p,id);
            }
        }
        private static void Release(HuntingPolicy p,int id)
        {
            var t=FindThing(id);var d=t?.Map?.designationManager.DesignationOn(t,DesignationDefOf.Hunt);if(d!=null)t.Map.designationManager.RemoveDesignation(d);p.AutomaticTargets.Remove(id);
        }
        private static Thing FindThing(int id)
        {
            foreach(var map in Find.Maps){var t=map.listerThings.AllThings.FirstOrDefault(x=>x.thingIDNumber==id);if(t!=null)return t;}return null;
        }
        private static bool HasHuntDesignation(Thing t)=>t?.Map?.designationManager.DesignationOn(t,DesignationDefOf.Hunt)!=null;
        public static int MeatOnMap(Map map)=>map?.listerThings.AllThings.Where(t=>t.def?.ingestible!=null&&(t.def.ingestible.foodType&FoodTypeFlags.Meat)!=0).Sum(t=>t.stackCount)??0;
        private static IEnumerable<HunterCapability> Capabilities(FamilyComponent c,Pawn p)
        {
            var a=c.Registry.Assignments();
            if(CareerService.IsAssignedSpecialist(p,AgricultureCareers.Career,"BigGameHunter"))yield return HunterCapability.BigGameHunter;
            if(CareerService.IsAssignedSpecialist(p,AgricultureCareers.Career,"Gamekeeper"))yield return HunterCapability.Gamekeeper;
            if(CareerService.IsAssignedSpecialist(p,AgricultureCareers.Career,"Stalker"))yield return HunterCapability.Stalker;
            if(CareerService.IsAssignedSpecialist(p,AgricultureCareers.Career,"Hunter"))yield return HunterCapability.Hunter;
            if(a.IsAssigned(p.thingIDNumber,AgricultureCareers.Tracker))yield return HunterCapability.Tracker;
        }
        private static List<HuntTargetFacts> Facts(Map map,List<Pawn> hunters,HuntingPolicy policy)
        {
            var animals=map.mapPawns.AllPawnsSpawned.Where(p=>p.RaceProps?.Animal==true&&p.Faction==null&&!p.Dead&&!p.Downed).ToList();
            var populations=animals.GroupBy(p=>p.def.defName).ToDictionary(g=>g.Key,g=>g.Count(x=>!HasHuntDesignation(x)));var result=new List<HuntTargetFacts>();
            foreach(var animal in animals)
            {
                int distance=(int)hunters.Min(h=>h.Position.DistanceTo(animal.Position));var nearest=hunters.OrderBy(h=>h.Position.DistanceToSquared(animal.Position)).First();
                float size=animal.BodySize;result.Add(new HuntTargetFacts{Id=animal.thingIDNumber,Species=animal.def.defName,Population=populations[animal.def.defName],
                    Size=size<=.35f?GameSize.Small:size<=1f?GameSize.Medium:GameSize.Large,Meat=Math.Max(1,(int)(size*30f)),Distance=distance,
                    RevengePercent=(int)Math.Round((animal.RaceProps?.manhunterOnDamageChance??0f)*100f),Predator=animal.RaceProps?.predator==true,
                    Adult=animal.ageTracker==null||animal.ageTracker.CurLifeStageIndex>=animal.RaceProps.lifeStageAges.Count-1,
                    Pregnant=animal.health?.hediffSet?.HasHediff(HediffDefOf.Pregnant)==true,Reachable=nearest.CanReach(animal,PathEndMode.Touch,Danger.Some),AlreadyDesignated=HasHuntDesignation(animal)});
            }
            return result;
        }
    }
}
