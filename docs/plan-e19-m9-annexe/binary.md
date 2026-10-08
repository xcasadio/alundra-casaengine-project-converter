# E19.m9 - binary facts (ALUN_CD.EXE, France), parts 1 and 2

Disassembler: `python docs/plan-e19-backlog-1006-annexe/mipsdis.py <hexaddr> <count>`. All addresses below were read with it
(and the handler table with a small script, see README). "state" = the 0x38-byte program state (`a2` of InitializeEventData).

## 1. InitializeEventData 0x80041EE4 - every store into the state

Prologue 0x80041EE4-0x80041F04 only saves registers. Body:

| address | instruction | meaning |
|---|---|---|
| 0x80041F08 | `lw s0, 0x4c(entity + slot*4)` | program byte of the slot (`ProgramIndexes[slot]`) |
| 0x80041F10-0x80041F2C | bit 0x80 picks `a1 = 0x80126E70` (set) or `0x80126EC0` (clear) | table block (current map vs global map_alundra) |
| 0x80041F38-0x80041F54 | `(s0 & 0x7f) * 2 + table[slot]` -> `lhu`, + code base (`0x30(a1)`) | `a0` = code pointer of the program |
| **0x80041F5C** | `sw a0, 4(a2)` | state **+4** := code pointer (current pc) |
| **0x80041F60** | `sw a0, 0(a2)` | state **+0** := code pointer (program start, used by 0x49/0x4A/0x4B restart) |
| 0x80041F64-0x80041F80 | debug-flag test (`0x800DC058`, bit 0x10), then range check of `a0` against the code area | only prints on error (0x80041FBC-0x80041FF8 then `0x800831E0`) |
| 0x80042000-0x80042014 | epilogue | |

These are the ONLY two stores into the state. There is no loop and no `sw $zero` in the function. In particular
**+8 .. +0x28 (the port's `Parameters[1..9]`), +0x2C (`Result`), +0x30 and +0x34 are NOT written by InitializeEventData**.
(The 0x8004201C-0x8004204C routine just after it writes the entity's six program bytes from a record, unrelated.)

## 1b. State layout, mapped to the port (`EventProgramState`)

Established from RunScript 0x8004205c (loop at 0x80042280-0x80042340: `s3` = state, `s2 = s3+4`, handlers get `a2 = s2` (pointer to the
pc word) and `a3 = s3`), from the handlers' own accesses, and from `EventProgramState.cs` of the decompilation.

| state offset | binary meaning | port field |
|---|---|---|
| +0x00 | program start pointer (set by init; zeroed with +4 when the state is "cleared") | `Parameters[0]` (= start `CodeIndex`); `Codes != null` stands for "+0 and +4 nonzero" |
| +0x04 | current pc pointer (`lw v0,(s2)` 0x80042280; `+= result` 0x80042330-0x8004233C) | `CodeIndex` / `Sp` / `Codes` |
| +0x08 | cleared after EVERY advancing instruction (0x8004232C `sw zero, 8(s3)`) and on Break (0x800421F4); wait marker | `Parameters[1]` |
| +0x0C .. +0x28 | handler scratch | `Parameters[2..9]` |
| +0x2C | `Result` (read by 0x03/0x04/0x4A/0x4B: 0x8003D1A0, 0x8003E9EC `lw v0, 0x2c(a3)`) | `Result` |
| +0x30 / +0x34 | timer counter (0x73/0x74 at 0x8003FA3C, 0x8003FA58) / auxiliary (0x78-0x81 family) | `_30` / `_34` |

"Cleared state" (END_SCRIPT 0x80042344-0x80042350, when `s5` is set by g_clearProgramState, and 0x80042314-0x80042318 for another entity's
state): `sw zero,(s3)` and `sw zero,4(s3)` - **+0 and +4 only**; +8.. stay. The map-event out-of-zone re-arm (0x8003C7F0-0x8003C800) zeroes
slot +4, +8 and +0x30 = state +0, +4 and +0x2C (`Result`), again leaving state +8.. untouched (the slot state is copied into/out of the hero's
state by the 0x10-byte loops at 0x8003C7A8-0x8003C7E0).

So in the binary the wait marker (+8) and its data (+0xC..) survive a re-initialization AND a clear. The port's `Array.Clear(state.Parameters)`
(Alundra/Scripts/AlundraEventProgramRunner.cs:352) is therefore an extra, with `Parameters[0] = CodeIndex` (:364) being the equivalent of the
`sw a0, 0(a2)` store.

## 1c. Which state RunScript passes

Jump table at 0x80023E10 (read from the exe): slot 0 (A) -> 0x80042168, 1 (B) -> 0x800420DC, 2 (C) -> 0x80042108, 3 (D) -> 0x80042168,
4 (E) -> 0x80042168, 5 (F) -> 0x80042150 (then falls into 0x80042168).

- **B (0x800420DC) and C (0x80042108)**: `s3 = s4 + 0x234` (the entity's own state). Resume when `lw 4(s3)` (state +4) and `lw 0x234(s4)` (state +0) are
  both nonzero (0x800420E0-0x800420F8 for B; 0x8004210C-0x80042124 for C, which also restores the Last* target when `entity+0x22C != 2`).
  Otherwise it jumps to 0x80042190/0x80042194 and calls InitializeEventData with `a2 = s3`.
- **A, D, E (0x80042168) and F (0x80042150 then 0x80042168)**: `0x8014 - 0x2618` -> `s3 = 0x8013D9E8`, **one global state** for the four slots
  (F first zeroes the hero's forces at `0x80127D30 + 0xC4/0xC8/0xD4/0xD8`, 0x80042150-0x80042164). They always fall to 0x80042190 and re-initialize:
  there is no resume test for them. Confirmed: the four slots share one state and one wait marker.

## 2. Opcodes that keep state in +8.. across suspended calls

Handler table at 0x80098FAC (`lui 0x800a; addiu -0x7054`, 4 bytes per opcode, read from the exe). A scan of every handler (0x00-0xC4) for
`lw/sw n($a3)` with n in 8..0x2C, plus a manual read of the ones that move `a3`, gives:

| opcode | binary handler | uses | port (line, same file) | ported? |
|---|---|---|---|---|
| 0x0B walk until beyond radius | 0x8003D468 (marker +8 = pc ptr, +0xC/+0x10 = X/Y memo) | +8 +0xC +0x10 | `WalkUntilBeyondRadius` :2799 (`Parameters[1]` = CodeIndex, [2..3] memo); case :612 | yes |
| 0x1C repeat animation | 0x8003D7FC | +8 +0xC | `RepeatAnimation` :2754 | yes |
| 0x1D repeat animation with collision | 0x8003D890 (calls 0x8003D7FC) | via 0x1C | case :686 -> `RepeatAnimation` | yes |
| 0x1E walk | 0x8003D8D8 (+8 = signature/pc, +0xC/+0x10 = X/Y) | +8 +0xC +0x10 | `Walk` :2606 (`signature` in [1], [2..3] memo) | yes |
| 0x1F walk with collision | calls the 0x1E handler | via 0x1E | case :699 -> `Walk(..., allowDetour:false)` | yes |
| 0x20 wait Z distance | 0x8003D9BC | +8 +0xC | `WaitZDistance` :2659 | yes |
| 0x21 wait Z distance or Z contact | 0x8003DA28 (calls 0x20) | via 0x20 | case :726 | yes |
| 0x22 wait height target | 0x8003DA70 (`s1 = a3`; marker +8 = pc ptr) | +8 +0xC | `WaitHeightTarget` :2689 | yes |
| 0x23 wait height target or Z contact | 0x8003DB28 (calls 0x22) | via 0x22 | case :735 | yes |
| 0x37 wait | 0x8003E3DC (`lw 8(a3)` vs pc ptr; `+0xC` counter) | +8 +0xC | `Wait` :2548 (`Parameters[1]`, `[2]`) | yes |
| 0x5F wait (counters at +0x18, +0x1C; reads entity +0x114..+0x11C, +0xAC, +0xB0) | 0x8003F218 | +8 +0xC +0x10 +0x14 +0x18 +0x1C | none | **no (skipped by size)** |
| 0x66 copy global state into the entity state | 0x8003F794 (copies 0x38 bytes) | all | none | no |
| 0x6A/0x6B/0x6C/0x6D save entity +0x114..+0x11C / read back (read-only reading of the code) | 0x8003F8DC / F908 / F94C / F990 (data in +0xC..+0x14, set by 0x6A, read by the others, no marker) | +0xC +0x10 +0x14 | none | no |
| 0xB2 | 0x80041628 | +4 +8 +0xC | none | no |

The marker of the binary's waits is the pc **pointer** (`sw a2, 8(a3)` where `a2 = lw (a2)`, e.g. 0x8003E414); the port's is the code **index**
(`Parameters[1] = CodeIndex`) or, for walks, a signature of the operands. Both identify the instruction; the port's pc 0 collision is documented
(LoopGuard_ALoopWithAWait test comment) and no site in the corpus has `entry_pc == 0`.

Since every advancing instruction and every Break zero +8 (0x8004232C, 0x800421F4; port :461 and :407), a marker can survive into the next call's
InitializeEventData only if the previous call SUSPENDED (result 0) inside such a wait and nothing advanced after it. After re-initialization the
program starts again at its first instruction, so the surviving marker is consumed only when the program's FIRST instruction is that very wait
(an earlier advancing instruction zeroes it). Hence the census looks at first instructions only.
