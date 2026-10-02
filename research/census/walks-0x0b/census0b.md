# E19.d2 discovery, surface "census0b": the 0x0B and 0x1E walks, DLL against the original

Read-only, 2026-10-01/02. Parent `chantier/e19-opcodes` at `0664e94`, engine submodule untouched (its `CasaEngine.Launcher/Program.cs`
author edit left alone). Nothing edited, staged, built or run in the repository. Scripts, raw outputs and the full per-site table:
`scratchpad/e19d2-disc/census0b/` (see §10).

Tags: [binary] ALUN_CD.EXE (France), text 0x80020000 at file 0x800; [DLL] `Alundra/Scripts`; [engine] `CasaEngineMonogame`; [data] the
`alundra-project/` export; [model] my static census (a computation on the data, not an observation of either game); [emu] an
emulation of the binary loop (the checker's `emu.py`, copied, or the jump surface's `jsim.py`).

## 1. Summary

- **Population.** 2341 walk sites at instruction boundaries of the linear decode: `0x0B` 1258 (100 maps), `0x1E` 1083 (131 maps).
  2215 are reachable (205 maps; 0x0B 1170, 0x1E 1045), 126 dormant. The reach rule, the real-start rules (0x8A / 0x2D+0x64, real hero
  arrivals, ENTRY_SPEC) and the flag dependencies are those of the corrected 0x24 census (`e19d2/census_v2.py`), copied.
- **Rules compared.** DLL (cell field, step 3, per-axis, entity ghosts, header walk mask); ORIG0 (binary tile rule: joint ray, corner
  slide for movers without NoObstacleSlide, gravity snap only with Gravity, border clamp); ORIG = ORIG0 + the ported 0x28-0x2B class
  ops + the scripted jumps the jump surface certified + **entity obstacles whose presence is certain by structure** (S1: a static
  block spawned earlier by the same program; S2: a load-spawned record with no C program that nothing moves, destroys or
  decollides); FIX = DLL + the same obstacles (the collision surface's option E); FIXC = FIX + 0x28-0x2B. Cross-program obstacles
  (Meade, Rumi, rec41 for B[9]) are undecidable statically: they come from the emulations (collision surface, my `scene185.py`).
- **Totals (reachable 2215).** passes in both 1430; same outcome, start-dependent 172; **blocked in DLL only 43** (+19 for some
  starts); blocked in both 55; blocked in original only 17 (+17 for some starts); downstream (not reached in the DLL only) 96; not
  reached in both 263; uncertain 103.
- **The defect class, 43 sites, by mechanism.**
  - **entity, 12 sites, all released by the recommended fix**: map 10 `@1215`, `@3673`, `@5172`, `@5071` (reached only once `@5172`
    passes), the same four on 331 (a copy of map 10), **185 `@506`** (day 4, the only way to map 362; [emu] certified), and three
    Coal Mine cart rides (61 `@980`, 62 `@957`, 68 `@809`), whose 0x24 legs stop on "Bloc transparent" rails.
  - **class-op, 15 sites**: 0x28-0x2B (set/clear ClassA/ClassB, so the walk mask) are not ported. Five Muruta on map 2, five on 384,
    hero cart walks on 61, 63, 66, 328. The fix plus a port of 0x28-0x2B releases 13 of them; none is on a story map.
  - **rule, 16 sites**: 13 hero/NPC walks that the original's corner slide lets through (E19.h, D-E19-9), and 3 scripted jumps (10
    `@2456`, 10 `@6394`, 331 `@6394`; surface jump, O-E19-19). The entity fix releases none of these.
- **So the recommended fix releases 12 of the 43 "blocked in DLL only" sites** (10 directly reachable, plus the two `@5071`), five of
  them on story maps (10 ×4, 185 `@506`), plus 23 of the 96 downstream sites; with 0x28-0x2B also ported, 25 (+62 downstream).
- **The fix also adds blocks**: 7 sites pass in the DLL and block under FIX (52 `@1576`, 61 `@937`, `@998`, 62 `@1006`, 199 `@835`,
  346 `@943`, 426 `@668`; none on a story map), plus 13 "some starts" NPC walks into graveyard records (Tombe) on maps 10 and 331
  under held control, and 240 `@603`. Each needs an emulation or an arc before the fix merges.
- **The P3 list of E19.d** (nine walks blocked only because of the 0x24 port): 10/331 `@1215` and `@5172` and 185 `@506` are entity
  defects, released by the fix. 363 `@247`, 365 `@268`, 334 `@260` and 366 `@418` show **no DLL/original difference** for the same
  start: the blocked start is a model artifact (a loop re-entry after the program's own 0x24, or a second candidate start).

| class (reachable sites) | all | 0x0B | 0x1E | story maps | held control | story + held |
|---|---|---|---|---|---|---|
| passes in both | 1430 | 756 | 674 | 192 | 571 | 105 |
| same outcome, start-dependent | 172 | 100 | 72 | 18 | 123 | 14 |
| blocked in DLL only | 43 | 21 | 22 | 8 | 27 | 5 |
| blocked in DLL only (some starts) | 19 | 10 | 9 | 2 | 18 | 2 |
| blocked in both | 55 | 22 | 33 | 8 | 21 | 3 |
| blocked in original only | 17 | 5 | 12 | 2 | 9 | 1 |
| blocked in original only (some starts) | 17 | 13 | 4 | 6 | 17 | 6 |
| downstream | 96 | 54 | 42 | 15 | 67 | 7 |
| not reached | 263 | 137 | 126 | 48 | 0 | 0 |
| uncertain | 103 | 52 | 51 | 14 | 39 | 1 |
| **total reachable** | 2215 | 1170 | 1045 | 313 | 892 | 144 |


Reading the classes:
- *passes in both*: every modelled start passes under the DLL rule and under ORIG.
- *same outcome, start-dependent*: some modelled starts block, others pass, with the same outcome in both rules for each start. The
  blocked starts are mostly model artifacts: several candidate positions, loop re-entries, or hero samples in a 0x3B box.
- *blocked in DLL only*: the DLL blocks (B, or M), ORIG passes. *(some starts)*: the same, but only for some of the starts.
- *blocked in both*: the census says the original hangs too (§3 F12).
- *blocked in original only*: the DLL passes, ORIG blocks. This is the reverse class, and the risk class for the fix.
- *downstream*: the DLL never reaches the site, because a walk or a flag upstream is blocked in the DLL only; ORIG reaches it.
- *not reached*: no state reaches the site in either rule (flag never set: 136; walk blocked in both upstream: 127).
- *uncertain*: indeterminate in both (60), zero speed (20), partly indeterminate (19), and 4 others.
- *held control* means a modelled state holds the hero's control (the census lock), so it is known only for reached sites.

## 2. Method (copied census, changes, rules)

1. **Copy, not import.** `census_exec_orig.py`, `census_v2_orig.py`, `m.py` and `gstatic.pkl` are copies of `e19d/census-exec/`,
   `e19d2/` and `e19d/binary/`. `c0b.py` is census_v2 with these changes:
   - the sites are every `0x0B` and `0x1E`;
   - the walk of every program goes through `geo.walk` under the run's rule;
   - a blocked `0x0B` **or `0x1E`** cuts the rest of its program. 0x1E is modelled without the E4.d detour, which the original
     does not have; the DLL detour is assessed apart (`detour.py`);
   - the end rule is exact. A walk is blocked iff the 1 px iteration count `n < r`. The reachable distance D satisfies
     n <= D < n+1, and the binary ends on `r <= |dX|>>16` (F1). The 0x24 census cut at `n < r - 1`, its P4 "1 px gap".
2. **DLL rule** [DLL][engine]. Per axis (h1 = X, then h2 = Y). A corner blocks when its cell hits the walk mask, or when its ground
   is above foot + 3 (CharacterControllerComponent.cs:1108-1286). Out-of-grid points read the edge cell, so an entity can leave the
   map (AlundraCellsCollisionField.cs:279-301). The walk mask is the header's (`WalkabilityMaskFor`), because 0x28-0x2B are
   skipped (F3). No entity blocks (D-E12D-1).
3. **ORIG rule** [binary], from collision.md F3-F5, collision-check.md, border.md F1-F4 and e19a2/binary.md F1, F9, F14:
   - joint ray (both axes together, no per-axis advance for an oblique step);
   - FindEntityCollisionCandidate before the tiles (half-open AABB on X, Y and Z; mover and obstacle Collidable; AnimFlags bit 0x80
     clear);
   - a corner blocks on the mask, or on a height above the foot, unless the gravity snap applies (Gravity flag and a rise of 3 px at
     most);
   - corner slide: a cardinal step blocked by the tiles on exactly one front corner, for a mover without NoObstacleSlide (an entity
     never slides);
   - border clamp: the footprint stays in [0,1248] x [0,960];
   - 0x28-0x2B tracked per walk state on the logic entity (CLASSOPS);
   - the jumps certified by the jump surface (10 and 331 `@2456`, `@6394`) never cut.
4. **Obstacles.**
   - *cert*: S1 + S2 (summary).
     - S1 lives in the walk state: the spawned record carries its pin. Its pin is dropped when the same program destroys it (0x2E),
       deactivates it (0x19) or clears its Collidable bit (0x63).
     - Obstacle Z: the record height (Height × 8, the 0x2D spawn Z), raised to the ground. With Gravity, the record stands on the
       ground. A gravity-less record floating more than 2 px above the ground is a platform a mover may stand on, so it is left out
       (map 389 rec2 under the sailor rec11).
     - A mover that starts inside an obstacle is reported as `start-overlap`.
   - *max* (`cloud.py`, runs ORIGMAX / FIXMAX): every place a Collidable record can stand in any state of the DLL run, ignoring
     time. It is an indicator only.
5. **Certified overrides** (rep.py `CERTIFIED`): sites decided by an emulation of the binary loop.
   - Map 10 `@1215`, `@3673`, `@5071`, `@5172`: collision surface F10-F14, confirmed by collision-check.
   - The same four on 331, by data identity (F4).
   - 185 `@506`: my `scene185.py` (F7).
   - 10 and 331 `@6394`, 10 `@2456`: jump surface, `jsim.py`.
6. **Runs** (4 passes each, as the 0x24 census):
   - DLL, ORIG0, ORIGnc (ORIG without the class ops), ORIG, FIX, FIXC, ORIGMAX and FIXMAX (the last two use an older Z rule, as
     indicators).
   - Map 292 keeps oscillating on 6 sites after pass 4, in every run.

## 3. Facts

**F1 [binary] 0x0B, 0x1E, 0x1F.** Handler table 0x80098FAC.
- `0x0B` (0x8003D468) writes `TargetAnimationId = v1` of the logic entity on every call.
  - The first call at a pc stores the code pointer at state+8 and PosX/PosY at +0xC/+0x10, then returns 0.
  - Later calls return 4 when `r <= |dX|>>16` or `r <= |dY|>>16`, with `r = v2 | v3<<8`; otherwise they return 0
    (0x8003D4E0-0x8003D50C).
- `0x1E` (0x8003D8D8) is the same with no animation write and `r = v1 | v2<<8`; it returns 3.
- `0x1F` (0x8003D974) calls 0x1E, and also returns 3 when ForceAdjusted (+0x13C) is non-zero.
- Neither 0x0B nor 0x1E has an exit on a blocked walk.

**F2 [binary][DLL] Key of the walk, detour.**
- RunScript zeroes state+8 after every completed instruction (0x8004232C) and on a Break (0x800421F4). The DLL zeroes
  `Parameters[1]` the same way (AlundraEventProgramRunner.cs:396, :450).
- So the DLL's 0x1E, keyed on its operand signature (:2117), behaves like the binary's code-pointer key.
- The DLL adds the E4.d detour to 0x0B (:2243) and 0x1E (:2154), but not to 0x1F (:657-664).
- Detour (:2264-2321): one A* attempt, 4-connected, on the navigation grid, which ignores heights (walkable iff
  `(walk | gp<<8) & 0x40 == 0`, NavigationWriter.cs:233-235). The goal is the memorised start + sign(dir) × (r + 24).

**F3 [binary][DLL] 0x28-0x2B change the walk mask; the DLL skips them.**
- [binary] 0x28 (0x8003DC24) `Flags |= 8` (ClassB), 0x29 (0x8003DC3C) `&= ~8`, 0x2A (0x8003DC54) `|= 1` (ClassA), 0x2B (0x8003DC6C)
  `&= ~1`. They act on the logic entity, with size 1. The tile mask is 0x40, |0x01 with ClassB, |0x1000 with ClassA (collision.md F5).
- [DLL] Dispatch has no case for them. They are skipped by size (EventOpcodeSizeTable.cs:77-80), so the controller keeps the header
  mask.
- [data] They sit in 166 maps, often around a scripted walk:
  - Muruta: 0x2B, walk through the enemy-only (0x1000) cells, 0x2A, then 0x40 to native AI. Example: 384 `rec0 C[1] @320-@381`.
  - The hero in the Coal Mine carts: 0x29 on map 62 `B[5] @923`.

**F4 [data] 331 is a copy of map 10.**
- Same code (6824 bytes), entry tables, map events and records (117).
- The cells differ only at columns 3-7 of rows 21-25 (walkability), on 9 slope cells (row 12, and (4,21), (4,22), (6,23)) and on 1
  height cell (4,25). None of them is in the B[9], B[14] or B[20] scene areas.
- 331 is reached by the 0x53 of 481 `@776` and by the 0x38 10→331 of 197 `@667`.

**F5 [model] Population and reach.**
- 2341 sites: 1258 `0x0B` in 100 maps, 1083 `0x1E` in 131 maps.
- 2215 reachable in 205 maps, 126 dormant (88 `0x0B`, 38 `0x1E`), 0 DLL-only.

**F6 [model] Totals:** see the table above.
- The defect class has 43 sites: entity 12, class-op 15, rule 16 (13 slide, 3 jump).
- The reverse class has 17 sites: entity 7, class-op 4, rule 6.

**F7 [emu] 185, day 4 (`scene185.py`, the checker's binary-loop emulator).**
- Setup: G120-G123 set; the hero at (132,296), inside B[1]'s box; B[1] from `@81`. B[1] `@84` activates Septimus rec6 at (132,184),
  and C[5] `@454` activates the transparent block rec7 at (120,344), z 16.
- With entity collision:
  - the hero's `0x24 @95` ends at (106.0, 296.0);
  - Septimus `0x24 @458` ends at (132.0, **330.0**) against rec7, at t 93;
  - `0x24 @463` ends at x **182.0** (the 16 px step at (8,20));
  - rec7 is destroyed by `@466`;
  - `0x0B @506` ends at (182.0, **342.5**).
- With the DLL's kept D-E19-13 lag the values are the same; `@506` ends one tick later, and Septimus rests at 343.5.
- Without entity collision (the DLL today): 394.0, then 230.0, and `@506` never ends. FA = 1 at contact, so the DLL detour would
  engage, but no grid path exists (cell (9,26) is 0x41, chain-check C6). This is the stall of chain.md R1.
- [data] rec6 has Flags 0x83A180, box (-10,-6; 20,12); rec7 has 0x006080, no Gravity, box (-12,-8; 24,16; 32). Both stand at z 16.

**F8 [model] The 12 entity sites the fix releases** (table in §5).
- In ten of them a 0x24 (or 0x1F) stops earlier on a block. Five of those blocks are spawned by the same program: rec41 for Septimus
  on 10/331, rec7 on 185, the cart rails rec33-35 on 61, rec19 on 62, rec21 on 68. On map 10, Meade, Rumi and rec41 (for B[9]) are
  cross-program, and come from the emulations.
- In the DLL all twelve are B. The detour does not save them: no grid path, or a path the mover cannot walk (rise or class bit);
  `detour.py`, flags `det:`.

**F9 [model] Class-op sites.**
- 15 are blocked in the DLL only because the walk crosses a cell that is blocked only by a class bit (0x01 or 0x1000), which the
  program had cleared with 0x29 or 0x2B.
- ORIGnc still blocks them, ORIG passes them. FIXC (fix + class ops) passes 13 of them.
- 384 `@325`-`@513` (five Muruta) is a cutscene that never reaches its dialogues in the DLL (`0x0D @330`, `@346`).

**F10 [model] Rule sites (16).**
- Corner slide, 13 sites. All are hero walks under held control except 440 `@2277` (Jeune Meia). Mostly a hero start sampled near a
  corner: 2 `@427`, 8 `@661`, 10/331 `@6507`, 19 `@1011`, 138 `@671`, 388 `@58`, 398 `@2052`/`@2063`, 439 `@1400`, 472 `@337`,
  474 `@339`.
- Jump, 3 sites: 10 `@2456` (B[20], mandatory day-3 chain), 10 and 331 `@6394` (Giles).
- 398 runs 0x46 then 0x45 before its walks (O-E19-20). The model uses the header's NoObstacleSlide.

**F11 [model] Blocks the fix adds** (§5).
- 7 sites pass in the DLL and block under FIX. Each starts or stops on an S1/S2 obstacle:
  - 52 `@1576`: Meia against rec34, "Mur à boule de fer (2×2×1) permanent";
  - 199 `@835`: hero against the chest rec4 (start states sampled around it);
  - 346 `@943`: stair trap rec7 against rec10, "Mur à boule de fer – axe";
  - 426 `@668`: hero starting inside signpost rec46;
  - 61 `@937`, `@998`, 62 `@1006`: cart rides, sensitive to sub-pixel contact positions in the model.
- Plus 13 "some starts" on 10/331: Cephas, Septimus rec56 and Meade rec57 walking among the graveyard's Tombe records (Collidable,
  0x036180). Half of their blocked starts are loop re-entries. Plus 240 `@603`.
- None is on the mandatory chain programs.

**F12 [model] Blocked in both (55).**
- By actor: enemies (Muruta, Mimming, Zorgia) 26; blocks and objects (transparent blocks, push blocks, iron balls) 16; the hero 7;
  NPCs 6. 6 are near a jump (0x25 within 8 instructions); 10 stop on a cell that a reachable 0x54/0x55/0x85 rewrites.
- On story maps:
  - 15 `@722`, `@832`: Muruta of the scripted day-2 fight, E14;
  - 117 `@825`, `@953`, `@993`: iron balls of Tarn's Manor, a puzzle with pushed balls;
  - 135 `@1806`: Septimus, chapter 15; 135 `@2326`: rec16 C[8];
  - 165 `@684`: the cell-write artifact of chain.md F14.
- None is a mandatory chain program.

**F13 [model] Cross-program entities are undecidable statically.**
- Under the time-free cloud, ORIGMAX differs from ORIG on 807 sites.
- 522 of the 1430 "passes in both" would meet some record's standing place on their path at some time: 137 B/M, 385 not reached.
- ORIGMAX ran on an earlier model state (no class ops, no certified jumps, ground-only Z), so these counts are an indicator only.
- This measures timing dependence, not a defect. A universal fix makes these contacts real, as in the original.

**F14 [model] The P3 list of E19.d (nine walks).**
- 10/331 `@1215`, `@5172` and 185 `@506`: entity, released.
- 363 `@247` (Zane): two candidate starts. From (1044,232) the census's own 0x24 reaches the east wall and `@247` blocks in both rules;
  from (780,248) it passes in both.
- 365 `@268`, 334 `@260` (Jess): the blocked state starts from a pin produced by the program's later 0x24 `@276`/`@300` (loop
  re-entry); the first-pass state passes in both.
- 366 `@418` (Beaumont): same outcome per start, loop-flagged. It passes only under FIXMAX (cross-program).

**F15 [model] Story maps** (Inoa 162-189, 44, 10, 14, 15, 114-117, 135, 136, 362): 313 reachable sites.
- Day 1 (162-165) and the dream 44: no DLL/original difference. 165/174/179/181 show "same outcome" hero samples (chain-check C13:
  impossible arrivals).
- The day-3 mandatory chain differs only at 10 `@2456` (jump). Downstream of it: 135 `@930`, `@936` and 178 ×5 are reached under
  ORIG, not in the DLL.
- Day 4 (185) differs at `@506` (entity) and at `@219` (downstream, T40).
- 10 `B[9]` and `B[14]` differ at `@1215`, `@3673`, `@5071`, `@5172` (entity) and downstream at `@3693`-`@3865`.
  - `@3734` stays B under ORIG and FIX in the model, because B[9]'s destroy of rec41 is cross-program.
  - The checker's emulation completes the scene to G482 only with slot recycling; with a naive port it stalls there too
    (collision-check, "missed" 1).

## 4. Design options

- **A (recommended).**
  - The collision surface's option E: an engine dynamic-obstacle predicate in the controller's field stage, with the DLL rule.
  - With collision-check's mandatory additions: FlagToDestroy entities leave the obstacle set at the next tick, the native E destroy
    or an equivalent, AnimFlags loaded from the animation set, and the E12.d contact taken from the blocking report.
  - It releases the 12 entity sites, 5 of them on story maps, including the mandatory 185 `@506`, plus 23 downstream sites.
- **A + C (recommended addition).** Also port 0x28-0x2B: four lines and a controller resync, like 0x16/0x17. It releases 13 more
  sites (Muruta cutscenes on 2 and 384, Coal Mine carts) and 39 more downstream sites (62 in all under FIXC). None is on a story map.
  The plan's E19.l lists 0x2A and 0x2B (0x28/0x29 under "etc."); moving them is a scope question.
- **A-narrow.** Scripted movers plus the hero under the script lock only. It releases 11 of the 12 for certain: 62 `@957` runs from a
  T190 gate whose lock the model does not see. It avoids almost none of the 7 new blocks: the movers there are scripted, or the
  hero is under lock (62 `@1006` excepted).
- **Slide (E19.h).** Porting the corner slide releases the 13 slide sites and most of the 12 "some starts" rule sites. It stays in
  E19.h unless the author moves it (D-E19-9).
- **Rejected.** Per-site script workarounds, or a timer exit on a blocked walk (D-E19-6: fix at the root). Making the 0x0B detour
  cover these cases: the grid ignores heights and entities, and in the 12 entity sites no grid path helps (F8).

## 5. Tables of the difference classes

Columns: S = story map; H = held control; mech = mechanism; verdicts DLL / ORIG0 / ORIGnc / ORIG / FIX / FIXC (P, B, M, Z, I, U:cause);
release = what releases it ("fix" = entity blocking; "fix + 0x28-0x2B"; "[emu]" = certified by emulation).
Flags: `xp:` = verdict under the time-free cloud (cross-program indicator); `cw` = the DLL block is on a rewritten cell; `loop` = the
blocked starts come from after the site; `jump` = a 0x25 within 8 instructions; `det:` = DLL detour (none / blocked-path / clean).
The "upstream entity stops" column comes from a best-effort provenance index keyed by end position; collisions are possible.

### blocked in DLL only
| map | pc | op | actor | S | H | mech | DLL/ORIG0/ORIGnc/ORIG/FIX/FIXC | DLL block | ORIG block; upstream entity stops | release | flags |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 2 | 427 | 1E | hero |  | H | rule | M/P/P/P/M/M | cell (30,29), 2/24 px |  |  | det:clean |
| 2 | 705 | 0B | rec6 ◆Muruta |  | H | class-op | B/B/B/P/B/P | cell (48,25), 0/16 px |  | fix + 0x28-0x2B | xp:B det:clean |
| 2 | 1066 | 1E | rec14 ◆Muruta |  |  | class-op | B/B/B/P/B/P | cell (26,35), 1/16 px |  | fix + 0x28-0x2B | xp:B jump det:blocked-path |
| 2 | 1141 | 1E | rec15 ◆Muruta |  |  | class-op | B/B/B/P/B/P | cell (26,35), 49/64 px |  | fix + 0x28-0x2B | xp:B det:blocked-path |
| 2 | 1209 | 1E | rec16 ◆Muruta |  |  | class-op | B/B/B/P/B/P | cell (26,35), 1/16 px |  | fix + 0x28-0x2B | xp:B jump det:blocked-path |
| 8 | 661 | 0B | hero |  | H | rule | M/P/P/P/M/M | cell (27,48), 12/16 px |  |  | xp:B det:clean |
| 10 | 1215 | 0B | hero | S | H | entity | B/B/B/B/B/B | cell (36,16), 2/48 px | cell (36,16) | fix [emu] | xp:U:walk det:blocked-path |
| 10 | 2456 | 0B | hero | S | H | rule | B/B/P/P/B/B | step (22,53), 17/194 px |  | NOT fix (scripted jump: 0x25 / IsZForceApplied, surface jump) | xp:U:walk jump det:blocked-path |
| 10 | 3673 | 0B | rec39 Septimus | S |  | entity | B/B/P/P/P/P | step (36,16), 2/72 px | @3670 rec41 | fix [emu] | xp:U:walk det:blocked-path |
| 10 | 5071 | 0B |  | S |  | entity | U:flag/U:flag/U:flag/U:flag/U:flag/U:flag |  |  | fix [emu] (reached once @5172 passes) |  |
| 10 | 5172 | 0B | rec74 Nestus | S | H | entity | B/B/B/B/B/B | step (40,21), 0/28 px | step (40,21) | fix [emu] | xp:U:flag det:blocked-path |
| 10 | 6394 | 0B | rec98 Giles | S | H | rule | B/B/P/P/B/B | step (22,53), 21/210 px |  | NOT fix (scripted jump: 0x25 / IsZForceApplied, surface jump) | xp:U:flag jump det:blocked-path |
| 10 | 6507 | 0B | hero | S | H | rule | M/P/P/P/M/M | cell (38,46), 0/36 px |  |  | xp:B det:clean |
| 19 | 1011 | 0B | hero |  | H | rule | M/P/P/P/M/M | cell (2,50), 7/16 px |  |  | det:clean |
| 61 | 980 | 1E | hero |  | H | entity | B/B/P/P/P/P | cell (3,52), 0/64 px | @1076 rec33, @959 rec34, @964 rec35 | fix | xp:B det:none |
| 61 | 1019 | 1E | hero |  | H | class-op | B/B/B/P/B/I+P | cell (17,45), 0/64 px |  |  | xp:B cw:1/1 det:blocked-path |
| 61 | 1048 | 1E | hero |  | H | class-op | B/B/B/P/B/I+BP | cell (20,54), 0/64 px |  |  | xp:B det:blocked-path |
| 62 | 957 | 1E | hero |  |  | entity | B/B/P/P/P/P | cell (20,52), 48/96 px | @944 rec19 | fix | xp:U:walk cw:2/2 det:blocked-path |
| 63 | 677 | 1E | hero |  | H | class-op | B/B/B/P/B/P | cell (28,10), 0/32 px | @646 rec30, @651 rec31, @669 rec32 | fix + 0x28-0x2B | xp:B det:none |
| 63 | 855 | 1E | hero |  |  | class-op | B/B/B/P/B/P | cell (37,12), 26/48 px |  | fix + 0x28-0x2B | xp:B det:blocked-path |
| 66 | 753 | 1E | hero |  |  | class-op | B/B/B/P/B/P | cell (48,13), 145/184 px |  | fix + 0x28-0x2B | xp:U:walk det:blocked-path |
| 68 | 809 | 1E | hero |  | H | entity | B/B/P/P/P/P | step (37,47), 44/128 px | @806 rec21 | fix | det:blocked-path |
| 138 | 671 | 0B | hero |  | H | rule | B/P/P/P/B/B | step (32,23), 11/60 px |  |  | xp:U:flag det:clean |
| 185 | 506 | 0B | rec6 Septimus | S |  | entity | B/B/P/P/P/P | cell (9,25), 0/12 px | @458 rec7 | fix [emu] | xp:B det:none |
| 328 | 207 | 1E | hero |  | H | class-op | B/B/B/P/B/I+P | cell (0,50), 0/32 px |  |  | xp:B det:blocked-path |
| 331 | 1215 | 0B | hero |  | H | entity | B/B/B/B/B/B | cell (36,16), 2/48 px | cell (36,16) | fix [emu] | xp:U:walk det:blocked-path |
| 331 | 3673 | 0B | rec39 Septimus |  |  | entity | B/B/P/P/P/P | step (36,16), 2/72 px | @3670 rec41 | fix [emu] | xp:U:walk det:blocked-path |
| 331 | 5071 | 0B |  |  |  | entity | U:flag/U:flag/U:flag/U:flag/U:flag/U:flag |  |  | fix [emu] (reached once @5172 passes) |  |
| 331 | 5172 | 0B | rec74 Nestus |  | H | entity | B/B/B/B/B/B | step (40,21), 0/28 px | step (40,21) | fix [emu] | xp:U:flag det:blocked-path |
| 331 | 6394 | 0B | rec98 Giles |  | H | rule | B/B/P/P/B/B | step (22,53), 21/210 px |  | NOT fix (scripted jump: 0x25 / IsZForceApplied, surface jump) | xp:U:flag jump det:blocked-path |
| 331 | 6507 | 0B | hero |  | H | rule | M/P/P/P/M/M | cell (38,46), 0/36 px |  |  | xp:B det:clean |
| 384 | 325 | 1E | rec0 ◆Muruta |  |  | class-op | B/B/B/P/B/P | cell (29,53), 0/168 px |  | fix + 0x28-0x2B | xp:B det:blocked-path |
| 384 | 393 | 1E | rec1 ◆Muruta |  |  | class-op | B/B/B/P/B/P | cell (30,53), 0/168 px |  | fix + 0x28-0x2B | xp:B det:blocked-path |
| 384 | 433 | 1E | rec2 ◆Muruta |  |  | class-op | B/B/B/P/B/P | cell (31,53), 0/168 px |  | fix + 0x28-0x2B | xp:B det:blocked-path |
| 384 | 473 | 1E | rec3 ◆Muruta |  |  | class-op | B/B/B/P/B/P | cell (32,53), 0/168 px |  | fix + 0x28-0x2B | xp:B det:blocked-path |
| 384 | 513 | 1E | rec4 ◆Muruta |  |  | class-op | B/B/B/P/B/P | cell (33,53), 0/168 px |  | fix + 0x28-0x2B | xp:B det:blocked-path |
| 388 | 58 | 1E | hero |  | H | rule | M/P/P/P/M/M | cell (14,23), 31/64 px |  |  | det:clean |
| 398 | 2052 | 0B | hero |  | H | rule | M/P/P/P/M/M | cell (13,13), 137/144 px |  |  | det:clean |
| 398 | 2063 | 0B | hero |  | H | rule | M/P/P/P/M/M | cell (13,13), 0/80 px |  |  | det:blocked-path |
| 439 | 1400 | 1E | hero |  | H | rule | M/P/P/P/M/M | cell (0,34), 0/16 px |  |  | xp:U:walk det:blocked-path |
| 440 | 2277 | 0B | rec23 Jeune Meia |  | H | rule | M/P/P/P/M/M | step (43,18), 2/48 px |  |  | det:blocked-path |
| 472 | 337 | 1E | hero |  | H | rule | M/P/P/P/M/M | cell (7,48), 5/96 px |  |  | det:clean |
| 474 | 339 | 1E | hero |  | H | rule | M/P/P/P/M/M | cell (42,36), 5/48 px |  |  | det:clean |
### blocked in DLL only (some starts)
| map | pc | op | actor | S | H | mech | DLL/ORIG0/ORIGnc/ORIG/FIX/FIXC | DLL block | ORIG block; upstream entity stops | release | flags |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 17 | 2177 | 1E | hero |  | H | rule | M/M/M/M/M/M | step (40,41), 11/24 px | step (41,42) |  | xp:B loop jump det:clean |
| 54 | 1202 | 1E | hero |  | H | rule | M/M/M/M/M/M | cell (26,13), 13/16 px | cell (26,8) |  | det:clean |
| 62 | 619 | 1E | hero |  | H | class-op | B/B/B/M/B/I+BP | step (10,36), 32/96 px | step (10,36); @606 rec23 |  | xp:B det:blocked-path |
| 63 | 579 | 1E | hero |  | H | class-op | B/B/B/M/B/M | cell (51,36), 0/64 px | cell (47,7); @566 rec26, @571 rec27, @576 rec28 |  | xp:B det:none |
| 64 | 650 | 1E | hero |  | H | class-op | B/B/B/M/B/M | cell (0,23), 0/32 px | cell (40,28); @647 rec33 |  | xp:B det:none |
| 66 | 518 | 1E | hero |  |  | entity | B/B/M/M/M/M | cell (28,15), 81/208 px | cell (20,14); @476 rec18, @494 rec17, @515 rec17 |  | xp:U:walk det:none |
| 66 | 608 | 1E | hero |  | H | class-op | M/M/M/M/M/M | cell (49,39), 0/72 px | cell (49,39) |  | det:blocked-path |
| 66 | 618 | 1E | hero |  | H | class-op | B/B/B/M/B/M | cell (36,19), 32/184 px | cell (36,19) |  | xp:B det:blocked-path |
| 135 | 894 | 0B | hero | S | H | rule | M/M/M/M/M/M | cell (30,40), 16/128 px | cell (26,45) |  | xp:U:flag det:clean |
| 136 | 210 | 0B | hero | S | H | rule | M/M/M/M/M/M | step (32,13), 25/32 px | step (32,13) |  | det:clean |
| 145 | 500 | 0B | rec1 Jess |  | H | entity | B/B/M/M/M/M | cell (18,16), 0/48 px | cell (18,20); @487 rec6, @493 rec7 |  | xp:U:flag det:none |
| 197 | 116 | 0B | hero |  | H | rule | M/M/M/M/M/M | cell (41,14), 13/24 px | cell (41,14) |  | det:clean |
| 217 | 77 | 0B | hero |  | H | rule | M/M/M/M/M/M | step (37,10), 0/24 px | start-overlap  rec7; @77 rec7 |  | det:clean |
| 231 | 134 | 0B | hero |  | H | rule | M/M/M/M/M/M | cell (3,13), 9/48 px | cell (4,11) |  | det:clean |
| 231 | 374 | 0B | hero |  | H | rule | M/M/M/M/M/M | cell (10,7), 1/32 px | cell (9,6) |  | det:none |
| 357 | 134 | 0B | hero |  | H | rule | B/M/M/M/B/B | cell (50,55), 73/420 px | step (32,29) |  | det:blocked-path |
| 358 | 142 | 0B | hero |  | H | rule | M/M/M/M/M/M | cell (41,9), 65/72 px | cell (41,9) |  | det:clean |
| 439 | 2177 | 1E | hero |  | H | rule | M/M/M/M/M/M | step (40,41), 11/24 px | step (41,42) |  | loop jump det:clean |
| 440 | 2045 | 0B | rec20 Jeune Meia |  | H | rule | M/M/M/M/M/M | step (6,51), 1/12 px | step (6,51) |  | jump det:clean |
### blocked in original only
| map | pc | op | actor | S | H | mech | DLL/ORIG0/ORIGnc/ORIG/FIX/FIXC | DLL block | ORIG block; upstream entity stops | release | flags |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 634 | 0B | rec0 ◆Muruta |  |  | rule | P/B/B/B/P/P |  | border  |  | jump |
| 15 | 665 | 0B | rec0 ◆Muruta | S |  | rule | P/B/B/B/P/P |  | cell (24,18) |  |  |
| 17 | 2234 | 1E | hero |  | H | rule | P/M/M/M/P/P |  | step (41,44); @2184 rec27 |  | xp:U:walk |
| 52 | 1576 | 0B | rec36 Meia |  | H | entity | P/P/B/B/B/B |  | obstacle  rec34; @1576 rec34 | NEW block (B) |  |
| 61 | 937 | 1E | hero |  | H | entity | P/P/M/M/M/M |  | cell (25,41); @1076 rec33, @932 rec32 | NEW block (M) | xp:P |
| 61 | 998 | 1E | hero |  | H | entity | P/P/M/M/M/I+P |  | border ; @1076 rec33, @959 rec34, @964 rec35 | NEW block (M) | xp:P |
| 62 | 1006 | 1E | hero |  |  | entity | P/P/M/M/M/I+P |  | border ; @944 rec19, @993 rec20, @998 rec21 | NEW block (M) | xp:U:walk |
| 66 | 613 | 1E | hero |  | H | rule | P/M/M/M/P/P |  | step (37,15) |  |  |
| 102 | 591 | 1E | rec0 Bloc à pousser/ |  |  | entity | P/P/P/M/P/M |  | cell (14,48) |  | xp:P |
| 102 | 650 | 1E | rec0 Bloc à pousser/ |  |  | entity | P/P/P/M/P/M |  | cell (14,48) |  | xp:P |
| 102 | 743 | 1E | rec9 Bloc à pousser/ |  |  | entity | P/P/P/M/P/M |  | cell (25,48) |  | xp:P |
| 102 | 802 | 1E | rec9 Bloc à pousser/ |  |  | entity | P/P/P/M/P/M |  | cell (25,48) |  | xp:P |
| 135 | 900 | 0B | hero | S | H | rule | P/M/M/M/P/P |  | cell (30,45); @452 rec17 |  | xp:U:flag |
| 199 | 835 | 0B | hero |  | H | entity | P/P/M/M/M/M |  | obstacle  rec4; @835 rec4 | NEW block (M) |  |
| 346 | 943 | 1E | rec7 Trappe d’escali |  |  | entity | P/P/B/B/B/B |  | obstacle  rec10; @943 rec10 | NEW block (B) |  |
| 426 | 668 | 1E | hero |  | H | entity | P/P/M/M/M/M |  | start-overlap  rec46; @668 rec46 | NEW block (M) | jump |
| 439 | 2234 | 1E | hero |  | H | rule | P/M/M/M/P/P |  | step (41,44); @2184 rec27 |  |  |
### blocked in original only (some starts)
| map | pc | op | actor | S | H | mech | DLL/ORIG0/ORIGnc/ORIG/FIX/FIXC | DLL block | ORIG block; upstream entity stops | release | flags |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 531 | 1E | hero |  | H | rule | M/M/M/M/M/M | cell (18,43), 2/24 px | cell (4,54) |  | xp:U:flag det:blocked-path |
| 10 | 4205 | 0B | rec55 Cephas | S | H | entity | M/M/M/M/M/M | step (28,18), 1/24 px | step (35,17); @4205 rec19 |  | xp:U:flag loop:103/117 det:clean |
| 10 | 4221 | 0B | rec55 Cephas | S | H | entity | M/M/M/M/M/M | step (28,18), 1/16 px | step (35,17); @4221 rec19 |  | xp:U:flag det:clean |
| 10 | 4313 | 0B | rec56 Septimus | S | H | entity | M/M/M/M/M/M | step (25,19), 6/24 px | step (25,21); @4313 rec18, @4313 rec19, @4313 rec21 |  | xp:U:flag loop:75/81 det:clean |
| 10 | 4326 | 0B | rec56 Septimus | S | H | entity | M/M/M/M/M/M | step (25,19), 6/16 px | step (25,21); @4326 rec18, @4326 rec19, @4326 rec21 |  | xp:U:flag det:clean |
| 10 | 4393 | 0B | rec57 Meade | S | H | entity | M/M/M/M/M/M | step (28,18), 9/24 px | step (25,20); @4393 rec19 |  | xp:U:flag loop:111/136 det:clean |
| 10 | 4409 | 0B | rec57 Meade | S | H | entity | M/M/M/M/M/M | step (28,18), 9/16 px | step (26,17); @4409 rec19 |  | xp:U:flag det:clean |
| 17 | 1342 | 1E | hero |  | H | rule | M/M/M/M/M/M | step (29,50), 2/48 px | cell (22,40) |  | xp:B loop:1/9 det:blocked-path |
| 17 | 1691 | 1E | hero |  | H | rule | M/M/M/M/M/M | step (25,42), 0/24 px | border  |  | xp:B det:blocked-path |
| 240 | 603 | 0B | rec2 Beaumont |  | H | entity | M/M/M/M/M/M | step (4,45), 1/24 px | step (4,45); @603 rec6 |  | xp:B det:clean |
| 331 | 4205 | 0B | rec55 Cephas |  | H | entity | M/M/M/M/M/M | step (28,18), 1/24 px | step (35,17); @4205 rec19 |  | xp:U:flag loop:103/117 det:clean |
| 331 | 4221 | 0B | rec55 Cephas |  | H | entity | M/M/M/M/M/M | step (28,18), 1/16 px | step (35,17); @4221 rec19 |  | xp:U:flag det:clean |
| 331 | 4313 | 0B | rec56 Septimus |  | H | entity | M/M/M/M/M/M | step (25,19), 6/24 px | step (25,21); @4313 rec18, @4313 rec19, @4313 rec21 |  | xp:U:flag loop:75/81 det:clean |
| 331 | 4326 | 0B | rec56 Septimus |  | H | entity | M/M/M/M/M/M | step (25,19), 6/16 px | step (25,21); @4326 rec18, @4326 rec19, @4326 rec21 |  | xp:U:flag det:clean |
| 331 | 4393 | 0B | rec57 Meade |  | H | entity | M/M/M/M/M/M | step (28,18), 9/24 px | step (25,20); @4393 rec19 |  | xp:U:flag loop:111/136 det:clean |
| 331 | 4409 | 0B | rec57 Meade |  | H | entity | M/M/M/M/M/M | step (28,18), 9/16 px | step (26,17); @4409 rec19 |  | xp:U:flag det:clean |
| 439 | 1342 | 1E | hero |  | H | rule | M/M/M/M/M/M | step (29,50), 2/48 px | cell (17,41) |  | xp:B det:blocked-path |


The full per-site table (2341 rows, every class, both opcodes): `census0b/table.tsv` (and `table.pkl`).

## 6. Proposed tests (values written before the code)

**T-A19, arc, map 185, day 4.** This is chain.md's A19, now with binary values ([emu] F7).
- Setup: flags G204, G120, G121, G122, G123; arrival through portal 183.6, then `PlaceHero(132, 296, ground)` (tile (5,18)); real
  prefabs.
- Red today: fails in its limit naming `slot 2 program @452: last 0x0B @506`. Septimus at (15073280, 25821184) = (230.0, 394.0); T70
  never set; control held.
- Green after the fix, in this order:
  - hero `0x24 @95` ends at PosX 6946816 (106.0);
  - Septimus `0x24 @458` ends at PosY 21626880 (330.0), last contact rec7, which stands at (7864320, 22544384);
  - `0x24 @463` ends at PosX 11927552 (182.0);
  - rec7 destroyed by `@466`;
  - `0x0B @506` ends with PosY >= 22413312 (342.0); [emu] 22446080 (342.5), with and without the D-E19-13 lag;
  - T10, T30, T40, T50, T60, T70 set in this order;
  - `0x53 @147` → 362, arrival (16515072, 17301504, 2097152), effect 4 (chain.md F20).
- Tolerance: ±3 units on the two contacts (DLL bisection in float).

**T-A10v and T-ARC-B9, map 10.** As in collision.md §6, with collision-check's corrections:
- DLL pins under D-E19-13: Nestus 33914880 (517.5), Meade at rest 32997376 (503.5);
- B9 completion = T515, `@1234`, Septimus `@3693`/`@3725`/`@3731`/`@3734`, G482, `0x11 @3738`;
- the seven B[2] actors present.
- Add for this census: `@3734` must end, which proves that rec41 left the obstacle set (slot recycling).

**T-C61, arc, map 61 B[6] cart ride.** Relational pins on each rail contact [model]; the hero box is (-10,-7; 21,15), the block box
(-12,-8; 24,16).
- `0x24 @959` (west) ends at PosX 17956864 (274.0), touching rec34 at (252,696). rec34 is activated by `0x2D @951` at its record
  position.
- `0x24 @964` (south) ends at PosY 50855936 (776.0), touching rec35 at (276,792).
- `0x64 @965` puts rec34 at (156,776,32). `0x24 @977` (west) ends at PosX 11665408 (178.0).
- `0x1E @980` ends (|dY| >= 64).
- Red today: `@980` never ends; the hero is at (82.0, 824.0), cell (3,52) [model].

**T-C384, map 384, if 0x28-0x2B are ported.**
- Unit:
  - `2B` on the logic entity: Flags bit 1 cleared, controller mask 0x1040 → 0x40;
  - `29`: bit 8 cleared, 0x41 → 0x40;
  - `28` / `2A`: the inverse;
  - size 1; the owner is untouched when the logic entity is another.
- Arc: rec0 Muruta C[1] (record (58,105) → (708, 848)), `0x2B @320`, dir 8, `0x1E [168] @325`.
  - Red today: it never ends; the Muruta is at (708,848), cell (29,53) = 0x1000.
  - Green: it ends with |dX| >= 168, then `0x0D @330` opens.

**T-NEW, before the fix merges.** Emulate each of the 7 new-block sites and the 13 graveyard starts in the binary loop, with
`emu.py` extended: 52 `@1576`, 199 `@835`, 346 `@943`, 426 `@668`, 61 `@937`/`@998`, 62 `@1006`, 10/331 `@4205`-`@4409`.
- If the binary also blocks, the site moves to "blocked in both". The DLL's current pass is then the deviation, to put to the
  author.
- If the binary passes, the census model is wrong there, and the arc pins the binary value.
- No value can be written before that emulation.

**Regression.** Re-run every existing pin: A0-A9, A5r, the cabin, the sailor 12, the traces, and the E12.d interaction tests.
Any moved value stops the slice (§5 of the plan).

## 7. Risks

- **Static model limits.** Starts are candidates; every branch is taken as possible; loops are re-entered (59 sites flagged). The
  census ignores:
  - cell writes (0x54/0x55/0x85; 11 "blocked in both" flagged);
  - jumps other than the 4 certified (25 flagged);
  - native AI (E14);
  - gravity toggles (0x16/0x17/0x62/0x63 on 0x100) and NoObstacleSlide toggles (0x45/0x46);
  - D-E19-13 (spatially neutral);
  - Z beyond ground plus record height.
  The slide is modelled only for cardinal directions; the 0x8B spawn offsets are read as signed without verification. Map 292 is
  not stable after 4 passes (6 sites).
- **Cross-program timing.** Under universal blocking, 522 "passes in both" walks can meet a standing record at some time (F13),
  faithful to the original but timing-dependent. Arcs, not the census, decide them.
- **The fix adds blocks** (F11): 7 sites, plus 13 graveyard starts under held control on 10/331. The fix must not merge without
  T-NEW.
- **A naive fix** (no slot recycling for FlagToDestroy) leaves rec41 blocking after B[9]: `@3734` stalls (model, and collision-check's
  emulation).
- **The DLL detour** has a clean grid path on 21 DLL-only sites ("det:clean": 20 rule, 1 class-op). On those the DLL may
  finish the walk by detour, so the census overstates their DLL block.
- **The 0x1E key** (F2) is equivalent only while `Parameters[1]` is reset after each instruction; any future change there breaks the
  equivalence.

## 8. Author questions (product / authority)

1. **0x28-0x2B.** Port them in E19.d2 next to the entity fix (+13 sites, +39 downstream; Muruta cutscenes on 2 and 384, Coal Mine
   carts; none on a story map), or leave them in E19.l?
2. **The corner slide (13 hero/NPC sites + 12 partial, none on a mandatory chain program).** Keep it in E19.h (D-E19-9), or move it
   forward?
3. **New blocks the fix adds** (7 + 13 starts, none on a story map). Make T-NEW (binary emulation of each) a stop before the merge of
   the fix, or accept them as known risks with arcs later?
4. **Reproducibility** (P4 of E19.d). Should the census scripts (c0b/geo/rep) become a repository tool with pinned totals, or stay in
   the scratchpad?

## 9. Technical unknowns

- Presence and timing of cross-program obstacles anywhere except map 10 B[9]/B[14] and 185 (emulated).
- The real start of the sites with several candidates (172 "same outcome"); 363 `@247` is one.
- The cross-program indicator (ORIGMAX/FIXMAX) was not re-run on the final model.
- Whether the binary blocks at the 7 + 13 new-block sites (T-NEW).
- Coal Mine cart mechanics: hero animation 55, any carrier, exact contact sub-pixels. They decide 61 `@937`/`@998` and 62 `@1006`.
- The DLL detour's real outcome where a grid path exists (20 sites), and the lock of map 62 B[5] (T190 gate).
- Native AI (E14) moving C-index-native records that S2 excludes; the 0x8B offset sign (164 rec4).

## 10. Scripts and outputs (`scratchpad/e19d2-disc/census0b/`)

- Copied model: `census_exec_orig.py`, `census_v2_orig.py`, `m.py`, `gstatic.pkl`.
- Census and geometry: `c0b.py` (census; MODE / OBST / CLASSOPS / JUMPS), `geo.py` (walk geometry for the three rules).
- Patches to c0b: `patch_cert.py`, `patch_z.py`, `patch_class.py`, `patch_jump.py`.
- Obstacle and flag inputs: `cloud.py` (time-free cloud), `tags.py`, `cellw.py` (cell writes), `detour.py` (DLL detour).
- Comparison and inspection: `rep.py` (comparison → `table.tsv`, `table.pkl`, `totals.txt`), `show.py` (a site in every run, with
  provenance).
- Emulation and binary: `scene185.py`, `emu.py` and `mlib.py` (copies of collision-check's), `jdis.py` (binary disassembly).
- Raw run outputs: `rows_*.pkl`, `prov_*.pkl`, `run_*.log`. Listings: `d334.txt`, `d363.txt`, `d365.txt`, `d366.txt`, `d61.txt`.
