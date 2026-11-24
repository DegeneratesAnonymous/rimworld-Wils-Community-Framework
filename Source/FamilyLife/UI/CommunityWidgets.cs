using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// Shared table pieces for Community pages that manage people (prisoners, patients, pupils, researchers): a column header row, zebra rows, small bars, and a name that jumps to the pawn.
    /// Public so optional modules use the one look; nothing here changes the game.
    /// </summary>
    public static class CommunityWidgets
    {
        public const float RowHeight = 34f;
        public static readonly Color Gold = new Color(.89f, .72f, .29f), Good = new Color(.45f, .78f, .45f), Fair = new Color(.9f, .75f, .3f), Poor = new Color(.85f, .38f, .33f), Muted = new Color(.62f, .62f, .64f);

        /// <summary>Green when high, amber in the middle, red when low (health, mood, food).</summary>
        public static Color Level(float pct) => pct >= .66f ? Good : pct >= .33f ? Fair : Poor;

        /// <summary>A small filled bar with an optional label drawn over it.</summary>
        public static void Bar(Rect r, float pct, Color color, string label = null)
        {
            pct = Mathf.Clamp01(pct);
            Widgets.DrawBoxSolid(r, new Color(.12f, .12f, .13f));
            Widgets.DrawBoxSolid(new Rect(r.x, r.y, r.width * pct, r.height), new Color(color.r, color.g, color.b, .85f));
            if (!string.IsNullOrEmpty(label))
            {
                Text.Font = GameFont.Tiny; Text.Anchor = TextAnchor.MiddleCenter; Widgets.Label(r, label);
                Text.Anchor = TextAnchor.UpperLeft; Text.Font = GameFont.Small;
            }
        }

        /// <summary>One line of column titles; columns are given as (title, width) and laid out left to right. Returns the y below the header.</summary>
        public static float Header(Rect area, float y, IList<KeyValuePair<string, float>> columns)
        {
            float x = 0f; Text.Font = GameFont.Tiny; GUI.color = FamilyKit.Gold; Text.Anchor = TextAnchor.LowerLeft;
            foreach (var col in columns) { Widgets.Label(new Rect(x, y, col.Value, 18f), col.Key); x += col.Value; }
            Text.Anchor = TextAnchor.UpperLeft; Text.Font = GameFont.Small; GUI.color = Color.white;
            return y + 20f;
        }

        /// <summary>Background for row <paramref name="index"/>, with a hover highlight. Returns the row rect.</summary>
        public static Rect Row(Rect area, float y, int index, float height = RowHeight)
        {
            var row = new Rect(0f, y, area.width, height);
            if (index % 2 == 0) Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, .04f));
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
            return row;
        }

        /// <summary>A pawn's name that selects them and moves the camera on click.</summary>
        public static void NameButton(Rect r, Pawn pawn)
        {
            float size = Mathf.Min(32f, r.height);
            GUI.DrawTexture(new Rect(r.x, r.y, size, size), PortraitsCache.Get(pawn, new Vector2(size, size), Rot4.South, new Vector3(0f, 0f, .1f), 1.15f));
            Text.Anchor = TextAnchor.MiddleLeft; Widgets.Label(new Rect(r.x + size + 4f, r.y, Mathf.Max(0f, r.width - size - 4f), r.height), pawn.LabelShortCap.ToString().Truncate(Mathf.Max(1f, r.width - size - 4f))); Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(r)) { if (pawn.Spawned) CameraJumper.TryJumpAndSelect(pawn); else CameraJumper.TryJump(pawn); }
            TooltipHandler.TipRegion(r, pawn.LabelCap + "\n" + "WFL_Table_ClickToJump".Translate());
        }

        /// <summary>Plain text inside a cell, vertically centred.</summary>
        public static void Cell(Rect r, string text, Color? color = null, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Text.Anchor = anchor; if (color.HasValue) GUI.color = color.Value;
            Widgets.Label(r, text);
            GUI.color = Color.white; Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>A button that opens a menu of choices; the label shows the current one.</summary>
        public static void Choice(Rect r, string current, Func<IEnumerable<FloatMenuOption>> options)
        {
            if (Widgets.ButtonText(r, current)) { var list = new List<FloatMenuOption>(options()); if (list.Count > 0) Find.WindowStack.Add(new FloatMenu(list)); }
        }
    }
}

