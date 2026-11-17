using System;
using System.Linq;
using FamilyLife.Domain;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>Module-local staffing entry point using the framework's existing assignment checks.</summary>
    public static class CommunityStaffing
    {
        public static float Draw(Rect area, float y, FamilyComponent component, string module)
        {
            return CommunityPageLayout.Button(area, y, "WFL_UX_ManageStaff".Translate(), () =>
            {
                var roles = CareerService.Professions.All.Where(r => (module == null || r.ModuleId == module) && FamilyFramework.Catalog.IsEnabled(r.ModuleId)).OrderBy(r => r.Level).ThenBy(r => r.Id).ToList();
                var options = roles.Select(role => new FloatMenuOption(CareerService.TitleOf(role, component.Registry), () => ProfessionTree.AssignMenu(component, role))).ToList();
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
            });
        }

        public static float Metric(Rect area, float y, string label, string value)
        {
            float h = Mathf.Max(66f, Text.CalcHeight(label, area.width - 24f) + 42f);
            Widgets.DrawBoxSolid(new Rect(0f, y, area.width, h), new Color(.15f, .16f, .17f));
            Widgets.Label(new Rect(12f, y + 6f, area.width - 24f, h - 38f), label);
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(12f, y + h - 34f, area.width - 24f, 32f), value); Text.Font = GameFont.Small;
            return y + h + 8f;
        }
    }
}
