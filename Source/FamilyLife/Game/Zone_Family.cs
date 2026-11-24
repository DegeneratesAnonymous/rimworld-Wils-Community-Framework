using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>A painted preferred home and its linked, map-scoped area. Movement limits remain player controlled.</summary>
    public class Zone_Family : Zone
    {
        private static readonly Color[] Colors =
        {
            new Color(0.95f, 0.65f, 0.45f, 0.05f), new Color(0.55f, 0.8f, 0.95f, 0.05f), new Color(0.75f, 0.9f, 0.55f, 0.05f), new Color(0.9f, 0.6f, 0.85f, 0.05f), new Color(0.95f, 0.85f, 0.45f, 0.05f)
        };

        public int areaId;                 // the allowed area this zone owns; 0 until a family is chosen

        public Zone_Family() { }

        // Far clearer than the game's own zones: only a faint tint, so everything underneath can be seen.
        protected override Material CreateMaterial()
        {
            var material = SolidColorMaterials.SimpleSolidColorMaterial(new Color(color.r, color.g, color.b, 0.05f));
            material.renderQueue = 3600;
            return material;
        }

        public Lineage Family(FamilyRegistry registry) => areaId == 0 ? null : registry.Lineages.FirstOrDefault(l => l.ZoneAreaId == areaId && l.ZoneMapId == Map.uniqueID);

        public override void PostRegister()
        {
            base.PostRegister();
            if (Hidden == FamilyZoneView.Show) Hidden = !FamilyZoneView.Show;
        }

        public Zone_Family(ZoneManager zoneManager) : base("WFL_FamilyZone".Translate(), zoneManager) { }

        protected override Color NextZoneColor => Colors[Find.CurrentMap == null ? 0 : (Find.CurrentMap.zoneManager.AllZones.Count(z => z is Zone_Family) % Colors.Length)];

        public Area LinkedArea => areaId == 0 ? null : FamilyZones.FindArea(areaId, Map.uniqueID);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref areaId, "wflAreaId");
        }

        public override void AddCell(IntVec3 c)
        {
            ZoneForce.Add(this, c, base.AddCell);
            GuestPasses.Invalidate(Map);
            var area = LinkedArea;
            if (area != null) area[c] = true;
        }

        public override void RemoveCell(IntVec3 c)
        {
            base.RemoveCell(c);
            GuestPasses.Invalidate(Map);
            var area = LinkedArea;
            if (area != null) area[c] = false;
        }

        public override void Delete(bool playSound)
        {
            GuestPasses.Invalidate(Map);
            var component = FamilyComponent.Current;
            var lineage = component?.Registry.Lineages.FirstOrDefault(l => l.ZoneAreaId != 0 && l.ZoneAreaId == areaId && l.ZoneMapId == Map.uniqueID);
            var area = LinkedArea;
            if (component != null && lineage != null && area != null) FamilyZones.Release(component, lineage, area);
            // Forget the link first so nothing can reach this area through a half-deleted zone, then remove only an area the game lets us delete
            // and that is still registered. The map's own Home area and other fixed areas are never touched; the game logs an error if asked to delete one.
            var map = Map; areaId = 0;
            if (area != null && area.Mutable && map != null && map.areaManager.AllAreas.Contains(area)) area.Delete();
            base.Delete(playSound);
        }

        /// <summary>Gives the zone to a family: a new linked area is made, painted with the zone's cells, for household activities and supply storage.</summary>
        public void SetFamily(FamilyComponent component, Lineage lineage)
        {
            var previous = component.Registry.Lineages.FirstOrDefault(l => l.ZoneAreaId != 0 && l.ZoneAreaId == areaId && l.ZoneMapId == Map.uniqueID);
            var area = LinkedArea;
            if (area == null)
            {
                if (!Map.areaManager.TryMakeNewAllowed(out Area_Allowed made))
                {
                    Messages.Message("WFL_ZoneTooMany".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }
                area = made;
                areaId = made.ID;
                foreach (var c in cells) area[c] = true;
            }
            if (previous != null && previous != lineage) FamilyZones.Release(component, previous, area);
            (area as Area_Allowed)?.SetLabel("WFL_ZoneSuffix".Translate(component.Registry.LineageDisplayName(lineage)).ToString());
            label = area.Label;
            FamilyZones.Assign(component, lineage, area);
            GuestPasses.Invalidate(Map);
            if (previous != lineage) DealBeds(component, lineage);
        }

        /// <summary>Shares out the beds in this zone among the family and says how it went.</summary>
        public void DealBeds(FamilyComponent component, Lineage lineage)
        {
            int given = ZoneBeds.Deal(component, lineage, this);
            var look = cells.Count > 0 ? new LookTargets(cells[0], Map) : LookTargets.Invalid;
            Messages.Message(given > 0 ? "WFL_ZoneBedsDealt".Translate(given, component.Registry.LineageDisplayName(lineage)) : "WFL_ZoneBedsNone".Translate(),
                look, given > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent, false);
        }

        public override string GetInspectString()
        {
            var component = FamilyComponent.Current;
            var lineage = component?.Registry.Lineages.FirstOrDefault(l => l.ZoneAreaId != 0 && l.ZoneAreaId == areaId && l.ZoneMapId == Map.uniqueID);
            if (lineage == null) return "WFL_ZoneNone".Translate();
            var area = LinkedArea;
            return "WFL_ZoneCurrent".Translate(component.Registry.LineageDisplayName(lineage),
                area == null ? 0 : FamilyZones.RestrictedCount(component, lineage, area), component.Registry.LivingCount(lineage.Id, Presence.Colony));
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;
            yield return new Command_Action
            {
                defaultLabel = "WFL_ZS_Button".Translate(), defaultDesc = "WFL_ZS_ButtonDesc_Family".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile", false) ?? BaseContent.BadTex,
                action = () => ZoneStorage.Offer(this, ZoneStorage.Family)
            };
            yield return new Command_Action
            {
                defaultLabel = "WFL_ZoneExpand".Translate(),
                defaultDesc = "WFL_ZoneExpandDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile", false) ?? BaseContent.BadTex,
                action = () => Find.DesignatorManager.Select(new Designator_ZoneExpand_Family(this))
            };
            yield return new Command_Action
            {
                defaultLabel = "WFL_ZoneDealBeds".Translate(),
                defaultDesc = "WFL_ZoneDealBedsDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/AssignOwner", false) ?? BaseContent.BadTex,
                action = () =>
                {
                    var component = FamilyComponent.Current;
                    var lineage = component?.Registry.Lineages.FirstOrDefault(l => l.ZoneAreaId != 0 && l.ZoneAreaId == areaId && l.ZoneMapId == Map.uniqueID);
                    if (lineage == null) { Messages.Message("WFL_ZoneNone".Translate(), MessageTypeDefOf.RejectInput, false); return; }
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("WFL_ZoneDealBedsConfirm".Translate(component.Registry.LineageDisplayName(lineage)), () => DealBeds(component, lineage)));
                }
            };
            yield return new Command_Action
            {
                tutorTag = "WFL_ChooseFamily",
                defaultLabel = "WFL_ZoneChooseFamily".Translate(),
                defaultDesc = "WFL_ZoneChooseFamilyDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/AssignOwner", false) ?? BaseContent.BadTex,
                action = () =>
                {
                    var component = FamilyComponent.Current;
                    if (component == null) return;
                    var options = component.Registry.Lineages.Where(l => component.Registry.IsListedFamily(l.Id))
                        .Select(l => { var lin = l; return new FloatMenuOption(component.Registry.LineageDisplayName(lin), () => SetFamily(component, lin)); }).ToList();
                    if (options.Count == 0) options.Add(new FloatMenuOption("WFL_ZoneNoFamilies".Translate(), null));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }
    }

    /// <summary>The Architect > Zone tool that paints family zones. Shrinking and deleting use the game's own Delete Zone tool.</summary>
    public class Designator_ZoneAdd_Family : Designator_ZoneAdd
    {
        protected override string NewZoneLabel => "WFL_FamilyZone".Translate();

        public Designator_ZoneAdd_Family()
        {
            zoneTypeToPlace = typeof(Zone_Family);
            defaultLabel = "WFL_FamilyZone".Translate();
            defaultDesc = "WFL_FamilyZoneDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile", false) ?? BaseContent.BadTex; defaultIconColor = new Color(0.95f, 0.65f, 0.45f);
            hotKey = null;
        }

        protected override Zone MakeNewZone() => new Zone_Family(Find.CurrentMap.zoneManager);

        public override void DesignateMultiCell(IEnumerable<IntVec3> cells) { ZoneCells.Paint(Map, cells, zoneTypeToPlace, c => CanDesignateCell(c).Accepted, MakeNewZone, out bool any, zone => ZoneStorage.OfferNew(zone, ZoneStorage.Family)); Finalize(any); }

        /// <summary>
        /// A home has beds, tables and other furniture in it, so unlike the game's own zones this one can be painted over things. Only fog,
        /// the map edge, another kind of zone and solid blocks such as walls are refused.
        /// </summary>
        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map)) return false;
            var existing = Map.zoneManager.ZoneAt(c);
            if (existing != null && existing.GetType() != zoneTypeToPlace) return false;
            if (c.Fogged(Map)) return false;
            if (c.InNoZoneEdgeArea(Map)) return "TooCloseToMapEdge".Translate();
            if (ZoneCells.SolidAt(c, Map)) return false;
            return true;
        }
    }

    /// <summary>Adds cells to one chosen family zone (furniture may be painted over), from the zone's Expand button.</summary>
    public class Designator_ZoneExpand_Family : Designator_ZoneAdd_Family
    {
        private readonly Zone_Family target;

        public Designator_ZoneExpand_Family() { }
        public Designator_ZoneExpand_Family(Zone_Family zone)
        {
            target = zone;
            defaultLabel = "WFL_ZoneExpand".Translate();
            defaultDesc = "WFL_ZoneExpandDesc".Translate();
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            var ok = base.CanDesignateCell(c);
            if (!ok.Accepted) return ok;
            return target == null || target.Map == Map;
        }

        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            if (target == null || target.Map != Map) return;
            int added = 0;
            foreach (var c in cells)
                if (CanDesignateCell(c).Accepted && !target.ContainsCell(c)) { if (Map.zoneManager.ZoneAt(c) == null) { target.AddCell(c); added++; } }
            Finalize(added > 0);
        }
    }
}

namespace FamilyLife
{
    /// <summary>Which cells a home-style zone refuses. Furniture does not count, only walls and solid rock.</summary>
    public static class ZoneCells
    {
        /// <summary>Paints cells into the zone under them (or touching them), or a new one. Done here so nothing but <paramref name="ok"/> decides what a cell may hold.</summary>
        public static void Paint(Map map, IEnumerable<IntVec3> cells, System.Type type, System.Func<IntVec3, bool> ok, System.Func<Zone> make, out bool any, System.Action<Zone> onNew = null)
        {
            any = false; var all = cells.ToList(); var list = all.Where(ok).ToList();
            var refused = all.Except(list).Take(6).Select(c => c + " (" + (c.GetEdifice(map)?.def.defName ?? "no building") + ", zone " + (map.zoneManager.ZoneAt(c)?.GetType().Name ?? "none") + ")").ToList();
            if (all.Count > list.Count) Log.Message("[Wil's Family Life] Zone painting refused " + (all.Count - list.Count) + " of " + all.Count + " cells, for example: " + string.Join(", ", refused));
            if (list.Count == 0) return;
            Zone zone = null;
            foreach (var c in list) { var z = map.zoneManager.ZoneAt(c); if (z != null && z.GetType() == type) { zone = z; break; } }
            if (zone == null)
                foreach (var c in list)
                {
                    foreach (var d in GenAdj.CardinalDirections) { var n = c + d; if (!n.InBounds(map)) continue; var z = map.zoneManager.ZoneAt(n); if (z != null && z.GetType() == type) { zone = z; break; } }
                    if (zone != null) break;
                }
            bool created = false; if (zone == null) { zone = make(); created = true; map.zoneManager.RegisterZone(zone); Find.Selector.Select(zone, false); }
            foreach (var c in list) if (map.zoneManager.ZoneAt(c) == null) { zone.AddCell(c); any = true; }
            if (created && any) onNew?.Invoke(zone);
        }

        public static bool SolidAt(IntVec3 c, Verse.Map map)
        {
            var edifice = c.GetEdifice(map);
            return edifice != null && (edifice.def.Fillage == FillCategory.Full || edifice.def.building?.isNaturalRock == true);
        }
    }
}

namespace FamilyLife
{
    /// <summary>
    /// The game refuses to put a zone cell under a building whose definition says zones cannot overlap it, which covers most furniture, and only logs an error.
    /// Our home-style zones are meant to hold furniture, so they add such a cell the same way the game does, minus that refusal.
    /// </summary>
    public static class ZoneForce
    {
        private static readonly System.Reflection.MethodInfo Grid = typeof(ZoneManager).GetMethod("AddZoneGridCell", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.FieldInfo Shuffled = typeof(Zone).GetField("cellsShuffled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>
        /// When a save is loaded the game puts every zone cell back into its zone grid with the same refusal as above, so a cell under a building that cannot overlap zones
        /// is left out of the grid and the zone stops covering it (the zone still lists the cell, but nothing is "in" it). Put those cells back.
        /// </summary>
        public static int Repair(Map map)
        {
            if (Grid == null || map == null) return 0;
            int fixedCells = 0;
            foreach (var zone in map.zoneManager.AllZones.OfType<Zone_Family>().ToList())
                foreach (var c in zone.cells.ToList())
                {
                    if (!c.InBounds(map) || map.zoneManager.ZoneAt(c) != null) continue;
                    Grid.Invoke(zone.zoneManager, new object[] { zone, c });
                    map.mapDrawer.MapMeshDirty(c, MapMeshFlagDefOf.Zone);
                    fixedCells++;
                }
            return fixedCells;
        }

        public static void Add(Zone zone, IntVec3 c, System.Action<IntVec3> normal)
        {
            if (Grid == null || zone.Map == null || zone.cells.Contains(c) || !zone.Map.thingGrid.ThingsListAt(c).Any(t => !t.def.CanOverlapZones)) { normal(c); return; }
            zone.cells.Add(c);
            Grid.Invoke(zone.zoneManager, new object[] { zone, c });
            zone.Map.mapDrawer.MapMeshDirty(c, MapMeshFlagDefOf.Zone);
            AutoHomeAreaMaker.Notify_ZoneCellAdded(c, zone);
            Shuffled?.SetValue(zone, false);
        }
    }
}