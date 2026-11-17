using System;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>Shared, responsive section layout for framework and optional module pages.</summary>
    public static class CommunityPageLayout
    {
        public const float Gap = 16f;
        public static bool TwoColumns(Rect r) => r.width >= 680f;
        public static float ColumnWidth(Rect r) => TwoColumns(r) ? (r.width - Gap) / 2f : r.width;

        // Content uses local coordinates. Returning its actual height keeps long translations and lists scrollable.
        [ThreadStatic] private static System.Collections.Generic.List<string> pendingHelp;

        /// <summary>Explanatory text that would clutter the page: collected by the enclosing section and shown as a hover tip on its "?" mark. Takes no space.</summary>
        [ThreadStatic] private static int sectionDepth;

        public static float Help(Rect r, float y, string text)
        {
            if (string.IsNullOrEmpty(text)) return y;
            if (sectionDepth == 0) return Line(r, y, text, true);          // not inside a section: nowhere to hang a "?", so show it
            (pendingHelp ?? (pendingHelp = new System.Collections.Generic.List<string>())).Add(text);
            return y;
        }

        // Content uses local coordinates. Returning its actual height keeps long translations and lists scrollable.
        public static float Section(Rect r, float x, float y, float width, string title, Func<Rect, float> draw)
        {
            GUI.BeginGroup(new Rect(r.x + x, r.y + y, width, 10000f));
            var outer = pendingHelp; pendingHelp = null; sectionDepth++;
            try
            {
                float heading = Mathf.Max(28f, Text.CalcHeight(title, width - 40f) + 10f);
                Widgets.DrawBoxSolid(new Rect(0f, 0f, width, heading), new Color(.18f, .18f, .19f));
                GUI.color = FamilyKit.Gold;
                Widgets.Label(new Rect(8f, 5f, width - 40f, heading - 5f), title);
                GUI.color = Color.white;
                float used;
                GUI.BeginGroup(new Rect(8f, heading + 8f, width - 16f, 9900f));
                try { used = draw(new Rect(0f, 0f, width - 16f, r.height)); }
                finally { GUI.EndGroup(); }
                if (pendingHelp != null && pendingHelp.Count > 0)
                {
                    var mark = new Rect(width - 28f, (heading - 20f) / 2f, 20f, 20f);
                    Widgets.DrawBoxSolid(mark, Mouse.IsOver(mark) ? FamilyKit.Gold : new Color(.32f, .32f, .34f));
                    Text.Anchor = TextAnchor.MiddleCenter; GUI.color = Mouse.IsOver(mark) ? Color.black : Color.white; Widgets.Label(mark, "?");
                    Text.Anchor = TextAnchor.UpperLeft; GUI.color = Color.white;
                    TooltipHandler.TipRegion(mark, string.Join(Environment.NewLine + Environment.NewLine, pendingHelp));
                }
                return y + heading + 8f + used + Gap;
            }
            finally { pendingHelp = outer; sectionDepth--; GUI.EndGroup(); GUI.color = Color.white; }
        }

        public static float Line(Rect r, float y, string text, bool dim = false)
        {
            float height = Text.CalcHeight(text, r.width);
            if (dim) GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, y, r.width, height), text);
            GUI.color = Color.white;
            return y + height + 6f;
        }

        public static float Toggle(Rect r, float y, string text, ref bool value, bool enabled = true, string locked = null)
        {
            var row = new Rect(0f, y, r.width, 26f);
            if (enabled) Widgets.CheckboxLabeled(row, text, ref value);
            else
            {
                GUI.color = Color.gray; Widgets.Label(row, text + (string.IsNullOrEmpty(locked) ? "" : " - " + locked)); GUI.color = Color.white;
            }
            return y + 30f;
        }

        public static float Stepper(Rect r, float y, string label, int value, int step, int min, int max, Action<int> set, string suffix = "")
        {
            float controls = Mathf.Min(180f, r.width * .44f), labelWidth = r.width - controls - 8f;
            Widgets.Label(new Rect(0f, y + 3f, labelWidth, 25f), label);
            float x = r.width - controls, button = 34f, valueWidth = controls - button * 2f - 8f;
            if (Widgets.ButtonText(new Rect(x, y, button, 28f), "-") && value - step >= min) set(value - step);
            Text.Anchor = TextAnchor.MiddleCenter; Widgets.Label(new Rect(x + button + 4f, y, valueWidth, 28f), value + suffix); Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(new Rect(r.width - button, y, button, 28f), "+") && value + step <= max) set(value + step);
            return y + 34f;
        }

        public static float Slider(Rect r, float y, string label, ref int value, int min, int max, string suffix = "")
        {
            float labelWidth = Mathf.Min(250f, r.width * .48f);
            Widgets.Label(new Rect(0f, y + 2f, labelWidth, 24f), label + ": " + value + suffix);
            value = (int)Math.Round(Widgets.HorizontalSlider(new Rect(labelWidth + 8f, y + 3f, r.width - labelWidth - 8f, 20f), value, min, max, true));
            return y + 30f;
        }

        public static float Tabs(Rect r, float y, System.Collections.Generic.IReadOnlyList<string> labels, int selected, Action<int> choose)
        {
            float x = 0f, rowHeight = 32f;
            for (int i = 0; i < labels.Count; i++)
            {
                float width = Mathf.Max(86f, Text.CalcSize(labels[i]).x + 22f);
                if (x > 0f && x + width > r.width) { x = 0f; y += rowHeight; }
                var color = i == selected ? FamilyKit.Gold : Color.white;
                if (Widgets.ButtonText(new Rect(x, y, width, 28f), labels[i], true, true, color)) choose(i);
                x += width + 4f;
            }
            return y + rowHeight + 4f;
        }

        public static float ButtonGrid(Rect r, float y, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, Action>> actions, int columns = 2)
        {
            columns = Math.Max(1, columns); float gap = 6f, width = (r.width - gap * (columns - 1)) / columns, rowHeight = 34f; int index = 0;
            foreach (var action in actions)
            {
                int column = index % columns; int row = index / columns;
                if (Widgets.ButtonText(new Rect(column * (width + gap), y + row * rowHeight, width, 28f), action.Key)) action.Value();
                index++;
            }
            return y + (int)Math.Ceiling(index / (float)columns) * rowHeight;
        }

        public static float Button(Rect r, float y, string text, Action action)
        {
            float height = Mathf.Max(30f, Text.CalcHeight(text, r.width - 20f) + 10f);
            if (Widgets.ButtonText(new Rect(0f, y, r.width, height), text)) action();
            return y + height + 6f;
        }
    }
}
