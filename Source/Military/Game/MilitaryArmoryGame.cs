using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FamilyLife
{
    /// <summary>The Armory: a real stockpile zone, set up to hold weapons and armour, so haulers fill it like any other. It is what arming the unarmed takes from.</summary>
    public class Zone_Armory : Zone_Stockpile
    {
        public Zone_Armory() { }
        public Zone_Armory(StorageSettingsPreset preset, ZoneManager zoneManager) : base(preset, zoneManager) { }
        protected override Color NextZoneColor => new Color(0.4f, 0.6f, 0.85f, 0.5f);
    }

    /// <summary>Architect &gt; Zone &gt; Armory. Painted like a stockpile, with its filter already set to weapons and armour (the usual stockpile settings can still be changed).</summary>
    public class Designator_ZoneAddStockpile_Armory : Designator_ZoneAddStockpile
    {
        protected override string NewZoneLabel => "WFL_Military_Armory".Translate();

        public Designator_ZoneAddStockpile_Armory()
        {
            preset = StorageSettingsPreset.DefaultStockpile; zoneTypeToPlace = typeof(Zone_Armory);
            defaultLabel = "WFL_Military_Armory".Translate(); defaultDesc = "WFL_Military_ArmoryDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile", false) ?? BaseContent.BadTex; hotKey = null; defaultIconColor = new Color(0.45f, 0.65f, 0.9f);
        }

        protected override Zone MakeNewZone()
        {
            var zone = new Zone_Armory(preset, Find.CurrentMap.zoneManager); var filter = zone.settings.filter; filter.SetDisallowAll();
            foreach (var name in new[] { "Weapons", "ApparelArmor" }) { var category = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(name); if (category != null) filter.SetAllow(category, true); }
            return zone;
        }
    }

    /// <summary>Finds what an Armory holds and who needs a weapon from it.</summary>
    internal static class MilitaryArmory
    {
        private static readonly Dictionary<int, int> tried = new Dictionary<int, int>();
        public static void Reset() => tried.Clear();

        public static IEnumerable<Zone_Armory> Zones(Map map) => map.zoneManager.AllZones.OfType<Zone_Armory>();

        public static List<ThingWithComps> Weapons(Map map)
        {
            var list = new List<ThingWithComps>();
            foreach (var zone in Zones(map))
                foreach (var cell in zone.Cells)
                    foreach (var thing in cell.GetThingList(map)) if (thing is ThingWithComps w && w.def.IsWeapon && w.Spawned && !list.Contains(w)) list.Add(w);
            return list;
        }

        /// <summary>Whether what the pawn holds is a ranged weapon, or null when they hold nothing.</summary>
        public static bool? Held(Pawn p) { var weapon = p.equipment?.Primary; return weapon == null ? (bool?)null : weapon.def.IsRangedWeapon; }

        public static bool Needs(Pawn p, string role) => role != null && p.equipment != null && ArmoryRules.NeedsWeapon(PostPlanner.TypeOf(role), Held(p));

        /// <summary>An order to fetch and equip the best suited weapon from the list, or null. The caller takes the weapon off the list so two soldiers are never sent for the same one.</summary>
        public static Job ArmJob(Pawn p, string role, List<ThingWithComps> weapons)
        {
            if (weapons.Count == 0 || p.equipment == null || p.WorkTagIsDisabled(WorkTags.Violent) || !Needs(p, role)) return null;
            var facts = weapons.Select(w => new WeaponFacts { Id = w.thingIDNumber, Ranged = w.def.IsRangedWeapon, Quality = w.TryGetQuality(out QualityCategory q) ? (int)q : -1, Value = w.MarketValue });
            foreach (var fact in ArmoryRules.Rank(PostPlanner.TypeOf(role), facts))
            {
                var weapon = weapons.First(x => x.thingIDNumber == fact.Id);
                if (weapon.IsForbidden(p) || !p.CanReserveAndReach(weapon, PathEndMode.ClosestTouch, Danger.Deadly) || !EquipmentUtility.CanEquip(weapon, p)) continue;
                return JobMaker.MakeJob(JobDefOf.Equip, weapon);
            }
            return null;
        }

        public static bool MayTry(Pawn p, int now) => ArmoryRules.MayTry(now, tried.TryGetValue(p.thingIDNumber, out int t) ? t : 0);
        public static void NoteTry(Pawn p, int now) => tried[p.thingIDNumber] = now;

        /// <summary>At Yellow, soldiers with nothing to do who lack a suitable weapon go and fetch one. (At Red the muster does it, before sending them to their positions.)</summary>
        public static void Run(int now)
        {
            var c = FamilyComponent.Current; if (c == null) return;
            if (c.Registry.Alert().Effective != AlertLevel.Yellow) return;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || MilitaryDutyGame.ThreatNow(map)) continue;
                var weapons = Weapons(map); if (weapons.Count == 0) continue;
                int sent = 0;
                foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(MilitaryRoles.Serving).OrderBy(x => x.thingIDNumber))
                {
                    if (sent >= ArmoryRules.PerLook) break;
                    string role = MilitaryRoles.ActiveRole(p);
                    if (!Needs(p, role) || !MayTry(p, now) || MilitaryDutyGame.OnDuty(p) || !MilitaryDutyGame.SoldierFree(p)) continue;
                    NoteTry(p, now); var job = ArmJob(p, role, weapons); if (job == null) continue;
                    weapons.Remove((ThingWithComps)job.targetA.Thing);
                    p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, tag: JobTag.Misc); sent++;
                }
            }
        }
    }
}
