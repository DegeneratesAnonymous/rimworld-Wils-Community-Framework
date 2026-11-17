using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FamilyLife
{
    public sealed class MilitaryMod : Mod
    {
        public MilitaryMod(ModContentPack content) : base(content) { FamilyFramework.Install(new MilitaryModule()); }
    }

    public sealed class MilitaryModule : IFamilyModule
    {
        public string Id => MilitaryCareers.Module;
        public void Register()
        {
            FamilyFramework.Catalog.Register(new ModuleInfo { Id = Id, Name = "Wil's Role Modules : Military", ApiVersion = FamilyFramework.ApiVersion, Requires = new[] { ModuleCatalog.FrameworkId } });
            CareerService.RegisterCareer(MilitaryCareers.Definition());
            CareerService.RegisterRole(new ProfessionRole { Id = MilitaryCareers.Recruit, ModuleId = Id, Career = MilitaryCareers.Career, Level = 1 });
            foreach (var branch in MilitaryCareers.Branches) CareerService.RegisterRole(new ProfessionRole { Id = MilitaryCareers.Role(branch), ModuleId = Id, Career = MilitaryCareers.Career, Level = 2, Specialty = branch });
            foreach (var specialty in MilitaryCareers.Specialties)
                CareerService.RegisterRole(new ProfessionRole { Id = MilitaryCareers.Role(specialty), ModuleId = Id, Career = MilitaryCareers.Career, Level = 3, Specialty = specialty, BonusKey = "WFL_Military_" + specialty + "Bonus" });
            FamilyFramework.CombatEvents.Add(Id + ".combat", Id, MilitaryCombat.Handle);
            FamilyFramework.Tended.Add(Id + ".tends", Id, MilitaryCombat.Tended);
            FamilyFramework.TargetBias.Add(Id + ".targets", Id, MilitaryDoctrineGame.Bias);
            FamilyFramework.SocialInteraction.Add(Id + ".relationships", Id, MilitaryStories.Social);
            FamilyFramework.Pages.Register(new CommunityPage<Func<Rect, FamilyComponent, float>> { Id = Id + ".alert", ModuleId = Id, LabelKey = "WFL_Military_Page", Order = 160, Icon = "WilsFamilyLife/Pages/military", Render = MilitaryPage.Draw, Status = MilitaryPage.Attention });
            FamilyFramework.PlayControls.Add(Id + ".playcontrols", Id, MilitaryPlayControls.Draw);
            FamilyFramework.Roster.Register(new WorkItem { Id = Id + ".alert", ModuleId = Id, Interval = 250, Run = MilitaryAlertDriver.Run });
            FamilyFramework.Roster.Register(new WorkItem { Id = Id + ".duties", ModuleId = Id, Interval = 300, Run = MilitaryDutyGame.Run });
            FamilyFramework.Roster.Register(new WorkItem { Id = Id + ".armory", ModuleId = Id, Interval = 600, Run = MilitaryArmory.Run });
            FamilyFramework.Reconcilers.Add(Id + ".reconcile", Id, MilitaryAlertDriver.Reconcile);
            FamilyFramework.Resets.Add(Id + ".reset", Id, () => { MilitaryDoctrineGame.Reset(); MilitaryAlertDriver.Reset(); MilitaryDutyGame.Reset(); });
            FamilyFramework.Removal.Add(Id + ".removal", Id, c =>
            {
                var report = new CleanupReport(Id);
                report.Step("End every duty in progress", () => ModuleCleanup.EndJobs("WFL_MilitaryDuty") + " soldier(s) released from duty");
                report.Step("Remove the Training Yard and Armory zones", () => { int n = 0; foreach (var map in Find.Maps) { foreach (var z in map.zoneManager.AllZones.OfType<Zone_TrainingYard>().ToList()) { z.Delete(); n++; } foreach (var z in map.zoneManager.AllZones.OfType<Zone_Armory>().ToList()) { z.Delete(); n++; } } return n + " zone(s) removed (a saved zone of an absent module cannot load; the weapons and armour in an Armory stay where they are)"; });
                report.Step("Stand down any Red alert", () => { var state = c.Registry.Alert(); int n = state.Drafted.Count; MilitaryAlertDriver.StandDown(c, state); state.RaisedByThreat = false; state.Set = AlertLevel.Normal; return n + " soldier(s) this drafted were undrafted; posts are kept (they are only saved numbers)"; });
                report.Step("Clear in-memory combat records", () => { MilitaryDoctrineGame.Reset(); MilitaryAlertDriver.Reset(); MilitaryDutyGame.Reset(); return "careers, experience and memories retained; the module's one job was ended above and it adds no hediffs"; });
                return report;
            });
            FamilyFramework.CrossTraining.Add(Id + ".cross.firstaid", Id, new CrossTrainingRule { From = MilitaryCareers.Career, To = "wils.care.practice", KindPrefix = "mil.duty.FirstAid", Share = .5f });
            FamilyFramework.CrossTraining.Add(Id + ".cross.patrol", Id, new CrossTrainingRule { From = MilitaryCareers.Career, To = "wils.wardens.wardens", KindPrefix = "mil.duty.Patrol", Share = .25f });
            FamilyFramework.CrossTraining.Add(Id + ".cross.gate", Id, new CrossTrainingRule { From = MilitaryCareers.Career, To = "wils.wardens.wardens", KindPrefix = "mil.duty.GateGuard", Share = .25f });
            FamilyFramework.CrossTraining.Add(Id + ".cross.lookout", Id, new CrossTrainingRule { From = MilitaryCareers.Career, To = "wils.wardens.wardens", KindPrefix = "mil.duty.Lookout", Share = .25f });
            FrameworkServices.RegisterNeedAction(Id, Id + ".yard", "WFL_Need_PlanYard", () => { Find.MainTabsRoot.EscapeCurrentTab(); Find.DesignatorManager.Select(new Designator_ZoneAdd_TrainingYard()); });
            FamilyFramework.HealthChecks.Add(Id + ".health", Id, c => MilitaryHealth.Check(c, Id));
            CareerService.Registered(Id);
        }
    }

    /// <summary>Who is assigned to which Military role. Looked up by the shooting score and the stat parts many times a second, so the answer is kept for a short while.</summary>
    public static class MilitaryRoles
    {
        private struct Entry { public int Tick; public string Specialty; public bool Serving; }
        private static readonly Dictionary<int, Entry> cache = new Dictionary<int, Entry>();
        public const int KeepTicks = 250;
        public static void Reset() => cache.Clear();

        /// <summary>The combat specialty the pawn is qualified for and assigned to, or null.</summary>
        public static string Specialty(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return null;
            return Look(pawn).Specialty;
        }

        /// <summary>Whether the pawn holds any Military role at all (Recruit, Soldier, Ranger or a specialty). The Officer's aura reaches everyone who does.</summary>
        public static bool Serving(Pawn pawn) => pawn != null && !pawn.Dead && Look(pawn).Serving;

        private static Entry Look(Pawn pawn)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (cache.TryGetValue(pawn.thingIDNumber, out var e) && now >= e.Tick && now - e.Tick < KeepTicks) return e;
            string found = null; bool serving = false; var c = FamilyComponent.Current;
            if (c != null)
            {
                var book = c.Registry.Assignments(); var ledger = c.Registry.Careers();
                foreach (var s in MilitaryCareers.Specialties)
                    if (book.IsAssigned(pawn.thingIDNumber, MilitaryCareers.Role(s)) && ledger.IsSpecialist(pawn.thingIDNumber, MilitaryCareers.Career, s)) { found = s; break; }
                serving = found != null || book.IsAssigned(pawn.thingIDNumber, MilitaryCareers.Recruit) || MilitaryCareers.Branches.Any(b => book.IsAssigned(pawn.thingIDNumber, MilitaryCareers.Role(b)));
            }
            return cache[pawn.thingIDNumber] = new Entry { Tick = now, Specialty = found, Serving = serving };
        }

        /// <summary>The role whose events the pawn lives: specialty, else branch, else Recruit. Null if they hold no Military role at all.</summary>
        public static string ActiveRole(Pawn pawn)
        {
            var book = FamilyComponent.Current?.Registry.Assignments(); if (book == null || pawn == null) return null;
            foreach (var s in MilitaryCareers.Specialties) if (book.IsAssigned(pawn.thingIDNumber, MilitaryCareers.Role(s))) return s;
            foreach (var b in MilitaryCareers.Branches) if (book.IsAssigned(pawn.thingIDNumber, MilitaryCareers.Role(b))) return b;
            return book.IsAssigned(pawn.thingIDNumber, MilitaryCareers.Recruit) ? "Recruit" : null;
        }
    }

    /// <summary>Reads the situation around a soldier into the plain facts the doctrine decides from. Kept for a few ticks per pawn.</summary>
    public static class MilitaryDoctrineGame
    {
        private struct Held { public int Tick; public CombatContext Context; }
        private static readonly Dictionary<int, Held> contexts = new Dictionary<int, Held>();
        public const int ContextTicks = 30, NearCells = 5, CoverCells = 6;

        public static void Reset() { contexts.Clear(); MilitaryRoles.Reset(); MilitaryCombat.Reset(); MilitaryStories.Reset(); }

        private static bool Hostile(Pawn pawn, Thing other) => other != null && other.Spawned && other.HostileTo(pawn);

        public const int OfficerCells = 10;

        /// <summary>
        /// The situation around a serving pawn. In a fight it counts hostiles, a friend to cover and an Officer nearby; out of one it only looks for a downed friend, and only for a Field Medic, so an
        /// idle colony does no scanning.
        /// </summary>
        public static CombatContext Context(Pawn pawn, string role)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (contexts.TryGetValue(pawn.thingIDNumber, out var held) && now >= held.Tick && now - held.Tick < ContextTicks) return held.Context;
            var c = new CombatContext(); var map = pawn.Map;
            if (pawn.Spawned && map != null)
            {
                Thing target = pawn.mindState?.enemyTarget; if (target == null && pawn.CurJob != null && (pawn.CurJob.def == JobDefOf.AttackMelee || pawn.CurJob.def == JobDefOf.AttackStatic)) target = pawn.CurJob.targetA.Thing;
                bool fighting = Hostile(pawn, target) && !(target is Pawn down && down.Downed);
                if (fighting) { c.InCombat = true; c.TargetDistance = pawn.Position.DistanceTo(target.Position); c.Stationary = pawn.pather == null || !pawn.pather.MovingNow; }
                if (fighting || role == "FieldMedic")
                    foreach (var p in map.mapPawns.AllPawnsSpawned)
                    {
                        if (p == pawn || p.Dead) continue;
                        float d = pawn.Position.DistanceTo(p.Position);
                        if (fighting && d <= NearCells && !p.Downed && p.HostileTo(pawn)) c.HostilesNear++;
                        if (p.Faction != pawn.Faction || !p.RaceProps.Humanlike) continue;
                        if (d <= CoverCells)
                        {
                            if (p.Downed) { c.AllyDownedNear = true; if (fighting) c.CoveringAlly = true; }
                            else if (fighting && target.Position.DistanceTo(p.Position) <= 2f) c.CoveringAlly = true;          // the enemy is on top of a friend we can reach
                        }
                        if (fighting && !c.OfficerNear && d <= OfficerCells && !p.Downed && MilitaryRoles.Specialty(p) == "Officer") c.OfficerNear = true;
                    }
            }
            contexts[pawn.thingIDNumber] = new Held { Tick = now, Context = c };
            return c;
        }

        /// <summary>A shooter's nudge between two enemies, from their doctrine. Marksman, Gunner and Sniper only.</summary>
        public static float Bias(Pawn shooter, Thing target)
        {
            string role = MilitaryRoles.Specialty(shooter);
            if (role != "Marksman" && role != "Gunner" && role != "Sniper") return 0f;
            var enemy = target as Pawn; if (enemy == null || shooter.Map == null) return 0f;
            var weapon = enemy.equipment?.Primary;
            var facts = new TargetFacts { Distance = shooter.Position.DistanceTo(enemy.Position), Armed = weapon != null, Ranged = weapon != null && weapon.def.IsRangedWeapon };
            if (role == "Marksman") facts.Exposed = CoverUtility.CalculateOverallBlockChance(enemy, shooter.Position, shooter.Map) < .1f;
            if (role == "Gunner" && enemy.mindState?.enemyTarget is Pawn victim && victim != shooter && victim.Faction == shooter.Faction) facts.AttackingAlly = true;
            if (role == "Sniper" && weapon != null)
            {
                var verb = weapon.def.Verbs != null && weapon.def.Verbs.Count > 0 ? weapon.def.Verbs[0] : null;
                float damage = verb?.defaultProjectile?.projectile != null ? verb.defaultProjectile.projectile.GetDamageAmount(weapon) : 0f;
                facts.Threat = Mathf.Clamp01(damage * Math.Max(1, verb?.burstShotCount ?? 1) / 40f);
            }
            return MilitaryDoctrine.TargetBias(role, facts);
        }
    }

    /// <summary>A hostile blow or kill: experience for the colonist who dealt it, and a memory for those who saw a friend fall or were struck down.</summary>
    public static class MilitaryCombat
    {
        private static readonly HashSet<long> engaged = new HashSet<long>();
        private const int EngagedCap = 600;
        public static void Reset() => engaged.Clear();

        private static bool Colonist(Pawn p) => p != null && p.Faction == Faction.OfPlayer && p.RaceProps.Humanlike;

        public static void Handle(CombatReport r)
        {
            var c = FamilyComponent.Current; if (c == null || r == null) return;
            if (Colonist(r.Victim)) { Suffered(r); return; }
            if (!Colonist(r.Attacker)) return;
            long key = ((long)r.Attacker.thingIDNumber << 32) | (uint)r.Victim.thingIDNumber;
            bool first = engaged.Add(key); if (engaged.Count > EngagedCap) { engaged.Clear(); engaged.Add(key); }
            if (!first && !r.Killed) return;
            var facts = Gather(r); var track = CombatClassifier.Classify(facts); if (track == null) return;
            var ledger = c.Registry.Careers();
            bool inBranch = ledger.IsSpecialist(r.Attacker.thingIDNumber, MilitaryCareers.Career, track.Route);
            string credit = inBranch ? track.Specialty : track.Route;
            int points = r.Killed ? MilitaryCareers.KillPoints : MilitaryCareers.EngagePoints;
            if (inBranch) points = MilitaryCareers.SpecialtyPoints(points);          // a specialty is earned mainly through daily duties; the fight only adds a share
            string kind = (r.Ranged ? "mil.shoot" : "mil.melee") + (r.Killed ? ".kill" : ".engage");
            CareerService.Report(c, r.Attacker, MilitaryCareers.Career, credit, kind, (r.Killed ? "kill:" : "engage:") + r.Victim.thingIDNumber, points, FrameworkServices.Now);
            if (r.Killed) MilitaryStories.Kill(r.Attacker, track, facts, r.Victim.thingIDNumber);
        }

        /// <summary>
        /// A comrade was tended while hostiles were about: the Field Medic's experience from the field. Anyone who does it banks it (the specialty needs the Doctor qualification as well, which
        /// is checked when it is earned). Once per patient per day.
        /// </summary>
        public static void Tended(Pawn tender, Pawn patient)
        {
            var c = FamilyComponent.Current; if (c == null || !Colonist(tender) || !Colonist(patient) || tender == patient || tender.Map == null || tender.Map != patient.Map) return;
            if (!HostilesAbout(tender.Map)) return;
            int day = FrameworkServices.Now / ActivityTuning.TicksPerDay;
            CareerService.Report(c, tender, MilitaryCareers.Career, "FieldMedic", "mil.tend", "tend:" + patient.thingIDNumber + ":" + day, MilitaryCareers.FieldTendPoints, FrameworkServices.Now);
        }

        internal static bool HostilesAbout(Map map)
        {
            foreach (var p in map.mapPawns.AllPawnsSpawned) if (!p.Dead && !p.Downed && p.Faction != Faction.OfPlayer && p.HostileTo(Faction.OfPlayer) && !p.IsPrisoner) return true;
            return false;
        }

        private static CombatFacts Gather(CombatReport r)
        {
            var a = r.Attacker; var f = new CombatFacts { Ranged = r.Ranged, Distance = r.Distance, Burst = r.Burst, Armor = a.GetStatValue(StatDefOf.ArmorRating_Sharp) };
            if (a.Map == null) return f;
            if (r.Victim.mindState?.enemyTarget is Pawn target && target != a && target.Faction == a.Faction) f.DefendedAlly = true;
            foreach (var p in a.Map.mapPawns.AllPawnsSpawned)
            {
                if (p == a || p == r.Victim || p.Dead) continue;
                float d = a.Position.DistanceTo(p.Position);
                if (d <= MilitaryDoctrineGame.NearCells && !p.Downed && p.HostileTo(a)) f.OtherEnemiesNear++;
                if (!f.DefendedAlly && d <= MilitaryDoctrineGame.CoverCells && p.Faction == a.Faction && p.RaceProps.Humanlike && p.Downed) f.DefendedAlly = true;
            }
            return f;
        }

        /// <summary>A colonist was hit. If they went down or died, other soldiers who saw it remember; a soldier who was struck down remembers it too.</summary>
        private static void Suffered(CombatReport r)
        {
            var map = r.Victim.Map; if (map == null) return;
            if (r.Killed)
            {
                foreach (var p in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
                    if (p != r.Victim && Colonist(p) && p.Position.DistanceTo(r.Victim.Position) <= 12f) MilitaryStories.Fell(p, r.Victim);
            }
            else if (r.Victim.Downed) MilitaryStories.Struck(r.Victim);
        }
    }

    public static class MilitaryStories
    {
        private static readonly Dictionary<string, int> last = new Dictionary<string, int>();
        public static void Reset() => last.Clear();

        private static void GiveFact(Pawn subject, Pawn other, string role, string fact)
        {
            if (role == null || fact == null) return;
            var e = MilitaryEvents.Eligible(role, fact, other != null).FirstOrDefault(); if (e == null) return;
            int day = FrameworkServices.EconomyDay; string key = subject.thingIDNumber + ":" + e.Id + ":" + (other?.thingIDNumber ?? 0);
            if (last.TryGetValue(key, out int before) && day - before < 3) return;
            last[key] = day; FrameworkServices.GainMemory(subject, e.Thought, other);
        }
        private static void Give(Pawn subject, Pawn other, string role, int category, int index) => GiveFact(subject, other, role, MilitaryEvents.Fact(role, category, index));

        /// <summary>A kill: an outstanding result when it was hard (outnumbered or from far off), otherwise routine success or pride in the calling.</summary>
        public static void Kill(Pawn pawn, CombatTrack track, CombatFacts facts, int seed)
        {
            string role = MilitaryRoles.ActiveRole(pawn); if (role == null) return;
            bool hard = facts.OtherEnemiesNear >= 2 || facts.Distance >= CombatClassifier.SniperRange;
            Give(pawn, null, role, MilitaryEvents.Positive, hard ? 1 : (Math.Abs(seed) % 2 == 0 ? 0 : 4));
        }

        /// <summary>A friend fell within sight: the roles that feel it most remember. (Named facts, because the right reaction is different for each role.)</summary>
        public static void Fell(Pawn witness, Pawn fallen)
        {
            string role = MilitaryRoles.ActiveRole(witness); if (role == null) return;
            string fact = role == "Guardian" ? "FriendFell" : role == "Soldier" ? "SquanderedLife" : role == "Vanguard" ? "LostMyFlank" : role == "FieldMedic" ? "TooLateToSave" : role == "Officer" ? "WeightOfCommand" : null;
            GiveFact(witness, null, role, fact);
        }

        /// <summary>A soldier was struck down.</summary>
        public static void Struck(Pawn pawn)
        {
            string role = MilitaryRoles.ActiveRole(pawn); if (role == null) return;
            string fact = role == "Recruit" ? "FirstBlood" : role == "Soldier" ? "WoundedInAction" : role == "Vanguard" ? "CutDownLeading" : role == "Duelist" ? "StruckDown" : role == "Sniper" ? "ExposedAlone" : role == "Ranger" ? "SpottedPosition" : role == "FieldMedic" ? "HitWhileTending" : null;
            GiveFact(pawn, null, role, fact);
        }

        /// <summary>A finished block of duty: routine success or pride in the calling, in turn.</summary>
        public static void Duty(Pawn pawn, string role, int seed)
        {
            if (role == null) return;
            Give(pawn, null, role, MilitaryEvents.Positive, Math.Abs(seed) % 2 == 0 ? 0 : 4);
        }

        /// <summary>Two soldiers trained together: each remembers the other kindly (praised, helped or shared know-how, in turn).</summary>
        public static void Together(Pawn a, Pawn b, int seed)
        {
            string ra = MilitaryRoles.ActiveRole(a), rb = MilitaryRoles.ActiveRole(b); int index = Math.Abs(seed) % 3;
            if (ra != null && rb != null) { Give(a, b, ra, MilitaryEvents.Social, index); Give(b, a, rb, MilitaryEvents.Social, index); }
        }

        public static void Social(Pawn actor, Pawn other, InteractionDef interaction)
        {
            string role = MilitaryRoles.ActiveRole(actor); if (role == null || MilitaryRoles.ActiveRole(other) == null) return;
            int seed = (actor.thingIDNumber * 397) ^ other.thingIDNumber ^ (FrameworkServices.EconomyDay / 3);
            Give(actor, other, role, MilitaryEvents.Social, Math.Abs(seed) % 5);
        }
    }

    /// <summary>
    /// Applies a combat specialty's doctrine to the stat it is attached to (patched onto movement, melee, accuracy, aiming, cooldown and incoming damage). Does nothing unless the pawn is
    /// qualified and assigned to that exact specialty and is in the situation the specialty is for.
    /// </summary>
    public sealed class StatPart_MilitaryDoctrine : StatPart
    {
        private float Factor(StatRequest req, out string role)
        {
            role = null; var pawn = req.Thing as Pawn; if (pawn == null || parentStat == null || pawn.Dead || !MilitaryRoles.Serving(pawn)) return 1f;
            role = MilitaryRoles.Specialty(pawn);
            string stat = parentStat.defName;
            // Only look at the surroundings when something could apply: the pawn's own specialty touches this stat, or the Officer's aura does.
            bool aura = stat == MilitaryDoctrine.Accuracy || stat == MilitaryDoctrine.MeleeHit;
            if (role == null && !aura) return 1f;
            var context = MilitaryDoctrineGame.Context(pawn, role);
            float f = role == null ? 1f : MilitaryDoctrine.Factor(role, stat, context);
            return f * MilitaryDoctrine.AuraFactor(stat, context);
        }
        public override void TransformValue(StatRequest req, ref float val) { float f = Factor(req, out _); if (f != 1f) val *= f; }
        public override string ExplanationPart(StatRequest req) { float f = Factor(req, out string role); return f != 1f ? (role ?? "Officer's aura") + ": x" + f.ToString("0.##") : null; }
    }
}
