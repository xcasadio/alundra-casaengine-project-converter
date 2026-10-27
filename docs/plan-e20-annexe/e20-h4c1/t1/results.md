# E20.h4c1 T1 (phase A) - results (annex)

Run at the HEAD `8b6e2ec2` (branch `chantier/theme2-attaques-objets`, 2026-10-27). Plan: `docs/plan-e20-heros.md` section 1.31.3 (revision 2, READY), T1; D-H4C1-1 to D-H4C1-3. Two commits: the tests with a compiling skeleton of `Alundra/Ai/Items/EarthTome1Ai.cs`
(`ad17de6f`), then the code. Nothing under `CasaEngineMonogame/`, `alundra-datas-analyser/`, `alundra-project/` (but the Debug DLL deposit), `data-extracted/`, `tools/` was changed. The phase A is INERT in production: `EarthTome1Ai` has no
caller outside the tests (the entries of the tome in `PlayerManager` and the call in `NativeSpriteRegistry.CreateForProduction` are the phase B, T2).

## 1. What was written

| File | Content |
|---|---|
| `Alundra/Ai/Items/EarthTome1Ai.cs` (new) | `SpriteTypes = {16}`, `ImpactCode = 3`, `Keys`, `TableBytes` (the 194 bytes of `0x8002343C`) and `Table` (u16 tick, s16 speed of Z, s16 delta), `Driver(hero)` (the three phases), `ImpactDeactivate(entity)` (E3), `Cancel(hero)` (the neutral `0x800351A0`), `Register(registry)` (E3 for the type 16 only) |
| `Alundra.Tests/Ai/Items/EarthTome1AiTests.cs` | (t1), (d1) x3, (d2), (d3), (d4) x2, (e1) x2, (e2): 11 tests |
| `Alundra.Tests/Ai/Items/EarthTome1WorldTests.cs` | (w1) x2, (w2) x4, (w3) x4 + 1 + 2, (w4), (w5) x8, (w6) x4, (c1): 27 test cases |
| `Alundra.Tests/Ai/Items/EarthTome1Run.cs`, `EarthTome1Compare.cs`, `EarthTome1Key.cs`, `EarthTome1Vectors.cs` | the bench (`HitArena` + the synthetic field, one tick per frame, the three seams of h0a, E3 registered on the runner's registry only if its key is absent), the field-by-field comparison, the key and vector loaders |
| `Alundra.Tests/Support/AnnexFiles.cs` | `EarthTome1Annex(relative)` |
| `docs/plan-e20-annexe/e20-h4c1/t1/` | `dump_table.py` (reads the table from the France executable, SHA-1 checked), `table_8002343C.hex` (the 194 bytes), `table_8002343C.txt` (decoded), this file |

The expected values: the table of the executable; the 689 vectors of the driver (`disc/h4copy/vec/vec_driver_2B.json`), the 384 of the base directions 4, 12, 20, 28 (`t0/vec/vec_driver_2B_dirs.json`) and the 224 of E3 (`vec_e3.json`);
the keys `disc/keys/` (flat x5, deep water, wall column 9, 8 Beannoid, 4 interruptions) and `critic/work/fcopy/_crit_out/` (flat, band of slope 1, steps of 16 and 32 px, point-blank wall column 5).

## 2. Red before, green after

- Tests commit `ad17de6f` against the skeleton (every function inert, `Register` registers nothing, empty table): **38 of 38 tests red** (`red/` run of the scratchpad; `EarthTome1AiTests` 11, `EarthTome1WorldTests` 27 cases).
- With the code: **38 of 38 green** in Release (copy neutralised).
- Real mutants applied by script and undone (`git diff` identical after), the T3 list not yet run (T3 owns the 18 mutants): speed of the second group 1024 (27 tests red), delta sign (26), `& 31` removed (27), `<< 7` (29), phase 1 threshold 99 (22),
  guard of the phase 1 removed (5), E3 without `| 0x40` (26): all killed.
- No existing test moved: the suites are 7671 tests = 7633 before + 38 new, zero failure, in Release (copy neutralised, `-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets`) and in Debug (built last). The suites were run by namespace
  in chunks under 10 minutes (the whole suite takes about 17 minutes): Hero+Entities+Combat+Events 3451, Ai.Objects 634, Ai (the rest) 645, Arcs+UI+SaveGames+Rendering 1513, the rest 1428 in Release; the same chunks in Debug (3451, 634, 645, 2941).
  The traces `docs/hero-trace-389-*.txt` were rewritten by the suites (end of lines only: `git diff --ignore-cr-at-eol` empty) and restored.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding (the one warning is old: `TextBoxGpu.cs`).
- The test seams are reset by the tests (the first complete run, before the reset of `ItemStartForTests` and `ItemDriverForTests` in `EarthTome1AiTests`, made 22 tests of `BombHandsWorldTests` red by leaking the thread-static seams: fixed in the code commit).

## 3. What the world tests found (the bench vs the keys)

Once the bench's own mistakes were fixed (the birth tick of a recycled slot, the creation of the hit sparks, the anchor of the effect 6), (w1), (w2), the slope band, the flat key of the critic, (w4), the 8 (w5) and the 4 (w6) were **equal to the keys, every tick, every field**, except the points below.

- **Animation row and current animation** (D-H4C1-3): compared from the tick after the birth, read at the END of the frame (`FrameSyncPasses`); everything else is read at the inventory pass of the tick. No other difference.
- **Generator**: the draws of a tick are the key's `draws` plus the one step of the end of the frame (the arrows' rule): 1 step a tick on a flat field, 10 on a hit (9 + 1). The keys of this annex do not model the per-frame step, so the state of the generator is not compared.
- **Hits** (w5): the DLL creates the three sparks without the creation seam, so a hit is checked by the centre the hit pass writes (`HitSparksForTests`, x3, in pixels), the number of draws (9) and the cooldown 25 / the shooter slot / the place of the target; **the nine VALUES of the draws (the speeds of the sparks) are not compared** (a visual detail; the count of steps is).
- **Interruptions** (w6): the object, its phase, its counter, the rocks, the births and the sounds are compared; the hero after T is the injury of E20.c2b (R-EL-6): its animation, its place, its sound 23 (excluded from the compared sounds) are not.
- **Key `earth1_vs_beannoid_84_104`** is compared up to the tick 127: at 128 the key's Beannoid hurts the hero (9 draws, cooldown 0).
- **Edge of the map (P5)**: the three rocks born at (77, slot 12), (87, 3), (92, 14) are at the status 3 at 94, 103, 109 (17, 16, 17 ticks after their birth), as the key.

### (w3) against an obstacle: a gap between R-EL-5 as written and the measure (to be disposed)

R-EL-5 (2026-10-18) accepts the flag "blocked" one tick early (D-E20-38) and reads the chronology from the observed status 3. The four obstacle keys (column 9, point-blank column 5, steps of 16 and 32 px) show, besides that flag:

1. **The status 3 is at the key's tick or one tick before**: 10 lives of 32 in the point-blank wall, 2 in the step of 16 px, 4 in the step of 32 px, 0 in the column 9; never late. As R-EL-5.
2. **The position at the wall differs, for the step into the obstacle and the following one**: the binary searches the length of the step by halves on X and Y together (`0x80037730`), so the blocked axis stops a few raw units short of the wall and the free axis is scaled; the DLL goes to the contact on the blocked
   axis and keeps the full step on the free one, and slides one more step the tick after. Measured on the four keys: the first difference is always at S-2 (S = the observed status 3); on the blocked axis (X: the obstacles are columns) the difference is **5 raw units at most**; on the free axis (Y) it is
   between 1.07 and 1.8 times the free-flight force (never more than twice). This is the family kept by **D-E20-80** (Q-STEP, B: "la division conjointe en diagonale, la vérité entière de XY" and the `ForceAdjusted` of one tick are kept), which postdates R-EL-5 and the plan text of (w3); it is not in the R-EL-5 sentence.
3. **A rock stopped in the air by the obstacle goes on falling** in both, so the height, its force, the ground contact and the slope follow the tick, not the status 3; at the tick of the death they are frozen at the values of the tick before (in the key as in the DLL); the death is at S + 13 in both.

The test (w3) therefore reads each life as: the status 3 of the DLL at S, the key's at S + lead (lead 0 or 1); before S every field equal but the position from S-2 on and the flag of the wall at S-1 when lead is 1; from S on the DLL's tick against the key's tick + lead for every field but the position and the vertical set; the position within 8 raw units
on X (measured 5) and twice the free-flight force on Y. **This tolerance is the measure bounded by the D-E20-80 family, not a number of the plan**: it is reported to the main session as a deviation of the plan text to be confirmed (or turned into an explicit R-EL-5 revision).

## 4. Other deviations from the plan text

- D-H4C1-2: the "tables of h0a" are three ternaries (`StartItem`, `UpdateItemUseDriver`, `CancelItemMode`), not tables (T0 section 4); the phase B adds three branches. `EarthTome1Ai.Cancel` is the named no-op D-H4C1-2 asks for; it is already in the phase A so that (c1) and the bench use it.
- (d1) "le compteur et la libération par le chemin du tick": done by a separate test through the real tick of the hero (`TomeBench`), on one bench for all the vectors with the slope 0 and the animations 0, 0x32, 0x33, 0x34 (a vector with the slope 4 cannot be posed: the controller writes the slope).
- (c1) is red against the skeleton (the inert driver gives no rock); the plan's "vert avant et après" is read as before and after the phase B.
- (w1) to (w6) are green in phase A through the three seams of h0a (R-EL-4), E3 registered by the bench on the runner's registry only if the key is absent (the phase B registers it in production: the same tests then run on the production registration).
