using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FamilyLife.Framework;

namespace FamilyLife.Domain
{
    public sealed class Squad { public int Id, ParentId; public string Name; public List<int> Members = new List<int>(); }

    /// <summary>
    /// The community's own squads: named groups of soldiers, which can hold smaller squads. A person can be in several squads. Everything the player does with a squad (select it, post all of it to a route or position) applies
    /// to its members and the members of the squads inside it. Nesting is limited to <see cref="MaxDepth"/> levels. Saved by person id.
    /// </summary>
    public sealed class SquadBook : IModuleState
    {
        public const string StateId = "wils.military.squads";
        public const int MaxDepth = 3, MaxSquads = 60, MaxName = 32;
        private readonly List<Squad> squads = new List<Squad>(); private int next = 1;

        public IReadOnlyList<Squad> All => squads;
        public Squad Get(int id) => squads.FirstOrDefault(s => s.Id == id);

        private int DepthOf(int id) { int d = 0; for (var s = Get(id); s != null && d < 20; s = Get(s.ParentId)) d++; return d; }

        public static string CleanName(string name) { name = (name ?? "").Trim(); return name.Length > MaxName ? name.Substring(0, MaxName) : name; }

        /// <summary>Makes a squad, optionally inside another. Refused for an empty name, an unknown parent, nesting that is too deep, or too many squads. Returns the new squad.</summary>
        public Squad Create(string name, int parent = 0)
        {
            name = CleanName(name); if (name.Length == 0 || squads.Count >= MaxSquads) return null;
            if (parent != 0 && (Get(parent) == null || DepthOf(parent) >= MaxDepth)) return null;
            var s = new Squad { Id = next++, ParentId = parent, Name = name }; squads.Add(s); return s;
        }

        public bool Rename(int id, string name) { var s = Get(id); name = CleanName(name); if (s == null || name.Length == 0) return false; s.Name = name; return true; }

        /// <summary>Removes a squad. Squads inside it move up to its parent; its members simply leave it.</summary>
        public bool Delete(int id)
        {
            var s = Get(id); if (s == null) return false;
            foreach (var child in squads.Where(q => q.ParentId == id)) child.ParentId = s.ParentId;
            squads.Remove(s); return true;
        }

        public bool Add(int squad, int person) { var s = Get(squad); if (s == null || person <= 0 || s.Members.Contains(person)) return false; s.Members.Add(person); return true; }
        public bool Remove(int squad, int person) { var s = Get(squad); return s != null && s.Members.Remove(person); }
        public IEnumerable<Squad> SquadsOf(int person) => squads.Where(s => s.Members.Contains(person));

        /// <summary>Drops a person from every squad (they died or left).</summary>
        public int Forget(int person) { int n = 0; foreach (var s in squads) n += s.Members.RemoveAll(m => m == person); return n; }

        /// <summary>The squad's members and those of every squad inside it.</summary>
        public HashSet<int> Effective(int id)
        {
            var result = new HashSet<int>(); var todo = new Stack<int>(); var seen = new HashSet<int>(); todo.Push(id);
            while (todo.Count > 0)
            {
                int cur = todo.Pop(); if (!seen.Add(cur)) continue; var s = Get(cur); if (s == null) continue;
                foreach (var m in s.Members) result.Add(m);
                foreach (var child in squads.Where(q => q.ParentId == cur)) todo.Push(child.Id);
            }
            return result;
        }

        /// <summary>Top-level squads first, each followed by the squads inside it, with how deep each one is.</summary>
        public List<KeyValuePair<Squad, int>> Tree()
        {
            var list = new List<KeyValuePair<Squad, int>>();
            void Walk(int parent, int depth) { foreach (var s in squads.Where(q => q.ParentId == parent).OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase)) { list.Add(new KeyValuePair<Squad, int>(s, depth)); if (depth < MaxDepth + 1) Walk(s.Id, depth + 1); } }
            Walk(0, 0); return list;
        }

        public string Encode() => new StateWriter().Int("n", next).Ints("id", squads.Select(s => s.Id).ToList()).Ints("pa", squads.Select(s => s.ParentId).ToList())
            .Strings("nm", squads.Select(s => s.Name).ToList()).Strings("mb", squads.Select(s => string.Join(" ", s.Members.Select(m => m.ToString(CultureInfo.InvariantCulture)))).ToList()).ToString();

        public static SquadBook Decode(StateReader r)
        {
            var b = new SquadBook(); var id = r.Ints("id"); var pa = r.Ints("pa"); var nm = r.Strings("nm"); var mb = r.Strings("mb"); b.next = Math.Max(1, r.Int("n", 1));
            for (int i = 0; i < id.Count && i < nm.Count; i++)
            {
                if (id[i] <= 0 || b.Get(id[i]) != null || string.IsNullOrEmpty(nm[i])) continue;
                var s = new Squad { Id = id[i], ParentId = i < pa.Count ? pa[i] : 0, Name = CleanName(nm[i]) };
                if (i < mb.Count) foreach (var part in mb[i].Split(' ')) if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int m) && m > 0 && !s.Members.Contains(m)) s.Members.Add(m);
                b.squads.Add(s); if (s.Id >= b.next) b.next = s.Id + 1;
            }
            foreach (var s in b.squads) if (s.ParentId != 0 && b.Get(s.ParentId) == null) s.ParentId = 0;          // a parent that is missing: top level
            return b;
        }
    }

    public sealed class SquadBookFactory : IStateFactory<SquadBook>
    {
        public static readonly SquadBookFactory Instance = new SquadBookFactory();
        public int Version => 1;
        public SquadBook Create() => new SquadBook();
        public SquadBook Decode(StateReader reader, int version) => SquadBook.Decode(reader);
        public SquadBook FromLegacy(StateReader legacy) => Create();
    }

    public static class SquadAccess
    {
        public static SquadBook Squads(this FamilyRegistry r) => r.Shared.Of(SquadBook.StateId, SquadBookFactory.Instance);
    }
}
