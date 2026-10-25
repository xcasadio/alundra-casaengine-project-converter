# E20.c2b annex

`results.md`: T0 (prerequisites, replay, vectors, inventory, presets, file name). `oracle/vectors_entry.py` and `oracle/scen_new.py`: new scripts that import the E20.c2 oracle read only (`oracle/out/` = their output).
`inventory/`: the temporary wiring (a patch kept as text, never applied), the logs and the red lists of the suite with the wiring. `replay/`: the replay of the c2 and `W7-hurt-shore` oracles.
Replay (Python 3 + capstone, `python -B`, from a copy of the annex): `python -B oracle/vectors_entry.py <outdir>` and `python -B oracle/scen_new.py <outdir>`.
`results.md` also holds T1 and T2 (the entry and the hurt block, the red-before counts, the moved tests against the prediction, the deviations). `t1/before-stub.txt`: outcome of every case of `HeroInjuryTests` with the entry stubbed to -1; `t1/t2-before-code.txt`: outcome of every case of the touched classes of T2 before the code of the hurt block; `t2/moved-observed-after-wiring.red`: the 29 cases that moved when the entry was first wired (identical to the predicted list `inventory/sub-all.red`).
