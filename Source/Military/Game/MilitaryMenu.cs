using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FamilyLife
{
    /// <summary>
    /// Community → Military: the Military management menu. Four tabs. Alert (the level, automatic Red, daily duties), Positions (defensive positions and patrol routes), Facilities (the Training Yard)
    /// and Soldiers (each serving soldier's role, what they are practising, what they are doing and where, and which route or position they are posted to).
    /// </summary>
    public static class MilitaryPage
    {
        public static readonly Color Gold = new Color(0.95f, 0.8f, 0.3f);
        private enum Tab { Alert, Positions, Facilities, Soldiers, Groups }
        private static Tab tab = Tab.Alert;
        private static int lastDraw = -10;

        /// <summary>True while the menu is on screen, so the map can show the positions and routes beside it.</summary>
        public static bool IsOpen => Time.frameCount - lastDraw <= 2;

        public static int Attention() { var c = FamilyComponent.Current; return c != null && c.Registry.Alert().Effective == AlertLevel.Red ? 1 : 0; }

        internal static void Choose(FamilyComponent c, AlertLevel level)
        {
            var state = c.Registry.Alert(); AlertRules.Choose(state, level, FrameworkServices.Now); MilitaryAlertDriver.Apply(c, state);
        }

        private static void Place(Designator d) { Find.MainTabsRoot.EscapeCurrentTab(); Find.DesignatorManager.Select(d); }

        public static float Draw(Rect r, FamilyComponent c)
        {
            lastDraw = Time.frameCount;
            float y = CommunityPageLayout.Section(r, 0f, 0f, r.width, "WFL_Military_MenuTitle".Translate(), s =>
            {
                var tabs = Enum.GetValues(typeof(Tab)).Cast<Tab>().ToList(); if (!tabs.Contains(tab)) tab = Tab.Alert;
                float bw = (s.width - 6f * (tabs.Count - 1)) / tabs.Count;
                for (int i = 0; i < tabs.Count; i++)
                {
                    var t = tabs[i]; var rect = new Rect(i * (bw + 6f), 0f, bw, 30f); GUI.color = t == tab ? Gold : Color.white;
                    if (Widgets.ButtonText(rect, ("WFL_Military_Tab_" + t).Translate())) tab = t;
                    GUI.color = Color.white;
                }
                return 36f;
            });
            switch (tab)
            {
                case Tab.Positions: return DrawPositions(r, y, c);
                case Tab.Facilities: return DrawFacilities(r, y, c);
                case Tab.Soldiers: return DrawSoldiers(r, y, c);
                case Tab.Groups: return MilitarySquadsTab.Draw(r, y, c);
                default: return DrawAlert(r, y, c);
            }
        }

        // ------------------------------------------------------------------ Alert

        private static float DrawAlert(Rect r, float y, FamilyComponent c)
        {
            var state = c.Registry.Alert(); var posts = c.Registry.Posts(); float width = r.width;
            y = CommunityPageLayout.Section(r, 0f, y, width, "WFL_Military_AlertTitle".Translate(), s =>
            {
                float h = CommunityPageLayout.Line(s, 0f, "WFL_Military_AlertNow".Translate(AlertRules.Label(state.Effective), state.RaisedByThreat ? "WFL_Military_ByThreat".Translate().ToString() : ""));
                float bw = (s.width - 12f) / 3f;
                foreach (AlertLevel lvl in Enum.GetValues(typeof(AlertLevel)))
                {
                    var rect = new Rect((int)lvl * (bw + 6f), h, bw, 30f);
                    GUI.color = lvl == state.Set ? Gold : Color.white;
                    if (Widgets.ButtonText(rect, AlertRules.Label(lvl))) Choose(c, lvl);
                    GUI.color = Color.white; TooltipHandler.TipRegion(rect, ("WFL_Military_AlertExplain_" + lvl).Translate());
                }
                h += 38f;
                bool auto = state.AutoRed; Widgets.CheckboxLabeled(new Rect(0f, h, s.width, 24f), "WFL_Military_AutoRed".Translate(), ref auto); state.AutoRed = auto;
                TooltipHandler.TipRegion(new Rect(0f, h, s.width, 24f), "WFL_Military_AutoRedTip".Translate()); h += 30f;
                return CommunityPageLayout.Help(s, h, "WFL_Military_AlertHelp".Translate());
            });
            y = CommunityPageLayout.Section(r, 0f, y, width, "WFL_Military_DutiesTitle".Translate(), s =>
            {
                float h = 0f;
                bool on = state.DutiesOn; Widgets.CheckboxLabeled(new Rect(0f, h, s.width, 24f), "WFL_Military_DutiesOn".Translate(), ref on); state.DutiesOn = on;
                TooltipHandler.TipRegion(new Rect(0f, h, s.width, 24f), "WFL_Military_DutiesOnTip".Translate()); h += 28f;
                bool vol = state.Volunteers; Widgets.CheckboxLabeled(new Rect(0f, h, s.width, 24f), "WFL_Military_Volunteers".Translate(), ref vol); state.Volunteers = vol;
                TooltipHandler.TipRegion(new Rect(0f, h, s.width, 24f), "WFL_Military_VolunteersTip".Translate()); h += 30f;
                int yard = 0, onDuty = 0;
                foreach (var map in Find.Maps)
                {
                    if (!map.IsPlayerHome) continue;
                    yard += map.zoneManager.AllZones.OfType<Zone_TrainingYard>().Sum(z => z.Cells.Count);
                    foreach (var p in map.mapPawns.FreeColonistsSpawned) if (p.CurJobDef == MilitaryDefOf.WFL_MilitaryDuty) onDuty++;
                }
                h = CommunityPageLayout.Line(s, h, "WFL_Military_DutiesStatus".Translate(onDuty, yard));
                if (yard == 0) h = CommunityPageLayout.Line(s, h, "WFL_Military_NoYard".Translate(), true);
                if (posts.All.Count == 0 && c.Registry.Patrols().Spots.Count == 0) h = CommunityPageLayout.Help(s, h, "WFL_Military_NoPostsHint".Translate());
                return CommunityPageLayout.Help(s, h, "WFL_Military_DutiesHelp".Translate());
            });
            return y;
        }

        // ------------------------------------------------------------------ Positions

        private static string PostLabel(MilitaryPost p) => ("WFL_Military_Post_" + p.Kind).Translate() + " (" + p.X + ", " + p.Z + ")";

        private static IEnumerable<Pawn> Soldiers() => Find.Maps.Where(m => m.IsPlayerHome).SelectMany(m => m.mapPawns.FreeColonistsSpawned).Where(MilitaryRoles.Serving).OrderBy(p => p.thingIDNumber);

        private static float DrawPositions(Rect r, float y, FamilyComponent c)
        {
            var posts = c.Registry.Posts(); var patrols = c.Registry.Patrols(); var postings = c.Registry.Postings(); float width = r.width;
            y = CommunityPageLayout.Section(r, 0f, y, width, "WFL_Military_PositionsTitle".Translate(posts.All.Count, PostBook.MaxPosts), s =>
            {
                float h = CommunityPageLayout.Help(s, 0f, "WFL_Military_PositionsHelp".Translate());
                float bw = (s.width - 18f) / 4f; int i = 0;
                foreach (var k in new[] { PostKind.Melee, PostKind.Ranged, PostKind.Mixed, PostKind.Rally })
                {
                    var kind = k; var rect = new Rect(i++ * (bw + 6f), h, bw, 30f);
                    if (Widgets.ButtonText(rect, "WFL_Military_PlaceKind".Translate(("WFL_Military_Post_" + k).Translate()))) Place(new Designator_MilitaryPost(kind));
                    TooltipHandler.TipRegion(rect, ("WFL_Military_PostDesc_" + k).Translate());
                }
                h += 38f;
                var soldiers = Soldiers().ToList(); int shown = 0;
                foreach (var p in posts.All.ToList())
                {
                    if (++shown > 16) { h = CommunityPageLayout.Line(s, h, "WFL_Military_MorePosts".Translate(posts.All.Count - 16), true); break; }
                    var here = soldiers.Where(x => postings.PositionOf(x.thingIDNumber) == p.Id).Select(x => x.LabelShortCap).ToList();
                    string text = PostLabel(p) + (here.Count > 0 ? "  -  " + "WFL_Military_PostedHere".Translate(string.Join(", ", here)) : "");
                    Widgets.Label(new Rect(0f, h, s.width - 90f, 24f), text);
                    if (Widgets.ButtonText(new Rect(s.width - 84f, h, 84f, 24f), "WFL_Military_Remove".Translate())) posts.Remove(p.Id);
                    h += 28f;
                }
                return h;
            });
            y = CommunityPageLayout.Section(r, 0f, y, width, "WFL_Military_RoutesTitle".Translate(patrols.Routes.Count, PatrolBook.MaxRoutes, patrols.Spots.Count, PatrolBook.MaxSpots), s =>
            {
                float h = CommunityPageLayout.Help(s, 0f, "WFL_Military_RoutesHelp".Translate());
                if (patrols.Routes.Count < PatrolBook.MaxRoutes) h = CommunityPageLayout.Button(s, h, "WFL_Military_AddRoute".Translate(), () => patrols.AddRoute());
                if (patrols.Routes.Count == 0) return CommunityPageLayout.Line(s, h, "WFL_Military_NoRoutes".Translate(), true);
                foreach (var route in patrols.Routes.ToList())
                {
                    var spots = PatrolPlanner.Order(patrols.InRoute(route.Id));
                    Widgets.DrawBoxSolid(new Rect(0f, h + 4f, 14f, 14f), MilitaryMapComponent.ColourOf(route.Id));
                    GUI.color = Gold; Widgets.Label(new Rect(20f, h, s.width - 120f, 24f), "WFL_Military_RouteLine".Translate(route.Name, spots.Count)); GUI.color = Color.white;
                    if (Widgets.ButtonText(new Rect(s.width - 96f, h, 96f, 24f), "WFL_Military_DeleteRoute".Translate())) { patrols.RemoveRoute(route.Id); foreach (var pawn in Soldiers()) if (postings.RouteOf(pawn.thingIDNumber) == route.Id) postings.Clear(pawn.thingIDNumber); continue; }
                    h += 28f;
                    float bw = (s.width - 18f) / 3f; int i = 0;
                    foreach (var t in new[] { PostKind.Melee, PostKind.Ranged, PostKind.Mixed })
                    {
                        var type = t; var rect = new Rect(i++ * (bw + 6f), h, bw, 26f);
                        if (Widgets.ButtonText(rect, "WFL_Military_PlaceSpotKind".Translate(("WFL_Military_Spot_" + t).Translate()))) Place(new Designator_PatrolSpot(route.Id, type));
                    }
                    h += 32f; int count = 0;
                    foreach (var spot in spots)
                    {
                        if (++count > 10) { h = CommunityPageLayout.Line(s, h, "WFL_Military_MorePosts".Translate(spots.Count - 10), true); break; }
                        Widgets.Label(new Rect(20f, h, s.width - 200f, 24f), ("WFL_Military_Spot_" + spot.Type).Translate() + " (" + spot.X + ", " + spot.Z + ")");
                        var moveRect = new Rect(s.width - 190f, h, 100f, 24f);
                        if (patrols.Routes.Count > 1 && Widgets.ButtonText(moveRect, "WFL_Military_MoveSpot".Translate()))
                        {
                            var id = spot.Id; var options = patrols.Routes.Where(x => x.Id != route.Id).Select(x => { var to = x.Id; return new FloatMenuOption(x.Name, () => patrols.Move(id, to)); }).ToList();
                            Find.WindowStack.Add(new FloatMenu(options));
                        }
                        if (Widgets.ButtonText(new Rect(s.width - 84f, h, 84f, 24f), "WFL_Military_Remove".Translate())) patrols.Remove(spot.Id);
                        h += 28f;
                    }
                    h += 8f;
                }
                return h;
            });
            return y;
        }

        // ------------------------------------------------------------------ Facilities

        private static float DrawFacilities(Rect r, float y, FamilyComponent c)
        {
            y = CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Military_YardTitle".Translate(), s =>
            {
                int yard = 0, zones = 0;
                foreach (var map in Find.Maps) if (map.IsPlayerHome) { var z = map.zoneManager.AllZones.OfType<Zone_TrainingYard>().ToList(); zones += z.Count; yard += z.Sum(x => x.Cells.Count); }
                float h = CommunityPageLayout.Line(s, 0f, "WFL_Military_YardStatus".Translate(zones, yard));
                h = CommunityPageLayout.Help(s, h, "WFL_Military_YardHelp".Translate());
                return CommunityPageLayout.Button(s, h, "WFL_Military_PlaceYard".Translate(), () => Place(new Designator_ZoneAdd_TrainingYard()));
            });
            y = CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Military_ArmoryTitle".Translate(), s =>
            {
                int zones = 0, melee = 0, ranged = 0, needing = 0;
                foreach (var map in Find.Maps)
                {
                    if (!map.IsPlayerHome) continue;
                    zones += MilitaryArmory.Zones(map).Count();
                    foreach (var w in MilitaryArmory.Weapons(map)) { if (w.def.IsRangedWeapon) ranged++; else melee++; }
                    foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(MilitaryRoles.Serving)) if (MilitaryArmory.Needs(p, MilitaryRoles.ActiveRole(p))) needing++;
                }
                float h = CommunityPageLayout.Line(s, 0f, "WFL_Military_ArmoryStatus".Translate(zones, melee, ranged));
                h = CommunityPageLayout.Line(s, h, "WFL_Military_ArmoryNeeding".Translate(needing));
                h = CommunityPageLayout.Help(s, h, "WFL_Military_ArmoryHelp".Translate());
                return CommunityPageLayout.Button(s, h, "WFL_Military_PlaceArmory".Translate(), () => Place(new Designator_ZoneAddStockpile_Armory()));
            });
            return y;
        }

        // ------------------------------------------------------------------ Soldiers

        private static string DutyText(Pawn p)
        {
            string report = p.jobs?.curDriver?.GetReport();
            if (p.CurJobDef == MilitaryDefOf.WFL_MilitaryDuty) { string at = MilitaryDutyGame.WhereOf(p); return string.IsNullOrEmpty(at) ? report : report + " (" + at + ")"; }
            return string.IsNullOrEmpty(report) ? "WFL_Military_Idle".Translate().ToString() : report;
        }

        private static void PostingMenu(FamilyComponent c, Pawn pawn)
        {
            var postings = c.Registry.Postings(); var patrols = c.Registry.Patrols(); var posts = c.Registry.Posts(); int id = pawn.thingIDNumber;
            var options = new List<FloatMenuOption> { new FloatMenuOption("WFL_Military_PostingAuto".Translate(), () => postings.Clear(id)) };
            foreach (var route in patrols.Routes) { var rid = route.Id; options.Add(new FloatMenuOption("WFL_Military_PostingRoute".Translate(route.Name), () => postings.Post(id, PostingKind.Route, rid))); }
            foreach (var post in posts.All.Where(x => x.Kind != PostKind.Rally && x.Map == pawn.Map?.uniqueID)) { var pid = post.Id; options.Add(new FloatMenuOption("WFL_Military_PostingPosition".Translate(PostLabel(post)), () => postings.Post(id, PostingKind.Position, pid))); }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string PostingLabel(FamilyComponent c, Pawn pawn)
        {
            var postings = c.Registry.Postings(); int id = pawn.thingIDNumber;
            int route = postings.RouteOf(id); if (route > 0) return c.Registry.Patrols().Route(route)?.Name ?? "WFL_Military_PostingAuto".Translate().ToString();
            int pos = postings.PositionOf(id); if (pos > 0) { var post = c.Registry.Posts().All.FirstOrDefault(x => x.Id == pos); if (post != null) return PostLabel(post); }
            return "WFL_Military_PostingAuto".Translate().ToString();
        }

        private static float DrawSoldiers(Rect r, float y, FamilyComponent c)
        {
            var soldiers = Soldiers().ToList();
            return CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Military_SoldiersTitle".Translate(soldiers.Count), s =>
            {
                float h = CommunityPageLayout.Help(s, 0f, "WFL_Military_SoldiersHelp".Translate());
                if (soldiers.Count == 0) return CommunityPageLayout.Line(s, h, "WFL_Military_NoSoldiers".Translate());
                Text.Font = GameFont.Tiny;
                float wName = s.width * .20f, wRole = s.width * .14f, wProg = s.width * .22f, wDuty = s.width * .26f, wPost = s.width - wName - wRole - wProg - wDuty;
                foreach (var p in soldiers)
                {
                    string role = MilitaryRoles.ActiveRole(p); string target = MilitaryDutyGame.CreditTarget(c, p, role);
                    var nameRect = new Rect(0f, h, wName - 4f, 24f);
                    if (Widgets.ButtonText(nameRect, p.LabelShortCap, false)) { Find.MainTabsRoot.EscapeCurrentTab(); CameraJumper.TryJumpAndSelect(p); }
                    TooltipHandler.TipRegion(nameRect, "WFL_Military_SelectTip".Translate());
                    Widgets.Label(new Rect(wName, h + 2f, wRole - 4f, 24f), role == null ? "" : ("wils.military." + role.ToLowerInvariant() + "_label").Translate().ToString());
                    string prog;
                    if (role != null && MilitaryCareers.Specialties.Contains(role)) prog = "WFL_Military_Assigned".Translate().ToString();
                    else if (target != null && MilitaryDutyGame.Progress(c, p, target, out int have, out int need)) prog = "WFL_Military_Practising".Translate(("wils.military." + target.ToLowerInvariant() + "_label").Translate(), have, need).ToString();
                    else prog = "WFL_Military_NothingToPractise".Translate().ToString();
                    Widgets.Label(new Rect(wName + wRole, h + 2f, wProg - 4f, 24f), prog);
                    Widgets.Label(new Rect(wName + wRole + wProg, h + 2f, wDuty - 4f, 24f), DutyText(p));
                    var postRect = new Rect(wName + wRole + wProg + wDuty, h, wPost, 24f);
                    if (Widgets.ButtonText(postRect, PostingLabel(c, p))) PostingMenu(c, p);
                    TooltipHandler.TipRegion(postRect, "WFL_Military_PostingTip".Translate());
                    h += 28f;
                }
                Text.Font = GameFont.Small;
                return h;
            });
        }
    }
}
