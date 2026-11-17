using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Framework;

namespace FamilyLife.Domain
{
    /// <summary>One patrol spot: a cell a patrol walks to, typed Melee, Ranged or Mixed, and belonging to one route.</summary>
    public sealed class PatrolSpot
    {
        public int Id, Map, X, Z, Route;
        /// <summary>Melee, Ranged or Mixed (never Rally).</summary>
        public PostKind Type;
        public double DistanceTo(int x, int z) => Math.Sqrt((double)(X - x) * (X - x) + (double)(Z - z) * (Z - z));
    }

    /// <summary>A named patrol route. The spots in it are ordered automatically; the cursor remembers how far patrols have got so a long route is covered over several patrols.</summary>
    public sealed class PatrolRoute
    {
        public int Id, Cursor, LastWalked;
        public string Name;
    }

    /// <summary>The colony's patrol routes and spots (saved as <c>wils.military.patrols</c>). A route may hold any number of spots; spots are bounded overall, and so are routes.</summary>
    public sealed class PatrolBook : IModuleState
    {
        public const string StateId = "wils.military.patrols";
        public const int MaxRoutes = 12, MaxSpots = 300;
        public string Id => StateId;
        private readonly List<PatrolRoute> routes = new List<PatrolRoute>();
        private readonly List<PatrolSpot> spots = new List<PatrolSpot>();
        private int nextRoute = 1, nextSpot = 1;
        public IReadOnlyList<PatrolRoute> Routes => routes;
        public IReadOnlyList<PatrolSpot> Spots => spots;

        public PatrolRoute Route(int id) => routes.FirstOrDefault(r => r.Id == id);
        public IEnumerable<PatrolSpot> InRoute(int route) => spots.Where(s => s.Route == route);

        /// <summary>A new route, named "Route N" (the lowest number not in use) unless a name is given. Null when there are already <see cref="MaxRoutes"/>.</summary>
        public PatrolRoute AddRoute(string name = null)
        {
            if (routes.Count >= MaxRoutes) return null;
            if (string.IsNullOrWhiteSpace(name)) { int n = 1; while (routes.Any(r => r.Name == "Route " + n)) n++; name = "Route " + n; }
            var route = new PatrolRoute { Id = nextRoute++, Name = name.Trim() }; routes.Add(route); return route;
        }

        /// <summary>Removes a route and every spot in it.</summary>
        public bool RemoveRoute(int id) { bool had = routes.RemoveAll(r => r.Id == id) > 0; spots.RemoveAll(s => s.Route == id); return had; }

        /// <summary>Places a spot in a route, or changes the route and type of one already on that cell. Null when the route does not exist, the type is not Melee, Ranged or Mixed, or the book is full.</summary>
        public PatrolSpot Place(int route, PostKind type, int map, int x, int z)
        {
            if (Route(route) == null || type == PostKind.Rally) return null;
            var here = spots.FirstOrDefault(s => s.Map == map && s.X == x && s.Z == z);
            if (here != null) { here.Route = route; here.Type = type; return here; }
            if (spots.Count >= MaxSpots) return null;
            var spot = new PatrolSpot { Id = nextSpot++, Map = map, X = x, Z = z, Route = route, Type = type }; spots.Add(spot); return spot;
        }

        public bool Move(int spot, int route) { var s = spots.FirstOrDefault(x => x.Id == spot); if (s == null || Route(route) == null) return false; s.Route = route; return true; }
        public bool Remove(int spot) => spots.RemoveAll(s => s.Id == spot) > 0;
        public bool RemoveAt(int map, int x, int z) => spots.RemoveAll(s => s.Map == map && s.X == x && s.Z == z) > 0;
        public PatrolSpot At(int map, int x, int z) => spots.FirstOrDefault(s => s.Map == map && s.X == x && s.Z == z);

        /// <summary>Drops spots whose route is gone. Returns how many.</summary>
        public int Reconcile() { var ids = new HashSet<int>(routes.Select(r => r.Id)); return spots.RemoveAll(s => !ids.Contains(s.Route)); }

        public string Encode()
        {
            var w = new StateWriter().Int("nextr", nextRoute).Int("nexts", nextSpot);
            return w.Ints("rid", routes.Select(r => r.Id).ToList()).Strings("rname", routes.Select(r => r.Name).ToList()).Ints("rcur", routes.Select(r => r.Cursor).ToList()).Ints("rlast", routes.Select(r => r.LastWalked).ToList())
                .Ints("sid", spots.Select(s => s.Id).ToList()).Ints("smap", spots.Select(s => s.Map).ToList()).Ints("sx", spots.Select(s => s.X).ToList()).Ints("sz", spots.Select(s => s.Z).ToList()).Ints("sroute", spots.Select(s => s.Route).ToList()).Ints("stype", spots.Select(s => (int)s.Type).ToList()).ToString();
        }

        public static PatrolBook Decode(StateReader r)
        {
            var b = new PatrolBook(); var rid = r.Ints("rid"); var rname = r.Strings("rname"); var rcur = r.Ints("rcur"); var rlast = r.Ints("rlast");
            for (int i = 0; i < rid.Count && i < rname.Count && b.routes.Count < MaxRoutes; i++)
                if (b.routes.All(x => x.Id != rid[i])) b.routes.Add(new PatrolRoute { Id = rid[i], Name = rname[i], Cursor = i < rcur.Count ? Math.Max(0, rcur[i]) : 0, LastWalked = i < rlast.Count ? rlast[i] : 0 });
            var sid = r.Ints("sid"); var smap = r.Ints("smap"); var sx = r.Ints("sx"); var sz = r.Ints("sz"); var sroute = r.Ints("sroute"); var stype = r.Ints("stype");
            int n = new[] { sid.Count, smap.Count, sx.Count, sz.Count, sroute.Count, stype.Count }.Min();          // a damaged record keeps what is whole
            for (int i = 0; i < n && b.spots.Count < MaxSpots; i++)
                if (stype[i] >= 0 && stype[i] <= 3 && stype[i] != (int)PostKind.Rally && b.Route(sroute[i]) != null && b.At(smap[i], sx[i], sz[i]) == null && b.spots.All(x => x.Id != sid[i]))
                    b.spots.Add(new PatrolSpot { Id = sid[i], Map = smap[i], X = sx[i], Z = sz[i], Route = sroute[i], Type = (PostKind)stype[i] });
            b.nextRoute = Math.Max(r.Int("nextr", 1), b.routes.Count == 0 ? 1 : b.routes.Max(x => x.Id) + 1);
            b.nextSpot = Math.Max(r.Int("nexts", 1), b.spots.Count == 0 ? 1 : b.spots.Max(x => x.Id) + 1);
            return b;
        }
    }

    public sealed class PatrolBookFactory : IStateFactory<PatrolBook>
    {
        public static readonly PatrolBookFactory Instance = new PatrolBookFactory();
        public int Version => 1;
        public PatrolBook Create() => new PatrolBook();
        public PatrolBook Decode(StateReader reader, int version) => PatrolBook.Decode(reader);
        public PatrolBook FromLegacy(StateReader legacy) => Create();
    }

    /// <summary>What a soldier is posted to: a patrol route or a defensive position.</summary>
    public enum PostingKind : byte { Route = 1, Position = 2 }

    /// <summary>
    /// The soldiers the player has posted to a specific route or position (saved as <c>wils.military.postings</c>). Anyone not listed is automatic. A soldier has at most one posting, and a posting
    /// that points at something that is gone is dropped when the colony is loaded.
    /// </summary>
    public sealed class PostingBook : IModuleState
    {
        public const string StateId = "wils.military.postings";
        public const int Max = 200;
        public string Id => StateId;
        private readonly Dictionary<int, KeyValuePair<PostingKind, int>> postings = new Dictionary<int, KeyValuePair<PostingKind, int>>();
        public int Count => postings.Count;

        public bool Post(int pawn, PostingKind kind, int target)
        {
            if (pawn <= 0 || target <= 0) return false;
            if (!postings.ContainsKey(pawn) && postings.Count >= Max) return false;
            postings[pawn] = new KeyValuePair<PostingKind, int>(kind, target); return true;
        }
        public bool Clear(int pawn) => postings.Remove(pawn);
        public bool TryGet(int pawn, out PostingKind kind, out int target)
        {
            if (postings.TryGetValue(pawn, out var p)) { kind = p.Key; target = p.Value; return true; }
            kind = 0; target = 0; return false;
        }
        /// <summary>The route a soldier is posted to, or 0.</summary>
        public int RouteOf(int pawn) => TryGet(pawn, out var k, out int t) && k == PostingKind.Route ? t : 0;
        /// <summary>The position a soldier is posted to, or 0.</summary>
        public int PositionOf(int pawn) => TryGet(pawn, out var k, out int t) && k == PostingKind.Position ? t : 0;

        /// <summary>Drops postings of soldiers who are gone and of routes or positions that no longer exist. Returns how many.</summary>
        public int Reconcile(Func<int, bool> pawnExists, Func<int, bool> routeExists, Func<int, bool> positionExists)
        {
            var drop = postings.Where(p => !pawnExists(p.Key) || (p.Value.Key == PostingKind.Route ? !routeExists(p.Value.Value) : !positionExists(p.Value.Value))).Select(p => p.Key).ToList();
            foreach (int id in drop) postings.Remove(id); return drop.Count;
        }

        public string Encode()
        {
            var ids = postings.Keys.OrderBy(x => x).ToList();
            return new StateWriter().Ints("pawn", ids).Ints("kind", ids.Select(i => (int)postings[i].Key).ToList()).Ints("target", ids.Select(i => postings[i].Value).ToList()).ToString();
        }

        public static PostingBook Decode(StateReader r)
        {
            var b = new PostingBook(); var pawn = r.Ints("pawn"); var kind = r.Ints("kind"); var target = r.Ints("target");
            int n = new[] { pawn.Count, kind.Count, target.Count }.Min();
            for (int i = 0; i < n && b.postings.Count < Max; i++) if ((kind[i] == 1 || kind[i] == 2) && pawn[i] > 0 && target[i] > 0) b.postings[pawn[i]] = new KeyValuePair<PostingKind, int>((PostingKind)kind[i], target[i]);
            return b;
        }
    }

    public sealed class PostingBookFactory : IStateFactory<PostingBook>
    {
        public static readonly PostingBookFactory Instance = new PostingBookFactory();
        public int Version => 1;
        public PostingBook Create() => new PostingBook();
        public PostingBook Decode(StateReader reader, int version) => Decode(reader);
        private static PostingBook Decode(StateReader reader) => PostingBook.Decode(reader);
        public PostingBook FromLegacy(StateReader legacy) => Create();
    }

    /// <summary>One stretch of a route: the spots to walk, in order, and where the route's cursor stands afterwards.</summary>
    public sealed class PatrolStretch { public List<PatrolSpot> Spots = new List<PatrolSpot>(); public int NextCursor; }

    public static class PatrolPlanner
    {
        /// <summary>Ticks to walk one cell, and to stand and look at each spot.</summary>
        public const int TicksPerCell = 13, PauseTicks = 240;

        /// <summary>Whether a soldier of this type walks to a spot of that type: their own, any Mixed spot, and a Mixed soldier walks them all.</summary>
        public static bool Eligible(PostKind soldier, PostKind spot) => spot != PostKind.Rally && (soldier == PostKind.Mixed || spot == PostKind.Mixed || soldier == spot);

        /// <summary>The spots of a route in a fixed order: a nearest-neighbour chain starting at the spot placed first. The same spots always give the same order, so a cursor means the same thing next time.</summary>
        public static List<PatrolSpot> Order(IEnumerable<PatrolSpot> spots)
        {
            var left = spots.OrderBy(s => s.Id).ToList(); var ordered = new List<PatrolSpot>();
            if (left.Count == 0) return ordered;
            var current = left[0]; left.RemoveAt(0); ordered.Add(current);
            while (left.Count > 0)
            {
                var next = left.OrderBy(s => s.DistanceTo(current.X, current.Z)).ThenBy(s => s.Id).First(); left.Remove(next); ordered.Add(next); current = next; ordered[ordered.Count - 1] = next;
            }
            return ordered;
        }

        /// <summary>
        /// The next stretch of an ordered route for a soldier standing at (<paramref name="x"/>, <paramref name="z"/>): from the cursor, as many spots as fit in the time budget, at least two when the
        /// route has two (a lone spot is a stretch of one), and never more than one lap. The cursor wraps, so a short route is walked in full and a long one over several patrols.
        /// </summary>
        public static PatrolStretch Plan(IList<PatrolSpot> ordered, int cursor, int x, int z, int budget)
        {
            var result = new PatrolStretch(); int n = ordered.Count; if (n == 0) return result;
            int start = ((cursor % n) + n) % n; long spent = 0; int px = x, pz = z;
            for (int i = 0; i < n; i++)
            {
                var spot = ordered[(start + i) % n];
                long cost = (long)Math.Round(spot.DistanceTo(px, pz) * TicksPerCell) + PauseTicks;
                if (result.Spots.Count >= Math.Min(2, n) && spent + cost > budget) break;
                result.Spots.Add(spot); spent += cost; px = spot.X; pz = spot.Z;
            }
            result.NextCursor = (start + result.Spots.Count) % n; return result;
        }

        /// <summary>The route to patrol automatically: of those that have at least one spot this soldier may walk (two, if they have two), the one walked least recently; ties by id.</summary>
        public static PatrolRoute Choose(IEnumerable<PatrolRoute> routes, Func<PatrolRoute, int> eligibleSpots)
        {
            var candidates = routes.Select(r => new { Route = r, Count = eligibleSpots(r) }).Where(x => x.Count > 0).ToList(); if (candidates.Count == 0) return null;
            return candidates.OrderBy(x => x.Count >= 2 ? 0 : 1).ThenBy(x => x.Route.LastWalked).ThenBy(x => x.Route.Id).First().Route;
        }
    }
}
