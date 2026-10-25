# E20.b2: Object 008 (the legendary sword) read in the binary and in the DLL (2026-10-24)

Provenance: read-only discovery of 2026-10-24 (part B of the theme 2 project pass: part A, the point-blank shot, is another slice), paid into the
repository by T0 of E20.b2 (2026-10-25, branch `chantier/theme2-attaques-objets`). Binary `ALUN_CD.EXE` (France), read with capstone and run in the E20.c1b
damage oracle (copied from `docs/plan-e20-annexe/e20-c1b/oracle/tools/`). Nothing in the repository or in the binary was changed by the discovery.
Frozen evidence: paths, addresses and line numbers refer to that state (HEAD `86d421b5` for the reading agent, `42cd834a` for the critic).

- `report-main.md`: the reading agent's report (French), kept verbatim (whole file; only section 3, "Partie B", concerns this slice; the part A it
  also holds is not used here).
- `critic-main.md`: a fresh critic's report (French), verbatim; its section B is binding over the report where they differ. Notably: the record 31 of the
  map 7 is a live pickup that must be probed (done by T0 (c), `../e20-b2/results.md`), and `Result` at pc 1508 and 1510 must be measured (done).
- `c1b_legend.py`, `c1b_legend.txt`, `c1b_cmp.py`: the replay of the c1b damage oracle with the inventories `legend` (kit + item 8) and `legend_holy`
  (kit + Elnard + item 8); `c1b_cmp.py` shows that the defence sheet of the hero is the same for kit, Elnard and item 8. T0 (b) redoes the
  replay from a copy of the frozen oracle and keeps the keys (`../e20-b2/K1-legend.*`, `K2-legend.*`).
- `bx.py`, `scan_local.py`, `t1_slots.py` to `t12_const8.py`, `out_sources_item8.txt`, `out_map7_B10.txt`: the reading agent's scripts and outputs on the
  slot table `0x800B9FE8`, the item to record table, the equipment sheets, the sources of item 8 (one `0x82 [8]` in all the programs, at pc 1623 of
  `B[10]` of the map 7) and the program `B[10]` listing.
- `runpy_cap.py`: the wrapper that puts capstone on the path.
- `critic/`: the critic's scripts for part B (`scan8.py`, `scan38.py`, `scan38b.py`, `scan_hdr.py`, `b1.py` to `b7.py`, `cap.py`, `d1.py`, `lib.py`) and
  `scan8_out.json` (the scan of the item 8 in every program).

Left out on purpose (as announced by the plan, section 1.4.1, T0 (a)): the copies of annexes (`c1b/`, `f4f6bin/`, `critic/w/c1b_oracle/`,
`critic/w/f4f6bin/`), the part A files (`pb_*.py`, `out/pb_*`, `out/keys/`, `crit_sweep.py`, `halving_model.py`) and every `__pycache__`.

The scripts still carry the absolute paths of the session's scratchpad and read the binary and `data-extracted/` at absolute paths. `SHA1SUMS.txt` lists
the files.
