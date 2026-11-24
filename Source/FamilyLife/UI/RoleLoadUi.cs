using System;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>The people-side of <see cref="RoleLoad"/>: how many professions someone carries against what their skill can carry, a chip that says so, and a confirm before stacking one more.</summary>
    internal static class RoleLoadUi
    {
        public static int Capacity(FamilyRegistry r, int id) => RoleLoad.Capacity(r.GetPerson(id)?.TopSkillLevel ?? 0);

        public static int Held(FamilyRegistry r, int id) =>
            r.Assignments().RolesOf(id).Select(x => CareerService.Professions.ById(x)?.Career).Where(s => !string.IsNullOrEmpty(s)).Distinct().Count();

        /// <summary>Assigns now, or first asks when this would be a profession beyond what the person's skill carries. The assignment is never refused.</summary>
        public static void Assign(FamilyRegistry r, Pawn pawn, int id, ProfessionRole role, Action assign)
        {
            bool newCareer = !r.Assignments().RolesOf(id).Any(x => CareerService.Professions.ById(x)?.Career == role.Career);
            int held = Held(r, id), cap = Capacity(r, id);
            if (newCareer && held + 1 > cap) Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("WFL_Load_Confirm".Translate(pawn?.LabelShort ?? "?", held, cap, RoleLoad.SkillStep), assign));
            else assign();
        }

        public static void Chip(Rect r, FamilyRegistry reg, int id)
        {
            int held = Held(reg, id), cap = Capacity(reg, id); var level = RoleLoad.Of(held, cap);
            var color = level == RoleLoad.Level.Over ? new Color(.95f, .4f, .35f) : level == RoleLoad.Level.Full ? new Color(.95f, .75f, .3f) : new Color(.5f, .85f, .5f);
            Text.Font = GameFont.Tiny; GUI.color = color; Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(r, held + "/" + cap + " " + ("WFL_Load_" + level).Translate());
            Text.Anchor = TextAnchor.UpperLeft; GUI.color = Color.white; Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(r, ("WFL_Load_Tip_" + level).Translate(held, cap, reg.GetPerson(id)?.TopSkillLevel ?? 0, RoleLoad.SkillStep));
        }
    }
}
