# Counter-check of the L-a discovery (engine f86a981b), working notes, 2026-10-08

Nothing was built, run, edited, staged or committed in the repository or its submodules. Reads only (`git show`,
`git grep`, `git ls-tree`, `git archive`). Everything that ran, ran in a PHYSICAL copy:
`build/engine` = `git archive f86a981b` of the engine + `git archive` of MGUI `18b2c14a` and NvgSharp `9c0da031`
(the gitlinks of f86a981b). Builds used `-nodeReuse:false -p:UseSharedCompilation=false`, so no MSBuild node or
compiler server outlives a build; `dotnet test` hosts exit at the end of each run. No game, demo or DLL was started.

Labels: F = read in code, measured or computed; H = hypothesis.

## Files
- `seams.py` / `seams.out`: own recomputation of loop regions and seam buffers from the versioned L-b table
  `docs/plan-e19-loop-annexe/data/loops_final.tsv` (read only).
- `margins.py` / `margins.out`: own margin arithmetic.
- `proto/apply_t1.py`: a minimal prototype of rules R1-R6 applied to the copy, with one optional mutation.
- `proto/VerifyLoopStartTests.cs`: the discovery's written-in-advance values as real xUnit tests (L1, L1w, L2,
  L2w, L3, L4, L5, L6, L7, L8, B2, W1-W7, S1-S7, P1, a T2 check). `proto/VerifyExtraTests.cs`: FileStream seek
  allocation and L1 at the default depth.
- `proto/run_mutations.sh`, `proto/summarize.py`; `results/*.trx` and logs.
- `pristine/`: the seven runtime/editor files of f86a981b, restored before each mutation.

## Runs (F, all in the copy, filter `CasaEngine.Tests.Audio | SoundAssetInspectorPanel` unless stated)
- Baseline f86a981b: 1289/1289 pass.
- T2 alone (`DefaultQueuedBufferTarget` 24): 1287/1289; the two failures are exactly
  `Play_StartsAStreamAndQueuesAhead` (expected 3, actual 16) and `Update_TopsTheQueueBackUpAsBuffersAreConsumed`
  (expected 1, actual 14). Full suite at 24: 4187/4194, the 5 other failures are `AssimpToGltfConverterTests`,
  `PathTooLongException` loading `assimp.dll` from my deep scratch path (environment, unrelated).
- T1 prototype + 26 new test cases, depth 3: 1315/1315 pass (every written value holds; no existing test moves).
- Mutations: see verify.md table.
- Mutations (`results/mut_*.trx`), red tests among existing + new:
  - no_data_offset: W1, W7, L6, plus L1, L2, L3, L5, L8 (both modes where applicable); 0 existing tests red.
  - range_off_by_one: W3, L5(1000).
  - float_read: S6, S7.
  - fillqueue_rewind: L1 (4 rows incl. worker twins), L2 inline, L6, L8, B2.
  - worker_rewind: L1w (2 rows), L2w.
  - key_not_written: S4. key_written_at_zero: S5 + 4 demo-theory rows + 6 inspector tests (already guarded today).
  - no_preview_copy: P1. no_cap: B2 (10 s watchdog; run ends).
- Combined T1 + T2 + MusicPlayerTests edits: subset 1317/1317; full suite 4217/4222 (5 assimp path-length failures).
- Processes: the MSBuild nodes (`/nodeReuse:true`, created 11:03:59-11:04:05) and VBCSCompiler (10:50) that run on
  the machine predate my first build (copy's `CasaEngine/obj` created 11:05:39); not mine, not touched.

## Own derivations
- Seams (`seams.out`, F): 38 looping tracks, header 44 B, loop end = file end on every one; shortest region 248 430
  frames (tracks 45/46); largest loop start 10 695 720 (track 14); smallest seam buffer 81 frames (track 13, first
  pass, 9 560 145 mod 4096), then 134 (track 6, steady); no zero-length seam.
- Margins (`margins.out`, F): depth 24 at the 81-frame seam 2045.2 ms; depth 6 373.4 ms (+21.9 over 351.5 ms).
- 351.5 ms = Debug worst 2-frame fill gap, `docs/plan-e19-backlog-1006-annexe/o63/combined.md:14` (F).
- Repository state at the end: engine HEAD f86a981b, only the author's Program.cs modified; the parent shows untracked content inside the alundra-datas-analyser submodule (not written by me; I never wrote there).
