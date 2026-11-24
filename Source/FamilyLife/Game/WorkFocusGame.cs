using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// Suggests work priorities from the professions a colonist holds. It only ever turns work ON or raises it, never switches work off, leaves any priority the player has changed by hand since, and can be undone.
    /// It acts when the player presses a button, turns a colonist's "follow" on, or a followed colonist's professions change, never every tick.
    /// </summary>
    internal static class WorkFocusGame
    {
        private static int lastRun = -100000;
        private const int Interval = 2500;

        private static bool UsingPriorities => Current.Game?.playSettings?.useWorkPriorities ?? true;

        private static string SignatureOf(AssignmentBook book, int id) { string main = book.PrimaryOf(id); return main + "#" + string.Join(",", book.RolesOf(id).OrderBy(x => x, System.StringComparer.Ordinal)); }

        private static void Careers(FamilyRegistry r, int id, out string main, out List<string> sides)
        {
            var book = r.Assignments(); string mainRole = book.PrimaryOf(id); main = null; sides = new List<string>();
            foreach (var roleId in book.RolesOf(id))
            {
                var role = CareerService.Professions.ById(roleId); if (role == null || !FamilyFramework.Catalog.IsEnabled(role.ModuleId)) continue;
                if (roleId == mainRole) main = role.Career; else if (!sides.Contains(role.Career)) sides.Add(role.Career);
            }
            if (main != null) sides.Remove(main);
        }

        /// <summary>Sets the suggested priorities for one colonist. Returns how many work types changed.</summary>
        internal static int Apply(FamilyComponent c, Pawn pawn)
        {
            if (c == null || pawn?.workSettings == null || pawn.Dead) return 0;
            var r = c.Registry; Careers(r, pawn.thingIDNumber, out var main, out var sides);
            var wanted = WorkFocusPlan.Targets(FamilyFramework.WorkFocus.Enabled(), main, sides);
            if (!UsingPriorities) foreach (var k in wanted.Keys.ToList()) wanted[k] = 3;
            var book = r.WorkFocus();
            var plan = WorkFocusPlan.Plan(wanted, wt => PriorityOf(pawn, wt), wt => CanDo(pawn, wt), book.ChangesFor(pawn.thingIDNumber));
            foreach (var ch in plan) { var def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(ch.WorkType); if (def != null) pawn.workSettings.SetPriority(def, ch.Applied); }
            book.Record(pawn.thingIDNumber, plan); book.SetSignature(pawn.thingIDNumber, SignatureOf(r.Assignments(), pawn.thingIDNumber));
            return plan.Count;
        }

        /// <summary>Puts back what was there before, for work types still at the priority this feature set.</summary>
        internal static int Undo(FamilyComponent c, Pawn pawn)
        {
            if (c == null || pawn?.workSettings == null) return 0;
            var book = c.Registry.WorkFocus(); var undo = WorkFocusPlan.Undo(book.ChangesFor(pawn.thingIDNumber), wt => PriorityOf(pawn, wt));
            foreach (var ch in undo) { var def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(ch.WorkType); if (def != null) pawn.workSettings.SetPriority(def, ch.Before); }
            book.Forget(pawn.thingIDNumber, undo.Concat(book.ChangesFor(pawn.thingIDNumber).Where(e => !undo.Contains(e) && PriorityOf(pawn, e.WorkType) != e.Applied)).ToList());          // a work type the player changed since is theirs: no longer tracked
            return undo.Count;
        }

        private static int PriorityOf(Pawn p, string workType)
        {
            var def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workType); if (def == null || p.workSettings == null || !p.workSettings.WorkIsActive(def)) return 0;
            return p.workSettings.GetPriority(def);
        }

        private static bool CanDo(Pawn p, string workType) { var def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workType); return def != null && !p.WorkTypeIsDisabled(def); }

        internal static IEnumerable<Pawn> Colonists(FamilyComponent c)
        {
            var r = c.Registry; var book = r.Assignments();
            foreach (int id in book.AllPeople().Distinct()) { var p = c.TryGetPawn(id); if (p != null && !p.Dead && p.workSettings != null && r.GetPerson(id)?.InColony == true) yield return p; }
        }

        internal static int ApplyAll(FamilyComponent c) => Colonists(c).Sum(p => Apply(c, p));
        internal static int UndoAll(FamilyComponent c) => c.Registry.WorkFocus().Following.Concat(Colonists(c).Select(p => p.thingIDNumber)).Distinct().Sum(id => { var p = c.TryGetPawn(id); return p == null ? 0 : Undo(c, p); });

        /// <summary>Re-suggests for followed colonists whose professions have changed since the last time. Cheap: a few string compares every so often.</summary>
        internal static void Run(FamilyComponent c, int now)
        {
            if (now - lastRun < Interval) return; lastRun = now;
            var r = c.Registry; var book = r.WorkFocus(); if (!book.Following.Any()) return;
            var assignments = r.Assignments();
            foreach (int id in book.Following.ToList())
            {
                var p = c.TryGetPawn(id); if (p == null || p.Dead || r.GetPerson(id)?.InColony != true) { if (p != null && p.Dead) book.ForgetPerson(id); continue; }
                if (book.SignatureOf(id) != SignatureOf(assignments, id)) Apply(c, p);
            }
        }

        internal static void Reset() { lastRun = -100000; }
    }
}
