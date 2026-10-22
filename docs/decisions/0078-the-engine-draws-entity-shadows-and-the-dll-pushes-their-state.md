# ADR-0078: The engine draws entity shadows and the DLL pushes their state

- **Status**: Accepted
- **Date**: 2026-10-22
- **Source**: this chantier: the author's answer of 2026-10-22 to Q-E19-SM1 (`docs/plan-e19-opcodes.md`, section 3; decision D-E19-121 in section 0.1)

## Context

- The opcode `0xAC` (Script_172_0AC) rewrites the shadow-size bits of the first matched entity's flags. The DLL already ports that write (`Alundra/Events/EventProgramRunner.cs:1255`, `:2591-2602`, through `EntityFlags.WithShadowSize`).
- `0xAC` has 32 sites in the corpus. Nothing draws a shadow today, neither the DLL nor the engine, so the opcode is listed among the partial opcodes, in E19.sm6 (`docs/plan-e19-opcodes.md`, section 1.4).
- For the map backdrops, the engine draws and the DLL pushes the state (E9.b).
- Asked who carries the shadows, the author retained the proposal: the engine draws them, the DLL pushes their state.

## Decision

- The engine draws entity shadows, as a generic rendering feature of the engine.
- The DLL pushes each entity's shadow state to it: the shadow size that the flags hold and that `0xAC` rewrites, plus whatever the binary's shadow drawing reads, which the slice's discovery reads in the binary.
- An engine slice comes first, with its own engine ADR; E19.sm6 then carries the DLL part.

## Consequences

- Until the engine slice lands, the shadow bits are written and nothing is drawn, as today.
- The look of a shadow (shape, size, depth order against the entity and the tiles) follows the binary. A rendering defect is fixed in the engine, not worked around in the DLL or the converter.
- Any engine gap found on the way is reported, not worked around.
