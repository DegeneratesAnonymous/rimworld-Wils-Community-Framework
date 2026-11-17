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
    /// <summary>The Today page: the colony alert, and who is doing what across every module right now, with who is on a duty, who is free, and who holds roles in so many modules they are likely overbooked.</summary>
    internal static class TodayPage
    {
        public static int OverbookedModules => FamilyLifeMod.Settings?.overbookedAt ?? 3;

        private static string ModuleName(string id)
        {
            var m = FamilyFramework.Catalog.Modules.FirstOrDefault(x => x.Id == id);
            return m == null ? id : m.Name.Replace("Wil's Role Modules : ", "");
        }

        private static Color AlertColour(ColonyAlertLevel l) => l == ColonyAlertLevel.Red ? CommunityWidgets.Poor : l == ColonyAlertLevel.Yellow ? CommunityWidgets.Fair : CommunityWidgets.Good;

        private sealed class Row { public int Id; public Pawn Pawn; public List<ProfessionRole> Roles; public int Modules; public string Holder; }

        public static float Draw(Rect r, FamilyComponent c)
        {
            var reg = c.Registry; var book = reg.Assignments(); var rows = new List<Row>();
            foreach (int id in book.AllPeople().Distinct())
            {
                var pawn = c.TryGetPawn(id); if (pawn == null || pawn.Dead || reg.GetPerson(id)?.InColony != true) continue;
                var roles = book.RolesOf(id).Select(x => CareerService.Professions.ById(x)).Where(x => x != null && FamilyFramework.Catalog.IsEnabled(x.ModuleId)).ToList(); if (roles.Count == 0) continue;
                rows.Add(new Row { Id = id, Pawn = pawn, Roles = roles, Modules = roles.Select(x => x.ModuleId).Distinct().Count(), Holder = FrameworkServices.DutyHolder(pawn) });
            }
            int onDuty = rows.Count(x => x.Holder != null), overbooked = rows.Count(x => x.Modules >= OverbookedModules);
            var level = FrameworkServices.ColonyAlertPublished; string by = FrameworkServices.ColonyAlertRaisedBy;

            float y = CommunityPageLayout.Section(r, 0f, 0f, r.width, "WFL_Today_Status".Translate(), s =>
            {
                var chip = new Rect(0f, 0f, 150f, 30f); Widgets.DrawBoxSolid(chip, AlertColour(level) * new Color(1f, 1f, 1f, .35f));
                CommunityWidgets.Cell(chip, ("WFL_Today_Level_" + level).Translate(), AlertColour(level), TextAnchor.MiddleCenter);
                TooltipHandler.TipRegion(chip, ("WFL_Today_LevelTip_" + level).Translate(by == null ? "" : ModuleName(by)));
                CommunityWidgets.Cell(new Rect(162f, 0f, s.width - 162f, 30f), "WFL_Today_Counts".Translate(rows.Count, onDuty, rows.Count - onDuty), CommunityWidgets.Muted);
                CommunityPageLayout.Help(s, 0f, "WFL_Today_Help".Translate());
                float h = 36f;
                if (overbooked > 0) h = CommunityPageLayout.Line(s, h, "WFL_Today_Overbooked".Translate(overbooked, OverbookedModules));
                return h;
            });

            var needs = reg.Needs().Open.Where(n => FamilyFramework.Catalog.IsEnabled(n.Module)).OrderBy(n => n.PostedTick).ToList();
            if (needs.Count > 0)
                y = CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Need_Title".Translate(needs.Count), s =>
                {
                    CommunityPageLayout.Help(s, 0f, "WFL_Need_Help".Translate());
                    float h = 0f; int i = 0; int now = FrameworkServices.Now;
                    foreach (var n in needs)
                    {
                        var row = CommunityWidgets.Row(s, h, i++, 28f); float btn = 70f, ageW = 80f, whoW = Mathf.Max(90f, s.width * .15f), textW = s.width - btn * 3f - ageW - whoW - 8f;
                        string text = n.LabelKey.Translate().ToString() + (string.IsNullOrEmpty(n.Detail) ? "" : " (" + n.Detail + ")");
                        CommunityWidgets.Cell(new Rect(6f, row.y, textW, row.height), text.Truncate(textW), Color.white);
                        CommunityWidgets.Cell(new Rect(6f + textW, row.y, whoW, row.height), ModuleName(n.Module), CommunityWidgets.Muted);
                        CommunityWidgets.Cell(new Rect(6f + textW + whoW, row.y, ageW, row.height), "WFL_Need_Age".Translate(Mathf.Max(0, (now - n.PostedTick) / 60000)), CommunityWidgets.Muted);
                        var plan = new Rect(s.width - btn * 3f - 6f, row.y + 2f, btn - 2f, row.height - 4f); var go = new Rect(s.width - btn * 2f - 4f, row.y + 2f, btn - 2f, row.height - 4f); var dismiss = new Rect(s.width - btn, row.y + 2f, btn - 2f, row.height - 4f);
                        if (FrameworkServices.TryNeedAction(n.Key, out var planKey, out var planAction) && Widgets.ButtonText(plan, planKey.Translate())) planAction();
                        if (n.HasPlace) { if (Widgets.ButtonText(go, "WFL_Need_Go".Translate())) { var map = Find.Maps.FirstOrDefault(m => m.uniqueID == n.Map); if (map != null) CameraJumper.TryJump(new IntVec3(n.X, 0, n.Z), map); } }
                        if (Widgets.ButtonText(dismiss, "WFL_Need_Dismiss".Translate())) { reg.Needs().Dismiss(n.Key); break; }
                        TooltipHandler.TipRegion(dismiss, "WFL_Need_DismissTip".Translate());
                        h += row.height;
                    }
                    return h;
                });
            return CommunityPageLayout.Section(r, 0f, y, r.width, "WFL_Today_People".Translate(), s =>
            {
                if (rows.Count == 0) return CommunityPageLayout.Line(s, 0f, "WFL_Today_Nobody".Translate(), true);
                float w = s.width, nameW = Mathf.Max(110f, w * .2f), rolesW = w * .32f, statusW = 110f, doingW = w - nameW - rolesW - statusW;
                float h = CommunityWidgets.Header(s, 0f, new List<KeyValuePair<string, float>> { new KeyValuePair<string, float>("WFL_Table_Name".Translate(), nameW), new KeyValuePair<string, float>("WFL_Today_Roles".Translate(), rolesW), new KeyValuePair<string, float>("WFL_Today_Doing".Translate(), doingW), new KeyValuePair<string, float>("", statusW) });
                int i = 0;
                foreach (var x in rows.OrderByDescending(q => q.Modules >= OverbookedModules).ThenByDescending(q => q.Holder != null).ThenBy(q => q.Pawn.LabelShort).Take(60))
                {
                    var row = CommunityWidgets.Row(s, h, i++);
                    CommunityWidgets.NameButton(new Rect(4f, row.y, nameW - 6f, row.height), x.Pawn);
                    var rolesRect = new Rect(nameW, row.y, rolesW - 6f, row.height);
                    CommunityWidgets.Cell(rolesRect, string.Join(", ", x.Roles.Select(q => CareerService.TitleOf(q, reg))).Truncate(rolesRect.width), Color.white);
                    TooltipHandler.TipRegion(rolesRect, string.Join("\n", x.Roles.Select(q => CareerService.TitleOf(q, reg) + " - " + ModuleName(q.ModuleId))));
                    string report = x.Pawn.jobs?.curDriver?.GetReport(); string doing = x.Holder != null ? ModuleName(x.Holder) + (string.IsNullOrEmpty(report) ? "" : ": " + report) : (string.IsNullOrEmpty(report) ? "WFL_Today_Idle".Translate().ToString() : report);
                    var mate = FrameworkServices.CompanionOf(x.Pawn); var boss = FrameworkServices.LeaderOf(x.Pawn);
                    if (mate != null) doing += " (" + "WFL_Today_With".Translate(mate.LabelShort) + ")"; else if (boss != null) doing += " (" + "WFL_Today_Joining".Translate(boss.LabelShort) + ")";
                    var doingRect = new Rect(nameW + rolesW, row.y, doingW - 6f, row.height);
                    CommunityWidgets.Cell(doingRect, doing.Truncate(doingRect.width), x.Holder != null ? CommunityWidgets.Gold : CommunityWidgets.Muted);
                    var tag = new Rect(w - statusW, row.y, statusW - 4f, row.height);
                    if (x.Modules >= OverbookedModules) { CommunityWidgets.Cell(tag, "WFL_Today_Busy".Translate(), CommunityWidgets.Fair, TextAnchor.MiddleRight); TooltipHandler.TipRegion(tag, "WFL_Today_BusyTip".Translate(x.Modules)); }
                    else if (x.Holder != null) CommunityWidgets.Cell(tag, "WFL_Today_OnDuty".Translate(), CommunityWidgets.Gold, TextAnchor.MiddleRight);
                    h += row.height;
                }
                return h;
            });
        }
    }
}
