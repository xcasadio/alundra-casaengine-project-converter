# ADR-0103: The bomb is born in the hands by the Circle and runs C62, with a start, a driver and a cancellation registered in production

- **Status**: Accepted
- **Date**: 2026-10-26
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.30.3 (E20.h3b, revision 4.1; D-H-1 to D-H-6, D-C2B-11, D-E20-52, Q-H3B-1); `docs/plan-e20-annexe/e20-h3b/` (`README.md`, `t1-results.md`, `t2-results.md`, `t3-results.md`, `t4.md`); `docs/plan-e20-annexe/e20-h3/` (oracle and critic); `docs/plan-e20-annexe/e20-a362-disc/` (keys `k_w8_explode_in_hands.txt`, `k_throw_explode.txt`); reading of `ALUN_CD.EXE` (France): the start `0x8003453C`, the driver `0x800352C4`, the cancellation `0x80034B54`, C62 `0x8007A978`

## Context

- After E20.h3a the bomb at rest explodes and vanishes, but nothing makes one: the entry `0x23` of the three tables of the use of an item (ADR-0099) was a named stub, and C62 (the fuse, the sparks, the flight) did not exist.
- The oracle (MIPS replay of the real code and the world keys of h3 and of the A362 reading) fixes the values to the tick.

## Decision

- `BombAi` (`Alundra/Ai/Items/BombAi.cs`) gains `Start`, `Drive`, `Cancel` and `Tick` (C62, slot C, type 2 only). The start refuses (sound 3, answer 1) when the hero holds something, stands on the slope 4 or has an item in use, or when three entities of the type 2 are in the world whatever their status (a bomb flagged to be destroyed counts until its slot is recycled); otherwise the bomb is born at the hero by the interface, its fuse is set to 240 after the birth, and it is put in the hands (`PlayerManager.PutInHands`). The driver answers 1 when the hands hold nothing or something other than a bomb, 0 while the bomb is carried, and cancels the item in the deep water (slope 4). The cancellation tests the throw code at `+0x24` (the decompilation says `+0x20`: the binary decides), clears the link, emits the smoke (global effect 12) and destroys the bomb with the single-argument destruction. C62 decrements the fuse, makes one spark (effect 9, 8 px up, two draws only when the stage gave the effect) per fuse multiple of 8 under 60, goes to the status 3 and lets go at the fuse 0, carries, throws with the flight animation 2, bounces once on a wall in the air, explodes at the foot of a wall of the class B, and plays the sound `0x18` on landing.
- `PlayerManager` points the entries `0x23` of the three tables to these functions (the test seams still win); `NativeSpriteRegistry.CreateForProduction` registers `(C, 62)` through `BombAi.Keys`.
- Two resolutions inside the DLL, no engine file: the bomb does not join the tick of its birth (`JoinsBirthTick = false`), and an entity carried by the hero keeps the fraction of the carrier's position (`PushLogicalPositionToRoot(keepSubPixel: true)` from `FollowCarrier` and once at the birth), as the binary adds the offset to the carrier's position with its fraction.
- Q-H3B-1 is kept as the default A, as the original: a bomb thrown downwards by a hero who stays hurts him (5 hearts of 10).

## Consequences

- Circle with the bomb equipped: the bomb is lifted, burns 240 ticks with seven sparks, is thrown by the Square (34.5 px east and west, 23 px north and south), bounces once on a wall, explodes alone; the lock of the control or the deep water put it out in smoke; at most three bombs.
- Pinned by 25 vectors per function, `BombHandsWorldTests` (w1 to w11, 26 cases, tick by tick against the keys with no tolerance), and by 34 real mutants, all killed except two equivalent ones that were replaced by their neighbours (`docs/plan-e20-annexe/e20-h3b/t4.md`).
- Every object carried by the hero now keeps the hero's fraction (crates, barrels, torches): no existing test or key moved.
- Added the preset `bombe-m14` for the recipe of the author.

## Amendment (E20.e0a6, 2026-10-27)

- `keepSubPixel` (introduced here for `FollowCarrier` and the birth of the bomb) is now also passed by `WorldProxy.SpawnEntityBySpriteIndex` (D-E20-92, D-E0A6-1): an entity born by the interface keeps the fraction of the pixel of its birth position at the root and in its logical position, like the original, whose birth routine `0x80039D04` stores the 16.16 position unshifted (the loot birth `0x80032B90` passes the 16.16 position of the crate).
- The other call sites (map-load loop, record spawn) start from whole positions and are unchanged.
- Pinned by `SpawnBySpriteIndexInterfaceTests.S1_...`, `WorldProxySpawnSubPixelTests.S2_...` (the rocks of the earth tome 2 against the key `cast_2c_pf_mid_x600y480`) and `LiftableBreakWorldTests.B5` (rewritten by derivation); the mutant `keepSubPixel: false` is killed by all three (5 red of 32). See `docs/plan-e20-annexe/e20-e0a6/t2/results.md`.
