# E19.f3 discovery, surface "DLL, engine and corpus side of choices at HEAD" (read-only)

Parent repo `chantier/e19-suite` at `9956d56` when I started (HEAD moved to `4d89f81` while I worked: the G2d agent commits `Alundra/Scripts/AlundraBackdropStage.cs` and friends, none of the files cited here; the working tree was clean at the start; other agents commit slices G2d and f2b1a/b/c in this
checkout, so line numbers may drift by a few lines if a later commit touches the files below). Engine submodule `CasaEngineMonogame` at
`ebeb81c9` (its working tree only holds the author's `CasaEngine.Launcher/Program.cs`, untouched). Nothing built, run, exported, edited, staged or
committed in the repository. Everything I ran lives in this folder: `census44.py`, `census44b.py`, `ctrlmode.py`, `calib.py`,
`zero_entry_check.py`, `peek.py`; results `sites44.json`, `sites44_detail.json` / `.tsv` (one row per 0x44 site), `sites44_linear.json`,
`sites44_ctrl.json`.

Tags: **[code]** read in the repository (file:line); **[decomp]** read in `alundra-datas-analyser` (decompilation, read never corrected);
**[calc]** computed by one of my scripts over `alundra-project/`; **[data]** read in `data-extracted/` or `alundra-project/`;
**[hyp]** not proven. I did not read the binary (another surface owns it); every binary fact below comes from the plan or the decompilation and
is tagged as such.

## Summary

1. **One opener, three DLL callers, one poll shape.** `AlundraDialogueDirector.OpenChoice(IReadOnlyList<string>)` (`:509-523`) is the DLL's single
   opener, as `0x80050BA8` is the binary's: callers are opcode `0x44` (`AlundraEventProgramRunner.cs:977`), the save book (`AlundraSaveBook.cs:285`)
   and the save screen's question (`AlundraSaveScreenDirector.cs:1000`). Result = `TakeChoiceResult()` (`:532-544`) polled by the script/state
   machine (`:981`, `AlundraSaveBook.cs:295`, `AlundraSaveScreenDirector.cs:1021`). The decompilation has the same three callers plus two more
   (MemoryCardManager.cs:895 ETC 0x81/0x82, and :584 a debug literal), see 1.9.
2. **Labels are fixed ETC strings, never Yarn options.** `0x44` has size 1, no operand (`EventOpcodeSizeTable`); labels = ETC 0x43/0x44
   (`AlundraEtcStringTable.cs:55-56`, `TryResolveYesNo` `:130-135`), nodes `Etc_0067`/`Etc_0068` of the compiled `Dialogues/Etc.dialogue`; the book uses ETC 0x41/0x42, the
   save screen 0x4A/0x4B, a third and fourth pair. **All four raw strings are `OUI`/`NON`** [data] (`ETC_RES.R.json` at the offsets of
   `EtcIndexTable.csv`; ETC 0x81/0x82 too): three ETC index pairs in the DLL, one text pair. The Yarn corpus has **zero** options (`->`, 485 `.yarn`
   files, `rg --no-ignore`), and the engine runner's `OnOptions` is empty (`YarnDialogueRunner.cs:169-171`).
3. **Input today is MGUI, not the pad.** The engine `DialogueScreen` builds one `MGButton` per label (`DialogueScreen.cs:370-405`); a click or the MGUI
   focus navigation + Submit calls `presenter.SelectChoice(i)` (`:394`, engine doc `dialogue-choices-and-bitmap-fonts.md:60-72`) which raises
   `ChoiceSelected` -> `OnPresenterChoiceSelected` (`AlundraDialogueDirector.cs:525-529`: `Result = index == 0 ? 1 : 0`). Nothing in the choice path
   reads `TickPad`; the director's own `Tick()` seam reads only Square (`:441-449`).
4. **Corpus: 101 `0x44` sites in 39 maps** [calc] (55 in B programs, 46 in C programs; 83 distinct programs; 86 distinct (map, text) questions). The
   plan figure "117 sites in 52 maps" (`plan-e12-dialogues.md:109`) is an artefact: it decodes the **six table words at the start of `Codes`** as code
   (entry 0 means "no program"; the tables themselves occupy `[0, 224)` in e.g. map 26). With zero entries included my script reproduces exactly
   `0x44` 117/52, `0x5C` 784/115, `0x50` 207/100; without them 101/39, 765/106, 179/80. Two independent decodes (recursive descent, linear sweep from
   every entry) give the same 101 sites.
5. **Every site has the same shape**: `0x0D` (56) or `0x5C` (45) with **control mode 1 (MessageBox) on all 101**, then `0x50 4` (close mask = script
   only), `0x36` (wait for the flag the text sets at its end), usually `0x06`, then `0x44`, then **always `0x51`** (the script close), then `0x39` (73
   sites) or an immediate `0x03`/`0x04` on Result (28). So a choice is always asked **over a text box that is open, typed to its end and held by
   mask 4**; the box closes only with the `0x51` after the answer. The save book has the same shape; the save screen's question is the only lone one.
6. **Tests**: 24 `SelectChoiceForTests` call sites in 8 files at HEAD (the plan says 23), 6 `ChoicesForTests` readers, 4 direct `OpenChoice` calls, 9
   tests behind the shared helper `AnswerAndClose` and 9 behind `RunToTheQuestion`. Only **A17** (`AlundraDay3SceneArcTests.cs:212-229`) and the sailor-12
   test (`AlundraDialogueOpcodesProductionTests.cs:130-146`) answer a `0x44` through a loop; no other arc mentions a choice. The answer is delivered
   **synchronously between ticks** and many tests pin same-tick or 1-tick timings; a pad-driven box moves all of them (section 3).
7. **View**: the choice frame is already baked: sprite `g_uiBoxesConfigurationBackgroundMessageChoice`, id `829f31a7-39d1-5fc5-b436-befd59b50985`
   (128 x 32, screen position (176, 144), 16 x 4 cells of 8 px). A choice screen needs no new MGUI capability beyond what the f2b1 probe proved for
   the text box; the open items are the binary's unknowns (selection cursor, label positions, any tint ramp) and the gaps already listed in
   `view-notes.md` (G1, G3, G4, G5, O-E19-64, O-E19-66). Section 4.

## 1. The current choice path

### 1.1 Opcode 0x44 **[code]**
`AlundraEventProgramRunner.cs:950-989`, `case 0x44`:
- no `DialogueDirector` or `!HasPresenter` (`:961`) -> `state.Result = 1`, once-logged degraded, `return 1` (`:963-965`) (T5 degraded mode, pinned by
  `AlundraDialogueOpcodeDispatchTests.cs:210-224`);
- first entry = `!choiceDirector.IsAwaitingChoice` (`:968`): `AlundraEtcStringTable.TryResolveYesNo(EngineEnvironment.ProjectPath, ...)` (`:970`); labels
  missing -> `Result = 1`, return 1 (`:972-975`); else `OpenChoice(new[] { yes, no })` (`:977`), `return 0` (suspend, the instruction repeats);
- afterwards `TakeChoiceResult()` (`:981`): `null` -> `return 0`; a value -> `state.Result = value`, `return 1` (`:987-988`).
- **The "already asked" memo is the director's `IsAwaitingChoice`**, not the binary's `Parameters[1] == CodeIndex` + `g_scriptDialogChoiceResult` pair
  [decomp] (`EntityEventHandlers.cs:1376-1407`, `Script_68_044 @ 8003E88C`: first entry stores `Parameters[1] = CodeIndex` only if
  `InitializeAsyncOperation` returned non-zero, i.e. an async operation is not already running; result 0 = pending, 1 = first, else second).
  Consequence **[hyp]**: if another caller (book, save screen) has a choice up, a `0x44` reaching its first entry in the DLL would take that choice as
  its own; the binary would retry. The corpus makes this unlikely (a `0x44` always follows an open box, `OpenDialog` retries while a box is open).
- Result register: `EventProgramState.Result` of the calling slot's state (never cleared by `InitializeEventData`, `:159-160`); consumed by the next
  `0x03`/`0x04` (If true/false goto, `:569-573`), possibly after `0x51` and `0x39` (neither writes Result).

### 1.2 The director **[code]** (`Alundra/Scripts/AlundraDialogueDirector.cs`)
- Interface seam `IAlundraDialogueDirector` (`:23-91`): `HasPresenter` (`:29`), `IsOpen` (`:34`), `IsAwaitingChoice` (`:37`), `OpenChoice` (`:83`),
  `TakeChoiceResult` (`:90`); the only implementer is the director (grep over `Alundra`, `Alundra.Tests`).
- State: `_awaitingChoice`, `_pendingChoiceResult`, `_choiceHandler` (`:149-151`); `IsAwaitingChoice` `:155`.
- `OpenChoice` (`:509-523`): sets `_awaitingChoice = true`, clears the result, returns when `_presenter == null`; otherwise subscribes
  `OnPresenterChoiceSelected` (guarded double subscription `:519-521`) and calls `_presenter.ShowChoices(labels)` (`:522`).
- `OnPresenterChoiceSelected` (`:525-529`): `_pendingChoiceResult = e.SelectedIndex == 0 ? 1 : 0` (the binary's 1/2 collapsed to 1/0).
- `TakeChoiceResult` (`:532-544`): returns the result once, clears `_awaitingChoice`, unsubscribes. It does **not** close the engine window.
- `CancelChoice` (`:552-557`, used by the book `AlundraSaveBook.cs:411`) and `CloseStandaloneChoice` (`:569-581`, used by the save screen `:359`, `:1032`;
  refuses while a box is open, closes the presenter, never touches `PlayerControlFlags`).
- `Open` clears the choice state (`:246-247`); `InstallForMapEntry` too (`:224`, `:231-232`); `ResetForTests` `:610-611`.
- `ShowTypedText` (`:394-405`) **returns while a choice waits** (`if (_awaitingChoice) return;`): the engine service drops its choices when shown a
  line; the typed text is sent after the answer. This guard disappears with f2b1c R5 (`plan` section E19.f2b1c).
- Test seams: `ChoicesForTests` (`:637`), `SelectChoiceForTests(int)` (`:641`) = `_presenter?.SelectChoice(index) ?? false`.
- `Pass(squareHeld, squarePressed)` (`:434-439`) and `Tick()` (`:441-449`) know only Square. World proxy: `_squareOfTick` records Square per tick
  (`AlundraWorldProxy.cs:2042-2045`), `Pass` is called per tick at `:2113`.

### 1.3 The save book **[code]** (`AlundraSaveBook.cs`)
Constants `:85-96`: question ETC 0x40 node `Etc_0064`, labels ETC 0x41/0x42, close mask 4. `RunMessage` (`:235-257`): `HasPresenter` (`:235`), abandons if
`dialogue.IsOpen || dialogue.IsAwaitingChoice` (`:241`), `dialogue.Open(asset, "Etc_0064", 1)`, `SetCloseMask(4)`, 61-tick wait. `RunQuestion`
(`:266-287`): `HasPresenter` (`:272`), labels `TryResolveText(0x41/0x42)` (`:278-279`), **`dialogue.OpenChoice(new[] { yes, no })` `:285`**.
`RunAnswer` (`:295`): `TakeChoiceResult()`; `null` and not awaiting = abandon; else `CloseOwnedBox`, `answer != 1` = NON abandon, OUI = 61-tick wait then capture.
Same shape as the 0x44 sites: question box (mask 4) open, then the choice over it.

### 1.4 The save screen's question **[code]** (`AlundraSaveScreenDirector.cs`)
Constants `:93-94` ETC 0x4A/0x4B, `MaxLineLength = 0x40` `:102`. `RunPickerInput` (`:949-...`): Cross (`ButtonsJustPressedByInterval`) calls `AskQuestion`
(`:976-1003`): `HasPresenter` (`:984`), labels `ResolveEtc` + `Bound` (`:991-1000`), **`OpenChoice` `:1000`**, `_questionOpened = true`. `PollAnswer` (`:1007-1036`):
`TakeChoiceResult()` (`:1021`), then `CloseStandaloneChoice()` (`:1032`), `_answer = result == 1 ? 1 : 0`. While the question waits the screen's own keys are ignored
(`AlundraSaveScreenDirectorTests.cs:557-561`, `RunPickerInput` is not called while `PickerQuestion` is set, `:929-941`). **The only choice with no text box
under it.** The class comment says "the dialogue screen shows it over this one and owns its keys" (`:971-973`).

### 1.5 The engine chain **[code]**
- `IDialoguePresenter` (`CasaEngine/Framework/Dialogue/Presentation/IDialoguePresenter.cs`): `Choices`, `HasChoices`, `ChoiceSelected`, `ShowChoices(labels)`,
  `SelectChoice(index)`, `Close()`.
- `DialogueService` (`Runtime/DialogueService.cs`): `ShowChoices` (`:56-74`) copies the labels, state `AwaitingChoice`, raises `StateChanged` and
  `PresentationChanged`; `SelectChoice` (`:76-98`) returns false outside `AwaitingChoice`, throws on a bad index, clears the choices, state back to
  **`Open`** (not `Closed`), raises `PresentationChanged` then `ChoiceSelected`; `ShowLine` (`:38-54`) drops pending choices; `Close` (`:100-110`).
- `DialogueScreen` (`UI/DialogueScreen.cs`, `XamlUIScreenBase`, `Layer => Modal`, `IsModal` `:130-131`): `RefreshChoices` (`:370-405`) rebuilds one `MGButton` per
  label (`new MGButton(Window, _ => _presenter.SelectChoice(choiceIndex))` `:394`, text in the font family `:398-399`); `pnlChoices` collapses without choices
  (`:384-388`); window width `min(720, max(320, w - 80))` bottom-anchored, >= 150 px (`:136-152`, `:336-352`). Replacement markup:
  `alundra-project/UI/Screens/DialogueScreen.xaml` (Padding 14, `rgba(12,18,26,235)`, `lblLine` 16, `pnlChoices` Spacing 4, `IsCloseButtonVisible=False`).
- Alundra's wrapper `AlundraDialoguePresenter` (`Alundra/Scripts/AlundraDialoguePresenter.cs`): owns a `DialogueService` and the `DialogueScreen` (`:43-45`, `:85-87`);
  `ShowChoices` pushes the screen on Closed -> Open (`:136-147`), `Close` removes it (`:153-158`), `RequestClose` = the window's own close control -> `NotifyPresenterClosed` (`:190`).
  Wiring: `AlundraWorldProxy.InstallDialogueSystems` (`:1198-1220`), `TryWireDialoguePresenterOnce` (`:1236-1252`, called `:2086`), disposed at `OnEndPlay` (`:2784`).
- `AlundraDialogueCapturePresenter` (the Yarn runner's presenter) forwards `ShowChoices`/`SelectChoice` to the world presenter (`:88-90`) but the runner never produces choices
  (`OnOptions` empty, corpus has no option): dead path.
- Today the engine window is pushed at the box's `Open` (`ShowTypedText` at `:265`), so a `0x44` finds the window open and the choice appears in the same window as the
  question text (the `lblLine` stays under the buttons, `dll-notes.md` f2b1 section 4). After f2b1c R5 it is pushed by `ShowChoices` only.
- Engine tests are untouched by f3 unless Alundra stops using the API (it stays in the engine; `CasaEngine.Tests/Dialogue/DialogueServiceChoiceTests.cs`).

### 1.6 Where the labels come from **[code]/[data]**
- `AlundraEtcStringTable.TryResolveYesNo` (`:130-135`) -> `TryGetEtcText(0x43)`, `(0x44)` (`:55-56`) -> node `Etc_{index:D4}` line `line:Etc_0067_p0` of the compiled
  `dialogue_etc` asset (`:162-196`, line id `:178`), cached `font3` text (`AlundraDialogueCapturePresenter.ToFont3Text`). Book and screen use `TryResolveText` (`:128`) / `ResolveEtc`.
- Exported: `alundra-project/Dialogues/Etc.yarn` lines 241-299: `Etc_0064` "Enregistrer tes progrès?", `Etc_0065`/`0066` OUI/NON (ETC 0x41/0x42, book),
  `Etc_0067`/`0068` OUI/NON (0x43/0x44, opcode), `Etc_0074`/`0075` OUI/NON (0x4A/0x4B, save screen). Raw `ETC_RES.R.json` strings at offsets 3648, 3652, 3656, 3660,
  3947, 3951, 4060, 4064: `OUI`/`NON` (no control code, no trailing space). ETC 0x40 = "Enregistrer tes progrès?" (3622), 0x84 = "Enregistrer tes exploits ici?" (4114), 0x93/0x94 =
  "Impossible d'enregistrer l'histoire." / "Est-ce bien clair?".
- **Distinct label pairs: 1 text pair (OUI, NON); 3 ETC index pairs used by the DLL (0x41/42, 0x43/44, 0x4A/4B); a 4th ETC pair (0x81/0x82) exists in the decompilation only.**
- Yarn: `OnOptions` is empty (`YarnDialogueRunner.cs:169-171`); `rg --no-ignore -e '^\s*->' --glob '*.yarn'` over `alundra-project` finds nothing (485 files); the `0x44` opcode has no
  operand to choose labels or a count.

### 1.7 Input today **[code]**
- Choice window input = MGUI only: click, or `UIFocusNavigationService` (Up/Down/gamepad, Submit = Enter/Space/gamepad A) per the engine doc
  (`CasaEngineMonogame/docs/engine/dialogue-choices-and-bitmap-fonts.md:60-72`); the buttons call `SelectChoice`. **[hyp]** which keys the author's keyboard maps to Submit was not examined.
- The Alundra pad is not read by the choice: `AlundraDialogueDirector` reads only Square (`:117`, `:441-449`); the pad is built by `AlundraPlayerController.BuildPadState` (`:218-236`,
  action -> bit table `:76-103`: `MoveUp/Down` -> Up/Down, `Jump` -> **Cross 0x40**, `Attack` -> Square) into `GameState.LastPadState` by the player's own proxy each frame
  (`AlundraEntityScriptProxy.cs:1099-1110`), then `TickPad.Update` per logic tick (`AlundraWorldProxy.cs:2044`). Cross/Up/Down already reach the DLL during a **modal** screen:
  the save carousel reads Up/Down/Cross from `TickPad` while its modal screen is up (`AlundraSaveScreenDirector.cs:949-...`). The engine's modal rule only routes pointer/keyboard to the modal view
  (`InputRouter.cs:360-376`, `:450`); `view-notes.md` section 2 says the pad path was not read, and I did not read it either **[hyp]** (the save carousel and the Square close of the
  engine's modal box are the in-play evidence).
- `PlayerControlFlags`: the box of the 101 sites sets MessageBox (control mode 1) (`ApplyControlMode`, `AlundraDialogueDirector.cs:285-296`); the player is frozen, scripts keep ticking.

### 1.8 Tests' view of the engine window **[code]**
`AlundraSaveScreenDirectorTests.cs:555-566` (`ChoicesForTests`, `_presenter.ScreenForTests` pushed/removed, `_presenter.IsOpen`), `Scripts/AlundraSaveScreenPresenterTests.cs:137-160`
(dialogue screen pushed over the save screen, `dialogueScreen.Layer > _screen.Layer` Modal > Menu, then removed alone) and `AlundraDialoguePresenterWiringTests.cs:106-111`
(`ShowChoices` closed -> open pushes). `ScreenStack` does not use `UILayer` for order, push order decides (`docs/plan-e19-f2b1-annexe/dll-notes.md`, summary item 5), so the `Layer` assertion pins nothing real.

### 1.9 The binary's callers, from the decompilation **[decomp]** (not re-read in the binary)
`GameEngine.InitializeAsyncOperation` (`GameEngine.cs:2605-2615`, `0x80050BA8`) stores the two strings, calls `StartAsyncCallback` (`:2630-2637`: plays **sound 4**, `SetTransitionType(3)`,
fade overlay sprites), always returns 1. Its twin `StartAsyncOperation` (`0x80050C00`, `:2618-2627`) has no caller in the decompilation. Callers:
| call site | labels | result store |
|---|---|---|
| `EntityEventHandlers.cs:1401` (`Script_68_044`) | ETC 0x43/0x44 | `g_scriptDialogChoiceResult` |
| `FunctionTypeC.cs:6875` (save book, state 2) | ETC 0x41/0x42 | `g_warpStatusFlag` |
| `MemoryCardManager.cs:2336` (save carousel question) | ETC 0x4A/0x4B | `g_asyncOperationResult` |
| `MemoryCardManager.cs:895` (state 7 -> 0x6b, after ETC 0x93/0x94 "Impossible d'enregistrer l'histoire. / Est-ce bien clair?") | ETC 0x81/0x82 | `g_asyncOperationResult` |
| `MemoryCardManager.cs:584` (state 0x2710, debug "セーブのてすと") | Japanese literals | `g_asyncOperationResult` |
The DLL has no equivalent of the last two (`rg 0x81|0x82|0x93|0x94 AlundraSaveScreenDirector.cs` finds nothing; its failure path is `StateFailedMessage 0x15`, a message then Square).
Reachability of `MemoryCardManager.cs:895` was not examined **[hyp]** (a card-error branch, likely unreachable without a card; the plan's "three unidentified callers" may be these plus a binary-only one).
The labels are drawn by `UIManager.DisplayMessageChoice` (`UIManager.cs:~307-470`, `0x800501FC`): each label is measured (`CalculateTextWidthFromScript`), its sprite has `h = 0x10`, **`clut = 8`**,
rendered with `RenderTextBitmap(... 0x80, 0x10)` (128 x 16 each), the box animated by `UpdateUiBoxesPosition`, render function `FUN_800501a4`.

## 2. Corpus census of `0x44` **[calc]** (`census44.py`, `census44b.py`)

Method: 483 `Maps/**/*.events.json`; entries = the non-zero words of tables A..F (`EventCodesATable`...); recursive descent following `0x02`, `0x03/0x04`, `0x30/0x31`
(delta at +3), `0x74`, `0x78` (+ return point), `0x79/0x7A`, `0x7B/0x7C`, `0x57/0x58` (4 targets, no fall-through), ends at `0xFF`, `0x49`, `0x7D`, or an opcode of size 0;
sizes from `EventOpcodeSizeTable.cs`. Cross-check: a linear sweep from every entry to `0xFF` finds the same 101 sites (0 only in one, 0 only in the other).
**Zero entries are not programs**: the first words of `Codes` are the tables (`Header.EventCodesASize` 64 + B 6 + C 80 + D 8 + E 2 + F 64 = 224 = the first non-zero entry of map 26; map 474 `[0,0,0,0,...,64,...]`
words; `zero_entry_check.py`: decoding from offset 0 never reaches a clean `0xFF`). Including them gives the plan's 117/52 (`calib.py`).

Totals: **101 sites, 39 maps, 83 distinct programs, 86 distinct (map, text-node) questions, 28 sites in a program that holds another `0x44`; slot B 55, slot C 46.**
Per map (count; programs): 10 (1 C[2]), 11 (1 C[35], shared text `Shared_S126`), 21 (1), 26 (1), 68 (2), 134 (5), 135 (5), 136 (2), 143 (1), 144 (1), 147 (1), 152 (4), 163 (3), 165 (2),
172 (2), 179 (3), 192 (1), 196 (1), **198 (8)**, 221 (2), 229 (2), 240 (2), 258 (4), 263 (1), 270 (3), 290 (1), 292 (1), 295 (1), 298 (4), 303 (1), 331 (1), 389 (1, C[12] @1398, the sailor),
394 (2), **398 (9)**, 440 (1), 472 (5), 473 (5), **474 (8)**, 475 (2). Full rows (map, program, offset, open op and node, the opcodes between, the 4 after, the first line of the question): `sites44_detail.tsv`.

Shape (all 101): open op **`0x0D` x 56 or `0x5C` x 45**, **control mode 1 x 101** (`ctrlmode.py`); then `0x50` with parameter **4** (script-only close) **x 101** (`census44b.py`
"close mask before"), `0x36`, usually `0x06`, in the orders `50 36 06` (71), `36 06 50` (16), `50 36` (8), `36 50` (2), one `50 06 36`, one `50 36 06 06`, one `50 36 06 2D 09 36 06`, one
long chain after a `0x39 0x7D` (the opcodes between the last open and the `0x44`); **immediate predecessor of `0x44`: `0x06` 74, `0x50` 18, `0x36` 9; immediate successor `0x51` x 101**; after the `0x51`: `0x39` x 73 (then `0x03` 44, `0x11` 11, `0x04` 10,
`0x37` 4, `0x0D` 2, other 2), `0x03`/`0x04` x 28 (`0x03` 12, `0x04` 16; then `0x5C` 9, `0x00` 10, `0x05` 4, `0x0D` 4, `0x74` 1). So Result is tested by `0x03`/`0x04` immediately after the `0x51` (28) or after `0x51 0x39` (54).
Text: 100 local (`M{map}_S{nnn}`), 1 shared (`Shared_S126`, map 11 debug zone). Two sites (map 165, `M165_S007`) name a text **absent from the export** (an empty original slot, D-E15-10; programs C[13]/C[14] of a map
whose same slot is also disused: the question box would be empty and the program waits for a flag the text never sets) and map 163 B[2] opens `\#Disuse` (an unused text). Those are probably dead code; I did not prove it.

Consequences for f3 **[calc]/[hyp]**:
- the **push-order risk of f2b1 (a `0x44` before the box's first drawn pass) has no instance in the corpus**: the `0x36` between the open and the `0x44` waits for the flag the question text sets, i.e. the box has been
  drawn; same for the book (61-tick wait). A text box screen pushed at its first drawn pass is always under a choice screen pushed at `0x44`'s first entry.
- a **choice is never asked without an open text box in the corpus; the save screen's question is the only lone choice**: a choice view must work with and without the text box screen.
- after the answer the script closes the box with `0x51` (mask 4): the choice box and the text box have independent lifetimes (the choice ends at the answer, the text box slides out ~18 passes later, f2a).

Save book: one flow (`AlundraSaveBook`, entity program 72 of `ProgramCTick`, map-independent, `ETC 0x40` question). Save screen: one question per carousel entry confirmation (Cross, `AskQuestion`).

## 3. Test census (Alundra.Tests) and what a pad-driven faithful box would move **[code]**

### 3.1 Who answers / reads / opens choices
| seam | sites |
|---|---|
| `SelectChoiceForTests` (**24**, plan says 23) | `AlundraDay3SceneArcTests.cs:223` (A17 loop); `AlundraDialogueBoxOrderTests.cs:328` (K5); `AlundraDialogueOpcodeDispatchTests.cs:450`, `:472`; `AlundraDialogueOpcodesProductionTests.cs:139` (sailor 12, map 389); `AlundraSaveBookEndToEndTests.cs:167`, `:180`; `AlundraSaveBookTests.cs:155`, `:199`, `:350`, `:372`, `:392`, `:408`, `:422`, `:439`, `:463`; `AlundraSaveScreenDirectorTests.cs:121` (helper `AnswerAndClose`, 9 callers), `:523`, `:563`, `:587`, `:806`, `:813`, `:858`; `Scripts/AlundraSaveScreenPresenterTests.cs:153` |
| `ChoicesForTests` (6) | `BoxOrderTests.cs:352`, `OpcodeDispatchTests.cs:443`, `OpcodesProductionTests.cs:138`, `SaveBookTests.cs:151`, `SaveBookEndToEndTests.cs:166`, `SaveScreenDirectorTests.cs:555` (all `{"OUI","NON"}`) |
| direct `OpenChoice` (4) | `BoxOrderTests.cs:324`, `SaveScreenDirectorTests.cs:616`, `:629`, `SaveBookTests.cs:233` (labels `{"A","B"}`, with `CancelChoice` `:241`) |
| `TakeChoiceResult` in tests | `BoxOrderTests.cs:329`, `SaveScreenDirectorTests.cs:637`; `CloseStandaloneChoice` `SaveScreenDirectorTests.cs:619`, `:632` |
| `IsAwaitingChoice` readers | `Day3SceneArcTests.cs:221`, `FramePassTests.cs:174`, `OpcodeDispatchTests.cs:442`, `OpcodesProductionTests.cs:130, :146, :457`, `SaveBookTests.cs` (96, 106, 147, 150, 159, 240, 302, 348), `SaveScreenDirectorTests.cs` (120, 565, 603, 621, 634, 729, 739, 776), `SaveBookEndToEndTests.cs:165`, `:179` |
| `IDialoguePresenter` test doubles that carry `ShowChoices` | `AlundraDialogueBoxMontage.cs:40-85` (`PrefixRecordingPresenter` on a real `DialogueService`); stubs `AlundraDialogueFlagMarkerTests.cs:84-98`, `AlundraYarnBindingsTests.cs:450-468`, `AlundraYarnVariableStorageTests.cs:449-467`, `AlundraYarnVariableStorageProbeTests.cs:259-277`; many tests attach a bare `new DialogueService()` only to make `HasPresenter` true (`dll-notes.md` f2b1 section 3) |
| other | `AlundraTextBoxOracleTests.cs:320-345` (K5 oracle: the choice is a scripted wait, no choice API), `:403-430` (Sailor12: "0x44 first entry", "0x44 resolved" notes); degraded-mode `AlundraDialogueOpcodeDispatchTests.cs:210-224` (no director -> Result 1) |

### 3.2 Arcs / production-shaped tests that answer a choice
- **A17** (`AlundraDay3SceneArcTests.cs:212-229` `RunAcceptingTheChoice`: each frame, `if (IsAwaitingChoice) SelectChoiceForTests(0)` then `arc.OneFrameWithTheDialogueButton()`; used `:260-261`; asserts `0x44 @1056` and `0x05 @1072`, `:283`). The Square rule is
  `AlundraArcSupport.cs:495-500` (`_dialogueHold = director.IsOpen && !(IsWaitingForPress && _dialogueHoldPreviousFrame)`) through `PadStateProviderForTests` (`:188-192`: `ButtonsHold | Square`).
- **Sailor 12, map 389 C[12]** (`AlundraDialogueOpcodesProductionTests.cs:90-165`): in the frame callback, `if (director.IsAwaitingChoice) { Assert labels; Assert.True(SelectChoiceForTests(0)); }`, then the follow-up box is expected by frame ~116.
- Arcs on maps that hold a `0x44` but never mention a choice: A8 (map 163), A9 (172), A10 (165), A10J (map 10), A12 and A14 (179). None is blocked by a `0x44` today (A17's note says a loop without an answer stalled on `0x44 @1056`), so
  their programs do not reach one **[hyp, not asserted by any test]**.
- Golden traces (`IntroTraceHarnessTests.cs`, six traces): the 389 intro window (1704 frames) dispatches none of the eight dialogue opcodes (`plan-e12-dialogues.md:100-106`), so a choice change cannot move a golden byte.

### 3.3 What a pad-driven faithful box moves (list, in order of cost)
1. **Timing pins that assume an immediate, synchronous answer** (all tick-exact): `AlundraDialogueBoxOrderTests.cs:324-329` (K5: `SelectChoiceForTests(0)` then `TakeChoiceResult() == 1` **in the same tick**, then `RequestScriptClose`);
   `SaveBookTests` (`:155-157`, `:199-200`, `:350-351` ... `Tick()` right after the answer, `Tick(1 + Wait)`); `SaveScreenDirectorTests.cs:121-123` (`Tick(1 + 18)`: "the picker ends 18 ticks after the answer's tick"),
   `:523-526` (`Tick()` = "the answer arms the closing slide"), `:563-565`, `:587-588`, `:806-813`, `:858-861`; `Scripts/AlundraSaveScreenPresenterTests.cs:153-156`. If the binary delivers the result at the Cross press, only the *delivery path*
   changes (pad edge instead of a method call) and these keep their tick; if it delivers it after a closing slide of the choice box, every one of these moves (a binary question).
2. **Pad seam**: `AlundraDialogueDirector.Pass(bool, bool)` / `Tick()` / `_squareOfTick` (`AlundraWorldProxy.cs:2042-2045`, `:2105-2117`) carry Square only; Cross/Up/Down must be carried per tick the same way (the box reads the pad of the previous tick). Direct callers of `Pass`:
   `IntroTraceHarnessTests.cs:594-600`, the montage/`DialogueBoxPassDriver`, the oracle hosts (`AlundraTextBoxOracle.cs`, `OraclePad*`).
3. **Arcs and montages**: `AlundraArcSupport.OneFrameWithTheDialogueButton` (`:495-500`) must also press Cross when a choice waits (A17's loop becomes pad-driven); `AlundraDialogueBoxMontage` pads (`PadForFrame`).
4. **Engine-window assertions go**: `AlundraSaveScreenDirectorTests.cs:555-566`, `Scripts/AlundraSaveScreenPresenterTests.cs:137-160` (Modal > Menu, pushed over, removed alone), `AlundraDialoguePresenterWiringTests.cs:106-111`, `UI/AlundraDialogueScreenAssetTests.cs:113-159`
   and `UI/AlundraDialoguePresenterFontTests.cs:195-257` if the engine `DialogueScreen` is no longer used by Alundra at all (f2 sketch: "le `DialogueScreen` du moteur reste dans le moteur, Alundra cesse de l'utiliser"; also the project's `UI/Screens/DialogueScreen.{xaml,uiscreen}` and
   `ProjectWriter.SetDialogueScreenAsset`, `UiWriter.cs:161-196`, become dead **[hyp, plan call]**).
5. **`HasPresenter` semantics** (6 sites: `AlundraEventProgramRunner.cs:937, :961, :1873`, `AlundraSaveBook.cs:235, :272`, `AlundraSaveScreenDirector.cs:984`) is "an engine presenter exists" and decides degraded vs real. A pad-driven choice machine needs no view (the box machine of f2a is already view-independent, F2-R2),
   so `0x44` could stop degrading with no presenter: that moves `AlundraDialogueOpcodeDispatchTests.cs:210-224` and every test that attaches a bare `DialogueService` only to make `HasPresenter` true.
6. `AlundraDialogueCapturePresenter.ShowChoices/SelectChoice` forwarding (`:88-90`) and `PrefixRecordingPresenter` stay or go with the engine path; nothing reads them for options (no option in the corpus).

### 3.4 Proposed shape of the test hook (replaces `SelectChoiceForTests`)
Principle: tests should drive the **same input the production machine reads** (a pad edge per tick), so the hook is an injected pad, not a method that jumps to the result. Proposal (names indicative):
```csharp
// AlundraDialogueDirector (internal), all test-only
internal IReadOnlyList<string> ChoicesForTests { get; }          // unchanged contract: the two labels of the choice box, empty when none
internal int ChoiceCursorForTests { get; }                       // 0 or 1
internal bool IsChoiceAcceptingInputForTests { get; }            // opened and past its entry (the box ignores the pad before)
internal bool AnswerChoiceForTests(int index);                   // false if no choice is open / not accepting input; otherwise queues the presses a player makes (Up or Down to reach `index`, then Cross),
                                                                 // one per pass, consumed by the choice machine INSTEAD of the pad of that tick (so the move sound, the cursor and the validation sound play as in production)
```
- Same `Assert.True(Dialogue.AnswerChoiceForTests(i))` shape as the 24 sites; the sites that read the result in the same tick (K5, `SaveScreenDirectorTests.cs:523-526`) need a run-to-result helper in `AlundraDialogueBoxTestSupport` (`TicksToAnswer`) or a re-pin to the latency the binary gives.
- World-level tests (arcs, `AlundraSaveBookEndToEndTests.cs:165-182`, sailor 12) use the real pad: extend `AlundraArcSupport` (`:188-192`, `:495-500`) so the frame's `ButtonsHold`/`ButtonsJustPressed` carry Cross when `IsChoiceAcceptingInputForTests` and the arc asks to accept (A17: `RunAcceptingTheChoice` becomes "press Cross on the first accepting frame", the cursor already rests on OUI).
- Director-level tests keep a shortcut only if a fidelity-free case is needed: `internal void ForceChoiceResultForTests(int result)` (writes the machine's result as the binary's callback does: 1 first, 2 second), clearly separated from the pad-driven path; do not keep `SelectChoiceForTests` under its old name (it jumps over the cursor and the sounds).
- Test-support: a `ChoicePadDriver` (alongside `DialogueBoxPassDriver`) that holds `LastPadState` for N ticks, so unit tests of the choice machine itself (cursor, sounds 1/4/5/2/3 per the f3 sketch, validation by Cross only, the Cross that opened the save question must not also answer it) are written against the pad.
- Keep `TakeChoiceResult` returning `int?` (null pending) or move to the binary's convention `0 pending / 1 / 2` **[decision for f3]**; the DLL maps 1 -> Result 1 else 0 in three places (`AlundraDialogueDirector.cs:528`, `AlundraSaveBook.cs:309`, `AlundraSaveScreenDirector.cs:1034`).

## 4. The view side of a choice **[code]/[data]/[hyp]**

### 4.1 What f2b1c implies
- `view-notes.md` (f2b1 annex) proves, texel-exact on a real GPU at x1/x2/x3 and in an offset view, a `Window` 320 x 240 (`BorderThickness="0"`, `Padding="0"`, `IsTopmost`, transparent) with a `Canvas` of `Image` (frame by GUID, cursor by `SourceName`) and a clipped `Canvas` of font3 `TextBlock`s
  (`Padding="0" LinePadding="0" WrapText="False" VerticalContentAlignment="Top" Width="255" AllowsInlineFormatting="False"`), bound to a view model that notifies only on change, pushed/removed by a presenter after each box pass
  (`AlundraTextBoxScreen` : `XamlUIScreenBase`, `AlundraTextBoxPresenter`; plan E19.f2b1c R2-R5; `AlundraSaveScreen*` is the pattern: `AlundraSaveScreenPresenter.Tick` `AlundraSaveScreenPresenter.cs:45-64`, `AlundraWorldProxy.cs:2072-2081` per-tick call).
- A choice view is the same ingredients: a frame image, two label `TextBlock`s, a cursor. Authored XAML files live in `alundra-project/UI/Screens/` (versioned, `.gitignore:62-68`); `UiWriter.RegisterVersionedScreens` (`UiWriter.cs:161-196`) catalogues every `*.uiscreen` it finds, name from the envelope; a new `ChoiceScreen.{xaml,uiscreen,design.json}` needs no converter change.
- **The frame asset**: `g_uiBoxesConfigurationBackgroundMessageChoice`: `UiBoxes.csv` row `g_uiBoxesConfigurationBackgroundMessageChoice;176;144;16;4` [data]; sprite id **`829f31a7-39d1-5fc5-b436-befd59b50985`** (`alundra-project/UI/g_uiBoxesConfigurationBackgroundMessageChoice.sprite`, location 0,0,128,32, hotspot (64,16)), texture `b7356c91-b7a2-5506-8388-51ad1270eefa`, png `04caf959-cecd-52f8-8066-b323ac4fed05`
  (`UI/Textures/g_uiBoxesConfigurationBackgroundMessageChoice.png` 128 x 32, baked by `UiBoxWriter` from the decompilation's `SPRT_ARRAY_800a45ec`, 64 cells, plan E19.f1 section 1.2j.2). Position (176, 144) = X 0xB0, Y 0x90; it overlaps the text frame (16, 168, 288 x 56) over y 168..176.
  **The name is carried by three assets (png, texture, sprite), so the XAML must name it by GUID** (the same trap as `g_uiBoxesInventoryDescriptionBackground`, `view-notes.md` 1.3 and G4).
  Sibling name-box asset `g_textTilesConfiguration` (id `91ca17ae-e279-5a45-bb45-b15fbabdde68`, 112 x 32 at (64, 140)) is for f4.
- Cursor of a choice: nothing of the choice box's own selection marker is exported or identified in this surface; the wait cursor (`wind_150/173/201/228` ids `785b80e4-...`, `a68a47ff-...`, `4d7dff2a-...`, `3d59f23b-...`, animation `ui_dialogue_cursor` `1c7bdb56-6d86-51e4-be2c-fbd3bb9b30f7`) is the text box's. **[open: binary surface]** what marks the selected label (sprite, tint, blink).
- Labels: font3 text, `Etc_0067`/`0068` etc. -> 3 glyphs each; the binary renders them into 128 x 16 bitmaps with **`clut = 8`** [decomp `UIManager.cs`], so the same O-E19-66 caveat as the text box applies (font3.png's palette comes from FONT3.TIM's CLUT, the box takes palette 8 of WIND.CL, they differ at index 4) and O-E19-64 (`Padding="0"`).

### 4.2 Separate screen or part of the text box screen? (options, with the evidence; a design call for the plan)
- **A. Separate `AlundraChoiceScreen` + presenter (recommended by the evidence)**: the binary has the text box and the choice box as separate callback slots (slot 3, cfg `0x800A4FEC`, update `0x800501FC`, layer 5 vs the text box, `f2b-disc/notes.md` 1.2, plan f2 sketch) with independent lifetimes (section 2: the choice ends at the answer, the box ~18 passes later); the save screen's question has **no text box** at all, so a choice inside the text box screen would need that screen (needs `AssetContentManager` + `UIFonts`, retry wiring) up with only its choice parts visible;
  push order text box -> choice holds in the whole corpus (section 2), and `ScreenStack` orders by push order, not `UILayer` (`docs/plan-e19-f2b1-annexe/dll-notes.md`, summary item 5); the save question pushes over the `SaveScreen` (already up). Cost: a second XAML, view model, presenter and `TryWire...Once` + `OnEndPlay` release (copy of `:1399-1419`, `:2764-2790` as for f2b1c), one more T5 pixel case.
- **B. Part of the text box screen**: no second wiring, canvas child order = z-order, one view model; but the lone question needs the text box screen wired and shown with its box parts hidden, couples two independent binary callbacks and a faithful pad machine for the choice to the text-box presenter.
- Either way the engine `DialogueScreen` is out of the path, so the engine-side "choices drop on `ShowLine`" guard and `ShowTypedText` go (f2b1c R5) and the "close the engine window on answer" fix proposed by f2b1 (`TakeChoiceResult` closing the presenter) is unnecessary if f3 lands right after f2b.

### 4.3 MGUI gaps for a choice view
- **No new MGUI capability is needed** for a frame `Image` by GUID, two font3 `TextBlock`s, a cursor `Image` and bound `Top`/`Visibility`: all exercised by the f2b1 probe (summary 1-2, 8; `view-notes.md` 1.2-1.4). A pad-driven choice has no MGUI interactive control at all (no focus, no `MGButton`), so the engine's focus/Submit mechanism is simply unused.
- Gaps shared with f2b1c (already listed there): **G1** the view is one render frame behind the box tick (`CasaEngineGame.cs:537` vs `:555`; a cursor move is seen one frame late, `SourceName` applies at once, other bindings at the next `Desktop.Update()`; O-E19-65); **G3** `BorderThickness="0"` must be declared on the `Window`;
  **G4** GUID not name (above); **G5** `Alundra.Tests.csproj:17` pins MonoGame 3.8.4.1 vs the engine 3.8.5.1 so a `CasaDesktopRuntime`-based pixel test needs the f2b1c R6 pin change first; **O-E19-64/66** (+1,+1 offset, palette index 4).
- **[hyp, binary question]** if the selected label is drawn with a colour ramp or additive tint that is not a multiplication, MGUI cannot render it (same limit as the f4 portrait ramp, plan f4 sketch: "il ne fait que multiplier"): to be reported as a gap if the binary needs it.
- **[hyp]** modal: D-E19-80 makes the text box screen modal; a choice screen should be too (`IsModal => true`, like `DialogueScreen.cs:130-131`); since pad input reaches the DLL through `PlayerInput` and not through MGUI routing (save carousel precedent), this does not block Cross/Up/Down; not proven for a choice screen on top of a modal text box.
- **Stack order test** (cheap, from `dll-notes.md` f2b1 2.4): a recording `IUIViewRuntime` asserting `Pushed == [textBox, choice]` after `Open` + `OpenChoice`, and `[saveScreen, choice]` for the lone case.

## 5. Facts that contradict or refine the plan's text
- The plan (`plan-e19-opcodes.md:4279-4281`) says 23 test sites; HEAD has **24** `SelectChoiceForTests` call sites (list in 3.1).
- `plan-e12-dialogues.md:109` "0x44 117/52" counts table words as code; the programs hold **101 sites in 39 maps**.
- f2b1's `dll-verify.md` V19 "51 to 101 sites, partial" is now complete: 101, zero reached through "dialog open + only non-yielding opcodes".
- The plan sketch's "three unidentified callers" of `0x80050BA8`: the decompilation lists two more call sites (ETC 0x81/0x82 at `MemoryCardManager.cs:895`; a debug literal at `:584`), neither ported; the binary's own `jal` list is another surface's.
- f2b1c R5 (`TakeChoiceResult` closes the engine window "quand une boîte est active") and `CloseStandaloneChoice` (lone) are two paths today; with a pad-driven choice both disappear.

## 6. Open questions (not answerable from this surface)
1. Binary: when exactly does the opener's callback deliver the result (at the Cross press, or after the choice box's own closing slide)? This decides whether the 24 sites keep their ticks or move (3.3 item 1).
2. Binary: the input rules (Up/Down/Cross only? repeat? Triangle/Circle cancel? the Cross that opens the save question must not answer it), sounds 4, 1, 5, 2, 3, the selected-label marker and label positions in the 128 x 32 frame, the box's entry/exit animation (it uses `UpdateUiBoxesPosition`).
3. Design: separate `AlundraChoiceScreen` (A) or part of the text box screen (B); whether `HasPresenter` stays "an engine presenter exists" or becomes "a view is wired" once Alundra stops using `DialogueScreen`.
4. Whether to keep a degraded `Result = 1` for `0x44` when no choice view is wired (today: no presenter), now that the machine does not need a view.
5. Not examined: the keyboard keys the author's setup maps to the engine's Submit (today's only way to answer in play besides the mouse); the 2 map-165 and the map-163 sites (dead code?).
