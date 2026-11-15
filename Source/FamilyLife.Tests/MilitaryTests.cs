using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class MilitaryTests
    {
        private static CombatContext Ctx(float d = 15f, int near = 1, bool cover = false, bool still = false) => new CombatContext { InCombat = true, TargetDistance = d, HostilesNear = near, CoveringAlly = cover, Stationary = still };
        private static readonly float[] Distances = { 1f, 4f, 8f, 15f, 22f, 28f, 35f };

        /// <summary>A factor improves its stat when it moves it the way that helps the soldier. For three stats a smaller number is the better one.</summary>
        private static bool Improves(string stat, float f) => stat == MilitaryDoctrine.Incoming || stat == MilitaryDoctrine.Cooldown || stat == MilitaryDoctrine.AimDelay ? f < 1f : f > 1f;
        private static bool Worsens(string stat, float f) => stat == MilitaryDoctrine.Incoming || stat == MilitaryDoctrine.Cooldown || stat == MilitaryDoctrine.AimDelay ? f > 1f : f < 1f;

        private static System.Collections.Generic.IEnumerable<CombatContext> Grid()
        {
            foreach (float d in Distances) foreach (int near in new[] { 0, 1, 2, 3, 4 }) foreach (bool cover in new[] { false, true }) foreach (bool still in new[] { false, true }) yield return Ctx(d, near, cover, still);
        }

        // ------------------------------------------------------------------ the tree

        [Fact]
        public void Soldier_and_ranger_are_parallel_branches_and_each_specialty_needs_its_own()
        {
            var d = MilitaryCareers.Definition();
            Assert.Equal(new[] { "Ranger", "Soldier" }, d.Level2Branches.Keys.OrderBy(x => x).ToArray());
            foreach (var s in MilitaryCareers.SoldierSpecialties) Assert.Equal(new[] { "Soldier" }, d.Specialties[s].RequiresAny.ToArray());
            foreach (var s in MilitaryCareers.RangerSpecialties) Assert.Equal(new[] { "Ranger" }, d.Specialties[s].RequiresAny.ToArray());
            Assert.Equal(6, d.Specialties.Count);
            foreach (var s in MilitaryCareers.Specialties) Assert.Equal(MilitaryCareers.SoldierSpecialties.Contains(s) ? "Soldier" : "Ranger", MilitaryCareers.BranchOf(s));
        }

        [Fact]
        public void A_ranger_cannot_become_a_vanguard_by_experience_alone()
        {
            var d = MilitaryCareers.Definition(); var l = new CareerLedger();
            l.Award(1, d.Id, "Ranger", "mil", "a", 150, 0); l.Evaluate(1, d, k => 20); Assert.True(l.IsSpecialist(1, d.Id, "Ranger"));
            l.Award(1, d.Id, "Vanguard", "mil", "b", 300, 1); l.Evaluate(1, d, k => 20);
            Assert.False(l.IsSpecialist(1, d.Id, "Vanguard"));
            l.Award(1, d.Id, "Sniper", "mil", "c", 300, 2); l.Evaluate(1, d, k => 20);
            Assert.True(l.IsSpecialist(1, d.Id, "Sniper"));
        }

        [Fact]
        public void Skill_gates_keep_the_masters_for_the_skilled()
        {
            var d = MilitaryCareers.Definition(); var l = new CareerLedger();
            l.Award(1, d.Id, "Soldier", "mil", "a", 150, 0); l.Evaluate(1, d, k => k == "Melee" ? 11 : 0);
            Assert.True(l.IsSpecialist(1, d.Id, "Soldier"));
            l.Award(1, d.Id, "Duelist", "mil", "b", 400, 1); l.Evaluate(1, d, k => k == "Melee" ? 11 : 0); Assert.False(l.IsSpecialist(1, d.Id, "Duelist"));          // needs Melee 12
            l.Evaluate(1, d, k => k == "Melee" ? 12 : 0); Assert.True(l.IsSpecialist(1, d.Id, "Duelist"));
        }

        // ------------------------------------------------------------------ what a fight is credited to

        [Theory]
        [InlineData(false, 1f, 1, 0, false, 0f, "Soldier", "Duelist")]          // one on one
        [InlineData(false, 1f, 1, 3, false, 0f, "Soldier", "Vanguard")]          // pressing into several
        [InlineData(false, 1f, 1, 1, false, .8f, "Soldier", "Vanguard")]          // heavily armoured
        [InlineData(false, 1f, 1, 3, true, 0f, "Soldier", "Guardian")]          // the fight was for a friend, whatever the odds
        [InlineData(true, 40f, 1, 0, false, 0f, "Ranger", "Sniper")]
        [InlineData(true, 8f, 5, 0, false, 0f, "Ranger", "Gunner")]
        [InlineData(true, 20f, 1, 0, false, 0f, "Ranger", "Marksman")]
        [InlineData(true, 20f, 5, 0, false, 0f, "Ranger", "Marksman")]          // a burst weapon from mid range is still precision work
        [InlineData(true, 30f, 5, 0, false, 0f, "Ranger", "Sniper")]          // far away is a sniper's shot whatever the weapon
        public void Fights_are_credited_to_the_role_they_resemble(bool ranged, float distance, int burst, int others, bool defended, float armor, string route, string specialty)
        {
            var t = CombatClassifier.Classify(new CombatFacts { Ranged = ranged, Distance = distance, Burst = burst, OtherEnemiesNear = others, DefendedAlly = defended, Armor = armor });
            Assert.Equal(route, t.Route); Assert.Equal(specialty, t.Specialty);
        }

        [Fact] public void No_facts_no_credit() => Assert.Null(CombatClassifier.Classify(null));

        [Fact]
        public void Every_specialty_can_be_earned_by_some_kind_of_fight()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (bool ranged in new[] { false, true }) foreach (float d in new[] { 1f, 8f, 20f, 40f }) foreach (int burst in new[] { 1, 5 }) foreach (int others in new[] { 0, 1, 3 }) foreach (bool def in new[] { false, true }) foreach (float armor in new[] { 0f, .8f })
                seen.Add(CombatClassifier.Classify(new CombatFacts { Ranged = ranged, Distance = d, Burst = burst, OtherEnemiesNear = others, DefendedAlly = def, Armor = armor }).Specialty);
            Assert.Equal(MilitaryCareers.Specialties.OrderBy(x => x), seen.OrderBy(x => x));
        }

        // ------------------------------------------------------------------ doctrine: distinct jobs, each paid for

        [Fact]
        public void Every_specialty_has_its_own_job_and_a_stated_weakness()
        {
            Assert.Equal(MilitaryCareers.Specialties.OrderBy(x => x), MilitaryDoctrine.Profiles.Select(p => p.Role).OrderBy(x => x));
            Assert.Equal(6, MilitaryDoctrine.Profiles.Select(p => p.Job).Distinct().Count());
            foreach (var p in MilitaryDoctrine.Profiles) { Assert.False(string.IsNullOrWhiteSpace(p.Job)); Assert.False(string.IsNullOrWhiteSpace(p.Strength)); Assert.False(string.IsNullOrWhiteSpace(p.Weakness)); Assert.True(p.MinRange <= p.PreferredRange && p.PreferredRange <= p.MaxRange, p.Role); }
        }

        [Fact]
        public void Every_specialty_is_better_at_something_and_worse_at_something_else()
        {
            foreach (var role in MilitaryCareers.Specialties)
            {
                var factors = (from stat in MilitaryDoctrine.Stats from c in Grid() select new { stat, f = MilitaryDoctrine.Factor(role, stat, c) }).ToList();
                Assert.True(factors.Any(x => Improves(x.stat, x.f)), role + " has no strength");
                Assert.True(factors.Any(x => Worsens(x.stat, x.f)), role + " has no weakness");
            }
        }

        [Fact]
        public void No_two_specialties_behave_the_same()
        {
            string Signature(string role) => string.Join("|", from stat in MilitaryDoctrine.Stats from c in Grid() select MilitaryDoctrine.Factor(role, stat, c).ToString("0.###"));
            Assert.Equal(6, MilitaryCareers.Specialties.Select(Signature).Distinct().Count());
            // and each touches stats the others leave alone, or reads the situation differently: nobody is a copy with a different label
            foreach (var role in MilitaryCareers.Specialties) Assert.True(MilitaryDoctrine.Stats.Any(s => MilitaryDoctrine.Touches(role, s)), role);
        }

        [Fact]
        public void Melee_roles_never_touch_shooting_and_ranged_roles_never_touch_melee()
        {
            foreach (var role in MilitaryCareers.SoldierSpecialties) foreach (var stat in new[] { MilitaryDoctrine.Accuracy, MilitaryDoctrine.AimDelay, MilitaryDoctrine.Cooldown }) Assert.False(MilitaryDoctrine.Touches(role, stat), role + " " + stat);
            foreach (var role in MilitaryCareers.RangerSpecialties) foreach (var stat in new[] { MilitaryDoctrine.MeleeDamage, MilitaryDoctrine.MeleeHit, MilitaryDoctrine.MeleeDodge, MilitaryDoctrine.MoveSpeed, MilitaryDoctrine.Incoming }) Assert.False(MilitaryDoctrine.Touches(role, stat), role + " " + stat);
        }

        [Fact]
        public void Doctrine_factors_are_always_bounded_and_need_a_fight()
        {
            foreach (var role in MilitaryCareers.Specialties.Concat(new[] { "Soldier", "Ranger", "Recruit", null, "Nobody" })) foreach (var stat in MilitaryDoctrine.Stats.Concat(new[] { "Other", null }))
            {
                foreach (var c in Grid()) { float f = MilitaryDoctrine.Factor(role, stat, c); Assert.InRange(f, MilitaryDoctrine.MaxDown, MilitaryDoctrine.MaxUp); }
                Assert.Equal(1f, MilitaryDoctrine.Factor(role, stat, new CombatContext { InCombat = false, TargetDistance = 5f, HostilesNear = 1, CoveringAlly = true, Stationary = true }));
                Assert.Equal(1f, MilitaryDoctrine.Factor(role, stat, null));
            }
        }

        [Fact]
        public void Each_doctrine_applies_only_where_its_job_is()
        {
            // Vanguard: faster while closing, not once in contact or from far away
            Assert.Equal(1.2f, MilitaryDoctrine.Factor("Vanguard", MilitaryDoctrine.MoveSpeed, Ctx(10f)));
            Assert.Equal(1f, MilitaryDoctrine.Factor("Vanguard", MilitaryDoctrine.MoveSpeed, Ctx(1f)));
            Assert.Equal(1f, MilitaryDoctrine.Factor("Vanguard", MilitaryDoctrine.MoveSpeed, Ctx(30f)));
            Assert.Equal(1.1f, MilitaryDoctrine.Factor("Vanguard", MilitaryDoctrine.MeleeDamage, Ctx(1f)));
            // Guardian: tougher only when covering someone
            Assert.Equal(.85f, MilitaryDoctrine.Factor("Guardian", MilitaryDoctrine.Incoming, Ctx(1f, cover: true)));
            Assert.Equal(1f, MilitaryDoctrine.Factor("Guardian", MilitaryDoctrine.Incoming, Ctx(1f, cover: false)));
            // Duelist: better alone, worse when outnumbered
            Assert.True(MilitaryDoctrine.Factor("Duelist", MilitaryDoctrine.MeleeDodge, Ctx(1f, near: 1)) > 1f);
            Assert.Equal(1f, MilitaryDoctrine.Factor("Duelist", MilitaryDoctrine.MeleeDodge, Ctx(1f, near: 2)));
            Assert.True(MilitaryDoctrine.Factor("Duelist", MilitaryDoctrine.MeleeDodge, Ctx(1f, near: 3)) < 1f);
            // Marksman: the middle band
            Assert.True(MilitaryDoctrine.Factor("Marksman", MilitaryDoctrine.Accuracy, Ctx(20f)) > 1f);
            Assert.True(MilitaryDoctrine.Factor("Marksman", MilitaryDoctrine.Accuracy, Ctx(3f)) < 1f);
            Assert.Equal(1f, MilitaryDoctrine.Factor("Marksman", MilitaryDoctrine.Accuracy, Ctx(40f)));
            // Gunner: volume up close, not at range
            Assert.True(MilitaryDoctrine.Factor("Gunner", MilitaryDoctrine.Cooldown, Ctx(10f)) < 1f);
            Assert.True(MilitaryDoctrine.Factor("Gunner", MilitaryDoctrine.Accuracy, Ctx(8f)) > 1f);
            Assert.True(MilitaryDoctrine.Factor("Gunner", MilitaryDoctrine.Accuracy, Ctx(30f)) < 1f);
            // Sniper: far and set up
            Assert.True(MilitaryDoctrine.Factor("Sniper", MilitaryDoctrine.Accuracy, Ctx(35f, still: true)) > 1f);
            Assert.Equal(1f, MilitaryDoctrine.Factor("Sniper", MilitaryDoctrine.Accuracy, Ctx(35f, still: false)));
            Assert.True(MilitaryDoctrine.Factor("Sniper", MilitaryDoctrine.Accuracy, Ctx(5f)) < 1f);
            Assert.True(MilitaryDoctrine.Factor("Sniper", MilitaryDoctrine.AimDelay, Ctx(35f)) > 1f);
        }

        // ------------------------------------------------------------------ who a shooter prefers

        [Fact]
        public void Only_the_three_shooting_roles_have_a_target_preference_and_it_is_bounded()
        {
            var open = new TargetFacts { Distance = 15f, Armed = true, Ranged = true, Exposed = true, AttackingAlly = true, Threat = 1f };
            foreach (var role in MilitaryCareers.SoldierSpecialties.Concat(new[] { "Soldier", "Recruit", null })) Assert.Equal(0f, MilitaryDoctrine.TargetBias(role, open));
            foreach (var role in MilitaryCareers.RangerSpecialties)
                foreach (float d in new[] { 1f, 5f, 15f, 40f }) foreach (bool b in new[] { false, true }) foreach (float threat in new[] { 0f, .5f, 1f })
                    Assert.InRange(MilitaryDoctrine.TargetBias(role, new TargetFacts { Distance = d, Armed = b, Ranged = b, Exposed = b, AttackingAlly = b, Threat = threat }), -MilitaryDoctrine.MaxBias, MilitaryDoctrine.MaxBias);
            Assert.Equal(0f, MilitaryDoctrine.TargetBias("Sniper", null));
        }

        [Fact]
        public void Shooters_prefer_different_enemies()
        {
            var exposed = new TargetFacts { Distance = 20f, Exposed = true }; var covered = new TargetFacts { Distance = 20f, Exposed = false };
            Assert.True(MilitaryDoctrine.TargetBias("Marksman", exposed) > MilitaryDoctrine.TargetBias("Marksman", covered));
            var near = new TargetFacts { Distance = 4f }; var far = new TargetFacts { Distance = 25f };
            Assert.True(MilitaryDoctrine.TargetBias("Gunner", near) > MilitaryDoctrine.TargetBias("Gunner", far));
            var deadly = new TargetFacts { Distance = 30f, Threat = 1f, Armed = true, Ranged = true }; var weak = new TargetFacts { Distance = 30f, Threat = .1f };
            Assert.True(MilitaryDoctrine.TargetBias("Sniper", deadly) > MilitaryDoctrine.TargetBias("Sniper", weak));
            Assert.True(MilitaryDoctrine.TargetBias("Sniper", new TargetFacts { Distance = 4f, Threat = 1f }) < MilitaryDoctrine.TargetBias("Sniper", deadly));          // leaves the close ones to someone else
            // the same two enemies, three different first choices
            Assert.True(MilitaryDoctrine.TargetBias("Gunner", near) > MilitaryDoctrine.TargetBias("Sniper", near));
        }

        // ------------------------------------------------------------------ events and packaging

        [Fact]
        public void Every_military_role_has_five_positive_negative_and_relationship_events()
        {
            Assert.Equal(new[] { "Recruit", "Soldier", "Ranger", "Vanguard", "Guardian", "Duelist", "Marksman", "Gunner", "Sniper" }, MilitaryEvents.Roles);
            foreach (var role in MilitaryEvents.Roles) { var e = MilitaryEvents.All.Where(x => x.Role == role).ToList(); Assert.Equal(5, e.Count(x => x.Mood > 0)); Assert.Equal(5, e.Count(x => x.Mood < 0)); Assert.Equal(5, e.Count(x => x.Opinion != 0)); Assert.Equal(15, e.Select(x => x.Fact).Distinct().Count()); }
            Assert.Equal(MilitaryEvents.All.Length, MilitaryEvents.All.Select(x => x.Id).Distinct().Count());
            Assert.Equal(MilitaryEvents.All.Length, MilitaryEvents.All.Select(x => x.Thought).Distinct().Count());
        }

        [Fact]
        public void Every_event_has_a_thought_and_every_role_a_name_and_a_bonus_text()
        {
            string root = RepoPaths.Root();
            var names = XDocument.Load(Path.Combine(root, "Packages/WilsMilitary/Defs/MilitaryThoughts.xml")).Descendants("defName").Select(x => x.Value).ToList();
            Assert.Equal(MilitaryEvents.All.Length, names.Count);
            foreach (var e in MilitaryEvents.All) Assert.Contains(e.Thought, names);
            var keys = XDocument.Load(Path.Combine(root, "Packages/WilsMilitary/Languages/English/Keyed/Military.xml")).Root.Elements().Select(x => x.Name.LocalName).ToList();
            Assert.Contains("WFL_Military_Career", keys);
            foreach (var role in MilitaryEvents.Roles) Assert.Contains("wils.military." + role.ToLowerInvariant() + "_label", keys);
            foreach (var s in MilitaryCareers.Specialties) Assert.Contains("WFL_Military_" + s + "Bonus", keys);
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Packages/WilsMilitary"), "*.xml", SearchOption.AllDirectories)) XDocument.Load(file);
        }

        [Fact]
        public void The_stories_the_game_code_names_exist_in_the_event_table()
        {
            foreach (var pair in new[] { ("Guardian", "FriendFell"), ("Soldier", "SquanderedLife"), ("Vanguard", "LostMyFlank"), ("Recruit", "FirstBlood"), ("Soldier", "WoundedInAction"), ("Vanguard", "CutDownLeading"), ("Duelist", "StruckDown"), ("Sniper", "ExposedAlone"), ("Ranger", "SpottedPosition") })
                Assert.Contains(MilitaryEvents.All, e => e.Role == pair.Item1 && e.Fact == pair.Item2);
            foreach (var role in MilitaryEvents.Roles) foreach (int cat in new[] { 0, 1, 2 }) for (int i = 0; i < 5; i++) Assert.NotNull(MilitaryEvents.Fact(role, cat, i));
            Assert.Null(MilitaryEvents.Fact("Nobody", 0, 0)); Assert.Null(MilitaryEvents.Fact("Sniper", 3, 0)); Assert.Null(MilitaryEvents.Fact("Sniper", 0, 5));
        }

        [Fact]
        public void The_stat_patch_covers_every_stat_the_doctrine_changes_and_the_module_has_no_patch_of_its_own()
        {
            string root = RepoPaths.Root();
            var doc = XDocument.Load(Path.Combine(root, "Packages/WilsMilitary/Patches/MilitaryStats.xml")); string text = doc.ToString();
            foreach (var stat in MilitaryDoctrine.Stats) Assert.Contains("defName=\"" + stat + "\"", text);
            Assert.Contains("FamilyLife.StatPart_MilitaryDoctrine", text);
            foreach (var op in doc.Root.Elements("Operation")) Assert.Equal("Always", op.Element("success")?.Value);          // a stat another game version lacks is skipped quietly
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Source/Military"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains("/obj/"))) Assert.DoesNotContain("HarmonyPatch", File.ReadAllText(file));
        }
    }
}
