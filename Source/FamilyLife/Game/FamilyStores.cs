using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>What a household store limits separately: perishable food, food that keeps, and each other thing a household needs.</summary>
    internal enum StoreGate { Perishable, Durable, Medicine, Drug, Apparel, Book, Fuel }

    /// <summary>
    /// A shelf, cellar or stockpile inside a family zone can be given to the family with one toggle on the building. While it is on, the family keeps it to what this household actually uses:
    /// medicine for each member, only as much perishable food as the store's temperature lets them eat before it spoils, one spare piece of clothing per piece worn, one book, drugs only for the dependent,
    /// and fuel only for something in the house that burns it. Pawns outside the family leave it alone except in the rare cases the household already allows, and a player's direct order always works.
    /// </summary>
    internal static class FamilyStores
    {
        private static Texture2D icon;
        public static Texture2D Icon => icon ?? (icon = ContentFinder<Texture2D>.Get("UI/WFL_FamilyZones", false) ?? TexButton.ShowZones);

        /// <summary>Gates that are full right now, per store. Refreshed hourly; read on every storage decision, so it stays a plain lookup.</summary>
        private static readonly Dictionary<int, HashSet<StoreGate>> closed = new Dictionary<int, HashSet<StoreGate>>();

        public static Zone_Family ZoneOf(Building_Storage b)
        {
            if (b?.Map == null || !b.Spawned) return null;
            foreach (var cell in b.OccupiedRect().Cells)
                if (b.Map.zoneManager.ZoneAt(cell) is Zone_Family z && z.areaId != 0) return z;
            return null;
        }

        public static bool IsOn(FamilyRegistry r, Thing b) => r.FamilyStores.Contains(b.thingIDNumber);

        public static void Set(FamilyComponent c, Building_Storage b, bool on)
        {
            var r = c.Registry;
            if (on)
            {
                if (!r.FamilyStores.Contains(b.thingIDNumber)) r.FamilyStores.Add(b.thingIDNumber);
                var family = ZoneOf(b)?.Family(r);
                if (family != null) Refresh(c, family, r.FamilyStores.Select(id => StoreById(id)).Where(s => s != null && ZoneOf(s)?.Family(r)?.Id == family.Id).ToList());
            }
            else
            {
                r.FamilyStores.Remove(b.thingIDNumber); closed.Remove(b.thingIDNumber);
                foreach (var t in Contents(b)) r.ForgetStock(t.thingIDNumber);
            }
        }

        private static Building_Storage StoreById(int id)
        {
            foreach (var map in Verse.Find.Maps)
                foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_Storage>())
                    if (b.thingIDNumber == id) return b;
            return null;
        }

        private static IEnumerable<Thing> Contents(Building_Storage b)
        {
            foreach (var cell in b.AllSlotCellsList())
                foreach (var t in cell.GetThingList(b.Map).ToList())
                    if (t != b && t.def.category == ThingCategory.Item) yield return t;
        }

        // ---- what a gate is, and how full it is ----
        internal static StoreGate? GateOf(Thing t)
        {
            switch (FamilyEconomy.KindOf(t))
            {
                case SupplyKind.Food: return t.def.GetCompProperties<CompProperties_Rottable>() != null ? StoreGate.Perishable : StoreGate.Durable;
                case SupplyKind.Medicine: return StoreGate.Medicine;
                case SupplyKind.Drug: return StoreGate.Drug;
                case SupplyKind.Apparel: return StoreGate.Apparel;
                case SupplyKind.Book: return StoreGate.Book;
                case SupplyKind.Fuel: return StoreGate.Fuel;
                default: return null;
            }
        }

        private static float Amount(Thing t, StoreGate gate) =>
            gate == StoreGate.Perishable || gate == StoreGate.Durable ? t.stackCount * Math.Max(0f, t.GetStatValue(StatDefOf.Nutrition)) : t.stackCount;

        private static IEnumerable<ThingDef> Descendants(string category) =>
            DefDatabase<ThingCategoryDef>.GetNamedSilentFail(category)?.DescendantThingDefs ?? Enumerable.Empty<ThingDef>();

        private static bool IsEdible(ThingDef d) => d.ingestible != null && !d.IsDrug && d.ingestible.preferability >= FoodPreferability.RawBad && d.GetStatValueAbstract(StatDefOf.Nutrition) > 0f;

        /// <summary>Days a typical cooked meal lasts at this temperature: the yardstick for how much perishable food is worth keeping.</summary>
        private static float MealUsableDays(float temperature)
        {
            var rot = ThingDefOf.MealSimple?.GetCompProperties<CompProperties_Rottable>();
            return StockLimits.UsableDays(rot?.daysToRotStart ?? 4f, GenTemperature.RotRateAtTemperature(temperature));
        }

        private static bool Wearable(Pawn p, ThingDef def, HashSet<ApparelLayerDef> layers) =>
            def.apparel != null && ApparelUtility.HasPartsToWear(p, def) && (def.apparel.developmentalStageFilter & p.DevelopmentalStage) != 0 && def.apparel.layers.Any(layers.Contains);

        /// <summary>Sets every store this household has toggled: which things its filter accepts, and which gates are full.</summary>
        private static void Refresh(FamilyComponent c, Lineage family, List<Building_Storage> stores)
        {
            if (stores.Count == 0) return;
            var r = c.Registry;
            var members = c.ZoneMemberIds(family).Select(c.TryGetPawn).Where(p => p != null && !p.Dead).ToList();
            var map = stores[0].Map;
            var area = FamilyZones.AreaFor(family);

            // What this household needs.
            int dependents = members.Count(p => AddictionUtility.AddictedToAnything(p) || (p.health?.hediffSet?.hediffs.Any(h => h.def.defName.IndexOf("Dependency", StringComparison.OrdinalIgnoreCase) >= 0) ?? false));
            var fuelRates = new List<float>(); var fuelDefs = new HashSet<ThingDef>();
            if (area != null && area.Map == map)
                foreach (var building in map.listerBuildings.allBuildingsColonist.Where(b => area[b.Position]).Take(128))
                {
                    var fuel = building.GetComp<CompRefuelable>();
                    if (fuel == null || fuel.Props.fuelIsMortarBarrel) continue;
                    fuelRates.Add(fuel.Props.fuelConsumptionRate);
                    foreach (var d in fuel.Props.fuelFilter.AllowedThingDefs) fuelDefs.Add(d);
                }
            var caps = new Dictionary<StoreGate, float>
            {
                [StoreGate.Medicine] = StockLimits.MedicineCap(members.Count, family.MedicineReserve),
                [StoreGate.Drug] = StockLimits.DrugCap(dependents),
                [StoreGate.Apparel] = StockLimits.SpareApparelCap(members.Select(p => p.apparel?.WornApparelCount ?? 0)),
                [StoreGate.Book] = StockLimits.BookCap,
                [StoreGate.Fuel] = StockLimits.FuelCap(fuelRates, family.FuelReserveDays),
            };
            float foodCap = StockLimits.DurableFoodCap(members.Count, family.FoodReserveDays);

            // What the household already keeps in its stores.
            var held = new Dictionary<StoreGate, float>();
            foreach (var s in stores)
                foreach (var t in Contents(s))
                {
                    var g = GateOf(t); if (g == null) continue;
                    held[g.Value] = (held.TryGetValue(g.Value, out var h) ? h : 0f) + Amount(t, g.Value);
                }
            float Held(StoreGate g) => held.TryGetValue(g, out var h) ? h : 0f;
            float food = Held(StoreGate.Perishable) + Held(StoreGate.Durable);

            // Medicine is only worth the doctor's visit when somebody can use it; drugs only for the dependent; apparel only the family can wear.
            var layers = new Dictionary<Pawn, HashSet<ApparelLayerDef>>();
            foreach (var p in members) layers[p] = new HashSet<ApparelLayerDef>((p.apparel?.WornApparel ?? new List<Apparel>()).SelectMany(a => a.def.apparel.layers));

            foreach (var s in stores)
            {
                var settings = s.GetStoreSettings(); if (settings?.filter == null) continue;
                float temperature = GenTemperature.GetTemperatureForCell(s.Position, s.Map);
                float mealDays = MealUsableDays(temperature), rot = GenTemperature.RotRateAtTemperature(temperature);

                var defs = new HashSet<ThingDef>();
                foreach (var d in Descendants("Foods").Where(IsEdible))
                {
                    var rottable = d.GetCompProperties<CompProperties_Rottable>();
                    if (rottable == null || StockLimits.UsableDays(rottable.daysToRotStart, rot) >= .5f) defs.Add(d);
                }
                foreach (var d in Descendants("Medicine")) defs.Add(d);
                if (dependents > 0) foreach (var d in Descendants("Drugs")) defs.Add(d);
                foreach (var d in Descendants("Apparel")) if (members.Any(p => Wearable(p, d, layers[p]))) defs.Add(d);
                foreach (var d in Descendants("Books")) defs.Add(d);
                foreach (var d in fuelDefs) defs.Add(d);

                settings.filter.SetDisallowAll();
                foreach (var d in defs) settings.filter.SetAllow(d, true);
                if (settings.Priority < StoragePriority.Preferred) settings.Priority = StoragePriority.Preferred;

                var shut = new HashSet<StoreGate>();
                if (!StockLimits.Open(food, foodCap)) { shut.Add(StoreGate.Durable); shut.Add(StoreGate.Perishable); }
                else if (!StockLimits.Open(Held(StoreGate.Perishable), StockLimits.PerishableFoodCap(members.Count, family.FoodReserveDays, mealDays))) shut.Add(StoreGate.Perishable);
                foreach (var pair in caps) if (!StockLimits.Open(Held(pair.Key), pair.Value)) shut.Add(pair.Key);
                closed[s.thingIDNumber] = shut;

                foreach (var t in Contents(s))
                    if (r.ClaimFor(t.thingIDNumber) == null && settings.filter.Allows(t))
                        r.ClaimStock(t.thingIDNumber, family.Id, family.Id, s.Map.uniqueID, FamilyEconomy.KindOf(t));
            }
        }

        /// <summary>The hourly pass: a store that was destroyed or left the family zone is dropped; each household's remaining stores are re-planned against what it needs now.</summary>
        public static void Maintain(FamilyComponent c)
        {
            var r = c.Registry; if (r.FamilyStores.Count == 0) { closed.Clear(); return; }
            var live = new Dictionary<int, Building_Storage>();
            foreach (var map in Verse.Find.Maps)
                foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_Storage>())
                    if (r.FamilyStores.Contains(b.thingIDNumber)) live[b.thingIDNumber] = b;
            r.FamilyStores.RemoveAll(id => !live.TryGetValue(id, out var b) || ZoneOf(b) == null);
            foreach (var id in closed.Keys.ToList()) if (!r.FamilyStores.Contains(id)) closed.Remove(id);
            foreach (var group in live.Values.Where(b => r.FamilyStores.Contains(b.thingIDNumber)).GroupBy(b => ZoneOf(b)?.Family(r)).Where(g => g.Key != null))
                Refresh(c, group.Key, group.ToList());
        }

        /// <summary>True when this store's gate for the thing is full, so the household takes no more of it. Things already inside, or being tidied there, are never refused.</summary>
        internal static bool Refuses(Building_Storage b, Thing t)
        {
            if (b == null || t == null || !closed.TryGetValue(b.thingIDNumber, out var shut) || shut.Count == 0) return false;
            if (t.Spawned && t.Map == b.Map && b.OccupiedRect().Contains(t.Position)) return false;
            var gate = GateOf(t);
            return gate != null && shut.Contains(gate.Value);
        }
    }

    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.GetGizmos))]
    internal static class Patch_FamilyStorageToggle
    {
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Storage __instance)
        {
            foreach (var g in __result) yield return g;
            Gizmo extra = null;
            try
            {
                var c = FamilyComponent.Current;
                if (c != null && FamilyStores.ZoneOf(__instance) != null)
                {
                    var b = __instance;
                    extra = new Command_Toggle
                    {
                        defaultLabel = "WFL_FS_Label".Translate(),
                        defaultDesc = "WFL_FS_Desc".Translate(),
                        icon = FamilyStores.Icon,
                        isActive = () => FamilyStores.IsOn(c.Registry, b),
                        toggleAction = () => FamilyStores.Set(c, b, !FamilyStores.IsOn(c.Registry, b)),
                    };
                }
            }
            catch (Exception ex) { HookSafety.Report("family storage toggle", ex); }
            if (extra != null) yield return extra;
        }
    }

    /// <summary>A household store that already holds all its family needs of something stops taking more of it. Everything already inside stays put.</summary>
    [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.AllowedToAccept), new[] { typeof(Thing) })]
    internal static class Patch_FamilyStorageLimits
    {
        private static void Postfix(StorageSettings __instance, Thing t, ref bool __result)
        {
            if (!__result) return;
            try { if (__instance.owner is Building_Storage b && FamilyStores.Refuses(b, t)) __result = false; }
            catch (Exception ex) { HookSafety.Report("family storage limits", ex); }
        }
    }
}
