using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    public sealed class WardenEvent
    {
        public string Id, Role, Fact, Thought; public int Mood, Opinion;
        public bool Matches(string role, string fact, bool other) => Role == role && Fact == fact && (Opinion == 0 || other);
    }

    /// <summary>Every Warden role has five positive, five negative and five relationship events; the thought for each is in the package's Defs. Generated from one table so the facts and the thoughts cannot drift apart.</summary>
    public static class WardenEvents
    {
        public static readonly string[] Roles = { "Guard", "Warden", "Confessor", "Overseer", "ContainmentKeeper" };
        /// <summary>Facts for each role, in trigger order. Positive: routine success, outstanding result, milestone, recognised by others, pride. Negative: failed work, waste or setback, strain, rejected, danger or harm. Social: praised, helped, shared know-how, dispute, betrayal or stolen credit.</summary>
        private static readonly Dictionary<string, string[][]> facts = new Dictionary<string, string[][]>
        {
            { "Guard", new[] { new[] { "OrderKept", "SteadyWatch", "FirstShiftDone", "TrustedWithKeys", "ProudOfThePost" }, new[] { "ScuffleInTheCells", "SleeplessWatch", "WornByTheShifts", "DisrespectedByThem", "ThreatenedByAPrisoner" }, new[] { "PraisedMyWatch", "TookMyShift", "ShowedMeTheKeys", "CalledMeAJailer", "LeftMeToIt" } } },
            { "Warden", new[] { new[] { "TrustEarned", "QuietCellBlock", "ReachedThem", "NotedByTheColony", "AFairHand" }, new[] { "PrisonerWouldNotSpeak", "ResistanceHolds", "WornByTheWork", "QuestionedMyMethods", "PrisonerHarmed" }, new[] { "PraisedTheBlock", "HelpedWithAPrisoner", "ShowedMeHowToTalk", "CalledMeCruel", "TookTheCredit" } } },
            { "Confessor", new[] { new[] { "ConvictionShared", "FaithTook", "TearsOfRelief", "TrustedWithSouls", "SureOfThePath" }, new[] { "FaithRefused", "DoubtCreeps", "WornByArguments", "CalledAZealot", "ConversionBacklash" }, new[] { "ThankedForTheFaith", "SatWithMe", "SharedAParable", "QuestionedMyFaith", "MockedTheBelief" } } },
            { "Overseer", new[] { new[] { "OrderHolding", "NoUnrest", "SettledFamilies", "TheColonyRelies", "FirmButFair" }, new[] { "UnrestStirs", "SlaveRanOff", "WearyOfChains", "BlamedForCruelty", "RebelsStruck" }, new[] { "ThankedForTheOrder", "BackedMyCall", "ShowedMeFirmness", "CalledMeATyrant", "WentOverMyHead" } } },
            { "ContainmentKeeper", new[] { new[] { "ContainmentHolds", "ReadingsSteady", "EntityQuiet", "TrustedWithTheCell", "SteadyNerves" }, new[] { "BreachScare", "ThingStirred", "SleeplessVigil", "WarningsDismissed", "ContainmentFailed" }, new[] { "ThankedForTheWatch", "StoodGuard", "SharedTheLore", "CalledMeUnsafe", "LeakedMyFindings" } } },
        };
        public static string Fact(string role, int category, int index) => facts.TryGetValue(role, out var f) && category >= 0 && category < 3 && index >= 0 && index < 5 ? f[category][index] : null;
        public const int Positive = 0, Negative = 1, Social = 2;
        public static string Thought(string role, int category, int index) => "WFL_W_" + role + "_" + "PNS"[category] + (index + 1);
        public static readonly WardenEvent[] All = Build().ToArray();
        private static IEnumerable<WardenEvent> Build()
        {
            foreach (var role in Roles)
                for (int category = 0; category < 3; category++)
                    for (int i = 0; i < 5; i++)
                        yield return new WardenEvent { Id = role + new[] { "Positive", "Negative", "Social" }[category] + i, Role = role, Fact = Fact(role, category, i), Thought = Thought(role, category, i), Mood = category == 0 ? 2 + i % 2 : category == 1 ? -2 - i % 2 : 0, Opinion = category == 2 ? (i < 3 ? 3 : i == 3 ? -3 : -4) : 0 };
        }
        public static IEnumerable<WardenEvent> Eligible(string role, string fact, bool other) => All.Where(e => e.Matches(role, fact, other));
    }
}
