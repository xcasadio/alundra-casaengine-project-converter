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

## T1 - phase A, the tests first (measured)

Start HEAD of T1: `fd8caaab` (the T0 docs commit). The skeleton that compiles: `ISoundPlayer.StopSfx(int)` as a default member that does nothing (D-G5C-1) and `SoundPlayer.StopSfx` with an empty body; `case 0xBE` and `case 0x75`
still call `PlaySfx`, `PillarAi` still calls its named stub. Test recorders: `RecordingSoundPlayer.StopRequests` / `StopSfx` (support) and `FakeSoundPlayer.StopRequests` / `StopSfx` (in `EventProgramRunnerTests`), both additive.

### The 24 new tests

| File | Tests | Red on the skeleton |
|---|---|---|
| `Alundra.Tests/Audio/SoundPlayerStopTests.cs` | 12: the voices of the id and no other (25 / 85, both tones of 85); the cap freed; the anti-duplicate table not written nor cleared (the play after a stop in the same frame refused; a stop alone does not register the id); id 0, an unknown id, an id not playing, a one-shot already finished (each next to a stop that acts on the same player); not gated by the armed state of the fade machine (where the play is); the redirection by `RefSfxId` (500 to 501); one stop cuts the sound of the other object (D-G5C-5) | 12 / 12 |
| `Alundra.Tests/Events/EventProgramRunnerTests.cs` (6 added) | `0xBE` asks `StopSfx((b2 << 8) | b1)` with `b2 != 0` and never plays; `0xBE` with the id 25 (`b2 == 0`); `0xBE` and `0x75` without a player (degraded, skipped by size) next to the same bytes with a player; `0x75` asks `StopSfx(b1)` (the byte after it is non-zero) and never plays; `0xBD` and `0x12` stay plays and ask no stop | 5 / 6 |
| `Alundra.Tests/Ai/Objects/PillarAiStopSoundTests.cs` | 4: the stop of the 25 once when the contact ends after a push, none while pushing, none on the next idle tick; none for a pillar never touched, next to the one pushed; the pillar of the map 26 in the real world (`PillarRun`): the player receives the stop once at the frame 103, none at 101 and 102, none after, the stop is not a play; a hero that walks past stops nothing, next to the one that pushed | 4 / 4 |
| `Alundra.Tests/Events/StopSoundSitesTests.cs` | 2: the census read from the export (392 `0xBE` on 119 maps, 12 on the chain maps 10, 135, 178, 391, 43 ids, 204 on the id 25, no `0x75`); each of the 392 sites through the real runner asks the stop of its id (3 bytes, no play, some ids above 255) | 1 / 2 |

Red on the skeleton: **22 of 24**. The two that are green from the start are pins by design, not new behaviour: the census of the data (it cannot be red on code) and "`0xBD` and `0x12` stay plays" (the plan asks them unchanged).
The 22 red tests name the missing behaviour (`StopRequests` empty, the voice still alive), none a compile or set-up failure.

Proof that the 24 are right and not only red: with the prototype body of the discovery wired by hand (the 12 lines of `SoundPlayer.StopSfx`, the two runner lines, the pillar's call; restored byte for byte from a backup, SHA-1 checked) the 24 are **green**, in particular the pillar
world test (stop at the frame 103). That wiring is not committed; phase B (T2) writes the real one.

### Suites on the skeleton (Release, neutralised copy, the same partitions)

| partition | tests | failures | of which new |
|---|---|---|---|
| 1 | 1 311 | 16 | 16 |
| 2 | 2 877 | 6 | 6 |
| 3 | 1 802 | 0 | 0 |
| sum | 5 990 | 22 | 22 |

5 990 = 5 966 + 24. Compared by name with the T0 trx: **no existing test moved** (the two of the predicted list, `PlaySound2Bis_0xBE_...` and `PlaySoundEffect_0x75_...`, are still green on the skeleton because the opcodes still call `PlaySfx`; they move in T2).
`python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 existing warning, `TextBoxGpu.cs`). The four `docs/hero-trace-389-*.txt` rewritten by the suites (line endings): `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0, restored by `git checkout`, never indexed.

## T2 - phase B, the code (measured)

Start HEAD of T2: `370bdeef` (T1). Written: `SoundPlayer.StopSfx` (every live voice of the REQUESTED id stopped by `AudioService.Stop` and forgotten; nothing for the id 0, an unknown id or a finished voice; not gated by the armed state; the
anti-duplicate table untouched), `case 0xBE` (`StopSfx((v[2] << 8) | v[1])`, 3 bytes) and `case 0x75` (`StopSfx(v[1])`, 2 bytes) of `EventProgramRunner.Dispatch`, the pillar calling `context.SoundPlayer?.StopSfx(PushSfx)` in place of its
named stub (the stub and its test seam `StopSoundStubCallsForTests` removed; the comments of `PillarAi.cs` corrected: the sound of the push loops). `ISoundPlayer.StopSfx` (default member) was already in the T1 skeleton. The prototypes of the
discovery were read, not applied; the code is written by hand.

### Existing tests that move

Exactly the two of the predicted list, rewritten by derivation (same bytes and same expected id, the assertion moves from the play list to the stop list, and the play list must be empty); renamed because the old names said "Play":

| before | after |
|---|---|
| `EventProgramRunnerTests.PlaySound2Bis_0xBE_Implemented_SameTwoByteDerivationAs0xBD` | `EventProgramRunnerTests.StopSound2Bis_0xBE_Implemented_SameTwoByteDerivationAs0xBD_AsAStop` (`StopRequests == [301]`, `Requests` empty) |
| `EventProgramRunnerTests.PlaySoundEffect_0x75_Implemented_DerivesIdFromSingleByte_NotTwo` | `EventProgramRunnerTests.StopSoundEffect_0x75_Implemented_DerivesIdFromSingleByte_NotTwo_AsAStop` (`StopRequests == [42]`, `Requests` empty) |

The two pillar files adapted without a changed expected value: `PillarRun.cs` (`Stops` now reads `Sounds.StopRequests`) and `PillarAiGoldenTests.cs` (the stop journal read from the `RecordingSoundPlayer`). No other existing test moved (by name, all green).
`PillarAiStopSoundTests.cs`: its summary comment only (the stub is gone).

### World tests (real `SoundPlayer` over the real manifest, `FakeAudioBackend`; helper `Alundra.Tests/Audio/RealSoundWorld.cs`, with `IgnoreStop`, the player of the HEAD before g5c)

| Test | Result |
|---|---|
| `Ai.Objects.PillarSoundWorldTests.TheScrapeLivesFromTheStartOfThePushToItsRelease_AndStopsAtTheFrame103` (map 26: silent 33 frames, one looping voice at frame 34, alive through the push, one play only, 0 voice after the frame 103) | red on the skeleton, green |
| `Ai.Objects.PillarSoundWorldTests.WithoutTheStop_TheSameScrapeNeverEnds_TenSecondsAfterTheRelease` (the contrast: 1 voice 500 frames after the release) | green (by design) |
| `Events.ButtonStopSoundWorldTests.Torla_TheBE25AtTheFrame215_EndsTheScrapeOfTheButton` (map 426, record 17: 1 voice at 214, 0 at 216 and at the end) | red on the skeleton, green |
| `Events.ButtonStopSoundWorldTests.Torla_WithoutTheStop_TheSameScrapeNeverEnds` (the contrast: 1 voice at 214, 216 and the end) | green (by design) |

Red check: `SoundPlayer.cs` put back to its T1 skeleton (empty `StopSfx`), the four tests run: the two positives fail, the two contrasts pass; the file restored (`git diff` shows the T2 body only).

### Red before, green after (T1 tests)

The 22 tests red in T1 on the skeleton are green now (the 24 of T1 all green); the two pins were green before and after.

### Suites (Release with the neutralised copy, then Debug last without it; `--blame-hang-timeout 60s`; the partitions of T0, 3 = the exact negation of 1 and 2)

| partition | tests Release | failures | tests Debug | failures |
|---|---|---|---|---|
| 1 | 1 313 | 0 | 1 313 | 0 |
| 2 | 2 879 | 0 | 2 879 | 0 |
| 3 | 1 802 | 0 | 1 802 | 0 |
| sum | 5 994 | 0 | 5 994 | 0 |

5 994 = 5 990 (T1) + 4 (the world tests above). `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: equal. `check_names.py --rules`: 0 finding (1 existing warning, `TextBoxGpu.cs`).
The four `docs/hero-trace-389-*.txt` rewritten by the suites (line endings): `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0, restored by `git checkout`, never indexed. Engine, converter, data and `alundra-datas-analyser` untouched.

## T3 - mutants, suites, ADR (measured)

Start HEAD of T3: `31de49ee` (T2). The two fresh verifiers are launched by the main session.

### Real mutants (script `mut.py` of this folder: apply the bytes to the file, build Release with the neutralised copy, run the filter on the seven test classes of the slice
(`SoundPlayerStopTests`, `EventProgramRunnerTests`, `PillarAiStopSoundTests`, `PillarAiGoldenTests`, `PillarSoundWorldTests`, `ButtonStopSoundWorldTests`, `StopSoundSitesTests`; 277 tests), restore the bytes; `git diff -- Alundra Alundra.Tests` empty after every batch, "RESTORED True")

19 mutants, **18 killed**, 1 survivor shown equivalent:

| # | Mutant | Red of 277 |
|---|---|---|
| 0 | `0xBE` still `PlaySfx` | 6 |
| 1 | `0xBE` as `StopAllSfx` | 6 |
| 2 | `StopSfx` cuts the other ids too | 5 |
| 3 | `StopSfx` does not forget the voices (`voices.Clear()` removed) | **0, EQUIVALENT** (see below) |
| 4 | `StopSfx` forgets the voices but does not stop them (the cap stays taken, the sound goes on) | 14 |
| 5 | only one tone stopped | 2 |
| 6 | `0xBE` id on 8 bits | 4 |
| 7 | `StopSfx` gated by the armed state | 1 |
| 8 | `StopSfx` registers the id in the anti-duplicate table | 1 |
| 9 | `0x75` forgotten (no call) | 3 |
| 10 | `0x75` still `PlaySfx` | 3 |
| 11 | `0x75` id on 16 bits | 3 |
| 12 | C76 stops unconditionally (call site) | 6 |
| 13 | C76 never stops (call site) | 6 |
| 14 | C76 stops another id (`PushSfx + 1`) | 6 |
| 15 | C76 `Step`: stop whatever the animation | 5 |
| 16 | C76 `Step`: stop only for the animation 0 | 6 |
| 17 | C76 `Step`: stop also in the push branch | 6 |
| 18 | `StopSfx` on an unknown id (`TryGetValue` replaced by the indexer) | 3 |

Deviation from the plan ("tous tués"), mutant 3 ("`StopSfx` qui ne libère pas le plafond"): removing `voices.Clear()` is behaviourally equivalent. `AudioService.Stop` releases the entry, so `IsAlive` is false and
`SoundPlayer.PlaySfx` prunes the dead voice (`PruneFinishedVoices`) before it counts the cap: the cap is freed by the stop itself, and `Clear` is redundant bookkeeping. The non-equivalent forms of "the cap stays taken"
are mutants 4 (the voice is forgotten but alive) and 5 (a tone left alive), both killed; the cap test is among the red ones of mutant 4.

### Suites (Release with the neutralised copy, then Debug last without it; `--blame-hang-timeout 60s`; partitions by test namespace of T0, 3 = the exact negation of 1 and 2; `part.sh` of the executor's scratchpad)

| partition | tests Release | failures | tests Debug | failures |
|---|---|---|---|---|
| 1 (`Arcs Ai Audio Cameras Cells Combat Dialogues`) | 1 313 | 0 | 1 313 | 0 |
| 2 (`Entities Events ExportGuards Hero Items`) | 2 879 | 0 | 2 879 | 0 |
| 3 (the rest) | 1 802 | 0 | 1 802 | 0 |
| sum | 5 994 | 0 | 5 994 | 0 |

- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: equal (Debug built last).
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding, 1 existing warning (`TextBoxGpu.cs`).
- The four `docs/hero-trace-389-*.txt` rewritten by the suites (line endings): `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0, restored by `git checkout`, never indexed: the hero traces are intact.
- Engine (`CasaEngineMonogame`), converter, data (`data-extracted`, `alundra-project`), `alundra-datas-analyser`, `tools/`: no diff since `de1861a2` (`git diff de1861a2 HEAD --stat` over these paths is empty).

### Predicted list of the moved tests against the measured one

Compared by name, all `trx` of the T0 base (5 966, all passed) against the final Release trx (5 994, all passed) and the Debug trx (5 994, all passed): **2 names disappear, 30 appear** (28 new tests of T1 and T2 plus the 2 renamed).
The 2 that disappear are exactly the predicted ones, renamed by derivation (`PlaySound2Bis_0xBE_...` to `StopSound2Bis_0xBE_..._AsAStop`, `PlaySoundEffect_0x75_...` to `StopSoundEffect_0x75_..._AsAStop`); the two pillar files (`PillarRun.cs`,
`PillarAiGoldenTests.cs`) are adapted without a changed expected value. No other existing test moved; no arc moved. Prediction and measure are equal.

### ADR and gaps

- ADR number recounted on all branches (`git branch -a`, `ls-tree docs/decisions/` of each): the last taken is 0089, so the new one is **ADR-0090** (`docs/decisions/0090-...md`, indexed in `docs/decisions/README.md`).
  ADR-0086 carries "amended by ADR-0090" on its status line.
- `engine-gaps.md` (this folder): G1 (no release per tone, the stop is net) and G2 (the mixer loop window, known, out of g5c).
