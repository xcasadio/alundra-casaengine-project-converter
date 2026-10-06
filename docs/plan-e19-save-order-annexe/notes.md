# D-E19-93 discovery: the save screen's tick after the dialogue pass (slot 10 after slot 3), read-only

Repository `chantier/e19-suite`, HEAD `79bbe5b`. Nothing built, run, exported, edited, staged or committed inside the repository (its `alundra-project/Alundra.dll`
is still the 07:02 one; `git status` unchanged: ` m CasaEngineMonogame`, `? alundra-datas-analyser`, the author's `Program.cs`). Everything lives in this directory;
the scratch copy `repo/` is a PHYSICAL copy (robocopy, `alundra-project` included, no junction): its build deploys `Alundra.dll` into the copy only.
`CasaEngine.Launcher/Program.cs` untouched. Nothing read from `scratchpad/bak/`.

Tags: **[bin]** read in the disassembly (`lib.py`, capstone) ; **[emu]** executed: the REAL binary code in the MIPS interpreter (`slotorder.py`, `slotorder2.py`) ;
**[code]** read in the repository at HEAD (file:line) ; **[model]** `hostmodel.py` ; **[sim]** run on the scratch copy of HEAD patched ; **[hyp]** hypothesis.

## 0. Summary

1. The binary frame is `RenderScene` then `Update(0)`. `RenderScene` calls `UpdateMemoryCardProcess` (0x8005EC98, the screen's state machine) at 0x8002BE54, then the
   dispatcher 0x80048054 at 0x8002BE5C: slots 0..12 ASCENDING (loop 0x8004813C-0x80048174), then `DisplayUserInterface` (0x80044C5C) at 0x8002BE64. `Update(0)`
   (0x8002BAEC): pad update 0x8002E38C, `UpdateWorld` 0x8002E058 = `RunMapEvents`, `UpdateEntities` (all entity scripts), `UpdateEffects`, then the inventory trigger 0x80055570.
   So slot 3 (choice) runs before slot 10 (file menu) in the same frame, both before the map events and the entity scripts of that frame.
2. Slot 10 opens the OUI/NON question (opener 0x80050BA8 at 0x80059504) and polls its result word (0x80180124) at 0x8005912C-0x80059150. The word is written by slot 3's
   callback at C+18. **[emu]** (real code of both slots, dispatcher order): opener in frame S, first slot-3 pass S+1, interactive S+19, result written AND accepted by slot 10
   in the same frame: S+37 (OUI at the earliest), S+38 (NON), S+48 / S+49 for the late answers tried (Cross at S+30; Right S+25, Cross S+31). Section 2.
3. Today the DLL ticks the screen in the FIRST per-tick loop (`AlundraWorldProxy.cs:2189`), before the pass of the dialogue (second loop, `:2251`). Moving the screen tick (and
   its presenter) into the second loop, right after the choice presenter (`:2259`) and before the gate (`:2261`), gives exactly the binary's relation: opener S, first pass S+1,
   answer accepted in the tick of the closing pass. **[sim]** the real proxy gives the model's numbers in the two orders (section 4).
4. Values that move: ONE pinned value, `AlundraSaveScreenDirectorTests.cs:613` `DownDuringTheQuestion` **20 -> 19** (plus the order of the two test helpers, which mirror the proxy).
   Everything else is unchanged: 2857/2857 in Debug and the screen/book/choice classes in Release, with that one re-pin. The end-to-end book test observations are identical (62 / 139 / 116 / 38 / 42);
   the six traces are identical (modulo line endings, as in the baseline run).
5. One implementation trap: a frame of several ticks. The screen reads two pad words (`ButtonsJustPressed` for Square at `:541`, `ButtonsJustPressedByInterval` for Cross/Down/Up at `:951`);
   in the second loop `GameState.TickPad` only holds the LAST tick's edges. The proxy already records both words per tick (`_choicePadOfTick`, `:159`, filled `:2179`): the director needs a
   `Tick(uint, uint)` overload. A naive `Tick()` in the second loop loses a Cross pressed on the first tick of a two-tick frame (**[sim]** red).

## 1. Binary facts [bin]

Table `0x800A731C`, 13 entries of 0x1C bytes (runtime copy `0x80153028`, flags at +0, update at +0x14; names from the analyser's `StaticVariables.g_initialCallbackTable`, address comments checked):

| Slot | update (+0x14) | what |
|------|----------------|------|
| 0 | 0x80046EF0 | text box (UIManager) |
| 1 | 0x8004BEA4 | HUD |
| 2 | 0x8004F628 | `Fun_8004f628` (dialog background sprites; empty body in the analyser) |
| 3 | 0x800501FC | choice box (`DisplayMessageChoice`) |
| 4 | 0x80053328 | sub-inventory |
| 5 | 0x8004A8A8 | debug SFX menu |
| 6 | 0x80054F1C | main inventory |
| 7 | none | |
| 8 | 0x8004AFE8 | debug BGM menu |
| 9 | 0x80050EC8 | memory card message box (`Fun_80050ec8`) |
| 10 | 0x800583EC, which installs 0x80058F24 at entry +0x14 (0x80058420) | memory card file menu (the save screen's picker, its OUI/NON question) |
| 11 | 0x80051550 | debug flags menu |
| 12 | 0x8005A3E0 | name box |

- Dispatcher 0x80048054: pre-checks of GameFlags (0x800000, 0x200000, 0x400000), then `for s0 = 0x80153028, s1 = 0; s1 < 13; ++s1, s0 += 0x1C: if (flags & 1 && +0x14 != 0) jalr +0x14` (0x8004813C-0x80048174),
  then the post-process states (0x80048178-0x800481D4: state 1 = fade out when slot 6 flags are 0; state 2 = `0x80055570` inventory open when slot 4 flags are 0).
- RenderScene 0x8002BD60 (dump `f2b1-disc/oracle/dumps/renderscene.txt`): `jal 0x8005EC98` (0x8002BE54) `jal 0x80048054` (0x8002BE5C) `jal 0x80044C5C` (0x8002BE64). The analyser has the same order
  (`GraphicManager.cs:62-64`: `UpdateMemoryCardProcess`, `UpdateUserInterface`, `DisplayUserInterface`; `GameEngine.cs:225-229`: `RenderScene()` then `Update(0)`).
- Update(0) 0x8002BAEC calls in order: 0x8002E38C (pads), 0x8004E428, 0x8004E5C4, 0x8004DD30, 0x8002E058 (`UpdateWorld`: `RunMapEvents`, `UpdateEntities`, `UpdateEffects`; `GameEngine.cs:1637-1664`), 0x80055570, 0x8004B1D4.
- File menu question (slot 10, state idle: flags & 0xD == 0): `lhu 0x80126F2E & 0x40` (Cross, by-interval word) at 0x800594C8-0x800594D4; `sw zero -> 0x80180124` (0x800594E4); ETC 0x4A/0x4B
  via 0x800816D4; `jal 0x80050BA8` (0x80059504, a2 = 0x80180124); `flags := 8` (0x80059544-0x80059548). Poll: `andi flags, 8` (0x8005912C); `lw 0x80180124`; `-1; sltiu 2`
  (0x80059138-0x80059148); on 1 or 2 it arms the closing (`flags := 2`, 0x80059150). The callback that writes the word is 0x80050B98, called by slot 3's closing update 0x8004FEFC at C+18.
- Slot 10's own state machine (the DLL's `RunState`) is `UpdateMemoryCardProcess`, which runs BEFORE the dispatcher; the DLL's `RunTransition` is slots 9 and 10.

## 2. Execution of the real binary code in dispatcher order [emu] (`slotorder.py`, `slotorder2.py`)

Slot 3 (opener 0x80050BA8, updates 0x800501FC, 0x800501A4, 0x8004FFA8, 0x8004FEFC) and slot 10 (0x80058F24; its runtime entry is the table entry with update = 0x80058F24, what 0x800583EC installs; the picker's records are zeroed, the real 0x800583EC is not run) called in the dispatcher's order,
pad through the real update 0x8002E250. Stubs: libc, sound, VRAM upload, cell init, string lookup, text call 0x800472D0. After the poll accepts the word, slot 10's closing code divides by zeroed slide
steps of this stand-alone state (stopped, caught): irrelevant, the accept (flags 8 -> 2) is recorded before.

| Scenario (S = frame where slot 10's real code sees Cross) | first slot-3 pass | result written by slot 3 | accepted by slot 10 |
|---|---|---|---|
| OUI at the earliest (Cross seen S+19) | S+1 | S+37 | S+37, same frame |
| NON at the earliest (Right S+19, Cross S+20) | S+1 | S+38 | S+38, same frame |
| OUI late (Cross S+30) | S+1 | S+48 | S+48, same frame |
| NON late (Right S+25, Cross S+31) | S+1 | S+49 | S+49, same frame |

Frame 5 (S): slot 10 only (slot 3 inactive when the dispatcher passed index 3), sound 4, flags 0 -> 8. Frame 6: slot 3 `0x800501FC` (init pass). Frame 24: slot 3 at 0x8004FFA8, sounds 5, 2. Frame 42: slot 3 writes the word
(0 -> 1) and slot 10 in the same call goes 8 -> 2.

## 3. The DLL today and the target [code]

Today, per logic tick (`AlundraWorldProxy.Update`): entity scripts (engine loop, before the proxy) -> loop 1 (`:2175-2216`: `TickPad.Update`, inventories, post-process, **`AlundraSaveScreenDirector.Instance.Tick()` `:2189`**,
portrait, presenters, **`_saveScreenPresenter?.Tick()` `:2215`**) -> TryWire -> loop 2 (`:2246-2281`: `Pass` `:2251` (box, choice, name box, portrait), text-box presenter `:2255`, choice presenter `:2259`, gate `:2261`, map events `:2276`).
The screen reads `TickPad` of the CURRENT tick (lag 0), the pass reads the pad of the tick BEFORE (lag 1).

Target: screen tick and its presenter in loop 2, after `_choicePresenter?.Tick()` and before `var tickBlocked` (`:2261`): the MenuOpen it posts is read by the gate of the same tick (map events of the tick frozen, as today and as the binary: state machine
then `RunMapEvents` gated by `GameplayBlockedMask`); the screen runs even when the hero is missing or the gate closed (the old loop 1 was unconditional, the `continue` comes after).

Pad words: `_choicePadOfTick[tick]` (JustPressed, ByInterval of THIS tick): same words as the screen read in loop 1, so the picker's input timing is unchanged (lag 0 kept).

Tick relation in the model (N = tick of the opener, i.e. the screen tick that sees Cross):
- order A (today): the pass of the SAME tick is the machine's init pass (N+1 happens at tick N), interactive N+18, closed N+36, accepted by the screen at N+37 (the screen runs before the pass): the count N -> acceptance is 37 for an answer given at the earliest,
  but a press made at pass p is accepted at p+19 (one late vs the binary's p+18).
- order B (target): first pass N+1, interactive N+19, closed N+37, accepted N+37 in the same tick; a press at pass p is accepted at p+18 = the binary's.
- Same count N -> 37 / 38 for answers armed before the first interactive pass (the hook arms before N+18 in A, before N+19 in B); one tick less for answers armed later.

## 4. Model validated against today's pins [model] and measured [sim]

`hostmodel.py`: Python ports of `AlundraChoiceBox` (stage logic) and `AlundraChoiceAnswerPlan` (the hook), pad = `choice_model.PadWords` (binary-validated), two host orders. Reproduces today's pins in order A, gives the new ones in order B.

| Pin | today (A), model | today, test/real | B, model |
|---|---|---|---|
| `AnswerAndClose(OUI)`: question taken, picker ends | 37, 55 | `Tick(37 + 18)`, tight in B too (mutation `-1`: the 21 instances turn red) | 37, 55 |
| `AnswerAndClose(NON)` | 38, 56 | `Tick(38 + 18)` | 38, 56 |
| `Carousel_Closing_…` `taken` | 37 | 37 | 37 |
| `Cross_AsksOuiNon_…` `taken` | 37 | 37 | 37 |
| `DownDuringTheQuestion_…` `taken` | **20** | 20 | **19** |
| `Question_TheDialogueScreenGoesOverTheSaveScreen_…` `taken` (NON) | 38 | 38 | 38 |

Arming grid (ticks from the arming to the screen taking the answer, tick 0 = the Cross, arming after tick a; full table: `python hostmodel.py`): OUI, A and B both 37 - a for a = 0..16 and 20 at a = 17; then A stays 20, B is 19 from a = 18.
NON, A and B both 38 - a for a = 0..16 and 21 at a = 17; then A stays 21, B is 20 from a = 18. An arming before the first interactive pass is the same in both orders; a later one is one tick shorter in B.

Real proxy (`SaveOrderProbeTests`, scratch only, frames of 1 or 2 ticks, `AlundraWorldProxy.Update`, frames from the arming to `!IsQuestionPending`):

| frame, arming after tick, answer | old order | new order |
|---|---|---|
| 1 tick, 0, OUI | 37 | 37 |
| 1 tick, 1, NON | 37 | 37 |
| 1 tick, 17, OUI | 20 | 20 |
| 1 tick, 18, OUI | 20 | **19** |
| 1 tick, 26, OUI | 20 | **19** |
| 1 tick, 26, NON | 21 | **20** |
| 2 ticks, 0, OUI | 19 | 19 |
| 2 ticks, 2, NON | 19 | **18** |
| 2 ticks, 18, OUI | 11 | **10** |
| 2 ticks, 26, OUI | 11 | **10** |
| 2 ticks, 26, NON | 11 | **10** |
| 2 ticks, 28, OUI | 11 | **10** |

Old = the model's order A (1 tick) and the skew of a two-tick frame ([screen k, screen k+1, pass k, pass k+1]); new = model B, tick-exact whatever the frame grouping. Debug and Release give the same numbers.
`TwoTickFrame_CrossHeldFromItsFirstTick_StillOpensTheQuestion`: green in the old order, green with per-tick pad words, RED with a naive `Tick()` in the second loop.

## 5. Suites on the scratch copy [sim]

- Baseline HEAD: `Alundra.Tests` Debug 2857/2857 (`test-base.log`).
- Production change only (screen after the pass, helpers untouched): 2857/2857 (nothing through the real proxy moves).
- Production + helper flips (screen-test `Tick`, presenter-test `Tick`): exactly ONE red, `DownDuringTheQuestion` `Expected: 20 Actual: 19`; with `Assert.Equal(19, ...)`: 2857/2857 Debug; Release: the save/book/choice/text-box wiring classes + probe, 185/185.
- End-to-end book test (`AlundraSaveBookEndToEndTests`, instrumented `FramesUntil`): old and new order identical: choice awaits 62 (budget 200), picker after the book's OUI 139 (300), screen flow after the screen's OUI 116 (400), Square end 38 (200), second picker 42 (300).
- Six traces (`docs/hero-trace-389-{spawn,highground}-{fixedstep,freestep}.txt`, `intro-trace-389.txt`, `intro-programs-389.txt`) regenerated by the harness tests in the scratch copy: identical to the repository's (the four hero traces modulo CRLF only).

## 6. Edge cases

- Opened by the answer in the same frame: the BOOK consumes its answer in an entity script (before both loops) and calls `Screen.Start`; the screen's first state runs the same tick in both orders (unchanged; origin shift of the scripts, D-E19-64, not touched).
  The book's question box releases 19 passes after its answer (`max(typing end + 19, L + 19)`) and the screen's examine state comes >= 61 ticks after the answer: the release (`ClearControlFlags` clears MessageBox | MenuOpen) never collides with the screen's `PostMenuOpen`; if it did, order B keeps the screen's MenuOpen (pass first), order A lost it.
- Dialogue closing: the screen's `CloseStandaloneChoice` refuses while a box is open (`_box.IsActive`): the pass now releases a box before the screen looks (B). No corpus case (a screen question has no box; entities are frozen under MenuOpen).
- Several ticks per frame: new interleave [pass k, screen k, map k] per tick, tick-exact; before: [screen 0, screen 1, pass 0, map 0, pass 1, map 1]. The MenuOpen posted by screen k+1 is no longer visible to the map events of tick k (as in the binary, one frame per tick). Per-tick pad words required (section 0.5).
- Zero ticks in the frame: nothing runs in either loop.
- Blocked/transition states: the pass and now the screen run before the `continue` of `PlayerEntity == null || tickBlocked`; `AlundraWarpDirector.IsTransitionInProgress` does not gate the screen (it never did). `InstallForMapEntry` unchanged.
- First frame of a world: `TryWireSaveScreenOnce` runs between the loops: the presenter can tick from the first `Update` (it is wired before loop 2), where before it ticked from the second. No effect (idle director).
- Inventories: slots 4 and 6 sit between 3 and 10 in the binary; the DLL runs the inventory directors in loop 1, before the pass, and keeps doing so (relative order inventory tick then screen tick unchanged; the screen is never active with an inventory, J9).

## 7. Not exact, unchanged by this tranche [hyp] / pre-existing

1. The state machine half of the screen (`RunState` = `UpdateMemoryCardProcess`, before the dispatcher in the binary) now runs after the pass too (the whole `Tick` moves). No cross-read with the pass (it touches `PlayerControlFlags`, the HUD director, the messages, the save service). Splitting it would be unobservable.
2. The name box and the portrait (slot 12, after slot 10 in the binary) stay inside `Pass`, before the screen.
3. Pad lag: the screen reads the pad of the current tick (lag 0), the pass the tick before (lag 1); the binary's slots 3 and 10 read the same word. The screen opens its question one tick earlier, relative to the pad, than the binary would. Not in D-E19-93; changing it would shift every pad-driven pin of the picker by one tick (not measured). To record as an open point if the author wants it.
4. Slots 4 and 6 (inventories) before slot 3 (see above).

## 8. Files in this directory

`hostmodel.py` (model, `python hostmodel.py`), `slotorder.py`, `slotorder2.py` (+ `emu.py`, `drive.py`, `lib.py` copies: real binary code in dispatcher order), `slots.py`, `dis1.py` (table and disassembly dumps), `patch-proposal.diff` (the scratch patch: 2 production files, 2 test helpers, one value),
`probe-tests.cs.txt` (the proxy-level probe class), `e2e-old.log`/`e2e-new.log`, `probe-old.log`/`probe-new.log`/`probe-rel.log`, `test-*.log`, `build-*.log`, `orig/` and `new/` (the files patched), `repo/` (the scratch copy).
