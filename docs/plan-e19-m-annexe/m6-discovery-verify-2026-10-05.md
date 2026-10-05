# E19.m6 discovery: verification (refutation pass)

Read-only, 2026-10-05. Repository at `d353136` (`chantier/e19-opcodes`), engine submodule at `a6efd2a9`. Nothing was built, run,
exported or edited in the repository. No git state changed. The engine's `CasaEngine.Launcher/Program.cs` was not touched.
Scratch files in this folder:
- `engine_contact_f32.py` / `.out.txt`: an independent float32 emulation of the engine's blocked-axis contact.
- `pattern_check.py` / `.out.txt`: mutation anchors counted at HEAD.
- `slide_model.rerun.txt` / `binary_check.rerun.txt`: reruns of the previous scripts (byte-identical to the originals).
- `cxy_dis.txt`: disassembly of `0x80037730`-`0x80037E30`.

Verdicts: CONFIRMED = re-read or recomputed and found to hold; REFUTED = contradicted by evidence; UNCONFIRMED = not provable
without a run, or the evidence is missing.

## Context claims

| Claim | Verdict | Evidence |
|---|---|---|
| HEAD `d353136`; the listed scope files have no commit in `bafbd5a..HEAD` except `AlundraTurnOrderTests.cs` (`d3e206f`) | CONFIRMED | `git log bafbd5a..HEAD -- <file>`: 0 commits for each listed file, 1 for TurnOrder |
| "E19.f2a touched none of them" | Partly REFUTED (wording) | True for the listed files. But the report also cites files that did move. `AlundraEventProgramRunner.cs` was changed by `0c151b6` (m0), `d3e206f` (m1) and `7ba6e40` (f2a). `AlundraEventProgramRunnerTests.cs` and the three dialogue test files of the m1 item were changed by `7ba6e40`. Every cited line in them still holds at HEAD: `:666-673`, `:419-420`, `:1737-1752`, `:38`, `:57`, `:70`, `:44`. No impact. |
| Slide code unchanged since `a09cd66` | CONFIRMED | `git diff a09cd66 HEAD -- AlundraScriptedMotion.cs`: hunks only at 238-310 (vertical tick). The engine pointer moved `22228ffd` → `a6efd2a9`, but `CharacterControllerComponent.cs` and `Entities/Systems` have an empty diff between the two. |
| The h4 mutation anchors are unique at HEAD | CONFIRMED | `pattern_check.out.txt`: count 1 for sign_s, sign_e, gateent, accord, obl_table and direct (3 anchors). The R2 M6 anchor (`:157`), the two UO anchors (`:668`, `:672`) and the ladder anchor (`:292`) are also unique. |
| The models reproduce T-SL1 and T-SL3 | CONFIRMED | Reruns are byte-identical. The float32 emulation reproduces the T-SL1 tick 8 pin (11993088) and the T-SL3 tick 11 pin (17956864). |

## Per item

### M-19a (`AlundraPlayerManagerTests.cs:95-110`): rename only. CONFIRMED.
- The name contradicts `:98-99` and `:105` (0x2D + Right → 1).
- Production: `AlundraPlayerManager.cs:300-308` (jump-state branch); `:417-418` (others untouched).
- SJ3 (`AlundraHeroJumpStatesTests.cs:194-220`, `AirStill` + Right → Moving) covers the 0x2D half.
- The generic mutation that sends every other animation through the tail is killed by `AlundraPlayerManagerTests.cs:164-175`. The LoadingMap `if` at `AlundraPlayerManager.cs:271` is separate from the else-if chain, so 0x36 with Up would become Moving.
- Nuance: a targeted mutation that adds 0x20 to the jump-state list at `:300` would be killed only by `:107-109`. A rename keeps that assertion, so the recommendation stands.
- Plan citations of the old name: `:3692`, `:3942` and also `:3930`.

### M-19b (SJ4b, `AlundraHeroJumpStatesTests.cs:263-271`): rename and fix the comment. CONFIRMED.
- `:266` says "walking" but the hero is `JumpStanding` (`:267`).
- `JumpStartTickStamp` defaults to -1 (`AlundraEntityScriptProxy.cs:268`). `MotionTickCount` starts at 0 (`:357`) and is incremented only at `AlundraScriptedMotion.cs:218`.
- Mutating `!=` → `>` at `AlundraPlayerManager.cs:303` is killed by SJ3b (`:222-233`), SJ4 (`:239-261`) and SJ4b.
- No mutation was found that only SJ4b kills. This is an absence claim from reading, which is acceptable for a rename.

### M-19c (`AlundraLadderClimbTests.cs:423`): close without change. CONFIRMED. The value -65536 is UNCONFIRMED.
- UH-7 kills `else if (gravity)` → `else if (gravity && !fall)` (`AlundraScriptedMotion.cs:292`, `fall` is in scope from `:251`). At the first fall tick (15), `:355` expects ForceZ -32768 and `:353` expects PosZ 1015808. The mutant gives 0 and 1048576.
- The M-23 precedent (plan `:7610-7611`) applies.
- Derivation of -65536 is plausible by reading:
  - The pad is `default` (`:405`), so the climb gate (`AlundraPlayerManager.cs:250-259`, which needs `buttonsHold != 0`) does not fire.
  - The vertical is not external, so `HeadPullGroundTrusted` is true (`AlundraEntityScriptProxy.cs:1172`).
  - The motion system runs before the proxy (`:1125-1130`).
  - The fall is entered at update 1 (`AlundraScriptedMotion.cs:251-279`), then decays (`:292-297`).
  - The value also relies on exactly one logic tick per `Update(1/50)`, which the loop `:424-431` shows only for the later updates. Measure it first.
- Correction: the risk "D5b's F2 rule could move the entry tick" is very likely inapplicable. The F2 rule is "`IsOnGround` 1 while carried" (plan `:3854`), and the ladder hero is not carried. This does not change the recommendation.

### M-31 sign S (TSL7)
- Montage and DLL values: CONFIRMED by recomputation, and the engine contact is supported independently.
  - `Down` = 0x4000 → `DirectionByButtons[4]` = 0 (`AnimationTables.cs:55-59`); `OffsetY[0]` = 0x200 and `OffsetX[0]` = 0 (`:67-87`), so 312 × 512 = 159744.
  - The contact at 152.0 needs the far edge `BitDecrement(152.5 + 7.5)` = 159.99998 (row 9). At +1 unit it is 160.0 (row 10).
  - `engine_contact_f32.py` emulates float32 for the pre-probe, the 24-iteration bisection and the 4 recheck step-backs (`CharacterControllerComponent.cs:18-19`, `:1230-1297`), plus `GetCorners` (`:970-994`), `TrySampleGround` floor (`AlundraCellsCollisionField.cs:281-287`) and the proxy pull (`AlundraEntityScriptProxy.cs:2086-2087`). It gives exactly 9961472 at tick 8, a blocked pre-probe at tick 9, a slide to 17285120, and 10121216 at tick 24.
  - This is still an emulation, not a run.
- `sign_s` at tick 9 gives X 17186816: CONFIRMED (17235968 − 49152).
- The reason given for survival ("no test walks south into a single corner") is REFUTED. Arc **A14** does exactly that:
  - `AlundraArcSupport.cs:282-287`: "A14 at (25767936 ; 9961472) going south (direction 0, corner [2] alone)", `ArcsThatSlide["A14"] = 1`. A14 is `AlundraDay3SceneArcTests.cs:99-142`, `RealController: true`.
  - The h4 verifier's slide log (`scratchpad/slidelog.txt:1504-1505`) records that slide: `dir=0 … blocked=4`, `SLIDE (49152;0)`. It is the only south slide in the suite.
- "Survives today": UNCONFIRMED.
  - The only evidence is the h4 verifier's sentence (plan `:5837-5839`). There is no `sign_s` log in the scratchpad; the logs exist for gateent, accord, direct, obl_table and others, all full-suite 2474/2474.
  - A14 asserts only `AssertWithin` end positions and the slide count. The log shows no further south attempt after its single slide, so a mirrored −0.75 slide probably survives. But it may not: if it produces a second slide, the `ArcsThatSlide` guard fails.
  - This is decision-carrying. If A14 kills `sign_s`, TSL7 "kills nothing new", which is the M-23 precedent for closing.
- Binary: the slide sign is CONFIRMED in the binary. Jump table `0x80023734`, entry 0 = `0x80037A0C`. `c2 && !c3` → `+0xC000` at `0x80037A6C`/`0x80037A74`; `c3 && !c2` → `0xFFFF4000` at `0x80037BA8`-`0x80037BB4`. Stack slots `0x10`/`0x14`/`0x18`/`0x1C` = c0..c3, consistent with case 16 at `0x80037B1C`.
- "Same one tick later" matches the `cxy.py` port output (contact 9961469 at tick 8, slides at ticks 10-24). That is evidence from the port, not a new reading of the binary.

### M-31 sign E (TSL8): CONFIRMED (values and survival at h4); survival at HEAD is UNCONFIRMED.
- `Right` → 0x18; `OffsetX[24]` = 0x300 = 768, and 208 × 768 = 159744.
- Contact 229.0 (`229.5 + 10.5` = 240.0 → 239.99998, column 9). The float32 emulation agrees: 15007744, then blocked, then a slide to 11632640, then 15167488 at tick 24.
- The binary's case 24 (`0x80037BB8`-`0x80037C4C`) gives `c1 && !c3` → `+0x8000`, matching `:653`.
- The h4 slide log contains no east slide at all (266 `dir=24` attempts, all `blocked=10` → table FA).
- Only A10J, A14 and A18 may slide among real-controller arcs, because the guard fails on any other. So a new east slide at HEAD could only come from non-arc tests.

### M-31 entity gate (TSL9): CONFIRMED by reading; survival at HEAD is UNCONFIRMED (h4 log: gateent 2474/2474 green).
- `JumpHeroRig.Build(field, probeFactory, configure, x, y, …, freePad)` exists (`AlundraJumpTestSupport.cs:282-335`).
- `ContactWorld.AddEntity` (`AlundraContactTestSupport.cs:129-176`) builds the box [240, 264) × [160, 176), z 0..32, collidable.
- The probe is asked first (`CharacterControllerComponent.cs:1318-1324`). Its rounding (`AlundraMovementObstacleProbe.cs:46-47`) and the semi-open test (`AlundraEntityCollision.cs:61-77`) block exactly where the cell does: top 176 is free, 176 − 1 unit overlaps. So ticks 1-8 equal T-SL1.
- Every blocked bisection test reports the Wall, so `XCollisionEntity` is the Wall from tick 8 (`AlundraEntityScriptProxy.cs:2071-2077`).
- Ticks 9-12 are forbidden (`:595`) → FA 1, no slide.
- Under `gateent`, the slide's Move resets `XCollisionEntity` (`:2071`) and is free (hero top 176 against wall bottom 176). Result: X 17285120, 1 slide, FA 0.
- `CheckEntityInteraction` returns 0 for an entity without an F program (`AlundraPlayerManager.cs:688-711`). `BlockedByEntity` has no production writer.
- The cell is required: `BlockedCorners` is cell-only (`:676-700`). Without the cell, `slideX` is 0 → FA 1, the same as the gate.

### M-31 agreement (TSL10): CONFIRMED by reading; survival at HEAD is UNCONFIRMED (h4 log: accord 2474/2474 green).
- Tick 1:
  - Recompute with speed 208: target (0, −106496), steps 79872 / 53248, forces (−79872, −53248).
  - X pre-probe: `BitDecrement(250)` → 239.99998, column 9, blocked → FA 1 (`:2080-2083`).
  - Y goes to 13053952.
  - `agrees` is false (forceX ≠ 0) → return, FA stays 1.
  - Under `accord`, the cardinal branch sees Y progress → FA 0.
- FA is cleared at the head of each tick (`AlundraScriptedMotion.cs:180`). Tick 2 gives Y 12947456 and FA 0.
- The binary's FA is 1 at tick 1; its Y stays at 200.0 (from the port, O-E19-28 a).

### M-31 slide moved into `MoveControllerAndPullPosition`: CONFIRMED.
- The only production callers are `AlundraScriptedMotion.cs:518` and `:663` (rg). Test callers are `AlundraNpcCharacterControllerMoverTests.cs` and `AlundraMovementObstacleProbeTests.cs`.
- The h4 log shows `direct` 2474/2474 green.
- TSL11 values follow from reading: FA 1 and 0 slides; under the mutation, 1 slide, X 17285120, FA 0.

### M-31 oblique table term: CONFIRMED as unreachable in stage 1 (static argument) and in the h4 log (`obl_table` 2474/2474 green).
- With exactly one axis progressing, the final position is a candidate the engine validated in the same tick. The 16.16 pull is exact, near edges agree, and the far edges are exclusive by one unit on both sides.
- Off-tick changes, such as a 0x85 cell mutation under a standing hero, are the only theoretical route. That is a contrived montage.

### M-31 UO-1 through 0x42: CONFIRMED.
- `:666-673` at HEAD. `logic = entity.LogicEntity ?? entity` (`:419`). 0x42 writes `owner.LogicEntity` and returns 1 (`:1737-1752`).
- `FakeWorld` (`AlundraEventProgramRunnerLogicEntityTests.cs:28-47`) has a settable `PlayerEntity`.
- The only writers of `LogicEntity` are `:1745`, `:1765` and `AlundraWorldProxy.cs:2413`, so the word persists across calls. That gives CodeIndex 2 then 1, hero 0x2100 then 0x100, and 256 under the mutation.
- "Survives today" (reading): the production sites are the 178 `B[1]` (bytes 104-162) and the 185 `B[1]` (bytes 48-109). They contain no 0x42/0x43, and a map-event program runs with owner = player and logic = `mapEvent.Entity ?? player` (`AlundraWorldProxy.cs:2398-2419`; `Entity` starts null). So logic == owner and the swap is invisible in arcs.

### M-32 overlay half: test and values CONFIRMED by reading; "no test catches `:157`" UNCONFIRMED but corroborated.
- Fixture:
  - `CreateSyntheticComponent` (`:783-827`) maps raw 60 → local 5 and raw 61 → local 9.
  - `ApplyFloor` (`WallPlacementOverlay.cs:306-351`) strips (1,4) and submits key `(7,0,0)`.
  - `Create` (`AlundraCellVisualSync.cs:139-162`) seeds the model and `_nextFloorStableId` = 1, and excludes (1,7) from `_flatFloorCells`.
- Emptying removes the entry (`:242-248`).
- The refill is adopted with stable id 1 and depth slot 0 (`:282`, `:314-315`, `:454-458`), at `drawY` 4 (`:299`), so the entry is (0,5,1,4) with key `(7,0,1)`.
- Under M6, (1,7) is in `_flatFloorCells` → height 3 → the degraded warning, no entry (`:255-279`).
- The R2 verifier's own probe with this exact M6 mutation (`scratchpad/r2verify/mut.py`, `p2_M6.txt` against `p2_fixed.txt`): "tried=20 adopted=0 degraded=1" against "adopted=20". That is consistent with the P3 "no test covers it" (plan `:6599-6600`).
- No arc asserts warnings beyond "bare entity" (`AlundraArcChecks.cs:61-67`).
- Missed detail, not decision-carrying: removals at `:289-293` (unmapped tile id) and `:305-309` (off-map) also make a later refill hit the mutation.

### m1 advisory: close. CONFIRMED, with a count correction.
- There are 28 `IEntityWorldContext` implementations in 27 files. 27 of them are `private`; one is `internal sealed class HeadlessIntroSimulation` (`IntroTraceHarnessTests.cs:303`). `AlundraBackgroundLayerMaskTests.cs` holds two.
- `FakeEntityWorldContext` is private at `AlundraEventProgramRunnerTests.cs:38`, with 85 occurrences in the file.

### Ownership: CONFIRMED.
- D5b keeps SJ-12, M-16 and M-17 (plan `:7615`). None of the m6 files is in D5b, h1b2, h2, §1.2w or f2b.
- `AlundraHeroSlideTests.cs:18` sits in the music collection.
- The only m6 contact with support files is read-only use of `JumpHeroRig.Build` and `ContactWorld.AddEntity`.

## Corrections to carry into the Plan
1. M-31 sign S: the survival reason is wrong, because A14 is a south single-corner slide in the suite. Before adding TSL7, run `sign_s` on the whole suite (`h4v_mut.py sign_s`, no filter). If A14 turns red, TSL7 kills nothing new: close it like M-23, or keep it only as a local pin, which is the author's call.
2. Survival at HEAD of sign_e, gateent, accord, direct and obl_table is evidenced only at h4 (2474 tests; the suite has grown since, with 18 new test files). The executor's red-first runs are the measurement.
3. "E19.f2a touched none of them" is true for the listed tests only. The runner and the m1 files moved, but their cited lines hold.
4. m1: "28 private" should read "27 private plus 1 internal harness, 28 implementations in 27 files".
5. M-19c: drop the D5b F2 risk, because the F2 rule concerns a carried hero. The -65536 value stays "measure first".
6. TSL7 and TSL8 exact contacts: an independent float32 emulation agrees with the report. Still measure first; a contradiction stops the slice.
