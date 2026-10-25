# E20.e0k5 discovery: the first step of an entity born in game (O-E20-24), part A of the read-only reading of 2026-10-24

Provenance: part A of the project pass of 2026-10-24 (HEAD `86d421b5`, critic at `42cd834a`), copied here on 2026-10-25 by T0 of
`docs/plan-e20-heros.md` section 1.18.5 (E20.e0k5) from the session scratchpad `scratchpad/t2-proj/`. Binary `ALUN_CD.EXE` (France).
Nothing in the repository, the binary or the data was changed by the reading. Frozen evidence: the files are copied unchanged;
`SHA1SUMS.txt` lists them (this README and the sums file excepted).

- `report-main.md`: the reader's report (French). Part A (section 2, "O-E20-24") is the subject here. **Part B (the Objet 008, section 3) is
  NOT part of this slice**: its scripts (`c1b/`, `o8/`, `c1b_*.py`) and outputs were left out; the text is kept as it was written.
- `critic-main.md`: the fresh critic's report (French); section A is the subject.
- `runpy_cap.py`, `pb_probe.py`, `pb_sweep.py`, `pb_hero_wall.py`, `pb_dirs.py`, `pb_keys.py`: the scripts. They expect `f4f6bin/`, a COPY of
  `docs/plan-e20-annexe/e20-f4f6/bin/` (not copied here: no annex copies), next to them; run them from a copy of this folder
  (`python -I runpy_cap.py pb_keys.py`), never in place. They name absolute paths of the machine (Python site-packages, the binary).
- `out/pb_*.txt`, `out_pb_dirs.log`: the sweeps and the flush positions of the hero on the four sides (the north, south and west sides rest
  on a set-up that was not cross-checked: O-E0K5-1).
- `out/keys/`: the keys the reading generated (plain form, without the step by image), with `arrow3_point_blank.txt` and `fire11_point_blank.txt`
  (copies of the frozen keys). The keys the tests read are produced by T0 under `e20-f4f6/f5/oracle/out/` and `e20-f4f6/f4/oracle/out/`
  (see `docs/plan-e20-annexe/e20-e0k5/results.md`).
- `critic/w/`: the critic's own sweep (`crit_sweep.py`, `out_sweep/crit_sweep.txt`), the integer-halving model (`halving_model.py`) and the small
  disassembly helpers (`lib.py`, `cap.py`, `d1.py`). Part B scripts of the critic were left out.
