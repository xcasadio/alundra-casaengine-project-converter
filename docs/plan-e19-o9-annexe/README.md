# E19.m7 annex (open point O-E19-9: zero-duration Loop animations)

Plan: `docs/plan-e19-opcodes.md`, section "E19.m7".

- `notes.md`: read-only discovery of 2026-10-07 (night): the raw data of the five animations (`raw_dump.txt`, `dump_raw.py`), the
  binary entity animation routine `UpdateAnimation` `0x80038AB4` (`dis_update_animation.txt`), the real routine run in the MIPS
  interpreter for 600 ticks (`run_update_animation.py` and `.out`: frame 0 stays displayed, the loop counter stays 0; the 2-tick and
  10-tick siblings loop as expected), the engine sampler today, the scan of all 9624 exported `.anim2d` (`scan_anim2d.py` and `.out`:
  exactly 5 files with duration 0), the accesses to the countdown field (`xref_0xa8.txt`), and the slice proposal.
- Not counter-checked by a second discovery: the plan review and the fresh verifier check the facts the slice rests on.
- The interpreter helpers (`emu.py`, `lib.py`) are those of `docs/plan-e19-g3d-annexe/discovery/lib/`; the scripts read the game binary
  and the data through absolute paths of the session scratchpad and of the author's machine: adapt them before running.
