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
    /// <summary>
    /// A shelf, cellar or stockpile inside a family zone can be given to the family with one toggle on the building. While it is on, the family keeps its filter to what a household needs, prefers it
    /// for storing, and treats whatever is inside as the family's own: pawns outside the family leave it alone except in the rare cases the household already allows (a starving person where the family
    /// permits emergency sharing, or someone tending an emergency patient), and a player's direct order always works.
    /// </summary>
    internal static class FamilyStores
    {
        private static Texture2D icon;
        public static Texture2D Icon => icon ?? (icon = ContentFinder<Texture2D>.Get("UI/WFL_FamilyZones", false) ?? TexButton.ShowZones);

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
                Tidy(c, b);
            }
            else
            {
                r.FamilyStores.Remove(b.thingIDNumber);
                foreach (var t in Contents(b)) r.ForgetStock(t.thingIDNumber);
            }
        }

        private static IEnumerable<Thing> Contents(Building_Storage b)
        {
            foreach (var cell in b.AllSlotCellsList())
                foreach (var t in cell.GetThingList(b.Map).ToList())
                    if (t != b && t.def.category == ThingCategory.Item) yield return t;
        }

        /// <summary>Keeps the filter to the household profile, makes this the preferred place to store, and marks what is inside as the family's.</summary>
        private static void Tidy(FamilyComponent c, Building_Storage b)
        {
            var zone = ZoneOf(b); var family = zone?.Family(c.Registry);
            if (family == null) return;
            var settings = b.GetStoreSettings(); if (settings?.filter == null) return;
            if (!ZoneStorage.Matches(settings.filter, ZoneStorage.Family)) ZoneStorage.Set(settings.filter, ZoneStorage.Family);
            if (settings.Priority < StoragePriority.Preferred) settings.Priority = StoragePriority.Preferred;
            foreach (var t in Contents(b))
                if (c.Registry.ClaimFor(t.thingIDNumber) == null && settings.AllowedToAccept(t))
                    c.Registry.ClaimStock(t.thingIDNumber, family.Id, family.Id, b.Map.uniqueID, FamilyEconomy.KindOf(t));
        }

        /// <summary>The hourly pass: a store that was destroyed or left the family zone is dropped; the rest are tidied.</summary>
        public static void Maintain(FamilyComponent c)
        {
            var r = c.Registry; if (r.FamilyStores.Count == 0) return;
            var live = new Dictionary<int, Building_Storage>();
            foreach (var map in Find.Maps)
                foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_Storage>())
                    if (r.FamilyStores.Contains(b.thingIDNumber)) live[b.thingIDNumber] = b;
            r.FamilyStores.RemoveAll(id => !live.TryGetValue(id, out var b) || ZoneOf(b) == null);
            foreach (var b in live.Values) if (r.FamilyStores.Contains(b.thingIDNumber)) Tidy(c, b);
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
}
