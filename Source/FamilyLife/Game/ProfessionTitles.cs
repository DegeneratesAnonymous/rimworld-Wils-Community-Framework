using FamilyLife.Domain;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// Shows a colonist's main profession where the game shows their adulthood backstory title (the colonist bar, the character card, tooltips). Only for adults with a backstory, only while they hold a profession, and
    /// switchable in the mod settings. The backstory itself is not changed: its skills, traits and work restrictions stay exactly as they were.
    /// </summary>
    internal static class ProfessionTitles
    {
        private static readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.KeyValuePair<int, string>> remembered = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.KeyValuePair<int, string>>();
        private const int RememberTicks = 250;

        /// <summary>The title is read many times a frame by the colonist bar, so each pawn's answer is kept for a few seconds (null means "show the backstory").</summary>
        public static string For(Pawn_StoryTracker story, Pawn pawn)
        {
            if (pawn == null) return null;
            int now = FrameworkServices.Now;
            if (remembered.TryGetValue(pawn.thingIDNumber, out var e) && now >= e.Key && now - e.Key < RememberTicks && FamilyLifeMod.Settings?.professionTitle != false) return e.Value;
            var title = Compute(story, pawn); if (remembered.Count > 4000) remembered.Clear();
            remembered[pawn.thingIDNumber] = new System.Collections.Generic.KeyValuePair<int, string>(now, title); return title;
        }

        private static string Compute(Pawn_StoryTracker story, Pawn pawn)
        {
            try
            {
                var settings = FamilyLifeMod.Settings; if (settings != null && !settings.professionTitle) return null;
                var c = FamilyComponent.Current; if (pawn == null || c == null || story?.Adulthood == null || !pawn.IsColonist) return null;
                string role = c.Registry.Assignments().PrimaryOf(pawn.thingIDNumber); if (role == null) return null;
                var def = CareerService.Professions.ById(role); if (def == null || !FamilyFramework.Catalog.IsEnabled(def.ModuleId)) return null;
                return CareerService.TitleOf(def, c.Registry);
            }
            catch (System.Exception) { return null; }
        }
    }

    [HarmonyPatch(typeof(Pawn_StoryTracker), "get_Title")]
    internal static class Patch_ProfessionTitle { static void Postfix(Pawn_StoryTracker __instance, Pawn ___pawn, ref string __result) { var t = ProfessionTitles.For(__instance, ___pawn); if (t != null) __result = t; } }

    [HarmonyPatch(typeof(Pawn_StoryTracker), "get_TitleCap")]
    internal static class Patch_ProfessionTitleCap { static void Postfix(Pawn_StoryTracker __instance, Pawn ___pawn, ref string __result) { var t = ProfessionTitles.For(__instance, ___pawn); if (t != null) __result = t.CapitalizeFirst(); } }

    [HarmonyPatch(typeof(Pawn_StoryTracker), "get_TitleShort")]
    internal static class Patch_ProfessionTitleShort { static void Postfix(Pawn_StoryTracker __instance, Pawn ___pawn, ref string __result) { var t = ProfessionTitles.For(__instance, ___pawn); if (t != null) __result = t; } }

    [HarmonyPatch(typeof(Pawn_StoryTracker), "get_TitleShortCap")]
    internal static class Patch_ProfessionTitleShortCap { static void Postfix(Pawn_StoryTracker __instance, Pawn ___pawn, ref string __result) { var t = ProfessionTitles.For(__instance, ___pawn); if (t != null) __result = t.CapitalizeFirst(); } }
}
