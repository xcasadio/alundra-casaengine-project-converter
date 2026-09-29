# ADR-0015: The event interpreter ports the missing opcodes from the binary, with a production loop guard and the E4.d detour for scripted walks

- **Status**: Accepted
- **Date**: 2026-09-29
- **Source**: this chantier: `docs/plan-e19-opcodes.md`, §0.1 (author decisions D-E19-1 to D-E19-7 of 2026-09-29), after the read-only progression diagnostic and the E19 discovery (§0.2)

## Context

- The story cannot progress past the Klark ship. Flags are not the cause: the interpreter already implements every flag opcode, and Yarn `<<flag n>>` and `$flag_n` reach the same banks (E16.a, E16.f). The cause is opcode coverage: 88 opcodes reachable in the corpus are still skipped by size (14,644 of 111,136 reachable instructions, 392 of 483 maps), and no plan step owned them.
- On the ship chain (389, 390, 476, 478, 476, 392, 391, 416, 163) the skipped opcodes cause:
  - a soft-lock on 390 (the captain's exit cutscene waits on the hero instead of the captain, because `0x43`/`0x42` are skipped);
  - a hard stop on 476 (`0xC4` never opens its dialogue, so the temporary flag 999 is never set);
  - a process freeze on 478 (two tick programs loop without a suspending opcode once `0x0B` is skipped, and `RunOneScriptCall` has no production loop guard).
- In `ALUN_CD.EXE` every opcode handler is called as `handler(logic = *(owner+0x230), owner, &ptr, state)`, with the logic word reloaded before every instruction (`0x80042284`). Only `0x42`, `0x43` and `0x66` touch the owner. `0x42` and `0x43` retarget the logic word; `0x66` does not, contrary to the port's comments and `docs/intro-roadmap.md`. The DLL never reads its `LogicEntity` field.
- The original has no loop guard. Its program loops always contain an opcode that suspends. Across the corpus, the longest finite suspend-free run is 52 dispatched opcodes, and the worst suspend-free timer loop is 120.
- The original's scripted walk wait `0x0B` and its wall wait `0x24` have no exit when the walker is blocked. The DLL's own `0x1E` walk already detours around an obstacle through the navigation grid, a documented deviation (E4-D5).
- The original draws the speaker's name in a framed plate above the dialogue box. The plate is fed by `0x0D`, `0x5C` and `0xC4`, about 2,900 reachable sites. The DLL's dialogue box is the engine's generic window, not the original's geometry. Portraits and the typewriter belong to E12.c.
- Script visual effects (`0x90`-`0x94`, `0xA0`-`0xA3`) gate nothing on the chain. The converter does not export them (544 records in 157 maps), and the DLL has no effect pool or renderer.

## Decision

- **D-E19-1** — A new master-plan step, E19, ports the missing interpreter opcodes. Its first phase follows the ship chain to the first save book (map 163). Its second phase covers the rest of the corpus, one family per slice.
- **D-E19-2** — Every ported opcode follows `ALUN_CD.EXE`, which settles any disagreement with the decompilation, as in earlier steps. In particular, the logic entity is resolved before every instruction and persists on its owner, and each map event keeps its own. `0x42` sets the hero, `0x43` takes the last match and writes `Result` on both paths, and `0x43 [0x80]` is kept as the binary's no-op.
- **D-E19-3** — The interpreter gets a production loop guard, a documented deviation from the original. After about 1,024 dispatched opcodes in one script call, the program yields until the next frame with its position kept, as a Break would. The first occurrence is logged, and the trace sink reports it.
- **D-E19-4** — The name plate is ported in E19, with the original's dialogue box geometry and frame, for `0x0D`, `0x5C` and `0xC4`. Portraits and the typewriter stay in E12.c.
- **D-E19-5** — Unblocking comes first. `0xC4` first opens its dialogue without a name, like `0x5C` today; the name plate follows in a later E19 slice.
- **D-E19-6** — The scripted walk wait `0x0B` gets the E4.d navigation detour that `0x1E` already has. `0x24` gets no detour, since it waits for a wall by design. No timeout completes a stuck wait: a walker that the port blocks where the original does not is fixed at the root, in the collisions or the engine.
- **D-E19-7** — Script visual effects are ported in E19's second phase, after the ship chain, as a slice of their own (converter export, effect pool, rendering).

## Consequences

- Programs that never run `0x42`/`0x43` behave exactly as before; about 98 maps that do run them change, faithfully to the binary. Some of that change is visible outside the chain, where an NPC's later programs act on the entity its tick program retargeted.
- A script that loops without suspending no longer freezes the game. It yields once per frame and logs, and the original's behaviour is unchanged wherever it never loops that way.
- Until the typewriter (E12.c), a dialogue whose text sets a flag at its end sets it when the box opens. Cutscene boxes opened by `0xC4` then close about 1.2 s after opening, as the `0x5C` boxes already do.
- A new soft-lock found after porting `0x0B` or `0x24` is a collision divergence to fix, not a script to patch.
- The ship chain can be played through without script visual effects; they arrive in the second phase.
- Each slice is planned in detail and approved separately, as in E16.
