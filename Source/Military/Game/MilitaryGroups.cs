using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// Optional compatibility with Wil's Group Organization (ColonyRoster). Read through reflection only, so nothing here is needed when that mod is absent and a change in it can only make the groups
    /// disappear from the Military menu, never break it. Groups and subgroups are read; nothing in that mod is changed.
    /// </summary>
    internal static class GroupOrgBridge
    {
        internal sealed class GroupInfo { public int Id, Depth; public string Name; public object Raw; }

        private static Type componentType; private static bool looked; private static FieldInfo fId, fName;

        private static GameComponent Instance()
        {
            try
            {
                if (!looked) { looked = true; componentType = GenTypes.GetTypeInAnyAssembly("ColonyRoster.RosterGameComponent"); var g = componentType?.Assembly.GetType("ColonyRoster.PawnGroup"); fId = g?.GetField("id"); fName = g?.GetField("name"); }
                if (componentType == null || fId == null || fName == null || Current.Game == null) return null;
                return Current.Game.GetComponent(componentType);
            }
            catch (Exception) { return null; }
        }

        public static bool Available => Instance() != null;

        /// <summary>Top-level groups first, each followed by its subgroups, with how deep each one is.</summary>
        public static List<GroupInfo> Groups()
        {
            var list = new List<GroupInfo>(); var inst = Instance(); if (inst == null) return list;
            try
            {
                var top = componentType.GetMethod("TopLevelGroups")?.Invoke(inst, null) as IEnumerable; var children = componentType.GetMethod("Children");
                if (top == null || children == null) return list;
                void Walk(object group, int depth)
                {
                    if (depth > 6) return;
                    list.Add(new GroupInfo { Raw = group, Depth = depth, Id = (int)fId.GetValue(group), Name = fName.GetValue(group) as string ?? "?" });
                    if (children.Invoke(inst, new[] { group }) is IEnumerable kids) foreach (var k in kids) Walk(k, depth + 1);
                }
                foreach (var g in top) Walk(g, 0);
            }
            catch (Exception) { list.Clear(); }
            return list;
        }

        /// <summary>Everyone in the group, including its subgroups.</summary>
        public static List<Pawn> Members(GroupInfo group)
        {
            var inst = Instance(); var result = new List<Pawn>(); if (inst == null || group == null) return result;
            try { if (componentType.GetMethod("EffectiveMembers")?.Invoke(inst, new[] { group.Raw }) is IEnumerable all) foreach (var o in all) if (o is Pawn p && !p.Dead) result.Add(p); }
            catch (Exception) { result.Clear(); }
            return result;
        }
    }

    /// <summary>The Groups tab of the Military menu (shown only when Wil's Group Organization is running): each group and subgroup, its serving soldiers, and one posting for the whole group.</summary>
    internal static class MilitaryGroupsTab
    {
        private static IEnumerable<Pawn> Serving(IEnumerable<Pawn> members) => members.Where(p => p.Spawned && p.IsColonistPlayerControlled && MilitaryRoles.Serving(p));

        private static void PostGroup(FamilyComponent c, List<Pawn> soldiers, Action<PostingBook, int> apply)
        {
            var postings = c.Registry.Postings(); foreach (var p in soldiers) apply(postings, p.thingIDNumber);
        }

        private static void Menu(FamilyComponent c, List<Pawn> soldiers)
        {
            if (soldiers.Count == 0) return;
            var patrols = c.Registry.Patrols(); var posts = c.Registry.Posts(); var map = soldiers[0].Map?.uniqueID ?? -1;
            var options = new List<FloatMenuOption> { new FloatMenuOption("WFL_Military_PostingAuto".Translate(), () => PostGroup(c, soldiers, (b, id) => b.Clear(id))) };
            foreach (var route in patrols.Routes) { var rid = route.Id; options.Add(new FloatMenuOption("WFL_Military_PostingRoute".Translate(route.Name), () => PostGroup(c, soldiers, (b, id) => b.Post(id, PostingKind.Route, rid)))); }
            foreach (var post in posts.All.Where(x => x.Kind != PostKind.Rally && x.Map == map)) { var pid = post.Id; options.Add(new FloatMenuOption("WFL_Military_PostingPosition".Translate(("WFL_Military_Post_" + post.Kind).Translate() + " (" + post.X + ", " + post.Z + ")"), () => PostGroup(c, soldiers, (b, id) => b.Post(id, PostingKind.Position, pid)))); }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string Shared(FamilyComponent c, List<Pawn> soldiers)
        {
            if (soldiers.Count == 0) return "-";
            var postings = c.Registry.Postings(); var labels = new HashSet<string>();
            foreach (var p in soldiers)
            {
                int id = p.thingIDNumber, route = postings.RouteOf(id), pos = postings.PositionOf(id);
                labels.Add(route > 0 ? (c.Registry.Patrols().Route(route)?.Name ?? "?") : pos > 0 ? "WFL_Military_PostingPosition".Translate(pos.ToString()).ToString() : "WFL_Military_PostingAuto".Translate().ToString());
            }
            return labels.Count == 1 ? labels.First() : "WFL_Military_GroupMixed".Translate().ToString();
        }

        public static float Draw(Rect r, float y, FamilyComponent c)
        {
            return CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Military_GroupsTitle".Translate(), s =>
            {
                CommunityPageLayout.Help(s, 0f, "WFL_Military_GroupsHelp".Translate());
                var groups = GroupOrgBridge.Groups();
                if (groups.Count == 0) return CommunityPageLayout.Line(s, 0f, "WFL_Military_NoGroups".Translate(), true);
                float h = 0f; int i = 0;
                foreach (var g in groups)
                {
                    var members = GroupOrgBridge.Members(g); var soldiers = Serving(members).ToList(); var row = CommunityWidgets.Row(s, h, i++, 30f);
                    float indent = 6f + g.Depth * 18f, postW = Mathf.Max(150f, s.width * .28f), selW = 70f, countW = 110f, nameW = s.width - postW - selW - countW - indent - 8f;
                    CommunityWidgets.Cell(new Rect(indent, row.y, nameW, row.height), (g.Depth > 0 ? "- " : "") + g.Name, g.Depth > 0 ? CommunityWidgets.Muted : (Color?)null);
                    CommunityWidgets.Cell(new Rect(indent + nameW, row.y, countW, row.height), "WFL_Military_GroupCount".Translate(soldiers.Count, members.Count), soldiers.Count > 0 ? (Color?)null : CommunityWidgets.Muted);
                    var sel = new Rect(indent + nameW + countW, row.y + 2f, selW - 4f, row.height - 4f);
                    if (Widgets.ButtonText(sel, "WFL_Military_GroupSelect".Translate()) && members.Count > 0) { Find.Selector.ClearSelection(); foreach (var p in members.Where(x => x.Spawned)) Find.Selector.Select(p, false); Find.MainTabsRoot.EscapeCurrentTab(); }
                    TooltipHandler.TipRegion(sel, "WFL_Military_GroupSelectTip".Translate());
                    var post = new Rect(s.width - postW, row.y + 2f, postW - 4f, row.height - 4f);
                    if (soldiers.Count == 0) CommunityWidgets.Cell(post, "-", CommunityWidgets.Muted);
                    else { if (Widgets.ButtonText(post, Shared(c, soldiers))) Menu(c, soldiers); TooltipHandler.TipRegion(post, "WFL_Military_GroupPostTip".Translate(soldiers.Count)); }
                    h += row.height;
                }
                return h;
            });
        }
    }
}
