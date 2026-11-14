using FamilyLife.Domain;
using RimWorld;
using Verse;

namespace FamilyLife
{
    public sealed class StatPart_AgricultureSpecialty:StatPart
    {
        public string specialty,label,target;
        public float factor=1f;
        private bool Applies(StatRequest req)
        {
            var pawn=req.Thing as Pawn;if(pawn==null||!CareerService.IsAssignedSpecialist(pawn,AgricultureCareers.Career,specialty))return false;
            if(string.IsNullOrEmpty(target))return true;
            var thing=pawn.CurJob?.targetA.Thing;if(thing?.def==null)return false;
            if(target=="medicine")return thing.def.defName.IndexOf("Healroot",System.StringComparison.OrdinalIgnoreCase)>=0||thing.def.defName.IndexOf("Medicine",System.StringComparison.OrdinalIgnoreCase)>=0;
            if(target=="tree")return thing.def.plant?.IsTree==true;
            return true;
        }
        public override void TransformValue(StatRequest req,ref float val){if(Applies(req))val*=factor;}
        public override string ExplanationPart(StatRequest req)=>Applies(req)?label+": x"+factor.ToString("0.##"):null;
    }
}
