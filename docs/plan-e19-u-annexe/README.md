# E19.u annex (open point O-E19-78, decision D-E19-103: the frame order of the inventory trigger)

Plan: `docs/plan-e19-opcodes.md`, section 1.2y.

- `notes.md`: the read-only discovery of 2026-10-08, worked in a physical copy (repository `486fdf5`, engine `f86a981b`): the binary's
  frame order (`dis-mainloop.txt`, `dis-render.txt`, `dis-update.txt`, `dis-updateworld.txt`), real-code measurements (`bin-out.txt`), the
  port probes (`O78ProbeTests.cs.txt`, `probe-out/base` today, `probe-out/pending` the candidate), the candidate change
  (`candidate-pending.diff.txt`, applied by `apply_o78.py`; in test form: the production edit still needs review) and the red-first tests
  (`AlundraInventoryTriggerFrameOrderTests.cs.txt`).
- `verify/verify.md`: the fresh counter-check, **CONFIRMED** (own disassembly, own copy, three more freeze windows at 1, 2 and 3 ticks
  per frame). Where it disagrees with `notes.md`, it wins: an unlisted behaviour change (a map event that starts a warp on the tick
  Start is pressed: the warp now starts and the trigger refuses Start through its warp guard, D-E13D-30); the six trace files prove
  nothing here (their harnesses do not run `AlundraWorldProxy.Update`); the closed file list misses comments and docs (listed there).
- The scripts read the game binary and the scratchpad through absolute paths of the author's machine: adapt them first.
