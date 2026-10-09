> Report returned by one agent of the E20.c1b-2 read-only discovery workflow of 2026-10-09, kept verbatim as evidence. The plan holds the reconciled facts.

# E20.c1b-2 cross-check (label: cross-check)

I worked read-only: nothing was built or run, and nothing in the repository was written. I re-derived every claim below myself, with my own decoder, cell and record dumps, a reach model and capstone. My reach model reproduces the c1a oracle sweep exactly: contact at tick 1 for dx -9..38, at tick 5 for dx 39..59, and no contact from dx 60.

Scripts are in `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20c1b2/cross-check/`: `dec.py`, `sweep.py`, `recs.py`, `cells.py`, `spr.py`, `place.py`, `bin.py`.

Notation:
- R1 = maps-115-116, R2 = maps-117-44, R3 = arc-scaffolding.
- H or N = the tick whose hit pass writes the hit. That is the press tick for every placement below.

## (a) Table of checks

### Tick arithmetic and semantics

| # | Claim (report) | Verdict | Evidence |
|---|---|---|---|
| 1 | `0x37 Wait n` passes n+1 ticks after its first call (R1, R2) | CONFIRMED | Binary `0x8003E3DC`: memo≠ptr → memo=ptr, count=0, return 0; else `old=count++; return old>=n ? 2 : 0`. Same in DLL `Wait` (`AlundraEventProgramRunner.cs:2601-2614`) |
| 2 | `0x00` Break ends the call with pc+1; `0xFF` stays put (R1, R3) | CONFIRMED (DLL) | Runner `:404-418` |
| 3 | `0x1C [1]` after `0x1A` (same tick s) on a Hold of L ticks passes at s+L+1 | CONFIRMED as pinned DLL behaviour; INFERENCE for these sprites | T-D1 pin (`AlundraAnimationClockDriveTests.cs:267-291`, L=24 → s+25) |
| 4 | Animation lengths, all FACT from the map JSON terminators | CONFIRMED except the pillar (row 5) | Lever sprite 49: anims 0/1 are 1×10 ticks Loop (term Delay 1); anims 2/3 are 10×3 = 30 Hold (term Delay 0, TIL 128), Sfx 218. Bascule sprite 52: anim 0 is 4×2 Loop; anims 1/2 are 6×1 = 6 Hold. Door sprite 72: Loop, Speed 64/128 |
| 5 | Pillar sprite 70 anim 3 is "a 40-tick Loop" (R2) | REFUTED | Terminator Delay 0, TransformIndexLow 0, so it is a **Chain to anim 0** (`AnimationEndClassifier.cs:51-63`). The DLL bumps `AnimCompleteCounter` on a chain end (`AlundraEntitySpawnFactory.cs:320-326`), so the pass at N+96 still holds (INFERENCE). The pillar then returns to anim 0 |
| 6 | D picked at H+1, C resumes at H+2; counters pass before the hit pass (all three) | CONFIRMED | `RunEntityBlock` order, `AlundraWorldProxy.cs:2655-2729`; pick logic `AlundraEntityScriptProxy.cs:1519-1608` |
| 7 | Map events run first in a tick, so a flag set by C/D is seen one tick later (R1) | CONFIRMED | `RunMapEventsPass :2887`. It is gated only by MenuOpen and Unused40, not by ControlLocked |

### 115 NW lever (rec 2)

| # | Claim (report) | Verdict | Evidence |
|---|---|---|---|
| 8 | Programs: D[1] @1476 `2B; 05 T2; FF`; C[1] @1200; B[1] @128 (R1) | CONFIRMED | `Codes[]`; jumps hand-checked |
| 9 | Door timeline: open sequence at H+34, frame C at H+55, frame D at H+76, Break at H+97 (R1) | CONFIRMED | Only H+33, which is the `0x1C` pass, rests on row 3 |
| 10 | Recs 0/1 (sprite 42) never spawn (R1) | CONFIRMED | SD 131/130 have bit 0x40 clear. `0x2D` in 115 targets only 19, 22, 24-27, 6-9, 21 |
| 11 | Door frame cells A/B/C/D (R1 §2.3) | CONFIRMED | (1..8, 21): A W1/H4, B W1/H3, C W1/H2, D W0/GP144/H1/Slope 4. Tiles 8514/8515 |

### 116 levers and bridge (recs 39, 40)

| # | Claim (report) | Verdict | Evidence |
|---|---|---|---|
| 12 | Toggle C/D programs; ClassA re-set at H+33 (R1) | CONFIRMED | Jumps 692→702, 703→720, 699/717/729→692 |
| 13 | Bridge cell steps at t0, +9, +18, +27, +36; control back at t0+45; retraction at r … r+36, control at r+45 (R1) | CONFIRMED | B[1] @174-225 and @241-293. No Break inside either sequence |
| 14 | The B[1] load write `0x85 [30,19,3,5,40,20]` is "identical to the export" (R1) | REFUTED (minor) | Cell (42,24) has GP 18 in the export and GP 2 after tick 1, copied from (32,23). Take baselines after frame 1 |
| 15 | Plates rec 7/8 never spawn (R1) | CONFIRMED | SD 128. The only `0x2D` in 116 is in C[46]/C[47], which no record uses. Rec 18 (sprite 45) does spawn but toggles T3 only |

### 117 bascule, lever 22 and pillar

| # | Claim (report) | Verdict | Evidence |
|---|---|---|---|
| 16 | Bascule state machine T1→T4→T2→T3→T1, the frame table and N+9/15/21/27 (R2) | CONFIRMED | D[1] jumps +24/+28/+32 and else-branch 1200. C[1] branches @519/@568/@638/@470. Initial facing SD 193 → 16 |
| 17 | F0 differs only by GP 130 at row 6 (R2) | CONFIRMED | (42,6),(43,6) GP 130; F1..F3 GP 2. Note: 130 = 0x82 sets the portal-floor bit (0x8000 in `CombinedVramFlagsAND`, `AlundraPortalTrigger.cs:59`). It only warps with a held direction and `PlayerControlFlags == 0` |
| 18 | 117 B[2] lock is about 21 ticks (R3) vs 24 ticks (R2) | R3 REFUTED, R2 CONFIRMED | 4 × `Wait 5` = 24 ticks. `0x10` at T0, `0x11` at T0+24 (pc 380). F0..F3 written at T0, +6, +12, +18 |
| 19 | Lever 22 timeline N+1/N+2/N+33; pillar N+34/N+55/N+96 (R2) | CONFIRMED for the lever; pillar N+34 is INFERENCE | The pillar tick assumes rec 2 runs before rec 22 in slot order. If not, the `0x36` passes at N+33 and the rest shift by -1 |

### Map 44 (recs 7-12, doors 0-6)

| # | Claim (report) | Verdict | Evidence |
|---|---|---|---|
| 20 | Phase-1 timeline: N+2 anim 2, N+33 T81, N+34 doors, N+99 control back (R2) | **REFUTED** | C[8] contains Breaks R2 left out: 1562, 1604, 1608, 1630, 1677, 1684, 1729, 1751, 1798. Corrected timeline in (c) |
| 21 | Phase 2 "same code" (R2) | REFUTED (asymmetric) | Phase 2 has no Break between the anim-0 set (1708-1723) and `T81 off` (1726). T81 clears at N2+34, not N2+35 |
| 22 | D[8] gated by G1633 (id 97 \| 6<<8 = 0x661); clears ClassA on recs 7-12 | CONFIRMED | — |
| 23 | Door paths and lengths (rec 0 W48 + S32; rec 1 E96; …) (R2) | CONFIRMED (spot-checked C[1], C[2]) | Speed 128 gives 1.5 px/tick E/W and 1 px/tick N/S, so 64 motion ticks for every door. Completion ≈ t+65 is INFERENCE |
| 24 | Setting G1634 activates entity 34 at tick 1 (R2) | CONFIRMED | B[3] @328 → 524 `2D [34]`, 526 sets G1634 |

### Opcodes, spawn gate, flags, arc harness

| # | Claim (report) | Verdict | Evidence |
|---|---|---|---|
| 25 | Unported opcodes: 115, 116, 44 none; 117 only `0xB9` @171 (R1, R2, R3) | CONFIRMED | Linear sweep of every table entry against the 154 `case` labels plus 0x00/0xFF |
| 26 | `0xB9` unreachable from both 117 arcs | CONFIRMED | B[1] needs hero tile in (0..13, 3..23) and T1000 (talking to book rec 20). Bascule tile (20,12) and lever tile (3,40) are outside |
| 27 | Spawn zone gate is load-time only (R3) | CONFIRMED | 10 `jal 0x8003A1B8`. `0x8003B308` passes a2=a0=0; `0x800646A0` passes a2=a1=1; the other eight pass `addiu a2,1`. DLL: `AdoptPlayerPawn` (:832) runs before the spawn loop (:857-870) |
| 28 | `ArcSpec.HeroTileX/Y` ignored for spawning; Arrival needed for 115/116/117 (R1, R2, R3) | CONFIRMED | `AlundraArcSupport.cs:209` then `:249-261` |
| 29 | Temporary flags in `ArcSpec.Flags` are wiped (R3) | CONFIRMED | Applied at :156-159; `InstallForMapEntry` (`AlundraWorldProxy.cs:783`) clears the temporary bank (`AlundraGameState.cs:351`) |
| 30 | Hero hit mask (R1 "1" vs R2 "0x801") | R2 CONFIRMED | Hero flags 0x3118C → `ClassMaskOf` = 0x801. R1's result is harmless: levers and bascule carry bit 0 |
| 31 | Balance 305/308: response 16 for nibbles 1-7, 128 for 8-11 | CONFIRMED | `balance.json` |
| 32 | R1 suggests reading T flags with `GetFlag(0x8001)` | REFUTED as an accessor | `GetFlag` returns the whole 32-bit word. Use `ArcChecks.IsTemporarySet(n)` (`AlundraArcChecks.cs:73`) |
| 33 | Arrival direction is a raw facing (0/8/16/24) | CONFIRMED | `proxy.TargetDirection = arrivalRecord.DirectionId` (`AlundraWorldProxy.cs:1930`) |
| 34 | Arc accessors (R3 §4) | CONFIRMED | They exist: `CellMutator` :511, `GetFloorTileId/GetHeight/GetWalkability/GetGroundProperty/GetWallTileStack` (`AlundraCellStore.cs:148-176`), `HitSparksForTests` :2784, `FramesOf`, `EntityByRecord` :336. **There is no slope getter**, so frame D's Slope 4 in 115 cannot be asserted without a new accessor |
| 35 | World names (R3) | CONFIRMED | `world-index.json` lines 46/117/118/119. 117 is `Tarn's Manor (unused broken)-117`; the label comes from the converter's `maps.json` (community naming, meaning not established) |

### Hero placements (computed: floor walkable at the lever's height, no GP 0x20, tick-1 contact)

| # | Placement (report) | Verdict | Evidence |
|---|---|---|---|
| 36 | 115: (84,120,16) W (R1); (82,120,16) W (R3); alternatives (60,120) E and (60,136,32) N (R1) | CONFIRMED | All on walkable floor at the lever's height, contact at tick 1. Spark centre for (84,120,16) recomputed exactly: raw (4128767, 8126463, 1802239) |
| 37 | 116: (876,424) W and (1116,424) W (R1) | CONFIRMED, but 1 px margin | (878,424) already overlaps the H10 block at (37,26). Prefer (866,424) W and (1106,424) W: tick-1 contact, centred in cells 35-36 / 45-46 |
| 38 | 116: (852,424) E and (1092,424) E (R1 mirrors) | CONFIRMED | — |
| 39 | 116 rec 39: (842,424,128) E (R3) | **REFUTED** | Body x [832,852] enters wall column x=34 (W 0x41) |
| 40 | 116 rec 40: (1082,424,128) E (R3) | **REFUTED** | Body enters cell (44,26), Height 10 = 160 px, 32 px above the lever floor |
| 41 | 117 bascule: (500,192,16) E (R2) | CONFIRMED | — |
| 42 | 117 bascule: (506,192,16) E (R3) | REFUTED (marginal) | Body x [496,516] overlaps the collidable bascule body [516,540) by 1 px |
| 43 | 117 lever 22: (72,640,32) E (R2) | CONFIRMED | — |
| 44 | 44 standoffs: R2 (lever.x−24, or (936,432) N for rec 12); R3 (lever.x−22, or rec 12 at dx 12 facing E) | CONFIRMED (all twelve) | R2's statement "rec 12: E and W fail" holds only at a 24 px standoff |

### Load-time interference

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 45 | 115 NW room: ME0 only; no lock, camera or dialogue | CONFIRMED | Tiles (3,7)/(4,7)/(5,10) lie only in ME0 |
| 46 | 116: no lock until T1 and T2 are both on; nothing else | CONFIRMED | `0x10` only in B[1]; no `0x67` or `0x0D` anywhere in 116 |
| 47 | 117 bascule room: B[2] lock (24 ticks) plus B[5] sounds | CONFIRMED | — |
| 48 | 117 lever-22 room: only B[6] (sounds) | CONFIRMED | — |
| 49 | 44 with G1633 set and G1634 clear: quiet away from the polled areas | CONFIRMED | B[1] does one check at tick 1 then sits at End. B[2] goes to End. B[3]/B[4]/B[5] poll areas away from the levers. C[17] waits T180 forever. C[18..22] teleport recs 17-21 to y 216-280 and wait T1050 |
| 50 | Nearby attackers | CONFIRMED none | Beannoids sit at least 56 px from every stand point, and no swing box reaches them |

### 44 levers before or after the Beannoid wave

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 51 | Can the levers be hit before or after the wave? | FACT: both, programmatically. UNKNOWN: in game | D[8] tests only G1633, which is set by the intro B[2] @321 when the hero reaches (5..6, 30), the doorway of lever 7's room. Nothing on the lever path reads G1634. The wave starts only on tile (43..44, 20); it raises pillars 30-32 and spawns recs 22-29. Those have native D (index 0), a no-op until E14.a, so in DLL play the wave never ends (B[3] @465-502 waits for 22-29 gone). Whether the walking route to any lever crosses (43..44, 20) is UNKNOWN |

## (b) Corrections

1. **Map 44 phase-1 timeline (R2 §3.3).** C[8] has Breaks at 1562, 1604, 1608 and 1630. Corrected values:
   - Hero anim 0 and levers anim 2 at **N+3**, not N+2.
   - `0x1C` passes at **N+34** (INFERENCE).
   - T81 on at **N+35**.
   - Doors start at **N+36**.
   - ClassA and control come back **one tick after** the last door T-flag (≈N+102, INFERENCE), not in the same tick.
2. **Map 44 phase 2.** T81 clears at **N2+34**, in the same tick as the `0x1C` pass, because there is no Break before 1726. Doors start at N2+35.
3. **117 load lock is 24 ticks**, not about 21 (R3).
4. **Pillar sprite 70 anim 3 is a Chain to anim 0**, not a Loop (R2). The end tick of `0x1C` is unchanged; the pillar returns to anim 0 afterwards.
5. **R3's stand points for 116 are wrong**: (842,424) enters the wall at x=34, (1082,424) enters the H10 block. **R3's (506,192) on 117** overlaps the collidable bascule by 1 px.
6. **116 baseline cells:** after tick 1, (42,24) has GP 2, not the exported 18. Read baselines from the cell store after frame 1.
7. **T-flag reads must mask the bit:** use `IsTemporarySet(n)`, not `GetFlag(0x8000+n)`.
8. **GP 2 on the 116 lever wings:** INFERENCE from the decompilation (read only), not UNKNOWN. GP 2 gives `Slope_18c` = 1, which `IsSlopeInAquaticTile` treats as aquatic. In `MovePlayer` the `switch (slope)` cases 1 and 2 just `break`, so the swing is allowed in the original as in the DLL. The binary would decide this for certain.
9. **Minor:** R1's 115 x window [54,101] is neither the box window ([50,101]) nor the standing window ([58,101], wall at x ≤ 47). Not decision-carrying.

## (c) Recommended acceptance design for E20.c1b-2

**Common rules.**
- `ArcSpec(..., RealController: true, Prefabs: true, Arrival: ...)` with `AnimationId = ResetAnimationId`.
- Assert `AssertNoUnexpectedError` and `AssertNothingSkippedOrExceeded` in every arc.
- Before the press: hero `IsOnGround == 1`, `PlayerControlFlags == 0`, `TargetAnimationId == 0`, and position equal to the placement.
- H is `arc.Frame` before `Press(Square)`. Trace frames are absolute; assert gaps between trace frames rather than wall-clock values.
- Sounds are optional. If you assert them, inject a recorder and use `Contains`.
- Values that rest on `0x1C` and on door motion are INFERENCE. Write them before the run. A mismatch is a stop and must be diagnosed; never re-fit the expectation to the run.

**A. 115 NW lever.**
- Spec: world `Tarn's Manor-115`; Arrival px (84,120), z `1<<20`, direction 8. Flags: none.
- At load: recs 0 and 1 are null. Rec 2 is at px (60,120) with ClassA set; its Z is 24 (INFERENCE, assert it). After frame 1, cells (4,6) and (5,6) are W1/H4/GP16 with tiles 8514/8515.
- At H: lever `TouchingEntity == hero`, `FrameCollisionTickCounter == 25`, `Hero.HitCounter == 1`, one spark call.
- At H+1: D pcs 1476, 1477, 1480 (slot 3). ClassA clear, T2 set.
- At H+2: C pcs 1200, 1203, 1206, 1208. T2 clear, target anim 2.
- At H+33 (INFERENCE): pcs 1210, 1212. T1 set, anim 1.
- At H+34: B pc 135 passes. Cells are W1/H3.
- At H+55: cells W1/H2.
- At H+76: cells W0/H1/GP144 (walkable).
- At H+97: pc 168 (Break).
- Exact gaps: 21 / 21 / 21, and "T1 frame + 1 = pc 135 pass frame".
- A second press after H+25 gives `HitCounter` 0 and no D in the trace.
- Control is never locked.

**B. 116 two levers and bridge.**
- Spec: world `Tarn's Manor-116`; Arrival tile (3,20), direction 8. Right after construction, `PlaceHero(866,424,128)`.
- At load: recs 39 and 40 present, recs 7 and 8 null.
- Lever 39, hit at H1:
  - H1+1: D[5] runs (1076, 1077, 1080, 1083); T1 and T101 set.
  - H1+2: C pcs 702, 703, 720; anim 2. B[1] pcs 164, 169, 163 (no extension).
  - H1+33 (INFERENCE): ClassA set again, T101 clear.
- Then wait until the hero is idle (H1+21 or later), `PlaceHero(1106,424,128)`, facing 8, settle.
- Lever 40, hit at H2:
  - H2+1: T2 set.
  - t0 = H2+2: B[1] pc 174, ControlLocked set, hero target anim 0. Cells (40..42, 20) become [H10/GP0/16574 | H10/GP0/16575 | H8/GP2/58146], with wall stacks [49979,49989] / [49980,49990] on x=40/41.
  - t0+9 / +18 / +27 / +36: rows 21 / 22 / 23 / 24, source (29,27), third tile 58173.
  - t0+45: ControlLocked clear (pc 225).
- Retraction:
  - Press lever 40 again at H3 ≥ H2+48.
  - r = H3+2: pcs 227, 232, 241. Row 24 is restored from (30,23) at r, then rows 23 / 22 / 21 / 20 at r+9 / r+18 / r+27 / r+36.
  - Control back at r+45. Final cells equal the frame-1 baseline.

**C. 117 bascule.**
- Spec: world `Tarn's Manor (unused broken)-117`; Arrival px (500,192), z `1<<20`, direction 24.
- Load:
  - Frame T0 (1): pc 284, ControlLocked. S0..S2 = F0 at T0, F1 at T0+6, F2 at T0+12, F3 at T0+18.
  - Pc 380 at T0+24, then control is free. A[1] has run: T1 set, T2..T5 clear.
- Hit N:
  - N+1: D pcs 1184, 1185, 1209, 1212, 1215, 1236, 1239. Result: T1 off, T4 on, T5 on.
  - N+2: TargetDirection 16, anim 1.
  - N+9 (INFERENCE): TargetDirection 24, anim 0, T5 clear, S1 ← F2.
  - N+15: S1 ← F1.
  - N+21: S1 ← F0, so GP(25,6) = GP(26,6) = 130.
  - N+27: ClassA set.
- Assert S1 through the row-5 wall stack (F0 is `…8678,8688,8698,8708` / `…8679,…`) and the row-6 GP.
- Optional hits 2-4, each at least 27 ticks after the previous one:
  - States become (F0,F3,F3), then (F3,F3,F0), then (F3,F3,F3).
  - Final facings 0, 8, 16.

**D. 117 lever 22 (could sit with C).**
- Spec: Arrival px (72,640), z `2<<20`, direction 24.
- N+1: T230 set, ClassA clear.
- N+2: T230 clear, anim 2.
- N+33 (INFERENCE): T231 set, anim 1.
- Pillar rec 2: `0x36` pass at N+34 (INFERENCE on slot order); N+55 anim 3; N+96 End (INFERENCE); TargetAnimationId back to 0.
- Do **not** assert pillar Z (UNKNOWN).

**E. 44 lever 7.**
- Spec: world `Wendels Nightmare-44`; Flags {1633}; Arrival px (120,440), z `8<<20`, direction 24.
- N+1: D pcs 2320 … 2353. T80 set, ControlLocked set, `Flags & 1 == 0` on recs 7-12.
- N+2: pcs 1556, 1559, 1562 (Break). T80 clear.
- N+3: pc 1563 (hero target anim 0), anim 2 on all six levers, pc 1584 first call.
- N+34 (INFERENCE): pc 1584 passes, anim 1 on the six levers.
- N+35: T81 set.
- N+36: every door executes its `0x1A [2]` and its first `0x1E` call.
- ≈N+101 (INFERENCE, door travel 64 motion ticks plus latency): T10…T70 set and pc 1630.
- Exactly one tick later: pcs 1631-1676. ClassA on six levers, T10..T70 clear, control free.
- Door final pixels from R2 §3.1, tolerance ±2 px.
- Control variant: an arc without G1633. The hit runs only 2320 → 2353; no lock, no T80.
- Phase 2 is optional. If included, assert T81 clear at N2+34.

**What stays UNKNOWN and how to settle it.**

| UNKNOWN | How to settle it |
|---|---|
| U-1. The `0x1C` pass (s+L+1) for L = 30 and L = 6 | The first trace frame of pcs 1210 (115), 478 (117) and 1586 (44) |
| U-2. Door travel in 44 | The frame of door `0x05 [10,128]` minus its start frame (INFERENCE 65) |
| U-3. Lever Z in 115 (24 vs 16) | Read `PosZ` at frame 0. Either value still gives contact |
| U-4. Slot order rec 2 vs rec 22 in 117 | The trace |
| U-5. Whether 116, 117 and 44 load headless | A smoke test before writing assertions: construct, 6 frames, `AssertNoUnexpectedError` |
| U-6. In-game reachability: 115 via the hall; 116 behind the grate (E14.a); 117 (unused?); 44 versus the wave route | Author play-test or a route search; outside the arcs |

## (d) Split and stop conditions

**Split: yes, recommended.**
- **c1b-2a** (deterministic program and cell timelines, needs only the arc scaffolding):
  - a load smoke test for all four maps;
  - arcs A (115), B (116) and C (117 bascule).
  - Every expected value except U-1 is fixed by bytecode.
- **c1b-2b**:
  - arc E (44), whose acceptance rests on door motion (U-2), is about 230 frames, and needs G1633;
  - arc D (117 lever 22 and pillar, pillar motion UNKNOWN).

**Stop conditions.**
1. A map fails to load in `ArcRun`, or needs a loader that requires a `GraphicsDevice` or an engine change. Stop and ask. Never touch `CasaEngine.Launcher/Program.cs`.
2. The measured `0x1C` pass differs from s+L+1, or the door travel differs from 64 motion ticks by more than 1. That is a DLL clock or motion finding outside c1b-2, so stop and diagnose.
3. Asserting Slope (115 frame D) needs a new accessor on `AlundraCellStore`, which is a production change. Either drop that assertion or get the author's agreement.
4. Author decisions:
   - keep or drop 117, given the "(unused broken)" label and uncertain in-game reachability;
   - `0xB9` remains a skip by size (it is not on any lever path);
   - whether in-game recipes for 116 and 44 wait for E14.a (the grate, and the wave if it lies on the route).
5. Any trace showing a skipped opcode on a lever path contradicts this check, so stop and re-check.
