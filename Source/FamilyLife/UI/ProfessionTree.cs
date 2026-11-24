using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>Draws one career as a left-to-right tree: entry roles on the left, each qualification a step to the right. Like a family tree, the lines show who may follow whom.</summary>
    internal static class ProfessionTree
    {
        private static bool showGraph;
        private static Dictionary<ProfessionRole, Color> Lanes = new Dictionary<ProfessionRole, Color>();
        private const float NodeH = 78f, GapY = 12f, GapX = 26f;

        internal static void AssignMenu(FamilyComponent c, ProfessionRole role)
        {
            var residents = c.Registry.Persons.Where(p => p.InColony && !p.Juvenile).ToList();
            Find.WindowStack.Add(new FloatMenu(Options(c, c.Registry.Assignments(), c.Registry.Careers(), role, residents, CareerService.Career(role.Career))));
        }

        public static float DrawCompact(Rect r, float y, FamilyComponent c, string career, List<ProfessionRole> roles, string search)
        {
            if (Widgets.ButtonText(new Rect(0f, y, Mathf.Min(260f, r.width), 30f), (showGraph ? "WFL_UX_CardView" : "WFL_UX_GraphView").Translate())) showGraph = !showGraph;
            y += 38f;
            if (showGraph) return Draw(r, y, c, career, roles, search);
            var shown = roles.Where(q => search.Length == 0 || CareerService.TitleOf(q, c.Registry).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(q => q.Level).ThenBy(q => q.Id).ToList();
            y = CommunityPageLayout.Line(r, y, "WFL_UX_StaffingKey".Translate(), true);
            int columns = r.width >= 620f ? 3 : r.width >= 400f ? 2 : 1;
            float width = (r.width - 12f * (columns - 1)) / columns;
            var def = CareerService.Career(career);
            foreach (var tier in shown.GroupBy(q => q.Level))
            {
                y = CommunityPageLayout.Line(r, y, (tier.Key == 1 ? "WFL_Tree_Entry" : tier.Key == 2 ? "WFL_Tree_Skilled" : "WFL_Tree_Specialist").Translate());
                int i = 0;
                foreach (var role in tier)
                {
                    float x = i % columns * (width + 12f), top = y + i / columns * 132f;
                    Widgets.DrawBoxSolid(new Rect(x, top, width, 120f), new Color(.15f, .16f, .17f));
                    string title = CareerService.TitleOf(role, c.Registry);
                    Widgets.Label(new Rect(x + 10f, top + 8f, width - 20f, 26f), title.Truncate(width - 20f));
                    var holders = c.Registry.Assignments().Holders(role.Id).Where(id => c.Registry.GetPerson(id)?.InColony == true).ToList();
                    string who = holders.Count == 0 ? "WFL_UX_NeutralRole".Translate().ToString() : string.Join(", ", holders.Select(id => CommunityCorePages.NameOf(c, id)));
                    CommunityWidgets.Cell(new Rect(x + 10f, top + 34f, width - 20f, 28f), who.Truncate(width - 20f), holders.Count == 0 ? CommunityWidgets.Muted : CommunityWidgets.Good);
                    TooltipHandler.TipRegion(new Rect(x, top, width, 64f), title + "\n" + who);
                    if (Widgets.ButtonText(new Rect(x + 10f, top + 76f, width - 20f, 30f), "WFL_UX_Details".Translate())) Find.WindowStack.Add(new RoleInspector(c, role, roles));
                    i++;
                }
                y += (int)Math.Ceiling(i / (float)columns) * 132f;
            }
            return y;
        }

        private sealed class RoleInspector : Window
        {
            private readonly FamilyComponent component; private readonly ProfessionRole role; private readonly List<ProfessionRole> roles; private Vector2 scroll; private float height = 900f;
            public RoleInspector(FamilyComponent c, ProfessionRole r, List<ProfessionRole> all) { component = c; role = r; roles = all; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
            public override Vector2 InitialSize => new Vector2(Mathf.Min(600f, UI.screenWidth - 40f), Mathf.Min(620f, UI.screenHeight - 60f));
            public override void DoWindowContents(Rect rect)
            {
                var def = CareerService.Career(role.Career); var reg = component.Registry;
                var area = new Rect(0f, 0f, rect.width, rect.height - 50f);
                Widgets.BeginScrollView(area, ref scroll, new Rect(0f, 0f, rect.width - 20f, height));
                try {
                    var body = new Rect(0f, 0f, rect.width - 20f, height);
                    float y = CommunityPageLayout.Line(body, 0f, CareerService.TitleOf(role, reg));
                    if (!string.IsNullOrEmpty(role.BonusKey)) y = CommunityPageLayout.Line(body, y, role.BonusKey.Translate());
                    var parents = ParentsOf(def, roles, role);
                    if (parents.Count > 0) y = CommunityPageLayout.Line(body, y, "WFL_Tree_Needs".Translate(string.Join(" / ", parents.Select(q => CareerService.TitleOf(q, reg)))));
                    if (def != null && role.Level > 1) {
                        var rule = RuleOf(def, role);
                        foreach (var skill in rule.Skills) y = CommunityPageLayout.Line(body, y, SkillName(skill.Key) + ": " + skill.Value);
                        y = CommunityPageLayout.Line(body, y, "WFL_UX_XpRequired".Translate(rule.GeneralXp, rule.SpecialtyXp));
                        foreach (var required in rule.RequiresAll) y = CommunityPageLayout.Line(body, y, "WFL_UX_Requires".Translate(CareerService.Describe(required)));
                    }
                    y = CommunityPageLayout.Line(body, y, "WFL_UX_StaffingKey".Translate(), true);
                    y = CommunityPageLayout.Line(body, y, "WFL_UX_Candidates".Translate());
                    foreach (var person in reg.Persons.Where(p => p.InColony && !p.Juvenile).OrderBy(p => CommunityCorePages.NameOf(component, p.Id))) {
                        var pawn = component.TryGetPawn(person.Id); if (pawn == null || pawn.Dead) continue;
                        var check = AssignmentBook.Check(role, reg.Careers(), person.Id); bool held = reg.Assignments().IsAssigned(person.Id, role.Id);
                        CommunityWidgets.NameButton(new Rect(0f, y, body.width * .5f, 36f), pawn);
                        string label = held ? "WFL_Prof_Remove".Translate().ToString() : check == AssignResult.Assigned ? "WFL_Career_Assign".Translate().ToString() : "WFL_Career_Needs_Short".Translate().ToString();
                        var action = new Rect(body.width * .55f, y, body.width * .45f, 32f);
                        if (held || check == AssignResult.Assigned) { if (Widgets.ButtonText(action, label)) { if (held) reg.Assignments().Unassign(person.Id, role.Id); else RoleLoadUi.Assign(reg, pawn, person.Id, role, () => reg.Assignments().Assign(person.Id, role, reg.Careers(), FrameworkServices.Now)); } }
                        else CommunityWidgets.Cell(action, label, CommunityWidgets.Muted);
                        y += 40f;
                    }
                    height = y + 12f;
                } finally { Widgets.EndScrollView(); }
            }
        }

        internal static List<ProfessionRole> ParentsOf(CareerDefinition def, List<ProfessionRole> roles, ProfessionRole role)
        {
            if (role.Level <= 1) return new List<ProfessionRole>();
            CareerRule rule = null;
            if (def != null && !string.IsNullOrEmpty(role.Specialty)) { if (!def.Level2Branches.TryGetValue(role.Specialty, out rule)) def.Specialties.TryGetValue(role.Specialty, out rule); }
            var named = rule == null ? new List<ProfessionRole>() : rule.RequiresAny.SelectMany(q => roles.Where(x => x != role && x.Specialty == q)).Distinct().ToList();
            if (named.Count > 0) return named;
            // A branched career may pair each entry role with one skilled branch. This keeps, for example,
            // Forager -> Farmer separate from Tracker -> Hunter instead of drawing every entry into one trunk.
            if (role.Level == 2 && !string.IsNullOrEmpty(role.Specialty))
            {
                var paired = roles.Where(x => x.Level == 1 && x.Path == role.Specialty).ToList();
                if (paired.Count > 0) return paired;
            }
            var below = roles.Where(x => x.Level == role.Level - 1).ToList();
            if (below.Count == 0) below = roles.Where(x => x.Level < role.Level).ToList();
            return below;
        }

        internal static Dictionary<ProfessionRole, int> Depths(CareerDefinition def, List<ProfessionRole> roles)
        {
            var depth = new Dictionary<ProfessionRole, int>();
            Func<ProfessionRole, int, int> find = null;
            find = (x, guard) =>
            {
                if (depth.TryGetValue(x, out var d)) return d;
                if (guard > 8) return 0;
                var ps = ParentsOf(def, roles, x); int v = ps.Count == 0 ? 0 : ps.Max(p => find(p, guard + 1)) + 1;
                depth[x] = v; return v;
            };
            foreach (var x in roles) find(x, 0);
            return depth;
        }

        public static float Draw(Rect r, float y, FamilyComponent c, string career, List<ProfessionRole> roles, string search)
        {
            var reg = c.Registry; var book = reg.Assignments(); var ledger = reg.Careers(); var def = CareerService.Career(career);
            if (UsesSkillLanes(def, roles)) return DrawSkillLanes(r, y, c, roles, search, reg, book, ledger, def);
            var depth = Depths(def, roles); int cols = depth.Values.DefaultIfEmpty(0).Max() + 1;
            float nodeW = Mathf.Clamp((r.width - GapX * (cols - 1)) / cols, 110f, 210f), top = y + 26f;
            var residents = reg.Persons.Where(p => p.InColony && !p.Juvenile).OrderBy(p => CommunityCorePages.NameOf(c, p.Id)).ToList();
            var children = roles.ToDictionary(x => x, x => roles.Where(k => ParentsOf(def, roles, k).Contains(x)).ToList());
            var pos = new Dictionary<ProfessionRole, Rect>();
            // Order each column by where its parents sit, so a branch's specialties stay together beside it and links do not cross.
            var order = new Dictionary<ProfessionRole, float>();
            for (int col = 0; col < cols; col++)
            {
                var inCol = roles.Where(x => depth[x] == col).OrderBy(x => { var ps = ParentsOf(def, roles, x).Where(order.ContainsKey).ToList(); return ps.Count == 0 ? 0f : ps.Average(q => order[q]); }).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
                for (int i = 0; i < inCol.Count; i++) order[inCol[i]] = i;
            }
            for (int col = cols - 1; col >= 0; col--)
            {
                var inCol = roles.Where(x => depth[x] == col).OrderBy(x => order[x]).ToList();
                var want = new Dictionary<ProfessionRole, float>();
                foreach (var x in inCol)
                {
                    var kids = children[x].Where(k => pos.ContainsKey(k)).ToList();
                    want[x] = kids.Count > 0 ? kids.Average(k => pos[k].center.y) - NodeH / 2f : float.NaN;
                }
                if (inCol.All(x => float.IsNaN(want[x])) && col < cols - 1)
                {
                    var next = pos.Where(kv => depth[kv.Key] == col + 1).Select(kv => kv.Value).ToList();
                    float mid = next.Count > 0 ? (next.Min(n => n.y) + next.Max(n => n.yMax)) / 2f : top, total = inCol.Count * NodeH + (inCol.Count - 1) * GapY, start = mid - total / 2f;
                    foreach (var x in inCol) { want[x] = start; start += NodeH + GapY; }
                }
                float floor = float.MinValue;
                foreach (var x in inCol)
                {
                    float cy = float.IsNaN(want[x]) ? (floor == float.MinValue ? top : floor) : want[x];
                    if (floor != float.MinValue) cy = Mathf.Max(cy, floor);
                    pos[x] = new Rect(col * (nodeW + GapX), cy, nodeW, NodeH); floor = cy + NodeH + GapY;
                }
            }
            float shift = top - pos.Values.Min(n => n.y); foreach (var x in roles.ToList()) { var n = pos[x]; pos[x] = new Rect(n.x, n.y + shift, n.width, n.height); }
            float bottom = pos.Values.Max(n => n.yMax);
            // column headings
            for (int col = 0; col < cols; col++)
            {
                var inCol = roles.Where(x => depth[x] == col).ToList(); if (inCol.Count == 0) continue;
                int lvl = inCol.Min(x => x.Level); string key = lvl <= 1 ? "WFL_Tree_Entry" : lvl == 2 ? "WFL_Tree_Skilled" : "WFL_Tree_Specialist";
                Text.Font = GameFont.Tiny; GUI.color = new Color(FamilyKit.Gold.r, FamilyKit.Gold.g, FamilyKit.Gold.b, .8f);
                Widgets.Label(new Rect(col * (nodeW + GapX) + 4f, y + 4f, nodeW, 18f), key.Translate().ToString().ToUpperInvariant()); GUI.color = Color.white; Text.Font = GameFont.Small;
            }
            var line = new Color(.62f, .55f, .34f, .9f);
            var palette = new[] { new Color(.9f, .55f, .25f), new Color(.35f, .65f, .95f), new Color(.6f, .8f, .4f), new Color(.8f, .45f, .8f) };
            var lane = new Dictionary<ProfessionRole, Color>(); int laneIx = 0;
            foreach (var g in roles.Where(x => x.Level == 2).OrderBy(x => order[x])) lane[g] = palette[laneIx++ % palette.Length];
            Lanes = lane;
            // entry roles share one trunk: anyone may start there, so they all lead to the same next step
            var entries = roles.Where(x => ParentsOf(def, roles, x).Count == 0).ToList();
            var gated = roles.Where(x => x.Level >= 2 && ParentsOf(def, roles, x).Count > 0 && ParentsOf(def, roles, x).All(q => q.Level == 1)).ToList();
            if (entries.Count > 0 && gated.Count > 0)
            {
                float ex = entries.Max(q => pos[q].xMax), top2 = entries.Min(q => pos[q].center.y), bot2 = entries.Max(q => pos[q].center.y), mx = ex + GapX / 2f - 4f, mid = (top2 + bot2) / 2f;
                foreach (var q in entries) Widgets.DrawLine(new Vector2(pos[q].xMax, pos[q].center.y), new Vector2(mx, pos[q].center.y), line, 2f);
                Widgets.DrawLine(new Vector2(mx, top2), new Vector2(mx, bot2), line, 2f);
                foreach (var g in gated) Curve(new Vector2(mx, mid), new Vector2(pos[g].x, pos[g].center.y), lane.TryGetValue(g, out var lc) ? lc : line);
            }
            foreach (var x in roles) foreach (var p in ParentsOf(def, roles, x)) if (!(x.Level >= 2 && ParentsOf(def, roles, x).All(q => q.Level == 1)))
                Curve(new Vector2(pos[p].xMax, pos[p].center.y), new Vector2(pos[x].x, pos[x].center.y), lane.TryGetValue(p, out var pc) ? pc : line);
            foreach (var x in roles) Node(pos[x], c, reg, book, ledger, def, x, residents, search, roles);
            return bottom + 8f;
        }

        private static bool UsesSkillLanes(CareerDefinition def, List<ProfessionRole> roles)
        {
            if (def == null) return false;
            var skilled = roles.Where(x => x.Level == 2 && !string.IsNullOrEmpty(x.Specialty)).ToList();
            return skilled.Count > 1 && skilled.All(s => roles.Any(e => e.Level == 1 && e.Path == s.Specialty));
        }

        /// <summary>Draws a multi-discipline career as separate horizontal bands, with the gate skills visible before any role is opened.</summary>
        private static float DrawSkillLanes(Rect r, float y, FamilyComponent c, List<ProfessionRole> roles, string search, FamilyRegistry reg, AssignmentBook book, CareerLedger ledger, CareerDefinition def)
        {
            const float laneLabelH = 28f, laneGap = 16f;
            const int cols = 3;
            float nodeW = Mathf.Clamp((r.width - GapX * (cols - 1)) / cols, 110f, 210f);
            var residents = reg.Persons.Where(p => p.InColony && !p.Juvenile).OrderBy(p => CommunityCorePages.NameOf(c, p.Id)).ToList();
            var skilled = roles.Where(x => x.Level == 2).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            var palette = new[] { new Color(.45f, .72f, .38f), new Color(.38f, .64f, .9f), new Color(.86f, .62f, .3f), new Color(.78f, .45f, .8f) };
            Lanes = new Dictionary<ProfessionRole, Color>();

            for (int col = 0; col < cols; col++)
            {
                string key = col == 0 ? "WFL_Tree_Entry" : col == 1 ? "WFL_Tree_Skilled" : "WFL_Tree_Specialist";
                Text.Font = GameFont.Tiny; GUI.color = new Color(FamilyKit.Gold.r, FamilyKit.Gold.g, FamilyKit.Gold.b, .8f);
                Widgets.Label(new Rect(col * (nodeW + GapX) + 4f, y + 4f, nodeW, 18f), key.Translate().ToString().ToUpperInvariant());
            }
            GUI.color = Color.white; Text.Font = GameFont.Small;
            float cursor = y + 26f;
            for (int laneIndex = 0; laneIndex < skilled.Count; laneIndex++)
            {
                var root = skilled[laneIndex]; var colour = palette[laneIndex % palette.Length]; Lanes[root] = colour;
                var entries = roles.Where(x => x.Level == 1 && x.Path == root.Specialty).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
                var specialists = roles.Where(x => x.Level >= 3 && ParentsOf(def, roles, x).Contains(root)).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
                int rows = Math.Max(1, Math.Max(entries.Count, specialists.Count)); float nodesH = rows * NodeH + (rows - 1) * GapY;
                var band = new Rect(0f, cursor, r.width, laneLabelH + nodesH + 12f);
                Widgets.DrawBoxSolid(band, new Color(colour.r, colour.g, colour.b, .055f));
                Widgets.DrawBoxSolid(new Rect(band.x, band.y, 4f, band.height), new Color(colour.r, colour.g, colour.b, .75f));

                string requirements = string.Join("  •  ", RuleOf(def, root).Skills.Select(s => SkillName(s.Key) + " " + s.Value));
                Text.Font = GameFont.Tiny; GUI.color = colour;
                Widgets.Label(new Rect(12f, cursor + 5f, r.width - 24f, 18f), (CareerService.TitleOf(root, reg) + " path" + (requirements.Length > 0 ? "  —  " + requirements : "")).ToUpperInvariant());
                GUI.color = Color.white; Text.Font = GameFont.Small;

                float nodesTop = cursor + laneLabelH, centreY = nodesTop + nodesH / 2f;
                var positions = new Dictionary<ProfessionRole, Rect>();
                PositionColumn(entries, 0, nodeW, nodesTop, nodesH, positions);
                positions[root] = new Rect(nodeW + GapX, centreY - NodeH / 2f, nodeW, NodeH);
                PositionColumn(specialists, 2, nodeW, nodesTop, nodesH, positions);

                foreach (var entry in entries) Curve(new Vector2(positions[entry].xMax, positions[entry].center.y), new Vector2(positions[root].x, positions[root].center.y), colour);
                foreach (var specialist in specialists) Curve(new Vector2(positions[root].xMax, positions[root].center.y), new Vector2(positions[specialist].x, positions[specialist].center.y), colour);
                foreach (var role in entries.Concat(new[] { root }).Concat(specialists)) Node(positions[role], c, reg, book, ledger, def, role, residents, search, roles);
                cursor = band.yMax + laneGap;
            }
            return cursor;
        }

        private static void PositionColumn(List<ProfessionRole> roles, int col, float nodeW, float top, float totalH, Dictionary<ProfessionRole, Rect> positions)
        {
            float used = roles.Count * NodeH + Math.Max(0, roles.Count - 1) * GapY;
            float yy = top + (totalH - used) / 2f;
            foreach (var role in roles) { positions[role] = new Rect(col * (nodeW + GapX), yy, nodeW, NodeH); yy += NodeH + GapY; }
        }

        private static string SkillName(string defName)
        {
            var def = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
            return (def?.skillLabel ?? defName).CapitalizeFirst();
        }

        private static void Curve(Vector2 a, Vector2 b, Color col)
        {
            const int steps = 14; Vector2 prev = a;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps, s = t * t * (3f - 2f * t);      // smooth step: leaves flat, turns in the middle
                var cur = new Vector2(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, s));
                Widgets.DrawLine(prev, cur, col, 2f); prev = cur;
            }
        }

        private static void Node(Rect n, FamilyComponent c, FamilyRegistry reg, AssignmentBook book, CareerLedger ledger, CareerDefinition def, ProfessionRole role, List<PersonRecord> residents, string search, List<ProfessionRole> all)
        {
            var holders = book.Holders(role.Id).Where(id => reg.GetPerson(id)?.InColony == true).ToList();
            string title = CareerService.TitleOf(role, reg);
            bool match = search.Length == 0 || title.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            int qualified = residents.Count(p => !book.IsAssigned(p.Id, role.Id) && AssignmentBook.Check(role, ledger, p.Id) == AssignResult.Assigned);
            Color accent = holders.Count > 0 ? FamilyKit.Gold : qualified > 0 ? new Color(.45f, .7f, .5f) : new Color(.4f, .4f, .42f);
            var parents = def == null ? new List<ProfessionRole>() : ParentsOf(def, all, role);
            if (!match) GUI.color = new Color(1f, 1f, 1f, .35f);
            Widgets.DrawBoxSolid(n, new Color(.16f, .16f, .17f)); Widgets.DrawBoxSolid(new Rect(n.x, n.y, 4f, n.height), accent);
            var laneColours = (role.Level == 2 ? new List<ProfessionRole> { role } : parents).Where(q => Lanes.ContainsKey(q)).Select(q => Lanes[q]).ToList();
            for (int i = 0; i < laneColours.Count; i++) Widgets.DrawBoxSolid(new Rect(n.x + 4f + (n.width - 4f) * i / laneColours.Count, n.yMax - 3f, (n.width - 4f) / laneColours.Count, 3f), laneColours[i]);
            var inner = new Rect(n.x + 10f, n.y + 4f, n.width - 14f, n.height - 8f);
            Text.Font = GameFont.Small; Text.WordWrap = false;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), title.Truncate(inner.width));
            GUI.color = match ? Color.gray : new Color(.5f, .5f, .5f, .35f);
            string who = holders.Count == 0 ? "WFL_Hub_Vacant".Translate().ToString() : string.Join(", ", holders.Select(id => CommunityCorePages.NameOf(c, id)));
            Widgets.Label(new Rect(inner.x, inner.y + 20f, inner.width, 20f), who.Truncate(inner.width));
            Text.WordWrap = true; GUI.color = Color.white;
            string tip = who + "\n" + "WFL_Hub_Qualified".Translate(role.Level, qualified);
            if (def != null) { var rule = RuleOf(def, role); if (rule.Skills.Count > 0) tip += "\n" + string.Join(", ", rule.Skills.Select(s => s.Key + " " + s.Value)); }
            if (role.Level >= 3 && parents.Count > 0) tip += "\n" + "WFL_Tree_Needs".Translate(string.Join(" / ", parents.Select(q => CareerService.TitleOf(q, reg)))).ToString();
            if (!string.IsNullOrEmpty(role.BonusKey)) tip += "\n" + "WFL_Career_Bonus".Translate(role.BonusKey.Translate());
            TooltipHandler.TipRegion(new Rect(n.x, n.y, n.width, 42f), tip);
            if (Widgets.ButtonText(new Rect(inner.x, inner.yMax - 26f, inner.width, 24f), "WFL_Career_Assign".Translate() + (qualified > 0 ? " (" + qualified + ")" : ""))) Find.WindowStack.Add(new FloatMenu(Options(c, book, ledger, role, residents, def)));
            GUI.color = Color.white;
        }

        private static CareerRule RuleOf(CareerDefinition def, ProfessionRole role)
        {
            string route = role.Level == 1 ? role.Path : role.Specialty;
            if (!string.IsNullOrEmpty(route)) { if (def.Level2Branches.TryGetValue(route, out var b)) return b; if (def.Specialties.TryGetValue(route, out var s)) return s; }
            return def.Level2;
        }

        /// <summary>The skills that matter for a role: the ones its own rule asks for, or, for an entry role, the ones the career's later steps ask for.</summary>
        internal static List<string> RelevantSkills(CareerDefinition def, ProfessionRole role)
        {
            if (def == null) return new List<string>();
            var own = RuleOf(def, role).Skills.Keys.ToList(); if (own.Count > 0 && (role.Level > 1 || !string.IsNullOrEmpty(role.Path))) return own;
            return new[] { def.Level2 }.Concat(def.Level2Branches.Values).Concat(def.Specialties.Values).SelectMany(q => q.Skills.Keys).Distinct().ToList();
        }

        private static List<FloatMenuOption> Options(FamilyComponent c, AssignmentBook book, CareerLedger ledger, ProfessionRole role, List<PersonRecord> residents, CareerDefinition def)
        {
            var options = new List<FloatMenuOption>(); var skills = RelevantSkills(def, role);
            Func<PersonRecord, int[]> levels = p => { var pawn = c.TryGetPawn(p.Id); var of = pawn == null ? (Func<string, int>)(k => 0) : CareerService.SkillsOf(pawn); return skills.Select(k => of(k)).ToArray(); };
            foreach (var p in residents.OrderByDescending(q => levels(q).Sum()).ThenBy(q => CommunityCorePages.NameOf(c, q.Id)))
            {
                int id = p.Id; bool already = book.IsAssigned(id, role.Id); var check = AssignmentBook.Check(role, ledger, id); var lv = levels(p);
                string skill = skills.Count == 0 ? "" : skills.Count == 1 ? " (" + lv[0] + ")" : " (" + string.Join(", ", skills.Select((k, i) => (DefDatabase<SkillDef>.GetNamedSilentFail(k)?.skillLabel ?? k).CapitalizeFirst() + " " + lv[i])) + ")";
                string label = CommunityCorePages.NameOf(c, id) + skill + (already ? " [" + "WFL_Career_Assigned".Translate() + "]" : check != AssignResult.Assigned ? " [" + "WFL_Career_Needs_Short".Translate() + "]" : "");
                options.Add(new FloatMenuOption(label, already ? (Action)(() => book.Unassign(id, role.Id)) : check == AssignResult.Assigned ? () => RoleLoadUi.Assign(c.Registry, c.TryGetPawn(id), id, role, () => book.Assign(id, role, ledger, Find.TickManager.TicksGame)) : (Action)null));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("WFL_ZoneNoFamilies".Translate(), null));
            return options;
        }
    }
}

