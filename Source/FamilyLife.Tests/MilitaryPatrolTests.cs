using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class MilitaryPatrolTests
    {
        private static PatrolSpot S(int id, int x, int z = 0, PostKind type = PostKind.Mixed, int route = 1) => new PatrolSpot { Id = id, X = x, Z = z, Type = type, Route = route };

        // ------------------------------------------------------------------ the book

        [Fact]
        public void Routes_are_named_numbered_bounded_and_removing_one_takes_its_spots()
        {
            var b = new PatrolBook(); var one = b.AddRoute(); var two = b.AddRoute(); Assert.Equal("Route 1", one.Name); Assert.Equal("Route 2", two.Name);
            b.RemoveRoute(one.Id); Assert.Equal("Route 1", b.AddRoute().Name);          // the lowest unused number is reused, the id is not
            Assert.Equal("North wall", b.AddRoute("  North wall ").Name);
            b.Place(two.Id, PostKind.Melee, 1, 1, 1); b.Place(two.Id, PostKind.Ranged, 1, 2, 1); Assert.Equal(2, b.InRoute(two.Id).Count());
            Assert.True(b.RemoveRoute(two.Id)); Assert.Empty(b.Spots.Where(s => s.Route == two.Id)); Assert.False(b.RemoveRoute(two.Id));
            var full = new PatrolBook(); for (int i = 0; i < PatrolBook.MaxRoutes; i++) Assert.NotNull(full.AddRoute()); Assert.Null(full.AddRoute());
            Assert.True(one.Id < b.Routes.Max(r => r.Id));          // ids only grow
        }

        [Fact]
        public void A_route_may_hold_any_number_of_spots_up_to_the_overall_bound()
        {
            var b = new PatrolBook(); var r = b.AddRoute();
            for (int i = 0; i < PatrolBook.MaxSpots; i++) Assert.NotNull(b.Place(r.Id, PostKind.Mixed, 1, i, 0));
            Assert.Equal(PatrolBook.MaxSpots, b.InRoute(r.Id).Count()); Assert.Null(b.Place(r.Id, PostKind.Mixed, 1, 9999, 0));
            Assert.NotNull(b.Place(r.Id, PostKind.Melee, 1, 0, 0));          // an existing cell can still change type when the book is full
        }

        [Fact]
        public void A_spot_is_typed_melee_ranged_or_mixed_never_rally_and_needs_a_real_route()
        {
            var b = new PatrolBook(); var r = b.AddRoute();
            Assert.Null(b.Place(r.Id, PostKind.Rally, 1, 1, 1)); Assert.Null(b.Place(999, PostKind.Melee, 1, 1, 1));
            foreach (var t in new[] { PostKind.Melee, PostKind.Ranged, PostKind.Mixed }) Assert.NotNull(b.Place(r.Id, t, 1, (int)t, 0));
        }

        [Fact]
        public void Placing_on_a_taken_cell_changes_it_and_a_spot_can_be_moved_between_routes()
        {
            var b = new PatrolBook(); var a = b.AddRoute(); var c = b.AddRoute();
            var spot = b.Place(a.Id, PostKind.Melee, 1, 5, 5); Assert.Same(spot, b.Place(c.Id, PostKind.Ranged, 1, 5, 5)); Assert.Equal(c.Id, spot.Route); Assert.Equal(PostKind.Ranged, spot.Type); Assert.Single(b.Spots);
            Assert.True(b.Move(spot.Id, a.Id)); Assert.Equal(a.Id, spot.Route); Assert.False(b.Move(spot.Id, 999)); Assert.False(b.Move(999, a.Id));
            Assert.True(b.RemoveAt(1, 5, 5)); Assert.False(b.RemoveAt(1, 5, 5)); b.Place(a.Id, PostKind.Mixed, 2, 5, 5); Assert.NotNull(b.At(2, 5, 5)); Assert.Null(b.At(1, 5, 5)); Assert.True(b.Remove(b.Spots[0].Id));
        }

        [Fact]
        public void The_book_round_trips_including_names_with_commas_and_a_damaged_record_keeps_what_is_whole()
        {
            var b = new PatrolBook(); var r = b.AddRoute("North, east wall"); var q = b.AddRoute(); r.Cursor = 3; r.LastWalked = 777;
            b.Place(r.Id, PostKind.Melee, 1, 1, 2); b.Place(q.Id, PostKind.Mixed, 2, 3, 4); b.Remove(b.Spots[0].Id); b.Place(r.Id, PostKind.Ranged, 1, 9, 9);
            var d = PatrolBook.Decode(new StateReader(b.Encode()));
            Assert.Equal(b.Routes.Select(x => (x.Id, x.Name, x.Cursor, x.LastWalked)), d.Routes.Select(x => (x.Id, x.Name, x.Cursor, x.LastWalked)));
            Assert.Equal(b.Spots.Select(x => (x.Id, x.Map, x.X, x.Z, x.Route, x.Type)), d.Spots.Select(x => (x.Id, x.Map, x.X, x.Z, x.Route, x.Type)));
            Assert.Equal("North, east wall", d.Route(r.Id).Name);
            Assert.True(d.AddRoute().Id > b.Routes.Max(x => x.Id)); Assert.True(d.Place(r.Id, PostKind.Melee, 1, 50, 50).Id > b.Spots.Max(x => x.Id));
            var damaged = PatrolBook.Decode(new StateReader("rid=1,2\nrname=A\nsid=1,2,3\nsmap=1,1,1\nsx=1,2,3\nsz=1,2,3\nsroute=1,1,1\nstype=0,1,2")); Assert.Single(damaged.Routes); Assert.Equal(2, damaged.Spots.Count);          // a lone route name; a Rally-typed spot is dropped
            Assert.Empty(PatrolBook.Decode(new StateReader("rid=1\nrname=A\nsid=1\nsmap=1\nsx=1\nsz=1\nsroute=7\nstype=0")).Spots);          // a spot in a route that does not exist
            Assert.Empty(PatrolBook.Decode(new StateReader("")).Routes);
        }

        [Fact]
        public void Reconcile_drops_spots_whose_route_is_gone()
        {
            var b = new PatrolBook(); var r = b.AddRoute(); b.Place(r.Id, PostKind.Mixed, 1, 1, 1); Assert.Equal(0, b.Reconcile()); Assert.Single(b.Spots);
        }

        // ------------------------------------------------------------------ postings

        [Fact]
        public void A_soldier_has_one_posting_and_clearing_returns_them_to_automatic()
        {
            var b = new PostingBook(); Assert.True(b.Post(10, PostingKind.Route, 2)); Assert.Equal(2, b.RouteOf(10)); Assert.Equal(0, b.PositionOf(10));
            Assert.True(b.Post(10, PostingKind.Position, 5)); Assert.Equal(0, b.RouteOf(10)); Assert.Equal(5, b.PositionOf(10)); Assert.Equal(1, b.Count);          // a new posting replaces the old
            Assert.True(b.Clear(10)); Assert.False(b.Clear(10)); Assert.Equal(0, b.RouteOf(10)); Assert.False(b.TryGet(10, out _, out _));
            Assert.False(b.Post(0, PostingKind.Route, 1)); Assert.False(b.Post(1, PostingKind.Route, 0));
        }

        [Fact]
        public void Postings_round_trip_are_bounded_and_reconciled()
        {
            var b = new PostingBook(); b.Post(1, PostingKind.Route, 3); b.Post(2, PostingKind.Position, 4); b.Post(3, PostingKind.Route, 9); b.Post(4, PostingKind.Position, 8);
            var d = PostingBook.Decode(new StateReader(b.Encode())); Assert.Equal(4, d.Count); Assert.Equal(3, d.RouteOf(1)); Assert.Equal(4, d.PositionOf(2));
            Assert.Equal(3, d.Reconcile(id => id != 1, id => id == 3, id => id == 4));          // pawn 1 gone, route 9 gone, position 8 gone
            Assert.Equal(1, d.Count); Assert.Equal(4, d.PositionOf(2));
            var full = new PostingBook(); for (int i = 1; i <= PostingBook.Max; i++) Assert.True(full.Post(i, PostingKind.Route, 1)); Assert.False(full.Post(9999, PostingKind.Route, 1)); Assert.True(full.Post(1, PostingKind.Position, 2));
            Assert.Equal(0, PostingBook.Decode(new StateReader("pawn=1\nkind=7\ntarget=1")).Count);          // an unknown kind is dropped
            Assert.Equal(2, PostingBook.Decode(new StateReader("pawn=1,2\nkind=1,2\ntarget=1,2")).Count); Assert.Equal(1, PostingBook.Decode(new StateReader("pawn=1,2\nkind=1\ntarget=1,2")).Count);          // a damaged record keeps what is whole
        }

        // ------------------------------------------------------------------ ordering and stretches

        [Fact]
        public void A_route_is_ordered_as_a_chain_from_the_spot_placed_first_and_always_the_same()
        {
            var spots = new[] { S(1, 0), S(2, 30), S(3, 10), S(4, 20) };
            Assert.Equal(new[] { 1, 3, 4, 2 }, PatrolPlanner.Order(spots).Select(s => s.Id).ToArray());
            Assert.Equal(new[] { 1, 3, 4, 2 }, PatrolPlanner.Order(spots.Reverse()).Select(s => s.Id).ToArray());          // however they arrive
            Assert.Empty(PatrolPlanner.Order(new PatrolSpot[0])); Assert.Single(PatrolPlanner.Order(new[] { S(7, 1) }));
        }

        [Fact]
        public void A_soldier_walks_their_own_type_and_mixed_and_a_mixed_soldier_walks_everything()
        {
            Assert.True(PatrolPlanner.Eligible(PostKind.Melee, PostKind.Melee)); Assert.True(PatrolPlanner.Eligible(PostKind.Melee, PostKind.Mixed)); Assert.False(PatrolPlanner.Eligible(PostKind.Melee, PostKind.Ranged));
            Assert.True(PatrolPlanner.Eligible(PostKind.Ranged, PostKind.Ranged)); Assert.False(PatrolPlanner.Eligible(PostKind.Ranged, PostKind.Melee)); Assert.True(PatrolPlanner.Eligible(PostKind.Ranged, PostKind.Mixed));
            foreach (var t in new[] { PostKind.Melee, PostKind.Ranged, PostKind.Mixed }) Assert.True(PatrolPlanner.Eligible(PostKind.Mixed, t));
            foreach (var t in new[] { PostKind.Melee, PostKind.Ranged, PostKind.Mixed }) Assert.False(PatrolPlanner.Eligible(t, PostKind.Rally));
        }

        [Fact]
        public void A_short_route_is_walked_in_full_and_the_cursor_wraps()
        {
            var ordered = PatrolPlanner.Order(new[] { S(1, 0), S(2, 5), S(3, 10) });
            var stretch = PatrolPlanner.Plan(ordered, 0, 0, 0, MilitaryDuties.BlockTicks);
            Assert.Equal(new[] { 1, 2, 3 }, stretch.Spots.Select(s => s.Id).ToArray()); Assert.Equal(0, stretch.NextCursor);          // one whole lap, back to the start
            var again = PatrolPlanner.Plan(ordered, stretch.NextCursor, 10, 0, MilitaryDuties.BlockTicks); Assert.Equal(1, again.Spots[0].Id);
        }

        [Fact]
        public void A_long_route_is_covered_over_several_patrols_each_continuing_where_the_last_stopped()
        {
            var ordered = PatrolPlanner.Order(Enumerable.Range(0, 40).Select(i => S(i + 1, i * 10)));          // 40 spots, 10 cells apart: far more than one block
            int cursor = 0; var seen = new System.Collections.Generic.List<int>(); int patrols = 0;
            while (seen.Distinct().Count() < 40 && patrols < 40)
            {
                var stretch = PatrolPlanner.Plan(ordered, cursor, ordered[cursor % 40].X, 0, MilitaryDuties.BlockTicks);
                Assert.InRange(stretch.Spots.Count, 2, 39); seen.AddRange(stretch.Spots.Select(s => s.Id)); cursor = stretch.NextCursor; patrols++;
            }
            Assert.Equal(40, seen.Distinct().Count()); Assert.InRange(patrols, 3, 40);          // the whole route over several patrols, never one endless walk
            Assert.Equal(1, seen[0]);          // the first patrol begins at the first spot
        }

        [Fact]
        public void A_stretch_is_never_more_than_one_lap_never_empty_and_a_lone_spot_is_a_stretch_of_one()
        {
            var ordered = PatrolPlanner.Order(new[] { S(1, 0), S(2, 1) });
            Assert.Equal(2, PatrolPlanner.Plan(ordered, 0, 0, 0, int.MaxValue / 2).Spots.Count);          // a huge budget still stops after one lap
            Assert.Equal(2, PatrolPlanner.Plan(ordered, 0, 500, 500, 1).Spots.Count);          // a tiny budget still gives the minimum of two
            Assert.Single(PatrolPlanner.Plan(PatrolPlanner.Order(new[] { S(1, 0) }), 0, 0, 0, 10).Spots); Assert.Empty(PatrolPlanner.Plan(new PatrolSpot[0], 0, 0, 0, 100).Spots);
            Assert.Equal(2, PatrolPlanner.Plan(ordered, -1, 0, 0, 100000).Spots[0].Id);          // a negative cursor wraps to the last spot
            Assert.Equal(2, PatrolPlanner.Plan(ordered, 7, 0, 0, 1).Spots[0].Id);          // a cursor past the end wraps (7 mod 2 is the second spot)
        }

        [Fact]
        public void The_automatic_route_is_the_one_walked_least_recently_with_enough_spots()
        {
            var a = new PatrolRoute { Id = 1, LastWalked = 500 }; var b = new PatrolRoute { Id = 2, LastWalked = 100 }; var c = new PatrolRoute { Id = 3, LastWalked = 0 }; var d = new PatrolRoute { Id = 4, LastWalked = 0 };
            System.Func<PatrolRoute, int> count = r => r.Id == 3 ? 0 : r.Id == 4 ? 1 : 3;          // route 3 has nothing this soldier may walk, route 4 only one
            Assert.Equal(2, PatrolPlanner.Choose(new[] { a, b, c, d }, count).Id);          // routes with two or more eligible spots come first, then the least recently walked
            Assert.Equal(4, PatrolPlanner.Choose(new[] { c, d }, count).Id);          // a one-spot route is still better than nothing
            Assert.Null(PatrolPlanner.Choose(new[] { c }, count)); Assert.Null(PatrolPlanner.Choose(new PatrolRoute[0], count));
            Assert.Equal(1, PatrolPlanner.Choose(new[] { new PatrolRoute { Id = 2 }, new PatrolRoute { Id = 1 } }, r => 3).Id);          // ties by id
        }
    }
}
