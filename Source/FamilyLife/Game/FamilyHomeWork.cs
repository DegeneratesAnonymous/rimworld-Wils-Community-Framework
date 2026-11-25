using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace FamilyLife
{
    /// <summary>
    /// Members of a family do the work inside their own home first. When the work a family member would pick is somewhere else, the same search is
    /// made again with the world outside the family's zones put out of bounds; if that finds work of the same importance in the home (the order of
    /// the player's work priorities is never changed), they take it. It applies only to work they are able to do well, never to emergencies, and
    /// never to what the player ordered.
    /// </summary>
    internal static class FamilyHomeWork
    {
        private const int Throttle = 30, MinSkill = 4;
        private static readonly Dictionary<int, int> lastTried = new Dictionary<int, int>();
        private static readonly HashSet<string> Urgent = new HashSet<string> { "Firefighter", "Doctor", "Patient", "PatientBedRest", "BasicWorker_Emergency" };

        [ThreadStatic] private static Pawn active;
        [ThreadStatic] private static List<Zone_Family> zones;

        public static bool Enabled => FamilyDoorRule.Enabled && (FamilyLifeMod.Settings == null || FamilyLifeMod.Settings.familyHomeFirst);
        public static void Reset() => lastTried.Clear();

        /// <summary>While a home-first search is running: whether this cell is outside every home zone of the pawn, and so out of bounds for it.</summary>
        public static bool OutOfBounds(IntVec3 cell, Pawn pawn)
        {
            if (active == null || active != pawn || zones == null) return false;
            var map = pawn.MapHeld; if (map == null || !cell.InBounds(map)) return false;
            return !(map.zoneManager.ZoneAt(cell) is Zone_Family z && zones.Contains(z));
        }

        public static List<Zone_Family> OwnZones(Pawn pawn)
        {
            var map = pawn.MapHeld;
            if (map == null) return null;
            List<Zone_Family> list = null;
            foreach (var z in map.zoneManager.AllZones)
                if (z is Zone_Family f && f.areaId != 0 && FamilyDoorRule.BelongsHere(f, pawn)) (list ?? (list = new List<Zone_Family>())).Add(f);
            return list;
        }

        /// <summary>Whether this pawn is good enough at what the work needs: no relevant skill, a skill of four or more, or a passion for it.</summary>
        public static bool SkilledFor(Pawn pawn, WorkTypeDef type)
        {
            if (type == null || pawn.skills == null) return true;
            var relevant = type.relevantSkills; if (relevant == null || relevant.Count == 0) return true;
            foreach (var def in relevant)
            {
                var skill = pawn.skills.GetSkill(def);
                if (skill == null || skill.TotallyDisabled) continue;
                if (skill.Level >= MinSkill || skill.passion != Passion.None) return true;
            }
            return false;
        }

        private static IntVec3 Where(Job job)
        {
            var t = job.targetA;
            if (t.HasThing) return t.Thing.PositionHeld;
            return t.Cell;
        }

        public static bool Eligible(Pawn pawn)
            => pawn != null && pawn.Spawned && pawn.Faction == Faction.OfPlayer && pawn.RaceProps.Humanlike && pawn.workSettings != null
               && !pawn.Drafted && !pawn.Downed && !pawn.InMentalState && !pawn.IsPrisoner && !pawn.IsSlave && pawn.DevelopmentalStage == DevelopmentalStage.Adult;

        public static void TryHome(JobGiver_Work giver, Pawn pawn, JobIssueParams jobParams, ref ThinkResult result)
        {
            if (!Enabled || !result.IsValid || !Eligible(pawn)) return;
            var job = result.Job;
            var def = job?.workGiverDef;
            if (def == null || job.playerForced || def.workType == null || Urgent.Contains(def.workType.defName)) return;
            var own = OwnZones(pawn);
            if (own == null || own.Count == 0) return;
            if (own.Any(z => z.ContainsCell(Where(job)))) return;            // already work at home
            int now = Find.TickManager.TicksGame;
            if (lastTried.TryGetValue(pawn.thingIDNumber, out int last) && now - last < Throttle) return;
            lastTried[pawn.thingIDNumber] = now;

            active = pawn; zones = own;
            ThinkResult again;
            try { again = giver.TryIssueJobPackage(pawn, jobParams); }
            finally { active = null; zones = null; }
            if (!again.IsValid) return;
            var home = again.Job; var homeDef = home?.workGiverDef;
            if (homeDef == null || homeDef.workType == null || !SkilledFor(pawn, homeDef.workType)) return;
            // The player's order of work is kept: only work of the same priority is taken instead.
            if (pawn.workSettings.GetPriority(homeDef.workType) != pawn.workSettings.GetPriority(def.workType)) return;
            if (!own.Any(z => z.ContainsCell(Where(home)))) return;
            result = again;
        }
    }

    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    internal static class Patch_FamilyHomeFirstWork
    {
        [ThreadStatic] private static bool inside;

        private static void Postfix(JobGiver_Work __instance, Pawn pawn, JobIssueParams jobParams, ref ThinkResult __result)
        {
            if (inside) return;
            inside = true;
            try { FamilyHomeWork.TryHome(__instance, pawn, jobParams, ref __result); }
            catch (Exception ex) { HookSafety.Report("family home work", ex); }
            finally { inside = false; }
        }
    }
}
