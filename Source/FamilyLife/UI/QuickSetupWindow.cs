using System;
using System.Collections.Generic;
using System.Linq;
using FamilyLife.Domain;
using FamilyLife.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace FamilyLife
{
    /// <summary>
    /// Quick setup for an existing colony: looks at what each adult is good at, proposes one entry-level profession each (nobody is left out of a profession that has no one, and no profession takes everyone),
    /// lets the player change any row, and applies them all with one click. Entry-level roles need no qualification, so this only assigns; it never touches offices, pay or the treasury. Undo reverses the last apply.
    /// </summary>
    public class Window_QuickSetup : Window
    {
        private const string DoneKey = "quicksetup:done", DismissedKey = "quicksetup:dismissed";
        private static List<KeyValuePair<int, string>> lastApplied = new List<KeyValuePair<int, string>>();

        private readonly FamilyComponent comp;
        private SetupPreset preset = SetupPreset.Balanced;
        private Dictionary<string, ProfessionRole> entry; private List<SetupCandidate> candidates; private Dictionary<int, string> choice = new Dictionary<int, string>();
        private Vector2 scroll; private string note;

        public override Vector2 InitialSize => new Vector2(640f, 620f);

        public Window_QuickSetup(FamilyComponent comp)
        {
            this.comp = comp; doCloseX = true; forcePause = false; absorbInputAroundWindow = false; draggable = true; resizeable = true;
            Scan(); Replan();
        }

        // ---------------------------------------------------------------- offer on the Overview page

        /// <summary>True when an existing colony has nobody assigned yet and the offer was not dismissed.</summary>
        public static bool Offered(FamilyRegistry reg)
        {
            var book = reg.Assignments();
            return !book.HasMigrated(DoneKey) && !book.HasMigrated(DismissedKey) && book.Count == 0 && EntryRoles().Count > 0 && reg.Persons.Any(p => p.InColony && !p.Juvenile);
        }

        public static void Dismiss(FamilyRegistry reg) => reg.Assignments().MarkMigrated(DismissedKey);

        private static Dictionary<string, ProfessionRole> EntryRoles()
        {
            var map = new Dictionary<string, ProfessionRole>();
            foreach (var role in CareerService.Professions.All.Where(r => r.Level <= 1 && FamilyFramework.Catalog.IsEnabled(r.ModuleId)).OrderBy(r => r.Id, StringComparer.Ordinal))
                if (!map.ContainsKey(role.Career)) map[role.Career] = role;
            return map;
        }

        // ---------------------------------------------------------------- the proposal

        private static IEnumerable<IDictionary<string, int>> Rules(CareerDefinition def)
        {
            if (def == null) yield break;
            yield return def.Level2.Skills;
            foreach (var r in def.Level2Branches.Values) yield return r.Skills;
            foreach (var r in def.Specialties.Values) yield return r.Skills;
        }

        private void Scan()
        {
            entry = EntryRoles(); candidates = new List<SetupCandidate>(); var reg = comp.Registry; var book = reg.Assignments();
            foreach (var person in reg.Persons.Where(p => p.InColony && !p.Juvenile))
            {
                if (book.RolesOf(person.Id).Any()) continue;
                var pawn = comp.TryGetPawn(person.Id); if (pawn == null || pawn.Dead || pawn.skills == null || !pawn.IsColonistPlayerControlled) continue;
                var cand = new SetupCandidate { Person = person.Id };
                foreach (var career in entry.Keys)
                {
                    float fit = QuickSetupPlanner.Fit(Rules(CareerService.Career(career)),
                        name => { var def = DefDatabase<SkillDef>.GetNamedSilentFail(name); var sk = def == null ? null : pawn.skills.GetSkill(def); return sk == null || sk.TotallyDisabled ? 0 : sk.Level; },
                        name => { var def = DefDatabase<SkillDef>.GetNamedSilentFail(name); var sk = def == null ? null : pawn.skills.GetSkill(def); return sk == null ? 0 : sk.passion == Passion.Major ? 2 : sk.passion == Passion.Minor ? 1 : 0; });
                    if (fit > 0f) cand.Fit[career] = fit;
                }
                candidates.Add(cand);
            }
        }

        private void Replan()
        {
            choice.Clear();
            foreach (var s in QuickSetupPlanner.Plan(candidates, entry.Keys.ToList(), preset)) choice[s.Person] = s.Career;
        }

        private string CareerLabel(string career) => career == null ? "WFL_Quick_None".Translate().ToString() : CareerService.TitleOf(entry[career], comp.Registry);

        // ---------------------------------------------------------------- window

        public override void DoWindowContents(Rect inRect)
        {
            float y = 0f;
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0f, y, inRect.width - 30f, 32f), "WFL_Quick_Title".Translate()); Text.Font = GameFont.Small; y += 34f;
            GUI.color = Color.gray; float h = Text.CalcHeight("WFL_Quick_Intro".Translate(), inRect.width); Widgets.Label(new Rect(0f, y, inRect.width, h), "WFL_Quick_Intro".Translate()); GUI.color = Color.white; y += h + 8f;

            // presets
            float pw = (inRect.width - 12f) / 3f;
            for (int i = 0; i < SetupPreset.All.Length; i++)
            {
                var p = SetupPreset.All[i]; var rect = new Rect(i * (pw + 6f), y, pw, 30f); GUI.color = p == preset ? CommunityWidgets.Gold : Color.white;
                if (Widgets.ButtonText(rect, ("WFL_Quick_Preset_" + p.Id).Translate())) { preset = p; Replan(); note = null; }
                GUI.color = Color.white; TooltipHandler.TipRegion(rect, ("WFL_Quick_PresetTip_" + p.Id).Translate());
            }
            y += 38f;

            if (candidates.Count == 0) { Widgets.Label(new Rect(0f, y, inRect.width, 60f), "WFL_Quick_Nobody".Translate()); return; }

            float nameW = inRect.width * .3f, fitW = 90f, listH = inRect.height - y - 82f;
            y = CommunityWidgets.Header(new Rect(0f, 0f, inRect.width, 0f), y, new List<KeyValuePair<string, float>> { new KeyValuePair<string, float>("WFL_Table_Name".Translate(), nameW), new KeyValuePair<string, float>("WFL_Quick_Role".Translate(), inRect.width - nameW - fitW - 20f), new KeyValuePair<string, float>("WFL_Quick_Fit".Translate(), fitW) });
            var outer = new Rect(0f, y, inRect.width, listH); float rowH = CommunityWidgets.RowHeight, viewH = candidates.Count * rowH;
            var view = new Rect(0f, 0f, inRect.width - 16f, viewH);
            Widgets.BeginScrollView(outer, ref scroll, view);
            int idx = 0;
            foreach (var cand in candidates.OrderBy(c => comp.TryGetPawn(c.Person)?.LabelShort))
            {
                var pawn = comp.TryGetPawn(cand.Person); if (pawn == null) continue;
                var row = CommunityWidgets.Row(view, idx * rowH, idx); idx++;
                CommunityWidgets.NameButton(new Rect(4f, row.y, nameW - 6f, row.height), pawn);
                choice.TryGetValue(cand.Person, out var career); int person = cand.Person;
                CommunityWidgets.Choice(new Rect(nameW, row.y + 1f, view.width - nameW - fitW - 8f, row.height - 2f), CareerLabel(career), () =>
                {
                    var options = new List<FloatMenuOption> { new FloatMenuOption(CareerLabel(null), () => choice.Remove(person)) };
                    foreach (var kv in entry.OrderByDescending(e => cand.Fit.TryGetValue(e.Key, out var f) ? f : 0f)) { var key = kv.Key; options.Add(new FloatMenuOption(CareerLabel(key) + "  (" + Mathf.RoundToInt(cand.Fit.TryGetValue(key, out var fit) ? fit : 0f) + ")", () => choice[person] = key)); }
                    return options;
                });
                float best = career != null && cand.Fit.TryGetValue(career, out var v) ? v : 0f;
                CommunityWidgets.Bar(new Rect(view.width - fitW + 6f, row.y + 7f, fitW - 12f, row.height - 14f), best / 20f, CommunityWidgets.Level(best / 14f), career == null ? "" : Mathf.RoundToInt(best).ToString());
            }
            Widgets.EndScrollView();

            // buttons
            float by = inRect.height - 70f; int count = choice.Count;
            if (note != null) { GUI.color = CommunityWidgets.Good; Widgets.Label(new Rect(0f, by - 24f, inRect.width, 22f), note); GUI.color = Color.white; }
            float bw = (inRect.width - 12f) / 3f;
            GUI.color = count > 0 ? CommunityWidgets.Gold : Color.gray;
            if (Widgets.ButtonText(new Rect(0f, by, bw, 34f), "WFL_Quick_Apply".Translate(count)) && count > 0) Apply();
            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(bw + 6f, by, bw, 34f), "WFL_Quick_Undo".Translate()) && lastApplied.Count > 0) Undo();
            if (Widgets.ButtonText(new Rect((bw + 6f) * 2f, by, bw, 34f), "WFL_Quick_Close".Translate())) Close();
            TooltipHandler.TipRegion(new Rect(0f, by, bw, 34f), "WFL_Quick_ApplyTip".Translate());
            TooltipHandler.TipRegion(new Rect(bw + 6f, by, bw, 34f), "WFL_Quick_UndoTip".Translate());
        }

        private void Apply()
        {
            var reg = comp.Registry; var book = reg.Assignments(); var ledger = reg.Careers(); int now = Find.TickManager.TicksGame; var done = new List<KeyValuePair<int, string>>();
            foreach (var kv in choice)
            {
                if (!entry.TryGetValue(kv.Value, out var role)) continue;
                if (book.Assign(kv.Key, role, ledger, now) == AssignResult.Assigned) done.Add(new KeyValuePair<int, string>(kv.Key, role.Id));
            }
            lastApplied = done; book.MarkMigrated(DoneKey); note = "WFL_Quick_Applied".Translate(done.Count);
            Scan(); Replan();
        }

        private void Undo()
        {
            var book = comp.Registry.Assignments(); int n = 0;
            foreach (var kv in lastApplied) if (book.Unassign(kv.Key, kv.Value)) n++;
            lastApplied = new List<KeyValuePair<int, string>>(); note = "WFL_Quick_Undone".Translate(n); Scan(); Replan();
        }
    }
}
