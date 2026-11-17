using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// Patient management and colony health for the Community Care page: who needs treatment (with the game's own medical care setting beside each name) and a compact health tile for every colonist.
    /// Reads the game's health state; the only thing changed is the same medical care level the colonist's own tab sets.
    /// </summary>
    internal static class CarePatients
    {
        private static bool showResidents;
        private static List<Pawn> colonistsCache = new List<Pawn>(); private static int colonistsAt = -9999;
        private static List<Pawn> Colonists()
        {
            int now = FrameworkServices.Now; if (now >= colonistsAt && now - colonistsAt < 60) return colonistsCache;
            var list = new List<Pawn>();
            foreach (var map in Find.Maps) if (map.IsPlayerHome) list.AddRange(map.mapPawns.FreeColonistsSpawned);
            colonistsCache = list; colonistsAt = now; return list;
        }

        private static float Health(Pawn p) => p.health.summaryHealth.SummaryHealthPercent;
        private static bool NeedsTending(Pawn p) => p.health.HasHediffsNeedingTend(false);
        private static bool Bleeding(Pawn p) => p.health.hediffSet.BleedRateTotal > .01f;
        private static bool IsPatient(Pawn p) => p.Downed || NeedsTending(p) || Bleeding(p) || Health(p) < .8f;

        /// <summary>The worst few conditions as short text.</summary>
        private static string Conditions(Pawn p)
        {
            var parts = new List<string>();
            if (p.Downed) parts.Add((p.ageTracker.AgeBiologicalYears < 3 ? "WFL_UX_InfantAssistance" : "WFL_Care_Downed").Translate());
            if (Bleeding(p)) parts.Add("WFL_Care_Bleeding".Translate());
            foreach (var h in p.health.hediffSet.hediffs.Where(x => x.Visible && (x.Bleeding || x.TendableNow(false) || x.def.makesSickThought)).OrderByDescending(x => x.Severity).Take(2)) parts.Add(h.LabelCap);
            return parts.Count == 0 ? "-" : string.Join(", ", parts);
        }

        private static string Where(Pawn p)
        {
            var bed = p.CurrentBed();
            if (bed != null) return bed.Medical ? "WFL_Care_MedicalBed".Translate().ToString() : "WFL_Care_OrdinaryBed".Translate().ToString();
            return p.Downed ? "WFL_Care_Floor".Translate().ToString() : "-";
        }

        public static float Patients(Rect s)
        {
            var all = Colonists(); var patients = all.Where(IsPatient).OrderByDescending(Bleeding).ThenByDescending(NeedsTending).ThenBy(Health).ToList();
            float y = CommunityPageLayout.Line(s, 0f, "WFL_Care_PatientSummary".Translate(all.Count, patients.Count, all.Count(NeedsTending), all.Count(p => p.Downed)));
            if (patients.Count == 0) return CommunityPageLayout.Line(s, y, "WFL_Care_NoPatients".Translate(), true);
            float w = s.width, nameW = Mathf.Max(100f, w * .15f), barW = Mathf.Max(80f, w * .11f), careW = Mathf.Max(120f, w * .16f), whereW = Mathf.Max(90f, w * .12f), condW = w - nameW - barW - careW - whereW - 60f;
            y = CommunityWidgets.Header(s, y, new List<KeyValuePair<string, float>>
            {
                new KeyValuePair<string, float>("WFL_Table_Name".Translate(), nameW), new KeyValuePair<string, float>("WFL_Table_Health".Translate(), barW),
                new KeyValuePair<string, float>("WFL_Care_Conditions".Translate(), condW), new KeyValuePair<string, float>("WFL_Care_Bed".Translate(), whereW), new KeyValuePair<string, float>("WFL_Care_Level".Translate(), careW + 60f)
            });
            int i = 0;
            foreach (var p in patients.Take(30))
            {
                var row = CommunityWidgets.Row(s, y, i++); float x = 0f; float h = Health(p);
                GUI.color = Bleeding(p) ? CommunityWidgets.Poor : NeedsTending(p) ? CommunityWidgets.Fair : Color.white; CommunityWidgets.NameButton(new Rect(4f, row.y, nameW - 6f, row.height), p); GUI.color = Color.white; x += nameW;
                CommunityWidgets.Bar(new Rect(x, row.y + 6f, barW - 8f, row.height - 12f), h, CommunityWidgets.Level(h), h.ToStringPercent()); x += barW;
                TooltipHandler.TipRegion(new Rect(x, row.y, condW - 6f, row.height), Conditions(p));
                CommunityWidgets.Cell(new Rect(x, row.y, condW - 6f, row.height), Conditions(p).Truncate(Mathf.Max(1f, condW - 6f)), NeedsTending(p) ? CommunityWidgets.Fair : Color.white); x += condW;
                CommunityWidgets.Cell(new Rect(x, row.y, whereW, row.height), Where(p), CommunityWidgets.Muted); x += whereW;
                if (p.playerSettings != null) MedicalCareUtility.MedicalCareSelectButton(new Rect(x, row.y + 1f, careW + 54f, row.height - 2f), p);
                y += row.height;
            }
            return CommunityPageLayout.Help(s, y + 4f, "WFL_Care_PatientsHelp".Translate());
        }

        /// <summary>A tile per colonist: a name and a health bar coloured by how they are, pain and tending shown by a mark. Click to go to them.</summary>
        public static float Overview(Rect s)
        {
            if (Widgets.ButtonText(new Rect(0f, 0f, s.width, 30f), (showResidents ? "− " : "+ ") + "WFL_UX_AllResidents".Translate(Colonists().Count))) showResidents = !showResidents;
            if (!showResidents) return 36f;
            GUI.BeginGroup(new Rect(0f, 36f, s.width, 10000f));
            try { return 36f + ResidentTiles(s); } finally { GUI.EndGroup(); }
        }

        private static float ResidentTiles(Rect s)
        {
            var all = Colonists().OrderBy(Health).ThenBy(p => p.LabelShort).ToList();
            if (all.Count == 0) return CommunityPageLayout.Line(s, 0f, "WFL_Care_NoColonists".Translate(), true);
            float tileW = 150f, tileH = 38f, gap = 6f; int perRow = Mathf.Max(1, Mathf.FloorToInt((s.width + gap) / (tileW + gap)));
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i]; var tile = new Rect((i % perRow) * (tileW + gap), (i / perRow) * (tileH + gap), tileW, tileH);
                float h = Health(p); Widgets.DrawBoxSolid(tile, new Color(.16f, .16f, .17f));
                CommunityWidgets.Bar(new Rect(tile.x + 4f, tile.yMax - 12f, tile.width - 8f, 8f), h, CommunityWidgets.Level(h));
                string marks = (p.Downed ? " [D]" : "") + (NeedsTending(p) ? " [+]" : "") + (Bleeding(p) ? " [!]" : "");
                Text.Font = GameFont.Tiny; Text.Anchor = TextAnchor.MiddleLeft; Widgets.Label(new Rect(tile.x + 6f, tile.y + 2f, tile.width - 12f, 20f), p.LabelShortCap + marks);
                Text.Anchor = TextAnchor.UpperLeft; Text.Font = GameFont.Small;
                if (Mouse.IsOver(tile)) Widgets.DrawHighlight(tile);
                TooltipHandler.TipRegion(tile, p.LabelCap + ": " + h.ToStringPercent() + "\n" + Conditions(p) + "\n" + "WFL_Table_ClickToJump".Translate());
                if (Widgets.ButtonInvisible(tile)) CameraJumper.TryJumpAndSelect(p);
            }
            int rows = (all.Count + perRow - 1) / perRow;
            CommunityPageLayout.Help(s, 0f, "WFL_Care_OverviewHelp".Translate());
            return rows * (tileH + gap) + 2f;
        }
    }
}

