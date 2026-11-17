# Wardens — agreed design (slices 1 to 5 built)

A third career module in the same pattern as Crafting, Intelligence and Military: a career tree in the shared ledger, experience from real game activity, bonuses only for a pawn **qualified and assigned to that exact specialty**, and five positive, five negative and five relationship events per role. It would reference only the framework. **Slices 1 and 2 are built** (the career tree, 75 events, strings, the three stat bonuses, experience hooks and packaging; then the treatment record, kin reactions and conflict of interest); **slice 3 (rooms and the Community page) and slice 4 (cell rounds and the slave-family check) are built too, but the Interview and Visiting rooms do nothing yet (see below).** Every decision below was made by the owner, one question at a time; the parts marked *default* are mine, to be confirmed before building.

## Tree

Guard (anyone) → **Warden** (qualified) → **Confessor**, **Overseer**, **Containment Keeper**.

| Specialty | Needs | Bonus (assigned only) | Verified in the game's code |
|---|---|---|---|
| **Confessor** | Ideology | +15% **conversion power** | the stat `ConversionPower` exists |
| **Overseer** | Ideology (slaves) | +15% **suppression power** | the stat `SuppressionPower` exists |
| **Containment Keeper** | Anomaly | +10% **study efficiency** (studying a contained entity) | the stat `StudyEfficiency` exists |

- **No Persuader.** The owner left it out. So the plain **Warden earns experience only**, like every other level-2 role: recruiting, chatting and feeding prisoners earn Warden experience and unlock the specialties, but carry no bonus. A base-game colony with ordinary prisoners therefore has the career and its events but no bonus.
- Without Ideology the Confessor and Overseer have no work, and without Anomaly neither has the Keeper, exactly as Dark Study needs Anomaly.
- The containment stat (`ContainmentStrength`) belongs to buildings, not people, so the Keeper cannot honestly improve it; hence the study bonus. It overlaps the Dark Scholar a little, which is intended: they work together.

## Family ties

1. **Treatment and kin reactions.** How a prisoner is treated is recorded and credited to the warden responsible. **Treatment means both care and outcomes**: *care* is whether the prisoner was fed, tended and kept in a decent mood while that warden looked after them; *outcomes* is what finally happened (recruited, released, enslaved, executed, died in custody). The prisoner's relatives in the colony react to both, with memories and opinion changes toward the warden. It follows the pattern of the doctor's favouritism record in Community Care.
2. **Conflict of interest.** A warden who is kin to a prisoner, or in the same house, gets conflict events, and others notice if they go easy or hard on a relative.
3. **Slave families.** On their rounds the **Overseer notices whether slave family members share a room or area.** Families kept together leave positive memories for them and a good mark on the Overseer's record; split ones leave negative memories and a bad mark. It observes and reacts; **it never moves anyone.**

Not chosen, so not planned: a gentle-or-hard doctrine for wardens.

## Places (recognised from what is in them, never painted)

- **Interview Room:** two chairs and a table. Persuasion and conversion done there earn more experience (as the Intelligence rooms do, 25%) and nothing else changes.
- **Visiting Room:** a room where a prisoner's kin in the colony can meet them; makes the kin reactions a real event.
- **Cell Block:** a recognised block of prison cells, scored for how humane it is (space, cleanliness, light); it feeds the *care* half of treatment.
- Not chosen: a Courtroom.

*Default rules, to confirm:* Interview Room = an enclosed room with at least two chairs and a table and no bed; Visiting Room = the same, near but not inside the cells; Cell Block = rooms the game itself treats as prison cells or barracks. Matched by what the game exposes, with name fragments where it does not.

## Daily work: cell rounds

An idle Warden in their Work hours walks the Cell Block checking on prisoners (and, for an Overseer, slaves): a short automated round, in the same gentle form as Military's duties (idle only, rested, not needed elsewhere, never with hostiles about, capped per day, any need or order interrupts, a hostile or a draft ends it). It feeds the care record (who was seen, fed and tended) and the slave-family check, and earns experience. (Escorts to the Interview and Visiting rooms came later, in slice 5.)

## Experience (default, matched by name, unverified)

Warden: the game's warden work type (`LabourReport.WorkType` = Warden) and prisoner jobs by name fragment; Confessor: conversion jobs; Overseer: slave and suppression jobs; Keeper: study of contained entities. These are name matches and need checking in play, as Intelligence's are.

## Default skills (to confirm)

Warden: Social 6, 120 experience. Confessor: Social 8 and Intellectual 6. Overseer: Social 8 and Melee 6. Containment Keeper: Intellectual 8 and Melee 6.

## Not yet decided

Whether the Confessor should need the Chaplain's post or stay independent of it (they overlap); the Warden's events (the 75 thoughts, to be written); the exact care measurements (what counts as "decent mood", how long a stint is credited); and whether prisoner outcomes the player chooses by hand are credited to the warden at all or only to the colony.

## Build slices

1. **Built:** tree, careers, events, strings, stat bonuses, experience hooks, tests, packaging. Experience is routed by name (`WardenClassifier`): a conversion job, a slave or suppression job, a holding-platform or containment job, else any job under the Warden work type or with *Prisoner* or *Warden* in its name. Memories: routine success, outstanding result or pride after a finished job, and relationship events when two role-holders interact. **Not verified in the game:** that those names match the real jobs, that the work-type name is `Warden`, and the stat parts. The negative events have thoughts but no triggers yet.
2. **Built:** the treatment record, kin reactions and conflict of interest (below).
3. **Built:** the recognised rooms and the Community page (below).
4. Cell rounds and the slave-family check (built).

## Slice 2: the treatment record

- **Who is responsible.** A holder of any Warden role who finishes a job whose target is a prisoner of the colony becomes **the warden responsible** for that prisoner for the next five days (renewed by every job). Nobody else is ever credited.
- **Care, every hour.** Each prisoner on a home map is looked at: **Good** if fed (30% or more), in a decent mood (40% or more) and not needing tending; **Poor** if starving (under 15%), miserable (under 20%) or needing tending now; otherwise Fair. The result is added to the responsible warden's record, counted separately for kin and for everyone else.
- **Outcomes.** Live references to the current prisoners are kept; when someone stops being a prisoner their final state is read: dead (executed if the colony had set them to be executed, otherwise died in custody), a slave of the colony (enslaved), a member of the colony (recruited), otherwise released. (A prisoner who escapes also reads as released.) Counted on the responsible warden's record.
- **Kin reactions.** Relatives (blood, spouse, lover or fiancé) in the colony react to **both**: good care pleases them (mood +2) and poor care angers them (−3); a recruit (+3) and a release (+4) please them; an enslavement (−5), a death in custody (−4) and an execution (−8) hurt. If a warden was responsible they also gain a **good or bad opinion of that warden** (+6 or −6 for five days). Fair care changes nothing. Each is given at most once in three days per person, thought and prisoner.
- **Conflict of interest.** A warden looking after a prisoner who is their own kin is **torn between duty and family** (mood −2). And a warden whose share of good care for kin prisoners differs from their share for everyone else's by 40 points or more (at least six looks at each; either direction, easy or hard) is noticed: **colleagues** (other Warden-role holders) get a bad opinion of them, at most once a week, after which the counts are halved so it is not announced again at once. Whole-number arithmetic, so a gap of exactly forty counts.
- **Saved** as `wils.wardens.record` (per-warden counters, who is responsible for whom, the time of the last favouritism notice), bounded: counters are halved at 400, 200 prisoners are remembered, and records of people who are gone are dropped on load.
- **It only watches.** A test fails if the code ever starts changing a prisoner (faction, jobs, health, interaction mode, drafting, release, killing).
- **Not verified in the game:** that the prisoner and slave properties read what is meant, that the execution mode is read in time, that the relation checks find the kin, the thought values and wording in play, and the hourly look at a large prison. Slave families are slice 4, below.

## Slice 3: rooms and the Community page

- **Recognised, never painted** (`WardenRooms`, cached 600 ticks per room): a **prison cell** is whatever the game says is one (its room role is prison cell or prison barracks); **humane** means at least two of: a bed, six cells of room for each bed, a clean floor (cleanliness not below zero). An **Interview Room** is an enclosed room with no bed, a table and at least two chairs; a **Visiting Room** the same with at least four chairs.
- **Deviation from the design.** I wrote that the Visiting Room would be "near but not inside the cells". Distance to a cell block is awkward to define, so I told the two apart by chair count instead. Say so if you want it changed.
- **Community → Wardens** shows how many of each room the game has recognised, each warden's treatment record (care good, fair and poor, kin, and outcomes), and what each specialty does.
- **The Interview and Visiting rooms had no effect until slice 5.** In the vanilla game a warden chats with, feeds and converts a prisoner where the prisoner is, in their cell, and nothing ever takes a prisoner to another room. The design said the module would never move anyone, which would leave these two rooms recognised but unable to matter. **The owner chose to add escorts so they work.** That reverses "never moves anyone" and needs its own design and feasibility checks, so it was built last, as slice 5, below.
- **Not verified in the game:** that the room role and the chair, table and bed counts read what is meant, and that the page lists rooms.

## Slice 4: cell rounds and the slave-family check

- **The round** is the job `WFL_WardenRound`. `WardenRoundGame.Run` looks every 500 ticks. A Warden is sent only if idle (the game's own idle tag, not forced by the player), awake, outside Sleep and Joy hours, not hungry or tired, with no need or medical rest pending, rested (4000 ticks since the last round, at most 3 a day) and no hostiles on the map. At most half the wardens (at least one) are on a round at once, and at most one is started per look. Holding any Warden role counts as consent; the Community "Wardens" page has a switch to turn rounds off.
- **Where:** `WardenRounds.Plan` takes the rooms where prisoners sleep (an Overseer also gets the slave rooms), the rooms seen least recently first, at most 6 stops, then walks them nearest-first. Each stop is a 200-tick pause. Deterministic and tested.
- **What a round does:** whoever is in the room at a stop becomes that warden's responsibility (the same record the care samples credit), and a round that made at least one stop earns 4 points (an Overseer's count toward their specialty). It never moves, locks, frees or changes anyone; a guard test enforces that.
- **Slave families:** at the end of an Overseer's round every pair of slave kin is looked at: the same room is *together*, different rooms *apart* (unknown rooms and repeated pairs are not counted). The slaves get a 3-day memory (+2 together, -3 apart), the Overseer's record keeps the counts (shown on the page), and nothing is moved.
- **Not verified in the game:** idle detection and job pickup, room recognition for stops and slave beds, the thought values, and whether wardens walk sensibly around a large prison.

## Slice 5: escorts (built)

A Warden brings a prisoner to the Interview Room (for persuasion or conversion) or, if a relative is in the colony, to the Visiting Room. Decided by the owner:

- **Who:** only willing or calm prisoners. Never flight risks, anyone in a mental break, or the injured or downed.
- **How often:** at most once a day per prisoner, during the Warden's rounds.
- **How:** the Warden leads and the prisoner follows. The prisoner takes a short "being escorted" job: walks to the room beside the Warden, stays a set time, then walks back to their cell. If the Warden is interrupted or hostiles appear, the prisoner is returned at once and the escort ends. They never leave the enclosed area.
- **Effect:** a small mood lift and good care on the warden's record; a bigger lift for a visit from kin, and kin hear of it as good treatment; and an interview also eases recruitment or conversion for that day. The recruitment effect changes game balance, so it is the most important thing to verify in play.

**As built:**
- `WardenEscortGame.Run` looks every 600 ticks (and honours the same rounds switch). The Warden must be idle and rested; at most half the wardens are out at once, 3 escorts a day each, and a rest after each. Candidates are checked by `WardenEscorts.Check` (mood at least 40%, not hungry or tired below 30%, not breaking out, not needing tending) and by a pathfinding check that every cell on the way is in an enclosed, roofed room that does not touch the map edge, within 80 steps.
- **Room:** the Visiting Room if a free relative can come (they get the same job and a "visited kin" memory), otherwise the Interview Room if the prisoner is being recruited, having resistance reduced, or converted; otherwise nobody is taken.
- **One job, three roles** (`WFL_WardenEscort`): the Warden walks there and stays 1200 ticks with the prisoner close by; the prisoner and any visitor wait for the Warden; the prisoner then walks home. Hostiles, a draft, a mental state, a need, the Warden being interrupted or 5000 ticks all end the wait, and the prisoner still goes home.
- **Effects, deliberately small:** interview: +3 mood for a day, resistance reduced by 0.5 once (about half a vanilla recruitment chat; never below zero), or conviction down 2 points for a conversion; visit: +6 mood for two days for the prisoner and +3 for the visiting relative. Both count as good care on the warden's record (kin hear of it) and earn 6 experience.
- **Balance:** with a daily limit per prisoner and per warden, the most an interview can remove is 0.5 resistance a day per prisoner. Treat it as a gentle help to the Confessor, not a substitute for recruiting, and tune in play.
- **Not verified in the game:** the prisoner's behaviour once given a job (that they resume their routine and return to the cell), the pathing check, whether guards and doors interact, the thought values, and that the guest resistance and certainty changes behave. These are the first things to check in a test run.

## What it would not do

Apart from the escorts (a calm prisoner walked to a room and back), it never moves, frees or punishes anyone, never decides a prisoner's fate, and changes no vanilla chance except by the three agreed stat bonuses.
