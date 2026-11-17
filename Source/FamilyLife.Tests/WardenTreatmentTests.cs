using System.IO;
using System.Linq;
using System.Xml.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using Xunit;

namespace FamilyLife.Tests
{
    public class WardenTreatmentTests
    {
        [Theory]
        [InlineData(.9f, .8f, false, PrisonerCare.Good)]
        [InlineData(.30f, .40f, false, PrisonerCare.Good)]          // exactly the bar
        [InlineData(.29f, .80f, false, PrisonerCare.Fair)]
        [InlineData(.90f, .39f, false, PrisonerCare.Fair)]
        [InlineData(.14f, .90f, false, PrisonerCare.Poor)]          // starving
        [InlineData(.90f, .19f, false, PrisonerCare.Poor)]          // miserable
        [InlineData(.90f, .90f, true, PrisonerCare.Poor)]          // needs tending right now, however well fed
        [InlineData(.15f, .20f, false, PrisonerCare.Fair)]          // not yet poor
        public void Care_is_rated_from_food_mood_and_whether_they_need_tending(float food, float mood, bool tending, PrisonerCare expected) => Assert.Equal(expected, WardenCare.Rate(food, mood, tending));

        [Theory]
        [InlineData(true, true, false, false, PrisonerOutcome.Executed)]
        [InlineData(true, false, false, false, PrisonerOutcome.Died)]
        [InlineData(true, true, true, true, PrisonerOutcome.Executed)]          // dead is dead, whatever else was true
        [InlineData(false, false, true, true, PrisonerOutcome.Enslaved)]
        [InlineData(false, false, true, false, PrisonerOutcome.Recruited)]
        [InlineData(false, false, false, false, PrisonerOutcome.Released)]
        [InlineData(false, true, false, false, PrisonerOutcome.Released)]          // marked for execution but let go: released
        public void What_became_of_a_prisoner(bool dead, bool execution, bool colonist, bool slave, PrisonerOutcome expected) => Assert.Equal(expected, PrisonerOutcomes.Classify(dead, execution, colonist, slave));

        [Fact]
        public void Kin_react_to_good_and_poor_care_and_fair_care_changes_nothing()
        {
            Assert.Equal((KinReactions.WellCared, KinReactions.Grateful), (PostLabel(PrisonerCare.Good).Mood, PostLabel(PrisonerCare.Good).Opinion)); Assert.Equal((KinReactions.Neglected, KinReactions.Resents), (PostLabel(PrisonerCare.Poor).Mood, PostLabel(PrisonerCare.Poor).Opinion)); Assert.Null(KinReactions.ForCare(PrisonerCare.Fair));
        }
        private static KinReaction PostLabel(PrisonerCare c) => KinReactions.ForCare(c);

        [Fact]
        public void Every_outcome_has_a_reaction_and_the_good_outcomes_please_while_the_bad_ones_anger()
        {
            foreach (PrisonerOutcome o in System.Enum.GetValues(typeof(PrisonerOutcome))) { var r = KinReactions.For(o); Assert.NotNull(r.Mood); Assert.NotNull(r.Opinion); }
            Assert.Equal(KinReactions.Grateful, KinReactions.For(PrisonerOutcome.Recruited).Opinion); Assert.Equal(KinReactions.Grateful, KinReactions.For(PrisonerOutcome.Released).Opinion);
            foreach (var o in new[] { PrisonerOutcome.Enslaved, PrisonerOutcome.Executed, PrisonerOutcome.Died }) Assert.Equal(KinReactions.Resents, KinReactions.For(o).Opinion);
        }

        [Fact]
        public void Every_thought_the_reactions_name_is_defined_with_the_right_sign_and_kind()
        {
            var defs = XDocument.Load(Path.Combine(RepoPaths.Root(), "Packages/WilsWardens/Defs/WardenKinThoughts.xml")).Descendants("ThoughtDef").ToDictionary(x => x.Element("defName").Value);
            Assert.Equal(KinReactions.AllThoughts.OrderBy(x => x), defs.Keys.OrderBy(x => x));
            foreach (var good in new[] { KinReactions.WellCared, KinReactions.Recruited, KinReactions.Freed }) Assert.True(int.Parse(defs[good].Descendants("baseMoodEffect").Single().Value) > 0, good);
            foreach (var bad in new[] { KinReactions.Neglected, KinReactions.Enslaved, KinReactions.Executed, KinReactions.Died, KinReactions.Conflicted }) Assert.True(int.Parse(defs[bad].Descendants("baseMoodEffect").Single().Value) < 0, bad);
            Assert.True(int.Parse(defs[KinReactions.Executed].Descendants("baseMoodEffect").Single().Value) < int.Parse(defs[KinReactions.Died].Descendants("baseMoodEffect").Single().Value));          // an execution hurts more than a death in custody
            foreach (var social in new[] { KinReactions.Grateful, KinReactions.Resents, KinReactions.Favouritism }) { Assert.Equal("Thought_MemorySocial", defs[social].Element("thoughtClass").Value); Assert.NotNull(defs[social].Descendants("baseOpinionOffset").Single()); }
            Assert.True(int.Parse(defs[KinReactions.Grateful].Descendants("baseOpinionOffset").Single().Value) > 0); Assert.True(int.Parse(defs[KinReactions.Resents].Descendants("baseOpinionOffset").Single().Value) < 0);
        }

        [Fact]
        public void Favouritism_needs_enough_looks_at_both_and_a_real_gap_in_either_direction()
        {
            Assert.True(Favouritism.Detected(10, 10, 1, 10)); Assert.True(Favouritism.Detected(1, 10, 10, 10));          // easy on a relative, or hard on one
            Assert.False(Favouritism.Detected(5, 10, 5, 10)); Assert.False(Favouritism.Detected(6, 10, 3, 10));          // 0.6 against 0.3 is a gap of thirty points: not enough
            Assert.True(Favouritism.Detected(7, 10, 3, 10));          // exactly forty points counts
            Assert.False(Favouritism.Detected(4, 10, 8, 20));          // 0.4 against 0.4: no gap at all
            Assert.False(Favouritism.Detected(5, 5, 0, 20)); Assert.False(Favouritism.Detected(5, 20, 0, 5)); Assert.False(Favouritism.Detected(0, 0, 0, 0));          // too few looks at one side
        }

        // ------------------------------------------------------------------ the ledger

        [Fact]
        public void A_warden_is_responsible_for_a_prisoner_for_a_few_days_after_they_last_looked_after_them()
        {
            var l = new WardenLedger(); l.Assign(10, 1, 1000); Assert.Equal(1, l.WardenOf(10, 1000)); Assert.Equal(1, l.WardenOf(10, 1000 + WardenLedger.KeepResponsibleTicks)); Assert.Equal(0, l.WardenOf(10, 1001 + WardenLedger.KeepResponsibleTicks));
            l.Assign(10, 2, 5000); Assert.Equal(2, l.WardenOf(10, 5000)); Assert.Equal(0, l.WardenOf(99, 5000));
            l.Assign(0, 1, 1); l.Assign(5, 0, 1); Assert.Equal(0, l.WardenOf(5, 1));          // nobody is nobody's warden
            Assert.Equal(2, l.WardenOf(10, 100));          // a clock that went back still counts them as responsible
        }

        [Fact]
        public void Care_is_counted_for_kin_and_others_separately_and_outcomes_by_kind()
        {
            var l = new WardenLedger(); l.Rate(1, PrisonerCare.Good, true); l.Rate(1, PrisonerCare.Poor, true); l.Rate(1, PrisonerCare.Good, false); l.Rate(1, PrisonerCare.Fair, false);
            var s = l.Of(1); Assert.Equal((2, 1, 1), (s.Good, s.Fair, s.Poor)); Assert.Equal((2, 1, 2, 1), (s.KinTotal, s.KinGood, s.OtherTotal, s.OtherGood));
            foreach (PrisonerOutcome o in System.Enum.GetValues(typeof(PrisonerOutcome))) l.Outcome(1, o); l.Outcome(1, PrisonerOutcome.Died); l.Outcome(0, PrisonerOutcome.Died);
            Assert.Equal(new[] { 1, 1, 1, 1, 2 }, new[] { s.Recruited, s.Released, s.Enslaved, s.Executed, s.Died }); Assert.Equal(6, s.Outcomes); Assert.False(l.Has(0));
        }

        [Fact]
        public void Counters_are_halved_when_they_grow_so_the_record_stays_bounded_and_recent()
        {
            var l = new WardenLedger(); for (int i = 0; i < WardenLedger.CounterCap + 50; i++) l.Rate(1, i % 2 == 0 ? PrisonerCare.Good : PrisonerCare.Poor, i % 3 == 0);
            var s = l.Of(1); Assert.InRange(s.Samples, 1, WardenLedger.CounterCap); Assert.InRange(s.KinTotal, 1, WardenLedger.CounterCap); Assert.InRange(s.OtherTotal, 1, WardenLedger.CounterCap);
        }

        [Fact]
        public void Favouritism_is_announced_once_a_week_and_resets_the_gap_so_it_is_not_repeated_at_once()
        {
            var l = new WardenLedger(); for (int i = 0; i < 10; i++) { l.Rate(1, PrisonerCare.Good, true); l.Rate(1, PrisonerCare.Poor, false); }
            Assert.True(l.TakeFavouritism(1, 100000)); Assert.False(l.TakeFavouritism(1, 100001));          // the counts were halved and it was just announced
            for (int i = 0; i < 20; i++) { l.Rate(1, PrisonerCare.Good, true); l.Rate(1, PrisonerCare.Poor, false); }
            Assert.False(l.TakeFavouritism(1, 100000 + WardenLedger.FavouritismCooldown - 1)); Assert.True(l.TakeFavouritism(1, 100000 + WardenLedger.FavouritismCooldown));
            Assert.False(new WardenLedger().TakeFavouritism(7, 1)); var even = new WardenLedger(); for (int i = 0; i < 10; i++) { even.Rate(2, PrisonerCare.Good, true); even.Rate(2, PrisonerCare.Good, false); } Assert.False(even.TakeFavouritism(2, 1));
        }

        [Fact]
        public void The_ledger_round_trips_and_drops_what_is_gone()
        {
            var l = new WardenLedger(); l.Assign(10, 1, 500); l.Assign(11, 2, 600); l.Rate(1, PrisonerCare.Good, true); l.Rate(2, PrisonerCare.Poor, false); l.Outcome(1, PrisonerOutcome.Recruited); l.Of(2).LastFavouritism = 42;
            var d = WardenLedger.Decode(new StateReader(l.Encode()));
            Assert.Equal(1, d.WardenOf(10, 500)); Assert.Equal(2, d.WardenOf(11, 600)); Assert.Equal((1, 1, 1), (d.Of(1).Good, d.Of(1).KinGood, d.Of(1).Recruited)); Assert.Equal((1, 42), (d.Of(2).Poor, d.Of(2).LastFavouritism));
            Assert.Equal(2, d.Prune(w => w == 1));          // warden 2 gone, and prisoner 11 whose warden it was
            Assert.False(d.Has(2)); Assert.Equal(0, d.WardenOf(11, 600)); Assert.Equal(1, d.WardenOf(10, 500));
            Assert.Empty(WardenLedger.Decode(new StateReader("")).Wardens); Assert.Equal(0, WardenLedger.Decode(new StateReader("w=1,2\ngood=5")).Of(2).Good);          // a short record fills in zeros
        }

        [Fact]
        public void The_ledger_is_bounded_in_prisoners()
        {
            var l = new WardenLedger(); for (int i = 1; i <= WardenLedger.MaxPrisoners + 30; i++) l.Assign(i, 1, i);
            Assert.Equal(0, l.WardenOf(1, 1)); Assert.Equal(1, l.WardenOf(WardenLedger.MaxPrisoners + 30, WardenLedger.MaxPrisoners + 30));          // the oldest were dropped
        }

        [Fact]
        public void The_treatment_record_only_watches_and_reacts_it_never_changes_a_prisoner_a_warden_or_a_decision()
        {
            string code = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Wardens/Game/WardenTreatmentGame.cs"));
            foreach (var forbidden in new[] { ".SetFaction(", ".Kill(", ".Destroy(", "SetExclusiveInteractionMode", "jobs.", ".health.", "Drafted", ".Release", "TryTakeOrderedJob", "StartJob", "SetGuestStatus" })
                Assert.DoesNotContain(forbidden, code);
            Assert.Contains("PrisonersOfColony", code); Assert.Contains("IsPlayerHome", code);          // only the colony's own prisoners on its own maps
            Assert.DoesNotContain("\"WFL_W_", code);          // every thought comes from the named constants, which are tested against the Defs
            Assert.Contains("warden != kin", code); Assert.Contains("p != warden", code);          // nobody is given an opinion of themselves, and colleagues exclude the warden
        }

        [Fact]
        public void The_record_is_registered_hourly_credits_the_last_warden_and_is_pruned_on_load()
        {
            string module = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Wardens/Game/WardensModule.cs"));
            Assert.Contains("Interval = 2500, Run = WardenTreatmentGame.Run", module); Assert.Contains("WardenTreatmentGame.Labour", module); Assert.Contains("WardenTreatmentGame.Reconcile", module);
            Assert.Contains("WardenTreatmentGame.Reset()", module);
            string code = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Wardens/Game/WardenTreatmentGame.cs"));
            Assert.Contains("WardenStories.ActiveRole(r.Pawn) == null", code);          // only a holder of a Warden role becomes the responsible warden
        }
    }
}
