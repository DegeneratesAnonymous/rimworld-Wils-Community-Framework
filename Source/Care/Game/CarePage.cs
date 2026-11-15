using System;
using System.Linq;
using FamilyLife.Domain;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>Care's view of existing staff, workload and check-up actions.</summary>
    internal static class CarePage
    {
        public const string PageId = "wils.care.care";
        public static void Register(string moduleId) =>
            FamilyFramework.Pages.Register(new Framework.CommunityPage<Func<Rect, FamilyComponent, float>> { Id = PageId, ModuleId = moduleId, LabelKey = "WFL_Care_Page", Order = 120, Render = Draw, Status = Attention, Icon = "UI/Community/care" });
        private static int Attention() { var c = FamilyComponent.Current; return c == null ? 0 : CareEvents.IsSickCount(c); }

        public static float Draw(Rect r, FamilyComponent c)
        {
            var reg = c.Registry;var staff=CareCareers.Practitioners(reg).Where(id=>reg.GetPerson(id)?.InColony==true).ToList();int sick=CareEvents.IsSickCount(c);var seat=reg.OfficeFor(OfficeKind.Healer);
            float top=CommunityPageLayout.Section(r,0f,0f,r.width,"Care overview",a=>
            {
                float y=CommunityPageLayout.Line(a,0f,staff.Count+" care worker"+(staff.Count==1?"":"s")+"   •   "+sick+" colonist"+(sick==1?"":"s")+" needing attention   •   "+(seat!=null&&seat.Held?"care seat staffed":"care seat vacant"),seat==null||!seat.Held);
                return CommunityPageLayout.Button(a,y,"Manage care professions",()=>MainTabWindow_Community.Open(FamilyLife.Framework.ModuleCatalog.FrameworkId+".professions"));
            });
            float w = CommunityPageLayout.ColumnWidth(r);
            float left = CommunityPageLayout.Section(r, 0f, top, w, "WFL_Care_Practitioners".Translate(), a =>
            {
                float y = 0f;
                if (staff.Count == 0) y = CommunityPageLayout.Line(a, y, "WFL_Care_None".Translate(), true);
                foreach (int id in staff) y = CommunityPageLayout.Line(a, y, (c.TryGetPawn(id)?.LabelShort ?? "#" + id) + " - " + string.Join(", ", reg.Assignments().RolesOf(id).Select(x => { var role = CareerService.Professions.All.FirstOrDefault(p => p.Id == x); return role != null ? CareerService.TitleOf(role, reg) : x; })));
                return CommunityPageLayout.Line(a, y, "WFL_Care_Capacity".Translate(CheckupWorkload.CommunityLimit(false), CheckupWorkload.CommunityLimit(true)));
            });
            bool columns = CommunityPageLayout.TwoColumns(r);
            float right = CommunityPageLayout.Section(r, columns ? w + CommunityPageLayout.Gap : 0f, columns ? top : left, w, "WFL_Care_Rounds".Translate(), a =>
            {
                if (seat == null || !seat.Held) return CommunityPageLayout.Line(a, 0f, "WFL_Care_NoSeat".Translate(), true);
                float y = CommunityPageLayout.Line(a, 0f, (seat.Care().IllnessRound ? "WFL_Care_OutbreakRound" : "WFL_Care_RoutineRound").Translate());
                y = CommunityPageLayout.Button(a, y, "WFL_Event_BeginCheckups".Translate(), () => CareEvents.BeginCare(PendingEvent.Checkups));
                return CommunityPageLayout.Button(a, y, "WFL_Event_BeginIllness".Translate(), () => CareEvents.BeginCare(PendingEvent.Illness));
            });
            return Mathf.Max(left, right);
        }
    }
}
