using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class MilitaryAlertTests
    {
        // ------------------------------------------------------------------ the alert

        [Fact]
        public void A_threat_raises_red_and_it_drops_back_to_the_set_level_after_a_calm_hour()
        {
            var s = new AlertState { Set = AlertLevel.Yellow };
            Assert.Equal(AlertLevel.Yellow, s.Effective);
            Assert.Equal(AlertChange.Raised, AlertRules.Step(s, true, 1000)); Assert.Equal(AlertLevel.Red, s.Effective);
            Assert.Equal(AlertChange.None, AlertRules.Step(s, true, 1250));          // still a threat: nothing new
            Assert.Equal(AlertChange.None, AlertRules.Step(s, false, 2000)); Assert.Equal(AlertLevel.Red, s.Effective);          // calm, but not for long enough
            Assert.Equal(AlertChange.None, AlertRules.Step(s, false, 1250 + AlertRules.CalmTicks - 1));
            Assert.Equal(AlertChange.Lowered, AlertRules.Step(s, false, 1250 + AlertRules.CalmTicks)); Assert.Equal(AlertLevel.Yellow, s.Effective);
        }

        [Fact]
        public void A_renewed_threat_restarts_the_calm_clock()
        {
            var s = new AlertState(); AlertRules.Step(s, true, 100);
            AlertRules.Step(s, false, 100 + AlertRules.CalmTicks - 10); AlertRules.Step(s, true, 100 + AlertRules.CalmTicks - 5);
            Assert.Equal(AlertChange.None, AlertRules.Step(s, false, 100 + AlertRules.CalmTicks + 100)); Assert.Equal(AlertLevel.Red, s.Effective);
            Assert.Equal(AlertChange.Lowered, AlertRules.Step(s, false, 100 + AlertRules.CalmTicks - 5 + AlertRules.CalmTicks));
        }

        [Fact]
        public void With_automatic_red_off_a_threat_changes_nothing()
        {
            var s = new AlertState { AutoRed = false, Set = AlertLevel.Normal };
            Assert.Equal(AlertChange.None, AlertRules.Step(s, true, 500)); Assert.Equal(AlertLevel.Normal, s.Effective);
        }

        [Fact]
        public void A_red_the_player_set_stays_until_they_change_it()
        {
            var s = new AlertState(); Assert.Equal(AlertChange.Set, AlertRules.Choose(s, AlertLevel.Red, 10)); Assert.False(s.RaisedByThreat);
            Assert.Equal(AlertChange.None, AlertRules.Step(s, false, 10 + AlertRules.CalmTicks * 5)); Assert.Equal(AlertLevel.Red, s.Effective);
            Assert.Equal(AlertChange.None, AlertRules.Step(s, true, 99999)); Assert.False(s.RaisedByThreat);          // already Red: a threat does not make it "by threat"
            Assert.Equal(AlertChange.Set, AlertRules.Choose(s, AlertLevel.Normal, 100000)); Assert.Equal(AlertLevel.Normal, s.Effective);
        }

        [Fact]
        public void Choosing_a_level_during_a_threat_red_keeps_red_until_calm_then_uses_the_choice()
        {
            var s = new AlertState(); AlertRules.Step(s, true, 100);
            Assert.Equal(AlertChange.None, AlertRules.Choose(s, AlertLevel.Yellow, 200)); Assert.Equal(AlertLevel.Red, s.Effective);
            Assert.Equal(AlertChange.Lowered, AlertRules.Step(s, false, 100 + AlertRules.CalmTicks)); Assert.Equal(AlertLevel.Yellow, s.Effective);
        }

        [Fact]
        public void A_clock_that_went_back_never_strands_red()
        {
            var s = new AlertState(); AlertRules.Step(s, true, 500000);
            Assert.Equal(AlertChange.None, AlertRules.Step(s, false, 1000)); Assert.Equal(AlertLevel.Red, s.Effective);          // loaded an earlier save: the old time is clamped
            Assert.Equal(AlertChange.Lowered, AlertRules.Step(s, false, 1000 + AlertRules.CalmTicks)); Assert.Equal(AlertLevel.Normal, s.Effective);
        }

        [Fact]
        public void Alert_state_round_trips_including_automatic_red_being_off()
        {
            var s = new AlertState { Set = AlertLevel.Yellow, AutoRed = false, RaisedByThreat = true, LastThreat = 42, Since = 7 }; s.Drafted.AddRange(new[] { 3, 5 }); s.Ordered.AddRange(new[] { 3, 5, 9 });
            var d = AlertState.Decode(new StateReader(s.Encode()));
            Assert.Equal(AlertLevel.Yellow, d.Set); Assert.False(d.AutoRed); Assert.True(d.RaisedByThreat); Assert.Equal(42, d.LastThreat); Assert.Equal(7, d.Since);
            Assert.Equal(new[] { 3, 5 }, d.Drafted); Assert.Equal(new[] { 3, 5, 9 }, d.Ordered);
            var fresh = AlertState.Decode(new StateReader("")); Assert.True(fresh.AutoRed); Assert.Equal(AlertLevel.Normal, fresh.Set);          // an empty record is the defaults
            var off = AlertState.Decode(new StateReader(new AlertState { AutoRed = false }.Encode())); Assert.False(off.AutoRed);
            Assert.Equal(AlertLevel.Normal, AlertState.Decode(new StateReader("set=9")).Set);          // a value from a newer version is read as Normal
        }

        [Fact]
        public void The_framework_can_hold_and_recreate_the_state()
        {
            var f = AlertStateFactory.Instance; Assert.Equal(1, f.Version); Assert.Equal(AlertLevel.Normal, f.Create().Set); Assert.Equal(AlertLevel.Normal, f.FromLegacy(new StateReader("")).Set);
        }

        // ------------------------------------------------------------------ posts

        [Fact]
        public void Posts_are_placed_changed_and_removed_by_cell()
        {
            var b = new PostBook(); var a = b.Place(PostKind.Guard, 1, 10, 10);
            Assert.Same(a, b.Place(PostKind.Overwatch, 1, 10, 10)); Assert.Equal(PostKind.Overwatch, a.Kind); Assert.Single(b.All);          // same cell: change its kind
            b.Place(PostKind.Rally, 1, 11, 10); b.Place(PostKind.Rally, 2, 10, 10); Assert.Equal(3, b.All.Count);
            Assert.True(b.RemoveAt(1, 11, 10)); Assert.False(b.RemoveAt(1, 11, 10)); Assert.True(b.Remove(a.Id)); Assert.Equal(1, b.Count(PostKind.Rally));
            Assert.Equal(1, b.Clear(2)); Assert.Empty(b.All);
        }

        [Fact]
        public void The_book_is_bounded_and_ids_are_never_reused()
        {
            var b = new PostBook(); for (int i = 0; i < PostBook.MaxPosts; i++) Assert.NotNull(b.Place(PostKind.Guard, 1, i, 0));
            Assert.Null(b.Place(PostKind.Guard, 1, 999, 0)); Assert.NotNull(b.Place(PostKind.Rally, 1, 0, 0));          // an existing cell can still change kind
            int last = b.All.Max(p => p.Id); b.Remove(last); Assert.True(b.Place(PostKind.Guard, 1, 500, 0).Id > last);
        }

        [Fact]
        public void The_book_round_trips_and_a_damaged_record_keeps_what_is_whole()
        {
            var b = new PostBook(); b.Place(PostKind.Overwatch, 1, 4, 5); b.Place(PostKind.Rally, 1, 6, 7); b.Remove(b.All[0].Id); b.Place(PostKind.Guard, 2, 1, 2);
            var d = PostBook.Decode(new StateReader(b.Encode()));
            Assert.Equal(b.All.Select(p => (p.Id, p.Map, p.X, p.Z, p.Kind)), d.All.Select(p => (p.Id, p.Map, p.X, p.Z, p.Kind)));
            Assert.True(d.Place(PostKind.Guard, 3, 0, 0).Id > b.All.Max(p => p.Id));
            var damaged = PostBook.Decode(new StateReader("id=1,2,3\nmap=1,1\nx=1,2,3\nz=1,2,3\nkind=0,1,2")); Assert.Equal(2, damaged.All.Count);
            Assert.Empty(PostBook.Decode(new StateReader("id=1\nmap=1\nx=1\nz=1\nkind=7")).All);          // an unknown kind is dropped
            Assert.Single(PostBook.Decode(new StateReader("id=1,2\nmap=1,1\nx=5,5\nz=5,5\nkind=0,1")).All);          // two on one cell: one stays
        }

        [Fact]
        public void Each_role_goes_to_the_post_that_suits_it()
        {
            Assert.Equal(new[] { PostKind.Overwatch, PostKind.Rally }, PostPlanner.Preference("Sniper"));
            Assert.Equal(new[] { PostKind.Overwatch, PostKind.Rally }, PostPlanner.Preference("Marksman"));
            Assert.Equal(new[] { PostKind.Guard, PostKind.Rally }, PostPlanner.Preference("Guardian"));
            foreach (var role in new[] { "Duelist", "FieldMedic", "Officer", null, "Recruit", "Soldier" }) Assert.Equal(new[] { PostKind.Rally }, PostPlanner.Preference(role));
        }

        [Fact]
        public void Soldiers_take_the_nearest_free_post_of_their_kind_and_fall_back_to_the_rally_point()
        {
            var posts = new[] { new MilitaryPost { Id = 1, Kind = PostKind.Overwatch, X = 0, Z = 0 }, new MilitaryPost { Id = 2, Kind = PostKind.Overwatch, X = 50, Z = 0 }, new MilitaryPost { Id = 3, Kind = PostKind.Rally, X = 25, Z = 25 }, new MilitaryPost { Id = 4, Kind = PostKind.Guard, X = 10, Z = 10 } };
            var soldiers = new[]
            {
                new RedSoldier { Id = 10, Specialty = "Sniper", X = 48, Z = 0 }, new RedSoldier { Id = 11, Specialty = "Sniper", X = 49, Z = 0 }, new RedSoldier { Id = 12, Specialty = "Sniper", X = 1, Z = 1 },
                new RedSoldier { Id = 13, Specialty = "Guardian", X = 9, Z = 9 }, new RedSoldier { Id = 14, Specialty = "Guardian", X = 9, Z = 9 }, new RedSoldier { Id = 15, Specialty = "Officer", X = 0, Z = 0 }
            };
            var plan = PostPlanner.Assign(soldiers, posts);
            Assert.Equal(2, plan[10].Id);          // nearest free Overwatch
            Assert.Equal(1, plan[11].Id);          // the near one is taken, so the other Overwatch
            Assert.Equal(3, plan[12].Id);          // both taken: Rally
            Assert.Equal(4, plan[13].Id); Assert.Equal(3, plan[14].Id);          // one soldier per Guard post
            Assert.Equal(3, plan[15].Id);
        }

        [Fact]
        public void Rally_is_shared_and_with_no_post_at_all_nobody_is_sent_anywhere()
        {
            var rally = new MilitaryPost { Id = 1, Kind = PostKind.Rally, X = 5, Z = 5 };
            var many = Enumerable.Range(1, 8).Select(i => new RedSoldier { Id = i, Specialty = "Sniper" }).ToList();
            Assert.Equal(8, PostPlanner.Assign(many, new[] { rally }).Count(x => x.Value.Id == 1));
            Assert.Empty(PostPlanner.Assign(many, new MilitaryPost[0]));
            Assert.Empty(PostPlanner.Assign(many, new[] { new MilitaryPost { Id = 2, Kind = PostKind.Guard } }));          // a Guard post is no use to a Sniper, and there is no Rally
        }

        [Fact]
        public void Planning_is_deterministic_whatever_order_soldiers_arrive_in()
        {
            var posts = new[] { new MilitaryPost { Id = 1, Kind = PostKind.Overwatch, X = 0 }, new MilitaryPost { Id = 2, Kind = PostKind.Overwatch, X = 1 } };
            var s = new[] { new RedSoldier { Id = 5, Specialty = "Sniper" }, new RedSoldier { Id = 3, Specialty = "Sniper" } };
            var a = PostPlanner.Assign(s, posts); var b = PostPlanner.Assign(s.Reverse(), posts);
            Assert.Equal(a.OrderBy(x => x.Key).Select(x => x.Value.Id), b.OrderBy(x => x.Key).Select(x => x.Value.Id));
        }
    }
}

namespace FamilyLife.Tests
{
    public class MilitaryGameContractTests
    {
        private static string Root => RepoPaths.Root();

        [Fact]
        public void Every_string_key_the_military_code_uses_exists_in_the_language_file()
        {
            var keys = System.Xml.Linq.XDocument.Load(System.IO.Path.Combine(Root, "Packages/WilsMilitary/Languages/English/Keyed/Military.xml")).Root.Elements().Select(x => x.Name.LocalName).ToHashSet();
            foreach (var file in System.IO.Directory.GetFiles(System.IO.Path.Combine(Root, "Source/Military"), "*.cs", System.IO.SearchOption.AllDirectories).Where(f => !f.Replace('\\', '/').Contains("/obj/")))
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(file), "\"(WFL_Military_[A-Za-z]+)\""))
                    Assert.True(keys.Contains(m.Groups[1].Value), System.IO.Path.GetFileName(file) + " uses a missing key " + m.Groups[1].Value);
            foreach (var kind in new[] { "Overwatch", "Guard", "Rally" }) { Assert.Contains("WFL_Military_Post_" + kind, keys); Assert.Contains("WFL_Military_PostDesc_" + kind, keys); }
            foreach (var level in new[] { "Normal", "Yellow", "Red" }) Assert.Contains("WFL_Military_AlertExplain_" + level, keys);
        }

        [Fact]
        public void The_architect_patch_names_classes_that_exist_and_cannot_raise_an_error()
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(Root, "Packages/WilsMilitary/Patches/MilitaryZone.xml")); string source = string.Join("\n", System.IO.Directory.GetFiles(System.IO.Path.Combine(Root, "Source/Military"), "*.cs", System.IO.SearchOption.AllDirectories));
            string code = string.Join("\n", System.IO.Directory.GetFiles(System.IO.Path.Combine(Root, "Source/Military"), "*.cs", System.IO.SearchOption.AllDirectories).Where(f => !f.Replace('\\', '/').Contains("/obj/")).Select(System.IO.File.ReadAllText));
            foreach (var cls in new[] { "Designator_ZoneAdd_TrainingYard", "Designator_MilitaryPost", "Designator_RemoveMilitaryPost" })
            { Assert.Contains("<li>FamilyLife." + cls + "</li>", text); Assert.Matches(@"class\s+" + cls + @"\b", code); }
            Assert.Contains("<success>Always</success>", text);
            Assert.Matches(@"class\s+Zone_TrainingYard\b", code); Assert.Matches(@"class\s+MilitaryMapComponent\s*:\s*MapComponent", code); Assert.Matches(@"class\s+Alert_MilitaryAlert\s*:\s*Alert", code);
        }

        [Fact]
        public void Red_is_applied_once_per_soldier_and_only_stands_down_those_it_drafted()
        {
            string code = System.IO.File.ReadAllText(System.IO.Path.Combine(Root, "Source/Military/Game/MilitaryAlertGame.cs"));
            Assert.Contains("!state.Ordered.Contains(p.thingIDNumber)", code);          // handled once per episode, so a player's order afterwards stands
            Assert.Contains("state.Drafted.Add(id)", code); Assert.Contains("foreach (int id in state.Drafted.ToList())", code);          // only people this drafted are undrafted
            Assert.Contains("if (!p.Drafted) { p.drafter.Drafted = true;", code);          // someone already drafted is left alone
            Assert.Contains("p.Downed", code); Assert.Contains("InMentalState", code); Assert.Contains("WorkTagIsDisabled(WorkTags.Violent)", code);          // never drafts someone who cannot or must not fight
            Assert.Contains("FreeColonistsSpawned", code);          // free colonists only: never prisoners or slaves
        }
    }
}
