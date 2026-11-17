using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    /// <summary>One adult who could be set up: how well each career suits them (0 = no suitable skills).</summary>
    public sealed class SetupCandidate { public int Person; public Dictionary<string, float> Fit = new Dictionary<string, float>(); }

    /// <summary>One suggested assignment.</summary>
    public sealed class SetupSuggestion { public int Person; public string Career; public float Fit; }

    /// <summary>How many people of the colony may be suggested for one career, and the lowest fit worth suggesting.</summary>
    public sealed class SetupPreset
    {
        public string Id; public float Share; public float MinFit;
        public static readonly SetupPreset Light = new SetupPreset { Id = "Light", Share = .12f, MinFit = 6f };
        public static readonly SetupPreset Balanced = new SetupPreset { Id = "Balanced", Share = .25f, MinFit = 4f };
        public static readonly SetupPreset Broad = new SetupPreset { Id = "Broad", Share = .5f, MinFit = 3f };
        public static readonly SetupPreset[] All = { Light, Balanced, Broad };
    }

    /// <summary>
    /// Suggests entry-level profession assignments for an existing colony. Pure: it only reads the fit scores it is given and returns suggestions; applying them is the player's click. Each person gets at most one
    /// suggestion, nobody is suggested for a career they do not suit, and no career takes more than its share of the colony (at least one place each). People who already hold a role are not offered.
    /// </summary>
    public static class QuickSetupPlanner
    {
        public static List<SetupSuggestion> Plan(IList<SetupCandidate> candidates, IList<string> careers, SetupPreset preset, IDictionary<string, int> alreadyHeld = null)
        {
            var result = new List<SetupSuggestion>(); if (candidates == null || careers == null || preset == null || candidates.Count == 0) return result;
            int cap = Math.Max(1, (int)Math.Ceiling(candidates.Count * preset.Share)); var taken = new HashSet<int>(); var count = new Dictionary<string, int>();
            foreach (var c in careers) count[c] = alreadyHeld != null && alreadyHeld.TryGetValue(c, out int n) ? n : 0;

            bool TryAdd(SetupCandidate who, string career, float fit)
            {
                if (taken.Contains(who.Person) || fit < preset.MinFit || count[career] >= cap) return false;
                taken.Add(who.Person); count[career]++; result.Add(new SetupSuggestion { Person = who.Person, Career = career, Fit = fit }); return true;
            }

            // first, every career that has nobody gets its best-suited person, so no profession is left empty
            foreach (var career in careers.OrderBy(c => c, StringComparer.Ordinal))
            {
                if (count[career] > 0) continue;
                var best = candidates.Where(p => !taken.Contains(p.Person)).OrderByDescending(p => p.Fit.TryGetValue(career, out var f) ? f : 0f).ThenBy(p => p.Person).FirstOrDefault();
                if (best != null) TryAdd(best, career, best.Fit.TryGetValue(career, out var fit) ? fit : 0f);
            }
            // then everyone else goes to their best career while it has room
            foreach (var who in candidates.Where(p => !taken.Contains(p.Person)).OrderByDescending(p => p.Fit.Values.DefaultIfEmpty(0f).Max()).ThenBy(p => p.Person))
                foreach (var pair in who.Fit.OrderByDescending(f => f.Value).ThenBy(f => f.Key, StringComparer.Ordinal))
                    if (count.ContainsKey(pair.Key) && TryAdd(who, pair.Key, pair.Value)) break;
            return result.OrderBy(s => s.Career, StringComparer.Ordinal).ThenByDescending(s => s.Fit).ToList();
        }

        /// <summary>How well skills match what a career asks for: the best average level over the skills one of its steps names, plus a little for passion. 0 when a career names no skills.</summary>
        public static float Fit(IEnumerable<IDictionary<string, int>> rules, Func<string, int> level, Func<string, int> passion)
        {
            float best = 0f;
            foreach (var rule in rules)
            {
                if (rule == null || rule.Count == 0) continue;
                float sum = 0f; foreach (var skill in rule.Keys) sum += level(skill) + passion(skill) * 2f;
                best = Math.Max(best, sum / rule.Count);
            }
            return best;
        }
    }
}
