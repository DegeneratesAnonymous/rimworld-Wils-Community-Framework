using System.Collections.Generic;
using System.Linq;
using FamilyLife.Framework;

namespace FamilyLife.Domain
{
    /// <summary>One thing the colony needs: who noticed it, what it is, where (if anywhere), and since when.</summary>
    public sealed class ColonyNeed
    {
        public string Key, Module, LabelKey, Detail; public int PostedTick, Map = -1, X, Z;
        public bool HasPlace => Map >= 0;
    }

    /// <summary>
    /// The colony's open needs, posted by modules ("no classroom", "no medical bed", "no training yard", "wages unpaid") and resolved by the same module when it sees the need met. The player can dismiss a need they
    /// do not want to be reminded of; it stays dismissed until it is met and arises again. A module posts and resolves only its own needs, never another's. Saved with the colony, so the list survives a load.
    /// </summary>
    public sealed class NeedBoard : IModuleState
    {
        public const string StateId = "wils.needs";
        private readonly List<ColonyNeed> open = new List<ColonyNeed>();
        private readonly HashSet<string> dismissed = new HashSet<string>();

        public IReadOnlyList<ColonyNeed> Open => open;
        public bool IsDismissed(string key) => dismissed.Contains(key);

        /// <summary>Posts or refreshes a need. Returns true only when it is new (so the player is told once). A dismissed need is not shown again until it has been resolved.</summary>
        public bool Post(string module, string key, string labelKey, string detail, int tick, int map = -1, int x = 0, int z = 0)
        {
            if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(key) || dismissed.Contains(key)) return false;
            var existing = open.FirstOrDefault(n => n.Key == key);
            if (existing != null) { existing.Detail = detail; existing.Map = map; existing.X = x; existing.Z = z; return false; }
            open.Add(new ColonyNeed { Key = key, Module = module, LabelKey = labelKey, Detail = detail, PostedTick = tick, Map = map, X = x, Z = z });
            return true;
        }

        /// <summary>The need is met. Removes it, and forgets a dismissal so that the need can be raised again if it comes back.</summary>
        public bool Resolve(string module, string key)
        {
            dismissed.Remove(key); int n = open.RemoveAll(q => q.Key == key && q.Module == module); return n > 0;
        }

        public bool Dismiss(string key) { var q = open.FirstOrDefault(n => n.Key == key); if (q == null) return false; open.Remove(q); dismissed.Add(key); return true; }

        public string Encode() => new StateWriter()
            .Strings("k", open.Select(n => n.Key).ToList()).Strings("m", open.Select(n => n.Module).ToList()).Strings("l", open.Select(n => n.LabelKey).ToList()).Strings("d", open.Select(n => n.Detail ?? "").ToList())
            .Ints("t", open.Select(n => n.PostedTick).ToList()).Ints("p", open.Select(n => n.Map).ToList()).Ints("x", open.Select(n => n.X).ToList()).Ints("z", open.Select(n => n.Z).ToList())
            .Strings("x2", dismissed.OrderBy(s => s, System.StringComparer.Ordinal).ToList()).ToString();

        public static NeedBoard Decode(StateReader r)
        {
            var b = new NeedBoard(); var k = r.Strings("k"); var m = r.Strings("m"); var l = r.Strings("l"); var d = r.Strings("d"); var t = r.Ints("t"); var p = r.Ints("p"); var x = r.Ints("x"); var z = r.Ints("z");
            for (int i = 0; i < k.Count && i < m.Count && i < l.Count; i++)
            {
                if (string.IsNullOrEmpty(k[i]) || b.open.Any(n => n.Key == k[i])) continue;
                b.open.Add(new ColonyNeed { Key = k[i], Module = m[i], LabelKey = l[i], Detail = i < d.Count ? d[i] : "", PostedTick = i < t.Count ? t[i] : 0, Map = i < p.Count ? p[i] : -1, X = i < x.Count ? x[i] : 0, Z = i < z.Count ? z[i] : 0 });
            }
            foreach (var s in r.Strings("x2")) b.dismissed.Add(s);
            return b;
        }
    }

    public sealed class NeedBoardFactory : IStateFactory<NeedBoard>
    {
        public static readonly NeedBoardFactory Instance = new NeedBoardFactory();
        public int Version => 1;
        public NeedBoard Create() => new NeedBoard();
        public NeedBoard Decode(StateReader reader, int version) => NeedBoard.Decode(reader);
        public NeedBoard FromLegacy(StateReader legacy) => Create();
    }

    public static class NeedBoardAccess
    {
        public static NeedBoard Needs(this FamilyRegistry r) => r.Shared.Of(NeedBoard.StateId, NeedBoardFactory.Instance);
    }
}
