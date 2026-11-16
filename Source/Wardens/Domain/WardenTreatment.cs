using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Framework;

namespace FamilyLife.Domain
{
    /// <summary>How well a prisoner was looked after at one look: fed, tended and in a decent mood.</summary>
    public enum PrisonerCare : byte { Poor = 0, Fair = 1, Good = 2 }

    /// <summary>What finally became of a prisoner.</summary>
    public enum PrisonerOutcome : byte { Recruited = 0, Released = 1, Enslaved = 2, Executed = 3, Died = 4 }

    public static class WardenCare
    {
        public const float GoodFood = .30f, GoodMood = .40f, PoorFood = .15f, PoorMood = .20f;

        /// <summary>
        /// Good: fed, in a decent mood and not in need of tending. Poor: starving, miserable or needing tending right now. Anything between is Fair. Food and mood are fractions from 0 to 1.
        /// </summary>
        public static PrisonerCare Rate(float food, float mood, bool needsTending)
        {
            if (needsTending || food < PoorFood || mood < PoorMood) return PrisonerCare.Poor;
            return food >= GoodFood && mood >= GoodMood ? PrisonerCare.Good : PrisonerCare.Fair;
        }
    }

    public static class PrisonerOutcomes
    {
        /// <summary>
        /// What happened to someone who is no longer a prisoner. Dead: executed if the colony had set them to be executed, otherwise died in custody. Alive: a slave of the colony was enslaved, any other
        /// member of the colony was recruited, and anyone else was released.
        /// </summary>
        public static PrisonerOutcome Classify(bool dead, bool executionMode, bool colonist, bool slave)
        {
            if (dead) return executionMode ? PrisonerOutcome.Executed : PrisonerOutcome.Died;
            if (slave) return PrisonerOutcome.Enslaved;
            return colonist ? PrisonerOutcome.Recruited : PrisonerOutcome.Released;
        }
    }

    /// <summary>How a prisoner's relatives in the colony feel: a mood thought for themselves and, when a warden was responsible, an opinion of that warden. Both are thoughts defined in the package's Defs.</summary>
    public sealed class KinReaction { public string Mood, Opinion; }

    public static class KinReactions
    {
        public const string WellCared = "WFL_W_KinWellCared", Neglected = "WFL_W_KinNeglected", Recruited = "WFL_W_KinRecruited", Freed = "WFL_W_KinFreed", Enslaved = "WFL_W_KinEnslaved", Executed = "WFL_W_KinExecuted", Died = "WFL_W_KinDied";
        public const string Grateful = "WFL_W_KinGrateful", Resents = "WFL_W_KinResents", Favouritism = "WFL_W_Favouritism", Conflicted = "WFL_W_Conflicted";
        public static readonly string[] AllThoughts = { WellCared, Neglected, Recruited, Freed, Enslaved, Executed, Died, Grateful, Resents, Favouritism, Conflicted };

        /// <summary>Good care pleases them and poor care angers them; Fair care changes nothing.</summary>
        public static KinReaction ForCare(PrisonerCare level) => level == PrisonerCare.Good ? new KinReaction { Mood = WellCared, Opinion = Grateful } : level == PrisonerCare.Poor ? new KinReaction { Mood = Neglected, Opinion = Resents } : null;

        public static KinReaction For(PrisonerOutcome outcome)
        {
            switch (outcome)
            {
                case PrisonerOutcome.Recruited: return new KinReaction { Mood = Recruited, Opinion = Grateful };
                case PrisonerOutcome.Released: return new KinReaction { Mood = Freed, Opinion = Grateful };
                case PrisonerOutcome.Enslaved: return new KinReaction { Mood = Enslaved, Opinion = Resents };
                case PrisonerOutcome.Executed: return new KinReaction { Mood = Executed, Opinion = Resents };
                default: return new KinReaction { Mood = Died, Opinion = Resents };
            }
        }
    }

    public static class Favouritism
    {
        public const int MinSamples = 6, MinGapPercent = 40;
        /// <summary>
        /// A warden who looks after a relative's prisoner much better than everyone else's: at least <see cref="MinSamples"/> looks at each, and a share of good care that differs by at least
        /// <see cref="MinGapPercent"/> points. Going easy or hard on a relative are both noticed (a gap in either direction). Whole-number arithmetic, so a gap of exactly forty points counts.
        /// </summary>
        public static bool Detected(int kinGood, int kinTotal, int otherGood, int otherTotal)
        {
            if (kinTotal < MinSamples || otherTotal < MinSamples) return false;
            long gap = Math.Abs((long)kinGood * otherTotal - (long)otherGood * kinTotal);          // the gap in shares, scaled by both totals
            return gap * 100 >= (long)MinGapPercent * kinTotal * otherTotal;
        }
    }

    /// <summary>What the colony knows about its wardens' care and the fates of its prisoners (saved as <c>wils.wardens.record</c>). Bounded: counters are halved when they grow, and records of people who are gone are dropped.</summary>
    public sealed class WardenLedger : IModuleState
    {
        public const string StateId = "wils.wardens.record";
        public const int KeepResponsibleTicks = 5 * 60000, FavouritismCooldown = 7 * 60000, CounterCap = 400, MaxPrisoners = 200;
        public string Id => StateId;

        public sealed class Stats
        {
            public int Good, Fair, Poor, KinGood, KinTotal, OtherGood, OtherTotal, Recruited, Released, Enslaved, Executed, Died, LastFavouritism;
            public int Samples => Good + Fair + Poor;
            public int Outcomes => Recruited + Released + Enslaved + Executed + Died;
        }

        private readonly Dictionary<int, Stats> wardens = new Dictionary<int, Stats>();
        private readonly Dictionary<int, KeyValuePair<int, int>> responsible = new Dictionary<int, KeyValuePair<int, int>>();          // prisoner -> (warden, tick)
        public IEnumerable<int> Wardens => wardens.Keys;
        public Stats Of(int warden) { if (!wardens.TryGetValue(warden, out var s)) wardens[warden] = s = new Stats(); return s; }
        public bool Has(int warden) => wardens.ContainsKey(warden);

        /// <summary>Notes that a warden has just looked after a prisoner: they are the one responsible for them for the next few days.</summary>
        public void Assign(int prisoner, int warden, int now)
        {
            if (prisoner <= 0 || warden <= 0) return;
            if (!responsible.ContainsKey(prisoner) && responsible.Count >= MaxPrisoners) { int oldest = responsible.OrderBy(x => x.Value.Value).First().Key; responsible.Remove(oldest); }
            responsible[prisoner] = new KeyValuePair<int, int>(warden, now);
        }

        /// <summary>The warden responsible for a prisoner, or 0 if nobody has looked after them lately.</summary>
        public int WardenOf(int prisoner, int now) => responsible.TryGetValue(prisoner, out var r) && (now < r.Value || now - r.Value <= KeepResponsibleTicks) ? r.Key : 0;

        public void Rate(int warden, PrisonerCare level, bool kin)
        {
            var s = Of(warden);
            if (level == PrisonerCare.Good) s.Good++; else if (level == PrisonerCare.Fair) s.Fair++; else s.Poor++;
            if (kin) { s.KinTotal++; if (level == PrisonerCare.Good) s.KinGood++; } else { s.OtherTotal++; if (level == PrisonerCare.Good) s.OtherGood++; }
            if (s.Samples > CounterCap || s.KinTotal > CounterCap || s.OtherTotal > CounterCap) Halve(s);
        }

        private static void Halve(Stats s) { s.Good /= 2; s.Fair /= 2; s.Poor /= 2; s.KinGood /= 2; s.KinTotal /= 2; s.OtherGood /= 2; s.OtherTotal /= 2; }

        public void Outcome(int warden, PrisonerOutcome outcome)
        {
            if (warden <= 0) return; var s = Of(warden);
            switch (outcome) { case PrisonerOutcome.Recruited: s.Recruited++; break; case PrisonerOutcome.Released: s.Released++; break; case PrisonerOutcome.Enslaved: s.Enslaved++; break; case PrisonerOutcome.Executed: s.Executed++; break; default: s.Died++; break; }
        }

        /// <summary>Whether to tell colleagues this warden plays favourites. At most once a week; telling halves the kin and other counts so the same gap is not announced again at once.</summary>
        public bool TakeFavouritism(int warden, int now)
        {
            if (!wardens.TryGetValue(warden, out var s) || !Favouritism.Detected(s.KinGood, s.KinTotal, s.OtherGood, s.OtherTotal)) return false;
            if (s.LastFavouritism > 0 && now >= s.LastFavouritism && now - s.LastFavouritism < FavouritismCooldown) return false;
            s.LastFavouritism = now; s.KinGood /= 2; s.KinTotal /= 2; s.OtherGood /= 2; s.OtherTotal /= 2; return true;
        }

        /// <summary>Drops wardens who are gone and prisoners whose warden is gone. Returns how many were dropped.</summary>
        public int Prune(Func<int, bool> wardenExists)
        {
            var gone = wardens.Keys.Where(w => !wardenExists(w)).ToList(); foreach (int w in gone) wardens.Remove(w);
            var orphans = responsible.Where(p => !wardenExists(p.Value.Key)).Select(p => p.Key).ToList(); foreach (int p in orphans) responsible.Remove(p);
            return gone.Count + orphans.Count;
        }

        public string Encode()
        {
            var ids = wardens.Keys.OrderBy(x => x).ToList(); Func<Func<Stats, int>, List<int>> col = f => ids.Select(i => f(wardens[i])).ToList();
            var pr = responsible.Keys.OrderBy(x => x).ToList();
            return new StateWriter().Ints("w", ids).Ints("good", col(s => s.Good)).Ints("fair", col(s => s.Fair)).Ints("poor", col(s => s.Poor)).Ints("kg", col(s => s.KinGood)).Ints("kt", col(s => s.KinTotal)).Ints("og", col(s => s.OtherGood)).Ints("ot", col(s => s.OtherTotal))
                .Ints("rec", col(s => s.Recruited)).Ints("rel", col(s => s.Released)).Ints("ens", col(s => s.Enslaved)).Ints("exe", col(s => s.Executed)).Ints("die", col(s => s.Died)).Ints("fav", col(s => s.LastFavouritism))
                .Ints("p", pr).Ints("pw", pr.Select(i => responsible[i].Key).ToList()).Ints("pt", pr.Select(i => responsible[i].Value).ToList()).ToString();
        }

        public static WardenLedger Decode(StateReader r)
        {
            var l = new WardenLedger(); var ids = r.Ints("w");
            Func<string, List<int>> col = k => r.Ints(k); var good = col("good"); var fair = col("fair"); var poor = col("poor"); var kg = col("kg"); var kt = col("kt"); var og = col("og"); var ot = col("ot");
            var rec = col("rec"); var rel = col("rel"); var ens = col("ens"); var exe = col("exe"); var die = col("die"); var fav = col("fav");
            Func<List<int>, int, int> at = (list, i) => i < list.Count ? Math.Max(0, list[i]) : 0;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] > 0 && !l.wardens.ContainsKey(ids[i])) l.wardens[ids[i]] = new Stats { Good = at(good, i), Fair = at(fair, i), Poor = at(poor, i), KinGood = at(kg, i), KinTotal = at(kt, i), OtherGood = at(og, i), OtherTotal = at(ot, i), Recruited = at(rec, i), Released = at(rel, i), Enslaved = at(ens, i), Executed = at(exe, i), Died = at(die, i), LastFavouritism = at(fav, i) };
            var p = r.Ints("p"); var pw = r.Ints("pw"); var pt = r.Ints("pt");
            for (int i = 0; i < p.Count && i < pw.Count && i < pt.Count && l.responsible.Count < MaxPrisoners; i++) if (p[i] > 0 && pw[i] > 0) l.responsible[p[i]] = new KeyValuePair<int, int>(pw[i], pt[i]);
            return l;
        }
    }

    public sealed class WardenLedgerFactory : IStateFactory<WardenLedger>
    {
        public static readonly WardenLedgerFactory Instance = new WardenLedgerFactory();
        public int Version => 1;
        public WardenLedger Create() => new WardenLedger();
        public WardenLedger Decode(StateReader reader, int version) => WardenLedger.Decode(reader);
        public WardenLedger FromLegacy(StateReader legacy) => Create();
    }

    public static class WardenStateAccess
    {
        public static WardenLedger Wardens(this FamilyRegistry r) => r.Shared.Of(WardenLedger.StateId, WardenLedgerFactory.Instance);
    }
}
