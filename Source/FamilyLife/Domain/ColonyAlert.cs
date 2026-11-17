using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    /// <summary>How alert the colony is as a whole. Published by whichever module raises it (Military) and read by every module, so each can adapt without knowing about the others.</summary>
    public enum ColonyAlertLevel : byte { Normal = 0, Yellow = 1, Red = 2 }

    /// <summary>The published alert levels, one per module. The colony's level is the highest of them; a module that is stopped or absent publishes nothing. Pure and never saved: the publisher republishes each pass.</summary>
    public sealed class ColonyAlertBoard
    {
        private readonly Dictionary<string, ColonyAlertLevel> published = new Dictionary<string, ColonyAlertLevel>();

        public void Publish(string module, ColonyAlertLevel level) { if (!string.IsNullOrEmpty(module)) published[module] = level; }
        public void Withdraw(string module) { if (module != null) published.Remove(module); }
        public void Clear() => published.Clear();

        /// <summary>The highest level published by a module that is currently enabled.</summary>
        public ColonyAlertLevel Level(System.Func<string, bool> enabled)
        {
            var best = ColonyAlertLevel.Normal;
            foreach (var kv in published) if (kv.Value > best && (enabled == null || enabled(kv.Key))) best = kv.Value;
            return best;
        }

        /// <summary>Who raised the colony to its current level (the first publisher at that level), or null at Normal.</summary>
        public string RaisedBy(System.Func<string, bool> enabled)
        {
            var level = Level(enabled); if (level == ColonyAlertLevel.Normal) return null;
            return published.Where(kv => kv.Value == level && (enabled == null || enabled(kv.Key))).Select(kv => kv.Key).OrderBy(k => k, System.StringComparer.Ordinal).FirstOrDefault();
        }
    }
}
