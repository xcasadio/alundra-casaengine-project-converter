# D1: the MovePlayer oracle (summary; full notes in ../d1-oracle-moveplayer/notes.md)

> Report returned by one agent of the E20.0 workflow of 2026-10-09 (docs/plan-e20-heros.md section 1.2), kept verbatim as evidence.
> Where it disagrees with its check or with the critic, the plan section 0.2 holds the reconciled fact.

**D1 result: PASS on S-A and S-B.** The oracle runs MovePlayer and the tick passes as real code from `ALUN_CD.EXE` (France) in an emulator and computes the trace. Only `tools/compare.py` reads the annex values. The comparison covers 24 ticks × 9 fields = 216 values per scenario, with 0 differences:

- current animation
- ForceX, ForceY, ForceZ
- PosX and PosY minus the start position
- PosZ, compared as oracle − 1 (the +1 convention)
- IsOnGround
- CollidedWithEntityZ

There is no first differing tick or field. The S-B warm-up takes 3 ticks and ends with two Moving ticks at ForceX 159744.

**Pass table as built** (order of the tick function `0x8003B388`, `[0x800DC4B8]` = 0):

| # | Pass | Decision |
|---|---|---|
| 1 | `0x80038634` slots | real code |
| 2 | `0x800386D0` events | real code. `jal 0x80031b50` (MovePlayer) runs first; the entity loops are not entered because `[0x80134360]` = 0 |
| 3 | `0x80038998` counters | real code, all slots |
| 4 | `0x800384F4` lists | real code |
| 5 | `0x80038E18` animation | real code. It runs `0x80038AB4` on the hero over the hero bank |
| 6 | `0x80038364` physics | real code, nothing stubbed |
| 7 | `0x80038E84` effects | stub, no effect, journalled |
| 8 | `0x80039300` hits | stub, no effect, journalled |

- **MovePlayer callees:** all real code (PlayerTryAttack, TryUseItem, PlayerTryAction, CheckEntityInteraction, `0x8002F49C`, `0x8002F768`, `0x8002F884`, `0x80031A68`, CreatePlayerAnimationEffects, `0x8003634C`, UpdateItemEffectState).
- **Rider copy `0x80037364`:** `zsim.py` stubs it; here it is never entered.
- **Other stubs, each journalled:**
  - sound `0x800490FC` notes the id.
  - effects `0x8003BDD8`/`0x8003BE74` note id and arguments and return a zeroed dummy slot at `0x801E8000`.
  - spawn `0x80039F58` returns 0.
- **S-A journal:** sound 10 at tick 1, sound 0 at tick 2, then sound 11 and 3 dust effects at tick 22.
- **Coverage of MovePlayer:** none of the early exits is taken: `+0x20` ≠ 0, locked branch, damage, death, `+0x188 & 0x80`.
- **Random generator:** `0x800A8708` is seeded with `0x12345678` and never consumed in S-A/S-B.

**`0x80038634` verdict: real code, not a stub.**
- FACT: for each of the 64 slots it reads `+0x10`. Status 4 promotes the staging entity at `0x80134368` into the slot and sets its index. A non-empty slot sets last = i. It then writes `[0x80134360]` = last. That word is the entity count read by `0x800384F4`, `0x800386D0` (`0x80038700`) and `0x80038998` (`0x800389AC`).
- It reads the hero's slot, so I run it for real. With the hero alone its effect is the constant 0: skipping it leaves the traces identical (`out/mutations.txt`).
- `pass38634.py` runs it on prepared arrays (`out/pass-80038634.txt`): the hero alone gives 0, slots 0, 5 and 9 give 9, and a status-4 slot receives the staging entity.

**Pad-routine verdict: match.**
- The binary routine is `0x8002E250(a0 = pad struct, a1 = new state)`, called by `0x8002E38C`. The formulas are `new & (new ^ old)` stored at `+0x12` (pressed) and `old & (new ^ old)` at `+0x14` (released). The repeat logic is also the same: reset when old ≠ new or old = 0, silence for 20 ticks, then every tick since RepeatInterval is 0.
- `AlundraTickPad.cs:57-58` match these formulas exactly; the repeat logic (`:60-101`) matches too.
- `padcheck.py` ran the real routine against a Python port of `AlundraTickPad.Update` on the S-A and S-B hold sequences, a long-hold sequence and 40 random sequences of 300 ticks. All four words are equal on every tick (`out/padcheck.txt`).
- The oracle repeats this cross-check on every tick of every run.

**Hero data:**
- **Hero slot:** built by the real `InitializeEntity` `0x80039D04` over the real balance blob (disc `DATA/BALANCE.BIN`, its 512 offsets match the decoded JSON). Then status = 2 and HP = 10. Full dump in `out/start-state.txt`.
- **Stats and inventory:** built by the new-game code: InitPlayerStats `0x8004DAC0`, AddItem for items 1, 17 and 25, then SetPlayerWeaponId(1). WeaponId = 1 and boots level = 0.
- **Bank source:** `data-extracted/data/map_alundra.json`, `SpriteInfo.SpriteRecords[0]` (Sector5Id 0 = `alundra_0`, 94 animation sets).
- **Bank layout:** the record sits at `0x801A0000` and is `entity +0x64`.
  - `+0/+4/+8/+0xC` point to the animation sets (`0x801A0020`, 14-byte entries), the frame bytes (`0x801A0550`, 5-byte frames), the collision records (`0x801A4160`, 6 bytes) and the image sets (`0x801A4860`).
  - The 16 header bytes are at `+0x10`.
  - `bank.py` asserts that extractor addresses equal offset + 5i, with 0 clashes between animations writing the same byte.
- **BSS words:** the nine words read before any write are set to 0 explicitly, each with its reason (`world.BSS_ZERO`). With them set, no read of an unwritten byte occurs in setup or ticks.

**Does the comparison have teeth?**
- Leaving out a pass, gravity 100, ZViscosity 512, HP 0, `+0x20` = 1 or the locked branch makes it FAIL, or the S-B warm-up impossible.
- Skipping pass 1 or the counters pass, a held Cross, a different start direction, 3 idle ticks before S-A and four other start positions stay PASS (`out/mutations.txt`).
- A second interpreter (`mips.py`, with unaligned load/store and div re-implemented bytewise) gives byte-identical slot, stats, globals and journal on all 62 scripts (S-A, S-B and 60 random 80-tick scripts of all buttons).
- Both interpreters ignore the MIPS load delay slot. 702 executed loads checked, 0 hazards.
- Determinism: two runs give identical `*.trace.json`.

**Files** (all under `docs/plan-e20-annexe/d1-oracle-moveplayer/`):
- `notes.md` (how to run, pass decisions, stubs, hero-bank layout, limits)
- `tools/` holds the E19 copies and the new scripts. The copies are unchanged (`cmp` equal) except that `run_update_animation.py` now imports `emu` from its own folder; its output equals the E19 annex output. New scripts: `machine.py`, `bank.py`, `world.py`, `oracle.py`, `compare.py`, `mutations.py`, `padcheck.py`, `pass38634.py`, `startstate.py`, `hazards.py`, `interp2.py`, `diff2.py`, `explore.py`, `dump_dis.py`, `callgraph.py`, `run_all.py`.
- `out/` holds `S-A.trace.txt/.json`, `S-B.trace.txt/.json`, `*.machine.txt`, `comparison.txt`, `comparison-detail.txt`, `mutations.txt`, `padcheck.txt`, `pass-80038634.txt`, `start-state.txt`, `load-delay-check.txt`, `diff2.txt`, `explore.txt`, `dis_passes.txt`, `dis_pad.txt`, `dis_moveplayer.txt`, `callgraph_moveplayer.txt`, `run_update_animation.out`, `run.log`.
- Rebuild with `python -B tools/run_all.py` (about 25 s; `--full` adds `diff2` and `explore`, about 4 min in total). Exit code 0 means PASS.
- `git status` shows only this folder, plus the `m CasaEngineMonogame` and `? alundra-datas-analyser` lines that were already there before I started.

**Open issues** (also in `notes.md` section 8):
1. Only the jump arcs are validated against a reference. Attack, sprint and item routes run as real code and agree between the two interpreters, but have no annex.
2. The world is synthetic: flat field, no slope or cell property, no other entity, no map events, no transition.
3. UNKNOWN: whether the real game re-points the hero's `+0x1C4` to `0x80127008`. `ComputeNewHp` references `0x80127008` once (`0x8003A3B0`), inferred for the hero. It is not exercised, since hits are stubbed.
4. INFERENCE: the BSS words are zero at power on; `0x800DC058` is a "transition" flag, from its readers and `0x8002AB64`.
5. INFERENCE: the attack record layout of 2 bytes per attack. It is unused here.
6. The role of `0x8003634C` (D5) is not decoded; in S-A/S-B it runs every tick but its callees are never reached.
7. UNKNOWN: the BIOS pad word's bit convention (`0x8008A688` lies outside the EXE image). The oracle gives each tick the scenario's hold word and does not model when the main loop calls `0x8002E38C` relative to the tick. That belongs to E20.a (D7).
8. I added unaligned load/store and `mthi/mtlo` in `machine.py`, because the copied `emu.py` lacks them. `emu.py` itself is unchanged.
