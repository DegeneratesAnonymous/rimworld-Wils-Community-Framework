using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Framework;

namespace FamilyLife.Domain
{
    /// <summary>
    /// The colony's military alert. Normal is routine and training; Yellow is a heightened watch (patrols, lookout, guard); Red is defensive positions. The player sets a level, and a threat on
    /// the map raises Red by itself and lets it drop back to the set level once it has been calm for a while. Pure: the game side only tells it whether hostiles are about and what time it is.
    /// </summary>
    public enum AlertLevel : byte { Normal = 0, Yellow = 1, Red = 2 }

    public enum AlertChange { None, Raised, Lowered, Set }

    public sealed class AlertState : IModuleState
    {
        public const string StateId = "wils.military.alert";
        public string Id => StateId;

        /// <summary>What the player has chosen.</summary>
        public AlertLevel Set = AlertLevel.Normal;
        /// <summary>Raise Red automatically when hostiles appear. On by default: assigning a Military role is the player's consent.</summary>
        public bool AutoRed = true;
        /// <summary>Red is in force only because of a threat (so it ends by itself); a Red the player set stays until they change it.</summary>
        public bool RaisedByThreat;
        public int LastThreat, Since;
        /// <summary>People this system drafted for the current Red, so only they are stood down. And people already given their orders this episode, so nobody is dragged back after the player moves them.</summary>
        public readonly List<int> Drafted = new List<int>(), Ordered = new List<int>();

        public AlertLevel Effective => RaisedByThreat ? AlertLevel.Red : Set;

        public string Encode() => new StateWriter().Int("set", (int)Set).Bool("noauto", !AutoRed).Bool("threat", RaisedByThreat).Int("last", LastThreat).Int("since", Since).Ints("drafted", Drafted).Ints("ordered", Ordered).ToString();

        public static AlertState Decode(StateReader r)
        {
            var s = new AlertState();
            int set = r.Int("set"); s.Set = set >= 0 && set <= 2 ? (AlertLevel)set : AlertLevel.Normal;
            s.AutoRed = !r.Bool("noauto");
            s.RaisedByThreat = r.Bool("threat"); s.LastThreat = r.Int("last"); s.Since = r.Int("since");
            s.Drafted.AddRange(r.Ints("drafted").Distinct()); s.Ordered.AddRange(r.Ints("ordered").Distinct());
            return s;
        }
    }

    public sealed class AlertStateFactory : IStateFactory<AlertState>
    {
        public static readonly AlertStateFactory Instance = new AlertStateFactory();
        public int Version => 1;
        public AlertState Create() => new AlertState();
        public AlertState Decode(StateReader reader, int version) => AlertState.Decode(reader);
        public AlertState FromLegacy(StateReader legacy) => Create();
    }

    public static class AlertRules
    {
        /// <summary>How long the map must be free of hostiles before a threat-raised Red stands down (about an hour of game time).</summary>
        public const int CalmTicks = 2500;

        /// <summary>The player chooses a level. Choosing leaves a Red raised by a threat to end on its own; choosing Red keeps it until it is changed.</summary>
        public static AlertChange Choose(AlertState s, AlertLevel level, int now)
        {
            var before = s.Effective; s.Set = level; if (s.Set == AlertLevel.Red) s.RaisedByThreat = false; s.Since = now;
            return before == s.Effective ? AlertChange.None : AlertChange.Set;
        }

        /// <summary>
        /// One look at the map. A threat raises Red (when automatic Red is on) and notes when it was last seen; Red raised by a threat stands down once the map has been calm for
        /// <see cref="CalmTicks"/>. A clock that has gone back (a loaded game) never leaves an old threat time in the future.
        /// </summary>
        public static AlertChange Step(AlertState s, bool threat, int now)
        {
            var before = s.Effective;
            if (s.LastThreat > now) s.LastThreat = now;
            if (threat)
            {
                s.LastThreat = now;
                if (s.AutoRed && s.Set != AlertLevel.Red) { s.RaisedByThreat = true; }
            }
            else if (s.RaisedByThreat && now - s.LastThreat >= CalmTicks) s.RaisedByThreat = false;
            var after = s.Effective;
            if (after == before) return AlertChange.None;
            s.Since = now;
            return after == AlertLevel.Red ? AlertChange.Raised : AlertChange.Lowered;
        }

        public static string Label(AlertLevel level) => level == AlertLevel.Red ? "Red" : level == AlertLevel.Yellow ? "Yellow" : "Normal";
    }

    // ------------------------------------------------------------------ posts

    /// <summary>Where a soldier stands when the alert is Red. The player places them.</summary>
    public enum PostKind : byte { Overwatch = 0, Guard = 1, Rally = 2 }

    public sealed class MilitaryPost
    {
        public int Id, Map, X, Z;
        public PostKind Kind;
        public double DistanceTo(int x, int z) => Math.Sqrt((double)(X - x) * (X - x) + (double)(Z - z) * (Z - z));
    }

    public sealed class PostBook : IModuleState
    {
        public const string StateId = "wils.military.posts";
        public const int MaxPosts = 60;
        public string Id => StateId;
        private readonly List<MilitaryPost> posts = new List<MilitaryPost>();
        private int next = 1;
        public IReadOnlyList<MilitaryPost> All => posts;

        /// <summary>Places a post, or changes the kind of one already on that cell. Returns null when the book is full.</summary>
        public MilitaryPost Place(PostKind kind, int map, int x, int z)
        {
            var here = At(map, x, z); if (here != null) { here.Kind = kind; return here; }
            if (posts.Count >= MaxPosts) return null;
            var p = new MilitaryPost { Id = next++, Map = map, X = x, Z = z, Kind = kind }; posts.Add(p); return p;
        }
        public MilitaryPost At(int map, int x, int z) => posts.FirstOrDefault(p => p.Map == map && p.X == x && p.Z == z);
        public bool Remove(int id) => posts.RemoveAll(p => p.Id == id) > 0;
        public bool RemoveAt(int map, int x, int z) => posts.RemoveAll(p => p.Map == map && p.X == x && p.Z == z) > 0;
        public int Clear(int map) => posts.RemoveAll(p => p.Map == map);
        public int Count(PostKind kind) => posts.Count(p => p.Kind == kind);

        public string Encode()
        {
            var w = new StateWriter().Int("next", next);
            return w.Ints("id", posts.Select(p => p.Id).ToList()).Ints("map", posts.Select(p => p.Map).ToList()).Ints("x", posts.Select(p => p.X).ToList()).Ints("z", posts.Select(p => p.Z).ToList()).Ints("kind", posts.Select(p => (int)p.Kind).ToList()).ToString();
        }

        public static PostBook Decode(StateReader r)
        {
            var b = new PostBook(); var id = r.Ints("id"); var map = r.Ints("map"); var x = r.Ints("x"); var z = r.Ints("z"); var kind = r.Ints("kind");
            int n = new[] { id.Count, map.Count, x.Count, z.Count, kind.Count }.Min();          // a damaged record keeps what is whole
            for (int i = 0; i < n && b.posts.Count < MaxPosts; i++)
                if (kind[i] >= 0 && kind[i] <= 2 && b.At(map[i], x[i], z[i]) == null && b.posts.All(p => p.Id != id[i])) b.posts.Add(new MilitaryPost { Id = id[i], Map = map[i], X = x[i], Z = z[i], Kind = (PostKind)kind[i] });
            b.next = Math.Max(r.Int("next", 1), b.posts.Count == 0 ? 1 : b.posts.Max(p => p.Id) + 1);
            return b;
        }
    }

    public sealed class PostBookFactory : IStateFactory<PostBook>
    {
        public static readonly PostBookFactory Instance = new PostBookFactory();
        public int Version => 1;
        public PostBook Create() => new PostBook();
        public PostBook Decode(StateReader reader, int version) => PostBook.Decode(reader);
        public PostBook FromLegacy(StateReader legacy) => Create();
    }

    public sealed class RedSoldier { public int Id, X, Z; public string Specialty; }

    public static class PostPlanner
    {
        /// <summary>The kinds of post a role would rather stand at, best first. Rally is everyone's fallback. The Officer, Field Medic and Duelist go to the Rally point: the line's centre.</summary>
        public static PostKind[] Preference(string specialty)
        {
            switch (specialty)
            {
                case "Sniper": case "Marksman": return new[] { PostKind.Overwatch, PostKind.Rally };
                case "Guardian": case "Vanguard": case "Gunner": return new[] { PostKind.Guard, PostKind.Rally };
                default: return new[] { PostKind.Rally };
            }
        }

        /// <summary>
        /// Gives each soldier a post: the nearest free one of the kind they prefer, else the nearest Rally point (which any number can share). An Overwatch or Guard post holds one soldier.
        /// With no suitable post a soldier gets none, and is simply left where they are. Deterministic: soldiers are taken in id order.
        /// </summary>
        public static Dictionary<int, MilitaryPost> Assign(IEnumerable<RedSoldier> soldiers, IEnumerable<MilitaryPost> posts)
        {
            var result = new Dictionary<int, MilitaryPost>(); var taken = new HashSet<int>(); var all = posts.ToList();
            foreach (var s in soldiers.OrderBy(x => x.Id))
            {
                MilitaryPost pick = null;
                foreach (var kind in Preference(s.Specialty))
                {
                    var candidates = all.Where(p => p.Kind == kind && (kind == PostKind.Rally || !taken.Contains(p.Id)));
                    pick = candidates.OrderBy(p => p.DistanceTo(s.X, s.Z)).ThenBy(p => p.Id).FirstOrDefault();
                    if (pick != null) break;
                }
                if (pick == null) continue;
                result[s.Id] = pick; if (pick.Kind != PostKind.Rally) taken.Add(pick.Id);
            }
            return result;
        }
    }

    public static class MilitaryStateAccess
    {
        public static AlertState Alert(this FamilyRegistry r) => r.Shared.Of(AlertState.StateId, AlertStateFactory.Instance);
        public static PostBook Posts(this FamilyRegistry r) => r.Shared.Of(PostBook.StateId, PostBookFactory.Instance);
    }
}
