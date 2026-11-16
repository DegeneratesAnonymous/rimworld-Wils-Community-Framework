using System;
using System.Collections.Generic;

namespace FamilyLife.Domain
{
    /// <summary>
    /// Works: two careers in one module. Builder (Labourer, Journeyman, then Mason, Carpenter or Engineer) and Miner (Labourer, Journeyman, then Prospector, Quarryman or Tunneller). The Journeyman
    /// earns experience only; the bonuses belong to the specialties, each on a stat the game has. Owner: Works.
    /// </summary>
    public static class WorksCareers
    {
        public const string Module = "wils.works", Builder = "wils.works.builder", Miner = "wils.works.miner";
        public static string Role(string name) => Module + "." + name.ToLowerInvariant();

        public const string BuilderLabourer = "BuilderLabourer", BuilderJourneyman = "BuilderJourneyman", MinerLabourer = "MinerLabourer", MinerJourneyman = "MinerJourneyman";
        public static readonly string[] BuilderSpecialties = { "Mason", "Carpenter", "Engineer" };
        public static readonly string[] MinerSpecialties = { "Prospector", "Quarryman", "Tunneller" };

        public static string CareerOf(string specialty) => Array.IndexOf(BuilderSpecialties, specialty) >= 0 ? Builder : Array.IndexOf(MinerSpecialties, specialty) >= 0 ? Miner : null;

        public static CareerDefinition BuilderDefinition() => new CareerDefinition
        {
            Id = Builder, ModuleId = Module, LabelKey = "WFL_Works_Builder",
            Level2 = new CareerRule { GeneralXp = 120 }.Skill("Construction", 5),
            Specialties = new Dictionary<string, CareerRule>
            {
                { "Mason", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Construction", 8) },
                { "Carpenter", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Construction", 8).Skill("Crafting", 4) },
                { "Engineer", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Construction", 8).Skill("Intellectual", 6) }
            }
        };

        public static CareerDefinition MinerDefinition() => new CareerDefinition
        {
            Id = Miner, ModuleId = Module, LabelKey = "WFL_Works_Miner",
            Level2 = new CareerRule { GeneralXp = 120 }.Skill("Mining", 5),
            Specialties = new Dictionary<string, CareerRule>
            {
                { "Prospector", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Mining", 8).Skill("Intellectual", 4) },
                { "Quarryman", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Mining", 8) },
                { "Tunneller", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Mining", 10) }
            }
        };

        /// <summary>Experience for one finished piece of work: building, repairing or smoothing; mining or drilling; and one maintenance round.</summary>
        public const int BuildPoints = 4, RepairPoints = 3, SmoothPoints = 2, MinePoints = 4, RoundPoints = 4;
    }
}

namespace FamilyLife.Domain
{
    /// <summary>
    /// A specialty is worth something only while the pawn is qualified and assigned to that exact specialty (docs/SPECIALTY-BONUSES.md). Mason: +15% construction speed and +10% construction success.
    /// Carpenter: +15% smoothing speed and +10% construction success. Engineer: +15% construction speed and +15% success at fixing broken-down buildings. Prospector: +15% mining speed.
    /// Quarryman: +20% mining speed. Tunneller: +10% mining speed (the game gives no safe way to make digging safer, so there is no injury effect). The Labourer and Journeyman have no bonus.
    /// </summary>
    public static class WorksBonuses
    {
        public const string ConstructionSpeed = "ConstructionSpeed", ConstructSuccessChance = "ConstructSuccessChance", SmoothingSpeed = "SmoothingSpeed", FixBroken = "FixBrokenDownBuildingSuccessChance", MiningSpeed = "MiningSpeed";
        public static readonly string[] Stats = { ConstructionSpeed, ConstructSuccessChance, SmoothingSpeed, FixBroken, MiningSpeed };

        public static float Factor(string specialty, string stat)
        {
            switch (specialty)
            {
                case "Mason": return stat == ConstructionSpeed ? 1.15f : stat == ConstructSuccessChance ? 1.10f : 1f;
                case "Carpenter": return stat == SmoothingSpeed ? 1.15f : stat == ConstructSuccessChance ? 1.10f : 1f;
                case "Engineer": return stat == ConstructionSpeed ? 1.15f : stat == FixBroken ? 1.15f : 1f;
                case "Prospector": return stat == MiningSpeed ? 1.15f : 1f;
                case "Quarryman": return stat == MiningSpeed ? 1.20f : 1f;
                case "Tunneller": return stat == MiningSpeed ? 1.10f : 1f;
                default: return 1f;
            }
        }
    }
}
