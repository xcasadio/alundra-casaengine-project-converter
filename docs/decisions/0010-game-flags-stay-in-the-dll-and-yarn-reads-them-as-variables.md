# ADR-0010: Game flags stay in the DLL, and Yarn reads and writes them as `$flag_n` and `$tmp_flag_n`

- **Status**: Accepted; D-E16-16 range superseded by ADR-0011
- **Date**: 2026-09-28
- **Source**: this chantier: `docs/plan-e16-etat-partie.md` §0.1 (D-E16-6 of 2026-09-27; D-E16-14 to D-E16-18, the author's answers of 2026-09-28) and `docs/plan-e15-yarn.md` §0.1 (D-E15-13)

## Context

- `AlundraGameState` owns the game's two flag banks, `GameFlags` (kept across maps, saved by the original) and `TemporaryFlags` (cleared on map entry, never saved), 1024 32-bit words each. A flag id `n` addresses bit `n & 0x1f` of word `(n >> 5) & 0x3ff`, and bit `0x8000` of the id selects the temporary bank (`Alundra/Scripts/AlundraGameState.cs:192`, `:220`, `:244-259`). The event program interpreter sets and clears a flag with opcodes `0x05` and `0x06` (`Alundra/Scripts/AlundraEventProgramRunner.cs:445-459`); the text's `<<flag n>>` command sets flag `n | 0x8000`, in the temporary bank (`Alundra/Scripts/AlundraYarnBindings.cs:152`).
- The engine lets a game inject a `Yarn.IVariableStorage` into `YarnDialogueRunner.VariableStorage`; when none is set, each dialogue starts with a fresh `Yarn.MemoryVariableStore` (`CasaEngineMonogame/CasaEngine/Framework/Dialogue/Yarn/YarnDialogueRunner.cs:25-32`, `:142`; engine ADR-0042). The DLL sets none today (`Alundra/Scripts/AlundraDialogueDirector.cs:171-172`).
- Yarn Spinner 3.2.1's storage contract: `TryGetValue<T>`, `GetVariableKind`, `Program` and `SmartVariableEvaluator` (`Yarn.IVariableAccess`), `SetValue` for a string, a number or a boolean, and `Clear` (`Yarn.IVariableStorage`).
- None of the 485 exported `.yarn` files declares, reads or writes a Yarn variable; their only commands are `<<flag>>` (932) and `<<falcon_update>>` (7). E15 left the storage to E16 (D-E15-13).

## Decision

- **D-E16-6** — The flags stay the DLL's property. The interpreter (Alundra's script engine) and the DLL's directors read and write them; the engine's script systems reach them only through a bridge the DLL provides.
- **D-E16-14** — The Yarn bridge is built in E16, as its own slice (E16.f), although no exported text uses a variable yet.
- **D-E16-15** — The DLL's Yarn storage exposes the two flag banks and nothing else.
- **D-E16-16** — `$flag_n` is flag `n` of `GameFlags` and `$tmp_flag_n` flag `n` of `TemporaryFlags`, `n` in decimal from 0 to 32767, without the `0x8000` bank bit (the same `n` as `<<flag n>>` for the temporary bank).
- **D-E16-17** — Flags are Yarn booleans, readable and writable: `true` sets the bit, as opcode `0x05` does, and `false` clears it, as opcode `0x06` does.
- **D-E16-18** — Any other name (including Yarn's own internal variables), a flag name out of range, or a non-boolean value stored under a flag name is refused: logged once per name, no exception, no state change.

## Consequences

- The storage keeps no state of its own: a flag written from Yarn is saved, or not, exactly like one written by an opcode (`GameFlags` saved, `TemporaryFlags` not); the save object gains no Yarn field.
- Yarn features that need variables of their own (`visited()`, declared or smart variables) do not work with this storage; needing one reopens this decision.
- `Clear()` cannot empty the flag banks: their lifecycle stays with `AlundraGameState` (map entry, new game, load).
- The cinematics bridge (E17) is still to design; it may reuse these names.
- Until some Yarn text uses a flag variable, only E16.f's tests exercise the storage.
