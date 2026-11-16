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
        /// <summary>Daily duties are on, and colonists who are not soldiers may volunteer to join the training. Both default to on (assigning a Military role is the consent); the player can turn either off.</summary>
        public bool DutiesOn = true, Volunteers = true;
        /// <summary>Red is in force only because of a threat (so it ends by itself); a Red the player set stays until they change it.</summary>
        public bool RaisedByThreat;
        public int LastThreat, Since;
        /// <summary>People this system drafted for the current Red, so only they are stood down. And people already given their orders this episode, so nobody is dragged back after the player moves them.</summary>
        public readonly List<int> Drafted = new List<int>(), Ordered = new List<int>();

        public AlertLevel Effective => RaisedByThreat ? AlertLevel.Red : Set;

        public string Encode() => new StateWriter().Int("set", (int)Set).Bool("noauto", !AutoRed).Bool("noduty", !DutiesOn).Bool("novol", !Volunteers).Bool("threat", RaisedByThreat).Int("last", LastThreat).Int("since", Since).Ints("drafted", Drafted).Ints("ordered", Ordered).ToString();

        public static AlertState Decode(StateReader r)
        {
            var s = new AlertState();
            int set = r.Int("set"); s.Set = set >= 0 && set <= 2 ? (AlertLevel)set : AlertLevel.Normal;
            s.AutoRed = !r.Bool("noauto"); s.DutiesOn = !r.Bool("noduty"); s.Volunteers = !r.Bool("novol");
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

        /// <summary>The level a click on the alert button sets next: Normal, Yellow, Red, and back to Normal.</summary>
        public static AlertLevel Next(AlertLevel set) => set == AlertLevel.Normal ? AlertLevel.Yellow : set == AlertLevel.Yellow ? AlertLevel.Red : AlertLevel.Normal;

        public static string Label(AlertLevel level) => level == AlertLevel.Red ? "Red" : level == AlertLevel.Yellow ? "Yellow" : "Normal";
    }

    // ------------------------------------------------------------------ posts

    /// <summary>
    /// Where a soldier stands when the alert is Red: a defensive position typed for melee, for ranged or for either (Mixed), or the Rally point. The player places them. Ranged and Melee keep the
    /// saved numbers 0 and 1 of the Overwatch and Guard posts they replaced, so nothing needs migrating. The same three types also describe a soldier and a patrol spot.
    /// </summary>
    public enum PostKind : byte { Ranged = 0, Melee = 1, Rally = 2, Mixed = 3 }

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
                if (kind[i] >= 0 && kind[i] <= 3 && b.At(map[i], x[i], z[i]) == null && b.posts.All(p => p.Id != id[i])) b.posts.Add(new MilitaryPost { Id = id[i], Map = map[i], X = x[i], Z = z[i], Kind = (PostKind)kind[i] });
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

    public sealed class RedSoldier
    {
        public int Id, X, Z;
        public string Specialty;
        /// <summary>The id of a position the player has posted this soldier to, or 0 for automatic.</summary>
        public int Posted;
    }

    public static class PostPlanner
    {
        /// <summary>A soldier's type: Melee (the Soldier branch), Ranged (the Ranger branch) or Mixed (Support, Recruits and anyone without a branch).</summary>
        public static PostKind TypeOf(string role)
        {
            switch (role)
            {
                case "Vanguard": case "Guardian": case "Duelist": case "Soldier": return PostKind.Melee;
                case "Marksman": case "Gunner": case "Sniper": case "Ranger": return PostKind.Ranged;
                default: return PostKind.Mixed;
            }
        }

        /// <summary>
        /// The kinds of position a soldier would rather stand at, best first: their own type, then Mixed, then the Rally point. A Mixed soldier (a Field Medic, an Officer, a Recruit) prefers
        /// Mixed, then Rally, and takes a typed position only when there is nothing else.
        /// </summary>
        public static PostKind[] Preference(string role)
        {
            switch (TypeOf(role))
            {
                case PostKind.Melee: return new[] { PostKind.Melee, PostKind.Mixed, PostKind.Rally };
                case PostKind.Ranged: return new[] { PostKind.Ranged, PostKind.Mixed, PostKind.Rally };
                default: return new[] { PostKind.Mixed, PostKind.Rally, PostKind.Melee, PostKind.Ranged };
            }
        }

        /// <summary>
        /// Gives each soldier a post. A soldier the player has posted to a position goes there (if it still exists and nobody posted earlier holds it). The rest take the nearest free one of the
        /// kinds they prefer, in order. A typed position holds one soldier; the Rally point is shared. With no suitable post a soldier gets none and is left where they are. Deterministic:
        /// soldiers are taken in id order, those posted first.
        /// </summary>
        public static Dictionary<int, MilitaryPost> Assign(IEnumerable<RedSoldier> soldiers, IEnumerable<MilitaryPost> posts)
        {
            var result = new Dictionary<int, MilitaryPost>(); var taken = new HashSet<int>(); var all = posts.ToList(); var ordered = soldiers.OrderBy(x => x.Id).ToList();
            foreach (var s in ordered.Where(x => x.Posted > 0))
            {
                var post = all.FirstOrDefault(p => p.Id == s.Posted);
                if (post == null || (post.Kind != PostKind.Rally && taken.Contains(post.Id))) continue;
                result[s.Id] = post; if (post.Kind != PostKind.Rally) taken.Add(post.Id);
            }
            foreach (var s in ordered)
            {
                if (result.ContainsKey(s.Id)) continue;
                MilitaryPost pick = null;
                foreach (var kind in Preference(s.Specialty))
                {
                    pick = all.Where(p => p.Kind == kind && (kind == PostKind.Rally || !taken.Contains(p.Id))).OrderBy(p => p.DistanceTo(s.X, s.Z)).ThenBy(p => p.Id).FirstOrDefault();
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
        public static PatrolBook Patrols(this FamilyRegistry r) => r.Shared.Of(PatrolBook.StateId, PatrolBookFactory.Instance);
        public static PostingBook Postings(this FamilyRegistry r) => r.Shared.Of(PostingBook.StateId, PostingBookFactory.Instance);
    }
}
