# E19.sm2 value vectors (0x6A, 0x6B, 0x6C, 0x6D) - format, provenance, mapping

Candidate content of `docs/plan-e19-sm-annexe/sm2/` (T0 of the slice). Nothing here was written in the repository.

## Provenance

- Handlers: the REAL code of `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (outside the repository, path in `tools/lib.py`), table `0x80098FAC + 4*op`:
  `0x6A` -> `0x8003F8DC`, `0x6B` -> `0x8003F908`, `0x6C` -> `0x8003F94C`, `0x6D` -> `0x8003F990`. Run by `tools/emu.py` (a copy of the annex's minimal MIPS-I interpreter).
- Generator: `tools/vectors_sm2.py` (copy of the annex oracle's machinery, `sm1-sm4/work/oracle/`; Python 3.14.2, capstone 5.0.7). Call: `h(a0 = logic entity, a1 = owner, a2 = a3 = STATE)`,
  the faithful layout of `RunScript` (`0x800422C0-0x800422F8`): `STATE+0` holds the pointer to the instruction, `STATE+4` Parameters[0], `+8` Parameters[1], `+0xC/+0x10/+0x14` Parameters[2..4],
  `+0x2C` Result, `+0x30` `_30`, `+0x34` `_34`.
- Two independent models reproduce EVERY row (the run stops otherwise): the binary model (from the disassembly: `subu`, `bgez`/`negu`, `lbu`,`lbu`,`sll 8`,`or`,`sll 0x10`,`slt`,`xori 1`) and the
  DLL model (the int32 formula of rule SM2-R2, as `unchecked` C# computes it).
- Cross-checks with the annex's two oracles on 3 000 random worlds per opcode: 0 mismatch (`evidence/crosscheck_annex.txt`).
- Two runs of the generator give identical bytes: `vectors-sm2.tsv`, 1 114 rows + header, sha256 `b42f0f4558719b4a2b9e66f571826a47e2b0cc7b9f22ee6e331089228420b72b`.

## Footprint proof (an unmapped byte is a STOP)

Every data read and every write of every run is logged; the generator stops if one falls outside this table, if a mapped one is not read, or if any word of the window `0x80100000..0x80190000`
changes outside the mapped writes. The table is the mapping of every byte the handlers read to a DLL member:

| Binary address (logic entity `E`, state `S`, instruction pointer `*S`) | Size | DLL member (HEAD names) | Read by | Written by |
|---|---|---|---|---|
| `E+0x114` | 4 | `EntityScriptProxy.PosX` of the LOGIC entity (`Dispatch`'s `entity` parameter = `owner.LogicEntity ?? owner`) | 6A, 6B | - |
| `E+0x118` | 4 | `EntityScriptProxy.PosY` of the logic entity | 6A, 6C | - |
| `E+0x11C` | 4 | `EntityScriptProxy.PosZ` of the logic entity (binary Z = DLL Z + 1 at rest, ADR-0026: only an absolute read is affected) | 6A, 6D | - |
| `S+0xC` | 4 | `EventProgramState.Parameters[2]` (the OWNER's running state, `Dispatch`'s `state`) | 6B | 6A |
| `S+0x10` | 4 | `EventProgramState.Parameters[3]` | 6C | 6A |
| `S+0x14` | 4 | `EventProgramState.Parameters[4]` | 6D | 6A |
| `S+0x2C` | 4 | `EventProgramState.Result` | - | 6B, 6C, 6D |
| `S+0` then `*S+1`, `*S+2` | 4, 1, 1 | the instruction bytes: `v[1]`, `v[2]` of `Dispatch` (`FillDataFromCommand`, zero-extended 0..255) | 6B, 6C, 6D | - |

Not read and not written (checked): the owner entity (the vectors give it decoy positions `0x0DEC0114/0x0DEC0118/0x0DEC011C`), `Parameters[0]`, `[1]`, `[5..9]`, `_30`, `_34`, the stack, every other word of the window.
(`Parameters[1]` is cleared by the interpreter loop after every instruction that returns nonzero, in the binary `sw zero,8(s3)` at `0x8004232C` and in the DLL `state.Parameters[1] = 0` at `EventProgramRunner.cs:465`: not a handler write.)

## File format (`vectors-sm2.tsv`)

Tab separated, UTF-8, `\n`, one header line, no comments. All numbers are 8 hex digits of the 32-bit two's-complement word, except `op` (2 hex digits), `instr` (the instruction bytes, hex, space separated, exactly its size)
and `v0` (decimal). `Result` is read as a signed int by the DLL: `FFFFFFFF` is -1.

| Column | Meaning |
|---|---|
| `id` | `sm2-NNNN`, stable order |
| `group` | `save` / `random` (0x6A) ; `edge-threshold` (threshold below 0x8000 around `d` = thr-1, thr, thr+1, 0, 1, 2, int.MaxValue, int.MinValue, both signs) ; `edge-negative-threshold` (v2 >= 0x80: 0x8000, 0x8001, 0xFF00, 0xFFFF) ; `edge-wrap` (`memo - pos` overflows int32) ; `fresh-memo` (memo = 0: a 0x6D read with no 0x6A) ; `other-axes` (the two other axes random) ; `random` |
| `op` | `6A`, `6B`, `6C`, `6D` |
| `instr` | the instruction (`6A` ; `6B lo hi`) |
| `pos_x`, `pos_y`, `pos_z` | the LOGIC entity's `PosX/PosY/PosZ` before the instruction |
| `memo_p2`, `memo_p3`, `memo_p4` | `Parameters[2..4]` before |
| `result_in` | `Result` before (one of `00000000`, `00000001`, `00000007`, `FFFFFFFF`) |
| `v0` | the handler's return value = the instruction size (1, 3, 3, 3) |
| `result_out` | `Result` after (equal to `result_in` for 0x6A) |
| `memo_p2_out`, `memo_p3_out`, `memo_p4_out` | `Parameters[2..4]` after (equal to the inputs for 0x6B-0x6D) |
| `reads`, `writes` | the DLL members read and written, logged from the run (the same set for every row of an opcode) |

Counts: 0x6A 31 rows (30 change the memo: red today), 0x6B / 0x6C / 0x6D 361 rows each (253 / 258 / 253 change `Result`: red today). 318 rows have `v[2] >= 0x80`. Result histograms:
0x6B 211 ones / 150 zeros, 0x6C 205 / 156, 0x6D 208 / 153.

## How a C# test must set a row up

`logic = new EntityScriptProxy { PosX = (int)pos_x, PosY = (int)pos_y, PosZ = (int)pos_z }` ; `owner = new EntityScriptProxy { PosX = 0x0DEC0114, PosY = 0x0DEC0118, PosZ = 0x0DEC011C, LogicEntity = logic }` ;
`state = new EventProgramState { Codes = bytes(instr, 0xFF), Result = (int)result_in }` with `Parameters[2..4]` = the memo columns, `Parameters[0]`, `[5..9]`, `_30`, `_34` set to distinct sentinels ;
`runner.RunOneScriptCall(owner, state)` on a bare runner (no world context, `new GameState()`). Expected: `state.Result`, `Parameters[2..4]`, `CodeIndex == v0`, the first trace record `Kind == Implemented` with `Size == v0`,
`Parameters[1] == 0`, the sentinels intact, `logic` and `owner` positions unchanged.

## Regeneration

`python -B tools/vectors_sm2.py [out.tsv]` (reads the EXE at the path of `tools/lib.py`; deterministic: seed 20261018). `python -B tools/mutants_vectors.py`, `tools/mutants_rows.py`, `tools/killmatrix.py` replay the mutants.
Run from a COPY of the folder, never in place in the repository.
