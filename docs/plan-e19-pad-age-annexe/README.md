# E19.f3d annex (age of the pad word read by the save screen, O-E19-75, D-E19-99)

Plan: `docs/plan-e19-opcodes.md`, section "E19.f3d".

- `notes.md`: the read-only discovery of 2026-10-06 (night): frame order of the binary (`RenderScene` then `Update(0)`, one pad
  sample per frame), every pad reader of the save screen path, real-code runs (a Cross at P read at P+1; earliest accepted answer
  P+19), the one-line fix, values measured through the real proxy, tests and mutants.
- `patch-proposal.diff.txt`: the complete proposed change (production line, comments, tests), applied and run in a physical copy of
  the repository; `probe-tests.cs.txt`: the new tests with their measured values.
- `padage_model.py`, `hostmodel.py`, `padage_sweep.py`, `validate_model.py`, `cmp_binary_model.py`: the host-order model (42 of 42
  real-proxy measurements); `drive.py`, `emu.py`, `frameorder*.py`, `slotorder*.py`, `padrefs.*`, `reach.py`, `wordref.py`, `xref.py`:
  binary probes.
- `verify/verify.md`: the fresh counter-check. **Where it disagrees with `notes.md`, the counter-check wins:**
  - `docs/plan-e19-opcodes.md` is CRLF in the working tree; the LF files are `AlundraSaveBookEndToEndTests.cs`, ADR-0040 and the
    two notes of `plan-e19-save-order-annexe`.
  - more stale lines to annotate: plan F3C-R2 and the f3c result paragraph, ADR-0040 body line 15,
    `plan-e19-save-order-annexe/notes.md:85`, `plan-e19-f3-annexe/census-notes.md:187`.
  - cite the anchor text `var (screenPressed, screenInterval) = _choicePadOfTick[tick];`, not a line number (G3c edits the same file).
  - the main inventory (slot 6) and the sub-inventory (slot 4, `0x80053328`) also read their pad words in `RenderScene` (one sample
    old) while the port reads the current tick: a separate open point, not part of f3d.
- The scripts read the game binary through absolute paths of the session scratchpad and of the author's machine: adapt them first.
