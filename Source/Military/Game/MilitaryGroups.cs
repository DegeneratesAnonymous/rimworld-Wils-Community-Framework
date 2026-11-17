using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>A small box that asks for a squad's name.</summary>
    internal sealed class Window_NameSquad : Window
    {
        private string text; private readonly Action<string> accept;
        public override Vector2 InitialSize => new Vector2(380f, 160f);
        public Window_NameSquad(string initial, Action<string> accept) { text = initial ?? ""; this.accept = accept; doCloseX = true; forcePause = false; absorbInputAroundWindow = false; closeOnClickedOutside = true; }

        public override void DoWindowContents(Rect r)
        {
            Widgets.Label(new Rect(0f, 0f, r.width, 24f), "WFL_Military_SquadNamePrompt".Translate());
            text = Widgets.TextField(new Rect(0f, 28f, r.width, 30f), text);
            if (Widgets.ButtonText(new Rect(0f, r.height - 36f, 120f, 32f), "WFL_Military_SquadOk".Translate()) && SquadBook.CleanName(text).Length > 0) { accept(text); Close(); }
            if (Widgets.ButtonText(new Rect(r.width - 120f, r.height - 36f, 120f, 32f), "WFL_Military_SquadCancel".Translate())) Close();
        }
    }

    /// <summary>
    /// The Squads tab of the Military menu: the community's own groups of soldiers, with squads inside squads. Add soldiers, select a whole squad, and post all of it to a patrol route or position at once.
    /// Needs nothing but this module.
    /// </summary>
    internal static class MilitarySquadsTab
    {
        private static List<Pawn> Serving() => Find.Maps.Where(m => m.IsPlayerHome).SelectMany(m => m.mapPawns.FreeColonistsSpawned).Where(MilitaryRoles.Serving).OrderBy(p => p.LabelShort).ToList();

        private static void PostSquad(FamilyComponent c, List<Pawn> soldiers, Action<PostingBook, int> apply)
        {
            var postings = c.Registry.Postings(); foreach (var p in soldiers) apply(postings, p.thingIDNumber);
        }

        private static void PostMenu(FamilyComponent c, List<Pawn> soldiers)
        {
            if (soldiers.Count == 0) return;
            var patrols = c.Registry.Patrols(); var posts = c.Registry.Posts(); var map = soldiers[0].Map?.uniqueID ?? -1;
            var options = new List<FloatMenuOption> { new FloatMenuOption("WFL_Military_PostingAuto".Translate(), () => PostSquad(c, soldiers, (b, id) => b.Clear(id))) };
            foreach (var route in patrols.Routes) { var rid = route.Id; options.Add(new FloatMenuOption("WFL_Military_PostingRoute".Translate(route.Name), () => PostSquad(c, soldiers, (b, id) => b.Post(id, PostingKind.Route, rid)))); }
            foreach (var post in posts.All.Where(x => x.Kind != PostKind.Rally && x.Map == map)) { var pid = post.Id; options.Add(new FloatMenuOption("WFL_Military_PostingPosition".Translate(("WFL_Military_Post_" + post.Kind).Translate() + " (" + post.X + ", " + post.Z + ")"), () => PostSquad(c, soldiers, (b, id) => b.Post(id, PostingKind.Position, pid)))); }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string Shared(FamilyComponent c, List<Pawn> soldiers)
        {
            if (soldiers.Count == 0) return "-";
            var postings = c.Registry.Postings(); string label = null;
            foreach (var p in soldiers)
            {
                int id = p.thingIDNumber, route = postings.RouteOf(id), pos = postings.PositionOf(id);
                string one = route > 0 ? (c.Registry.Patrols().Route(route)?.Name ?? "?") : pos > 0 ? "WFL_Military_PostingPosition".Translate(pos.ToString()).ToString() : "WFL_Military_PostingAuto".Translate().ToString();
                if (label == null) label = one; else if (label != one) return "WFL_Military_GroupMixed".Translate().ToString();
            }
            return label;
        }

        private static void AddMenu(FamilyComponent c, Squad squad, List<Pawn> serving)
        {
            var book = c.Registry.Squads(); var options = new List<FloatMenuOption>();
            var free = serving.Where(p => !squad.Members.Contains(p.thingIDNumber)).ToList();
            if (free.Count > 1) options.Add(new FloatMenuOption("WFL_Military_SquadAddAll".Translate(free.Count), () => { foreach (var p in free) book.Add(squad.Id, p.thingIDNumber); }));
            foreach (var p in free) { var pawn = p; options.Add(new FloatMenuOption(pawn.LabelShortCap, () => book.Add(squad.Id, pawn.thingIDNumber))); }
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void MoreMenu(FamilyComponent c, Squad squad)
        {
            var book = c.Registry.Squads(); var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("WFL_Military_SquadRename".Translate(), () => Find.WindowStack.Add(new Window_NameSquad(squad.Name, n => book.Rename(squad.Id, n)))),
                new FloatMenuOption("WFL_Military_SquadNewInside".Translate(), () => Find.WindowStack.Add(new Window_NameSquad("", n => book.Create(n, squad.Id))))
            };
            foreach (int id in squad.Members.ToList()) { int member = id; var pawn = c.TryGetPawn(member); options.Add(new FloatMenuOption("WFL_Military_SquadRemoveMember".Translate(pawn?.LabelShort ?? "#" + member), () => book.Remove(squad.Id, member))); }
            options.Add(new FloatMenuOption("WFL_Military_SquadDelete".Translate(), () => book.Delete(squad.Id)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public static float Draw(Rect r, float y, FamilyComponent c)
        {
            var book = c.Registry.Squads();
            foreach (var s in book.All.ToList()) foreach (int id in s.Members.ToList()) if (c.TryGetPawn(id) == null && c.Registry.GetPerson(id)?.InColony != true) book.Remove(s.Id, id);          // someone who has died or left
            return CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Military_GroupsTitle".Translate(), s =>
            {
                CommunityPageLayout.Help(s, 0f, "WFL_Military_GroupsHelp".Translate());
                if (Widgets.ButtonText(new Rect(0f, 0f, 160f, 28f), "WFL_Military_SquadNew".Translate())) Find.WindowStack.Add(new Window_NameSquad("", n => book.Create(n)));
                float h = 36f; var tree = book.Tree(); var serving = Serving();
                if (tree.Count == 0) return CommunityPageLayout.Line(s, h, "WFL_Military_NoGroups".Translate(), true);
                int i = 0;
                foreach (var kv in tree)
                {
                    var squad = kv.Key; int depth = kv.Value; var members = book.Effective(squad.Id).Select(id => c.TryGetPawn(id)).Where(p => p != null).ToList(); var soldiers = members.Where(p => p.Spawned && serving.Contains(p)).ToList();
                    var row = CommunityWidgets.Row(s, h, i++, 30f);
                    float indent = 6f + depth * 18f, postW = Mathf.Max(140f, s.width * .24f), btn = 62f, countW = 100f, nameW = s.width - postW - btn * 2f - 34f - countW - indent - 12f;
                    CommunityWidgets.Cell(new Rect(indent, row.y, nameW, row.height), (depth > 0 ? "- " : "") + squad.Name, depth > 0 ? CommunityWidgets.Muted : (Color?)null);
                    var tip = new Rect(indent, row.y, nameW, row.height); TooltipHandler.TipRegion(tip, members.Count == 0 ? "WFL_Military_SquadEmpty".Translate().ToString() : string.Join(", ", members.Select(p => p.LabelShort)));
                    CommunityWidgets.Cell(new Rect(indent + nameW, row.y, countW, row.height), "WFL_Military_GroupCount".Translate(soldiers.Count, members.Count), soldiers.Count > 0 ? (Color?)null : CommunityWidgets.Muted);
                    float x = indent + nameW + countW;
                    if (Widgets.ButtonText(new Rect(x, row.y + 2f, btn - 2f, row.height - 4f), "WFL_Military_SquadAdd".Translate())) AddMenu(c, squad, serving); x += btn;
                    var sel = new Rect(x, row.y + 2f, btn - 2f, row.height - 4f);
                    if (Widgets.ButtonText(sel, "WFL_Military_GroupSelect".Translate()) && members.Count > 0) { Find.Selector.ClearSelection(); foreach (var p in members.Where(m => m.Spawned)) Find.Selector.Select(p, false); Find.MainTabsRoot.EscapeCurrentTab(); }
                    TooltipHandler.TipRegion(sel, "WFL_Military_GroupSelectTip".Translate()); x += btn;
                    var post = new Rect(x, row.y + 2f, postW - 4f, row.height - 4f);
                    if (soldiers.Count == 0) CommunityWidgets.Cell(post, "-", CommunityWidgets.Muted);
                    else { if (Widgets.ButtonText(post, Shared(c, soldiers))) PostMenu(c, soldiers); TooltipHandler.TipRegion(post, "WFL_Military_GroupPostTip".Translate(soldiers.Count)); }
                    if (Widgets.ButtonText(new Rect(s.width - 32f, row.y + 2f, 30f, row.height - 4f), "...")) MoreMenu(c, squad);
                    h += row.height;
                    int maxPortraits = Mathf.Max(1, (int)((s.width - indent) / 42f));
                    foreach (var member in members.Take(maxPortraits)) {
                        int portraitIndex = members.IndexOf(member);
                        var portrait = new Rect(indent + portraitIndex * 42f, h, 38f, 42f);
                        GUI.DrawTexture(portrait, PortraitsCache.Get(member, portrait.size, Rot4.South, new Vector3(0f,0f,.1f),1.15f));
                        TooltipHandler.TipRegion(portrait,member.LabelCap);
                        if(Widgets.ButtonInvisible(portrait)) CameraJumper.TryJumpAndSelect(member);
                    }
                    if(members.Count>0) h+=48f;
                }
                return h;
            });
        }
    }
}

