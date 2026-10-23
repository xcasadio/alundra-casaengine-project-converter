# E20.g5b: the opcodes 0x72, 0xA9 and 0xAA, measured results

Plan: `docs/plan-e20-heros.md` section 1.24.6, sub-section "E20.g5b" (revision 1, READY). Discovery annex: `../g5b-disc/`. Start HEAD: `39b5c2e7` (branch
`chantier/theme1-porter-plateforme`, after E20.g5a). This file is written task by task; this first version is T0 (the commit of docs) and T1 (the red tests).

## T0 - re-reading, replays, probes, prediction

### Oracle and vectors replayed from copies (scratch, outside the repository)

`../g5b-disc/` and the MIPS machine `../../e20-g1a/work/` (as `work/`) were copied to a scratch folder; the sm3 and sm4 annexes were copied likewise (the
scripts write next to themselves and read `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`, checked, at the path `lib.py` names).

| replay | command | result |
|---|---|---|
| the three handlers and the search against the real code | `python -B -X utf8 ops_full.py 9000 mut gold` (57 s) | 9 000 handler vectors and 8 899 search vectors, 0 mismatch; the output equals `../g5b-disc/ops_full_out.txt` (line endings aside); 78 one-word mutants, 72 killed, 6 survivors (the `move` to `subu` equivalents); `ops_goldens.json` rewritten byte for byte equal, SHA-256 `c4c75a9146eaa239e8d8b3cb38cb4a2c3da82b6f369f080d0a6acc5ae0027dba` |
| the 837 sm3 vectors (A9 and AA are 28 of them) | `python -B -X utf8 gen_vectors.py out` in a copy of `docs/plan-e19-sm-annexe/sm3/vectors/` | `OK: every read mapped, no unmodeled write, binary == python model on every vector`; `sm3-vectors.jsonl` (837 lines), `read-map.json`, `stats.json` equal to the annex (line endings aside) |
| the sm4 vectors of `0x72` (67) | `python -B -X utf8 gen_vectors.py ../out 20261018 60` in a copy of `docs/plan-e19-sm-annexe/sm4/tools/` | `op72.json` equal to the annex, SHA-256 `0158fd87cc773f71ed1cba911c6cf4dd17b6a5db3f8eb4bfa9bfd6d3b5c972e9` (the manifest value) |

### The sites, re-read at the start HEAD (by symbol)

- `EventProgramRunner.Dispatch`: `default: return UnknownOpcode(command, state);` at `Alundra/Events/EventProgramRunner.cs:2110-2111`; `UnknownOpcode` at `:3073`; the logic
  entity `var logic = entity.LogicEntity ?? entity;` at `:431`; `case 0x71` at `:791`; `case 0xB8` at `:2062`. No `case` for `0x72`, `0xA9`, `0xAA`.
- `EntityScriptProxy`: `TargetAnimationId` and `TargetDirection` (`uint`) at `:158-159`; `LastTargetAnimationId` and `LastTargetDirection` (`uint`) at `:1225-1226`;
  `XCollisionEntity` (`EntityScriptProxy?`) at `:364`, written by `MoveControllerAndPullPosition` (`:2424-2431`).
- `EventOpcodeSizeTable`: `0x72` is `new(1, "Set LastTargetAnim and Direction")` (`:160`); `0xA9` and `0xAA` are `new(2, "")` (`:217`, `:218`).
- The mirror `ImplementedOpcodes`: `Alundra.Tests/Arcs/HeadlessIntroSimulation.cs:85`. No test names the three opcodes.
- `StoryChainSkippedOpcodesTests` (`Alundra.Tests/Events/StoryChainSkippedOpcodesTests.cs:20`) derives its oracle `IsPorted` from the real runner (`StoryChainOpcodeAudit`);
  the three opcodes have no site on the 30 maps of the chain.

### The probe replayed on the start HEAD with the prototype

A copy of the start HEAD (`Alundra`, `Alundra.Tests`, `docs`, `tools` from `git archive HEAD`; the engine and the export from the discovery's copy; no `Musics`),
the discovery's `prototype_ops.diff` applied to `EventProgramRunner.cs` (it applies cleanly: the runner is byte-identical to the discovery's), built in Debug.
`G5_CASES=../g5b-disc/cases_a.json` (8 cases) and `cases_phase.json` (12 cases): all the `.txt` and `.trace.txt` outputs are equal to `../g5b-disc/probe_out_proto/`
(line endings aside). Nothing to re-measure on the maps 40 and 426: 0 to 5.5 px south for the map 40, 80 px in 160 ticks and the flag 1266 for the button of the 426.

### Height of the hero in front of a Mini-Nilude block (maps 424 and 421)

Cases in `t0-cases/`, outputs in `t0-out/`. Coordinates in pixels, `z` as the probe prints it.

**Map 424, block record 3 (type 433) at (912, 448), z 16** (the program C[3] at `@692`, `AA 81` at `@700`, `0x57` at `@708` with the table
`[245, 245, 245, 9]` for the hero animation directions 0, 0x10, 8 and 0x18, so only a push towards the east, hero direction 0x18, takes `@717`):
- The floor around it, from a grid of `PlaceHero` at z 16 (`pg424`): the block sits in a pit whose floor is z 16 to the south (y 464 and more) and to the east (x 936 and
  more, y 448). To the west (x 888 at y 448, z 48; x 864, z 64) and to the north (y 432 and less, z 64) the floor is higher, so a hero there stands above the
  block and never touches it (the discovery's probes at z 48 and 64 had no contact for that reason). From x 864 the hero cannot even walk east (blocked at once, `adj1`).
- **East** (`h424_east_L`, hero at (960, 448, 16), `L` held): the hero steps to 48 at the first frame then settles to z 16, contact (`xc=3`) from the pad frame 13 at x 946;
  the first `0x74` is at the trace frame 16 (so `0xAA` returned 1 from the previous tick), 40 consecutive `0x74` (frames 16 to 55), then `0x57` at `@708` takes the entry
  of the direction 8 to `@697` (`0x73`): the block does not move and `@717` is never reached. This is the arc S3-A1b of sm3.
- **South** (`h424_south2_U`, hero at (912, 472, 16), `U` held; (912, 490) is a ramp, z 32, no contact): contact from the pad frame 2, 40 `0x74` (trace frames 11 to 50), `0x57`
  takes the entry of the direction 0x10 to `@697`: no motion, `@717` never reached.
- **West** (S3-A1, the push the script was written for): NOT reachable by walking in the DLL. The hero cannot stand west of the block at the block's height.
  Not measured further; the arc S3-A1 stays optional (R-SM-6, Q-G5B-3). A flag to the author's attention: the original script slides block 3 only for a push from the
  west, and the DLL puts the floor to the west above the block.

**Map 421, blocks records 8 to 12 (C[8] at `@1464`, `AA 81` at `@1467`)**: the hero is frozen by the control word (`ctl4`, then `ctl14` after 300 settle frames) whatever the
start in the cases tried (west, north, east, south of record 8, z 64, `h421_s300*`): the program B[1] at `@180` waits on the flag (0, 5) (`0x36`) and the map's introduction
holds the control. No start height was found for the map 421 in this pass: the arc S3-A2 stays optional.

### Prediction of the existing tests that move

Measured with the discovery's prototype installed in the copy at the start HEAD, the whole suite in four partitions (Debug, `--blame-hang-timeout 60s`), against the same
copy without the prototype. The copy fails tests of its own environment (no `Musics`, an older export): the failures are the same set with and without the prototype.

| partition | tests | failures at the HEAD runner | failures with the prototype |
|---|---|---|---|
| `Alundra.Tests.Arcs.` | 76 | not run (0 expected, as g5a) | 0 |
| `Ai.`, `Audio`, `Cameras`, `Cells`, `Combat`, `Data`, `Dialogues` | 1 134 | 7 (same set) | 7 |
| `Entities`, `Events`, `ExportGuards`, `Hero`, `Items` | 2 485 | 0 | 0 |
| everything else | 1 790 | 7 | 7 (same set) |

The 14 environment failures of the copy: `MusicPlayerTests` x2, `WarpDepartureTests` x7, and five projectile tests that read the copy's older export
(`ChargedArrowLifeTests.C6` x3, `C7`, `ArrowLifeTests.E7`); none passes or fails differently with the three `case`s. They pass in the real tree (g5a: 5 484 tests, 0 failures).

**Predicted list for T2: no existing test moves.** (T2 measures it again with the final code.)

### Scratch outputs kept here

`t0-cases/` (the four case files written in T0), `t0-out/` (the text output of each case; the traces only for `h424_east_L` and `h424_south2_U`).
