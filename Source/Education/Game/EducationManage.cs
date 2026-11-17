using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>The management tables on the Education page: teachers (hold or end a class), classes under way, and the pupils with what they have been learning. Uses the same class code as the lectern button.</summary>
    internal static class EducationManage
    {
        private static string SubjectLabel(string subject) => string.IsNullOrEmpty(subject) ? "-" : (DefDatabase<SkillDef>.GetNamedSilentFail(subject)?.LabelCap.ToString() ?? subject);

        private static bool MayDirect(FamilyRegistry reg) => OfficeAuthority.MayDirect(reg, FrameworkServices.AvatarId, OfficeKind.Teacher);

        public static float Teachers(Rect s, FamilyComponent c)
        {
            var reg = c.Registry; var list = EducationCareers.Instructors(reg);
            if (list.Count == 0) return CommunityPageLayout.Line(s, 0f, "WFL_Edu_None".Translate(), true);
            float w = s.width, nameW = Mathf.Max(110f, w * .2f), focusW = Mathf.Max(100f, w * .18f), btnW = 110f, statusW = w - nameW - focusW - btnW;
            float y = CommunityWidgets.Header(s, 0f, new List<KeyValuePair<string, float>> { new KeyValuePair<string, float>("WFL_Table_Name".Translate(), nameW), new KeyValuePair<string, float>("WFL_Edu_Focus".Translate(), focusW), new KeyValuePair<string, float>("WFL_Edu_Status".Translate(), statusW), new KeyValuePair<string, float>("", btnW) });
            bool may = MayDirect(reg); int i = 0, now = FrameworkServices.Now;
            foreach (int id in list)
            {
                var pawn = c.TryGetPawn(id); if (pawn == null) continue;
                var row = CommunityWidgets.Row(s, y, i++); var session = ClassSessions.Of(id);
                CommunityWidgets.NameButton(new Rect(4f, row.y, nameW - 6f, row.height), pawn);
                string focus = EducationCareers.FocusSubject(reg, id);
                CommunityWidgets.Cell(new Rect(nameW, row.y, focusW, row.height), string.IsNullOrEmpty(focus) ? "WFL_Edu_AnySubject".Translate().ToString() : SubjectLabel(focus), CommunityWidgets.Muted);
                string status = session != null && !session.Finished ? "WFL_Edu_ClassLine".Translate(pawn.LabelShort, SubjectLabel(session.Subject), session.Roster.Count).ToString() : "WFL_Edu_Free".Translate().ToString();
                CommunityWidgets.Cell(new Rect(nameW + focusW, row.y, statusW - 6f, row.height), status, session != null ? CommunityWidgets.Gold : Color.white);
                var btn = new Rect(w - btnW + 4f, row.y + 1f, btnW - 8f, row.height - 2f);
                if (may)
                {
                    if (session != null && !session.Finished) { if (Widgets.ButtonText(btn, "WFL_Edu_EndClass".Translate())) ClassSessions.End(pawn, false, null); }
                    else if (Widgets.ButtonText(btn, "WFL_Edu_HoldNow".Translate())) HoldClass.StartTeacher(pawn, now);
                }
                y += row.height;
            }
            return CommunityPageLayout.Help(s, y + 4f, "WFL_Edu_TeachersHelp".Translate());
        }

        /// <summary>What each pupil has been taught lately, from the lesson log: subject and number of classes.</summary>
        private static Dictionary<int, Dictionary<string, int>> learningCache; private static int learningAt = -9999, learningCount = -1;
        private static Dictionary<int, Dictionary<string, int>> Learning(FamilyRegistry reg)
        {
            int now = FrameworkServices.Now; var log = reg.EducationRecord().LessonLog;
            if (learningCache != null && now >= learningAt && now - learningAt < 600 && log.Count == learningCount) return learningCache;          // the log is parsed line by line, so not every frame
            var map = new Dictionary<int, Dictionary<string, int>>();
            foreach (var text in log)
            {
                LessonRecord rec; try { rec = LessonRecord.Parse(text); } catch (Exception) { continue; }
                if (rec == null || rec.Pupil <= 0 || rec.Classes <= 0) continue;
                if (!map.TryGetValue(rec.Pupil, out var skills)) map[rec.Pupil] = skills = new Dictionary<string, int>();
                skills[rec.Skill] = (skills.TryGetValue(rec.Skill, out int n) ? n : 0) + rec.Classes;
            }
            learningCache = map; learningAt = now; learningCount = log.Count; return map;
        }

        public static float Pupils(Rect s, FamilyComponent c)
        {
            var reg = c.Registry; var settings = FamilyLifeMod.Settings; float y = 0f;
            if (settings != null)
            {
                bool adults = settings.adultStudents; Widgets.CheckboxLabeled(new Rect(0f, 0f, Mathf.Min(s.width, 420f), 24f), "WFL_S_AdultStudents".Translate(), ref adults);
                if (adults != settings.adultStudents) { settings.adultStudents = adults; settings.Write(); }
                y = 28f;
            }
            bool includeAdults = settings != null && settings.adultStudents;
            var pupils = reg.Persons.Where(p => p.InColony && (p.Juvenile || includeAdults)).Select(p => new KeyValuePair<int, Pawn>(p.Id, c.TryGetPawn(p.Id))).Where(k => k.Value != null).OrderBy(k => k.Value.ageTracker.AgeBiologicalYears).ToList();
            if (pupils.Count == 0) return CommunityPageLayout.Line(s, y, "WFL_Edu_NoPupils".Translate(), true);
            float w = s.width, nameW = Mathf.Max(110f, w * .2f), ageW = 60f, statusW = Mathf.Max(120f, w * .22f), learnW = w - nameW - ageW - statusW;
            y = CommunityWidgets.Header(s, y, new List<KeyValuePair<string, float>> { new KeyValuePair<string, float>("WFL_Table_Name".Translate(), nameW), new KeyValuePair<string, float>("WFL_Table_Age".Translate(), ageW), new KeyValuePair<string, float>("WFL_Edu_Learning".Translate(), learnW), new KeyValuePair<string, float>("WFL_Edu_Status".Translate(), statusW) });
            var learning = Learning(reg); var assess = reg.Assessments(); int i = 0;
            foreach (var kv in pupils.Take(40))
            {
                var row = CommunityWidgets.Row(s, y, i++); var pawn = kv.Value;
                CommunityWidgets.NameButton(new Rect(4f, row.y, nameW - 6f, row.height), pawn);
                CommunityWidgets.Cell(new Rect(nameW, row.y, ageW, row.height), pawn.ageTracker.AgeBiologicalYears.ToString(), CommunityWidgets.Muted);
                string learned = learning.TryGetValue(kv.Key, out var skills) ? string.Join(", ", skills.OrderByDescending(x => x.Value).Take(3).Select(x => SubjectLabel(x.Key) + " x" + x.Value)) : "WFL_Edu_NoLessons".Translate().ToString();
                CommunityWidgets.Cell(new Rect(nameW + ageW, row.y, learnW - 6f, row.height), learned, learning.ContainsKey(kv.Key) ? Color.white : CommunityWidgets.Muted);
                string status = pawn.ageTracker.AgeBiologicalYears < 3 ? "WFL_UX_NotEnrolled".Translate().ToString() : ClassSessions.Board.IsBusy(kv.Key) ? "WFL_Edu_InClass".Translate().ToString() : assess.PupilBusy(kv.Key) ? "WFL_Edu_Sitting".Translate().ToString() : "WFL_Edu_Free".Translate().ToString();
                CommunityWidgets.Cell(new Rect(nameW + ageW + learnW, row.y, statusW, row.height), status, ClassSessions.Board.IsBusy(kv.Key) ? CommunityWidgets.Gold : CommunityWidgets.Muted);
                y += row.height;
            }
            return CommunityPageLayout.Help(s, y + 4f, "WFL_Edu_PupilsHelp".Translate());
        }
    }
}

