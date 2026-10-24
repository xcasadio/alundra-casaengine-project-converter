# E20.g5c - execution record

Plan: `docs/plan-e20-heros.md` section 1.24.6, "E20.g5c" (revision 1, READY; D-E20-67; D-G5C-1 to D-G5C-8). Branch of the parent
`chantier/theme1-porter-plateforme`. Start HEAD of the execution: `de1861a2` (E20.g2b done and CONFIRMED at the plan).
Discovery annex: `docs/plan-e20-annexe/e20-g5/g5c-disc/`.

## T0 - prerequisites, re-reading, replay, counts

### Prerequisites (checked)

- E20.g2b is closed at the plan: `de1861a2 docs(plan): E20.g2b done and CONFIRMED by two fresh verifiers`; its implementer of `ISoundPlayer` (`BreakRun.Sounds`) is in the tree.
- `git status` at the start: no modification under `Alundra/`, `Alundra.Tests/` or `docs/plan-e20-annexe/`; only the known marks of the two submodules
  (`CasaEngineMonogame`, `alundra-datas-analyser`) and the author's untracked files under `tools/`.

### Sites re-read at the start HEAD (by symbol)

- `EventProgramRunner.Dispatch`: `case 0xBE` (still `PlaySfx((v[2] << 8) | v[1])`, length 3) and `case 0x75` (still `PlaySfx(v[1])`, length 2); `0xBD` and `0x12` call `PlaySfx`
  with the same derivations. The labels of `EventOpcodeSizeTable.cs` are untouched (D-G5C-7).
- `PillarAi`: the stub `StopSoundEffect` (private) and the test seam `StopSoundStubCallsForTests` (`[ThreadStatic]`, read by `PillarRun.cs:54,97` and `PillarAiGoldenTests.cs:87,94`).
- `SoundPlayer`: `_liveVoicesBySfxId` keyed by the requested id; `StopAllSfx` is the only stop.
- Implementers of `ISoundPlayer` at the start HEAD: **25** (1 of production, `SoundPlayer`; 24 of test, among them `BreakRun.Sounds` from g2b). Same number as the discovery's.
- Census of the sites, replayed from a copy (`sites_stop.py` over `alundra-project/Maps`): `0xBE` **392 sites on 119 maps, 12 on the 4 maps of the story chain**; `0x75` **0**; `sites_stop.json`
  equal to the annex's and `sites_stop_summary.txt` equal (line endings aside).

### Replay of the binary oracles (from a copy in the session scratchpad; the `emu` folder of `work/` is the copy of `docs/plan-e14-annexe/e14a/oracle-a1/tools/emu.py`, as the annex's header says)

- `stop_oracle.py 3000`: output equal to `stop_oracle_out.txt` (3 000 vectors, 0 mismatch; 70 / 70 instructions of Stop and the voice finder; the Play front 120 / 137). With `gold`: the
  7 goldens, SHA-256 `f75208112cbd5b0dba1635b0544d2ed7e63734c89c14de1f8fdb1023908d3e06`, equal to `stop_goldens.json`. With `mut`: 88 mutants, 69 killed, 19 survivors (the annex's figures).
- `handlers_oracle.py`: output equal to `handlers_oracle_out.txt` (400 vectors x 4 handlers, 0 mismatch): `0xBD` Play 16-bit id, `0xBE` Stop 16-bit id, `0x12` Play 8-bit id, `0x75` Stop 8-bit id.

### Test counts at the start HEAD (Release, neutralised copy)

`--list-tests`: **5 966** tests (the 5 913 of the discovery, plus the 53 of g2b). Partitions disjoint by test namespace (`part.sh` of the executor's scratchpad;
partition 3 is the exact negation of the other two): 1 = `Arcs Ai Audio Cameras Cells Combat Dialogues`, 2 = `Entities Events ExportGuards Hero Items`, 3 = the rest.

| partition | tests | failures |
|---|---|---|
| 1 | 1 295 | 0 |
| 2 | 2 869 | 0 |
| 3 | 1 802 | 0 |
| sum | 5 966 | 0 |

(no test runs in two partitions here: the filters are namespace-exact, so the sum equals the list.)

### Predicted list of the existing tests that move (re-measured)

Method of the discovery (`cmp_trx_out.txt`): the two prototype diffs (`prototype_stopsfx.diff`, `prototype_pillar_wiring.diff`) applied to the tree, Release build, the three partitions run
again with a trx, then the files restored by `git checkout` (diff empty after). Result: 5 966 tests before and after, **exactly two** went from passed to failed, none the other way:

1. `Alundra.Tests.Events.EventProgramRunnerTests.PlaySound2Bis_0xBE_Implemented_SameTwoByteDerivationAs0xBD`
2. `Alundra.Tests.Events.EventProgramRunnerTests.PlaySoundEffect_0x75_Implemented_DerivesIdFromSingleByte_NotTwo`

The pillar tests (`PillarAiGoldenTests`, the `PillarRun` ones) stay green with the wiring and their two files adapted (no expected value changes). The list equals the plan's: no stop.

Other facts: the four `docs/hero-trace-389-*.txt` were rewritten by the suites (line endings), `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0, restored by `git checkout`, never indexed.
The engine, the converter, the data and `alundra-datas-analyser` are untouched.
