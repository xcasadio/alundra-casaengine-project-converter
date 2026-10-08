# Counter-check of the L-a discovery (engine `f86a981b`), 2026-10-08

Fresh counter-check of `../notes.md`, `../sim.py` and `../sim.out`. **Nothing was built, run, edited, staged or committed
in the repository or its submodules**; only reads (`git show`, `git grep`, `git ls-tree`, `git archive`). Every run below
happened in a physical copy, `build/engine` = `git archive f86a981b` plus MGUI `18b2c14a` and NvgSharp `9c0da031` (the
gitlinks of `f86a981b`). Builds ran with `-nodeReuse:false -p:UseSharedCompilation=false`, so no build server outlived
them, and every test host exited. No game, demo or DLL was started. Details and paths: `notes.md`.

Method: I re-read every anchor, then **implemented rules R1-R6 as a minimal prototype** (`proto/apply_t1.py`) and wrote
the report's written-in-advance values as **real xUnit tests** (`proto/VerifyLoopStartTests.cs`, 26 cases). Then I ran
them, ran every mutation the report names (plus "no cap"), and ran T2 alone on the existing suite.

Labels: **F** = read in code, measured or computed; **H** = hypothesis.

## Verdict

**CONFIRMED, with minor corrections.** Every decision-carrying fact and every value in the table holds. They now hold
from a real run in the copy, not only from the model. The predicted test movement is exact, and so is the mutation
map. None of the corrections changes the slice, the two tasks, the file list or the questions. Three of them belong
in the plan's wording.

## Claims

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 1 | Per-call cap already on both feeding paths, `MusicPlayer.cs:358-360` and `:325-327`; no music test pins it | CONFIRMED | F: read. `ReportsZeroPendingBuffers` is used only by `AudioServiceStereoVoiceTests.cs:210` (`git grep`). |
| 2 | Two loop sites, both rewind: `FillQueue` `:373`, worker `StreamingWorker.cs:263`; the worker is the production path (`AudioService.cs:98`); `Acquire` `:60` and `Reset` `:205` carry no loop start; `Play` copies only `IsLooped` `:123` | CONFIRMED | F: read. `SoundEmitterComponent.cs:142` also streams through `Music.Play`, and `AudioService.PlaySoundCore` refuses a streamed asset (`:685-692`). So `MusicPlayer` is the only streaming path. |
| 3 | `SoundAsset.ReadNumber` returns a float (`:348-362`); 16 777 217 would become 16 777 216 | CONFIRMED | F: read. The float-read mutation turns S6 red (run). |
| 4 | `CreatePreviewCopy` (`SoundAssetInspectorPanel.cs:318`) can become `internal` and be tested | CONFIRMED | F: `CasaEngine.Editor/InternalsVisibleTo.Tests.cs:3`; P1 compiled and passed in the copy. |
| 5 | Demo keys O and H taken; free letters A, E, Q, U, W, X, Y, Z | CONFIRMED | F: `AudioDemo.cs:337-480` uses B C D F G H I J K L M N O P R S T V, Space, PageUp/Down and the arrows. Wording: `DemosGame.cs` itself binds only F1, Enter and Escape (`:517-532`). Add/Subtract come from `PlayerComponent.cs:81-87`, and the arrows from the demo browser (`DemoKeyboard.cs`). No impact. |
| 6 | Rules 1-8 | CONFIRMED (feasible as written) | F: the prototype implements R1-R6 in about 60 lines over the 7 runtime/editor files. It builds, and the existing suite stays green. |
| 7 | Value table (L1, L1w, L2, L2w, L3, L4, L5, L6, L8, B2, W1, W7, S1/S4/S5/S6, P1) | **CONFIRMED by a run** | F: `results/t1.trx`, 1315/1315 = 1289 existing + 26 new. Each value in the table is an assertion that passed: 4000/1600/1600 then 3 × 1600; the 9 sizes of L2/L2w with the worker crossing the seam; L5 for 1000 and 5 000 000 with exactly one warning; L8 4000/4/4; B2 7200 then 12 000 (zero-pending fake, recording off, `Task.Wait(10 s)`); W1 offset 60, position 2400, frames (600,10600)..(603,10603), then 2416; W7 offset 46, and the naive read gives (10599,600); S1, S6 (16 777 217 exact) and the -5 clamp; P1. The L7 allocation check (65 536 frames, loop start 32 768, 200 updates: 0 B) and S7 (`"600"`, 600.5, `true` → 0, one warning each) pass too. |
| 8 | Mutations and the tests they turn red | **CONFIRMED by a run** | See the mutation table. Each named mutation turns at least the named tests red. |
| 9 | Depth table and margins | CONFIRMED (arithmetic) | F: `margins.out`: 3 → 95.9 / 185.8, 6 → 374.6 / 464.4, 24 → 2046.4 / 2136.2 ms. 351.5 ms is the Debug worst 2-frame fill gap (`docs/plan-e19-backlog-1006-annexe/o63/combined.md:14`). Correction (minor): the worst seam buffer of all tracks is 81 frames, see M2. |
| 10 | Costs: 384 KiB queue, 768 KiB ring per track, 384 KiB read in `Play`; mixer max 64 buffers per voice | CONFIRMED | F: ring = slot size × `2 × target` (`MusicPlayer.cs:134`, `StreamingWorker.cs:170`); channels are never freed (`Acquire` only grows the array, `:76-83`); `VoiceChunkQueueCapacity` 128 (`SoftwareMixer.cs:74`); 16 KiB = 2 chunks of 4096 samples (`:40`, `:54`, `:862-863`); pool grows to 4096 (`:41`). |
| 11 | Closed file list | CONFIRMED, one wording fix | Every runtime and editor change of the prototype falls in the listed files. See M1 for `SoundAssetTests.cs`. |
| 12 | ADR 0072 is the next free number | CONFIRMED | F: `git ls-tree` over every local and remote branch: max 0071 (`chantier/e19-suite2`), `main` 0070; worktrees ≤ 0070; nothing untracked; no duplicate number. |
| 13 | T1 moves no existing test | **CONFIRMED by a run** | F: 1289/1289 existing tests green with the prototype at depth 3. |
| 14 | T2 moves exactly `Play_StartsAStreamAndQueuesAhead` (16) and `Update_TopsTheQueueBackUp…` (14/14); with `SampleCount` 106 496 they read 24/22/24; tests written against the constant "expected to pass, not run" | **CONFIRMED by a run** (the H is now F) | F: `results/t2.trx` 1287/1289. The 2 failures read "Expected 3, Actual 16" and "Expected 1, Actual 14". Full suite at depth 24: 4187/4194. The other 5 failures are `AssimpToGltfConverterTests`, a `PathTooLongException` loading `assimp.dll` from my deep scratch path (environment). So `MusicPlayerWorkerTests:191`, `:273`, `SoftwareAudioBackendStreamingCountTests:118` and the `MusicRig` tests (`new MusicPlayer(Service, false)`, `AudioServiceGameParameterTests.cs:136`) pass at 24. 24/22/24 with 106 496 frames: see "Combined run". |
| 15 | DLL facts (no engine `MusicPlayer` use; DLL fake reports 0 pending `:189`, `OpenStream => null` `:335`) | CONFIRMED | F: read; the DLL plays BGM through `PlayClip` (`AlundraMusicPlayer.cs:385`). |
| 16 | Alundra values: shortest region 248 430 frames (5.6 s); largest loop start 10 695 720 (track 14); 5 344 920 = track 25 | CONFIRMED | F: `seams.out` from `docs/plan-e19-loop-annexe/data/loops_final.tsv`: 38 looping tracks, all with a 44-byte header and a loop end equal to the file end (so no loop end is needed). The largest loop start fits a float exactly, which agrees with the report. |

## Mutations (run in the copy; existing tests + the 26 new cases)

| Mutation | Report says red | Red in my run |
|---|---|---|
| Seek ignores the data offset (`44 + frame × 4`) | W1, W7, L6 | W1, W7, L6 (both modes), and also L1 (both modes), L2 (both), L3, L5 (both), L8. **No existing test turns red** (1289/1289, see M5). |
| Range check off by one (`>` in the reader, `<=` in `Play`) | W3, L5 | W3 (`W2_W3_W4`), L5 (1000) |
| Rewind in `FillQueue` | L1, L2, L6, B2 | L1 (all 4 rows, including the worker twins: their seam is in `Play`'s inline fill), L2 inline, L6, L8, B2 |
| Rewind in the worker | L1w, L2w | L1w (both headers), L2w |
| Key not written | S4 | S4 |
| Key written at 0 | S5 | S5, and also 4 rows of `Serialize_OfADemoSoundFile_KeepsExactlyItsOriginalKeys` and 6 `SoundAssetInspectorPanelTests`: today's tests already guard this |
| Float read | S6 | S6, S7 (600.5 is accepted silently) |
| No preview copy | P1 | P1 |
| No per-call cap (not in the report's list) | — | B2 only (fails at the 10 s watchdog; the run still ends, 1314/1315) |

## Missed or worth correcting (with plan impact)

- **M1. `Loader_DemoSoundFiles_HaveTheVariationDefaults` is not a directory scan.** It takes a hard-coded list of
  3 files (`SoundAssetTests.cs:72-77`). A new `loop_point_demo.sound` enters only
  `Serialize_OfADemoSoundFile_KeepsExactlyItsOriginalKeys`, which does scan (`SoundAssetEditorSerializationTests.cs:89-95`).
  Impact: fix the wording "adds rows to two existing theories" to one theory. Covering the demo file by the loader
  theory needs an explicit list edit; `SoundAssetTests.cs` is already in the file list. P3.
- **M2. Worst seam buffer.** Track 6's steady seam (134 frames) is not the smallest: track 13's first seam (first pass from
  frame 0, 9 560 145 mod 4096) is **81 frames** (`seams.out`). The margins move by 1.2 ms (depth 24: 2045.2 ms; depth 6:
  373.4 ms, +21.9 ms over 351.5). No decision changes. Fix the "track 6" label to "worst track". P4.
- **M3. Publication order in `Channel.Reset`.** The worker reads `State` with `Volatile.Read` (`StreamingWorker.cs:226`)
  and only then the other fields. R6 should require writing `_loopStartSample` **before**
  `Volatile.Write(ref State, ActiveState)` (`:213`), like `_reader` and `_isLooped` today. No test can pin this
  deterministically. P3.
- **M4. "One throttled warning" is shared.** Every warning of a player goes through the single `AudioLogThrottle _log`
  (`MusicPlayer.cs:34`, 5 s window, `AudioLogThrottle.cs:29-58`). A bad loop start played within 5 s of any other
  music warning is only counted as "suppressed". The tests hold (fresh player each); the production rule is "at most one
  per 5 s per player". Optional: a dedicated throttle. P4.
- **M5. `Rewind`'s data offset is pinned by no existing test.** Under the "ignore the data offset" mutation, all 1289
  existing tests stay green: `Rewind_ReplaysExactlyTheSameBytes` uses a 44-byte header (`WavStreamReaderTests.cs:113-130`).
  Since `Rewind` becomes `SeekToSample(0)`, the planned W4 "`SeekToSample(0)` equals `Rewind`" is circular after the
  refactor. W4 should instead assert the first frame read after `SeekToSample(0)` on a non-44 header with a non-zero
  first value (for example the R channel 10000). My own frame-0 assertion passed under the mutation by coincidence:
  the 2 bytes before offset 46 are the high word of the data size, 0, which equals L(0). P3.
- **M6. `GetPosition` doc.** "Loops back to zero on a rewind" (`MusicPlayer.cs:244`) becomes "back to the loop
  start". It is in a listed file. P4.
- **M7. "No cap" guard.** The report counts on B2 to pin the cap but lists it only under "rewind in `FillQueue`". Add
  "remove the per-call cap → B2 red (watchdog)" to the mutation list. Run: B2 alone turns red. P4.
- **M8. `FileStream.Seek` allocation**, left unconfirmed on 2026-10-05: see "Combined run" (W6f on a real
  `FileStream`).

## Combined run (T1 + T2 in the copy)

F: T1 prototype + T2 (`DefaultQueuedBufferTarget` 24) + the report's `MusicPlayerTests` edits (`SampleCount` 106 496,
asserts 24, 22, 24), in the copy:
- Audio + inspector subset: **1317/1317**. That includes `Play_StartsAStreamAndQueuesAhead` (24) and
  `Update_TopsTheQueueBackUp…` (22, then 24), all 26 T1 cases unchanged at depth 24 (they build their players with
  an explicit target of 3, as planned), and two extra checks:
  - **W6f**: 500 `SeekToSample` + `Read` calls on a real `FileStream` allocate **0 B**. This closes the 2026-10-05
    open point.
  - L1 at the default depth: `service.Music.Play` gives 24 buffers, 4000 B then 23 × 1600 B (`sim.out` B).
- Full `CasaEngine.Tests`: 4217/4222. The only failures are the 5 environmental `AssimpToGltfConverterTests`
  (`PathTooLongException` on my scratch path, same as the T2-alone run).

## Still unconfirmed

- The demo itself (not built or started here), the WAV generator, the back-buffer capture, listening.
- The MonoGame (OpenAL) backend at depth 24 in a live run. H: no queue cap (the 2026-10-05 decompile read a plain
  `Queue`, `docs/plan-e19-loop-annexe/engine/verify/verify.md` row 21; not re-read here).
- Whether depth 24 holds on the unmeasured routes (cold disk cache, tracks 21/30, 476 → 478). O-E19-63 measured only
  10 transitions.
- `Alundra.Tests` count (not built or run). F: L-a touches no DLL symbol, and the DLL fake's `OpenStream => null` means a
  streamed `Play` returns `None` in DLL tests until L-b changes the fake.
- Release builds (every run here was Debug).
