# Wardens — agreed design (not yet built)

A third career module in the same pattern as Crafting, Intelligence and Military: a career tree in the shared ledger, experience from real game activity, bonuses only for a pawn **qualified and assigned to that exact specialty**, and five positive, five negative and five relationship events per role. It would reference only the framework. **Nothing here is in the code yet.** Every decision below was made by the owner, one question at a time; the parts marked *default* are mine, to be confirmed before building.

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

An idle Warden in their Work hours walks the Cell Block checking on prisoners (and, for an Overseer, slaves): a short automated round, in the same gentle form as Military's duties (idle only, rested, not needed elsewhere, never with hostiles about, capped per day, any need or order interrupts, a hostile or a draft ends it). It feeds the care record (who was seen, fed and tended) and the slave-family check, and earns experience. Visit escorts were *not* chosen.

## Experience (default, matched by name, unverified)

Warden: the game's warden work type (`LabourReport.WorkType` = Warden) and prisoner jobs by name fragment; Confessor: conversion jobs; Overseer: slave and suppression jobs; Keeper: study of contained entities. These are name matches and need checking in play, as Intelligence's are.

## Default skills (to confirm)

Warden: Social 6, 120 experience. Confessor: Social 8 and Intellectual 6. Overseer: Social 8 and Melee 6. Containment Keeper: Intellectual 8 and Melee 6.

## Not yet decided

Whether the Confessor should need the Chaplain's post or stay independent of it (they overlap); the Warden's events (the 75 thoughts, to be written); the exact care measurements (what counts as "decent mood", how long a stint is credited); and whether prisoner outcomes the player chooses by hand are credited to the warden at all or only to the colony.

## Build slices (proposed)

1. Tree, careers, events, strings, stat bonuses, experience hooks, tests.
2. Treatment record, kin reactions and conflict of interest.
3. The recognised rooms and the Community page.
4. Cell rounds and the slave-family check.

## What it would not do

It never moves, frees or punishes anyone, never decides a prisoner's fate, and changes no vanilla chance except by the three agreed stat bonuses.
