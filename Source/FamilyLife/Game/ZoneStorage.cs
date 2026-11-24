using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
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

        /// <summary>Makes one filter accept only what the profile lists.</summary>
        public static void Set(ThingFilter filter, StorageProfile profile)
        {
            filter.SetDisallowAll();
            foreach (var name in profile.Categories) { var d = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(name); if (d != null) filter.SetAllow(d, true); }
            foreach (var name in profile.Defs) { var d = DefDatabase<ThingDef>.GetNamedSilentFail(name); if (d != null) filter.SetAllow(d, true); }
        }

        /// <summary>True when the filter already accepts nothing the profile does not list.</summary>
        public static bool Matches(ThingFilter filter, StorageProfile profile)
        {
            var wanted = new ThingFilter(); Set(wanted, profile);
            return filter.AllowedThingDefs.All(d => wanted.Allows(d));
        }

        /// <summary>Makes every shelf and storage building inside the zone accept only what the profile lists. Returns how many were set.</summary>
        public static int Apply(Zone zone, StorageProfile profile)
        {
            int n = 0;
            foreach (var p in StorageIn(zone).ToList()) { Set(p.GetStoreSettings().filter, profile); n++; }
            return n;
        }

        /// <summary>Asks, then sets. Said once when a zone is first painted and available any time from the zone's own button.</summary>
        public static void Offer(Zone zone, StorageProfile profile)
        {
            int count = Count(zone); if (count == 0) { Messages.Message(("WFL_ZS_None").Translate(), MessageTypeDefOf.RejectInput, false); return; }
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(("WFL_ZS_Ask_" + profile.Key).Translate(count), () => Messages.Message("WFL_ZS_Done".Translate(Apply(zone, profile)), MessageTypeDefOf.TaskCompletion, false)));
        }

        /// <summary>Called once for a freshly painted zone, only when there is storage inside to set. The player's remembered answer (always / never) is followed; otherwise they are asked, and can make the answer stick.</summary>
        public static void OfferNew(Zone zone, StorageProfile profile)
        {
            int count = Count(zone); if (count == 0) return;
            int choice = FamilyLifeMod.Settings?.zoneStorageAuto ?? 0;
            if (choice == 2) return;
            if (choice == 1) { Messages.Message("WFL_ZS_Done".Translate(Apply(zone, profile)), MessageTypeDefOf.TaskCompletion, false); return; }
            Find.WindowStack.Add(new Window_ZoneStorageOffer(zone, profile, count));
        }
    }

    /// <summary>Asks whether to set a new zone's storage to what it keeps, with four answers: yes or not now for this zone, or always or never from now on (changeable in the mod settings).</summary>
    public class Window_ZoneStorageOffer : Window
    {
        private readonly Zone zone; private readonly StorageProfile profile; private readonly int count;
        public override Vector2 InitialSize => new Vector2(460f, 210f);
        public Window_ZoneStorageOffer(Zone zone, StorageProfile profile, int count)
        {
            this.zone = zone; this.profile = profile; this.count = count; forcePause = false; absorbInputAroundWindow = false; doCloseX = true; draggable = true;
        }
        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(0f, 0f, inRect.width, 90f), ("WFL_ZS_Ask_" + profile.Key).Translate(count));
            float w = (inRect.width - 12f) / 2f, y = inRect.height - 76f;
            if (Widgets.ButtonText(new Rect(0f, y, w, 32f), "WFL_ZS_Yes".Translate())) { Messages.Message("WFL_ZS_Done".Translate(ZoneStorage.Apply(zone, profile)), MessageTypeDefOf.TaskCompletion, false); Close(); }
            if (Widgets.ButtonText(new Rect(w + 12f, y, w, 32f), "WFL_ZS_NotNow".Translate())) Close();
            if (Widgets.ButtonText(new Rect(0f, y + 38f, w, 32f), "WFL_ZS_Always".Translate())) { FamilyLifeMod.Settings.zoneStorageAuto = 1; FamilyLifeMod.Settings.Write(); Messages.Message("WFL_ZS_Done".Translate(ZoneStorage.Apply(zone, profile)), MessageTypeDefOf.TaskCompletion, false); Close(); }
            if (Widgets.ButtonText(new Rect(w + 12f, y + 38f, w, 32f), "WFL_ZS_Never".Translate())) { FamilyLifeMod.Settings.zoneStorageAuto = 2; FamilyLifeMod.Settings.Write(); Close(); }
        }
    }
}
