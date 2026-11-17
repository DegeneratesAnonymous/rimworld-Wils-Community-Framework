using System;
using System.Collections.Generic;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>Whether the placed positions and patrol routes are drawn on the map outside of placing and the Military menu. A view choice only; it changes nothing in the game.</summary>
    public static class MilitaryView { public static bool ShowPositions = true; }

    /// <summary>
    /// The Military icons in the bottom-right icon cluster: the alert (tinted by the current level; left-click steps Normal, Yellow, Red, back to Normal; right-click picks a level or opens the Military menu)
    /// and a toggle that hides the placed positions and routes for a cleaner map. A Red that a threat raised stands down by itself whatever you click.
    /// </summary>
    public static class MilitaryPlayControls
    {
        private static readonly Color YellowTint = new Color(0.95f, 0.8f, 0.2f, 0.55f), RedTint = new Color(0.85f, 0.2f, 0.2f, 0.65f);
        private static Texture2D alertIcon, postsIcon;
        private static Texture2D AlertIcon => alertIcon ?? (alertIcon = ContentFinder<Texture2D>.Get("WilsFamilyLife/Pages/military", false) ?? BaseContent.BadTex);
        private static Texture2D PostsIcon => postsIcon ?? (postsIcon = ContentFinder<Texture2D>.Get("WilsFamilyLife/Pages/posts", false) ?? TexButton.ShowZones);

        private static FamilyComponent Colony => FamilyComponent.Current != null && FamilyFramework.Catalog.IsEnabled(MilitaryCareers.Module) ? FamilyComponent.Current : null;

        private static void Set(FamilyComponent c, AlertLevel level)
        {
            MilitaryPage.Choose(c, level);
            Messages.Message("WFL_Military_AlertSet".Translate(AlertRules.Label(level)), MessageTypeDefOf.SilentInput, false);
        }

        private static void Menu(FamilyComponent c)
        {
            var options = new List<FloatMenuOption>();
            foreach (AlertLevel level in Enum.GetValues(typeof(AlertLevel)))
            {
                var chosen = level; options.Add(new FloatMenuOption(AlertRules.Label(level) + " - " + ("WFL_Military_AlertExplain_" + level).Translate(), () => Set(c, chosen)));
            }
            options.Add(new FloatMenuOption("WFL_Military_OpenMenu".Translate(), () => MainTabWindow_Community.Open(MilitaryCareers.Module + ".alert")));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public static void Draw(WidgetRow row)
        {
            var c = Colony; if (c == null) return;
            var state = c.Registry.Alert(); var level = state.Effective;
            string tip = "WFL_Military_ButtonTip".Translate(AlertRules.Label(level), state.RaisedByThreat ? "WFL_Military_ByThreat".Translate().ToString() : "", AlertRules.Label(AlertRules.Next(state.Set)));
            var rect = row.ButtonIconRect(24f);
            Color? back = level == AlertLevel.Red ? RedTint : level == AlertLevel.Yellow ? YellowTint : (Color?)null;
            if (row.ButtonIcon(AlertIcon, tip, null, back)) Set(c, AlertRules.Next(state.Set));
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(rect)) { Event.current.Use(); Menu(c); }
            row.ToggleableIcon(ref MilitaryView.ShowPositions, PostsIcon, "WFL_Military_ShowPositionsTip".Translate(), SoundDefOf.Mouseover_ButtonToggle);
        }
    }
}
