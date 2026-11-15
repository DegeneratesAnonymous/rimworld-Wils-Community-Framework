using System;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using Verse;

namespace FamilyLife
{
    public sealed class AgricultureMod:Mod
    {
        public AgricultureMod(ModContentPack content):base(content){FamilyFramework.Install(new AgricultureModule());}
    }

    public sealed class AgricultureModule:IFamilyModule
    {
        public string Id=>AgricultureCareers.Module;
        public void Register()
        {
            FamilyFramework.Catalog.Register(new ModuleInfo{Id=Id,Name="Wil's Role Modules : Agriculture",ApiVersion=FamilyFramework.ApiVersion,Requires=new[]{ModuleCatalog.FrameworkId}});
            CareerService.RegisterCareer(AgricultureCareers.Definition());
            CareerService.RegisterRole(new ProfessionRole{Id=AgricultureCareers.Forager,ModuleId=Id,Career=AgricultureCareers.Career,Level=1,Path="Farmer"});
            CareerService.RegisterRole(new ProfessionRole{Id=AgricultureCareers.Tracker,ModuleId=Id,Career=AgricultureCareers.Career,Level=1,Path="Hunter"});
            CareerService.RegisterRole(new ProfessionRole{Id=AgricultureCareers.Handler,ModuleId=Id,Career=AgricultureCareers.Career,Level=1,Path="Rancher"});
            foreach(var branch in new[]{"Farmer","Hunter","Rancher"})CareerService.RegisterRole(new ProfessionRole{Id=AgricultureCareers.Role(branch),ModuleId=Id,Career=AgricultureCareers.Career,Level=2,Specialty=branch});
            foreach(var specialty in new[]{"Horticulturist","Herbalist","Arborist","Stalker","Gamekeeper","BigGameHunter","Shepherd","Breeder","Beastmaster"})
                CareerService.RegisterRole(new ProfessionRole{Id=AgricultureCareers.Role(specialty),ModuleId=Id,Career=AgricultureCareers.Career,Level=3,Specialty=specialty,BonusKey="WFL_Agriculture_"+specialty+"Bonus"});
            FamilyFramework.Pages.Register(new CommunityPage<Func<UnityEngine.Rect,FamilyComponent,float>>{Id=Id+".management",ModuleId=Id,LabelKey="WFL_Agriculture_Page",Order=150,Icon="WilsFamilyLife/Roles/protector_eye",Render=AgriculturePage.Draw,Status=AgriculturePage.Attention});
            FamilyFramework.Roster.Register(new WorkItem{Id=Id+".hunting",ModuleId=Id,Interval=2500,Run=AutomaticHunting.Run});
            FamilyFramework.HuntWorkAllowed.Add(Id+".workers",Id,AutomaticHunting.WorkerAllowed);
            FamilyFramework.LabourDone.Add(Id+".experience",Id,AgricultureWork.Completed);
            FamilyFramework.SocialInteraction.Add(Id+".relationships",Id,AgricultureStories.Social);
            FamilyFramework.Reconcilers.Add(Id+".reconcile",Id,c=>AutomaticHunting.Reconcile(c));
            FamilyFramework.Resets.Add(Id+".reset",Id,()=>{AutomaticHunting.Reset();AgricultureStories.Reset();});
            FamilyFramework.Removal.Add(Id+".removal",Id,c=>
            {
                var report=new CleanupReport(Id);report.Step("Remove automatic hunt designations",()=>AutomaticHunting.RemoveAll(c)+" automatic target(s) released; careers and policy retained");return report;
            });
            CareerService.Registered(Id);
        }
    }

    public static class AgricultureWork
    {
        private static bool Has(string value,params string[] parts){value=value??"";foreach(var part in parts)if(value.IndexOf(part,StringComparison.OrdinalIgnoreCase)>=0)return true;return false;}
        public static void Completed(LabourReport r)
        {
            var c=FamilyComponent.Current;if(c==null||r?.Pawn==null)return;
            string route=null,specialty=null,kind=null;
            if(r.TargetAnimal&&(Has(r.JobDef,"Hunt")||string.Equals(r.WorkType,"Hunting",StringComparison.OrdinalIgnoreCase)))
            {
                route="Hunter";bool managed=c.Registry.HuntingPolicy().AutomaticTargets.Remove(r.ThingId);specialty=r.TargetBodySize>1f||r.TargetPredator||r.TargetRevengeChance>.05f?"BigGameHunter":managed?"Gamekeeper":"Stalker";kind="agriculture.hunt";
            }
            else if(string.Equals(r.WorkType,"Growing",StringComparison.OrdinalIgnoreCase)||string.Equals(r.WorkType,"PlantCutting",StringComparison.OrdinalIgnoreCase)||Has(r.JobDef,"Sow","Harvest","CutPlant"))
            {
                route="Farmer";specialty=Has(r.TargetDef,"Healroot","Medicine")?"Herbalist":Has(r.TargetDef,"Tree")?"Arborist":"Horticulturist";kind="agriculture.plant";
            }
            else if(string.Equals(r.WorkType,"Handling",StringComparison.OrdinalIgnoreCase)||Has(r.JobDef,"Tame","Train","Milk","Shear","Rope"))
            {
                route="Rancher";specialty=Has(r.JobDef,"Train","Tame")?"Beastmaster":Has(r.JobDef,"Milk","Shear")?"Shepherd":"Breeder";kind="agriculture.animal";
            }
            if(route==null)return;
            var ledger=c.Registry.Careers();string credit=ledger.IsSpecialist(r.Pawn.thingIDNumber,AgricultureCareers.Career,route)?specialty:route;
            CareerService.Report(c,r.Pawn,AgricultureCareers.Career,credit,kind,kind+":"+r.JobId,route=="Hunter"?8:4,FrameworkServices.Now);
            AgricultureStories.Worked(r.Pawn,route,specialty,r.JobId);
        }
    }

    public static class AgricultureStories
    {
        private static readonly System.Collections.Generic.Dictionary<string,int> last=new System.Collections.Generic.Dictionary<string,int>();
        public static void Reset()=>last.Clear();
        private static string ActiveRole(Pawn pawn,string route,string specialty)
        {
            if(specialty!=null&&CareerService.IsAssignedSpecialist(pawn,AgricultureCareers.Career,specialty))return specialty;
            if(route!=null&&CareerService.IsAssignedSpecialist(pawn,AgricultureCareers.Career,route))return route;
            var a=FamilyComponent.Current?.Registry.Assignments();
            if(route=="Hunter"&&a?.IsAssigned(pawn.thingIDNumber,AgricultureCareers.Tracker)==true)return "Tracker";
            if(route=="Rancher"&&a?.IsAssigned(pawn.thingIDNumber,AgricultureCareers.Handler)==true)return "Handler";
            return "Forager";
        }
        private static void Give(Pawn subject,Pawn other,string role,string fact)
        {
            var e=AgricultureEvents.Eligible(role,fact,other!=null).OrderBy(x=>x.Id).FirstOrDefault();if(e==null)return;int day=FrameworkServices.EconomyDay;
            string key=subject.thingIDNumber+":"+e.Id+":"+(other?.thingIDNumber??0);if(last.TryGetValue(key,out int before)&&day-before<3)return;last[key]=day;FrameworkServices.GainMemory(subject,e.Thought,other);
        }
        public static void Worked(Pawn pawn,string route,string specialty,int seed)
        {
            string[] facts=route=="Hunter"?new[]{"CleanKill","Resourceful","ProudWork"}:route=="Rancher"?new[]{"HealthyStock","Resourceful","ProudWork"}:new[]{"GoodHarvest","Resourceful","ProudWork"};
            Give(pawn,null,ActiveRole(pawn,route,specialty),facts[System.Math.Abs(seed)%facts.Length]);
        }
        private static string Role(Pawn pawn)
        {
            var a=FamilyComponent.Current?.Registry.Assignments();if(a==null||pawn==null)return null;
            foreach(var role in Enumerable.Reverse(AgricultureEvents.Roles))if(a.IsAssigned(pawn.thingIDNumber,AgricultureCareers.Role(role)))return role;
            if(a.IsAssigned(pawn.thingIDNumber,AgricultureCareers.Tracker))return "Tracker";if(a.IsAssigned(pawn.thingIDNumber,AgricultureCareers.Handler))return "Handler";if(a.IsAssigned(pawn.thingIDNumber,AgricultureCareers.Forager))return "Forager";return null;
        }
        public static void Social(Pawn actor,Pawn other,InteractionDef interaction)
        {
            string role=Role(actor);if(role==null||Role(other)==null)return;int seed=(actor.thingIDNumber*397)^other.thingIDNumber^(FrameworkServices.EconomyDay/3);
            string fact=new[]{"SharedKnowledge","HelpedWork","PraisedSkill","TerritoryDispute","AnimalDispute"}[System.Math.Abs(seed)%5];Give(actor,other,role,fact);
        }
    }
}
