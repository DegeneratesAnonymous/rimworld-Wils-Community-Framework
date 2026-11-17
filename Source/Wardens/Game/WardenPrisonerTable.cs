using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// The Prisoners page's table: every prisoner (and, with Ideology, slave) on the player's maps with mood, health, food, resistance and the way they are being handled, and a menu to change that.
    /// It reads the game's own state and sets only the game's own interaction mode, the same setting as the prisoner tab.
    /// </summary>
    public static class WardenPrisonerTable
    {
        public static float Draw(Rect s, FamilyComponent c)
        {
            var prisoners = new List<Pawn>(); var slaves = new List<Pawn>();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                prisoners.AddRange(map.mapPawns.PrisonersOfColony);
                if (ModsConfig.IdeologyActive) slaves.AddRange(map.mapPawns.SlavesOfColonySpawned);
            }
            float y = CommunityPageLayout.Line(s, 0f, "WFL_Wardens_PrisonersSummary".Translate(prisoners.Count, slaves.Count, prisoners.Count(p => p.Downed || p.health.HasHediffsNeedingTend(false))));
            if (prisoners.Count == 0 && slaves.Count == 0) return CommunityPageLayout.Line(s, y, "WFL_Wardens_PrisonersNone".Translate(), true);

            float w = s.width, nameW = Mathf.Max(110f, w * .17f), barW = Mathf.Max(70f, w * .11f), resW = Mathf.Max(90f, w * .13f);
            float modeW = Mathf.Max(130f, w - nameW - barW * 3f - resW);
            var cols = new List<KeyValuePair<string, float>>
            {
                new KeyValuePair<string, float>("WFL_Table_Name".Translate(), nameW), new KeyValuePair<string, float>("WFL_Table_Mood".Translate(), barW),
                new KeyValuePair<string, float>("WFL_Table_Health".Translate(), barW), new KeyValuePair<string, float>("WFL_Table_Food".Translate(), barW),
                new KeyValuePair<string, float>("WFL_Wardens_Resistance".Translate(), resW), new KeyValuePair<string, float>("WFL_Wardens_Handling".Translate(), modeW)
            };
            y = CommunityWidgets.Header(s, y, cols);
            int i = 0;
            foreach (var p in prisoners.OrderBy(p => p.LabelShort)) { Row(s, ref y, i++, p, false, nameW, barW, resW, modeW); }
            if (slaves.Count > 0)
            {
                y = CommunityPageLayout.Line(s, y + 6f, "WFL_Wardens_SlavesHeading".Translate(), true);
                foreach (var p in slaves.OrderBy(p => p.LabelShort)) { Row(s, ref y, i++, p, true, nameW, barW, resW, modeW); }
            }
            return CommunityPageLayout.Help(s, y + 4f, "WFL_Wardens_PrisonersHelp".Translate());
        }

        private static void Row(Rect s, ref float y, int index, Pawn p, bool slave, float nameW, float barW, float resW, float modeW)
        {
            var row = CommunityWidgets.Row(s, y, index); float x = 0f;
            bool trouble = p.Downed || p.health.HasHediffsNeedingTend(false);
            GUI.color = trouble ? CommunityWidgets.Poor : Color.white;
            CommunityWidgets.NameButton(new Rect(x + 4f, row.y, nameW - 6f, row.height), p); GUI.color = Color.white; x += nameW;
            float mood = p.needs?.mood?.CurLevelPercentage ?? 0f, food = p.needs?.food?.CurLevelPercentage ?? 0f, health = p.health.summaryHealth.SummaryHealthPercent;
            CommunityWidgets.Bar(new Rect(x, row.y + 6f, barW - 8f, row.height - 12f), mood, CommunityWidgets.Level(mood), p.needs?.mood != null ? mood.ToStringPercent() : "-"); x += barW;
            CommunityWidgets.Bar(new Rect(x, row.y + 6f, barW - 8f, row.height - 12f), health, CommunityWidgets.Level(health), health.ToStringPercent()); x += barW;
            CommunityWidgets.Bar(new Rect(x, row.y + 6f, barW - 8f, row.height - 12f), food, CommunityWidgets.Level(food), p.needs?.food != null ? food.ToStringPercent() : "-"); x += barW;
            if (slave)
            {
                var sup = p.needs?.TryGetNeed<Need_Suppression>();
                if (sup != null) CommunityWidgets.Bar(new Rect(x, row.y + 6f, resW - 8f, row.height - 12f), sup.CurLevelPercentage, CommunityWidgets.Level(sup.CurLevelPercentage), "WFL_Wardens_Suppression".Translate());
                x += resW;
                CommunityWidgets.Cell(new Rect(x, row.y, modeW - 6f, row.height), p.guest?.slaveInteractionMode?.LabelCap ?? "-", CommunityWidgets.Muted);
            }
            else
            {
                string resistance = p.guest != null ? p.guest.resistance.ToString("0.#") : "-";
                CommunityWidgets.Cell(new Rect(x, row.y, resW - 4f, row.height), resistance, p.guest != null && p.guest.resistance > 0.01f ? Color.white : CommunityWidgets.Good); x += resW;
                CommunityWidgets.Choice(new Rect(x, row.y + 1f, modeW - 8f - 40f, row.height - 2f), p.guest?.ExclusiveInteractionMode?.LabelCap ?? "-", () => Modes(p));
                var holds = FamilyComponent.Current?.Registry.ReleaseHolds(); var when = holds?.When(p.thingIDNumber) ?? ReleaseWhen.None;
                var hold = new Rect(x + modeW - 8f - 36f, row.y + 1f, 36f, row.height - 2f);
                if (holds != null)
                {
                    GUI.color = when != ReleaseWhen.None ? CommunityWidgets.Gold : Color.white;
                    if (Widgets.ButtonText(hold, when == ReleaseWhen.None ? "R" : "R*")) Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        new FloatMenuOption("WFL_Wardens_HoldHealthy".Translate(), () => holds.Set(p.thingIDNumber, ReleaseWhen.Healthy)),
                        new FloatMenuOption("WFL_Wardens_HoldWalk".Translate(), () => holds.Set(p.thingIDNumber, ReleaseWhen.CanWalk)),
                        new FloatMenuOption("WFL_Wardens_HoldNone".Translate(), () => holds.Set(p.thingIDNumber, ReleaseWhen.None))
                    }));
                    GUI.color = Color.white; TooltipHandler.TipRegion(hold, when == ReleaseWhen.None ? "WFL_Wardens_HoldTip".Translate().ToString() : ("WFL_Wardens_HoldActive_" + when).Translate().ToString());
                }
            }
            y += row.height;
        }

        private static IEnumerable<FloatMenuOption> Modes(Pawn p)
        {
            if (p.guest == null) yield break;
            foreach (var def in DefDatabase<PrisonerInteractionModeDef>.AllDefs.Where(d => !d.isNonExclusiveInteraction).OrderBy(d => d.listOrder))
            {
                if (p.IsWildMan() && !def.allowOnWildMan) continue;
                var mode = def;
                yield return new FloatMenuOption(mode.LabelCap, () => p.guest.SetExclusiveInteraction(mode));
            }
        }
    }
}
