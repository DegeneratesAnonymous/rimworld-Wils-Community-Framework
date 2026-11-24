using System;

namespace FamilyLife.Domain
{
    /// <summary>
    /// How much profession work one person can carry. Every profession they already hold in another career raises what a new qualified role asks of their skills by <see cref="SkillStep"/> levels,
    /// so a person can only stack as many professions as their skill can pay for. The same rule, read the other way, gives a capacity from their best skill: a person at <see cref="BaseSkill"/> carries one,
    /// and every <see cref="SkillStep"/> levels above that carries one more. It is advice and a gate on new qualifications, never a removal: roles already held stay.
    /// </summary>
    public static class RoleLoad
    {
        public const int BaseSkill = 8;
        public static int SkillStep = 2;

        public enum Level { Open, Full, Over }

        /// <summary>Extra skill levels a new qualified role asks for from someone who already holds <paramref name="otherCareersHeld"/> other professions.</summary>
        public static int Penalty(int otherCareersHeld) => Math.Max(0, otherCareersHeld) * Math.Max(0, SkillStep);

        /// <summary>How many professions a person with this best skill can carry.</summary>
        public static int Capacity(int bestSkill) => SkillStep <= 0 ? int.MaxValue : Math.Max(1, 1 + (bestSkill - BaseSkill) / SkillStep);

        public static Level Of(int held, int capacity) => held > capacity ? Level.Over : held == capacity ? Level.Full : Level.Open;
    }
}
