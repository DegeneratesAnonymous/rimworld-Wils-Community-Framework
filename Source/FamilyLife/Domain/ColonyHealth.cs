using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    public enum HealthLevel : byte { Good = 0, Warn = 1, Bad = 2 }

    /// <summary>One finding about how a part of the community is doing: what it is, how serious, and what would fix it.</summary>
    public sealed class HealthNote
    {
        public string Module, Text, Hint; public HealthLevel Level;
        public static HealthNote Good(string module, string text) => new HealthNote { Module = module, Text = text, Level = HealthLevel.Good };
        public static HealthNote Warn(string module, string text, string hint = null) => new HealthNote { Module = module, Text = text, Hint = hint, Level = HealthLevel.Warn };
        public static HealthNote Bad(string module, string text, string hint = null) => new HealthNote { Module = module, Text = text, Hint = hint, Level = HealthLevel.Bad };
    }

    public enum StaffReason : byte { Fine = 0, Unstaffed = 1, Idle = 2 }

    public static class HealthRules
    {
        public static HealthLevel Worst(IEnumerable<HealthNote> notes) => notes == null || !notes.Any() ? HealthLevel.Good : notes.Max(n => n.Level);

        /// <summary>Worst first, then by module, so the page opens on what needs attention.</summary>
        public static List<HealthNote> Order(IEnumerable<HealthNote> notes) => (notes ?? Enumerable.Empty<HealthNote>()).OrderByDescending(n => n.Level).ThenBy(n => n.Module, System.StringComparer.Ordinal).ToList();

        /// <summary>
        /// Whether a module's professions are doing anything: nobody holds a role (Unstaffed), or people do but nobody has done any work lately (Idle, only once the game has run long enough to have seen any;
        /// recent work is not saved, so just after a load every module would look idle).
        /// </summary>
        public static StaffReason Staffing(int holders, int learning, bool sessionLongEnough)
        {
            if (holders <= 0) return StaffReason.Unstaffed;
            if (learning <= 0 && sessionLongEnough) return StaffReason.Idle;
            return StaffReason.Fine;
        }

        /// <summary>A need that has stood unmet for a few days is serious.</summary>
        public static HealthLevel NeedLevel(int ageTicks, int seriousAfterTicks = 300000) => ageTicks >= seriousAfterTicks ? HealthLevel.Bad : HealthLevel.Warn;
    }
}
