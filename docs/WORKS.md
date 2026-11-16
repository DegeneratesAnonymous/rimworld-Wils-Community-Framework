# Works (Construction and Mining) — slice 1 built

Casters were considered and **set aside for now**: the owner could not see how those roles would casually benefit the community. They may return as their own module later.

## Decided by the owner, one question at a time

1. **Shape.** One module, **Works**, holding **two careers**: Builder and Miner. One assembly, one package, one Community page; a pawn may hold both. Like the other modules it references only the framework.
2. **Builder specialties:** Mason, Carpenter, Engineer. (Surveyor was offered and not chosen.)
3. **Miner specialties:** Prospector, Quarryman, Tunneller. (Deep Driller was offered and not chosen.)
4. **Tunneller:** faster digging through rock. *Fewer injuries* was wanted but is not possible (see 7), so the specialty is digging speed only.

5. **Ladder.** Labourer (level 1, anyone) → Journeyman (level 2, a modest skill and some experience, earns experience only) → specialty (level 3, a higher skill, carries the bonus). Same shape for both careers.
6. **Daily work: maintenance rounds.** An idle Builder walks the colony repairing damaged buildings through the game's own repair job; an idle Miner walks the mines and tunnels checking supports and roofs and earns experience (nothing is changed). Gentle form as in Military duties and Warden rounds: idle only, rested, capped per day, ended at once by any need, order or hostiles, with an on/off switch.
7. **Bonuses** (each only for a pawn qualified and assigned to that exact specialty; stats checked to exist in the 1.6 reference): Mason +15% ConstructionSpeed and +10% ConstructSuccessChance; Carpenter +15% SmoothingSpeed and +10% ConstructSuccessChance; Engineer +15% ConstructionSpeed and +15% FixBrokenDownBuildingSuccessChance; Prospector +15% MiningSpeed; Quarryman +20% MiningSpeed; Tunneller +10% MiningSpeed (the "fewer injuries" idea was checked and **dropped**: the only roof-collapse hook in the game is a field on building definitions, not a pawn stat, so there is no safe way to do it). **There is no MiningYield stat in the game's code, so ore yield is not boosted.** All five stats were confirmed in both the 1.6 and 1.7 references.

8. **Places:** a **Workshop** (an enclosed room with a work table or two; work in it earns ×1.25 experience for Builders) and a **Mine** (the colony's own dug-out tunnels; work there earns ×1.25 for Miners). Recognised from what is in them, never painted, and shown on the Community page. How a Mine is told from natural rock still has to be worked out.
9. **Family and social effects:** (a) **dangerous work worries kin**: a Builder or Miner hurt, nearly killed or killed on the job leaves their relatives in the colony a worry or grief memory, and a safe stretch earns a relieved memory; (b) **home-building pride**: a Builder who finishes a room or building that a family member lives in leaves that relative a "built this for us" memory; (c) **working families**: relatives on the same crew get a small bond bonus and a shared memory for a job done together. Each role also gets the usual 5 positive, 5 negative and 5 relationship events, generated like the other modules'.

## Still to settle (my defaults, to be confirmed before building)

- What counts as experience: jobs finished, by kind (construction, repair, smoothing, mining, deep drilling), with Journeyman and specialty thresholds to be chosen.
- Build slices and verification, as for Wardens: career tree and events first, then bonuses, then experience hooks, then places and the page, then rounds, then family effects.

## Slice 1: the careers and events (built)

- Module `wils.works`, assembly and package `WilsWorks`. Two careers: **Builder** (`wils.works.builder`: Labourer, Journeyman, then Mason, Carpenter, Engineer) and **Miner** (`wils.works.miner`: Labourer, Journeyman, then Prospector, Quarryman, Tunneller). The Journeyman needs skill 5 (Construction or Mining) and 120 experience; a specialty needs 250 general and 60 specialty experience and: Mason Construction 8; Carpenter Construction 8 and Crafting 4; Engineer Construction 8 and Intellectual 6; Prospector Mining 8 and Intellectual 4; Quarryman Mining 8; Tunneller Mining 10. (My defaults, to be tuned.)
- 150 events (5 positive, 5 negative and 5 relationship for each of the 10 roles), generated from `scripts/role_events/spec.py` by `gen.py` into `WorksEvents.cs` and `WorksThoughts.xml`. Thoughts are prefixed `WFL_K_`.
- **Nothing earns experience or does anything in the game yet.** Bonuses are slice 2, experience hooks slice 3, places and the page slice 4, maintenance rounds slice 5 and family effects slice 6.

## Slice 2: the bonuses (built)

`WorksBonuses.Factor` and `StatPart_WorksSpecialty`, patched onto ConstructionSpeed, ConstructSuccessChance, SmoothingSpeed, FixBrokenDownBuildingSuccessChance and MiningSpeed (`Patches/WorksStats.xml`, always-succeeding so a missing stat is skipped). Applies only to a pawn qualified and assigned to that exact specialty. **Not verified in the game:** that the stat parts show and apply, and the balance.

## Slice 3: experience (built)

`WorksClassifier` routes a finished job by the game's own job name, matched whole (so "Examine" is not "Mine"): **FinishFrame** (building: Engineer if the thing built is power, production or temperature, Carpenter if furniture, Mason if made of stone, otherwise plain Builder; 4 points), **FixBrokenDownBuilding** (Engineer, 3), **Repair** (Builder, 3), **SmoothFloor/SmoothWall** (Carpenter, 2), **Mine** (Prospector for ore, Tunneller for rock under thick mountain roof, Quarryman for rock in the open; 4) and **OperateDeepDrill** (Miner, 4). Hooked through the framework's LabourDone report, once per job. Finished work also gives the worker the routine, outstanding or pride memory of their role, and chatting with another Works worker the relationship events, at most once in three days. **Not verified in the game:** the job names, the frame, stuff and roof reads at the moment a job ends, and the memory pacing.

## Slice 4: places and the Community page (built)

`WorksRooms.Classify`: a **Workshop** is an enclosed room with at least two work benches and no bed; a **Mine** is a room of at least six cells, not outdoors, most of it under thick mountain roof (so a dug-out room, whether or not it is sealed). Building, fixing, repairing and smoothing in a Workshop, and digging in or beside a Mine, earn 1.25 times the experience (at least one more point); work elsewhere earns the normal amount. The Community page "Works" (order 170) lists the places found and what each specialty does. **A weakness, said plainly:** construction usually happens where the building is going, not in a workshop, so the Workshop bonus will mostly apply to furniture and machinery built or repaired inside one. **Not verified in the game:** how rooms are cut inside a mountain, whether a dug-out room counts as outdoors in play, and whether the bench and roof reads are right.

## Slice 5: maintenance rounds (built)

`WorksRoundGame.Run` looks every 500 ticks. A worker is sent only if idle (the game's idle tag, not forced by the player), awake, outside Sleep and Joy hours, not hungry or tired, with no need or medical rest pending, rested (4000 ticks since the last round, 3 a day at most), and no hostiles on the map; at most half the workers are out at once. **A Builder** is given the game's own repair job (`JobDefOf.Repair`) on the most damaged repairable building below three quarters of its hit points, nearest if equal, only if reachable and reservable; the repair earns its experience through the normal job hook. **A Miner** takes the job `WFL_WorksRound`: up to 4 dug-out rooms, least recently seen first, 200 ticks at each, then 4 experience; nothing is changed. The Community page has a switch (saved as `wils.works.record`, on by default) to turn rounds off. **Not verified in the game:** idle detection and job pickup, whether the repair job accepts being given this way, and the dug-out room recognition the Miner's walk depends on.
