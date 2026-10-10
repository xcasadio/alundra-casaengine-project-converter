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
