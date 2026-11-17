using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>What this module reports to the Community overview: what is working and what is not.</summary>
    internal static class EducationHealth
    {
        public static IEnumerable<HealthNote> Check(FamilyComponent c, string module)
        {
            var reg = c.Registry; var list = new List<HealthNote>();
            int children = reg.Persons.Count(p => p.InColony && p.Juvenile); int teachers = EducationCareers.Instructors(reg).Count;
            if (children > 0 && teachers == 0) list.Add(HealthNote.Warn(module, "WFL_Health_Edu_NoTeacher".Translate(children), "WFL_Health_Edu_NoTeacherHint".Translate()));
            else if (children > 0) list.Add(HealthNote.Good(module, "WFL_Health_Edu_Teaching".Translate(children, teachers)));
            return list;
        }
    }
}
