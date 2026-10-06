# E19.f3 simulation: the faithful choice box ported into an OFF-REPO copy of the DLL, validated against the model, the suite run on it

Repository `chantier/e19-suite`, code of HEAD `db8d61c` (the two later commits, `8c3bcfb` and `21fee93`, only touch `docs/`). Nothing was built, run, exported, edited,
staged or committed inside the repository; `CasaEngine.Launcher/Program.cs` untouched. Everything lives under `f3-sim/` (scratchpad). The working tree of the repository
changed under me (another agent: `Alundra/Scripts/AlundraEntityScriptProxy.cs` modified, `Alundra.Tests/AlundraHeightWaitOpcodesTests.cs` new, E19.h1b2): not mine, and not in my copy.

Tags: **[fact]** measured or read (file:line of the repository at HEAD, or a file of `f3-sim/`); **[model]** `docs/plan-e19-f3-annexe/model/choice_model.py`;
**[sim]** run on the scratch DLL; **[hyp]** hypothesis.

## 0. Layout of `f3-sim/`

| Path | What |
|------|------|
| `repo/` | scratch copy of `Alundra/`, `Alundra.Tests/`, the engine (`CasaEngine`, `CasaEngine.Compiler`, `MGUI`, props) and `docs/` at HEAD, then patched. `repo/alundra-project` is a directory JUNCTION to the real export (read by the tests, never written: `find -newer` found nothing new in it; the deploy target of `Alundra.csproj` is removed in the copy). |
| `repo-base/` | the same copy NOT patched in `Alundra/` ("today" tree), tests patched with the measurement notes only. |
| `repo/Alundra/Scripts/AlundraChoiceBox.cs` | NEW: the C# port (see section 1). |
| `patch_dll.py` .. `patch_dll4.py` | the patches of the director and of the world proxy, applied on the pristine copy (each old text must occur once). `patch_tests.py faithful|notes <tree>` builds the patched tests from `orig_tests/`. |
| `pyref/` | unchanged copy of the annex model, `gen_ref.py`, `ref_choice.json` (610 cases). |
| `repo/Alundra.Tests/Sim*.cs` | `SimChoiceModelTests` (port vs model), `SimChoiceIntegratedTests` (V8 pad A, V9 on the real director), `SimChoiceProxyTests` (real pad through the proxy; "0x44 again"), `SimNotes.cs` (log helper). |
| `logs/` | `validate/`, `faith1` (unmodified tests, faithful box), `finalA`/`finalB` (patched tests, both save-screen orders), `base/` (today), `mutants/`. `results-*/` hold the trx. |
| `sim_pins_table.md` | the table "test, assertion, today, faithful" for the plan. |

## 1. The port [fact]

`AlundraChoiceBox.cs` is a line-for-line port of `choice_model.py` (`ChoiceBox`): stages Init / SlideIn / Active / SlideOut, `UpdateUiBoxesPosition` (15 steps + 2 settle passes, C# integer
division = the binary's truncation), Cross on the just-pressed word, Left/Right on the by-interval word, sounds 4 (opener), 1 (only when the selection changes), 5 then 2 or 3 (press pass),
result word 1/2 written only when the slide-out ends, cursor counter persistent across boxes (wraps at 40), labels cut at 6 characters, drawn state (frame, labels, cursor, image) per pass.
The pad words are those of `AlundraTickPad` (the DLL's port of the binary pad update 0x8002E250, same delay 20 / interval 0).

Wiring (copy of the DLL):
- `AlundraDialogueDirector`: owns the box; `Pass(squareHeld, squarePressed, choicePressed = 0, choiceInterval = 0)` runs the text box pass THEN the choice pass; `OpenChoice` opens the box (no engine
  window any more, no presenter needed) and logs sound 4; `TakeChoiceResult` returns null until the result word is written (1 -> 1, 2 -> 0) and clears `IsAwaitingChoice`; `Open`, `InstallForMapEntry`,
  `CancelChoice`, `CloseStandaloneChoice` drop the box; `Tick()` (seam of the tests without proxy) feeds the words of the call before from its own `AlundraTickPad`.
- `AlundraWorldProxy.Update`: records, per tick, `TickPad.ButtonsJustPressed` and `ButtonsJustPressedByInterval` next to the Square of the tick, and gives the choice pass the words of the tick BEFORE
  (the same rule as the text box, F2-R1).
- Test hook `SelectChoiceForTests(index)` (name kept): arms the presses of a player: Left/Right until `index` is selected, then Cross, one press per pass, starting at the first interactive pass
  at or after the arming; idempotent; returns true while a choice awaits. `ChoicesForTests` = the labels while the box runs; `ChoiceBoxForTests`, `ChoiceSoundLogForTests`.
- UNCHANGED: `AlundraEventProgramRunner` case 0x44 (first entry = `!IsAwaitingChoice`, then `TakeChoiceResult()`), `AlundraSaveBook`, `AlundraSaveScreenDirector`. The three consumers need no code
  change: only the latency of `TakeChoiceResult` moves.

## 2. Validation of the port against the model (a mismatch would have been a stop: none) [sim]

1. Bare scenarios V1..V7 and 603 more (3 specials + 600 random pad sequences of 160 frames, default selection and cursor counter drawn at random): per pass, the update function, drawn or not, close,
   sounds, frame/label/cursor x and y, image, selection, counter, the opener sound, then the tick and the Result at which the 0x44 is resolved. **610 cases, 46,534 passes compared, 577 resolved,
   0 mismatch** (`logs/validate/choice_port_vs_model.txt`). V1 N+37 Result 1, V2 N+40 Result 0, V3 N+48, V4 N+80, V5 N+37, V6 N+37 Result 0, V7 N+37, earliest NON N+38, a direction held from N+7 then Cross N+58.
2. V8 (sailor 12, pad A) and V9 (the save book's question over a box still typing) on the REAL director (f2a text box, Yarn runner, f3 choice), with a script host in the model's order: **events,
   text milestones (first/last glyph, typing done, close trigger, release), choice milestones and sounds identical** (`logs/validate/integrated_vs_model.txt`): V8 open N+94, result N+131, `0x51` N+131,
   close trigger N+132, next open N+150; V9 opener 61, result 98, typing done 115, close trigger 116, release 134. V8 pad B is not replayed (the choice is independent of the text pad; only the typing length changes).
3. The real pad through the world proxy (`DialogueBoxMontage`, one tick per frame): 5 sequences (Right held before the interactive window; Cross held through N+19 then pressed again; Cross at the
   earliest; Right held, repeat at N+29, then Cross; Cross one pass too early = lost for ever) give the model's resolution ticks (N+50, N+52, N+37, N+80, never) and sounds.
4. "A 0x44 reached again asks again" and "the cursor counter is not reset by the opener": the second box starts at counter 35 and draws image 3 at 36 (matches the model's V7 shape). The annex claim
   "the 0x44 marker is never cleared" is REFUTED by `binary-verify.md`; the DLL's memo (`IsAwaitingChoice`) already behaves like the refuted-claim-corrected binary.
5. Sensitivity: 14 deliberate mutants of the port (settle 2->1, steps 15->14, wrap 40->39, start x 320->319, pitch 48->47, cursor dy -8->-7, opener sound 4->5, result without +1, Cross on the
   by-interval word, Left/Right sound rules, drawing on the close pass, drawing on the init pass, result always 1): **14/14 caught** (`logs/mutate_port.log`, `logs/mutants/mutate_port_out_subset.txt`;
   the opener sound was first NOT caught, the comparison was strengthened).

## 3. The suite on the scratch DLL [sim]

- "Today": `repo-base` 2716/2716 green (Intro and Hero trace harness tests, 3, also green; they open no choice).
- Faithful box, tests UNCHANGED (`logs/faith1`): **44 red** of 2716 = 31 test methods (the two theories expand to 10 and 5 rows). First failing assertion per test: `sim_pins_table.md`.
  Three causes: (a) the test answers and reads the result in the same tick or the next one (the box needs 37/38 ticks); (b) the save screen tests never tick the dialogue director, so the box never advances
  (`AlundraSaveScreenDirectorTests.cs:75-83`, `Scripts/AlundraSaveScreenPresenterTests.cs:59-66`); (c) 8 tests assert the engine's dialogue window (`ScreenForTests` pushed/removed, `Choices` of the presenter).
- Faithful box, pad-driven patched tests (`patch_tests.py faithful`): **2720/2720 green** (2716 + the 4 Sim tests) with the save-screen order A (screen tick, then the dialogue pass: the DLL order today);
  with order B (dialogue pass, then the screen tick: the binary's slot 3 before slot 10) the same except ONE value: `DownDuringTheQuestion` takes 19 ticks instead of 20 (a LATE answer, see 5).
- Census of `OpenChoice` over the whole suite (`logs/finalA/opens.log`, the caller's test method read from the stack): only the save book and save screen tests, K5, the dispatch tests, the wiring/presenter
  tests, the sailor-12 test, the book end-to-end test and A17. **No other arc opens a choice** (A8, A9, A10, A10J, A12, A14, A18, A20 ... never reach a 0x44; the hypothesis of `dll-notes.md` 3.2 is now measured).

## 4. What the faithful box moves

Constants [sim, = model]: N = tick of the opener. Init pass N+1 (nothing drawn), first drawn N+2, interactive N+19, press C, closed and result written at C+18, seen by its consumer at the same tick
(script after the pass) or at the next frame's script phase (entity script before the pass): **in both cases the consumer sees it at the 37th tick after the opener's tick for OUI (Cross at N+19), the 38th for
NON (Right at N+19, Cross at N+20)**; the origin shift (opener one pass early, consumer one pass late) cancels. Today the same consumer sees it 1 tick after the opener.
So every arc that crosses a 0x44 is delayed by **+36 ticks (OUI at the earliest) or +37 (NON)**, plus one tick per tick the player waits after N+19 (the hook arms before N+19: no wait).
The corpus has 101 sites in 39 maps, all `... 0x44 0x51 (0x39 | 0x03/0x04)`: the 0x51 and the 0x39 add nothing (the box is released at max(typing end + 19, L + 19)).

Arcs and production-shaped runs [sim, "today" from `repo-base`]:

| Run | Value | Today | Faithful |
|-----|-------|-------|----------|
| A17 (`AlundraDay3SceneArcTests.cs:212-283`) | frame of C `0x0D @1048` / `0x36 @1053` / `0x44 @1056` (first entry) | 326 / 326 / 581 | 326 / 326 / 581 |
| | choice awaits from the start of frame | 582 | 582 |
| | answer taken: no longer awaiting at the start of frame | 583 (taken in 582) | 619 (taken in 618) |
| | `0x03 @1059` / `0x05 @1072` (after the 0x51 and the 0x39) | 601 / 601 | 637 / 637 |
| | `0x11 @1115` / `0x05 @1120` | 1126 / 1157 | 1162 / 1193 |
| | end frame (`FrameLimit` 2500, unchanged by max(current, 1.2 x end)) | 1158 | 1194 |
| Sailor 12 (`AlundraDialogueOpcodesProductionTests.cs:71-165`, harness order scripts-then-pass) | T999 set during frame / choice awaits after frame | 95 / 96 | 95 / 96 |
| | choice taken in frame | 97 | 133 |
| | question box released after frame / follow-up box opens after frame | 115 / 116 | 151 / 152 |
| | follow-up released after frame (callback budget 400) | 318 | 354 (margin 46) |
| Book end-to-end, map 17 (`AlundraSaveBookEndToEndTests.cs:165-198`) | `FramesUntil` used of budget: choice awaits (200) / picker open after the book's OUI (300) / screen flow after the screen's OUI (400) / Square end (200) / second picker (300) | 62 / 103 / 80 / 38 / 42 | 62 / 139 / 116 / 38 / 42 |
| `State5_CaptureRefused_EndsInBoundedTicks` loop (bound 500) | ticks | 102 | 138 |
| `Book_WithTheRealScreen_Oui` screen loop (bound 400) | ticks | 80 | 116 |

Model V8 (pad A) and the DLL run of the sailor test differ in absolute frames (other text pad, other order of the script and the pass) but agree from the opener on: result N+37, release N+56 after the opener
(box close trigger N+38, +18), next open at the release.

## 5. Facts that matter for the plan

1. The hook must be armed before N+19 to answer at the earliest. A later arming presses at the next pass: `DownDuringTheQuestion` arms 26 ticks after the Cross (the box has been interactive since 18),
   answers at the next pass and takes **20 ticks in order A, 19 in order B**. For an on-time answer the count is 37/38 in both orders.
2. The save screen tick order (`AlundraWorldProxy.cs:2117` runs `AlundraSaveScreenDirector.Instance.Tick()` in the pad loop, the box passes later in the frame): with the order of today the screen sees an answer at
   the same count as the binary for an on-time answer, because the opener runs one pass early and the consumer one pass late. To mirror slot 3 before slot 10 literally the choice pass would have to precede
   that call; the counts change only for late answers (+1).
3. `AlundraDialogueDirector.Open` cancels a waiting choice (E16.e SE2, `AlundraSaveBookTests.cs:310 State4_AnotherOpenClearsTheQuestion`): kept in the copy; the binary's slots are independent. No corpus case.
4. `HasPresenter` still means "an engine presenter is attached" and gates 0x44 (degraded `Result = 1`), the book and the screen; the choice box itself needs no presenter. Eight tests assert the engine window
   and cannot survive: `AlundraDialoguePresenterWiringTests.cs:181,260`, `AlundraTextBoxWiringTests.cs:193-201,212-214`, `Scripts/AlundraSaveScreenPresenterTests.cs:147-159`,
   `AlundraSaveScreenDirectorTests.cs:556,566-567,630,635-636`, `AlundraDialogueFramePassTests.cs:248`.
5. 25 `SelectChoiceForTests` call sites at HEAD (the annex counted 24: `AlundraTextBoxWiringTests.cs:197` came with f2b1c), 6 `ChoicesForTests` readers, 4 direct `OpenChoice` callers in tests.
6. `AnswerAndClose` (`AlundraSaveScreenDirectorTests.cs:116-124`) has 8 call sites (the `dll-verify.md` count) that expand to 21 test instances through the two theories (20 OUI, 1 NON): one helper edit moves 21 tests.

## 6. Hypotheses and limits [hyp]

- Logic only: no pixels, no tpage, no view (the choice screen is not part of this simulation). The drawn state is compared with the model, not rendered.
- The answer is injected by the hook (words of a player); no test drives the Cross through a real `AlundraPlayerController`. The hero cannot jump during a choice because every 0x44 site opens a mode-1 box
  (`ControlLocked`/`MessageBox`/`MenuOpen` for the book and the screen) [`dll-notes.md` 1.7, 2, not re-verified here].
- The text pad B of V8 and the origins "map event" vs "entity" were checked through the Answer counts of tests of both shapes (book: entity order; dispatch/K5: script and pass), not through a dedicated replay.
- Label widths (OUI 19, NON 23) are the binary's; the port's `Width` uses the DLL's `font3` advance provider and is not part of the comparison.

## 7. Rerun

- `mkjunctions.cmd` recreates the junctions `repo/alundra-project` and `repo-base/alundra-project`; `rmjunctions.cmd` removes only the links (never delete the scratch folder with `rm -r` while a junction exists).
  The junctions were removed at the end of this session (links only: the export is intact, `find -newer` over it was empty).
- Build: `dotnet build Alundra.Tests/Alundra.Tests.csproj -c Debug` in `repo/` (and in `repo-base/`); the engine is built from the copy under `repo/CasaEngineMonogame`, never from the repository.
- Reference values: `python pyref/gen_ref.py` writes `pyref/ref_choice.json` (model unchanged, reads `data-extracted/` read-only).
- Tests: set `F3_REF` (that json), `F3_LOGDIR` (a log folder), `F3_SCREEN_ORDER` (A or B), then `dotnet test Alundra.Tests/Alundra.Tests.csproj --no-build --filter "FullyQualifiedName!~IntroTraceHarness&FullyQualifiedName!~HeroTraceHarness"`.
  `F3_LOGDIR` receives `events.log` (choice events stamped with the passes of the director), `opens.log` (census of the callers of `OpenChoice`), `values.log` (the notes of the patched tests).
- Mutants of the port: `python mutate_port.py [name fragment]` (restores `AlundraChoiceBox.cs` in its `finally`; do not run tests in parallel with it).
- Patching from scratch: copy the repository files again (`setup.cmd`, which overwrites the patched files: re-fix the csproj deploy target FIRST, then run `patch_dll.py`, `patch_dll2.py`, `patch_dll3.py`, `patch_dll4.py` and `patch_tests.py faithful repo`).
