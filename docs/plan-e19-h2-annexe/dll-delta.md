# dll-delta.md - DLL value against binary value, per pin (E19.h2 D1, step 4)

> **Errata (independent check, `recompute/corrections.md`):** no number of this annex changes; C1-C9 retag labels and provenance ([R], [input], [I], [D]); **C3** corrects the A10J X pin (below). Read the corrections before using a value.

Read-only study. Nothing was built or run on the DLL (another process builds and tests): every **DLL value** below is *read* from a test file (`file:line`; HEAD was `f06e087` at the start and `5305027` at the end, and none of the cited files differs between the two, see README.md section 5) or from a plan document, never measured tonight. Every **binary value** is *measured* by the replays of this annex (file named in the cell) unless the cell says **[I]** (inferred) or **[R]** (read in the disassembly only).

Tags: **[M]** measured on the real code of `ALUN_CD.EXE` (France) by two engines that agree (see `values.md` section 5), **[I]** inferred, **[R]** read.

## 0. The four mechanisms of the DLL (code references)

| id | mechanism | DLL | binary |
|---|---|---|---|
| (a) | order and terrain of the step | NPC tick: script, animation clock, `TickScriptedNpc` = **XY step** (`Alundra/Scripts/AlundraEntityScriptProxy.cs:1368`), then `EvaluateEntitySupport` = **Z step with the terrain sampled after the XY step** (`:1379`) | `MoveEntity` 0x80037E34 calls **ComputeZ 0x800375E0 (0x80037EF8) then ComputeXY 0x80037730 (0x80037F00)**; ComputeZ probes the terrain at 0x80037690-0x80037694 (`jal 0x800370c4`, store `+0x138`) at the position **before** the XY step **[R]**, only for an entity with no carrier (`+0x28 == 0`, 0x80037E4C-0x80037E54) **[R]** |
| (b) | landing test | `FinalForceZ < 1 && moddedPosZ + FinalForceZ <= landingTop - 1` (`AlundraEntityScriptProxy.cs:871`): lands when the next position is **<=** the top | `slt` at 0x80036C24 after `addu` 0x80036C20: no landing when `T < ModdedPosZ + FinalForceZ` (binary convention); in DLL convention (`PosZ_bin - 1`): lands iff `PosZ + F < T`, **strict** **[R]**; `contact` `+0x140` written 1 at 0x800376E0, `ForceZ = 0` only with the Gravity bit (0x800376F4-0x80037704) **[R]** |
| (c) | 4 px ground magnet of the engine | the NPC declares `Controller.SetExternalVerticalDisplacement(FinalForceZ / 65536f)` (`:968`): zero or negative in a fall, so the engine does not hold the entity "airborne" (`CharacterControllerComponent.cs:1423`) and its field snap accepts a ground up to `GroundSnapDistance` = 4 px under the foot after the step (`:1523-1545`, rejects only strictly below; 4 px exported by `alundra-casaengine-project-converter/Writers/SpriteWriter.cs:426,469`) | no such magnet in a fall (the only snap is the 3 px magnet at the top, 0x80037848, which needs `ForceZ == 0` and an XY force) **[R]** |
| (d) | `IsOnGround` source | at the head of every frame `PosZ = root.Z` (`:1266`) and `IsOnGround = Controller.IsGrounded` (`:1271`) | `IsOnGround` (`+0x144`) = `!(FloorHeight(+0x134) < PosZ(+0x11C))` written by the end pass 0x80038064 (0x800380F8-0x8003810C) for every entity, `FloorHeight` from the terrain probed **after** the XY step **[R]** + **[M]** (ledge table) |

(c) and (d) act together: the magnet makes the engine's `IsGrounded` true after the tick, and (d) shows it at the head of the next frame, which is one tick before the binary's own end-pass value.

## 1. Matrix pin x mechanism

| pin | gap (DLL -> binary) | (a) | (b) | (c) | (d) | other |
|---|---|---|---|---|---|---|
| UJ-1 (IZF 1360, flat) | none on the asserted fields | | | | | |
| UJ-7 `0x25` of a 1360 jump (no test) | `s+22` -> `s+23` | | | **yes** (last air point 1.375 px) | carrier | |
| UJ1b (IZF 1280) | `ForceZ` at update 21: `0` -> `-327680` | | **yes** | no (last air point 5.0 px > 4) | | |
| UJ1c (walking jump 1280) | X fraction kept at the landing | | (b) moves the landing 21 -> 22 | | | **O-E19-29** (`PushLogicalPositionToRoot` truncation), not (a)-(d) |
| TR-V (bouquets) | `0x25` `t+8` -> `t+16` | | no (no Gravity bit: `ForceZ` is not zeroed; the DLL's contact is already strict, UJ1b comment `:74-75`) | **yes** (foot at exactly 4.000 px at update 8) | carrier | |
| A10 Bergus (165) | `+1` image per jump | | | **yes** | carrier | the second jump needs the impulse of the 2nd `1A [2]` **[I]** |
| A12 Bergus (179, program B) | none on `0x25` (`F+23`) | | | **yes** (-1 update) | **yes** (+1 image: a B program reads the head-of-frame value) | the two cancel; B after the entities (update 1 at F+1, not F) is the accepted gap O-E19-28 |
| A10J Giles (10) | `0x25` `F0+119` -> `F0+120` | **no effect** (ledge seen 1 to 2 updates before the landing) | no | **yes** (last air point 3.125 px above the ledge) | carrier | XY of update 1: `129024` (binary) against `159744` (plan's DLL-derived), D-E19-13 |
| A3 dogs (478, IZF 768) | `ForceZ` at update 13 (`0` -> `-196608`) and `0x25` only; heights and contact equal; no pin | | **yes** (`ForceZ`) | **yes** (3.0 px, `0x25`) | carrier | |
| Bomb 362 (IZF 1024) | same as 768 (`ForceZ` at update 17 `0` -> `-262144`, `0x25`); no pin | | **yes** (`ForceZ`) | **yes**/**boundary** (4.000 px) | carrier | |
| ledge fall (new pin) | first tick in the air `4` -> `5` | **yes** | | | | |

## 2. Per pin

### 2.1 UJ-1 - IZF 1360, flat 0 (`Alundra.Tests/AlundraAnimationImpulseTests.cs:45-65`)

- DLL (test): PosZ of updates 1 to 22 = `Flight1360` (`:22-27`, asserted `:54`), `IsOnGround` 0 and `CollidedWithEntityZ` 0 at every one of them (`:55-56`); update 23: PosZ 0, `ForceZ` 0, `IsOnGround` 1, `CollidedWithEntityZ` 1 (`:60-64`).
- Binary **[M]** (`out/replay_S-1360.txt`): the same 22 PosZ_dll (348160 ... 90112), `IsOnGround` 0 and contact 0 at 1..22; update 23: PosZ_dll 0, `ForceZ` 0, `FinalForceZ` -372736, contact 1, `IsOnGround` 1.
- Delta: **none** on what the test asserts (`FinalForceZ -372736` is not asserted). The same list is obtained on four real prefabs (Giles, Bergus x3) on their real cells.

### 2.2 UJ-7 - `0x25` of a 1360 jump (`docs/plan-e19-d2c-valeurs.md:35`, no test; measured through A10 `AlundraInoaDayOneArcTests.cs:201`)

- DLL: `0x25` executes `s+3/s+4 .. s+22`, returns at **s+22**.
- Binary **[M]**: first poll at `s+4` for `37 [3]` (`s+3` for `37 [2]`), returns at **s+23** (20 executions for `37 [3]`: s+4 to s+23; 21 for `37 [2]`). Real script, engines A and B, five prefab/map variants.
- Delta: +1. Mechanism (c) with (d): the last air point is 1.375 px above the floor (`out/replay_S-1360.txt`, update 22: h = 1.375), inside the engine's 4 px, so `Controller.IsGrounded` is true at the head of the next frame; the binary's `IsOnGround` is 0 until update 23.

### 2.3 UJ1b - IZF 1280 (`AlundraAnimationImpulseTests.cs:68-84`)

- DLL (test): update 21: PosZ 0, `ForceZ` **0** (`:78`), `IsOnGround` 1 (`:79`), `CollidedWithEntityZ` 0 (`:80`); update 22: `CollidedWithEntityZ` 1 (`:83`).
- Binary **[M]** (`out/replay_S-1280.txt`): update 21: PosZ 0, `ForceZ` **-327680**, `FinalForceZ` -327680, `IsOnGround` 1, contact 0; update 22: PosZ 0, `ForceZ` 0, `FinalForceZ` -360448, contact 1, `IsOnGround` 1. `0x25` returns at **s+21** (IsOnGround at 21), as in the DLL.
- Delta: `ForceZ` at update 21 only. Mechanism (b): P reaches 0 exactly at 21; the DLL's `<=` lands (zeroing `ForceZ`), the binary's strict test does not. (c) plays no part (last air point 5.0 px). h2.json's red-first value is **confirmed**.

### 2.4 UJ1c - walking jump, IZF 1280 (`AlundraAnimationImpulseTests.cs:87-111`)

- DLL (test): PosZ at update 20 = 327680 (`:102`) - equal to the binary (**[M]** `out/replay_S-1280.txt` update 20: 327680); update 21 = landing with X truncated to the pixel (`:106-111`).
- Binary: the landing path writes only `+0x11C`, `+0x140` and `+0xB8` (0x800376E0-0x80037704) **[R]**, so X is never truncated; with the strict test the entity is on the floor but not "landed" at update 21 and lands at 22 **[M]** for Z. The replay is Z only; the XY fraction is not measured here.
- Delta: not a mechanism of (a)-(d): it is O-E19-29. Changes only if h2 also removes `PushLogicalPositionToRoot` from the landing; this is author question 1 of `h2.json` (see `notes.md`).

### 2.5 TR-V - the bouquets (`AlundraEntityContactArcTests.cs:218-241`, pin `Assert.InRange(wait[^1] - drop, 8, 17)` at `:228`)

- DLL: `0x25` returns at **t+8** (measure of E19.d2c1 C1 as quoted by `h2.json` fact 13 and its `expected_at_head`; not re-measured tonight). The test comment (`:218-221`) says "between t+8 (the 4 px magnet of the engine, IsOnGround 1) and t+17" and the assertion is the range 8..17 (`:228`). `FrameOf(stopPc)` equals the return frame (`:229`); after it `ForceZ == 0` and PosZ == rest (`:238-241`).
- Binary **[M]** (`out/replay_S-TRV.txt`; the real bouquets rec69 and rec81 give the same table): no gravity, 8 px above the floor, `1B [128,255]` at tick t: PosZ_dll - floor = 491520 - 32768 (k-1) at the updates 1..15 (ticks t..t+14), update 16 (tick t+15) is on the floor (`IsOnGround` 1), `0x25` returns at **t+16** in the real script (engines A and B). The `1B [0,0]` that follows runs in that call, so `ForceZ` is 0 from update 17 and **the contact is never raised**; without the tail (`out/replay_S-TRV-keep.txt`) the contact is raised at update 17. h2.json said "contact a la 17": true only without the tail.
- Delta: 8 ticks. Mechanism (c): the foot reaches **exactly 4.000 px** at update 8 (`524288 - 8 x 32768 = 262144`), and the engine's field snap accepts a ground at `foot - 4 px` inclusive (`CharacterControllerComponent.cs:1535`), giving `IsGrounded` at the head of t+8 (d). (b) plays no part: the bouquet has no Gravity bit and the DLL's contact flag is already on the strict test.
- The test stays green (8 and 16 are both in 8..17) but is too wide: the tightened pin is `wait[^1] - drop == 16` for both bouquets (rec69 `@4738/@4741/@4742`, rec81 `@5542/@5545/@5546`).

### 2.6 A10 - Bergus on map 165 (`AlundraInoaDayOneArcTests.cs`)

| line | DLL value (test) | binary, first jump **[M]** | binary, second jump **[I]** (hypothesis H: the 2nd `1A [2]` @839 is given its impulse) |
|---|---|---|---|
| `:201` `FramesOf(838)` | `Range(597, 20)` (`s` = 594, polls 597..616) | polls 597..617 = `Range(597, 21)` (`37 [2]` returns at s+3; `0x25` returns at s+23 = 617) | - |
| `:202-203` `@839`, `@841` | 616 | 617 | - |
| `:204` `FramesOf(843)` | `Range(619, 20)` | - | `Range(620, 21)` (s' = 617, first poll s'+3, returns s'+23 = 640) |
| `:205-206` `@844`, `@846` | 638 | - | 640 |
| `:207` `@847` | 696 | - | 696 (bounded by the release of the box S030) - unchanged |
| `:210` `AssertFrame(B, 354, 1775)` | 1775 | - | unchanged (the later waits) |
| `:273` flight 1 `HeightAfterImage(594 + i)` | `rest + flight[i]`, i = 0..21 | identical (updates 1..22 of `out/replay_R-1360-Bergus165.txt`) | - |
| `:274` flight 2 `HeightAfterImage(616 + i)` | `rest + flight[i]` | image 616 = rest (landing, update 23 of the first jump) | `HeightAfterImage(617 + i)` |
| `:277-278` `HeightAfterImage(638)`, `(639)` | rest | - | rest at 639 and 640 (landing of the second jump at update 23 = 639) |

- Mechanism: (c) with (d), exactly as 2.2 (+1 per jump). The first-jump half is measured on the real prefab (Bergus, map 165 rec2, terrain 0) with the real script `1A [2]`, `37 [2]`, `0x25`.
- **H is not established**: the binary gives an impulse only when the switch block of `UpdateAnimation` (0x80038B18) is entered: target != current, **or** direction row changed, **or** the end of a chain (0x80038B08-0x80038B14, 0x80038D2C). The prefab data say that animation 2 of Bergus ends with `Chain 4` (`out/prefabs_survey.txt`, rec2) and the DLL, which ports the same rule, gives the second flight (`:274`), so H is plausible, but the stand-in of this study cannot show it: it needs `UpdateAnimation` run on the real sprite bytes. The frames of the second jump are therefore **[I]** and must be checked by the arc, as `h2.json` fact 31 says.
- Do not pre-write absolute frames: a derivation by hand was already wrong once on this arc (E19.d2c1, T102 620 against 621).

### 2.7 A12 - Bergus on map 179 (`AlundraBergusJumpArcTests.cs:61-83`)

- DLL (test): `F` = frame of `1A [2] @411` = 1058 (`:63`); `37 [3] @413` returns at `F+4` (`:64`); `0x25 @415` executes `F+4 .. F+23`, 20 executions (`:65`); `1A [0] @416` at `F+23` (`:66`); `0x53 @451` at 2277 (`:67`); heights `rest + flight[i]` at `HeightAfterImage(F + 1 + i)` (`:79`), rest at `F+23` and `F+24` (`:82-83`).
- Binary **[M]** (`out/replay_R-1360-Bergus179.txt`, rec8 on its real cell, terrain 16 px, rest 1048576): `37 [3]` returns at `s+4`, `0x25` executes `s+4 .. s+23` and returns at `s+23`, `1A [0]` in the same call; update 1 = the tick of the `1A`. Because the map events (`RunMapEvents` 0x8003C67C, call at 0x8002E100) run **before** `UpdateEntities` (0x8002E108) **[R]**, a `B` program's `1A` at tick F gives update 1 at tick F.
- So the binary's `0x25 @415` returns at **F+23** = the DLL pin **[M]+[R]**. The DLL reaches the same frame by two effects that cancel **[I]** (read from the DLL's structure, not measured): the `B` program runs after the entity updates of the image (update k at image F+k, accepted gap O-E19-28) and reads the head-of-frame `IsOnGround` (d); mechanism (c) makes the engine's `IsGrounded` true after update 22, visible at the head of image F+23 - one update early but one image late, so F+23. After h2 the in-tick floor rule gives `IsOnGround` 1 at update 23 = image F+23, and a `B` pass that runs after the entity update of that image sees it at once: F+23 again, **provided the new in-tick value is visible to the `B` pass of the same image** (to check when h2 is written). The heights stay one image later than the binary's (update 1 at tick F in the binary, image F+1 in the DLL): accepted gap, not (a)-(d).

### 2.8 A10J - Giles on map 10 (`AlundraHeroJumpArcTests.cs:159-164`)

- DLL (test, relational +-1): `Near(f0 + 119, FramesOf(6393)[^1])` (`:160`), `Near(f0 + 119, FrameOf(6394))` (`:161`), `@6394` starts at X in [521.5, 522.5] px (`:163`).
- Binary **[M]** (`out/giles_ledge.txt`, engines A and B identical; XY start taken from the plan, see below): landing on the 16 px ledge at **update 19** (PosZ_dll 1048576, contact 1, `IsOnGround` 1), the 18 in-air PosZ are the UJ-1 list, so `0x25 @6393` returns at **s+19 = F0+120** (s = F0+101) and `@6394` starts at F0+120. PosX after update 19 = 522.28 px with the **binary's** XY; but the pin `:163` is an absolute X of the **DLL**, whose XY leads by 61440 units from update 2: once `0x25 @6393` returns one image later, the sample is F0+119 = 34289664 = **523.22 px, about 0.72 px above the window [521.5, 522.5]** **[I]** (corrected by `recompute/corrections.md` C3): this absolute pin must be re-pinned or widened when h2 lands.
- Delta: +1 (still inside +-1). Mechanism (c): the last air point (update 18) is 19.125 px, **3.125 px** above the ledge, within the engine's 4 px, so the DLL sees `IsGrounded` at F0+119. **(a) has no effect on the landing tick**: the box right edge crosses x = 528 px after update 17 (post-step probe) so the DLL order sees the ledge from update 17 and the binary order from update 18, both before the landing at 19 (margins 2 and 1 updates; K = 19 for any start x0 from 490.6 px, the plan's start is 493.3125 px).
- Assumptions that are **not** measured: the start x (plan value `32329728` at the tick of `1A`, DLL-derived) and the pre-jump XY force (walking animation 9, speed 208, steady). With the binary's UpdateAnimation order the first step of the jump is `129024` (the new animation 3 applies in the same pass, 0x80038B34), against the plan's DLL-derived `159744` (the DLL reads the animation one frame late, D-E19-13): the binary's X lags the plan's by 61440 units (0.9375 px) from update 2 on. This touches no pin of A10J that is relational +-1 px, but any future absolute X pin must take it into account.
- The later A10J pins (`0x24 @6400` `F0+255`, `0x19` `F0+266`, recycle `F0+267`, `:175-185`) are consequences of the walk after the landing: +1 tick **[I]**, inside their +-1 windows; they are the pins that O-E19-29 also moves (author question 1).

### 2.9 A3 - the dogs of map 478 (IZF 768), no height pin

- No test pins the heights (the d2c document has an optional pin, `docs/plan-e19-d2c-valeurs.md` A.2 "A3"). Binary **[M]** (`out/replay_R-768-Dog478a.txt`, `...b.txt`, real cells, Height 2 = terrain 16 px): PosZ above the rest = 196608, 360448, 491520, 589824, 655360, 688128, 688128, 655360, 589824, 491520, 360448, 196608 at updates 1..12, 0 at update 13 (`IsOnGround` 1, `ForceZ` -196608), contact at 14. These are exactly the heights listed for the DLL in that document.
- Delta: `ForceZ` -196608 against 0 at update 13 (b); the contact is at 14 in both (the DLL's contact is already strict, UJ1b comment `:74-75`); a `0x25` (none exists on the dogs) would return at `s+13` against `s+12` for the DLL **[I]** (c: last air point 3.0 px). `A3` (frames 301, 856, 1368, 1624, 2008, 2262) does not depend on any of it: unchanged.

### 2.10 Bomb of map 362 (IZF 1024), no pin

- Binary **[M]** (`out/replay_S-1024.txt`, `out/replay_R-1024-Bomb362.txt`: `1A [2]` then `0x16` in one call, as `C[5]` @635/@637): PosZ above the rest = 262144 ... 491520 ... 262144 (updates 1..16), `IsOnGround` 1 at update 17 (`ForceZ` -262144), contact at 18, `0x25` at `s+17`.
- DLL **[I]**: P = 0 at update 17 too (`<=`), `0x25` at `s+16` if the engine's snap at exactly 4.000 px is accepted (the t+8 of TR-V, a measure of E19.d2c1 C1 quoted by `h2.json`, implies that the 4 px boundary is inclusive), `s+17` otherwise: to be measured by the DLL, not replayable here. Delta: (b), (c) boundary.

### 2.11 The ledge fall (new pin, `h2.json` proposed test 3)

- DLL: first tick in the air = **4** (terrain sampled after the step, `AlundraEntityScriptProxy.cs:1379`), value read in the discovery notes of E19.h (`scratchpad e19h-disc/A/notes.md` section 5.5) and re-derived by arithmetic here (box left edge `PosX - 10 < 144` iff PosX < 154: after the step of tick 3 PosX = 153 is still over the high cells, after tick 4 PosX = 154 is not) - **not run on the DLL**.
- Binary **[M]** (`out/giles_ledge.txt` section L; engines A and B; independent hand arithmetic): first tick in the air = **5** (terrain sampled before the step: pre-step PosX at tick 4 is 153, at tick 5 is 154). At tick 4 the contact is 1 and `IsOnGround` already 0 (the floor is computed from the post-step terrain), PosZ still at 16 px; tick 5: PosZ_dll 1015808.
- Delta: 1 tick, mechanism (a).

## 3. Proposed values of `h2.json` that this annex confirms or corrects

| proposed test (h2.json) | verdict |
|---|---|
| UJ1b-binary: update 21 PosZ 0, `ForceZ` -327680, `IsOnGround` 1, contact 0; update 22 `ForceZ` 0, contact 1 | **confirmed** [M] |
| UJ-7w: `37` at s+4, `0x25` executes s+4..s+23 (20 executions), returns at s+23, PosZ 1..22 = UJ-1 list, update 23 PosZ 0, contact 1, `IsOnGround` 1 | **confirmed** [M] (`37 [3]`) |
| TR-V exact: `wait[^1] - drop == 16`, PosZ - floor = 491520 - 32768 k | **confirmed**; precision: 15 in-air values (ticks t..t+14), the floor is reached at update 16 = tick t+15; contact is **never** raised with the `1B [0,0]` tail (17 only without it) |
| Ledge: first tick in the air = 5 | **confirmed** [M]; DLL value 4 by arithmetic only |
| A10: `Range(597, 21)`, 617, `Range(620, 21)`, 640, flights 594+i and 617+i, rest 639/640 | first jump **[M]**; second jump **[I]** (hypothesis H, section 2.6) |
| Guards: A12 `F+23`, A3, stairs, six gold traces | A12 and A3 consistent with the replays; stairs and traces are DLL-side and were not touched here |

## 4. What cannot be settled tonight

- Every DLL value above: no `dotnet` (tests are running elsewhere). The pins marked "DLL **[I]**" must be measured before being written.
- `UpdateAnimation` on real sprite bytes (second jump of Bergus), the B/C scheduler of the DLL, the real dialog pacing: not part of the existing emulation (see `notes.md`, section 4).
- The walks before the jumps (Giles) and the XY forces of NPCs in general are *injected*, not produced by the movement opcodes `0x0B`/`0x09`.
