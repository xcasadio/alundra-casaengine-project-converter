# Open point O-E19-77 (pad-word age of the inventory screens): no gap

Plan: `docs/plan-e19-opcodes.md`, rows O-E19-77 and O-E19-78.

- `notes.md`: read-only discovery of 2026-10-07 (night), worked in a physical copy of the repository. The binary's inventory
  (slot 6, `0x80056598`, seven by-interval reads) and sub-inventory (slot 4, `0x80053328`, six) run in `RenderScene` and read the
  sample of the previous frame; the trigger (`0x8002BCA4`, `0x8002BCC8`) runs in `Update(0)` after `UpdateWorld`. By design the port's
  tick k is the binary's `Update(k)` plus `RenderScene(k+1)` (`AlundraInventoryDirector.cs`, class summary), so the port's per-frame
  call n reads the same sample as the binary's call n: 21 of 21 schedules equal through the real proxy (`probe-tests.cs.txt`,
  `probe-out/`, `bin-events.txt`, `cmp-today.txt`); the change that fixed the save screen (E19.f3d) would break 16 of 21
  (`apply_m2.py`, `cmp-m2.txt`) and three `AlundraInventoryPortraitWiringTests` pins.
- `verify/verify.md`: the fresh counter-check, **CONFIRMED** (own disassembly, own binary driver and port probe comparing the full
  state at every sample, 10 schedules of which 2 new: 10 of 10 equal; the mutant changes 10 of 10). It also measured a frame-order
  difference (not a pad-age one): the binary skips the map events in `Update(P+1)` to `Update(P+37)` around an inventory opened at P
  and closed at P+19 (`menuopen_phase.py`), the port on ticks P to P+36 (`port-mapevents.tsv`): one tick early at both ends. Open
  point O-E19-78.
- The scripts read the game binary through absolute paths of the session scratchpad and of the author's machine: adapt them first.
  The physical copy of the repository used for the runs was not kept.
