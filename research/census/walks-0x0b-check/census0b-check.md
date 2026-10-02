# E19.d2 discovery, adversarial check of surface "census0b" (0x0B / 0x1E walks, DLL against the original)

Read-only, 2026-10-02. Parent `chantier/e19-opcodes` @ `0664e94`; engine submodule untouched (its `CasaEngine.Launcher/Program.cs`
author edit left alone). Nothing edited, staged, built or run in the repository. My scripts and outputs are in
`scratchpad/e19d2-disc/census0b-check/`. They are independent re-implementations and import nothing from `census0b/`:
- `pop.py`: population and reach;
- `mysim.py`: walk geometry, 16.16 integers;
- `compare.py`: verdicts per state;
- `flagflow.py`, `xflag.py`: flag ops on the walker;
- `oblique.py`, `origin.py`, `samestart.py`, `recount.py`, `down.py`, `pins.py`, `sample.py`, `m331.py`, `obst.py`, `lst.py`;
- `bin.py`: capstone.

The checker's tools read the census's pickles only as input: start states and verdicts.

Tags: [binary] ALUN_CD.EXE France; [decomp] analyser `PhysicsEngine.cs`; [DLL] `Alundra/Scripts`; [engine]
`CasaEngineMonogame`; [data] `alundra-project/`; [model] a static computation (mine or the census's).

## 1. Summary

The census's machinery is sound where I could re-derive it independently:
- the population;
- the reach set;
- both collision rules, state by state;
- the 0x0B / 0x1E / 0x1F / 0x28-0x2B handlers;
- the arithmetic of T-A19 and T-C61.

Three defects in the model, and a few counting errors, change the published classes. None of them changes the
recommendation (option A, entity blocking in the engine field stage). They do change the numbers and the "blocked in both"
evidence.

1. **0x62 / 0x63 on the walker are not modelled (systematic).** Both the DLL and the binary apply them:
   - [DLL] `SetEntitiesFlagsLow16` / `ClearEntitiesFlagsLow16` plus `ResyncControllerFromFlags`;
   - [binary] OR / AND of `+0x6C`.
   They change ClassA, ClassB, Gravity, NoObstacleSlide and Collidable, so the walk mask. The census tracks only 0x28-0x2B.
   Seven "blocked in both" sites have a `0x63 [0x80, 1|3|131, …]` that clears ClassA before the walk, in the same program. They
   pass in both rules:
   - 15 `@722` (story map, day-2 Muruta);
   - 17 `@3454`, 154 `@1088` and `@1415`, 393 `@190` and `@246`, 439 `@3454`.
   This also releases 16 "not reached" sites downstream of them. Four more "blocked in both" sites can pass if another
   program's flag write lands first (150 `@692`, 369 `@162`, 382 `@798` and `@934`).
2. **The ORIG rule never slides an oblique walk.** The binary's DECIDE_FINAL_OBSTACLE has slide cases for all 32 directions.
   The impact is small: 13 states, 2 sites. The class changes only for 15 `@665`.
3. **A start at (0,0)** comes from a later `0x64 [0x80,0,0,…]` (park before self-destroy), used as a start candidate:
   - 1 `@634` "blocked in original only" is an artifact;
   - 1 `@702` "blocked in both" is really "blocked in DLL only, class-op", released by FIXC.
4. **Counts.**
   - The class-op sites on map 2 are four, not five.
   - FIXC passes 12 of the 15 class-op sites, not 13. The published "13" counts 1 `@702`, which the census itself classes
     "blocked in both".
   - Option A+C adds **4 new blocks on map 102** (blocks "à pousser" after 0x2A), which the findings do not mention. FIXC
     newly blocks 9 sites: 52 `@1576`, 61 `@937`, 102 ×4, 199 `@835`, 346 `@943`, 426 `@668`.
   - The fix also cuts 3 sites the DLL reaches today: 346 ×2, 52 ×1.
   - In 3 "blocked in original only" rule rows the ORIG-blocked states are starts that only the ORIG run produces: 17 `@2234`,
     439 `@2234`, 135 `@900` (story map). That is not a same-start rule difference.

Corrected headline, all reachable:

| class | census | corrected |
|---|---|---|
| blocked in both | 55 | ≤ 47 (≤ 43 if the cross-program writes land) |
| blocked in original only | 17 | 15 |
| blocked in DLL only | 43 | 44 (class-op 16) |
| passes in both | 1430 | ≥ 1439 |
| not reached | 263 | ≤ 247 |

## 2. Facts

**C1 [model, own code] Population and reach reproduced exactly** (`pop.py`).
- 483 maps; 2341 linear sites (0x0B 1258 in 100 maps, 0x1E 1083 in 131 maps).
- 2215 root-reachable in 205 maps (0x0B 1170, 0x1E 1045): the same set of (map, pc) as the census, with 0 differences.
- All roots carry 0x80, so "0 DLL-only" holds.
- My recursive descent uses the full 0x78-0x81 family (0x7E/0x7F/0x80/0x81 returns, 0x7B/0x7C remembering pc+5, which the
  census `succ` lacks) and 0x5F = 8 [binary]. It finds no site that the linear decode misses.
- 13 linear sites are unreachable from any table entry (data or dead code): 212 ×4, 326 ×8, 424 `@1221`. All are among the
  dormant.

**C2 [binary] F1 and F3 confirmed** (`bin.py`).
- 0x0B (0x8003D468) writes `+0x88` = v1 on every call. Its key is state+8 = the code pointer; it stores +0x114/+0x118 at
  +0xC/+0x10; it returns 4 iff `r <= |dX|>>16` or `r <= |dY|>>16`, with r = v2 | v3<<8.
- 0x1E (0x8003D8D8) is the same with r = v1 | v2<<8 and returns 3. 0x1F (0x8003D974) returns 3 when 0x1E does, or when
  `+0x13C != 0`.
- 0x28/0x29/0x2A/0x2B (0x8003DC24/3C/54/6C): `Flags(+0x6C) |= 8`, `&= ~8`, `|= 1`, `&= ~1`; they return 1.
- 0x62 (0x8003F514) ORs `v2 | v3<<8` into +0x6C of every entity the search (0x8003C954, v1) finds. 0x63 (0x8003F590) ANDs it
  out.

**C3 [model, own code] The DLL rule reproduced, state by state** (`compare.py DLL`).
- 6110 blocked and 30193 pass states agree.
- 6 states differ only in naming. They are iron balls of map 48 (`@915`, `@960`) leaving the map: the census counts "left" as
  pass, which is right for the DLL, since the edge cell repeats and r is reached.
- My implementation is independent: `AlundraCellsCollisionField` sampling, CharacterController h1-then-h2 with an exclusive far
  corner, step 3. All 383 controller prefabs have `step_height` 3 [data].

**C4 [model, own code] The ORIG0 rule reproduced, state by state, with the census's own slide.**
- With a cardinal-only slide (`compare.py ORIGOBL`), 0 differences out of 36 347 positioned states.
- The census's `slide_limit` 48 never binds.

**C5 [decomp][binary] Oblique slide missing from the census's ORIG.**
- [decomp] PhysicsEngine.cs:600-810, DECIDE_FINAL_OBSTACLE, has cases for directions 1-7, 9-15, 17-23 and 25-31. They zero
  FinalForceX or FinalForceY and retry; FINAL only on the leading corner, or on the opposite-corner pair. e19a2/binary.md F13
  says these paths match the binary.
- [model] With these cases added (`compare.py ORIG0`), 13 states at 2 sites flip from blocked to pass:
  - 15 `@665`, Muruta rec0 dir 26 from (588,280) r 40: passes, ends (628.62, 281.0);
  - 61 `@1043`, hero dir 12, 12 states; class unchanged ("passes in both").
- Oblique states are rare: 40-50 per run (`oblique.py`).

**C6 [DLL][binary][data] 0x62 / 0x63 on the walker are ignored by the census** (`flagflow.py`, `pins.py`).
- 372 reachable sites run after a flag op on their logic entity in the same program:
  - ClassA in 174 states, ClassB 87, Gravity 112, NoSlide 35.
- c0b.py's `class_bits` follows only 0x28-0x2B (`("CF", actor)`), and `mover_for` takes gravity, NoObstacleSlide and
  Collidable from the header.
- [data] Examples:
  - map 15 C[6] `@656 0x63 [0x80,1,0]` before `@665`; C[7] `@716 0x63 [0x80,3,0]` before `@722`;
  - 154 C[8] `@1068 0x63 [0x80,0x83,0x20]` (ClassA, Collidable, NoObstacleSlide) before `@1088`;
  - 393 C[1] `@176 0x63 [0x80,1,0]` before `@190`.
- [model] With the walker's mask after the program's own op, the 7 sites pass in both rules (header mask 0x1040 → cleared 0x40):

  | site | start | dir | r | header mask | cleared mask |
  |---|---|---|---|---|---|
  | 15 `@722` | (444,296) | 24 | 208 | blocked at cell (18,18) | passes, (652.0, 296.0) |
  | 17 / 439 `@3454` | (108,536) | 24 | 96 | blocked at 110.0, cell (5,33) | passes, (204.0, 536.0) |
  | 154 `@1088` | (84,600) | 0 | 64 | blocked, cell (3,37) | passes, (84.0, 664.0) |
  | 154 `@1415` | (132,600) | 0 | 64 | blocked, cell (5,37) | passes, (132.0, 664.0) |
  | 393 `@190` | (1068,696) | 0 | 16 | blocked, cell (44,43) | passes, (1068.0, 712.0) |
  | 393 `@246` | (1044,696) | 0 | 16 | blocked, cell (43,43) | passes, (1044.0, 712.0) |

- Downstream "not reached (walk blocked in both)" sites that they release, 16 in all (`down.py`):

  | map | sites |
  |---|---|
  | 15 | `@753`, `@759` |
  | 17 | `@3464`, `@3474`, `@3481` |
  | 154 | `@1093`, `@1098`, `@1103`, `@1125`, `@1420`, `@1425`, `@1430`, `@1470` |
  | 439 | `@3464`, `@3474`, `@3481` |

- Cross-program writes (`xflag.py`, timing unknown) could also release four sites: 150 `@692` (300 states), 369 `@162`,
  382 `@798` and `@934`. 382 `@798` and `@934` alone have 17 sites downstream.

**C7 [model] (0,0) start artifact** (`origin.py`, `lst.py 1`).
- 1 `@634`. rec0 C[1] parks the Muruta with `@680 0x64 [0x80,0,0,0,0,0,0]` before `@688 0x2E [0x80]`. The census takes (0,0) as
  the only start of `@634`, which runs before `@680`. The DLL "passes" by leaving the map; ORIG blocks on the border at n=1.
- 1 `@702`, rec1 C[2]:
  - DLL B from (1188,856), cell (49,53), header mask 0x1040;
  - ORIG's only state is (0,0), border;
  - after `@692 0x2B` the binary mask is 0x40, and from (1188,856) the walk passes in my ORIG.
  - The table already says "released by FIX + 0x28-0x2B" (FIXC = P) while it classes the site "blocked in both".

**C8 [model] Counts** (`recount.py`).
- Class-op DLL-only, 15 sites:

  | map | sites |
  |---|---|
  | 384 | 5 |
  | 2 | 4 (not five) |
  | 61 | 2 |
  | 63 | 2 |
  | 66 | 1 |
  | 328 | 1 |

  FIXC: 12 P, 2 I+P (61 `@1019`, 328 `@207`), 1 I+BP (61 `@1048`).
- `rel == "released by FIX + 0x28-0x2B"` holds on 13 rows: these 12, plus 1 `@702` ("blocked in both").
- Downstream, 96 sites: FIX reaches and passes 23; FIXC 62 (+39). Both confirmed.
- New blocks where the DLL passes: FIX 7, as published. FIXC 9: the FIX list minus 61 `@998` and 62 `@1006` (I+P under FIXC),
  plus 102 `@591`, `@650`, `@743`, `@802`. 102 is "Bloc à pousser/attaquer": 0x2A at `@578`, then 0x1E.
- The DLL reaches, and FIX and FIXC do not: 346 ×2, 52 ×1. They sit downstream of the new blocks 346 `@943` and 52 `@1576`.
- The md §5 table labels the four 102 rows "entity", but `table.tsv` says "class-op". The "17 = entity 7 / class-op 4 / rule 6"
  split is right.

**C9 [model] "blocked in original only" does not require a shared start** (`samestart.py`, rep.py `klass`).
- Outside the M/M case, the class comes from the run letters only.
- In 6 of the 17 rows, none of the ORIG-blocked states exists in the DLL run:
  - 17 `@2234` and 439 `@2234` (9 states each);
  - 135 `@900` (5 states, starts at (637|709, 712-760) that only ORIG's upstream walks produce);
  - 61 `@937`, 61 `@998`, 62 `@1006`.
- For the cart rows this is inherent (an upstream entity stop moves the start). For the 3 rule rows the label "rule" is not
  shown on a same start.
- On the DLL-only side, 8 rows have no shared blocked state: 7 are entity or class-op (expected), plus 439 `@1400` (rule).

**C10 [data] F4 holds for code, tables, records and map events. Two omissions** (`m331.py`).
- Walkability also differs at (28,43), (27,44) and (16,50), and in columns 3-7 the differing rows are 21-24.
- **Portals differ**: 13 on map 10 against 11 on 331, with different destinations. Hero arrivals, and so held-control hero
  starts, do not carry over from 10 to 331.
- The scene cells (B[9], B[14], B[20]) are unaffected, so the [emu] carry-over of `@1215`, `@3673`, `@5071`, `@5172` stands.

**C11 [data][model] The entity and emulation inputs check out** (`obst.py`, arithmetic).
- 185: rec6 0x83A180, box (-10,-6;20,12); rec7 0x006080, no Gravity, box (-12,-8;24,16;32), Height 2.
- Contacts:
  - 330 = 336 − 6;
  - 182 = 192 − 10.
- T-A19 pins in 16.16:
  - 6946816 = 106.0;
  - 21626880 = 330.0;
  - 11927552 = 182.0;
  - 22413312 = 342.0;
  - 22446080 = 342.5;
  - (230, 394) = (15073280, 25821184).
- T-C61 contacts:
  - 274 = 264 + 10 (rec34 x2);
  - 776 = 784 − 8 (rec35 y1);
  - 178 = 168 + 10 (rec34 moved to 156).
- The cart rails (61 rec32-35, 62 rec19-21, 68 rec21) are all Collidable `0x006080` with no Gravity; anim 0 Acceleration has no
  0x80.

**C12 [model] Sample re-derivation** (`sample.py`, 20 random reachable rows, seed 19, neither story nor held). Read by hand
against my own listings, all 20 match the program:
- direction (0x09, 0x3A, 0x5A, 0x5B cardinal table);
- animation;
- radius;
- logic actor (308 `@118` after 0x43 [0]);
- 0x64 / 0x8A starts (63 `@855` (948,200); 64 `@609` (180,440); 84 `@2694` (1104,712)).

363 `@247` (P3 list) also matches the census reading:
- the (1044,232) start needs G248 at load (A[3] `@73`), but C[1] `@189` jumps away when G248 is set;
- rec7 and rec8 ("Bloc transparent") are not spawned before `@247` (SpriteDirection 0, `0x2D @149` / `@253`);
- so the census's "artifact" reading holds.

**C13 [model] Story-map rows.**
- The F15 story difference list is reproduced, except:
  - 15 `@665` should be "passes in both" (C5, C6);
  - 15 `@722` should be "passes in both" (C6);
  - 135 `@900` "blocked in original only" rests on ORIG-only starts (C9).
- The day-3 chain (10 `@2456`), the day-4 site 185 `@506`, and 10/331 B[9]/B[14] are unchanged.

## 3. Design options

- **A (census recommendation): unchanged, still recommended.** None of C5-C9 touches the 12 entity sites.
- **A + C (0x28-0x2B).** The count to quote is 12 class-op sites released (13 if 1 `@702` is re-classed), +39 downstream.
  It also adds **4 new blocks on map 102**, which need the same T-NEW treatment as the 7 FIX blocks.
- **Census correction before any number is pinned (recommended, scratch-only).**
  - Model 0x62/0x63 (search 0x80 = logic; < 0x80 = record id; 0x81 = hero) for ClassA, ClassB, Gravity, NoObstacleSlide and
    Collidable in all runs, the DLL included.
  - Add the oblique slide cases to ORIG.
  - Drop 0x64 / 0x8A writes that come after the site as start candidates.
  - Require shared starts before labelling a rule difference.
  - Re-run.

## 4. Proposed tests (values pre-written from the model, [model])

- **T-CEN-1 (census regression, if the census becomes a repo tool).** These walks, with the program's own 0x63 applied, pass in
  both rules:
  - 15 `@722`: Muruta rec1 from (444,296), dir 24, r 208, ends (652.0, 296.0);
  - 17 and 439 `@3454`: from (108,536), dir 24, r 96, end (204.0, 536.0);
  - 154 `@1088`: (84,600) → (84.0, 664.0);
  - 154 `@1415`: (132,600) → (132.0, 664.0);
  - 393 `@190`: (1068,696) → (1068.0, 712.0);
  - 393 `@246`: (1044,696) → (1044.0, 712.0).

  With the header mask 0x1040, each blocks at n = 0, or n = 2 for `@3454`.
- **T-CEN-2 (oblique slide).** 15 `@665`, from (588,280), dir 26, r 40, ORIG rule: passes, ends at about (628.62, 281.0); without
  the oblique cases it blocks at n = 3.
- **T-DLL-63 (unit, the DLL, existing behaviour).** `0x63 [0x80,1,0]` on an entity with ClassA: the controller mask goes from
  0x1040 to 0x40. This pins what the census missed.
- **T-NEW extension.** Add 102 `@591`, `@650`, `@743`, `@802` to the pre-merge emulation list if 0x28-0x2B are ported. No
  value can be written before that emulation.

## 5. Risks

- **The census overstates "the original hangs too".** At least 8 of the 55 "blocked in both" sites are model artifacts (C6, C7),
  4 more are possible, and one is on a story map (15 `@722`). F12's argument that only a few blocks remain in the original
  must use the corrected list.
- **A+C has unpublished costs**: 4 new blocks on map 102.
- **Other unported or unmodelled ops on the walker remain.** 0x45/0x46 are not ported in the DLL. Flag writes from other
  programs (C6, cross-program) and hero flags that persist across programs are invisible statically.
- **The detour.** 10 of the 13 "slide" DLL-only sites have a clean grid path (`det:clean`). The DLL may well finish them, so the
  slide defect class may be mostly empty in the DLL.

## 6. Author questions (product / authority)

- None new beyond census0b's §8. Question 1 (0x28-0x2B in E19.d2) should be asked with the corrected cost: +12 sites, +39
  downstream, **+4 new blocks on map 102**.

## 7. Technical unknowns

- The timing of the cross-program flag writes on 150, 369 and 382 (C6).
- Whether the DLL detour engages for a hero walk under held control (ForceAdjusted of the hero proxy). This decides the 10
  `det:clean` slide sites.
- The real starts of 17 / 439 `@2234` and 135 `@900` (C9).
