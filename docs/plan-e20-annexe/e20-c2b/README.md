# E20.c2b annex

`results.md`: T0 (prerequisites, replay, vectors, inventory, presets, file name). `oracle/vectors_entry.py` and `oracle/scen_new.py`: new scripts that import the E20.c2 oracle read only (`oracle/out/` = their output).
`inventory/`: the temporary wiring (a patch kept as text, never applied), the logs and the red lists of the suite with the wiring. `replay/`: the replay of the c2 and `W7-hurt-shore` oracles.
Replay (Python 3 + capstone, `python -B`, from a copy of the annex): `python -B oracle/vectors_entry.py <outdir>` and `python -B oracle/scen_new.py <outdir>`.
