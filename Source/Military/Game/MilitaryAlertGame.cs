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
    /// Puts the alert into effect. At Red, every serving soldier who is able is drafted (if they are not already) and sent once to their post; at stand-down only the people this drafted are
    /// undrafted. Each soldier is handled once per Red, so anything the player orders afterwards stands, and nobody is dragged back.
    /// </summary>
    internal static class MilitaryAlertDriver
    {
        private static readonly HashSet<IntVec3> claimed = new HashSet<IntVec3>();
        public static void Reset() => claimed.Clear();

        public static void Run(int now)
        {
            var c = FamilyComponent.Current; if (c == null) return;
            var state = c.Registry.Alert();
            bool threat = false; foreach (var map in Find.Maps) if (map.IsPlayerHome && MilitaryCombat.HostilesAbout(map)) { threat = true; break; }
            var change = AlertRules.Step(state, threat, now);
            if (change == AlertChange.Raised) Messages.Message("WFL_Military_RedRaised".Translate(), MessageTypeDefOf.ThreatBig, false);
            else if (change == AlertChange.Lowered) Messages.Message("WFL_Military_StoodDown".Translate(AlertRules.Label(state.Effective)), MessageTypeDefOf.PositiveEvent, false);
            Apply(c, state);
        }

        /// <summary>Carries out the current level: musters at Red, stands down otherwise. Also called straight after the player chooses a level.</summary>
        public static void Apply(FamilyComponent c, AlertState state)
        {
            if (state.Effective == AlertLevel.Red) Muster(c, state);
            else if (state.Drafted.Count > 0 || state.Ordered.Count > 0) StandDown(c, state);
        }

        private static bool CanMuster(Pawn p) => p.Spawned && !p.Dead && !p.Downed && p.drafter != null && !p.InMentalState && !p.WorkTagIsDisabled(WorkTags.Violent) && p.Faction == Faction.OfPlayer;

        private static void Muster(FamilyComponent c, AlertState state)
        {
            var posts = c.Registry.Posts();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                var pending = new List<Pawn>();
                foreach (var p in map.mapPawns.FreeColonistsSpawned) if (MilitaryRoles.Serving(p) && !state.Ordered.Contains(p.thingIDNumber) && CanMuster(p)) pending.Add(p);
                if (pending.Count == 0) continue;
                var plan = PostPlanner.Assign(pending.Select(p => new RedSoldier { Id = p.thingIDNumber, Specialty = MilitaryRoles.Specialty(p), X = p.Position.x, Z = p.Position.z }), posts.All.Where(x => x.Map == map.uniqueID));
                foreach (var p in pending)
                {
                    int id = p.thingIDNumber; bool alreadyDrafted = p.Drafted;
                    if (!alreadyDrafted) { p.drafter.Drafted = true; state.Drafted.Add(id); }
                    state.Ordered.Add(id);
                    // someone the player had already drafted is under the player's hand: they are counted but not sent, so an order given a moment ago is never cut across
                    if (!alreadyDrafted && plan.TryGetValue(id, out var post)) Send(p, map, post);
                }
            }
        }

        private static void Send(Pawn pawn, Map map, MilitaryPost post)
        {
            var center = new IntVec3(post.X, 0, post.Z); IntVec3 dest = IntVec3.Invalid;
            foreach (var cell in GenRadial.RadialCellsAround(center, post.Kind == PostKind.Rally ? 5f : 1f, true))
            {
                if (!cell.InBounds(map) || !cell.Standable(map) || claimed.Contains(cell)) continue;
                var there = cell.GetFirstPawn(map); if (there != null && there != pawn) continue;
                if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) continue;
                dest = cell; break;
            }
            if (!dest.IsValid) return;
            claimed.Add(dest);
            var job = JobMaker.MakeJob(JobDefOf.Goto, dest); job.locomotionUrgency = LocomotionUrgency.Jog;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public static void StandDown(FamilyComponent c, AlertState state)
        {
            foreach (int id in state.Drafted.ToList())
                if (c.TryGetPawn(id) is Pawn p && !p.Dead && p.Spawned && p.drafter != null && p.Drafted) p.drafter.Drafted = false;
            state.Drafted.Clear(); state.Ordered.Clear(); claimed.Clear();
        }

        /// <summary>After a load: forget people who are gone, so a stale record can never keep an episode open.</summary>
        public static void Reconcile(FamilyComponent c)
        {
            var state = c.Registry.Alert();
            state.Drafted.RemoveAll(id => c.TryGetPawn(id) == null); state.Ordered.RemoveAll(id => c.TryGetPawn(id) == null);
        }
    }

    /// <summary>Shows the posts on the map while one is being placed, and while Red is in force.</summary>
    public sealed class MilitaryMapComponent : MapComponent
    {
        public MilitaryMapComponent(Map map) : base(map) { }

        private bool Showing()
        {
            var c = FamilyComponent.Current; if (c == null || !FamilyFramework.Catalog.IsEnabled(MilitaryCareers.Module)) return false;
            return Find.DesignatorManager.SelectedDesignator is Designator_MilitaryPost || Find.DesignatorManager.SelectedDesignator is Designator_RemoveMilitaryPost || c.Registry.Alert().Effective == AlertLevel.Red;
        }

        public override void MapComponentUpdate()
        {
            if (map != Find.CurrentMap || !Showing()) return;
            foreach (var p in FamilyComponent.Current.Registry.Posts().All) if (p.Map == map.uniqueID) GenDraw.DrawTargetHighlight(new LocalTargetInfo(new IntVec3(p.X, 0, p.Z)));
        }

        public override void MapComponentOnGUI()
        {
            if (map != Find.CurrentMap || !Showing()) return;
            Text.Font = GameFont.Tiny;
            foreach (var p in FamilyComponent.Current.Registry.Posts().All)
                if (p.Map == map.uniqueID) GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(new IntVec3(p.X, 0, p.Z)), ("WFL_Military_Post_" + p.Kind).Translate(), Color.white);
        }
    }

    /// <summary>Architect: place a Military post. Choosing it asks which kind, then each click places one.</summary>
    public class Designator_MilitaryPost : Designator
    {
        private PostKind kind = PostKind.Overwatch;
        public Designator_MilitaryPost(PostKind kind) : this() { this.kind = kind; }

        public Designator_MilitaryPost()
        {
            defaultLabel = "WFL_Military_PlacePost".Translate(); defaultDesc = "WFL_Military_PlacePostDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", false) ?? BaseContent.BadTex; hotKey = null; useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
        }

        public override void ProcessInput(Event ev)
        {
            var options = new List<FloatMenuOption>();
            foreach (PostKind k in Enum.GetValues(typeof(PostKind)))
            {
                var chosen = k;
                options.Add(new FloatMenuOption(("WFL_Military_Post_" + k).Translate() + " - " + ("WFL_Military_PostDesc_" + k).Translate(), () => { kind = chosen; base.ProcessInput(ev); }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            var map = Map; if (map == null || !c.InBounds(map) || c.Fogged(map) || !c.Standable(map)) return false;
            if (!map.IsPlayerHome) return "WFL_Military_PostHomeOnly".Translate();
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var book = FamilyComponent.Current?.Registry.Posts(); if (book == null) return;
            if (book.Place(kind, Map.uniqueID, c.x, c.z) == null) Messages.Message("WFL_Military_PostsFull".Translate(PostBook.MaxPosts), MessageTypeDefOf.RejectInput, false);
        }

        public override void SelectedUpdate() { base.SelectedUpdate(); GenUI.RenderMouseoverBracket(); }
    }

    /// <summary>Architect: take a Military post away.</summary>
    public class Designator_RemoveMilitaryPost : Designator
    {

        public Designator_RemoveMilitaryPost()
        {
            defaultLabel = "WFL_Military_RemovePost".Translate(); defaultDesc = "WFL_Military_RemovePostDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", false) ?? BaseContent.BadTex; hotKey = null; useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_Cancel;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            var book = FamilyComponent.Current?.Registry.Posts(); return book != null && Map != null && c.InBounds(Map) && book.At(Map.uniqueID, c.x, c.z) != null;
        }

        public override void DesignateSingleCell(IntVec3 c) { FamilyComponent.Current?.Registry.Posts().RemoveAt(Map.uniqueID, c.x, c.z); }
    }

    /// <summary>The Training Yard: a painted zone where soldiers drill, spar and practise. It stores nothing; it only says where.</summary>
    public class Zone_TrainingYard : Zone
    {
        public Zone_TrainingYard() { }
        public Zone_TrainingYard(ZoneManager zoneManager) : base("WFL_Military_TrainingYard".Translate(), zoneManager) { }
        protected override Color NextZoneColor => new Color(0.75f, 0.35f, 0.3f, 0.05f);
        protected override Material CreateMaterial() { var m = SolidColorMaterials.SimpleSolidColorMaterial(new Color(color.r, color.g, color.b, 0.06f)); m.renderQueue = 3600; return m; }
    }

    /// <summary>Architect &gt; Zone &gt; Training Yard.</summary>
    public class Designator_ZoneAdd_TrainingYard : Designator_ZoneAdd
    {
        protected override string NewZoneLabel => "WFL_Military_TrainingYard".Translate();

        public Designator_ZoneAdd_TrainingYard()
        {
            zoneTypeToPlace = typeof(Zone_TrainingYard);
            defaultLabel = "WFL_Military_TrainingYard".Translate(); defaultDesc = "WFL_Military_TrainingYardDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile", false) ?? BaseContent.BadTex; hotKey = null;
        }

        protected override Zone MakeNewZone() => new Zone_TrainingYard(Find.CurrentMap.zoneManager);

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map)) return false;
            var existing = Map.zoneManager.ZoneAt(c);
            if (existing != null && existing.GetType() != zoneTypeToPlace) return false;
            if (c.Fogged(Map)) return false;
            if (c.InNoZoneEdgeArea(Map)) return "TooCloseToMapEdge".Translate();
            if (c.Impassable(Map)) return false;
            return true;
        }
    }

    /// <summary>The on-screen alert: shown whenever the level is not Normal, so the current level is always in view.</summary>
    public class Alert_MilitaryAlert : Alert
    {
        public Alert_MilitaryAlert() { defaultPriority = AlertPriority.High; }

        private static AlertLevel Level()
        {
            var c = FamilyComponent.Current; if (c == null || !FamilyFramework.Catalog.IsEnabled(MilitaryCareers.Module)) return AlertLevel.Normal;
            return c.Registry.Alert().Effective;
        }

        public override AlertReport GetReport() => Level() != AlertLevel.Normal ? AlertReport.Active : AlertReport.Inactive;
        public override string GetLabel() => ("WFL_Military_AlertLabel").Translate(AlertRules.Label(Level()));
        public override TaggedString GetExplanation() => ("WFL_Military_AlertExplain_" + Level()).Translate();
    }

    public static class MilitaryPage
    {
        public static readonly Color Gold = new Color(0.95f, 0.8f, 0.3f);
        public static int Attention() { var c = FamilyComponent.Current; return c != null && c.Registry.Alert().Effective == AlertLevel.Red ? 1 : 0; }

        private static void Choose(FamilyComponent c, AlertLevel level)
        {
            var state = c.Registry.Alert(); AlertRules.Choose(state, level, FrameworkServices.Now); MilitaryAlertDriver.Apply(c, state);
        }

        private static void Place(Designator d) { Find.MainTabsRoot.EscapeCurrentTab(); Find.DesignatorManager.Select(d); }

        public static float Draw(Rect r, FamilyComponent c)
        {
            float y = 0f; float width = r.width;
            var state = c.Registry.Alert(); var posts = c.Registry.Posts();
            y = CommunityPageLayout.Section(r, 0f, y, width, "WFL_Military_AlertTitle".Translate(), s =>
            {
                float h = 0f; var eff = state.Effective;
                h = CommunityPageLayout.Line(s, h, "WFL_Military_AlertNow".Translate(AlertRules.Label(eff), state.RaisedByThreat ? "WFL_Military_ByThreat".Translate().ToString() : ""));
                float bw = (s.width - 12f) / 3f;
                foreach (AlertLevel lvl in Enum.GetValues(typeof(AlertLevel)))
                {
                    var rect = new Rect((int)lvl * (bw + 6f), h, bw, 30f);
                    GUI.color = lvl == state.Set ? MilitaryPage.Gold : Color.white;
                    if (Widgets.ButtonText(rect, AlertRules.Label(lvl))) Choose(c, lvl);
                    GUI.color = Color.white; TooltipHandler.TipRegion(rect, ("WFL_Military_AlertExplain_" + lvl).Translate());
                }
                h += 38f;
                bool auto = state.AutoRed; Widgets.CheckboxLabeled(new Rect(0f, h, s.width, 24f), "WFL_Military_AutoRed".Translate(), ref auto); state.AutoRed = auto;
                TooltipHandler.TipRegion(new Rect(0f, h, s.width, 24f), "WFL_Military_AutoRedTip".Translate()); h += 30f;
                return CommunityPageLayout.Line(s, h, "WFL_Military_AlertHelp".Translate(), true);
            });
            y = CommunityPageLayout.Section(r, 0f, y, width, "WFL_Military_DutiesTitle".Translate(), s =>
            {
                float h = 0f;
                bool on = state.DutiesOn; Widgets.CheckboxLabeled(new Rect(0f, h, s.width, 24f), "WFL_Military_DutiesOn".Translate(), ref on); state.DutiesOn = on;
                TooltipHandler.TipRegion(new Rect(0f, h, s.width, 24f), "WFL_Military_DutiesOnTip".Translate()); h += 28f;
                bool vol = state.Volunteers; Widgets.CheckboxLabeled(new Rect(0f, h, s.width, 24f), "WFL_Military_Volunteers".Translate(), ref vol); state.Volunteers = vol;
                TooltipHandler.TipRegion(new Rect(0f, h, s.width, 24f), "WFL_Military_VolunteersTip".Translate()); h += 30f;
                int yard = 0, onDuty = 0, watch = 0;
                foreach (var map in Find.Maps)
                {
                    if (!map.IsPlayerHome) continue;
                    yard += map.zoneManager.AllZones.OfType<Zone_TrainingYard>().Sum(z => z.Cells.Count);
                    foreach (var p in map.mapPawns.FreeColonistsSpawned) if (p.CurJobDef == MilitaryDefOf.WFL_MilitaryDuty) onDuty++;
                }
                watch = posts.Count(PostKind.Overwatch) + posts.Count(PostKind.Guard);
                h = CommunityPageLayout.Line(s, h, "WFL_Military_DutiesStatus".Translate(onDuty, yard));
                if (yard == 0) h = CommunityPageLayout.Line(s, h, "WFL_Military_NoYard".Translate(), true);
                if (watch == 0) h = CommunityPageLayout.Line(s, h, "WFL_Military_NoPostsHint".Translate(), true);
                return CommunityPageLayout.Line(s, h, "WFL_Military_DutiesHelp".Translate(), true);
            });
            y = CommunityPageLayout.Section(r, 0f, y, width, "WFL_Military_PostsTitle".Translate(posts.All.Count, PostBook.MaxPosts), s =>
            {
                float h = 0f;
                h = CommunityPageLayout.Line(s, h, "WFL_Military_PostsHelp".Translate(), true);
                foreach (PostKind k in Enum.GetValues(typeof(PostKind)))
                {
                    var kind = k;
                    h = CommunityPageLayout.Button(s, h, "WFL_Military_PlaceKind".Translate(("WFL_Military_Post_" + k).Translate()), () => Place(new Designator_MilitaryPost(kind)));
                }
                h = CommunityPageLayout.Button(s, h, "WFL_Military_PlaceYard".Translate(), () => Place(new Designator_ZoneAdd_TrainingYard()));
                int shown = 0;
                foreach (var p in posts.All.ToList())
                {
                    if (++shown > 12) { h = CommunityPageLayout.Line(s, h, "WFL_Military_MorePosts".Translate(posts.All.Count - 12), true); break; }
                    Widgets.Label(new Rect(0f, h, s.width - 90f, 24f), ("WFL_Military_Post_" + p.Kind).Translate() + " (" + p.X + ", " + p.Z + ")");
                    if (Widgets.ButtonText(new Rect(s.width - 84f, h, 84f, 24f), "WFL_Military_Remove".Translate())) posts.Remove(p.Id);
                    h += 28f;
                }
                return h;
            });
            y = CommunityPageLayout.Section(r, 0f, y, width, "WFL_Military_RolesTitle".Translate(), s =>
            {
                float h = 0f;
                foreach (var prof in MilitaryDoctrine.Profiles)
                {
                    GUI.color = MilitaryPage.Gold; h = CommunityPageLayout.Line(s, h, ("wils.military." + prof.Role.ToLowerInvariant() + "_label").Translate(), false); GUI.color = Color.white;
                    h = CommunityPageLayout.Line(s, h - 4f, prof.Job + "  +: " + prof.Strength + "  -: " + prof.Weakness, true);
                }
                return h;
            });
            return y;
        }
    }
}
