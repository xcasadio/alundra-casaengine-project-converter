# Counter-check of the night backlog discovery (read-only), 2026-10-06 evening

Nothing built, run, exported, edited, staged or committed in a repository. Probes only in this folder (`vdis.py`, `pal.py`, `idx4.py`,
`scan0.py`, `seam.py`). Labels: F = read or measured, H = hypothesis. Repo state read: parent `bbea80a` (`chantier/e19-suite2`), engine
`dd1f8afa`, analyser `a958d86`.

## A. Confirmed (own reads, own probes)

| # | Claim | Evidence |
|---|---|---|
| A1 | Pins: parent pins engine `dd1f8afa` and analyser `a958d86`; engine `main` = `19aa21fe`, 2 commits behind HEAD; analyser differs from the oracle base `db69b82` by `AlundraTools.csproj` (+9) and `FontGlyphTable.csv` only | `git ls-tree HEAD`, `git rev-list --count main..HEAD` = 2, `git diff --stat db69b82 HEAD` |
| A2 | No streaming loop point anywhere | `git grep -i "LoopStartSample\|loop_start_sample"` over EVERY engine branch: 0 hit. `WavStreamReader` has only `Rewind` (`:91-97`); `MusicPlayer.FillQueue` `Rewind()` at `:373`; `StreamingWorker.Channel.Service` `Rewind()` at `:263`; `Channel.Reset(reader, isLooped, name)` `:205`, `Acquire` `:60/85` |
| A3 | S2 delivered only worker reads, the per-call bound (`MusicPlayer.cs:351-358`) and resident loop regions | `WithLoopRegion` `AudioVoiceParameters.cs:83`, `MixerVoice.LoopStart/LoopEnd :27-30`, `SoftwareMixer.cs:306,2058`; audio plan P11 "Pas de champ serialise en S2 (SoundAsset inchange)", written as an AGENT arbitration "a confirmer par l'auteur" (`audio-modern-tasks.md:645-654`) |
| A4 | DLL plays music as a resident looped clip | `AlundraMusicPlayer.cs:377-386` (`isLooped: true`, `PlayClip`) |
| A5 | Resident clips are pinned by the provider; the real provider implements `OpenStream`; the DLL fake returns null | `AssetContentManagerAudioClipProvider.cs:7-12,22-44,58+`; `Alundra.Tests/FakeAudioBackend.cs:189` (`GetPendingBufferCount => 0`) and `:335` (`OpenStream => null`) |
| A6 | `SeekToSample(0)` cannot throw, so `Rewind()` = `SeekToSample(0)` is behaviour-neutral | header check `dataLength < blockAlign` throws at construction (`WavStreamReader.cs:182-185`): every reader has at least one block |
| A7 | Default backend Software, worker mode for Software and MonoGame | `AudioBackendSelection.cs:30`, `AudioService.cs:98` |
| A8 | Oracle is coherent | `scratchpad/loop-data/oracle/sound/bgm.json`: 45 entries, 38 `Looping` true, false = 26, 36, 37, 38, 40, 42, 43; every WAV size = 44 + Frames x 735 x 4; total 1 343 140 980 B; `LoopStartSample == LoopStartFrame x 735` on all 38 |
| A9 | Current data: 45 BGM WAV, 699 960 120 B; `data-extracted` == `Alundra Remake/remaster-data-extracted` today | `du -sb`; `diff -rq` empty; file list + sizes identical (4536 files); SHA-256 of `sound/bgm.json` and `ui/font3.png` equal; D: 461 GB free; data folders git-ignored (`.gitignore:60,64,66`) |
| A10 | Engine ADR numbers | engine `docs/decisions`: `e19-suite2` highest 0069; `0070-view-layout-area-and-window-ui.md` only on `chantier/demo-browser` (also the only unmerged-to-main branch besides the stack; it edits `AudioDemo.cs`); parent highest 0046 (`chantier/*` scan), next 0047 |
| A11 | O-E19-64 | 18 `<TextBlock font3>` without `Padding`/`LinePadding`/`WrapText`/`VerticalContentAlignment`: Inventory 50-53, SubInventory 49-52, Save 36-43 and 47-48 (= 10); pattern `TextBoxScreen.xaml:36-38` and `ChoiceScreen.xaml:34,36`; Hud/Dialogue have no font3 TextBlock; `view-verify.md` row 9 and `view-notes.md:27` say +1/+1 px measured on a real GPU; `.uiscreen` files are envelopes (`source_xaml_file`), the `.xaml` is the source. The two `*ScreenFontTests` do not read layout |
| A12 | O-E19-66 binary facts | `0x80044B48` loads `taki\screen\wind.cl` (string `0x80023EFC`), size 0x200, fills `0x80146E28[i] = GetClut(0x120, 0x1E0+i)`, uploads a 16x16 rect; `FONT3.tim` is read at `0x80044FEC` into `0x8014ACE8` and the glyph blit at `0x80047968` reads its raw 4 bpp image at +0x40 (its own CLUT is skipped), so the on-screen colours come from the CLUT table; `0x80050424` reads entry 8 (`0x6E38`) for the text-band primitives |
| A13 | O-E19-66 data facts | `pal.py`: FONT3.TIM clut0 vs WIND.CL palettes: palette 8 differs ONLY at index 0 and index 4, the other 15 palettes differ at all 16 indices; index 4 = 44 texels in cells 4, 14, 15, 21 (8), 22-25 (2 each), 26-29 (5 each), 64 (5) (`idx4.py`); index 4 is (82,90,57) in `data-extracted/ui/font3.png`, the remaster copy and `alundra-project/UI/Textures/font3.png`; (74,82,57) would be palette 8. `Program.cs:1130` writes `font3.FontBitmapTim`; `Font3.GenerateFontBitmapTim(Color[])` ignores its palette (`Font3.cs:78-92`); no test pins the colour (rg) and the GPU pixel tests read the exported PNG itself |
| A14 | O-E19-9 | `scan0.py` over 9624 `.anim2d` (5211 Loop, 4413 Once): exactly 5 files with every key at t = 0, all Loop, the 5 named; `TryEvaluateBool` keeps the LAST key with time <= t (`Animation2dCompositionSampler.cs:323-340`) so `true@0, false@0` is invisible; the sampler handles a duration-0 Loop explicitly (`UpdateLooping :91-97` calls `ApplyTracks(0)`), so the converter fix alone is enough (the H in the report is settled) |
| A15 | O-E19-65 / O-E19-70 not in the engine gap report | `ai-agent/audits/mgui-gaps-from-xaml-screens.md` ends at G11; `CasaEngineGame.cs:537` `view.UIView?.Update` precedes `GameManager.UpdateWorld` (`:555`); `UIRoot.cs:131` `Desktop.Update()` |
| A16 | Stale headers and rows | plan lines 2526, 4274, 4591, 4849, 5315, 5660, 5925, 6293, 7456, 8253, 8614, 9667, 10512 say what the report says; K2-3 verified at 6382-6389 (CONFIRMED 2026-10-03), bullet 6371 still "pending"; `chantier/e19k2-layer-mask` is an ancestor of engine `main`; O-E19-6, -7, -15, -20, -26, -48, -50 rows carry no resolution mark while their slices are done (h1b1/h1b2, h4, l1, m2) |
| A17 | D5b not started, blocked by a design decision | no `LastTickDeltaZ`/`UH-16` in `Alundra*`; plan 3950-3962 |
| A18 | E19.h2 is a sketch blocked by a missing scene-loop emulation | plan 6794-6797, 7264-7271; pin `AlundraAnimationImpulseTests.cs` (UJ-1b) |
| A19 | Baselines traceable | 4126 engine tests (plan 8542), 3034 `Alundra.Tests` (plan 9048, 3032 + 2 reds fixed by `b98b609`); the converter 666 not traced |
| A20 | Sizes of the DLL tests | `AlundraMusicPlayerTests` 22 `PlayCalls` hits, `AlundraWarpDepartureTests` 27, `AlundraBgmFadeDirectorTests` 40 hits (report: 39 + the flip): same thing |

## B. Refuted or overstated

**B1. "No product decision is open; treat the L-a re-ownership as a session technical choice (precedent D-E19-86..88)."** Overstated.
- D-E19-74 is recorded "(2026-10-05, l'auteur, O-E19-62)" and says L-a goes "dans la tranche S2 ..., pas dans la pile E19".
- The alternative "do L-a now on the E19 stack" WAS on the table and the discovery recommended it (`loop-engine/notes.md` section 6, question 1: "Recommendation: do it now"); the author chose S2 anyway. This is an explicit author choice, not an incidental placement.
- The precedents do not match: D-E19-86 is marked "confirme par l'auteur le 2026-10-06"; D-E19-87 and -88 are session choices that contradict no author statement.
- The premise is nevertheless void (S2 merged without it, P11 was an agent arbitration, audio plan D31 keeps the music in WAV so nobody else will own it), and L-a is reversible (separate engine branch, nothing merged or pushed). Proceeding is defensible.
- Correct classification for the plan and the morning report: "deviation from D-E19-74, premise void, author to confirm", not "session technical choice". Under the global rules (no approval for a plan > 1 commit means no code) this also needs the slice plan + fresh plan-verifier first (serialized field = risk trigger).

**B2. "The margin is 3 x 16 KiB, i.e. 186-279 ms" and the harness criterion "max stall + 1 frame < 2136 ms for depth 24".** Omits the seam worst case that D-E19-75 itself states (96 ms) and that `loop-engine-verify` correction 2 carried.
- Every loop seam submits one short end-of-data buffer, which occupies one of the 3 slots. `seam.py` on the oracle: the smallest loop-pass seam buffer is track 6, 536 B = 3.04 ms, so worst margin = 92.88 + 3.04 = 95.9 ms; next 8 (4.8 ms), 35 (10.4 ms).
- At depth 24 the seam case is 22 full buffers + the short one = about 2046 ms, not 2136 ms. Either set the criterion at about 2046 ms or make `FillQueue`/`Channel.Service` fill across the seam (full buffers always). The report's L-a does not do the latter.

**B3. "Remaining hygiene: E19.m items (G1 tests, G3, G4, G5, G7), listed in backlog-2026-10-03.md".** The file is stale and the list inherits it.
- E19.m5 (CONFIRMED 2026-10-05, plan 10125-10230) did M-14, M-22, M-24, M-28, M-29, M-33 (half), M-35, M-38, M-46 and closed M-23 and M-40 without change; E19.m0 fixed M-04 (the "possible hotfix"), m1 M-10 and M-47, m6 M-19, M-31 and half of M-32. Plan 10176: only M-16, M-17, SJ-12 (D5b) and M-45's test half (after G4) remain from G1.
- Real remainder: G3 proper (M-01, M-02, M-09, M-13, M-26), G4 (M-11, M-15, M-45, M-33 latch), G5 (M-34, M-37), G7, and G1-late (M-20, M-25, M-32 arc, M-42, M-44), which the report omits. The backlog file still says "Open" for M-04 and M-23: reconcile it in the docs commit.

**B4 (minor). "Next free engine ADR is 0071".** R-BR (2) counts `main`, `chantier/audio-modern`, `chantier/e19*` and G2d only (plan 346-350), which gives 0070 and would collide with the author's `demo-browser` ADR-0070. 0071 is a prudent deviation: write it down. Same for parent 0047: the G3c/G3d siblings may take it first, so re-scan before each commit.

## C. Missed, with plan impact

1. **L-b docs list omits `docs/formats/audio-manifests.md`**, which documents the `bgm-manifest.json` schema (`loop_detected`, `frames`, ...). Rule 8 (format change) requires adding `looping`, `loop_start_frame`, `loop_start_sample` and the new meaning of `frames` (file now ends at J2). Add it to the L-b closed list (loop-data/notes.md 4.x does not have it either).
2. **L-a test list misses the second silent-drop site.** `CreatePreviewCopy` (`SoundAssetInspectorPanel.cs:318`, private, copies 7 fields) would silently drop `LoopStartSample` in the editor preview. `CasaEngine.Editor` already has `InternalsVisibleTo("CasaEngine.Tests")` and `CasaEngine.Tests/Editor/SoundAssetInspectorPanelTests.cs` exists: make it `internal` and add a test (carry `loop-engine-verify` correction 1). The report names the file but no test.
3. **Gates not in the tonight order.** L-a (serialized field, public engine API) and the data cycle (data migration, 3 x 1.34 GB) are independent-review triggers: slice plan, fresh plan-verifier READY, then execute, then fresh verifier. "fais tout" is not approval of an unseen plan (global rule); the precedent is the E19 AUTO mandate of 2026-10-02 which covered slices already in the E19 plan. §1.2w is in that plan, but L-a's placement is the exception (B1).
4. **Decouple the cheap data fixes from L-b.** O-E19-9 needs only an export (converter-only), O-E19-66 needs an extraction because the fix is placed in the analyser, but the wrong colour is unique (82,90,57) to index 4, so a converter-side remap is possible (loses the "extract with palette 8" faithfulness). If L-b pauses (B1 or O-E19-63), run O-E19-66 + O-E19-9 as their own cycle instead of waiting; the predicted file sets are disjoint (46 BGM + 1 font3 + 5 anim2d), so a bundled manifest proof can still attribute each diff.
5. **Practical traps for tonight**: the engine working tree carries the author's `CasaEngine.Launcher/Program.cs` change (stage by file, never `git add -A`; `git diff` there prints "ignoring broken ref" noise); the analyser has an untracked `AlundraTools/log.txt`; `CasaEngine.Tests` is not in the `.sln` (build it explicitly before `--no-build`); `Alundra Remake/` is not a git repo, so the old BGM data have no rollback except re-running the `db69b82` extractor (deterministic, 4536 files) or a scratchpad copy of `data-extracted/sound/bgm` (700 MB) taken before the mirror.
6. **Evidence limit of O-E19-66's fix**: only the dialogue text bands and the cursor are proven to use entry 8. Other consumers read other table entries (`0x8004F83C` reads entry 0). The export has a single palette for all font3 text and `font3.json` declares `Palette 8` for every tile, so palette 8 is the right single choice, but "exactly what the game draws everywhere" is not proven.

## D. Unconfirmed

- H "the S2 worker does not help a game-thread stall": consistent with the code (`FillQueueFromRing` submits at most `_queuedBufferTarget` per call and only while pending < target, `MusicPlayer.cs:296-330`; ring is 2 x target slots), not measured.
- "The 478 load took more than 1 s in Debug": plan row text only.
- Baselines: not re-run (not allowed); 666 converter tests not traced.
- Predicted exports (`report.json` unchanged except the file entries): not testable read-only.
- Whether the harness can run an unattended window/GPU/OpenAL session tonight: not checked.
