# E20.a: list of the pins that moved (A7)

Date: 2026-10-09. Every value below was measured on the final E20.a tree; "old" is the value pinned at `1e25740`. The reasons are the binary's order
(`UpdateWorld` `0x8002E058`: map events `0x8003C67C`, then `UpdateEntities` `0x8003B388` whose first call is the recycling `0x80038634`, then the programs
`0x800386D0` with `MovePlayer` `0x80031B50` first, the counters `0x80038998`, the lists `0x800384F4`, the animation `0x80038E18`, the physics `0x80038364`,
the hits `0x80039300`) and the pad of the binary (`g_padState1`, refreshed once per tick just before `UpdateWorld`, `0x8002BB5C`); the mechanisms of the arcs
are in `a0-mesure.md`. The jump pins are re-derived from the D1 oracle (`docs/plan-e20-annexe/d1-oracle-moveplayer/out/S-A.trace.txt` and `S-B.trace.txt`,
`python -B tools/run_all.py`: S-A and S-B PASS, 0 differences, files unchanged).

## Pins of the order (map events before the entities of the tick, D-E20-11)

| Test (file) | Pin | Old | New | Reason |
|---|---|---|---|---|
| `AlundraShipArcTests.A1` | status of the captain 1 after the first image | `Destroyed` and unlisted | `FlagToDestroy`, then `Destroyed` and unlisted one image later | `0x80038634` opens `UpdateEntities`: a program that flags an entity in tick k sees it recycled at the head of tick k+1 |
| `AlundraInoaDayOneArcTests.A11` | status of record 4 after the E handler | `Destroyed` | `FlagToDestroy`, then `Destroyed` one image later | same |
| `AlundraInoaDayOneArcTests.A11` | `FirstSet[200]` against the opening of box 131 (C@325) | +29 | +30 | a box opened by an entity program has its first pass at N+1 (ADR-0029, binary row) |
| `AlundraInoaDayOneArcTests.A10` | T102 set (edge) | 698 | 699 | an entity's write is read by a map event one tick later |
| `AlundraInoaDayOneArcTests.A10` | T105 set (edge) | 792 | 793 | follows T102 |
| `AlundraShipBlockArcTests.A6` | frames of the three boxes opened by map programs | `[180, 643, 1332]` | `[181, 644, 1333]` | the block's landing is read by the map program one tick later |
| `AlundraShipBlockArcTests.A6` | closing `0x51` frames | `[592, 1203, 1800]` | `[593, 1204, 1801]` (durations 412, 560, 468 unchanged) | same shift |
| `AlundraShipBlockArcTests.A6` | the loop of record 4 (`0x65` x 40, `514`, `528`, `529`) | 2302..2341, 2301, 2341, 2342 | 2303..2342, 2302, 2342, 2343 | same shift |
| `AlundraShipCorridorArcTests.A5r` | hero (PosX, PosY) at the end of frame 225 | `(50523408, 16302440)` | `(50492832, 16298384)` | the roll starts in the tick of the map program's write (map events before `MovePlayer`); 6 roll ticks instead of 5; the roll's Y force is below the walk it replaces |
| `AlundraVisionAndCoastArcTests.A9` | frame after the box of Wendell closes; `0x06 @544`, `0x11 @547` | `f530+119`; `f530+119` | `f530+99`; `f530+98` | the box's first pass is N+1; the helper's alternate-frame presses meet the typing on the other parity (typed at frame 83 instead of 103) |
| `AlundraHeroJumpArcTests.A10J` | PosZ of the jump `F0+159+i`; PosX of the air | starts at `F0+159` | starts at `F0+158` | the animation written by the map program is seen by `MovePlayer` in the same tick |
| `AlundraHeroJumpArcTests.A10J` | Giles: impulse, landing, `0x24`, `0x19`, recycled | `F0+101`, `+119`, `+255`, `+266`, `+267` | `F0+100`, `+118`, `+254`, `+265`, `+267` (flagged at `+266`) | the entity programs see the writes of the map programs in the same tick; recycling at the head of the next tick |
| `AlundraHeroJumpArcTests.A10J` | Giles's run of 2.0 px steps (O-E19-29, a deviation of the port) | `F0+241..244` | `F0+237..243` (after one step of 147456 at `F0+236`) | consequence of the one-image shift on a pixel-exact defect of the port; not isolated by a trace (open) |
| `AlundraBergusJumpArcTests.A12` | executions of `0x25 @415`; `0x53 @451`; flight list; rest | `F+4..F+23` (20), 2277, `F+1+i`, `F+23` | `F+4..F+22` (19), 2276, `F+i`, `F+22` | Bergus is written by B and moved by the entity block in the same tick |
| `AlundraHeightTargetSitesTests` `s115_rec19` | (first frame, end frame) | `(756, 1077)` | `(757, 1078)` | entity write read one tick later by the map event / box N+1 |
| `AlundraDay3SceneArcTests.A14` (`ArcsThatSlide`) | second steps along a wall | 1 | 2 | the hero walks one tick earlier against the map programs; a second corner contact |
| `AlundraDay3SceneArcTests.A18`, `AlundraAnimationSoundTests.T1` (same arc) | second steps along a wall | 1 | 0 | a map event spawns an entity at frame 278; the lists, refreshed before the physics, already make it an obstacle (`0x800384F4` before `0x80038364`) |
| `AlundraDay3SceneArcTests.A17` | "T1 set" sampled at the end of the frame | seen at 324 | not sampled; asserts Ronan's `0x06 @971` ran | T1 (B[14] `0x05 @945`) is cleared by Ronan's C[1] in the same tick; no blockage, the scene reaches G14 |
| `IntroTraceHarnessTests` (own `RunFrame`, map events now before the entities) | `flag83EAFrame`; `flag83E9Frame`; `flag860Frame`; `0x11` | 1034; 1202; 1704; 1704 | 1033; 1201; 1703; 1704 | sailor 11's wait on flag 232 (a map event) ends at 554 instead of 555; the chain is one frame sooner; B1 reads sailor 12's flag one tick later |
| `AlundraSoundOpcodesProductionTests` (intro) | request of the sound 61 | 1087 | 1086 | same chain |
| `docs/intro-trace-389.txt` | re-measured | | 86 of 1705 frames differ in content (Entity 11: 102 lines, Entity 15: 3), the map-event lines come first in each frame; stop at 1704, 41765 lines, raw dispatches 56543 -> 56542 | same chain |

## Pins of the pad and of `MovePlayer` per tick (A1, A4)

| Test (file) | Pin | Old | New | Reason |
|---|---|---|---|---|
| `AlundraHeroFallAndPadJumpTests.SJ6b` | target and ForceX after a two-tick frame, walking jump | `2`, `159744` | `44` (0x2C), `155136` | `MovePlayer` runs at each tick: tick 2 is in the air and takes 0x2C; ForceX 155136 is tick 2 of UH2 and of the oracle S-B |
| same, standing variant | target | `43` | `45` (0x2D) | tick 2 of the oracle S-A: animation 45, PosZ 622593 (622592 + 1) |
| `...SJ5b` | a press on a frame without tick | the jump survived (stamp) | the Cross must be held until the first tick (edge of the tick pad) | the pad is sampled once per tick; a press and a release between two ticks has no edge |
| `...SJ5c` | same at 144 Hz | Cross held on frame 1 | held on frames 1 to 3 (first tick on frame 3); same PosZ list | same |
| `AlundraHeroJumpStatesTests.SJ3b`, `SJ8a` | `JumpStartTickStamp` | -1 | field removed | no stamp |
| `AlundraHeroJumpStatesTests.SJ4` | take-off target kept until a tick ran | kept | rewritten by the tail at every call | `0x80031E84` then `0x80031EA8`; replaced; `SJ4b` deleted |
| `AlundraHeroJumpStatesTests.SJ8b` | Cross after a message box | held, edge injected | released one frame, then pressed | the edge is derived from the hold of the previous tick (`pressed = new & ~old`) |
| `AlundraInteractionPassTests` (`SquarePress`) | the pad seam | edge only | hold and edge | same: the hero reads the tick pad |
| `AlundraEventProgramRunnerTests` `0x2F` (4 tests) and the pad seam test | source of the pad | `LastPadState` | `GameState.TickPad` (`TickPad.Update(hold)` in the test) | `0x2F` modes 0 and 1 read the pad of the tick |

## Unmoved

- `docs/hero-trace-389-{spawn,highground}-{freestep,fixedstep}.txt`: content unchanged (`git diff --ignore-cr-at-eol` empty) after the full run.
- `AlundraDialogueBoxOrderTests` (K1-entity, K4, K7) and `AlundraAnimationSoundTests:281`: they run the entities through fake hosts or a bare `World` (self-driven), so
  they do not see the production order; they stay as they are.
- UH-1, UH-2 and the other jump pins: equal to the oracle, unchanged.
