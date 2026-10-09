> Report returned by one agent of the read-only discovery workflow for E20.c2 and E14.a of 2026-10-09 (slice E20.c2), kept verbatim as evidence. The plan holds the reconciled facts.

# E20.c2 oracle proposal (label: c2-oracle)

Method: static reading only. I did not run any repository oracle tool, build, or write into the repository. I disassembled `ALUN_CD.EXE` (France) with my own capstone scripts, read the repo plans, tools and DLL code, and censused `data-extracted/data/map_*.json`.

Scratch scripts: `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-c2-e14a/c2-oracle/` (`mdis.py`, `scan.py`, `scanoff.py`, `rd.py`, `rd2.py`, `ctl20.py`, `hazards_scan.py`).

Labels: **FACT** (binary address, repo file:line, or a measured census), **INFERENCE** (with its base), **UNKNOWN** (with what would decide it). Repo root `D:/development/repo/alundra-casaengine-project-converter` is written `R/`.

---

## 0. Things that change the plan

**1. The oracle for `0x80031A68` mostly exists already, so the work is columns and scenarios, not new emulation.**
- FACT: `MovePlayer` is real code in the oracle, so every routine named in the slice already runs for real. These are `0x80031A68`, the `0x31`/`0x1C` handlers, the counters pass, `0x800307E8`, `0x8003634C` (second site `0x80031CC4`), `0x8004DC68` and `0x8004DD30` (`R/docs/plan-e20-annexe/d1-oracle-moveplayer/notes.md` §3).
- FACT: the E20.c1 traces `H3` and `H1-both` already contain the real hurt path. In `H3.txt`:
  - tick 17: hit written on the hero, `+0x1D0 = 25`, `TouchingEntity` = slot 1.
  - tick 18: `TargetAnimationId` 0x31, direction 24 to 8, HP 10 to 9, `+0x1CC` 119, ForceX −196608, `sound(23)`.
  - ticks 22 to 32: animation 0x1C, `AnimFlags` 0x44.
  - tick 33: Idle; tick 34: walking again.
- FACT: c1a's DLL test excludes everything after tick 18 of `H3` and after tick 2 of `H1-both`.
- Consequence: c2 promotes those excluded ticks to compared ticks. It adds only what the c1 rows lack: stats HP/HpMax, visibility, `VramOR`, `Slope_18c`, sheet bytes, boots, regen counters, control flags.

**2. c2 changes the HP model: the hero entity becomes authoritative and `PlayerStats` is only a mirror.**
- FACT, spawn (`0x800319F8-0x80031A08`): `+0x18 = GetPlayerHpMax()` (`0x8004DC50`) and `+0x14 = GetPlayerHp()` (`0x8004DD18`), read from the stats.
- FACT, every tick (epilogue `0x80032914`):
  - `UpdateItemEffectState`.
  - `SetPlayerHpMax(+0x18)` at `0x80032924`.
  - `SetPlayerHp(+0x14)` at `0x80032930`.
  - The stats are overwritten each tick, and clamped to 50 for the maximum and to `[0, max]` for the life.
- FACT: `IncreaseHpMaxAndCreateEffect` (`0x800333AC`) raises the entity (`+0x18 += 1`, `+0x14 = +0x18`). It has three callers: `0x80033E70`, `0x80034080`, `0x800341C0`.
- FACT: the nearest AddHp/SubHp-style wrappers (`0x8004DD84`, `0x8004DDB0`, `0x8004DCC0`, `0x8004DCEC`) have no `jal` caller in the EXE.
- FACT, DLL today:
  - Hero `Hp` is "deliberately NOT ported" from the stats (`R/Alundra/Scripts/AlundraWorldProxy.cs:1827-1828`). It is set from the balance record, MaxHp 10 (`:1976-1978`).
  - `IncreaseHpMaxAndFill` writes the stats directly, a "provisional seam" (`AlundraPlayerManager.cs:1004-1008`, called from `AlundraEventProgramRunner.cs:3099`).
  - The HUD reads `PlayerStats.Hp` (`AlundraHudDirector.cs:545`).
  - A save load writes `stats.Hp` (`AlundraSaveGame.cs:482`).
- Consequence: once the mirror exists, a hero created at 10/10 overwrites a loaded 4/12 on the first tick, and the life vessel is undone one tick later. c2 must therefore also:
  - load the entity from the stats at spawn;
  - move the vessel seam (ADR-0048) onto the entity;
  - re-test the save-load path.

**3. The epilogue runs on every exit of `MovePlayer`; the DLL skips it on two of them.**
- FACT, binary: it is reached from these exits.
  - `+0x20 != 0` (`0x80031BBC`).
  - Locked branch with flags ≠ 0x20 (`0x80031C0C`).
  - Locked branch after the 0x20 damage test (`0x80031C20`, `0x80031C28`).
  - Hurt (`0x80031C98`).
  - HP == 0 (death branch falls into `0x80032914`).
  - Normal ends.
- FACT, DLL: `AlundraPlayerManager.cs:212` (`BlockedByEntity` return) and `:222` (locked return) leave before `UpdateItemEffectState`.
- Consequence: during a dialogue lock the equipment records (c1b K2), boots, sheet and HP mirror would not update. The common end needs restructuring.

**4. `controlFlags == 0x20` (ForcedSequence) cannot occur in the DLL today.**
- FACT: nothing in `R/Alundra` writes bit `0x20` (`AlundraGameState.cs:79` only declares it).
- FACT, binary: bit 0x20 is raised at `0x80062740` and `0x80075264` (native sprite handlers, E14/E20.h domain). It is cleared at `0x800621DC` and `0x80062330`.
- Consequence: that path is provable only by the oracle plus a test that pokes the flag. It has no in-game recipe before a native handler exists.

**5. Hazard cells are absent from the story-chain maps.**
- FACT, census of `data-extracted/data/map_*.json` using the cell word `(Walkability | GroundProperty << 8) & 0x180`:
  - 36 maps have hazard cells: 6738 of cost 1, 559 of cost 2, 25 of cost 3, total 7322. This matches O-E19-22.
  - The cost-3 cells are all on map 0.
  - Maps 14, 15 and 115 have none.
  - Maps with hazards include 27, 36, 52, 54-60, 84, 86, 87, 89, 90, 92, 94, 101, 104, 107, 120, 122, 160, 325, 411, 422, 425, 426, 438, 443-448.
- Consequence: "hearts go down on contact" is testable on map 14 with a Beannoid. The hazard half needs another map for its arc, for example 52.

**6. The blink phase starts hidden.**
- FACT: `+0x1CC = 0x78` is set in `MovePlayer` (`0x80031C9C`), then decremented by the counters pass of the same tick to 119.
- FACT: the lists pass skips an entity when `(+0x1CC & 3) == 3` (`0x800385E8-0x800385F4`).
- INFERENCE (arithmetic): `119 & 3 == 3`, so the hero is out of the list on the hurt tick itself. He is hidden at hurt tick + 0, 4, 8 … 116, which is 30 of the first 120 ticks.
- FACT: the visible list (`0x80134250`, count `0x8013435C`) feeds the depth keys (`0x800399B8`) and the loop that submits packet `+0x194` (`0x8003B4A4`).

**7. A Beannoid cannot show the armour table.**
- FACT: Beannoid power is 1, so `(1 × r) >> 4` is 0, and 0 becomes 1. It costs 1 HP at every armour level.
- INFERENCE (arithmetic, `0x8004464C` rule confirmed by K2): the Orc mace (map 15, balance 341, Hammer nibble 2, power 4) gives 4 / 3 / 2 / 1 HP for cloth 0x10, leather 0x0C, ancient 8, silver 4. No armour gives 1 and the bracelet 0x0F gives 3.

**8. An immune response probably still hurts.**
- FACT: the return of `ComputeNewHp` is dropped (`0x80031A94-0x80031AB8`). The hit pass reads the hero's `+0x1C4` record, whose responses are all 0, so it never treats him as immune.
- INFERENCE: with the Charm of Nava (response 0x80 at nibble 6, fire), a fire hit gives the hurt state, blink and 0x78 with no HP loss. A DLL written as "if damage then hurt" would be wrong. The oracle decides.

**9. The item-use driver is a no-op until E20.h.**
- FACT: the only start of the state `[0x80127164]` is `sw` at `0x80034A90` (inside the item-start code). `TryUseItem` starts it only on a Circle edge with `[0x800DC4B8] == 0` (`0x8002ED64-0x8002ED98`).
- FACT: with state 0, `0x8003634C` returns 1 at once and `0x800350C0` also returns at once. Neither writes `[0x8012715C]`.
- Consequence: "the driver for real" has no observable effect in c2. The oracle pins the invariants (state stays 0, counter untouched) plus one informational probe.

**10. Regeneration is narrower than the plan implies.**
- FACT, `0x80030C24-0x80030D3C`: it runs only when `([0x800DC4B8] & 0x7C) == 0`, `+0x20 == 0`, **the pad hold word `lhu 0x80126F28` is 0**, `Hp != 0` and `Hp < HpMax`.
- FACT: any failed gate zeroes the three counters `0x80126FE8/EC/F0`. A counter advances only when its equipment record exists and has a non-zero byte `+2`.
- FACT: HP gains 1 when `byte+2 < counter`, so the period is byte+2 + 1.
- FACT: armour and weapon records carry 0. The accessories 0x37 and 0x38 carry 200.
- INFERENCE: a period of 201 ticks (the decompilation's `<=` would give 200).

**11. Hurt-state handlers, all in the binary.**
- FACT: state 0x31 (`0x80032680`) only calls `TryUseItem`.
- FACT: states 0x1C and 0x3E (`0x80032558`) call `TryUseItem`. When `+0xAC == 1` they flip `+0x8C` by 0x10, then write Idle on the ground or 1 in the air.
- FACT: states 0x3A and 0x3B (`0x80031DB4`) flip `+0x8C` by 0x10, then write `+0x88 = 0` on the ground or `0x2D` in the air, then `TryUseItem`.
- INFERENCE: 0x3A lasts about one tick in `MovePlayer` terms. This is UNKNOWN until the water scenarios run.

**12. c1a pins flip in c2.**
- FACT: `R/Alundra.Tests/AlundraHitPassTests.cs:278-328` and `:454` assert the pre-c2 hero:
  - the hero is hit at ticks `{1, 26, 51, 76}` with `+0x1CC` staying 0;
  - a dx −10 swing touches at tick 12.
- Consequence: after c2 the hero is hit once, then invulnerable for 120 ticks (INFERENCE: second hurt at tick 138 of `H3`, see A6). These tests must be rewritten against the c2 oracle.
- Consequence: the c1b-2 lever arcs (untracked `AlundraLeverArcSupport.cs`, `AlundraLeverSmokeTests.cs`) will change if the hero touches an enemy. Check each arc for contact.

---

## 1. What the existing oracles do (read, not run)

All five folders (`d1-oracle-moveplayer`, `d2-oracle-coups`, `e20-b/oracle`, `e20-c1/oracle`, `e20-c1b/oracle`) share one design. Each new tranche copies the previous `tools/` byte-identical and adds files. Output is deterministic, with an `md5` manifest.

| Aspect | FACT (file) |
|---|---|
| Loading the binary | `lib.py` reads `ALUN_CD.EXE` at import. `emu.Emu` maps the image at `0x80020000` into a 2 MB RAM (`file offset = 0x800 + addr − 0x80020000`). |
| Machine | `machine.Machine(emu.Emu)` adds a shadow map of initialised bytes (an uninitialised read is recorded with its pc), call counts, a pc-coverage set, `lwl/lwr/swl/swr/mthi/mtlo`, and a journal of stub calls. |
| Calls | `m.call(addr, *args)` uses sp `0x801FFF00` and a sentinel return. `side_call` takes an explicit sp for calls made from inside a hook (`c1world.py:280`). |
| Stubs | `m.hooks[addr] = fn(m)` replaces a function and sets `pc = r31`. Stubs: sound `0x800490FC`, effects `0x8003BDD8` and `0x8003BE74` (they return a zeroed dummy slot so the spark RNG draws happen), spawn `0x80039F58`, effects pass `0x80038E84`. |
| Inert targets | `install_inert` hooks all 201 non-null handler addresses of the five tables at `0x80098F4C+4k` and the bytecode runner `0x8004205C` with a journalling no-op. The real events pass still chooses the program letter (C, or D after a touch). |
| World | No real map is loaded. `world.build_world` makes a flat 52×60 field (cell words at `TILES+0x604+row*416+col*8`, `cells(col,row)` returning `(height, slope, walk, gp)`). The map header holds map 14's constants (asserted equal to map 10's). The balance blob is `DATA/BALANCE.BIN` at `0x80143DF8`, with `0x80044520(D)` run for real. |
| Hero | `place_hero` runs the real `InitializeEntity 0x80039D04` over the hero bank laid out from `map_alundra.json` SpriteRecords[0] (`bank.py`, `0x801A0000`). It then forces status 2 and HP 10 by poke. |
| Inventory | Real `InitPlayerStats 0x8004DAC0`, `AddItem 0x8004E530`, `SetPlayerWeaponId`, `SetCurrentItemId`. `kworld.INVENTORIES` has kit, steel, holy, ring, leather, noweapon, all, noarmour, bracelet 0x35, charm 0x36, tonic 0x38, rings2. |
| Targets | The real spawner `0x8003A1B8(0,i,0)` over a synthetic per-map block `0x80126E70` (records at `0x801CF000`, sprite pointers `0x801CE000`, bank `0x801C0000`). `KINDS` allows another real sprite record (map 69 sprite 126). |
| Pad | A Python port of `AlundraTickPad.Update`, cross-checked every tick against the real `0x8002E250` on a second pad struct at `0x801E9000`. |
| Tick | `world.tick` runs the 8 passes of `0x8003B388` in order, once per tick. The hit pass is real since c1 (`c1world.setup` pops the stub). |
| Logs | `c1world.E_FIELDS` (about 39 per-entity columns) plus RNG word, latch `0x80146DF8` and the stub journal, after each tick. `scen.write_scenario` writes `.txt` and `.json` (`spec/meta/pre/rows/experiments`). The C# `HitOracleTrace.Load` (`R/Alundra.Tests/AlundraHitOracle.cs`) reads columns by name, so extra columns are free. |
| Checks | Independent models (`scen.hit_model`, `kmodel.py`). A second interpreter (`mips.py` via `interp2.py`) compared text for text. One-word mutation patches with the expected change written beforehand (`checks.py`, `kmut.py`) plus controls. Load-delay hazard check. Coverage. The D1 gate (S-A and S-B, 216 values each, 0 differences) runs first in every tranche. |
| Costs | D1 about 25 s; e20-b about 2.5 min; c1b about 95 s; c1 about 8 min (364-run sweep). |

**What is already real vs stubbed for c2.** Everything c2 needs is already real. The stubs stay as they are, because c2 needs nothing else stubbed:
- sound, effect creation, spawn, effects pass `0x80038E84`;
- target programs (inert).

Nothing new is stubbed, and no routine has to be ported into the emulator.

---

## 2. The proposed oracle

**Location and base.** New folder `R/docs/plan-e20-annexe/e20-c2/oracle/`.
- Copy `e20-c1b/oracle/tools/` (it already contains D1 `world.py`, `c1world.py`, `kworld.py`, `kmodel.py`, `kmut.py`, `kcheck.py`, `mips.py`, `interp2.py`) plus `scen.py` from `e20-c1/oracle/tools/` for the per-tick writers. Keep the copies byte-identical.
- Add new files only: `hworld.py`, `hscen.py`, `hmodel.py`, `hmut.py`, `run_c2.py`. Run order: D1 gate, re-run of `H3` (the common columns must equal c1's), scenarios, models, second interpreter, mutations, coverage, manifest.

**Harness additions, each doable without editing a copied file** (poke after setup, or patch a module attribute from `hworld.py`):

| Missing today | How |
|---|---|
| Hero life from the stats (`place_hero` pokes HP 10 and the stats HP is whatever `InitPlayerStats` left) | After `kworld.setup_inv`: real `0x8004DC68(max)`, `0x8004DD30(hp)`, then the two real getters `0x8004DC50` and `0x8004DD18`, stored into `+0x18`/`+0x14` exactly as `0x800319F8-0x80031A08`. |
| Sheet `0x80127008` and boots `[0x80127000]` empty until the first epilogue, so a hit at tick 1 reads uninitialised bytes | One real call of `0x800307E8` at setup, with the pad hold word `0x80126F28` zeroed first (K1 does this for the same reason). |
| BSS words read before written: regen counters `0x80126FE8/EC/F0`, item words `0x8012715C`, `0x80127164` | Extend `world.BSS_ZERO` from `hworld.py` (the shadow map would otherwise report them). |
| Hazard and water cells (`c1world.setup` calls `build_world(m)` with no `cells`) | Poke the cell words after setup: `0x80` = cost 1, `0x100` = cost 2, `0x180` = cost 3, `0x800` = deep water (all four corners at the hero's height). Corner rules: `VramOR` is the OR of the four corner words (`0x80038170-0x80038190`); `Slope_18c` is the minimum of `(word & 0xE00) >> 9` (`0x80038344-0x80038348`). Corners at the wrong height contribute 0 (`0x80038138`). |
| Control-flag schedule `[0x800DC4B8]` | The `before={tick: fn}` hook of `c1world.run` already exists. |
| Orc (map 15, table index 85, balance 341) and Fish (map 14, index 136, balance 392) as targets | Add two entries to `C.KINDS`. Both are per-map sprites (`SpriteDirection & 0x80`), the same layout as the existing map-69 kind. The fish needs its animation forced to 3 (the first hurting one) through a `bean_anim`-style `target_init`, since its program is inert. |
| Synthetic attacks for nibble sweeps | Append `(0x06,4)`, `(0x07,4)`, `(0x02,16)` and so on to `kworld.SYN_ATTACKS`. |
| Inventories | Add armour 0x13 (ancient) and 0x14 (silver), boots 0x1A / 0x1B / 0x1C and combinations, and ring 0x37. |
| Extra columns (`+0x180`, `+0x184`, `+0x18C`, `+0xAC`, `+0x18`, `+0x2C`; globals) | Wrap `C.snap_entity`; the hero dict gets `g_*` keys (see section 4). The `.json` stays loadable by column name. |
| Row size | H3's JSON is 101 KB for 46 ticks (indent 1). 80 scenarios would reach tens of MB. Use compact JSON and trim target columns; INFERENCE: about 5 to 8 MB total. |

**Scenarios.** Expected values marked "INFERENCE" are hand-derived and unconfirmed. The oracle itself holds no expected value, only inputs.

*Group A, touch (toucher branch).*

| ID | Setup and inputs | Ticks | What it pins |
|---|---|---|---|
| A1 | `H3` again: kit, Beannoid (map 14), HP 10/10, Right held | 200 | Hit at 17, hurt at 18, then the full 0x31, 0x1C, Idle timeline with the new columns. Blink phase. Second hurt not before 138 (INFERENCE). Sound 23 at the hurt tick (FACT in `H3.txt`). |
| A2 | Orc (map 15 kind) overlapping the hero at rest; inventories kit, leather, ancient, silver, noarmour, bracelet; (Hp,HpMax) in {(12,12), (5,12), (3,3)} | 18 runs × 40 | Damage per armour (INFERENCE 4/3/2/1/1/3), the stats mirror and clamp, HP at hurt tick. The contact dx is picked by a short sweep, as c1's reach-sweep. |
| A3 | Beannoid in contact, synthetic attack nibble 1 to 7, power 4, inventories kit, leather, charm, bracelet | 28 × 12 | Sheet response per nibble. Charm + fire gives hurt without HP loss (INFERENCE, section 0.8). |
| A4 | Beannoid placed at 8 compass bearings and 4 near-diagonals around the hero | 12 × 8 | `+0x8C` after the hurt (the real `GetDirection 0x8002E814` on raw 16.16 deltas hero − toucher). The DLL already has a bit-for-bit port (`ScriptHelper.GetDirectionToTarget`). |
| A5 | Touch during: swing 0x10 (as `H1-both`), ground walk, jump apex, jump landing; pad held during the hurt | 6 × 60 | The entry overrides any state. The pad is ignored during 0x31 and 0x1C. In the air: gravity, and the end of 0x1C writes 1 not Idle. |
| A6 | A1 kept in contact for 260 ticks, plus a variant timed so the next overlap falls at hurt + 118 to + 122 | 3 × 260 | The hit pass skips the hero while `+0x1CC != 0`: no `TouchingEntity`, no new `+0x1D0`, no attacker `HitCounter`. The boundary tick. Second hurt at hurt + 120 (INFERENCE). |
| A7 | Two overlapping Beannoids at the hero | 20 | First attacker wins, HP −1 not −2 (extends c1's H7). |

*Group B, dangerous cells (no toucher).*

| ID | Setup | Ticks | What it pins |
|---|---|---|---|
| B1 | Cell word {0x80, 0x100, 0x180} × boots {none, 0x1A, 0x1B, 0x1C} (levels 0, 1, 2, 3) under a standing hero | 12 × 140 | Cost 1 / 2 / 3 (FACT table `0x80028B8C = [0,1,2,3]`). Boots 3 gives no damage (`slti 3` at `0x80031AD8`). Periodic re-hurt every 120 ticks. |
| B2 | HP 2 with cost 3, HP 1 with cost 1, HP 3 with cost 3 | 3 × 80 | `a1 = cost < hp ? hp − cost : 0` (`0x80031B14-0x80031B20`). |
| B3 | Hazard under one corner only (OR rule), hazard cell at a different height, hazard plus a toucher | 3 × 60 | One corner suffices. A height mismatch gives no `VramOR`. With a toucher the branch is `ComputeNewHp`, so armour applies and the cost table is ignored. |
| B4 | Facing in 8 directions | 8 × 12 | `+0x8C = (+0x8C + 0x10) & 0x1F`. |

*Group C, water (`Slope_18c == 4`, hurt state 0x3A).*

| ID | Setup | Ticks | What it pins |
|---|---|---|---|
| C1 | 3×3 deep-water cells (0x800), boots item 0x1B, Fish with animation 3 touching | 100 | 0x3A vs 0x31, then what 0x3A / 0x3B do at hurt + 1 (UNKNOWN until run). Feeds E20.i. |
| C2 | Same, water under only 3 corners | 60 | `min` rule gives 0x31 (INFERENCE). |
| C3 | Same, boots 0 | 60 | Informational: `0x800374FC` blocks entry to deep water below boots level 2 (O-E19-22). |

*Group D, control flags.*

| ID | Setup | Ticks | What it pins |
|---|---|---|---|
| D1 | `[0x800DC4B8]` in {0x20, 0x24, 0x30, 0x10, 0x04, 0x28, 0x08, 0x40, 0x48, 0x80, 0xA0} with a toucher overlapping; for 0x20, a release to 0 at tick 40 | 11 × 60 | Only exactly 0x20 reaches the entry. Then HP −1 and `+0x1CC = 0x78` at `0x80031C2C`, with **no** animation, direction or sound change. Flags 0x24 / 0x30 / 0x04 / 0x10 lose the hit (the hit pass has already set `+0x1D0`). Flags with 0x08 or 0x40 stop `MovePlayer` entirely (`& 0x48`). The epilogue (sheet, mirror) still runs while locked. |
| D2 | 0x20 on a hazard cell | 40 | The shared entry again. |

*Group E, HP and stats.*

| ID | Setup | Ticks | What it pins |
|---|---|---|---|
| E1 | HP 1 + Beannoid, HP 2 + Orc, HP 2 + cost-3 cell | 3 × 100 | HP floors at 0. Death branch `0x800327A4` entered at hurt + 1 (coverage point). Divergence from the "no death" DLL (see section 8). Regen blocked at HP 0. Informational for E18. |
| E2 | Stats HP written from outside between two ticks; real `0x800333AC(hero)` called between two ticks | 2 × 10 | Entity wins; the stats follow at the next epilogue. This pins the vessel-seam migration of section 0.2. |
| E3 | Hero created from stats (HpMax 12, Hp 4) | 3 | Spawn load order. |

*Group F, regeneration.*

| ID | Setup | Ticks | What it pins |
|---|---|---|---|
| F1 | Inventory tonic (item 0x38 set as ItemId), HP 5/10, no pad | 450 | HP +1 at ticks 201 and 402 (INFERENCE). |
| F2 | F1 with Up held at tick 100 | 450 | Counters zeroed by the failed gate; first +1 at 301. |
| F3 | F1 with `[0x800DC4B8] = 0x04` for ticks 150-200 | 450 | `& 0x7C` gate. |
| F4 | HP = HpMax; HP = 0 | 2 × 250 | No regen. |
| F5 | Ring 0x37; armour/weapon only | 2 × 450 | Only accessories regenerate. |
| F6 | Hurt during regeneration | 450 | Regen continues during invulnerability (no animation gate in the code). |

*Group G, equipment and boots (K1 style, 2 to 3 ticks each).* Boots level for items {none, 0x19, 0x1A, 0x1B, 0x1C, 0x1A+0x1C, 0x1B+0x1A} against the rule 0x1C → 3, 0x1B → 2, 0x1A → 1, else 0 (`0x8003081C-0x8003088C`). Sheet bytes 3 to 13 for the armours (0x11 to 0x14), the bracelet and the charm.

*Group H, item driver.* Invariant columns in every run: `[0x80127164] == 0` and `[0x8012715C]` unchanged. Optional informational probe: press Circle with seed / cape / bomb to see whether `[0x80127164]` becomes non-zero, then hurt the hero. Adopt it only if the probe starts the state.

About 80 runs and 10,500 ticks per interpreter (INFERENCE from the documented 60 to 100 ticks/s).

---

## 3. Facts the scenarios rest on (binary)

| Item | FACT |
|---|---|
| Normal path | `flags |= 0x100` and `CreatePlayerAnimationEffects(0)` (`0x80031C30-0x80031C3C`); entry at `0x80031C44`; result ≠ −1 starts the hurt block. |
| Hurt block | `[0x80126FFC] = 0` (`0x80031C5C`); `UpdatePlayerCarriedEntity(2)` with `[0x80127110] = 0` in its delay slot (`0x80031C64-0x80031C68`); `0x8003634C` (`0x80031C6C`); `+0x88 = 0x31`, or `0x3A` when the `+0x18C` read at `0x80031B98` is 4 (`0x80031C74-0x80031C84`); `+0x8C` = entry result; `+0x1CC = 0x78`; jump to the epilogue. |
| Toucher branch | `ComputeNewHp(hero)` (`0x80031A90`) then `GetDirection(hero.x − toucher.x, hero.y − toucher.y)` (`0x8002E814`). `+0x1CC` is not tested here. |
| Hazard branch | Requires `+0x224 == 0`, `+0x1CC == 0`, `[0x80127000] < 3`, `(+0x180 & 0x180) >> 7 != 0`. Otherwise it returns −1. |
| No-hit tail | `+0x14 == 0` goes to the death branch (`0x80031CA8`); `+0x188 & 0x80` goes to `0x80032768`; the driver runs at `0x80031CC4`. |
| Death branch | Table `0x80022FBC` indexed by `anim − 0x1C`: 0x31 / 0x39 → `0x80032870`, 0x1C → `0x80032898`, 0x3A / 0x3C → `0x80032884`, 0x3B → `0x800328CC`, 0x4E / 0x4F → `0x800327F0` (item 0x27 or `g_desiredMap = 0x1DD`). |
| Locked branch | `[0x800DC4B8] & 0x34`: `CreatePlayerAnimationEffects(1)`, charge and pointer zeroed, `UpdatePlayerCarriedEntity(1)`, `0x800350C0`, then the 0x20 test. |
| Carried entity | `0x8002F884` returns at once when hero `+0x2C` is null (`0x8002F89C`), so it is a no-op in c2. Dropping a carried object is E20.g. |
| `CreatePlayerAnimationEffects` | Anim 0x1C / 0x31 / 0x3A / 0x3B map to the default target `0x8002FDC8` (table `0x800228AC`): no hurt-specific effect. |
| Equipment epilogue | `UpdateItemEffectState` (`0x800307E8`): boots → records → sheet copy (255 bytes) → response combine → regen. |

---

## 4. Per-tick log

State AFTER the passes of the tick, as in c1. The hero dict gains these keys; the targets keep c1's columns.

| Group | Columns |
|---|---|
| Existing (c1) | `status`, `hp` (`+0x14`), `anim_target`/`anim_cur`, `dir_target`/`dir_cur`, `anim_flags_B4`, `force_x/y/z`, `pos_x/y/z`, `on_ground`, `c1cc`, `c1d0`, `hit_counter`, `touching_slot`, `ctl_20`, `frame_counter_1C`, latch `0x80146DF8`, RNG word, journal (sound id, effect id and centre, handler stubs). |
| New entity | `hpmax_18`, `vram_or_180`, `vram_and_184`, `slope_18c`, `anim_end_AC`, `carried_2C`, `visible` (hero pointer present in the list at `0x80134250`), `visible_count` (`0x8013435C`), `blink_expected = (c1cc & 3) == 3` (a check column, not an input). |
| New global | `stats_hp`, `stats_hpmax` (`[0x80176318]+0/+2`), `ctl_flags` (`0x800DC4B8`), `hold_word` (`0x80126F28`), `boots` (`0x80127000`), `sheet_r1..r11` (bytes 3 to 13 of `0x80127008`, the full 255 bytes only at tick 0 and on change), `regen0/1/2` (`0x80126FE8/EC/F0`), `item_state` (`0x80127164`), `item_ticks` (`0x8012715C`), `charge` (`0x80126FFC`), `wptr` (`0x80127110`), the three equipment pointers (`0x80146E00/08/10`). |

The `.txt` writer reuses `scen.ent_table` with `extra=` columns.

---

## 5. Checks of the oracle itself

- **D1 gate and harness regression.** S-A and S-B must stay at 216 values each with 0 differences. The c1 `H3` rows must be equal on the common columns.
- **Independent model (`hmodel.py`)**, written from the plan and this report, fed only with inputs and geometry columns, not with the code:
  - damage = c1b's `ResolveBalanceTarget` rule on the hero sheet;
  - hazard cost and floor;
  - the `+0x1CC` sequence and the blink phase;
  - the stats clamp (`max < hp → max`, `hp < 0 → 0`, max capped 50);
  - the regen counters;
  - boots from the inventory.
  - Sheet combination reuses `kmodel.combine_response`.
- **Second interpreter**: all scenarios through `mips.py`/`interp2.tick2`, compared text for text and slot bytes after every tick. The `before` hooks are interpreter-neutral.
- **Mutations** (about 14, expected change written beforehand in `hmut.py`, all must be caught), listed as `change : scenario that must catch it`:
  1. Toucher branch `beqz` at `0x80031A88` forced : A1.
  2. Direction operands swapped : A4.
  3. Boots test `slti 3` → 4 (`0x80031AD8`) : B1.
  4. Cost index shift `srl 7` (`0x80031AF0`) : B1.
  5. `0x78` → `0x77` (`0x80031C90`) : blink and window.
  6. 0x31 / 0x3A selector inverted : C1.
  7. Blink `andi 3` → 7 (`0x800385F0`) : visible column.
  8. Regen `slt` at `0x80030CD4` : F1.
  9. Regen pad gate `bnez` (`0x80030C5C`) removed : F2.
  10. `andi 0x7C` → 0x74 (`0x80030C2C`) : F3.
  11. The `== 0x20` compare (`0x80031C0C`) : D1.
  12. `SetPlayerHp` clamp direction (`0x8004DD44`) : E.
  13. Hazard HP floor : B2.
  14. Control: a debug-only read (no change expected).
- Coverage of `0x80031A68` (every instruction), the regen block `0x80030C88-0x80030D28`, the epilogue; the load-delay hazard check; two runs give an identical manifest.

---

## 6. Work estimate

| Piece | Lines (new) | Notes |
|---|---|---|
| `hworld.py` (stats-based hero HP, sheet warm-up, cell painter, BSS extras, KINDS, inventories, extra columns) | about 250 | No edit of any copied file. |
| `hscen.py` (about 80 runs as data, writers, summary) | about 400 | Reuses `scen.write_scenario`, `describe` with extra columns. |
| `hmodel.py` | about 250 | |
| `hmut.py` + `run_c2.py` + coverage / manifest | about 350 | Same pattern as `kmut.py` / `run_c1b.py`. |
| Notes and report | — | |

- **Size**: about 1,250 new Python lines against about 1,800 for c1b's K tools. One `executor` contract (the bulk of one working day for an agent), then one fresh `verifier` with a binary re-decode.
- **Run time**: about 5 to 8 minutes for both interpreters plus mutations (INFERENCE from c1's and e20-b's documented timings).
- **Repo growth**: about 5 to 8 MB with compact JSON.
- **DLL side, not asked but implied by section 0**:
  - entity-borne HP and mirror;
  - spawn from stats;
  - vessel seam;
  - entry (toucher and hazard);
  - 0x31 / 0x1C (and 0x3A) handlers;
  - `+0x1CC` and `IsVisible`;
  - boots / sheet / regen;
  - epilogue on every exit;
  - the 0x20 path;
  - rewrite of the c1a pins.

---

## 7. What an oracle cannot show, and how it is accepted instead

| Not shown | Why | Acceptance |
|---|---|---|
| Rendering of the blink | The oracle sees only list membership (FACT: the lists pass and the draw loop at `0x8003B4A4`), not pixels. | (a) A DLL test: the published `IsVisible` of the hero (`AlundraFrameSyncPasses` already drives `entity.IsVisible`, `:96-104`) equals the oracle's `visible` column of the last tick of each image, for images of 1 and 2 ticks. It must not fight the destroyed-entity use of that flag. (b) An in-process back-buffer capture series (`GetBackBufferData` + `SaveAsPng`, the `--screenshot-out` method; never CopyFromScreen) over about 20 consecutive images after a scripted contact, counting images where the hero region is absent: period 4, first image hidden. (c) The author's eye. |
| Shadow follows the blink | UNKNOWN: the draw loop submits `+0x194` packets, and whether the shadow lives in that packet is not decoded. Also UNKNOWN: whether the DLL hero prefab has a separate shadow renderable. | Decide by decoding `0x8003B4A4-0x8003B600` and listing the hero prefab's components. |
| Hearts on screen | The HUD is E13's. The oracle gives `stats_hp` / `stats_hpmax`. | Existing HUD tests plus the in-game recipe on map 14. |
| Audible hurt sound | The oracle gives the sound id (23, FACT in `H3.txt`) at the tick. | Existing animation-sound tests plus the recipe. |
| Real enemy behaviour | Targets are inert (their native handlers are E14). | The recipe uses map 14 Beannoids standing still. |
| Engine collision of the knockback with walls | The field is flat. | Optional later scenario with a wall cell behind the hero. |
| The 0x20 path in the game | Nothing sets bit 0x20 yet (section 0.4). | Oracle + a test poking the flag. |

---

## 8. UNKNOWNs and what decides each

1. **0x3A / 0x3B behaviour in water** (INFERENCE from `0x80031DB4-0x80031DF0` that 0x3A lasts about one tick): decided by C1.
2. **Armour 0x13 / 0x14 response bytes per nibble** (only "8" and "4" are recorded in D4's check): decided by group G (K1-style).
3. **Whether the inert Beannoid body blocks the hero** in A6 (it changes whether he stays in overlap for the second hit): decided by A1 and A6 rows. Fall back to a harness-placed overlap.
4. **Second-hurt tick (138 in H3 terms)**: INFERENCE; decided by A6.
5. **Item-driver probe** (can Circle start `[0x80127164]` with the simplest items?): a short probe before writing group H. If not, group H stays an invariant check.
6. **Hazard cells and deep water on a real map arc**: map 52 or 55 for hazards (FACT, hazard cells present); map 14 for water (81 deep-water words, FACT census). Whether the test saves reach them is UNKNOWN. Decide with the author or the E19.d2a save list.
7. **Fire / ice effects 4 and 5 on the hero**: attached to the target by the hit pass (`0x8003BE74`) and journalled as a stub. The real effect belongs to E20.f. The oracle only journals them.
8. **The "no death" DLL divergence (D-E20-5)**:
   - FACT: the binary enters `0x800327A4` at hurt + 1.
   - INFERENCE from `0x800328B0-0x800328C8`: on the ground, the hero stays in 0x31 and 0x1C until the 0x1C end flag, then writes `0x39`.
   - The DLL (no death) will go Idle there.
   - Compare HP-0 scenarios through the end of 0x1C, then pin the DLL's own behaviour with a DLL-only test.
9. **Packet-level shadow behaviour**: see section 7.
