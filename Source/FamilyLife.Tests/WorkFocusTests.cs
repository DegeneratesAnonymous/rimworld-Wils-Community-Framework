using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class WorkFocusTests
    {
        private static readonly WorkFocusRule[] Rules =
        {
            new WorkFocusRule { Career = "care", WorkTypes = new[] { "Doctor" } },
            new WorkFocusRule { Career = "farm", WorkTypes = new[] { "Growing", "Hunting" } },
            new WorkFocusRule { Career = "cook", WorkTypes = new[] { "Cooking", "Hunting" } }
        };

        private static Dictionary<string, int> State(params (string, int)[] p) => p.ToDictionary(x => x.Item1, x => x.Item2);

        [Fact]
        public void Main_profession_work_comes_first_side_work_next_and_a_shared_work_type_keeps_the_better_priority()
        {
            var t = WorkFocusPlan.Targets(Rules, "care", new[] { "farm", "cook" });
            Assert.Equal(1, t["Doctor"]); Assert.Equal(2, t["Growing"]); Assert.Equal(2, t["Cooking"]); Assert.Equal(2, t["Hunting"]);
            var t2 = WorkFocusPlan.Targets(Rules, "farm", new[] { "cook" }); Assert.Equal(1, t2["Hunting"]);          // main farm outranks the side cook for the shared work type
            Assert.Empty(WorkFocusPlan.Targets(Rules, "unknown", null));
        }

        [Fact]
        public void It_turns_work_on_and_never_off_and_remembers_what_was_there()
        {
            var cur = State(("Doctor", 0), ("Growing", 3)); var wanted = WorkFocusPlan.Targets(Rules, "care", new[] { "farm" });
            var plan = WorkFocusPlan.Plan(wanted, wt => cur.TryGetValue(wt, out var v) ? v : 0, null, null);
            Assert.Equal(new[] { "Doctor", "Growing", "Hunting" }, plan.Select(p => p.WorkType).OrderBy(x => x));
            var doctor = plan.Single(p => p.WorkType == "Doctor"); Assert.Equal(0, doctor.Before); Assert.Equal(1, doctor.Applied);
            Assert.DoesNotContain(plan, p => p.Applied == 0);          // nothing is ever switched off
        }

        [Fact]
        public void A_colonist_who_cannot_do_a_work_type_is_left_alone_and_a_priority_already_right_is_not_touched()
        {
            var wanted = State(("Doctor", 1), ("Growing", 2)); var cur = State(("Doctor", 1), ("Growing", 4));
            var plan = WorkFocusPlan.Plan(wanted, wt => cur[wt], wt => wt != "Growing", null);
            Assert.Empty(plan);
        }

        [Fact]
        public void A_priority_the_player_changed_by_hand_since_is_not_overridden()
        {
            var earlier = new[] { new WorkChange { WorkType = "Doctor", Before = 3, Applied = 1 } };
            var wanted = State(("Doctor", 1));
            Assert.Empty(WorkFocusPlan.Plan(wanted, wt => 4, null, earlier));          // player moved it to 4
            wanted = State(("Doctor", 2)); var again = WorkFocusPlan.Plan(wanted, wt => 1, null, earlier);          // still ours (1): safe to move on to 2, remembering the first "before"
            Assert.Equal(3, again.Single().Before); Assert.Equal(2, again.Single().Applied);
        }

        [Fact]
        public void Undo_puts_back_only_what_is_still_at_the_priority_it_was_set_to()
        {
            var earlier = new[] { new WorkChange { WorkType = "Doctor", Before = 3, Applied = 1 }, new WorkChange { WorkType = "Growing", Before = 0, Applied = 2 } };
            var cur = State(("Doctor", 1), ("Growing", 4));          // the player raised Growing's number themselves
            var undo = WorkFocusPlan.Undo(earlier, wt => cur[wt]); Assert.Equal("Doctor", undo.Single().WorkType); Assert.Equal(3, undo.Single().Before);
            Assert.Empty(WorkFocusPlan.Undo(null, wt => 0));
        }

        [Fact]
        public void The_book_keeps_who_follows_the_first_before_and_survives_a_save()
        {
            var b = new WorkFocusBook(); b.Follow(5, true); b.Follow(6, true); b.Follow(6, false); b.Follow(0, true);
            b.Record(5, new[] { new WorkChange { WorkType = "Doctor", Before = 3, Applied = 1 } }); b.Record(5, new[] { new WorkChange { WorkType = "Doctor", Before = 1, Applied = 2 }, new WorkChange { WorkType = "Cooking", Before = 0, Applied = 2 } });
            Assert.Equal(new[] { 5 }, b.Following.ToArray()); Assert.Equal(3, b.ChangesFor(5).Single(c => c.WorkType == "Doctor").Before); Assert.Equal(2, b.ChangesFor(5).Single(c => c.WorkType == "Doctor").Applied);
            var loaded = WorkFocusBook.Decode(new StateReader(b.Encode()));
            Assert.True(loaded.IsFollowing(5)); Assert.Equal(2, loaded.ChangesFor(5).Count); Assert.Equal(3, loaded.ChangesFor(5).Single(c => c.WorkType == "Doctor").Before);
            loaded.Forget(5, loaded.ChangesFor(5).ToList()); Assert.False(loaded.HasChanges(5));
        }

        [Fact]
        public void The_number_of_followers_is_limited()
        {
            var b = new WorkFocusBook(); for (int i = 1; i <= WorkFocusBook.MaxPeople + 20; i++) b.Follow(i, true);
            Assert.Equal(WorkFocusBook.MaxPeople, b.Following.Count());
        }
    }
}
