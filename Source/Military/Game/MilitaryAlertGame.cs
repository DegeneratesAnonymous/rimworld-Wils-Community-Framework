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
            FrameworkServices.PublishColonyAlert(MilitaryCareers.Module, (ColonyAlertLevel)(byte)state.Effective);          // every other module reads the colony level from here
            if (state.Effective == AlertLevel.Red) Muster(c, state);
            else if (state.Drafted.Count > 0 || state.Ordered.Count > 0) StandDown(c, state);
        }

        private static bool CanMuster(Pawn p) => p.Spawned && !p.Dead && !p.Downed && p.drafter != null && !p.InMentalState && !p.WorkTagIsDisabled(WorkTags.Violent) && p.Faction == Faction.OfPlayer;

        private static void Muster(FamilyComponent c, AlertState state)
        {
            var posts = c.Registry.Posts(); var postings = c.Registry.Postings();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                var pending = new List<Pawn>();
                foreach (var p in map.mapPawns.FreeColonistsSpawned) if (MilitaryRoles.Serving(p) && !state.Ordered.Contains(p.thingIDNumber) && CanMuster(p)) pending.Add(p);
                if (pending.Count == 0) continue;
                var weapons = MilitaryArmory.Weapons(map); int now = FrameworkServices.Now;
                var plan = PostPlanner.Assign(pending.Select(p => new RedSoldier { Id = p.thingIDNumber, Specialty = MilitaryRoles.Specialty(p), X = p.Position.x, Z = p.Position.z, Posted = postings.PositionOf(p.thingIDNumber) }), posts.All.Where(x => x.Map == map.uniqueID));
                foreach (var p in pending)
                {
                    int id = p.thingIDNumber; bool alreadyDrafted = p.Drafted;
                    if (!alreadyDrafted) { p.drafter.Drafted = true; state.Drafted.Add(id); }
                    state.Ordered.Add(id);
                    // someone the player had already drafted is under the player's hand: they are counted but not sent, so an order given a moment ago is never cut across
                    // a soldier with no suitable weapon fetches the best one from an Armory first, then goes to their position
                    Job arm = null;
                    if (!alreadyDrafted && weapons.Count > 0 && MilitaryArmory.MayTry(p, now)) { MilitaryArmory.NoteTry(p, now); arm = MilitaryArmory.ArmJob(p, MilitaryRoles.ActiveRole(p), weapons); }
                    if (arm != null) { weapons.Remove((ThingWithComps)arm.targetA.Thing); p.jobs.TryTakeOrderedJob(arm, JobTag.Misc); }
                    if (!alreadyDrafted && plan.TryGetValue(id, out var post)) Send(p, map, post, arm != null);
                }
            }
        }

        private static void Send(Pawn pawn, Map map, MilitaryPost post, bool queue = false)
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
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, queue);
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
            var patrols = c.Registry.Patrols(); var posts = c.Registry.Posts(); patrols.Reconcile();
            c.Registry.Postings().Reconcile(id => c.TryGetPawn(id) != null, id => patrols.Route(id) != null, id => posts.All.Any(x => x.Id == id));          // a posting never outlives its soldier, route or position
        }
    }

    /// <summary>Shows the defensive positions and patrol routes on the map while one is being placed, while the Military menu is open, and while Red is in force.</summary>
    public sealed class MilitaryMapComponent : MapComponent
    {
        private static readonly Color[] Palette =
        {
            new Color(.35f, .75f, 1f), new Color(1f, .6f, .2f), new Color(.55f, .9f, .4f), new Color(.95f, .4f, .7f), new Color(.8f, .7f, 1f), new Color(1f, .9f, .3f),
            new Color(.3f, .9f, .85f), new Color(.9f, .5f, .4f), new Color(.6f, .6f, .95f), new Color(.75f, .95f, .55f), new Color(1f, .7f, .8f), new Color(.7f, .8f, .8f)
        };
        private static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
        public static Color ColourOf(int route) => Palette[((route - 1) % Palette.Length + Palette.Length) % Palette.Length];
        private static Material MaterialOf(int route)
        {
            if (!materials.TryGetValue(route, out var m)) { var c = ColourOf(route); m = SolidColorMaterials.SimpleSolidColorMaterial(new Color(c.r, c.g, c.b, .45f)); m.renderQueue = 3600; materials[route] = m; }
            return m;
        }

        public MilitaryMapComponent(Map map) : base(map) { }

        private bool Showing()
        {
            var c = FamilyComponent.Current; if (c == null || !FamilyFramework.Catalog.IsEnabled(MilitaryCareers.Module)) return false;
            var d = Find.DesignatorManager.SelectedDesignator;
            if (d is Designator_MilitaryPost || d is Designator_PatrolSpot || d is Designator_RemoveMilitaryPost || MilitaryPage.IsOpen) return true;
            return MilitaryView.ShowPositions && c.Registry.Alert().Effective == AlertLevel.Red;
        }

        private static string Letter(PostKind k) => k == PostKind.Melee ? "M" : k == PostKind.Ranged ? "R" : k == PostKind.Mixed ? "X" : "";

        public override void MapComponentUpdate()
        {
            if (map != Find.CurrentMap || !Showing()) return;
            var reg = FamilyComponent.Current.Registry;
            foreach (var p in reg.Posts().All) if (p.Map == map.uniqueID) GenDraw.DrawTargetHighlight(new LocalTargetInfo(new IntVec3(p.X, 0, p.Z)));
            var book = reg.Patrols();
            foreach (var route in book.Routes)
            {
                var ordered = PatrolPlanner.Order(book.InRoute(route.Id).Where(s => s.Map == map.uniqueID));
                foreach (var spot in ordered) CellRenderer.RenderCell(new IntVec3(spot.X, 0, spot.Z), MaterialOf(route.Id));
                for (int i = 0; i + 1 < ordered.Count; i++) GenDraw.DrawLineBetween(new IntVec3(ordered[i].X, 0, ordered[i].Z).ToVector3Shifted(), new IntVec3(ordered[i + 1].X, 0, ordered[i + 1].Z).ToVector3Shifted());
            }
        }

        public override void MapComponentOnGUI()
        {
            if (map != Find.CurrentMap || !Showing()) return;
            Text.Font = GameFont.Tiny; var reg = FamilyComponent.Current.Registry;
            foreach (var p in reg.Posts().All)
                if (p.Map == map.uniqueID) GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(new IntVec3(p.X, 0, p.Z)), ("WFL_Military_Post_" + p.Kind).Translate(), Color.white);
            var book = reg.Patrols();
            foreach (var spot in book.Spots)
                if (spot.Map == map.uniqueID) GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(new IntVec3(spot.X, 0, spot.Z)), (book.Route(spot.Route)?.Name ?? "?") + " " + Letter(spot.Type), ColourOf(spot.Route));
        }
    }

    /// <summary>Architect: place a defensive position (Melee, Ranged or Mixed) or the Rally point. Choosing it asks which kind, then each click places one.</summary>
    public class Designator_MilitaryPost : Designator
    {
        private PostKind kind = PostKind.Mixed;
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
            foreach (var k in new[] { PostKind.Melee, PostKind.Ranged, PostKind.Mixed, PostKind.Rally })
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

    /// <summary>Architect: place a patrol spot. Choosing it asks which route (or a new one) and which type, then each click places one in that route.</summary>
    public class Designator_PatrolSpot : Designator
    {
        private int route; private PostKind type = PostKind.Mixed;
        public Designator_PatrolSpot(int route, PostKind type) : this() { this.route = route; this.type = type; }

        public Designator_PatrolSpot()
        {
            defaultLabel = "WFL_Military_PlaceSpot".Translate(); defaultDesc = "WFL_Military_PlaceSpotDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", false) ?? BaseContent.BadTex; hotKey = null; useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
        }

        public override void ProcessInput(Event ev)
        {
            var book = FamilyComponent.Current?.Registry.Patrols(); if (book == null) return;
            var routes = new List<FloatMenuOption>();
            foreach (var r in book.Routes) { var chosen = r.Id; routes.Add(new FloatMenuOption(r.Name, () => ChooseType(chosen, ev))); }
            if (book.Routes.Count < PatrolBook.MaxRoutes) routes.Add(new FloatMenuOption("WFL_Military_NewRoute".Translate(), () => { var made = book.AddRoute(); if (made != null) ChooseType(made.Id, ev); }));
            if (routes.Count == 0) return;
            Find.WindowStack.Add(new FloatMenu(routes));
        }

        private void ChooseType(int routeId, Event ev)
        {
            var options = new List<FloatMenuOption>();
            foreach (var t in new[] { PostKind.Melee, PostKind.Ranged, PostKind.Mixed })
            {
                var chosen = t;
                options.Add(new FloatMenuOption(("WFL_Military_Spot_" + t).Translate(), () => { route = routeId; type = chosen; base.ProcessInput(ev); }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            var map = Map; if (map == null || !c.InBounds(map) || c.Fogged(map) || !c.Standable(map)) return false;
            if (!map.IsPlayerHome) return "WFL_Military_PostHomeOnly".Translate();
            return route > 0;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var book = FamilyComponent.Current?.Registry.Patrols(); if (book == null) return;
            if (book.Place(route, type, Map.uniqueID, c.x, c.z) == null) Messages.Message("WFL_Military_SpotsFull".Translate(PatrolBook.MaxSpots), MessageTypeDefOf.RejectInput, false);
        }

        public override void SelectedUpdate() { base.SelectedUpdate(); GenUI.RenderMouseoverBracket(); }
    }

    /// <summary>Architect: take a defensive position or a patrol spot away.</summary>
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
            var reg = FamilyComponent.Current?.Registry; if (reg == null || Map == null || !c.InBounds(Map)) return false;
            return reg.Posts().At(Map.uniqueID, c.x, c.z) != null || reg.Patrols().At(Map.uniqueID, c.x, c.z) != null;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var reg = FamilyComponent.Current?.Registry; if (reg == null) return;
            reg.Posts().RemoveAt(Map.uniqueID, c.x, c.z); reg.Patrols().RemoveAt(Map.uniqueID, c.x, c.z);
        }
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
}
