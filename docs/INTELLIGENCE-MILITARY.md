# Intelligence and Military — first draft

Two optional role modules, built the way Crafting and Agriculture are: a career tree in the shared career ledger, experience from real game activity, specialty bonuses that apply only to a pawn who is **qualified and assigned to that exact specialty**, and five positive, five negative and five relationship events for every role. Each references only the framework.

**Status: compiles for 1.6 and 1.7, 1,608 offline tests pass, the three new Harmony patches pass the repository's patch check against both reference assemblies. Nothing has run in RimWorld.** Section 7 says exactly what that does and does not prove.

## 1. Intelligence — `SpacemanWil.WilsIntelligence`

Assistant (anyone) → **Researcher** (Intellectual 6, 120 experience) → a specialty (250 general + 60 specialty experience).

| Specialty | Skills | Experience comes from | Assigned bonus |
|---|---|---|---|
| **Analyst** | Intellectual 8, Social 5 | finishing a scanner operation (any job whose name contains "Scanner") | +10% research speed on any project (the generalist) |
| **Geneticist** | Intellectual 10, Medicine 10 | research on gene and xenogerm projects; making a xenogerm or genepack at a bench | +15% research speed on gene projects |
| **Dark Scholar** (Dark Study) | Intellectual 10 | research on Anomaly projects; finishing a study session | +15% research speed on Anomaly projects, +20% study efficiency |
| **Author** (Writing) | Intellectual 8, Artistic 8 | writing a book (Anomaly books) | books gain one quality band, up to Excellent |
| **Mech Commander** (Biotech) | Intellectual 8, Crafting 6 | finishing work on a mechanoid (any job whose name contains "Mech") | +1 mech bandwidth, for a mechanitor only |
| **Diplomat** | Intellectual 6, Social 8 | talking with an outsider: a guest, a visitor or a prisoner (once per person per day) | +10% negotiation ability, +5% social impact |
| **Chemist** | Intellectual 8, Medicine 4 | finishing a drug or medicine bill | a 10% chance of one extra unit when a bill finishes |

Changes agreed with the owner: the draft's *Scientist* is folded into **Researcher**; the **Inventor** is removed (it had no work of its own: its bonus applied to nearly all research by mid-game and to none early); **Mech Commander**, **Diplomat** (the "Diplomat or Informant" idea; the name is open) and **Chemist** are added. **Analyst** stays the research generalist.

**Why these bonuses.** Each is on something that could be checked: `NegotiationAbility`, `SocialImpact` and `MechBandwidth` all appear in the game's code. The drug and medicine production-speed stats do **not**, so a Chemist bonus on them could silently do nothing; the Chemist's bonus is instead the extra unit, which the module decides itself (from the job and the pawn, so it is the same however often it is asked) and which only an *assigned* Chemist gets. It adds a unit to a stack the game has already made, so it changes no recipe. Mech work, the drug and medicine tests and the gene, book and study names are matched by name fragment and need checking in play.

**Geneticist** keeps the rule from [CAREERS.md](CAREERS.md): it wraps an existing vanilla bill (the Xenogerm Forge's), not an invented activity, and changes no vanilla surgery. "Doctor first" is dropped because modules never depend on each other; Medicine 10 stands in for it.

**Dark Study needs Anomaly, Geneticist and Mech Commander need Biotech, Author needs books to write.** Without them those specialties simply have no work and nobody earns them. Nothing is gated or faked. `StudyEfficiency` is an Anomaly stat; the patch that touches it succeeds quietly where it is absent.

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

Every role has five positive, five negative and five relationship events (Intelligence: 9 roles, 135 events; Military: 11 roles, 165). Each has its own thought with its own wording. They are generated from one table (`scripts/role_events/spec.py`, run `gen.py`) so a fact and its thought cannot drift apart. Within each category the index means the same thing for every role: positive — routine success, outstanding result, milestone, recognised by others, pride; negative — failed work, waste or setback, strain, rejected, danger or harm; relationship — praised, helped, shared know-how, dispute, betrayal or stolen credit.

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

## 8. Agreed with the owner

Settled by question and answer. **The Military items are now built (section 9); the Intelligence items are not yet.**

**Military**
- **Alert levels:** *Normal* (drills, sparring, range practice, muster, first-aid rounds), *Yellow* (patrols, lookout, gate and home guard), *Red* (hold defensive positions). The player sets Normal or Yellow, with an easily reachable toggle; a threat on the map **raises Red automatically** and it drops back to the set level when clear.
- **Positions:** player-placed defensive positions (typed Melee, Ranged or Mixed, plus a Rally point; see section 11) and a painted Training Yard. With no positions placed, nobody is sent anywhere at Red.
- **Daily duties** (automatic, only for free assigned soldiers in Work hours, any hostile or player order ends them, bounded experience): Vanguard leads drills, Duelist spars, Marksman runs range practice, Gunner patrols, Sniper keeps lookout, Guardian guards the gate and home, Field Medic runs first-aid rounds, Officer holds the morning muster.
- **Training is open to willing volunteers:** soldiers plus any free colonist with a Melee or Shooting passion or a soldier relative; combat training from 13.
- **Experience:** specialties mainly from duties, combat adds the 40% share (built).

**Intelligence**
- **Built (section 11):** Inventor removed; Mech Commander, Diplomat and Chemist added; Analyst kept as the research generalist; rooms recognised, not painted: **Library**, **Research Lab**, **Gene Lab** (no Dark Study room).

**Other modules under discussion:** Wardens and Casters.

## 9. Build slices for Military v2

1. **Built:** the cross-career rule, the Support branch (Field Medic, Officer) with doctrine, events and strings, the 40% combat share, field tending experience.
2. **Built:** alert levels, the toggle, posts and the Training Yard (below).
3. **Built:** the daily duties and their experience (below).

### Slice 2: what it does

- **Alert state** (`AlertState`, saved as `wils.military.alert`): Normal, Yellow or Red. The player sets a level on **Community → Military** (three buttons). With *automatic Red* on (the default), hostiles on a home map raise Red; after an hour (`CalmTicks` = 2,500) with none it drops back to the set level. A Red the player sets stays until they change it. An on-screen alert ("Military alert: Yellow/Red") is shown whenever the level is not Normal, and the Military page carries a badge at Red.
- **At Red** every *serving* soldier (anyone assigned a Military role) who is a free colonist, able, not in a mental state and not barred from violence is **drafted** if not already, and sent **once** to a position: their own type first (Melee for the Soldier branch, Ranged for the Ranger branch), then a Mixed one, then the Rally point; Field Medics, Officers and Recruits prefer Mixed; a soldier the player has posted to a position goes there. A typed position holds one soldier, Rally is shared, and with no suitable position nobody is sent. Each soldier is handled once per Red, so anything the player orders afterwards stands. At stand-down **only the people Red drafted** are undrafted. Assigning a Military role is the consent; there is no other switch beyond *automatic Red*.
- **Positions** are placed from Architect → Zone ("Place a defensive position", then Melee, Ranged, Mixed or Rally; "Remove") or from the Military menu, are saved per map, shown on the map while placing, while the menu is open and at Red, and capped at 60. (Slice 2 first had Overwatch and Guard posts; they became Ranged and Melee in the menu work, keeping their saved numbers.)
- **Training Yard** is a painted zone (Architect → Zone). It stores nothing; it is where the drills, sparring, range practice and first aid happen (slice 3).
- Not built: a hotkey, and a button on the map itself. The on-screen alert and the Community page are the ways in for now.

**Not verified in the game:** that drafting and the Goto order behave as intended (including pawns that are asleep, carrying someone, or on a job the game protects), that the Architect entries appear, that the on-screen alert shows and the map labels draw, and the zone's behaviour. All of it compiles for 1.6 and 1.7 and its rules are tested, but none of it has run.

### Slice 3: daily duties

- **Who and when.** Serving soldiers (anyone assigned a Military role) who are idle in their Work or Anything hours (never Sleep or Joy), not drafted, not in a mental state, not hungry or tired, not obeying an order, aged 13 or more and able to fight, are given a duty every 300 ticks. At most half the serving soldiers are on duty at once, at most two start per look, each does at most 3 blocks a day (a block is 1,500 ticks) with 4,000 ticks of rest between. **Never at Red, never while the map has hostiles, never with the daily-duties switch off.** A duty is an ordinary job: the game's own needs and any order of the player interrupt it, and a hostile appearing or a draft ends it.
- **What each does.** At Normal: Vanguard leads a **drill**, Duelist **spars**, Marksman runs **range practice**, Field Medic stands by with **first aid**, Officer holds **muster** (at a Rally point, or in the yard if there is none); Guardian drills, Gunner and Sniper practise at the range; Recruits, Soldiers and Rangers drill or practise. At Yellow: Gunner **patrols** (a patrol route; with no route to walk, a chain of positions, or around the home area), Sniper **keeps lookout** at a Ranged or Mixed position, Guardian **guards the gate** at a Melee or Mixed position (a soldier takes the position they are posted to, else their own type, then Mixed), and everyone else without a specialty patrols; the other specialties carry on. With no yard or no suitable post, that duty simply does not happen, and the Military page says so.
- **Groups.** A Vanguard, Marksman or Officer who starts a drill, range practice or muster opens a session; other soldiers whose duty is the same join it (up to 5 besides the leader), holding only for what is left of the leader's block. Colonists who are not soldiers may **volunteer** into a drill or range practice already running (up to 3 per session), in their own free time only (Anything or Joy, never Work), if they have a passion for Melee or Shooting or a soldier in the family, and train their skills. Sparring pairs the Duelist with another free soldier, or they practise alone.
- **What it earns.** Skill experience in the duty's skill each minute (5 points, about 120 a block). Career experience for serving soldiers once a block is done (or at least half of it): 12 for leading or first aid, 10 for watch duty, 8 for taking part; a leader with nobody joining earns half. An assigned specialist earns toward their own specialty. A soldier who has earned only a branch **practises the specialty their skills and experience put them nearest to**: the one they meet the skills for, hold the branch (and the Doctor qualification, for the Field Medic) for, and have the most experience banked in. This is how a specialty is earned before it can be assigned. A Recruit builds toward the branch of their better skill. Volunteers earn skills only.
- **Memories.** A finished block can leave a routine-success or pride memory; trainers and partners leave each other a kind relationship memory.
- **Switches** on Community → Military: daily duties, and volunteers. Both default on (assigning a role is the consent).
- **Removal.** Preparing the module for removal ends every duty in progress, deletes the Training Yard zones (a saved zone of an absent module cannot load), stands down Red, and keeps the posts (only saved numbers).

**Not verified in the game:** that the duty job runs and ends cleanly; that an "idle" soldier is detected often enough in Work hours to ever be given duty; that pawns find and reach yard cells; the patrol route; that the experience arrives and a specialty is in fact earned this way; and the pacing. Training in the yard has no animation or targets, the pawn stands there while their skill improves, which is honest but plain.

## 10. Intelligence v2

- **Specialties** are as in section 1. The Diplomat's experience comes from the framework's existing `SocialInteraction` hook (a colonist talking with a non-colonist, or a prisoner); the Mech Commander's and the rest from `LabourDone`, `ProductMade` and `ResearchDone`.
- **Rooms.** An *enclosed* room is a **Gene Lab** if it holds gene equipment (extractor, assembler, gene bank or processor, xenogerm forge), else a **Research Lab** if it holds a research bench, else a **Library** if it has at least two bookshelves and a table. Matched by name fragment; each room is looked at again at most once every 600 ticks; nothing is saved or painted. **Work done in a room that suits it earns 25% more experience** (at least one more point): research at a bench in a Research Lab, a book in a Library, a xenogerm in a Gene Lab. Nothing else about the work changes.
- **Community → Intelligence** shows how many of each room the game has recognised, the rule, and what each specialty does.
- **Not verified in the game:** the room matching (furniture names), that a pawn's room is the one meant, the mech job names, that a Chemist's extra unit lands on the stack, the stat parts on `NegotiationAbility`, `SocialImpact` and `MechBandwidth`, and that the page lists rooms.

## 11. Military management menu

Settled with the owner by question and answer. **Slice 1 is built** (the tabbed menu, typed positions, patrol routes, posting); **the Armory and the bottom-bar icon are not yet.** Until the icon exists the on-screen alert only *displays* the level and there is **no quick control**: the Alert tab is the only place to set it.

- **Home:** the Military page becomes a tabbed menu in the Community window: **Alert**, **Positions**, **Facilities**, **Soldiers**. Placing things still happens on the map.
- **Defensive positions** are typed **Melee, Ranged or Mixed**, plus the **Rally** point, and replace Overwatch and Guard (an existing Overwatch post becomes Ranged, Guard becomes Melee, Rally is unchanged). A soldier takes a position of their own type first (Soldier branch: Melee, Ranger branch: Ranged), then a Mixed one, then Rally; Field Medics and Officers prefer Mixed. One soldier per position; Rally is shared.
- **Patrol spots** are typed **Melee, Ranged or Mixed**. Single spots, **ordered automatically within a route**, **no limit on spots per route, several routes**. A route is named and numbered (Route 1, Route 2, ...), each with a colour on the map; **the route is chosen when the spot is placed**, and a spot can be moved to another route from the menu. **A patrol walks a stretch of its route for about one duty block and the next patrol continues where it stopped**, so a long route is covered over several patrols and a short one is walked in full. With fewer than two spots a patrol still falls back to the home area.
- **Armory:** a painted zone preset as a weapons and armour stockpile. At **Yellow or Red** a serving soldier with no suitable weapon (none, or the wrong kind for their branch) takes **the best suited** one from it (highest quality of the right kind, ties by value; any for Support and Recruits) and **keeps it**. Existing gear is never touched.
- **Training Yard:** unchanged, one type.
- **Who goes where:** automatic by type and distance by default, with **optional posting**: on the Soldiers tab a specific soldier can be posted to a route or position and then always uses it; choosing automatic puts them back.
- **Soldiers tab:** each serving soldier's role, the specialty they are practising and their progress; what they are doing now and where (route or position), click to select; and the posting control. (No off-duty toggle.)
- **Quick alert control:** an **icon in the bottom bar's button cluster**, coloured by level. **Left-click cycles** Normal, Yellow, Red and back; **right-click** opens a menu to pick a level or open the Military menu. The Military menu's Alert tab is the secondary place to set it.

**Build slices:** (1) **built:** typed positions, patrol routes, posting and the tabbed menu; (2) the Armory and arming the unarmed; (3) the bottom-bar icon.

### Slice 1: what it does

- **Community → Military** is now four tabs. **Alert** holds the level buttons, automatic Red and the duties and volunteers switches. **Positions** places and lists defensive positions (showing who is posted to each) and manages patrol routes. **Facilities** paints the Training Yard. **Soldiers** lists every serving soldier.
- **Defensive positions:** Melee, Ranged, Mixed and the Rally point, as agreed. A position is one soldier; Rally is shared. The lookout and the gate guard use them too.
- **Patrol routes:** up to 12 routes and 300 spots, each route named (Route 1, Route 2, ...) and coloured on the map, each spot typed Melee, Ranged or Mixed and placed in a route you choose (from the Architect "Place a patrol spot", which asks for the route, or a new one, and the type; or the route's own buttons in the menu). A spot can be moved to another route or removed. Within a route the order is automatic: a chain from the first spot placed, nearest next. A patrol walks the **stretch** of its route that fits in about one duty block (at least two spots, never more than one lap) starting where the last patrol on that route stopped, so a long route is covered over several patrols and a short one is walked in full. A soldier walks the spots of their own type and Mixed (a Mixed soldier walks all) on the route walked least recently that has spots they may walk; a soldier posted to a route walks all of it.
- **Posting:** on the Soldiers tab each soldier has a button: Automatic, a route or a position. A posted soldier always uses it; a posting to something that is gone, or a soldier who is gone, is dropped when the colony loads. At Red a soldier posted to a position goes there first.
- **Soldiers tab columns:** name (click to go to them), role, what their duties are counting toward and the experience banked, what they are doing and where, and the posting.
- **Not verified in the game:** every control and layout of the menu, the placement flow of the two-step Architect menus, the route colours and lines on the map, that a long route really is walked in stretches, and the Soldiers tab's live text.

## 12. Open items

- A Community page for each module (the doctrine table, who holds which role, recent combat credit), like Agriculture's.
- Positioning (section 3).
- More negative and milestone triggers.
- A compatibility mapping surface for modded research, books, weapons and scanners.
- Treasury's research wage may share the raw-rate scale issue found in section 1 (`Labour.ResearchPointsPerUnit` = 400 is compared with the hook's raw amount). Not changed here; worth a look.
