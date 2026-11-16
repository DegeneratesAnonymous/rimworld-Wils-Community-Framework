using System;
using System.Collections.Generic;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// The Military alert button in the bottom bar. It is tinted by the current level (nothing at Normal, yellow, red). Left-click steps the level you set (Normal, Yellow, Red, back to Normal); right-click
    /// opens a menu to pick a level or open the Military menu. A Red that a threat raised stands down by itself whatever you click.
    /// </summary>
    public class MainButtonWorker_MilitaryAlert : MainButtonWorker
    {
        private static readonly Color YellowTint = new Color(0.95f, 0.8f, 0.2f, 0.40f), RedTint = new Color(0.85f, 0.2f, 0.2f, 0.50f);

        private static FamilyComponent Colony => FamilyComponent.Current != null && FamilyFramework.Catalog.IsEnabled(MilitaryCareers.Module) ? FamilyComponent.Current : null;

        public override bool Visible => Colony != null && Find.CurrentMap != null;

        private static void Set(FamilyComponent c, AlertLevel level)
        {
            MilitaryPage.Choose(c, level);
            Messages.Message("WFL_Military_AlertSet".Translate(AlertRules.Label(level)), MessageTypeDefOf.SilentInput, false);
        }

        public override void Activate() { var c = Colony; if (c != null) Set(c, AlertRules.Next(c.Registry.Alert().Set)); }

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

        public override void DoButton(Rect rect)
        {
            var c = Colony; if (c == null) return;
            var state = c.Registry.Alert(); var level = state.Effective;
            if (level != AlertLevel.Normal) Widgets.DrawBoxSolid(rect, level == AlertLevel.Red ? RedTint : YellowTint);
            if (Widgets.ButtonTextSubtle(rect, def.LabelCap)) Activate();
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(rect)) { Event.current.Use(); Menu(c); }
            TooltipHandler.TipRegion(rect, "WFL_Military_ButtonTip".Translate(AlertRules.Label(level), state.RaisedByThreat ? "WFL_Military_ByThreat".Translate().ToString() : "", AlertRules.Label(AlertRules.Next(state.Set))));
        }
    }
}
