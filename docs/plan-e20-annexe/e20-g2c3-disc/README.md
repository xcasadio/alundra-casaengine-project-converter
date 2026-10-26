# E20.g2c3 - the read-only discovery and critique of 2026-10-26

Deposited by T0 of E20.g2c3 (docs/plan-e20-heros.md section 1.24.5, "E20.g2c3"), 2026-10-26.

- `g2c3/` - the discovery at HEADs `22ddf0be` / `0d3e7e14` (`report-main.md` = the entire report, the scripts `s1_drop_cycle.py`, `s2_drop_on_torch.py`, `s3_hit_window.py`, `census_*.py`, `classify_progs.py`, `drop_heights.py`, `near.py`, `overlap.py`, `scan_global.py`, `dump_map.py`, `dump_prog.py`, `bx.py`,
  the disassemblies `dis_a19.txt`, `dis_c71.txt`, `dis_destroy.txt`, `c71_pseudocode.txt`, the outputs `out_*.txt`, `census_*.txt`, `drop_heights.txt`, `near.txt`, the copies of the code of the DLL at that HEAD `EntityScriptProxy.HEAD.cs`, `EntitySupport.HEAD.cs`, `EventProgramRunner.HEAD.cs`, and `replay/` = `c71_full.py`, `c71_goldens.json`, `c4_scen.py`, `c4_full.py`, `f1_flame.py`, `f2_drop.py`, `f2b_drop_sweep.py`, `out/s8_torch.txt`).
- `critic/` - the fresh critique (`report-main.md`: its values are the ones that stand where it corrects the discovery, except one: the drop's prefab HAS a controller, revision 2 of the plan), its scripts (`arrivals.py`, `census*.py`, `classify.py`, `e20dis.py`, `e2dis.py`, `scanref.py`, `tables.py`, `bx.py`), its disassemblies `dis_*.txt`, `out_arrivals.txt` and `rerun/` (its replays `out_*.txt`, `s4_66.txt` to `s4_70.txt`, `exp_*.txt`, `got_*.txt`).

Not deposited (copies of repository annexes and caches): the `replay/work/` directory (identical to `docs/plan-e20-annexe/e20-g1a/work/`, `diff -rq` empty, checked at T0), every `__pycache__`, and the critique's `copy/` (a copy of the discovery folder).
To replay: copy `g2c3/` next to a copy of `e20-g1a/work/` placed at `g2c3/replay/work`, then `python -B -X utf8 <script>` (not `-I`: the user site-packages hold `capstone`). The scripts read the binary `ALUN_CD.EXE` (France) and the export in `data-extracted/`.
The T0 replay (identical outputs) is in `docs/plan-e20-annexe/e20-g2c3/replay/`, the results in `docs/plan-e20-annexe/e20-g2c3/results.md`.

`SHA1SUMS.txt`: SHA-1 of every deposited file (`sha1sum -c SHA1SUMS.txt` from this directory).
