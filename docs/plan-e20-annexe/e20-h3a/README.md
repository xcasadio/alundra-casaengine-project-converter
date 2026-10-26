# E20.h3a T0: the annex, the oracle keys, the census and the predicted list (2026-10-26)

Written by T0 of E20.h3a (`docs/plan-e20-heros.md` section 1.30.2, revision 3), branch `chantier/theme2-attaques-objets`, HEAD of departure `af1ec8e9`. **No code of `Alundra/` or `Alundra.Tests/`
changed**: the only throwaway code (two probe tests, never committed, texts kept in `probe/`) was removed before the commit. The scripts read the binary `ALUN_CD.EXE` (France) at the absolute
path they name (outside the repository), the data at `data-extracted/data/`, and write next to themselves: rerun them from a COPY, never in place. `SHA1SUMS.txt` lists every file here.

Method of every replay below: `docs/plan-e20-annexe/e20-h3/critic/run/` (the oracle: MIPS interpreter, real `ALUN_CD.EXE` code) copied to a scratch folder with `results/` beside it; for the
scenario scripts, the copy also holds the `k_*.py` of `../e20-a362-disc/critic/src/` and `../e20-a362-disc/critic/run/runpy.py`.

## (a) The vectors and the field table

| function | address | vectors | result |
|---|---|---|---|
| D30, E12 (and the 10 other functions of the critic) | `0x8007E79C`, `0x8007F27C` | `../e20-h3/critic/results/goldens_critic.json`: 300 vectors, 25 per function, all words read and written | `python -B my_goldens.py 25` from a copy regenerates the file **byte for byte** (`cmp`); `python -B my_goldcheck2.py` (empty memory, any read of an unlisted word is an error): `vectors 300 differences 0 reads of unlisted words: 0` (`replay_goldens_critic.out.txt`) |
| **E19** (the lid, `0x8007F690` to `0x8007F6C4`) | `0x8007F690` | **25 new vectors**, produced on the real code by `my_goldens_e19.py` (same method as `critic/run/my_goldens.py`: every data word read and written recorded, the journal of the calls), seed `20261026`, 20 distinct entities (a random slot with probability 8/9, the hero's otherwise) | `goldens_e19.json`; `replay_e19.py` (a model written for the slice: sound `0xE6`, then `DestroyEntity(E, -1)`; no word read, none written): `vectors 25 differences 0 words read by the real code 0` (`replay_e19.out.txt`). Regenerating the file gives the same bytes. |

What E19 does, read in `critic/lst` and re-disassembled (`mydis.py 0x8007F690`): `PlaySoundEffect(0xE6)`, then `DestroyEntity(E, -1)` (`0x8003A59C`, which gives the break effect of the sprite header,
byte `+0x1E`: 11 for the sprites 241 to 243). It reads no data word of the entity: the vectors prove it (`reads` is empty in all 25).

### Table of the fields (binary offset -> member of the DLL)

| offset | role in D30, E12, E19 | member of `EntityScriptProxy` | note |
|---|---|---|---|
| `+0x10` | status (D30 writes 3) | `Status` (`EntityStatus`: `Normal` 2, `Deactivated` 3, `FlagToDestroy` 4, `Destroyed` 0) | |
| `+0x88` | target animation (E12 reads 1, writes 1) | `TargetAnimationId` (`uint`) | |
| `+0xAC` | end of the animation (E12 reads 1) | `ForceResetAnimationFlag` | named so in `BallAi`, `ArrowAi`, `LiftableAi` (the same `+0xAC == 1` test) |
| `+0x6C` | flags (E12: `\| 0x40`, `& ~0x30`) | `Flags` (`uint`; `EntityFlags.DeactivateOnAnimationEnd` = `0x40`, `DeactivateOnImpact` = `0x10`, `DeactivateOnHit` = `0x20`) | E12 sets `0x40` and clears `0x10 \| 0x20` |
| `+0x184` | "hole" (E12: `& 4` -> destroy) | `CombinedVramFlagsAND` | the doc of `LiftableAi` calls the same word "the four corners on the ground property 4"; filled for the marked entities only today (`UpdateVramFlags`, E20.g2b2): **to check at T1 for a bomb born by the interface** |
| `+0x224` | the entity that touches (D30 reads it) | `TouchingEntity` (`Entity?`, then `.GameplayProxy as EntityScriptProxy`) | |
| `+0x1C8` | its attack (D30 reads the byte at the pointer, low nibble) | `CurrentAttack` (`BalanceAttack?`, `.Attribute`) | the binary does not test the frame box nor a zero byte; `HitOpcodes.AttacksWithNibble` does (`HasFrameBox`, `Attribute != 0`): D30 must read `Attribute & 0xF` itself, as the model does (4, 6, 10 -> status 3) |
| call | E12 calls the grass cut (`CheckAndTriggerTileEffect`, `0x8003A7B0`) at every call | `TileEffect.Trigger(EntityScriptProxy)` (`Alundra/Cells/TileEffect.cs:66`) | the form of the call is the one of D-H-6 and of E20.e |
| `0x8003A59C` | `DestroyEntity(E, -1)` | `WorldProxy`'s `DestroyEntity(e, -1)` (E14.a1b) | |

## (b) The census of E19

`scan_e19.py` (a copy of `../e20-a362-disc/discovery/scan_e19.py`) rerun: `scan_e19.out.txt` is **byte for byte** the file of the reading (`../e20-a362-disc/discovery/out/scan_e19.txt`): 23 records with
`ProgramDeactivate = 19`: the 3 global lids (sprite 241 break effect 11, 242 effect 11, 243 effect 11) and 20 records of map pages (sprite 58 or 59, effect 10, `Collidable` alone).
`scan_e19_placements.py` / `scan_e19_placements.out.txt` (new) counts the PLACEMENTS (the entity records of the 483 maps; a placement names an index of the `SpriteTable` of its map, an entry other
than -1 means the page of the map, whose sprite type is `index | 0x100`, `EntitySpawnFactory.cs:541`): 76 placements on 32 maps.

- **Registered by h3a (the types 241 to 243, global page)**: 30 placements on 14 maps (6, 14, 61, 63, 64, 67, 80, 85, 90, 104, 151, 362, 382, 429); the map 362 holds one (the lid, record 0, type 243, no program of its own in the slot E).
- **Another user, named here, out of the slice**: the **types 314 and 315** (the map-page sprites 58 and 59, `Collidable` walls with the break effect 10), 46 placements on 19 maps (2, 7, 17, 33, 47, 62,
  67, 74, 97, 107, 113, 119, 120, 122, 126, 158, 159, 426, 439). They run the same native E19 in the original; the DLL stays as it is
  (they are not registered, T1 registers 241 to 243 only, as the plan says).
- Placements with an own map program in the slot E (`EventCodesE_DeactivateIndex != 0`): 16 (the maps 90, 151, 429; the map script, not the native function, serves them).
- **None of the 30 maps of the story chain places a sprite whose header has E = 19** (`on the 30 chain maps: {}`): the stop "E19 serves a sprite of the chain outside the lids" does not fire.

## (c) The keys of the tests of the world

The DLL bench of the tests (w): `HitArena` (the real world of the map 14, the hero at (100, 100)) + `SyntheticField` (the synthetic field of the oracle) + every entity of the map but the hero removed, as
`BallRun` and `ArrowRun` do; (w5) is the bench of A362. Convention of the keys: `k+N` is the tick N after the tick K at the end of which the harness puts the bomb in status 3 (K = 5).

| key | file | script and arguments | produced | hero |
|---|---|---|---|---|
| **(w1)** a bomb posed, the hero far | `../e20-a362-disc/discovery/out/k_w1_away.txt`, replayed `replay/k_w1_away.txt` | `k_w1_posed.py 200 200 5 90` (bomb at (300, 300), hero (100, 100)) | replayed from a copy: **identical** (`cmp`) | 282.8 px |
| **(w2)** the zone against the record 335 | `zone_335.txt`, `zone_335_grid.txt` | `scen_zone335.py scan` and `scen_zone335.py grid` (a copy of `critic/run/scen_zone.py`; target = the page record 79 of `data/map_14.json`, type 79 + 0x100 = 335, Beannoid, header size (20, 14, 8), offset (-10, -7, 0); real `SpawnWarpEntity` with the map page) | new | (100, 100), 240 px or more from the target |
| **(w3)** the chain, bombs posed, the hero far | `../e20-a362-disc/discovery/out/k_w3_chain_away.txt`, replayed `replay/k_w3_chain_away.txt` | `k_w3_chain.py 200 200 5 90` | replayed: **identical** | 282.8 px |
| **(w4)** the grass, posed | `grass_placed_pf.txt` | `sc_grass_placed_pf.py` (a copy of `bin/run/sc_misc.py grass`, defaults `252 200 5 60`), run from a copy of `../e20-h3/bin/run` | new | (100, 100), 182 px from the bomb, 110 px from the nearest bush cell |
| **(w5)** the lid | `../e20-a362-disc/discovery/out/k_w5_lid.txt`, replayed `replay/k_w5_lid.txt` | `k_w5_lid.py 5 20` | replayed: **identical** | 282.8 px (lid at (300, 300)) |
| **(w6)** the hero in the zone | `../e20-a362-disc/discovery/out/k_w1_17east.txt`, replayed `replay/k_w1_17east.txt` | `k_w1_posed.py 17 0 5 90` (bomb 17 px east of the hero) | replayed: **identical** | 17 px, in the zone |
| generator probe (f) | `rng_probe_pf.txt` | `sc_grass_placed_pf.py 252 200 5 60 nograss` | new | as (w4), without bush |

Content of the keys (read in the files):

- **(w1)**: status 3 at k+0; animation 1 at k+1 with the sound 46; frame box from k+17 to k+31 (gone at k+32); end of the animation (`+0xAC` = 1) at k+52; status 4 at k+53; slot free at k+54. The plan's
  chronology (animation 1 at +1, boxes at +17, end at +52, status 4 at +53, slot free at +54) holds exactly.
- **(w2)**: the Beannoid has 9 life; the bomb at (300, 300), status 3 at tick 4; at the centre the touch is at tick 22 (= K + 18), the life goes 9 -> 0 in **one** change (`changes` = 1; D1 sets the
  animation 5), the entity is destroyed at tick 44 to 45. The zone is a **strict rectangle**: touched for |dx| <= 33 at dy = 0 and |dy| <= 22 at dx = 0 (misses at dx = +/-34, dy = +/-23); the grid of 25 offsets
  (`zone_335_grid.txt`) confirms it at the corners: (+/-33, +/-22) touched (first tick 31), (+/-34, any), (any, +/-23) not touched (life stays 9, status 2, `changes` 0). First touch tick: 22 at the
  centre, 25 to 31 elsewhere (the frame box grows then shrinks). "Juste hors des bornes": dx = 34 or dy = 23.
- **(w3)**: bomb 1 at (300, 300), bomb 2 at (311, 300); bomb 1's hit counter is 1 at k+17 (its box meets bomb 2); bomb 2 in status 3 at k+18, animation 1 at k+19 with the
  sound 46; bomb 1 free at k+54; bomb 2 end flag at k+70, status 4 at k+71, free at k+72. The hero never touched (life 10).
- **(w4)** (new): the bomb is born at (252, 200) (the centre of the bush grid: columns 8 to 12, rows 10 to 14, cell 24 x 16 px, centre of the cell (10, 12)), the generator set to the seed `0x12345678`
  at the end of the tick K (so `rng0(k+1)` = seed), the per-frame step `s <- s * 0x7D2B89DD + 0xE06A02E7` added after `rng1` and before the next `rng0` (never in an after-tick hook). The box opens at
  k+17; **five** cells are cut: (10, 12) at **k+18** (one effect 2 at (252, 200, 0), one sound 31, 1 draw: `rng0=C666A38F rng1=6091BC5A`) and (9, 11), (9, 13), (11, 11), (11, 13) at **k+21** (four effects 2 at
  (228 or 276, 184 or 216, 0), four sounds 31, 5 draws: `rng0=A2571973 rng1=BCFF9042`); no other tick draws. The hero is never hurt; the bomb is free at k+54. Note the difference with the scenario `grass` of
  the reading (`sc_misc_grass_original.out.txt`: a thrown bomb, 8 cells cut, effects at three ticks): the posed bomb of the test is not that bomb. Two runs are byte for byte equal.
- **(w6)**: the bomb 17 px east of the hero (contact at k+17 on the hero's body, `TouchingEntity` set at k+17, counter 25); at k+18 life 10 -> 5, state `0x31`, sound 23, `DamagedTickCounter` 119;
  `0x31` to k+21, `0x1C` from k+22 to k+32, rest from k+33; total recoil -33.09375 px to the west (x 100 -> 66.906); the plan's numbers hold exactly.

**The hero of the benches (w1) to (w5) is more than 60 px from every bomb** (the distances above; the smallest is 182 px in (w4)). **The hero of (w6) is in the zone (17 px).**

### Finding: the hero of the keys has the kit of the new game, not "no armour and no inventory" (`verify_hero.py`, `verify_hero.out.txt`)

The plan (and the reading's list of risks) say that (w6) needs a hero "sans armure, sans inventaire" to get 5 life points. **The measure says the opposite.** The hero of the oracle is the hero of the new game:
the word `g_playerStats + 0xC` (the equipped armour) is 0, but the inventory holds the three items of the new-game kit, **1 (category 1), 17 (category 2 = armour) and 25 (category 0)**, and the sheet of the hero
is built from the owned armour (`PlayerManager`, step 6 of `UpdateItemEffectState`: "the owned item of the highest number whose category is 2"). Replaying (w6) with the inventory emptied
(`k_w1_posed_inv.py`, `EMPTY_INV=1`, output `k_w1_17east_emptyinv.txt`) gives **10 -> 9** (one life point), not 10 -> 5; without the env var the script is byte for byte the original key. So the key
(w6) is the key of the kit hero.

The DLL bench has the same hero: the probe of the bench (`probe/probe_generator_dll.txt`, first line) lists the items owned by the hero of `HitArena` before any `Give`: **`[1x1(cat1), 17x1(cat2), 25x1(cat0)]`**,
the same kit. So T2 must NOT empty the inventory of the bench hero (the "no armour, no inventory" of the plan would produce a loss of 1 and a red test that is the plan's fault); the expected loss of
5 is to be checked in T2 on the real bomb. The corrected wording for T2: "the hero of the new-game kit (items 1, 17, 25), exactly the hero of the oracle".

## (d) The predicted list, recounted at the HEAD of departure (`af1ec8e9`)

**The registry of production holds 18 keys at the HEAD** (`probe/registry_counts_head.txt`: `production=18 pickup=2 chest=1 charged=1 arrow=1 pillar=1 liftable=1 break=1 c4=1 ball=2`; the two
Beannoid/Muruta classes give 3 + 3, the wave sword 1: 6 + 1 + 2 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 2 = 18). After h3a it holds **21** (`BombAi.Register` gives (D, 30) and (E, 12), `BombLidAi.Register` gives (E, 19)).
`CreateForProduction` makes **twelve** `Register` calls today (`BeannoideAi`, `MurutaAi`, `WaveSwordAi`, `PickupAi`, `ChestAi`, `ChargedArrowAi`, `ArrowAi`, `PillarAi`, `LiftableAi.Register`, `.RegisterBreak`,
`.RegisterC4Production`, `BallAi`); h3a adds two.

**The pins of the registry are FIVE at the HEAD, not four** (E20.f6b added the fifth):

| # | test | file : lines (HEAD) | what moves |
|---|---|---|---|
| 1 | `ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn` | `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:58` | the sum gains `+ BombAi.Keys.Count + BombLidAi.Keys.Count` (18 -> 21) |
| 2 | `R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses` | `Alundra.Tests/Ai/Objects/LiftableAiRegistrationTests.cs:77-78` (the `sum` registry at `:63-76` gains the two `Register`) | same |
| 3 | `C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses` | `Alundra.Tests/Ai/Projectiles/ChargedArrowAiTests.cs:239-240` (the `sum` registry at `:223-236`) | same |
| 4 | `V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem` | `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs:184-191` (pure arithmetic, no `sum`) | same |
| 5 | `M3_TheProductionRegistryHoldsC60AndE9_ForTheSixBallTypesAlone_AndItsCountIsTheSumOfTheCountsOfTheClassesPlusTwoKeys` | `Alundra.Tests/Ai/Projectiles/BallFlamesTests.cs:237-239` (the `Sum()` helper at `:212-224` gains the two `Register`) | same |

Not pins (own registries, counts of their own class, unchanged): `BallAiTests` (`:532`-`:562`), `LiftableAiTests:149`, `LiftableBreakTests:184,206`, `PillarAiRegistrationTests:50-51`, `PickupAiPlacedTests:529`,
`ChargedArrowAiTests:206`, `NativeSpriteRegistryTests:253-260` (relative count). No test names `(D, 30)`, `(E, 12)` or `(E, 19)` as absent (grep at the HEAD) except the two A362 pins.

**The two pins of the arc of A362** (`Alundra.Tests/Arcs/OlenBombLidArcTests.cs`, theories at the cadences k = 1, 2, 3, so six cases):
`A9_TheLidStaysInStatus3_E19Missing` (`:557`; after h3a: the lid is destroyed with the sound `0xE6` and the break effect 11 one tick after its status 3) and
`A9_TheBombStaysInStatus3Invisible_E12Missing` (`:576`; after h3a: the bomb is destroyed at the end of its explosion, E12 finding the animation 1 finished). Their names carry "Missing" and are rewritten at
T3 (D-A362-1); the exact ticks are re-derived at T3 from the keys, not asserted here. The other cases of the arc do not move: (a8) reads the lid in status 3 at H + 1 (E19 acts at H + 2), the lid of
(n2) is immune (response `0x80`) and stays `Normal`, the hero is 81 px from the bomb.

**`LogicalZWorldTests`** (the class `AlundraLogicalZWorldTests` of the plan was renamed by the restructuring: `Alundra.Tests/Entities/LogicalZWorldTests.cs`): **Z4** is at `:272`
(`Z4_AnImpulseOf1024_LandsAtTheTickOfTheOracleFlatOver16PxAndOver32Px_OnTheSeriesOfTheCrateMinusOneRawUnit`) and **Z7** at `:397`
(`Z7_TheStepHeight_IsZeroInTheAir_TheValueOfThePrefabAtRestWithGravity_AndZeroWithoutGravity`); both are in `class LogicalZWorldTests` (`:31`), the arc `Overworld 3,1-14` with the real controller,
the bomb (sprite 2) born by the interface and put on animation 2 by the test (it keeps its own animation target; nothing writes its status 3). The native switch is on in that arc, so D30 runs on the bomb
at every tick (and E12 never: no status 3); with D30 reading `TouchingEntity` null-safely the prediction is **neutral**. The measure itself needs the code of T1/T3 and is taken there (T0 cannot
register what does not exist); `ControllerlessVerticalStepTests.Z4/Z7` (`Entities/ControllerlessVerticalStepTests.cs:140,189`) are other tests with the same letters and do not use the registry.
**Hence the predicted list: the five pins of the registry (rows 1 to 5), the two pins of A362 (six cases), and nothing else.** Any other existing test that changes is a STOP.

Names for T1 (checked now so that they are not a stop later): the new folders `Alundra/Ai/Items/` and `Alundra/Ai/Props/`, the namespaces `Alundra.Ai.Items`, `Alundra.Ai.Props` and the mirror folder
`Alundra.Tests/Ai/Items/` (ADR-0080) were tried with throwaway files: `python -B tools/dll-structure/check_names.py --rules` reports 0 finding (the files were deleted, `git status` clean).

## (e) The result of A362 and its bench

`../e20-a362/results.md` (commits `1cbbf977`, `750e7b1d`, `9ca209d5`; plan `af1ec8e9`). The arc: **`Alundra.Tests.Arcs.OlenBombLidArcTests`** (`Alundra.Tests/Arcs/OlenBombLidArcTests.cs`), 46 cases at
the three cadences (47 with the content test of the preset), all green; it plays the map 362 on the real controller from the preset arrival (204, 144, 2) (D-A362-4: the hero walks up to Olen, holds Up, presses Square, B[3] then walks him 50 px
south), shares one scene per cadence (`Scene(k, flag248)`, `Play`, the `Rig` and `Timeline` nested classes, `Cache`), and has the bench of the sword `Bench(k, heroX, heroY, heroZ, direction)` (the real
map 362 without G248, the lid alone at (212, 100, 32)). The support it employs: `ArcRun` and `ArcSpec` (`Alundra.Tests/Support/`), `ArcRun.OneFrameWithTheDialogueButton(elapsed)` (gained the optional
`elapsed` in A362), `ArcRun.PlaceHero`, `EntityByRecord`, the preset `olen-m362` (`Alundra/SaveGames/TestSaves.cs`, slot `test-olen-m362`, tile (8, 9, 2), flag 248).

The measure names **two things the DLL lacks and no other**: `(E, 19)` (rows 19, the lid stays in status 3 without the sound `0xE6` and the break effect 11) and the removal of the bomb by `(E, 12)` (row 20, the bomb
stays in the world in status 3). Its deviations (the arrival and the bench of (n2) at (176, 105, 32), rows 4, 7 and 17 as inference, no assertion at A + 51 and A + 52) are about the test, not about
the production code. **The stop "the measure of A362 names another lack" does not fire.** (w5) uses this bench: a lid of the 362 whose program D puts it in status 3 (by the bomb of the scene, or by a test that
pokes the status as `k_w5_lid.py` does at the oracle level) -> the sound 230 and the effect 11 one tick after.

## (f) The probe of the generator on the bench of the tests of the world

`probe/probe_generator.cs.txt` is the text of a throwaway test (never committed) run at the HEAD of departure on the bench `HitArena` + `SyntheticField` (the bench of `BallRun`), with a bomb born by the
interface at (252, 200), no grass, no target, the generator set to `0x12345678` at the head of the tick 1, 54 frames of one tick; it prints the generator after each frame and the number of steps
(`ArrowRun.Steps`) made during it: `probe/probe_generator_dll.txt`. `probe/compare_generator_probe.py` compares it to the key `rng_probe_pf.txt` (the oracle without grass and target, the per-frame step added):
after the frame n, the DLL's state equals `rng0(k+n+1)` of the key, and its steps equal `draws + 1` = 1. **53 frames compared, 0 deviation** (`probe/probe_generator_compare.out.txt`; the 54th has no key line
because the bomb's slot is free in the key). No stop. The read point is after `ArcRun.OneFrame` at one tick per frame, as the plan's point 3 (revision 4) says.

## Deviations from the plan and flags for the main session

1. **The hero of (w6) and of the benches is the kit hero, not "without armour nor inventory"** (section (c), finding): the plan's wording, taken literally, makes (w6) red by one life point. The fix is in T2's
   setup text, not in the key.
2. The plan says "the four pins of the registry"; there are **five** (`BallFlamesTests` is the fifth), as the task statement already said. Sums recounted: 18 keys at the HEAD, 21 after.
3. The name `AlundraLogicalZWorldTests` is `LogicalZWorldTests` (restructuring); Z4 at `:272` and Z7 at `:397` as the plan says.
4. The census of the placements (new): the types 314 and 315 (map pages) are the "other user" of E19 (19 maps, 46 placements), and the maps 90, 151 and 429 place lids with a map program of their own in the slot E.
5. (w4) is not the scenario of the reading (no hero, no throw): its numbers (5 cells cut, not 8) are the posed bomb's. The tick of the status 3 is K = 5, the generator is posed at the end of that tick.
6. The `CasaEngineMonogame` and `alundra-datas-analyser` entries of `git status` are the author's earlier local modifications; untouched.

## Files

`my_goldens_e19.py`, `goldens_e19.json`, `replay_e19.py`, `replay_e19.out.txt`, `replay_goldens_critic.out.txt` (a); `scan_e19.py`, `scan_e19.out.txt`, `scan_e19_placements.py`,
`scan_e19_placements.out.txt` (b); `replay/` (the four keys replayed), `scen_zone335.py`, `zone_335.txt`, `zone_335_grid.txt`, `sc_grass_placed_pf.py`, `grass_placed_pf.txt`, `rng_probe_pf.txt`,
`sc_misc_grass_original.out.txt`, `verify_hero.py`, `verify_hero.out.txt`, `k_w1_posed_inv.py`, `k_w1_17east_emptyinv.txt` (c); `probe/` (the probe text, its output, the counts of the registry, the comparison) (d, f).

Reproduce (from a COPY of `../e20-h3/critic/run` with a `results/` folder beside it, and the files of `../e20-a362-disc/critic/src/` and `../e20-a362-disc/critic/run/runpy.py` added):
`python -B my_goldens.py 25 && cmp ../results/goldens_critic.json <the file of the annex>`; `python -B my_goldcheck2.py`; `python -B my_goldens_e19.py 25 && python -B replay_e19.py ../results/goldens_e19.json`;
`python -B runpy.py k_w1_posed.py 200 200 5 90`; `python -B runpy.py k_w1_posed.py 17 0 5 90`; `python -B runpy.py k_w3_chain.py 200 200 5 90`; `python -B runpy.py k_w5_lid.py 5 20`;
`python -B runpy.py scen_zone335.py scan` (and `grid`); `python -B runpy.py verify_hero.py`; then, from a copy of `../e20-h3/bin/run` with `runpy.py`: `python -B runpy.py sc_grass_placed_pf.py` and
`python -B runpy.py sc_grass_placed_pf.py 252 200 5 60 nograss`.
