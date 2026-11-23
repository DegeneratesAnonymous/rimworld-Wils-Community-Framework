using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// An optional guide for a new colony: five steps, each ticking itself off when the colony really has it, with a button that takes the player straight to it. It is offered once (never forced), can be
    /// closed at any point, and can be opened again from the mod settings.
    /// </summary>
    public class Window_GettingStarted : Window
    {
        private const string OfferedKey = "gettingstarted:offered";

        private sealed class Step { public string Key; public Func<FamilyComponent, bool> Done; public Action<FamilyComponent> Go; }

        private static readonly List<Step> Steps = new List<Step>
        {
            new Step { Key = "Zone", Done = c => Find.Maps.Any(m => m.zoneManager.AllZones.OfType<Zone_Family>().Any()), Go = c => FamilyZones.StartPainting() },
            new Step { Key = "Family", Done = c => c.Registry.LineagesWithZones().Any(), Go = c => OpenFamilies() },
            new Step { Key = "Storage", Done = c => c.Registry.FamilyStores.Count > 0, Go = c => Messages.Message("WFL_GS_StorageHint".Translate(), MessageTypeDefOf.NeutralEvent, false) },
            new Step { Key = "Roles", Done = c => c.Registry.Assignments().Count > 0, Go = c => Find.WindowStack.Add(new Window_QuickSetup(c)) },
            new Step { Key = "Window", Done = c => Find.WindowStack.WindowOfType<Window_Families>() != null || opened, Go = c => OpenFamilies() },
        };
        private static bool opened;

        private readonly FamilyComponent comp;
        public override Vector2 InitialSize => new Vector2(520f, 440f);

        public Window_GettingStarted(FamilyComponent comp)
        {
            this.comp = comp; doCloseX = true; forcePause = false; absorbInputAroundWindow = false; draggable = true;
        }

        private static void OpenFamilies() { opened = true; if (Find.WindowStack.WindowOfType<Window_Families>() == null) Find.WindowStack.Add(new Window_Families()); }

        /// <summary>Offers the guide once to a colony that has no family zone yet, a day or so in. Either answer ends the offer; the settings button always opens it.</summary>
        public static void OfferOnce(FamilyComponent c)
        {
            var s = FamilyLifeMod.Settings; if (s != null && !s.offerGuide) return;
            var book = c.Registry.Assignments();
            if (book.HasMigrated(OfferedKey) || Find.TickManager.TicksGame < ActivityTuning.TicksPerDay || Find.CurrentMap == null) return;
            if (Find.Maps.Any(m => m.zoneManager.AllZones.OfType<Zone_Family>().Any()) || Find.WindowStack.WindowOfType<Window_GettingStarted>() != null) { book.MarkMigrated(OfferedKey); return; }
            book.MarkMigrated(OfferedKey);
            Find.WindowStack.Add(new Dialog_MessageBox("WFL_GS_Offer".Translate(), "WFL_GS_Yes".Translate(), () => Find.WindowStack.Add(new Window_GettingStarted(c)), "WFL_GS_No".Translate(), null, "WFL_GS_Title".Translate()));
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "WFL_GS_Title".Translate()); Text.Font = GameFont.Small;
            float y = 40f;
            Widgets.Label(new Rect(0f, y, inRect.width, 44f), "WFL_GS_Intro".Translate()); y += 52f;
            int done = 0;
            foreach (var step in Steps)
            {
                bool ok = false; try { ok = step.Done(comp); } catch { }
                if (ok) done++;
                var row = new Rect(0f, y, inRect.width, 56f);
                Widgets.DrawBoxSolid(new Rect(row.x, row.y, 4f, row.height - 6f), ok ? new Color(.45f, .8f, .45f) : new Color(.6f, .6f, .6f));
                Widgets.Label(new Rect(14f, y, inRect.width - 150f, 24f), (ok ? "✓ " : "") + ("WFL_GS_" + step.Key).Translate());
                GUI.color = Color.gray; Widgets.Label(new Rect(14f, y + 22f, inRect.width - 150f, 32f), ("WFL_GS_" + step.Key + "Why").Translate()); GUI.color = Color.white;
                if (Widgets.ButtonText(new Rect(inRect.width - 128f, y + 8f, 124f, 28f), ok ? "WFL_GS_Again".Translate() : "WFL_GS_Show".Translate())) step.Go(comp);
                y += 62f;
            }
            Widgets.Label(new Rect(0f, inRect.height - 28f, inRect.width, 24f), "WFL_GS_Progress".Translate(done, Steps.Count));
        }
    }
}
