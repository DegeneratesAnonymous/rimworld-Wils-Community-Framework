using FamilyLife.Domain;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// Optional link to Search and Rescue: finished field treatment, rescue and evacuation jobs (its jobs are named SAR_...) earn Care experience, so a field medic grows as a doctor. Capturing someone and mech work earn nothing here.
    /// Nothing happens without that mod.
    /// </summary>
    internal static class CareFieldRescue
    {
        public static bool Counts(string jobDef) =>
            jobDef != null && jobDef.StartsWith("SAR_", System.StringComparison.Ordinal) && !jobDef.Contains("Capture") && !jobDef.Contains("Mech") && !jobDef.Contains("Restock") && !jobDef.Contains("Wait");

        public static void OnLabour(LabourReport rep)
        {
            if (rep?.Pawn == null || !Counts(rep.JobDef)) return;
            var c = FamilyComponent.Current; if (c == null) return;
            CareerService.Report(c, rep.Pawn, CareRoles.Career, null, "care.fieldrescue", "sar|" + rep.JobId, 3, FrameworkServices.Now);
        }
    }
}
