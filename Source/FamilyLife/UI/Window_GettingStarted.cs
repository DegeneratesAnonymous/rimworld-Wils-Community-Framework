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

        private sealed class Step { public string Key; public Func<FamilyComponent, bool> Done; public Action<FamilyComponent> Go; public Func<IEnumerable<string>> Glow; }

        private static readonly List<Step> Steps = new List<Step>
        {
            new Step { Key = "Zone", Done = c => Find.Maps.Any(m => m.zoneManager.AllZones.OfType<Zone_Family>().Any()), Go = c => FamilyZones.StartPainting(), Glow = TourTags.ForZone },
            new Step { Key = "Family", Done = c => c.Registry.LineagesWithZones().Any(), Go = c => OpenFamilies(), Glow = () => new[] { TourTags.ChooseFamily } },
            new Step { Key = "Storage", Done = c => c.Registry.FamilyStores.Count > 0, Go = c => Messages.Message("WFL_GS_StorageHint".Translate(), MessageTypeDefOf.NeutralEvent, false), Glow = () => new[] { TourTags.FamilyStorage } },
            new Step { Key = "Roles", Done = c => c.Registry.Assignments().Count > 0, Go = c => Find.WindowStack.Add(new Window_QuickSetup(c)), Glow = () => new[] { TourTags.CommunityTab, TourTags.QuickSetup } },
            new Step { Key = "Window", Done = c => Find.WindowStack.WindowOfType<Window_Families>() != null || opened, Go = c => OpenFamilies(), Glow = () => new[] { TourTags.CommunityTab } },
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
            int done = 0; bool lit = false;
            foreach (var step in Steps)
            {
                bool ok = false; try { ok = step.Done(comp); } catch { }
                if (ok) done++;
                // The first step still to do glows where it lives: the button on the bottom bar, in the Architect menu, or on the selected building.
                if (!ok && !lit && step.Glow != null) { lit = true; try { foreach (var tag in step.Glow()) if (!string.IsNullOrEmpty(tag)) UIHighlighter.HighlightTag(tag); } catch (Exception ex) { Log.Warning("[Wil's Family Life] Guide highlight skipped: " + ex.Message); } }
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

    /// <summary>The names the game uses to make a button glow. Read from real instances, so they always match the game's own spelling for this version.</summary>
    internal static class TourTags
    {
        public const string QuickSetup = "WFL_Tour_QuickSetup";
        private static string Gizmo(string tutor) => new Command_Action { tutorTag = tutor }.HighlightTag;
        public static string ChooseFamily => Gizmo("WFL_ChooseFamily");
        public static string FamilyStorage => Gizmo("WFL_FamilyStorage");
        public static string CommunityTab => CommunityDefOf.WFL_Community?.cachedHighlightTagClosed;
        public static IEnumerable<string> ForZone()
        {
            yield return MainButtonDefOf.Architect?.cachedHighlightTagClosed;
            var zone = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("Zone");
            var field = zone == null ? null : HarmonyLib.AccessTools.Field(typeof(DesignationCategoryDef), "cachedHighlightClosedTag");
            yield return field?.GetValue(zone) as string;
            yield return new Designator_ZoneAdd_Family().HighlightTag;
        }
    }
}
