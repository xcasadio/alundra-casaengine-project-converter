# O-E19-75 / D-E19-99 discovery: the age of the pad word read by the save screen (read-only)

Repository `chantier/e19-suite2`, HEAD `bbea80a`. Nothing built, run, exported, edited, staged or committed inside the repository (`git status` unchanged: ` m CasaEngineMonogame`, `? alundra-datas-analyser`,
the author's `Program.cs` untouched). Everything lives in this directory. The scratch copy `repo/` is a PHYSICAL copy of HEAD (robocopy, `setup.cmd`, `alundra-project` physical, no junction, `CasaEngine.Launcher` not copied);
its builds deploy `Alundra.dll` into the copy only. Nothing read from `scratchpad/bak/`.

Tags: **[bin]** read in the disassembly (`lib.py`, capstone) ; **[emu]** the REAL binary code executed in the MIPS interpreter (`emu.py`, copy) ; **[code]** file:line at HEAD ; **[model]** `padage_model.py` ;
**[sim]** the real `AlundraWorldProxy.Update` in the scratch copy ; **[hyp]** hypothesis.

## 0. Answer

1. **[bin][emu]** Every reader of the screen's pad words in the binary runs inside `RenderScene`; the pad is sampled ONCE per frame, by the first call of `Update(0)`, which runs AFTER `RenderScene`. So slot 0 (the text box's
   Square), slot 3 (the choice box), slot 10 (the file menu: Cross, Down, Up) and the memory-card process (`0x8005EC98`, the screen's Square wait) ALL read the word sampled by the PREVIOUS frame's `Update(0)`:
   one sample old. Real code, binary frame order, physical presses: the Cross that opens the question is read by slot 10 in the next frame (P+1); the earliest answering press the choice box takes is P+19 (**19 samples**).
2. **[code][sim]** The port gives slot 0 and slot 3 (the pass) the words of the tick BEFORE (`_squareOfTick[tick-1]`, `_choicePadOfTick[tick-1]`, last tick of the previous frame at tick 0) = the binary. It gives the screen the words of
   ITS OWN tick (`_choicePadOfTick[tick]`, `AlundraWorldProxy.cs:2286`): one sample too young for Cross, Down, Up AND Square. Counts: before f3c 17 (model A and the f3c annex), after f3c 18 (measured in the real proxy), binary 19 (real code).
3. **Exact fix: ONE line of production code.** `var (screenPressed, screenInterval) = tick == 0 ? _choicePadOfLastTick : _choicePadOfTick[tick - 1];` (the very tuple the pass reads two statements above). No change to the director,
   to `Pass`, to the text box (F2-R1) or to the choice box (F3A-R3): the pad count becomes 19 = the binary, for Cross, Down, Up and Square at once.
4. **[sim]** Full `Alundra.Tests` (Debug) with that line alone: 13 red of 3034, all of them proxy-level tests that assert something right after the press frame: the 12 cells of
   `AlundraSaveScreenTickOrderTests.TheAnswer_IsTakenInTheTickOfTheClosePass_NotOneTickLater` and `AlundraSaveBookEndToEndTests.RealMap17_TheBook_Oui_...`. 3021 stay green (every arc, the oracle tests, the choice and text box tests,
   the director-level and presenter tests, the book tests). Nothing else moves. With the proposed test changes: **3048/3048** (3034 + 14 new), six traces identical (hero traces modulo CRLF only, as before).
5. No genuinely open product question: the binary decides, the author's rule (a proven defect is corrected) applies. One adjacent observation (the inventories) is NOT part of this slice, see section 10.

## 1. Binary facts [bin]

| Fact | Address |
|---|---|
| Steady main loop: `Update(1)` is a one-shot at `0x8002C3E4`; the loop is `0x8002AC2C`, **`RenderScene` `jal 0x8002BD60` at `0x8002C3FC`**, **`Update(0)` `jal 0x8002BAEC` at `0x8002C404`**, back-branch `0x8002C45C` | main loop |
| `RenderScene`: `jal 0x8005EC98` (memory-card process, the screen's state machine) at `0x8002BE54`, `jal 0x80048054` (dispatcher, slots 0..12 ascending) at `0x8002BE5C`, `jal 0x80044C5C` at `0x8002BE64` | `0x8002BD60` |
| `Update(0)`: its FIRST call is the pad sampler `jal 0x8002E38C` at `0x8002BB5C` = BIOS PadRead `0x8008A688`, then the real pad update `0x8002E250` on block `0x80126F18` (hold `+0x10`, pressed `0x80126F2A`, released `+0x14`, by interval `0x80126F2E`) ; then `UpdateWorld` (`RunMapEvents`, `UpdateEntities`), the inventory trigger `0x80055570` | `0x8002BAEC` |
| Other callers of the sampler: `0x8002C490` (after the loop), `0x8004F2D8` (a blocking wait `0x8004F2BC`, no `jal` caller) : not in the frame loop | xref |
| The only callers of `0x8005EC98` and of the dispatcher `0x80048054` are `0x8002BE54` and `0x8002BE5C`; the only indirect call of a slot-table entry's `+0x14` is the dispatcher's `jalr` at `0x80048160` (the other `lw 0x14(..)` + `jalr` pairs are an allocator vtable at `0x800571AC` and library/BIOS stubs above `0x80080000`) | xref, `jalr` scan |
| **Slot 0 (text box, `0x80046EF0`)**: Square pressed `0x80126F2A & 0x80` at `0x800459FC` (`0x80045988`) and `0x8004602C` (`0x80045FE0`), `lbu 0x80126F2A >> 7` at `0x80045E80` (`0x80045E60`), Square HELD `0x80126F28 & 0x80` at `0x800460B0`. The three functions are called only from the slot's update (`0x8004706C`, `0x8004708C`, `0x8004709C`) | reach/xref |
| **Slot 3 (choice box)**: Cross pressed `0x80126F2A & 0x40` at `0x8004FFDC`; Left `0x8000` / Right `0x2000` by interval `0x80126F2E` at `0x8005010C` / `0x80050144` | `0x8004FFA8` |
| **Slot 10 (file menu, update `0x80058F24`)**: Cross `0x80126F2E & 0x40` at `0x800594CC` (loaded in `$a0`, reused for Down `0x4000` at `0x8005954C`), Up `0x1000` at `0x800598DC` (re-read of the same word) | `0x80058F24` |
| **Memory-card process (`0x8005EC98` -> `0x8005F458`/`0x8005ED2C`)**: Square pressed `0x80126F2A & 0x80` at `0x800601D0` (state `0x3F5`, the port's `StateWaitSquare`), also `0x8005F360` (state `0x3F0`), `0x8005FBB8` (`0x3EF`); Circle `& 0x20` at `0x8005FE6C` (`0x3F2`): none of the other states exist in the port | `0x8005EC98` |

So: nothing that decides the screen's timing reads the pad in `Update(0)`. The binary has no reader that is younger than one sample on this path.

## 2. Execution of the real code [emu]

Scripts (this directory): `frameorder.py` (slots 3 and 10, real pad sampler `0x8002E38C` with a hooked BIOS `PadRead` that returns a PHYSICAL schedule; the frame is exactly `dispatcher` then `sampler`),
`frameorder_mcard.py` (memory-card process in state `0x3F5`), `frameorder_box.py` (slot 0's Square reader `0x80045E60`), `cmp_binary_model.py`.

- Slot 3 + slot 10 (`python frameorder.py`): physical Cross at frame P opens the question in frame **P+1** (slot 10 reads the sample taken at the end of frame P). Answer Cross physically at P+14 .. P+18: never accepted;
  at **P+19**: result written and accepted in frame P+38; P+20 -> P+39, ... (19 samples between the opening press and the earliest accepted press).
- Memory-card process, state `0x3F5` (`python frameorder_mcard.py`): physical Square at frame P: the Square branch (`jal 0x80050C64`) runs in frame **P+1**.
- Slot 0 Square reader (`python frameorder_box.py`): physical Square at frame P: close sound 7 in frame **P+1**.
- `python cmp_binary_model.py`: 62 press schedules (OUI: Cross at P+q; NON: Right at P+q, Cross at P+q+1; q = 15..45): acceptance frame of the real code against the three host modes of the port. **B1 (the fix): 0 mismatches of 62.**
  B0 (today): 2 mismatches (the two earliest presses, accepted 1 sample too early). A (before f3c): 59.
  Late presses give the same count in B0 and B1: the age only matters for the presses made in the 18 samples that follow the opening press (the box slides in): B0 accepts a press the binary drops.

## 3. The port today [code]

Per logic tick (`AlundraWorldProxy.Update`): entity scripts (engine loop, before the proxy) -> **pad pass** (`:2203-2206`: `TickPad.Update`, records `_squareOfTick`, `_choicePadOfTick` = (JustPressed, ByInterval) of THIS tick) ->
inventories etc. -> loop 2 per tick: `Pass(squareHeld, squarePressed, choicePressed, choiceInterval)` with `tick == 0 ? ...LastTick : ...OfTick[tick - 1]` (`:2267-2270`: **lag 1**) -> text box and choice presenters -> **screen tick**
(`:2286-2287`: `_choicePadOfTick[tick]`, **lag 0**) -> gate -> map events. `_squareOfLastTick`/`_choicePadOfLastTick` are saved from the last tick of the frame at `:2319-2320`.
The director: `Tick(uint, uint)` `AlundraSaveScreenDirector.cs:437` stores `_padPressed` (Square read at `:557`, `StateWaitSquare`) and `_padInterval` (`RunPickerInput` `:965-:967`: Cross, Down, Up); `Tick()` `:425` (hosts, reads `TickPad`).
No other pad read in the screen files. Port tick k's "pad k" = the sample the binary takes in `Update(0)` of frame k; port reads "pad k-1" at tick k = the binary's `RenderScene` of frame k.

## 4. The fix

`AlundraWorldProxy.cs:2286`: `var (screenPressed, screenInterval) = tick == 0 ? _choicePadOfLastTick : _choicePadOfTick[tick - 1];` (or reuse `choicePressed, choiceInterval` computed at `:2269`). Comments: field summaries `:159`, `:162`, the F3C comment `:2283`.
`AlundraSaveScreenDirector.cs`: XML docs of `Tick()` (`:419-424`) and `Tick(uint, uint)` (`:434-436`) say "THIS tick": reword. Behaviour of the director: unchanged. Which reads move: Cross/Down/Up (by interval) and Square (pressed) of the save screen, each one tick later
(relative to the press); nothing else.

## 5. Model validated, values written in advance [model][sim]

`padage_model.py` = `hostmodel.py` (ports of `AlundraChoiceBox` and the hook) + the host pad (`choice_model.PadWords` = binary `0x8002E250`) + three host modes (A before f3c, B0 today, B1 the fix), frames of k ticks,
per-tick pad words, the screen reduced to its question. Validated against **today's pins and the real proxy**: `validate_model.py` compares 14 grid scenarios x 3 variants (today old shape, today new shape, fixed new shape)
with the numbers measured by the real proxy in the scratch copy (`probe-out/padage-probe-base.txt`, `padage-probe-fix.txt`): **42/42 equal**; it also reproduces the f3c table (A: 37/37/20/20/20/21, 19/19/11/11/11/11; B0: 37/37/20/19/19/20, 19/18/10/10/10/10).
`padage_sweep.py`: earliest real-pad answer, after the opening press: A 17, B0 18, **B1 19** (OUI); NON (Right then Cross): Right at 17/18/**19**.

**Grid of `TheAnswer_IsTakenInTheTickOfTheClosePass_NotOneTickLater`: NO VALUE MOVES.** With the new shape (`Frame(1, Cross); Assert.False(pending); Frame(1); Assert.True(pending)`, the hook counted from the OPENER) every InlineData keeps its value:
`(1,0,0)=37 (1,1,1)=37 (1,17,0)=20 (1,18,0)=19 (1,26,0)=19 (1,26,1)=20 (2,0,0)=19 (2,2,1)=18 (2,18,0)=10 (2,26,0)=10 (2,26,1)=10 (2,28,0)=10`; also measured `(3,3,1)=12 (3,0,0)=13`. (Old shape on the fixed proxy: `Assert.True(pending)` is red,
which is the 12 reds of section 0.4.) The director-level pins (`AnswerAndClose` 55/56, `Carousel_Closing` 37, `Cross_AsksOuiNon` 37, `DownDuringTheQuestion` 19, `Question_TheDialogueScreenGoesOverTheSaveScreen` 38) do not move: the test helpers feed the director
by hand (the words they choose), the proxy does the aging.

**End-to-end book test (`AlundraSaveBookEndToEndTests.RealMap17_...`)**: only the assertion after `Press(proxy, Cross)` (`:178-179`) moves: one `Frames(proxy, 1)` before `Assert.True(IsAwaitingChoice)`. Observations (budgets unchanged, nothing pinned):
today 62 / 139 / 116 / **38** / 42 frames, with the fix 62 / 139 / 116 / **39** / 42 (the Square that ends the screen is read one tick later). Budgets 200 / 300 / 400 / 200 / 300.

**New tests (14), values measured by the real proxy** (`probe-tests.cs.txt`, `probe-out/`), in `AlundraSaveScreenTickOrderTests.cs`:
- `TheCross_IsReadByTheScreenOneTickAfterItsPress(ticksPerFrame, openedInTheCrossFrame)`: (1, false) (2, true) (3, true).
- `ThePadAnswer_IsTakenFromTheNineteenthSampleAfterTheOpeningPress(non, q, expectedTaken, expectedSaved)`, real pad, 1 tick per frame, ticks from the opening press: OUI q=18 -> (-1, false); q=19 -> (38, true); q=20 -> (39, true);
  NON (Right at q, Cross at q+1) q=18 -> (38, true) [the Right is dropped, the Cross at 19 answers OUI]; q=19 -> (39, false); q=20 -> (40, false). Today (measured): OUI q=18 -> 37; NON q=17 -> 37 (OUI), q=18 -> 38 (NON).
- `TheSquareOfTheWait_IsReadOneTickAfterItsPress(ticksPerFrame, expectedFrames)`: frames from the press frame until the state leaves `0x3F5`: (1, 20) (2, 10) (3, 6). Today: 19 / 9 / 6 (the 3-tick case does not discriminate, harmless).
- `TheDownOfThePicker_IsReadOneTickAfterItsPress(ticksPerFrame, selectionAfterThePressFrame)`: (1, 0) (2, 1); selection 1 after the next frame. Today: 1 / 1.
The old `TwoTickFrame_CrossHeldFromItsFirstTick_StillOpensTheQuestion` and `MultiTickFrame_SquareHeldFromItsFirstTick_LeavesTheWaitSquareState` stay green and unchanged.
`patch-proposal.diff.txt` = the complete proposed change of the four files (production comments and line, two test files), applied and green in the scratch copy.

## 6. Closed file list (parent repository only; the engine, MGUI and the analyser are not touched; no export)

- Production: `Alundra/Scripts/AlundraWorldProxy.cs` (one line + comments `:159`, `:162`, `:2283`), `Alundra/Scripts/AlundraSaveScreenDirector.cs` (XML docs only, `:419-424`, `:434-436`).
- Tests: `Alundra.Tests/AlundraSaveScreenTickOrderTests.cs` (`FramesToTheAnswer` shape, class summary, 14 new tests), `Alundra.Tests/AlundraSaveBookEndToEndTests.cs` (one line at `:178`).
  Not touched (verified green with the fix alone): `AlundraSaveScreenDirectorTests.cs`, `Scripts/AlundraSaveScreenPresenterTests.cs`, `AlundraSaveBookTests.cs`, `AlundraSaveBookDispatchTests.cs`, all oracle, arc and trace tests.
- Documents: `docs/plan-e19-opcodes.md` (D-E19-99 `:334-335`, the O-E19-75 row `:10936`, the f3c lines `:5604` and `:5649`, a new section "E19.f3d" after f3c, before `###### E19.f4` `:5660`), a new annex `docs/plan-e19-pad-age-annexe/`
  (this `notes.md`, a `verify.md`, `padage_model.py`, `validate_model.py`, `padage_sweep.py`, `frameorder*.py`, `cmp_binary_model.py`, `padrefs.txt`, `probe-tests.cs.txt`, `patch-proposal.diff.txt`, the two probe outputs),
  a new ADR of the parent (next free number, 0047 at HEAD, to recheck before the commit) and its row in `docs/decisions/README.md`; ADR-0040: status line and last consequence bullet point to the new ADR (`Accepted; the pad-age gap of its last consequence closed by ADR-0047`, as ADR-0006/0010 do).
  Optional one-line pointers in `docs/plan-e19-save-order-annexe/notes.md` section 7.3 and `verify.md` section 4.4 ("closed by E19.f3d").
- Line endings: CRLF in `AlundraWorldProxy.cs`, `AlundraSaveScreenDirector.cs`, `AlundraSaveScreenTickOrderTests.cs`, `docs/decisions/README.md`; LF in `AlundraSaveBookEndToEndTests.cs`, `docs/plan-e19-opcodes.md`, ADR-0040. Use the Edit tool or a byte-safe script, never `sed -i` (it converts CRLF to LF).

## 7. Acceptance (for the plan)

1. New tests first: red on today's code. Measured on the filtered classes (96 tests): **18 red** (12 grid cells, `TheCross` tpf 1, `ThePadAnswer` OUI 18 and NON 18, `TheSquareOfTheWait` tpf 1 and 2, `TheDownOfThePicker` tpf 1), then green with the fix (96/96).
2. Mutants of the screen's tuple (`apply_fix.py`, `run_mutants.sh`): each must redden at least one test. Measured: M2 tick 0 reads `default` (22 red, E2E included); M3 ticks after the first read their own words, tick 0 the last tick (8 red: the 2- and 3-tick cases);
   M4 pressed aged but interval of the tick (16 red); M5 interval aged but pressed of the tick (2 red: only the Square tests); today's code (18 red).
3. `Alundra.Tests` Release then Debug (Debug last): 3048/3048 expected at `bbea80a` (3034 + 14), measured Debug in the scratch copy; `cmp` of `Alundra.dll` Debug and the project; six traces unchanged (hero traces equal modulo CRLF, `intro-*` byte-identical); no export.
4. Stops: any pin that moves outside the 12 cells' shape and the E2E assertion; a closed-list violation; a red text box oracle test (F2-R1) or choice test (F3A-R3).
5. Recipe F3D (author): the save book end to end (answer, picker, Cross, OUI/NON, Square at the end). Only a Cross pressed twice within 18 samples (0.36 s) can show the difference (the second press is now dropped).
6. Rollback: revert the commit.

## 8. Risks

- **Text box Square (F2-R1) and choice (F3A-R3) timing**: not touched. `Pass`, `_squareOfTick`, `_choicePadOfTick[tick-1]` unchanged; the fix only reads an already recorded tuple. Evidence: 3021 green with the fix alone, 3048 with the new tests (K5, the book, sailor 12, A17, all oracle tests).
  The binary agrees with both (section 1, 2): they read the same sample as the screen will.
- A frame of several ticks: tick 0 reads `_choicePadOfLastTick` (kept when a frame has zero ticks, saved only if `ticksThisFrame > 0`, `:2317-2320`); the mutant that forgets it (M2) is red.
- Director-level helpers keep feeding the words of the tick they read (lag 0) while the proxy ages them: documented in the director docs; the age is covered by proxy-level tests. Rejected alternative: aging the helpers would shift about a hundred pins by one tick for no extra coverage.
- Visible effect: the save screen reacts one tick (20 ms) later to Cross, Up, Down, Square; a second Cross within 18 samples of the opening press is dropped (binary behaviour).
- The Cross that opens the question and the Square wait are the only pad actions of the screen; no consumer reads the screen's state in a way that depends on the tick of the press (inventory trigger unchanged).
- Number collisions (ADR numbers, R-BR) and CRLF (section 6).

## 9. Questions

None genuinely open: the slice can run without the author. (If the author wants the helpers of the director-level tests aged, that is a style choice, not a blocker; the proposal keeps them.)

## 10. Adjacent observation, not in this slice [bin] + [hyp]

The main inventory's per-frame update `0x80056598` is installed as the slot 6 update by the open routine (`0x80055554-0x80055564`) and reads the by-interval word at `0x800567C8`, `0x8005680C`, `0x80056870`, `0x800568D4`: it runs in the dispatcher,
so one sample old, while the inventory TRIGGER `0x80055570` (Start held, `0x800555B8`) runs in `Update(0)` after the sampler (age 0). The port's `AlundraInventoryDirector`/`AlundraSubInventoryDirector` read `TickPad` of the current tick for both
(**[hyp]** same class of gap for the inventory's own keys; not analysed further, not touched). If the author wants it, it is a separate slice (an open point to register, no pause).

## 11. Files in this directory

`notes.md`; binary: `lib.py`, `emu.py`, `drive.py` (copies), `slotorder*.py` (copies of the f3c proof), `padrefs.py`/`padrefs.txt` (every load of the pad block), `reach.py`, `xref.py`, `cs.py`, `wordref.py`, `frameorder.py`, `frameorder_mcard.py`, `frameorder_box.py`;
model: `hostmodel.py` (copy), `padage_model.py`, `padage_sweep.py`, `cmp_binary_model.py`, `validate_model.py`; simulation: `setup.cmd`, `repo/` (physical copy with the proposed change applied), `apply_fix.py`, `make_test_patch.py`, `run_mutants.sh`,
`probe-tests.cs.txt`, `probe-out/`, `patch-proposal.diff.txt`, logs `build-*.log`, `test-*.log`, `mut-*.log`, `results-*`.
