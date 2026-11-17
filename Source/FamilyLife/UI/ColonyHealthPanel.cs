using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// The community at a glance: one line per finding, worst first, with a green, amber or red dot, so the player can see straight away what is working well and what is not. Findings come from the framework's own checks
    /// (is each module's profession staffed, is anyone doing the work, are needs going unmet) and from checks each module adds for itself.
    /// </summary>
    internal static class ColonyHealthPanel
    {
        private static int cachedAt = -9999; private static List<HealthNote> cache = new List<HealthNote>();
        private static int firstSeen = -1;

        public static Color Colour(HealthLevel l) => l == HealthLevel.Bad ? CommunityWidgets.Poor : l == HealthLevel.Warn ? CommunityWidgets.Fair : CommunityWidgets.Good;

        private static string ModuleName(string id)
        {
            var m = FamilyFramework.Catalog.Modules.FirstOrDefault(x => x.Id == id);
            return m == null ? id : m.Name.Replace("Wil's Role Modules : ", "");
        }

        /// <summary>Every finding, worst first. Recomputed at most every 250 ticks.</summary>
        public static List<HealthNote> Collect(FamilyComponent c)
        {
            int now = FrameworkServices.Now; if (firstSeen < 0) firstSeen = now;
            if (now - cachedAt < 250 && now >= cachedAt) return cache;
            var notes = new List<HealthNote>(); var reg = c.Registry; var book = reg.Assignments(); bool longEnough = now - firstSeen > GainTracker.WindowTicks;
            foreach (var module in CareerService.Professions.All.Where(r => FamilyFramework.Catalog.IsEnabled(r.ModuleId)).GroupBy(r => r.ModuleId))
            {
                var holders = module.SelectMany(r => book.Holders(r.Id)).Distinct().Where(id => reg.GetPerson(id)?.InColony == true).ToList();
                int learning = holders.Count(id => module.Any(r => CareerService.RecentGain(id, r.Career, now) > 0)); string name = ModuleName(module.Key);
                switch (HealthRules.Staffing(holders.Count, learning, longEnough))
                {
                    case StaffReason.Unstaffed: notes.Add(HealthNote.Warn(module.Key, "WFL_Health_Unstaffed".Translate(name), "WFL_Health_UnstaffedHint".Translate())); break;
                    case StaffReason.Idle: notes.Add(HealthNote.Warn(module.Key, "WFL_Health_Idle".Translate(name, holders.Count), "WFL_Health_IdleHint".Translate())); break;
                    default: notes.Add(HealthNote.Good(module.Key, "WFL_Health_Working".Translate(name, holders.Count, learning))); break;
                }
            }
            foreach (var n in reg.Needs().Open.Where(n => FamilyFramework.Catalog.IsEnabled(n.Module)))
            {
                var text = n.LabelKey.Translate().ToString(); int age = now - n.PostedTick;
                notes.Add(new HealthNote { Module = n.Module, Level = HealthRules.NeedLevel(age), Text = "WFL_Health_Need".Translate(text, Mathf.Max(0, age / 60000)), Hint = "WFL_Health_NeedHint".Translate() });
            }
            foreach (var check in FamilyFramework.HealthChecks.Enabled())
                try { notes.AddRange(check(c) ?? Enumerable.Empty<HealthNote>()); } catch (Exception ex) { Log.WarningOnce("[Wil's Family Life] A health check was skipped: " + ex.Message, 7710212); }
            cache = HealthRules.Order(notes); cachedAt = now; return cache;
        }

        public static float Draw(Rect r, float y, FamilyComponent c)
        {
            var notes = Collect(c); var worst = HealthRules.Worst(notes); int bad = notes.Count(n => n.Level == HealthLevel.Bad), warn = notes.Count(n => n.Level == HealthLevel.Warn);
            var banner = new Rect(0f, y, r.width, 40f); Widgets.DrawBoxSolid(banner, new Color(.13f, .13f, .15f)); Widgets.DrawBoxSolid(new Rect(0f, y, 6f, banner.height), Colour(worst));
            string summary = worst == HealthLevel.Good ? "WFL_Health_AllGood".Translate().ToString() : bad > 0 ? "WFL_Health_Problems".Translate(bad, warn).ToString() : "WFL_Health_Watch".Translate(warn).ToString();
            CommunityWidgets.Cell(new Rect(18f, y, r.width - 28f, banner.height), summary, Colour(worst));
            TooltipHandler.TipRegion(banner, "WFL_Health_Help".Translate());
            y += banner.height + 6f; int i = 0; int shownGood = 0;
            foreach (var n in notes)
            {
                if (n.Level == HealthLevel.Good && ++shownGood > 6 && worst != HealthLevel.Good) continue;          // when there are problems, keep the good news short
                var row = CommunityWidgets.Row(r, y, i++, 26f);
                Widgets.DrawBoxSolid(new Rect(8f, y + 8f, 10f, 10f), Colour(n.Level));
                var textRect = new Rect(26f, y, r.width - 34f, 26f); CommunityWidgets.Cell(textRect, n.Text.Truncate(textRect.width), n.Level == HealthLevel.Good ? CommunityWidgets.Muted : Color.white);
                TooltipHandler.TipRegion(row, (ModuleName(n.Module)) + (string.IsNullOrEmpty(n.Hint) ? "" : "\n" + n.Hint) + "\n" + "WFL_Health_Open".Translate());
                if (Widgets.ButtonInvisible(row)) { var page = FamilyFramework.Pages.Visible().FirstOrDefault(p => p.ModuleId == n.Module && p.Id != n.Module + ".overview"); if (page != null) MainTabWindow_Community.Open(page.Id); }
                y += 26f;
            }
            return y + 8f;
        }
    }
}
