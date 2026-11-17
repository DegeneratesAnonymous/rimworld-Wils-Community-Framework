using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// Conditional release, built in: a prisoner marked "release when healthy" (or "when able to walk") keeps whatever handling they have until the condition is met, then the game's own Release mode is set so the wardens
    /// let them go. A prisoner who is no longer a prisoner is forgotten. Nothing else about their treatment is touched.
    /// </summary>
    internal static class WardenReleaseGame
    {
        public static void Run(int now)
        {
            var c = FamilyComponent.Current; if (c == null) return;
            var holds = c.Registry.ReleaseHolds(); if (holds.Count == 0) return;
            var prisoners = Find.Maps.Where(m => m.IsPlayerHome).SelectMany(m => m.mapPawns.PrisonersOfColony).ToDictionary(p => p.thingIDNumber);
            foreach (int id in holds.People)
            {
                if (!prisoners.TryGetValue(id, out var p) || p.guest == null) { holds.Set(id, ReleaseWhen.None); continue; }
                bool canWalk = p.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
                if (!ReleaseRule.Due(holds.When(id), p.Downed, p.health.HasHediffsNeedingTend(false), canWalk)) continue;
                p.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.Release); holds.Set(id, ReleaseWhen.None);
                Messages.Message("WFL_Wardens_ReleaseNow".Translate(p.LabelShortCap), p, MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
