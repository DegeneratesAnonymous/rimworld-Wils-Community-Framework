using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace FamilyLife
{
    /// <summary>One blow or kill in a hostile fight that involves the player's faction, with the few facts a career or a story needs. Read-only: it describes what the game did.</summary>
    public sealed class CombatReport
    {
        public Pawn Attacker, Victim;
        /// <summary>The blow was a shot (the damage came from a ranged attack), not a melee strike.</summary>
        public bool Ranged, Killed;
        public float Distance, Damage;
        /// <summary>Shots per burst of the weapon that did it; 1 for a melee blow or a single shot.</summary>
        public int Burst = 1;
        public string Weapon;
    }

    public static class CombatHooks
    {
        /// <summary>Builds a report, or null when this is not a hostile fight the player's faction is part of (a friendly scuffle, a mental break, a training dummy, an animal hitting an animal).</summary>
        internal static CombatReport Describe(Pawn victim, DamageInfo dinfo, float damage, bool killed)
        {
            var attacker = dinfo.Instigator as Pawn;
            if (attacker == null || victim == null || attacker == victim || attacker.Map == null || attacker.Map != victim.Map) return null;
            var player = Faction.OfPlayer;
            if (attacker.Faction != player && victim.Faction != player) return null;
            if (!attacker.HostileTo(victim)) return null;
            var weapon = dinfo.Weapon;
            var verb = weapon?.Verbs != null && weapon.Verbs.Count > 0 ? weapon.Verbs[0] : null;
            return new CombatReport
            {
                Attacker = attacker, Victim = victim, Killed = killed, Damage = damage, Weapon = weapon?.defName,
                Ranged = dinfo.Def != null && dinfo.Def.isRanged, Distance = attacker.Position.DistanceTo(victim.Position), Burst = verb != null ? Math.Max(1, verb.burstShotCount) : 1
            };
        }

        internal static void Raise(CombatReport report)
        {
            if (report == null) return;
            foreach (var listener in FamilyFramework.CombatEvents.Enabled())
                try { listener(report); } catch (Exception ex) { Log.Warning("[Wil's Family Life] A combat listener was skipped: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    internal static class Hook_CombatHit
    {
        private static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            try { if (FamilyFramework.CombatEvents.Count == 0 || totalDamageDealt <= 0f) return; CombatHooks.Raise(CombatHooks.Describe(__instance, dinfo, totalDamageDealt, false)); }
            catch (Exception ex) { HookSafety.Report("combat hit", ex); }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill), new[] { typeof(DamageInfo?), typeof(Hediff) })]
    internal static class Hook_CombatKill
    {
        private static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            try { if (FamilyFramework.CombatEvents.Count == 0 || dinfo == null || !__instance.Dead) return; CombatHooks.Raise(CombatHooks.Describe(__instance, dinfo.Value, 0f, true)); }
            catch (Exception ex) { HookSafety.Report("combat kill", ex); }
        }
    }

    /// <summary>
    /// Lets a shooter's doctrine nudge which enemy the game picks when it is choosing a target (a Marksman prefers the exposed, a Sniper the dangerous, a Gunner the nearest). Adds a bounded
    /// amount to the game's own score and never removes a target, so a role changes a preference and never makes anyone ignore a threat or fire at a friend.
    /// </summary>
    [HarmonyPatch(typeof(AttackTargetFinder), "GetShootingTargetScore")]
    internal static class Hook_ShootingTargetScore
    {
        private static void Postfix(ref float __result, IAttackTarget target, IAttackTargetSearcher searcher)
        {
            try
            {
                if (FamilyFramework.TargetBias.Count == 0) return;
                var shooter = searcher?.Thing as Pawn; var thing = target?.Thing; if (shooter == null || thing == null || shooter.Faction != Faction.OfPlayer) return;
                float bias = 0f;
                foreach (var listener in FamilyFramework.TargetBias.Enabled()) bias += listener(shooter, thing);
                if (bias != 0f) __result += Math.Max(-20f, Math.Min(20f, bias));
            }
            catch (Exception ex) { HookSafety.Report("target score", ex); }
        }
    }
}
