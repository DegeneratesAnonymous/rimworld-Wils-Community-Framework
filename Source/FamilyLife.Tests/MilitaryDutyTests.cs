using System.Linq;
using FamilyLife.Domain;
using Xunit;

namespace FamilyLife.Tests
{
    public class MilitaryDutyTests
    {
        private static readonly string[] Roles = { "Recruit", "Soldier", "Ranger", "Vanguard", "Guardian", "Duelist", "Marksman", "Gunner", "Sniper", "FieldMedic", "Officer" };

        [Fact]
        public void There_is_no_routine_at_red_and_every_role_has_one_otherwise()
        {
            foreach (var role in Roles) { Assert.Equal(DutyKind.None, MilitaryDuties.For(role, AlertLevel.Red)); Assert.NotEqual(DutyKind.None, MilitaryDuties.For(role, AlertLevel.Normal)); Assert.NotEqual(DutyKind.None, MilitaryDuties.For(role, AlertLevel.Yellow)); }
            Assert.Equal(DutyKind.None, MilitaryDuties.For(null, AlertLevel.Normal)); Assert.Equal(DutyKind.None, MilitaryDuties.For("Nobody", AlertLevel.Normal)); Assert.Equal(DutyKind.None, MilitaryDuties.For("", AlertLevel.Yellow));
        }

        [Fact]
        public void Each_specialty_has_its_agreed_duty()
        {
            Assert.Equal(DutyKind.Drill, MilitaryDuties.For("Vanguard", AlertLevel.Normal)); Assert.Equal(DutyKind.Spar, MilitaryDuties.For("Duelist", AlertLevel.Normal));
            Assert.Equal(DutyKind.RangePractice, MilitaryDuties.For("Marksman", AlertLevel.Normal)); Assert.Equal(DutyKind.FirstAid, MilitaryDuties.For("FieldMedic", AlertLevel.Normal)); Assert.Equal(DutyKind.Muster, MilitaryDuties.For("Officer", AlertLevel.Normal));
            // the watch trio train at Normal and stand watch at Yellow
            Assert.Equal(DutyKind.Patrol, MilitaryDuties.For("Gunner", AlertLevel.Yellow)); Assert.Equal(DutyKind.Lookout, MilitaryDuties.For("Sniper", AlertLevel.Yellow)); Assert.Equal(DutyKind.GateGuard, MilitaryDuties.For("Guardian", AlertLevel.Yellow));
            foreach (var role in new[] { "Gunner", "Sniper", "Guardian" }) Assert.True(MilitaryDuties.UsesYard(MilitaryDuties.For(role, AlertLevel.Normal)), role);
            // and the others carry on at Yellow
            foreach (var role in new[] { "Vanguard", "Duelist", "Marksman", "FieldMedic", "Officer" }) Assert.Equal(MilitaryDuties.For(role, AlertLevel.Normal), MilitaryDuties.For(role, AlertLevel.Yellow));
            foreach (var role in new[] { "Recruit", "Soldier", "Ranger" }) Assert.True(MilitaryDuties.IsWatch(MilitaryDuties.For(role, AlertLevel.Yellow)));
        }

        [Fact]
        public void Only_the_named_roles_lead_and_only_group_duties_can_be_joined()
        {
            Assert.True(MilitaryDuties.Leads("Vanguard", DutyKind.Drill)); Assert.True(MilitaryDuties.Leads("Marksman", DutyKind.RangePractice)); Assert.True(MilitaryDuties.Leads("Officer", DutyKind.Muster)); Assert.True(MilitaryDuties.Leads("Duelist", DutyKind.Spar));
            Assert.False(MilitaryDuties.Leads("Soldier", DutyKind.Drill)); Assert.False(MilitaryDuties.Leads("Vanguard", DutyKind.RangePractice)); Assert.False(MilitaryDuties.Leads("Sniper", DutyKind.RangePractice));
            Assert.True(MilitaryDuties.IsGroup(DutyKind.Drill)); Assert.True(MilitaryDuties.IsGroup(DutyKind.RangePractice)); Assert.True(MilitaryDuties.IsGroup(DutyKind.Muster));
            Assert.False(MilitaryDuties.IsGroup(DutyKind.Spar)); Assert.False(MilitaryDuties.IsGroup(DutyKind.Patrol)); Assert.False(MilitaryDuties.IsGroup(DutyKind.None));
        }

        [Fact]
        public void Duties_know_where_they_happen_and_what_they_exercise()
        {
            Assert.Equal(PostKind.Rally, MilitaryDuties.PostFor(DutyKind.Muster));
            foreach (var k in new[] { DutyKind.Drill, DutyKind.Patrol, DutyKind.Lookout, DutyKind.GateGuard, DutyKind.Spar, DutyKind.None }) Assert.Null(MilitaryDuties.PostFor(k));
            Assert.True(MilitaryDuties.UsesPosition(DutyKind.Lookout)); Assert.True(MilitaryDuties.UsesPosition(DutyKind.GateGuard)); Assert.False(MilitaryDuties.UsesPosition(DutyKind.Patrol)); Assert.False(MilitaryDuties.UsesPosition(DutyKind.Muster));
            foreach (DutyKind k in System.Enum.GetValues(typeof(DutyKind)).Cast<DutyKind>().Where(k => k != DutyKind.None)) Assert.NotNull(MilitaryDuties.Skill(k));
            Assert.Null(MilitaryDuties.Skill(DutyKind.None));
            Assert.Equal("Medicine", MilitaryDuties.Skill(DutyKind.FirstAid)); Assert.Equal("Social", MilitaryDuties.Skill(DutyKind.Muster)); Assert.Equal("Shooting", MilitaryDuties.Skill(DutyKind.RangePractice)); Assert.Equal("Melee", MilitaryDuties.Skill(DutyKind.Spar));
        }

        [Fact]
        public void Experience_is_bounded_and_leading_and_watching_are_worth_more_than_joining()
        {
            Assert.Equal(MilitaryDuties.LeadPoints, MilitaryDuties.Points("Vanguard", DutyKind.Drill)); Assert.Equal(MilitaryDuties.TrainPoints, MilitaryDuties.Points("Soldier", DutyKind.Drill));
            Assert.Equal(MilitaryDuties.WatchPoints, MilitaryDuties.Points("Sniper", DutyKind.Lookout)); Assert.Equal(MilitaryDuties.LeadPoints, MilitaryDuties.Points("FieldMedic", DutyKind.FirstAid)); Assert.Equal(0, MilitaryDuties.Points("Vanguard", DutyKind.None));
            Assert.True(MilitaryDuties.LeadPoints > MilitaryDuties.TrainPoints); Assert.True(MilitaryDuties.WatchPoints >= MilitaryDuties.TrainPoints);
            // the most one soldier can earn from duties in a day is well under a specialty's bar of 60 specialty experience
            Assert.True(MilitaryDuties.MaxBlocksPerDay * MilitaryDuties.LeadPoints < 60 * 2);
            // duties are the main route: a Vanguard leading every day qualifies in about a week, not a season
            int days = (int)System.Math.Ceiling(60.0 / (MilitaryDuties.MaxBlocksPerDay * MilitaryDuties.LeadPoints)); Assert.InRange(days, 1, 4);
        }

        [Fact]
        public void The_garrison_is_never_all_on_duty_at_once()
        {
            Assert.Equal(0, MilitaryDuties.MaxOnDuty(0)); Assert.Equal(1, MilitaryDuties.MaxOnDuty(1)); Assert.Equal(1, MilitaryDuties.MaxOnDuty(2)); Assert.Equal(2, MilitaryDuties.MaxOnDuty(3)); Assert.Equal(5, MilitaryDuties.MaxOnDuty(10));
            for (int n = 2; n < 40; n++) Assert.True(MilitaryDuties.MaxOnDuty(n) < n);
        }

        [Fact]
        public void A_soldier_rests_between_blocks_and_has_a_daily_limit()
        {
            Assert.True(MilitaryDuties.MayStart(1000, 0, 0)); Assert.False(MilitaryDuties.MayStart(1000 + MilitaryDuties.CooldownTicks - 1, 1000, 1)); Assert.True(MilitaryDuties.MayStart(1000 + MilitaryDuties.CooldownTicks, 1000, 1));
            Assert.False(MilitaryDuties.MayStart(999999, 1000, MilitaryDuties.MaxBlocksPerDay)); Assert.True(MilitaryDuties.MayStart(10, 5000, 1));          // a clock that went back never blocks forever
        }

        [Fact]
        public void A_cut_short_block_counts_only_if_enough_was_done()
        {
            Assert.True(MilitaryDuties.Counts(true, 0)); Assert.False(MilitaryDuties.Counts(false, 100)); Assert.True(MilitaryDuties.Counts(false, MilitaryDuties.BlockTicks / 2)); Assert.Equal(0f, MilitaryDuties.Done(-5)); Assert.Equal(1f, MilitaryDuties.Done(MilitaryDuties.BlockTicks * 3));
        }

        // ------------------------------------------------------------------ practising toward a specialty

        private static string Practise(System.Func<string, int> skill, System.Func<string, int> xp = null, System.Func<string, bool> held = null, System.Func<string, bool> holds = null, params string[] branches)
            => MilitaryDuties.Practising(MilitaryCareers.Definition(), skill, xp ?? (s => 0), held ?? (s => false), holds ?? (s => false), branches);

        [Fact]
        public void A_soldier_practises_the_specialty_their_skills_allow_with_the_most_experience_banked()
        {
            Assert.Equal("Vanguard", Practise(k => k == "Melee" ? 10 : 0, null, null, null, "Soldier"));          // Melee 10: Vanguard (Guardian needs Medicine 3, Duelist needs 12)
            Assert.Equal("Duelist", Practise(k => k == "Melee" ? 12 : 3, k => k == "Duelist" ? 20 : 0, null, null, "Soldier"));
            Assert.Equal("Vanguard", Practise(k => k == "Melee" ? 12 : 3, k => k == "Vanguard" ? 40 : k == "Duelist" ? 20 : 0, null, null, "Soldier"));
            Assert.Equal("Sniper", Practise(k => k == "Shooting" ? 12 : 0, k => k == "Sniper" ? 5 : 0, null, null, "Ranger"));
        }

        [Fact]
        public void Nothing_is_practised_without_the_skills_the_branch_or_a_free_specialty()
        {
            Assert.Null(Practise(k => 0, null, null, null, "Soldier"));
            Assert.Null(Practise(k => 20, null, null, null));          // no branch held
            Assert.Null(Practise(k => k == "Shooting" ? 20 : 0, null, null, null, "Soldier"));          // a ranger's skills do nothing for a soldier's specialties
            Assert.Null(Practise(k => 20, null, s => true, null, "Soldier", "Ranger"));          // everything already held
            Assert.Null(MilitaryDuties.Practising(MilitaryCareers.Definition(), k => 20, s => 0, s => true, s => true, null));
        }

        [Fact]
        public void The_field_medic_is_practised_only_by_someone_who_holds_the_doctor_qualification()
        {
            string Medic(bool doctor) => Practise(k => k == "Medicine" ? 9 : 0, k => k == "FieldMedic" ? 99 : 0, null, q => doctor && q == MilitaryCareers.DoctorQualification, "Ranger");
            Assert.Null(Medic(false)); Assert.Equal("FieldMedic", Medic(true));
        }

        [Fact]
        public void The_officer_needs_social_and_either_branch()
        {
            Assert.Equal("Officer", Practise(k => k == "Social" ? 8 : 0, null, null, null, "Ranger")); Assert.Equal("Officer", Practise(k => k == "Social" ? 8 : 0, null, null, null, "Soldier"));
            Assert.Null(Practise(k => k == "Social" ? 7 : 0, null, null, null, "Soldier"));
        }

        [Fact]
        public void A_recruit_builds_toward_the_branch_of_their_better_skill()
        {
            Assert.Equal("Soldier", MilitaryDuties.BranchToward(8, 3)); Assert.Equal("Ranger", MilitaryDuties.BranchToward(3, 8)); Assert.Equal("Soldier", MilitaryDuties.BranchToward(5, 5));
        }

        [Fact]
        public void A_kind_and_a_length_pack_into_one_number_and_back()
        {
            foreach (DutyKind k in System.Enum.GetValues(typeof(DutyKind))) foreach (int len in new[] { 1, 600, MilitaryDuties.BlockTicks, 99999 })
            { int p = MilitaryDuties.Pack(k, len); Assert.Equal(k, MilitaryDuties.KindOf(p)); Assert.Equal(len, MilitaryDuties.LengthOf(p)); }
            Assert.Equal(1, MilitaryDuties.LengthOf(MilitaryDuties.Pack(DutyKind.Drill, -50)));          // never zero or negative
            Assert.Equal(MilitaryDuties.BlockTicks, MilitaryDuties.LengthOf((int)DutyKind.Drill));          // a plain kind with no length means a whole block
            Assert.Equal(DutyKind.Patrol, MilitaryDuties.KindOf(MilitaryDuties.Pack(DutyKind.Patrol, int.MaxValue))); Assert.True(MilitaryDuties.LengthOf(MilitaryDuties.Pack(DutyKind.Patrol, int.MaxValue)) > 0);
        }
    }
}
