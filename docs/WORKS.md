# Works (Construction and Mining) — design in progress, nothing built

Casters were considered and **set aside for now**: the owner could not see how those roles would casually benefit the community. They may return as their own module later.

## Decided by the owner, one question at a time

1. **Shape.** One module, **Works**, holding **two careers**: Builder and Miner. One assembly, one package, one Community page; a pawn may hold both. Like the other modules it references only the framework.
2. **Builder specialties:** Mason, Carpenter, Engineer. (Surveyor was offered and not chosen.)
3. **Miner specialties:** Prospector, Quarryman, Tunneller. (Deep Driller was offered and not chosen.)
4. **Tunneller:** faster digging through rock, plus *fewer injuries*. **Feasibility caveat, to be checked before building:** vanilla has no cave-ins, so "fewer injuries" must be something the game can actually express (for example a reduced chance of harm from roof collapse near mined areas, or from mining accidents). If the game gives no safe hook for it, it will be dropped and the specialty will be digging speed only, and I will say so rather than claim it.

5. **Ladder.** Labourer (level 1, anyone) → Journeyman (level 2, a modest skill and some experience, earns experience only) → specialty (level 3, a higher skill, carries the bonus). Same shape for both careers.
6. **Daily work: maintenance rounds.** An idle Builder walks the colony repairing damaged buildings through the game's own repair job; an idle Miner walks the mines and tunnels checking supports and roofs and earns experience (nothing is changed). Gentle form as in Military duties and Warden rounds: idle only, rested, capped per day, ended at once by any need, order or hostiles, with an on/off switch.
7. **Bonuses** (each only for a pawn qualified and assigned to that exact specialty; stats checked to exist in the 1.6 reference): Mason +15% ConstructionSpeed and +10% ConstructSuccessChance; Carpenter +15% SmoothingSpeed and +10% ConstructSuccessChance; Engineer +15% ConstructionSpeed and +15% FixBrokenDownBuildingSuccessChance; Prospector +15% MiningSpeed; Quarryman +20% MiningSpeed; Tunneller +10% MiningSpeed plus the injury idea if feasible. **There is no MiningYield stat in the game's code, so ore yield is not boosted.** To be re-checked against 1.7 by compiling before building.

8. **Places:** a **Workshop** (an enclosed room with a work table or two; work in it earns ×1.25 experience for Builders) and a **Mine** (the colony's own dug-out tunnels; work there earns ×1.25 for Miners). Recognised from what is in them, never painted, and shown on the Community page. How a Mine is told from natural rock still has to be worked out.
9. **Family and social effects:** (a) **dangerous work worries kin**: a Builder or Miner hurt, nearly killed or killed on the job leaves their relatives in the colony a worry or grief memory, and a safe stretch earns a relieved memory; (b) **home-building pride**: a Builder who finishes a room or building that a family member lives in leaves that relative a "built this for us" memory; (c) **working families**: relatives on the same crew get a small bond bonus and a shared memory for a job done together. Each role also gets the usual 5 positive, 5 negative and 5 relationship events, generated like the other modules'.

## Still to settle (my defaults, to be confirmed before building)

- What counts as experience: jobs finished, by kind (construction, repair, smoothing, mining, deep drilling), with Journeyman and specialty thresholds to be chosen.
- Build slices and verification, as for Wardens: career tree and events first, then bonuses, then experience hooks, then places and the page, then rounds, then family effects.
