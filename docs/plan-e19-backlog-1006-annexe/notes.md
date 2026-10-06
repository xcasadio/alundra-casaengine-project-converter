# Night backlog discovery (read-only), 2026-10-06 evening

Repo `D:\development\repo\alundra-casaengine-project-converter`, parent HEAD `bbea80a` (branch `chantier/e19-suite2`), engine HEAD
`dd1f8afa` (= `chantier/e19-suite2`, 2 commits ahead of engine `main` `19aa21fe`), analyser `a958d86` (only `FontGlyphTable.csv`
and its csproj line since `db69b82`, the base of the BGM oracle). Sibling discoveries running at the same time (scratchpad dirs
`night-g3c-disc`, `night-g3d-disc`, `night-pad-disc`): G3c, G3d and the pad-word age (O-E19-75). Not duplicated here.
Nothing built, run, edited, staged or committed in a repository.

## 1. Section 1.2w, loop at the sequence marker (D-E19-70, 72..74, O-E19-62)

### Facts (engine HEAD `dd1f8afa`)
- F1.1 Loop regions exist ONLY for RESIDENT and software-stereo voices: `AudioVoiceParameters.WithLoopRegion(startFrame, endFrame)`
  (`CasaEngine/Framework/Audio/AudioVoiceParameters.cs:83`), `MixerVoice.LoopStart/LoopEnd` (`Software/MixerVoice.cs:27-30`),
  `SoftwareMixer.ValidateLoopRegion` (`SoftwareMixer.cs:306`), `ApplyLoopRegion` (`:2058`). ADR-0056 (P11) and audio plan T2.4
  (`ai-agent/tasks/audio-modern-tasks.md:802`, P11 at `:651`: "Pas de champ serialise en S2 (`SoundAsset` inchange)"). The
  MonoGame backend ignores the region (ADR-0056, `docs/engine/audio-system.md:454-460`).
- F1.2 Streamed music has NO loop point: `MusicPlayer.FillQueue` does `track.Reader.Rewind()` at end of data
  (`Streaming/MusicPlayer.cs:366-380`); the S2 worker does the same (`Streaming/StreamingWorker.cs:257-265`, `Channel.Reset(reader,
  isLooped, name)` has no start argument `:205`); `WavStreamReader` has only `Rewind()` (`:91-97`), no seek to a sample;
  `SoundAsset` has no `LoopStartSample` (properties `SoundAsset.cs:35-130`, `IsLooped` `:62`); `docs/engine/audio-system.md:415`
  still says "La boucle rembobine le lecteur".
- F1.3 So S2 of `chantier/audio-modern` (merged: `git merge-base --is-ancestor chantier/audio-modern main` true, tip `f35d94f2`,
  ADR-0055..0064 + 0067 on it) delivered ONLY: worker-thread reads (P13), per-call bound `FillQueue` <= `QueuedBufferTarget`
  (`MusicPlayer.cs:353-357`), loop region on resident voices. D-E19-74's engine half (SoundAsset.LoopStartSample, seek in the
  reader, MusicPlayer returning to the point) is NOT done anywhere (rg `LoopStartSample` in the engine: 0 hit).
- F1.4 The DLL still plays music as a RESIDENT clip, looped whole: `AlundraMusicPlayer.StartVoice`
  (`Alundra/Scripts/AlundraMusicPlayer.cs:377-386`: `new AudioVoiceParameters(MaxVolume, 0f, 0f, isLooped: true)` then
  `_audioService.PlayClip(clip, AudioBusNames.Music, parameters, owner: this)`). Resident clips are pinned for the life of the
  provider (`AssetContentManagerAudioClipProvider.cs:7-12`, `GetClip` `:23-44`), so a resident route with `WithLoopRegion` would
  pin up to 1.34 GB (46 files after option B); streaming is the right route (same reason as D-E19-70/73).
- F1.5 Default backend is Software (`AudioBackendSelection.DefaultKind`, `AudioBackendSelection.cs:30`); `AudioService` builds
  `Music = new MusicPlayer(this, _backend is SoftwareAudioBackend or MonoGameAudioBackend)` = worker mode (`AudioService.cs:98`).
- F1.6 Data side (converter, parent): `BgmEntry` (`SoundManifestReader.cs:12-24`) and `ReadBgm` (`:85-107`) have the 10 old fields +
  AssetId; `AudioWriter.ConvertBgm` (`Writers/AudioWriter.cs:66-99`) writes the manifest by serialising the entries (no code
  change needed for new nullable properties, per the L-b audit).
- F1.7 Extractor (analyser, `a958d86`): `ExtractDataFromBgm` `AlundraTools/AlundraDataExtractor/Program.cs:177`,
  `RenderBgmTrackUntilLoop` `:256`. Unchanged since the oracle base `db69b82` (diff = csproj + FontGlyphTable.csv).
- F1.8 Existing oracle (scratchpad `loop-data/oracle/`, 45 WAV + bgm.json, 1.3 GB, SHA-256 in `oracle_sha256.tsv`): complete and
  independently cross-checked (see `loop-data/notes.md` summary). Harness `loop-data/harness`, extractor snapshot
  `loop-data/extractor-bin`. Predicted diffs: re-extraction 46 M (45 WAV + sound/bgm.json), export 47 M (45 WAV +
  `Musics/bgm-manifest.json` + `report.json`), WAV 699 960 120 B -> 1 343 140 980 B per copy (3 copies: `data-extracted/`,
  `Alundra Remake/remaster-data-extracted`, `alundra-project/Musics`). Disk: D: 461 GB free.
- F1.9 Engine ADR numbers: highest on `chantier/e19-suite2` is 0069, BUT the author's `chantier/demo-browser` (worktree
  `CasaEngineMonogame/.claude/worktrees/demo-browser`, tip `d8184c5d`) holds `0070-view-layout-area-and-window-ui.md`: next free
  engine ADR is 0071 (re-check before the commit, R-BR rule, plan line 342-350). Parent: highest 0046 on `chantier/e19-suite2`
  -> next 0047 (parent `main` is at 0043).

### State and remaining
- Remaining = L-a (engine, E19 stack, NOT in S2) + L-b (data + converter + DLL, ready as design and oracle, blocked only by L-a).
- Premise change to record: D-E19-74 says the engine half goes into S2 of the author's branch. S2 closed and merged without it, so
  the reason (no overlap with a branch in flight) is void. This is a technical re-ownership; treat as a session technical choice
  (precedent: D-E19-86/87/88 are "choix technique de la session"), amend D-E19-74 in the plan. If the controlling agent prefers
  strictness: it is the only question of this item.

(continued below)

### L-a, engine slice (to do now on the E19 stack; design = `loop-engine/notes.md` section 2-3, to be adapted to the S2 worker)
Rules (facts to port, each with its proof in `loop-engine/notes.md`):
- R-a1 `SoundAsset.LoopStartSample` (long sample frames, default 0, negative clamped to 0), `Load` reads optional `loop_start_sample`,
  `EditorAssetJsonSerializer.SaveSoundAsset` (`CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:791-799`) writes the key only
  when not 0; `SoundAssetInspectorPanel.CreatePreviewCopy` (`CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs:318`) copies it.
- R-a2 `WavStreamReader.SeekToSample(long frame)`: `DataOffset + frame * BlockAlign`, `_dataBytesRead = frame * BlockAlign`, throws
  `ArgumentOutOfRangeException` unless `0 <= frame < DataLength / BlockAlign`; `Rewind()` = `SeekToSample(0)`; no allocation.
  Trap: a non-44-byte header (18-byte `fmt ` + `LIST` chunk gives data offset 60; naive `44 + 600*4` lands on frame 596).
- R-a3 TWO loop sites must seek (new vs the 2026-10-05 design): `MusicPlayer.FillQueue` (inline mode and the synchronous first fill,
  `MusicPlayer.cs:366-380`) AND `StreamingWorker.Channel.Service` (`StreamingWorker.cs:253-265`, `Channel.Reset` `:205` needs the
  loop start argument; `Acquire(reader, isLooped, name)` `:60`). Out-of-range loop start: warn once in `Play`, fall back to 0.
- R-a4 The per-call bound of `FillQueue` already exists (`MusicPlayer.cs:353-357`, S2). Not to redo.
- R-a5 Queue depth: DO NOT change in L-a (D-E19-75: measure first, O-E19-63).
Files (closed list, engine): `CasaEngine/Framework/Audio/SoundAsset.cs`, `.../Streaming/WavStreamReader.cs`, `.../Streaming/MusicPlayer.cs`,
`.../Streaming/StreamingWorker.cs`, optional `.../Streaming/WavFormatInfo.cs`; `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`;
`CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs`; tests `CasaEngine.Tests/Audio/WavStreamReaderTests.cs`, `MusicPlayerTests.cs` (or a new
`MusicPlayerLoopStartTests.cs`), `MusicPlayerWorkerTests.cs`, `SoundAssetTests.cs`, `SoundAssetEditorSerializationTests.cs`; docs
`docs/engine/audio-system.md` (sections 3, 5, 9, 11; `:415` says the loop rewinds the reader), ADR 0071 (see F1.9) + `docs/decisions/README.md`,
engine plan `ai-agent/tasks/<subject>-tasks.md`; demo `CasaEngine.Demos/Demos/AudioDemo.cs` + generated 3 s WAV (design `loop-engine/notes.md` 5).
Conflict risk: the author's `chantier/demo-browser` edits demos; `AudioDemo.cs` is the shared file.
Tests (values written in advance, `loop-engine/fillsim.py`/`fillsim.out`): W1-W6 (reader), L1-L7 (player; ramp R1000, loop start 600:
buffers 4000/1600/1600 B `[0..999] [600..999] [600..999]`; header-60 variant), worker-mode twin of L1, S1-S5 (asset + serializer round trip).
Mutations: `44 + ...` offset (W1, L6), `Rewind()` back in FillQueue AND in the worker (L1/L2/L6, worker twin), key written at 0 (S5).
Tests that move: none (a loop start of 0 keeps the bytes). Acceptance: `CasaEngine.Tests` full (2748+ tests at HEAD per plan; re-measure), demo run from
`CasaEngine.Demos/` with back-buffer capture and log check, listening left to the author.

### L-b, data + converter + DLL (ready as design and oracle, BLOCKED ONLY BY L-a)
- Data oracle: scratchpad `loop-data/` (see F1.8); rules in `loop-data/notes.md` section 1 (render to second jump J2 at cap 480 s, seam
  crossfade 735 samples, release tail 60 silent frames on the 7 one-shots, new fields `Looping`, `LoopStartFrame`, `LoopStartSample`).
- Closed file lists (copy from `loop-data/notes.md` 4.1-4.5): analyser `AlundraTools/AlundraDataExtractor/Program.cs`; converter
  `Readers/SoundManifestReader.cs` (BgmEntry + ReadBgm + nullable helpers), docs of `Writers/AudioWriter.cs`; converter tests
  `BgmFreshSoundSystemTests.cs` (T-D, T-E, golden red on 45 and re-pointed), `SoundManifestReaderTests.cs`, `AudioWriterTests.cs`; DLL
  `Alundra/Scripts/AlundraMusicPlayer.cs` (StartVoice, StopCurrentVoice, IsCurrentVoiceAlive, CurrentVoiceForTests, ResetForTests,
  BgmManifestEntry); DLL tests `AlundraMusicPlayerTests.cs` (22 PlayCalls lines), `AlundraBgmFadeDirectorTests.cs` (39 + the flip at :448),
  `AlundraWarpDepartureTests.cs` (27), `FakeAudioBackend.cs` (`FakeAudioClipProvider.OpenStream` at `:335` returns null: must serve a
  seekable WAV; `GetPendingBufferCount` hard-coded 0 at `:189`, safe thanks to the per-call bound); parent ADR 0047 (see F1.9);
  `docs/plan-e19-opcodes.md`; data re-extraction + mirror + export (+643 MB x 3 copies).
- Sequence: re-extraction into a scratch folder first (`diff -rq` against the oracle), then in place; mirror `data-extracted/` from the
  remaster folder per memory "always-rerun-full-export" (check the DIRECTION of any diff first), then full export in place with manifest proof.

## 2. E19.d2c2 (section 1.2h.3.2, plan lines 3713-4075) and D5b
State at HEAD (facts): D0, D1, D2, D3, D4 done; D5a done and CONFIRMED (`950b85e`, verifier on it, plan 3985-3995); first-pass verifier
CONFIRMED on `103dcb7` (plan 4021-4033). NOT done: D5b (= F2 "Z platforms carry the hero", UH-16, plus M-16/M-17/M-18 attached to it),
D6 (final verify/close), D7 (author recipe, never reached in the 2026-10-06 recipe: plan 10506-10510). Code facts: no `UH-16`, no
`LastTickDeltaZ` anywhere in `Alundra/` or `Alundra.Tests/` (rg); `EntitySupport.UpdateRidingEntityOfHero` (`EntitySupport.cs:290`) and
the per-frame `UpdateRidingEntities` (`:270`, called from `AlundraWorldProxy.cs:2912`) both use the EXACT rule `top + 1 == ModdedPosZ`.
Why paused: the value audit (scratchpad `e19d2c2-audit-d5/notes.md`, sections 3-4) contradicted F2 (C1 `IsOnGround` rule moves UH-10, C2/C3 UH-16
phases, G1 design: the hero runs BEFORE the platform in a frame, so at a change of speed of the platform the hero hops (stop after a rise)
or falls through (stop after a descent)); plan 3956-3959: "conception a reprendre ... a soumettre a l'auteur". D-E19-42 (author) wants the
hero carried by moving platforms. Binary rule for the rider: `0x80037364` (rider copies FinalForceZ of the carrier), `0x800364C8`,
`0x80036828`; binary computes riding for all entities on pre-move positions, then forces, then moves.
Census (e19h-disc/A notes, lines 82-101): moving-Z sites: `0x21` 16 sites in 8 maps (52, 83, 148, 152, 154, 344, 410, 423),
`0x6F` 16 sites in 6 maps (36, 41, 54-57), `0x3E` before them on 31, 36, 148, 344: none on the story chain (plan 3984, 4266).
Blocked by: a design decision (an order-of-update compromise: rider resolved with a one-tick phase lag, auditor's rule
`PosZ - (top+1) == F_applied - LastTickDeltaZ`; or a frame restructure, which collides with "Preserver l'architecture existante"). The plan says
resubmit to the author. Slice shape if the author agrees: D5b-1 tests UH-16 (rise, descent, sink, rise-stop, descent-stop, teleport, each phase from
rest) + UH-10/15 guards, D5b-2 `LastTickDeltaZ` field on the proxy + carried rule in `EntitySupport.cs`/`AlundraScriptedMotion.cs` (+ M-18 copy
`TickForceZ` in both fields), D5b-3 verify; D6 closes d2c2. Files: `Alundra/Scripts/EntitySupport.cs`, `AlundraScriptedMotion.cs`,
`AlundraEntityScriptProxy.cs`, tests `AlundraHeroObjectTopsTests.cs`, `AlundraJumpTestSupport.cs`. Not for tonight.
The only closable part without D5b: record "d2c2 closed except D5b (paused by the author)" (docs) -- needs the author's agreement.

## 3. E19.h2 (section 1.2n.2, plan 7264-7271)
State: sketch only, no plan, no code (rg: no NPC air-state field; `HeroAirborne` etc. exist for the hero only, `AlundraEntityScriptProxy.cs:225`).
Plan 6796-6797: it closes D-E19-40 (the NPC `0x25` returns one image before the original because of the engine's 4 px ground snap) and "Z des PNJ
en retard" of O-E19-28; "Avant le plan: une emulation de la boucle de scene du binaire pour les images de TR-V, A10 (Bergus), A12, A10J (Giles)".
Pins that would move (rg): `AlundraAnimationImpulseTests.cs:68` (UJ-1b lands at update 21, binary 22), `AlundraEntityContactArcTests.cs:151,218` (TR-V),
`AlundraBergusJumpArcTests.cs` (A12), `AlundraHeroJumpArcTests.cs:15` (A10J Giles, relational +-1 image), A10. Engine untouched (sentinel API exists:
`AlundraScriptedMotion.AirborneExternalDisplacementSentinel` `:75`). Blocked by: the missing emulation discovery (hours), not by a decision.
Value: 1-image fidelity of NPC jumps and ledge falls, nothing on the chain. Not plannable tonight at READY level; a discovery-only run is possible.

## 4. E19.k2 header (section 1.2k.2, plan 6293)
Facts: K2-3 WAS run: plan 6382-6389 "Verification d'E19.k2 (2026-10-03) ... Verificateur neuf: CONFIRMED" (CasaEngine.Tests 2502, Alundra.Tests 2539, cmp, six
traces, mutations). Stale text: header 6293 "verifier frais K2-3 a lancer", bullet 6371 "K2-3 ... (pending)", 6389 "non mergee" (engine branch
`chantier/e19k2-layer-mask` is an ancestor of engine `main`, git merge-base --is-ancestor true). Its P3 avis (M-23, test cannot fail) was closed
without change by E19.m5 (plan 10168-10170). Remaining for k2: recipe K2-4 (author, not reached 2026-10-06: plan 10508), O-E19-43 palette cycle
(engine, "plus tard"). Other stale headers found (docs-only fix list): 1.2h (2526, d2c1 done), 1.2j (4274), 1.2j.3 f2 (4591), f2b (4849), f3 (5315),
f4 (5660), f4c (5925), 1.2o.5 G2b (8253: G2b-1..3 all CONFIRMED), 1.2o.6 G1/G3 (8614: G1, G3a, G3b done), 1.2o title (7456), 1.2s (9667: m0-m6
done, G1/G3/G4 leftovers live in `docs/plan-e19-m-annexe/backlog-2026-10-03.md`), 1.2w (10512, needs the L-a premise note). Open-point rows
resolved but not marked: O-E19-6, -7, -15 (E19.h1b1/h1b2, plan 6907-7000), -20 (h4), -26 (l1, plan 6547/6671), -48/-50 (m2), -8 (advanced).

## 5. Open points (plan section 3), state at HEAD and what is session-actionable
Conventions: FACT = read/measured with file:line; HYP = not verified.

### O-E19-57 (frozen frame during a warp departure) -> AUTHOR
Row says "Auteur"; choice = freeze the whole scene (engine captures the last image) or keep the gap. Today the port keeps ticking camera,
animations, NPC physics, HUD, dialogue during the fade (plan 10101, 10918; gating only `GameplayBlockedMask`, as the plan states).
No decision recorded since. Not tonight.

### O-E19-63 (stream margin measure) -> SESSION (measurement), gates L-b
FACTS at HEAD: `MusicPlayer.DefaultQueuedBufferTarget = 3`, buffer 16384 B (`MusicPlayer.cs:25-28`), so 278.6 ms best / 185.8 ms worst at 44.1 kHz
stereo 16 bit (`loop-engine/notes.md` 4.1). The S2 worker reads off-thread into a ring of `_queuedBufferTarget * 2` slots (`MusicPlayer.cs:134`,
`StreamingWorker`) but only the game thread copies ring to voice in `MusicPlayer.Update`, so a game-thread stall still starves the voice after 3
buffers (HYP derived from the code: confirm with the harness). Warps that keep the track = a synchronous load under live music
(`AlundraMusicPlayer.cs:210-212`): 389<->390 (track 25), track 7 (20 maps), 16 (137), 21 (56), 30 (48) (`loop-disc/notes.md` section 2). Only timing
evidence: a 1-second-resolution Debug log (the 478 load took more than 1 s).
Shape: out-of-repo console harness (precedent: memory "capture-ecran-backbuffer-en-processus"): open `alundra-project/AlundraGame.json` with
`GameplayExecutionPolicies.Runtime`, `GameManager.SetWorldToLoad(...)` for 389->390->389, two track-7 maps, two track-16 maps, a Stopwatch per
`Game.Tick`, max per world change, Debug and Release; pass criterion proposed in `loop-engine/notes.md` 4.6: max stall + 1 frame < 2136 ms for depth 24.
No repo file touched (the result goes to the plan row). It decides depth 3 -> 24 (a separate tiny engine change: `MusicPlayer.DefaultQueuedBufferTarget`,
tests `MusicPlayerTests.cs:38-53` and `:68-81`, `SampleCount`).

### O-E19-64 (text margin of font3 TextBlocks in the three older screens) -> SESSION, small, XAML only
FACTS: tracked hand-authored XAML, never written by the converter (`UiWriter.cs:43`; `.gitignore` re-opens `alundra-project/UI/Screens/`). `Padding="0"`
on the three files is on the root Window only (`InventoryScreen.xaml:8`, `SubInventoryScreen.xaml:8`, `SaveScreen.xaml:8`). The 18 `<TextBlock>`
(Inventory 4: lines 50-53, SubInventory 4: lines 49-52, Save 10: lines 36-48) carry no `Padding`, `LinePadding`, `WrapText`, `VerticalContentAlignment`.
Established pattern (ChoiceScreen, TextBoxScreen): `Padding="0" LinePadding="0" WrapText="False" VerticalContentAlignment="Top"` (`TextBoxScreen.xaml:36-38`).
Measured on a real GPU by the f2b1 discovery: texts drawn 1 px right and 1 px down at x1 and x2 (`docs/plan-e19-f2b1-annexe/view-verify.md` row 9,
`view-notes.md:27`); `Padding="0"` cancels it.
Closed file list (parent only): the three `.xaml`; tests `Alundra.Tests/UI/AlundraInventoryScreenXamlTests.cs`, `AlundraSubInventoryScreenXamlTests.cs`,
`AlundraSaveScreenXamlTests.cs` (new assertions on every TextBlock, pattern `AlundraTextBoxScreenXamlTests.cs:146-198`), optional pixel test on
`TextBoxGpuHarness.cs`; the plan row. No export; no DLL rebuild needed to ship (XAML is a project asset).
Tests that move: none expected (no existing test reads padding or layout bounds; rg on those five files finds only `PreferredWidth/Height` 320x240).
Acceptance: red-first XAML assertions + a GPU measure (first ink pixel at CanvasLeft + glyph offset, not +1) at x1 and x2; author recipe (look at the three
screens). Risks: `WrapText="False"` changes the behaviour of any long description line (HYP: check max line widths; the game pre-splits lines);
HudScreen and DialogueScreen have no font3 TextBlock (rg). Not a MGUI gap workaround: the f2b1c and f3b screens already set these in XAML.

### O-E19-66 (font3.png palette index 4) -> SESSION, tiny; needs the data cycle (re-extraction + mirror + export), bundle with L-b
FACTS: (a) the binary loads `taki\screen\wind.cl` (string `0x80023EFC`, loader `0x80044B48`, upload `0x80044BC4`) and fills the CLUT table
`*(0x80146E28 + 2*i) = GetClut(0x120, 0x1E0 + i)`, i = 0..15 (`0x80044B7C-0x80044B8C`), disassembled here with capstone; text bands and cursor use entry 8.
(b) the analyser writes `ui/font3.png` from `font3.FontBitmapTim`, FONT3.TIM decoded with ITS OWN CLUT 0 (`AlundraDataExtractor/Program.cs:1130`,
`Font3.LoadImageTim` `Font3.cs:141-145`); `Font3.GenerateFontBitmapTim(Color[] pal)` IGNORES `pal` (`Font3.cs:78-92`), so a real recolour needs new code.
(c) measured here (`night-backlog-disc/font3pal.py`): FONT3.TIM clut0 and WIND.CL palette 8 differ ONLY at index 0 (transparent: (156,165,132) vs (0,0,0))
and index 4: 5-bit (10,11,7) -> (82,90,57) vs (9,10,7) -> (74,82,57), same bit-replication convention. Index 4 has exactly 44 texels, in glyph cells 4, 14, 15,
21 (8 texels, `\W5`), 22-25 (2 each), 26-29 (5 each), 64 '@' (5): matches `oracle-verify.md` 3.1. No letter or digit.
Predicted diff: re-extraction changes exactly 1 file, `ui/font3.png` (44 texels (82,90,57,255) -> (74,82,57,255), the other 65 492 unchanged, index 0 kept
(156,165,132,0)); export changes `UI/Textures/font3.png` and `report.json` (HYP: AssetInfos unchanged). Oracle = pixel-exact decode, not bytes (GDI+ PNG encoder).
Closed file list: analyser `AlundraTools/AlundraDataExtractor/Program.cs` and `AlundraTools/AlundraEngine/Text/Font3.cs` (new recolour method; MainForm also loads Font3);
a converter test (new, pins the 44 texels) next to `FontWriterTests.cs`; `docs/formats/font.md`; data mirror + export. Tests that move: none expected (font tests read
the PNG itself; no stored hash or colour pin, rg). Visible impact about nil (one 5-bit step on 44 texels) but it is a proven defect of the export.

### O-E19-9 (zero-duration Loop animations invisible) -> SESSION (converter + export), small
FACT confirmed at HEAD by scan (`night-backlog-disc/scan_zero_loops.py`): of 9624 `.anim2d` (5211 Loop) exactly 5 have all keys at t = 0:
`Entities/Interrupteur tricolore/bank127_anim0_{left,right}` and `Entities/YM08 barriere.../bank151_anim1_{down,left,up}`; their Visible track is `true@0`
then `false@0` (the terminator frame's empty-quad key at the same instant, `SpriteWriter.cs:640-645` and `:696`), hence invisible; the sibling
`bank127_anim0_down` (0.04 s) works. The binary shows the frozen image (plan row, not re-proved here). Fix = no hide key for the terminator of a zero-duration
Loop (HYP: check the engine sampler for a Loop of duration 0 before choosing). Files: `Writers/SpriteWriter.cs`, a converter test, export in place (5 files predicted, `report.json`).

### O-E19-75 -> sibling discovery `night-pad-disc` (pad-word age, D-E19-99)
Fact only: the save screen reads `_choicePadOfTick[tick]` (`AlundraWorldProxy.cs:2286`) while the dialogue pass reads the tick before (`:2269`); binary 19 samples,
port 18 after F3C. Two-line change plus the pins that depend on the selector pad.

### O-E19-65 and O-E19-70 (MGUI view one frame late; stale layout on window re-add) -> SESSION, docs only
Neither is recorded in the engine gap report `CasaEngineMonogame/ai-agent/audits/mgui-gaps-from-xaml-screens.md` (last entry G11; rg for O-E19-65/G12/G13
in the engine finds nothing). Rule (memory "signaler-manques-mgui-jamais-contourner"): consign gaps there. Add G12 (`CasaEngineGame.cs:537` UI update before world
`:555`, `UIRoot.cs:131`) and G13 (re-added window laid out one `Desktop.Update` late, 1898-3556 texels). Commit in the engine submodule (docs) + pointer bump.

### Needs the author (do NOT run tonight)
O-E19-33 (preset day3-start), O-E19-36 (XA tracks), O-E19-49 (36 rand draws per save), O-E19-57, O-E19-24 (reopen at chapter 16), O-E19-17 and O-E19-29 (author said
"later, separate slice": rules are known [binaire] (`ApplyEntityForces 0x800366FC`; fraction kept at landing) but each moves pins), O-E19-22/31/32 ("a placer"),
O-E19-35/37/39/23 (E14), O-E19-40/43 (engine, later), D5b.
### Resolved but unmarked in the table: O-E19-6, -7, -8, -15, -20, -26, -48, -50 (see section 4)
### Hygiene backlog still open (E19.m remnants, `docs/plan-e19-m-annexe/backlog-2026-10-03.md`)
G1 tests (M-14, M-16, M-17, M-22, M-24, M-28, M-29, M-40, M-45 test half, M-46), G3 (M-01, M-02, M-09, M-13, M-26), G4 (M-11, M-15, M-45, M-33), G5 engine (M-34, M-37),
G7 corpus tests (M-05, M-07). M-23 closed (E19.m5), M-19 and M-31 done (E19.m6).
