# E20.d run oracle (2026-10-10)

Per-tick traces of the hero run (Triangle held: states 0x29, 0x03, 0x2A, 0x04, 0x3E, 0x1C), produced by running the real code of
`ALUN_CD.EXE` (France) in the MIPS emulator of E20.c1 / c1b / c2. Specification: `docs/plan-e20-heros.md` section 1.13; evidence:
`../run-binary.md`, `../run-dll.md`, `../run-oracle.md`, `../check.md`.

```
oracle/
  base/   the harness: tools/ (E20.c1b tools, compare.py identical to e20-c2/oracle/tools/compare.py), rworld.py, rscen.py, rmut.py,
          rgate.py, rhazards.py, manifest.py, index.py, and the E20.c2 files they build on
  out/    gN/<scenario>.txt (readable trace) and .json (same columns, column-major), summary-GN.txt, INDEX.txt, check/, gate/, manifest.txt
```

## Replay

Python 3 with capstone; the France disc file and `data-extracted/` must be present; always `python -B`, from the repository root.

```
python -B docs/plan-e20-annexe/e20-d/oracle/base/rscen.py [G1 .. G10] [--cross]    # --cross repeats each run through the second interpreter
python -B docs/plan-e20-annexe/e20-d/oracle/base/manifest.py [name]                  # md5 of every output file, written to out/<name>
```

`manifest.py` run on the copy gives a file identical to `out/manifest.txt` (557 files; checked at T0).

## Groups

| Group | Content |
|---|---|
| G1 | departures: cardinals, diagonals, direction added late, about-face, jump and walk into Triangle, early release |
| G2 | 120-tick straight run; east map edge |
| G3 | the cone (52 cases) |
| G4 | stop (0x2A) and resume (release k ticks, then press again) |
| G5 | rebound against walls (E, W, N, S) and raised steps |
| G6 | ledge fall, landing, release in the air, Square in the air |
| G7 | charge (0x04), sacred sword counter, Beannoid hits, Tackle sweep |
| G8 | ladder |
| G9 | shallow water, deep water, ice |
| G10 | the dash-nibble override: the 48 departures and 15 resumes used by E20.d2 (stick rule) |

## Columns

`t hold | anim target>current (b0 = AnimCompleteCounter) | dir target/cur facing | ForceX ForceY ForceZ | pos x y z (px) |
ground adj collZ | slope vorOR | sprintCnt chargeCnt | journal (snd<id>, fx<id>@(x,y,z px), spawns)`. Tick 0 is the start state.

## Limits

- Sound 12, dust and the landing (sound 11, effect 7) are stubs (E20.e); the hurt columns are not meaningful (E20.c2b).
- The east wall rebounds at tick 87 in the binary; the DLL may differ by one tick (O-E19-42 / D-E19-58).
- `on_ground` is judged at the tick of the loss (O-E20-12).
- `out/` is generated: never edit it; rerun and compare manifests.

## Swim (E20.i, 2026-10-10)

Per-tick traces of the hero in deep water (slope 4), produced by the same harness, with three NEW files in `base/` and nothing else edited:
`sscen.py` (the scenarios, groups W1 to W10), `sverify.py` (non-regression proof), `sindex.py` (index and manifest). Specification: `docs/plan-e20-heros.md`
section 1.14; evidence: `../../e20-i/` (`swim-binary.md`, `swim-dll.md`, `swim-oracle.md`, `check.md`). Output: `out-swim/` (`wN/<scenario>.txt` and `.json`,
`summary-WN.txt`, `check/xcheck-WN.txt`, `INDEX.txt`, `manifest-swim.txt`); `out/` is never touched.

| Group | Content |
|---|---|
| W1 | entering deep water: walking and running by boots level 0..3, a jump over the shore, a fall from a ledge, a higher bank |
| W2 | swimming: idle, the eight directions, turning, release, boots level 3, a level-0 hero already inside |
| W3 | the dive: each button edge, held buttons, repeated presses, direction at the dive tick, the surfacing, the first tick after the entry |
| W4 | leaving the water: equal-height land, a dive in progress, Triangle held, diagonal, a bank one unit higher |
| W5 | walls in the water, the map edge, level 0/1 walls (information) |
| W6 | weapon state in the water: charge counter, pending-shot pointer, aura pointer |
| W7 | hurt in the water (information for E20.c2b) |
| W8 | the slope-4 state table: the 80 animation ids at the start of a water tick (240 three-tick runs) |
| W9 | the Circle button in the water and the land-side handling of the swim states |
| W10 | contradictory direction nibbles, channels, shallow water to deep water |

Replay (Python 3 with capstone, `python -B`, from the repository root; run it from a COPY of the annex, the D1 gate cannot be replayed in place):

```
python -B docs/plan-e20-annexe/e20-d/oracle/base/sscen.py [W1 .. W10] [--cross]
python -B docs/plan-e20-annexe/e20-d/oracle/base/sindex.py        # INDEX.txt and manifest-swim.txt (785 lines)
python -B docs/plan-e20-annexe/e20-d/oracle/base/sverify.py       # the swim extension does not change the D1 gate nor any trace of out/ (writes ../out-verify)
```

Checked at E20.i T0 from a scratch copy: the ten groups with `--cross` give `out-swim/manifest-swim.txt` byte for byte; `sverify.py` reports the gate PASS
(S-A `1ae0cc02...`, S-B `975c342a...`, 216 values each) and the 549 regenerated files of `out/` identical. The gate needs, in the copy, the values document
`docs/plan-e19-d2c-valeurs.md` at `docs/plan-e20-annexe/` and the D1 baseline traces in `oracle/d1/out/` (from `../../d1-oracle-moveplayer/out/`).
