using System;
using System.Linq;
using FamilyLife.Domain;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>Education's view of existing staff, sessions and assessments.</summary>
    internal static class EducationPage
    {
        public const string PageId = "wils.education.education";
        public static void Register(string moduleId) =>
            FamilyFramework.Pages.Register(new Framework.CommunityPage<Func<Rect, FamilyComponent, float>> { Id = PageId, ModuleId = moduleId, LabelKey = "WFL_Edu_Page", Order = 110, Render = Draw, Status = Attention, Icon = "WilsFamilyLife/Pages/education" });
        private static int Attention() { var c = FamilyComponent.Current; return c == null ? 0 : EducationEvents.EligibleNow(c, FrameworkServices.Now).Count; }

        public static float Draw(Rect r, FamilyComponent c)
        {
            float top = CommunityPageLayout.Section(r, 0f, 0f, r.width, "WFL_Edu_Instructors".Translate(), a => EducationManage.Teachers(a, c));
            top = CommunityPageLayout.Section(r, 0f, top, r.width, "WFL_Edu_Pupils".Translate(), a => EducationManage.Pupils(a, c));
            top = CommunityPageLayout.Section(r, 0f, top, r.width, "WFL_Edu_Classes".Translate(), a =>
            {
                float h = 0f; var running = ClassSessions.RunningSessions.Where(x => !x.Finished).ToList();
                if (running.Count == 0) h = CommunityPageLayout.Line(a, h, "WFL_Edu_NoClasses".Translate(), true);
                foreach (var x in running) h = CommunityPageLayout.Line(a, h, "WFL_Edu_ClassLine".Translate(c.TryGetPawn(x.TeacherId)?.LabelShort ?? "#" + x.TeacherId, x.Subject, x.Roster.Count));
                return h;
            });
            float right = CommunityPageLayout.Section(r, 0f, top, r.width, "WFL_Edu_Assessments".Translate(), a =>
            {
                float y = 0f; int now = FrameworkServices.Now; var due = EducationEvents.EligibleNow(c, now);
                if (due.Count == 0) return CommunityPageLayout.Line(a, y, "WFL_Edu_NoAssessments".Translate(), true);
                y = CommunityPageLayout.Line(a, y, "WFL_Edu_AssessmentsDue".Translate(string.Join(", ", due.Select(p => p.LabelShort))));
                y = CommunityPageLayout.Button(a, y, "WFL_Event_Begin".Translate(), () => EducationEvents.BeginAll(c, now));
                y = CommunityPageLayout.Button(a, y, "WFL_Edu_Postpone".Translate(), () => EducationEvents.PostponeAll(c, now));
                return CommunityPageLayout.Button(a, y, "WFL_Edu_Skip".Translate(), () => EducationEvents.SkipAll(c, now));
            });
            return right;
        }
    }
}
