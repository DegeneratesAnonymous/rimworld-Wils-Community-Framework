using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>Recognises the rooms Wardens use from what is in them. Never saved; each room is looked at again at most once every 600 ticks.</summary>
    public static class WardenRoomGame
    {
        private struct Seen { public int Tick; public WardenRoom Kind; public int Humane; }
        private static readonly Dictionary<long, Seen> cache = new Dictionary<long, Seen>();
        private const int Keep = 600, MaxCells = 400;
        public static void Reset() => cache.Clear();

        private static WardenRoomFacts Facts(Room room)
        {
            var f = new WardenRoomFacts { Enclosed = room.ProperRoom && !room.TouchesMapEdge, PrisonCell = room.Role == RoomRoleDefOf.PrisonCell || room.Role == RoomRoleDefOf.PrisonBarracks, Cells = room.CellCount, Cleanliness = room.GetStat(RoomStatDefOf.Cleanliness) };
            if (!f.Enclosed) return f;
            var counted = new HashSet<Thing>(); int scanned = 0;
            foreach (var cell in room.Cells)
            {
                if (++scanned > MaxCells) break;
                foreach (var thing in cell.GetThingList(room.Map))
                {
                    if (!(thing is Building b) || !counted.Add(b)) continue;
                    if (b is Building_Bed) f.Beds++;
                    if (b.def.building != null && b.def.building.isSittable) f.Chairs++;
                    if (b.def.surfaceType == SurfaceType.Eat) f.Tables++;
                }
            }
            return f;
        }

        private static Seen Look(Room room)
        {
            if (room == null || room.Map == null) return new Seen();
            int now = Find.TickManager.TicksGame; long key = ((long)room.Map.uniqueID << 32) ^ (uint)room.ID;
            if (cache.TryGetValue(key, out var seen) && now >= seen.Tick && now - seen.Tick < Keep) return seen;
            var f = Facts(room); seen = new Seen { Tick = now, Kind = WardenRooms.Classify(f), Humane = WardenRooms.Humane(f) }; cache[key] = seen; return seen;
        }

        public static WardenRoom KindOf(Room room) => Look(room).Kind;
        public static int HumaneOf(Room room) => Look(room).Humane;
    }

    /// <summary>Community: Wardens. The rooms the game has recognised, each warden's care and outcomes (the treatment record), and what each specialty does.</summary>
    public static class WardensPage
    {
        public static float Draw(Rect r, FamilyComponent c)
        {
            float y = CommunityPageLayout.Section(r, 0f, 0f, r.width, "WFL_Wardens_RoomsTitle".Translate(), s =>
            {
                int cells = 0, humane = 0, interview = 0, visiting = 0;
                foreach (var map in Find.Maps)
                {
                    if (!map.IsPlayerHome) continue;
                    foreach (var room in map.regionGrid.AllRooms)
                    {
                        var kind = WardenRoomGame.KindOf(room);
                        if (kind == WardenRoom.Cell) { cells++; if (WardenRoomGame.HumaneOf(room) >= WardenRooms.HumaneScore) humane++; }
                        else if (kind == WardenRoom.InterviewRoom) interview++; else if (kind == WardenRoom.VisitingRoom) visiting++;
                    }
                }
                float h = CommunityPageLayout.Line(s, 0f, "WFL_Wardens_RoomsFound".Translate(cells, humane, interview, visiting));
                return CommunityPageLayout.Line(s, h, "WFL_Wardens_RoomsHelp".Translate(), true);
            });
            y = CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Wardens_RecordTitle".Translate(), s =>
            {
                var ledger = c.Registry.Wardens(); float h = CommunityPageLayout.Line(s, 0f, "WFL_Wardens_RecordHelp".Translate(), true); int shown = 0;
                foreach (int id in ledger.Wardens.OrderBy(x => x))
                {
                    var st = ledger.Of(id); if (st.Samples == 0 && st.Outcomes == 0) continue;
                    var pawn = c.TryGetPawn(id); string name = pawn != null ? pawn.LabelShortCap : "?";
                    h = CommunityPageLayout.Line(s, h, "WFL_Wardens_RecordLine".Translate(name, st.Good, st.Fair, st.Poor, st.KinGood, st.KinTotal, st.Recruited, st.Released, st.Enslaved, st.Executed, st.Died));
                    shown++;
                }
                return shown == 0 ? CommunityPageLayout.Line(s, h, "WFL_Wardens_RecordNone".Translate(), true) : h;
            });
            y = CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Wardens_SpecialtiesTitle".Translate(), s =>
            {
                float h = 0f;
                foreach (var specialty in WardenCareers.Specialties)
                    h = CommunityPageLayout.Line(s, h, ("wils.wardens." + specialty.ToLowerInvariant() + "_label").Translate() + ": " + ("WFL_Wardens_" + specialty + "Bonus").Translate(), true);
                return h;
            });
            return y;
        }
    }
}
