# E19.f2b1 discovery - the DLL side at HEAD (read-only)

Repository `chantier/e19-suite` at `897504b` (working tree: `Alundra.Tests/AlundraHeroSlideTests.cs`, `Alundra/Scripts/AlundraScriptedMotion.cs`,
four `docs/hero-trace-389-*.txt` modified by someone else, untouched), engine submodule at `ebeb81c9`. Nothing built, run, exported, edited,
staged or committed in the repository. Scratch only in this folder (`slide_clip.py`). Tags: **[code]** read in the repository,
**[bin]** read in the binary `ALUN_CD.EXE` (France) with the capstone helper `../e19m0-disc/dd.py` (reading only), **[calc]** computed by
`slide_clip.py`, **[hyp]** hypothesis, not proven. Background read first: `../f2b-disc/notes.md`, `../f2b-disc-verify/verify.md`,
`docs/plan-e19-opcodes.md` sections E19.f2a, E19.f2b, E19.f2b0, E19.f2b1 (lines 4559, 4763, 4797, 4872).

## 0. Summary

1. The box (`AlundraDialogueBox`) exposes at HEAD no per-pass "what was drawn" state. It has: `Y`, `ScrollPixels`, `CursorImage`, `Lines`,
   `TypedText`, `Phase`; the three things R1 needs are missing (clip from the previous pass's cfg.Y, rows as drawn on the scroll-ending pass,
   per-band `\H` width). Two of them cannot be derived afterwards by a reader of the box (the old top band is **cleared inside the same pass**
   that ends a scroll, `AlundraDialogueBox.cs:496-501`; the previous Y is overwritten by `AdvanceSlide`, `:397/:402`).
2. `Y` is not the binary's cfg.Y between boxes: `Open` writes 240 (`:246`), the binary leaves cfg at 168 (static rect) until the first
   slide step writes 240. The first slide-in pass would clip from 239 instead of 172 (**C3** of the verification; [calc] table in section 1.4).
   A private `_cfgY` mirror fixes it without touching `Y` (so `AlundraDialogueBoxOracleComparisonTests.cs:235-240` does not move).
3. The typed prefix path (`ShowTypedText`, `TakeTypedChanged`, `_typed`, `_pendingNewLines`, `_typedChanged`) feeds only the engine presenter;
   `TypedTextForTests` has **zero** readers in `Alundra.Tests`, `TakeTypedChanged` has one (the director). All of it becomes dead with R5.
4. **R5 has two consequences the sketch does not state**: (a) the engine screen is today pushed at **`Open`** (the director sends an empty
   prefix `ShowTypedText()` at `AlundraDialogueDirector.cs:265`; K6 pins `(0, "")`); after R5 it is pushed only by `ShowChoices`, and after
   the answer `DialogueService.SelectChoice` leaves the service **Open**, so the (now empty) engine window stays pushed until the box is
   released (`Released`, `:471-481`) - an empty 150-px dark window; the answer needs an explicit `_presenter.Close()`; (b) the order of the
   screens in the stack inverts when `0x44` runs before the box's first drawn pass (latent: the known sailor-12 program has a `0x36` wait between
   `0x0D` and `0x44`, `docs/plan-e12-dialogues.md:72`, `:145`; section 2.4).
5. `ScreenStack` does **not** use `UILayer` at all: z-order and the modal-update rule are by **push order** (`ScreenStack.cs:58-72`, `:137-148`;
   the only `Layer` mentions in `UIRoot.cs`/`ScreenStack.cs` are comments). So "Layer Menu under the engine's Modal" is not a mechanism;
   the text-box screen must be pushed before the choice window.
6. Test census at HEAD: the plan's list is complete; exact lines in section 3. Beyond it nothing else reads the typed prefix. Two movers are
   not in the plan: the two last assertions of the **world-install wiring tests** (they push-check after `Open`) and the montage's
   `PrefixRecordingPresenter` constructor argument. `Closes` of that presenter and `TypedTextForTests` have no reader.
7. Choices today: one engine window (project markup `DialogueScreen.xaml`), bottom-anchored, >= 150 desktop px, that holds the question text
   **and** the buttons; after R5 it holds only the buttons and, at x2/x3, covers the whole faithful text area (section 4).

## 1. The box at HEAD, per pass

### 1.1 State held (`Alundra/Scripts/AlundraDialogueBox.cs`)

| What | Where | Written | Notes |
|---|---|---|---|
| `Y` (240 closed, 168 open) | prop `:139` | `Open` `:246` (=240), `AdvanceSlide` `:397`, `:402`, release `:350` (=168), `Reset` `:266` (=240) | Not the binary's cfg.Y between boxes (fact 2). Read by tests only (`OracleComparison :201/:239`). |
| slide: from, to, step, settle | `_slideFrom/_slideTo/_slideStep/_slideSettle` `:97-100` | `BeginSlide` `:379-385` (at `Open` `:247`, at the close trigger `:453`), `AdvanceSlide` `:388-407` | 15 steps (`Y = from + (to-from)*step/15`, C# truncation), 2 settle passes, 1 end pass (`:390-393`). |
| flags (1 slide-in, 2 slide-out, 4 active) | `_flags` `:120`; `IsActive` `:130`, `IsSliding` `:141`, `IsClosing` `:143` | `Open` `:245` (=5), slide end `:347`, close `:451`, release `:351` (=0), `Reset` `:265` | `Phase` `:157-188`. |
| bands (3 `StringBuilder`) + shift | `_bands` `:122`, `_bufferShift` `:113` (private, no accessor) | `Draw` `:639`, `Scroll` `:499-500` (shift + clear of the band `(shift+lineIndex)%3`), `Open`/`Reset` clear | `Lines` `:203-206` = bands rotated by the shift (allocates 3 strings + array per call). Text is the **display char** (Unicode of the Yarn text, or `(char)glyphId` for a `glyph` marker). |
| line index | `_lineIndex` `:112`, `LineIndex` `:197` | `NewLine` `:577` (0..2), `Open` | The row the next glyph goes to; 2 at every scroll arm (`NewLine :566-575`). |
| scroll | `_scrollPending` `:117`, `_scrollLeft` `:110`, `_scrollWait` `:111` (not reset by `Open`), `_scrollPixels` `:121`, `ScrollPixels` `:194` | `Pass` resets pixels `:337`; `Scroll` `:494-495` sets `(8-left)*16/8` = 2..16 | The shift happens in the **same** pass as the pixels = 16 (`:496-501`): `Lines` and `ScrollPixels` are inconsistent on that pass (fact 1). |
| typed prefix | `_typed` `:92`, `_pendingNewLines` `:93`, `_typedChanged` `:94`; `TypedText` `:200`, `TakeTypedChanged` `:209-214` | `Draw` `:632-640`, `Open` `:221-223`, `ReleaseCursor` `:555-558` (cleared at each page turn), `Reset` `:269-270` | The **page** typed so far (all lines, not only the 3 bands: after a 4th line it still holds line 1). Feeds only the engine presenter (`AlundraDialogueDirector.cs:394-405`). |
| cursor | `_cursorShown` `:118`, `_cursorCounter` `:119`; `IsCursorShown` `:146`, `CursorImage` `:149` (= counter/10, -1 hidden) | `Step` `:618` (`CursorWait` token), `ReleaseCursor` `:546`, `Scroll` wait branch `:489`, `AnimateCursor` `:409-421` (+1, wrap at 40), `Open` resets the counter `:236` | `AnimateCursor` runs in the slide branch `:358`, typed `:365`, interpreter `:376`, **not** in the scroll branch `:371` (as the binary: RenderText is not called there). Counter continues over the pages of one box (not reset by `ReleaseCursor`). |
| waiting for press | `IsWaitingForPress` `:152` | derived | |
| centre token | `case DialogueTokenKind.Center: continue;` `:606-607` | - | Ignored: no per-band width exists. The token comes from the marker `center` (`AlundraDialogueDirector.cs:357-359`, position-ordered by `Tokenize :323-342`). |
| page / token cursor | tokens `_tokens` `:90`, `_next` `:91` (private) | `AppendPage` `:252-259` (a page = its tokens + a `CursorWait` when a page follows) | Director side: `_pageCount` `:140`, `_pagesDelivered` `:141`, `_pageIndex` `:142` (`PageIndexForTests :626`), `_pageLine` `:143` (whole page, `CurrentLineForTests :630`). |

### 1.2 What the pass does, branch by branch, and what the binary draws in it

Binary `MsgBoxRender 0x80046EF0` [bin, re-read: `e19f-disc/binaire/render.txt`]: the clip is computed first (`0x80046F10-0x80046FC4`,
`cfg.Y` read at `0x80046F20`), then `flags & 3` selects the branch (`0x80046FCC-0x80046FD8`):
slide -> `UpdateUiBoxesPosition` (`0x80046FF4`), then `RenderText` (`0x800470A4`) unless the slide-out ended (`0x80047038-0x80047054`:
cfg restored from the origin, `DialogClosed` `0x8004501C`, return 0, no RenderText); typed -> `ProcessCloseAdvance` then `RenderText`;
scroll pending -> `ScrollText` only, return 1 (`0x8004708C-0x80047098`); else `TextInterpreter` then `RenderText`.

**New fact [bin]**: on the release pass the **frame is not drawn either**. `DialogClosed` calls `0x80047CB0` which stores 0 in the first
halfword of the callback entry (`sh $zero,($v0)` at `0x80047CB8`); the cell chaining `0x800481F8` (called by `0x80044C5C` at `0x80044CA0`,
after the dispatcher) skips every entry whose first halfword has bit 0 clear (`0x80048224-0x80048230`, loop over 13 entries of 0x1C from
`0x80153028`). Also `DialogClosed` zeroes `g_dialog_flags` and clears `0x18` of `0x800DC4B8` (the control flags). So `Drawn = false` on that
pass covers frame, cursor and text.

DLL `Pass` `:335-377` maps one to one:

| DLL branch | Lines | Drawn (binary) | Rows offset | Cursor |
|---|---|---|---|---|
| closed (`!IsActive`) | `:338-341` | nothing | - | - |
| slide, not finished | `:343-345`, `:358-359` | frame at the new Y, cursor if shown, rows at the new Y, clip from the Y of the previous pass | 0 | `AnimateCursor` |
| slide-in ends | `:347`, falls to `:358` | drawn (RenderText) | 0 | |
| slide-out ends (release) | `:348-355` (`Y = OpenY`, `_flags = 0`, `Released()`) | **nothing** | - | - |
| typed (close evaluated) | `:362-367` | drawn; the trigger pass still draws at 168 (slide begins next pass) | 0 | `AnimateCursor` |
| scroll | `:369-373`, `Scroll :456-502` | frame + rows (+clip), **no cursor** | 0 while waiting (`:487-491`), 2..16 | none (hidden by `:489` before) |
| interpreter | `:375-376` | drawn | 0 | `AnimateCursor` |

### 1.3 What R1 must add (names are proposals; all `internal`, on `AlundraDialogueBox`)

Rule of the proposal: **the snapshot is written inside `Pass`**, because two inputs (the previous cfg.Y and the old top band) are destroyed
inside the pass; a reader after the pass cannot rebuild them. No per-pass allocation: one mutable instance (or fields) updated in place,
strings assigned only when the bands changed.

| Binary fact | Field(s) | Written where | Derivation |
|---|---|---|---|
| Drawn at all (frame, cursor, rows) | `bool Drawn` | end of every `Pass` exit; false for `!IsActive` (`:338`), for the release exit (`:354`), and cleared by `Open` and `Reset` | `IsActive` after the pass. `Open` must clear it (a stale `true` would survive until the next pass). |
| Clip from the previous pass's cfg.Y (3a) | `int ClipTop`, `int ClipHeight`; private `int _cfgY` | `ClipTop`/`ClipHeight` computed at the **top** of `Pass` from `_cfgY` read before any branch; `_cfgY = Y` at the end of `AdvanceSlide`; `_cfgY = OpenY` at release `:350` and in `Reset`; **not touched by `Open`** | `ClipTop = min(_cfgY + 4, 239)`, `ClipHeight = min(50, 240 - ClipTop)` (`0x80046F2C-0x80046F64`). X is 32, width 258 (`0x80046F68-0x80046FC4`). |
| First slide-in pass clips from 168 (C3) | same | `_cfgY` initial value 168 (not 240) | `InitializeDialogMessage` never writes cfg.X/Y (verification C3); [calc] pass 1 clipTop 172 (not 239). |
| Frame/text at the new Y | reuse `Y` | already | RenderText runs after `UpdateUiBoxesPosition`. |
| Rows as drawn (3b) | `string Row(int r)` (3 strings) or `Rows[3]`; `int RowOffset` (= `ScrollPixels`, 0 or 2..16) | At every `RenderText`-equivalent exit: copy the bands (rotated by `_bufferShift`) **only when a band changed** since the last copy (dirty flag set in `Draw`, `NewLine`-scroll shift, `Open`, `Reset`). In `Scroll`: copy **before** the `if (_scrollLeft == 0)` shift at `:496`, with `RowOffset` = 16. | The pre-shift copy is the only way to keep the old top row (its band is cleared at `:500`). Visual difference between pre-shift@16 and post-shift@0 is exactly the one pixel row y = 172 of the old top line (descender glyphs only), one pass. |
| `\H` width per band, cleared at open and for the new bottom band at scroll end | private `int[3] _lineWidth`; `int RowX(int r)` | `Step`, `case Center` (`:606`): `_lineWidth[(_bufferShift + _lineIndex) % 3] = RestOfLineWidth()`; cleared in `Open` (all 3) and in `Scroll` at `:500` for band `(_bufferShift + _lineIndex) % 3` (the one just cleared) | `RestOfLineWidth` = sum of advances of the `Character`/`MarkedGlyph` tokens after `_next` up to the first `NewLine`, `CursorWait` or the end of `_tokens` (`CalcTextWidth 0x8004771C`: stops at `\A`/`\N`, skips `\T \Y \B-\G`, flags; a page always ends with `CursorWait` when another follows, `:255-258`, so the end of the list is the end of the text). `RowX(r) = w == 0 ? 32 : 16 + (288 - w)/2` with the band of row `r` = `(shift_at_draw + r) % 3` (`0x80045640-0x800456B8`, `ScrollText 0x80045B14-0x80045C70`); pre-shift width on the scroll-ending pass. |
| Cursor image from the box counter, none on scroll passes | `int CursorImage` (exists `:149`) | already right: `AnimateCursor` runs in the same pass; the scroll branch never calls it | Set `-1` explicitly in the snapshot on scroll passes to be safe (the scroll start branch `:487-492` does not clear `_cursorShown`; today it is false there anyway because `ReleaseCursor :546` cleared it first) **[hyp]**. |

Where the advances come from (Q1, decided: `font3.fnt` advances) - DLL facts only: the DLL holds no advance table today (grep: none);
`BitmapFontDescriptor` is `internal` to the engine and parses only `face`/`page` (`BitmapFontDescriptor.cs:28-60`); the glyphs are read by
FontStashSharp (NuGet binary, not readable here); `BitmapFont` exposes `SpriteFontBase Font` (`BitmapFont.cs`), but `UIFontRegistry.Acquire`
hands back only an `IDisposable` hold (`AlundraSaveScreen.cs:53`). The test side already reads `char id=.. xadvance=..` from the real `.fnt`
(`AlundraFont3GlyphTests.cs:95-101`) and proves the engine's measured advance equals it for raw ids 16, 18, 22, 26, 28, 29 (`:73`, `:399-418`;
GPU test). So the box needs an injected `int Advance(char display)` (host interface `IAlundraDialogueBoxHost :43-56`, or a ctor delegate):
production = a small reader of the exported `.fnt` text (path through the asset manager) or the engine text engine; tests = a table.
**[hyp]** a `glyph id=N` marker whose raw id has no `char` line in the 145-char `.fnt` has no advance; the corpus check (211 distinct centred
texts, `f2b-disc/notes.md` point 4) found none.

Pitfalls found while mapping (each needs a decision or a test line):
- `Draw` appends **display chars**; the width of a `Character` token is that of its display char, of a `MarkedGlyph` that of `(char)glyphId`.
- A second `\H` on the rest of the line: `CalcTextWidth` counts the code char `H` as a glyph (verification C1: `\H`..`\M`, `\O`..`\S`, `\U`, `: .. @`
  are read as normal glyphs); the box's `Step` treats `Center` as free. Not in the corpus after a first `\H` (verification C1 says no `\V` after
  an `\H`; a second `\H` not checked here) **[hyp]**.
- `Reset()` (`:262-284`) must clear `Drawn`, `_lineWidth`, set `_cfgY = 168`; it is the out-of-band close and the map-entry reset (no binary
  counterpart).
- The oracle (`AlundraTextBoxOracle.cs:102`) starts with `Y = BoxYOut` (240); for the first-ever box its clip would be 239 where the binary has
  172: the extended oracle (T1) must start at 168. (Oracle surface, noted here because T1 compares the two.)
- Multi-tick frames: the presenter reads after **each** pass inside the frame (section 2.2), but MGUI draws once per frame: only the last pass
  of a catch-up frame is seen (already in the plan's risks); the one-pass y = 172 row is lost whenever it is not the frame's last pass.

### 1.4 [calc] slide values (`slide_clip.py`)

Slide-in, `_cfgY` starting at 168 (clipTop, height): pass 1 Y 240 -> (172, 50) (C3; with a start at 240 it would be (239, 1)); passes 2-3 (239, 1);
pass 4 Y 226 (235, 5); 5 (230, 10); 6 (225, 15); 7 (220, 20); 8 (216, 24); 9 (211, 29); 10 (206, 34); 11 (201, 39); 12 (196, 44); 13 (192, 48);
14-15 (187/182, 50); settle passes Y 168 (177, 50), (172, 50); end pass (172, 50). Y after each pass = 240 236 231 226 221 216 212 207 202 197 192 188
183 178 173 168 168 168 (equals `AlundraTextBoxOracleTests.cs:92`).
Slide-out from 168 (Y after the pass -> clipTop): 168 -> 172, 172 -> 172, 177 -> 176, 182 -> 181, 187 -> 186, 192 -> 191, 196 -> 196, 201 -> 200,
206 -> 205, 211 -> 210, 216 -> 215, 220 -> 220, 225 -> 224, 230 -> 229, 235 -> 234, 240 -> 239, 240 -> 239; the plan's example (Y 168/168/172/177 ->
ClipTop 172/172/172/176) is this table. Cut of row 3 = Delta-Y - 1 pixel rows (C2).

## 2. Wiring today and where R4/R5 hook in

### 2.1 Who does what today [code]

- Build: `AlundraWorldProxy.InstallDialogueSystems` `:1198-1220` (eager, needs `GetActiveUIView()`: `new AlundraDialoguePresenter(uiView, world.Game?.UIFonts,
  world.Game?.AssetContentManager)` then `AlundraDialogueDirector.Instance.AttachToWorld(presenter, GameState, SoundPlayer)` and `InstallForMapEntry()`);
  per-frame retry `TryWireDialoguePresenterOnce` `:1236-1254` (guard `_dialoguePresenterWired :295`), called at `Update` `:2086`. Disposed in `OnEndPlay` (`:2764`, the presenter at `:2784`).
  Per-proxy presenter, session-scoped director/box (`AlundraDialogueDirector.cs:114`).
- `AlundraDialoguePresenter` (`:36-191`): wraps `DialogueService` + the engine `DialogueScreen` built with the project markup
  (`:85-87`, `ShowCloseButton = false`); `ShowLine :123-134` and `ShowChoices :136-147` push the screen on Closed -> Open (`PushScreenIfNeeded :160-169`);
  `Close :153-158` closes the service and removes the screen; `RequestClose :190` -> `AlundraDialogueDirector.Instance.NotifyPresenterClosed()`
  (wired by the engine as `window.WindowClosed` `DialogueScreen.cs:153`; `Desktop.Windows.Remove` in `ScreenStack.Remove :97-107` does **not** raise it,
  only `MGWindow.TryCloseWindow` does, `MGWindow.cs:1025-1026`; the project markup hides the title-bar close button, `DialogueScreen.xaml`
  `IsCloseButtonVisible="False"`). So a presenter-side `Close()` while the box is active cannot re-enter `NotifyPresenterClosed` **[code]**.
- Director -> presenter calls: `Open :240-266` (`_box.Open()` `:249`, then `ShowTypedText()` `:265`, which sends `""` because `Box.Open` sets
  `_typedChanged = true`, `AlundraDialogueBox.cs:223`: this is the push at `Open`); `Pass :434-439` (`_box.Pass` then `ShowTypedText()`);
  `Released :471-481` (`_runner.Stop()`, `_presenter?.Close()`); `NotifyPresenterClosed :492-506` (`_box.Reset()`, `_presenter?.Close()`);
  `InstallForMapEntry :217-237` (`:236`); `OpenChoice :509-523` (`ShowChoices`); `CloseStandaloneChoice :569-581` (`Close`, refuses while a box is open).
- Who calls `Open`: `AlundraEventProgramRunner.OpenDialog :1868-1891` (opcodes 0x0D, 0x5C, 0xC4; the degraded path `:1873-1882` plays the node headless
  with `NullDialoguePresenter :1950`) and `AlundraSaveBook.RunMessage :254`. `HasPresenter` (`:153`) = `_presenter != null`; tested by 0x39 (`:937`),
  0x44 (`:961`), `OpenDialog`, the save book (`:235`, `:272`) and the save screen (`:984`).
- Coupling to remember for f3 (not for f2b1): `AttachToWorld` builds the Yarn runner only `if presenter != null && gameState != null`
  (`AlundraDialogueDirector.cs:183-189`, `:204-206`): without the **engine** presenter the box opens **empty**. The text box therefore still needs the
  engine presenter to exist until f3 removes that dependency.
- Frame loop: pad loop `:2041-2082` (presenters of the other screens at `:2072`, `:2077`, `:2081`; it runs **all ticks first**), wiring retries
  `:2086-2090`, then the box loop `:2110-2135` (`Pass` at `:2113`, then the gate `:2115-2116`, `continue` `:2119` when blocked, map events `:2130`).

### 2.2 R4 hooks (the save screen is the pattern)

- New files by pattern: `AlundraTextBoxScreen : XamlUIScreenBase` (`AlundraSaveScreen.cs:27-117`: ctor `(AssetContentManager, UIFontRegistry)` holds font3
  `:53`, `OnWindowLoaded :79-93`, `OnScreenBoundsChanged :97-103`, `ApplyScreenBounds :107-116`), `AlundraTextBoxViewModel : ViewModelBase`
  (reuse `InventoryImageViewModel` with `SourceName`/`Left`/`Top`/`Visibility`/`Show`, `AlundraInventoryViewModel.cs:14-100`, and `InventoryTextViewModel :191-`),
  `AlundraTextBoxPresenter` (`AlundraSaveScreenPresenter.cs:45-65` shape: push when the director says so, `Apply`, remove).
- `AlundraWorldProxy` additions (all by copy of the save screen): fields `_textBoxScreenWired`, `_textBoxPresenter`, `_textBoxScreen` (after `:370`);
  `TryWireTextBoxScreenOnce` (copy of `:1399-1419`, needs uiView + `AssetContentManager` + `UIFonts`; **it constructs the screen with no try/catch**: a
  missing asset throws from `Update` every frame, same as the save screen); `AttachTextBoxPresenterForTests` (copy of `:1424-1427`); the call in the retry
  list `:2086-2090`; `_textBoxScreen?.Dispose()` in `OnEndPlay` (`:2764-2790`, next to `_saveScreen?.Dispose()` `:2776`); **`_textBoxPresenter?.Tick();` immediately after `:2113`**, before the gate read
  and before the `continue` at `:2119` (the pass runs even when the hero is null or the gate is closed). It cannot go in the pad loop (`:2072-2081`): that loop
  finishes all ticks before any box pass, so it would read the last pass for every tick.
- The presenter class is `public` in the pattern while `AlundraDialogueBox` is `internal`: the presenter ctor takes the public `AlundraDialogueDirector`
  and `Apply` is `internal` (InternalsVisibleTo exists: tests read `director.Box`).
- Wiring tests cannot reach `TryWireTextBoxScreenOnce` through the existing world fixtures: the reflected `CasaEngineGame` of
  `AlundraDialoguePresenterWiringTests.cs:145-160` / `:207-221` has no asset manager and no `UIFonts`, so the retry never completes there (as for the save
  screen); `AlundraArcSupport.cs:606-639` sets an `AssetContentManager` by reflection on such a game but no `UIFonts`.
- Push order (section 2.4): R4 should push the text-box screen at **`Open`** (invisible until the first `Drawn` pass), not at the first drawn pass.

### 2.3 R5 hooks (director side)

- Remove: `ShowTypedText` `:394-405` and its two calls `:265`, `:438`; in the box `_typed`, `_pendingNewLines`, `_typedChanged`, `TypedText`, `TakeTypedChanged`,
  the `ReleaseCursor` reset `:555-558`, the `Draw` appends `:632-637`, `Open :221-223`, `Reset :269-270`; `TypedTextForTests :633` (no reader).
  The guard `if (_awaitingChoice) return;` in `ShowTypedText` disappears with it (its reason - `DialogueService.ShowLine` drops pending choices,
  `DialogueService.cs` `ShowLine` sets `Choices = NoChoices` - no longer applies).
- Keep: `_presenter` for `ShowChoices`/`Close`/`SelectChoice`/`Choices`/`ChoiceSelected` (`:509-523`, `:569-581`, `:637-641`), `HasPresenter`,
  `NotifyPresenterClosed`, `_pageLine`/`CurrentLineForTests :630` (the director's own page, independent of the presenter).
- **Add**: close the engine presenter when the answer is taken while a box is active (e.g. in `TakeChoiceResult :532-544`, which runs on the script tick,
  not inside the `ChoiceSelected` event raised from the button handler): `DialogueService.SelectChoice` leaves `State = Open` (`DialogueService.cs`
  `SelectChoice`), `RemoveScreen` is called only by `Close`, so without it an empty window stays up until `Released`. The lone-choice path already does
  this through `CloseStandaloneChoice` (`AlundraSaveScreenDirector.cs:1032`), which refuses while a box is open (`:571-574`). No test pins "the engine
  service stays Open after an answer in a box" (grep of `presenter.IsOpen`/`State`: only `AlundraSaveScreenDirectorTests.cs:566`, `:635`, lone choices).
- Empty-window consequence **[hyp]**: with `CurrentLine` empty the project markup still declares `lblLine` (empty, `WrapText`, font size 16) + `pnlChoices`
  with `Spacing=8`, `Padding=14`, min window height 150 (`DialogueScreen.cs:136`): the window is the same size as today minus the text.

### 2.4 Stack order of the text box versus the engine choice window

- Fact [code]: `ScreenStack.Push` appends and registers windows in push order (`ScreenStack.cs:58-72`); `Update` updates only the screens at or above the
  **topmost** blocking screen (`TopBlockingScreen :34-48`, `Update :137-148`); `Layer` is never read there.
- Fact [code]: the interpreter keeps executing instructions in one call until one returns 0 (`AlundraEventProgramRunner.cs:374-420`), and `0x0D` returns its size
  (`:1889-1890`), so a program `0x0D` then `0x44` with nothing between would open the choice at the **same tick** as the box. The one known real program
  (sailor 12, `docs/plan-e12-dialogues.md:72`) has `0x50`, `0x36` (waits for the flag `\999` the text sets at its end, `:145`) between them: the choice opens
  only once the text has been typed up to that code (many ticks after the first pass). The save book waits `WaitTicks = 0x3C` (`AlundraSaveBook.cs:100`, `:257`, `:266-271`); the save screen's question has no box. Whether any
  other exported program reaches `0x44` before the first drawn pass was **not censused** (event programs not scanned here) **[hyp]**.
- Today: `Open` pushes the engine screen at its tick (`ShowTypedText`), so `OpenChoice` -> `ShowChoices` finds it open and the order is always [engine screen].
  After R4/R5 with "push at the first drawn pass": for the sailor-12 shape the order is [text box, engine choice window] (good); for a `0x0D` immediately followed
  by `0x44` it would be [engine choice window, text box].
- **[hyp]** consequences if inverted, to prove with a test: the text box (modal, full-screen transparent window like `SaveScreen.xaml`) is drawn over the choice window
  and is the top blocking screen, so the choice window is not updated by `ScreenStack.Update`; whether MGUI input still reaches its buttons is unverified.
  Options: (A) push the text-box screen at `Open` (hidden until `Drawn`), which makes the order independent of the program; (B) re-push the engine screen when the text
  box appears; (C) a window sized to the box instead of full-screen. Test: a recording `IUIViewRuntime` (as `AlundraDialoguePresenterWiringTests.cs:46-65`) asserting
  the `Pushed` order after `Open` + `OpenChoice` in one tick, and for the sailor-12 shape.

## 3. Census of the tests that move under R1-R5 (HEAD, `Alundra.Tests`)

Search used: every file mentioning `AlundraDialogueDirector|AlundraDialoguePresenter|DialogueScreen|AlundraDialogueBox|IDialoguePresenter|HasPresenter|
TypedTextForTests|CurrentLineForTests|PrefixRecordingPresenter|ShowLine`, then `\.Shown|\.Presenter|\.Closes|\.Pushed|\.Removed|ScreenForTests|DialogueService|
\.CurrentLine|ChoicesForTests|SelectChoiceForTests|\.Box\b|TypedText|ScrollPixels|CursorImage|\.Lines\b`.

**Move (red under R1-R5):**
| Test | Line | Assertion today | Why it moves | New shape |
|---|---|---|---|---|
| `AlundraDialogueBoxOrderTests.K5_...` | `:360` | `montage.Presenter.Shown.Last(s => s.Text != string.Empty).Text == "bonjour"` | `Shown` is empty (no `ShowLine`): `Last` throws | assert the drawn row 0 of the box at a frame during the wait (e.g. 60) is `"bonjour"` (stronger: shown *during* the choice, as the binary); `:352` (`ChoicesForTests`) and `:329` stay |
| `AlundraDialogueBoxOrderTests.K6_...` | `:366-380` (name `:366`, `:379`) | `Presenter.Shown` equals `(0,""), (19,"a"), (23,"ab"), (31,"ab\nc"), (35,"ab\ncd")` | no prefix sent; no push at `Open` | assert per-pass drawn rows through R1 on the `DialogueBoxPassDriver` (`AlundraDialogueBoxTestSupport.cs:32-75`); `:374`, `:380` (`CurrentLineForTests` whole page) stay green |
| `AlundraDialogueFramePassTests.AttachToWorld_RePointsWithoutResetting_...` | `:246` | `rePointedPresenter.CurrentLine.Text == "p"` (a `DialogueService`) | the new presenter is no longer driven by the typing | assert the drawn row `"p"` on the box, and that an `OpenChoice` after the re-point reaches `rePointedPresenter`; `:239-244` (cursor, page index, `CurrentLineForTests`) stay |
| `AlundraDialoguePresenterWiringTests.InstallDialogueSystems_WithAnActiveUiView_...` | `:138-186`, assertion `:179` | `recorder.Pushed.Count > 0` right after `Director.Open(...)` | no push at `Open` any more | push check after `OpenChoice` (or after a pass with the text-box test seam); `HasPresenter` `:175` stays |
| `AlundraDialoguePresenterWiringTests.Update_WiresThePresenter_OnceTheViewAppearsAfterWorldInit` | `:199-265`, assertion `:257` | same | same | same; `:232-238`, `:253` (`HasPresenter`) stay |

**Stay green but their premise changes (comment/name only):**
- `AlundraDialogueOutOfBandCloseTests.cs:48-69` (`AWindowDrivenClose_...`): calls the private `RequestClose` by reflection (`:62-65`) then asserts the box is down and the
  flags cleared; it never asserts a push. Green under R5 as long as `RequestClose`/`NotifyPresenterClosed` stay. The window it models now exists only over a choice.
  `:41-46` (`ShowCloseButton` false) unaffected.
- `AlundraDialogueBoxMontage.cs:40-85` `PrefixRecordingPresenter`: `Shown` is filled only by `ShowLine` (now never called by the director); its readers are only
  `OrderTests :360` and `:379`; `Closes` has **no reader anywhere**; the `Func<int> _pass` ctor argument (`:45`, `:149`) only stamps `Shown`. It must stay a choice-capable
  `IDialoguePresenter` (K5 `:324-352` use `OpenChoice`, `SelectChoiceForTests`, `ChoicesForTests` through its inner `DialogueService`).

**Unaffected (verified by reading the assertions):**
- Direct presenter tests: `AlundraDialoguePresenterWiringTests.cs:67-126` (`ShowLine`/`ShowChoices`/`Close` on the presenter class itself, which keeps its API),
  `UI/AlundraDialoguePresenterFontTests.cs:195-257` (`ShowLine` on the presenter, `lblLine` font), `UI/AlundraDialogueScreenAssetTests.cs:113-159` (project markup keeps `lblLine`/`pnlChoices`).
- Choice flows (push on `ShowChoices`): `AlundraSaveScreenDirectorTests.cs:535-640` (`:556`, `:630`, `:567`, `:636`), `Scripts/AlundraSaveScreenPresenterTests.cs:137-160`,
  `AlundraSaveBookTests.cs`/`AlundraSaveBookEndToEndTests.cs` (`ChoicesForTests`, `SelectChoiceForTests`; the book's `Open` no longer pushes but nothing asserts the push),
  `AlundraDialogueOpcodeDispatchTests.cs:443-472`, `AlundraDialogueOpcodesProductionTests.cs:138-139`, `AlundraDay3SceneArcTests.cs:223`.
- `CurrentLineForTests` readers (director's own page, independent of the presenter): `AlundraDialogueFlagMarkerTests.cs:122-216`, `AlundraDialogueYarnRenderingTests.cs:77-200`,
  `AlundraDialogueOpcodesProductionTests.cs:117`, `:150`, `AlundraDialogueSpeakerOpcodeTests.cs:111`, `:132`, `AlundraDialogueOpcodeDispatchTests.cs:129`, `:400`,
  `AlundraSaveBookTests.cs:139`, `AlundraVisionArcTests.cs:120`.
- Tests that attach a bare `new DialogueService()` only to make `HasPresenter` true (`AlundraDialogueFlagMarkerTests.cs:46`, `OpcodeDispatch :102`, `OpcodesProduction :105/:218/:326/:450`,
  `YarnRendering :58`, `SpeakerOpcode :88`, `GlobalFreezeEntityUpdate :103`, `HudDirector :527`, `HudPresenter :373`, `YarnVariableStorage :354`, `FramePass :76/:165/:188/:223`).
- Capture presenter tests (`AlundraDialogueCapturePresenterTests`, `AlundraDialogueFlagMarkerTests.cs:239-276`): the capture presenter does not change. The Yarn-runner tests with private
  `FakeDialoguePresenter` (`YarnBindings`, `YarnVariableStorage`, `YarnVariableStorageProbe`) never touch the director.
- `AlundraDialogueBoxOracleComparisonTests.cs:197-241` compares phase, glyph count, cursor flag, control flags and `Y` per frame; no field R1 adds is read there. It moves only if `Y`'s
  semantics change (`:235-240` carve-out, and `AlundraTextBoxOracle.cs:102`): the `_cfgY` mirror keeps `Y` as it is.
- `IntroTraceHarnessTests.cs:594-600` calls `AlundraDialogueDirector.Instance.Pass` with no proxy: no presenter is ticked there.
- Arc harness (`AlundraArcSupport.cs:161-222`): a real `AlundraDialoguePresenter` on a `RecordingUIViewRuntime`; no arc asserts its `Pushed`/`Removed`.

**New tests R1-R5 add (not moves):** per-screen XAML tests (pattern `UI/AlundraSaveScreenXamlTests.cs`: `UIScreenLoader.Load(desktop, source)` on a
`HeadlessUiTestHarness.NewDesktop()`, envelope id, `design.json` populated by `JsonConvert.PopulateObject` so the view model needs public settable properties),
`UI/AlundraScreensFollowTheWindowTests.cs` (`AssertFollowsTheDesktop :74-100`) and `Scripts/AlundraAssetHandleReleaseTests.cs` (`OnEndPlay` release) entries,
a `TryWireTextBoxScreenOnce` wiring test, the push-order test of section 2.4.

## 4. Choices today (f3 overlap)

- Callers [code]: opcode 0x44 (`AlundraEventProgramRunner.cs:950-989`: first call `OpenChoice` with the ETC OUI/NON labels and returns 0, then polls `TakeChoiceResult`),
  the save book (`AlundraSaveBook.cs:285` question after its own box, `:295` answer), the save screen's lone question (`AlundraSaveScreenDirector.cs:1000`, `:1021`, `:1032`).
- Director: `OpenChoice :509-523` (sets `_awaitingChoice`, subscribes `ChoiceSelected`, `_presenter.ShowChoices`), result `index == 0 ? 1 : 0` (`:525-529`), `TakeChoiceResult :532-544`,
  `CancelChoice :552-557`, `CloseStandaloneChoice :569-581`.
- Engine side [code]: `DialogueScreen` is `Layer = Modal`, `IsModal = true` (`DialogueScreen.cs:130-131`); window width `min(720, max(320, boundsW - 80))` (`:148`), height grows from
  `MinWindowHeight = 150` (`:136`, `ResizeToFitContent :336-352`), bottom edge at `bounds.Bottom - 48` (`BottomMargin :138`, `:350`); content = `lblLine` (the line: today the typed page, and
  it **stays** under the buttons after `ShowChoices` because the service keeps `CurrentLine`) + `pnlChoices` with one `MGButton` per label (`:370-405`, `new MGButton(Window, _ => _presenter.SelectChoice(i))`).
  Project markup `alundra-project/UI/Screens/DialogueScreen.xaml`: `Padding=14`, `Background rgba(12,18,26,235)`, `IsTopmost`, `lblLine FontSize=16`, `pnlChoices Spacing=4`. Selection is by MGUI
  (mouse/MGUI keyboard navigation); the Alundra pad is not used (`AlundraSaveScreenDirector.cs:972-973`: the dialogue screen "owns its keys").
- [calc] overlap with the faithful box once R5 empties the line: at x2 (640 x 480) the window is 560 wide at (40, 282..432) against the faithful frame (32, 336)-(608, 448) and rows at y 346, 378, 410
  (32 px each): rows 0 and 1 fully covered, row 2 covered down to y 432; at x3 (960 x 720) 720 wide at (120, 522..672) against rows at 519..663: all three rows covered; at x1 (320 x 240) it spans y 42..192 and covers only
  row 0 (173..189). **[hyp]** the window may be taller than 150 with its content (empty line + 2 buttons ~ 2 x 28 + padding); not measured. If the choice window is **above** the text box (push order, section 2.4) the question
  text is hidden for the whole 0x44; if below, the frame covers its lower part.
- Binary side (for f3, from `f2b-disc/notes.md` 1.2): choice box = slot 3 of the callback table, cfg `0x800A4FEC`, offsets `{16, 8, 32, 4}`, update `0x800501FC`, layer 5. Not re-read here.
- Option to keep today's UX during f2b1 if the author finds the 0x44 regression too visible (not recommended by the plan, D-E19-79): while a choice awaits, still send the page text to the engine
  window; costs keeping `_typed`.

## 5. Hypotheses and risks (summary)

1. **[hyp]** Push order (section 2.4): latent only (a `0x44` before the box's first drawn pass); input/updates of the choice window if the text box ends up above it. Needs the push-order test and, for the input part, a GPU/MGUI test; the corpus was not scanned for such a program.
2. **[hyp]** `HasPresenter` stays "engine presenter exists" while the text box can be missing for a few frames (its wiring needs `AssetContentManager` and `UIFonts`, the engine presenter only a UI view): boxes run but
   nothing draws until `TryWireTextBoxScreenOnce` succeeds; the box machine is independent of the view by F2-R2. In tests with no `UIFonts` the text box is never wired (same as the save screen).
3. **[hyp]** `Desktop.Windows.Remove` + `Windows.Add` of the same screen instance across consecutive ticks (box released at tick T, next box opened by the same tick's map events): the save screen already
   re-pushes the same instance (`AlundraSaveScreenPresenter.Tick :47-64`), so it works for the pattern; not tested for this screen.
4. **[hyp]** Advances for a raw glyph id missing from the 145-char `.fnt` (section 1.3).
5. Per-frame drawing shows only the last pass of a catch-up frame (the y = 172 row of the scroll-ending pass and the slide clip lag are then only seen when that pass is the last of its frame).
6. `TryWireTextBoxScreenOnce` copied from `TryWireSaveScreenOnce` has no try/catch: a missing or invalid text-box asset throws from `Update` every frame (the engine presenter degrades gracefully, `AlundraDialoguePresenter.cs:66-77`).
7. `Open` + R5: nothing is pushed until the first drawn pass; option A of section 2.4 needs a hook from `Open` to the presenter (e.g. a director event, or a presenter that pushes on `Box.IsActive` and shows on `Box.Drawn`); the proxy loop only runs the presenter after a pass, so an `Open` made after the pass of its tick is pushed one tick late unless the hook exists.

## 6. Questions that need the author (product, only if genuinely open)

None of the above needs a product decision beyond what D-E19-78..81 settle. Two technical choices want a line in the plan (not product): the stack order of the text box versus the engine choice window (option A:
push at `Open`, only needed if a program can reach `0x44` before the first drawn pass), and where the `\H` advance table lives in the DLL (injected `int Advance(char)`, production reader of the exported `.fnt`; Q1 decided "from font3.fnt").
If the author sees the 0x44 question text hidden behind the engine window at x2/x3 as unacceptable before f3, that is a product call (keep sending the text to the engine window while a choice waits, or land f3 first).
