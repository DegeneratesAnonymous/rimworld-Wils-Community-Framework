using System.IO;
using System.Linq;
using System.Xml.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class WardenEscortTests
    {
        [Fact]
        public void A_calm_rested_prisoner_is_taken_and_anything_else_is_refused_with_a_reason()
        {
            Assert.True(WardenEscorts.Willing(new EscortFacts()));
            Assert.Equal(EscortRefusal.Asleep, WardenEscorts.Check(new EscortFacts { Awake = false })); Assert.Equal(EscortRefusal.Downed, WardenEscorts.Check(new EscortFacts { Downed = true }));
            Assert.Equal(EscortRefusal.Mental, WardenEscorts.Check(new EscortFacts { MentalState = true })); Assert.Equal(EscortRefusal.Breaking, WardenEscorts.Check(new EscortFacts { Breaking = true }));
            Assert.Equal(EscortRefusal.Hurt, WardenEscorts.Check(new EscortFacts { NeedsTending = true })); Assert.Equal(EscortRefusal.Needs, WardenEscorts.Check(new EscortFacts { Hungry = true })); Assert.Equal(EscortRefusal.Needs, WardenEscorts.Check(new EscortFacts { Tired = true }));
            Assert.Equal(EscortRefusal.Unhappy, WardenEscorts.Check(new EscortFacts { Mood = .39f })); Assert.True(WardenEscorts.Willing(new EscortFacts { Mood = WardenEscorts.CalmMood })); Assert.Equal(EscortRefusal.Done, WardenEscorts.Check(new EscortFacts { DoneToday = true })); Assert.Equal(EscortRefusal.Done, WardenEscorts.Check(null));
        }

        [Fact]
        public void The_room_chosen_is_the_visiting_room_for_kin_and_the_interview_room_for_persuasion_and_none_otherwise()
        {
            Assert.Equal(WardenRoom.VisitingRoom, WardenEscorts.Choose(true, true, true, true)); Assert.Equal(WardenRoom.InterviewRoom, WardenEscorts.Choose(false, true, true, true));
            Assert.Equal(WardenRoom.InterviewRoom, WardenEscorts.Choose(true, true, true, false));          // kin but no visiting room: interview instead
            Assert.Equal(WardenRoom.None, WardenEscorts.Choose(true, false, true, false)); Assert.Equal(WardenRoom.None, WardenEscorts.Choose(false, false, true, true)); Assert.Equal(WardenRoom.None, WardenEscorts.Choose(false, true, false, true));
        }

        [Fact]
        public void An_interview_eases_resistance_a_little_never_below_zero_and_never_negative()
        {
            Assert.Equal(.5f, WardenEscorts.Relief(10f)); Assert.Equal(.2f, WardenEscorts.Relief(.2f)); Assert.Equal(0f, WardenEscorts.Relief(0f)); Assert.Equal(0f, WardenEscorts.Relief(-3f));
            Assert.True(WardenEscorts.ResistanceRelief <= .5f);          // half a vanilla recruitment chat, at most
            Assert.Equal(.02f, WardenEscorts.CertaintyDrop(.9f)); Assert.Equal(.01f, WardenEscorts.CertaintyDrop(.01f)); Assert.Equal(0f, WardenEscorts.CertaintyDrop(0f));
        }

        [Fact]
        public void The_daily_pace_cannot_empty_a_prison_of_its_wardens()
        {
            Assert.Equal(0, WardenEscorts.MaxOut(0)); Assert.Equal(1, WardenEscorts.MaxOut(1)); Assert.Equal(1, WardenEscorts.MaxOut(2)); Assert.Equal(1, WardenEscorts.MaxOut(3)); Assert.Equal(2, WardenEscorts.MaxOut(4));
            for (int n = 2; n < 40; n++) Assert.True(WardenEscorts.MaxOut(n) * 2 <= n);
            Assert.True(WardenEscorts.MayEscort(WardenEscorts.MaxEscortsPerDay - 1)); Assert.False(WardenEscorts.MayEscort(WardenEscorts.MaxEscortsPerDay));
            Assert.False(WardenEscorts.TimedOut(1000, 1000 + WardenEscorts.LimitTicks)); Assert.True(WardenEscorts.TimedOut(1000, 1001 + WardenEscorts.LimitTicks)); Assert.False(WardenEscorts.TimedOut(1000, 500));
            Assert.True(WardenEscorts.StayTicks < WardenEscorts.LimitTicks);
        }

        [Fact]
        public void The_escort_thoughts_are_defined_and_pleasant_and_a_visit_is_better_than_an_interview()
        {
            var defs = XDocument.Load(Path.Combine(RepoPaths.Root(), "Packages/WilsWardens/Defs/WardenFamilyThoughts.xml")).Descendants("ThoughtDef").ToDictionary(x => x.Element("defName").Value);
            int Mood(string n) => int.Parse(defs[n].Descendants("baseMoodEffect").Single().Value);
            Assert.True(Mood(WardenEscorts.InterviewThought) > 0); Assert.True(Mood(WardenEscorts.VisitThought) > Mood(WardenEscorts.InterviewThought)); Assert.True(Mood(WardenEscorts.VisitorThought) > 0);
        }

        [Fact]
        public void The_escort_never_touches_doors_locks_factions_fates_or_needs_and_always_checks_the_way_and_the_prisoners_state()
        {
            string code = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Wardens/Game/WardenEscortGame.cs"));
            foreach (var forbidden in new[] { ".SetFaction(", ".Kill(", ".Destroy(", "SetExclusiveInteractionMode", ".Release(", "SetGuestStatus", "Teleport", "Building_Door", ".Open", ".ForcedOpen", ".needs.food.CurLevel =", "SetInitiator", "TryTakeOrderedJob", ".Position =" })
                Assert.DoesNotContain(forbidden, code);
            Assert.Contains("StaysIndoors", code); Assert.Contains("TouchesMapEdge", code); Assert.Contains("PsychologicallyOutdoors", code); Assert.Contains("IsPrisonBreaking", code); Assert.Contains("HostilesAbout", code);
            Assert.Contains("WardenEscorts.Willing", code); Assert.Contains("WardenEscorts.Relief", code); Assert.Contains("PrisonersOfColony", code); Assert.Contains("IsPlayerHome", code);
            Assert.Contains("RoundsOn", code);          // one switch turns rounds and escorts off together
        }

        [Fact]
        public void The_prisoner_always_has_a_way_home_and_the_escort_is_registered_and_reset()
        {
            string code = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Wardens/Game/WardenEscortGame.cs"));
            Assert.Contains("PrisonerFollow", code); Assert.Contains("targetQueueA", code); Assert.Contains("JumpToToil(home)", code); Assert.Contains("yield return home", code);
            string module = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Source/Wardens/Game/WardensModule.cs")); Assert.Contains("WardenEscortGame.Run", module); Assert.Contains("WardenEscortGame.Reset()", module);
            Assert.Contains("WFL_WardenEscort", File.ReadAllText(Path.Combine(RepoPaths.Root(), "Packages/WilsWardens/Defs/WardenJobs.xml")));
        }
    }
}
