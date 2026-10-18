# E20.h3 (the bomb and the beans) and the DLL state for E20.h0, h1, h3, h4 (2026-10-18)

Provenance: read-only discovery of 2026-10-18, run while the DLL restructuring waits for the author (no code slice during the program),
to prepare the item-use frame (E20.h0) and the bomb and the beans (E20.h3) of the author's fallback theme "all the attacks of the player".
Repository read at `66d86eb` to `8887a48` (no change under `Alundra/` or `Alundra.Tests/` in between); binary `ALUN_CD.EXE` (France), SHA-1
`a705f8dab665645af5dfaf8fb348c7926b5caa58`, read with capstone and executed in the MIPS interpreters of the E20.h4 oracle (copied). Nothing in
the repository or the binary was changed. Frozen evidence: paths and line numbers refer to that state.

- `bin/`: the reading of the bomb (item 0x23: start 0x8003453C, driver 0x800352C4, cancel 0x80034B54, C62, D30, E12) and the beans (item
  0x1F: start 0x80034224, driver 0x80035204, cancel 0x80034ACC, C73, E21; the magic pot A20/C74). `report.md` is the reading agent's final
  message, kept verbatim (its own report file could not be written).
- `critic/`: an independent adversarial critic: it re-disassembled every cited function, wrote its own models and ran the real code against
  them (6 000 and 20 000 vectors per function, 0 deviation; 1 156 real-code mutants explained). **Its verdict prevails where it differs**:
  Circle starts the item from 63 of the 80 hero states (not only from idle and moving: also from the two attack groups and the hurt state);
  the lid handler (E, 19) is missing from the plan; `bin/results/goldens_h3.json` is not replayable from an empty memory (use
  `critic/results/goldens_critic.json`). `check_final_message.md` is its final message.
- `state-f81304b/`: the DLL and test state for E20.h0, h1, h3, h4 (`dll/state.md`) and its counter-check (`check/check.md`, which prevails):
  84 citations of the h4 and g1a readings translated to the restructured paths, 0 false, 4 imprecise; three findings that change a task
  (the default cancel is a `SwimHandlerOf(state) == Skip` test, not a final `else`; 15 call sites of `TryUseItem` in the binary, 8 covered by
  the DLL; E3 also serves the global sprites 150 to 153).

The scripts read the binary at the absolute path they name (outside the repository) and write next to themselves: rerun them from a copy,
never in place. `SHA1SUMS` lists every file here.
