# E20.h3b T0: the annex, the oracle keys, the spark policy, the census and the predicted list (2026-10-26)

Written by T0 of E20.h3b (`docs/plan-e20-heros.md` section 1.30.3, revision 4.1), branch `chantier/theme2-attaques-objets`, HEAD of departure `dd3adb1d`. **No code of `Alundra/` or `Alundra.Tests/` changed**: the only
throwaway code (three probe tests, never committed, texts kept in `probe/`) was removed before the commit. The scripts read the binary `ALUN_CD.EXE` (France) at the absolute path they name (outside the
repository), the data at `data-extracted/data/`, and write next to themselves: rerun them from a COPY, never in place. `SHA1SUMS.txt` lists every file here.

Method of every replay below: the oracle of E20.h3 (`../e20-h3/critic/run/` for the critic's scenarios `scen_*.py`, `../e20-h3/bin/run/` for the reading's `sc_*.py`: MIPS interpreter, real `ALUN_CD.EXE` code),
copied to a scratch folder with a `results/` folder beside it; the `k_*.py` of `../e20-a362-disc/critic/src/` and `../e20-a362-disc/critic/run/runpy.py` added to the copy of the critic. **The oracle is NOT modified**:
the hero is hurtable (D-H-7 is void, D-C2B-11); the stored files of the critic (`../e20-h3/critic/results/`) were produced without the setting too, which is why every replay below is **byte for byte identical** to its stored twin.

## (a) The vectors of the start, the driver, the cancellation and C62

| what | command (from a copy) | result |
|---|---|---|
| the 300 self-contained vectors of the critic (12 functions, 25 each: `start_beans`, `start_bomb`, `drv_beans`, `drv_bomb`, `cancel_beans`, `cancel_bomb`, `dispatch`, `C62`, `D30`, `E12`, `C73`, `E21`; every data word read and written, the journal of the calls) | `python -B my_goldens.py 25` in `critic/run`, then `cmp` with `../e20-h3/critic/results/goldens_critic.json` | **identical** (the file T1 reads for the vectors of `start_bomb`, `drv_bomb`, `cancel_bomb`, `C62`, as `BombVector.cs` reads those of D30 and E12) |
| the same file replayed through the models of the critic from an EMPTY memory (any read of an unlisted word is an error) | `python -B my_goldcheck2.py` | `vectors 300 differences 0 reads of unlisted words: 0` (`vectors/replay_goldens_critic.out.txt`) |
| the real code against the models of the reading (`h3_vec.py`, structured random worlds, seed 100) | `python -B h3_vec.py 4000 100 <function>` for the four functions | `start_bomb`, `drv_bomb`, `cancel_bomb`, `C62`: 4000 vectors each, **0 mismatches**; coverage 81/81, 23/23, 34/34, 186/186 instructions (`vectors/vec_real_vs_models.out.txt`) |
| the real code against the models of the critic (`my_diff.py`, seed 100) | `python -B my_diff.py 6000 100 start_bomb drv_bomb cancel_bomb C62` | 6000 vectors each, **0 mismatches**, same coverage (`vectors/my_diff_real_vs_critic_models.out.txt`) |

Flag: `../e20-h3/bin/results/goldens_h3.json` (the reading's own 480 vectors) is **not byte-reproducible** with this Python (3.14.2): `python -B h3_goldens.py 40` is deterministic here but differs from the stored file from
the 42nd vector on (291 of 480 vectors differ, the first 40 are identical; the generator of the structured worlds draws in an order that moved). Nothing depends on it: the plan, and T1, use the critic's file (identical), and
the real code against the models is clean on fresh vectors. Not a stop (the oracle itself is intact: every key below is identical).

## (b) The keys of the world, replayed

`replay/` holds the replay of every key (identical to its stored twin, `cmp`); `replay_pf/` the same scenarios with the **per-frame step** of the generator (section (c)); `explode_in_hands.txt` and `k_throw_explode.txt`
are the two files the plan names.

| key | stored twin | script | replay | used by |
|---|---|---|---|---|
| `bomb_timeline` | `../e20-h3/critic/results/bomb_timeline.txt` | `critic/run/scen_bomb1.py` | **identical** | (w1) |
| `bomb_limit3` | `../e20-h3/critic/results/bomb_limit3.txt` | `scen_limit3.py` | **identical** | (w2) |
| `throw_dirs` | `../e20-h3/critic/results/throw_dirs.txt` | `scen_throwdirs.py` | **identical** | (w3) |
| `water` | `../e20-h3/critic/results/water.txt` | `scen_water.py` | **identical** | (w6b) |
| `hurt_circle` | `../e20-h3/critic/results/hurt_circle.txt` | `scen_hurt_circle.py` | **identical** | (w8) in part |
| `hero_hit` | `../e20-h3/critic/results/hero_hit.txt` | `scen_herohit.py` | **identical** | (w1) in part |
| `bomb_wall`, `bomb_wall2`, `bomb_locked`, `bomb_air_throw` | `../e20-h3/bin/results/scenarios/` and `../e20-a362-disc/discovery/out/bin_*.replay.txt` | `bin/run/sc_misc.py wall`, `wall2`, `locked`, `air_throw` | **identical** | (w4), (w5), (w3) in the air |
| `bomb_water`, `bomb_thrown_in_water` | `bomb_water.txt`, `bomb_thrown_in_water.txt` | `sc_misc.py water`, `thrown_in_water` | **identical** | (w6a), (w6b) |
| `bomb_throw_4dirs`, `bomb_limit3`, `bomb_fuse`, `bomb_chain`, `bomb_grass`, `bomb_air_start`, `bomb_carry_actions`, `bomb_death`, `bomb_beans_relift` | same folder | `sc_throw.py 0x23`, `sc_misc.py <name>` | **identical** (all) | (w3), (w2), (w1), (w7) family |
| `bomb_hurt_while_carrying_bomb` (w11) | `../e20-h3/bin/results/scenarios/bomb_hurt_while_carrying_bomb.txt` | `bin/run/sc_hurt.py 222` (the default argument) | **identical** | (w11) |
| **(w8)** `explode_in_hands.txt` | `../e20-a362-disc/discovery/out/k_w8_explode_in_hands.txt`, `critic/out/crit_w8.txt` | `k_w8_explode_in_hands.py` (a copy of `scen_hurt_circle.py` without any setting, prolonged to tick 340) | **identical** to both | (w8) |
| **(b2)** `k_throw_explode.txt` | `../e20-a362-disc/discovery/out/k_throw_explode.txt`, `critic/out/crit_throw_explode.txt` | `k_throw_explode.py` | **identical** to both | (w10) |
| the thresholds of the hero in the zone | `../e20-a362-disc/critic/out/crit_hero_thresholds.txt` | `c_zone_hero.py` | **identical** (`replay/hero_thresholds.txt`) | (w10) bounds |
| `k_limit3_hero` | `../e20-a362-disc/discovery/out/k_limit3_hero.txt` | `k_limit3_hero.py` | **identical** | (w2) with the hero in the zone |

Content of the keys that T2 compares (read in the files, tick = tick of the scenario, Circle at tick 1):

- **(w1) `bomb_timeline`**: Circle at tick 1 (hero state 5, sound 44, `+0x274` = 240 at tick 1, animation 0 and Z 0 then 1.0 at tick 2); the bomb carried at Z 32 from tick 37 (hero state 12); sparks at the ticks **187, 195, 203, 211,
  219, 227, 235** (effect 9; the fuse counts 56, 48 ... 8); status 3 at **243** (the link cleared, the hero state 12 held to 243); animation 1 and sound 46 at **244**; the box of the explosion hits the hero at **260**
  (three effects 9 at 260: the hit sparks of the wound, three draws each; the hero state `0x31` = 49 and sound 23 at **261**, state `0x1C` = 28 at 265, state 0 at 276); end of the animation (`+0xAC` = 1) at **295**, status 4 at **296**, the slot free at
  **297** (T+296 with T = 1). The hero is hurt in this key (10 -> 5, x 100 -> 103 at 261 then 133.09 at 276: a recoil of 33.09 px, `hero_hit.txt`).
- **(w2) `bomb_limit3`**: three accepted starts (sound 44 at ticks 1, 71, 141, one bomb more each time), the fourth refused at 211 (sound 3, the hero keeps state 0, no object in use) and again at 281 while the first
  bomb is still in the table (status 3 from 243; the table is empty by 323); a start accepted again at 351 with all the slots free. Bombs explode in the order of their births (sound 46 at 244, then twice at 262). The slots are all free by 323.
- **(w3) `throw_dirs`**: the object at its first Z = 0 is 34.50 px east and west of the hero, 23.00 px north and south (hero at 101.8 / 98.2 / 98.8 / 101.2), identical for the bomb (`0x23`) and the seeds (`0x1F`). In the air
  (`bomb_air_throw`) sound 10 at the start then 24 at the landing 37 ticks later.
- **(w4) `bomb_wall`, `bomb_wall2`**: `bomb_wall`: the unique bounce (`+0x278` = 1) at tick 51 with an effect 9 and the half turn, the landing at 74 to 75 with sound 24; `bomb_wall2`: a wall at the column 10 stops the bomb in the air (x 229, ForceAdjusted at the landing, the target animation 0 at 73 and **no sound 24**), a wall at the column 11 lets it land at x 240.6 with sound 24 at 73.
- **(w5) `bomb_locked`**: the lock of the control while the bomb is carried: tick 61 the bomb in status 4 with the effect 12 (id 12 at the bomb's position, Z 32), gone at 62; no explosion.
- **(w6a) `bomb_water`**: carried into the deep water: tick 80 the effect 12 and the bomb in status 4, gone at 81 (sound 22 of the hero). **(w6b) `bomb_thrown_in_water` / `water`**: thrown into slope 4: the effect 6 (mode 1, attached) at the
  tick after the landing, status 4, gone the tick after.
- **(w8) `explode_in_hands.txt`** (printed only at the ticks where something of the hero or the bombs changes): bomb 1 status 3 at 243, animation 1 + sound 46 at 244; the hero hurt at **261** (hp 10 -> 5, state `0x31`, sound 23, x 100 -> 103);
  Circle at 262 (one tick after the wound): bomb 2 born (status 1, sound 44, link `0x80128258`, item `0x23`, the hero in state 5), status 3 at 264 (the fuse already over), animation 1 + sound 46 at 265, **no second hurt** (hp stays 5, the hurt counter non zero);
  bomb 1's end flag at 295, status 4 at 296, gone at 297 (the hero returns to state `0x0C` at 298, the link `0x80128258` and the item `0x23` held); bomb 2's end flag at **316**, status 4 at **317** with the link cleared (the item `0x23` still held), and at **318** the item is 0, the hero in state 0 and the entity gone (`DestroyEntity(E, -1)`).
- **(w9)**: the Start menu refused from the start to T+243, open after: read in the h0a keys (`bomb_timeline` item `0x23` held to tick 243, then 0).
- **(w10) `k_throw_explode.txt`**: the hero stays where he threw: RIGHT bomb at rest 35.25 px east (hero untouched), LEFT -35.25, UP -23.50 (untouched), **DOWN +23.50: first hurt at tick 273, hp 5, state `0x31`**. Thresholds of the zone
  (`hero_thresholds.txt`, the critic's): east touched at 34.75 px or less (5 hp left), indemnified from 35.0; north touched at 22.0, safe from 23.0; south touched up to 23.5, safe from 24.0; west safe from 34.0. The resting
  offsets of the throw (35.25 / 23.50) are 0.25 to 0.5 px from the thresholds: the south throw is in the zone by half a pixel (Q-H3B-1, default A, as the original).
- **(w11) `bomb_hurt_while_carrying_bomb`**: at tick **264** the hero carrying bomb 2 is hurt (hp 10 -> 5, state `0x31`, recoil 3 px a tick) and the bomb is dropped **in the same tick** (animation 2, link `c28` = 0); the object in use freed at **265**
  (`inuse` 0x23 -> 0); the bomb lands at tick **286** (x 258.9, Z 0.0) and rests at **287** (animation 0, x 259.6 = 35.2 px east of the hero's carrying position 224.4), not broken; its fuse goes on: **status 3 at tick 332**, animation 1 at 333, status 4 at 385.
  Bomb 1 (the first throw, 241.3) explodes at 246 without touching the hero (221 to 225 px away).

**The per-frame step** (rule of section 1.20, block, point 3: `s <- s * 0x7D2B89DD + 0xE06A02E7` after each logic tick, the end of the frame function `0x8002BD0C-0x8002BD34`): the oracle's `W.tick` does not apply it. `scripts/pf_run.py` (a wrapper
that applies it to `0x80098708` AFTER each tick of a scenario script, never inside) reran every scenario: **only `bomb_timeline` differs from the plain run, in one line** (tick 260, the values of the two last of the three hit sparks (effects 9)
of the wound: `2121329109, 87438` / `3645226074, 49443` become `2639079770, 38194` / `260487795, 79170`; these are drawn values of the hero's wound, not of the C62 sparks). The other 19 scenarios print nothing that depends on a
draw (`replay_pf/` equals `replay/` for them). The sparks of the carried bomb carry their drawn values in the slot of the effect, which the journal does not print: `scripts/k_h3b_sparks.py` prints them (section (c)).

## (c) The policy of the sparks

**What the binary does** (C62, `0x8007A978`, read in the model `h3_models.py c62`, 186/186 instructions covered by the vectors): while the fuse `v` is under `0x3C` and a multiple of 8 (v = 56, 48, ... 8: **seven sparks**, ticks 187 to 235 of the key), the
bomb calls `CreateEffectEntity(0, 9, 0, x, y, z + 0x80000)`; **only if the call hands back an effect** it draws twice: `st1 = step(rng)`, `st2 = step(st1)` (the generator advances two steps), the force X of the effect (slot `+0x54`) is
`hi(st1 * 0x30001) + 0xFFFE8000` and the force Y (`+0x58`) is `hi(st2 * 0x20001) + 0xFFFF0000` (32-bit words, `hi` = the high word of the 64-bit product). Without an effect: no draw. `sparks/spark_values_plain.txt` and `sparks/spark_values_pf.txt`
(`scripts/k_h3b_sparks.py`, plain and with the per-frame step) print, for each of the seven sparks, the generator after the tick, the arguments of the call and the words of the slot; `scripts/check_sparks.py` recomputes the forces from the generator
with the formula above: **7 sparks checked, 0 differences, plain and with the step** (`sparks/check_sparks_*.txt`). The oracle's stub always hands back a slot: in the keys, "obtained" is always true.

**What the DLL's `EffectStage` says** (probe `probe/probe_effectstage.cs.txt`, output `probe/probe_effectstage.txt`, both on a default stage and on the real bank of the map 14):
- `EffectStage.CreateNow(false, 9, 0, x, y, z)` (the immediate creator of E20.f1b, `Alundra/Rendering/Effects/EffectStage.cs`) returns the slot AT ONCE, and **null only when the reservoir's 128 slots are all active** (`CreateNow` number 129 returned
  null after 128 successes; a freed slot gives room again, and slot 0 is the first free one). A spark of the real bank lives 27 ticks (2 with the empty bank), so three bombs with their seven sparks never fill the reservoir by themselves.
- `EffectStage.Emit(operation)` (what `BallAi` and `TileEffect` use) only QUEUES the operation, applied when the stage runs its frame: its `Create` gives no answer to the caller (on a full reservoir it returns null silently inside the operation), so **the "draw twice
  only if the effect is obtained" condition cannot be expressed with `Emit`**.
- With no stage (`world.EffectStage` null, benches without one) there is no effect and so no draw; the benches of T2 (`ArcRun`, `HitArena`) have a stage.

**The policy for T1**: C62 creates its sparks with `CreateNow` and draws twice (X then Y, the formula above, `GameRandom` as the other native handlers) exactly when the call returns a slot; it writes the two forces into the returned slot (`ForceX`, `ForceY`).
`NativeEffects` keeps its `Emit` creators for the others. The consequence to note for T1: the two draws happen at the entity's call, in the order of the entity pass, while the cut of the grass of a bomb that explodes in the same tick is queued (`Emit`) and drawn when the stage
applies it; in the keys the sparks (ticks 187 to 235 of a bomb) and the cuts and hit sparks (from 259) never share a tick, and (w1) to (w11) have no bush under a bomb in flight, so the order between the two kinds is not exercised; if T2 finds a key that does,
it is a stop, not a re-fit. The key to compare the draws is (w1) with the per-frame step (the forces above, `sparks/spark_values_pf.txt`).

## (d) The predicted list, recounted at the HEAD of departure (`dd3adb1d`)

**The registry of production holds 26 keys at the HEAD** (`probe/registry_counts_head.txt`: `Count = 26`; (C, 62) absent for the type 2, (D, 30) and (E, 12) present). It holds 27 after T3 if C62 is registered by `BombAi.Register`.

**The five sums of the registry** (`NativeSpriteRegistryTests.cs:60`, `LiftableAiRegistrationTests.cs:85`, `ChargedArrowAiTests.cs:247`, `WaveSwordLifeTests.cs:187`, `BallFlamesTests.cs:246`, with their secondary sums `sum` registries at
`LiftableAiRegistrationTests.cs:78`, `ChargedArrowAiTests.cs:238`, `BallFlamesTests.cs:229`): **every one writes `BombAi.Keys.Count`** (and calls `BombAi.Register(sum)`), not a literal. **Choice written here**: C62 joins `BombAi.Keys` and
`BombAi.Register` (the plan puts C62 in `BombAi.cs`; D-H-6). Then the five sums follow by themselves and **stay green with no edit** (27 = 26 + 1); the plan's "+1 clé" costs no change in those five tests. Had C62 been registered by a separate
`Register`, the five sums and three `sum` registries would each have to gain a term: that is the more expensive design and is not taken.

The tests that move with it (the registry keys of the bomb are pinned by name):

| # | test | file : line (HEAD) | what moves at T1 or T3 |
|---|---|---|---|
| 1 | `B4_TheBombRegistersD30AndE12_ForTheType2Alone_NoOtherKeyAndNoOtherType` | `Alundra.Tests/Ai/Items/BombAiTests.cs:312-313` | `Assert.Equal(2, registry.Count)` becomes 3 and `BombAi.Keys` gains `(ProgramCTick, 62)`; the name and the "neighbour" asserts (a code 62 in another slot absent) are rewritten; (C, 62) present for the type 2 only |
| 2 | `B4_TheBombAndTheLidShareTheSlotE_WithoutAnyConflict_AndASecondRegistrationOfTheBombThrows` | `BombAiTests.cs:363,365` | `Assert.Equal(3, registry.Count)` twice becomes 4 (three keys of the bomb + the lid's) |
| 3 | `B4_TheRegisteredHandlersAreTheBombs_D30SetsTheStatus3_E12AsksTheAnimation1_AndTheDisposableTakesTheKeysOff` | `BombAiTests.cs:333` | no value moves (it ends on `registry.Count == 0`); it may gain the `(C, 62)` row |
| 4 | the seven `BombRun` constructions of `BombWorldTests` (the bench of h3a, W1 to W6) | `Alundra.Tests/Ai/Items/BombRun.cs:113` and `BombWorldTests.cs` | **a bench change, not an assertion change**: the bombs of `BombRun` are born by the interface with `+0x274` = 0 and observe production (the doc says "no hands and no C62"). Once (C, 62) is registered the first tick of C62 sees `v == 0` and puts them in status 3 at tick 1, not at the harness' poke at tick 5: W1 to W6 go red. `BombRun` must pose the fuse high after the birth, as the oracle's scripts do (`m.poke(b + 0x274, 240)`, the fuse "kept high"), through the four bytes of `EntityScriptProxy.Bytes`, so the assertions of W1 to W6 do not change |
| 5 | `LogicalZWorldTests.Z4_AnImpulseOf1024_LandsAtTheTickOfTheOracleFlatOver16PxAndOver32Px_...` | `Alundra.Tests/Entities/LogicalZWorldTests.cs:272` (the bomb born at `:294`, animation 2 at `:295`) | the bomb born by the interface with the fuse 0: with C62 it goes to status 3 at the first tick and E12 puts the animation 1 at the second |
| 6 | `LogicalZWorldTests.Z7_TheStepHeight_IsZeroInTheAir_...` | `LogicalZWorldTests.cs:397` (the bomb born at `:401`) | same |
| 7 | `HeroItemUseTests.A4_TheStubOfAnItemAnotherSliceCarries_StartsNothing_PlaysNoSound_AndJournalsTheCall_TheSeamedStartDoesPutTheItemInUse` | `Alundra.Tests/Hero/HeroItemUseTests.cs:310-329` | it names the item `0x23` as THE stub (`"start 0x23"` journalled, no item in use): at T3, when the entries `0x23` become real, the bomb is no longer a stub. The test is rewritten with an item that is still a stub (the seeds `0x1F` of h3c, or the cape `0x20`: the same assertions, `"start 0x1F"`) |
| 8 | `TestSavesTests`: `TheThirtyThreePresets_AreListed_InTheOrderOfTheTable` (`:134`, becomes the thirty-four), `ABuiltPreset_IsAcceptedByTheRealRules` (`:225`), `TwoConstructions_AreEqual_ByteForByte` (`:234`), `TheSlotOfAPreset_IsTestDashItsName_AndFollowsTheSlotNameRule` (`:284`) (one row more each: the three theories of `EveryPresetName`), `TheOtherPresets_LeaveTheDeathCounterAtZero` (`:938`, one iteration more); the constant list `AllPresetNames` (`:118`) gains the name | `Alundra.Tests/SaveGames/TestSavesTests.cs` | the preset of the recipe (below) |

**Choice for Z4 and Z7** (the plan's "le choix écrit au README"): the two tests keep the bomb as the entity with gravity and the oracle's Z values untouched; **they pose the fuse of the bomb they bear high (240) right after `Born(...)`**, through the
same helper as `BombRun` (so one seam serves the three), which keeps C62 out of their series: with the fuse at 240 the C62 of the bomb has no spark (v >= 0x3C), no status change, and (Z4: the bomb on the animation 2 in flight) its free branch only reads; at the
landing it writes the sound 24 and the target animation 0 at the tick that follows the landing, after the last sample of Z4 (`landing + 1` ticks, the series are read per tick). Fallback if T1's measure shows otherwise (the series moves, which would be a stop
of the test, not of the slice): the same tests on a gravity entity with no native handler, or the bomb's `SpriteType` changed after the birth to a type outside the registry (the motif of `LiftMap72ArcTests` "derived on a type that stays outside the set"): the gravity is the prefab's, taken at the birth.

**Not moved** (checked): `OlenBombLidArcTests` (section (e)), `BombLidAiTests` (`Keys` of the lid only), `BombWorldTests` assertions, the arcs that hold no sprite 2, `WaterDropAiRegistrationTests`/`PillarAiRegistrationTests`/`LiftableAiTests` (their own class counts). The hero tests that press Circle
with the item `0x23` (`HeroItemUseTests`) all pose the seams (`Seams(rig, ...)`) except `A4_TheStub...` (row 7) and the two table tests `A7_TheTableHasTheFiftyOneStates...`, `A9_TheHeroOfANewMap...` that do not use it. Any other existing test that changes is a STOP.

## (e) The bomb of Olen (the map 362)

- `scan_c62_placements.py` (`scan_c62_placements.out.txt`): over the 483 maps, **exactly one** placement of a sprite whose header has `ProgramTick = 62` (only the global sprite 2 has it): the **map 362, record 5**, with a map program of its own in the
  slot C (`EventCodesC_TickIndex` = 133). Not on the 30 maps of the story chain list. No other map places a bomb.
- The DLL (`probe/probe_362_record5.cs.txt`, output `probe/probe_362_record5.txt`): the entity born from the record 5 (after the talk to Olen, 441 frames into the probe) has the sprite type 2, `ProgramIndexes[A..F]` = `[0,0,133,0,0,0]` and
  `SpriteProgramIndexes[A..F]` = `[0,0,62,30,12,0]`: the sprite header gives it the native codes C62, D30, E12 and the map gives it a program in the slot C (index 133, `133 & 0x7f` = 5). `EventProgramRunner.RunSpriteEvent` consults the registry only for a slot whose
  `ProgramIndexes[slot] & 0x7f` is 0: **C62 is never reached for it** (D30 and E12 are, since h3a). The registry is consulted by the sprite's code AND the empty map slot, so the code 62 of its header does not make it a native C62 entity: the stop "a native C 62 for this entity"
  does not fire. Predicted change for the arc: **neutral**. The measure (`OlenBombLidArcTests` 47 cases) is taken at T3 with the real registration.

## (f) The sounds of the effects

The oracle journals the sounds CALLED BY THE CODE (`0x800490FC`) and the creation of the effects (arguments), without running their programs. The effects exported to `alundra-project/Data/effects/effects-global.json` have no sound (the keys of the file, checked: `Abr`,
`Animations`, `C`, `End`, `Frames`, `H`, `Idsv`, `ImageSets`, `Images`, `Semi`, `U`, `V`, `W`; no sound field), so the DLL plays none for the effects 2, 6, 8, 9, 12 either. The world tests of T2 compare the sounds of the code alone: in the keys 44 (start), 3 (refusal),
26 and 10 / 11 (hero animations: throw, air start, landing of the jump), 24 = `0x18` (the landing of the bomb), 46 (the explosion animation), 23 (the hero hurt), 22 (the hero in the water), 31 (the grass, `BombWorldTests`); the zeros (`sound 0`) are filtered, as every
key does. Q-H3-8 stays open (owner: the port of the effect interpreter, out of E20.h).

## The preset of the recipe

**`bombe-m14`**, slot `test-bombe-m14` (14 characters, the slot rule `[a-z0-9_-]{1,32}` holds), on the cell of `herbe-m14` (map 14, tile **(31, 43, 1)**): the cell already validated in game by E20.e (walkable, the hero rests, the 16 bushes of the cells 26 to 29, rows 40 to 44, are a few cells to the west, row 42 is a wall to the east), so the recipe can lift the bomb, walk, jump, throw it west toward the bushes (34.5 px, the explosion can cut them) and see three bombs at most. The shape: `new Preset("bombe-m14", "test-bombe-m14", 14, 31, 43, 1, [], [], [], Loadout(), Weapon: 1, Item: 35)`
(`Loadout()` carries the bomb count, 35 = `0x23`; `Item: 35` equips it on the Circle; the life is the kit's 10 of 10, since the explosion hurts: 5 hearts). It goes last of the table, after `gouttes-m92`.

## Deviations from the plan and flags for the main session

1. **"Les cinq sommes du registre (+1 clé)"**: no edit if C62 joins `BombAi.Keys` (section (d)); the moved tests of the registry are the three of `BombAiTests` (rows 1 to 3).
2. **The bench `BombRun` of h3a must pose the fuse** (row 4): not in the plan's list; a bench change, no assertion; without it W1 to W6 go red at T3. **`HeroItemUseTests.A4_TheStub...`** (row 7) is another test the plan does not name that moves at T3.
3. Z4 and Z7: the choice is written (poke the fuse), the fallback too.
4. The reading's `goldens_h3.json` is not reproducible byte for byte (section (a)); the critic's file is.
5. The spark policy requires `EffectStage.CreateNow` (immediate), not `Emit`.
6. The `CasaEngineMonogame` and `alundra-datas-analyser` entries of `git status` are the author's earlier local modifications; untouched.

## Files

`scripts/` (`pf_run.py`, `k_h3b_sparks.py`, `check_sparks.py`, `scan_c62_placements.py`), `replay/` and `replay_pf/` (the keys), `explode_in_hands.txt`, `k_throw_explode.txt`, `sparks/`, `vectors/`, `probe/` (the three throwaway probes and their outputs),
`scan_c62_placements.out.txt`.

Reproduce (from a COPY of `../e20-h3/critic/run` with a `results/` folder beside it and the `k_*.py` of `../e20-a362-disc/critic/src/` added; a copy of `../e20-h3/bin/run` with its own `results/` for the reading's scripts): `python -B my_goldens.py 25 && cmp ../results/goldens_critic.json <the stored file>`;
`python -B my_goldcheck2.py`; `python -B scen_bomb1.py`, `scen_limit3.py`, `scen_throwdirs.py`, `scen_water.py`, `scen_hurt_circle.py`, `scen_herohit.py`, `k_w8_explode_in_hands.py`, `k_throw_explode.py`, `k_limit3_hero.py`, `c_zone_hero.py`;
`python -B sc_misc.py <wall|wall2|locked|air_throw|water|thrown_in_water|limit3|fuse|chain|grass|air_start|carry_actions|death|relift>`, `sc_hurt.py 222`, `sc_throw.py 0x23`; with the step: `python -B pf_run.py <script> [args]`;
`python -B pf_run.py k_h3b_sparks.py > spark_values_pf.txt && python -B check_sparks.py spark_values_pf.txt pf`; `python -I -B scan_c62_placements.py`; the vectors: `python -B h3_vec.py 4000 100 <function>` and `python -B my_diff.py 6000 100 <functions>`.
