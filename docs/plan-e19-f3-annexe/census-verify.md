# E19.f3 counter-check of the "census" report (adversarial, read-only)

Repo `D:\development\repo\alundra-casaengine-project-converter`, branch `chantier/e19-suite`. HEAD seen: `21fee93` (was `db8d61c` at the census, `8c3bcfb` in the report):
`git diff --name-only db8d61c HEAD` lists only `docs/...` files, so every DLL/test line number below is read at a HEAD where no `.cs` file changed.
Nothing built, run, edited, staged or committed inside the repository; everything I ran is in this folder
(`C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\f3-census-verify\`).
Tags: [grep]/[code] read in the repo, [sim] my own run, [hand] hand derivation from `binary-notes.md` rules + host code. Verdicts: CONFIRMED / REFUTED / UNCONFIRMED.

## 0. What I ran myself

| What | Result |
|---|---|
| `dllsim.py` rerun from scratch (copy of the annex `choice_model.py`/`model.py`, byte-compared with the census copies first) | stdout byte-identical to the census copy's (`cmp`: IDENTICAL). Screen OUI/NON polled S+37/S+38, book 99/100, K5 90, map-event N+37 |
| `model/scenarios.py` regenerated in my folder | `values.md` and `values.json` byte-identical to `docs/plan-e19-f3-annexe/` |
| C# `AlundraTickPad` (copy of `Alundra/Scripts/AlundraTickPad.cs`, `diff` identical to the repo file) vs `PadWords` | **2090 cases** (my own seed 777, my own generator: random segments of 1..60 passes over a 14-word vocabulary + 90 boundary sweeps of holds 1..45 passes), `cmp out_cs.txt out_py.txt`: byte-identical, 0 mismatches |
| V1..V7 raw pad words driven through the C# pad, outputs fed to `ChoiceBox.render` | closed tick and sounds equal to `values.json` summaries for all 7 (`padcheck17/v17.py`: ALL OK). V8/V9 rest on the f2a text box model (annex says the binary check did not cover them): not re-run on the binary |
| 101 sites of `0x44` decoded by me from `alundra-project/Maps/**/*.events.json` at the `sites44_detail.tsv` offsets (`modes.py`) | byte at the offset is `0x44` for 101/101; opener `0x0D` x56 and `0x5C` x45, **control mode operand = 1 for 101/101**; successor `0x51` for 101/101 |
| Independent host-order sim of placements A and B (`placement_b.py`, host order written from `AlundraWorldProxy.Update` :2104-2202) | see section 2 |

## 1. Counts (all [grep], whole repo `*.cs`)

| Claim | Verdict | Evidence |
|---|---|---|
| `SelectChoiceForTests`: 25 call sites in 9 test files | CONFIRMED | 26 hits minus the definition (`AlundraDialogueDirector.cs:638`); files: Day3SceneArc 1, OpcodeDispatch 2, OpcodesProduction 1, BoxOrder 1, SaveBook 9, SaveBookEndToEnd 2, TextBoxWiring 1 (`:197`), SaveScreenDirector 7, Scripts/SaveScreenPresenter 1 |
| direct `OpenChoice` in tests: 9 calls in 6 files | CONFIRMED | BoxOrder :324; FramePass :247; PresenterWiring :180, :259 (`:139` is a method name); SaveBook :233; SaveScreenDirector :616, :629; TextBoxWiring :191, :211 |
| `ChoicesForTests` 6 readers | CONFIRMED | BoxOrder :352, OpcodeDispatch :443, OpcodesProduction :138, SaveBookE2E :166, SaveBook :151, SaveScreenDirector :555 (`:634` is the definition) |
| 8 callers of `AnswerAndClose`, 8 of `RunToTheQuestion` | CONFIRMED | `:163,:219,:300,:360,:652,:665,:677,:695`; `:197,:312,:369,:389,:406,:420,:437,:461` |
| the director is the only implementer of `IAlundraDialogueDirector` | CONFIRMED | only `AlundraDialogueDirector.cs:111`; the other hits are `IEntityWorldContext` doubles that return it |
| "10 direct callers of `Pass(bool,bool)`" (report 1.1) | REFUTED (internal slip) | the report's own 3.5 lists 12 test lines and I count 12 (+ the harness `IntroTraceHarnessTests.cs:597` = 13). No consequence: the signature is kept |

## 2. DLL side

| Claim | Verdict | Evidence |
|---|---|---|
| By-interval word exists, nothing to add to the pad classes; replay 0 mismatch | CONFIRMED | section 0 (2090 cases + V1..V7). **Line refs are off**: property `AlundraTickPad.cs:49`, `Update` `:53-97` (not `:63-68`, `:71-106`). First repeat = 21 passes after the press pass (edge at pass 0, repeats from pass 21 on): reproduced |
| Placement (A), top of the first loop before `TickPad.Update` (:2106), reads the previous tick's pad with no new list | CONFIRMED | first loop :2104-2144, `TickPad.Update` :2106, save screen `Tick()` :2117, second loop :2173-2202, box pass :2176, text box presenter :2180. `TickPad` is only written in that loop, so reading it at the top is the previous tick's pad, for catch-up frames too, and a 0-tick frame runs no pass |
| (A) table cells (map event/pending trigger N+37; entity script first pass N, active N+18, consumer N+37; screen first pass N+1, active N+19, N+37 same tick) | CONFIRMED | [hand] + [sim] (`placement_b.py`): screen A idx0 polled S+37 (active S+19), idx1 S+38; entity A idx0 99 = N+37, idx1 100; map event A N+37/N+38 |
| (B) table, save screen cell "first pass N (1 early), consumer sees N+38 (1 late)" | REFUTED for the earliest answer | [sim] screen B idx0: first pass S, active S+18, Cross S+18, closed S+36, **polled S+37** (idx1 S+38 -> NON S+38 is idx1). The "1 late" is only true relative to a real press (latency from the press to the poll is +1 vs the binary) or if the Cross is aligned on the binary's N+19 pass. Does not change the recommendation; changes the cell, and `AnswerAndClose` arithmetic would stay `37/38` under (B) too (the helper order differs: Dialogue.Tick() AFTER Director.Tick()). Entity and map-event cells for (B) agree with the report (N+37) |
| (A) "keeps the binary order slot 3 then slot 10" | CONFIRMED but incomplete | (A) also runs the choice pass BEFORE the text box pass of the same tick (second loop :2176), while the binary runs slot 0 (box) then slot 3 (choice). The two machines share no state (binary-notes 8, binary-verify 8), so the only observable difference is the order of two `PlaySfx` calls inside one tick; not mentioned in the report |
| Catch-up frames: all choice passes before the map-event passes (A) | CONFIRMED | follows from the two loops |
| Hero locked in the 3 callers, Cross cannot jump | CONFIRMED | `AlundraPlayerManager.cs:211` (`InputBlockedMask` = ControlLocked|MessageBox|ForcedSequence, :94 of `AlundraGameState.cs`) returns before the jump read (:440); `GameplayBlockedMask` = MenuOpen|Unused40 (:90) freezes the hero's whole entity update (`AlundraEntityScriptProxy.cs` ~:1075-1115); `AlundraSaveBook.cs:186` sets ControlLocked; **101/101 sites are mode 1 (my own decode)**. Caveat: `DebugIgnoreControlLock` (debug only) bypasses :211 |
| Sounds 1-5 through `_soundPlayer?.PlaySfx` (:445); ids 1-5 already played by the inventory | CONFIRMED | director :445; `AlundraInventoryDirector.cs:513 (4), :636-660 (1), :760 (5), :870-964 (3, 2)`; `sfx_0001..0005.wav` exist; `AttachToWorld` stores `_soundPlayer` (:184) before the null-presenter return (:186) |
| Interpreter zeroes `Parameters[1]` after a non-zero return, so a re-executed 0x44 asks again, and continues in the same tick | PARTIALLY REFUTED (mechanism) | `AlundraEventProgramRunner.cs:461` does `state.Parameters[1] = 0; state.CodeIndex += result;` (same-tick continuation: CONFIRMED), but **the DLL's 0x44 never reads `Parameters[1]`** (:950-989). It asks again because `!choiceDirector.IsAwaitingChoice` (:968) after `TakeChoiceResult` cleared `_awaitingChoice` (:533). So `IsAwaitingChoice` must stay true until the result is TAKEN (the report says so in 1.1) and that, not :461, carries the "asks again" behaviour |
| Line refs of the code to change | CONFIRMED | director `:149-155, :220-267, :419-433, :501-557, :569-589, :594-638`; book `:241, :285, :295-317, :409-412`; screen `:357-360, :976-1036, :1245`; proxy `:153, :1228, :1261, :1437, :2849`; interface `:23-91` (Tick `:74-78`); file lengths 639/434/1283 |

## 3. Tests that move (row by row; [hand] = derived from the host code + annex rules, [sim] = my rerun)

| Row | Verdict | Notes |
|---|---|---|
| K5 `BoxOrderTests.cs:324-331`: outputs unchanged by construction with `PadForFrame = f => f == 70 ? Cross : 0`; poll at 90 | CONFIRMED | opener 30 (entity script before the proxy: first pass 30, active 48), TickPad edge written in frame 70, seen by the pass of 71, closed 89 (written in the first loop of 89), entity script of 90 polls: `host_montage_k5` -> `polled (90,1)`, sounds (30,4),(71,5),(71,2). T=90 so (56,90,108)/109 hold. Frame-60 asserts unchanged. Note the montage runs the entity script only when `GameplayBlockedMask == 0` (`AlundraDialogueBoxMontage.cs:181`): mode 1 is MessageBox, not in that mask, as today |
| SaveBookTests `:155` (WaitScreen 124 -> 160; box released at 134 before the capture; latch at 99 < typing end 115 so the box timeline is unchanged) | CONFIRMED | [sim] book: opener 62, active 80, closed 98, OUI polled 99; 99+61 = 160. Box E=115, T=116, R=134 do not depend on the choice |
| SaveBookTests `:199` NON polled at 100; `Tick(71)` becomes non-binding | CONFIRMED | [sim] polled 100; release 134 < 100+71 |
| SaveBookTests `:350/:372/:392/:408/:422/:463` -> `Tick(37 + Wait)`; `:439` loop | CONFIRMED | 62 + 37 = 99, +61 = 160 = 37+Wait calls after the opener call; `:439` is a `do/while < 500` (37 + ~100 inside) |
| `AnswerAndClose` `SaveScreenDirectorTests.cs:116-125` -> `Tick(37+18)` OUI / `Tick(38+18)` NON, helper runs `Dialogue.Tick()` before `TickPad.Update`+`Director.Tick()` | CONFIRMED | [sim] screen A polled S+37/S+38; 8 callers read no PickWait tick count (`TicksIn` only for ExamineWait/ReadWait/Write/Saved/CloseWait/EndWait/End, `:206-212`, `:235`) |
| `:523` Carousel closing `Answer(0); Tick(37)` | CONFIRMED | the 37th tick polls and arms (`PollAnswer` then `ArmPickerClose` in the same `Director.Tick`, :933-935) |
| `:563`, `Presenter:153`, `TextBoxWiring:197`, `:191/:211`, `FramePass:247-248`, `PresenterWiring:180/:259`, the two `CloseStandaloneChoice` tests (R / R+V) | CONFIRMED | these assert the engine window (`ScreenForTests` pushed/removed, `rePointedPresenter.Choices`, `recorder.Pushed.Count > 0`) or the standalone route; they only stay green if `OpenChoice` keeps calling `_presenter.ShowChoices` (design says it does not) |
| A17 loop deleted, budget 2500, end ~1194 | UNCONFIRMED (arithmetic only) | `plan-e19-f2a-valeurs.md:511-512` V-131 gives end 1158 (a prediction, "modèle") +36 = ~1194 < 2500 is arithmetic, not measured; the loop must go (an `Assert.True(AnswerChoiceForTests(0))` every frame would fail after the Cross is taken: it returns false then) |
| Sailor 12 `OpcodesProductionTests.cs:98-167`, budget 400 | UNCONFIRMED (arithmetic only) | follow-up opens ~36 frames later (opener frame in `RunFrame`, first pass same frame, active +18, written +36, polled +37 vs +1 today); end ~320 -> ~356 from `second + 204` of O20 test 3; slack ~44 is plausible, not measured (harness order is R, box, C, not E, box, M) |
| E2E `:167/:180`, budgets 300/400 | UNCONFIRMED (arithmetic only) | 37+61+60 = 158 < 300; after the screen's Cross: 37 + ~80 (`WaitLimit` 0x13 per state) < 400 |
| Dispatch `:450/:472` use `ForceChoiceResultForTests` | CONFIRMED (they run no pass: `RunOneScriptCall` only) | the alternative (37 `Dialogue.Tick()` calls, the test already attaches `new AlundraGameState()` through `AttachToWorld`) needs no extra hook: a plan choice |
| U rows: ChoicesForTests readers, IsAwaitingChoice readers, HasPresenter sites (`ArcSupport:222`, `E2E:222`, `ScreenDirector:598`, `PresenterWiring:175/:234/:240/:255`), RunToTheQuestion, SaveBook `:233`/`:310`, K5 oracle, `Pass(bool,bool)` callers | CONFIRMED | all re-read; `:233` needs `CancelChoice` to stop the machine, `:310` needs `Open` to clear the machine (as the report's D-F3-4) |
| `OneFrameWithTheDialogueButton` (`AlundraArcSupport.cs:496-501`) gains an alternate Cross hold while a choice awaits | CONFIRMED as design | provider `:188-192`; the other arcs that use the helper reach no `0x44` (they would stall today: nobody answers); one that did would now answer OUI |
| Harness block `IntroTraceHarnessTests.cs:594-600`: "call the choice pass with the mirror's record (read before `Update`, the same lag)" | AMBIGUOUS, probably wrong | the harness updates `_dialoguePadMirror` FIRST and then calls `Pass(...)` with the mirror's values (:595-599), the pad being the one the previous frame's callback wrote. Reading the choice record before `Update` gives one more frame of lag than the Square. Immaterial for hook-armed tests (the hook replaces the record); material for any real-pad harness test. Use the same order as the Square |

## 4. Missed or wrong in the "moves" list

1. **REFUTED: the three `O20_Sailor12Of389_*` oracle tests (`AlundraTextBoxOracleTests.cs:449, :463, :483`) do not move.** `Production(...)` is an `OracleHost` over `AlundraTextBoxOracle`, a separate class "written from the rules ... NEVER from `AlundraDialogueDirector`" (`AlundraTextBoxOracle.cs:15`); `Sailor12` models 0x44 as `script.Hold(1)` inside the oracle. Nothing in them touches the director or the DLL, so f3 leaves them green and untouched (114 / 319 stay). They become a stale placeholder (the f2a `L = G+1`). Writing a choice oracle and a DLL-vs-oracle sailor-12 comparison is a plan option (`AlundraDialogueBoxOracleComparisonTests.cs` has no choice scenario: grep for choice/0x44/Hold finds nothing), not a forced movement.
2. Tests that drive the director through `Tick()` (all of `DialogueBoxPassDriver`, `AlundraSaveBookTests`, `SaveScreenDirectorTests` BookTick): `Tick()` gains a choice pass and a derived pad from `LastPadState`; with no choice open it must stay a no-op (no test movement). `ResetForTests` must also clear that derived pad (`SaveGameDirectorTestSupport.ResetSingletons`, :116, calls it).
3. `SaveScreenDirectorTests.cs:709-740` (`WorldChange_WhileTheScreensChoiceWaits_*`) and `:744-780` are U only if `InstallForMapEntry` resets the machine (`IsAwaitingChoice` false after `InstallDialogueSystems`): worth a named pin; also `Assert.Empty(newView.Pushed.Except(newView.Removed))` then needs the new choice screen's removal on a world change.
4. The sound path: `montage.Sounds` (`PassStampedSoundPlayer`) and the screen tests' `RecordingSound` are separate players; nobody asserts a full sound list in a test that opens a choice (`SaveScreenDirectorTests:158` is before any choice), so adding sounds 4/5/2/3/1 moves no assertion. The harness tests attach `new DialogueService()` with no sound player.
5. The report's "not proven: pad reaches the DLL under two stacked modals" has two precedents the report does not combine: the text box screen is modal (`FakeTextBoxScreen.IsModal => true`) and its Square comes from the same `TickPad` per-tick path, and the save carousel reads Cross/Up/Down under a modal screen. Still not shown for two stacked screens.

## 5. Summary of corrections

- REFUTED: O20 tests "move" (they cannot: oracle-only). 
- REFUTED (rejected alternative only): (B) save-screen cell N+38; the earliest-answer value is S+37 with the first pass at S.
- Mis-attributed: `:461` is not what makes a re-executed 0x44 ask again in the DLL; `IsAwaitingChoice` is.
- Slip: `Pass(bool,bool)` direct callers are 12 (13 with the harness), not 10; `AlundraTickPad.cs` lines are `:49` / `:53-97`.
- Incomplete: (A) inverts slot 0/slot 3 order inside a tick; harness record order ("before Update") ambiguous.
- Everything else CONFIRMED; the three budgets (A17 ~1194, sailor 12 slack ~44, E2E 158/117) are arithmetic from annex figures, UNCONFIRMED by a run.
