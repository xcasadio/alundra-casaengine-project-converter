# E19.f3b discovery: the faithful choice box VIEW, end to end (read-only on the repository)

Repo `D:\development\repo\alundra-casaengine-project-converter`, branch `chantier/e19-suite`, HEAD `ae32d54` at the start (E19.f3a landed; two docs-only commits for E19.f4a/f4b landed meanwhile, HEAD `2909ae3`, no code
change; `git status` = only the author's ` m CasaEngineMonogame` and `? alundra-datas-analyser`, as at the start). **Nothing built, run, exported, edited, staged or committed inside the repository**;
everything below lives in `C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\f3b-disc\` (`S` below). `S\bak` never used.

Tags: **[code]** read in the repository; **[bin]** from `docs/plan-e19-f3-annexe` (binary, model, tables); **[probe]** measured by the throwaway probe on this machine
(AMD Radeon RX 9060 XT, DesktopGL profile Reach, MonoGame 3.8.5.1); **[scratch export]** measured by running the built converter on a scratch output folder; **[hyp]** not proven.

## 0. Summary

1. **The exact files exist and are proven**: `S\view\ChoiceScreen.xaml`, `ChoiceScreen.uiscreen` (fresh id **`a623e495-f197-475c-887d-042140ec7248`**, name `ChoiceScreen`), `ChoiceScreen.design.json`, and the proposed
   C# `AlundraChoiceViewModel.cs`, `AlundraChoiceScreen.cs`, `AlundraChoicePresenter.cs` (same folder), compiled and run against the real `Alundra.dll` of HEAD (the probe assembly is named
   `Alundra.Tests` to get its `InternalsVisibleTo`, so it drives the REAL `AlundraChoiceBox` and the REAL director). The id is in no file of the repository and in no catalogue.
2. **0 texel mismatches** between the GPU render and an INDEPENDENT Python compositor (exported PNGs + `.fnt` + binary tables of `values.json`): 616 renders of the choice alone (every pass of V1-V7,
   V10 and two anim variants, x1 and x2 where it matters), 9 at x3, 26 in an offset view (window not 4:3; frame at x 311..320, labels up to x 375, cursor up to x 379, i.e. beyond the right edge of the image), 40 over the text box (rest and a full text row, text box
   first, choice above), plus the integrated runs (real director + real text box presenter + proposed choice presenter): 16 pixel checks, 0 differ. Controls differ as they must.
3. The machine -> view-model rule reproduces the binary's table on **428 passes, 0 difference** (drawn flag, frame x, both label x, cursor x, cursor image/sprite, selection, sounds).
4. **Label colour (O-E19-66) does not touch OUI/NON**: their glyphs hold only palette index 6 (+ transparent index 0); index 6 is `(41,49,16)` in FONT3.TIM's CLUT **and** in WIND.CL palette #8; the
   defect is index 4 only. Foreground/ink therefore equals the text box's ink, nothing to correct.
5. **Export prediction verified** by two scratch runs of the built converter (`--maps 389 --phase 7`, screens folder with 6 then 7 screens): exactly the deltas in section 5.
6. Two things to know (reported, not worked around): the one-frame view lag (O-E19-65) measured on the choice, and a **new one-frame stale-layout flash** when a choice is re-pushed after having been
   cancelled mid-slide (section 7). `AllowsClickThrough` must stay at its default `false` on the choice window (section 4.5).

## 1. The view: exact files, attributes, constants

Files (copy as is; LF in the scratch folder, the repo working tree is CRLF with `i/lf` index, same as the TextBoxScreen files):

| file | content |
|---|---|
| `S\view\ChoiceScreen.xaml` | 320x240 `IsTopmost` transparent `Window` (`Padding="0" BorderThickness="0"`, no title bar), `Canvas RootCanvas` (visibility bound), children in this order: `Frame` Image, `Cursor` Image, `Label0` TextBlock, `Label1` TextBlock |
| `S\view\ChoiceScreen.uiscreen` | `{"id":"a623e495-f197-475c-887d-042140ec7248","name":"ChoiceScreen","source_xaml_file":"ChoiceScreen.xaml","theme_name":"","preview_resolution":{"x":320,"y":240},"resource_files":[],"design_time_data_file":"ChoiceScreen.design.json"}` |
| `S\view\ChoiceScreen.design.json` | `view_model_type` `AlundraChoiceViewModel`; values: `RootVisibility` Visible, `Frame.Left` 176, `Label0` {OUI, 192}, `Label1` {NON, 240}, `Cursor` {`wind_173`, 196} (checked: `JsonConvert.PopulateObject` fills the proposed view model, [probe]) |

Element table (native pixels, layout bounds measured on the GPU session at x1 and x2, [probe]):

| element | source | CanvasLeft | CanvasTop | measured bounds at rest OUI (V1 N+19) |
|---|---|---|---|---|
| Frame | Image, `SourceName="829f31a7-39d1-5fc5-b436-befd59b50985"` (the baked 128x32 sprite; **GUID, not name**: the name is shared by png, texture, sprite) | bound `Frame.Left` (311 .. 176 .. 320) | 144 (static) | (176, 144, 128 x 32) |
| Cursor | Image, `SourceName` bound (`wind_150/173/201/228` by image 0-3) | bound `Cursor.Left` | 136 (static) | (196, 136, 16 x 16); NON rest: (244, 136) |
| Label0 | TextBlock, font3, text bound | bound `Label0.Left` | 152 (static) | (192, 152, **19** x 16) = the binary's OUI width |
| Label1 | same | bound `Label1.Left` | 152 (static) | (240, 152, **23** x 16) = the binary's NON width |

Draw order = the binary's OT order: frame cells, cursor (slot 5), labels (slot 6); the cursor overlaps the frame's top 8 rows; labels never overlap the cursor (cursor rows 136..151, labels 152..167).
RootCanvas bounds are the window's (320x240 at x1, 640x480 at x2; the integer scale is the canvas `RenderTransform`, as the text box).

Why static tops: the machine's `ChoiceDrawnState` carries `FrameY`/`LabelY`/`CursorY` but they are the constants of cfg `0x800A4FEC` (144, 152, 136; asserted over 428 passes). Binding them
instead is possible (one more property each) and costs nothing; static follows the author's rule "the structure of a screen is declared in XAML". **Decision for the plan.**

Which attributes carry the pixels (each removed alone from a copy of the XAML; V1 N+19 / V2 N+22, x1, texels differing from the reference, [probe]):

| removed | texels | verdict |
|---|---|---|
| label `Padding="0"` | **248** (glyphs one texel right and down: O-E19-64) | load-bearing |
| window `BorderThickness="0"` | **5375** (a border is drawn, first at (0,0)) | load-bearing |
| window `Padding="0"` | **3910** (the content is inset) | load-bearing |
| label `LinePadding="0"`, `WrapText="False"`, `VerticalContentAlignment="Top"`, `Foreground="White"`, `AllowsInlineFormatting="False"` | 0 | not needed for a 3-letter single-line label with an automatic width; kept for the same reasons as the text box rows (a longer label, a theme with another default colour, markup in a label) |
| frame by name instead of by GUID | 0 in the stand-in provider (it resolves the last asset of that name, here the sprite) | keep the GUID: the production catalogue's lookup was not run [hyp], and the name is shared |

The probe label strings come from the real export too: `Etc_0067/0068` (0x44), `Etc_0065/0066` (save book), `Etc_0074/0075` (file menu) all resolve to `OUI`/`NON`, char codes `[79,85,73]` / `[78,79,78]`,
no stray space [probe, via `AlundraEtcStringTable` on the real `Etc.dialogue`] (the cursor x depends on the label length: 196 = 176+16+4*3-8).

## 2. View model and the rule from the machine to each bound property

`AlundraChoiceViewModel` (proposed `S\view\AlundraChoiceViewModel.cs`, 3.8 KB): `RootVisibility` (Collapsed by default), `Frame.Left` (`ChoiceFrameViewModel`), `Label0`/`Label1`
(`InventoryTextViewModel`: `Text`, `Left`; its `Top` stays unused), `Cursor` (`ChoiceCursorViewModel`: `SourceName`, `Left`), `internal int AppliedCount`, `internal void Apply(AlundraChoiceBox)`;
notify only on change. The cursor sprite array `{ "wind_150", "wind_173", "wind_201", "wind_228" }` duplicates `AlundraTextBoxViewModel.CursorSprites` (private): share it (make that one `internal`) or accept the 4 strings **(plan decision)**.

`Apply(choice)` (the whole rule):

```
if choice.Drawn is null            -> RootVisibility = Collapsed                      (nothing else touched)
else                               -> RootVisibility = Visible
                                      Frame.Left   = Drawn.FrameX
                                      Label0.Text  = Labels[0]   Label0.Left = Drawn.Label0X
                                      Label1.Text  = Labels[1]   Label1.Left = Drawn.Label1X
                                      Cursor.SourceName = CursorSprites[Drawn.CursorImage]   Cursor.Left = Drawn.CursorX
```

Per pass kind of the machine at HEAD (`AlundraChoiceBox.Pass`), N = the tick of the opener, C = the Cross pass (values of `values.json` V1/V2, all reproduced):

| pass | machine stage before > after | `Drawn` | view model | presenter |
|---|---|---|---|---|
| opener tick N (`Open`, sound 4) | Closed > Init (the pass of tick N ran before the opener) | null | untouched (Collapsed) | not pushed |
| init pass N+1 | Init > SlideIn (`UpdateSlide` once, **nothing drawn**) | null | Collapsed | not pushed |
| slide-in N+2 .. N+17 | SlideIn | frame x 311, 301, 292, 282, 272, 263, 253, 244, 234, 224, 215, 205, 196, 186, 176, 176 | Visible, positions follow | **push at N+2**, then apply |
| N+18 | SlideIn > Active (the draw still happens) | x 176 | Visible | apply |
| active N+19 .. C | Active | x 176; cursor x 196 (OUI) / 244 (NON) follows `Selection`; image = counter/10 (counter +1 per DRAWN pass, wraps at 40, never reset at open) | Visible; the sprite changes every 10 drawn passes | apply |
| the Cross pass C | Active > SlideOut (sounds 5, 2/3) | at rest (176) | as above | apply |
| slide-out C+1 .. C+17 | SlideOut | 176, 185, 195, 204, 214, 224, 233, 243, 252, 262, 272, 281, 291, 300, 310, 320, 320 (C+16, C+17 drawn **off-screen**) | Visible, positions follow | apply, window stays on the desktop |
| close pass C+18 (`ClosedThisPass`, result written) | SlideOut > Closed | null | Collapsed | **remove** (after `Apply`) |
| no choice / after close | Closed | null | untouched | nothing |
| `Cancel()` (abandon, map entry, `Open`, `CloseStandaloneChoice`) | any > Closed | null at once | at the next presenter tick: Collapsed | remove at that tick (the window is still drawn once in between, see 7.2) |

OUI answered at the earliest: drawn N+2..N+36, closed N+37; NON: N+2..N+37, closed N+38 [probe, integrated: choice pushed at N+2, removed at N+37 / N+38].

## 3. The GPU probe (what, against what, results)

Probe: `S\probe\Probe.csproj` + `Program.cs` (+ `gen\*.cs` = the proposed sources, `ChoiceBox` -> `ChoiceBoxForTests` by a copy step, the only difference with the sources meant for the repo), run from `S\bin`
(copy of the DLLs of `Alundra.Tests\bin\Debug`, identical hash to the repo's `Alundra.dll`; `dotnet Alundra.Tests.dll`, ~25 s). Skeleton = the harness of `Alundra.Tests/UI/TextBoxGpuHarness.cs`
(hidden `Game`, `CasaDesktopRuntime` from `CasaMonoGameBackendBootstrap`, stand-in `ProjectAssets`, real `FontStashSharpTextEngine` with the exported font3, `desktop.Update()` x3 then `Draw()` into a
`RenderTarget2D`, `GetData`). Reference = `S\compositor\ref.py` (numpy + PIL only), driven by `make_states.py` (reads the annex `values.json`, adds V10 = the hook's NON: Right N+19, Cross N+20, and two anim
variants, run through a COPY of the annex model in `S\compositor\annex`, nothing written in the repo) and compared by `compare.py` -> `S\view\results.md`.

The compositor itself is checked: its text box rest state equals `docs/plan-e19-f2b1-annexe/pixels/rest.png` (binary-derived) with 0 texels.

| group | renders | exact | note |
|---|---|---|---|
| choice alone, every pass 1..close of V1 to V7, V10, V1_anim35, V1_anim9 (x1, and x2 for V1 V2 V5 V6 V7) | 616 | 616 | slide-in, rest, selection changes (V2, V4, V5), held Cross (V3), default NON (V6: cursor/label off the right edge), counter 17 (V7: sprite cycle incl. wrap) |
| x3 (slide, rest, NON) | 9 | 9 | |
| view offset (97, 41) in a larger target, x2 and x3, frames at x 311, 301, 292, 320 (off-screen), V6 (cursor at 379), V2 close | 26 | 26 | nothing drawn outside the view, not in the margin on the right |
| choice over the text box: underlay `rest` (design rest state) and a first row of 18 x glyph 0x1A (14 px, ink from row 0: passes under the choice frame), V1 N+2/5/16/19/25/34/35, V2 N+22, V10 N+22/30, x1 and x2 | 40 | 40 | the 8-row overlap (y 168..175) and the covered text ink |
| control: same states, windows added in the wrong order (text box above the choice) | 4 | 0 | 1366 texels differ (largest 345): the order is what is measured |
| sensitivity: a render against the reference of a neighbouring pass | 6 pairs | all differ | 192 .. 3516 texels |
| machine + proposed view model vs the tables, pass by pass | 428 passes | 0 differences | also sounds |
| integrated (real `AlundraDialogueDirector`, real `AlundraTextBoxPresenter`, proposed `AlundraChoicePresenter`, stand-in view = `ScreenStack` semantics): question box typing, choice opened at tick 61, OUI and NON via `SelectChoiceForTests`, pixels at N+1/2/3/5/19/25/36/37/38 (OUI) and N+2/19/22/30/37/38/39 (NON) | 16 | 16 | text box expected composed from its VM dump, choice from the tables |

Reference images and sample points ("values written in advance"): `S\view\pixels\*.png` (19 images, x1/x2: rest OUI, rest NON, N+2, N+5, N+25, N+34, off-screen N+36, closed N+37, init N+1, overlap rest, overlap full row)
and `S\view\points.md` (per state: coordinates, expected RGBA, what it pins, the wrong value an order or padding mistake would give). Key texels: choice frame top-left `(176,144)` is transparent
(shows `(100,149,237)` alone, the text box's `(88,96,72)` at `(303,175)` over it); first ink of OUI `(194,154)` and NON `(240,154)` = `(41,49,16)` (`(152,160,128)` with default padding);
cursor wind_173 first opaque texel `(197,138)` = `(192,192,192)`; order points such as `(286,168)` = `(184,176,144)` (choice over) vs `(88,96,72)` (text box over).

## 4. Where it is pushed and removed, how it follows the window, z-order, presenter tick

### 4.1 Wiring (to write, sketched; shape = the text box's, `AlundraWorldProxy.cs:378-386`, `:1443-1470`, `:2161`, `:2190`, `:2858`)
- fields `_choiceScreenWired`, `_choicePresenter`, `_choiceScreen`; `TryWireChoiceScreenOnce()` = copy of `TryWireTextBoxScreenOnce` (retry until the UI view, asset manager and fonts exist; one attempt, a failure to build
  is logged once and ends the retries: the machine then runs without a view, as the box does); call it right after `TryWireTextBoxScreenOnce();` (`:2161`).
- the tick, second loop of `Update`, **right after** `_textBoxPresenter?.Tick();` (`:2190`): `_choicePresenter?.Tick();` (so the text box is pushed first when both become drawn in one tick).
- `OnEndPlay`: `_choiceScreen?.Dispose(); _choiceScreen = null; _choicePresenter = null;`.
- seams: `AttachChoicePresenterForTests(vm, screen, uiView)`, `ChoiceScreenForTests`, `AttachChoiceScreenForTests(screen)`; director: `internal AlundraChoiceBox ChoiceBox => _choice;` next to `Box` (`ChoiceBoxForTests` stays for the existing tests).
- `AlundraDialogueDirector` class doc (`:108-109`, "the engine's presenter is only the choices' window (until E19.f3)") becomes stale; `HasPresenter` keeps its meaning.

### 4.2 Push / remove
`AlundraChoicePresenter.Tick()` (proposed): `Drawn != null` -> push once (first drawn pass N+2) then `Apply`; `Drawn == null` and pushed -> `Apply` (Collapsed) then remove. Mirrors `AlundraTextBoxPresenter`.
The presenter never leaves the window on the desktop while Collapsed, so a window is never updated Collapsed. Measured [probe]: push tick N+2, remove tick N+37 (OUI) / N+38 (NON); the text box is pushed at its own first
drawn pass (tick 1 in the run) and removed at its release (tick 134), long after the choice.

### 4.3 Follows the window
`XamlUIScreenBase.NotifyScreenBounds` is driven by `UIRoot.Update` only for screens ON the stack (`ScreenStack.cs:120-131`); `AlundraChoiceScreen` copies `OnScreenBoundsChanged`/`ApplyScreenBounds` of the text box (window = desktop
bounds, canvas scale = `max(1, width/320)`). The window is built at the first push (`Initialize` -> `BuildWindow`, bounds read then), so a resize while it is off the stack is seen at the next `UIRoot.Update` after a
re-push (one frame at the old scale) [code, same as the text box]. Test = the text box's `AssertFollowsTheDesktop` pattern (`AlundraScreensFollowTheWindowTests.cs:88` helper, `:163` the text box's `[Fact]`).

### 4.4 Z-order with the text box and with the save screen
- Both windows `IsTopmost="True"` (so is `SaveScreen.xaml`; the HUD's is not): `MGDesktop.Draw` draws `Windows.OrderBy(IsTopmost)` (`MGDesktop.cs:1725`, stable) = insertion order among topmost. `ScreenStack.Push` appends the screen's
  windows at the first push (`ScreenStack.cs:58-74`) and `Remove` deletes them (`:97-107`): **push order = z-order**, `UILayer` orders nothing. Presenters tick text box first, choice second.
- The corpus guarantees the order [bin]: the box is up before and after every choice (V9 save book: choice opens over a box still typing; sailor 12: `0x51` after the answer, box closes later; the lone save-screen
  question: `SaveScreen` pushed first). `Open` of a new dialogue cancels a pending choice, so a text box re-push while a choice is up does not arise.
- [probe] the window order on the desktop is `[textbox, choice]` from tick 63 to the choice's removal, and the pixels are exact (40 renders + integrated); the inverted order gives 1366 texels of difference.
- f4 note [hyp, for f4c]: the binary interleaves slots (name frame 5, choice frame and cursor 5, name text 6, choice labels 6); with the name box at x 64..175 and the choice at 176..303 nothing overlaps, so
  "choice window above the whole text box window" is equivalent; if a name text could reach x >= 176 the binary would draw it over the choice frame (not reproducible with two windows).

### 4.5 Mouse and z-order (new, [probe])
The two windows are full-screen; `MGWindow.ActivatesOnClick` defaults to true (a press brings the window to front) and `AllowsClickThrough` to false. With the defaults, left presses at three points leave the order
`[textbox, choice]` unchanged (the topmost window consumes the press; also unchanged in `[choice, textbox]`); **control**: with `choiceWindow.AllowsClickThrough = true` the same presses reach the text box window and the
order flips to `[choice, textbox]`. So the XAML must not set `AllowsClickThrough`; worth one assertion in the XAML test (`false`). The pad path is unaffected (the mouse has no equivalent in the binary's choice).

### 4.6 Layer / modal
`Layer => UILayer.Modal`, `IsModal => true`, like the text box screen (D-E19-80). `ScreenStack` only freezes the `IUIScreen.Update` callbacks below a blocking screen (both screens' are empty) and `HasModalInput` feeds
`InputRouter.ResolveModalView` (routes input to the view holding the modal screen; one view in Alundra). [hyp] the pad (built from `AlundraPlayerController` mappings, `AlundraEntityScriptProxy.cs:1201-1204`) is not
blocked, as it was not by the engine's modal dialogue box; the text box recipe (F2B1C-4) and this slice's recipe are the proof.

## 5. Export prediction (checked by two scratch runs of the built converter)

Method: `S\export-check\conv` = copy of `alundra-casaengine-project-converter\bin\Release\net9.0-windows`; outputs `A` (the 6 versioned screens) and `B` (the same + the 3 proposed files), run
`dotnet alundra-casaengine-project-converter.dll data-extracted <out> --maps 389 --phase 7` (12 s each, verification PASSED). Differences B - A, nothing else:

- files: `+ UI/Screens/ChoiceScreen.xaml`, `+ UI/Screens/ChoiceScreen.uiscreen`, `+ UI/Screens/ChoiceScreen.design.json` (OutputFileCount **+3**; OutputSizeBytes +4332 here with LF files, a few bytes more with the working tree's CRLF xaml).
- `AssetInfos.json`: **+1 entry**, nothing removed or changed: `{"id": "a623e495-f197-475c-887d-042140ec7248", "name": "ChoiceScreen", "file_name": "UI\\Screens\\ChoiceScreen.uiscreen", "asset_type": "uiscreen"}`, placed **just before `DialogueScreen`** (ordinal path order of `RegisterVersionedScreens`: `ChoiceScreen` < `DialogueScreen`; in B at positions 18474/18475).
- `report.json` counters: `Assets.UiScreen` 6 -> 7, `Verify.Loaded.uiscreen` 6 -> 7, `Verify.Assets` +1, `Verify.Loaded` +1, `Verify.LoadableFilesOnDisk` +1, `Metrics.OutputFileCount` +3, `Metrics.OutputSizeBytes` +size of the 3 files;
  Warnings, Errors, WarningsByCategory unchanged (Messages differ only by the output path of the scratch run); durations vary as always.

Applied to `alundra-project/report.json` at HEAD (values of the last full export, 2026-10-06 02:04): `Assets.UiScreen` 6 -> **7**, `Verify.Loaded.uiscreen` 6 -> **7**, `Verify.Assets` 22417 -> **22418**, `Verify.Loaded` 20027 -> **20028**,
`Verify.LoadableFilesOnDisk` 20027 -> **20028**, `OutputFileCount` 23747 -> **23750**. In git only the three versioned files appear (`.gitignore` keeps `alundra-project/UI/Screens/` only); `AssetInfos.json`/`report.json` are
ignored files that change on disk. [code] `UiWriter.RegisterVersionedScreens` (`UiWriter.cs:161-206`) catalogues each `*.uiscreen` by its envelope id and name (only the `.uiscreen`; `.xaml` and `.design.json` are named by it);
`AssetVerifier` (`:55` loader, `:320` `VerifyScreenFiles`) loads it and checks both named files exist. No converter change; converter tests build their own temp folders (no test reads the real catalogue count).

## 6. Tests that would move, new tests, harness change

Existing assertions that **change meaning** (after f3a they assert "the engine's window is not pushed"; they stay true but their rig must now carry the choice presenter):
- `Alundra.Tests/AlundraTextBoxWiringTests.cs` T4 stacking test `WithAChoice_TheTextBoxIsPushedFirst_TheEnginesWindowAboveIt_AndTheAnswerClosesOnlyTheEnginesWindow` (`:175-209`): rewrite as text box then **choice screen**:
  `Pushed == [textBox, choice]` after N+2, `Removed == [choice]` at the answer (37 passes, unchanged), text box still pushed; the `AlundraDialoguePresenter` is no longer needed in it. `AChoiceWithoutABox_StillClosesThroughTheStandaloneRoute` (`:212-224`):
  a lone choice pushes `[choice]` and `CloseStandaloneChoice` removes it at the next presenter tick.
- `Scripts/AlundraSaveScreenPresenterTests.cs` `Question_TheDialogueScreenGoesOverTheSaveScreen_AndLeavesAlone` (`:139-165`): its comment/assert "only the save screen is on the view" becomes `[save, choice]` while the question is up if the rig gets a choice presenter
  (otherwise it stays true; recommended to extend: it is the lone-question stacking proof).
- `AlundraDialoguePresenterWiringTests.cs:179-181` and `:259` (`Assert.Empty(recorder.Pushed)` after `OpenChoice`): stay true, the world's game in those tests has no asset manager, so no choice screen is wired; do not touch.
Unaffected [code]: `AlundraChoiceBoxTests` (machine), the director/opcode tests reading `ChoicesForTests`/`ChoiceBoxForTests`/`IsAwaitingChoice`, `AlundraTextBoxPixelTests`, `AlundraTextBoxScreenXamlTests`, `AlundraScreensFollowTheWindowTests`
(existing facts), converter tests.

Needs a harness edit: `Alundra.Tests/UI/TextBoxGpuHarness.cs` `TextBoxScreenAssets.New()` registers only `TextBoxScreen` + font3, so `new AlundraChoiceScreen(assets, ...)` would throw "no screen asset": give it the choice envelope too (id
`a623e495-...`, `UI\Screens\ChoiceScreen.uiscreen`) or a list parameter; `withTheScreen:false` (the text box failure test) must keep meaning "no screen asset". Extra effect: `TryWireChoiceScreenOnce` called by a test that runs `proxy.Update` with
`TextBoxScreenAssets.New()` would log (not throw) "the choice screen could not be built" once; none of today's tests does (`TheRetry_*` call `TryWireTextBoxScreenOnce` directly).

New tests (shapes exist): `AlundraChoiceScreenXamlTests` (copy of `AlundraTextBoxScreenXamlTests`: envelope id = `AlundraChoiceScreen.ScreenAssetId`, design data populates the VM, bare modal shell, children order `Frame, Cursor, Label0, Label1`,
frame by GUID, label attributes, `AllowsClickThrough` false, bindings push at rest bounds `(176,144,128x32)`, cursor `(196,136,16x16)`, labels at `(192,152)` / `(240,152)` - widths 19/23 only on the GPU, the headless font has other advances);
`AlundraChoiceViewModelTests` (`Apply` per pass kind of section 2 against the real machine, V1/V2 frames, notify only on change); `AlundraChoicePresenterTests`/wiring (push at N+2, remove at N+37/N+38, text box first, retry, `OnEndPlay`, map entry removes it,
proxy ticks it after the text box presenter and with no hero); follow-the-window `[Fact]`; `AlundraChoicePixelTests` (GPU, `[AlundraGpuFact]`) with the reference PNGs of `S\view\pixels` (to version, e.g. `docs/plan-e19-f3-annexe/pixels/`) and `points.md`, x1 and x2,
the offset view at x >= 311, the overlap and the inverted-order control. The reference compositor (`ref.py`, `make_states.py`, `compare.py`) and `values_all.json` are the versionable "values in advance" (like `docs/plan-e19-f2b1-annexe/scripts/`).

## 7. MGUI / engine gaps (reported, never worked around)

1. **O-E19-65 (known), measured on the choice** [probe, engine frame order]: a bound change reaches the pixels one frame later; frames N+3..N+36 show the layout of the previous pass with the cursor sprite of this pass
   (29 of 37 frames of V1 exactly "previous layout + current sprite", the other 8 are frames where nothing moves). Not a defect of the screen; accepted like the text box's.
2. **New: stale layout on re-push.** A window removed from the desktop is no longer updated; `Visibility` applies at once, positions at the next `Desktop.Update`. After a choice **completed** the stale layout is the
   off-screen one (x 320: nothing visible, measured exact B-pattern for a second choice); on the **very first** use a fresh window draws nothing. After a choice **cancelled mid-slide** (`CancelChoice` = the save book's abandon/reset, `Open` while a choice is pending, `InstallForMapEntry`:
   whether one of them happens mid-slide in play is not established, no case of the corpus is known) the next choice's first drawn frame shows the old frame for one frame: **2388 texels, x 234..319, y 138..175** at the first drawn pass (`fo_c`). Engine side: a window added back
   to a desktop should be laid out before its first draw (or `Desktop.Update` after the world update). If the author wants a screen-side guard later (not proposed now): park the view model off-screen for one more pushed tick before removing.
3. `UIRoot` cannot be built without a `CasaEngineGame` (sealed, needs a view), so the push/stack contract is proven with a stand-in that does what `ScreenStack.Push/Remove` do (windows appended at the first push, removed on removal) plus the code reading
   of `ScreenStack.cs`; the real `UIRoot`/`CasaUIAssetProvider` chain stays proven only by the in-game recipe.
4. The headless text engine does not give font3 advances (label widths 19/23 are only measurable on the GPU session).
5. By-name sprite lookup is last-writer-wins over names shared by png/texture/sprite (`AssetCatalog`): why the frame stays a GUID (also noted for the text box).

## 8. Not proven (hypotheses)

- The real `UIRoot` + `CasaUIAssetProvider` path, other GPUs/drivers, the pad while modal screens are up (section 4.6), the first drawn frame in the real engine on a first-ever push (measured with the stand-in order: nothing drawn, as for a fresh window).
- Label ink index/palette when the binary renders its labels (`RenderTextBitmap(label, ..., pen 0, rank 0, 128, 16)`, same call family as the text bands, `clut 8`): the glyphs of O, U, I, N use only index 6, equal in both tables; [hyp] the pen/rank arguments do not change the index (not re-read in the binary here).
- That the binary's name text cannot reach the choice frame (f4c).
- A label longer than 6 characters or with `{c`/`}c` escapes (the machine cuts at 6 and does not decode them; none in the corpus: 101 sites, always OUI/NON).

## 9. Suggested decisions for the plan (all small)

1. Static `CanvasTop` 144/152/136 in the XAML (recommended) vs three more bound properties.
2. Labels reuse `InventoryTextViewModel` (Top unused) vs a 25-line `ChoiceLabelViewModel`; cursor names shared with the text box VM (make `CursorSprites` internal) vs duplicated.
3. `AlundraDialogueDirector.ChoiceBox` accessor next to `Box`.
4. Modal like the text box; do not set `AllowsClickThrough`/`ActivatesOnClick`.
5. Accept the cancel-mid-slide flash (report it) or add the park-before-remove guard; either way list it as a gap, not a workaround of MGUI.
6. Version the 19 reference PNGs + `points.md` + the three Python scripts under the f3 annex; the C# pixel test reads the PNGs as the text box's does, plus the synthetic order/offset controls.
7. Recipe for the author (in game): sailor 12 of map 389 (choice over a typed box), the save book question, the file menu OUI/NON of the save screen; check slide-in from the right, cursor over the selected label animating, Cross-only
   validation, slide-out, the text box untouched under it; no flash at the second choice of a session.

## 10. Index (all under `S`)

`view\ChoiceScreen.xaml|.uiscreen|.design.json`, `view\Alundra{ChoiceViewModel,ChoiceScreen,ChoicePresenter}.cs`, `view\pixels\*.png`, `view\points.md`, `view\results.md`, `view\notes.md`, `view\new_guid.txt`;
`probe\Probe.csproj`, `probe\Program.cs`, `probe\DialogueTestAssets.cs`, `probe\gen\*.cs`; `compositor\ref.py`, `make_states.py`, `compare.py`, `states.json`, `values_all.json`, `annex\` (copy of the model);
`palette\pal.py` (FONT3.TIM vs WIND.CL palette #8); `export-check\{conv,A,B,runA.log,runB.log}`; `out\` (every PNG and JSON the probe wrote, `console.txt`, `compare.txt`).
Rerun: `cd S\probe && dotnet build Probe.csproj -c Debug`; `cd S\compositor && python make_states.py`; `cd S\bin && dotnet Alundra.Tests.dll`; `cd S\compositor && python compare.py` (set `PYTHONDONTWRITEBYTECODE=1`).
