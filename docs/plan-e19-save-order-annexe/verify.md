# Counter-check of the save-order discovery (D-E19-93), fresh context

Nothing built, run, edited, staged or committed inside the repository (`git status` unchanged: ` m CasaEngineMonogame`, `? alundra-datas-analyser`;
`alundra-project/Alundra.dll` still the 07:02 one; `Program.cs` untouched). Everything below lives in `verify/`; my OWN physical copy of the working tree is
`verify/repo2/` (own robocopy, `verify/setup2.cmd`, no `.git`, `alundra-project` physical so the deploy target stays in the copy). I did not read the report's
outputs as evidence: binary side = my own `vslot.py` / `vslot2.py` / `d1.py` / `tbl2.py` / `bxref.py` (capstone over ALUN_CD.EXE (France), emulator `emu.py` for the real slot-3 code);
code side = my own patch (`mypatch.py`, `mypatch2.py`), my own probe tests (`VerifyProbeTests.cs`, `VerifyFreezeTests.cs`), runs logged in `*.log`.
Source copy fidelity: the 4 files I patch were `cmp`-identical to the repository's before patching (`pristine/`).

## 1. Binary [bin] (own reads)

- Main loop: `0x8002C3E4` `Update(1)` is a ONE-SHOT before the loop; the steady loop (`0x8002C3F4`-`0x8002C45C`, back-branch `beqz ... 0x8002C3F4`) is
  `0x8002AC2C`, `RenderScene` (`0x8002C3FC`), `Update(0)` (`0x8002C404`). So per frame: RenderScene, then Update(0). CONFIRMED.
- `RenderScene`: `jal 0x8005EC98` at `0x8002BE54` (memory-card process = the screen's state machine), `jal 0x80048054` at `0x8002BE5C` (dispatcher), `jal 0x80044C5C` at `0x8002BE64`. CONFIRMED.
- `Update(0)` (`0x8002BAEC`): `jal 0x8002E38C` (pad sample: BIOS read `0x8008A688`, then the real pad update `0x8002E250` on block `0x80126F18`), then `jal 0x8002E058` = `UpdateWorld`:
  `0x8003C67C` (RunMapEvents, gated `& 0x48` on the control flags), `0x8003B388` (UpdateEntities), `0x8003C410`. CONFIRMED.
- Dispatcher `0x80048054`: loop `0x8004813C`-`0x80048174`, `s1` 0..12, `s0 += 0x1C` from `0x80153028`, runs `+0x14` when `flags & 1`: ASCENDING. CONFIRMED.
- Templates `0x800A731C + 0x1C*slot`, `+0x14`: 0 `0x80046EF0`, 1 `0x8004BEA4`, 2 `0x8004F628`, 3 `0x800501FC`, 4 `0x80053328`, 5 `0x8004A8A8`, 6 `0x80054F1C`, 7 NONE, 8 `0x8004AFE8`,
  9 `0x80050EC8`, 10 `0x800583EC` (installs `0x80058F24` at entry `+0x14`), 11 `0x80051550`, 12 `0x8005A3E0`. Loader `0x80047F94(slot)` copies the template, sets flag 1, calls `+0x10`. CONFIRMED
  (there is also a slot 11, update `0x80051550`, after slot 10; not mentioned by the report, irrelevant to the question; I did not identify it).
- Slot 10 update `0x80058F24`: `flags & 5 == 0` jumps to `0x8005903C` (skips the slide code, so in the question state `flags = 8` nothing gates the poll); `flags & 2 == 0` -> `0x8005912C`;
  `flags & 8` -> `lw 0x80180124`, `-1`, `sltiu 2` (accept 1 or 2), `flags := 2`. Idle (`flags & 0xD == 0`) -> `0x800594C8`: `lhu 0x80126F2E & 0x40` (Cross, by-interval),
  `sw zero -> 0x80180124` (`0x800594E4`), `jal 0x80050BA8` (`0x80059504`, `a2 = 0x80180124`), `flags := 8` (`0x80059548`). CONFIRMED (R2, R3).
- Opener `0x80050BA8`: stores the callback `0x80050B98`, calls `0x800505FC` which calls `0x80047F94(3)` (slot 3 loaded, template update `0x800501FC` = init pass). The callback does `*(0x8017E3F0) = a0`. CONFIRMED.
- Slot 3 reads the SAME pad block as slot 10 (`0x8004FFDC lhu 0x80126F2A & 0x40` for Cross; by-interval word for Left/Right). CONFIRMED.
- **Emulation of the REAL slot-3 code** (`vslot.py`, `vslot2.py`; slot 10 reduced to its disassembled idle opener and poll; real pad update `0x8002E250`; frame = dispatcher then pad sample), opener frame S = 5:
  first slot-3 pass = S+1; Cross seen at c >= S+19 writes the word at c+18, accepted by slot 10 in the SAME frame; Right then Cross: +1; table: OUI earliest S+37, NON earliest S+38,
  Cross at S+30 -> S+48, Right S+25 / Cross S+31 -> S+49 (all identical to the report's table); Cross before S+19 is ignored (init and slide passes). CONFIRMED.

## 2. Port [code + own runs]

- Lines `:2189`, `:2215`, `:2251`, `:2259`, `:2261`, `:159`, `:2179`, comment `:2239-2245` all as cited; Director `:423`, `:541`, `:951` as cited; no other pad read in the screen files (`rg`). CONFIRMED.
- The old/new relation: `AlundraChoiceBox` = init pass + 17 slide passes, interactive from pass 19, close 18 passes after Cross (read `AlundraChoiceBox.cs`): matches the binary's 19 / +18.
  Old order: the pass of the opening tick already runs (first pass S, one early), the answer is read the tick after the write; new order: first pass S+1, read in the tick of the write. Early arming: S+37 in both; late arming: -1 in the new order.
- My own patch (same design as the report: `Tick(uint, uint)` + two fields, tick and presenter in loop 2 after `_choicePresenter?.Tick()` and before `var tickBlocked`) compiled and behaved as follows (Debug, my copy):
  - baseline HEAD copy: 2857/2857;
  - production patch only, test helpers untouched: 2857/2857 (no existing test can see the order: all drive the director by hand);
  - plus helpers in the new order (`Dialogue.Tick(); TickScreen(hold)`; presenter helper pad / pass / choice presenter / screen / screen presenter) and `DownDuringTheQuestion` 20 -> 19: 2857/2857;
    with the helper changed and the value left at 20 the test FAILS (so 19 is the new value, nothing else moves). CONFIRMED.
  - Release (full) then Debug (full, last), each with my 37 extra probe tests: 2894/2894 both (= 2857 + 37; the 2 freeze tests and the Square tests were added afterwards, run alone and in mutant runs); traces: `intro-trace-389.txt`, `intro-programs-389.txt` byte-identical; the 4 hero traces identical modulo CR.
- Grid through the REAL `AlundraWorldProxy.Update`, frames to the answer, old / new (mine, `vprobe-old.log` / `vprobe-final.log`):
  1 tick: (arm 0, OUI) 37/37; (1, NON) 37/37; (17, OUI) 20/20; (18, OUI) 20/19; (26, OUI) 20/19; (26, NON) 21/20.
  2 ticks: (0, OUI) 19/19; (2, NON) 19/18; (18, OUI) 11/10; (26, OUI) 11/10; (26, NON) 11/10; (28, OUI) 11/10. All equal to the report's. Release == Debug.
- `Tick(37 + 18)` of `AnswerAndClose`: `+17` -> exactly 21 red in `AlundraSaveScreenDirectorTests` (42 tests); `+19` stays green (the bound is tight on the low side only). CONFIRMED.
- End-to-end book test through the real proxy (instrumented copy): checkpoints 75 / 214 / 256 / 372 / 411 frames in BOTH orders (= the report's 139, 42, 116, 38/39 deltas). CONFIRMED.
- Naive `Tick()` (no argument) in loop 2: only my two new multi-tick Cross tests go red (2 failures of 2894); the 2857 existing tests stay green. CONFIRMED (the trap is real and silent).
- Mutant "screen back in loop 1" is exactly the old code: red on the late-arming cells. Mutant "screen after the map events, inside loop 2 (after the `continue`)": all grid cells red (the screen never ticks once MenuOpen blocks the tick).

## 3. Refuted or corrected claims

1. **"Un déplacement de l'écran après les événements de carte n'est détecté par aucun test peu coûteux"** is wrong. A cheap proxy-level test exists, on the pattern of
   `AlundraSaveGameDirectorLoadTests.cs:373` (map-event record + `CountingRunner`, `TileMapObjectData` `EventCodesBIndex`): run one `Update` (the event runs), `Director.Start(ValidSave())`, run `Update` again, assert the run counter did not move
   (the screen's first tick posts MenuOpen before the gate). `VerifyFreezeTests.cs`, 1 and 2 ticks/frame. Mutant "screen in a third loop after the map events" (which keeps every grid number identical to the target): 2894 existing + probe tests all GREEN,
   only these 2 tests RED (`runs 1 -> 2`, `2 -> 4`). Plan impact: add this test to the new tests and to the acceptance mutants; drop the "keep by review" line.
2. **"Un armement du crochet avant la première passe interactive donne le même résultat dans les deux ordres"** holds at 1 tick per frame only. With coarser frames it is false even for early arming
   (my grid, old -> new): 2 ticks arm 0 NON 20 -> 19; 3 ticks arm 3 NON 13 -> 12; the report's own table has 2 ticks arm 2 NON 19 -> 18. No pinned test uses coarse frames, so no re-pin is missed,
   but the new tests' expected numbers must come from the full grid and the sentence must be scoped to 1 tick per frame.
3. **"A17, le marin 12 et le livre n'ouvrent pas la question de l'écran"** is imprecise: `AlundraSaveBookEndToEndTests.RealMap17_TheBook_Oui_TheScreen_Slot2_Oui_...` DOES open the SCREEN's question through the real proxy
   (`Press(Cross)`, `IsAwaitingChoice`, `SelectChoiceForTests(0)` at once). It is armed at once, hence order-insensitive (same frames both orders, measured). So it is the only real-proxy coverage of the question and it cannot tell the orders apart.
4. **"L'automate d'écran (RunState) ... déplacement inobservable : aucune lecture croisée"** is too strong. There is no read, but a shared WRITE on `PlayerControlFlags.MenuOpen`:
   the dialogue's `ClearControlFlags` (Released `:613`, `NotifyPresenterClosed` `:641`, reset `:270`) against the screen's `PostMenuOpen` (`:404`, `:445`) and clear (`:605`).
   In the binary the memory-card process runs BEFORE slot 0, which is the OLD DLL order for that pair (screen then pass); the full move reverses it for RunState (while making slot 9/10 faithful). Not reachable in the real flows:
   the book waits 61 ticks after closing its box before starting the screen, and scripted boxes cannot open under MenuOpen. Wording: "unreachable in practice", not "inobservable". Optional design: keep `RunState` in loop 1 (it reads only
   `TickPad`'s Square there, no per-tick words needed) and move only `RunTransition` + presenter after the pass; same numbers (RunState still precedes RunTransition, the picker result is still read one tick after it is set).

## 4. Missed items (plan impact)

1. **Square needs its own multi-tick test.** The report's new multi-tick test covers Cross (by-interval word) only. A half-converted director (`RunPickerInput` per tick, but `StateWaitSquare` still reading `state.TickPad.ButtonsJustPressed`, line `:541`)
   passes the 2857 existing tests, the grid and the Cross test; only a Square test catches it: `StartFailure()`, tick to `StateWaitSquare`, `Frame(2 or 3, Square)`, then ticks until the state leaves it
   (`VerifyProbeTests.MultiTickFrame_SquareHeld_LeavesTheWaitSquareState`: 1 tick green, 2 and 3 ticks RED with the mutant, `state=1013` stuck). Add it, and add the mutant to the acceptance list.
2. **Freeze test** (see 3.1) and the two mutants it kills belong to the acceptance list.
3. **Consumer of the screen's state in loop 1:** `AlundraInventoryDirector.TryTrigger` `:441` reads `AlundraSaveScreenDirector.Instance.IsActive` (and `PlayerControlFlags != 0` at `:384`) inside loop 1. After the move, in a frame of several ticks the
   inventory ticks 1..n-1 no longer see the screen ticks of the same frame (all of loop 1 runs before loop 2): a Start press on the very tick after the screen ended inside a coarse frame is refused where it was accepted. One-tick frames are unchanged
   (tests green). Negligible, but write it in the ADR/risks (or keep RunState in loop 1, which removes it). Unrun (analysis).
4. **The pad lag is real, derived from the binary, and the report's "no number moves" is true but the exactness statement should be scoped.** Both slots read the same pad block, sampled by the previous Update(0). Port: the screen reads `TickPad` of tick k (lag 0), the pass reads tick k-1.
   Pad samples between the press that opens the question and the earliest press the choice can accept: binary 19, port before the change 17, port after the change 18. So the reorder fixes tick order, first pass and write-to-accept, not the age of the pad word.
   D-E19-93 does not ask for it: record it as an open point as the report does (a double tap of Cross 0.36 s apart is the only way to see it).
5. **Plan lines that become stale and are not in the report's list:** `docs/plan-e19-opcodes.md` `:5353-5355` ("gagne un `Dialogue.Tick()` après le tick de l'écran (l'ordre du mandataire)"), `:5376` ("l'ordre du mandataire (écran, puis dialogue et choix)"), `:5406` (the pin list "`DownDuringTheQuestion` 20"),
   `:5306`; annex `docs/plan-e19-f3-annexe/sim-notes.md:100` and `census-notes.md:66,92,187` mention the first-loop placement / order A vs B. They are journal text of f3a (history): a D-E19-93 completion note is enough, an edit is optional.
   The report's own list (`:295-298`, `:5346-5350`, `:5379`, `:5397`, `:5488`, `:9997`, `sim-pins.md:60`) is accurate (checked line by line). Doc comments to rewrite in tests: `AlundraSaveScreenDirectorTests.cs:73-74`, `:613`, `AlundraSaveScreenPresenterTests.cs:68`, `:165`.
6. HEAD: the report says `79bbe5b`, the checkout is `0472402` (5 later commits, docs only: `git diff --stat 79bbe5b HEAD` = `docs/plan-e19-opcodes.md` and `docs/plan-e19-g1g3-annexe/README.md`). No code consequence; all cited plan lines match the current file.

## 5. Unconfirmed

- `hostmodel.py` itself (I replaced it by the real-proxy grid, which agrees on every cell I ran: 12 cells of the report plus 20 more).
- MenuOpen write-write flip (3.4) and the inventory `IsActive` staleness (4.3): derived from the code, not executed.
- Slot 10's full real code was not executed by me (only its disassembled opener and poll, plus the static proof that no slide gate precedes the poll in state 8); the same-frame acceptance follows from the ascending order plus that proof.
- The report's `patch-proposal.diff` was read only to compare designs (same as mine); I did not apply it.
