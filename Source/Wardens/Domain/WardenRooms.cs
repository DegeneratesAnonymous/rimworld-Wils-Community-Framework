using System;

namespace FamilyLife.Domain
{
    /// <summary>The kinds of room Wardens recognise. Recognised from what is in them, never painted.</summary>
    public enum WardenRoom : byte { None = 0, Cell = 1, InterviewRoom = 2, VisitingRoom = 3 }

    /// <summary>What a room holds and is, as the game side reads it.</summary>
    public sealed class WardenRoomFacts
    {
        public bool Enclosed;
        /// <summary>The game itself treats the room as a prison cell or prison barracks.</summary>
        public bool PrisonCell;
        public int Beds, Chairs, Tables, Cells;
        public float Cleanliness;
    }

    public static class WardenRooms
    {
        public const int InterviewChairs = 2, VisitingChairs = 4, SpacePerBed = 6;

        /// <summary>
        /// A prison cell is whatever the game itself says is one. Otherwise, an enclosed room with no bed and a table is a Visiting Room if it has at least <see cref="VisitingChairs"/> chairs and an
        /// Interview Room if it has at least <see cref="InterviewChairs"/>. (A room with a bed is somebody's bedroom, not an interview room.)
        /// </summary>
        public static WardenRoom Classify(WardenRoomFacts f)
        {
            if (f == null || !f.Enclosed) return WardenRoom.None;
            if (f.PrisonCell) return WardenRoom.Cell;
            if (f.Beds > 0 || f.Tables < 1) return WardenRoom.None;
            if (f.Chairs >= VisitingChairs) return WardenRoom.VisitingRoom;
            return f.Chairs >= InterviewChairs ? WardenRoom.InterviewRoom : WardenRoom.None;
        }

        /// <summary>How humane a cell is, 0 to 3: it has a bed, there is room to move (at least <see cref="SpacePerBed"/> cells for each bed) and it is clean (cleanliness not below zero).</summary>
        public static int Humane(WardenRoomFacts f)
        {
            if (f == null || !f.PrisonCell) return 0;
            int score = 0; if (f.Beds >= 1) score++;
            if (f.Cells >= SpacePerBed * Math.Max(1, f.Beds)) score++;
            if (f.Cleanliness >= 0f) score++;
            return score;
        }

        public const int HumaneScore = 2;
        public static bool IsHumane(WardenRoomFacts f) => Humane(f) >= HumaneScore;
    }
}
