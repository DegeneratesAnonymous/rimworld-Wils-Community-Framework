using System;

namespace FamilyLife.Domain
{
    /// <summary>What the game side read about a finished job's target: what was built, or what was dug.</summary>
    public sealed class WorksJobFacts
    {
        /// <summary>Built (or smoothed) from stone or blocks.</summary>
        public bool Stone;
        /// <summary>The thing built is furniture or a fitting.</summary>
        public bool Furniture;
        /// <summary>The thing built is power, production or other machinery.</summary>
        public bool Machine;
        /// <summary>Dug rock that holds ore.</summary>
        public bool Ore;
        /// <summary>Dug inside the mountain, under thick rock roof.</summary>
        public bool UnderRock;
    }

    public sealed class WorksTrack { public string Career, Specialty, Kind; public int Points; }

    /// <summary>
    /// Routes a finished job to a career, by the game's own job names. Building: Mason for stone, Carpenter for furniture, Engineer for machinery (and fixing breakdowns), plain Builder experience otherwise.
    /// Smoothing is the Carpenter's. Mining: ore is the Prospector's, rock inside the mountain the Tunneller's, rock in the open the Quarryman's.
    /// </summary>
    public static class WorksClassifier
    {
        private static bool Is(string job, params string[] names)
        {
            if (string.IsNullOrEmpty(job)) return false;
            foreach (var n in names) if (string.Equals(job, n, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static WorksTrack Classify(string jobDef, WorksJobFacts f)
        {
            f = f ?? new WorksJobFacts();
            if (Is(jobDef, "FinishFrame"))
                return new WorksTrack { Career = WorksCareers.Builder, Specialty = f.Machine ? "Engineer" : f.Furniture ? "Carpenter" : f.Stone ? "Mason" : null, Kind = "works.build", Points = WorksCareers.BuildPoints };
            if (Is(jobDef, "FixBrokenDownBuilding")) return new WorksTrack { Career = WorksCareers.Builder, Specialty = "Engineer", Kind = "works.fix", Points = WorksCareers.RepairPoints };
            if (Is(jobDef, "Repair")) return new WorksTrack { Career = WorksCareers.Builder, Kind = "works.repair", Points = WorksCareers.RepairPoints };
            if (Is(jobDef, "SmoothFloor", "SmoothWall")) return new WorksTrack { Career = WorksCareers.Builder, Specialty = "Carpenter", Kind = "works.smooth", Points = WorksCareers.SmoothPoints };
            if (Is(jobDef, "Mine")) return new WorksTrack { Career = WorksCareers.Miner, Specialty = f.Ore ? "Prospector" : f.UnderRock ? "Tunneller" : "Quarryman", Kind = "works.mine", Points = WorksCareers.MinePoints };
            if (Is(jobDef, "OperateDeepDrill")) return new WorksTrack { Career = WorksCareers.Miner, Kind = "works.drill", Points = WorksCareers.MinePoints };
            return null;
        }
    }
}
