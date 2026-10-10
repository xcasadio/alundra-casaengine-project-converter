> Report returned by one agent of the read-only discovery for E20.i, swimming (night of 2026-10-10), kept verbatim as evidence. The plan holds the reconciled facts.

Swim traces are done: the 80-entry slope-4 table, the dive, landing and leaving the water, walls, the charge counter and hurt in water all ran against the real `ALUN_CD.EXE` code. The extended harness leaves the D1 gate and every existing run trace unchanged. Nothing was built and the repository was not written to. `git status` shows ` m CasaEngineMonogame` and ` ? alundra-datas-analyser`, which I did not touch.

# Things that change the plan

1. **The slope-4 branch is a switch over all 80 animation ids, not five states.**
   - **FACT** (decoded at `0x8003260C`, table `0x80022E7C`, and matched by the W8 sweep of all 80 start states):
     - It zeroes `[0x80126FFC]` (charge) and `[0x80127110]` (shot pointer) and calls `UpdatePlayerCarriedEntity(2)`, then looks the target animation up in the table.
     - `0x0F` and `0x1D` go to handler `0x80032690`. It does, in order: write `TargetDirection = s1`, `TryUseItem` (0 ends the tick), then `CheckEntityInteraction`. A result of 1 ends the tick unchanged and 2 sets `0x1D`. Otherwise `pressed & 0xD0` gives `0x28`, a non-zero nibble gives `0x0F`, else `0x1D`.
     - `0x28` and `0x3A` run `TryUseItem` only. `0x3B` waits for `+0xAC == 1`, then flips the direction and goes to `0x1D`.
     - `0x1C`, `0x31` and `0x3E` flip the direction by `0x10`, then go to `0x1D`.
     - 15 states change nothing and only cancel the item (`0x800350C0`): `0x0D 1A 1B 1E 27 37 38 3D 43 45 47 48 4A 4C 4D`.
     - Every other state, jump and attack states included, goes to `0x1D` with no direction write.
   - The DLL needs the whole switch, not the five named states.
   - **FACT** (land side, `0x80031DB4`/`0x80031DD0`): `0x0F`, `0x1D`, `0x28`, `0x3A` and `0x3B` on slope ≠ 4 become Idle on the ground or `0x2D` in the air. `0x3A` and `0x3B` flip the direction first.

2. **Every land-to-swim transition costs one `0x1D` tick, and a dive press on that tick is lost.**
   - Walking in from (320,100) with boots level 2: slope 4 is recorded at the end of tick 12. Tick 13 is `0x1D`, tick 14 is `0x0F`. A Triangle press at tick 13 does nothing; at tick 14 it dives.
   - A press on a tick when slope is still 0 acts as the land rule, so the run preparation `0x29` starts for one tick.
   - Momentum carries over. Walk-in at 159744 becomes 139776 (tick 21, `0x1D`), then 137952 (tick 22, `0x0F`), then falls 1824 per tick to 110592.
   - Run-in (`R9-deepwater-boots2`): tick 89 is `0x03` at 258048, tick 90 is `0x1D` at 225792, tick 91 is `0x0F` at 218592. Triangle held neither runs nor dives.

3. **E20.d's deep-water guard differs from the binary in the forces.**
   - The binary gives `0x1D` with 225792 at tick 90.
   - **INFERENCE:** the DLL guard gives Idle, whose shift-1 step would give about 129024.
   - Three E20.d1 tests pin the guard and must be replaced: `AlundraHeroRunStatesTests.DeepWater_EveryStateOfTheRun_GoesToIdle…` (line 490), `DeepWater_TheTailDoesNotStartTheRun…` (503), and the two `G9_OnDeepWater…` / `G9_ATriangleOnDeepWater…` tests in `AlundraHeroRunTests` (685, 703).
   - `R9-deepwater-boots2` and `R9-allwater-*` become acceptance traces for the real branch.

4. **Swim motion constants.** All come from the oracle, using the generic physics already in the DLL.

   | State | Speed | Shift | X (cardinal) | Y (cardinal) | Diagonal | Duration |
   |---|---|---|---|---|---|---|
   | `0x0F` | 144 | 4 | 110592 | 73728 | 78192 / 52128 | 42 ticks, chains to itself |
   | `0x1D` | 0 | 3 | decel step \|F\|>>3 | | | 36-tick loop |
   | `0x28` | 176 | 0 | 135168 | 90112 | 95568 / 63712 | 42 ticks, chains to `0x1D` |

   - `0x0F` ramps 6912 per tick on X from rest, 4608 on Y, 4887/3258 on the diagonals.
   - `0x1D` from 110592 takes 13824 per tick, so 8 ticks to zero.
   - `0x28` reaches its full force on the press tick and holds it for all 42 ticks, about 86.6 px.
   - `dir_target` values are U `0x10`, R `0x18`, D `0x00`, L `0x08`, UR `0x14`, DR `0x1C`, DL `0x04`, UL `0x0C`.
   - Boots level 3 gives hero columns identical to level 2 over 90 ticks.
   - Z stays on the floor: 0.00 in the lake, 32.00 → 0.00 after a ledge fall.

5. **The dive.** FACT, from W3.
   - Triangle, Cross and Square edges (`pressed & 0xD0`) all dive; the three traces are identical. Circle only plays sound 3 (the kit has no item).
   - Triangle held without an edge never dives. A held button does not re-dive when the dive ends.
   - T+X on the same tick gives one dive.
   - The dive direction is `s1` read at the press tick. With no direction, or with Left+Right or Up+Down (nibble `0xA` or `0x5`), it keeps the previous `TargetDirection` (`W3-dive-dir-none`, `W10-dive-LR`).
   - Nothing is read during the dive: steering and presses are ignored, and `dir_target` stays `0x18` until tick 52.
   - Surfacing: the target is `0x1D` at press+42, with F = 118272. At +43 it is `0x0F` if a direction is held, else `0x1D` decelerating.
   - A press on the surfacing tick is lost; the next tick works. Taps every 5 ticks give dives at 10–51 and 55–96.
   - Sound 21 plays at the start. The ripple effect 6 is created whenever `+0x1C & 7 == 0` (ticks ≡ 1 mod 8 in the traces), by handler `0x8002FFE4` (T2 entry for `0x28`).
     - Position: (X, Y, `+0x134`).
     - Velocity: `(-ForceX)/4` and `(-ForceY)/4`, truncated toward zero (decoded; the oracle journals only the creation).
     - The phase is global, not tied to the dive start.

6. **Effects and sounds in water.** FACT.
   - Walking or running in is silent: only the sound-0 (no-sound) entries for `0x1D` and sound 22 for `0x0F`.
   - Sound 22 replays on every self-chain of `0x0F` (ticks 5, 47, 89, 131, 173 in `W2-long-R`). The DLL must reload the animation and replay its sfx on a same-id chain.
   - `0x0F` and `0x1D` create no effects.
   - A jump or fall into the water lands at slope 4. The `0x1D` tick after it (e.g. `W1-jump-s03` tick 24) carries sound 21 and effect 6 at (X, Y, floor). This is the E20.e landing handler `0x80030224-0x800303CC`, water branch. On dry land the same code gives sound 11 and effect 7.

7. **Weapon state in water.** FACT, from W6 with the holy sword and boots 0x1B.
   - The charge counter is zeroed on the first swim-branch tick. In `W6-walk-square-held` slope 4 appears at tick 83, the counter reads 59, 60, 60, then 0 from tick 84, with Square still held.
   - Square held in water never counts; the counter stays 0.
   - The shot pointer `[0x80127110]` is zeroed every tick.
   - `UpdatePlayerWeaponEffect`, `UpdateWeaponStepProgression` and `UpdatePlayerCarriedEntity(0)` are not called on slope 4. A poked aura pointer `[0x80126FF8]` stays non-null in water, but on land the same poke is cleaned within the tick.
   - Leaving the water with Square held, the counter restarts from 0 on land (13 at tick 100, reaching 60). No attack fires.
   - `UpdatePlayerCarriedEntity(2)` (decoded) copies the hero's direction and position (Z + 32 px) to the carried entity, sets its `+0x24 = -1`, and clears `[0x80098F28]`. It is a no-op when nothing is carried; E20.g owns the rest.

8. **Leaving the water is purely geometric.**
   - The swim branch needs all four box corners on deep-water cells at the hero's level; otherwise slope is 0 and the land rules apply.
     - At the shore the hero walks as soon as one corner is out (`W10-channel-edge`: Idle then Moving).
     - A 24 px channel is swum when the box is inside one column (`W10-channel-down`).
   - Swimming Left out of the lake, slope drops at tick 103 (x 345.59). Tick 104 is Idle with F = −55296 (halved), tick 105 is Moving at −107520, tick 106 at −159744.
   - With Triangle held, the run starts at once: `0x00` at 122, `0x29` for 25 ticks, `0x03` from 148.
   - A dive in progress is cut: `0x28` at 5–31, then `0x00` at 32 (`W4-leave-dive-s05`).
   - A bank 16 px or 32 px higher is a wall for the swimmer (x stays 346.00, state stays `0x0F`, Cross dive included).
   - Real shore geometry is not in the oracle (flat synthetic field): UNKNOWN.

9. **Walls in water.** FACT, from W5. There is no bounce state.
   - Wall at x 720: `ForceAdjusted` first set at tick 78, x stays 709.00, `0x0F` stays with F = 110592.
   - Diagonal into the wall slides (x clamped at 709.00, y keeps moving, `ForceAdjusted` stays 0).
   - A dive into the wall continues to its end.
   - Map edges: x stops at 1237.00, y at 7.00.

10. **Hurt in water.** FACT. This is information for c2b; the entry is the real `0x80031A68`.
    - From a hurt at tick T: `0x3A` runs T..T+3 (F = −196608 each tick, sound 23 at T), `0x3B` runs T+4..T+12 (F −184320, then −12288 per tick), `0x1D` at T+13.
    - The direction flips to `0x08` and is restored to `0x18` at T+13. The pad is ignored throughout. A Triangle or Cross press at tick 5 or 9 (inside `0x3A`/`0x3B`) is dropped, and one at tick 20 dives. Boots levels 2 and 3 give identical traces.
    - A hurt during a dive interrupts it: `0x28` at 45–80, `0x3A` at 81 (`W7-hurt-dive-s45`).
    - Hurt at the shore (slope 0) gives `0x31` for 4 ticks then `0x1C` for 11, then Idle (`W7-hurt-shore`).
    - A knockback that crosses the shore cuts the hurt: `0x3A` 2–4, slope 0, then Idle with F −98304 and the direction restored (`W7-hurt-leaving-x352`). The hero stopped at x 341.5.
    - A kept contact re-hurts every 120 ticks (ticks 2, 122, 242). E20.i must implement `0x3A`/`0x3B` or the hero sticks in `0x3B`; D-E20-23 stays as written until then.

# What E20.i needs from the deep-water wall (O-E19-22), without porting it

- **FACT** (decoded at `0x80037504`–`0x80037560`, and run):
  - The rule is `boots level < 2 && (cell & 0xE00) == 0x800`, applied per destination corner cell with no Z condition.
  - A level-0 or level-1 hero is stopped at x = 325.00 whether walking in (`ForceAdjusted` at tick 12), running in (bounce at 87), jumping over (stopped in the air, `W1-jump-L0`) or walking off a ledge toward the water (`W1-fall-walk-L0`).
  - A level-0 hero already inside the lake cannot move at all: x stays 624.00, `ForceAdjusted` from tick 5 (`W2-L0-inside-R`).
  - Level 0 and level 1 behave identically.
- **E20.i's own branch needs nothing from the wall.** Its correctness only needs a slope-4 probe (min over four corners at the hero's level, otherwise 0), which is in the DLL as `Slope_18c`. I did not read the probe's rule in the DLL source.
- **Consequence if the wall is not ported:** after E20.i, the new-game hero (boots level 0) will swim in lakes such as the 416 (399 water cells per the plan), where the original blocks him. E20.i's recipe then only matches the original for a hero who owns item `0x1B` or `0x1C`. Whether any test save owns one is UNKNOWN; I did not check.
- **INFERENCE:** `AlundraCellsCollisionField.WalkabilityMaskFor` returns a bit mask (`0x40`, `0x41`, `0x1000`) tested with `& != 0`. The wall is an equality on `0xE00` plus a boots condition. Bit `0x800` is also set for slopes 5–7, and the ladder cell is `0xC00`, so adding `0x800` to the mask would also wall ladders. A dedicated slope-4 test (in `TrySampleGround`'s caller or `BlockedCorners` in `AlundraScriptedMotion.cs`) is needed.
- **Q-N3 / Q-D2:** E20.i's row does not contain the wall, so unless the author's "collisions" covers it, O-E19-22 still has no owner. Since E20.i's swim only matches the original with boots, I suggest it carries an explicit question at its T0 and states the deviation if it is deferred.

# Harness and verification

- **New files only**, in my copy of the run-oracle tree:
  - `base/sscen.py` (swim scenarios, groups W1–W10)
  - `base/sverify.py` (non-regression proof)
  - `base/sindex.py` (index and manifest)
  - No existing file was edited. The only in-process change is extra `hworld.INV` entries (`holy_b1b`, `item1F`…`item32`).
- **Second interpreter:** 382 runs and 15,196 ticks (`--cross`), 0 mismatches against the second interpreter. Uninitialised-read sites: 0 in every group.
- **Determinism:** W4, W5 and W10 re-run give an identical manifest (785 files).
- **D1 gate, in the process that imported `sscen`:** `out-verify/gate/comparison.txt` shows S-A and S-B each at "216 values compared, 0 differences", OVERALL PASS. The traces' md5s equal the baseline (S-A `1ae0cc02…`, S-B `975c342a…`). The unmodified copy also passes.
- **Existing traces:** after importing `sscen`, rscen G1–G10 (with `--cross`) were regenerated into `out-verify/`. All 549 files of the reference manifest compared identical, with 0 different, 0 missing, 0 extra. That covers `g*/` traces, `summary-G*`, `check/xcheck-G*`, `check/cov-G*` and the gate files.
- **`sverify.py` ran an earlier version of `sscen.py`.** It imported it before I added W10 and the last two W7 scenarios; the import-time effects are unchanged.
- **The D1 gate cannot be replayed in place from the repository annex.**
  - The annex's `compare.py` resolves the D1 values document to `docs/plan-e20-annexe/plan-e19-d2c-valeurs.md`, which does not exist (the file is `docs/plan-e19-d2c-valeurs.md`).
  - `rgate.py` reads `oracle/d1/out/`, which the annex does not contain.
  - I replayed it in a scratch tree with the missing document and D1 baseline traces copied to the paths the scripts expect.

# Trace locations

Scratch root, `S` below:
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20i/swim-oracle/`

`O` = `S`tree/docs/plan-e20-annexe/e20-d/oracle/ (so `Obase/` is the harness, not `Ooutput`)

- **Traces:** `Oout-swim/w1…w10/<scenario>.txt` and `.json`. These use the same columns and JSON format as the run oracle, so the existing `RunOracleTrace` reader should load them.
  - Columns: state, anim target/current, direction and facing, forces X/Y/Z, position including Z, ground, `ForceAdjusted`, `collZ`, slope and `VramOR`, sprint and charge counters, sounds, effects.
- **Indexes and summaries:** `Oout-swim/INDEX.txt` (382 lines with start, pad pieces, layout, inventory), `summary-W1…W10.txt`, `manifest-swim.txt`, `check/xcheck-W*.txt`.
- **Decode and query helpers:** `Saux/` (`swimtab.py`, `fxtab.py`, `q.py`, `mdis.py`, `herodata.py`).
- **Replay** (`python -B`, from `S`tree): `docs/plan-e20-annexe/e20-d/oracle/base/sscen.py [W1…W10] [--cross]`.

Groups, their inputs (pads: U R D L = d-pad, T O X S = buttons) and the traces behind each claim above:

- **W1, entering.**
  - `W1-walk-L{0..3}`: Right from (300,100).
  - `W1-run-L{0..3}`: T+R from (100,100).
  - `W1-jump-s{03..15}`: Right plus a Cross tap at tick s.
  - `W1-fall-walk`, `W1-fall-run`, `W1-fall16-walk`: ledge into the lake.
  - `W1-fall-walk-L0/L1`, `W1-jump-L0/L1`: information.
- **W2, swimming.**
  - `W2-idle`; `W2-dir-{U,R,D,L,UR,DR,DL,UL}` (direction held from tick 5 to 70); `W2-turn`; `W2-L3-R`; `W2-L0/L1-inside-R`; `W2-long-R`.
- **W3, dive.**
  - `W3-{T,X,S,O}-{idle,swim}`; `W3-T-held`; `W3-T-taps5`; `W3-dive-dir-*`; `W3-dive-steer`; `W3-dive-end-*`; `W3-TX-same-tick`; `W3-X-during-dive`; `W3-T-near-end±`; `W3-enter-T±`.
- **W4, leaving.**
  - `W4-leave-L`, `-release`, `-T`, `-T-late`, `W4-leave-dive-s*`, `W4-leave-UL`, `W4-bank16`, `W4-bank32`.
- **W5, walls.**
  - `W5-wall-R`, `W5-wall-UR`, `W5-wall-dive`, `W5-edge-E`, `W5-edge-N`, `W5-L0/L1-wall-walk`, `W5-L0-wall-diag`.
- **W6, weapon state.**
  - `W6-walk-square-held`, `W6-allwater-square-held`, `W6-leave-water-square-held`, `W6-poke-state`, `W6-poke-state-land`, `W6-charge-then-water`.
- **W7, hurt.**
  - `W7-hurt-idle-L2/L3`, `W7-hurt-swim`, `W7-hurt-dive-s*`, `W7-hurt-shore`, `W7-hurt-leaving-x352`/`x346`, `W7-hurt-keep`, `W7-hurt-press-T/X`.
- **W8, the table.**
  - `w8/W8-s<id>-{idle,R,T}`, 240 three-tick runs.
- **W9, Circle and land side.**
  - `W9-circle-*`, `W9-item1F…32`, `W9-land-s*`.
- **W10, extras.**
  - `W10-LR-UD`, `W10-dive-LR`, `W10-channel-*`, `W10-shallow-to-deep`, `W10-shallow-to-deep-L0`.

Key excerpts (ForceX, tick after the pad change):

```
W3-T-swim  (Right from 5, T at 20): 0F 5-19 (F 96768 at 19) | 28 at 20-61, F 135168, snd21 at 20, fx6 at 25,33,41,49,57 | 1D at 62 | 0F from 63
W3-enter-T+1 (T at tick 13): 01 1-12 | 1D at 13 (press lost) | 0F 14-60      W3-enter-T+2 (tick 14): 1D 13 | 28 14-55
W4-leave-L: 103 slope 0 F -110592 | 104 00 F -55296 snd0 | 105 01 F -107520 snd0 | 106 F -159744
W5-wall-R: ForceAdjusted at 78, x 709.00, 0F kept, F 110592
W7-hurt-idle-L2: 3A 2-5 (snd23 at 2, F -196608) | 3B 6-14 (F -184320, -172032 …) | 1D at 15, dir 08 → 18
W6-walk-square-held: slope 4 at 83, charge 59, 60, 60, 0, 0 … ; aura 0x801E8000 unchanged through the entry
```

# UNKNOWN

- Real shore geometry and water-cell heights relative to the banks, and the 416 lake itself. The oracle uses flat synthetic fields.
- `CheckEntityInteraction` results 1 and 2 in water. The code is decoded but no interactable entity was available in the oracle.
- `TryUseItem` returning 0 in water. I put each of the 20 Circle items `0x1F`–`0x32` in the oracle's inventories, but every one answered with sound 3 only and no state change (cause not investigated), so item use in water is untested; E20.h owns it.
- Whether the carried entity pointer `hero.+0x2C` is cleared after `UpdatePlayerCarriedEntity(2)`. I saw no store to it in `0x8002FAB4`–`0x8002FAF0`.
- The ripple's velocity fields come from the disassembly only. The oracle's effect stub does not record them.
