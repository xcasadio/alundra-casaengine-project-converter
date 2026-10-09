# E20.c2-0 - oracle of the hurt hero, all real code (2026-10-10)

Purpose: the acceptance oracle of E20.c2a (hero life carried by the entity, mirror into the stats, `UpdateItemEffectState`) and E20.c2b (the hurt: `0x80031A68`, states `0x31` / `0x1C` / `0x3A`, `+0x1CC`
and the blink, the `controlFlags == 0x20` path), produced by running the **real code of `ALUN_CD.EXE` (France)** in the MIPS emulator of the previous tranches (E20.c1 / c1b).  Nothing outside this folder is
touched: no DLL, no C# test, no converter, no other annex (`e20-c1/oracle`, `e20-c1b/oracle` and `docs/plan-e20-heros.md` are only read).  The specification is `docs/plan-e20-heros.md` 1.11, designed by
`../c2-oracle.md` (sections 2-5), trimmed and corrected by `../check.md` (section c and the check table), with `../c2-binary.md` for the binary rules.
Labels: **FACT** (a measurement in `out/`, or a decoded instruction word with its address), **INFERENCE** (with its base), **UNKNOWN**.

## 1. Layout and how to run

```
docs/plan-e20-annexe/e20-c2/oracle/
  tools/        byte-identical copies of E20.c1b's tools (28 .py + dis_update_animation.txt) and of E20.c1's scen.py; never edited
  hworld.py     the hurt-hero world (new)            hscen.py   the scenarios, readers, writers, summary (new)
  hmodel.py     the independent model (new)          hmut.py    the mutation tests (new)
  run_c2.py     everything, in order (new)           out/       the outputs (compact JSON + .txt), out/d1 = the D1 gate
```
```
python -B docs/plan-e20-annexe/e20-c2/oracle/run_c2.py      # about 5 minutes; exit code 0 = every verdict PASS; needs Python 3 + capstone and the disc file of the France version
```
Order: (1) D1 gate (`tools/run_all.py` -> `out/d1/`; S-A and S-B 216 values each, 0 differences; the 19 files equal E20.c1b's `out/d1` byte for byte); (2) `H3` of E20.c1 re-run with the copied tools and compared with the
stored `e20-c1/oracle/out/H3.json`, then A1 (hworld) compared with it on every common column (the run STOPS, exit code 2, if the tick-18 row differs or the D1 gate moves); (3) the scenarios on `emu.py` -> `out/<name>.txt` and
`.json`, the model on every run -> `out/model.txt`; (4) every run again through `mips.py` (`interp2.py`) -> `out/diff2.txt`; (5) the mutations -> `out/mutations.txt`; (6) `out/coverage.txt`, `load-delay-check.txt`, `dis.txt`,
`summary.txt` (verdicts and the key values, computed), `manifest.txt` (md5 of every other file).  `--reuse-d1` (development only) skips step 1's re-run.  Two complete runs gave identical manifests (60 files).
The `.json` files are column-major and compact: `{"runs": [{"spec", "meta", "tick": [...], "hero": {column: [value per tick, tick 0 first]}, "g": {...}, "targets": [{...}], "journal": [...]}]}`, readable by column name.

## 2. Real code, stubs, harness writes

Real: everything of E20.c1 / c1b (the eight passes of `0x8003B388`, `MovePlayer` with the damage entry `0x80031A68`, the hit pass `0x80039300`, `ComputeNewHp`, `UpdateItemEffectState` `0x800307E8`, the spawner, the new-game
inventory code) plus `SetPlayerHpMax` `0x8004DC68`, `SetPlayerHp` `0x8004DD30`, the getters `0x8004DC50` / `0x8004DD18`, `IncreaseHpMaxAndCreateEffect` `0x800333AC`.  Stubs (journalled, as in c1): sound, effects
`0x8003BDD8` / `0x8003BE74`, spawn `0x80039F58`, the effects pass `0x80038E84`, and the targets' own programs (inert).  `0x8003634C` and `0x8002F884` run for real and are no-ops here (item state 0, no carried entity).

| Piece | How (hworld.py) |
|---|---|
| Hero life from the stats | after the c1b setup: real `SetPlayerHpMax(max)`, `SetPlayerHp(hp)`, then the two getters stored into `+0x18` / `+0x14`, as `0x800319F8-0x80031A08` does at spawn (FACT: `out/E3.txt`) |
| Sheet `0x80127008`, boots `[0x80127000]` | one real call of `0x800307E8` at setup with the pad hold word zeroed (K1 does the same).  That call also advances the regeneration counters, so they are zeroed again: tick 1 starts from the BSS state.  **The warm-up is itself an END-like call: a mutated regeneration (M8) already acts on it** |
| BSS words | regeneration counters `0x80126FE8/EC/F0`, `0x8012715C`, `0x80127164` zeroed (a run reports any read of an unwritten byte: none) |
| `[0x80134360]` (last used entity index) | poked to the highest spawned slot: the slot pass `0x80038634` writes it every tick, but a run whose flags stop that pass from tick 1 (D1 with 0x48) would otherwise draw only the hero |
| Cells | `hworld.paint` / `fill`: the u16 of a cell is `walk | gp << 8`; `0x80` cost 1, `0x100` cost 2, `0x180` cost 3, `0x800` deep water (`Slope_18c` 4).  Used on the whole field |
| Kinds | `c1world.KINDS` gets `map15-orc` (sprite 85, balance 341: attack 0 = attribute `0x02`, power 4) and `map14-fish` (sprite 136, balance 392) - dictionary entries added at import, no file edited |
| Synthetic attacks | `hworld.syn_attack` / `set_attack`: a 2-byte (attribute, power) pair poked into a target's `+0x1C8` (A2 uses it as a cross-check of the Orc) |
| Inventories | the real new-game code with ancient 0x13, silver 0x14, boots 0x19 / 0x1A / 0x1B / 0x1C and combinations, ring 0x37 added to `kworld.INV` (at import, in a copy) |
| Events | data `(kind, argument)` applied before a tick: `flags`, `stats_hp`, `stats_hpmax` (real setters), `vessel` (real `0x800333AC(hero)`), `teleport_x` (PosX poke), `hero_hp`; `flags_every_tick` pokes the flags before every tick.  The model reads the same data |
| Tick dispatcher | `hworld.htick`, used by both interpreters: pad words (cross-checked with the real `0x8002E250`), then **if `[0x800DC4B8] & 0x48` only the lists pass `0x800384F4`, else the eight passes** (`0x8003B388-0x8003B408`).  The base `world.tick` runs the eight passes whatever the flags; nothing was edited |
| Extra columns | hero: `hpmax_18 vram_or_180 vram_and_184 slope_18c anim_end_AC carried_2C visible visible_count blink_expected`; globals `g`: `stats_hp stats_hpmax ctl_flags hold_word boots sheet_r1..r11 regen0..2 item_state item_ticks charge wptr eq0..2` and, read after the events and before the tick, `stats_hp_before stats_hpmax_before ent_hp_before ent_hpmax_before` |

## 3. Scenarios (105 runs, 7979 ticks; the oracle holds only inputs)

Kept (check.md section c): A1 (H3 to 200 ticks, + pad 0 at 20 px, 260 ticks), A2 (6 inventories x stats 12/12, 5/12, 3/3 against the Orc, 40 ticks, + 2 synthetic-attack cross-checks), A4 (12 bearings, 8 ticks), A6 (contact restored at hurt + 117..122, + a
260-tick forced contact), B1 (3 cell words x boots 0..3, 125 ticks), B2 (HP 2 / cost 3, 1 / 1, 3 / 3), D1 (flags `0x00 0x20 0x24 0x30 0x10 0x04 0x28 0x08 0x40 0x48 0x80 0xA0`, 60 ticks; `0x20` released at tick 40, 140 ticks; `0x48` set after a hurt, held
hidden / visible / released), E2 (stats written from outside, the real vessel at 5/10, 49/49, 50/50), E3 (stats 12/4, 60/70, 5/9, -1/-3), F1, F2, F4, F5 (450 / 450 / 250 x 2 / 450 x 2 ticks), G (13 inventories).  INFORMATIONAL: C1 (deep water, E20.i) and E1 (HP 0, E18).
Removed by check.md: A3 A5 A7 B3 B4 C2 C3 D2 F3 F6 and group H.  Choices that differ from the design text (inputs, all in `hscen.py`): contact at 20 px with the hero at rest (touch at tick 1, hurt at 2; none at 28 px); A4 bearings given as the vector
hero - Beannoid; B1 125 ticks (two hurts); C1 water on the whole field, not 3 x 3 cells (boots 0x1B), Fish in animation 3 at 10 px (contact measured by a sweep of dx -10..58: tick 1 for dx <= 14); F4-zero pokes the flags to 0 before every tick (the death branch's
game-over request would otherwise close the regeneration flag gate at tick 76).

## 4. What the oracle says (FACT, `out/summary.txt`)

- **H3 / A1**: the first 45 rows of A1 equal H3 of E20.c1 on 4005 values (every hero and target column, RNG word, latch, journal); tick 17 hit written (`+0x1D0` 25), tick 18 hurt: HP 10 -> 9, `0x31`, direction 24 -> 8, `+0x1CC` 119, `sound(23)`;
  `0x1C` from tick 22, Idle at 33; hidden on 30 ticks; the stats follow (9/10).  The hero walks through the Beannoid during the window and never touches it again in 200 ticks (so no second hurt at "138": that needs contact).
- **Window (A6)**: touch possible again at hurt + 119 (tick 121), second hurt at 122, whatever the contact start between hurt + 117 and + 119; a contact restored at hurt + 120, + 121, + 122 touches that very tick.  Forced contact kept for 260 ticks: touches 1, 121, 241, hurts 2, 122, 242, HP 9, 8, 7.
  Pad 0 at 20 px: touched at tick 1 only, knocked 33 px west (232 -> 198.9), no second touch in 260 ticks (check.md #47 confirmed).
- **Damage (A2)**: the Orc (power 4) costs kit 4 / leather 3 / ancient 2 / silver 1 / no armour 1 / bracelet 3 HP, floored at 0 (3/3 kit -> 0).  Bearings (A4): E 24, W 8, S 0, N 16, the diagonals and near-diagonals as `out/summary.txt`; the independent model (a float arctangent) agrees on all 12.
- **Hazards (B1/B2)**: cost 1 / 2 / 3, boots 0, 1, 2 hurt at ticks 2 and 122 (HP 9, 8 / 8, 6 / 7, 4), boots 3 never; the floor is 0 (HP 2 with cost 3 -> 0).
- **Flags (D1)**: only the whole word `0x20` reaches the entry from the locked branch (HP 9, `+0x1CC` 119, no animation, direction or sound change; the next touch at 121); `0x24 0x30 0x10 0x04 0xA0` lose the hit (touched at 1, 26, 51, no hurt); `0x80` alone is the normal path (hurt like `0x00`);
  `0x28 0x08 0x40 0x48` stop the whole tick but the lists pass: `+0x1C`, `+0x1CC`, position, HP, animation, the stats (the poked 6 survives), the hit pass all frozen, and the draw list still follows the frozen `+0x1CC` (hidden for 119, visible for 118; released, the countdown and the animation resume).
  The END (sheet / stats mirror) runs under every locked flag: the poked stats HP 6 becomes the entity's 10 after tick 1.
- **Stats (E2/E3)**: the entity wins: a stats HP of 3 written between two ticks is back to 10 after the tick; the real vessel gives entity 11/11 (from 5/10), 50/50 from 49/49, **51/51 from 50/50 with the stats clamped to 50/50**; the hero is created from the stats (4/12 -> 4/12, 70/60 -> 50/50, 9/5 -> 5/5, -3/-1 -> 0/0).
- **Regeneration (F)**: tonic or ring 0x37 (record byte 2 = 200): +1 at ticks 201 and 402 (period 201); one pad press at tick 100 delays the first to 301; none at HP == HpMax or HP 0; none with the holy sword and the cloth armour only.
- **Boots / sheet (G)**: 0x19 gives 0; 0x1A 1; 0x1B 2; 0x1C 3 (also with 0x1A); sheets cloth 0x10, leather 0x0C, ancient 0x08, silver 0x04, no armour 0x00, bracelet 0x0F, charm 0x0F with 0x80 at response 6.
- **Informational**: C1 - in deep water the hurt is `0x3A` at the hurt tick, `0x3B` from +4, `0x1D` (direction flipped) at +13, and the hero DOES translate (-3 px per tick like `0x31`; c2-binary U3 reported none, not reproduced with a whole-field `0x800`); E1 - HP floors at 0, the hurt plays in full,
  tick 17 `0x39`, tick 67 `0x4E`, flags `0x4` at tick 76 (the game-over request), in the three runs.

## 5. Checks of the oracle itself

- **D1 gate**: unchanged (216 + 216 values, 0 differences; the 19 files equal c1b's).  **H3**: re-run equal to the stored file (rows, start state, experiments); A1's tick-18 row equals H3's.
- **Independent model** (`hmodel.py`): written from the plan and the reports, never from the emulated code; fed only with the run's inputs and geometry / counter columns (positions, the hero's previous direction and slope, the toucher of the previous tick and its attack bytes).  Shares with the oracle only the disc tables
  and `kmodel.py` (sheet combination, boots, `damage_model`) and `scen.model_vs_real` for the hit ticks.  Rules: boots, sheet, hero HP (damage, hazard cost and floor, regeneration), HpMax, `+0x1CC` (0x77, countdown, frozen), hurt animation `0x31` / `0x3A`, hurt direction, the timeline `0x31` x4, `0x1C` x11, Idle flipped,
  draw list and its count (blink), stats mirror and clamp, regeneration counters, the tick gate (`+0x1C`, position, touching, animation, force frozen), the stats / entity values the tick starts from.  1412 rule checks over 105 runs: no difference.
- **Second interpreter**: all 105 runs through `mips.py` give the same rows, start state, meta, hero + target slots after every tick, journal and RNG word (7979 ticks).
- **Mutations** (`hmut.py`, expected changes written in the file): 14 one-word patches, all CAUGHT (the written change happens and the model disagrees): toucher branch forced off, direction operand, boots `slti 3` -> 4, cost shift, `0x78` -> `0x77` (A1 and the A6 boundary), 0x31 / 0x3A selector, blink mask `& 3` -> `& 7`,
  regeneration comparison, counter step, regeneration pad gate, the `== 0x20` compare, `SetPlayerHp` clamp, hazard floor.  Control: a debug-print read changes nothing.  Dispatcher check: without the `& 0x48` gate the 0x48 / 0x08 / 0x40 / 0x28 lines of D1 change (+0x1C runs, the hero is touched) and the model's gate rule disagrees.
  `0x7C` -> `0x74` (c2-oracle item 10) is not retained: the bit `0x08` it drops is reachable only through the gated flags, so no kept scenario can see it.  **Two predicates were wrong in their first draft and were corrected after the first run, for reasons found in the code, not to fit the output**: M5b counted the contact of tick 1 as the "first touch" (the oracle's
  values, 120 vs 121, were what the draft meant); M8 forgot that the setup warm-up is an END-like call (HP 6 already at tick 0).
- **Coverage**: `0x80031A68` 58 of 58; the regeneration block 71 of 72 (`0x80030D20`, the cap `HP + gain > HpMax`, needs a gain above 1: only one record regenerates); the END epilogue 9 of 9; the lists blink 24 of 24; states `0x31` 3 of 3, `0x1C` 17 of 19, `0x3A` / `0x3B` general handler 0 of 17 (water takes the slope-4 handler, not ported by the DLL);
  MovePlayer's frozen exit (`+0x20 != 0`, `0x80031BB4-0x80031BC0`) not run (no kept scenario freezes the hero).  **Load delay**: none of the executed loads is followed by a use of its register (both interpreters ignore the delay slot).
- **Determinism**: no clock, no host path, no hash-ordered iteration in the outputs; two complete runs give the same `out/manifest.txt`.

## 6. Limits

1. The targets are inert: their own handlers (E14) never run, so nothing but the hit pass touches the hero; the Beannoid and the Orc are touched in their idle animation (the Orc's body box carries its mace attack).  2. One flat field; the knock-back meets no wall.  3. Level D = 0 (map 14).
4. The oracle sees draw-list membership, not pixels (c2-oracle section 7); the shadow follows the same bit test (FACT in c2-binary) but is not observed.  5. The item driver `0x8003634C` and the carried-entity drop are no-ops here (item state 0, `+0x2C` 0): the "for real" of D-E20-14 has no observable effect before E20.h / E20.g.
6. The death branch and the water states are recorded (E1, C1) but outside the model.  7. Not checked: the `0x80062678-0x80074DA8` hero-grab sequence that sets bit `0x20` (E14); `0x20` is poked by an event.
