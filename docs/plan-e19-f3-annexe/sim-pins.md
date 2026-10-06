# E19.f3: table "test, assertion, today, faithful choice box" (simulated, paste-ready)

Lines are those of the repository at HEAD (`db8d61c`, code identical at `21fee93`). "Today" = the value written in the test, confirmed by the run of the unpatched tree (`repo-base`, 2716/2716).
"Faithful" = the value measured on the scratch DLL with the box of `docs/plan-e19-f3-annexe/model/choice_model.py` ported to C# (identical to the model on 610 cases, see `notes.md`), with the
test hook `SelectChoiceForTests` re-implemented as a pad-driven answer (Left/Right then Cross, one press per pass from the first interactive pass at or after the arming).
"Red" = the unmodified assertion fails on the faithful box (`logs/faith1`); the patched value passes (`logs/finalA`).

**Conventions.** N = tick of the opener (0x44 first entry, book `RunQuestion`, `AskQuestion`, `OpenChoice` of a test). A consumer that polls once per tick sees the answer at the **37th tick after the
opener's tick for OUI** (Cross at N+19), **the 38th for NON** (Right at N+19, Cross at N+20), whatever the origin (script after the pass, entity script before it): written 37/38 below. Today it sees it at the 1st.
The box is released/closed by its own rules and is unchanged by this.

## 0. Test hook contract (applies to every row)

| Item | Today | Faithful |
|------|-------|----------|
| `SelectChoiceForTests(i)` | jumps to the result through the engine presenter, returns true once | arms the presses; true while `IsAwaitingChoice` (A17 calls it every frame for 37 frames); idempotent; presses at the first interactive pass at or after the arming |
| `ChoicesForTests` | the presenter's list, empty right after the answer | the labels while the box runs (opener to the close pass, N .. N+36) |
| Who must tick the director | the test that has no proxy ticked it or not (screen tests never ticked it) | every test that waits for an answer must run `AlundraDialogueDirector.Tick()` (or `Pass`) once per tick: 37/38 passes |
| Answer arrives | same call / next tick | tick N+37 / N+38 |
| Engine window (`ScreenForTests`, presenter `Choices`) | pushed at `OpenChoice` | never pushed |

## 1. 0x44 through the runner (director has no proxy)

| Test | Assertion | Today | Faithful |
|------|-----------|-------|----------|
| `AlundraDialogueOpcodeDispatchTests.Choice_0x44_FirstOptionSelected_ResultOne` | `:474-476` `CodeIndex`, `Result` on the dispatch after `Select(0)` (`:472`) | `CodeIndex 1`, `Result 1` on the next dispatch (tick 2 of the test) | red `:475`. Needs one `AlundraDialogueDirector.Tick()` per dispatch; `CodeIndex 1`, `Result 1` at the **37th** dispatch after the opener's |
| `...Choice_0x44_FirstEntry_OpensRealOuiNonLabels_ThenBlocksUntilSelected_ThenWritesResult` | `:446` second dispatch stays 0; `:450` `Select(1)`; `:452-454` | `CodeIndex 1`, `Result 0` on the next dispatch | red `:453`. Stays suspended until the **38th** dispatch after the opener's (NON = Right N+19, Cross N+20), then `CodeIndex 1`, `Result 0` (same tick) |
| `AlundraDialogueBoxOrderTests.K5_...` (montage, entity script) | `:322-325` `OpenChoice` at frame 30 | frame 30 | frame 30 (unchanged) |
| | `:326-331` `Select(0)`, `TakeChoiceResult() == 1`, `RequestScriptClose()` in the same tick | frame 90 (a test constant) | the answer is polled at the frame **67** (= 30 + 37); red `:329` (null). `RequestScriptClose` in that frame |
| | `:359` `(E, T, R)` | `(56, 90, 108)` | `(56, 67, 85)` (E unchanged: typing done 56) |
| | `:360` `seen` (frame the script sees the box released) | 109 | 86 |
| | `:350-355` list and `IsAwaitingChoice` at frame 60 | held | holds (box runs frames 30..66); `:358` glyph frames unchanged |
| `AlundraTextBoxWiringTests.WithAChoice_...` | `:197-198` `Select(0)`, `TakeChoiceResult() == 1` in the same call | same call | after **37** `Pass` + `textBox.Tick()` (poll after each); red `:193` first (engine screen) |

## 2. Save book (`AlundraSaveBookTests`; `Tick()` = book slot C, then `Dialogue.Tick()`; the book's question opens at the Tick 62 [state 2 countdown], unchanged)

| Test | Assertion | Today | Faithful |
|------|-----------|-------|----------|
| `Yes_RunsEveryStateWithTheOriginalWaits_...` | `:155-157` `Select(0); Tick();` then `StateCapture`, `IsOpen`, no choice awaiting, flags `ControlLocked|MessageBox` | answer taken at the Tick 63 | taken at the Tick **99** (37 ticks); red `:157` (state 4 instead of 5) |
| | `:165-168` capture and screen start (`Tick(Wait-1)`, `Tick()`, `StateWaitScreen`, `Single(Started)`) | Tick 124 | Tick **160** (= 99 + 61) |
| | `:175-181` release of the question box: `Tick(9)` up at 133, `Tick()` down at 134, flags `ControlLocked|MessageBox` at 133 | the book already waits for the screen (state 6) | release at the Tick **134, unchanged**, but BEFORE the capture: book still in state 5 at 134; flags `ControlLocked` only after 134 (`MessageBox` released); the up/down pair is at 133/134 right after the answer (`Tick(34)` then `Tick()`) |
| `No_Abandons_WithTheBoxClosed_AndNothingStarted` | `:199-200` `Select(1); Tick();` then `FlowEnded 1` (`:204`) | Tick 63 | NON taken at the Tick **100** (38 ticks); red `:204` |
| | `:207` `Tick(71)` then `AssertReleased` | release at 134 = 63 + 71 | `Tick(34)` (release at 134 = 100 + 34); `Tick(71)` stays green but loose |
| `SlotF_RepeatedInMidFlow_IsIgnored_AtStates2_4And6` | `:350-352` `Select(0); Tick(1 + Wait);` then `StateWaitScreen` | 62 ticks after the arming | `Tick(37 + Wait)` = **98** ticks (red `:352`, 5 instead of 6); `:362` `Tick(9)` loose |
| `State5_ScreenAlreadyActive_StartRefused_Abandons` | `:372-375` `Select(0); Tick(1 + Wait);` `Single(Started)` | 62 | **98** (red `:375`); `:379` `Tick(10)` loose |
| `State5_CaptureRefusedByTheValidation_...` | `:392-396` same, `FailuresStarted 1` | 62 | **98** (red `:396`) |
| `State5_RulesFactoryThrows_StartsTheFailureMessage` | `:408-412` same | 62 | **98** (red `:412`) |
| `State5_NoWorldName_StartsTheFailureMessage` | `:422-426` same | 62 | **98** (red `:426`) |
| `State5_NoScreenWired_Abandons` | `:463-464` same, then `:468` `Tick(10)`, `AssertReleased` | 62 | **98** (red inside `AssertReleased`, `:103`: flags not 0 because the book still waits for its answer); the box is released at 134, before the book abandons at the Tick 160 |
| `State5_CaptureRefused_EndsInBoundedTicks_WithEverythingReleased` | `:439` `Select(0)`, loop to idle (`:447`), `Assert.True(ticks < 500)` `:449` | 102 ticks | **138** ticks (bound 500 unchanged; no assertion on the count) |
| `State1_AChoiceAlreadyWaiting`, `State4_AnotherOpenClearsTheQuestion`, `State1/2_*` | direct `OpenChoice`, `Open` clearing the choice | green | green (the copy keeps `Open` cancelling a waiting choice) |

## 3. Save screen (`AlundraSaveScreenDirectorTests`; its `Tick()` never ticked the dialogue director: it must, order A = screen then dialogue as in the proxy of today, order B = dialogue then screen)

| Test | Assertion | Today | Faithful |
|------|-----------|-------|----------|
| `AnswerAndClose(choice)` helper (21 test instances: `Yes_RunsTheKeptStates...`, `No_WritesNothing_...` (NON), `Labels_AreReadFourTimes...`, `Labels_HostileSlots_...` x10, `AnySaveStatusButSaved_...` x5, `AServiceThatThrows...`, `AFakeThatThrows...`, `IsActiveAndMenuOpen_...`) | `:121-123` `Select(choice); Tick(1 + 18); Assert.False(Director.IsPickerActive)` | answer at tick 1, picker ends at 19 | red `:123`. Answer consumed at tick **37** (OUI) / **38** (NON) after the arming, picker ends 18 ticks later: `Tick(37 + 18)` = **55** (OUI), `Tick(38 + 18)` = **56** (NON). Same in order A and B |
| `Carousel_Closing_ThreeRowsLeaveUpwards_...` | `:523-526` `Select(0); Tick(); // the answer arms the closing slide`; `:527-...` `Tick(16)` etc. | arms at tick 1 | arms at tick **37** (a loop until `!IsQuestionPending`), then the same `Tick(16)`, `Tick()`, `Tick()` (red `:529`) |
| `Cross_AsksOuiNon_OverTheScreen_...` | `:556` `Pushed` contains the dialogue screen; `:563-567` `Select(1); Tick();` no choice awaiting, `_presenter.IsOpen` false, `Removed` | answer at the next tick | `:556`, `:566`, `:567` go (engine window); NON armed one tick after the Cross is taken **37** ticks later (S+38), `IsAwaitingChoice` false, flags `MenuOpen` |
| `DownDuringTheQuestion_ThenOui_WritesTheFrozenSlot_Once` | `:586-588` `Tick(25, hold: Down); Select(0); Tick(hold: Down);` | answer at the next tick | armed 26 ticks after the Cross, past the first interactive pass (18): pressed at the next pass; `Tick(hold: Down)` becomes **20** ticks in order A (**19** in order B); a loop until `!IsQuestionPending` is more robust. **E19.f3c (D-E19-93, 2026-10-06): the proxy now runs order B; the test pins 19.** |
| `CloseStandaloneChoice_ClosesALoneChoice_...` | `:630` `Pushed` contains, `:635-636` `_presenter.IsOpen`, `Removed` | engine window | assertions go; `CloseStandaloneChoice() == true`, `IsAwaitingChoice` false, `TakeChoiceResult() == null`, flags unchanged hold |
| `Book_WithTheRealScreen_Oui_WritesTheHerosCapture_ThenReleasesTheHero` | `:805-808` `BookTick(1 + WaitTicks + 1)` (62), `Select(0)`, `BookTick(1 + WaitTicks + 1)`, `Assert.True(Director.IsActive)` | 62 + 62 ticks | the second call is `BookTick(37 + WaitTicks + 1)` = **98** (red `:808`) |
| | `:813-815` the screen's `Select(0)`, loop bound 400 to `StateWaitSquare` | 80 ticks | **116** ticks (bound 400, margin 284) |
| `Book_WithTheRealScreen_ARefusedCapture_...` | `:857-860` same as above | 62 | **98** (red `:860`); `BookTick(3)` after it unchanged |
| `WorldChange_WhileTheScreensChoiceWaits_...`, `WorldChange_WithTheBookAtState2_...`, `CloseStandaloneChoice_DoesNothingWhileABoxIsOpen_...` | opens a choice, never answers | green | green |
| `Scripts/AlundraSaveScreenPresenterTests.Question_TheDialogueScreenGoesOverTheSaveScreen_AndLeavesAlone` | `:147` `Pushed == { _screen, dialogueScreen }`; `:149`, `:151` layers; `:153-155` `Select(1); Tick(); Removed == { dialogueScreen }`; `:159` `Removed == { dialogueScreen, _screen }` | engine window above the save screen | `:147` becomes `{ _screen }`, `:149`/`:151` go, `:155` `Removed` empty and the NON is taken after **38** ticks (armed in the tick of the Cross: the helper at `:59-66` must tick the dialogue), `:159` `{ _screen }` |

## 4. Engine-window assertions (no value: the assertion goes with the engine window)

| Test | Assertion | Today | Faithful |
|------|-----------|-------|----------|
| `AlundraDialoguePresenterWiringTests.InstallDialogueSystems_WithAnActiveUiView_...` | `:181` `recorder.Pushed.Count > 0` after `OpenChoice` | pushed | 0 |
| `AlundraDialoguePresenterWiringTests.Update_WiresThePresenter_OnceTheViewAppearsAfterWorldInit` | `:260` same | pushed | 0 |
| `AlundraTextBoxWiringTests.AChoiceWithoutABox_StillClosesThroughTheStandaloneRoute` | `:212` `Single(uiView.Pushed)`, `:214` `Single(uiView.Removed)` | 1, 1 | 0, 0 (`CloseStandaloneChoice` true, `IsAwaitingChoice` false) |
| `AlundraTextBoxWiringTests.WithAChoice_...` | `:193` `{ rig.Screen, engineScreen }`, `:194-195` layers, `:199` `Removed == { engineScreen }`, `:201` `IsOpen` | engine screen above the text box | `{ rig.Screen }`, no layer assertion, `Removed` empty |
| `AlundraDialogueFramePassTests.AttachToWorld_RePointsWithoutResetting_AnOpenDialogueSurvives` | `:248` `rePointedPresenter.Choices == { OUI, NON }` | the new presenter holds the list | `Choices` empty; `director.ChoicesForTests == { OUI, NON }` |

## 5. Arcs and production-shaped runs that cross a 0x44 or a book choice (green with the hook as armed; frames measured)

| Run | Value | Today | Faithful | Budget |
|-----|-------|-------|----------|--------|
| A17 (`AlundraDay3SceneArcTests.cs:212-283`, `RunAcceptingTheChoice` `:212-229`) | `0x0D@1048` / `0x36@1053` / `0x44@1056` first entry (frames, `:283`) | 326 / 326 / 581 | 326 / 326 / 581 | |
| | choice awaits from the start of frame / no longer awaits from | 582 / 583 | 582 / **619** | |
| | `0x03@1059`, `0x05@1072` (`:283` asserts they ran) | 601, 601 | **637**, **637** (the 0x39 waits for the box released 19 ticks after the answer) | |
| | `0x11@1115`, `0x05@1120` (`:260-261` stop signals) | 1126, 1157 | **1162**, **1193** | |
| | end frame | 1158 | **1194** | `FrameLimit` 2500, max(2500, ceil(1.2 x 1194) = 1433) = 2500 unchanged |
| | `:282` T0 set before the choice (`FirstSet[0] <= FrameOf(C, 1056) + 1`) | holds | holds (T0 set while typing) | |
| | helper `:221-224` | `IsAwaitingChoice` true for 1 frame, `Select` called once | `Select` called on each of the 37 frames; must keep returning true | |
| Sailor 12 (`AlundraDialogueOpcodesProductionTests.cs:71-165`, scripts then pass) | choice awaits after frame; taken in frame | 96; 97 | 96; **133** | |
| | question box released after frame; follow-up box opens after frame (`:144-145` comment says 116 / 115) | 115; 116 | **151; 152** | |
| | follow-up box released after frame; callback budget `:98` `RunFramesForTest(400, ...)` | 318 | **354** | 400 (margin 46, was 82) |
| Book end-to-end, map 17 (`AlundraSaveBookEndToEndTests.cs:132-198`) | `:165` choice awaits (budget 200) | 62 | 62 | 200 |
| | `:170` picker open after the book's OUI (300) | 103 | **139** | 300 (margin 161) |
| | `:182` screen flow after the screen's OUI (400) | 80 | **116** | 400 (margin 284) |
| | `:191` Square end (200) / `:198` second picker (300) | 38 / 42 | 38 / 42 | |
| Every other arc (A1..A20 of the suite, `IntroTraceHarness`, `HeroTraceHarness`) | opens no choice (census of `OpenChoice` by test method, `logs/finalA/opens.log`) | green | green, no value moves | |
