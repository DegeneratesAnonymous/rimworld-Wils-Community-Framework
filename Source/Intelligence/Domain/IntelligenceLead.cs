using FamilyLife.Framework;

namespace FamilyLife.Domain
{
    /// <summary>The colony's chosen lead researcher. A record of the player's choice only: it names who leads the work and changes nothing in the game. Saved by pawn id.</summary>
    public sealed class IntelligenceLedger : IModuleState
    {
        public const string StateId = "wils.intelligence.lead";
        public int LeadId;
        public string Encode() => new StateWriter().Int("lead", LeadId).ToString();
        public static IntelligenceLedger Decode(StateReader r) => new IntelligenceLedger { LeadId = r.Int("lead") };
    }

    public sealed class IntelligenceLedgerFactory : IStateFactory<IntelligenceLedger>
    {
        public static readonly IntelligenceLedgerFactory Instance = new IntelligenceLedgerFactory();
        public int Version => 1;
        public IntelligenceLedger Create() => new IntelligenceLedger();
        public IntelligenceLedger Decode(StateReader reader, int version) => IntelligenceLedger.Decode(reader);
        public IntelligenceLedger FromLegacy(StateReader legacy) => Create();
    }

    public static class IntelligenceStateAccess
    {
        public static IntelligenceLedger Intelligence(this FamilyRegistry r) => r.Shared.Of(IntelligenceLedger.StateId, IntelligenceLedgerFactory.Instance);
    }
}
