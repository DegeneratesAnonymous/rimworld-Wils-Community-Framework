using System.Collections.Generic;
using FamilyLife.Domain;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// The letter for a family's camping wish. It carries the same offer as always (nothing happens unless the player agrees and sends a caravan) but now leads to a planning window instead of
    /// a dead end: Plan the trip opens it, Not now declines, and Close leaves the wish open to be answered from the family's page.
    /// </summary>
    public class ChoiceLetter_CampInvite : ChoiceLetter
    {
        public int tripId;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var trip = FamilyComponent.Current?.Registry.CampTripFor(tripId);
                if (trip != null && trip.State == CampState.Proposed)
                {
                    yield return new DiaOption("WFL_Camp_OpenPlan".Translate()) { action = () => { Find.WindowStack.Add(new Window_CampPlan(tripId)); Find.LetterStack.RemoveLetter(this); }, resolveTree = true };
                    yield return new DiaOption("WFL_Camp_Decline".Translate()) { action = () => { FamilyComponent.Current?.Registry.DeclineCamp(tripId, Activities.Now); Find.LetterStack.RemoveLetter(this); }, resolveTree = true };
                }
                yield return Option_Close;
                if (lookTargets != null && lookTargets.IsValid) yield return Option_JumpToLocation;
            }
        }

        public override bool CanDismissWithRightClick => true;
        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref tripId, "wflCampTrip"); }
    }
}
