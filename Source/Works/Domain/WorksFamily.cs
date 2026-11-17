using System;

namespace FamilyLife.Domain
{
    /// <summary>
    /// How Works touches a worker's family. Dangerous work worries kin when a worker is hurt on the job, grieves them when one is lost, and a long safe stretch is a relief; a Builder who finishes
    /// something a relative lives in leaves them with a "built this for us" memory; relatives working the same site share a memory and a small bond. All of it is memories only. Owner: Works.
    /// </summary>
    public static class WorksFamily
    {
        public const string Worried = "WFL_K_KinWorried", Lost = "WFL_K_KinLost", Relieved = "WFL_K_KinRelieved", BuiltForUs = "WFL_K_BuiltForUs", CrewMood = "WFL_K_CrewMood", CrewBond = "WFL_K_CrewBond";
        public static readonly string[] Thoughts = { Worried, Lost, Relieved, BuiltForUs, CrewMood, CrewBond };

        /// <summary>The total severity of new injuries at which kin are worried: a scratch is not, a deep wound is.</summary>
        public const float WorryingSeverity = 5f;
        /// <summary>A safe stretch is fifteen days with no harm and no earlier relief, with work done in it.</summary>
        public const int SafeStretchTicks = 15 * 60000;
        /// <summary>Another worker on the same site counts as alongside if within this many cells.</summary>
        public const float CrewDistance = 12f;

        public static bool Worrying(float newSeverity) => newSeverity >= WorryingSeverity;

        /// <summary>Kin feel relieved after a safe stretch of real work: fifteen days since the later of the last harm and the last relief (a worker is given a baseline when first seen), and work done.</summary>
        public static bool ReliefDue(int now, int lastHarm, int lastRelief, bool worked)
        {
            if (!worked) return false; int since = Math.Max(lastHarm, lastRelief);
            return since > 0 && now >= since && now - since >= SafeStretchTicks;          // with no baseline yet there is nothing to be relieved about
        }

        /// <summary>Whether a death counts as lost to the work: the worker was seen on a Works job a moment ago and no hostile was about, so it was the work and not a fight.</summary>
        public static bool LostToTheWork(bool wasWorking, bool hostilesAbout) => wasWorking && !hostilesAbout;

        /// <summary>Whether two workers are alongside each other: both on a Works job and close.</summary>
        public static bool Alongside(bool aWorking, bool bWorking, float distance) => aWorking && bWorking && distance <= CrewDistance;
    }
}
