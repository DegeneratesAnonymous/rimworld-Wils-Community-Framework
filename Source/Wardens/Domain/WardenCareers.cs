using System;
using System.Collections.Generic;

namespace FamilyLife.Domain
{
    /// <summary>
    /// The Warden career: Guard (anyone) -> Warden (qualified) -> Confessor, Overseer or Containment Keeper. The plain Warden earns experience only; the bonuses belong to the three specialties,
    /// each on a stat the game has. Confessor and Overseer need Ideology (conversion and slaves) and the Keeper needs Anomaly; without them those specialties simply have no work. Owner: Wardens.
    /// </summary>
    public static class WardenCareers
    {
        public const string Module = "wils.wardens", Career = "wils.wardens.wardens";
        public const string Guard = "wils.wardens.guard", Warden = "wils.wardens.warden";
        public static string Role(string name) => Module + "." + name.ToLowerInvariant();

        public static readonly string[] Specialties = { "Confessor", "Overseer", "ContainmentKeeper" };

        public static CareerDefinition Definition() => new CareerDefinition
        {
            Id = Career, ModuleId = Module, LabelKey = "WFL_Wardens_Career",
            Level2 = new CareerRule { GeneralXp = 120 }.Skill("Social", 6),
            Specialties = new Dictionary<string, CareerRule>
            {
                { "Confessor", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Social", 8).Skill("Intellectual", 6) },
                { "Overseer", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Social", 8).Skill("Melee", 6) },
                { "ContainmentKeeper", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Intellectual", 8).Skill("Melee", 6) }
            }
        };

        /// <summary>Experience for one finished piece of warden work (feeding, chatting, recruiting), one conversion, one stretch of slave oversight, one piece of containment work.</summary>
        public const int WardenPoints = 4, ConvertPoints = 8, OverseePoints = 6, ContainPoints = 8;
    }

    public sealed class WardenTrack { public string Specialty, Kind; public int Points; }

    /// <summary>
    /// Routes a finished job to the career. All by name (the game's job and work-type names), so they are the first thing to check in play: conversion and slave work are Ideology's, containment
    /// work is Anomaly's. Anything else done under the Warden work type is plain Warden experience.
    /// </summary>
    public static class WardenClassifier
    {
        public const string WardenWorkType = "Warden";

        private static bool Has(string value, params string[] words)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (var w in words) if (value.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static bool IsConversion(string jobDef) => Has(jobDef, "Convert");
        public static bool IsSlaveWork(string jobDef) => Has(jobDef, "Slave", "Suppress");
        public static bool IsContainment(string jobDef) => Has(jobDef, "HoldingPlatform", "Containment", "Platform");

        public static WardenTrack Classify(string jobDef, string workType)
        {
            if (IsConversion(jobDef)) return new WardenTrack { Specialty = "Confessor", Kind = "ward.convert", Points = WardenCareers.ConvertPoints };
            if (IsSlaveWork(jobDef)) return new WardenTrack { Specialty = "Overseer", Kind = "ward.oversee", Points = WardenCareers.OverseePoints };
            if (IsContainment(jobDef)) return new WardenTrack { Specialty = "ContainmentKeeper", Kind = "ward.contain", Points = WardenCareers.ContainPoints };
            if (string.Equals(workType, WardenWorkType, StringComparison.OrdinalIgnoreCase) || Has(jobDef, "Prisoner", "Warden")) return new WardenTrack { Kind = "ward.warden", Points = WardenCareers.WardenPoints };
            return null;
        }
    }

    /// <summary>
    /// A specialty is worth something only while the pawn is qualified and assigned to that exact specialty (docs/SPECIALTY-BONUSES.md). Confessor: +15% conversion power. Overseer: +15% suppression
    /// power. Containment Keeper: +10% study efficiency. The plain Warden has no bonus.
    /// </summary>
    public static class WardenBonuses
    {
        public const string ConversionPower = "ConversionPower", SuppressionPower = "SuppressionPower", StudyEfficiency = "StudyEfficiency";
        public const float Confessor = 1.15f, Overseer = 1.15f, Keeper = 1.10f;

        public static float Factor(string specialty, string stat)
        {
            switch (specialty)
            {
                case "Confessor": return stat == ConversionPower ? Confessor : 1f;
                case "Overseer": return stat == SuppressionPower ? Overseer : 1f;
                case "ContainmentKeeper": return stat == StudyEfficiency ? Keeper : 1f;
                default: return 1f;
            }
        }
    }
}
