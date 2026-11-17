using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>What this module reports to the Community overview: what is working and what is not.</summary>
    internal static class MilitaryHealth
    {
        public static IEnumerable<HealthNote> Check(FamilyComponent c, string module)
        {
            var reg = c.Registry; var list = new List<HealthNote>();
            int serving = 0; foreach (var map in Find.Maps) if (map.IsPlayerHome) serving += map.mapPawns.FreeColonistsSpawned.Count(MilitaryRoles.Serving);
            int unarmed = Find.Maps.Where(m => m.IsPlayerHome).SelectMany(m => m.mapPawns.FreeColonistsSpawned).Count(p => MilitaryRoles.Serving(p) && MilitaryArmory.Needs(p, MilitaryRoles.ActiveRole(p)));
            if (unarmed > 0) list.Add(HealthNote.Warn(module, "WFL_UX_Unarmed".Translate(unarmed), "WFL_Military_ArmoryDesc".Translate()));
            var level = FrameworkServices.ColonyAlertPublished;
            if (level == ColonyAlertLevel.Red && serving == 0) list.Add(HealthNote.Bad(module, "WFL_Health_Mil_RedNoSoldiers".Translate(), "WFL_Health_Mil_RedNoSoldiersHint".Translate()));
            else if (level == ColonyAlertLevel.Red) list.Add(HealthNote.Warn(module, "WFL_Health_Mil_Red".Translate(serving), "WFL_Health_Mil_RedHint".Translate()));
            else if (serving > 0) list.Add(HealthNote.Good(module, "WFL_Health_Mil_Ready".Translate(serving)));
            return list;
        }
    }
}

