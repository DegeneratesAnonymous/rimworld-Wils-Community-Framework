using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FamilyLife.Framework;

namespace FamilyLife.Domain
{
    /// <summary>
    /// Which of the game's work types a profession is about (care: doctoring and patient care; works: construction and mining). A module names the work types of its own careers by career id and work-type name only,
    /// so no module needs another. Nothing here changes a priority by itself: <see cref="WorkFocusPlan"/> only works out what to suggest.
    /// </summary>
    public sealed class WorkFocusRule { public string Career; public string[] WorkTypes; }

    /// <summary>One priority this feature set for a colonist: what it was before and what it was set to, so it can be undone and so a later hand change is noticed.</summary>
    public sealed class WorkChange { public string WorkType; public int Before, Applied; }

    public static class WorkFocusPlan
    {
        public const int MainPriority = 1, SidePriority = 2;

        /// <summary>The priority wanted for each work type: the main profession's work first, then the side professions' work. A work type named twice keeps the better (lower) priority.</summary>
        public static Dictionary<string, int> Targets(IEnumerable<WorkFocusRule> rules, string mainCareer, IEnumerable<string> sideCareers)
        {
            var wanted = new Dictionary<string, int>(StringComparer.Ordinal);
            void Add(string career, int priority)
            {
                if (string.IsNullOrEmpty(career) || rules == null) return;
                foreach (var rule in rules)
                {
                    if (rule == null || rule.Career != career || rule.WorkTypes == null) continue;
                    foreach (var wt in rule.WorkTypes) if (!string.IsNullOrEmpty(wt) && (!wanted.TryGetValue(wt, out var have) || priority < have)) wanted[wt] = priority;
                }
            }
            Add(mainCareer, MainPriority);
            if (sideCareers != null) foreach (var c in sideCareers) Add(c, SidePriority);
            return wanted;
        }

        /// <summary>
        /// What to change. A work type is left alone when it already has the wanted priority, when the colonist cannot do it (<paramref name="canDo"/>), or when the player has changed it by hand since this feature last set it
        /// (its current priority is neither what was there before nor what was set). A priority of 0 means the work type is off; it is only turned on, never off.
        /// </summary>
        public static List<WorkChange> Plan(IDictionary<string, int> wanted, Func<string, int> current, Func<string, bool> canDo, IEnumerable<WorkChange> earlier)
        {
            var result = new List<WorkChange>(); var before = earlier == null ? new Dictionary<string, WorkChange>() : earlier.GroupBy(e => e.WorkType).ToDictionary(g => g.Key, g => g.First());
            foreach (var kv in wanted.OrderBy(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal))
            {
                if (canDo != null && !canDo(kv.Key)) continue;
                int now = current(kv.Key); if (now == kv.Value) continue;
                if (before.TryGetValue(kv.Key, out var e) && now != e.Applied) continue;          // the player has changed it since: that wins
                int original = before.TryGetValue(kv.Key, out var e2) ? e2.Before : now;
                result.Add(new WorkChange { WorkType = kv.Key, Before = original, Applied = kv.Value });
            }
            return result;
        }

        /// <summary>What undoing restores: only work types still at the priority this feature set. One the player has changed since is theirs and stays.</summary>
        public static List<WorkChange> Undo(IEnumerable<WorkChange> earlier, Func<string, int> current)
            => earlier == null ? new List<WorkChange>() : earlier.Where(e => current(e.WorkType) == e.Applied).ToList();
    }

    /// <summary>Which colonists follow their profession's work, and what was changed for each (so it can be undone). Saved as a community-wide payload.</summary>
    public sealed class WorkFocusBook : IModuleState
    {
        public const string StateId = "wils.workfocus";
        public const int MaxPeople = 400;
        private readonly HashSet<int> following = new HashSet<int>();
        private readonly Dictionary<int, List<WorkChange>> changes = new Dictionary<int, List<WorkChange>>();
        private readonly Dictionary<int, string> signature = new Dictionary<int, string>();

        public bool IsFollowing(int pawn) => following.Contains(pawn);
        public IEnumerable<int> Following => following.OrderBy(i => i);
        public void Follow(int pawn, bool on) { if (pawn <= 0) return; if (on) { if (following.Count < MaxPeople) following.Add(pawn); } else { following.Remove(pawn); signature.Remove(pawn); } }
        public IReadOnlyList<WorkChange> ChangesFor(int pawn) => changes.TryGetValue(pawn, out var l) ? (IReadOnlyList<WorkChange>)l : Array.Empty<WorkChange>();
        public bool HasChanges(int pawn) => changes.TryGetValue(pawn, out var l) && l.Count > 0;

        /// <summary>Remembers what was set, merging with what was set before (the first "before" for a work type is kept: it is what undo goes back to).</summary>
        public void Record(int pawn, IEnumerable<WorkChange> set)
        {
            if (pawn <= 0 || set == null) return;
            if (!changes.TryGetValue(pawn, out var list)) changes[pawn] = list = new List<WorkChange>();
            foreach (var c in set) { int at = list.FindIndex(x => x.WorkType == c.WorkType); if (at >= 0) list[at] = new WorkChange { WorkType = c.WorkType, Before = list[at].Before, Applied = c.Applied }; else list.Add(c); }
        }

        public void Forget(int pawn, IEnumerable<WorkChange> undone) { if (!changes.TryGetValue(pawn, out var list) || undone == null) return; foreach (var u in undone) list.RemoveAll(x => x.WorkType == u.WorkType); if (list.Count == 0) changes.Remove(pawn); }
        public void ForgetPerson(int pawn) { following.Remove(pawn); changes.Remove(pawn); signature.Remove(pawn); }

        /// <summary>The professions a followed colonist had when their work was last set; a different one means it is time to suggest again. Not saved.</summary>
        public string SignatureOf(int pawn) => signature.TryGetValue(pawn, out var s) ? s : null;
        public void SetSignature(int pawn, string s) { signature[pawn] = s; }

        public string Encode()
        {
            var w = new StateWriter().Ints("follow", following.OrderBy(i => i).ToList());
            w.Strings("chg", changes.OrderBy(k => k.Key).SelectMany(k => k.Value.Select(c => k.Key + "|" + c.WorkType.Replace('|', '/') + "|" + c.Before + "|" + c.Applied)).ToList());
            return w.ToString();
        }

        public static WorkFocusBook Decode(StateReader r)
        {
            var b = new WorkFocusBook(); foreach (int id in r.Ints("follow")) if (b.following.Count < MaxPeople) b.following.Add(id);
            foreach (var s in r.Strings("chg"))
            {
                var p = s.Split('|'); if (p.Length != 4) continue; int pawn = I(p[0]); if (pawn <= 0 || p[1].Length == 0) continue;
                if (!b.changes.TryGetValue(pawn, out var list)) b.changes[pawn] = list = new List<WorkChange>();
                list.Add(new WorkChange { WorkType = p[1], Before = I(p[2]), Applied = I(p[3]) });
            }
            return b;
        }
        private static int I(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    public sealed class WorkFocusFactory : IStateFactory<WorkFocusBook>
    {
        public static readonly WorkFocusFactory Instance = new WorkFocusFactory();
        public int Version => 1;
        public WorkFocusBook Create() => new WorkFocusBook();
        public WorkFocusBook Decode(StateReader reader, int version) => WorkFocusBook.Decode(reader);
        public WorkFocusBook FromLegacy(StateReader legacy) => new WorkFocusBook();
    }

    public static class WorkFocusAccess { public static WorkFocusBook WorkFocus(this FamilyRegistry r) => r.Shared.Of(WorkFocusBook.StateId, WorkFocusFactory.Instance); }
}
