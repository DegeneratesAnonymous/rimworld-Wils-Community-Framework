using System.IO;
using System.Linq;
using System.Xml.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class WardenRoundTests
    {
        private static RoundStop S(long room, int x, int z = 0, int seen = 0) => new RoundStop { Room = room, X = x, Z = z, LastSeen = seen };

        [Fact]
        public void A_round_visits_the_rooms_seen_least_recently_first_then_walks_them_nearest_first()
        {
            var plan = WardenRounds.Plan(new[] { S(1, 0, seen: 900), S(2, 50, seen: 0), S(3, 10, seen: 0), S(4, 20, seen: 500) }, 0, 0);
            Assert.Equal(new long[] { 1, 3, 4, 2 }, plan.Select(s => s.Room).ToArray());          // all four fit: nearest-first from the warden among them
        }

        [Fact]
        public void A_round_is_at_most_six_stops_and_leaves_out_the_most_recently_seen()
        {
            var stops = Enumerable.Range(1, 10).Select(i => S(i, i * 3, seen: i < 5 ? 0 : 1000 + i)).ToList();
            var plan = WardenRounds.Plan(stops, 0, 0); Assert.Equal(WardenRounds.MaxStops, plan.Count);
            Assert.All(new long[] { 1, 2, 3, 4 }, r => Assert.Contains(plan, s => s.Room == r));          // never seen: all in
            Assert.DoesNotContain(plan, s => s.Room >= 8);          // the most recently seen are left for next time
        }

        [Fact]
        public void A_room_is_listed_once_and_nothing_is_nothing()
        {
            Assert.Single(WardenRounds.Plan(new[] { S(1, 0), S(1, 5, seen: 10) }, 0, 0)); Assert.Empty(WardenRounds.Plan(new RoundStop[0], 0, 0)); Assert.Empty(WardenRounds.Plan(null, 0, 0)); Assert.Empty(WardenRounds.Plan(new RoundStop[] { null }, 0, 0));
        }

        [Fact]
        public void Planning_is_deterministic_whatever_order_the_rooms_arrive_in()
        {
            var stops = new[] { S(5, 3, seen: 7), S(2, 8, seen: 7), S(9, 1, seen: 0), S(4, 8, seen: 7) };
            Assert.Equal(WardenRounds.Plan(stops, 2, 2).Select(s => s.Room), WardenRounds.Plan(stops.Reverse(), 2, 2).Select(s => s.Room));
        }

        [Fact]
        public void A_warden_rests_between_rounds_has_a_daily_limit_and_the_prison_is_never_left_unwatched()
        {
            Assert.True(WardenRounds.MayStart(1000, 0, 0)); Assert.False(WardenRounds.MayStart(1000 + WardenRounds.CooldownTicks - 1, 1000, 1)); Assert.True(WardenRounds.MayStart(1000 + WardenRounds.CooldownTicks, 1000, 1));
            Assert.False(WardenRounds.MayStart(999999, 1000, WardenRounds.MaxPerDay)); Assert.True(WardenRounds.MayStart(10, 5000, 1));
            Assert.Equal(0, WardenRounds.MaxOnRound(0)); Assert.Equal(1, WardenRounds.MaxOnRound(1)); Assert.Equal(1, WardenRounds.MaxOnRound(2)); Assert.Equal(2, WardenRounds.MaxOnRound(3)); for (int n = 2; n < 30; n++) Assert.True(WardenRounds.MaxOnRound(n) < n);
        }

        // ------------------------------------------------------------------ slave families

        [Fact]
        public void Slave_kin_in_the_same_room_are_together_and_in_different_rooms_apart()
        {
            var rooms = new System.Collections.Generic.Dictionary<int, long> { { 1, 10 }, { 2, 10 }, { 3, 11 }, { 4, 12 } };
            var check = SlaveFamilies.Evaluate(new[] { new System.Collections.Generic.KeyValuePair<int, int>(1, 2), new System.Collections.Generic.KeyValuePair<int, int>(3, 4) }, id => rooms[id]);
            Assert.Equal((1, 1), (check.Together, check.Apart)); Assert.Equal(new[] { 1, 2 }, check.TogetherPeople.OrderBy(x => x).ToArray()); Assert.Equal(new[] { 3, 4 }, check.ApartPeople.OrderBy(x => x).ToArray());
        }

        [Fact]
        public void A_pair_is_counted_once_however_it_is_listed_and_an_unknown_room_is_not_counted()
        {
            var check = SlaveFamilies.Evaluate(new[] { new System.Collections.Generic.KeyValuePair<int, int>(1, 2), new System.Collections.Generic.KeyValuePair<int, int>(2, 1), new System.Collections.Generic.KeyValuePair<int, int>(1, 3), new System.Collections.Generic.KeyValuePair<int, int>(4, 4) }, id => id == 3 ? 0 : 7);
            Assert.Equal((1, 0), (check.Together, check.Apart));          // 1-2 once, 1-3 unknown, 4-4 is nobody's pair
            Assert.Equal((0, 0), (SlaveFamilies.Evaluate(null, id => 1).Together, SlaveFamilies.Evaluate(null, id => 1).Apart));
        }

        [Fact]
        public void The_overseer_is_pleased_only_if_nothing_was_apart_and_troubled_if_anything_was()
        {
            Assert.Equal(1, SlaveFamilies.OverseerEvent(new FamilyCheck { Together = 3 })); Assert.Equal(-1, SlaveFamilies.OverseerEvent(new FamilyCheck { Together = 3, Apart = 1 })); Assert.Equal(-1, SlaveFamilies.OverseerEvent(new FamilyCheck { Apart = 1 })); Assert.Equal(0, SlaveFamilies.OverseerEvent(new FamilyCheck()));
        }

        [Fact]
        public void The_family_thoughts_are_defined_with_the_right_signs()
        {
            var defs = XDocument.Load(Path.Combine(RepoPaths.Root(), "Packages/WilsWardens/Defs/WardenFamilyThoughts.xml")).Descendants("ThoughtDef").ToDictionary(x => x.Element("defName").Value);
            Assert.True(int.Parse(defs[SlaveFamilies.TogetherThought].Descendants("baseMoodEffect").Single().Value) > 0); Assert.True(int.Parse(defs[SlaveFamilies.ApartThought].Descendants("baseMoodEffect").Single().Value) < 0);
        }

        [Fact]
        public void The_overseers_family_counts_and_the_rounds_switch_are_saved_and_default_to_on()
        {
            var l = new WardenLedger(); l.Families(1, 3, 1); l.Families(1, 2, 0); l.Families(0, 5, 5); l.Families(1, -4, -4);
            var d = WardenLedger.Decode(new StateReader(l.Encode())); Assert.Equal((5, 1), (d.Of(1).FamiliesTogether, d.Of(1).FamiliesApart)); Assert.False(d.Has(0)); Assert.True(d.RoundsOn);
            l.RoundsOn = false; Assert.False(WardenLedger.Decode(new StateReader(l.Encode())).RoundsOn); Assert.True(WardenLedger.Decode(new StateReader("")).RoundsOn);
        }

        [Fact]
        public void The_round_only_walks_and_looks_it_never_moves_locks_or_changes_anyone()
        {
            string code = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Wardens/Game/WardenRoundGame.cs"));
            foreach (var forbidden in new[] { ".SetFaction(", ".Kill(", ".Destroy(", "SetExclusiveInteractionMode", ".Release", "SetGuestStatus", "Teleport", "Lock", "TryTakeOrderedJob", "DrawProgress", ".Position =" })
                Assert.DoesNotContain(forbidden, code);
            string shared = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/FamilyLife/Game/FrameworkServices.cs")); Assert.Contains("lastJobTag != JobTag.Idle", shared); Assert.Contains("playerForced", shared); Assert.Contains("Sleep", shared); Assert.Contains("FrameworkServices.FreeForDuty(p, ", code); Assert.Contains("HostilesAbout", code); Assert.Contains("RoundsOn", code);
            string module = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Wardens/Game/WardensModule.cs")); Assert.Contains("WardenRoundGame.Run", module); Assert.Contains("WardenRoundGame.Reset()", module);
            Assert.Contains("WFL_WardenRound", File.ReadAllText(Path.Combine(RepoPaths.Root(), "Packages/WilsWardens/Defs/WardenJobs.xml")));
        }
    }
}
