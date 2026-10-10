run-oracle (E20.d discovery) - layout and how to rerun.  Everything lives under this folder; the repository was only read.

base/            the harness.  tools/ + hworld.py hscen.py hmodel.py hmut.py run_c2.py = byte copies of docs/plan-e20-annexe/e20-c2/oracle (the c1/c1b/c2 oracle: real tick passes of ALUN_CD.EXE (France)
                 in a MIPS emulator, real hit pass, real Beannoid / Orc targets), unedited except tools/compare.py (ANNEX = absolute read-only path of docs/plan-e19-d2c-valeurs.md).
                 NEW files (the only ones written for this task): rworld.py (cell layout painted before the hero is placed, pad driver, dash-nibble override, per-tick trace),
                 rscen.py (the scenarios G1..G10 and their analysis), rmut.py (mutations), rgate.py (D1 gate through the harness), rhazards.py (load-delay check),
                 manifest.py, index.py.
d1/              byte copy of docs/plan-e20-annexe/d1-oracle-moveplayer/tools (compare.py: same ANNEX edit) and its out/ (the D1 gate run on the unmodified D1 tools).
out/gN/          the per-tick traces: <scenario>.txt (readable) and <scenario>.json (column-major, same data + journal + damage experiments); g7/tackle/ = the Tackle sweep.
out/summary-GN.txt   computed summaries (timelines, tick numbers).     out/INDEX.txt   one line per scenario: file, ticks, start, pad pieces, layout, targets, description.
out/check/       xcheck-GN.txt (second interpreter, every run), mutations.txt, load-delay-check-run-oracle.txt, cov-GN.json, determinism.txt.
out/gate/        the D1 gate through the harness (comparison.txt, gate.txt).      out/manifest.txt  md5 of every output file.
work/            throw-away probes (disassembly helper bdis.py, cell scans).

rerun (Python 3 with capstone; the disc file of the France version and data-extracted/ must be present; always python -B):
  python -B base/rscen.py [G1 .. G10] [--cross]     scenarios (--cross repeats every run through the second interpreter and compares tick by tick); about 1.5 s per 100-tick run
  python -B base/rgate.py      python -B base/rmut.py      python -B base/rhazards.py      python -B base/manifest.py [name]      python -B base/index.py
