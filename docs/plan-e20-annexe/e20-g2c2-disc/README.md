# E20.g2c2 - the two read-only discoveries of 2026-10-25 and the T0 replay

Deposited by T0 of E20.g2c2 (docs/plan-e20-heros.md section 1.24.5, "E20.g2c2"), 2026-10-26.

- `g2c2/` - the discovery at HEADs `8174c0cb` / `6df3f265` (`report-main.md`, the scripts, `out/` with the disassemblies of C70, E20, D34, A19, C71, the handler tables, the census, `scan_0x87.txt`, `spawnwarp_callsites.txt`, the replays `replay_f1_flame.txt`, `replay_f2_drop.txt`, `replay_f2b_drop_sweep.txt`, `replay_s8_torch.txt`, and `replay/` = the scripts `c4_scen.py` (scenario `s8_torch`), `f1_flame.py`, `f2_drop.py`, `f2b_drop_sweep.py`, `c4_full.py`).
- `critic/` - the fresh critique (`report-main.md`: its values are the ones that stand where it corrects the discovery), its scripts (`t1.py` to `t5.py`, `census2.py`, `scan41.py`, `scan87b.py`, `firenear.py`, `dump_*`), `out/` (`p_hitswitch.txt`, `p_rain.txt`, `p_selfhit.txt`, `replay_f1_flame_critic.txt`, the disassemblies, the scans) and `replay/` (`p_hitswitch.py`, `p_rain.py`, `p_selfhit.py`, `f1_flame.py`, `c4_scen.py`, `c4_full.py`).

Not deposited (copies of repository annexes and caches): the `replay/work/` directories (identical to `docs/plan-e20-annexe/e20-g1a/work/`, `diff -rq` empty, checked at T0), every `__pycache__`, and `LiftableAi.HEAD.cs` (a copy of a repository file at an old HEAD).
To replay: copy `g2c2/replay/` (or `critic/replay/`) next to a copy of `e20-g1a/work/` named `work`, then `python -B -X utf8 <script>` (not `-I`: the user site-packages hold `capstone`). The scripts read the binary `ALUN_CD.EXE` (France) and the export in `data-extracted/`.
The T0 replay (identical outputs) is in `docs/plan-e20-annexe/e20-g2c2/replay/`, the results in `docs/plan-e20-annexe/e20-g2c2/results.md`. `c4_scen.py`, `c4_full.py`, `dump_map.py`, `dump_prog.py`, `census_c4.py`, `census_scripted.py` and `bx.py` are the same files as in `docs/plan-e20-annexe/e20-g2c/disc/` and `e20-theme2/` (kept per directory).

`SHA1SUMS.txt`: SHA-1 of every deposited file (`sha1sum -c SHA1SUMS.txt` from this directory).
