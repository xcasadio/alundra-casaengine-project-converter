# ADR-0041: The target of 0x22 is the binary's target in the DLL's frame, with a per-controller minimum move distance

- **Status**: Accepted
- **Date**: 2026-10-06 (decisions D-E19-94 and D-E19-98 of the author, approved with the plan E19.h1b3 on 2026-10-06)
- **Source**: this chantier: `docs/plan-e19-opcodes.md` section 1.2n.1d E19.h1b3 (rules H1B3-R1 and H1B3-R2), D-E19-94, D-E19-98, the annex `docs/plan-e19-op22-annexe/` (notes, verification, site table, handler rows, probe set-ups). Binary: `ALUN_CD.EXE` (France) at `0x8003DA70` (the wait of `0x22`), `0x8003DB28` (`0x23`), `0x800375E0` (`ComputeZ`), `0x80036BFC` (landing), `0x80039EA0`-`0x80039F04` (spawn). Engine: ADR-0065 (the setting). Replaces in part the first exception of ADR-0026.

## Context

- The binary's handler memorises, at the first call, `byte(record + 9) << 19` and then compares it with `PosZ` for exact equality, clamping `ForceZ` to the gap only when it overshoots (it never creates or reverses a force). Every position the binary keeps carries a `+ 1` (rest `T + 1`, spawn `z + 1`, landing `T + 1 - ModZ`); the target does not.
- ADR-0026 keeps `PosZ_dll = PosZ_b - 1` and named the literal target of `0x22` as an exception. In that frame the binary's comparison is `PosZ_dll == (Height << 19) - 1`, in every direction (a random model of 38 478 waits, and 60 000 in the counter-check: 100 %). The literal target was right on an ascent that starts aligned, one call short on every aligned descent: map 115 B[2] ended after 320 calls where the binary needs 321.
- The shifted target makes the last step of that descent -1 raw unit (1/65536 px). `CharacterControllerComponent` dropped any displacement of 0.001 px or less, so the step was lost and the wait never ended (measured on the real DLL: the hero never regained control).
- A controller's root is a float32 in pixels, which holds no odd raw value from 256 px: above it (map 127 rec17 and rec18, height 38) the shifted target is unreachable and the ball rises for ever (measured: `ForceZ` 32767, `PosZ` 94830592 after 2478 frames). The literal target gives the binary's call count there (192), the ascent being aligned.

## Decision

- `WaitHeightTarget` memorises `(Height << 19) - 1` at the first call. For an entity with a controller whose shifted target is 2^24 or more, it memorises the literal `Height << 19` instead. Later calls do not change it.
- The spawn factory sets `MinMoveDistance = 0` on the controller of a script-driven entity, next to `IsVerticalOwnedExternally` (engine ADR-0065; a gap of the engine is fixed in the engine).
- ADR-0026 stays as it is, except its status line: its first exception (the literal target of `0x22`) is replaced by this decision.

## Consequences

- At the 17 sites (33 instances) the wait ends at the binary's call count: map 115 B[2] 321 calls, `PosZ` 3145727; every other ascent keeps its count and ends one unit lower (the binary's `+ 1`, in the DLL's frame). The 12 hanging ceilings of map 36 (`0x23`) and the two literal balls of map 127 are unchanged.
- The end pose of an ascent is one unit lower, so the platform's own `TileZ` is one lower where the height is even (read by no opcode on the five maps that have a site). A descent of an entity with a controller above 256 px would not be exact (none in the corpus); the riders of a platform were not measured (none carried at a site).
- `MinMoveDistance = 0` applies to every displacement of those entities, horizontal and inherited-ground ones included: neutral on every test and arc measured, not on the author's recipe.
- Rolling back is a revert of the DLL commit, then of the pointer and of the engine.
