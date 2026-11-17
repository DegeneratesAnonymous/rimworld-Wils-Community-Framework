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
    /// Two ways to see the professions beside the tree: People (each colonist with their main profession and side professions, an experience bar on each, and who is learning right now) and By role (each role with
    /// who holds it). A bar is how far along the person is toward the next step of that career; a green arrow and +N mean experience came in recently.
    /// </summary>
    internal static class ProfessionPeople
    {
        private static readonly Color Gold = FamilyKit.Gold, Gaining = new Color(.45f, .85f, .5f);

        private sealed class Held { public ProfessionRole Role; public CareerFocus Focus; public int Gain; public bool Main; }

        private static CareerFocus FocusOf(FamilyComponent c, Pawn pawn, ProfessionRole role)
        {
            var def = CareerService.Career(role.Career); if (def == null) return new CareerFocus();
            return CareerFocus.Of(c.Registry.Careers().Progress(pawn.thingIDNumber, def, CareerService.SkillsOf(pawn)), role.Specialty);
        }

        private static readonly Dictionary<int, KeyValuePair<int, List<Held>>> heldCache = new Dictionary<int, KeyValuePair<int, List<Held>>>();

        /// <summary>A person's professions with their progress. Working progress out reads the whole career ledger, so it is kept for two seconds instead of every frame.</summary>
        private static List<Held> HeldBy(FamilyComponent c, Pawn pawn, int now)
        {
            if (heldCache.TryGetValue(pawn.thingIDNumber, out var e) && now >= e.Key && now - e.Key < 120) return e.Value;
            var built = Build(c, pawn, now); if (heldCache.Count > 1000) heldCache.Clear();
            heldCache[pawn.thingIDNumber] = new KeyValuePair<int, List<Held>>(now, built); return built;
        }

        private static List<Held> Build(FamilyComponent c, Pawn pawn, int now)
        {
            var book = c.Registry.Assignments(); string main = book.PrimaryOf(pawn.thingIDNumber); var list = new List<Held>();
            foreach (var id in book.RolesOf(pawn.thingIDNumber))
            {
                var role = CareerService.Professions.ById(id); if (role == null || !FamilyFramework.Catalog.IsEnabled(role.ModuleId)) continue;
                list.Add(new Held { Role = role, Focus = FocusOf(c, pawn, role), Gain = CareerService.RecentGain(pawn.thingIDNumber, role.Career, now), Main = id == main });
            }
            return list.OrderByDescending(h => h.Main).ThenByDescending(h => h.Role.Level).ToList();
        }

        /// <summary>One bar: the fill, a label over it, and a green arrow with the recent gain when the person is learning.</summary>
        private static void XpBar(Rect r, Held h, string label)
        {
            var color = h.Gain > 0 ? Gaining : new Color(.38f, .64f, .85f);
            CommunityWidgets.Bar(r, h.Focus.Share, color, label + (h.Focus.Done ? "" : "  " + Mathf.RoundToInt(h.Focus.Share * 100f) + "%"));
            if (h.Gain > 0) { Text.Font = GameFont.Tiny; GUI.color = Gaining; Text.Anchor = TextAnchor.MiddleRight; Widgets.Label(new Rect(r.xMax - 46f, r.y, 44f, r.height), "▲+" + h.Gain); Text.Anchor = TextAnchor.UpperLeft; GUI.color = Color.white; Text.Font = GameFont.Small; }
            string tip = h.Focus.Done ? "WFL_Prof_BarDone".Translate().ToString() : "WFL_Prof_BarTip".Translate(h.Focus.Target == null ? "WFL_Prof_NextStep".Translate().ToString() : h.Focus.Target).ToString();
            TooltipHandler.TipRegion(r, tip + (h.Gain > 0 ? "\n" + "WFL_Prof_Learning".Translate(h.Gain) : ""));
        }

        private static void ChipMenu(FamilyComponent c, Pawn pawn, Held h)
        {
            var book = c.Registry.Assignments(); int id = pawn.thingIDNumber; var options = new List<FloatMenuOption>();
            if (!h.Main) options.Add(new FloatMenuOption("WFL_Prof_MakeMain".Translate(), () => book.SetPrimary(id, h.Role.Id)));
            options.Add(new FloatMenuOption("WFL_Prof_Remove".Translate(), () => book.Unassign(id, h.Role.Id)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public static float People(Rect r, float y, FamilyComponent c)
        {
            var reg = c.Registry; var book = reg.Assignments(); int now = FrameworkServices.Now; var people = new List<KeyValuePair<Pawn, List<Held>>>();
            foreach (int id in book.AllPeople().Distinct())
            {
                var pawn = c.TryGetPawn(id); if (pawn == null || pawn.Dead || reg.GetPerson(id)?.InColony != true) continue;
                var held = HeldBy(c, pawn, now); if (held.Count > 0) people.Add(new KeyValuePair<Pawn, List<Held>>(pawn, held));
            }
            y = CommunityStaffing.Draw(r, y, c, null);
            if (people.Count == 0) return CommunityPageLayout.Line(r, y, "WFL_Prof_NoPeople".Translate(), true);
            y = CommunityPageLayout.Line(r, y, "WFL_Prof_PeopleKey".Translate(), true);
            float nameW = Mathf.Max(120f, r.width * .17f), mainW = Mathf.Max(200f, r.width * .32f);
            foreach (var kv in people.OrderByDescending(p => p.Value.Any(h => h.Gain > 0)).ThenBy(p => p.Key.LabelShort))
            {
                var pawn = kv.Key; var held = kv.Value; var main = held.FirstOrDefault(h => h.Main) ?? held[0]; var sides = held.Where(h => h != main).ToList();
                float sideX = nameW + mainW + 8f, sideW = Mathf.Max(120f, (r.width - sideX) / 2f - 4f); int perRow = Mathf.Max(1, Mathf.FloorToInt((r.width - sideX) / (sideW + 4f))); int rows = Math.Max(1, (sides.Count + perRow - 1) / perRow);
                float h0 = Mathf.Max(48f, rows * 26f + 8f); var row = new Rect(0f, y, r.width, h0);
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, .035f)); if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                CommunityWidgets.NameButton(new Rect(6f, y, nameW - 8f, h0), pawn);
                var card = new Rect(nameW, y + 4f, mainW, h0 - 8f); Widgets.DrawBoxSolid(card, new Color(Gold.r, Gold.g, Gold.b, .10f)); Widgets.DrawBox(card);
                GUI.color = Gold; Text.Font = GameFont.Tiny; Widgets.Label(new Rect(card.x + 6f, card.y + 1f, card.width - 12f, 16f), "★ " + "WFL_Prof_Main".Translate() + ": " + CareerService.TitleOf(main.Role, reg)); Text.Font = GameFont.Small; GUI.color = Color.white;
                XpBar(new Rect(card.x + 6f, card.yMax - 20f, card.width - 12f, 16f), main, "");
                if (Widgets.ButtonInvisible(card)) ChipMenu(c, pawn, main);
                for (int i = 0; i < sides.Count; i++)
                {
                    var s = sides[i]; var chip = new Rect(sideX + (i % perRow) * (sideW + 4f), y + 4f + (i / perRow) * 26f, sideW, 22f);
                    XpBar(chip, s, CareerService.TitleOf(s.Role, reg).Truncate(sideW - 60f));
                    if (Widgets.ButtonInvisible(chip)) ChipMenu(c, pawn, s);
                }
                if (sides.Count == 0) { GUI.color = CommunityWidgets.Muted; Text.Anchor = TextAnchor.MiddleLeft; Widgets.Label(new Rect(sideX, y, r.width - sideX, h0), "WFL_Prof_NoSide".Translate()); Text.Anchor = TextAnchor.UpperLeft; GUI.color = Color.white; }
                y += h0 + 4f;
            }
            return y;
        }

        public static float ByRole(Rect r, float y, FamilyComponent c)
        {
            var reg = c.Registry; var book = reg.Assignments(); int now = FrameworkServices.Now;
            var roles = CareerService.Professions.All.Where(x => FamilyFramework.Catalog.IsEnabled(x.ModuleId)).OrderBy(x => x.ModuleId, StringComparer.Ordinal).ThenBy(x => x.Career, StringComparer.Ordinal).ThenBy(x => x.Level).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
            y = CommunityPageLayout.Line(r, y, "WFL_Prof_RoleKey".Translate(), true); string lastCareer = null;
            foreach (var role in roles)
            {
                var holders = book.Holders(role.Id).Distinct().Select(id => c.TryGetPawn(id)).Where(p => p != null && !p.Dead && reg.GetPerson(p.thingIDNumber)?.InColony == true).ToList();
                // The directory includes optional and unqualified roles so it can be searched by browsing.
                if (role.Career != lastCareer) { lastCareer = role.Career; var def = CareerService.Career(role.Career); y += 4f; GUI.color = Gold; Widgets.Label(new Rect(0f, y, r.width, 24f), (def != null ? CareerService.CareerName(def, reg) : role.Career).ToUpperInvariant()); GUI.color = Color.white; y += 26f; }
                int perRow = Mathf.Max(1, Mathf.FloorToInt((r.width - 200f) / 210f)); int rows = Math.Max(1, (holders.Count + perRow - 1) / perRow); float h0 = rows * 26f + 6f; var row = new Rect(0f, y, r.width, h0);
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, .035f));
                Text.Anchor = TextAnchor.MiddleLeft; Widgets.Label(new Rect(8f, y, 190f, h0), CareerService.TitleOf(role, reg).Truncate(180f) + "  (" + holders.Count + ")"); Text.Anchor = TextAnchor.UpperLeft;
                if (holders.Count == 0) { GUI.color = CommunityWidgets.Muted; Text.Anchor = TextAnchor.MiddleLeft; Widgets.Label(new Rect(204f, y, r.width - 210f, h0), "WFL_Prof_Vacant".Translate()); Text.Anchor = TextAnchor.UpperLeft; GUI.color = Color.white; }
                for (int i = 0; i < holders.Count; i++)
                {
                    var p = holders[i]; var h = HeldBy(c, p, now).FirstOrDefault(x => x.Role == role); if (h == null) continue;
                    var chip = new Rect(204f + (i % perRow) * 210f, y + 3f + (i / perRow) * 26f, 204f, 22f);
                    XpBar(chip, h, (h.Main ? "★ " : "") + p.LabelShort.Truncate(90f));
                    if (Widgets.ButtonInvisible(chip)) ChipMenu(c, p, h);
                }
                y += h0 + 3f;
            }
            return y;
        }
    }
}

