# E19.f3 closed-list discovery: tests that move, DLL code to change, test hook (read-only, HEAD db8d61c, branch chantier/e19-suite)

Nothing built, run, exported, edited, staged or committed inside the repository. Everything I ran lives in this folder:
`dllsim.py` (the binary model `choice_model.py` + the DLL host orders, ticks of the hosts of the existing tests),
`padcheck/` (a console project that compiles a COPY of `Alundra/Scripts/AlundraTickPad.cs` and replays 1512 random/held pad sequences against the binary pad model `PadWords`),
`gen.py`/`in.txt`/`out_*.txt`. `f3-sim/` was already populated by another agent (a copy of the repo, build and test logs, dated 02:24-02:27 today): I read nothing
from `scratchpad/bak/` and wrote nothing in `f3-sim/`.

Tags: **[code]** read in the repository at HEAD (file:line); **[calc]** computed by my scripts from the binary model + the DLL order read in the code;
**[annex]** stated by `docs/plan-e19-f3-annexe/*` (binary facts, not re-derived); **[hyp]** not proven.

## 0. Summary

1. **Counts at HEAD** [code]: `SelectChoiceForTests` 25 call sites in 9 test files (the earlier count was 24: f2b1c added `AlundraTextBoxWiringTests.cs:197`), `ChoicesForTests` 6
   readers, direct `OpenChoice` 9 calls in 6 files (the earlier census listed 4; the other 5 are FramePassTests:247, PresenterWiringTests:180/:259 and TextBoxWiringTests:191/:211, the last two added by f2b1c), 8 callers of `AnswerAndClose`,
   8 callers of `RunToTheQuestion`. Only ONE implementer of `IAlundraDialogueDirector` (the director): no fake to update.
2. **The by-interval word already exists**: `AlundraTickPad.ButtonsJustPressedByInterval` (`AlundraTickPad.cs:63-68`, `Update` `:71-106`, delay 20 / interval 0 = `PadManager.UpdatePad`).
   [calc] My C# replay of that class against the binary-validated `PadWords.update` (`choice_model.py:48-69`): 1512 cases (random words, long holds of Right/Left/Cross/Cross+Right,
   a held direction + an unrelated button), **0 mismatches** (`padcheck/`). Nothing to add to the pad classes; what is missing is a per-tick RECORD of Cross/Left/Right (like `_squareOfTick`).
3. **Where the pass runs** (recommended): at the TOP of the first per-tick loop of `AlundraWorldProxy.Update`, before `GameState.TickPad.Update` (`:2106`) and before
   `AlundraSaveScreenDirector.Instance.Tick()` (`:2117`). Reading the TickPad fields there gives the pad of the PREVIOUS tick (the binary's lag, as `_squareOfTick[tick-1]` does for the box)
   with no new record list, and it keeps the binary order slot 3 then slot 10. Cost: for a catch-up frame (several ticks) all choice passes precede the map-event passes (the box's B0 M0 B1 M1
   interleave is not kept for the choice). The alternative (second loop, right after `_textBoxPresenter?.Tick()` `:2180`) needs a per-tick record list and makes the save screen see the answer
   one tick late. Section 2.
4. **Hero lock**: yes in the three callers, so Cross (Jump) cannot leak: `MovePlayer` returns before any jump logic while `InputBlockedMask` (ControlLocked | MessageBox | ForcedSequence)
   is set (`AlundraPlayerManager.cs:211`; the Cross edge is read at `:440`, after the gate): the box of every `0x44` site is mode 1 = MessageBox (101/101 sites, `dll-notes.md` 2; `AlundraDialogueDirector.cs:293-296`),
   the book sets ControlLocked (`AlundraSaveBook.cs:186`), the save screen holds MenuOpen (`GameplayBlockedMask` freezes the entity update, `AlundraEntityScriptProxy.cs:1090`).
5. **Tests**: of the sites asked, **R (a value is re-pinned)**: K5 `:329` only for the delivery (its outputs are unchanged by construction, section 4.3), SaveBookTests :155, :199, :350,
   SaveScreenDirectorTests helper :121 (+8 callers), :523, :563, SaveScreenPresenterTests:153, TextBoxWiringTests:197, the 3 O20 oracle tests (not in your list), the engine-window pins (V);
   **C (helper or budget, no value moves)**: A17, sailor 12, E2E, the 2 dispatch tests, the other book tests, :587, :806/:813/:858, the harness block, `OneFrameWithTheDialogueButton`;
   **U**: `ChoicesForTests` readers (contract kept), `IsAwaitingChoice` readers, `HasPresenter` sites, SaveBookTests :233 and :310, K5 oracle, all `Pass(bool,bool)` direct callers.

## 1. The DLL at HEAD, what changes (file:line) [code]

### 1.1 `Alundra/Scripts/AlundraDialogueDirector.cs` (639 lines)
- `:23-91` `IAlundraDialogueDirector`: `IsAwaitingChoice` `:36-37` (must stay true from `OpenChoice` until the result is TAKEN, so it spans the slide-out: the 0x44 first-entry memo is
  `!IsAwaitingChoice`, `AlundraEventProgramRunner.cs:968`); `Tick()` `:74-78` (doc to extend: the seam of hosts without a proxy runs the choice pass too); `OpenChoice` `:80-83`;
  `TakeChoiceResult` `:85-90` (int?: 1 first option else 0; keep: the callers map the binary's 1/2 this way in 3 places, `:520`, `AlundraSaveBook.cs:309`, `AlundraSaveScreenDirector.cs:1034`).
- `:105-110` class doc "the engine's presenter is only the choices' window (until E19.f3)": to rewrite.
- `:149-151` `_awaitingChoice`, `_pendingChoiceResult`, `_choiceHandler` -> a machine field; `:155` `IsAwaitingChoice`.
- `:180-211` `AttachToWorld` (stores `_soundPlayer` at `:184` BEFORE the null-presenter return: the machine can play sounds without a presenter).
- `:220-240` `InstallForMapEntry` (`:227` Unsubscribe, `:234-235` clears the choice state) -> reset the machine, NOT the cursor counter (session-scoped like the binary's BSS word, `binary-notes.md` 10.7).
- `:243-267` `Open` (`:249-250` clears the choice state): policy decision D-F3-4 below.
- `:419-433` `Pass(bool,bool)` / `Tick()`: keep `Pass(bool squareHeld, bool squarePressed)` unchanged (10 direct callers in tests stay U, section 3.5); add `PassChoice(in AlundraChoicePad)`; `Tick()` also runs it
  from its own `AlundraTickPad` fed by `_gameState.LastPadState.ButtonsHold` with the one-call lag (the shape of `_tickSquareHeld/_tickSquarePressed`, `:146-147`, `:430-432`).
- `:445` `IAlundraDialogueBoxHost.PlaySound` -> `_soundPlayer?.PlaySfx(sfxId)`: the path for sounds 1-5 (4 at the opener tick, 1 on a selection change, 5 then 2/3 at the press). Ids 1-5 already play
  in production from the inventory (`AlundraInventoryDirector.cs:513, :636-660, :760, :870-950`), and `sfx_0001..0005.wav` exist [annex]. `_soundPlayer` is wired by `InstallDialogueSystems` (`AlundraWorldProxy.cs:1228`) and the retry (`:1261`).
- `:501-515` `OpenChoice` (subscribes `ChoiceSelected` `:511-513`, `ShowChoices` `:514`) -> opens the machine; no presenter needed. `:517-521` `OnPresenterChoiceSelected`: delete.
- `:524-544` `TakeChoiceResult`: reads the machine's word; delete the f2b1c R5 `_presenter?.Close()` (`:536-541`).
- `:552-557` `CancelChoice`: must also stop the machine (the book and the screen abandon paths). `:569-581` `CloseStandaloneChoice`: delete (callers `AlundraSaveScreenDirector.cs:359`, `:1032`; tests below).
- `:583-589` `UnsubscribeChoiceHandler`, `:594-613` `ResetForTests` (`:610-611`; add the counter reset), `:634` `ChoicesForTests` (source = the machine's labels), `:638` `SelectChoiceForTests` (replace).

### 1.2 `AlundraEventProgramRunner.cs`
- `:950-989` case 0x44: no change of logic if the director contract above holds (degrade `:961-965`, first entry `:968-978`, poll `:981-988`). `HasPresenter` stays the real/degraded switch (`:937`, `:961`, `:1873`), see D-F3-3.
- The interpreter zeroes the program marker after any non-zero handler return (`state.Parameters[1] = 0`, `:461-462`, the binary's `0x8004232C`, `binary-verify.md` 2), so a re-executed `0x44`
  asks again, as required; and a result is continued in the same call (the loop goes on to `0x51`): the same-tick continuation of the binary [annex].

### 1.3 `AlundraSaveBook.cs` (434 lines)
`:235` `HasPresenter`, `:241` guard `IsOpen || IsAwaitingChoice`, `:272`, `:285` `OpenChoice`, `:286` `_choiceOpened`, `:295-304` poll + "cleared before any answer" abandon (`!IsAwaitingChoice`), `:306-317` answer,
`:399-418` `Reset` (`:409-412`: `CancelChoice` only when `_choiceOpened && ownedBoxStillUp`: if `Open` stops clearing the choice (D-F3-4) the `ownedBoxStillUp` clause must go).
The book is an ENTITY script (slot C 72): its origin row in section 2.

### 1.4 `AlundraSaveScreenDirector.cs` (1283 lines)
`:357-360` map entry (`CloseStandaloneChoice` -> `CancelChoice`), `:930-935` the PickerQuestion branch (`PollAnswer` -> `ArmPickerClose`), `:949-958` Cross by interval asks, `:971-973` stale doc,
`:976-1003` `AskQuestion` (`:984` HasPresenter, `:1000` `OpenChoice`, `:1001` `_questionOpened`), `:1007-1036` `PollAnswer` (`:1021` take, `:1032` `CloseStandaloneChoice`: delete, the machine closes itself at C+18), `:1245` reset.
The screen's own tick is in the FIRST per-tick loop (`AlundraWorldProxy.cs:2117`).

### 1.5 `AlundraWorldProxy.cs`
`:153-156` `_squareOfTick` / `_squareOfLastTick` (stay); `:2104-2144` first loop: insert at the top (before `:2106`) the choice pass and, right after it, the choice presenter's tick; `:2148-2153` the wiring retries
(add `TryWireChoiceScreenOnce` on the shape of `TryWireTextBoxScreenOnce` `:1437-1464`, `OnEndPlay` release `:2847-2849`); `:2173-2202` second loop (box pass `:2176`, `_textBoxPresenter?.Tick()` `:2180`, gate `:2182`):
unchanged, the choice does not depend on the gate (the save question runs under MenuOpen).
Engine presenter wiring (`:1208-1262`, `AlundraDialoguePresenter`) can stay in f3 (D-F3-3).

### 1.6 Pad classes
`AlundraPadState` (`AlundraPlayerController.cs:23-37`) carries ButtonsHold/ButtonsJustPressed of the RENDERED frame (built by `BuildPadState`, `:218-236`): not the source. The source is `GameState.TickPad`
(per logic tick): `ButtonsJustPressed & Cross`, `ButtonsJustPressedByInterval & (Left|Right)`. No change.

### 1.7 New code (names indicative)
`AlundraChoiceBox` (the machine = `choice_model.py` `ChoiceBox`; reuse the `UpdateUiBoxesPosition` slide port at `AlundraDialogueBox.cs:462`), `AlundraChoicePad` (readonly struct: CrossPressed, LeftRepeat, RightRepeat),
a view (screen + view model + presenter, option A of `dll-notes.md` 4.2: separate screen, pushed at its first drawn pass N+2, removed the pass it is no longer drawn), `choice` host seam for sounds.

## 2. Origins and where the pass runs [code]+[calc]

Two placements; (A) recommended = top of the first loop, before `TickPad.Update`; (B) = second loop right after the box pass.
Ticks relative to the opener tick N (the tick in which `OpenChoice` runs), OUI at the earliest, pad of the previous tick, no gate involved:

| Origin | (A) first pass | (A) active from | (A) result written | (A) consumer sees it | (B) first pass | (B) consumer sees it |
|---|---|---|---|---|---|---|
| map event / `0x44` of a map program (second loop, after the box pass) | N+1 (binary) | N+19 | N+37 | N+37 same tick (binary) | N+1 | N+37 same tick (binary) |
| pending trigger (after the loops) | N+1 | N+19 | N+37 | same frame (binary) | N+1 | same frame |
| entity script (before the proxy: the save book, the sailors' C programs) | N (1 early, as the box, D-E19-64) | N+18 | N+36 | N+37 (1 late; = the binary's N+37) | N | N+37 |
| save screen (first loop, `:2117`) | N+1 (binary: choice pass before the screen tick) | N+19 | N+37 | N+37 same tick (binary) | N (1 early) | N+38 (1 late) |

[calc] `dllsim.py` host_screen: opener S, active from S+19, Cross seen S+19, closed and polled S+37 (OUI), S+38 (NON, Right S+19, Cross S+20); sounds (S,4), (S+19,5),(S+19,2) / (S+19,1),(S+20,5),(S+20,3).
Catch-up frames (several ticks): (A) puts all the choice passes of the frame before the map-event passes (a `0x44` can see its result up to n-1 ticks early); (B) keeps the interleave.
Recommendation: (A): exact for 3 of 4 origins on one-tick frames, no new record list, binary slot order 3 before 10.

## 3. Test census: classification (R value re-pinned / C helper or budget, no value moves / U unchanged; V = view-level: the assertion is about the engine window)

Assumed design (the session choice): machine independent of any presenter; `HasPresenter` semantics unchanged (D-F3-3); view = a new separate choice screen pushed at its first drawn pass; the engine
window no longer shows choices; `SelectChoiceForTests` replaced by `AnswerChoiceForTests` (section 4); `ChoicesForTests` kept (source = the machine).

### 3.1 `SelectChoiceForTests` (25 sites)
| # | Site | Assertion today | Class | What it becomes |
|---|---|---|---|---|
| 1 | `AlundraDay3SceneArcTests.cs:223` (A17 `RunAcceptingTheChoice` `:212-229`, used `:260-261`) | each frame `if (IsAwaitingChoice) Assert.True(SelectChoiceForTests(0))`, then `OneFrameWithTheDialogueButton()`; end pcs `0x11 @1115`, `0x05 @1120`, `0x44 @1056`/`0x05 @1072` (`:283`), `FirstSet[0] <= FrameOf(C,1056)+1` | **C** | the loop disappears: `RunUntilPressingTheButtonOnEveryDialogueFrame` suffices once `OneFrameWithTheDialogueButton` also presses Cross while a choice awaits (section 4.4). pcs and the T0 pin unchanged. Budget: FrameLimit 2500 (`:205`): [annex] V-131 gives the end 1158 with f2a; +36 for the real choice = ~1194 < max(2500, 1.2 x 1194): **2500 stays** [calc from annex, not run]. |
| 2 | `AlundraDialogueBoxOrderTests.cs:328` (K5 `:307-365`; `:324` OpenChoice, `:329` `TakeChoiceResult()==1` in the same call, `:331` `RequestScriptClose` same tick) | glyph frames `{28,32,...,52}`, `(E,T,R)=(56,90,108)`, seen 109; frame 60: `ChoicesForTests=={OUI,NON}`, `IsAwaitingChoice`, `Row(0)=="bonjour"` | **R (delivery) with outputs unchanged by construction** | `montage.PadForFrame = f => f == 70 ? Cross : 0`; the frame-90 body becomes a POLL: `TakeChoiceResult()` is `1` at frame 90 (opener 30 by the entity script, first pass 30, active from 48, Cross seen by the pass of 71, closed at the pass of 89, polled at 90 [calc `host_montage_k5`]) then `RequestScriptClose` in the same call: T=90, R=108, seen=109 unchanged. New pins: sounds 4@30, 5+2@71 (`PassStampedSoundPlayer`). Frame-60 asserts U (`ChoicesForTests` = the machine's labels). |
| 3 | `AlundraDialogueOpcodeDispatchTests.cs:450` (NON), `:472` (OUI); readers `:442-443` | first dispatch opens (CodeIndex 0, awaiting, labels), second still suspends, then `SelectChoiceForTests` and ONE more dispatch advances (`CodeIndex 1`, `Result` 0 / 1) | **C** | these call `runner.RunOneScriptCall` with no pass at all. Either `ForceChoiceResultForTests(2|1)` (writes the machine's word as the callback does; recommended: they pin the opcode contract) or a loop of `Dialogue.Tick()` + dispatch until `CodeIndex` moves (about 37/38 ticks). Values U. |
| 4 | `AlundraDialogueOpcodesProductionTests.cs:139` (sailor 12, map 389) | in the frame callback `if (IsAwaitingChoice) { labels; Assert.True(Select(0)) }`; later `OpenSerial == serialAtStart+2 && IsOpen && !IsAwaitingChoice` ("the follow-up box opens at the frame 116", comment `:147`) | **C** | hook call renamed; all conditions are state-based. The comment's frame moves by about +36 (~150). Budget `RunFramesForTest(400)` (`:98`): S002 closes about 204 frames after it opens (oracle O20 arcs pad), so ~355: slack ~45 frames: confirm with the extended oracle, raise to 450 if needed. |
| 5 | `AlundraSaveBookEndToEndTests.cs:167` (book) and `:180` (screen); readers `:165-166`, `:179` | book: wait for awaiting, labels, Select(0); screen: `Press(Cross)`, `IsAwaitingChoice`, Select(0) | **C** | `:167` -> `AnswerChoiceForTests(0)`; `:180` -> the first world-level real-pad pin: `FramesUntil(IsChoiceAcceptingInputForTests)` then `Press(proxy, Cross)` (the TickPad record path through the proxy). Budgets 300 (`:170`) and 400 (`:182`): +37 each, still inside (after the book's answer: 37 + 61 + 60 = ~160 frames to the settled picker, budget 300; after the screen's answer: 37 + ~100 to WaitSquare, budget 400) [calc, not run]. |
| 6a | `AlundraSaveBookTests.cs:155` (`Yes_RunsEveryStateWithTheOriginalWaits...`) | after OUI one `Tick()` reaches StateCapture, box still open, flags, `Tick(Wait-1)`, `Tick()` reaches WaitScreen (call 124), `Tick(9)` box up at 133, `Tick()` box down at 134 | **R** | [calc `host_book`] opener call 62, active from 80, Cross seen 80, closed 98, polled 99 (OUI). StateCapture at call 99 (box still open, flags ControlLocked|MessageBox), WaitScreen at **160** (was 124), the box is released at **134 (unchanged: the latch at 99 is before the typing ends at 115, close trigger 116)**: the order flips, the box goes down BEFORE the capture (the old "up at 133 / down at 134 while the book waits for the screen" becomes "up at 133, down at 134, MessageBox cleared, ControlLocked kept, capture at 160"). |
| 6b | `:199` (`No_Abandons...`; readers `:201-206`, `Tick(71)` `:207`) | NON then one `Tick()`: FlowEnded 1 | **R** | NON polled at call **100** (Right 80, Cross 81, closed 99); `Tick(38)` for FlowEnded; the final `Tick(71)` becomes `Tick(34)` (release 134) or stays as a non-binding budget. |
| 6c | `:350-351` (`SlotF_RepeatedInMidFlow...`) | `Select(0); Tick(1 + Wait)` then WaitScreen | **C** | `Tick(37 + Wait)` (WaitScreen at 160); the rest U (the mid-flow Interact ignored). Stale comment `:360-361`. |
| 6d | `:372-373`, `:392-393`, `:408-409`, `:422-423`, `:463-464` | `Select(0); Tick(1 + Wait)` then outcome asserts (refusal, failure start, abandon) | **C** | `Answer(0); Tick(37 + Wait)`; trailing `Tick(10)` budgets become non-binding (box released at 134 < 160). |
| 6e | `:439` (`State5_CaptureRefused_EndsInBoundedTicks...`) | `Select(0)` then `do { Tick(); _screen.Step(State); } while (... && ticks < 500)` | **C** | hook only: the loop already runs to the end (ticks < 500 still holds: 37 + ~100). |
| 7a | `AlundraSaveScreenDirectorTests.cs:121` (`AnswerAndClose` `:116-125`, 8 callers `:163,:219,:300,:360,:652,:665,:677,:695`) | `Press(Cross)`, `IsQuestionPending`, `IsAwaitingChoice`, `Select`, `Tick(1 + 18)`, `!IsPickerActive` | **R** | the helper's `Tick` must run the choice pass (`Dialogue.Tick()` BEFORE `TickPad.Update` + `Director.Tick()`, the production order): `AnswerChoiceForTests(choice)` then `Tick(37 + 18)` (OUI) / `Tick(38 + 18)` (NON): [calc `host_screen`] answer polled at S+37 / S+38, then the 18-tick closing slide (unchanged). The 8 callers keep their asserts (state-based `TickUntilState`, `TicksIn(...)` counts of other states, none counts PickWait). |
| 7b | `:523` (`Carousel_Closing_ThreeRowsLeaveUpwards...` `:518-545`) | `Select(0); Tick(); // arms` then `Tick(16)` positions (16,-57) etc. | **C** | `Answer(0); Tick(37)` (the 37th tick is the arming tick); every position/tint value after it U. |
| 7c | `:563` (`Cross_AsksOuiNon_OverTheScreen_...` `:550-571`; `:555` labels, `:556-557` engine screen pushed, `:559` `!IsOpen`, `:561` Down ignored, `:565-568`) | NON then `Tick()`: `!IsAwaitingChoice`, `!_presenter.IsOpen`, engine screen in `Removed`, MenuOpen kept | **R + V** | `Tick(38)`; the labels/!IsOpen/Down-ignored asserts U; `_presenter.ScreenForTests` and `_uiView.Pushed/Removed` asserts move to the choice presenter (pushed at the first drawn pass S+2, removed at the pass the machine is closed, S+38) or go; MenuOpen asserts U. |
| 7d | `:587` (`DownDuringTheQuestion_ThenOui_WritesTheFrozenSlot_Once` `:577-592`) | `Press(Cross)`, `Press(Down)`, `Tick(25, Down)`, `Select(0)`, `Tick(Down)`, `TickUntilState(WaitSquare)`, one save call, slot2 | **C** | hook only (the machine is active since S+19, Cross record at the next pass); Down held is ignored by the machine; `TickUntilState` (400) absorbs the latency. Values U. |
| 7e | `:806`, `:813` (`Book_WithTheRealScreen_Oui_...` `:783-833`; BookTick = book, `Dialogue.Tick()`, screen: already the binary order) | book OUI: `BookTick(1 + WaitTicks + 1)`, Select, `BookTick(1 + WaitTicks + 1)`, IsActive; screen OUI: `BookTick(hold: Cross)`, Select, loop `< 400` to WaitSquare | **C** | `BookTick(37 + Wait)` after the first answer (capture at 160); the screen's loop 400: +37 inside. Values U. |
| 7f | `:858` (`Book_WithTheRealScreen_ARefusedCapture_...` `:835`) | same book part, then `BookTick(3)` failure message, Square, `BookTick(60)` | **C** | `BookTick(37 + Wait)`; the `BookTick(3)` / `BookTick(60)` tails U. |
| 8 | `Scripts/AlundraSaveScreenPresenterTests.cs:153` (`Question_TheDialogueScreenGoesOverTheSaveScreen_AndLeavesAlone` `:139-160`) | after `Tick(hold: Cross)` the stack is `[_screen, dialogueScreen]`, layers Modal > Menu, then Select(1), `Tick()` removes `dialogueScreen` alone, then `_screen` at the end | **R + V** | the helper `Tick` (`:59-67`) must add `Dialogue.Tick()` + the choice presenter tick; the choice screen is pushed at its first drawn pass (S+2) not at the Cross; removal at S+38 (NON); the `Layer` pins (`:149-151`) pin nothing real (push order decides, `dll-notes.md` 1.8) and go or move to the choice screen's own layer; `[_screen, choiceScreen]` order and "removed alone" stay as stack-order pins. |
| 9 | `AlundraTextBoxWiringTests.cs:197-198` (f2b1c T4 `WithAChoice_TheTextBoxIsPushedFirst_TheEnginesWindowAboveIt_AndTheAnswerClosesOnlyTheEnginesWindow` `:175-202`) | text box pushed first, engine window above at `OpenChoice`, `Select`, `TakeChoiceResult()==1` in the same call, `Removed==[engineScreen]` | **R + V** | text box pushed first at its first drawn pass; the choice screen above at its first drawn pass (opener + 2); `TakeChoiceResult` at C+18; `Removed == [choiceScreen]` only then. The engine presenter is no longer involved. |

### 3.2 `ChoicesForTests` (6 readers) : **U** (contract kept: the labels of the choice in progress, empty when none)
`AlundraDialogueBoxOrderTests.cs:352`, `AlundraDialogueOpcodeDispatchTests.cs:443`, `AlundraDialogueOpcodesProductionTests.cs:138`, `AlundraSaveBookTests.cs:151`, `AlundraSaveBookEndToEndTests.cs:166`, `AlundraSaveScreenDirectorTests.cs:555`
(all `{"OUI","NON"}`). The source changes from `_presenter?.Choices` to the machine; `:352` (K5 frame 60) is the one that also asserts the box did not wipe it: still true by construction.

### 3.3 Direct `OpenChoice` (9)
- U: `AlundraDialogueBoxOrderTests.cs:324` (K5 opener at frame 30), `AlundraSaveBookTests.cs:233` (`State1_AChoiceAlreadyWaiting_Abandons` `:230-244`, labels `{"A","B"}`, `CancelChoice` `:241`: U if `CancelChoice` stops the machine).
- R (V): `AlundraDialogueFramePassTests.cs:247` (`AttachToWorld_RePointsWithoutResetting_AnOpenDialogueSurvives`, assertion `:248` `rePointedPresenter.Choices == {OUI,NON}` "the choice reaches the new presenter": the engine presenter no longer receives choices; becomes `ChoicesForTests`, and "a re-point keeps the choice" if wanted).
- R (V): `AlundraDialoguePresenterWiringTests.cs:180` (`InstallDialogueSystems_WithAnActiveUiView_WiresAPresenterThatPushesOnOpenChoice` `:139-190`) and `:259` (`Update_WiresThePresenter_OnceTheViewAppearsAfterWorldInit` `:201-262`): `Assert.True(recorder.Pushed.Count > 0)` after `OpenChoice`. These are the green-and-inert wiring guards of the engine presenter; their purpose moves to the choice screen's wiring (`OpenChoice` + 2 passes -> pushed), the engine-presenter pins (`HasPresenter`, `:175`, `:234`, `:240`, `:255`) stay U.
- R: `AlundraSaveScreenDirectorTests.cs:616` (`CloseStandaloneChoice_DoesNothingWhileABoxIsOpen_AndNeverTouchesTheFlags` `:611-624`) and `:629` (`CloseStandaloneChoice_ClosesALoneChoice_...` `:626-640`, `TakeChoiceResult` `:637`): the standalone route disappears; the second becomes `CancelChoice` (lone choice cleared, flags untouched), the first has no equivalent.
- R (V): `AlundraTextBoxWiringTests.cs:191` (T4 stacking, above) and `:211` (`AChoiceWithoutABox_StillClosesThroughTheStandaloneRoute` `:204-216`, `CloseStandaloneChoice` `:213`): the lone choice pushes/removes its own screen with the machine; no standalone close.
- Readers of `TakeChoiceResult` in tests: `AlundraDialogueBoxOrderTests.cs:329` (K5, above), `AlundraTextBoxWiringTests.cs:198`, `AlundraSaveScreenDirectorTests.cs:637`.

### 3.4 Helpers, arcs, `HasPresenter`
- `AlundraSaveBookTests.RunToTheQuestion` (`:90-97`, 8 callers `:197,:312,:369,:389,:406,:420,:437,:461`): **U** (it stops at the opener call 62: `StateAnswer`, `IsAwaitingChoice`). `Tick` helper (`:79-87`: book then `Dialogue.Tick()`): U, the entity-origin order of the production (row 3 of section 2) is already this one.
  `:310` (`State4_AnotherOpenClearsTheQuestion_Abandons_...`): **U if `Open` keeps clearing a waiting choice and the machine** (D-F3-4); otherwise R.
- `AlundraSaveScreenDirectorTests.Tick` helper (`:75-82`): **C**: add `State.LastPadState = {ButtonsHold = hold}` and `Dialogue.Tick()` BEFORE `TickPad.Update` + `Director.Tick()`. `AnswerAndClose`: 7a.
- `AlundraArcSupport.OneFrameWithTheDialogueButton` (`:496-501`, pad provider `:188-192`): **C**: also hold Cross while a choice awaits, released on alternate frames (a held Cross gives no edge: V3; a press before N+18 is lost anyway): `_choiceHold = director.IsAwaitingChoice && !_choiceHoldPreviousFrame`, ORed into `ButtonsHold`/`ButtonsJustPressed`. Its other users (`RunUntilPressingTheButtonOnEveryDialogueFrame` `:465-482`, `AlundraEntityContactArcTests.cs:342` T-B9 counter-proof, no choice) U.
  Arcs that reach no `0x44` (A8, A9, A10, A10J, A12, A14 [hyp, `dll-notes.md` 3.2]): unchanged; one that did would now be answered OUI instead of stalling.
- `HasPresenter` readers in tests: `AlundraArcSupport.cs:222`, `AlundraSaveBookEndToEndTests.cs:222`, `AlundraSaveScreenDirectorTests.cs:598` (`Cross_WithoutAPresenter_IsNon_NothingWritten`), `AlundraDialoguePresenterWiringTests.cs:175, :234, :240, :255`: **U** while `HasPresenter` keeps "an engine presenter is attached" (D-F3-3). The many tests that attach a bare `new DialogueService()` only to make it true stay U.
- `IsAwaitingChoice` readers: U except where they sit right after an answer (SaveBookTests `:159`, SaveScreenDirectorTests `:565` move with their tick).

### 3.5 Found beyond the closed list
- **`AlundraTextBoxOracleTests.cs:449`, `:463`, `:483` (three `O20_Sailor12Of389_*`)** : **R** (`:449`, `:463`, `:483`). `Sailor12` (`:403-428`) models the `0x44` as `script.Hold(1)` ("resolved a tick after", the f2a placeholder L = G+1). With the real choice: pad A (map origin): second box `114 -> 150`, close trigger `96 -> 132`
  ([annex] `values.md` V8: choice opens N+94, resolved N+131, trigger N+132, S002 N+150); the arcs-pad and entity-origin tests move by the same +36 on `second` (the offsets inside the second box, `+19/+184/+187/+205`, `A-wait`, `A-release`, `scroll-armed` are relative, unchanged) and `0x39` `319 -> ~355`. The oracle needs a choice oracle written from the rules (never from the director), like the text box oracle.
- `AlundraTextBoxOracleTests.cs:320-345` (K5 oracle, scripted wait to frame 90): **U**, and it agrees with the DLL K5 above (T=90 by construction).
- `IntroTraceHarnessTests.cs:594-600` (harness block `RunFramesForTest`: `_dialoguePadMirror.Update`, `Pass(...)`): **C**: call the choice pass with the mirror's record (read before `Update`, the same lag); only the sailor-12 test needs it (golden traces dispatch no dialogue opcode). Degrade path (`DialogueDirector => null`, `:1154`) U.
- Direct `Pass(bool,bool)` callers (`AlundraTextBoxDrawnStateTests.cs:235,:290,:298,:321`, `AlundraTextBoxWiringTests.cs:91,:185`, `AlundraTextBoxViewModelTests.cs:158,:210,:260,:297`, `UI/AlundraTextBoxPixelTests.cs:68,:79`): **U** because `Pass` keeps its signature.
- `DialogueBoxMontage.PrefixRecordingPresenter` (`AlundraDialogueBoxMontage.cs:36-85`) carries `ShowChoices/SelectChoice` on a real `DialogueService`: unused after f3, harmless; the montage's `PadForFrame` (`AlundraDialogueBoxMontage.cs:174`, written at `:180`) is the real-pad route of the world-level hosts. `AlundraDialogueBoxTestSupport.DialogueBoxPassDriver` (`:31-80`) writes Square only: generalise to a word per pass (Cross/Left/Right) for the machine's own tests.
- `AlundraDialogueCapturePresenter.ShowChoices/SelectChoice` (`:88-90`) dead path, untouched. `AlundraDialogueOutOfBandCloseTests.cs` (`NotifyPresenterClosed`): U while the engine presenter stays.
- UI tests of the engine window (`UI/AlundraDialogueScreenAssetTests.cs:113-159`, `:124` `pnlChoices`; `UI/AlundraDialoguePresenterFontTests.cs:195-257`): U unless the dead engine window assets are removed (a cleanup call, not f3).

## 4. The test hook

### 4.1 Members (all `internal`, test-only unless noted)
```csharp
// AlundraDialogueDirector
internal IReadOnlyList<string> ChoicesForTests { get; }        // contract kept: the labels of the choice in progress ({} when none, also after the result is taken)
internal bool IsChoiceAcceptingInputForTests { get; }          // the machine is in its 'active' state: the pass of the next tick reads the pad
internal int ChoiceSelectionForTests { get; }                  // 0 = first (OUI), 1 = second (NON), after the last pass
internal AlundraChoiceBox ChoiceBox { get; }                   // drawn state for the view and the tests (like Box)
internal bool AnswerChoiceForTests(int index);                 // 0 = first option, 1 = second
internal void ForceChoiceResultForTests(int index);            // opcode-level tests only: writes the machine's word as the callback does (index 0 -> 1, 1 -> 2) and closes it
```

### 4.2 `AnswerChoiceForTests(index)`: exact semantics
- Returns `false` (no effect) when no choice is awaiting (`!IsAwaitingChoice`), when the machine already took its Cross (sliding out / closed), or when `index` is not 0 or 1. Returns `true` as soon as `OpenChoice` ran, including BEFORE the first pass (the tests call it right after the opener, where `SelectChoiceForTests` returned true today).
- It arms a one-shot pad script that REPLACES the pad record the machine would read, consumed ONLY by passes in the `active` state, one record per pass: if `index` differs from the current selection, first a Left (index 0) or Right (index 1) by-interval record (selection changes, sound 1), then a Cross just-pressed record; if equal, the Cross record alone. Nothing is consumed during init or slide-in, so a hook armed at the opener does not shorten the 18-pass entry ("a Cross before N+18/N+19 is lost" keeps its meaning) and the real pad of those passes is ignored while armed. A second call re-arms.
- Resulting timeline = "the earliest answer a player can give": OUI Cross seen at the first active pass (opener + 18 in an entity-origin host, + 19 in a map-event or save-screen host), result written 18 passes later: OUI N+37 / NON N+38 as seen by its consumer (rows of section 2); sounds 4 (opener), then 5,2 (OUI) or 1 then 5,3 (NON).
- It does not run any tick and does not touch `LastPadState`: it works unchanged in the four hosts (director `Tick()` seam, montage, world proxy, harness).
- `ForceChoiceResultForTests(index)`: for the two dispatch tests only; clearly NOT a fidelity path.

### 4.3 The real-pad route (no hook), used by the machine's own tests and one world-level test
`Tick()` (director seam) derives its record from `GameState.LastPadState.ButtonsHold` through its own `AlundraTickPad` with the one-call lag; the montage/world write `LastPadState` per frame (`PadForFrame` / `PadStateProviderForTests`). Scenarios V1-V9 of the annex then become tests of the machine written against an oracle
(`ChoicePadDriver` next to `DialogueBoxPassDriver`): V1 default at once (init N+1, first drawn N+2, interactive N+19, closed N+37, sounds 4@N, 5+2@N+19), V2 Right then Cross, V3 held Cross gives no edge, V4 auto-repeat 21st pass then Left, V5 Cross+Right in the same pass (old selection stored, sound 1 after 5,2), V6 default NON (variant opener, not used by the DLL), V7 counter left at 17, V8/V9 sailor 12 and the book with the real box.
K5 is the pinned example: values unchanged by timing the press (frame 70).

### 4.4 `AlundraArcSupport` (world-level)
`OneFrameWithTheDialogueButton`: `_choiceHold = director.IsAwaitingChoice && !_choiceHoldPreviousFrame`; the pad provider (`:188-192`) ORs `Cross` into `ButtonsHold` and into `ButtonsJustPressed` when `_choiceHold` and the previous frame did not hold it (the same shape as the Square rule). Alternation matters: V3 (a Cross held through the first active pass gives no edge, a permanent hold would deadlock).

## 5. Facts that decide placement and design, with evidence

- The box pass reads the pad of the previous tick through `_squareOfTick[tick-1]` / `_squareOfLastTick` (`AlundraWorldProxy.cs:2175`, `:2206`); the TickPad fields read before `Update` in the first loop are that same pad: no extra state.
- Save screen: `Tick()` in the first loop (`:2117`) reads `TickPad` AFTER `Update(k)` (current tick, one tick earlier than the binary's frame, D-E19-64-like), so it opens the question at S and its poll must come after the choice pass of the same tick to see C+18 as the binary's slot 10 does.
- `AlundraSaveBook` is an entity script: it runs in `AlundraEntityScriptProxy.Update`, BEFORE the proxy (`World.cs:443-491`, D-E19-64), hence the "first pass one tick early, result seen one tick late" row.
- 0x44 programs run from map events (second loop `:2197`), entity scripts and pending triggers; the corpus has 101 sites, all over a mode-1 box (`dll-notes.md` 2).
- Cross is the only button of the choice; Square (the text box's) and Circle/Triangle are never read: `dll-verify`/`binary-verify` 1 row 8. So Square can keep closing/advancing the box underneath while a choice is up (the arcs' Square hold continues).

## 6. Decisions the plan has to take (named, with my recommendation)
- **D-F3-1 placement**: (A) first loop, top, before `TickPad.Update` (recommended) or (B) second loop after the box pass. Section 2.
- **D-F3-2 test hook**: `AnswerChoiceForTests` = earliest answer by armed pad script (recommended) + `ForceChoiceResultForTests` for the two dispatch tests.
- **D-F3-3 `HasPresenter`**: keep "an engine presenter is attached" (zero test movement for its 6 production sites and the tests that attach a bare `DialogueService`); alternative "a view is wired" moves `AlundraDialogueOpcodeDispatchTests.cs:210-224`, SaveScreenDirectorTests:598, the book's `State1_NoPresenter`/`State2_NoPresenter` tests and every bare-`DialogueService` test. `AlundraDialoguePresenter`, the engine `DialogueScreen` and the project's `UI/Screens/DialogueScreen.*` become dead after f3: a cleanup slice, not f3.
- **D-F3-4 `Open` and a waiting choice**: today `Open` clears `_awaitingChoice` (`AlundraDialogueDirector.cs:249-250`), pinned by `AlundraSaveBookTests.cs:310`. The binary's `InitializeDialogMessage` does not touch the choice slot [annex]. Keep the clear (and kill the machine with it): zero test movement, no corpus instance (the `0x0D` guard refuses to open over an open box). Dropping it makes `:310` R and removes the SE2 abandon path.
- **D-F3-5 labels**: the machine takes exactly two labels, at most 6 bytes each (binary `strncpy` 6); `labels.Count != 2` -> ignore + warning.
- **D-F3-6 cursor counter**: session-scoped, reset only by `ResetForTests` (V7 pin).
- **D-F3-7 stale marker**: moot, the binary's claim is refuted (`binary-verify.md` 2); the DLL's `Parameters[1] = 0` (`:461`) already asks again.

## 7. Not proven
- No test or build was run in the repository; the tick numbers of section 3 come from the binary model (annex) plus the host orders I read ([calc]); the K5, book, screen and arcs values are therefore predictions the oracle/simulation of the slice must confirm before the code (D-E19-77 style).
- Whether a MGUI choice screen on top of the modal text box screen lets the pad reach the DLL: the save carousel (modal screen up, Up/Down/Cross read from `TickPad`, validated in game on 2026-10-02 per the memory notes) is the precedent; not shown for two stacked screens [hyp].
- The sailor-12 harness budget slack (~45 frames) and the A17 end (~1194) are derived from annex figures, not measured.
- Behaviour on catch-up frames for placement (A) is a design cost, not measured.
