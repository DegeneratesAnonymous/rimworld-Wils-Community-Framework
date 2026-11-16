using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class WardenRoomTests
    {
        private static WardenRoomFacts R(bool enclosed = true, bool cell = false, int beds = 0, int chairs = 0, int tables = 0, int cells = 20, float clean = 0f) => new WardenRoomFacts { Enclosed = enclosed, PrisonCell = cell, Beds = beds, Chairs = chairs, Tables = tables, Cells = cells, Cleanliness = clean };

        [Fact]
        public void A_prison_cell_is_whatever_the_game_says_is_one()
        {
            Assert.Equal(WardenRoom.Cell, WardenRooms.Classify(R(cell: true, beds: 1))); Assert.Equal(WardenRoom.Cell, WardenRooms.Classify(R(cell: true, beds: 0, chairs: 6, tables: 2)));          // furniture does not change what it is
        }

        [Fact]
        public void An_interview_room_has_a_table_and_two_chairs_and_a_visiting_room_four_and_neither_has_a_bed()
        {
            Assert.Equal(WardenRoom.InterviewRoom, WardenRooms.Classify(R(chairs: 2, tables: 1))); Assert.Equal(WardenRoom.InterviewRoom, WardenRooms.Classify(R(chairs: 3, tables: 1)));
            Assert.Equal(WardenRoom.VisitingRoom, WardenRooms.Classify(R(chairs: 4, tables: 1))); Assert.Equal(WardenRoom.VisitingRoom, WardenRooms.Classify(R(chairs: 9, tables: 3)));
            Assert.Equal(WardenRoom.None, WardenRooms.Classify(R(chairs: 1, tables: 1))); Assert.Equal(WardenRoom.None, WardenRooms.Classify(R(chairs: 4, tables: 0)));
            Assert.Equal(WardenRoom.None, WardenRooms.Classify(R(chairs: 4, tables: 1, beds: 1)));          // somebody's bedroom
        }

        [Fact]
        public void A_room_with_no_walls_is_no_room_and_nothing_is_nothing()
        {
            Assert.Equal(WardenRoom.None, WardenRooms.Classify(R(enclosed: false, cell: true, chairs: 4, tables: 1))); Assert.Equal(WardenRoom.None, WardenRooms.Classify(null)); Assert.Equal(WardenRoom.None, WardenRooms.Classify(R()));
        }

        [Fact]
        public void A_cell_is_scored_for_a_bed_space_and_cleanliness()
        {
            Assert.Equal(3, WardenRooms.Humane(R(cell: true, beds: 1, cells: 6, clean: 0f))); Assert.Equal(3, WardenRooms.Humane(R(cell: true, beds: 2, cells: 12, clean: 1f)));
            Assert.Equal(2, WardenRooms.Humane(R(cell: true, beds: 1, cells: 5, clean: 0f))); Assert.Equal(2, WardenRooms.Humane(R(cell: true, beds: 1, cells: 6, clean: -.1f)));
            Assert.Equal(1, WardenRooms.Humane(R(cell: true, beds: 0, cells: 3, clean: 0f)));          // clean, but no bed and cramped
            Assert.Equal(2, WardenRooms.Humane(R(cell: true, beds: 3, cells: 12, clean: 0f)));          // three beds in twelve cells is crowded: a bed and a clean floor only
        }

        [Fact]
        public void Humane_means_at_least_two_points_and_only_cells_are_scored()
        {
            Assert.True(WardenRooms.IsHumane(R(cell: true, beds: 1, cells: 6, clean: 0f))); Assert.True(WardenRooms.IsHumane(R(cell: true, beds: 1, cells: 5, clean: 0f)));
            Assert.False(WardenRooms.IsHumane(R(cell: true, beds: 1, cells: 5, clean: -1f)));          // a bed and nothing else
            Assert.False(WardenRooms.IsHumane(R(cell: true, beds: 0, cells: 20, clean: -1f)));          // roomy but dirty with no bed: one point
            Assert.Equal(0, WardenRooms.Humane(R(cell: false, beds: 1, cells: 20, clean: 1f))); Assert.Equal(0, WardenRooms.Humane(null));
        }
    }
}
