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
        private static int firstSeen = -1; private static FamilyComponent cachedComponent;

        public static Color Colour(HealthLevel l) => l == HealthLevel.Bad ? CommunityWidgets.Poor : l == HealthLevel.Warn ? CommunityWidgets.Fair : CommunityWidgets.Good;

        private static string ModuleName(string id)
        {
            var m = FamilyFramework.Catalog.Modules.FirstOrDefault(x => x.Id == id);
            return m == null ? id : m.Name.Replace("Wil's Role Modules : ", "");
        }

        /// <summary>Every finding, worst first. Recomputed at most every 250 ticks.</summary>
        public static List<HealthNote> Collect(FamilyComponent c)
        {
            if (!ReferenceEquals(cachedComponent, c)) { cachedComponent = c; cachedAt = -9999; firstSeen = -1; cache.Clear(); }
            int now = FrameworkServices.Now; if (firstSeen < 0) firstSeen = now;
            if (now - cachedAt < 250 && now >= cachedAt) return cache;
            var notes = new List<HealthNote>(); var reg = c.Registry; var book = reg.Assignments(); bool longEnough = now - firstSeen > GainTracker.WindowTicks;
            foreach (var module in CareerService.Professions.All.Where(r => FamilyFramework.Catalog.IsEnabled(r.ModuleId)).GroupBy(r => r.ModuleId))
            {
                var holders = module.SelectMany(r => book.Holders(r.Id)).Distinct().Where(id => reg.GetPerson(id)?.InColony == true).ToList();
                int learning = holders.Count(id => module.Any(r => CareerService.RecentGain(id, r.Career, now) > 0)); string name = ModuleName(module.Key);
                switch (HealthRules.Staffing(holders.Count, learning, longEnough))
                {
                    case StaffReason.Unstaffed: break;
                    case StaffReason.Idle: break;
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

        private static bool showAll;
        public static float Draw(Rect r, float y, FamilyComponent c)
        {
            var notes = Collect(c).Where(n => n.Level != HealthLevel.Good).ToList();
            y = CommunityPageLayout.Line(r, y, notes.Count == 0 ? "WFL_Health_AllGood".Translate().ToString() : "WFL_UX_Attention".Translate(notes.Count).ToString());
            foreach (var n in notes.Take(showAll ? notes.Count : 3))
            {
                var pages = FamilyFramework.Pages.Visible().Where(p => p.ModuleId == n.Module).ToList();
                var page = pages.FirstOrDefault();
                float buttonW = 100f, textW = Mathf.Max(60f, r.width - buttonW - 36f);
                float h = Mathf.Max(56f, Text.CalcHeight(n.Text, textW) + 20f);
                Widgets.DrawBoxSolid(new Rect(0f, y, r.width, h), new Color(.15f, .16f, .17f));
                Widgets.DrawBoxSolid(new Rect(0f, y, 4f, h), Colour(n.Level));
                GUI.color = Colour(n.Level); Widgets.Label(new Rect(12f, y + 8f, 18f, 24f), n.Level == HealthLevel.Bad ? "!" : "•"); GUI.color = Color.white;
                Widgets.Label(new Rect(32f, y + 8f, textW - 12f, h - 12f), n.Text);
                TooltipHandler.TipRegion(new Rect(0f, y, r.width - buttonW - 8f, h), n.Hint ?? n.Text);
                if (page != null && Widgets.ButtonText(new Rect(r.width - buttonW - 8f, y + (h - 30f) / 2f, buttonW, 30f), "WFL_UX_Review".Translate())) MainTabWindow_Community.Open(page.Id);
                y += h + 6f;
            }
            if (notes.Count > 3) { if (Widgets.ButtonText(new Rect(0f, y, Mathf.Min(220f, r.width), 30f), (showAll ? "WFL_UX_ShowLess" : "WFL_UX_ShowAll").Translate())) showAll = !showAll; y += 36f; }
            return y + 8f;
        }
    }
}
