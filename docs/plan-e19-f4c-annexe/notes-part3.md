
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
