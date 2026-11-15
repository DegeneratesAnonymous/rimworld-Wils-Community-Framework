using System.Collections.Generic;
using System.Linq;

namespace FamilyLife.Domain
{
    public sealed class MilitaryEvent
    {
        public string Id, Role, Fact, Thought; public int Mood, Opinion;
        public bool Matches(string role, string fact, bool other) => Role == role && Fact == fact && (Opinion == 0 || other);
    }

    /// <summary>Every Military role has five positive, five negative and five relationship events; the thought for each is in the package's Defs. Generated from one table so the facts and the thoughts cannot drift apart.</summary>
    public static class MilitaryEvents
    {
        public static readonly string[] Roles = { "Recruit", "Soldier", "Ranger", "Vanguard", "Guardian", "Duelist", "Marksman", "Gunner", "Sniper" };
        /// <summary>Facts for each role, in trigger order. Positive: routine success, outstanding result, milestone, recognised by others, pride. Negative: failed work, waste or setback, strain, rejected, danger or harm. Social: praised, helped, shared know-how, dispute, betrayal or stolen credit.</summary>
        private static readonly Dictionary<string, string[][]> facts = new Dictionary<string, string[][]>
        {
            { "Recruit", new[] { new[] { "HeldMyGround", "FirstKillCleanly", "PassedTrial", "PraisedByComrades", "FoundPurpose" }, new[] { "FrozeUp", "WastedShots", "SoreAndWorn", "ScoldedPublicly", "FirstBlood" }, new[] { "StoodByMe", "CoveredMe", "TaughtMeStance", "MockedMyAim", "LeftMeBehind" } } },
            { "Soldier", new[] { new[] { "LineHeld", "RaiderRepelled", "EarnedStripes", "HonouredByColony", "SoldiersPride" }, new[] { "LineBroke", "SquanderedLife", "WarWeariness", "OrdersIgnored", "WoundedInAction" }, new[] { "ComradeInArms", "PulledMeOut", "DrillTogether", "BlamedForRout", "AbandonedPost" } } },
            { "Ranger", new[] { new[] { "ShotsLanded", "PickedTheirShooter", "EarnedTheBadge", "SteadyHandNoted", "OutOfDoorsHome" }, new[] { "JammedWeapon", "AmmoWasted", "LongWatchWear", "SpottedPosition", "FriendlyFireScare" }, new[] { "PraisedShooting", "SpottedForMe", "TaughtMeWind", "CalledMeReckless", "SteppedInFront" } } },
            { "Vanguard", new[] { new[] { "BrokeTheirLine", "FirstThroughGap", "HeldThePush", "FollowedMyLead", "BornToLead" }, new[] { "ChargeStalled", "LostMyFlank", "BruisedAndBattered", "RecklessCalled", "CutDownLeading" }, new[] { "FollowedMeIn", "CoveredMyCharge", "ShowedMeArmor", "BlamedTheCharge", "HungMeOut" } } },
            { "Guardian", new[] { new[] { "ShieldedAFriend", "HeldTheDoor", "NoOneFell", "ThankedByTheSaved", "ProtectorsPeace" }, new[] { "FriendFell", "BreachBehindMe", "ShieldWorn", "ProtectionRefused", "FailedToProtect" }, new[] { "OweMyLife", "TendedMyWounds", "StoodWithMe", "CalledMeSmothering", "LeftTheirPost" } } },
            { "Duelist", new[] { new[] { "CleanDuel", "StruckFirst", "MasterstrokeLanded", "DuelWatched", "DancerOfBlades" }, new[] { "MisjudgedOpponent", "DuelLost", "OutnumberedBad", "DuelRefused", "StruckDown" }, new[] { "RespectedTheBlade", "SparredWithMe", "TaughtMeFeint", "ChallengedMe", "DishonouredDuel" } } },
            { "Marksman", new[] { new[] { "CleanHit", "ThreatDropped", "ShotOfTheDay", "SpokenOfAim", "TrueAim" }, new[] { "MissedImportant", "OpenedTooSoon", "RifleStrain", "AimQuestioned", "ShotWentWide" }, new[] { "ThankedForCover", "CalledTargets", "ShowedMeZero", "WonBetOnMe", "TookMyKill" } } },
            { "Gunner", new[] { new[] { "HeadsDown", "SuppressedThem", "FireDiscipline", "CoverFireThanks", "RoarOfGuns" }, new[] { "ThrewRoundsAway", "BarrelBurned", "EarsRinging", "NoAmmoLeft", "HitOurOwn" }, new[] { "MovedUnderMyFire", "ReloadedForMe", "ShowedMeBursts", "WastefulCalled", "FrozeMyFire" } } },
            { "Sniper", new[] { new[] { "OneShotOneKill", "ThreatEliminated", "PatienceRewarded", "WatchedOverThem", "QuietPride" }, new[] { "ShotLost", "WaitForNothing", "StiffAndCold", "PositionCompromised", "ExposedAlone" }, new[] { "TrustedMyWatch", "SpottedForSniper", "ShowedMeBreath", "CalledMeCold", "GaveAwayPosition" } } },
        };
        public static string Fact(string role, int category, int index) => facts.TryGetValue(role, out var f) && category >= 0 && category < 3 && index >= 0 && index < 5 ? f[category][index] : null;
        public const int Positive = 0, Negative = 1, Social = 2;
        public static string Thought(string role, int category, int index) => "WFL_M_" + role + "_" + "PNS"[category] + (index + 1);
        public static readonly MilitaryEvent[] All = Build().ToArray();
        private static IEnumerable<MilitaryEvent> Build()
        {
            foreach (var role in Roles)
                for (int category = 0; category < 3; category++)
                    for (int i = 0; i < 5; i++)
                        yield return new MilitaryEvent { Id = role + new[] { "Positive", "Negative", "Social" }[category] + i, Role = role, Fact = Fact(role, category, i), Thought = Thought(role, category, i), Mood = category == 0 ? 2 + i % 2 : category == 1 ? -2 - i % 2 : 0, Opinion = category == 2 ? (i < 3 ? 3 : i == 3 ? -3 : -4) : 0 };
        }
        public static IEnumerable<MilitaryEvent> Eligible(string role, string fact, bool other) => All.Where(e => e.Matches(role, fact, other));
    }
}
