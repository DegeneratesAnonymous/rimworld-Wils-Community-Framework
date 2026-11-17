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
            var policy=c.Registry.HuntingPolicy();int meat=Find.Maps.Sum(AutomaticHunting.MeatOnMap),hunters=HunterCount(c);
            float top=CommunityPageLayout.Section(rect,0f,0f,rect.width,"Hunting overview",r=>
            {
                float y=0f;bool automatic=policy.Automatic;
                y=CommunityPageLayout.Toggle(r,y,"Automatic hunting",ref automatic);
                if(automatic!=policy.Automatic){policy.Automatic=automatic;if(!automatic)AutomaticHunting.RemoveAll(c);}
                string state=!automatic?"Off — manual hunt designations continue to work normally":hunters==0?"Waiting for an assigned Tracker or Hunter":meat>=policy.MeatTarget?"Reserve reached — no new targets will be selected":"Selecting safe targets until the reserve is restored";
                y=CommunityPageLayout.Line(r,y,state,!automatic||hunters==0);
                CommunityWidgets.Bar(new Rect(0f,y,r.width,22f), policy.MeatTarget <= 0 ? 1f : meat/(float)policy.MeatTarget, new Color(.38f,.64f,.85f), meat+" / "+policy.MeatTarget); y+=30f;
                if(policy.AutomaticTargets.Count==0 && automatic && meat<policy.MeatTarget) y=CommunityPageLayout.Line(r,y,"No automatic targets selected. Review workforce, allowed targets, distance and safety limits.",true);
                return CommunityPageLayout.Line(r,y,"Meat "+meat+" / "+policy.MeatTarget+"   •   "+hunters+" hunting professional"+(hunters==1?"":"s")+"   •   "+policy.AutomaticTargets.Count+" automatic target"+(policy.AutomaticTargets.Count==1?"":"s"),true);
            });
            float width=CommunityPageLayout.ColumnWidth(rect),left=top,right=top;
            left=CommunityPageLayout.Section(rect,0f,top,width,"Hunting policy",r=>Hunting(r,c,policy));
            bool columns=CommunityPageLayout.TwoColumns(rect);
            right=CommunityPageLayout.Section(rect,columns?width+CommunityPageLayout.Gap:0f,columns?top:left,width,"Professional workforce",r=>Workforce(r,c));
            return columns?Math.Max(left,right):right;
        }
        private static float Hunting(Rect r,FamilyComponent c,HuntingPolicy p)
        {
            float y=0f;
            y=CommunityPageLayout.Stepper(r,y,"Desired meat reserve",p.MeatTarget,50,0,2000,v=>p.MeatTarget=v);
            y=CommunityPageLayout.Stepper(r,y,"Maximum distance",p.MaxDistance,10,20,150,v=>p.MaxDistance=v);
            y=CommunityPageLayout.Stepper(r,y,"Maximum revenge chance",p.MaxRevengePercent,1,0,100,v=>p.MaxRevengePercent=v,"%");
            y=CommunityPageLayout.Stepper(r,y,"Population left untouched",p.MinimumPopulation,1,0,20,v=>p.MinimumPopulation=v);
            y=CommunityPageLayout.Stepper(r,y,"Targets per hunter",p.TargetsPerHunter,1,1,5,v=>p.TargetsPerHunter=v);
            y=CommunityPageLayout.Line(r,y,"Allowed targets",true);
            bool hunter=Has(c,"Hunter")||Has(c,"Stalker")||Has(c,"Gamekeeper")||Has(c,"BigGameHunter"),big=Has(c,"BigGameHunter");
            y=CommunityPageLayout.Toggle(r,y,"Medium game",ref p.AllowMedium,hunter,"Requires a Hunter");
            y=CommunityPageLayout.Toggle(r,y,"Large game",ref p.AllowLarge,big,"Requires a Big-Game Hunter");
            y=CommunityPageLayout.Toggle(r,y,"Predators",ref p.AllowPredators,big,"Requires a Big-Game Hunter");
            y=CommunityPageLayout.Line(r,y,"Conservation",true);
            y=CommunityPageLayout.Toggle(r,y,"Protect young animals",ref p.ProtectYoung);
            y=CommunityPageLayout.Toggle(r,y,"Protect pregnant animals",ref p.ProtectPregnant);
            y=CommunityPageLayout.Line(r,y,"These settings affect automatic targets only. Your manual designations are never changed.",true);
            return y;
        }
        private static float Workforce(Rect r,FamilyComponent c)
        {
            float y=0f;var a=c.Registry.Assignments();
            foreach(var role in new[]{AgricultureCareers.Tracker,AgricultureCareers.Role("Hunter"),AgricultureCareers.Role("Stalker"),AgricultureCareers.Role("Gamekeeper"),AgricultureCareers.Role("BigGameHunter")})
            {
                var def=CareerService.Professions.ById(role);int count=a.Holders(role).Count();y=CommunityPageLayout.Line(r,y,(def==null?role:CareerService.TitleOf(def,c.Registry))+": "+count);
            }
            y=CommunityStaffing.Draw(r,y,c,"wils.agriculture");
            y=CommunityPageLayout.Line(r,y,"Tracker: safe small game automatically\nHunter: unlocks medium game\nStalker: longer range\nGamekeeper: protects populations\nBig-Game Hunter: large game and predators",true);
            return y;
        }
        private static int HunterCount(FamilyComponent c)
        {
            var roles=new[]{AgricultureCareers.Tracker,AgricultureCareers.Role("Hunter"),AgricultureCareers.Role("Stalker"),AgricultureCareers.Role("Gamekeeper"),AgricultureCareers.Role("BigGameHunter")};
            return roles.SelectMany(role=>c.Registry.Assignments().Holders(role)).Distinct().Count(id=>c.TryGetPawn(id) is Pawn p&&p.Spawned&&!p.Dead);
        }
        private static bool Has(FamilyComponent c,string specialty)=>c.Registry.Assignments().Holders(AgricultureCareers.Role(specialty)).Any(id=>c.TryGetPawn(id) is Pawn p&&p.Spawned&&!p.Dead);
    }
}

