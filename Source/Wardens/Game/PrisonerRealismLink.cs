using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// Optional link to Prisoner Realism: its conditions (hunger strike, despair, institutionalized, solitary and so on) are hediffs whose names start with PR_. They are read as they are, so a new one in an update shows up
    /// without a change here. Read only; nothing in that mod is changed.
    /// </summary>
    internal static class PrisonerRealismLink
    {
        private static bool? present;
        public static bool Present => (present ?? (present = DefDatabase<HediffDef>.GetNamedSilentFail("PR_HungerStrike") != null || DefDatabase<HediffDef>.AllDefs.Any(d => d.defName.StartsWith("PR_")))).Value;

        public static List<Hediff> Conditions(Pawn p) => !Present || p?.health?.hediffSet == null ? new List<Hediff>() : p.health.hediffSet.hediffs.Where(h => h.def.defName.StartsWith("PR_") && h.Visible).ToList();

        public static int HungerStrikes(IEnumerable<Pawn> prisoners) => !Present ? 0 : prisoners.Count(p => p.health?.hediffSet?.hediffs.Any(h => h.def.defName == "PR_HungerStrike") == true);

        /// <summary>Small coloured squares at the right edge of a cell, one per condition (red for harmful ones), with the names on hover.</summary>
        public static void DrawMarks(Rect cell, Pawn p)
        {
            var list = Conditions(p); if (list.Count == 0) return;
            float x = cell.xMax - 12f;
            foreach (var h in list.Take(3)) { Widgets.DrawBoxSolid(new Rect(x, cell.y + cell.height / 2f - 4f, 8f, 8f), h.def.isBad ? CommunityWidgets.Poor : CommunityWidgets.Fair); x -= 11f; }
            TooltipHandler.TipRegion(cell, string.Join("\n", list.Select(h => h.LabelCap.ToString())));
        }
    }
}
