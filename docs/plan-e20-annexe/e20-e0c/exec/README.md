# E20.e0c T0 (2026-10-22): measurements, vectors, choices

Provenance: T0 of `docs/plan-e20-heros.md` section 1.26 (revision 3.1), run at HEAD `916f0374`. Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`.
No DLL code was written by T0.

- `results.md` (French): the result of each point (a) to (k), the predicted list of the tests that move by phase (e), the next ADR number, and the facts that differ from the plan.
- `vectors/`: the oracle the tests of T1 to T4 read (JSON, UTF-8, LF):
  - `loot-spawn.json`: `SpawnRandomLoot` and `SpawnContents` on the real code (323 cases, veto, table full).
  - `give-item.json`: `GiveItem` on the real code (228 vectors, 19 objects), effects in creation order with their forces.
  - `give-jump-table.json`: the jump table of `GiveItem` over the 53 placed types.
  - `c255.json`: C255 on the real code, `+0x274` 0 and 1 (424 vectors, 1 477 ticks), full journal and journal with the fanfare as a stub.
  - `pickup-message.json`: the raw ETC strings against the compiled ones (the message buffer).
  - `item-table.json`: the 98 rows of the object table of the binary, octets 4 to 7 (the columns of `item-drop-properties.json`, equal to it).
- `scripts/`: the generators. They import the harness of `../work/` (`lh.py`, `models.py`, `c255model.py`, `exelib.py`, `emu/`) and read the binary at the path `exelib.py` names; run them from a COPY
  of `../work/` with these files added (they write next to themselves): `python -B gen_loot.py`, `gen_give.py`, `gen_c255.py`, `gen_jumptable.py <posed-rev3/placed.json>`,
  `gen_message.py <repo root>`, `gen_itemtable.py`, `check_h1_bodies.py vectors/give-item.json <dir of model_h1.py of ../../e20-h1/disc/oracle>`. All are deterministic (fixed seeds).
- `probes/ZzT0Probes.cs.txt`: the DLL probes of (b), (c), (i), (j), (k), as a throwaway xunit file of `Alundra.Tests` (removed after the measurements, never committed as a test).
- `replays/`: the outputs of the replays of (a) and of `check_h1_bodies.py`.
- `SHA1SUMS.txt`: SHA-1 of the files above (LF).
