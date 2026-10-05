# E19.f2b1 - counter-check of the "dll-box" discovery (adversarial, read-only)

Repository `chantier/e19-suite`, HEAD `5812c63` (no `Alundra/` change since `897504b` other than the other agent's test files). Nothing built, run,
exported, edited, staged or committed in the repository; the engine submodule is at `ebeb81c9`. Everything below was re-derived from the sources and
from `ALUN_CD.EXE` (France) with **my own** tools in this folder (the earlier `dd.py`/`slide_clip.py` outputs were not used as evidence):

| File | What it does |
|---|---|
| `bd.py` | minimal capstone MIPS disassembler of the PS-X EXE (file offset = addr - 0x80020000 + 0x800) |
| `xr.py` | `jal` callers and lui/addiu/mem xref scan of the whole code area |
| `slide.py` | simulation of `MsgBoxRender` clip + `UpdateUiBoxesPosition` (constants read from the binary, not typed) |
| `adv.py` | font3.fnt `xadvance` against the binary glyph table `0x800993C4` (field 0, 20 bytes/glyph) |
| `scan44.py`, `scan44b.py`, `scan44c.py` | corpus probes: how a `0x44` (wait dialog choice) is reached, from the 483 exported `*.events.json` |
| `corpus_adv.py` | centred rows of the exported Yarn corpus: advances present? two `[center]` on a row? |

Tags: **[code]** read in the repository, **[bin]** read in the binary, **[calc]** computed by one of my scripts, **[hyp]** not proven.

## 1. Verdicts, claim by claim

### 1.1 The box at HEAD and what R1 must add

| # | Claim of the report | Verdict | Evidence |
|---|---|---|---|
| V1 | State per pass: `Y :139` (written `:246/:266/:350/:397/:402`), slide fields `:97-100`, `_flags :120`, `_bands :122`, `_bufferShift :113` (private), `_lineIndex :112`, `ScrollPixels` reset `:337` / set `:495`, cursor counter `:119`, `CursorImage :149`, typed prefix `:92-94`, centre token ignored `:606-607`, pages on the director `:140-143` | **CONFIRMED** | every line re-read in `AlundraDialogueBox.cs` and `AlundraDialogueDirector.cs`; nothing in `Alundra/` moved since `897504b` |
| V2 | `CursorImage` already matches the binary (`AnimateCursor` in the same pass, never in the scroll branch) | **CONFIRMED** | [bin] `RenderText 0x800455B4`: when `0x80149CD0` (cursorShown) is set, `0x80149CD4 += 1` (`0x80045820`), wraps at 40 (`0x80045828-0x80045834`), image = counter/10 (`0x66666667` magic, `0x80045840-0x80045878`); `ScrollText` never touches the counter; `InitializeDialogMessage` zeroes both (`0x800454F0-0x800454F8`). [code] `Pass :343-376`: `AnimateCursor` after slide, typed and interpreter, not after `Scroll`. Pass that shows the cursor first: counter 0->1, image 0 in both |
| V3 | R1 must be written inside `Pass` because the previous Y (overwritten by `AdvanceSlide`) and the old top band (cleared at `:496-501`) are gone afterwards | **CONFIRMED for the previous Y; the second reason depends on V7** | [code] `AdvanceSlide :388-407`, `Scroll :494-501` |
| V4 | `ClipTop = min(_cfgY+4, 239)`, `ClipHeight = min(50, 240-ClipTop)`, x 32, width 258 | **CONFIRMED** | [bin] `0x80046F10-0x80046FC4`: `top = entry.yoff(5) + cfg.Y - 1`, capped 239; `h = entry.hh(6)*8 + 2 = 50`, if `top + h >= 240` then `240 - top`; x = entry.xoff(16) + cfg.X(16) = 32; w = entry.w(32)*8 + 2 = 258. Constants read by `slide.py` from `0x800A731C` (entry 0) and `0x8009CFBC` (cfg X 16, Y 168, W 36, H 7). `cfg.Y` is read at `0x80046F20`, **before** `UpdateUiBoxesPosition` (`0x80046FF4`) |
| V5 | `_cfgY`: starts 168, written by `AdvanceSlide`, release and `Reset` set 168, `Open` leaves it | **CONFIRMED** (one caveat, C-3) | [bin] `InitializeDialogMessage 0x800450F0-0x80045590` has **no store** to `0x8009CFBC/BE`: it only reads cfg to fill the slide state (start Y = 240 `0x80045240`, target Y = cfg.Y `0x80045274-0x800452A4`, origin = cfg X/Y `0x800452C0-0x800452D8`). Global scan of stores to cfg X/Y: only the slide-out restore `0x80047040/0x80047050` (from the origin `0x18/0x1A` of the slide state) and the same restore in another box's callback (`0x80050F50/0x80050F5C`). `ProcessCloseAdvance 0x80045E60` builds the slide-out state (start = cfg.Y, target 240, `0x80045F2C-0x80045FCC`) and does **not** call `UpdateUiBoxesPosition`: the trigger pass draws at cfg.Y = 168 |
| V6 | C3 (first slide-in pass clips from 168, not 240) and the slide values | **CONFIRMED** | `slide.py` (independent simulation): slide-in clips `(172,50) (239,1) (239,1) (235,5) (230,10) (225,15) (220,20) (216,24) (211,29) (206,34) (201,39) (196,44) (192,48) (187,50) (182,50) (177,50) (172,50) (172,50)` for 18 passes, cfg.Y after: `240 236 231 226 221 216 212 207 202 197 192 188 183 178 173 168 168 168`; with a start at 240 pass 1 is `(239,1)`; slide-out clips `(172,50) (172,50) (176,50) (181,50) (186,50) (191,49) (196,44) (200,40) (205,35) (210,30) (215,25) (220,20) (224,16) (229,11) (234,6) (239,1) (239,1)` then the release pass. All identical to the report's table; Y sequence equals `AlundraTextBoxOracleTests.cs:92` |
| V7 | On the scroll-ending pass the **old top row is still drawn at offset 16**, so R1 must copy the rows before the shift; "visual difference = exactly one pixel row at y = 172 (descenders), one pass" | **Draw order CONFIRMED; visibility of that row UNCONFIRMED and probably REFUTED (C-1)** | [bin] `ScrollText 0x80045988`: positions computed first with the old shift and `offset = (8-left)*16/8` (`0x80045AA0-0x80045CF8`), y = cfg.Y + 5 + 16 r - offset; at left == 0: `shift+1`, band width cleared, then `ClearImage(960, 288 + 16*((shift'+2)%3), 64, 16)` at `0x80045E30`, i.e. the band of the **old top row**. The ClearImage and the draw: see C-1 |
| V8 | On the release pass the frame is not drawn either | **CONFIRMED** | [bin] `MsgBoxRender 0x80047038-0x80047054`: restore cfg, `DialogClosed`, return 0, no `RenderText`. `DialogClosed 0x8004501C` -> `0x80047CB0` (`lw v0,(a0); sh zero,(v0)` = first halfword of the callback entry) and zeroes `g_dialog_flags 0x80152F08`, clears `0x18` of `0x800DC4B8`. The dispatcher `0x80048054` runs the 13 entries (stride 0x1C from `0x80153028`, test of bit 0 at `0x8004813C`) and the frame function `0x8002BE5C/0x8002BE64` then calls the cell chain `0x80044C5C -> 0x800481F8` (called at `0x80044CA0`), which skips entries whose bit 0 is clear (`0x80048224-0x80048230`, 13 iterations) |
| V9 | `\H`: width stored per band at the step of the code; `RowX = w == 0 ? 32 : 16 + (288-w)/2`; band of row r = `(shift + r) % 3`; pre-shift width on the scroll-ending pass; cleared at open and for the new bottom band at the end of a scroll; `RestOfLineWidth` rule | **CONFIRMED** | [bin] jump table `0x80023FA8` (decoded by me): `H` -> `0x800469F0`: cursor+1, `CalcTextWidth(0x80149268 + cursor)`, stored at `lineWidth 0x80149BE8 + 4*((shift+lineIndex)%3)`, then back to the loop (free code). Row x: `RenderText 0x80045640-0x800456B8` and `ScrollText 0x80045B14-0x80045C70`: `w != 0` -> `cfg.X + (cfg.W*8 - w)/2` (signed /2), else `cfg.X + entry.xoff`. Clear at open: 3-word loop `0x80045380`; at scroll end: `lineWidth[(shift'+lineIndex)%3] = 0` (`0x80045DD8-0x80045DEC`). `CalcTextWidth 0x8004771C` jump table `0x80024098` decoded: `A`,`N` stop (`0x800478B4`); digits skipped; `B-G`,`T`,`Y` skip the code; `X` skips two; `W` reads a glyph; `: ; < = > ? @ H I J K L M O P Q R S U V` fall back to the code char read as a glyph |
| V10 | Q1: the DLL holds no advance table; `BitmapFontDescriptor` parses only face/page; the tests already read `xadvance` from the real `.fnt` | **CONFIRMED** | [code] `BitmapFontDescriptor.cs:23-61`, `AlundraFont3GlyphTests.cs:73,95-101,399-418`. [calc] `adv.py`: 144 of 145 `char` lines of `font3.fnt` have `xadvance` equal to the binary glyph table at the same index (field 0); the 145th, id 339, is outside the raw table (the E15.e remap) |
| V11 | [hyp] a glyph id with no `.fnt` entry has no advance; the corpus found none; [hyp] a second `\H` on a row not in the corpus | **both upgraded to fact for the exported corpus** | [calc] `corpus_adv.py` over the 485 `.yarn`: 521 lines, 823 centred rows, **0** characters or glyph ids without a `font3.fnt` entry, **0** rows with two `[center]`, widths 27..254 (none > 255) |

### 1.2 Wiring today, R4/R5

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| V12 | Build/retry/dispose sites: `InstallDialogueSystems :1198-1220`, `TryWireDialoguePresenterOnce :1236-1254` (guard `:295`), retry list `:2086-2090`, `OnEndPlay :2764-2790`, save-screen pattern `:1399-1419`, `:1424`, `:2776`, fields `:366-370` | **CONFIRMED** | re-read |
| V13 | Director -> presenter calls (`Open :249/:265`, `Pass :438`, `Released :480`, `NotifyPresenterClosed :505`, `InstallForMapEntry :236`, `OpenChoice :522`, `CloseStandaloneChoice :579`); `HasPresenter` testers (`0x39 :937`, `0x44 :961`, `OpenDialog :1873`, save book `:235/:272`, save screen `:984`) | **CONFIRMED** | re-read; `HasPresenter` is `_presenter != null` (`:153`) |
| V14 | The engine screen is pushed at `Open` today (the director sends `""`) | **CONFIRMED** | `Box.Open` sets `_typedChanged` (`:223`); `Open :265 ShowTypedText` -> `ShowLine("")`; `AlundraDialoguePresenter.ShowLine :123-134` pushes on Closed -> Open; K6 pins `(0, "")` (`OrderTests :379`) |
| V15 | Presenter tick must go right after `Pass :2113`, before the gate `:2115` and `continue :2119`; the pad loop `:2041-2082` runs all ticks first | **CONFIRMED** | re-read the two loops. The presenter is public while the box is internal: `AlundraDialogueDirector` is `public sealed`, `Box` is `internal` (`:166`) |
| V16 | R5 removal list; `TypedTextForTests :633` has no reader, `TakeTypedChanged` one | **CONFIRMED** | `grep` of the whole repo (excluding bin/obj) |
| V17 | R5 consequence (a): the engine window stays up and **empty** after an answer until the box is released; needs an explicit `_presenter.Close()` in `TakeChoiceResult` (script tick) | **CONFIRMED** | [code] engine `DialogueService.SelectChoice` -> State Open, choices cleared; `DialogueScreen.RefreshChoices` collapses `pnlChoices` when `!HasChoices` (`DialogueScreen.cs:384-388`), `RefreshLine` shows the empty `CurrentLine`, `MinWindowHeight = 150` (`:136`); `RemoveScreen` only from `Close`; the director's own comment on `CloseStandaloneChoice` (`:559-568`) says the same. `Remove` does not raise `WindowClosed` (`MGWindow.cs:1025-1026`, inside the `TryCloseWindow` path; `IsCloseButtonVisible="False"` in `DialogueScreen.xaml`). No test pins the old behaviour (`.IsOpen`/`.State` readers: only `AlundraSaveScreenDirectorTests.cs:566/:635`, lone choices) |
| V18 | `ScreenStack` ignores `UILayer`; modal-update rule by push order | **CONFIRMED for the update rule; the z-order/input mechanism is incomplete (C-2)** | `ScreenStack.cs:34-48, :58-72, :137-148`; `UILayer` only in comments |
| V19 | Push-order risk is latent: sailor 12 has a `0x36` between `0x0D` and `0x44` | **CONFIRMED, and now backed by a census** | [code] `plan-e12-dialogues.md:72,:145`; `AlundraEventProgramRunner.cs:374-420, :1889-1890` (one call runs instructions until one returns 0). [calc] `scan44b.py` (recursive descent over `0x02/0x03/0x04/0x30/0x31`, 483 maps): 51 `0x44` sites in 24 maps, 0 reached through "dialog-open then only non-yielding opcodes"; `scan44c.py` (linear sweep, 101 sites in 39 maps): 0 again; immediate predecessor of `0x44` is `0x06` (693), `0x50` (210), `0x36` (164). Partial by construction (the plan counts 117 sites in 52 maps); no counter-example |
| V20 | Coupling: `AttachToWorld` builds the runner only with a non-null presenter (`:183-189`, `:204-206`) | **CONFIRMED** | re-read |
| V21 | Smaller risks: `TryWireSaveScreenOnce` has no try/catch; wiring fixtures have no `UIFonts` | **CONFIRMED** | `:1399-1419`; the fixtures build the game with `GetUninitializedObject` (`WiringTests :145-160`, `:207-221`), so `UIFonts` and `AssetContentManager` are null |
| V22 | Option A (push at `Open`) needs a hook; a polling presenter pushes one tick late | **CONFIRMED** | `AlundraSaveScreenPresenter.Tick :45-65` polls `director.IsActive`; the proxy only runs the presenter after a `Pass`, and `Open` runs after the pass of its tick |

### 1.3 Test census

| # | Claim | Verdict |
|---|---|---|
| V23 | Red under R1-R5: `OrderTests :360` (K5), `:366-380` (K6), `FramePassTests :246`, `WiringTests :179` and `:257` | **CONFIRMED** (all five re-read). Add: the **name** of `WiringTests :139` is `...WiresAPresenterThatPushesOnOpen`: it contradicts R5 and must be renamed with the assertion |
| V24 | Premise change only: `OutOfBandCloseTests :48-69` (no push asserted; calls private `RequestClose` by reflection), montage `PrefixRecordingPresenter` (`Shown` read at `:360/:379` only, `Closes` no reader, must stay choice-capable for K5) | **CONFIRMED** |
| V25 | Unaffected list (direct presenter tests, `CurrentLineForTests` readers, bare `DialogueService` attachers, capture presenter, oracle comparison, arcs, `IntroTraceHarnessTests :594-600`) | **CONFIRMED** by the greps for `.Pushed/.Removed`, `.CurrentLine`, `DialogueRuntimeState.`, `.Shown/.Closes`; the Yarn-bindings tests use private fake presenters and never touch the director |

### 1.4 Choices

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| V26 | Callers of choices (`0x44 :950-989`, save book `:285/:295`, save screen `:1000/:1021/:1032`) | **CONFIRMED** | re-read |
| V27 | Engine window geometry: width `min(720, max(320, w-80))`, height >= 150, bottom at `bounds.Bottom - 48`, modal on `UILayer.Modal`; project markup `Padding=14`, `IsTopmost`, `lblLine FontSize=16` | **CONFIRMED**, small slips (C-4) | `DialogueScreen.cs:130-138, :148-152, :336-352`; `DialogueScreen.xaml` |
| V28 | Overlap with the faithful frame at x2 and x3 | **CONFIRMED**; at x1 the window (42..192) also covers the top 3 px of row 1 (189..205), not only row 0 | [calc] |

## 2. Corrections

- **C-1 (medium, changes the premise of D-E19-81's "one-frame descender row at y = 172" and the R1 pre-shift copy).** The report (and the f2b discovery
  behind it) infers that the old top line's bottom pixel row shows at y = 172 for the pass that ends a scroll because `ScrollText` positions the old rows
  at offset 16 *before* shifting. That is only true if the VRAM band still holds the glyphs when the GPU rasterises that frame. The same pass issues
  `ClearImage` on exactly that band (`0x80045E30`, rect `(960, 288 + 16*band, 64, 16)`; band = old shift % 3 = the old top row, checked by recomputing
  `(shift'+2)%3`). [bin] `0x80084EFC` is libgpu `ClearImage` (its rect check `0x80084DD4` is given the string `ClearImage` at `0x8002A180`) and queues its
  work with `(*vt[8])(vt[0xC], rect, 8, rgb)`, `vt = *0x800C812C`; `LoadImage 0x80084F90` and `DrawOTag 0x800852CC` queue with the **same** slot
  (`(*vt[8])(vt[0x20] ...)`, `(*vt[8])(vt[0x18], ot, 0, 0)`), so the GPU executes them in call order. The UI OT is built by the dispatcher
  (`0x8002BE5C`) then `0x80044C5C` (head stored at `0x800DC098`, `0x8002BE70`) and rasterised at the end of the frame by `DrawOTag(0x800DC098)`
  (`0x8002BA9C`, function `0x8002BA4C`). The `ClearImage` is therefore queued **before** that frame's `DrawOTag`: the old top band is blank when the
  sprite is rasterised. This is also the reading the report already relies on for typing (the glyph typed in a pass is visible in that pass because its
  `LoadImage` is queued before the `DrawOTag`): applying the same ordering to `ClearImage` gives a blank old row 0 on the scroll-ending pass. Consequence
  if confirmed: the picture of the scroll-ending pass equals the next pass (post-shift rows at offset 0, new bottom row empty); R1 does not need the
  pre-shift copy or `RowOffset = 16`; the `refcompose.py` reference and the pixel test must not contain a row at y = 172 for that pass; D-E19-81's third
  detail is moot. **[hyp] not run on an emulator**: the claim rests on the standard Psy-Q queue order seen in the binary. Cheap decisions that do not need
  the answer: keep the clip lag and the 255-px cut (not affected), and implement R1 so that the scroll-ending row set is one line (`RowOffset 0` +
  post-shift rows) with the pre-shift variant behind a constant until a frame capture (DuckStation GPU dump of a scroll end) settles it.
- **C-2 (low, conclusion unchanged).** "Z-order and modal-update follow push order" is true of `ScreenStack.Update`, but drawing and input are ordered by
  `MGDesktop`: `Windows.OrderBy(IsTopmost)` (`MGDesktop.cs:1725`, draw) and `Windows.Reverse().OrderByDescending(IsTopmost)` (`:1588`, input), ties by add
  order. Every project screen except the HUD declares `IsTopmost="True"` (`DialogueScreen.xaml`, `SaveScreen.xaml`, `InventoryScreen.xaml`,
  `SubInventoryScreen.xaml`), so push order decides **among them**, and R3's `TextBoxScreen.xaml` would inherit that by copying the save-screen pattern.
  It is also a lever: a non-topmost text-box window would always sit under the topmost choice window whatever the push order (at the price of HUD
  ordering, which should then be checked).
- **C-3 (low).** The message box shares its cfg `0x8009CFBC` with six other callbacks of the initial table `0x800A731C` (entries 2, 5, 7, 8, 9, 11:
  callbacks `0x8004F628`, `0x8004A8A8`, none, `0x8004AFE8`, `0x80050EC8`, `0x80051550`); each restores cfg X/Y from its own origin at the end of its
  slide-out (e.g. `0x80050F48-0x80050F5C`). "cfg.Y = 168 between boxes" holds as long as every user ends cleanly; an interrupted inventory sub-box would
  leave another value. Not worth modelling (inventory and dialogue are exclusive, J9), worth one line in the risks.
- **C-4 (P4 slips).** `AssertFollowsTheDesktop` is at `AlundraScreensFollowTheWindowTests.cs:88-105` (not `:74-100`); `DialogueScreen.xaml` has
  `pnlContent Spacing=8` and `pnlChoices Spacing=4` (the report's section 2.3 attributes 8 to `pnlChoices`); at x1 the choice window also covers 3 px of row 1.

## 3. Missed, or worth adding to the plan

1. The C-1 simplification (above) is the only finding that can change R1's shape. Everything else in R1 holds as written.
2. The 0x44 census (V19) turns the push-order risk from "not scanned" into "no direct case in 483 maps by two probes"; Option A can stay a documented
   fallback rather than a task. A test of the push order after `Open` + `OpenChoice` in one tick is still cheap and pins the engine side.
3. The corpus facts of V11 (no missing advance, no double `[center]`, widths 27..254) can be pinned as a converter/data test next to S025.
4. `WiringTests :139` needs a rename, not only a re-pointed assertion (V23).
5. [hyp] with a catch-up frame (several passes per frame) the presenter ticks after each pass but MGUI draws once: the report already lists it; note that
   it also means the first slide-in pass clip (172,50) is seen only when that pass is the last of its frame.

## 4. Open questions for the author

None is a product decision. One technical check is worth the author's time because D-E19-81 recorded a detail on a premise I could not confirm: whether
the old top row's last pixel row really appears for one frame at the end of a scroll (C-1). Everything else stands.
