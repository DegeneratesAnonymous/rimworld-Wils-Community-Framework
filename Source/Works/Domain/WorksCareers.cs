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
