using System.Collections.Generic;

namespace FamilyLife.Domain
{
    /// <summary>
    /// Which of our careers a Vanilla Skills Expanded expertise belongs to, by the expertise's def name. Careers are named by id only. An expertise that fits none of them is ignored. Pure: the game side reads a pawn's
    /// expertise, this says what it is worth to a career.
    /// </summary>
    public static class ExpertiseMap
    {
        public const float FitPerExpertise = 4f;

        private static readonly Dictionary<string, string> careerOf = Build();

        private static Dictionary<string, string> Build()
        {
            var d = new Dictionary<string, string>();
            void Add(string career, params string[] names) { foreach (var n in names) d[n] = career; }
            Add("wils.care.practice", "Surgeon", "BattlefieldMedic", "InfectiousDiseaseExpert", "VSE_Operating", "Pharmacologist");
            Add("wils.military.military", "Precision", "CloseQuarters", "Gunner", "VSE_Sniping", "Sharp", "Blunt", "VSE_Striking");
            Add("wils.wardens.wardens", "Warden");
            Add("wils.intelligence.intelligence", "Researcher", "VSE_Writing", "VSE_DarkStudy", "Hacker", "Negotiator", "Proselytizer");
            Add("wils.works.miner", "Driller", "OreExpert", "Tunneller", "Geologist");
            Add("wils.works.builder", "Flooring", "Repairman", "Architect", "Foreman", "VSE_Smoothing");
            Add("wils.crafting.craft", "Tailor", "Weaponsmith", "QualityExpert", "QuantityExpert", "IndustrialProcessExpert", "MechanoidExpert");
            Add("wils.hospitality.cooking", "Butcher", "DrugChef", "TopChef", "IndustrialChef");
            Add("wils.agriculture.agriculture", "Tamer", "Rancher", "Hunter", "Trainer", "Forager", "Treespeaker", "HarvesterCareful", "GreenThumb");
            return d;
        }

        public static string CareerOf(string expertiseDef) => expertiseDef != null && careerOf.TryGetValue(expertiseDef, out var c) ? c : null;

        /// <summary>The extra fit a person's expertise gives for a career (a flat amount for each expertise that belongs to it).</summary>
        public static float FitFor(string career, IEnumerable<string> expertiseDefs)
        {
            float fit = 0f; if (career == null || expertiseDefs == null) return 0f;
            foreach (var e in expertiseDefs) if (CareerOf(e) == career) fit += FitPerExpertise;
            return fit;
        }
    }
}
