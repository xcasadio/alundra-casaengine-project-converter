# Theme 2: the player's attacks and the uses of the inventory items, read in the binary and the data (2026-10-24)

Provenance: read-only discovery of 2026-10-24 (repository HEAD `5eccf614`), the inventory of the second author's fallback theme, paid into the
repository by T0 of E20.e0d (2026-10-25, branch `chantier/theme2-attaques-objets`). Binary `ALUN_CD.EXE` (France), read with capstone; exported data
read from `data-extracted/` and `alundra-project/`. Nothing was changed by the discovery. Frozen evidence: addresses and line numbers refer to that state.

- `report-main.md`: the reading agent's inventory (French): the weapons, the items, the states, the opcodes of the items, the chests (section on the
  chest led to the tranche E20.e0d); kept verbatim.
- `critic-main.md`: a fresh critic's report (French); its corrections are binding over the report where they differ.
- `critic/`: the critic's scripts (`c1.py` to `c14.py`, `hdr.py`, `lib.py`, `sprtypes.json`).
- `scan.py`, `t1_tables.py` to `t14_heroanim.py`, `bx.py`: the reading agent's scripts; `etc_items.txt`: the ETC names of the items.

The scripts still carry the absolute paths of the session's scratchpad; they are evidence, not a build step. `__pycache__` is left out.
`SHA1SUMS.txt` lists the files.
