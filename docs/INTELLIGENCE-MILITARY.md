# Intelligence and Military — first draft

Two optional role modules, built the way Crafting and Agriculture are: a career tree in the shared career ledger, experience from real game activity, specialty bonuses that apply only to a pawn who is **qualified and assigned to that exact specialty**, and five positive, five negative and five relationship events for every role. Each references only the framework.

**Status: compiles for 1.6 and 1.7, 1,560 offline tests pass, the three new Harmony patches pass the repository's patch check against both reference assemblies. Nothing has run in RimWorld.** Section 7 says exactly what that does and does not prove.

## 1. Intelligence — `SpacemanWil.WilsIntelligence`

Assistant (anyone) → **Researcher** (Intellectual 6, 120 experience) → a specialty (250 general + 60 specialty experience).

| Specialty | Skills | Experience comes from | Assigned bonus |
|---|---|---|---|
| **Inventor** | Intellectual 10, Crafting 5 | research on industrial-era and later projects | +15% research speed on those projects |
| **Analyst** | Intellectual 8, Social 5 | finishing a scanner operation (any job whose name contains "Scanner") | +10% research speed on any project (the generalist) |
| **Geneticist** | Intellectual 10, Medicine 10 | research on gene and xenogerm projects; making a xenogerm or genepack at a bench | +15% research speed on gene projects |
| **Dark Scholar** (Dark Study) | Intellectual 10 | research on Anomaly projects; finishing a study session | +15% research speed on Anomaly projects, +20% study efficiency |
| **Author** (Writing) | Intellectual 8, Artistic 8 | writing a book (Anomaly books) | books gain one quality band, up to Excellent |

The draft's *Scientist* is folded into **Researcher**: with Assistant → Researcher already the general science step, a third "generalist" specialty had nothing of its own to do. It is one entry to bring back if wanted.

**Geneticist** keeps the rule from [CAREERS.md](CAREERS.md): it wraps an existing vanilla bill (the Xenogerm Forge's), not an invented activity, and changes no vanilla surgery. "Doctor first" is dropped because modules never depend on each other; Medicine 10 stands in for it.

**Dark Study needs Anomaly, Geneticist needs Biotech, Author needs books to write.** Without them those specialties simply have no work and nobody earns them. Nothing is gated or faked. `StudyEfficiency` is an Anomaly stat; the patch that touches it succeeds quietly where it is absent.

Experience from research is given once per 50 *project points*, to the discipline of the project being worked on. (The game's `ResearchPerformed` hook reports the raw per-tick work rate, which `ResearchManager.ResearchPointsPerWorkTick` = 0.00825 scales into points; this module applies that scaling.) At speed 1 over an eight-hour shift that is about 165 points a day, and 4 experience per chunk is about 13 a day: roughly **9 days to Researcher and 19 to a specialty**, if the work is in that discipline. Preliminary.

## 2. Military — `SpacemanWil.WilsMilitary`

Recruit (anyone) → **Soldier** (Melee 6) or **Ranger** (Shooting 6), parallel, each 120 general + 60 branch experience → a specialty of that branch (250 + 60).

| Branch | Specialty | Skills |
|---|---|---|
| Soldier | Vanguard | Melee 10 |
| Soldier | Guardian | Melee 8, Medicine 3 |
| Soldier | Duelist | Melee 12 |
| Ranger | Marksman | Shooting 10 |
| Ranger | Gunner | Shooting 8 |
| Ranger | Sniper | Shooting 12 |
| Support (either branch) | **Field Medic** | Medicine 8, **and a Community Care Doctor qualification** |
| Support (either branch) | **Officer** | Social 8 |

**Combat earns the general and branch experience.** A first blow on an enemy is worth 3, a kill 10, once per enemy. Once the branch is earned a fight credits the specialty it resembled at **40%** of those points: specialties are earned mainly through daily duties (section 9), not by how a fight happened to go. The Field Medic also banks 6 for tending a comrade while hostiles are about (once per patient per day); the Officer earns only through duties. The fight is credited to the specialty it resembled (`CombatClassifier`): melee for a friend, or beside a downed one → Guardian; one-on-one → Duelist; pressing into two or more, or in heavy armour → Vanguard; ranged from 25+ cells → Sniper; a burst weapon within 12 cells → Gunner; any other shot → Marksman. Friendly scuffles, mental breaks, wild animals and training dummies never count (the hook requires the two sides to be hostile and one to be the player's).

## 3. Combat doctrine: a distinct job for each specialty

Every specialty (the Officer's strength is lent to others) has **one job and one weakness**, so none is simply better than another. All values are bounded multipliers (never outside 0.8 to 1.25) and apply only when the pawn is assigned that exact specialty **and** is in the situation the specialty is for; out of a fight they do nothing. Source: `MilitaryDoctrine`.

| Specialty | Job on the battlefield | Strength | Weakness |
|---|---|---|---|
| **Vanguard** | *Breaker.* Closes fast, breaks the line, goes for the shooters. | +20% move speed while closing (2–20 cells from the enemy); +10% melee damage once engaged | −15% melee dodge while charging |
| **Guardian** | *Anchor.* Holds the line, covers the wounded. | −15% incoming damage while covering an ally (a friend is downed within 6 cells, or the enemy is on top of one) | −8% move speed; no better at killing |
| **Duelist** | *Skirmisher.* Wins one-on-ones, picks off a straggler. | +15% melee hit and +20% melee dodge with one enemy near | −15% dodge once three or more are close |
| **Marksman** | *Precision.* Steady mid-range fire on whoever is exposed. | +10% accuracy at 12–30 cells; prefers exposed and armed targets | −10% accuracy inside 6 cells |
| **Gunner** | *Suppression.* Volume of fire that keeps heads down. | −15% weapon cooldown; +5% accuracy within 12 cells; prefers the nearest enemy and one attacking a friend | −8% accuracy beyond 20 cells |
| **Field Medic** | *Medic.* Keeps the line standing, tending the fallen where they lie. | +15% tend speed with a friend down within 6 cells (no fight needed) | −10% accuracy and −10% melee hit |
| **Officer** | *Commander.* Coordinates the line. | Other serving soldiers within 10 cells of them get +5% accuracy and melee hit in a fight (does not stack) | −8% accuracy and −8% melee dodge themselves |
| **Sniper** | *Overwatch.* Patient long-range kills on the most dangerous. | +15% accuracy beyond 25 cells **once set up** (not moving); prefers the most dangerous and armed targets, and leaves the close ones to others | +25% aiming delay; −15% accuracy inside 10 cells |

**Behaviour** is changed in two ways, both without ordering anyone around:

1. **How they fight** — the stat effects above, read from the situation (target distance, hostiles within 5 cells, a friend to cover, standing still), kept per pawn for 30 ticks.
2. **Who a shooter picks** — Marksman, Gunner and Sniper add a bounded nudge (at most ±15, on the game's own scale of tens) to the game's shooting-target score when it chooses between enemies. It adds to the score and never removes a target, so nobody ignores a threat or fires at a friend. Melee roles have no preference to add: the game chooses a melee opponent by distance.

**Not built, deliberately: positioning.** Each profile already carries a preferred range band (`MinRange`, `PreferredRange`, `MaxRange`), but nothing yet moves a pawn to it. Sending pawns to stations is the step most likely to fight the player or other mods, so it should be designed after the stat and target behaviour has been seen in play, probably as an opt-in setting with recorded orders in the way automatic hunting records its designations.

## 4. Events

Every role has five positive, five negative and five relationship events (Intelligence: 7 roles, 105 events; Military: 9 roles, 135). Each has its own thought with its own wording. They are generated from one table (`scripts/role_events/spec.py`, run `gen.py`) so a fact and its thought cannot drift apart. Within each category the index means the same thing for every role: positive — routine success, outstanding result, milestone, recognised by others, pride; negative — failed work, waste or setback, strain, rejected, danger or harm; relationship — praised, helped, shared know-how, dispute, betrayal or stolen credit.

**Wired to real triggers now**

- *Intelligence:* finishing a research chunk, scan, study session or product (routine / outstanding / pride in turn); a poor-quality book (the Author's *threw out the draft*); relationship events from social interactions between two holders of Intelligence roles.
- *Military:* a kill (outstanding if outnumbered or from 25+ cells, otherwise routine or pride); a friend killed within 12 cells (Guardian, Soldier, Vanguard each remember it in their own way); being struck down (Recruit, Soldier, Vanguard, Duelist, Ranger, Sniper); relationship events from social interactions.

Everything else (the other negative and milestone events) has its thoughts and catalogue entries but no trigger yet, as with Crafting and Agriculture. Each memory repeats at most once in three days per pawn and event.

## 5. Framework changes

**Cross-career requirements.** A role can now require another career's qualification by its ledger id (`CareerRule.Needs("wils.care.practice")`, checked with `CareerLedger.Holds`). The Field Medic uses it to need a real Community Care Doctor without Military referencing Community Care: the ledger is shared and the id is a string. If Community Care is absent nobody holds that qualification and the role is simply unavailable. Unmet requirements appear in the pawn tab's "needs" text. It would also let Geneticist ask for "Doctor first" again.


Modules cannot ship Harmony patches (a test enforces it), so the combat behaviour needed three small framework additions, in `Source/FamilyLife/Game/CombatHooks.cs`:

| Hook | What it gives modules |
|---|---|
| `FamilyFramework.CombatEvents` (patches `Pawn.PostApplyDamage` and `Pawn.Kill`) | a read-only `CombatReport` for a hostile blow or kill that involves the player's faction |
| `FamilyFramework.Tended` (existing) | a comrade tended: the Field Medic's field experience |
| `FamilyFramework.TargetBias` (patches the private `AttackTargetFinder.GetShootingTargetScore`) | a bounded nudge added to a player shooter's target score |

Both skip all work when no module is listening. Each patch class is applied on its own, so a game update that moves one is logged and skipped without taking the others down. `ResearchDone` and `LabourDone` already existed and are reused.

Also touched: the three scripts and the test project list the two new packages; the module packages may ship `Patches`; `RepoPaths` scans the two new source folders.

## 6. Where the numbers are guesses

All preliminary: skill and experience thresholds, the 3 and 10 combat points, the 50-point research chunk, the daily diminishing returns (the ledger's own), every doctrine multiplier, the 5-cell and 6-cell "near" radii, the 25-cell sniper range, heavy armour at 0.5 sharp, name matching for gene projects, books, xenogerms and Anomaly study jobs (modded content with unusual names may fall outside a track). They are constants in the domain files and tested for bounds, not for balance.

## 7. Verification

**Done:** both modules and the framework build for 1.6 (net472) and 1.7 (net10); 1,531 tests pass, including 48 new ones (tree and gating, fight classification, that every specialty has a strength and a weakness and no two behave the same, factor bounds, situational rules, target preferences, event counts, every thought present, every string present, patch XML), the assembly-boundary tests for both new modules in both versions (framework reference only, no type duplicated, only their own DLL), and the guard that no module declares a Harmony patch; the repository's patch checker passes all 43 patch classes against both reference assemblies, including the three new ones (target exists, parameter names match).

**Not verified, and not claimed:**

- That any of it runs: modules load, hooks fire, experience arrives, qualifications are earned.
- That the stat parts take effect, and at what cost: `ResearchSpeed`, `StudyEfficiency`, `MoveSpeed`, `MeleeDamageFactor`, `MeleeHitChance`, `MeleeDodgeChance`, `IncomingDamageFactor`, `ShootingAccuracyPawn`, `AimingDelayFactor` and `RangedCooldownFactor` are patched by name; whether each exists in the game's data and whether the game's stat cache refreshes them promptly (a 30-tick situation cache sits on top) is for play to show.
- That the scanner and study jobs and the Anomaly book and xenogerm products are named as assumed (matched by name fragment).
- Performance in a large fight: the situation and target code runs per stat read and per target scored, and is cached or cheap by design but unmeasured.
- Balance, and whether the doctrine feels distinct rather than merely numerically distinct.

## 8. Agreed with the owner, not yet built

Settled by question and answer; none of it is in the code yet.

**Military**
- **Alert levels:** *Normal* (drills, sparring, range practice, muster, first-aid rounds), *Yellow* (patrols, lookout, gate and home guard), *Red* (hold defensive positions). The player sets Normal or Yellow, with an easily reachable toggle; a threat on the map **raises Red automatically** and it drops back to the set level when clear.
- **Positions:** player-placed posts (Overwatch, Guard, Rally) and a painted Training Yard. With no posts placed, Red rallies everyone at the Rally point.
- **Daily duties** (automatic, only for free assigned soldiers in Work hours, any hostile or player order ends them, bounded experience): Vanguard leads drills, Duelist spars, Marksman runs range practice, Gunner patrols, Sniper keeps lookout, Guardian guards the gate and home, Field Medic runs first-aid rounds, Officer holds the morning muster.
- **Training is open to willing volunteers:** soldiers plus any free colonist with a Melee or Shooting passion or a soldier relative; combat training from 13.
- **Experience:** specialties mainly from duties, combat adds the 40% share (built).

**Intelligence**
- Remove **Inventor**. Add **Mech Commander** (Biotech), **Diplomat or Informant**, and **Chemist** (its skill and recipes must be checked before any bonus is promised). Keep **Analyst** as the research generalist.
- Rooms, recognised not painted: **Library**, **Research Lab**, **Gene Lab**. No Dark Study room.

**Other modules under discussion:** Wardens and Casters.

## 9. Build slices for Military v2

1. **Built:** the cross-career rule, the Support branch (Field Medic, Officer) with doctrine, events and strings, the 40% combat share, field tending experience.
2. **Built:** alert levels, the toggle, posts and the Training Yard (below).
3. The daily duties and their experience (not built).

### Slice 2: what it does

- **Alert state** (`AlertState`, saved as `wils.military.alert`): Normal, Yellow or Red. The player sets a level on **Community → Military** (three buttons). With *automatic Red* on (the default), hostiles on a home map raise Red; after an hour (`CalmTicks` = 2,500) with none it drops back to the set level. A Red the player sets stays until they change it. An on-screen alert ("Military alert: Yellow/Red") is shown whenever the level is not Normal, and the Military page carries a badge at Red.
- **At Red** every *serving* soldier (anyone assigned a Military role) who is a free colonist, able, not in a mental state and not barred from violence is **drafted** if not already, and sent **once** to a post: Sniper and Marksman to an Overwatch post, Guardian, Vanguard and Gunner to a Guard post, everyone else to the Rally point. An Overwatch or Guard post holds one soldier, Rally is shared, and with no suitable post nobody is sent. Each soldier is handled once per Red, so anything the player orders afterwards stands. At stand-down **only the people Red drafted** are undrafted. Assigning a Military role is the consent; there is no other switch beyond *automatic Red*.
- **Posts** are placed from Architect → Zone ("Place a post", then Overwatch, Guard or Rally; "Remove a post") or from the Military page, are saved per map, shown on the map while placing and at Red, and capped at 60.
- **Training Yard** is a painted zone (Architect → Zone). It stores nothing and **does nothing yet**: the drills that use it are slice 3.
- **Normal and Yellow have no effect yet** beyond the level showing: patrols, lookout, guard, drills and the rest are the duties of slice 3.
- Not built: a hotkey, and a button on the map itself. The on-screen alert and the Community page are the ways in for now.

**Not verified in the game:** that drafting and the Goto order behave as intended (including pawns that are asleep, carrying someone, or on a job the game protects), that the Architect entries appear, that the on-screen alert shows and the map labels draw, and the zone's behaviour. All of it compiles for 1.6 and 1.7 and its rules are tested, but none of it has run.

## 10. Open items

- A Community page for each module (the doctrine table, who holds which role, recent combat credit), like Agriculture's.
- Positioning (section 3).
- More negative and milestone triggers.
- A compatibility mapping surface for modded research, books, weapons and scanners.
- Treasury's research wage may share the raw-rate scale issue found in section 1 (`Labour.ResearchPointsPerUnit` = 400 is compared with the hook's raw amount). Not changed here; worth a look.
