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
