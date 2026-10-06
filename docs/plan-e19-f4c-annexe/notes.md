# E19.f4c discovery notes (view of the speaker name box and the dialogue portrait) - running log

Repo `chantier/e19-suite` HEAD `79bbe5b` (clean; the engine submodule is on `chantier/e19g2d-overlay-blend` with `CasaEngine.Launcher/Program.cs` modified by the author: untouched).
Read-only on the repository. Probes live in this folder: `rig.py` (copy of the f4 MIPS rig over `emu.py`/`lib.py` copies), `d.py` (disassembler), `namecells.py`, `uvprobe.py`, `t_ot.py`.
Tags: [bin] read in ALUN_CD.EXE (France) or run in the MIPS interpreter; [data] read in `data-extracted/` / `alundra-project/`; [code] read in the repo (file:line); [calc] computed here;
[hyp] hypothesis. Facts from the f4 annex that I re-ran are marked "(re-run)"; facts only cited are marked "(annex)".

## 1. Name box - what is drawn

1. **Frame graphic** [bin]+[data] (re-run `namecells.py`): cfg `0x800A58BC` = (x 64, y 140, 14 x 4 cells), two cell buffers `0x800A4FFC` / `0x800A545C` (one per display buffer),
   56 cells of 8 x 8 each, same (u, v) in both buffers, `u0/v0` read from the EXE data; composing them from `data-extracted/ui/wind.png` (all 56 tiles are `PaletteIndex 0` in `wind.json`)
   gives a 112 x 32 RGBA image that is **byte-identical** to the exported `UI/Textures/g_textTilesConfiguration.png` (the sprite `91ca17ae-e279-5a45-bb45-b15fbabdde68`, 112 x 32).
   The cells' stored `x0/y0` (144..168) are stale data: `UpdateUiBoxesPosition 0x80047DD0` rewrites them to `(cfg.x + 8c, cfg.y + 8r)` before each draw.
2. **Cell prim** [bin]: the cells are initialised once by the boot-time reset `0x8005A0C8` (called from the UI init `0x80044BE4` at `0x80044C28`), not by the opener: `SetSprt8` (`0x80084414`), `SetSemiTrans(p, 0)`
   (`0x800842FC`), `SetShadeTex(p, 1)` (`0x80084324`), clut = `*0x80146E28` (palette #0 of WIND.CL, the same variable the text box and choice cells use). So: opaque, raw texture (no modulation), palette 0.
3. **Draw order** [bin] (re-run `t_ot.py`, real `0x800481F8` + `0x80044C5C`): OT slot 0 text-box cells; slot 3 `full-screen DR_AREA, DR_MODE, PORTRAIT`; slot 5 `DR_MODE, name cells (56), choice cells (64), choice cursor`;
   slot 6 `DR_MODE, name DR_AREA, name SPRT, choice labels (2)`. All `DR_MODE` are built by `0x80044820` with `SetDrawMode(p, 0, 0, tpage, tw)` (`0x80085B60`, `a1 = a2 = 0` at `0x800449A0-0x80044A64`): **dither off, drawing to display area off**.
4. **Name text** [bin, annex binary-notes 2/7, re-checked]: ETC string rendered once at open into VRAM band 3 (960, 336) by `RenderTextBitmap` from the font3 glyph rectangles (`0x800472D0`), one SPRT 255 x 16 (u 0, v 0x50, **palette #8** of WIND.CL
   = the palette of the text rows and of the choice labels, ink (41, 49, 16)), re-positioned EVERY pass at `x = cfg.x + trunc((112 - w) / 2)`, `y = cfg.y + 8 = 148` (`0x8005A4B8-0x8005A534`), `w` = `CalcTextWidth`. So the text slides with the frame.
   Per-pass x of the frame (cfg.x): pass k = 1..15: `320 + trunc(-256 (k - 1) / 15)` = 320 303 286 269 252 235 218 201 184 167 150 133 116 99 82; passes 16.. : 64 (flags 5 -> 4 at the 18th). Close (pass j = 0..14 from the trigger T, j = 0 in T itself):
   `64 + trunc(256 j / 15)` = 64 81 98 115 132 149 166 183 200 217 234 251 268 285 302, then 320 at j = 15 and 16, released at j = 17 (T + 17), nothing drawn that pass. (Closed forms checked against `values.json`.)
5. **Name clip is a no-op** [bin] (re-run, `0x8005A5B8-0x8005A6E8`, DR_AREA (cfg.x + 16, 148, 258 clamped to 320, 34)): 60 names x 30 distinct x (slide-in, rest, slide-out) = 1800 checks, 0 cut, minimum (text left - clip left) = 12 px, widths 19..56. Not ported (and not needed).
   The only effect of that DR_AREA is on the choice labels (same OT slot 6, drawn after it): at rest it contains them; it would clip them if the name box were leaving while the choice were still on screen, which the corpus flow forbids
   (all 101 `0x44` sites of `docs/plan-e19-f3-annexe/sites44_detail.tsv` have a `0x36` wait-flag-off between the dialogue open and the choice, so the choice opens after the typing started (>= N+19, name box already at rest at x 64..175, the choice frame is at x >= 176 all along its slide) and the answer closes the choice (C + 18) before the text box close trigger (C + 19 at the earliest, f3 binary-notes)).

## 2. Portrait - what is drawn

1. **Prim** [bin] (re-run `uvprobe.py` on the real handlers + `0x80057EBC`): one POLY_FT4, code **0x2C** (textured, quad, modulated, **opaque**: no semi-transparency whatever the texels carry). Vertices (x, y), (x + w, y), (x, y + h), (x + w, y + h) (right/bottom edge exclusive:
   the rest quad (8,116)-(56,172) covers pixels x 8..55, y 116..171). UVs fixed: (u, v), (u + 48, v), (u, v + H), (u + 48, v + H) where H is the TEXTURE height of the frame record (56, or 72 for banks 122 and 162): the far edge is `u + w` (not `- 1`), so at 1:1 the
   rasteriser maps pixel i to texel i exactly. The three vertex colours are identical (r = g = b). tpage / clut come from the ENTITY's own tables (`+0x1B0`, `+0x1B4`), the pixels from the first image of the speaker's own sprite record (`SpriteRecord.GetPortraitImageset(br).Images[0]`
   in the extractor = what the binary reads at `*(record + 0xC)`); no dither (the OT's DR_MODE words are `SetDrawMode(p, 0, 0, tpage, tw)`: E1 word `0xE1000110`, bit 9 = 0, re-run `0x80085E5C`).
2. **Screen size is constant 48 x 56 at rest whatever the image** (hard-coded `0x80057E4C/0x80057E54`): a 48 x 72 image is squashed (binary), the port shows it whole (D-E19-49/90, rest (8, 100)).
3. **Per pass** (15 passes each way; closed forms of F4B-R3, all equal to `values.json` and to my lock-step run of `f4_model.py` + `model_drawn.py`): in: position `rest + trunc((head - rest) c / 15)`, size `trunc(48 (15 - c) / 15) x trunc(h (15 - c) / 15)`, colour `127 + trunc(128 c / 15)`;
   the first pass is 0 x 0 (255), the 15th pass of the flight is 44 x 52 (135) then the rest (128) from N+16; return (from the trigger T): same with `c` counted back and the head read at T, colour `127 + trunc(128 (15 - c) / 15)` = 127 at T (rest size) ... 246, last pass T+15 degenerate 0 x 0 at the rest position with colour 0, nothing from T+16.
4. **The colour ramp** [bin + calc]: vertex colour k = rgb / 128 on a modulated quad: opening 1.99 -> 1.05, rest 1.0, return 0.99 -> 1.92; the PSX GPU modulates `texel x rgb / 128` and saturates (a channel above 31/k of full scale whitens). It exceeds 1.0 on every
   pass except the rest and the pass at T (0.992). Effect measured on the 25 exported portraits (`tint.py`, 5-bit model [hyp: public GPU documentation, no hardware here], left-edge sampling): share of drawn texels that change by >= 8 / 255 per channel: 97-90% on the passes of size <= 32 x 37,
   mean max-channel delta 41-70 on the eight smallest quads, 21 at 38 x 44, 7 at 44 x 52, and 5.7 on the pass T (rest size, 127: one 5-bit level darker). So the opening and the return are a bright flash on a small quad (roughly 0.2 s of the 0.3 s) that normal tint omits.
   An **additive second pass** (draw normally, then again with `BlendType.Additive` and colour (k - 1)) reproduces the PSX 5-bit result within **8/255 per channel** on every portrait and every rgb 127..255 (computed in this session; mean 2-4).
5. **Texel selection on the flights** [calc + GPU probe]: the PSX rasteriser picks the texel under the pixel's top-left (`floor(i * src / dst)`, [hyp]); a GPU point sampler picks it at the pixel centre (`floor((i + 0.5) * src / dst)`). They differ on 25-72% of the destination texels of the 28 quad sizes of the
   flights (mean over the 25 portraits). The port can only do the GPU rule (author's D-E19-92 precedent: scaled quads at the resolution of the screen, smoother than the PS1 at k > 1). The real GPU result of the proposed XAML equals the centre rule exactly except on
   rows/columns where `(2 i + 1) * src / (2 dst)` is an integer (a float tie): 210 texels over 56 x1 frames, 0 outside those rows/columns; at x2/x3 the reference must be resolution-aware (centre rule at screen resolution), then 0..21 tie texels per frame.

## 3. Draw order and mapping to MGUI

Binary OT: slot 0 text-box cells (frame, cursor) | slot 2 text clip + bands | slot 3 full-screen DR_AREA, DR_MODE, **portrait** | slot 5 DR_MODE, **name cells**, choice cells, choice cursor | slot 6 DR_MODE, **name DR_AREA, name text**, choice labels.
Port: one canvas, children in order Frame, Cursor, TextClip(rows), **PortraitImage, NameFrame, NameText**; the choice screen is a separate window pushed after (above all of them).
Equivalences: portrait over the 4 px overlap of the text frame (y 168..171, x 16..55) OK; name frame over the text frame (y 168..171, x 64..175) OK; choice frame over name frame OK (same slot 5 order); **one inversion**: name text (slot 6) is above the choice frame (slot 5) in the binary, below it in the port; unreachable (previous section: name text right edge <= 148 at rest < 176).

## 4. Port design (view) - validated by a GPU probe (probe/, built and run in this folder only)

Probe: `probe/rt/Probe.exe` = the harness of `Alundra.Tests/UI/TextBoxGpuHarness.cs` (stand-in provider over `alundra-project/AssetInfos.json`, real font3, real `MGDesktop`, the machine's GPU) loading a PROPOSED `TextBoxScreen.xaml` with `UIScreenLoader`,
binding probe view models whose parts are the real public classes of `Alundra.dll` (`InventoryPortraitViewModel`, `InventoryTextViewModel`, `TextBoxCursorViewModel`), against the compiled engine/MGUI/Alundra libraries copied from `Alundra.Tests/bin/Debug` (snapshot of 2026-10-06 07:02).
References: `f4c_ref.py` (numpy + PIL; the f4 value model lock-stepped with the f2b1 drawn text box model; text rows and names composed from the BINARY's glyph table via `refcompose2`; frame, name frame, cursor and portrait pixels from the exported PNGs: the same caveat as f2b1/f3b).
Results (all with the proposal XAML in `proposal/TextBoxScreen.xaml`):
- rest frames of S1 (N+16..N+22, x1/x2/x3), S2, S3, S8 (48 x 72): **0 differing texels** (name frame, name text, portrait 1:1, text frame, order).
- every x1 frame of S1 N+1..N+41, S5 N+37..N+42, S8 flights: all differences are inside the portrait quad and on tie rows/columns (see 2.5); name frame, name text and text frame are exact on every pass.
- all 60 names x 3 positions (x 64, 303, 150): **180 images, 0 differing texel** against the binary-glyph composition (so `TextBlock font3`, `Padding 0`, no clip, x = frame + trunc((112 - w) / 2), y 148 is the binary's rendering for every real name).
- views with an offset (97, 41) at x2/x3, frames N+1, N+2, N+3, N+37, N+38: nothing drawn outside the view; inside = the picture without offset (claim 28 of the f4 dll-verify, UNCONFIRMED there, now CONFIRMED by the probe).
- a 0 x 0 pass (scale 0) draws nothing and throws nothing; Scale 0 is safe as in the inventory.
- **finding 1 (design)**: with the screen root kept visible while only the name/portrait are drawn (retry-ahead), the text frame, rows and cursor must be hidden individually (the shipped `Apply(box)` collapses the whole root): the first run drew the frame at (16, 0) on S5 N+40 (null `Frame.Top`). Fix proven: `Visibility` bindings on `Frame` and `TextClip` (the cursor already has one), default Visible so hand-built view models of the existing tests are unchanged.
- measured texture loads (decode + upload on this machine): map_6 sheet 512x238 3.5-4.0 ms, map_17 512x328 4.9-5.0 ms: no preload needed.

## 5. The port (what the plan can write)

**XAML** (`alundra-project/UI/Screens/TextBoxScreen.xaml`, three elements appended to `RootCanvas` after `TextClip`, plus two bindings; exact text in `proposal/TextBoxScreen.xaml`, which the probe drew):
- `PortraitImage`: `Image Stretch="None" SourceName="{Portrait.SourceName}" CanvasLeft="8" CanvasTop="116" RenderTransformTranslation="{Portrait.Translation}" RenderTransformScale="{Portrait.Scale}" Visibility="{Portrait.Visibility}"` (the inventory idiom, MGUI ADR-0020; top-left anchor; rest stays in the XAML at (8, 116)).
- `NameFrame`: `Image Stretch="None" SourceName="91ca17ae-e279-5a45-bb45-b15fbabdde68" CanvasLeft="{NameBox.Left}" CanvasTop="140" Visibility="{NameBox.Visibility}"`.
- `NameText`: `TextBlock Text="{NameBox.Text}" CanvasLeft="{NameBox.TextLeft}" CanvasTop="148" FontFamily="font3" Foreground="White" AllowsInlineFormatting="False" Padding="0" LinePadding="0" WrapText="False" VerticalContentAlignment="Top" Visibility="{NameBox.Visibility}"` (no `Width`, no clip canvas).
- `Frame` and `TextClip` gain `Visibility="{Frame.Visibility}"` / `"{Clip.Visibility}"` (finding 1: a screen kept up for the satellites alone must hide the text frame, rows and cursor).
- no change to `AlundraTextBoxScreen.cs` (`UseLinearFilteringWhenDownscaling = false` is already applied to every `MGImage` of the canvas at load, the portrait included).
**View models** (`AlundraTextBoxViewModel.cs`): new `TextBoxNameViewModel` (`Left`, `TextLeft`, `Text`, `Visibility`; `Apply(AlundraDialogueNameBox)`: `Drawn` null -> Collapsed, else `FrameX`, `TextX`, `Text` set only when it changes, Visible); `Portrait` = the existing `InventoryPortraitViewModel` with a NEW additive method
`ApplyDialogue(AlundraInventoryPortrait portrait, DialoguePortraitRef? source)` (not drawn this pass or no source -> Collapsed; else `SourceName = source.SpriteAssetId`, `Translation = (X - 8, Y - 116)`, `Scale = (DrawnWidth / 48f, DrawnHeight / (float)source.Height)`, Visible; a 0 x 0 pass stays Visible with scale 0: no visibility change in flight, as the inventory);
`TextBoxFrameViewModel.Visibility` and `TextBoxClipViewModel.Visibility` default **Visible** (so the hand-built states of the existing pixel and XAML tests are untouched); one new `Apply(box, speakerDrawn)` overload (the old `Apply(box)` = `Apply(box, false)`): root = `box.Drawn || speakerDrawn`; box not drawn but speaker drawn -> Frame, Clip and Cursor Collapsed.
**Presenter** (`AlundraTextBoxPresenter.Tick`): `any = box.Drawn || NameBox.Drawn != null || Portrait.DrawnThisStep`; push at the first `any`, one `Apply` per tick (keeps `AppliedCount` pins), remove at the first tick with no `any` (one last `Apply` before it, as today). Without a speaker this is the shipped behaviour to the call.
**Name text**: `AlundraDialogueNameBox` gets `public string Text` (set in `TryOpen`, additive). **Portrait source latch**: `AlundraDialogueDirector.OpenSpeaker` stores the accepted `DialoguePortraitRef` (`PortraitSource`) next to `_portraitSpeaker` and clears it with the reset (the view must not read the speaker's live field: a destroyed speaker recycled by ADR-0024 would swap the image mid-return).
**Preload**: none. Resolution is synchronous at the first `SourceName` set (`MGImage.UpdateActualSource` -> `MGResources.TryGetTexture` -> `CasaUIAssetProvider.TryResolveImage`, cached per desktop root scope, so once per sprite id; a collapsed image resolves too), the 13 canonical sheets are 14-111 KB, 512 x 106..1188; measured decode + upload 3.5-5 ms for the two sheets of the probe (< one 20 ms tick).
If a recette shows a hitch: `Window.Desktop.Resources.TryGetTexture(id)` at map entry for the map's speakers (no API missing).
**Lifetime facts that justify the union rule** (values.json S5): the second speaker's portrait first pass is N+38 and its name N+40 while the first box is still leaving (released N+40) and the second box first draws at N+41: on the pass N+40 only the satellites are drawn (portrait 6 x 7 at (139,101)).
**G11 (colour ramp above 1)**: a real MGUI gap (`MGImage.TextureColor` is multiplied, 8-bit, max 1; `BlendType.Additive` exists in `DrawSettings` and `MGImage` already swaps `DrawSettings` for the downscale filter, `MGImage.cs:239-262`, `DrawTransaction.cs:956` is the precedent of `SetDrawSettingsTemporary(CurrentSettings with {...})`).
Fix = one MGUI property (e.g. `MGImage.Brightness`, default 1; above 1 the image is drawn a second time with `BlendType.Additive` and colour (Brightness - 1)), its `Image` XAML attribute (bindable, `Controls.cs:1264-1312`), MGUI ADR-0021, an engine audit entry G11 (French, `ai-agent/audits/mgui-gaps-from-xaml-screens.md`), a GPU test with tolerance 8/255, pointer bumps (MGUI -> engine -> parent), plus
`Brightness = Rgb / 128f` in the two view models. No other gap found: the probe needed no workaround for anything else (known G8, `UseLinearFilteringWhenDownscaling`, is set in code by the screen as for every image).
The gap is only a gap if the ramp is wanted: D2 (author, inventory portrait) rules the ramp out for the same machine; D-E19-89 (session) extends D2 to the dialogue. This is the one open product question (below).

## 6. Pinned frames, values and how they are generated

Scripts (this folder, to be versioned as `docs/plan-e19-f4c-annexe/` with the paths made repo-relative): `assets.py` (sprite reader over the export), `f4c_ref.py` (model lock-step + compositor, `compose(rec, portrait_pixels, rule, tint)`), `gen_cases.py` (writes `out/cases.json` = the view-model state of each frame and `out/ref/*.png` = the references), `compare_gpu.py`, `tint.py`, `samplerule.py`,
`probe/` (a GPU probe, only to validate the plan: the test itself is a C# xunit test over the real screen like `AlundraTextBoxPixelTests`). Inputs: `docs/plan-e19-f4-annexe/model/*.py` (value model, validated on the binary), `docs/plan-e19-f2b1-annexe/scripts/` (text box drawn-state model), the exported PNGs, `names_widths.json`, `portraits_table.tsv`.
Frames (S1 = Jess, bank 4 = sprite `56f5809a-a47b-564e-b7f7-a66be1f98b04`, entity (200, 150, 0), camera (40, 20), text "AB"; S8 = Miming, bank 122 = `bf75c68e-424f-57c9-8f6a-535f33d769d7`, 48 x 72, name id 0x17A; S5 = two dialogues in a row; S2 portrait only; S3 name only):
| frame | on screen (binary) |
|---|---|
| S1 N+1 | text frame y 240 (below the screen), name frame x 320 (right of the screen), portrait 0 x 0 at (160, 98): a blank 320 x 240 image |
| S1 N+2 | name frame x 303 (17 px visible, text at x 348 off screen), portrait 3 x 3 at (149, 100), text frame y 236 |
| S1 N+8 | name 201 (text 246), portrait 22 x 26 at (89, 107), text frame y 207 |
| S1 N+13 | name 116 (text 161), portrait 38 x 44 at (38, 113), text frame y 183 |
| S1 N+16 | rest: name (64, 140) text (109, 148), portrait (8, 116) 48 x 56, text frame y 168 (typing starts N+19) |
| S1 N+21 | same, "AB" typed on row 0 |
| S1 N+22 | close trigger: name 64 (flags 6), portrait at rest with colour 127 (draws as rest in normal tint), text frame y 168 |
| S1 N+25 | name 115 (text 160), portrait 38 x 44 at (39, 112), text frame y 177 |
| S1 N+36 | name 302 (text 347 off screen), portrait 3 x 3 at (150, 99), text frame y 230 |
| S1 N+37 | name 320 (nothing visible), portrait degenerate 0 x 0 at (8, 116), text frame y 235 (only its top 5 rows on screen) |
| S1 N+38..N+40 | portrait gone from N+38, name at x 320 on N+38 then released (nothing) from N+39, text frame y 240 on N+38 and N+39, release pass N+40 (nothing drawn) |
| S8 N+16 | portrait 48 x 72 at (8, 100) (bottom 172), name Miming x 64 (text 64 + (112 - 44) / 2 = 98) |
| S5 N+37..N+42 | second speaker: portrait first pass N+38 (160, 98) 0 x 0, 3 x 3 N+39, 6 x 7 at (139, 101) on N+40 (the first box is released that pass, the second box first draws N+41: only the satellites are drawn), name N+40 x 320 |
Order points at S1 N+16 (the reference from `f4c_ref.py`): portrait over the text frame: (19, 168) portrait (72, 48, 32) over frame (88, 96, 72), (17, 170) (16, 8, 8) over (88, 96, 72), (20, 171) (144, 120, 72) over (184, 176, 144) (77 texels differ if the order is inverted); name frame over the text frame: (71, 171) (72, 64, 56) over (144, 136, 112),
(161, 171) (72, 64, 56) over (88, 96, 72) (6 texels); name frame corner (64, 140) = clear colour (transparent baked cell), (66, 142) = (152, 160, 128); first ink texel of "Jess" (110, 149) = (41, 49, 16).
Comparison policy proven by the probe: **rest frames and every pixel outside the portrait quad: exact**; inside the portrait quad on a flight: the centre-rule reference at the screen resolution (nearest upscale only for the rest) with a **tie tolerance** (a texel on a row or column where `(2 i + 1) * src / (2 dst)` is an integer may be either neighbour); the PSX rule is NOT the oracle (25-72% of the texels differ), say so in the ADR.

## 7. Files to touch (closed list proposal)

Production: `alundra-project/UI/Screens/TextBoxScreen.xaml`, `alundra-project/UI/Screens/TextBoxScreen.design.json`, `Alundra/Scripts/AlundraTextBoxViewModel.cs`, `Alundra/Scripts/AlundraInventoryViewModel.cs` (additive `ApplyDialogue`), `Alundra/Scripts/AlundraTextBoxPresenter.cs`, `Alundra/Scripts/AlundraDialogueNameBox.cs` (`Text`), `Alundra/Scripts/AlundraDialogueDirector.cs` (`PortraitSource`).
Not touched: `AlundraTextBoxScreen.cs`, `AlundraWorldProxy.cs` (the presenter already ticks after each pass), the export, the converter, `.uiscreen` envelope, `AssetInfos.json` (no export, no manifest).
Tests: `Alundra.Tests/UI/AlundraTextBoxScreenXamlTests.cs` (mover), `AlundraTextBoxViewModelTests.cs`, `AlundraTextBoxWiringTests.cs` (additions), new `UI/AlundraSpeakerPixelTests.cs`, additive helpers in `UI/TextBoxGpuHarness.cs`.
Docs: plan `docs/plan-e19-opcodes.md` (f4c section), annex `docs/plan-e19-f4c-annexe/`, parent ADR-0040 (next free number to re-check; f4a took 0039), no `docs/formats` change.
If G11 is fixed: MGUI `MGImage.cs` + XAML DTO `Image` + tests + MGUI ADR-0021, engine `ai-agent/audits/mgui-gaps-from-xaml-screens.md` (+ engine ADR if the engine owns any surface), pointers; the inventory view models (`AlundraInventoryViewModel.cs` + 2 XAML) only if D2 is lifted.

## 8. Tests and arcs that move (current -> new)

- **Arcs and traces**: none (presentation only; f4b changed no logic and f4c writes no logic; the six traces and `AlundraArcChecks` stay byte-identical).
- `AlundraTextBoxScreenXamlTests.RootCanvas_IsNativeThreeTwentyByTwoForty_AndHoldsTheFrameTheCursorThenTheTextClip`: children `["Frame", "Cursor", "TextClip"]` -> `["Frame", "Cursor", "TextClip", "PortraitImage", "NameFrame", "NameText"]` (rename), and the `LoadWindow` `imageSizes` gains `91ca17ae-...` = (112, 32) and the portrait id = (48, 56) (the headless provider answers 16 x 16 otherwise). The only certain mover.
- unchanged by construction (re-run as the gate): `Bindings_PushTheViewModel_AndTheBoundsAtRestAreTheOriginals` (default Visible keeps the frame and the clip at (16, 168, 288, 56) and (32, 172, 258, 50)), `DesignTimeData_PopulatesATextBoxViewModel_WithTheRestState` (new design members are additions; do not put `Translation`/`Scale` in the JSON: the inventory's design data only sets `SourceName` and `Visibility`),
  `AlundraTextBoxViewModelTests` (12: root collapsed while no box is drawn and no speaker; `ApplyingTheSameStateTwice_NotifiesNothingTheSecondTime` listens on vm, Frame, Clip, Row0-2, Cursor: add NameBox and Portrait to the list), `AlundraTextBoxWiringTests` (`AppliedCount` pins: `closePass - 2`, `PassCountForTests - 3`, 3, and the choice's 1 then 4 hold while `Apply` is called once per tick and not before the first drawn pass),
  `AlundraTextBoxPixelTests` (9) and `AlundraChoicePixelTests` (images and counts 338-345 unchanged: the new elements are collapsed in hand-built states), `AlundraScreensFollowTheWindowTests`, the inventory tests (`AlundraInventoryPortraitTests`, `...WiringTests`, the two screen XAML tests: `ApplyDialogue` is additive), `AlundraDialogueSpeaker*` (f4b).
- new: VM mapping per pass against `values.json` S1..S9 (`NameBox.Left/TextLeft`, `Portrait.Translation = (x - 8, y - 116)`, `Scale`), union lifetime on S5 (pushed once, removed once at the release, root visible on N+40), source latch, XAML test of the three elements (order, attributes, ids), design data, GPU tests (rest x1/x2/x3, 14 slide-in + 14 slide-out passes, S8, S5, S2/S3, 60 names x 3 positions, view offset, order points and control), G11 tests if fixed.

## 9. Acceptance, risks, questions

Acceptance: new tests red first on the code before, then green; the GPU tests green on the author's GPU (skipped without a GPU, so the run is the evidence); existing suites unchanged; `Alundra.Tests` Release then Debug, `cmp` of the DLLs, the six traces; no export; recette in the game (first dialogue of a named speaker with a portrait: Jess in map 6, Miming bank 122 for the 72 px case, two speakers in a row, a name without portrait, `0xC4`).
Risks: (1) flights are GPU-sampled (differs from the PS1 texels on 25-72%), equal to the proposal of D-E19-92, to be written in the ADR; (2) float ties make a flight test fragile without the tie rule; (3) the probe validated XAML + mapping with probe view models over a snapshot of the built libraries, not the future C# (the C# view models still have to match; the order of `Apply` calls and `AppliedCount` are the traps);
(4) the union rule changes the push/remove timing only when a speaker exists; (5) recycled speaker (latch); (6) G11 inconsistency with the inventory if fixed for the dialogue only; (7) GPU tests need `alundra-project/` exported (UI/Portraits).
Question (product, only one): see the report.
