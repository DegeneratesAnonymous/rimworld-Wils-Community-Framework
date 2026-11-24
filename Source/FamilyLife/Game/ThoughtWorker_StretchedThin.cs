using FamilyLife.Domain;
using RimWorld;
using Verse;

namespace FamilyLife
{
    /// <summary>A colonist who holds more professions than their skill can carry feels it. Only a mood nudge; their work is never slowed or refused.</summary>
    public class ThoughtWorker_StretchedThin : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var reg = FamilyComponent.Current?.Registry;
            if (reg == null || p.Dead || !p.IsColonist) return false;
            int held = RoleLoadUi.Held(reg, p.thingIDNumber);
            return held > 0 && RoleLoad.Of(held, RoleLoadUi.Capacity(reg, p.thingIDNumber)) == RoleLoad.Level.Over;
        }
    }
}
