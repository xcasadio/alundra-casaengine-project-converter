# Counter-check of the L-b + O-E19-66 discovery report (2026-10-08)

Verdict: **the data facts and the prototype are CONFIRMED**, including byte-for-byte reproduction from my own build. **The slice plan needs revision
before approval**: one file in L-b1 breaks a standing author rule, L-b2 misses three GPU pixel tests and five versioned reference images, and the
L-b2 size target is stale. Evidence is in `notes.md` and the `*.out` files of this folder. The repository was read-only throughout and its
status is unchanged. All runs happened in this folder.

## 1. Claims confirmed (re-derived independently)

| # | Claim | How I re-derived it | Result |
|---|---|---|---|
| 1 | Analyser `a958d86` changes only csproj + `FontGlyphTable.csv`, and the extractor does not reference that project | `git diff --stat`, extractor csproj | CONFIRMED |
| 2 | Anchors in Program.cs, Font3.cs, ImageHelper.cs, TimLoader.cs, EntityNames.cs, the fake backend and the DLL are at the stated lines | read at HEAD | CONFIRMED |
| 3 | `data-extracted` = remaster copy; today's WAVs in all 3 copies = `TodaySHA256`; oracle WAVs = table; font3.png identical in 3 places | `diff -rq`, `vcopies.py`, `vdata.py` | CONFIRMED |
| 4 | Prototype: `rendered=45 silent=1 failed=0`, `sequencer=18`; 45/45 WAVs + `bgm.json` = oracle; 47-line diff (46 predicted + font3.png); deterministic | My own `git archive` + `proto_extractor.patch` + Release build + run. My output = their `out-lb` (`diff -rq` empty), 4536 files, 4 248 045 580 B | CONFIRMED |
| 5 | font3: 44 texels of index 4, (82,90,57) to (74,82,57), transparent texels unchanged, SHA-256 `381c63ef…6fb5`, 9951 B | Own Python TIM/CLUT decoder: today's PNG = CLUT-0 decode, and both prototype PNGs = entry-8 decode (0 differences) | CONFIRMED |
| 6 | The binary fills the CLUT table from `wind.cl` at `0x80044B7C-0x80044B8C`; entry 8 is read for the bands | capstone on ALUN_CD.EXE (France): load at `0x80044B48`, `GetClut(0x120, 0x1E0+i)`, upload at `0x80044BC4`; the only direct `+0x10` reader is `0x80050424` | CONFIRMED (the claim that bands and cursor use entry 8 rests on the f2b1 trace) |
| 7 | `Font3.Palettes` must not be reused | `ImageHelper.cs:12-21` uses plain `<<3`, while index 4 needs replication | CONFIRMED |
| 8 | Render values: one-shot Frames 1220/601/620/531/543/772/900; `LoopStartSample = J1×735`; tail ≤ 1.9 s; L-C, L-E and the crossfade seam hold | `vdata.py` on my output: L-C 45 green, L-D 38/38 green, L-E 45 green; longest tail 113 frames = 1.883 s (track 26) | CONFIRMED |
| 9 | Engine `f86a981b` has no loop point; rewinds only at `MusicPlayer.cs:373` and `StreamingWorker.cs:263`; the worker runs only for the real backends | grep, `AudioService.cs:97` | CONFIRMED |
| 10 | DLL tests: 40 `[Fact]`; 89 `PlayCalls` lines (one is a comment); 11 / 38 / 37 / 4; the flip at `:448` | grep | CONFIRMED |
| 11 | Converter tests: 91 move (T-D 45, T-E 1, golden 45) | read `BgmFreshSoundSystemTests.cs` | CONFIRMED |
| 12 | B4 risk: during a one-shot's release tail the original restarts on `0xA5`, but the DLL ignores it | `0xA5` → `StopAllSound` → unconditional `PlaySeq`, which starts from the top on a stopped sequence (`plan-bgm-demarrage-binaire.md:73-86`); only 36 and 42 are reachable (no map entry; `a7scan.json` read, not re-run) | CONFIRMED (logic) |
| 13 | Disk use about +9.1 GB on D: | 4.25 + 3.6 + 0.64 + 0.64 GB; D: has 459 GB free | CONFIRMED |

## 2. Corrections and misses (with plan impact)

- **M1 (P1, rule).** L-b1 lists `AlundraTools/AlundraEngine/Text/Font3.cs` (a new decode method).
  - The author's standing rule (2026-10-03, "ne pas corriger la décompilation") forbids modifying `alundra-datas-analyser/AlundraTools/AlundraEngine`.
  - Fix: write the decode in `AlundraDataExtractor/Program.cs` only, calling the public `TimLoader.LoadTimRaw` / `TimLoader.DecodeBuffer`. This needs no AlundraEngine edit, and the pixels and SHA-256 stay the same.
- **M2 (P2, missed tests).** Five versioned reference images contain font3 index-4 texels (82,90,57):
  - `docs/plan-e19-f2b1-annexe/pixels/centred.png` (8 texels) and `centred_x2.png` (32);
  - `docs/plan-e19-f3-annexe/view/pixels/overlap_mrow_V1_t19_x1.png` (90), its `_x2.png` (360) and `overlap_mrow_V2_t22_x1.png` (90).

  Three `[AlundraGpuFact]` tests compare these images texel for texel with a GPU render of the exported `UI/Textures/font3.png`. They will go red after the L-b2 export:
  - `AlundraTextBoxPixelTests.ACentredLine_IsTheVersionedImage_AtX1AndX2`;
  - `AlundraChoicePixelTests.TheChoice_OverAFullFirstRow_…`;
  - `AlundraChoicePixelTests.TheInverseOrderOfTheWindows_…` (its 338-345 count window).

  The 10-06 backlog note "tests that move: none" missed them too. Fix: L-b2 updates the 5 images and runs the GPU tests red, then green. Two ways to update them:
  - re-run their independent compositors against the new page;
  - substitute (82,90,57) with (74,82,57) in the images. This is exact, because 82 is never a `<<3` value.
- **M3 (P2, stale acceptance value).** The current export reports `OutputSizeBytes` 1 311 900 066 and `OutputFileCount` 24 106, while the 10-05 base was 1 287 720 524 and 23 744.
  - The L-b2 target 1 930 905 296 is therefore wrong. Expect about **1 955 084 838** (± the `report.json` delta), with the file count unchanged at 24 106.
  - Re-baseline from the "before" export at the L-b2 base.
- **M4 (P3).** The report says L-D is "33 red today". On today's `data-extracted` it is red on **38/38**; the 33 is the 10-05 count for a raw J2 render without the crossfade.
- **M5 (P3).** The "per-call buffer bound" that L-b3 expects from L-a already exists in the inline path (`MusicPlayer.cs:354-358`). What L-a still has to supply:
  - `LoopStartSample`;
  - the seek;
  - looping to that point in both the inline path and the worker;
  - queue depth 24.
- **M6 (P3, transient).** Between L-b2 and L-b3 the DLL still loads music as whole clips, and caches them for the whole session (`AssetContentManagerAudioClipProvider.cs:22-45`).
  - Memory and load time per played track roughly double (`bgm_014` is 80 MB).
  - Fix: no in-game recipe until L-b3 lands, or ship the L-b2 export together with L-b3.
- **M7 (P4).** In the prototype, a one-shot tail that never reaches 60 silent frames before the cap is exported, not failed. So "hitting the cap is a failure" is not fully enforced. It has no effect on the outputs today (tails ≤ 113 frames); the real code can add the check without changing any byte.
- **M8 (P4).** The O-E19-66 row says "Convertisseur / correction côté export", but the proposal fixes it in the extractor. Record that choice in the plan (it is consistent with D-E19-102).

## 3. Not confirmed (remains H or unrun)

- The L-b2 manifest bytes (20 352 B, SHA-1 `8f302c1a…`) come from the 10-05 model. The converter was not run. Its base manifest (SHA-1 `509e8c68…`) and the converter sources (unchanged since `1c1e0d4`) still match that model's inputs.
- Whether font3 is drawn with table entries other than 8 elsewhere: 19 computed-index readers were not followed.
- `a7scan.json` reachability was read, not recomputed. The release tail as the binary's own release is analyser-only evidence.
- I did not observe the abort without `EntityNames.csv`; I only read the code that implies it.
- No tests were run (read-only repository).
