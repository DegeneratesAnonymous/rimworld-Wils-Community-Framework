using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    public sealed class IntelligenceEvent
    {
        public string Id, Role, Fact, Thought; public int Mood, Opinion;
        public bool Matches(string role, string fact, bool other) => Role == role && Fact == fact && (Opinion == 0 || other);
    }

    /// <summary>Every Intelligence role has five positive, five negative and five relationship events; the thought for each is in the package's Defs. Generated from one table so the facts and the thoughts cannot drift apart.</summary>
    public static class IntelligenceEvents
    {
        public static readonly string[] Roles = { "Assistant", "Researcher", "Analyst", "Geneticist", "DarkScholar", "Author", "MechCommander", "Diplomat", "Chemist" };
        /// <summary>Facts for each role, in trigger order. Positive: routine success, outstanding result, milestone, recognised by others, pride. Negative: failed work, waste or setback, strain, rejected, danger or harm. Social: praised, helped, shared know-how, dispute, betrayal or stolen credit.</summary>
        private static readonly Dictionary<string, string[][]> facts = new Dictionary<string, string[][]>
        {
            { "Assistant", new[] { new[] { "TidyFiles", "SharpEye", "FirstContribution", "ThankedByLead", "ThriveInSupport" }, new[] { "MislaidNotes", "ErrandWasted", "OverStretched", "OverlookedAgain", "BlamedUnfairly" }, new[] { "PraisedMe", "GaveMeAnswer", "ShowedMeMethod", "TalkedOverMe", "TookMyIdea" } } },
            { "Researcher", new[] { new[] { "ProjectAdvance", "ElegantResult", "Breakthrough", "CitedByPeers", "LoveOfTheQuestion" }, new[] { "FailedExperiment", "DeadEnd", "Burnout", "DismissedFindings", "LostDataLoss" }, new[] { "RespectedBy", "HelpedSolve", "ShareMethod", "MethodDispute", "CreditTaken" } } },
            { "Analyst", new[] { new[] { "PatternFound", "SurveyClean", "RichFind", "ReportActedOn", "TrustedEyes" }, new[] { "MisreadSignal", "EmptyScan", "WatchStrain", "ReportShelved", "WrongWarning" }, new[] { "ThanksForReport", "PointedOut", "ShowedMeReading", "DoubtedReading", "LeakedMyReport" } } },
            { "Geneticist", new[] { new[] { "CleanSequence", "SafeSplice", "NewGeneLine", "HealedByWork", "CodeOfLife" }, new[] { "GermFailure", "SampleSpoiled", "EthicsWeigh", "WorkFeared", "SpliceHarm" }, new[] { "TrustedMeWithBody", "HelpedWithSample", "ShowedMeGene", "CalledMeAMonster", "SoldMyResearch" } } },
            { "DarkScholar", new[] { new[] { "ForbiddenInsight", "EntityRevealed", "PowerGained", "RespectedForDarkness", "DrawnToTheDark" }, new[] { "StudyBackfired", "NightTerrors", "TooDeep", "TextsBanned", "ContainmentSlip" }, new[] { "UnderstoodMe", "StoodWatch", "SharedLore", "CalledMeUnsafe", "ReportedMe" } } },
            { "Author", new[] { new[] { "PageFlows", "FineProse", "BookFinished", "ReadAndLoved", "VoiceFound" }, new[] { "BlankPage", "DraftBinned", "DeadlineStrain", "Panned", "WordsMisused" }, new[] { "PraisedMyWriting", "ProofedForMe", "ShowedMeCraft", "HarshCritique", "CopiedMyLines" } } },
            { "MechCommander", new[] { new[] { "MechesRunningWell", "CleanCommand", "NewUnitOnline", "TrustedWithTheFleet", "OneWithTheMachines" }, new[] { "UnitLost", "BandwidthStrained", "SignalLost", "MechsDistrusted", "MechTurnedOnUs" }, new[] { "ThankedForTheMechs", "HelpedWithRepairs", "ShowedMeProtocol", "CalledThemSoulless", "BlamedMyUnit" } } },
            { "Diplomat", new[] { new[] { "WordsLandedWell", "DealStruck", "TrustWon", "SoughtForCounsel", "BornToTalk" }, new[] { "InsultGiven", "TalksBrokeDown", "WornByWords", "NotListenedTo", "TrustBetrayed" }, new[] { "PraisedMyTact", "IntroducedMe", "ShowedMeNuance", "DistrustedMotives", "RepeatedMyWords" } } },
            { "Chemist", new[] { new[] { "PureBatch", "YieldBeatEstimate", "NewCompound", "PatientsHelped", "LoveOfTheBench" }, new[] { "SpoiledBatch", "WastedReagents", "FumesAndFatigue", "WorkMisused", "ReagentAccident" }, new[] { "ThankedForMedicine", "BroughtMeSupplies", "ShowedMeMethod", "CalledMeADealer", "TookMyFormula" } } },
        };
        public static string Fact(string role, int category, int index) => facts.TryGetValue(role, out var f) && category >= 0 && category < 3 && index >= 0 && index < 5 ? f[category][index] : null;
        public const int Positive = 0, Negative = 1, Social = 2;
        public static string Thought(string role, int category, int index) => "WFL_I_" + role + "_" + "PNS"[category] + (index + 1);
        public static readonly IntelligenceEvent[] All = Build().ToArray();
        private static IEnumerable<IntelligenceEvent> Build()
        {
            foreach (var role in Roles)
                for (int category = 0; category < 3; category++)
                    for (int i = 0; i < 5; i++)
                        yield return new IntelligenceEvent { Id = role + new[] { "Positive", "Negative", "Social" }[category] + i, Role = role, Fact = Fact(role, category, i), Thought = Thought(role, category, i), Mood = category == 0 ? 2 + i % 2 : category == 1 ? -2 - i % 2 : 0, Opinion = category == 2 ? (i < 3 ? 3 : i == 3 ? -3 : -4) : 0 };
        }
        public static IEnumerable<IntelligenceEvent> Eligible(string role, string fact, bool other) => All.Where(e => e.Matches(role, fact, other));
    }
}
