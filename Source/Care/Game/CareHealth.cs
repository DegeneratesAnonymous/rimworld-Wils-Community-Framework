using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>What this module reports to the Community overview: what is working and what is not.</summary>
    internal static class CareHealth
    {
        public static IEnumerable<HealthNote> Check(FamilyComponent c, string module)
        {
            var reg = c.Registry; var list = new List<HealthNote>();
            var doctors = CareCareers.Practitioners(reg).Count(id => reg.GetPerson(id)?.InColony == true);
            int hurt = 0; foreach (var map in Find.Maps) if (map.IsPlayerHome) hurt += map.mapPawns.FreeColonistsSpawned.Count(p => p.Downed || p.health.HasHediffsNeedingTend(false));
            if (hurt > 0 && doctors == 0) list.Add(HealthNote.Bad(module, "WFL_Health_Care_NoDoctor".Translate(hurt), "WFL_Health_Care_NoDoctorHint".Translate()));
            else if (hurt > 0) list.Add(HealthNote.Good(module, "WFL_Health_Care_Tending".Translate(hurt, doctors)));
            else if (doctors > 0) list.Add(HealthNote.Good(module, "WFL_Health_Care_Quiet".Translate(doctors)));
            return list;
        }
    }
}
