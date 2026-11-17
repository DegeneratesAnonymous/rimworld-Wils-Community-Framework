using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FamilyLife.Domain;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// Optional link to Vanilla Skills Expanded: reads a pawn's expertise by reflection so Quick setup can weigh it and the Professions page can show it. Nothing here needs that mod; if it is absent or changes, the
    /// answer is simply "no expertise". Read only.
    /// </summary>
    public static class ExpertiseLink
    {
        private static bool looked; private static MethodInfo getTracker; private static FieldInfo listField, recDef;

        private static bool Ready()
        {
            if (looked) return getTracker != null;
            looked = true;
            try
            {
                var trackers = GenTypes.GetTypeInAnyAssembly("VSE.ExpertiseTrackers"); var tracker = GenTypes.GetTypeInAnyAssembly("VSE.ExpertiseTracker"); var rec = GenTypes.GetTypeInAnyAssembly("VSE.ExpertiseRecord");
                getTracker = trackers?.GetMethod("Expertise", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Pawn) }, null);
                listField = tracker?.GetField("expertise", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); recDef = rec?.GetField("def", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (listField == null || recDef == null) getTracker = null;
            }
            catch (Exception) { getTracker = null; }
            return getTracker != null;
        }

        /// <summary>The pawn's expertise as (def name, label), or an empty list.</summary>
        public static List<KeyValuePair<string, string>> Of(Pawn pawn)
        {
            var result = new List<KeyValuePair<string, string>>(); if (pawn == null || !Ready()) return result;
            try
            {
                var tracker = getTracker.Invoke(null, new object[] { pawn }); if (tracker == null) return result;
                if (listField.GetValue(tracker) is IEnumerable all) foreach (var r in all) if (recDef.GetValue(r) is Def def) result.Add(new KeyValuePair<string, string>(def.defName, def.LabelCap.ToString()));
            }
            catch (Exception) { result.Clear(); }
            return result;
        }

        /// <summary>Extra fit for a career from the pawn's expertise (0 without the mod).</summary>
        public static float Fit(Pawn pawn, string career) => ExpertiseMap.FitFor(career, Of(pawn).Select(e => e.Key));

        /// <summary>The expertise that belongs to a career, as readable text, or null.</summary>
        public static string Describe(Pawn pawn, string career)
        {
            var mine = Of(pawn).Where(e => ExpertiseMap.CareerOf(e.Key) == career).Select(e => e.Value).ToList();
            return mine.Count == 0 ? null : string.Join(", ", mine);
        }
    }
}
