using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>What a kind of home-style zone keeps on its shelves, and the one-step way to set the shelves and stockpiles inside it to that. Names that the game does not define are skipped.</summary>
    public sealed class StorageProfile
    {
        public string Key;
        public string[] Categories = new string[0], Defs = new string[0];
    }

    public static class ZoneStorage
    {
        public static readonly StorageProfile Family = new StorageProfile { Key = "Family", Categories = new[] { "Foods", "Medicine", "Drugs", "Apparel", "Books", "Chemfuel" }, Defs = new[] { "Wood", "Chemfuel" } };
        public static readonly StorageProfile Treasury = new StorageProfile { Key = "Treasury", Categories = new[] { "Artifacts" }, Defs = new[] { "Silver", "Gold", "Plasteel", "Jade" } };

        private static IEnumerable<IStoreSettingsParent> StorageIn(Zone zone)
        {
            var map = zone?.Map; if (map == null) yield break;
            var seen = new HashSet<Thing>();
            foreach (var c in zone.Cells)
                foreach (var t in c.GetThingList(map).ToList())
                    if (t is IStoreSettingsParent p && seen.Add(t) && p.GetStoreSettings()?.filter != null) yield return p;
        }

        public static int Count(Zone zone) => StorageIn(zone).Count();

        /// <summary>Makes every shelf and storage building inside the zone accept only what the profile lists. Returns how many were set.</summary>
        public static int Apply(Zone zone, StorageProfile profile)
        {
            int n = 0;
            foreach (var p in StorageIn(zone).ToList())
            {
                var filter = p.GetStoreSettings().filter; filter.SetDisallowAll();
                foreach (var name in profile.Categories) { var d = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(name); if (d != null) filter.SetAllow(d, true); }
                foreach (var name in profile.Defs) { var d = DefDatabase<ThingDef>.GetNamedSilentFail(name); if (d != null) filter.SetAllow(d, true); }
                n++;
            }
            return n;
        }

        /// <summary>Asks, then sets. Said once when a zone is first painted and available any time from the zone's own button.</summary>
        public static void Offer(Zone zone, StorageProfile profile)
        {
            int count = Count(zone); if (count == 0) { Messages.Message(("WFL_ZS_None").Translate(), MessageTypeDefOf.RejectInput, false); return; }
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(("WFL_ZS_Ask_" + profile.Key).Translate(count), () => Messages.Message("WFL_ZS_Done".Translate(Apply(zone, profile)), MessageTypeDefOf.TaskCompletion, false)));
        }

        /// <summary>Called once for a freshly painted zone: offers only when there is storage inside to set.</summary>
        public static void OfferNew(Zone zone, StorageProfile profile) { if (Count(zone) > 0) Offer(zone, profile); }
    }
}
