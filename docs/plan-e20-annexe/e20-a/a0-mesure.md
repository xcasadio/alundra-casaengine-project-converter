# E20.a A0: measurement of the new tick order (stage 1)

Date: 2026-10-09. Branch `chantier/e20-heros`, HEAD `1e25740` plus the uncommitted stage-1 working tree.
Contract: `docs/plan-e20-heros.md` section 1.3, task A0. Nothing here is a fix of a pin: every changed value is reported as measured.

## What was measured

The stage-1 working tree (not committed):

- `AlundraWorldProxy.Update` drives the ticks of its entities when `DrivesEntityTicks` is true (a world attached to a game): per tick, in this order,
  pad, box/text/choice/save passes (unchanged), gate, map events then recycling, then the **entity block** (`RunEntityBlock`: hero `MovePlayer`,
  programs of the entities in slot order, re-scan of the pending triggers, counters placeholder, lists, animation clock of every entity, physics of every
  entity with the hero's probes in the tick, hit-pass hook), then the inventory block (unchanged). A closed gate runs only the lists.
- `SyncAnimation` and `SyncTransform` run once after the last tick of the frame (`PublishEntityFrame`), no longer in `AlundraEntityScriptProxy.Update`.
- `MovePlayer` is **kept once per rendered frame on the per-frame pad** (A4 is stage 2): it runs in the hero's slot of the first open entity block of the
  frame, or, in a frame without tick, in `RunTicklessFrameEntityWork` (as before: only if the gate was open as the frame opened).
- A rig or fake host (`DrivesEntityTicks` false: fake hosts, and a bare `World` with no `Game`) keeps the entity's own loop (`RunSelfDrivenTicks`) and the
  world keeps its previous post-loop lists and pending-trigger re-scan.

Runs: `dotnet test Alundra.Tests -c Release` and `-c Debug`, `--no-build --blame-hang-timeout 300s`, foreground. Result identical in both:
**3265 tests, 13 fail, 3252 pass.** (Assumption: the suite was green at `1e25740`, as the plan states; HEAD was not re-run.)

## Decomposition (isolation experiments, removed afterwards)

To attribute each failure, four temporary variants of the driver were run (an environment switch, since deleted; the committed-to-be code has none).
All variants kept everything else of stage 1:

| Variant | Entity block placed | Publication | Pending re-scan | Lists | Failures |
|---|---|---|---|---|---|
| real order (stage 1) | after map events | after the last tick | in the block | in the block | 13 |
| V1 | before everything of the tick (old order) | after the last tick | in the block | in the block | 7: A4p, A6, A8 (Inoa awakening), TB9, A14, A18, T1 |
| V2 | as V1 | right after the entity block (old point) | in the block | in the block | 3: TB9, A18, T1 |
| V3 | as V2 | as V2 | after the map events (old point) | in the block | 3: TB9, A18, T1 |
| V4 | as V3 | as V2 | as V3 | at the end of the frame (old point) | **0** |

So with the old placement of the four things (entity block before map events, publication, pending re-scan, lists) the suite is green again: the
restructure itself loses nothing. Each failure comes from one of three placements:

- **I = inversion** (map events before the entity block, D-E20-11): fails in the real order only (passes in V1): A9, A5r, A1, A10, A11, A10J, s115_rec19, A17, A12.
- **P = publication after the last tick** (SyncAnimation/SyncTransform now see the map events of the frame): A6, A14 (fail in V1, pass in V2). A4p and A8 also fail in V1 only because of P, and TB9 because of L: they pass in the real order, where the inversion cancels the effect (masked, to re-check once the montages are rewritten).
- **L = lists (collidables, riding) refreshed inside the tick, before the physics** (`0x800384F4` before `0x80038364`): TB9, A18, T1 (fail in V3, pass in V4).

## A0 table: tests whose pinned value changes (real order)

Old = value pinned at HEAD. New = measured. "Binary reason" cites the order of `ALUN_CD.EXE` (`UpdateWorld` `0x8002E058`: map events `0x8003C67C`, then
`UpdateEntities` `0x8003B388`; inside it `0x80038634` recycling first, then `0x800386D0` programs, `0x80038998` counters, `0x800384F4` lists, `0x80038E18` animation,
`0x80038364` physics, `0x80039300` hits) and the plan fact D-E20-11.

| # | Test (file:line of the failing assertion) | Pin | Old | New | Cause | Binary reason |
|---|---|---|---|---|---|---|
| 1 | `AlundraShipArcTests.A1_...` (`:137`) | status of the captain 1 after the first image | `Destroyed` | `FlagToDestroy` | I | `0x80038634` (recycling) is the FIRST call of `UpdateEntities`: an entity that flags itself in the programs of tick k is recycled at the start of tick k+1, not in the same image. The old "recycled in that same image" came from entities-before-events. |
| 2 | `AlundraInoaDayOneArcTests.A11_...` (`:421`) | status after the scene | `Destroyed` | `FlagToDestroy` | I | Same as 1 (recycle at the start of the next tick). |
| 3 | `AlundraShipBlockArcTests.A6_...` (`:256`) | frames of the three boxes opened by map programs B@295/306/327 | `[180, 643, 1332]` | `[181, 644, 1333]` | I+P | The block entity lands (entity physics, end of the tick); the map event that polls it now runs BEFORE the entity block of the tick, so it sees the landing one tick later (+1). In V1 the pin moves too, to `2302 -> 2301` later in the test (publication: a map-event animation write is applied at the end of the same frame, one frame sooner). |
| 4 | `AlundraInoaDayOneArcTests.A10_...` (`:262`) | (frame, frame) of two instructions | `(698, 791)` | `(699, 791)` | I | An entity write seen by a map event one tick later (map events precede the entity block). |
| 5 | `AlundraHeightTargetSitesTests...("s115_rec19")` (`:148`) | (frame of the wait end, frame of the call, tile, Z) | `(756, 1077, 321, 3145727)` | `(757, 1078, 321, 3145727)` | I | Same +1: the wait is on an entity's height, written at the end of the entity block, read by the map event of the next tick. |
| 6 | `AlundraBergusJumpArcTests.A12_...` (`:65`) | frames at which the `0x25` wait executes | `[..., 1077, 1078, 1079, 1080, 1081]` | `[..., 1077, 1078, 1079, 1080]` | I | The jump is started by a map-program write that the entity block now sees in the SAME tick (`0x8003C67C` before `0x8003B388`), so the landing comes one image sooner and the wait polls once less. |
| 7 | `AlundraHeroJumpArcTests.A10J_...` (`:105`) | PosZ after F0+159 | `327680` | `622592` (= the old value of F0+160) | I | The hero's `MovePlayer` now runs after the map events of the tick: the animation 2 written by B@2451 at F0+158 is seen by the hero in that tick, so the jump is one image sooner (pins of the whole jump and landing move by -1 relative to F0). |
| 8 | `AlundraShipCorridorArcTests.A5r_...` (`:230`) | hero (PosX, PosY) at the end of frame 225 | `(50523408, 16302440)` | `(50492832, 16298384)` | I | The roll is a map-event walk of the hero: the hero moves in the tick of the map event's write, one tick earlier than before, so at frame 225 its position is one step further along. The Y component is smaller (not larger) than before: Y explained in "Mechanisms" below. |
| 9 | `AlundraVisionAndCoastArcTests.A9_...` (`:506`) | frame after the box of Wendell closes (helper presses Square every other frame) | `f530 + 119` (123) | `f530 + 99` (103) | I | The box opened by an entity program now makes its first pass at N+1 (ADR-0029 map-event row: first update N+1, release T+18) instead of N. The test helper presses Square on alternate frames from the frame after the opening; the one-tick shift flips the parity of those presses against the typing, so the box closes at 103 (a debug trace: box open from frame 5 to 103, presses on odd frames 5..101). The 20-frame jump is the press parity of the helper meeting the 18-tick close delay, not a different pace of the box. Order reason holds; mechanism confirmed in the section "Mechanisms" below. |
| 10 | `AlundraDay3SceneArcTests.A17_...` (`:319`) | flag T1 set by the end of the scene | set | "T1 never set" | I | A scene gated on a flag written by an entity program and read by a map program: the map program now reads it one tick later. The failure message is a never-reached signal (the arc's frame budget ends before it), not a wrong value: cause: a transient flag set and cleared in the same tick, see "Mechanisms" below (not a blockage). |
| 11 | `AlundraDay3SceneArcTests.A14_...` (`Dispose`, `:151`) | `ArcsWithoutSlide`: second steps along a wall | 1 | 2 | P | The hero walks by map-event writes; `SyncAnimation` after the map events applies the new animation (hence its speed) one frame sooner, so the hero reaches the wall corner on a different tick. Closer to the binary (the animation switch of `0x80038E18` follows the programs in the same tick) but the new pixel path is **not checked against an oracle**. |
| 12 | `AlundraDay3SceneArcTests.A18_...` (`Dispose`, `:419`) | `ArcsWithoutSlide` | 1 | 0 | L | Fails in every variant down to V3, passes only with the lists at the end of the frame (V4). `0x800384F4` (lists, collidables, riding) runs before `0x80038364` (physics) in the same tick; the port refreshed them after the frame, so the hero's step read the previous frame's list. The slide count is a pixel-exact consequence of that: **binary order is the reason, the new count is not oracle-checked**. |
| 13 | `AlundraAnimationSoundTests.T1_Map178_...` (`:309`, through `ArcRun.Dispose` of arc A18) | `ArcsWithoutSlide` | 1 | 0 | L | Same arc as 12. |

Not in the table, by design: the K1-entity / K4 / K7 pins of `AlundraDialogueBoxOrderTests` and `AlundraAnimationSoundTests:281` do **not** move: those tests
run the entities through fake hosts or a bare `World` (self-driven), so they never see the new production order. They will only move when those montages are
given a driving world or are rewritten (A7). The only production-order evidence for the ADR-0029 "entity row" is row 9 above.

## Mechanisms of the rows first marked "no binary reason" (second pass)

Method: temporary per-frame logs (hero pose, flags, program instructions, box state) in the arc tests, run in the real order and in the old order (a temporary switch in
the driver that put the entity block first, published right after it, re-scanned after the map events and refreshed the lists at the end of the frame; with it all 3265
tests were green). The instrumentation and the switch were deleted; `git diff` shows the stage-1 files only. The D1 oracle models `MovePlayer` only (no wall-slide table,
no dialogue box, no map events), so it cannot model these rows; each verdict below comes from the order of the binary.

**A17 ("T1 never set"): not a blockage, a sampling artefact of the test. Binary order reproduces it: yes.**
B[14] (map program, slot 1, prog 912) executes `0x05 @945` (sets T1) at frame 324, and in the same tick Ronan's C program (slot 2, prog 960) executes `0x06 @971`
(clears T1; it was waiting on it). Old order: Ronan ran BEFORE the map event, still waiting, so T1 was visible at the end of frame 324 and cleared at 325. New order: the
map event runs first, the entity block of the same tick sees T1 and clears it. The test samples temporary flags only at the end of each frame (`ArcSamples.OnFrame`), so it
never sees T1. With that single assertion removed, the rest of A17 passes (G1655 and G14 set, G14 at frame 1191, T0 set before the choice, the three blocks recycled): the scene
is not blocked and the player meets no regression. Binary: `0x8003C67C` sets the flag, then `0x800386D0` runs Ronan's wait in the same `UpdateWorld`. To settle the pin: sample the flag at the instruction, not at the frame end.

**A5r (hero at frame 225, Y "opposite"): binary order reproduces the alignment: yes; the force table itself is untouched DLL hero code (cannot tell, unchanged by stage 1).**
Same instruction trace in both orders. At frame 220 the map program executes `0x2F`, then sets the lock (flags 4) and direction 2 (`0x10`, `0x0C`). Old: the hero's tick of
frame 220 had already run (straight walk, Y +106496), so the roll forces (-30576/102440, then -61152/98384 four times) start at 221. New: the map event runs first and the hero's
tick of frame 220 already applies the roll force. By 225 the hero has done 6 roll ticks instead of 5, and the roll's Y force (98384 to 102440) is smaller than the walk it replaces
(106496): X is further negative AND Y is smaller. At frame 225 the unlock and the direction reset (`0x11 @124`, same frame in both) happen before the hero's tick, so the last
tick uses the ramp-down force (-30576, 102440). Order: map events `0x8003C67C` before `MovePlayer` `0x80031B50`.

**A9 (box closes at f530+99 instead of +119): parity mechanism confirmed; binary order reproduces it: yes.**
Logs of the box (`IsTypingDone`, `IsClosing`): the box opened by Wendell's `0x0D` at frame 4 is in the same state in both orders (sliding in) until the end of typing, which is at
frame 103 in the old order and 83 in the new; closing starts 2 frames later in both and the box is released 18 frames after the trigger (123 vs 103). The helper presses Square on
the odd frames 5, 7, 9... in both. The box reads the pad of the tick before and its first pass is now at N+1 = frame 5 instead of N = 4, so the presses fall on the other parity of
the box's own pass index; typing speeds up on the passes where the button is held, and with the other parity it saves 20 frames. Binary: a box opened by an entity program in the
entity pass has its first `MsgBoxRender` pass in the next frame's `RenderScene` (ADR-0029 row "N+1"). So f530+99 is what the binary gives for this pad phase; the old +119 was the other phase. The box code did not change.

**A14 (2 wall slides, expects 1): the shift is reproduced by the binary order (yes); the slide count is cannot-tell.**
At frame 440 a map program writes the hero's walk (animation 1, direction 8). Old: the hero starts moving at 441. New: at 440, in the same tick, because the hero's physics runs
after the map event. The walk is one tick earlier relative to the map programs that steer it; at frames 472/473 the new path touches a second corner (slides 1 then 2, at
(25866240, 9961472)) where the old one stopped at the first (25817088). The corner table `0x80023734` (ported in E19.h4) is not modelled by any oracle in the repository, so
whether the count is exactly 2 is cannot-tell; it would be settled by a run of the wall-slide table on this path, or by a binary trace of the hero at the map-179 corner.

**A18 and T1 (0 slides, expects 1): binary order reproduces it: yes.**
Same arc for both. The hero path is identical until frame 276; at 277 the hero is against the wall (ForceAdjusted 1). At frame 278 a map event spawns an entity (count 8 to 9, record
id 2). Old: the lists were refreshed at the end of the frame, so the new entity is not yet an obstacle and the hero makes its one second step along the wall (slides 1, X 62312448 to
62361600). New: the lists are refreshed in the tick before the physics, the entity is listed, blocks the hero (`XCollisionEntity` = 2, block count 1, slide forbidden) and no second
step is made. Binary: `0x80038634` takes the new slot into the slot maximum before the programs pass, the new entity picks its load program in that tick and becomes status Normal, then
`0x800384F4` lists it before `0x80038364` moves the hero. The DLL does not pick the new entity in its spawn tick (O-E20-11), but its list does not filter on that status, so the outcome
matches. Remaining uncertainty: the binary lists status 2 and 3 only, so the equivalence holds only if the load program makes it Normal in that tick (O-E20-11).

**Classification correction.** The variants V1/V2 above said "P" for A14 and A6; the logs show the first divergence of the real-order run is the hero walk or the box one tick
earlier or later (inversion). Treat A14 and A6 as I. P and L only move pins in variants where the inversion is absent.

**The masked ones (A4p, A8, TB9).** In the real order their pinned values do not move, so there is no moved point to explain. They fail only in a hypothetical variant (entity block first,
but publication after the map events, or lists in the tick); they show that the restructure alone is not neutral, and that the inversion cancels those effects in the real order.

## Hero reference traces

`docs/hero-trace-389-{spawn,highground}-{freestep,fixedstep}.txt`: **content unchanged** (`git diff --ignore-cr-at-eol` empty); the test run rewrites them with
line-ending noise, restored with `git checkout`. Reason: `MovePlayer` keeps its rhythm in stage 1, and a hero-only world has no map event or other entity.

## Intro reference traces

`IntroTraceHarnessTests` runs its own `RunFrame` (self-driven entities, then map events, then pending triggers); it does **not** use `AlundraWorldProxy`, so the
stage-1 code does not move it: `docs/intro-trace-389.txt` and `docs/intro-programs-389.txt` are unchanged by the run.

To measure the new order on the intro, the harness `RunFrame` was reordered temporarily (map events pass moved before the `entity.Update` loop; nothing else), the
suite and the trace were re-run, then both were restored with `git checkout` (not kept: the harness change is an A7 decision). Result:

- Stop condition unchanged: opcode `0x11` at frame **1704**, 1704 frames; "Trace lines" 41765 unchanged; raw dispatches 56543 -> 56542 (one fewer suspended wait).
- Within a frame the map-event lines now come before the entity lines (that is the bulk of the diff: 15643 lines moved).
- 86 of 1705 frames differ in content, **all in the chain of Entity 11 (102 lines) and Entity 15 (3 lines)**: their `0x36 Wait until flag on` ends one frame earlier
  (`2-555 (x554)` -> `2-554 (x553)` for Entity 11; `2-1034 (x1033)` -> `2-1033 (x1032)` for Entity 15), because the flags they wait on (232 and 234, group 131) are set by a map event,
  which now runs before the entity pass of the same tick. The whole chain after that is shifted by one frame (Entity 11: `0x09 Set direction` at 554 instead of 555, ..., the flag
  `0x83EA` set at **1033** instead of 1034). The two last lines (frames 1703 and 1704) differ only by a line moved within the frame.
- Pins that fail with the reorder: `IntroTraceHarnessTests.IntroTrace_Map389_ProducesOrderedOpcodeAndSystemTrace` (`:160`, `flag83EAFrame`) **1034 -> 1033**; and
  `AlundraSoundOpcodesProductionTests.Intro_RealSoundPlayer_Requests23SoundsAtTheExactFramesAndCounts` (`:84`, the sound 61 request) **1087 -> 1086**. Both are the same shift of the
  chain of the sailor 11 (a flag written by a map event is read by an entity in the same tick: `0x8003C67C` before `0x8003B388`). The end of the intro (0x11 at 1704) does not move.
- No other intro point or opcode count changed.

## Not covered by this run (stage 2 / A7)

- A1 (pad of the tick), A4 (`MovePlayer` at every tick, removal of `JumpStartTickStamp`), A5 (counters pass), A6 (first frames): not done.
  The pins the plan expects from D7 (SJ6b, SJ5b, SJ5c, SJ4, SJ4b) did not move because `MovePlayer` still runs once per frame.
- `AlundraTickStructureTests` (A8): not written.
