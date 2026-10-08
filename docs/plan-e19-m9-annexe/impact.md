# E19.m9 - what could move if `Array.Clear(state.Parameters)` is removed (part 5)

Nothing here was run on the full suite (not allowed); everything below is read from the census, from `dynamic.md`, and from reading the tests.
"Could move" means: a call re-initializes a program whose FIRST instruction is a stateful wait that was pending at the same pc.

## Corpus (MEASURED, census.tsv)

- Slots A, D, E, F: **0 sites** among 7134 distinct program entries of the 483 maps (first opcodes of D: 0x05, 0x87, 0x2B...; of F: 0x05, 0x27,
  0x10, 0x0D...; of A: 0x31, 0x1A, 0x62...). The slots the game calls repeatedly are not concerned by the clear on real data.
- Slot B: 154 programs (154 records) start with 0x37, 11 of them on the chain (162 B[5], 163 B[2,3,4], 169 B[3], 176 B[3,4,5], 183 B[3], 185 B[1],
  392 B[3]). Re-initialization happens at the out-of-zone re-arm (`RunMapEventsPass`, E19.j) when the hero leaves the zone while the first wait is
  pending, then comes back.
- Slot C: 243 start with 0x37 (32 on the chain), 47 with 0x0B (3 on the chain: map 10 C[23], C[39], C[43]), 1 with 0x1E (off chain, map 77 C[23]).
  Re-initialization needs the entity's state to be cleared while the first wait is pending: another entity's 0x40 through a logic entity
  (`RunOneScriptCall`'s cross-entity branch), which nothing in the census shows happening; an own 0x40 is a LATER instruction.
- 13 C programs (off chain) start with 0x6A (binary-only, not ported) - out of the port's scope.

## Chain sites to look at (all slots) - 46 sites, 14 maps

Map 10 (C[19,20,21,22,23,36,37,39,43,72]), 162 (B[5], C[2]), 163 (B[2,3,4]), 164 (C[2]), 165 (C[3,4,5,8,9,22,23]), 169 (B[3]), 172 (C[8,9,22,23]),
176 (B[3,4,5], C[2]), 179 (C[8,9,23]), 180 (C[3,5]), 182 (C[1,2]), 183 (B[3]), 185 (B[1], C[1..4]), 392 (B[3]). The other 16 chain maps (including
389, 390, 476, 478, 391, 416) have none.

## Existing tests

- The six reference traces (`docs/hero-trace-389-{spawn,highground}-{fixedstep,freestep}.txt`, `docs/intro-trace-389.txt`,
  `docs/intro-programs-389.txt`) run on map 389 (and its intro programs): **map 389 has no site** -> INFERRED unchanged (to be proven by the byte guard).
- Arcs on a map that has sites (from each arc's `WorldName`): A12 and A14 (179), A13 and A15 (176), T-A19 (185), T-A10v, T-B9 and A10J (10), A8 (163),
  A20 (162), A10 (165), A11 (164), A9 (172). They could move only if their trace re-enters one of the sites listed above after a leave/clear
  (E19.j's census found no re-entry on the chain except the empty program 476 B[3], plan section 1.2l). Arcs on 391, 478, 416, 135, 178, 61, 346, 15, 109
  have no site. NOT RUN: the claim "none moves" is an inference.
- Unit tests that pin the clear or re-initialization: read, none found sensitive.
  - `AlundraEventProgramRunnerTests.RunScript_DefaultSlot_AlwaysReInitializes_EvenAfterASuspend` (:1350): its Wait(5) is the SECOND instruction (0x01 first,
    which advances and zeroes the marker). MEASURED replica (dynamic.md 4d): after 8 calls TargetAnimationId = 0 in both as-is and keep. It would flip only
    if the Wait were first (replica: 3 with keep) - the test would then need its pad byte, which it already has.
  - `AlundraEventProgramRunnerProgramIndexTests.U13_...` (Wait after 0x40, not first) and `U12_...`, `RunMapEventsPass_J1a/J1b` (no wait), the `LoopGuard_*`
    tests (`RunOneScriptCall` directly, no init), `RunScript_SlotB_ResumesAcrossCalls...` (resume path, no re-init): unaffected by reading.
  - `Timer_0x73_0x74_...` asserts `_30` survives `InitializeEventData` (already the binary behaviour; the clear does not touch `_30`).
  - `InitializeEventData_MasksIndexWith0x7f...` asserts only `CodeIndex`.
  - Mover/animation test montages with a stateful first instruction (`Codes = {0x1F, 24, 0, ...}`, `{0x01, 0x1A, .., 0x1C, ..}`) run on the resumed B/C path.
- A test of the new behaviour would be needed for the future slice (not written here): B `Wait N` left and re-entered, A/D/E/F first-instruction wait
  called repeatedly (binary: ends at call N+2 if nothing intervenes).
