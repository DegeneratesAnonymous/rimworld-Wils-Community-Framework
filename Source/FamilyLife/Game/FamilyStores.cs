using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

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
        /// <summary>The household members who would read a book kept in each store.</summary>
        private static readonly Dictionary<int, List<Pawn>> readers = new Dictionary<int, List<Pawn>>();
        /// <summary>Each toggled store and the household it serves, for the family's own pickup of meals and medicine.</summary>
        private static readonly Dictionary<int, KeyValuePair<Building_Storage, int>> placed = new Dictionary<int, KeyValuePair<Building_Storage, int>>();
        /// <summary>For books: the best value on offer elsewhere on the map. A shelved book worth under half of it is swapped out; a new one under four fifths of it is not taken.</summary>
        private static readonly Dictionary<int, float> bestBook = new Dictionary<int, float>();
        /// <summary>For clothing: how many spare pieces of each kind (layer and body part) the household needs, and how many it already holds.</summary>
        private static readonly Dictionary<int, Dictionary<string, int>> apparelNeed = new Dictionary<int, Dictionary<string, int>>(), apparelHeld = new Dictionary<int, Dictionary<string, int>>();

        /// <summary>What a book still has to teach this household, by the game's own rule for whether a reader can still gain from it (a book teaches only up to a level), plus a small constant for books that give something else. Zero means it is finished with.</summary>
        internal static float BookValue(Thing book, IEnumerable<Pawn> household)
        {
            var comp = (book as ThingWithComps)?.GetComp<CompBook>(); if (comp == null) return 0f;
            float value = 0f;
            foreach (var doer in comp.Doers)
            {
                var skillDoer = doer as BookOutcomeDoerGainSkillExp;
                if (skillDoer == null) { value += .25f; continue; }
                foreach (var pair in skillDoer.Values)
                    foreach (var p in household)
                    {
                        var skill = p.skills?.GetSkill(pair.Key);
                        if (skill == null || skill.TotallyDisabled) continue;
                        if (CanProgress(p, pair.Key, skillDoer.Quality)) value += pair.Value;
                    }
            }
            return value;
        }

        /// <summary>The game's own test for whether a reader can still gain a skill from a book. It is private, so it is reached once by name; if a game version renames it, a plain level ceiling stands in.</summary>
        private static readonly Func<Pawn, SkillDef, QualityCategory, bool> canProgress = Bind();
        private static Func<Pawn, SkillDef, QualityCategory, bool> Bind()
        {
            try
            {
                var m = HarmonyLib.AccessTools.Method(typeof(BookOutcomeDoerGainSkillExp), "CanProgressSkill");
                if (m != null && m.IsStatic) return (Func<Pawn, SkillDef, QualityCategory, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, SkillDef, QualityCategory, bool>), m);
            }
            catch (Exception ex) { Log.Warning("[Wil's Family Life] Book progress check unavailable, using a level ceiling: " + ex.Message); }
            return null;
        }
        private static bool CanProgress(Pawn p, SkillDef skill, QualityCategory quality) =>
            canProgress != null ? canProgress(p, skill, quality) : (p.skills?.GetSkill(skill)?.Level ?? 20) < (p.DevelopmentalStage.Juvenile() ? ActivityTuning.StoryMaxChildSkill : 20);

        /// <summary>One kind of clothing: which layers it covers and which body parts it is worn on.</summary>
        internal static string ApparelKind(ThingDef d) => d.apparel == null ? "" :
            string.Join(",", d.apparel.layers.Select(l => l.defName).OrderBy(x => x, StringComparer.Ordinal)) + "|" + string.Join(",", d.apparel.bodyPartGroups.Select(g => g.defName).OrderBy(x => x, StringComparer.Ordinal));

        internal static bool IsInFamilyStore(Thing t)
        {
            if (t == null || !t.Spawned) return false;
            foreach (var pair in placed.Values) if (pair.Key != null && pair.Key.Spawned && pair.Key.Map == t.Map && pair.Key.OccupiedRect().Contains(t.Position)) return true;
            return false;
        }

        /// <summary>Things of one gate that a household's toggled stores hold on a map, for a family member to take from first.</summary>
        internal static IEnumerable<Thing> HeldBy(int family, Map map, Func<Thing, bool> match)
        {
            foreach (var pair in placed.Values)
                if (pair.Value == family && pair.Key != null && pair.Key.Spawned && pair.Key.Map == map)
                    foreach (var t in Contents(pair.Key)) if (match(t)) yield return t;
        }

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
                r.FamilyStores.Remove(b.thingIDNumber); Forget(b.thingIDNumber);
                foreach (var t in Contents(b)) r.ForgetStock(t.thingIDNumber);
            }
        }

        private static void Forget(int id) { closed.Remove(id); readers.Remove(id); placed.Remove(id); bestBook.Remove(id); apparelNeed.Remove(id); apparelHeld.Remove(id); }
        private static void ForgetAll() { closed.Clear(); readers.Clear(); placed.Clear(); bestBook.Clear(); apparelNeed.Clear(); apparelHeld.Clear(); }

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

        /// <summary>A book the household has finished with does not count as held, so a better one can take its place.</summary>
        private static float Amount(Thing t, StoreGate gate, List<Pawn> household) =>
            gate == StoreGate.Book && BookValue(t, household) <= 0f ? 0f : Amount(t, gate);

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
                [StoreGate.Book] = StockLimits.BookCap,
                [StoreGate.Fuel] = StockLimits.FuelCap(fuelRates, family.FuelReserveDays),
            };
            float foodCap = StockLimits.DurableFoodCap(members.Count, family.FoodReserveDays);

            // One spare of each kind of clothing a member wears, matched to what they actually wear (a spare shirt for a shirt, spare trousers for trousers).
            var need = new Dictionary<string, int>();
            foreach (var p in members)
                foreach (var worn in (p.apparel?.WornApparel ?? new List<Apparel>()).Take(4)) { var k = ApparelKind(worn.def); need[k] = (need.TryGetValue(k, out var n) ? n : 0) + 1; }

            // What the household already keeps in its stores; a book it has finished with is let go, so it can pass to a household that can still learn from it.
            var held = new Dictionary<StoreGate, float>(); var heldApparel = new Dictionary<string, int>();
            foreach (var s in stores)
                foreach (var t in Contents(s))
                {
                    var g = GateOf(t); if (g == null) continue;
                    held[g.Value] = (held.TryGetValue(g.Value, out var h) ? h : 0f) + Amount(t, g.Value, members);
                    if (g == StoreGate.Apparel) { var k = ApparelKind(t.def); heldApparel[k] = (heldApparel.TryGetValue(k, out var n) ? n : 0) + t.stackCount; }
                    if (g == StoreGate.Book && BookValue(t, members) <= 0f && r.ClaimFor(t.thingIDNumber) != null) r.ForgetStock(t.thingIDNumber);
                }

            // The best book on offer elsewhere, so the shelf holds the most useful one and not just the nearest.
            float bestElsewhere = 0f; var inStores = new HashSet<int>(stores.SelectMany(s => Contents(s)).Select(t => t.thingIDNumber));
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (!(t is ThingWithComps twc) || twc.GetComp<CompBook>() == null || inStores.Contains(t.thingIDNumber) || t.IsForbidden(Faction.OfPlayer)) continue;
                var claim = r.ClaimFor(t.thingIDNumber); if (claim != null && claim.HolderFamily != family.Id) continue;
                bestElsewhere = Math.Max(bestElsewhere, BookValue(t, members));
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
                closed[s.thingIDNumber] = shut; readers[s.thingIDNumber] = members; placed[s.thingIDNumber] = new KeyValuePair<Building_Storage, int>(s, family.Id);
                bestBook[s.thingIDNumber] = bestElsewhere; apparelNeed[s.thingIDNumber] = need; apparelHeld[s.thingIDNumber] = heldApparel;

                foreach (var t in Contents(s))
                    if (r.ClaimFor(t.thingIDNumber) == null && settings.filter.Allows(t))
                        r.ClaimStock(t.thingIDNumber, family.Id, family.Id, s.Map.uniqueID, FamilyEconomy.KindOf(t));
            }
        }

        /// <summary>The hourly pass: a store that was destroyed or left the family zone is dropped; each household's remaining stores are re-planned against what it needs now.</summary>
        public static void Maintain(FamilyComponent c)
        {
            var r = c.Registry; if (r.FamilyStores.Count == 0) { ForgetAll(); return; }
            var live = new Dictionary<int, Building_Storage>();
            foreach (var map in Verse.Find.Maps)
                foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_Storage>())
                    if (r.FamilyStores.Contains(b.thingIDNumber)) live[b.thingIDNumber] = b;
            r.FamilyStores.RemoveAll(id => !live.TryGetValue(id, out var b) || ZoneOf(b) == null);
            foreach (var id in closed.Keys.ToList()) if (!r.FamilyStores.Contains(id)) Forget(id);
            foreach (var group in live.Values.Where(b => r.FamilyStores.Contains(b.thingIDNumber)).GroupBy(b => ZoneOf(b)?.Family(r)).Where(g => g.Key != null))
                Refresh(c, group.Key, group.ToList());
        }

        /// <summary>True when this store's gate for the thing is full, so the household takes no more of it. Things already inside, or being tidied there, are never refused.</summary>
        internal static bool Refuses(Building_Storage b, Thing t)
        {
            if (b == null || t == null || !closed.TryGetValue(b.thingIDNumber, out var shut)) return false;
            var gate = GateOf(t); if (gate == null) return false;
            bool inside = t.Spawned && t.Map == b.Map && b.OccupiedRect().Contains(t.Position);
            if (gate == StoreGate.Book && readers.TryGetValue(b.thingIDNumber, out var household) && household.Count > 0)
            {
                // A book with nothing left to teach is not wanted, even on the shelf. One worth under half of a better book on offer is swapped out for it;
                // and a book is only taken if it is close to the best on offer, so the shelf holds the most useful one and not just the nearest.
                float value = BookValue(t, household); if (value <= 0f) return true;
                bestBook.TryGetValue(b.thingIDNumber, out float best);
                if (inside) return value * 2f < best;
                if (value < best * .8f) return true;
            }
            if (inside) return false;
            if (gate == StoreGate.Apparel)
                return !(apparelNeed.TryGetValue(b.thingIDNumber, out var need) && apparelHeld.TryGetValue(b.thingIDNumber, out var heldNow)
                    && need.TryGetValue(ApparelKind(t.def), out int wanted) && (heldNow.TryGetValue(ApparelKind(t.def), out int have) ? have : 0) < wanted);
            return shut.Contains(gate.Value);
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

    /// <summary>A family member who goes looking for food takes it from their own household's store first, when it holds something at least as good as what they would have found.</summary>
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.TryFindBestFoodSourceFor))]
    internal static class Patch_FamilyFoodFirst
    {
        private static void Postfix(Pawn getter, Pawn eater, bool desperate, ref Thing foodSource, ref ThingDef foodDef, ref bool __result)
        {
            if (!__result || desperate || eater == null || getter == null || foodSource == null || foodSource.def.ingestible == null) return;
            try
            {
                var c = FamilyComponent.Current; if (c == null || c.Registry.FamilyStores.Count == 0 || eater.Map == null) return;
                int family = c.Registry.FamilyOf(eater.thingIDNumber); if (family == 0 || FamilyStores.IsInFamilyStore(foodSource)) return;
                var chosen = FoodUtility.GetFinalIngestibleDef(foodSource); if (chosen?.ingestible == null) return;
                Thing best = null; float bestDistance = float.MaxValue;
                foreach (var t in FamilyStores.HeldBy(family, eater.Map, x => x.def.ingestible != null && x.def.ingestible.preferability >= chosen.ingestible.preferability && !x.IsForbidden(getter)))
                {
                    if (!FoodUtility.WillEat(eater, t, getter, false) || !getter.CanReserve(t) || !getter.CanReach(t, Verse.AI.PathEndMode.Touch, Danger.Some)) continue;
                    float d = (t.Position - getter.Position).LengthHorizontalSquared;
                    if (d < bestDistance) { best = t; bestDistance = d; }
                }
                if (best != null) { foodSource = best; foodDef = FoodUtility.GetFinalIngestibleDef(best); }
            }
            catch (Exception ex) { HookSafety.Report("family food first", ex); }
        }
    }

    /// <summary>Whoever tends a family member reaches for that household's own medicine first, if it is at least as potent as what they would have used.</summary>
    [HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.FindBestMedicine))]
    internal static class Patch_FamilyMedicineFirst
    {
        private static void Postfix(Pawn healer, Pawn patient, bool onlyUseInventory, ref Thing __result)
        {
            if (onlyUseInventory || healer == null || patient == null || patient.Map == null) return;
            try
            {
                var c = FamilyComponent.Current; if (c == null || c.Registry.FamilyStores.Count == 0) return;
                int family = c.Registry.FamilyOf(patient.thingIDNumber); if (family == 0 || FamilyStores.IsInFamilyStore(__result)) return;
                float chosen = __result == null ? 0f : __result.def.GetStatValueAbstract(StatDefOf.MedicalPotency);
                Thing best = null; float bestPotency = -1f;
                foreach (var t in FamilyStores.HeldBy(family, patient.Map, x => x.def.IsMedicine && !x.IsForbidden(healer)))
                {
                    float potency = t.def.GetStatValueAbstract(StatDefOf.MedicalPotency);
                    if (__result != null && potency < chosen || potency <= bestPotency) continue;
                    if (patient.playerSettings != null && !patient.playerSettings.medCare.AllowsMedicine(t.def)) continue;
                    if (!healer.CanReserve(t) || !healer.CanReach(t, Verse.AI.PathEndMode.Touch, Danger.Some)) continue;
                    best = t; bestPotency = potency;
                }
                if (best != null) __result = best;
            }
            catch (Exception ex) { HookSafety.Report("family medicine first", ex); }
        }
    }
}
