# Hit-pass oracle with the hero and a real target (summary; full notes in oracle/notes.md)

> Report returned by one agent of the E20.c1 discovery workflow of 2026-10-09, kept verbatim as evidence. The plan holds the reconciled facts.

# E20.c1 hit-pass oracle: hero swing and contact damage against real Beannoids

The oracle is built and runs end to end. All checks pass, including the D1 re-validation: S-A and S-B each match the annex, 216 values, 0 differences. Two complete runs give identical `out/` manifests.

**Run:** `python -B docs/plan-e20-annexe/e20-c1/oracle/tools/run_c1.py` (about 8 min, exit 0 = D1 PASS and every check passed).

**Files** (all under `D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-c1/oracle/`):
- `notes.md`
- `out/summary.txt`
- `out/H1.txt`, `H1-near`, `H1-both`, `H1-edge`, `H2`, `H3`, `H4a`, `H4b`, `H5` (each with a `.json`)
- `out/reach-sweep.txt`
- `out/data-boxes.txt`
- `out/diff2.txt`, `out/mutations.txt`, `out/load-delay-check.txt`, `out/manifest.txt`
- `out/d1/`
- `tools/run_c1.py`, `c1world.py`, `scen.py`, `sweep.py`, `checks.py`, `datareport.py`. The rest of `tools/` is the D1 tool set copied from `e20-b`. `interp2.py` is the only changed copy, with `jalr` stub support added.

## Setup
- **Real code:** all eight tick passes including the real hit pass, `MovePlayer`, `UpdateAnimation` over real banks, `ComputeNewHp` and `ResolveBalanceTarget`, `UpdateItemEffectState`, the spark routine, the random state `0x80098708`, and the hero's damage entry.
- **Beannoid spawn:** the Beannoid comes from the real spawner `0x8003A1B8`. Map 14 `SpriteRecords[79]` is laid out in RAM, and its map record is entity 0 of map 14 with XPos 20, YPos 11, Height 0, which puts it at (252, 96) px. Balance index is 335 at level D=0 (map 14's `Info.D`): MaxHp 9, one attack `0x01` power 1. Its status is set to 2 by the harness.
- **Stubs, journalled:** effects, sound, spawn and the effects pass.
- **Inert target:**
  - The real events pass chooses the program letter per entity and writes it to `+0x228`.
  - All 201 non-null handler pointers of the five tables, plus the map bytecode runner `0x8004205C`, are hooked with one no-op that journals the letter chosen and the slot.
  - So the Beannoid gets C every tick and D after a touch, but nothing runs: no move, no animation change, no reaction. Program A never runs because the status is set to 2.
- **Hero:** start (252−dx, 96+dy) px, facing east, Basic sword, new-game kit. The spawn's loot draw advances the random state, so tick 0 holds `0x24D2C78A` from seed `0x12345678`.

## Results
The hero's swing boxes (anim `0x10`) are: ticks 1-4 offset (0,0,0) 29×15×15; 5-6 (21,−8,0) 29×15×15; 7-11 (22,−16,0) 23×15×15; 12-21 (−5,−18,0) 23×15×15. The swing ends at tick 22.

- **H1 (dx 40, Square at tick 1)**
  - The first contact is at **tick 5**.
  - The target has `TouchingEntity` = hero, `+0x1D0 = 0x19`, and the hero's `HitCounter` is 1.
  - There are 3 sparks (effect 9) at (251.99998, 95.99998, 4) px, and the random state moves `0x24D2C78A` → `0x5AAD3C51`.
  - At tick 6 the counters pass clears the `HitCounter` and `TouchingEntity`, and the events pass chooses **D** for the target.
  - The boxes overlap on ticks 5-11, but only tick 5 is a hit because `+0x1D0` blocks the rest. The latch `0x80146DF8` is 16.
  - `ComputeNewHp` was called at tick 6 on copies of the machine. The attack is `0x81`, power 1, and the response byte is 16.
    - Empty equipment records give damage 1 (9→8).
    - Records filled by the real `0x800307E8` give damage **2** (9→7). They are the jacket (no attacks) and the sword record (attack 1, power 1), with slot 2 null.
    - The live memory equals the filled variant.
- **Close contacts**
  - H1-near (dx 30): contact on **tick 1**, the press tick, because the frame-0 box already overlaps.
  - H1-both (dx 20): both sides are hit on tick 1, with 6 sparks. The hero enters the hurt state at tick 2 (anim `0x31`, HP 10→9), which ends his swing.
- **H2 (dx 60 = reach + 1):** no contact and no overlap in 40 ticks. H1-edge (dx 59) still hits at tick 5.
- **Reach (364 sweep runs, dy=0):** contact for **dx in [−9, 59]**. First contact is at tick 1 for dx −9..38 and tick 5 for dx 39..59.
  - Y window: dx 20 gives dy in [−21, 24]; dx 40 gives [−13, 22]; dx 55 gives [−13, 14].
  - The Beannoid's own body box touches the hero for dx in [−18, 20].
- **H3 (hero walks east from 60 px)**
  - Contact is at **tick 17** (hero x 232.21875). The hit lands on the hero: `+0x1D0 = 25`, `TouchingEntity` = slot 1, the Beannoid's `HitCounter` is 1, and 3 sparks appear at (242.60936, 95.49998, 6.5).
  - At tick 18 the real damage entry runs inside `MovePlayer`:
    - anim `0x31` (AnimFlags `0x40`, sound 23), `TargetDirection` 24→8, HP 10→9, ForceX −196608;
    - `+0x1CC` reads 119 (0x78 set, then decremented by the same tick's counters pass; this is an inference);
    - anim `0x1C` at tick 22, Idle at 33, walking again at 34.
  - The Beannoid is unchanged.
- **H4a (Square at ticks 1 and 23):** hits at **ticks 5 and 30**, which is 5+25. Overlap ticks 6-11, 27-29 and 31-33 are blocked by `+0x1D0`.
- **H4b (Beannoid in anim 6):** no hit in 40 ticks.
  - `+0xB4 = 0x40` comes from the real `UpdateAnimation` after the harness writes `TargetAnimationId` = 6.
  - AnimFlags is animation-set byte `0xD`, which the decoded JSON calls `Acceleration`. Beannoid sets 5-14 and 16-18 have the bit.
- **H5 (targets at +40,0 and +52,+8):** both hit at tick 5, hero `HitCounter` 2, 6 sparks. D is chosen for both at tick 6.

## Checks of the oracle itself
- **Independent model:** a separate model of plan 0.2.5 equals the real hit ticks for every entity pair in every scenario and for all 364 sweep runs.
- **Two interpreters:** rows, hero and target slots after every tick, journal, experiments and the random word are identical on `emu.py` and `mips.py`.
- **Mutation tests:** 10 mutations, each with its expected change written before the run, all caught (class-mask bits, the `+0x1D0` guard, the AnimFlags `0x40` test, the `0x19` value, spark count 3→2, the equipment term, counters pass skipped, hit pass skipped, hero box 1 px wider moves the reach by exactly 1). Two controls change nothing relevant.
- **Load delay and unwritten bytes:** no load is followed by a use of its register, and there are no unwritten-byte reads in any scenario.

## Limits
- The targets are inert, so there is no hurt animation, knockback, HP loss or death for them. Programs A, C, D and E of the Beannoid are UNKNOWN (E14).
- `ComputeNewHp` is called at the D stub entry, on copies. The real D handler `0x8007D9A4` is not run.
- Effects 4 and 5 never occur here, because the attack attribute is 1.
- Only the Basic sword, one direction, and a flat field are covered.
- The per-frame random advance is not modelled.

## Incidents
- A stray Python path in my debug script created `D:\tmp`, and I deleted it with `rm -rf /d/tmp`. I cannot confirm it did not exist before, so anything that was in there is gone.
- `git status` shows `m CasaEngineMonogame` and `? alundra-datas-analyser`. I did not touch either.
