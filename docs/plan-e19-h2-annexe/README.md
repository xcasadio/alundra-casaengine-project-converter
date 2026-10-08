# docs/plan-e19-h2-annexe - E19.h2 D1: binary tables of the NPCs in the air (read-only study)

> **Errata (independent check, `recompute/corrections.md`):** no number of this annex changes; C1-C9 retag labels and provenance ([R], [input], [I], [D]); **C3** corrects the A10J X pin (below). Read the corrections before using a value.

Annex of the plan `docs/plan-e19-opcodes.md` section 1.2n (E19.h, "E19.h2 esquissee", O-E19-27 / O-E19-28 / O-E19-40), tranche **D1** of `docs/plan-e19-m-annexe/night-discovery-2026-10-09/h2.json`. It archives the (until now unversioned) emulation tools, replays the jumps of the NPCs on the real code of `ALUN_CD.EXE` (France), and compares the result with the DLL pins. **Nothing in the production tree was touched**: no `Alundra/`, `Alundra.Tests/`, `CasaEngineMonogame/`, `alundra-datas-analyser/`, plan or backlog file was modified, nothing was built or run with dotnet, nothing was staged or committed. The only new files are in this folder.

Read in this order: `notes.md` (findings, order of the tick, decision note, author questions) -> `values.md` (the measured numbers) -> `dll-delta.md` (DLL against binary per pin, mechanisms (a)-(d)).

## 1. Files

| file | what it is |
|---|---|
| `notes.md` | findings and corrections of `h2.json`, order of the tick (B/C), decision note on the scene-loop emulation, the four author questions copied unchanged |
| `values.md` | every measured value, generated from `out/*.json` by `tools/make_values_h2.py` |
| `dll-delta.md` | per pin: DLL value (test `file:line`), binary value (replay), gap, mechanism (a)-(d); measured / inferred / read tags |
| `out/replay_<case>.txt` | per-update table of one case: PosZ (binary and DLL convention), ForceZ, FinalForceZ, contact `+0x140`, IsOnGround `+0x144`, Terrain `+0x138`, FloorHeight `+0x134`, value read by `0x25` at s+k, agreement of the two engines and the three models. Cases: `S-1360 S-1280 S-768 S-1024` (synthetic montage of the DLL tests), `S-TRV` and `S-TRV-keep` (bouquet, with and without the `1B [0,0]` tail), `R-1360-Giles10 R-1360-Bergus165 R-1360-Bergus179 R-1360-Bergus172 R-768-Dog478a R-768-Dog478b R-1280-GilesAnim16 R-1024-Bomb362 R-TRV-rec69 R-TRV-rec81` (real prefab flags / box / AnimSets on their real map cells) |
| `out/replay_all.json`, `out/replay_summary.txt` | the same data machine-readable; one-line summary per case |
| `out/giles_ledge.txt`, `out/giles_ledge.json` | Giles' jump onto the 16 px ledge on the real cells of map 10 (with sweeps of the assumed start) and the ledge fall |
| `out/rules_vs_emu.out`, `out/validate.out`, `out/zmodel_dll.out` | the validations of the models (21000 ticks / 460 scenarios x 40 ticks + 3000 handler pairs + 3 mutation checks / 200000 random runs) |
| `out/prefabs_survey.txt`, `out/programs_listing.txt` | prefab flags, boxes, AnimSets and animation ends of the records used; listings of the event programs read (`1A`, `1B`, `37`, `25`, ...) |
| `tools/` | see section 2 and 3 |

## 2. Tools

Archived copies of the unversioned scratchpad tools (original path in brackets; `S` = the session scratchpad):

| file | origin | role | sha256 of the original | sha256 of the copy |
|---|---|---|---|---|
| `zsim.py` | `S/h1b2-disc/binary/zsim.py` | runs the REAL physics pass 0x80038364, the list builder 0x800384F4 and the REAL opcode handlers on cells of the exported maps; python re-write of the RunScript loop (14 lines) | `1a568b77...` | `1f318f9f...` (1 edit) |
| `mips.py` | `S/h1b2-disc/binary/mips.py` | minimal MIPS-I interpreter over the EXE image | `3aa32b7f...` | identical |
| `zmodel.py` | `S/h1b2-disc/binary/zmodel.py` | python rules of the vertical step of one entity (binary convention) | `8ea93b47...` | identical |
| `zmodel_dll.py` | `S/h1b2-disc/binary/zmodel_dll.py` | the same rules in DLL convention (binary - 1) | `77f71ccd...` | identical |
| `rules_vs_emu.py` | `S/h1b2-disc/binary-verify/rules_vs_emu.py` | the counter-verifier's own rules model (class `M`) against the real code; also the source of `M` used by `scenarios_h2.py` | `a82b6256...` | `141ff503...` (1 edit) |
| `m.py` | `S/e19h-disc/A/tools/m.py` | map loader (`Map`, `DATA`) that `zsim.py` imports | `c5b88d36...` | `1ee36b4d...` (2 edits) |
| `vz.py` | `S/h1b2-disc/binary-verify/vz.py` | harness of the counter-verifier (REAL RunScript 0x8004205C on the `emu.py` core) - **added**: `rules_vs_emu.py` imports it | `47a6da2c...` | `77c450df...` (1 edit) |
| `emu.py` | `S/f3-disc/binary/emu.py` | MIPS-I core used by `vz.py` - **added** (equal to the versioned `docs/plan-e19-pad-age-annexe/emu.py` apart from the path line) | `26dfa17e...` | `1a4475a7...` (1 edit) |
| `lib.py` | `S/e19j-disc/lib.py` | EXE loader used by `emu.py` - **added** (byte-identical to `docs/plan-e19-pad-age-annexe/lib.py`) | `ebbf0ae9...` | identical |
| `scen_real.py`, `traces.py`, `validate.py` | `S/h1b2-disc/binary/` | the scripts that produced `docs/plan-e19-h1b2-annexe/scen_real.out`, `traces.out` and the validation of `zmodel.py` - **added** to prove that the archived copies reproduce the versioned outputs | `4a8c8ab5...`, `5a8b726a...`, `42bace1e...` | identical |
| `scenarios_h2.py` | **new** (this study) | the replays of `out/replay_*` with engines A (zsim) and B (vz) and the three models; entity set-up, UpdateAnimation stand-in, per-update tables | - | - |
| `giles_ledge_h2.py` | **new** | Giles and the ledge (XY + Z, animation table in memory), sweeps, hand arithmetic of the ledge | - | - |
| `make_values_h2.py` | **new** | writes `values.md` from the JSON outputs | - | - |
| `survey_h2.py`, `listing_h2.py` | **new** (from two scratch helpers) | prefab survey and program listing | - | - |

### Edits made to the copies (no computation changed)

1. `zsim.py` line 18: `A = r"C:/Users/casad/.../scratchpad/e19h-disc/A/tools"` -> `A = HERE   # h2 annex edit: ...` (`HERE` is the folder of the script, defined line 14; `m.py` now sits next to `zsim.py`).
2. `m.py` lines 9 and 15: the two files read from `S/e19h-disc/A/head/` (copies of `EventOpcodeSizeTable.cs` and `AlundraEventProgramRunner.cs` frozen at an older HEAD) -> `ROOT + "/Alundra/Scripts/EventOpcodeSizeTable.cs"` and `ROOT + "/Alundra/Scripts/AlundraEventProgramRunner.cs"` (the live files, as `research/census/walks-0x0b/m.py` does). `ROOT` (absolute path of the repository, as in `mipsdis.py`) is unchanged.
3. `vz.py` lines 7-9: `SP = r"C:\Users\...\scratchpad"` and the two `sys.path.insert(0, os.path.join(SP, ...))` for `f3-disc/binary` and `e19j-disc` removed (replaced by a comment); the existing line that inserts its own folder stays, and `emu.py`, `lib.py` now sit there.
4. `emu.py` lines 7-8: `SP = ...` removed; `sys.path.insert(0, os.path.join(SP, 'e19j-disc'))` -> `sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))` (this is the form of the versioned `docs/plan-e19-pad-age-annexe/emu.py`).
5. `rules_vs_emu.py` line 3: `import sys, random; sys.path.insert(0, '.')` (depends on the current directory) -> `import sys, os, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))`.

Kept as they were: the absolute path of `ALUN_CD.EXE` (`D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE`, sha256 `5ba6e0db...`) in `mips.py` and `lib.py`, as in `docs/plan-e19-backlog-1006-annexe/mipsdis.py`; `ROOT` / `DATA` (the exported project) in `m.py`. `zsim.py` still swallows a failed import of `m.py` (`except Exception: Map = DATA = SPR = None`); `scenarios_h2.py` stops with an explicit message if that happens.

## 3. `m.py`: can the repository copy replace it?

`diff --strip-trailing-cr` between `S/e19h-disc/A/tools/m.py` and `research/census/walks-0x0b/m.py` shows exactly two lines (9 and 15): the scratchpad copy reads frozen copies of `EventOpcodeSizeTable.cs` and `AlundraEventProgramRunner.cs` (an older HEAD, kept in `S/e19h-disc/A/head/`), the repository copy reads the live files. The files differ in line endings only otherwise (CRLF in the repository). **Yes, the repository copy can replace it**: `zsim.py` uses only `Map` and `DATA`, and the `Map` class is the same code; the two files that differ only feed `SZ`, `NM`, `CASES`, used by `fmt()` / `dump()` for listings (live table: opcode `0x5F` is 8 bytes and `0x78` is 3, names of `0x35` / `0x36` swapped back, by E19.m8 / E19.m9). `tools/m.py` here **is** the repository copy: after edit 2 it is identical to it modulo CRLF (`diff --strip-trailing-cr tools/m.py ../../research/census/walks-0x0b/m.py` is empty). The reason to keep a copy: the annex then does not depend on a folder under `research/`. Risk to know: `m.py` reads two production sources at import; if one of them is mid-edit, the import can fail and `zsim.py` swallows it (see above).

## 4. Reproduce

From any directory (imports are relative to each script; the output directory is the argument; Python 3.14.2 with `capstone` 5.0.7 for `lib.py`; always `-B`):

```
cd D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-h2-annexe
python -B tools/scenarios_h2.py out          # about 2 s   -> out/replay_*.txt, replay_all.json, replay_summary.txt
python -B tools/giles_ledge_h2.py out        # about 16 s  -> out/giles_ledge.txt, giles_ledge.json
python -B tools/make_values_h2.py            # -> values.md
python -B tools/rules_vs_emu.py   > out/rules_vs_emu.out   # about 20 s
python -B tools/validate.py 400   > out/validate.out       # about 1.5 min (460 scenarios)
python -B tools/zmodel_dll.py     > out/zmodel_dll.out
python -B tools/survey_h2.py      > out/prefabs_survey.txt
python -B tools/listing_h2.py 10 4640 4745                 # <map> <first pc> <last pc>
python -B tools/scen_real.py | diff --strip-trailing-cr - ../plan-e19-h1b2-annexe/scen_real.out   # identical
python -B tools/traces.py    | diff --strip-trailing-cr - ../plan-e19-h1b2-annexe/traces.out      # identical
```

Verified at the end of the study: a second run of `scenarios_h2.py` and `giles_ledge_h2.py` into a scratch directory gives byte-identical files; `scen_real.py` and `traces.py` reproduce the versioned `docs/plan-e19-h1b2-annexe/scen_real.out` and `traces.out` byte for byte (modulo CRLF); `validate.out` shows the same counts as the first discovery's (460 scenarios, 0 mismatch, mutations caught in 311 / 30 / 87).

## 5. State of the repository

- `git rev-parse HEAD` at the start of the study: `f06e08736079f0d14c8c1428ff2d9a691c311724` (branch `chantier/e19-suite3`). Another process committed meanwhile; at the end: `53050272d5d094093f6606922f23d4a518fbf88b`. `git diff --stat` between the two touches only `docs/plan-e19-opcodes.md`, `docs/plan-e19-m-annexe/backlog-2026-10-03.md`, `Alundra.Tests/AlundraHeroFallAndPadJumpTests.cs`, `Alundra.Tests/AlundraHeroObjectTopsTests.cs` and the engine pointer (`a24fe31d` -> `b2205d9f`, `ProjectSettingsHelper.cs` and its test): **none of the files cited in `dll-delta.md` changed** (`AlundraEntityScriptProxy.cs`, `AlundraFrameSyncPasses.cs`, the five test files of the pins, `CharacterControllerComponent.cs`, `SpriteWriter.cs`). Plan line numbers are not cited by this annex except through `h2.json`.
- The exported project read (`alundra-project/Maps`, `alundra-project/Data/sprite-records.json`) and the EXE are read-only inputs.

## 6. Limits (what these files do **not** show)

- **UpdateAnimation (0x80038E18) is not executed.** The stand-in is the three lines of the hero's `run_ticks.py` (Current = Target, `+0x9C`, `+0xF8` = IZF, `+0xB4`): valid for a jump with **one** animation change. The second `1A [2]` of Bergus @839 (animation already requested) cannot be decided here.
- One entity per world: no support by other entities, no carried entity (`+0x28`), no slide tile; the scene loop, the B/C scheduler, the dialogs and the appearance of the records are not emulated. XY forces of the NPCs are **injected** (Giles: walking animation 9 steady; ledge: 65536 per tick) and the start x of Giles is the plan's DLL-derived value (swept in `out/giles_ledge.txt`).
- The two engines share the data they were given (cells, prefab fields, the stand-in): they are independent in interpreter, entity initialisation and script runner, not in those assumptions. The Z-only cases are also checked against three rules models; the Giles and ledge cases are checked by the second engine and, for the ledge, by hand arithmetic.
- **No DLL value was measured.** Every DLL value in `dll-delta.md` is read from a test or a document, or marked inferred.
- Not replayed although listed in the slice: the `1B` rise/fall of maps 174 and 181, the fall of 135 `C[9]` (programs listed in `out/programs_listing.txt`).
