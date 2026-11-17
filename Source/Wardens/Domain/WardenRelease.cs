using System.Collections.Generic;
using System.Linq;
using FamilyLife.Framework;

namespace FamilyLife.Domain
{
    public enum ReleaseWhen : byte { None = 0, Healthy = 1, CanWalk = 2 }

    public static class ReleaseRule
    {
        /// <summary>Whether a prisoner marked for conditional release may go now: healthy means up and with nothing needing treatment; able to walk means only that they can move.</summary>
        public static bool Due(ReleaseWhen when, bool downed, bool needsTreatment, bool canWalk) =>
            when == ReleaseWhen.Healthy ? !downed && !needsTreatment : when == ReleaseWhen.CanWalk && canWalk;
    }

    /// <summary>Prisoners the player has marked "release when ...". The Wardens module sets the game's own Release mode for them once the condition is met. Saved by pawn id.</summary>
    public sealed class ReleaseHold : IModuleState
    {
        public const string StateId = "wils.wardens.release";
        private readonly Dictionary<int, ReleaseWhen> holds = new Dictionary<int, ReleaseWhen>();

        public ReleaseWhen When(int person) => holds.TryGetValue(person, out var w) ? w : ReleaseWhen.None;
        public void Set(int person, ReleaseWhen when) { if (person <= 0) return; if (when == ReleaseWhen.None) holds.Remove(person); else holds[person] = when; }
        public IEnumerable<int> People => holds.Keys.ToList();
        public int Count => holds.Count;

        public string Encode() => new StateWriter().Ints("p", holds.Keys.OrderBy(k => k).ToList()).Ints("w", holds.OrderBy(k => k.Key).Select(k => (int)k.Value).ToList()).ToString();

        public static ReleaseHold Decode(StateReader r)
        {
            var b = new ReleaseHold(); var p = r.Ints("p"); var w = r.Ints("w");
            for (int i = 0; i < p.Count && i < w.Count; i++) if (w[i] == (int)ReleaseWhen.Healthy || w[i] == (int)ReleaseWhen.CanWalk) b.Set(p[i], (ReleaseWhen)w[i]);
            return b;
        }
    }

    public sealed class ReleaseHoldFactory : IStateFactory<ReleaseHold>
    {
        public static readonly ReleaseHoldFactory Instance = new ReleaseHoldFactory();
        public int Version => 1;
        public ReleaseHold Create() => new ReleaseHold();
        public ReleaseHold Decode(StateReader reader, int version) => ReleaseHold.Decode(reader);
        public ReleaseHold FromLegacy(StateReader legacy) => Create();
    }

    public static class ReleaseHoldAccess
    {
        public static ReleaseHold ReleaseHolds(this FamilyRegistry r) => r.Shared.Of(ReleaseHold.StateId, ReleaseHoldFactory.Instance);
    }
}
