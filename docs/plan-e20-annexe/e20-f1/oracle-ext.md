> Report returned by one agent of the targeted read-only discovery for E20.f1 (night of 2026-10-10), kept verbatim as evidence. The plan holds the reconciled facts.

# oracle-ext: report for slice E20.f1 (charge and release)

Scratch folder, referred to below as `<S>`: `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-f1/oracle-ext/`

I extended a copy of the discovery oracle and re-ran the five charge probes with it. In all five, the rows equal the stored probe row by row. The repository was only read. No dotnet build or run was done.

## Things that change the plan

1. **Aura condition (blocker 1), confirmed by running the binary.**
   - FACT, `0x8002F570-0x8002F590`: handle null creates; otherwise `lbu 0x7D(handle)` equal to 0 jumps to `0x8002F5C4` (no create); a non-zero value falls into the create.
   - Run A0: with +0x7D left at 0, C2-steel has exactly one effect-3 create (t33) over the 50 charge ticks t33..t82.
   - Run A1: +0x7D := 1 before t50 gives exactly one new create at t50. Run A2 (flag before t60 and before t70) gives [t33, t60, t70].

2. **Re-creation is the normal case on a long hold, not an edge case.**
   - FACT, exported bank `alundra-project/Data/effects/effects-global.json`: effect 3 animation 0 has 24 frames of 4 ticks, so 96 ticks, with End = Destroy (animation 1 is Loop, 48 ticks). Effect 16 (0x10) is 7 frames of 2 ticks, so 14 ticks, End = Destroy.
   - FACT, `0x8003BD58-0x8003BD68`: the script's end op sets +0x7D = 1 (also at `0x8003BC40` if the bank lookup fails).
   - FACT, `0x8003C444-0x8003C47C`: the effect pass frees a slot whose +0x7D is non-zero. The old aura therefore overlaps the new one for one tick.
   - INFERENCE: both creates use animation index 0 (`a2 = 0`), so a continuous charge re-creates the aura every 97 ticks.
   - Run A5 (C2-steel held 300 ticks, with a lifetime model, see §5): effect-3 creates at [t33, t130, t227]. The plan's example holds only 90 ticks and never exercises this, so T6 needs a 130+ tick hold.

3. **The create result is used synchronously, in the same tick and in the next one.**
   - FACT, `0x8002F61C`: a NULL from the hold-spark create skips the sound and both random draws.
   - FACT, `0x8002EBBC`: a NULL in the burst skips only that effect's field writes. The sound 0x2B still plays.
   - FACT, `0x8002F570`: a NULL aura leaves the handle at 0, so the create is retried at every charge tick. The sound 0x2A does not depend on the result.
   - Runs P1 and P2 show all of this (§4).
   - INFERENCE: the DLL host call must return slot-or-null at the tick it is made, so the next tick of the same frame sees the handle and the draw gating works. Queueing it in `RunFrame` cannot give that.
   - The pool is first-fit by index (`0x8003B9C4`) and reuses a freed slot immediately (A2: slot #0 is reused at t70).

4. **A release tick can hold one hold spark and then the 16 burst effects.**
   - This happens when timer = 60 and `+0x1C & 3 == 0` on the release tick, because `UpdatePlayerWeaponEffect` runs before `PlayerTryAttack`.
   - It occurs in C10, C14 and C16 at t101: 17 effect-0x10 creates, spark first (2 draws), burst after (no draws).
   - C2 and C6 release at t91 with `+0x1C = 90`, which has bit 1 set, so they have no spark there.
   - The plan's "random state after each spark" check must include this tick.

5. **The `+0x1C` tests read the value before the tick's counters pass.**
   - FACT (oracle): the hero's `+0x1C` is `tick - 1` while MovePlayer runs and `tick` after the tick.
   - Evidence: sound 42 at t33 (`fc = 32>33`), and the first spark at t85 (`fc = 84>85`).
   - The DLL must test the pre-increment counter.

6. **The oracle does not advance the generator per frame.** Details are in §3. Spark comparisons are valid only as (entry word, exit word) pairs, not as a free-running sequence.

7. **Effect Z values are in the binary's convention.** All Z values below are PosZ raw + 0x100000, where the raw rest value is terrain + 1. The DLL's PosZ is raw − 1, so DLL Z + 1 + 0x100000 is what is written.

## Files produced (all under `<S>`)

- `ext.py`: the extension.
  - The two effect stubs are removed.
  - The binary's own `0x8003BDD8`, `0x8003BE74`, `0x8003B9C4` (alloc) and `0x8003BB14` (init) run over a real 128-slot pool at `0x80138608`.
  - Observers journal arguments and the returned slot.
  - Rows gain `rng_before`, `rng_passes`, `rng_after`, `fc_in`, the aura handle with its +0x68 and +0x7D, and the end-of-tick slot fields of every created effect.
  - Options: `frame_rng`, `effect_free`, `effect_life`, `seed`.
- `probe_ext.py`: the runner. It writes `summary.txt` (161 lines) and `run_console.txt`.
- `out/`:
  - `C2-steel.txt`, `C6-william.txt`, `C10-crusher.txt`, `C14-ice.txt`, `C16-fire.txt`, each with a `.json` (per-tick data including `calls[].f`).
  - `C2-steel.frame-rng.txt`
  - `A0-aura-persist.txt`
  - `A1-aura-recreate.txt`
  - `A2-aura-recreate-twice.txt`
  - `A5-natural-expiry.txt`
  - `P1-pool-full.txt`
  - `P2-burst-5-free.txt`
- `out_baseline/`: the stored `probe.py` re-run unchanged.
- `out_orig/`: copy of the stored discovery outputs.
- Helpers: `probe_orig.py`, `mdis.py`, `xref.py`, `smoke.py`, `tools/`, and the disassemblies `dis_effect_create.txt`, `dis_effect_alloc_init.txt`, `dis_effect_update_one.txt`, `dis_effect_pass.txt`, `dis_update_weapon_effect.txt`, `dis_player_try_attack.txt`.
- `tools/` is byte-identical to the discovery copy, and `probe_orig.py` is identical to the stored `probe.py` (both checked with `diff`).
- To rerun, use `python probe_ext.py` from `<S>`, about 30 s. Do not use `-I`, because capstone lives in the user site-packages. The run is deterministic.

## 1. Pool model

- FACT, `0x8003B9C4`: the allocator takes the first of 128 slots (0x80 bytes) with +0x68 = 0, else NULL.
- FACT, `0x8003BB14`:
  - It copies the 0x80-byte template at `0x8013C608` over the slot and keeps word 0.
  - It sets +4, +0x30 = −1, +0x34 (0 for free effects, 1 when attached), +0x68 = 2, +0x6C..+0x71, and +0x3C/+0x40/+0x44 = X/Y/Z.
- FACT, `0x8003BE74` additionally sets +0x38 = the hero pointer and +0x60 = stack0, with attach offsets +0x48/+0x4C/+0x50 = stack1..3.
- Observed aura slot: `+34=1 +38=80127D30 +60=1 +48=0 +4C=0 +50=0x100000`, position = hero PosX/Y/Z.
- INFERENCE: the template is zero. The only code that refers to `0x8013C608` is the copy at `0x8003BB20`, and I found no writer.
- Setup creates no effects (`setup_effect_calls` = 0 in all five).
- Slots are never freed by the oracle, because the real effect pass `0x8003C410` is not run. The only free is the code's own `sw zero, 0x68(handle)` when the timer drops below 11.
- Five-probe peak use is 19-20 of 128 slots, with no NULL.
- Limitation: the stubbed pass `0x80038E84` itself calls `0x8003BDD8` at `0x80038FDC`, `0x8003905C`, `0x800390C0` and `0x800391F8`. Those effects, and the map-load effects, share the real pool and are absent here.

## 2. Comparison with the discovery outputs

- Row level: for each of the five scenarios, 0 differences across every `attack.ROW_FIELDS` key, `chg`, `ptr`, `cnt`, `weapon_rec`, `pos_z_dll` and the events text. This includes the `b0` column.
- Text level, after removing the `b0=` and `pz=` columns on every side: C6, C10, C14 and C16 have 0 line diffs against both the regenerated stored probe and the extension.
- C2-steel has 1 diff, line t91. It is the old `brief()` printing the 18 events uncollapsed; the stored events equal the row's event list joined with " | ".
- **Why the stored probe prints no `b0=` column.**
  - FACT, `grep` and mtimes: only `out/C2-steel.txt` has `b0=` (150 lines). It has no `pz=` and no event collapsing, and was written at 23:23:46.
  - C6, C10, C14 and C16 (23:24:11-23:24:29) have `pz=` and no `b0=`.
  - The current `probe.py` was saved at 23:24:57, after every output. Its `brief()` has `pz=` and no `b0=`.
  - INFERENCE: C2-steel came from the first version of `brief()`, the other four from the second, and the file was edited again afterwards. No output was regenerated.
- The `b0` column is hero entity `+0xB0` (`F['animcounter']`, 1 at idle, 2 during the hold), not the weapon-table flag b0 at `0x80028D14`.

Per-scenario timeline from the extension (rng words are entry > exit of the two draws):

| Probe | aura | sparks | release |
|---|---|---|---|
| C2-steel | t33 slot #0 | t85 (12345678>24D2C78A), t89 (24D2C78A>AFBE9EAC) | t91, burst in slots #3..#18, no spark |
| C6-william | t35 | same ticks and words as C2 | t91, same |
| C10-crusher | t46 | t97 (12345678>24D2C78A), t101 (24D2C78A>AFBE9EAC) | t101: spark #2 then burst #3..#18 |
| C14-ice | t43 | t93, t97, t101 (AFBE9EAC>A3E4185E) | t101: spark #3 then burst #4..#19 |
| C16-fire | t43 | same as C14 | same as C14 |

- In all five, every slot field equals the formulas I recomputed from the disassembly and the image tables. That is 16/16 burst tuples at every release and every hold spark.
- Every spark's exit word equals the second LCG step. The word changes only at spark ticks.
- The aura handle is cleared at the release tick + 1 (t92 for C2), and slot #0's +0x68 is then 0.

## 3. Per-frame RNG advance and seed

- FACT, `tools/world.py:187`: the oracle ticks with `PASSES` = `80038634, 800386D0, 80038998, 800384F4, 80038E18, 80038364, 80038E84, 80039300`, plus the pad port (`world.py:198`). That is exactly the `jal` list of `0x8003B388`.
- FACT, coverage of the C2-steel run (`m.cov`, 3002 PCs): `0x8002BAEC`, `0x8002BD0C`, `0x8002BD34`, `0x8002E058` and `0x8003C410` were not executed. The RNG word changes only at t85 and t89.
- **Answer: the per-frame advance does not run in this oracle.**
- FACT, `xref.py jal`:
  - `0x8002BD0C-0x8002BD34` is inside the function that starts at `0x8002BAEC`.
  - Its `jal` callers are `0x8002C3E4` and `0x8002C404`, both in the loop `0x8002C3F4-0x8002C45C` of the function around `0x8002BFE0`.
  - It calls `0x8002E058` at `0x8002BC4C`, which calls `0x8003B388` (at `0x8002E108`) and then the effect pass `0x8003C410` (at `0x8002E110`).
  - The store at `0x8002BD34` is in a branch delay slot, so every call advances the generator once, after the tick: `x = x * 0x7D2B89DD + 0xE06A02E7`.
- INFERENCE: this is one advance per displayed frame. I did not check for `jalr` callers.
- The seed is a harness choice. FACT: `tools/attack.py:51` has the default `seed=0x12345678` and `:77` does `m.poke(0x80098708, seed)`. The stored C2-steel header shows `"seed": 305419896`. The EXE image itself holds `0xB017C93D` at `0x80098708`. Neither is the real in-game state at a spark, which depends on every draw since boot.
- MODEL `frame_rng=True` (C2-steel): the generator advances after each tick.
  - The t85 spark then enters with `02611B3C`, not `12345678`, and jitters differently (jx=105826, jy=-50943 instead of 86057, -93365).
  - Running the image seed instead gives x=4276880, y=8030340.
  - All field checks still pass in that mode.
- Consequence for T1: the DLL's spark entry words will differ from the oracle's until E20.e0b lands. A DLL test should set the generator to the oracle's `rng_entry`, then compare slot fields and exit word.

## 4. Excerpts

**Burst, C2-steel release tick t91.** Hero PosX = PosY = 6553600, PosZ raw = 1. Slots #3..#18, all 16 equal the formula.

| i | X | Y | Z | ForceX | ForceY |
|---|---|---|---|---|---|
| 0 | 6553600 | 8126464 | 1048577 | 344064 | 0 |
| 1 | 5650432 | 8006656 | 1048577 | 318080 | 87808 |
| 2 | 4885504 | 7665664 | 1048577 | 243264 | 162176 |
| 3 | 4372480 | 7155712 | 1048577 | 131712 | 211904 |
| 4 | 4194304 | 6553600 | 1048577 | 0 | 229376 |
| 5 | 4372480 | 5951488 | 1048577 | -131712 | 211904 |
| 6 | 4885504 | 5441536 | 1048577 | -243264 | 162176 |
| 7 | 5650432 | 5100544 | 1048577 | -318080 | 87808 |
| 8 | 6553600 | 4980736 | 1048577 | -344064 | 0 |
| 9 | 7456768 | 5100544 | 1048577 | -318080 | -87808 |
| 10 | 8221696 | 5441536 | 1048577 | -243264 | -162176 |
| 11 | 8734720 | 5951488 | 1048577 | -131712 | -211904 |
| 12 | 8912896 | 6553600 | 1048577 | 0 | -229376 |
| 13 | 8734720 | 7155712 | 1048577 | 131712 | -211904 |
| 14 | 8221696 | 7665664 | 1048577 | 243264 | -162176 |
| 15 | 7456768 | 8006656 | 1048577 | 318080 | -87808 |

The generator is unchanged at t91 (`AFBE9EAC` before and after).

**First hold spark, C2-steel.** Rows show `chg`, `fc=in>out`, and `rng=before>afterPasses>afterFrameAdvance`; the frame advance is off, so the last two words are equal.

```
t 84 chg=60 fc=83>84 rng=12345678>12345678>12345678
t 85 chg=60 fc=84>85 rng=12345678>24D2C78A>24D2C78A  effect_3BDD8(a0=0,id=16,a2=0,a3=0x640000,stack=640000,100001,...) | sound(428)
      call new id=0x10 slot#001 rng_in=12345678 x=8274740 y=4686300 z=1048577 fx=-86057 fy=93365 +68=2 +7D=0
      decode: s1=B806E07F s2=24D2C78A jx=hi(s1*0x60001)-0x30000=86057 jy=hi(s2*0x40001)-0x20000=-93365
              x=heroX+20*jx y=heroY+20*jy fx=heroFX-jx fy=heroFY-jy
t 86..88 rng=24D2C78A (unchanged)
t 89 chg=60 fc=88>89 rng=24D2C78A>AFBE9EAC  sound(429)
      call new id=0x10 slot#002 rng_in=24D2C78A x=5488860 y=7531420 fx=53237 fy=-48891 (jx=-53237, jy=48891)
```

- FACT, `0x8002F5EC-0x8002F71C`: the two draws, the 20× position scale, and the force = hero force − unscaled jitter.
- The sound is 0x1AC (428) when `fc_in & 7` is 4, and 0x1AD (429) when it is 0.

**Aura re-creation (A1).** +0x7D := 1 on the live aura slot just before t50, free pass modelled.

```
t 49 chg=27 fc=48>49 aura=80138608(2,0)  sound(42)
t 50 chg=28 fc=49>50 aura=80138688(2,0)  effect_3BE74(a0=0,id=3,a2=0,a3=0x80127D30,stack=1,0,0,100000,0,0)
      call att id=0x03 slot#001 @80138688 x=6553600 y=6553600 z=1 +68=2 +7D=0 [+34=1 +38=80127D30 +60=1 +48=0 +4C=0 +50=0x100000]
t 51 chg=29 fc=50>51 aura=80138688(2,0)
after t49 slot#000 +68=2 +7D=0 | after t50 slot#000 +68=0 +7D=1 (freed) | handle -> slot#1
effect-3 creates: [(33,0), (50,1)]
```

- Without the free pass (A1b), the old slot stays allocated; the creates are still [t33, t50].
- A0 is the same run with no poke: [(33, 0)] only.
- A2 gives [(33,0), (60,1), (70,0)].
- Control A3: with the timer below 11, the poke creates nothing and the handle is 0.

**Natural expiry (A5, MODEL).** `+0x7D` is set by the harness 96 ticks after creation.

```
t129 aura=80138608(2,1)   (a spark is also created this tick)
t130 aura=80138888(2,0)   effect_3BE74(...) call att slot#005 +68=2 +7D=0
```

- Creates are [(33,0), (130,5), (227,4)].
- There are 54 hold sparks (t85, t89, ... t297), and the peak is 5 live slots.

**Pool full.**
- P1 (pool filled before t30 and freed before t60; filled before t84 and freed before t88):
  - The aura create returns NULL at every tick t33..t59 (27 attempts); `sound(42)` still plays on its ticks. The create succeeds at t60.
  - The t85 spark returns NULL: no sound 428 and no draw (rng `12345678>12345678`). The t89 spark then enters with `12345678`, not `24D2C78A`.
- P2 (only 5 free slots at t91):
  - The burst creates for i = 0..4 get slots #123..#127 and the other 11 return NULL.
  - Sound 43, the pointer `0x80028C8E` and animation 0x18 still happen.

## 5. Models and limits

- FACT: `effect_free` models only the free path `0x8003C444-0x8003C47C`: +0x68 == 2, `[0x800DC4B8] & 0x48` == 0 and +0x7D != 0 gives +0x68 = 0.
- `effect_life` is a MODEL, not the real update `0x8003BBDC`: it sets +0x7D that many ticks after the creation tick.
  - The values 96 (effect 3) and 14 (effect 0x10) come from the exported bank's frame delays.
  - The exact tick is INFERENCE from `0x8003BC74-0x8003BD68`: the pass of the creation tick is the first update, frame k is fetched at N + 4k, and the end op is reached at N + 96.
  - A tick frozen by `& 0x48` shifts it.
- INFERENCE: the global bank is used because the aura's first argument is 0, so `+0x6C` = 0.
- UNKNOWN: whether other effects (map effects, enemies, the stubbed pass `0x80038E84`) fill the 128 slots during a real charge. The five probes peak at 20 and A5 at 5.
- UNKNOWN: effect 3's drawn size and the effect's depth key. The anchor and depth-key question (blocker 4) is outside this oracle.
