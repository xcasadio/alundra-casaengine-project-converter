# E20.e0k5 T0: the versement, the oracle, the DLL probes and the predicted list of moved tests (2026-10-25)

HEAD at the start: `fb6704fb` (branch `chantier/theme2-attaques-objets`). Plan: `docs/plan-e20-heros.md` section 1.18.5 (revision 2, READY). No production
code and no engine file is changed by T0: the DLL probes and the temporary wiring below are throwaway and were reverted (`git diff` of `Alundra/` empty).

## (a) The versement and the keys

- Part A of `scratchpad/t2-proj/` is versed to `docs/plan-e20-annexe/e20-e0k5-disc/` (README, `SHA1SUMS.txt`, report and critic, the `pb_*` scripts,
  `out/`, `out/keys/`, the critic's sweep and halving model). No copy of an annex, no `__pycache__`, no part B file.
- Baseline first: the committed manifests of `f5/oracle/manifest.txt` and `f4/oracle/manifest.txt` were reproduced byte for byte by a complete run of
  `run_all.sh` of each folder from a COPY of `e20-f4f6` (so the oracle replays to its own frozen outputs before any change).
- New scenarios, added to the scripts that the loaders already read:
  - `f5/oracle/run_f5.py`: `arrow3_flush` (hero 109), `arrow3_near105` (hero 105), `arrow3_x107` (hero 107), each in both forms, written to
    `e20-f4f6/f5/oracle/out/json/{name}.json` and `{name}_pf.json` (and `out/traces/`), read by `ArrowOracle.Load("{name}", stepByImage: true)`.
    `arrow3_point_blank` (hero 108) and its `_pf` already exist (frozen plain key, `_pf` of f5). The scenarios pass `targets=[]` (the default target of
    `target_spec` is the Beannoid at x=300: a first draft without it put the target in slot 1 and was caught by the comparison below).
  - `f4/oracle/run_charged_pf.py`: `charged7_flush_pf` (hero 109) and `charged7_near105_pf` (hero 105), wall column 5, `first=100`, written to
    `e20-f4f6/f4/oracle/out/json/charged7_{flush,near105}_pf.json`, read by `ChargedArrowOracle.Load("flush" | "near105", stepByImage: true)`.
  - `arrow4_flush` and the ball keys (`fire11_flush`, `fire12_flush`, `ice14_flush`, `ice15_flush`) are not needed by T2: they stay in
    `e20-e0k5-disc/out/keys/` in their plain form (the ball keys belong to f6b).
- Manifests updated by `manifest.py` after a complete run of both `run_all.sh` from a fresh copy: `f5/oracle/manifest.txt` +12 lines (the 6 new
  json and the 6 new traces), `f4/oracle/manifest.txt` +4 lines; no other line moved.
- Cross-check against the reading: `arrow3_flush.json` and `arrow3_near105.json` (plain) are byte-identical to the reading's
  `out/keys/*.json`. The values of the keys (raw 16.16, S = tick 10): `arrow3_flush` x = 7143424 (109) at S, flag at S+0, status 3 at S+1; `arrow3_x107` x at S =
  **7143423** (109 minus one raw unit, the halving of the binary), 7143424 at S+1, flag at S+2, status 3 at S+3; `arrow3_near105`: 108 at S, 109 at S+1,
  flag at S+2, status 3 at S+3.

## (b) The replays (from copies, outputs identical)

| Replay | Result |
|---|---|
| `run_pointblank.py` (inside `run_all.sh` of f5) | `arrow3_point_blank` and `fire11_point_blank` identical to the committed manifest (the whole f5 manifest is reproduced) |
| `pb_keys.py` (copy of the disc folder + copy of `e20-f4f6/bin`) | the 9 generated keys, json and trace, identical to `e20-e0k5-disc/out/keys/`; the two point-blank keys identical |
| `pb_hero_wall.py`, `pb_sweep.py` | `out/pb_hero_wall.txt` and `out/pb_sweep_east.txt` identical |
| `work/xyfuzz.py 8000 11 -v` (copy of `e20-step-search`) | identical to `out/fuzz_seed11.txt`: 8 000 vectors, 0 mismatch, 449/449 instructions of `ComputeXYPosition` |
| `work/xyfuzz.py 5000 41 focus -v` | identical to `out/fuzz_focus_seed41.txt`: 5 000 vectors, 0 mismatch |

## (c) The DLL probes (throwaway test `E0k5T0ProbeTests`, kept as `E0k5T0ProbeTests.cs.txt`; outputs `probes-dll-before.txt`, `probes-dll-wired.txt`)

Release, neutralised copy, 21 probe cases, all passed (they only print and assert the hole).

1. **PA2 with a wall cell at 3 px** (the arrow spawned by the interface at x = 106, the box right edge at 117, the column-5 wall at 120): before the
   integration `Move((5,0,0))` returns `(5,0,0)` and the root moves by `(5,0,0)`: the hole of the engine, as the plan says. After one engine frame (world
   set; the entity has reached x = 111) the same `Move((5,0,0))` returns `(0,0,0)`: the field is read. `BirthTickProbePaTests.PA2` is not touched and
   stays true.
2. **The arrow against column 5, DLL today** (HEAD, 1 tick per frame; `xS` = position at the end of the birth tick S = 10, first flag, first status 3):

   | hero x | binary (reading) xS / flag / status 3 | DLL today |
   |---|---|---|
   | 106 | 109 / S+1 / S+2 | 109 / S+1 / S+2 (the 3 px step ends at 109: no gap) |
   | 107 | 109 minus 1 unit (7143423) / S+2 / S+3 | 110 / S+1 / S+2 |
   | 108 | 109 / S+1 / S+2 | 111 / S+1 / S+2 |
   | 109 | 109 / S+0 / S+1 | 112 / S+1 / S+2 |

   At 3 ticks per frame hero 108 and 109 give xS = 111 and 112 and the flag at **S+3** (status 3 at S+4): the birth tick and the two that follow are all
   unintegrated. At 2 ticks per frame: flag S+1.
3. **Against the keys, S to S+19, HEAD**: `arrow3_flush` (109), `arrow3_point_blank` (108), `arrow3_x107` (107): 20 of 20 offsets differ at 1, 2 and 3
   ticks per frame; the witness `arrow3_near105` (105): 0 of 20. The charged arrow (`charged7_flush_pf`, `charged7_near105_pf`): 3 of 20 offsets differ
   at 1 tick per frame and 5 of 20 at 3 ticks per frame.

## (d) The predicted list of the moved tests: measured by a temporary wiring, reverted

The wiring (`temp-wiring.diff.txt`, throwaway, not the T1 function): in `RunOneKinematicTick`, before `MoveControllerAndPullPosition`, for
`Controller != null && HasLogicalZ && SelfEntity.World == null` and a non-zero step, the halving search of `xymodel.py` (entity attempt = `BlockedCorners`
of the DLL at the candidate; no candidate entity; the first accepted whole step ends; `sra 1` halving; both axes at once; the loop ends as the model
says), the bounded step replaces `FinalForceX/Y`, and `ForceAdjusted` is raised after `SlideAlongWall` when no sub-step was accepted.

- Existing tests moved: **none**. Release, neutralised copy, `--blame-hang-timeout 60s`, the three disjoint partitions of
  `e20-g2c1/t4/run.sh` (`suite-wired/runpart.sh`, summaries and empty `.red` lists in `suite-wired/`): p1 1 689 passed, p2 3 085 passed, p3 1 913
  passed, **0 failed** (6 687 = 6 666 + the 21 probe cases). So the predicted list is empty: `ArrowLifeTests` (E7), `ChargedArrowLifeTests` (C7) unchanged
  (their walls are far from the shot), and `BirthTickProbePaTests.PA2` stays true. Stop threshold (more than 10 moved): not reached.
- With the wiring, against the keys, S to S+19 (probes `probes-dll-wired.txt`): `arrow3_flush` 0 of 20 at 1, 2 and 3 ticks per frame; `arrow3_point_blank`
  0 of 20 at 1 and 3; `arrow3_near105` 0 of 20; `charged7_flush_pf` and `charged7_near105_pf` 0 of 20 at 1 and 3 ticks per frame (the destruction at S+1
  with the effect line included in the compared line); **`arrow3_x107`: 0 of 20 at 3 ticks per frame but 19 of 20 at 1 tick per frame**.

## FLAG for T2 (found by the probe, not a stop of T0): `arrow3_x107` cannot be green at one tick per frame with the phase B of the plan as written

At 1 tick per frame the birth tick is bounded by the halving (xS = 7143423, equal to the key) but at S+1 the arrow is already integrated and the mover of
the engine decides: it moves the arrow by the last raw unit (x = 7143424, as the key) and raises `ForceAdjusted` at **S+1**, where the binary raises it at
**S+2** (the binary's halving accepts the unit at S+1, then nothing at S+2). The bounce then runs one tick early (status 3 at S+2, key S+3) and the
lines S+1 to S+19 differ. This is the one-tick flag gap of D-E20-38 ("one start in three", kept by D-E0K5-2: after the integration nothing changes), and
x0 = 107 (3 px step) is one of those starts. At 3 ticks per frame S+1 and S+2 are still unintegrated, so the bounded loop of the wiring covers them and the
key is equal. T2 as written ("`ArrowRun` at 107 against `arrow3_x107`, at the three rates, S to S+19") would be red at 1 tick per frame after phase B.
Options for the main session: compare `arrow3_x107` at S only (the unit `7143423`) plus the rates 2 and 3, or accept the known gap and pin it as a
documented deviation, or extend the scope to the binary loop after the integration (what D-E20-80 kept out). Nothing else contradicts the plan.

## Files

`E0k5T0ProbeTests.cs.txt` (the throwaway probe source), `probes-dll-before.txt`, `probes-dll-wired.txt`, `temp-wiring.diff.txt`, `suite-wired/`.

# E20.e0k5 T1: phase A, the pure halving search (2026-10-25)

Files: `Alundra/Physics/HalvingStepSearch.cs` (`Search(forceX, forceY, cardinal, isBlockedAt)`), `Alundra/Physics/HalvingStepResult.cs` (the secondary type of the names guard moved to its own file),
`Alundra.Tests/Physics/HalvingStepSearchTests.cs`, vectors in `step-vectors/` (`gen_vectors.py` seed 2025, `gen_named.py`, `halving-vectors.tsv.gz`, `named-cases.tsv`; both generators import `e20-step-search/work/xymodel.py` and rerun to the same bytes).
No production caller yet (the wiring is T2): no behaviour change.

- **The model's vectors.** 20 000 random cases from the generator of the real-code replay (`xyfuzz.gen_world`, non-hero subject, `0x2000` NoObstacleSlide, `0x100` cleared, no collidable) plus a wall world for half of them
  (a column or a row of `0x40/0x41/0x1040` cells placed so that the halving converges, the step often reaching a few pixels into the wall): 12 588 with a sub-step accepted (5 729 partial), 6 555 refused, 857 zero vectors;
  176 943 queries (120 924 refused, 56 019 free). Each case records the queries of the model in order; the test makes the pure function answer from them: an unrecorded query, another order, another count or another result fails the case.
- **The wall test and the field.** The pure function takes the wall test as a predicate (the offset from the start, raw units); it does not contain the cell test of `0x800373E4`. The wall test of the DLL is the existing `ScriptedMotion.BlockedCorners` (wired as the predicate in T2, which is where its fidelity to `0x800373E4` is exercised through the oracle keys). Plan deviation noted in the report.
- **Named cells.** The 10 cells of the reading (3 px: x0 = 105 to 109; 6 px: x0 = 104, 105, 107, 108, 109) against a wall that refuses above 109 pixels (7143424): 7143423 for (107, 3 px), (104, 6 px), (107, 6 px) = 3 of 10; the rule 'largest free position' differs by one raw unit on exactly those 3 (a test).
- **Red before, green after.** Skeleton (`default`): 28 failed, 4 passed (`east_zero`, `Halving_Flush_NothingAccepted`, `Halving_ZeroVector_MakesNoQueryAndAcceptsNothing`, `Fuzz_File_HasPartialRefusedZeroAndFullCases`: the file-shape test and the witnesses that the skeleton's `default` also satisfies). Body: 32 of 32 green; `Alundra.Tests.Physics` namespace: 140 of 140.
- **Throwaway mutants (real, by script, reverted, `git diff` equal to the committed body):** `-1` halved by plain `sra` (the loop never ends: killed by the hang), cardinal test inverted (17 failed), the whole first step not returned at once (9 failed), the accepted sub-steps not accumulated (14 failed). The 8 mutants of the plan are T3.
- `check_names.py --rules`: 0 finding. Engine and `alundra-project/`: untouched.

# E20.e0k5 T2: phase B, the first step of an unintegrated entity bounded by the halving search (2026-10-25)

Files: `Alundra/Physics/ScriptedMotion.cs` (`RunOneKinematicTick`: before the mover, for `Controller != null && HasLogicalZ && SelfEntity?.World == null` and a non-zero step, `HalvingStepSearch.Search` with `BlockedCorners` as the wall test, the bounded step replaces `FinalForceX/Y`, `ForceAdjusted` raised after `SlideAlongWall` when no sub-step is accepted), `Alundra.Tests/Physics/FirstStepBoundWorldTests.cs` (18 cases), `t2-red-before.txt`, `suite-t2/`.
Two commits: the red tests first (`a47489be`), then the wiring with the tests adjusted as below.

- **Tests** (real controller, column 5, east side only, S to S+19, `heroX` = the hero of the key's scenario): `ArrowRun` 109 / `arrow3_flush` and 108 / `arrow3_point_blank` at 1, 2, 3 ticks per frame; 107 / `arrow3_x107` (see the deviation); `ChargedArrowRun` 105 / `near105` and 109 / `flush` at 1, 2, 3; witness 105 / `arrow3_near105`.
- **Red before, green after** (`t2-red-before.txt` = the run with the production file at HEAD): 16 red, 2 green before; 18 of 18 green after. Red before: the 3 + 3 + 3 + 3 cases of flush (arrow), point-blank, charged near105, charged flush, `Arrow_AtHeroX105_ThreeTicksPerFrame...`, `Arrow_AtHeroX107_ThreeTicksPerFrame...`, `Arrow_AtHeroX107_OneAndTwoTicksPerFrame...` (2). Green before and after (the witness): `Arrow_Witness_AtHeroX105_TheFreeStepTo108IsNotBounded` at 1 and 2 ticks per frame.
- **Suite after the wiring**, partitions of `suite-wired/runpart.sh`, `--blame-hang-timeout 60s`: Release (neutralised copy) p1 1668, p2 3085, p3 1963 passed, 0 failed (6716 = 6698 + 18); then Debug (last) the same counts, 0 failed. Existing tests moved: **none** (the predicted list of T0 (d) was empty). `check_names.py --rules`: 0 finding. `Alundra/bin/Debug/.../Alundra.dll` and `alundra-project/Alundra.dll` identical (`cmp`). The four `docs/hero-trace-389-*.txt` differed by line endings only (`diff --ignore-cr-at-eol` clean) and were restored. Engine, converter, data: no diff.

## Deviations from the plan as written (both measured)

1. **The witness at 105 at three ticks per frame is not green before.** At 3 ticks per frame the step of S+1 (3 px from 108) is still unintegrated and ends at 111 inside the wall, so the arrow at 105 needs the bound too (key: 109 at S+1). The witness (green before and after) is therefore kept at 1 and 2 ticks per frame, and the 3-per-frame case is a red-then-green test (`Arrow_AtHeroX105_ThreeTicksPerFrame_TheSecondStepStillUnintegratedIsBoundedTo109`).
2. **`arrow3_x107` is green over S to S+19 at three ticks per frame only** (the flag raised by T0): at 1 and 2 ticks per frame S+1 is integrated and the mover of the engine raises the flag at S+1 where the binary raises it at S+2 (the one-tick flag gap of D-E20-38, kept by D-E0K5-2); the position of S (7143423, 109 minus one raw unit) is the halving's and is pinned at S at those two rates. Decision for the main session if the gap must be closed: extend the scope to the binary loop after the integration (what D-E20-80 kept out).
