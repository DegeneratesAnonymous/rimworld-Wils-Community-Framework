using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// The treatment record. Every hour each prisoner is looked at (fed, tended, in a decent mood) and the result credited to the warden who last looked after them. When someone stops being a prisoner,
    /// what became of them is noted. Their relatives in the colony react to both, and a warden who looks after a relative's prisoner very differently from the rest is noticed by colleagues. It only
    /// watches and reacts: nothing about a prisoner, a warden or any decision is changed.
    /// </summary>
    internal static class WardenTreatmentGame
    {
        private sealed class Watch { public Pawn Pawn; public bool Execution; }
        private static readonly Dictionary<int, Watch> watched = new Dictionary<int, Watch>();          // live references to current prisoners, so what became of them can be read afterwards
        private static readonly Dictionary<string, int> last = new Dictionary<string, int>();
        public static void Reset() { watched.Clear(); last.Clear(); }

        internal static bool Kin(Pawn a, Pawn b)
        {
            if (a?.relations == null || b == null || a == b) return false;
            try
            {
                foreach (var relative in a.relations.FamilyByBlood) if (relative == b) return true;
                return a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, b) || a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b) || a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b);
            }
            catch (Exception) { return false; }
        }

        /// <summary>Gives a thought, at most once in three days per person, thought, other person and prisoner.</summary>
        internal static void Give(Pawn subject, string thought, Pawn other, int prisoner)
        {
            if (subject == null || thought == null) return;
            int day = FrameworkServices.EconomyDay; string key = subject.thingIDNumber + ":" + thought + ":" + (other?.thingIDNumber ?? 0) + ":" + prisoner;
            if (last.TryGetValue(key, out int before) && day - before < 3) return;
            last[key] = day; FrameworkServices.GainMemory(subject, thought, other);
        }

        /// <summary>A holder of a Warden role has just finished a job whose target is a prisoner of the colony: they are now the warden responsible for that prisoner.</summary>
        public static void Labour(LabourReport r)
        {
            var c = FamilyComponent.Current; if (c == null || r?.Pawn == null || r.ThingId == 0 || WardenStories.ActiveRole(r.Pawn) == null) return;
            Pawn prisoner = null;
            foreach (var map in Find.Maps) { prisoner = map.mapPawns.PrisonersOfColony.FirstOrDefault(p => p.thingIDNumber == r.ThingId); if (prisoner != null) break; }
            if (prisoner != null) c.Registry.Wardens().Assign(prisoner.thingIDNumber, r.Pawn.thingIDNumber, FrameworkServices.Now);
        }

        private static IEnumerable<Pawn> Colonists() => Find.Maps.Where(m => m.IsPlayerHome).SelectMany(m => m.mapPawns.FreeColonistsSpawned);

        private static void React(Pawn prisoner, KinReaction reaction, Pawn warden)
        {
            if (reaction == null) return;
            foreach (var kin in Colonists().Where(p => Kin(p, prisoner)).ToList())
            {
                Give(kin, reaction.Mood, null, prisoner.thingIDNumber);
                if (warden != null && warden != kin) Give(kin, reaction.Opinion, warden, prisoner.thingIDNumber);          // an opinion of the person responsible, never of themselves
            }
        }

        private static void Concluded(FamilyComponent c, WardenLedger ledger, Pawn pawn, bool execution, int now)
        {
            if (pawn == null || pawn.Discarded) return;
            var outcome = PrisonerOutcomes.Classify(pawn.Dead, execution, pawn.Faction == Faction.OfPlayer && !pawn.IsPrisonerOfColony, pawn.IsSlaveOfColony);
            int wid = ledger.WardenOf(pawn.thingIDNumber, now); ledger.Outcome(wid, outcome);
            React(pawn, KinReactions.For(outcome), wid != 0 ? c.TryGetPawn(wid) : null);
        }

        /// <summary>Once an hour: a look at every prisoner, what became of those who are no longer prisoners, and whether a warden plays favourites.</summary>
        public static void Run(int now)
        {
            var c = FamilyComponent.Current; if (c == null) return; var ledger = c.Registry.Wardens(); var current = new HashSet<int>();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                foreach (var prisoner in map.mapPawns.PrisonersOfColony.ToList())
                {
                    int id = prisoner.thingIDNumber; current.Add(id);
                    watched[id] = new Watch { Pawn = prisoner, Execution = prisoner.guest?.ExclusiveInteractionMode == PrisonerInteractionModeDefOf.Execution };
                    var level = WardenCare.Rate(prisoner.needs?.food?.CurLevelPercentage ?? 1f, prisoner.needs?.mood?.CurLevelPercentage ?? .5f, HealthAIUtility.ShouldBeTendedNowByPlayer(prisoner));
                    int wid = ledger.WardenOf(id, now); var warden = wid != 0 ? c.TryGetPawn(wid) : null; bool kin = warden != null && Kin(warden, prisoner);
                    if (warden != null) { ledger.Rate(wid, level, kin); if (kin) Give(warden, KinReactions.Conflicted, null, id); }          // torn between duty and family
                    React(prisoner, KinReactions.ForCare(level), warden);
                }
            }
            foreach (int id in watched.Keys.Where(k => !current.Contains(k)).ToList()) { var w = watched[id]; watched.Remove(id); Concluded(c, ledger, w.Pawn, w.Execution, now); }
            foreach (int wid in ledger.Wardens.ToList())
                if (ledger.TakeFavouritism(wid, now) && c.TryGetPawn(wid) is Pawn warden)
                    foreach (var colleague in Colonists().Where(p => p != warden && WardenStories.ActiveRole(p) != null).ToList()) Give(colleague, KinReactions.Favouritism, warden, 0);
        }

        /// <summary>A warden took a prisoner somewhere they were treated well: good care on the record, and kin hear of it.</summary>
        internal static void Escorted(Pawn prisoner, Pawn warden)
        {
            var c = FamilyComponent.Current; if (c == null || prisoner == null || warden == null) return;
            c.Registry.Wardens().Rate(warden.thingIDNumber, PrisonerCare.Good, Kin(warden, prisoner)); React(prisoner, KinReactions.ForCare(PrisonerCare.Good), warden);
        }

        /// <summary>After a load: forget wardens who are gone.</summary>
        public static void Reconcile(FamilyComponent c) => c.Registry.Wardens().Prune(id => c.TryGetPawn(id) != null);
    }
}
