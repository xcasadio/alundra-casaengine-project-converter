# E20.g2c1 - the two read-only discoveries of 2026-10-24 and the T0 replay

Deposited by T0 of E20.g2c1 (docs/plan-e20-heros.md section 1.24.5), 2026-10-25.

- `g2c1/` - the C4 discovery at HEAD `73c05530` (`report-main.md`, `critic-main.md`, scripts, `out/`, `critic/`): `+0x18C`, the 43 scripted trunks, the corners reader, `+0x80`, probes P1 to P4 (+P5), the permuted objects. Includes `c4_full.py`, `c4_goldens.json`, `q8_p1.py`, `q3_corners.py`, `q8_p4.py`, `q8_p5.py`, `q9_swaps.py`.
- `shop/` - the shops discovery (`report-main.md`, `critic-main.md`, scripts, `out/`, `critic/`): the 26 articles, the programs B of the purchase, `shop_replay.py` (scenarios a to i).
- `replay/` - the T0 replay of 2026-10-25: outputs of the scripts above (`c4_full_replay.txt`, `q8_p1.txt`, `q3_corners.txt`, `q8_p4.txt`, `q8_p5.txt`, `q9_swaps.txt`, `shop_replay_*.txt`, `shop_b_table.txt`, `shop_h_table.txt`), `p1_col6.py` (+ output, the wall in column 6 on the binary) and `c64_lift.py` (+ output, `dis_c64.txt`) for T0 (g).
  All equal to the stored outputs (see `docs/plan-e20-annexe/e20-g2c1/results.md`).

Not deposited (copies of repository annexes and caches): the `work/` directories (the scripts expect `work/` = `docs/plan-e20-annexe/e20-g1a/work/`, identical to the copy they were run with, `diff -rq` empty on `orc/`),
the copies of `e20-g1a/work/` and `e20-g2c/disc/` inside `critic/` (the critics' `sim/work`, `rp/work`), and every `__pycache__`.
To replay: copy the directory next to a copy of `e20-g1a/work/` named `work`, then `python -B <script>` (not `-I`: the user site-packages hold `capstone`). `c4_full.py 20000 mut` is the full run (35 s). The scripts read the binary
`ALUN_CD.EXE` (France) and the export in `data-extracted/`, as they did when written.
The scripts of `g2c1/` and `shop/` that carry the same name (`dump_map.py`, `dump_prog.py`, `disasm_range.py`, `names.json`, `report-main.md`, `critic-main.md`) are the same family, kept per directory.

`SHA1SUMS.txt`: SHA-1 of every deposited file (`sha1sum -c SHA1SUMS.txt` from this directory).
