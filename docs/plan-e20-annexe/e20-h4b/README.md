# E20.h4b (the shared bricks of the spells) - binary reading and DLL landing (2026-10-18)

Provenance: read-only discovery of 2026-10-18, while the DLL restructuring waits for the author (no code slice during the program), to
detail E20.h4b of `docs/plan-e20-heros.md` section 1.31. Repository read at `eb285bf7` to `c221a76f` (no change under `Alundra/` or
`Alundra.Tests/` in between); binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Nothing in the repository or
the binary was changed. Frozen evidence: paths and line numbers refer to that state.

- `bin/`: the reading of the bricks (the push 0x800366FC and the force pass, the highest live slot [0x80134360], the target query 0x8003AF70 and
  its Hoare sort 0x8003AE04, the 256-entry sin/cos tables, the camera kick [0x800DD874], the shake block 0x800E4338 and the fire-2 buffer);
  `report.md` is the agent's final message; `vec/` holds replayable vectors (every word read and written) and `tables.json`.
- `critic/`: an independent adversarial critic with its own MIPS interpreter (`cmips.py`) and models: every decision-carrying fact CONFIRMED;
  one inference REFUTED (an on-demand maximum does not reproduce the stored count: a slot dead in the tick still counts until the next pass);
  the replay rule "a read of an unlisted byte is an error" made normative; 29 extra vectors with sentinels (`vec_extra/`). **Its verdict prevails.**
- `state-c221a76/`: where the bricks land in the DLL (`dll/state.md`) and its counter-check (`check/check.md`, which prevails: the "98
  sprites" of E3 are 98 sprite-table records, the static placements are 19 on 5 maps; `CameraSway` is reset by 1 of the 6 `ResetSingletons`
  of the tests).

The scripts read the binary at the absolute path they name (outside the repository) and write next to themselves: rerun them from a copy,
never in place. `SHA1SUMS` lists every file here.
