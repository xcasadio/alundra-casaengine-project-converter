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

## T1 - the red tests, the real-bytes theory, the arcs

Seven new files and one edit, in the mirror folder `Alundra.Tests/Events/` (ADR-0080; the plan's "Arcs/" is the folder of the 26 end-to-end scenarios only, and a file that drives `ArcRun` inside a domain stays in
that domain, as `HeightTargetSitesTests` does):

| file | content |
|---|---|
| `OpcodeWorld.cs` | the bare runner world (M1 of E19.sm3, R-SM-4): the hero first in `SpawnedEntities` and `PlayerEntity`, `IsPlayer`, `EntityRefId` -1; every proxy with its own non-null `LogicContextEntity`; `Result` 7 before; the program is the instruction then `0xFF` |
| `ContactVectors.cs` | the loader of the 202 `0xA9`/`0xAA` vectors (SHA-256 pinned), the world of a vector, `Check(id, expected)` |
| `ContactGroupOpcodesTests.cs` | U-A9-01 to 14 and U-AA-01 to 11 (named as in `sm3/tests.tsv`, the three `[Theory]` kept), U-A9-15 and U-AA-12 (the sm3 critic C2, in prose in the plan), U-ALL-01 reduced to A9-01 and AA-01, U-ALL-02, U-ALL-03 reduced to `A9 81` and `AA 81`, and the replay of the 103 vectors of `0xA9` and the 99 of `0xAA` |
| `LastTargetOpcodeTests.cs` | U72-1 to 6 (`sm4/report.md` 3.2; U72-6 through `RunScript` C, F, C), the new test of the size (the next instruction `1A 09` is reached, `Result` intact, the trace implemented), and the replay of the 67 vectors of `0x72` |
| `ButtonAndBlockOpcodesRealBytesTests.cs` | the theory on the real bytes: the 46 sites (20 `0x72`, 22 `0xAA`, 4 `0xA9`; bytes checked in the exported map at the pc, then run on a world deduced from the shape), and the guard of the counts that also asks the real runner (the oracle `IsPorted`) for the three opcodes |
| `ScriptedPushRun.cs` | the world of the arcs (`ArcRun`, real controller and prefabs, the discovery probe's set-up and numbering) |
| `ButtonAndBlockSitesTests.cs` | the arcs of T3, written now: the map 40 (north, east, south; the 12 instants of contact), the map 426 (the full push of the right button, the interrupted push and the wrong side, the second button with and without the flag of the first), the map 424 (S3-A1b and the south) |
| `Alundra.Tests/Support/AnnexFiles.cs` | `ButtonAnnex(relative)`, which finds `docs/plan-e20-annexe/e20-g5/g5b/` above the test binaries |

Annex: `vectors/` (the two vector files, `vectors/README.md`), `t1-red-names.txt` (the 112 red rows).

**Checked before they were committed.** (1) With the discovery's prototype installed in the copy of T0 the 95 unit and real-bytes tests and the 17 arc rows are all green: the expected values are right. (2) On the
real tree at the start HEAD (Release, the neutralised copy, `--filter` on the four classes): **112 tests, 112 red, 0 green**; every failure is a value mismatch (`Assert.Equal` 107, `Assert.True` 2, two messages of the
replays), none is an exception of the harness. The arcs take their numbers from the DLL with the prototype (D-G5B-5): see the doc comments of each test. `python -B tools/dll-structure/check_names.py --rules`:
215 DLL files, 465 test files, 0 finding, the same 1 warning.

**The "no effect" cases are never alone.** Each `Result`-0 vector starts from 7 (neither 0 nor 1), so it is red; each arc holds a positive effect beside its negative one (the map 40 south beside north and east, the
interrupted push and the wrong side beside the push that works, the second button without the flag beside the one with it, the map 424 whose counter of 40 never completes at the start HEAD).

### Red before, green after

| test (class) | rows | red before | green after |
|---|---|---|---|
| `ContactGroupOpcodesTests` (U-A9, U-AA, U-ALL, the two replays) | 40 | 40 | at T2 |
| `LastTargetOpcodeTests` (U72-1 to 6, size, replay) | 8 | 8 | at T2 |
| `ButtonAndBlockOpcodesRealBytesTests` (1 guard, 46 sites) | 47 | 47 | at T2 |
| `ButtonAndBlockSitesTests` (arcs of T3: the map 40 x1, its 12 phases, the map 426 x3, the map 424 x1) | 17 | 17 | at T2 (the arcs run on the same code) |

(The rows are those of `t1-red-names.txt`; the arcs of the map 40 phases are 12 rows of one theory.)

### What T0 settled for the arcs of E19.sm3

S3-A1b (the east push of the 424) is in. S3-A1 (the west push of the 424) and S3-A2 (the 421) are not: the geometry was not found (T0), so they stay optional (R-SM-6). S3-A3a, A3b and A7 (the map 440) were out of
T0's search; none is written (the plan: "sinon rien").
