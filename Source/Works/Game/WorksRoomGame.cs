using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>Recognises the places Works uses from what is in them and above them. Never saved; each room is looked at again at most once every 600 ticks.</summary>
    public static class WorksRoomGame
    {
        private struct Seen { public int Tick; public WorksPlace Kind; }
        private static readonly Dictionary<long, Seen> cache = new Dictionary<long, Seen>();
        private const int Keep = 600, MaxCells = 400;
        public static void Reset() => cache.Clear();

        private static WorksRoomFacts Facts(Room room)
        {
            var f = new WorksRoomFacts { Enclosed = room.ProperRoom && !room.TouchesMapEdge, Outdoors = room.PsychologicallyOutdoors, Cells = room.CellCount };
            int scanned = 0; var counted = new HashSet<Thing>();
            foreach (var cell in room.Cells)
            {
                if (++scanned > MaxCells) break;
                if (room.Map.roofGrid.RoofAt(cell)?.isThickRoof == true) f.ThickRoofCells++;
                foreach (var thing in cell.GetThingList(room.Map))
                {
                    if (!(thing is Building b) || !counted.Add(b)) continue;
                    if (b is Building_Bed) f.Beds++; else if (b is Building_WorkTable) f.Benches++;
                }
            }
            return f;
        }

        public static WorksPlace KindOf(Room room)
        {
            if (room == null || room.Map == null) return WorksPlace.None;
            int now = Find.TickManager.TicksGame; long key = ((long)room.Map.uniqueID << 32) ^ (uint)room.ID;
            if (cache.TryGetValue(key, out var seen) && now >= seen.Tick && now - seen.Tick < Keep) return seen.Kind;
            var kind = WorksRooms.Classify(Facts(room)); cache[key] = new Seen { Tick = now, Kind = kind }; return kind;
        }

        /// <summary>The place a piece of work was done in: the room of its cell, or (for digging, where the cell was solid rock) of a neighbouring cell.</summary>
        public static WorksPlace PlaceOf(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map)) return WorksPlace.None;
            var room = cell.GetRoom(map); var kind = KindOf(room); if (kind != WorksPlace.None) return kind;
            foreach (var next in GenAdj.AdjacentCells) { var c = cell + next; if (!c.InBounds(map)) continue; kind = KindOf(c.GetRoom(map)); if (kind != WorksPlace.None) return kind; }
            return WorksPlace.None;
        }
    }

    /// <summary>Community: Works. The places the game has recognised and what each specialty does.</summary>
    public static class WorksPage
    {
        public static float Draw(Rect r, FamilyComponent c)
        {
            float staffing = CommunityStaffing.Draw(r, 0f, c, "wils.works");
            float y = CommunityPageLayout.Section(r, 0f, staffing, r.width, "WFL_Works_PlacesTitle".Translate(), s =>
            {
                int workshops = 0, mines = 0;
                foreach (var map in Find.Maps)
                {
                    if (!map.IsPlayerHome) continue;
                    foreach (var room in map.regionGrid.AllRooms) { var kind = WorksRoomGame.KindOf(room); if (kind == WorksPlace.Workshop) workshops++; else if (kind == WorksPlace.Mine) mines++; }
                }
                float h = CommunityPageLayout.Line(s, 0f, "WFL_Works_PlacesFound".Translate(workshops, mines));
                return CommunityPageLayout.Help(s, h, "WFL_Works_PlacesHelp".Translate());
            });
            y = CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Works_RoundsTitle".Translate(), s =>
            {
                var ledger = c.Registry.Works(); bool on = ledger.RoundsOn; Widgets.CheckboxLabeled(new Rect(0f, 0f, s.width, 24f), "WFL_Works_RoundsOn".Translate(), ref on); ledger.RoundsOn = on;
                return CommunityPageLayout.Help(s, 28f, "WFL_Works_RoundsHelp".Translate());
            });
            y = CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Works_SpecialtiesTitle".Translate(), s =>
            {
                float h = 0f;
                foreach (var specialty in WorksCareers.BuilderSpecialties.Concat(WorksCareers.MinerSpecialties))
                    h = CommunityPageLayout.Line(s, h, (WorksCareers.Role(specialty) + "_label").Translate() + ": " + ("WFL_Works_" + specialty + "Bonus").Translate());
                return h;
            });
            return y;
        }
    }
}

