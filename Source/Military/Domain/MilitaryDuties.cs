using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    /// <summary>A soldier's daily duty. Which one depends on their role and the alert level (<see cref="MilitaryDuties.For"/>).</summary>
    public enum DutyKind : byte { None = 0, Drill = 1, Spar = 2, RangePractice = 3, FirstAid = 4, Muster = 5, Patrol = 6, Lookout = 7, GateGuard = 8 }

    /// <summary>
    /// The routine of a garrison. At Normal the soldiers train (the Vanguard leads drills, the Duelist spars, the Marksman runs the range, the Field Medic stands by with first aid, the Officer
    /// holds muster); at Yellow the watch roles stand watch (the Gunner patrols, the Sniper keeps lookout, the Guardian guards the gate) while the rest carry on; at Red there is no routine, the
    /// alert sends everyone to their posts. Pure rules: where a duty happens and how a pawn is sent to it are the game side's.
    /// </summary>
    public static class MilitaryDuties
    {
        /// <summary>One block of duty, about 36 minutes of game time. A soldier does at most <see cref="MaxBlocksPerDay"/>, resting <see cref="CooldownTicks"/> between them.</summary>
        public const int BlockTicks = 1500, CooldownTicks = 4000, MaxBlocksPerDay = 3;
        /// <summary>Experience for a finished block: leading a session, taking part in one, or keeping watch.</summary>
        public const int LeadPoints = 12, TrainPoints = 8, WatchPoints = 10;
        /// <summary>Never more than this share of the serving soldiers on duty at once (at least one), so the colony is never left with nobody ready.</summary>
        public const float MaxOnDutyShare = .5f;
        /// <summary>Most people who can join one leader's session besides the leader, and most of those who may be volunteers.</summary>
        public const int MaxParticipants = 5, MaxVolunteers = 3;
        /// <summary>Skill experience for each minute of training (60 ticks); about 120 over a block, a small fraction of a day's real work.</summary>
        public const float SkillXpPerMinute = 5f;
        /// <summary>A block cut short still counts if at least this share of it was done.</summary>
        public const float PartialCredit = .5f;
        /// <summary>The youngest who may train in combat.</summary>
        public const int MinTrainingAge = 13;

        public static DutyKind For(string role, AlertLevel level)
        {
            if (level == AlertLevel.Red || string.IsNullOrEmpty(role)) return DutyKind.None;
            bool yellow = level == AlertLevel.Yellow;
            switch (role)
            {
                case "Vanguard": return DutyKind.Drill;
                case "Duelist": return DutyKind.Spar;
                case "Marksman": return DutyKind.RangePractice;
                case "FieldMedic": return DutyKind.FirstAid;
                case "Officer": return DutyKind.Muster;
                case "Guardian": return yellow ? DutyKind.GateGuard : DutyKind.Drill;
                case "Gunner": return yellow ? DutyKind.Patrol : DutyKind.RangePractice;
                case "Sniper": return yellow ? DutyKind.Lookout : DutyKind.RangePractice;
                case "Ranger": return yellow ? DutyKind.Patrol : DutyKind.RangePractice;
                case "Soldier": case "Recruit": return yellow ? DutyKind.Patrol : DutyKind.Drill;
                default: return DutyKind.None;
            }
        }

        /// <summary>Whether the role runs this duty for others to join (the rest take part or work alone).</summary>
        public static bool Leads(string role, DutyKind kind) => (role == "Vanguard" && kind == DutyKind.Drill) || (role == "Marksman" && kind == DutyKind.RangePractice) || (role == "Officer" && kind == DutyKind.Muster) || (role == "Duelist" && kind == DutyKind.Spar);

        /// <summary>Duties others can join, because they happen in one place at one time.</summary>
        public static bool IsGroup(DutyKind kind) => kind == DutyKind.Drill || kind == DutyKind.RangePractice || kind == DutyKind.Muster;
        public static bool IsWatch(DutyKind kind) => kind == DutyKind.Patrol || kind == DutyKind.Lookout || kind == DutyKind.GateGuard;
        /// <summary>The duties that happen in the Training Yard.</summary>
        public static bool UsesYard(DutyKind kind) => kind == DutyKind.Drill || kind == DutyKind.Spar || kind == DutyKind.RangePractice || kind == DutyKind.FirstAid;

        /// <summary>The post a duty needs, if any (Muster falls back to the yard when there is no Rally point).</summary>
        public static PostKind? PostFor(DutyKind kind) => kind == DutyKind.Lookout ? PostKind.Overwatch : kind == DutyKind.GateGuard ? PostKind.Guard : kind == DutyKind.Muster ? (PostKind?)PostKind.Rally : null;

        /// <summary>The vanilla skill the duty exercises.</summary>
        public static string Skill(DutyKind kind)
        {
            switch (kind)
            {
                case DutyKind.Drill: case DutyKind.Spar: case DutyKind.GateGuard: case DutyKind.Patrol: return "Melee";
                case DutyKind.RangePractice: case DutyKind.Lookout: return "Shooting";
                case DutyKind.FirstAid: return "Medicine";
                case DutyKind.Muster: return "Social";
                default: return null;
            }
        }

        public static int Points(string role, DutyKind kind) => kind == DutyKind.None ? 0 : IsWatch(kind) ? WatchPoints : Leads(role, kind) || role == "FieldMedic" ? LeadPoints : TrainPoints;

        /// <summary>How many serving soldiers may be on duty at once.</summary>
        public static int MaxOnDuty(int serving) => serving <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(serving * MaxOnDutyShare));

        /// <summary>Whether a soldier may start another block: rested, and under the daily limit.</summary>
        public static bool MayStart(int now, int lastEnd, int blocksToday) => blocksToday < MaxBlocksPerDay && (lastEnd <= 0 || now - lastEnd >= CooldownTicks || now < lastEnd);

        /// <summary>The share of a block that was done, 0 to 1.</summary>
        public static float Done(int ticks) => Math.Max(0f, Math.Min(1f, ticks / (float)BlockTicks));
        public static bool Counts(bool completed, int ticks) => completed || Done(ticks) >= PartialCredit;

        // The job carries its kind in the low byte of one number and how long to hold in the rest, so someone who joins a session late holds only for what is left of it.
        public static int Pack(DutyKind kind, int length) => (int)kind | (Math.Max(1, Math.Min(length, 1 << 20)) << 8);
        public static DutyKind KindOf(int packed) => (DutyKind)(packed & 0xFF);
        public static int LengthOf(int packed) { int length = packed >> 8; return length > 0 ? length : BlockTicks; }

        // ------------------------------------------------------------------ practising a specialty before qualifying

        /// <summary>
        /// A specialty is assigned only once qualified, so someone who has only earned a branch must practise before then. They practise the specialty nearest to being earned: of those whose
        /// skills they already meet and whose branch (and other-career requirement) they hold, the one with the most experience banked, ties by name. Null when there is nothing left to
        /// practise, and they simply do their branch's routine.
        /// </summary>
        public static string Practising(CareerDefinition def, Func<string, int> skill, Func<string, int> specialtyXp, Func<string, bool> heldSpecialty, Func<string, bool> holdsQualification, IEnumerable<string> branchesHeld)
        {
            var branches = (branchesHeld ?? Enumerable.Empty<string>()).ToList();
            return def.Specialties
                .Where(s => !heldSpecialty(s.Key)
                    && (s.Value.RequiresAny.Count == 0 || s.Value.RequiresAny.Any(branches.Contains))
                    && s.Value.RequiresAll.All(holdsQualification)
                    && CareerLedger.Meets(s.Value, skill))
                .OrderByDescending(s => specialtyXp(s.Key)).ThenBy(s => s.Key, StringComparer.Ordinal)
                .Select(s => s.Key).FirstOrDefault();
        }

        /// <summary>The branch a Recruit builds toward: the one whose skill is higher (a tie goes to Soldier).</summary>
        public static string BranchToward(int melee, int shooting) => melee >= shooting ? "Soldier" : "Ranger";
    }
}
