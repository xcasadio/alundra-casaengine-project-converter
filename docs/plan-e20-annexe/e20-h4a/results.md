# E20.h4a T1 - results (tests, code, retirement proofs, mutants)

Starting HEAD `0fec1feb` (branch `chantier/theme2-attaques-objets`). Plan: `docs/plan-e20-heros.md` section 1.31.1 (revision 2, READY). The T0 results are in `README.md`. Commits of this pass:
`609d7495` (tests, with inert signatures) and `c8e436ec` (the code). This file is the third (docs) commit.

## 1. What was written

- `Alundra.Tests/Hero/TomeBench.cs`: the bench (map 14 through `ArcRun`, real prefabs, real controller and exported animations of the hero, `SyntheticField` flat ground, every other entity removed, sounds told to a
  `RecordingSoundPlayer`, one tick per frame).
- `Alundra.Tests/Hero/TomeStartTests.cs`: (t1) start, refusals in the binary's order, the staff; (t2) `DecreaseMp`; (t5) the promotion; (t6) the calendar; (o2) the four cases of the staff through the frame.
- `Alundra.Tests/Hero/TomeStatesTests.cs`: (t3a) the states, (t3b) the Circle during a tome, (t4) the guard of the slope.
- `Alundra.Tests/Hero/TomeOracleTests.cs`: (o1) the eight traces `W9-item2B` to `W9-item32` (`RunMontage`, compared field by field with the oracle, the sound 3 at the tick 10 once), (o3) the beginning of `trace_cast.py 0x2B`.
- `Alundra/Hero/PlayerManager.cs`: `TomeStart` (`0x800348E0`), `PayForTome`, `StartTomeTrack` (the journalled stub of `0x8005A9E0`, seam `TomeTrackCallsForTests`), `DecreaseMp` (`0x8004DF3C`, through `SetPlayerMp`),
  `TomeCastTick` (`0x800325A4`, states `0x32` and `0x33`), the `0x34` branch (`0x80032680`, `TryUseItem` only), `TomeSlopeGuard` (`0x800351B8`). The two branches of the states sit in the main chain of `MovePlayer`
  right before the D-H-5 test of the default. **Inert in production**: the tome entries of the table of the starts are still the stubs of h0a (the tests install `TomeStart` by `ItemStartForTests`).

## 2. Measured facts the tests pin

- The animations of the exported hero: with `0x32` posed before the tick 1, `0x33` shows at the tick 33 (the ticks 1 to 32 are `0x32`); with `0x34` posed, Idle shows at the tick 27 (1 to 26 are `0x34`). In the real start
  (o3) the press is the tick 1, `0x32` shows on the ticks 1 to 32 and `0x33` at the tick 33 with the counter 32: the oracle's `trace_cast_2B.txt` rows (tick 1 counter 0, tick 33 counter 32) exactly.
- The magic: `Mp` 4 to 3 at the start with the sword; the staff (`Give(7)`, `Equip(6)`, weapon id 7): 0 to 0 and 2 to 2; the sword at 0: sound 3, 0 to 0; the sword at 2: 2 to 1. The four lines of `staff_check.txt`.
- The tracks logged: 0, 1, 6, 6, 2, 3, 4, 5 for `0x2B` to `0x32`.
- The driver is first called at the tick after the start with the counter 0 (t6).
- The promotion (t5): the start receives 43 at the first press (the Circle becomes 44), 44 when one tick passed first (track 1).
- One bench fact for later tests: two Circle presses on consecutive frames are one edge for the controller (it reads the edge across frames); a test that presses twice lets go for one tick between.

## 3. Red before the code, green after

At `609d7495` the production code only had inert signatures (`TomeStart` answers 1, `DecreaseMp` returns the magic, `TomeSlopeGuard` answers 0, no handler). The 69 tests of the three classes: **59 red, 10 green**.
The 10 green are the negative witnesses of pairs (allowed by the plan): the three `T4` cases that answer 0 in the cast (`slope 0` with `0x32`, `0x33`; `slope 1` with `0x33`), the four `T3a` held cases
(`0x32`/`0x33` held with a tome in progress, bounds `0x2B`, `0x32`), and the three `T3a` pad cases (the hero in `0x32`, `0x33`, `0x34` does not move: no handler, no motion). The 59 red, by test:
O1 x8, O3, O2 x4, T1 start x8, T1 animation x4, T1 in use x2, T1 no magic, T1 hands, T1 staff x4, T2, T5, T6, T3a x13 (cast, end x3, held x7, freed x2), T3b x3, T4 x7.
After the code (`c8e436ec`): **69 green** (the Release run of the three classes), then the full suite.

Existing tests moved: **none** (predicted list: empty). The two sentinels of T0 did not move: `HeroItemUseTests.A6_*` (state `0x32u` in its theory) and the `A7` lists (51 + 17 states, unchanged).

## 4. Suites and guards

| Run | Result |
|---|---|
| Release, copy neutralised (`skipcopy.targets`), `--blame-hang-timeout 60s`, whole suite | **7 605 passed, 0 failed**, 17 min 57 s (7 536 + the 69 new tests) |
| Debug, whole suite, last (deposits the DLL) | **7 605 passed, 0 failed**, 20 min 4 s |
| `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `.pdb` against `alundra-project/` | identical |
| `git diff --ignore-cr-at-eol --quiet -- docs/` after each suite | clean; the four `docs/hero-trace-389-*.txt` restored by `git checkout`, never indexed |
| `python -B tools/dll-structure/check_names.py --rules` | 0 findings, 1 pre-existing warning (`TextBoxGpu.cs:266`) |
| Engine, converter, data | no diff (the submodule entries `CasaEngineMonogame` and `alundra-datas-analyser` were already shown modified by `git status` before this pass and were not touched) |

(The two full suites ran over 10 minutes; the shell moved each to the background on its own timeout, and the result was read from the output file once finished.)

## 5. Proof of retirement (plan, acceptance, revision 1)

Real mutations by script (`PlayerManager.cs` restored by `git checkout`, `git diff` empty after), the three classes re-run each time.

- **Start function retired** (`TomeStart` returns 1 at once): 38 red = O1 x8, O3, O2 x4, T1 x20, T5, T6, T3b x3. This is **exactly** (t1), (t3b), (t5), (t6), (o1), (o2), (o3). (t2), (t3a) and (t4) stay green.
- **Handlers of `0x32` to `0x34` retired**: 13 red = T3a x10, T3b x3. This is **exactly** (t3a) and (t3b). (o3) stays green (`0x32` still chains to `0x33` by the animation, as the plan said).

## 6. Mutants (18 applied, 18 killed; the plan asks for at least 12)

| Mutation | Red tests (kill) |
|---|---|
| start function retired | see section 5 |
| handlers retired | see section 5 |
| test of the staff removed | T1 staff x2, O2 x2 |
| the staff pays one magic | T1 staff, O2 |
| the staff needs magic | T1 staff, O2 |
| magic not taken (non-staff) | T1 start x8, T1 refusals x8, T1 staff, O2, O3, T3b x3 |
| refusal at 0 magic removed (D1) | T1 no magic, T1 staff, O2 |
| animation bound `< 3` | T1 animation (state 2) |
| object in the hands ignored | T1 hands |
| item in use ignored | T1 in use x2 |
| tracks shifted | T1 start x7, T1 staff x3, T5 |
| `0x33` left while a tome is in progress | T3a x9, O3, T3b x2 |
| exit to Idle in the air (instead of `0x2D`) | T3a freed x2 |
| `0x34` runs the `0x32` tick (leaves on release) | T3a end x2 |
| `0x34` reads the pad (direction written) | T3a pad |
| guard at 0 in deep water | T4 x2 |
| guard accepts `0x34` | T4 |
| `DecreaseMp` unbounded | T2 |

## 7. Deviations from the plan

1. **Inert signatures in the tests commit.** Tests that compile against symbols that do not exist cannot be committed buildable, so `609d7495` carries `TomeStart`, `DecreaseMp`, `TomeSlopeGuard` and the track journal as trivial
   bodies; the real code is `c8e436ec`. The red run was made on that state.
2. **T3a counts.** The plan wrote "`0x33` 32 ticks later" and "`0x34` held 26 ticks": with the state posed before the tick 1 the animations give `0x33` at the 33rd tick and Idle at the 27th (ticks 1 to 32 and 1 to 26 held),
   which is the same duration the oracle shows for a start at the tick 1 (o3: `0x33` at the tick 33). The constants of the tests are 33 and 27 for that reason.
3. **One test fix in the code commit** (`c8e436ec`): the control of (t3b) lets the Circle go for one tick before the second press (the controller reads the edge across frames).
4. **Not done here** (T2/T3 of the plan, not this pass): the ADR, the plan update, the two fresh verifiers. The mutants of section 6 were run as a check of the tests, not as the T3 closing mutants.
