using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>What this module reports to the Community overview: what is working and what is not.</summary>
    internal static class WardensHealth
    {
        public static IEnumerable<HealthNote> Check(FamilyComponent c, string module)
        {
            var reg = c.Registry; var list = new List<HealthNote>();
            int prisoners = 0, guards = 0; foreach (var map in Find.Maps) if (map.IsPlayerHome) { prisoners += map.mapPawns.PrisonersOfColony.Count; guards += map.mapPawns.FreeColonistsSpawned.Count(p => WardenStories.ActiveRole(p) != null); }
            if (prisoners > 0 && guards == 0) list.Add(HealthNote.Bad(module, "WFL_Health_Ward_NoGuard".Translate(prisoners), "WFL_Health_Ward_NoGuardHint".Translate()));
            else if (prisoners > 0) list.Add(HealthNote.Good(module, "WFL_Health_Ward_Watched".Translate(prisoners, guards)));
            var held = new List<Pawn>(); foreach (var map in Find.Maps) if (map.IsPlayerHome) held.AddRange(map.mapPawns.PrisonersOfColony);
            int strikes = PrisonerRealismLink.HungerStrikes(held);
            if (strikes > 0) list.Add(HealthNote.Bad(module, "WFL_Health_Ward_Hunger".Translate(strikes), "WFL_Health_Ward_HungerHint".Translate()));
            return list;
        }
    }
}
