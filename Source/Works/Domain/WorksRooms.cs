using System;

namespace FamilyLife.Domain
{
    public enum WorksPlace : byte { None = 0, Workshop = 1, Mine = 2 }

    /// <summary>What a room holds and is, as the game side reads it.</summary>
    public sealed class WorksRoomFacts
    {
        public bool Enclosed, Outdoors;
        public int Benches, Beds, Cells, ThickRoofCells;
    }

    /// <summary>
    /// The places Works recognises, never painted. A Workshop is an enclosed room with at least two work benches and no bed. A Mine is a dug-out room of at least six cells, not outdoors, most of which lies
    /// under thick mountain roof. Work done in a place that suits it earns <see cref="RoomExperience"/> times the experience (at least one more point).
    /// </summary>
    public static class WorksRooms
    {
        public const int MinBenches = 2, MinMineCells = 6;
        public const float RoomExperience = 1.25f;

        public static WorksPlace Classify(WorksRoomFacts f)
        {
            if (f == null || f.Outdoors) return WorksPlace.None;
            if (f.Enclosed && f.Beds == 0 && f.Benches >= MinBenches) return WorksPlace.Workshop;
            if (f.Cells >= MinMineCells && f.ThickRoofCells * 2 >= f.Cells) return WorksPlace.Mine;
            return WorksPlace.None;
        }

        /// <summary>The place a kind of work suits: building and repair in a workshop, digging in a mine.</summary>
        public static WorksPlace Suits(string kind)
        {
            switch (kind)
            {
                case "works.build": case "works.fix": case "works.repair": case "works.smooth": return WorksPlace.Workshop;
                case "works.mine": case "works.drill": return WorksPlace.Mine;
                default: return WorksPlace.None;
            }
        }

        public static int Points(int points, WorksPlace suits, WorksPlace where)
            => suits != WorksPlace.None && suits == where ? Math.Max(points + 1, (int)Math.Round(points * RoomExperience)) : points;
    }
}
