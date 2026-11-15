using System;
using System.Collections.Generic;

namespace FamilyLife.Domain
{
    /// <summary>
    /// The Military career. Recruit (anyone) qualifies into Soldier (line and melee) or Ranger (ranged), each parallel and earned from real hostile combat. Each branch then has three
    /// specialties, and every specialty has a different job on the battlefield (see <see cref="MilitaryDoctrine"/>). Owner: Military.
    /// </summary>
    public static class MilitaryCareers
    {
        public const string Module = "wils.military", Career = "wils.military.military";
        public const string Recruit = "wils.military.recruit";
        public static string Role(string name) => Module + "." + name.ToLowerInvariant();

        public static readonly string[] Branches = { "Soldier", "Ranger" };
        public static readonly string[] SoldierSpecialties = { "Vanguard", "Guardian", "Duelist" };
        public static readonly string[] RangerSpecialties = { "Marksman", "Gunner", "Sniper" };
        public static readonly string[] Specialties = { "Vanguard", "Guardian", "Duelist", "Marksman", "Gunner", "Sniper" };

        /// <summary>The branch a specialty grows out of.</summary>
        public static string BranchOf(string specialty) => Array.IndexOf(SoldierSpecialties, specialty) >= 0 ? "Soldier" : Array.IndexOf(RangerSpecialties, specialty) >= 0 ? "Ranger" : null;

        public static CareerDefinition Definition() => new CareerDefinition
        {
            Id = Career, ModuleId = Module, LabelKey = "WFL_Military_Career",
            Level2Branches = new Dictionary<string, CareerRule>
            {
                { "Soldier", new CareerRule { GeneralXp = 120, SpecialtyXp = 60 }.Skill("Melee", 6) },
                { "Ranger", new CareerRule { GeneralXp = 120, SpecialtyXp = 60 }.Skill("Shooting", 6) }
            },
            Specialties = new Dictionary<string, CareerRule>
            {
                { "Vanguard", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Melee", 10).After("Soldier") },
                { "Guardian", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Melee", 8).Skill("Medicine", 3).After("Soldier") },
                { "Duelist", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Melee", 12).After("Soldier") },
                { "Marksman", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Shooting", 10).After("Ranger") },
                { "Gunner", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Shooting", 8).After("Ranger") },
                { "Sniper", new CareerRule { GeneralXp = 250, SpecialtyXp = 60 }.Skill("Shooting", 12).After("Ranger") }
            }
        };

        /// <summary>Experience for one hostile engagement: a first blow on an enemy, and a kill. A repeat of the same fight earns nothing (the ledger keys it by enemy).</summary>
        public const int EngagePoints = 3, KillPoints = 10;
    }

    // ------------------------------------------------------------------ what happened in a fight

    /// <summary>The few facts about one blow or kill that decide which route and specialty it is credited to. Gathered by the game side; pure here so it is tested.</summary>
    public sealed class CombatFacts
    {
        public bool Ranged;
        /// <summary>Cells between attacker and enemy.</summary>
        public float Distance;
        /// <summary>Shots per burst of the weapon used (1 for a single shot or a melee blow).</summary>
        public int Burst = 1;
        /// <summary>Enemies within a few cells of the attacker, not counting the one being fought.</summary>
        public int OtherEnemiesNear;
        /// <summary>The enemy had turned on someone else (an ally) rather than the attacker, or a friend was down within reach.</summary>
        public bool DefendedAlly;
        /// <summary>The attacker's own sharp-armour rating, for telling a heavily armoured breaker from a light skirmisher.</summary>
        public float Armor;
    }

    public sealed class CombatTrack { public string Route, Specialty; }

    public static class CombatClassifier
    {
        public const float SniperRange = 25f, GunnerRange = 12f, HeavyArmor = .5f;
        public const int GunnerBurst = 3;

        /// <summary>
        /// Melee: a defender if the fight was for an ally, a duelist if it was one-on-one, otherwise a vanguard (pressing into several, or doing it in heavy armour).
        /// Ranged: a sniper from far away, a gunner for a burst weapon at short range, otherwise a marksman.
        /// </summary>
        public static CombatTrack Classify(CombatFacts f)
        {
            if (f == null) return null;
            if (!f.Ranged)
            {
                if (f.DefendedAlly) return new CombatTrack { Route = "Soldier", Specialty = "Guardian" };
                if (f.OtherEnemiesNear >= 2 || f.Armor >= HeavyArmor) return new CombatTrack { Route = "Soldier", Specialty = "Vanguard" };
                if (f.OtherEnemiesNear == 0) return new CombatTrack { Route = "Soldier", Specialty = "Duelist" };
                return new CombatTrack { Route = "Soldier", Specialty = "Vanguard" };
            }
            if (f.Distance >= SniperRange) return new CombatTrack { Route = "Ranger", Specialty = "Sniper" };
            if (f.Burst >= GunnerBurst && f.Distance <= GunnerRange) return new CombatTrack { Route = "Ranger", Specialty = "Gunner" };
            return new CombatTrack { Route = "Ranger", Specialty = "Marksman" };
        }
    }

    // ------------------------------------------------------------------ doctrine: what each specialty is for

    /// <summary>What is going on around one soldier right now. The game side reads it (cached for a short time) and the doctrine decides from it; nothing here touches the game.</summary>
    public sealed class CombatContext
    {
        /// <summary>The soldier has an enemy in view or is fighting one.</summary>
        public bool InCombat;
        /// <summary>Cells to the enemy the soldier is facing or shooting; 0 when there is none.</summary>
        public float TargetDistance;
        /// <summary>Hostile pawns within five cells of the soldier.</summary>
        public int HostilesNear;
        /// <summary>A friend within six cells is downed, or an enemy is next to a friend and the soldier is close enough to cover them.</summary>
        public bool CoveringAlly;
        /// <summary>The soldier has not moved for a moment (set up and waiting).</summary>
        public bool Stationary;
    }

    /// <summary>What a target looks like to a shooter choosing between enemies.</summary>
    public sealed class TargetFacts
    {
        public float Distance;
        /// <summary>The enemy is carrying a ranged weapon.</summary>
        public bool Armed, Ranged;
        /// <summary>The enemy is standing in the open, with no cover from the shooter.</summary>
        public bool Exposed;
        /// <summary>The enemy is currently attacking one of our people.</summary>
        public bool AttackingAlly;
        /// <summary>How dangerous the enemy's weapon is, 0 to 1 (best damage per second against our best).</summary>
        public float Threat;
    }

    /// <summary>
    /// Each combat specialty has one distinct job and pays for it with a weakness, so none is simply better than another. The numbers are bounded multipliers, applied only while the pawn is
    /// qualified and assigned to that exact specialty, and only in the situation the specialty is for. Nothing here forces a pawn to move or to shoot: the doctrine changes what each does
    /// well, and which enemy a shooter prefers when the game is choosing.
    /// </summary>
    public sealed class DoctrineProfile
    {
        public string Role, Job, Strength, Weakness;
        /// <summary>Where this role wants to fight, in cells: closest, preferred, farthest. Not enforced by the game; used for scoring and described in the UI.</summary>
        public float MinRange, PreferredRange, MaxRange;
    }

    public static class MilitaryDoctrine
    {
        // Stat names the game side extends. Kept as strings so this file knows no game types.
        public const string MoveSpeed = "MoveSpeed", MeleeDamage = "MeleeDamageFactor", MeleeHit = "MeleeHitChance", MeleeDodge = "MeleeDodgeChance", Incoming = "IncomingDamageFactor",
            Accuracy = "ShootingAccuracyPawn", AimDelay = "AimingDelayFactor", Cooldown = "RangedCooldownFactor";
        public static readonly string[] Stats = { MoveSpeed, MeleeDamage, MeleeHit, MeleeDodge, Incoming, Accuracy, AimDelay, Cooldown };

        /// <summary>The bound on how far any single doctrine factor moves a stat, in either direction.</summary>
        public const float MaxUp = 1.25f, MaxDown = .80f;

        public static readonly DoctrineProfile[] Profiles =
        {
            new DoctrineProfile { Role = "Vanguard", Job = "Breaker: closes fast and breaks the enemy's line, going for their shooters.", Strength = "Moves faster while closing; hits harder once engaged.", Weakness = "Exposed while charging: harder to dodge with.", MinRange = 0, PreferredRange = 2, MaxRange = 20 },
            new DoctrineProfile { Role = "Guardian", Job = "Anchor: holds the line and covers wounded and downed friends.", Strength = "Takes less damage while covering an ally.", Weakness = "Heavy on their feet and no better at killing.", MinRange = 0, PreferredRange = 2, MaxRange = 6 },
            new DoctrineProfile { Role = "Duelist", Job = "Skirmisher: wins a one-on-one and picks off an enemy who has strayed from the pack.", Strength = "Hits and dodges better against a single opponent.", Weakness = "Dodges worse once outnumbered.", MinRange = 0, PreferredRange = 1.5f, MaxRange = 5 },
            new DoctrineProfile { Role = "Marksman", Job = "Precision: steady, accurate fire at the middle distance on whoever is exposed.", Strength = "More accurate between 12 and 30 cells.", Weakness = "Less accurate point-blank.", MinRange = 8, PreferredRange = 20, MaxRange = 35 },
            new DoctrineProfile { Role = "Gunner", Job = "Suppression: volume of fire that keeps the enemy's heads down at short range.", Strength = "Fires more often; more accurate up close.", Weakness = "Less accurate at long range.", MinRange = 0, PreferredRange = 10, MaxRange = 18 },
            new DoctrineProfile { Role = "Sniper", Job = "Overwatch: patient long-range kills on the enemy's most dangerous.", Strength = "Much more accurate from far away once set up.", Weakness = "Slower to aim, and poor close in.", MinRange = 15, PreferredRange = 32, MaxRange = 45 }
        };

        public static DoctrineProfile Profile(string role) { foreach (var p in Profiles) if (p.Role == role) return p; return null; }

        private static float Clamp(float f) => Math.Max(MaxDown, Math.Min(MaxUp, f));

        /// <summary>
        /// The multiplier a role applies to one stat in this situation (1 means no effect). Returned values are always within [<see cref="MaxDown"/>, <see cref="MaxUp"/>].
        /// An unknown role, a stat the role does not touch, or no combat gives exactly 1.
        /// </summary>
        public static float Factor(string role, string stat, CombatContext c)
        {
            if (role == null || stat == null || c == null || !c.InCombat) return 1f;
            float d = c.TargetDistance;
            switch (role)
            {
                case "Vanguard":
                    if (stat == MoveSpeed && d > 2f && d <= 20f) return Clamp(1.20f);          // closing on the enemy
                    if (stat == MeleeDamage && d <= 2f) return Clamp(1.10f);          // once engaged
                    if (stat == MeleeDodge && d <= 20f) return Clamp(.85f);          // the cost of leading with the chest
                    break;
                case "Guardian":
                    if (stat == Incoming && c.CoveringAlly) return Clamp(.85f);
                    if (stat == MoveSpeed) return Clamp(.92f);
                    break;
                case "Duelist":
                    if (c.HostilesNear <= 1)
                    {
                        if (stat == MeleeHit) return Clamp(1.15f);
                        if (stat == MeleeDodge) return Clamp(1.20f);
                    }
                    else if (c.HostilesNear >= 3 && stat == MeleeDodge) return Clamp(.85f);
                    break;
                case "Marksman":
                    if (stat == Accuracy) return d >= 12f && d <= 30f ? Clamp(1.10f) : d > 0f && d < 6f ? Clamp(.90f) : 1f;
                    break;
                case "Gunner":
                    if (stat == Cooldown) return Clamp(.85f);          // a smaller cooldown fires more often
                    if (stat == Accuracy) return d > 0f && d <= 12f ? Clamp(1.05f) : d > 20f ? Clamp(.92f) : 1f;
                    break;
                case "Sniper":
                    if (stat == Accuracy) return d >= 25f && c.Stationary ? Clamp(1.15f) : d > 0f && d < 10f ? Clamp(.85f) : 1f;
                    if (stat == AimDelay) return Clamp(1.25f);          // patient: slower to settle, so the delay factor is larger
                    break;
            }
            return 1f;
        }

        /// <summary>The effect of a role on a stat, described for the tooltip: null when it has none.</summary>
        public static bool Touches(string role, string stat)
        {
            var c = new CombatContext { InCombat = true };
            foreach (float d in new[] { 1f, 8f, 20f, 30f })
            foreach (bool cover in new[] { false, true })
            foreach (int near in new[] { 0, 1, 3 })
            foreach (bool still in new[] { false, true })
            {
                c.TargetDistance = d; c.CoveringAlly = cover; c.HostilesNear = near; c.Stationary = still;
                if (Math.Abs(Factor(role, stat, c) - 1f) > .0001f) return true;
            }
            return false;
        }

        /// <summary>
        /// How much a shooter of this role prefers one enemy to another. Added to the game's own shooting score (which is on a scale of tens), so it nudges the choice without overriding
        /// the game's sense of danger. Bounded to [-<see cref="MaxBias"/>, <see cref="MaxBias"/>]. Melee roles have no bias: the game picks their opponent by distance.
        ///   Marksman - the exposed, and the armed. Gunner - the nearest and the many. Sniper - the most dangerous and the armed, and nothing close.
        /// </summary>
        public const float MaxBias = 15f;
        public static float TargetBias(string role, TargetFacts t)
        {
            if (role == null || t == null) return 0f;
            float bias = 0f;
            switch (role)
            {
                case "Marksman":
                    if (t.Exposed) bias += 8f;
                    if (t.Armed && t.Ranged) bias += 5f;
                    if (t.Distance < 8f) bias -= 4f;
                    break;
                case "Gunner":
                    bias += Math.Max(0f, 12f - t.Distance) * .8f;          // nearest first
                    if (t.AttackingAlly) bias += 4f;
                    break;
                case "Sniper":
                    bias += t.Threat * 10f;
                    if (t.Armed && t.Ranged) bias += 4f;
                    if (t.Distance < 10f) bias -= 8f;          // leave the close ones to someone else
                    break;
                default: return 0f;
            }
            return Math.Max(-MaxBias, Math.Min(MaxBias, bias));
        }
    }
}
