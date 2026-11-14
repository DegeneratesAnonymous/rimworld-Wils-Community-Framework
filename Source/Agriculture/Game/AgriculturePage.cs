using System;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    public static class AgriculturePage
    {
        public static int Attention()
        {
            var c=FamilyComponent.Current;if(c==null)return 0;var p=c.Registry.HuntingPolicy();return p.Automatic&&Find.Maps.Sum(m=>AutomaticHunting.MeatOnMap(m))<p.MeatTarget?1:0;
        }
        public static float Draw(Rect rect,FamilyComponent c)
        {
            var policy=c.Registry.HuntingPolicy();float width=CommunityPageLayout.ColumnWidth(rect),left=0f,right=0f;
            left=CommunityPageLayout.Section(rect,0f,0f,width,"Hunting management",r=>Hunting(r,c,policy));
            bool columns=CommunityPageLayout.TwoColumns(rect);
            right=CommunityPageLayout.Section(rect,columns?width+CommunityPageLayout.Gap:0f,columns?0f:left,width,"Professional workforce",r=>Workforce(r,c));
            return columns?Math.Max(left,right):right;
        }
        private static float Hunting(Rect r,FamilyComponent c,HuntingPolicy p)
        {
            float y=0f;
            y=CommunityPageLayout.Line(r,y,p.Automatic?"Automatic hunting: enabled":"Automatic hunting: disabled",!p.Automatic);
            y=CommunityPageLayout.Button(r,y,p.Automatic?"Disable automatic hunting":"Enable automatic hunting",()=>{p.Automatic=!p.Automatic;if(!p.Automatic)AutomaticHunting.RemoveAll(c);});
            y=CommunityPageLayout.Line(r,y,"Manual hunt designations are never restricted or removed by this policy.",true);
            y=Step(r,y,"Desired meat reserve",p.MeatTarget,50,0,2000,v=>p.MeatTarget=v);
            y=Step(r,y,"Maximum hunting distance",p.MaxDistance,10,20,150,v=>p.MaxDistance=v);
            y=Step(r,y,"Maximum revenge chance",p.MaxRevengePercent,1,0,100,v=>p.MaxRevengePercent=v,"%");
            y=Step(r,y,"Minimum wild population left",p.MinimumPopulation,1,0,20,v=>p.MinimumPopulation=v);
            y=Step(r,y,"Targets per assigned hunter",p.TargetsPerHunter,1,1,5,v=>p.TargetsPerHunter=v);
            bool hunter=Has(c,"Hunter")||Has(c,"Stalker")||Has(c,"Gamekeeper")||Has(c,"BigGameHunter"),big=Has(c,"BigGameHunter");
            y=Toggle(r,y,"Allow medium game",p.AllowMedium,hunter,v=>p.AllowMedium=v,hunter?null:"Requires an assigned Hunter or hunting specialist");
            y=Toggle(r,y,"Allow large game",p.AllowLarge,big,v=>p.AllowLarge=v,big?null:"Requires an assigned Big-Game Hunter");
            y=Toggle(r,y,"Allow predators",p.AllowPredators,big,v=>p.AllowPredators=v,big?null:"Requires an assigned Big-Game Hunter");
            y=Toggle(r,y,"Protect young animals",p.ProtectYoung,true,v=>p.ProtectYoung=v,null);
            y=Toggle(r,y,"Protect pregnant animals",p.ProtectPregnant,true,v=>p.ProtectPregnant=v,null);
            y=CommunityPageLayout.Line(r,y,"Automatic targets currently designated: "+p.AutomaticTargets.Count,true);
            return y;
        }
        private static float Workforce(Rect r,FamilyComponent c)
        {
            float y=0f;var a=c.Registry.Assignments();
            foreach(var role in new[]{AgricultureCareers.Tracker,AgricultureCareers.Role("Hunter"),AgricultureCareers.Role("Stalker"),AgricultureCareers.Role("Gamekeeper"),AgricultureCareers.Role("BigGameHunter")})
            {
                var def=CareerService.Professions.ById(role);int count=a.Holders(role).Count();y=CommunityPageLayout.Line(r,y,(def==null?role:CareerService.TitleOf(def,c.Registry))+": "+count);
            }
            y=CommunityPageLayout.Line(r,y,"Trackers automatically choose only nearby, low-risk small game. They may still perform any hunt the player designates.",true);
            y=CommunityPageLayout.Line(r,y,"Hunters unlock medium-game policy. Stalkers extend range, Gamekeepers favor healthy populations, and Big-Game Hunters unlock large and predatory automatic targets.",true);
            return y;
        }
        private static bool Has(FamilyComponent c,string specialty)=>c.Registry.Assignments().Holders(AgricultureCareers.Role(specialty)).Any(id=>c.TryGetPawn(id) is Pawn p&&p.Spawned&&!p.Dead);
        private static float Step(Rect r,float y,string label,int value,int step,int min,int max,Action<int> set,string suffix="")
        {
            y=CommunityPageLayout.Line(r,y,label+": "+value+suffix);
            return CommunityPageLayout.Button(r,y,"Change "+label.ToLowerInvariant(),()=>Find.WindowStack.Add(new FloatMenu(new[]{-step,step}.Where(d=>value+d>=min&&value+d<=max).Select(d=>new FloatMenuOption((d>0?"Increase to ":"Decrease to ")+(value+d)+suffix,()=>set(value+d))).ToList())));
        }
        private static float Toggle(Rect r,float y,string label,bool value,bool enabled,Action<bool> set,string locked)
        {
            if(!enabled)return CommunityPageLayout.Line(r,y,label+": locked — "+locked,true);
            return CommunityPageLayout.Button(r,y,label+": "+(value?"allowed":"not allowed"),()=>set(!value));
        }
    }
}
