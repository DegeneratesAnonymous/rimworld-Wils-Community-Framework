using System;

namespace FamilyLife.Domain
{
    /// <summary>What the game side read about a prisoner when deciding whether to take them to another room.</summary>
    public sealed class EscortFacts
    {
        public bool Awake = true, Downed, MentalState, Breaking, NeedsTending, Hungry, Tired, Drafted;
        public float Mood = .5f;
        /// <summary>The prisoner has been escorted today already.</summary>
        public bool DoneToday;
    }

    public enum EscortRefusal : byte { None = 0, Asleep, Downed, Mental, Breaking, Hurt, Needs, Unhappy, Done }

    /// <summary>
    /// The rules for taking a prisoner to the Interview or Visiting room. Only willing, calm prisoners: awake, upright, not in a mental state or breaking out, not in need of tending, not hungry or tired,
    /// in a decent mood, and not already taken today. The effects are deliberately small: a day's worth of conversation eases recruitment a little, it does not win anyone over. Owner: Wardens.
    /// </summary>
    public static class WardenEscorts
    {
        /// <summary>How long the prisoner stays in the room, in ticks (about 20 seconds of game time is too short to matter; 1200 is half an hour).</summary>
        public const int StayTicks = 1200;
        /// <summary>How long an escort may take altogether, walking included, before it is abandoned and the prisoner sent back.</summary>
        public const int LimitTicks = 5000;
        public const float CalmMood = .4f;
        /// <summary>How far an interview eases resistance. A vanilla recruitment chat removes about 1; this is half a chat, once a day, and never below zero.</summary>
        public const float ResistanceRelief = .5f;
        /// <summary>How far an interview eases an ideological conviction (certainty is 0 to 1).</summary>
        public const float CertaintyRelief = .02f;
        public const int MaxEscortsPerDay = 3, ReturnPoints = 6;
        public const string InterviewThought = "WFL_W_Interviewed", VisitThought = "WFL_W_Visited", VisitorThought = "WFL_W_VisitedKin";

        public static EscortRefusal Check(EscortFacts f)
        {
            if (f == null) return EscortRefusal.Done;
            if (f.DoneToday) return EscortRefusal.Done; if (!f.Awake) return EscortRefusal.Asleep; if (f.Downed) return EscortRefusal.Downed;
            if (f.MentalState || f.Drafted) return EscortRefusal.Mental; if (f.Breaking) return EscortRefusal.Breaking; if (f.NeedsTending) return EscortRefusal.Hurt;
            if (f.Hungry || f.Tired) return EscortRefusal.Needs; return f.Mood < CalmMood ? EscortRefusal.Unhappy : EscortRefusal.None;
        }

        public static bool Willing(EscortFacts f) => Check(f) == EscortRefusal.None;

        /// <summary>
        /// Where to take them: the Visiting Room if a relative is free to come and there is one, otherwise the Interview Room if they are being persuaded (recruited or converted) and there is one.
        /// None if neither applies, so nobody is taken without reason.
        /// </summary>
        public static WardenRoom Choose(bool kinFree, bool persuaded, bool hasInterview, bool hasVisiting)
        {
            if (kinFree && hasVisiting) return WardenRoom.VisitingRoom;
            return persuaded && hasInterview ? WardenRoom.InterviewRoom : WardenRoom.None;
        }

        /// <summary>The resistance an interview removes: the fixed relief, never more than is left and never negative.</summary>
        public static float Relief(float resistance) => Math.Max(0f, Math.Min(ResistanceRelief, resistance));

        public static float CertaintyDrop(float certainty) => Math.Max(0f, Math.Min(CertaintyRelief, certainty));

        /// <summary>How many escorts may be out at once: at most one for every two wardens, and at least one when there is a warden.</summary>
        public static int MaxOut(int wardens) => wardens <= 0 ? 0 : Math.Max(1, wardens / 2);

        /// <summary>Whether the escort has run out of time.</summary>
        public static bool TimedOut(int started, int now) => now >= started && now - started > LimitTicks;

        /// <summary>Whether a day's escort limit allows another: an escort needs a daily allowance for its warden.</summary>
        public static bool MayEscort(int doneToday) => doneToday < MaxEscortsPerDay;
    }
}
