using System.IO;
using System.Linq;
using System.Xml.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class WorksTests
    {
        [Fact]
        public void There_are_two_careers_each_with_its_three_specialties_and_no_surveyor_or_driller()
        {
            var b = WorksCareers.BuilderDefinition(); var m = WorksCareers.MinerDefinition();
            Assert.Equal(new[] { "Carpenter", "Engineer", "Mason" }, b.Specialties.Keys.OrderBy(x => x).ToArray()); Assert.Equal(new[] { "Prospector", "Quarryman", "Tunneller" }, m.Specialties.Keys.OrderBy(x => x).ToArray());
            Assert.Equal(WorksCareers.BuilderSpecialties.OrderBy(x => x), b.Specialties.Keys.OrderBy(x => x)); Assert.Equal(WorksCareers.MinerSpecialties.OrderBy(x => x), m.Specialties.Keys.OrderBy(x => x));
            Assert.NotEqual(b.Id, m.Id); Assert.Equal(WorksCareers.Module, b.ModuleId); Assert.Equal(WorksCareers.Module, m.ModuleId);
            Assert.DoesNotContain("Surveyor", WorksEvents.Roles); Assert.DoesNotContain("DeepDriller", WorksEvents.Roles); Assert.Empty(b.Level2Branches); Assert.Empty(m.Level2Branches);
            Assert.Equal(WorksCareers.Builder, WorksCareers.CareerOf("Mason")); Assert.Equal(WorksCareers.Miner, WorksCareers.CareerOf("Tunneller")); Assert.Null(WorksCareers.CareerOf("Smith"));
        }

        [Theory]
        [InlineData("Builder", "Mason", "Construction", 8)]
        [InlineData("Builder", "Carpenter", "Crafting", 4)]
        [InlineData("Builder", "Engineer", "Intellectual", 6)]
        [InlineData("Miner", "Prospector", "Intellectual", 4)]
        [InlineData("Miner", "Quarryman", "Mining", 8)]
        [InlineData("Miner", "Tunneller", "Mining", 10)]
        public void Each_specialty_needs_its_skills_and_a_journeyman_first(string career, string specialty, string skill, int need)
        {
            var d = career == "Builder" ? WorksCareers.BuilderDefinition() : WorksCareers.MinerDefinition(); string main = career == "Builder" ? "Construction" : "Mining";
            var l = new CareerLedger(); l.Award(1, d.Id, specialty, "w", "a", 400, 0);
            l.Evaluate(1, d, k => k == skill ? need - 1 : 20); Assert.False(l.IsSpecialist(1, d.Id, specialty));
            l.Evaluate(1, d, k => 20); Assert.True(l.IsSpecialist(1, d.Id, specialty));
            var low = new CareerLedger(); low.Award(2, d.Id, specialty, "w", "a", 400, 0); low.Evaluate(2, d, k => k == main ? 4 : 20); Assert.False(low.IsQualified(2, d.Id)); Assert.False(low.IsSpecialist(2, d.Id, specialty));          // skill 5 makes a Journeyman
        }

        [Fact]
        public void The_two_careers_are_independent_in_the_ledger()
        {
            var b = WorksCareers.BuilderDefinition(); var m = WorksCareers.MinerDefinition(); var l = new CareerLedger();
            l.Award(1, b.Id, "Mason", "w", "a", 400, 0); l.Evaluate(1, b, k => 20); l.Evaluate(1, m, k => 20);
            Assert.True(l.IsSpecialist(1, b.Id, "Mason")); Assert.False(l.IsQualified(1, m.Id)); Assert.False(l.IsSpecialist(1, m.Id, "Quarryman"));
        }

        [Fact]
        public void Every_role_has_fifteen_events_and_a_thought_for_each_with_the_right_sign()
        {
            Assert.Equal(10, WorksEvents.Roles.Length); Assert.Equal(150, WorksEvents.All.Length);
            var defs = XDocument.Load(Path.Combine(RepoPaths.Root(), "Packages/WilsWorks/Defs/WorksThoughts.xml")).Descendants("ThoughtDef").ToDictionary(x => x.Element("defName").Value);
            foreach (var e in WorksEvents.All)
            {
                Assert.True(defs.ContainsKey(e.Thought), e.Thought); var stage = defs[e.Thought].Descendants("li").First();
                if (e.Opinion != 0) Assert.Equal(e.Opinion, int.Parse(stage.Element("baseOpinionOffset").Value)); else Assert.Equal(e.Mood, int.Parse(stage.Element("baseMoodEffect").Value));
            }
            Assert.Equal(defs.Count, WorksEvents.All.Length);
        }

        [Fact]
        public void Works_thoughts_never_collide_with_the_other_role_modules()
        {
            var all = IntelligenceEvents.All.Select(e => e.Thought).Concat(MilitaryEvents.All.Select(e => e.Thought)).Concat(WardenEvents.All.Select(e => e.Thought)).Concat(WorksEvents.All.Select(e => e.Thought)).ToList();
            Assert.Equal(all.Count, all.Distinct().Count());
        }

        [Fact]
        public void The_module_registers_both_careers_and_uses_only_the_framework()
        {
            string code = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Works/Game/WorksModule.cs"));
            Assert.Contains("RegisterCareer(WorksCareers.BuilderDefinition())", code); Assert.Contains("RegisterCareer(WorksCareers.MinerDefinition())", code);
            foreach (var file in Directory.GetFiles(Path.Combine(RepoPaths.Root(), "Source/Works"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Replace('\\', '/').Contains("/obj/"))) Assert.DoesNotContain("HarmonyPatch", File.ReadAllText(file));
            string csproj = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Works/WilsWorks.csproj")); Assert.Contains("<AssemblyName>WilsWorks</AssemblyName>", csproj); Assert.DoesNotContain("WilsWardens", csproj);
        }

        [Theory]
        [InlineData("Mason", "ConstructionSpeed", 1.15f)]
        [InlineData("Mason", "ConstructSuccessChance", 1.10f)]
        [InlineData("Carpenter", "SmoothingSpeed", 1.15f)]
        [InlineData("Carpenter", "ConstructSuccessChance", 1.10f)]
        [InlineData("Engineer", "ConstructionSpeed", 1.15f)]
        [InlineData("Engineer", "FixBrokenDownBuildingSuccessChance", 1.15f)]
        [InlineData("Prospector", "MiningSpeed", 1.15f)]
        [InlineData("Quarryman", "MiningSpeed", 1.20f)]
        [InlineData("Tunneller", "MiningSpeed", 1.10f)]
        public void Each_specialty_has_the_agreed_bonus_and_nothing_else(string specialty, string stat, float factor)
        {
            Assert.Equal(factor, WorksBonuses.Factor(specialty, stat));
            foreach (var other in WorksBonuses.Stats.Where(s => s != stat && WorksBonuses.Factor(specialty, s) != 1f)) Assert.True(WorksBonuses.Factor(specialty, other) > 1f);          // only agreed stats
            Assert.Equal(1f, WorksBonuses.Factor("BuilderLabourer", stat)); Assert.Equal(1f, WorksBonuses.Factor("BuilderJourneyman", stat)); Assert.Equal(1f, WorksBonuses.Factor(null, stat));
        }

        [Fact]
        public void Bonuses_are_modest_and_a_builder_never_digs_faster_nor_a_miner_builds_faster()
        {
            foreach (var specialty in WorksCareers.BuilderSpecialties) Assert.Equal(1f, WorksBonuses.Factor(specialty, WorksBonuses.MiningSpeed));
            foreach (var specialty in WorksCareers.MinerSpecialties) foreach (var stat in WorksBonuses.Stats.Where(s => s != WorksBonuses.MiningSpeed)) Assert.Equal(1f, WorksBonuses.Factor(specialty, stat));
            foreach (var specialty in WorksCareers.BuilderSpecialties.Concat(WorksCareers.MinerSpecialties)) foreach (var stat in WorksBonuses.Stats) Assert.InRange(WorksBonuses.Factor(specialty, stat), 1f, 1.2f);
        }

        [Fact]
        public void The_patch_covers_exactly_the_bonus_stats_and_every_specialty_has_its_text()
        {
            string patch = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Packages/WilsWorks/Patches/WorksStats.xml"));
            foreach (var stat in WorksBonuses.Stats) Assert.Contains("defName=\"" + stat + "\"", patch); Assert.Contains("<success>Always</success>", patch); Assert.Equal(WorksBonuses.Stats.Length, System.Text.RegularExpressions.Regex.Matches(patch, "PatchOperationConditional").Count / 1);
            var lang = XDocument.Load(Path.Combine(RepoPaths.Root(), "Packages/WilsWorks/Languages/English/Keyed/Works.xml")).Root;
            foreach (var specialty in WorksCareers.BuilderSpecialties.Concat(WorksCareers.MinerSpecialties)) { Assert.NotNull(lang.Element("WFL_Works_" + specialty + "Bonus")); Assert.NotNull(lang.Element(WorksCareers.Role(specialty) + "_label")); }
        }

        [Theory]
        [InlineData("FinishFrame", true, false, false, "Builder", "Mason")]
        [InlineData("FinishFrame", false, true, false, "Builder", "Carpenter")]
        [InlineData("FinishFrame", true, true, false, "Builder", "Carpenter")]          // a stone table is furniture
        [InlineData("FinishFrame", false, false, true, "Builder", "Engineer")]
        [InlineData("FinishFrame", false, false, false, "Builder", null)]
        [InlineData("Repair", true, true, true, "Builder", null)]
        [InlineData("FixBrokenDownBuilding", false, false, false, "Builder", "Engineer")]
        [InlineData("SmoothWall", false, false, false, "Builder", "Carpenter")]
        [InlineData("SmoothFloor", false, false, false, "Builder", "Carpenter")]
        [InlineData("OperateDeepDrill", false, false, false, "Miner", null)]
        public void Finished_building_jobs_go_to_the_right_route(string job, bool stone, bool furniture, bool machine, string career, string specialty)
        {
            var t = WorksClassifier.Classify(job, new WorksJobFacts { Stone = stone, Furniture = furniture, Machine = machine });
            Assert.Equal(career == "Builder" ? WorksCareers.Builder : WorksCareers.Miner, t.Career); Assert.Equal(specialty, t.Specialty); Assert.True(t.Points > 0);
        }

        [Theory]
        [InlineData(true, true, "Prospector")]
        [InlineData(true, false, "Prospector")]
        [InlineData(false, true, "Tunneller")]
        [InlineData(false, false, "Quarryman")]
        public void Mining_goes_to_the_prospector_for_ore_the_tunneller_inside_the_mountain_and_the_quarryman_in_the_open(bool ore, bool underRock, string specialty)
        {
            var t = WorksClassifier.Classify("Mine", new WorksJobFacts { Ore = ore, UnderRock = underRock }); Assert.Equal(WorksCareers.Miner, t.Career); Assert.Equal(specialty, t.Specialty);
        }

        [Theory]
        [InlineData("Examine")]          // contains the letters of Mine, and is not mining
        [InlineData("Mineral")]
        [InlineData("DeliverFood")]
        [InlineData("")]
        [InlineData(null)]
        public void Other_jobs_earn_nothing_and_names_are_matched_whole(string job)
        {
            Assert.Null(WorksClassifier.Classify(job, null));
        }

        [Fact]
        public void Experience_is_reported_once_per_job_by_the_real_hooks_and_nothing_is_changed()
        {
            string code = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Works/Game/WorksModule.cs"));
            Assert.Contains("FamilyFramework.LabourDone.Add", code); Assert.Contains("track.Kind + \":\" + r.JobId", code);
            foreach (var forbidden in new[] { ".Destroy(", ".Kill(", "SetFaction", ".jobs.", "StartJob", "Designate" }) Assert.DoesNotContain(forbidden, code);
            Assert.True(WorksCareers.BuildPoints >= WorksCareers.RepairPoints && WorksCareers.RepairPoints >= WorksCareers.SmoothPoints);
        }

        [Fact]
        public void A_workshop_is_an_enclosed_room_with_two_benches_and_no_bed_and_a_mine_is_a_dug_room_under_thick_roof()
        {
            Assert.Equal(WorksPlace.Workshop, WorksRooms.Classify(new WorksRoomFacts { Enclosed = true, Benches = 2 })); Assert.Equal(WorksPlace.None, WorksRooms.Classify(new WorksRoomFacts { Enclosed = true, Benches = 1 }));
            Assert.Equal(WorksPlace.None, WorksRooms.Classify(new WorksRoomFacts { Enclosed = true, Benches = 3, Beds = 1, Cells = 4 }));          // somebody's bedroom
            Assert.Equal(WorksPlace.None, WorksRooms.Classify(new WorksRoomFacts { Enclosed = false, Benches = 3 })); Assert.Equal(WorksPlace.None, WorksRooms.Classify(null));
            Assert.Equal(WorksPlace.Mine, WorksRooms.Classify(new WorksRoomFacts { Cells = 10, ThickRoofCells = 5 })); Assert.Equal(WorksPlace.None, WorksRooms.Classify(new WorksRoomFacts { Cells = 10, ThickRoofCells = 4 }));
            Assert.Equal(WorksPlace.None, WorksRooms.Classify(new WorksRoomFacts { Cells = 5, ThickRoofCells = 5 })); Assert.Equal(WorksPlace.None, WorksRooms.Classify(new WorksRoomFacts { Cells = 20, ThickRoofCells = 20, Outdoors = true }));
        }

        [Fact]
        public void Work_in_a_suiting_place_earns_a_quarter_more_and_never_elsewhere()
        {
            Assert.Equal(5, WorksRooms.Points(4, WorksPlace.Workshop, WorksPlace.Workshop)); Assert.Equal(3, WorksRooms.Points(2, WorksPlace.Workshop, WorksPlace.Workshop));          // at least one more point
            Assert.Equal(4, WorksRooms.Points(4, WorksPlace.Workshop, WorksPlace.Mine)); Assert.Equal(4, WorksRooms.Points(4, WorksPlace.Mine, WorksPlace.None)); Assert.Equal(4, WorksRooms.Points(4, WorksPlace.None, WorksPlace.None));
            Assert.Equal(WorksPlace.Workshop, WorksRooms.Suits("works.build")); Assert.Equal(WorksPlace.Workshop, WorksRooms.Suits("works.smooth")); Assert.Equal(WorksPlace.Mine, WorksRooms.Suits("works.mine")); Assert.Equal(WorksPlace.None, WorksRooms.Suits("works.round"));
        }

        [Fact]
        public void The_page_and_place_recognition_exist_and_describe_themselves()
        {
            string code = string.Join("\n", Directory.GetFiles(Path.Combine(RepoPaths.Root(), "Source/Works"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Replace('\\', '/').Contains("/obj/")).Select(File.ReadAllText));
            Assert.Contains("class WorksRoomGame", code); Assert.Contains("WorksPage.Draw", code); Assert.Contains("WorksRoomGame.PlaceOf", code);
            var lang = XDocument.Load(Path.Combine(RepoPaths.Root(), "Packages/WilsWorks/Languages/English/Keyed/Works.xml")).Root;
            foreach (var key in new[] { "WFL_Works_Page", "WFL_Works_PlacesTitle", "WFL_Works_SpecialtiesTitle", "WFL_Works_PlacesFound", "WFL_Works_PlacesHelp" }) Assert.NotNull(lang.Element(key));
        }

        private static WorkStop S(long id, int x, float fraction = 1f, int seen = 0) => new WorkStop { Id = id, X = x, Fraction = fraction, LastSeen = seen };

        [Fact]
        public void A_builder_repairs_the_most_damaged_building_below_the_threshold_nearest_first_and_nothing_whole()
        {
            Assert.Equal(2, WorksRounds.ChooseRepair(new[] { S(1, 0, .6f), S(2, 50, .3f), S(3, 5, .9f) }, 0, 0).Id);
            Assert.Equal(1, WorksRounds.ChooseRepair(new[] { S(1, 2, .5f), S(2, 50, .5f) }, 0, 0).Id);          // equally damaged: the nearer
            Assert.Null(WorksRounds.ChooseRepair(new[] { S(1, 0, 1f), S(2, 0, .75f), S(3, 0, 0f) }, 0, 0)); Assert.Null(WorksRounds.ChooseRepair(null, 0, 0)); Assert.Null(WorksRounds.ChooseRepair(new WorkStop[] { null }, 0, 0));
        }

        [Fact]
        public void A_miners_walk_visits_the_least_recently_seen_rooms_first_at_most_four_nearest_first_and_deterministically()
        {
            var plan = WorksRounds.Plan(new[] { S(1, 0, seen: 900), S(2, 50), S(3, 10), S(4, 20, seen: 500), S(5, 5, seen: 2000), S(2, 1, seen: 7) }, 0, 0);
            Assert.Equal(WorksRounds.MaxStops, plan.Count); Assert.Equal(1, plan.Count(s => s.Id == 2)); Assert.DoesNotContain(plan, s => s.Id == 5);          // the most recently seen is left for next time
            Assert.Equal(plan.Select(s => s.Id), WorksRounds.Plan(new[] { S(1, 0, seen: 900), S(2, 50), S(3, 10), S(4, 20, seen: 500), S(5, 5, seen: 2000), S(2, 1, seen: 7) }.Reverse(), 0, 0).Select(s => s.Id));
            Assert.Empty(WorksRounds.Plan(null, 0, 0));
        }

        [Fact]
        public void Rounds_are_rationed_and_never_empty_the_site()
        {
            Assert.True(WorksRounds.MayStart(1000, 0, 0)); Assert.False(WorksRounds.MayStart(1000 + WorksRounds.CooldownTicks - 1, 1000, 1)); Assert.True(WorksRounds.MayStart(1000 + WorksRounds.CooldownTicks, 1000, 1)); Assert.False(WorksRounds.MayStart(999999, 1000, WorksRounds.MaxPerDay));
            Assert.Equal(0, WorksRounds.MaxOnRound(0)); Assert.Equal(1, WorksRounds.MaxOnRound(1)); Assert.Equal(1, WorksRounds.MaxOnRound(2)); Assert.Equal(2, WorksRounds.MaxOnRound(3)); for (int n = 2; n < 30; n++) Assert.True(WorksRounds.MaxOnRound(n) < n);
        }

        [Fact]
        public void The_rounds_switch_is_saved_and_defaults_to_on()
        {
            var l = new WorksLedger(); Assert.True(WorksLedger.Decode(new StateReader(l.Encode())).RoundsOn); l.RoundsOn = false; Assert.False(WorksLedger.Decode(new StateReader(l.Encode())).RoundsOn); Assert.True(WorksLedger.Decode(new StateReader("")).RoundsOn);
        }

        [Fact]
        public void The_round_only_repairs_through_the_games_job_or_walks_and_never_changes_anything_else()
        {
            string code = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Works/Game/WorksRoundGame.cs"));
            foreach (var forbidden in new[] { ".Destroy(", ".Kill(", "SetFaction", "Designate", "TakeDamage", ".HitPoints =", "HitPoints +=", "Teleport", "TryTakeOrderedJob", "roofGrid.SetRoof" }) Assert.DoesNotContain(forbidden, code);
            Assert.Contains("JobDefOf.Repair", code); string shared = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/FamilyLife/Game/FrameworkServices.cs")); Assert.Contains("lastJobTag != JobTag.Idle", shared); Assert.Contains("playerForced", shared); Assert.Contains("Sleep", shared); Assert.Contains("FrameworkServices.FreeForDuty(p, ", code); Assert.Contains("HostilesAbout", code); Assert.Contains("RoundsOn", code); Assert.Contains("CanReserveAndReach", code);
            string module = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Works/Game/WorksModule.cs")); Assert.Contains("WorksRoundGame.Run", module); Assert.Contains("WorksRoundGame.Reset()", module);
            Assert.Contains("WFL_WorksRound", File.ReadAllText(Path.Combine(RepoPaths.Root(), "Packages/WilsWorks/Defs/WorksJobs.xml")));
        }

        [Fact]
        public void Kin_are_worried_by_deep_wounds_not_scratches_and_grieve_only_what_the_work_took()
        {
            Assert.False(WorksFamily.Worrying(4.9f)); Assert.True(WorksFamily.Worrying(5f)); Assert.False(WorksFamily.Worrying(0f));
            Assert.True(WorksFamily.LostToTheWork(true, false)); Assert.False(WorksFamily.LostToTheWork(true, true)); Assert.False(WorksFamily.LostToTheWork(false, false));          // a death in a fight is the fight's, and someone not at work is not the work's
        }

        [Fact]
        public void Relief_comes_only_after_a_safe_fifteen_days_of_real_work_and_never_before_a_baseline_exists()
        {
            int d = 60000;
            Assert.False(WorksFamily.ReliefDue(100 * d, 0, 0, true)); Assert.False(WorksFamily.ReliefDue(100 * d, 80 * d, 0, false));
            Assert.False(WorksFamily.ReliefDue(94 * d, 80 * d, 0, true)); Assert.True(WorksFamily.ReliefDue(95 * d, 80 * d, 0, true));
            Assert.False(WorksFamily.ReliefDue(95 * d, 80 * d, 90 * d, true));          // relieved five days ago
            Assert.False(WorksFamily.ReliefDue(60 * d, 80 * d, 0, true));          // the clock never runs backwards
        }

        [Fact]
        public void Working_alongside_needs_both_at_work_and_close()
        {
            Assert.True(WorksFamily.Alongside(true, true, 12f)); Assert.False(WorksFamily.Alongside(true, true, 12.1f)); Assert.False(WorksFamily.Alongside(true, false, 1f)); Assert.False(WorksFamily.Alongside(false, true, 1f));
        }

        [Fact]
        public void The_family_thoughts_are_defined_with_the_right_signs_and_do_not_collide_with_events()
        {
            var defs = XDocument.Load(Path.Combine(RepoPaths.Root(), "Packages/WilsWorks/Defs/WorksFamilyThoughts.xml")).Descendants("ThoughtDef").ToDictionary(x => x.Element("defName").Value);
            Assert.Equal(WorksFamily.Thoughts.OrderBy(x => x), defs.Keys.OrderBy(x => x));
            int Mood(string n) => int.Parse(defs[n].Descendants("baseMoodEffect").Single().Value);
            Assert.True(Mood(WorksFamily.Worried) < 0); Assert.True(Mood(WorksFamily.Lost) < Mood(WorksFamily.Worried)); Assert.True(Mood(WorksFamily.Relieved) > 0); Assert.True(Mood(WorksFamily.BuiltForUs) > 0); Assert.True(Mood(WorksFamily.CrewMood) > 0);
            Assert.True(int.Parse(defs[WorksFamily.CrewBond].Descendants("baseOpinionOffset").Single().Value) > 0);
            Assert.Empty(WorksFamily.Thoughts.Intersect(WorksEvents.All.Select(e => e.Thought)));
        }

        [Fact]
        public void The_history_is_saved_bounded_and_forgets_the_gone()
        {
            var l = new WorksLedger(); l.Harmed(1, 500); l.Relieved(1, 900); l.Harmed(0, 5); l.Relieved(-3, 5); l.Relieved(2, 100);
            var d = WorksLedger.Decode(new StateReader(l.Encode())); Assert.Equal(500, d.LastHarm(1)); Assert.Equal(900, d.LastRelief(1)); Assert.Equal(100, d.LastRelief(2)); Assert.Equal(0, d.LastHarm(0)); Assert.Equal(0, d.LastRelief(-3)); Assert.True(d.RoundsOn);
            d.Prune(id => id == 1); Assert.Equal(0, d.LastRelief(2)); Assert.Equal(500, d.LastHarm(1));
            var big = new WorksLedger(); for (int i = 1; i <= WorksLedger.MaxWorkers + 50; i++) big.Harmed(i, i); Assert.Equal(0, big.LastHarm(WorksLedger.MaxWorkers + 1)); Assert.Equal(5, big.LastHarm(5));
        }

        [Fact]
        public void The_family_game_only_remembers_and_reacts_and_is_registered_and_pruned()
        {
            string code = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Works/Game/WorksFamilyGame.cs"));
            foreach (var forbidden in new[] { ".Destroy(", ".Kill(", "SetFaction", ".jobs.", "StartJob", "TakeDamage", ".Severity =", "ownership.Unclaim", "ClaimBedIfNonMedical", "Designate" }) Assert.DoesNotContain(forbidden, code);
            Assert.DoesNotContain("\"WFL_K_", code);          // every thought comes from the named constants, which are tested against the Defs
            Assert.Contains("IsPlayerHome", code); Assert.Contains("k != builder", code); Assert.Contains("k != worker", code);          // nobody is given a memory about themselves
            string module = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Works/Game/WorksModule.cs")); Assert.Contains("WorksFamilyGame.Run", module); Assert.Contains("WorksFamilyGame.Reconcile", module); Assert.Contains("WorksFamilyGame.Reset()", module); Assert.Contains("WorksFamilyGame.Crew", module); Assert.Contains("WorksFamilyGame.Built", module);
        }
    }
}
