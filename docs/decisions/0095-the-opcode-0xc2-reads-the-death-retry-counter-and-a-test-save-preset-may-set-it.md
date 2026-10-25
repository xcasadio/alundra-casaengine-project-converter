# ADR-0095: The opcode 0xC2 reads the death-retry counter, and a test-save preset may set it

- **Status**: Accepted
- **Date**: 2026-10-25
- **Source**: `docs/plan-e20-heros.md` section 1.4.1 (E20.b2, decisions D-B2-1 to D-B2-6, answers Q-B2-1 to Q-B2-3 through D-E20-82); `docs/plan-e20-annexe/e20-b2-disc/report-main.md` and `critic-main.md` (part B); `docs/plan-e20-annexe/e20-b2/results.md` (T0 to T3)

## Context

The Legendary Sword (object 8) is the slot-1 weapon of rank 4. In the original the Snow King gives it on the map 7 (program `B[10]`) only to a player who has resumed the game after a death 20 times or more: `0xC2 [20]` at pc 1508 sets `Result`, and the `If true goto` at pc 1510 reads it. The DLL had no `case 0xC2`: the opcode was skipped by its size and the jump read a stale `Result` (measured 0 at the HEAD, so the scene always refused). In the binary (`ALUN_CD.EXE`, France, `0x80041D34`) the opcode reads one parameter byte and the byte `g_saveData + 0x756` (the death-retry counter, raised and capped at `0x800315C0`/`0x800315D4`, reset at `0x80031860`), both with `lbu`, and sets `Result = (counter >= parameter)`, an unsigned comparison. The DLL keeps that counter in `GameState.DeathRetryCount`, which nothing raises before E18 (the hero's death and the resume). The selection, the swing and the damage of the object 8 were already carried by the data (the selection `0x8004E18C`, the record 11 equal to the record 0, the equipment sheet 38 with an attack of power 50 added to every hero attack with the equipment bit).

## Decision

- `EventProgramRunner` gets a `case 0xC2`: one parameter byte (the pc moves by 2), `Result = (GameState.DeathRetryCount >= parameter)`, both read unsigned, as the binary (D-B2-2).
- The original's condition is kept as is: 20 resumes or more (D-B2-1). Before E18, the sword is obtainable in a normal game only through a test save.
- A test-save preset may carry an optional `DeathRetryCount` (0 to 255, refused otherwise), applied to `GameState.DeathRetryCount`; two presets: `legende-m14` (the Beannoid arena with the object 8) and `legende-m7` (the map 7, in front of the Snow King's blocks at the cells (25, 16) and (26, 16), level 7, arrival (25, 17, 7), counter 20) (D-B2-3).
- The loss of the charge and of the wave when the object 8 is owned, even with Elnard, is kept (D-B2-4); the development maps' objects stay as they are (D-B2-5).
- The record 31 of the map 7 (a live pickup made twice by `0x8A [31]` in `B[10]`) is not a way around `0xC2`: in the binary its non-zero C index (133) runs the map script, not the native pickup handler, and in the DLL a copy on the hero is not picked up.

## Consequences

- With the counter at 20, walking north into a block of the Snow King plays the scene, gives the object 8 once and sets the flag 1787; at 19 the scene refuses (dialogue 129), gives nothing and leaves the flag off, so it plays again at the next entry on the map.
- Against a real Beannoid (response `0x10`) the object 8 deals 51 (100 to 49 hit points), the basic sword 2; a held Square with the object 8 counts no charge and makes no aura and no wave.
- 12 real mutants were applied by script and all killed (`docs/plan-e20-annexe/e20-b2/t3-mutants/`); the release and debug suites pass (6 471 tests).
- Getting the sword in a normal game waits for E18, which raises the counter.
