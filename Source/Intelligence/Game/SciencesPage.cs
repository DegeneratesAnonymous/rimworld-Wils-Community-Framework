using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>The Sciences page: the research focus, the lead researcher, every lab and library with what is in it, and the researchers with their skill. Reads the game; only the lead choice is saved.</summary>
    public static class SciencesPage
    {
        private sealed class Place { public IntelligenceRoom Kind; public int Cells, Benches, Shelves, Equipment, Books, Papers; public IntVec3 Spot; public Map Map; }
        private static List<Place> places = new List<Place>(); private static int surveyed = -1;
        public static void Reset() { places = new List<Place>(); surveyed = -1; }

        private static bool Named(Thing t, params string[] words) { string n = (t.def.defName ?? "") + " " + (t.def.label ?? ""); return words.Any(w => n.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0); }

        /// <summary>Looks at every room of the player's maps at most once every 600 ticks (not every frame).</summary>
        private static List<Place> Survey()
        {
            int now = Find.TickManager.TicksGame;
            if (surveyed >= 0 && now >= surveyed && now - surveyed < 600) return places;
            var found = new List<Place>();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                foreach (var room in map.regionGrid.AllRooms)
                {
                    var kind = IntelligenceRoomGame.KindOf(room); if (kind == IntelligenceRoom.None) continue;
                    var p = new Place { Kind = kind, Map = map, Spot = room.Cells.FirstOrDefault() }; var counted = new HashSet<Thing>(); int scanned = 0;
                    foreach (var cell in room.Cells)
                    {
                        if (++scanned > 400) break; p.Cells++;
                        foreach (var thing in cell.GetThingList(map))
                        {
                            if (thing.def.defName == "ResearchPaper") { if (counted.Add(thing)) p.Papers += thing.stackCount; continue; }          // Research Papers
                            if (thing is Book && counted.Add(thing)) { p.Books++; continue; }          // books of the game and of book mods
                            if (!(thing is Building b) || !counted.Add(b)) continue;
                            if (Named(b, "ResearchBench")) p.Benches++; else if (Named(b, "Bookcase", "Bookshelf", "Bookshelves")) p.Shelves++;
                            else if (Named(b, "GeneExtractor", "GeneAssembler", "GeneBank", "GeneProcessor", "Xenogerm")) p.Equipment++;
                        }
                    }
                    found.Add(p);
                }
            }
            surveyed = now; return places = found;
        }

        private static string KindName(IntelligenceRoom k) => k == IntelligenceRoom.Library ? "WFL_Sciences_Library".Translate().ToString() : k == IntelligenceRoom.GeneLab ? "WFL_Sciences_GeneLab".Translate().ToString() : "WFL_Sciences_ResearchLab".Translate().ToString();

        public static float Draw(Rect r, FamilyComponent c)
        {
            float y = CommunityPageLayout.Section(r, 0f, 0f, r.width, "WFL_Sciences_FocusTitle".Translate(), s => Focus(s));
            y = CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Sciences_LeadTitle".Translate(), s => Lead(s, c));
            y = CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Sciences_PlacesTitle".Translate(), s => Places(s));
            y = CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Sciences_ResearchersTitle".Translate(), s => Researchers(s, c));
            y = CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Intelligence_SpecialtiesTitle".Translate(), s =>
            {
                CommunityPageLayout.Help(s, 0f, "WFL_Intelligence_RoomsHelp".Translate());
                foreach (var specialty in IntelligenceCareers.Specialties)
                    CommunityPageLayout.Help(s, 0f, ("wils.intelligence." + specialty.ToLowerInvariant() + "_label").Translate() + ": " + ("WFL_Intelligence_" + specialty + "Bonus").Translate());
                float h = 0f; int i = 0;
                foreach (var specialty in IntelligenceCareers.Specialties) { var row = CommunityWidgets.Row(s, h, i++, 22f); CommunityWidgets.Cell(new Rect(6f, row.y, s.width, row.height), ("wils.intelligence." + specialty.ToLowerInvariant() + "_label").Translate(), CommunityWidgets.Muted); h += row.height; }
                return h;
            });
            return y;
        }

        private static float Focus(Rect s)
        {
            var project = Find.ResearchManager?.GetProject(); float y;
            var all = DefDatabase<ResearchProjectDef>.AllDefsListForReading; int done = all.Count(p => p.IsFinished);
            if (project == null) y = CommunityPageLayout.Line(s, 0f, "WFL_Sciences_NoProject".Translate(), true);
            else
            {
                y = CommunityPageLayout.Line(s, 0f, project.LabelCap + (project.techLevel != TechLevel.Undefined ? " (" + project.techLevel.ToString() + ")" : ""));
                float pct = project.ProgressPercent;
                CommunityWidgets.Bar(new Rect(0f, y, Mathf.Min(s.width, 420f), 18f), pct, CommunityWidgets.Level(pct), pct.ToStringPercent() + "  " + Mathf.RoundToInt(project.ProgressReal) + " / " + Mathf.RoundToInt(project.CostApparent));
                y += 26f;
                if (!project.description.NullOrEmpty()) CommunityPageLayout.Help(s, y, project.description);
            }
            y = CommunityPageLayout.Line(s, y, "WFL_Sciences_Finished".Translate(done, all.Count));
            return CommunityPageLayout.Button(new Rect(0f, 0f, Mathf.Min(s.width, 260f), s.height), y, "WFL_Sciences_OpenResearch".Translate(), () => Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research));
        }

        /// <summary>Colonists who can do research, best Intellectual first.</summary>
        private static List<Pawn> Candidates()
        {
            var list = new List<Pawn>();
            foreach (var map in Find.Maps) if (map.IsPlayerHome) list.AddRange(map.mapPawns.FreeColonistsSpawned.Where(p => p.skills != null && p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Research)));
            return list.OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Intellectual).Level).ThenBy(p => p.LabelShort).ToList();
        }

        private static float Lead(Rect s, FamilyComponent c)
        {
            var ledger = c.Registry.Intelligence(); var lead = ledger.LeadId > 0 ? c.TryGetPawn(ledger.LeadId) : null; float y;
            if (lead == null || lead.Dead) { if (ledger.LeadId > 0) ledger.LeadId = 0; y = CommunityPageLayout.Line(s, 0f, "WFL_Sciences_NoLead".Translate(), true); }
            else
            {
                if (Widgets.ButtonInvisible(new Rect(0f, 0f, s.width, 22f))) CameraJumper.TryJumpAndSelect(lead);
                y = CommunityPageLayout.Line(s, 0f, "WFL_Sciences_LeadIs".Translate(lead.LabelShortCap, lead.skills.GetSkill(SkillDefOf.Intellectual).Level));
            }
            var candidates = Candidates(); float bw = Mathf.Min(s.width, 280f);
            CommunityWidgets.Choice(new Rect(0f, y, bw, 28f), "WFL_Sciences_ChooseLead".Translate(), () => candidates.Select(p => { var pawn = p; return new FloatMenuOption(pawn.LabelShortCap + " (" + "WFL_Sciences_Intellectual".Translate() + " " + pawn.skills.GetSkill(SkillDefOf.Intellectual).Level + ")", () => ledger.LeadId = pawn.thingIDNumber); }));
            y += 34f;
            if (ledger.LeadId > 0) y = CommunityPageLayout.Button(new Rect(0f, 0f, bw, s.height), y, "WFL_Sciences_ClearLead".Translate(), () => ledger.LeadId = 0);
            return CommunityPageLayout.Help(s, y, "WFL_Sciences_LeadHelp".Translate());
        }

        private static float Places(Rect s)
        {
            var list = Survey(); int labs = list.Count(p => p.Kind == IntelligenceRoom.ResearchLab), libs = list.Count(p => p.Kind == IntelligenceRoom.Library), genes = list.Count(p => p.Kind == IntelligenceRoom.GeneLab);
            float y = CommunityPageLayout.Line(s, 0f, "WFL_Intelligence_RoomsFound".Translate(libs, labs, genes));
            y = CommunityPageLayout.Line(s, y, "WFL_Sciences_Totals".Translate(list.Where(p => p.Kind == IntelligenceRoom.ResearchLab).Sum(p => p.Benches), list.Where(p => p.Kind == IntelligenceRoom.Library).Sum(p => p.Shelves), list.Where(p => p.Kind == IntelligenceRoom.GeneLab).Sum(p => p.Equipment)));
            int i = 0;
            foreach (var p in list.OrderBy(p => p.Kind))
            {
                var row = CommunityWidgets.Row(s, y, i++);
                string detail = p.Kind == IntelligenceRoom.ResearchLab ? "WFL_Sciences_BenchCount".Translate(p.Benches).ToString() : p.Kind == IntelligenceRoom.Library ? "WFL_Sciences_ShelfCount".Translate(p.Shelves).ToString() : "WFL_Sciences_EquipCount".Translate(p.Equipment).ToString();
                if (p.Kind == IntelligenceRoom.Library && (p.Books > 0 || p.Papers > 0)) detail += ", " + "WFL_Sciences_Holdings".Translate(p.Books, p.Papers);
                CommunityWidgets.Cell(new Rect(6f, row.y, s.width * .3f, row.height), KindName(p.Kind));
                CommunityWidgets.Cell(new Rect(s.width * .3f, row.y, s.width * .5f, row.height), detail + ", " + "WFL_Sciences_CellCount".Translate(p.Cells), CommunityWidgets.Muted);
                if (Widgets.ButtonText(new Rect(s.width - 70f, row.y + 2f, 66f, row.height - 4f), "WFL_Sciences_Go".Translate())) CameraJumper.TryJump(p.Spot, p.Map);
                y += row.height;
            }
            return y;
        }

        private static float Researchers(Rect s, FamilyComponent c)
        {
            var list = Candidates(); if (list.Count == 0) return CommunityPageLayout.Line(s, 0f, "WFL_Sciences_NoResearchers".Translate(), true);
            float w = s.width, nameW = Mathf.Max(120f, w * .25f), skillW = Mathf.Max(110f, w * .2f);
            float y = CommunityWidgets.Header(s, 0f, new List<KeyValuePair<string, float>> { new KeyValuePair<string, float>("WFL_Table_Name".Translate(), nameW), new KeyValuePair<string, float>("WFL_Sciences_Intellectual".Translate(), skillW), new KeyValuePair<string, float>("WFL_Sciences_Doing".Translate(), w - nameW - skillW) });
            int i = 0; int lead = c.Registry.Intelligence().LeadId;
            foreach (var p in list.Take(24))
            {
                var row = CommunityWidgets.Row(s, y, i++); var sk = p.skills.GetSkill(SkillDefOf.Intellectual);
                CommunityWidgets.NameButton(new Rect(4f, row.y, nameW - 6f, row.height), p);
                CommunityWidgets.Bar(new Rect(nameW, row.y + 6f, skillW - 12f, row.height - 12f), sk.Level / 20f, CommunityWidgets.Level(sk.Level / 20f), sk.Level + (sk.passion == Passion.Major ? " **" : sk.passion == Passion.Minor ? " *" : ""));
                string doing = p.thingIDNumber == lead ? "WFL_Sciences_LeadMark".Translate().ToString() : p.CurJob != null && p.CurJob.def == JobDefOf.Research ? "WFL_Sciences_Researching".Translate().ToString() : "-";
                CommunityWidgets.Cell(new Rect(nameW + skillW, row.y, w - nameW - skillW, row.height), doing, p.thingIDNumber == lead ? CommunityWidgets.Gold : CommunityWidgets.Muted);
                y += row.height;
            }
            return y;
        }
    }
}
