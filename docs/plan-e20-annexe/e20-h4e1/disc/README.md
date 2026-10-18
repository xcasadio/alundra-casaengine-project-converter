# E20.h4e1 - wind 1 (item 0x31): the driver 0x80035EB0 (read-only discovery, 2026-10-18)

Read-only discovery for `docs/plan-e20-heros.md` section 1.31 (E20.h4, the magic tomes), slice h4e1. Repository read at `ed60a0eb` (no change under `Alundra/`, `Alundra.Tests/` or
`alundra-project/Data/` since `f81304b`); binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (checked by `tools/facts_check.py`). Nothing in the repository, the
binary, `data-extracted/` or `alundra-project/` was written, built, run or exported.

- `report.md`: the report (French). `check.md`: what was re-checked against the sources and the verdict on each claim.
- `keys/`: 42 REMADE oracle keys (84 files, `.json` = every tick, `.txt` = a short summary) + `MANIFEST.sha1`, identical on two complete runs (`out/MANIFEST_run1.sha1`, `out/MANIFEST_run2.sha1`).
  Slot indexes written (`+0 = i`, `+0x48 = -1`), generator step per frame (`_pf`), R-EL-4 bench (flat field, hero (100,100) facing east, seed `0x12345678` at tick 1, no map entity, Circle at tick 1).
- `vec/vec_wind1_80035EB0.json`: 800 replayable vectors of the driver over the REAL code (real query + sort + guard + cancel dispatcher; spawn and no-effect destroy stubbed and journalled), format of
  `docs/plan-e20-annexe/e20-h4b/bin/vec/README.txt`; `vec_wind1_gen.txt` (generation: 0 mismatch with my model, coverage), `vec_wind1_replay.txt` (replay from an empty memory: all ok), `SHA1SUMS`.
- `wind1_key.py` (keys), `wind1_vec.py` (vectors), `wind1_replay.py`, `wind1_model.py` (my own model of the driver, the query and the sort), `wind1_mut.py` (45 mutants), `wind1_compare.py`,
  `wind1_boundcheck.py`, `wind1_tables.py`; `tools/` (`lib.py`, `bdis.py`, `scan_off.py`, `facts_check.py`); `h4o/` (copy of the e20-h4 oracle as corrected by the h4d1 critic) and `h4b_h/` (copy of
  the e20-h4b vector framework); `stale/` (two keys made with the h4 harness as it was, `FAITHFUL=0`, to show the one-tick difference of R-EL-1).
- `out/`: `facts_check.txt`, `scan_off_144.txt` (every access to `+0x144`), `dis_*.txt` (capstone listings of the driver, the guard, the query, the pilot wrapper), `sprite22_dump.txt`, `compare.txt`,
  `boundcheck.txt`, `mutants.txt`, `expected_tables.txt`, `replay_trace_cast_31.txt` (the archived `trace_cast.py 0x31` summary, replayed identically).

Rerun (from a COPY of this folder; the scripts read the binary and `data-extracted/data/map_alundra.json` at the absolute paths they name and write next to themselves or under the folder given):

    python -B wind1_key.py OUTDIR [--only NAME ...]        (about 2 minutes for the 42 scenarios; FAITHFUL=0 in the environment gives the stale h4 keys)
    python -B wind1_vec.py 800 20261018 OUTFILE ; python -B wind1_replay.py OUTFILE ; python -B wind1_mut.py 800 20261018
    python -B wind1_compare.py KEYDIR STALEDIR ; python -B wind1_boundcheck.py KEYDIR
