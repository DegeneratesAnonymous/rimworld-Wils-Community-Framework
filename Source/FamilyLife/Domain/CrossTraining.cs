using System;
using System.Collections.Generic;

namespace FamilyLife.Domain
{
    /// <summary>
    /// Practice in one career that also teaches a little of a related one: guard duty for a warden, first aid drills for a medic, teaching for a researcher. A module declares the rules for its own work and names the related
    /// career by id only, so neither module needs the other. The related career gets a share of the points, never the full amount, and never wages (it is learning, not work done in that career).
    /// </summary>
    public sealed class CrossTrainingRule
    {
        public string From, To, KindPrefix; public float Share;
    }

    public static class CrossTraining
    {
        public const float MaxShare = .5f;

        /// <summary>The extra credits one finished activity earns: (career, points). A rule matches the activity's career and, if it names one, the start of its kind. A share is clamped to <see cref="MaxShare"/>; a credit is at least 1 point.</summary>
        public static List<KeyValuePair<string, int>> Credits(IEnumerable<CrossTrainingRule> rules, string career, string kind, int points)
        {
            var list = new List<KeyValuePair<string, int>>(); if (rules == null || points <= 0 || string.IsNullOrEmpty(career)) return list;
            foreach (var r in rules)
            {
                if (r == null || r.From != career || string.IsNullOrEmpty(r.To) || r.To == career || r.Share <= 0f) continue;
                if (!string.IsNullOrEmpty(r.KindPrefix) && (kind == null || !kind.StartsWith(r.KindPrefix, StringComparison.Ordinal))) continue;
                int pts = Math.Max(1, (int)Math.Round(points * Math.Min(r.Share, MaxShare)));
                int at = list.FindIndex(k => k.Key == r.To);
                if (at >= 0) list[at] = new KeyValuePair<string, int>(r.To, Math.Max(list[at].Value, pts)); else list.Add(new KeyValuePair<string, int>(r.To, pts));          // two rules for one target do not add up
            }
            return list;
        }
    }
}
