using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    public sealed class WorksEvent
    {
        public string Id, Role, Fact, Thought; public int Mood, Opinion;
        public bool Matches(string role, string fact, bool other) => Role == role && Fact == fact && (Opinion == 0 || other);
    }

    /// <summary>Every Works role has five positive, five negative and five relationship events; the thought for each is in the package's Defs. Generated from one table so the facts and the thoughts cannot drift apart.</summary>
    public static class WorksEvents
    {
        public static readonly string[] Roles = { "BuilderLabourer", "BuilderJourneyman", "Mason", "Carpenter", "Engineer", "MinerLabourer", "MinerJourneyman", "Prospector", "Quarryman", "Tunneller" };
        /// <summary>Facts for each role, in trigger order. Positive: routine success, outstanding result, milestone, recognised by others, pride. Negative: failed work, waste or setback, strain, rejected, danger or harm. Social: praised, helped, shared know-how, dispute, betrayal or stolen credit.</summary>
        private static readonly Dictionary<string, string[][]> facts = new Dictionary<string, string[][]>
        {
            { "BuilderLabourer", new[] { new[] { "CarriedItWell", "LaidItTrue", "MyFirstWall", "NoticedOnTheSite", "HonestWork" }, new[] { "DroppedTheLoad", "WastedMaterial", "AchingAllOver", "TalkedOver", "ACloseCall" }, new[] { "PraisedMyEffort", "LentAHand", "ShowedMeTheKnack", "BlamedMeForTheDelay", "TookMyIdea" } } },
            { "BuilderJourneyman", new[] { new[] { "ASoundBuild", "BuiltItFast", "MyHundredthJob", "TheCrewLooksToMe", "PrideInTheTrade" }, new[] { "AFlawedJoint", "ToreItDown", "WornByThePace", "Overruled", "AFallFromHeight" }, new[] { "PraisedMyCraft", "SteadiedTheFrame", "PassedOnATrick", "ArguedOverThePlan", "ClaimedTheBuild" } } },
            { "Mason", new[] { new[] { "ATrueWall", "AFortressInStone", "RaisedTheGreatHall", "AdmiredForMyStonework", "StoneRemembers" }, new[] { "ACrackedCourse", "LostAWeekToWeather", "DustInMyLungs", "MyDesignSetAside", "AWallCameDown" }, new[] { "PraisedMyWalls", "FetchedTheStone", "ShowedMeTheBond", "SaidItWasOverbuilt", "TookMyPlan" } } },
            { "Carpenter", new[] { new[] { "APerfectFit", "APieceToBeProudOf", "FurnishedTheWholeHouse", "SoughtOutForMyWork", "TheJoyOfWood" }, new[] { "WarpedTimber", "WastedAFineBoard", "SawdustAndSoreHands", "MyPieceWasIgnored", "AChiselSlipped" }, new[] { "PraisedTheFinish", "HeldTheBoardSteady", "ShowedMeTheDovetail", "SaidItWobbled", "SoldMyPieceAsTheirs" } } },
            { "Engineer", new[] { new[] { "ItRunsClean", "ACleverSolution", "ThePowerIsOn", "ConsultedOnTheGrid", "TheOrderOfMachines" }, new[] { "AShortInTheLine", "BurnedOutThePart", "WiredForDays", "MyWarningIgnored", "TheSparksFlew" }, new[] { "PraisedTheDesign", "HandedMeTheTool", "ExplainedTheCircuit", "BlamedTheOutageOnMe", "TookTheCreditForMyFix" } } },
            { "MinerLabourer", new[] { new[] { "DugMyShare", "ClearedTheFace", "MyFirstTunnel", "NoticedInThePit", "HonestDust" }, new[] { "SwungWide", "WastedTheRock", "CoughingFromTheDust", "ShoutedDown", "ANearMiss" }, new[] { "PraisedMyDigging", "ClearedMyRubble", "ShowedMeTheSwing", "SaidIWasTooSlow", "ClaimedMySeam" } } },
            { "MinerJourneyman", new[] { new[] { "ACleanCut", "StruckAGoodSeam", "MyHundredthShift", "TheCrewFollowsMe", "ThePrideOfThePit" }, new[] { "HitBadGround", "ABlindDrift", "WornByTheDark", "MySurveyWasDoubted", "ACollapseNearMe" }, new[] { "PraisedMyTunnel", "ShoredItForMe", "ShowedMeTheVein", "ArguedOverTheRoute", "TookCreditForTheFind" } } },
            { "Prospector", new[] { new[] { "StruckOre", "ARichVein", "MappedTheWholeHill", "TrustedToFindIt", "TheThrillOfTheFind" }, new[] { "DryHole", "AFalseLead", "WornByTheSearch", "MySurveyWasDismissed", "LostInTheTunnels" }, new[] { "PraisedMyEye", "CarriedTheSamples", "TaughtMeToReadTheRock", "DoubtedMyFind", "SoldMyMap" } } },
            { "Quarryman", new[] { new[] { "ACleanBlock", "AGreatHaulOfStone", "OpenedANewFace", "KnownForMyBlocks", "StoneFromTheMountain" }, new[] { "ShatteredTheBlock", "ARuinedFace", "AchingShoulders", "MyMethodWasOverruled", "ASlabSlipped" }, new[] { "PraisedTheBlocks", "MovedTheStone", "ShowedMeWhereToSplit", "SaidIWasWasteful", "TookMyQuarry" } } },
            { "Tunneller", new[] { new[] { "ASafePassage", "AFastDig", "BrokeThroughToTheCave", "TrustedUnderground", "AGoodTunnel" }, new[] { "TheRoofGroaned", "LostADayToACollapse", "TheDarkIsHeavy", "MyWarningWasBrushedAside", "TrappedForHours" }, new[] { "PraisedMyShoring", "HeldTheLamp", "ShowedMeTheSigns", "SaidIWasTooCareful", "TookTheWayIFound" } } },
        };
        public static string Fact(string role, int category, int index) => facts.TryGetValue(role, out var f) && category >= 0 && category < 3 && index >= 0 && index < 5 ? f[category][index] : null;
        public const int Positive = 0, Negative = 1, Social = 2;
        public static string Thought(string role, int category, int index) => "WFL_K_" + role + "_" + "PNS"[category] + (index + 1);
        public static readonly WorksEvent[] All = Build().ToArray();
        private static IEnumerable<WorksEvent> Build()
        {
            foreach (var role in Roles)
                for (int category = 0; category < 3; category++)
                    for (int i = 0; i < 5; i++)
                        yield return new WorksEvent { Id = role + new[] { "Positive", "Negative", "Social" }[category] + i, Role = role, Fact = Fact(role, category, i), Thought = Thought(role, category, i), Mood = category == 0 ? 2 + i % 2 : category == 1 ? -2 - i % 2 : 0, Opinion = category == 2 ? (i < 3 ? 3 : i == 3 ? -3 : -4) : 0 };
        }
        public static IEnumerable<WorksEvent> Eligible(string role, string fact, bool other) => All.Where(e => e.Matches(role, fact, other));
    }
}
