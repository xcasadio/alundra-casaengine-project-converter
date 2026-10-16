# The top of a ladder (annex of the open point O-E20-17)

Evidence for the open point O-E20-17 of `docs/plan-e20-heros.md` (section 2): on 2026-10-16 the author reported that at the very top of a
ladder Alundra fell back to the bottom instead of walking onto the ground; a later try showed the ladders passing. A test of the real
world of map 14 (ladder (45, 27), every display rate from 30 to 120 Hz, Up held) reached the top with the DLL of that day, so nothing
was changed. Against the binary, two gaps remain at the tick level, both without a visible effect in that test: the DLL climbs one tick
more (its guard `PosZ <= H` where the binary's, in the DLL's Z, is `PosZ < H`) and zeroes ForceZ at the exit, handing the vertical to
the engine, where the binary keeps the climb's `0x10000` and decays it in the tick (`+0.5 px`, then the apex, then the step of `0x2C`).
The test and the fix that would follow the binary were not committed.

## Oracle

`oracle/scripts/` holds four scripts written for this slice on top of the E20.d run-oracle harness, which they use unchanged:
`ladtop.py` (the three scenarios below), `ladprobe.py` (the guard of the climb, tick by tick), `ladwatch.py` (the stores of PosY, the
terrain and PosZ at the top) and `ladmut.py` (what-if runs on the real code). To run them, copy
`docs/plan-e20-annexe/e20-d/oracle/base` to a scratch folder, copy the four scripts into that copy and run, from it,
`python -B ladtop.py --cross`, then `python -B ladprobe.py`, `python -B ladwatch.py` and `python -B ladmut.py`; they write
`../out-ladtop/`. Two runs from two copies gave identical outputs, equal to `oracle/out-ladtop/`, and the second interpreter (`--cross`)
gave no mismatching tick in A, B and C. `oracle/checksums.txt` holds the SHA-1 of every file of the annex's oracle.

Layout of the scenarios (synthetic): a cliff of height 10 (Z 160 px) on rows 0 to 19, a platform of height 2 (Z 32 px) from row 20, a
strip of ladder cells (word `0x0C00`) on row 20; the hero starts at (100, 392) facing north. Every number is the emulated binary's.

- **A, Up held** (`A-up-held.json`, `.txt`): the climb (`0x0E`, ForceZ `0x10000`, +1 px per tick) ends at tick 170 at Z 160 + 1 unit;
  tick 171 is the exit: Idle, ForceZ `0x8000`, Z 160.5; tick 172 is `0x2C`, ForceY `-50176`, ForceZ 0, Z 160, on the ground, the box in
  row 19; tick 173 is Moving.
- **B, Up let go after the exit** (`B-release-at-exit.*`): tick 172 is `0x2D` with no step; the hero falls back to the foot.
- **C, a short climb** (cliff of height 4, `C-short-up-held.*`): the same pattern, exit at tick 75, on the top at tick 76.
- **M1** (`M1-exit-forcez0.txt`, from `ladmut.py`): the exit also writes ForceZ = 0, what the DLL did. Tick 171 falls to Z 159.5, tick
  172 is blocked by the north corners (ForceAdjusted 1) and the hero falls to the foot, then climbs again, in a loop: the author's
  symptom.

`oracle/check/` holds a second, independent harness written by another agent: `ladder_oracle.py`, which runs inside a copy of
`docs/plan-e20-annexe/d1-oracle-moveplayer/tools` (same files, plus this script), and its outputs `ladder_up.txt` (a 32 px climb onto a
walkable top: exit at tick 49 with ForceZ 32768 and Z 32.5, `0x2C` at tick 50 on the top, Moving at 51), `ladder_up_h4.txt` (a 64 px
climb, same pattern) and `ladder_release.txt` (Up let go at tick 50: `0x2D`, back on the foot at tick 61).

## Facts read in ALUN_CD.EXE (France)

- The climb's guard (`0x80032270`-`0x8003228C`): `0x8003A9E0(hero, 0, 0xFFFF0000)` gives H, the highest height of the four corners of
  the box moved one pixel north (`height << 20`); `slt v0, H, PosZ` exits to `0x80031E7C` when `H < PosZ`. Otherwise ForceZ = `0x10000`
  (`0x8003229C`), TargetAnimationId = `0x0E` and the gravity bit cleared (`0x800322A8`). In the DLL's Z (the binary's minus one unit,
  ADR-0026) the climb goes on while `PosZ < H`.
- The exit (`0x80031E7C`-`0x80031E80`, `j 0x80032914 ; sw zero,0x88(s0)`) writes only TargetAnimationId = 0 (Idle). ForceZ (`+0xB8`)
  keeps the climb's `0x10000`; the gravity bit (`+0x6C & 0x100`) is the one `0x80031C38` set at the head of the tick.
- The order of a tick: MovePlayer (`0x800386EC`), the animation pass (`0x80038E18`), the physics (`0x80038364`): the forces
  (`0x80036828`: with the gravity bit, ForceZ -= gravity << 8, bounded by ZViscosity << 8), then MoveEntity (`0x80037E34`): the Z step
  (`0x800375E0`) before the XY step (`0x80037730`).
- The XY step: no step without an XY force (`0x800377A0`-`0x800377A8`); the step rule (`0x8003783C`-`0x80037898`), only with the gravity
  bit and ForceZ == 0, sets PosZ on the terrain under the new box when it is within 3 px; the walls of the hero (`0x80037488`): a corner
  blocks when its height is at or above the foot.
- The tick after the exit: TargetAnimationId 0 goes to the Idle and Moving case (`0x80031E38`), then the common tail (`0x80031EA8`):
  in the air (`+0x144 == 0`) with a direction held, `0x2C` (`0x80031F4C`); without one, `0x2D` (`0x80032600`).

## Census of the ladders

149 ladder cells (slope 6, ground property 12 or 28) in 46 of the 483 exported maps. For 148 of them the top is the walkable flat ground
of the cell just north of the ladder, at exactly the guard's height; the one exception is map 424, cell (20, 47), whose north cell is
not walkable for the hero (walkability 65): refused in the binary as in the DLL. Map 389 (the ship, the start of a new game) has the
ladders (15, 55) and (21, 55) (80 px to 128 px, the only exits of the starting room), (19, 38) (128 px to 176 px) and (18, 36). The test
of 2026-10-16 used the ladder (45, 27) of map 14 (192 px to 288 px), reached by the real-world arcs of map 14.
