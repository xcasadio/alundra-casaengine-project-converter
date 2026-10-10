# E14.a0b T1 - the shared helpers and the ground of the enemies, all real code (2026-10-10)

Purpose: reference values for the ports of E14.a0b (`docs/plan-e14-ia-native.md` section 1.2): (a) each shared helper of the enemy AI run as **real code of `ALUN_CD.EXE` (France)** on at least 10,000 random states; (b) the floor
(`+0x134`) and the four corner heights (`+0x158`-`+0x164`) of the Beannoid and Muruta bodies and of the hero next to them, on the **real cells of maps 14, 115 and 116**.
Ground truth: PS-X EXE, text at `0x80020000`, file offset `0x800 + addr - 0x80020000`. Labels: **FACT** (decoded instruction words or a measurement, with where), **INFERENCE**, **UNKNOWN**.

`tools/` holds byte-identical copies of the 27 tools of `docs/plan-e20-annexe/e20-c1b/oracle/tools/` (the interpreters `emu.py` / `mips.py` + `interp2.py`, the machine, the world builder, the real spawner set-up of `c1world.py`, `kworld.py` for
the two-engine `call`) and the new files `hmodel.py h1.py h2.py hmut.py run_e14a0b.py`. Nothing outside this folder is touched.

## 0. EXPECTED VALUES (written before the first run of h1.py / h2.py; a run that disagrees is a finding, never re-fitted)

### 0.1 Helpers (a): the independent model `tools/hmodel.py` is the prediction of every row
Written from the disassembly (`out/dis.txt`), sharing with the emulated binary only the data tables of the EXE (atan `0x80022488` / `0x80022688`, quarter-turn `0x80028B34`, slope table `0x800236D4`).
- **relpos `0x8007FE20`**: `[|dx|, |dy|, |dz|, dx, dy, dz]` with `dx = self(+0x120) - hero(+0x120)`, `dy` likewise on `+0x124`, `dz = self(+0x134) - hero(+0x134)` (hero = slot 0, `0x80127D30`); `negu` wraps for the minimum. **relpos2 `0x8007FE8C`**: same with the Z of the entity = `+0x11C`.
- **TryAttackPlayer2 `0x8008048C`** `(a1 = rel, a2 = dir & 0xFF, a3, sp+0x10 = d2, sp+0x14 = d3)`: 0 if `d3 < |dz|`; then by direction 0: `a3 >= |dx|`, `dy <= 0`, `d2 >= |dy|`; 1: `a3 >= |dx|`, `dy >= 0`, `d2 >= |dy|`; 2: `a3 >= |dy|`, `dx >= 0`, `d2 >= |dx|`; 3: `a3 >= |dy|`, `dx <= 0`, `d2 >= |dx|`; any other direction 0. All comparisons signed.
  **TryAttackPlayerFront `0x800805C8`** `(a0 = entity, a1 = rel, a2, a3, sp+0x10 = d3)`: the same with the direction = the WORD at `entity+0x98`, `a2` in the place of `a3` and `a3` in the place of `d2`.
- **HandleAnimationDirection `0x80080144`** `(entity, anim, dist)`: `thr = dist + 1`, `t[k] = floor(+0x134) - corner[k](+0x158 + 4k)`; the decision tree of `0x80080170-0x80080258` (in `hmodel.handle_animation_direction`); on a turn it writes `+0x88 = anim & 0xFF`, zero to `+0xBC +0xC0 +0xC4 +0xC8 +0xD4 +0xD8`, `+0x8C = table[0x80028B34 + (+0x8C)]`, returns 1; else returns 0 and writes nothing.
- **StartWalk `0x8007FEF8`** `(entity, anim, range, chance)`: `s1 = lcg(rng)`; `hi(s1 * 0x65) < (chance & 0xFF)` (signed) -> `+0x8C = direction(hero - entity)`, else `s2 = lcg(s1)`, `+0x8C = hi(s2 * 0x20)`; `+0x88 = anim & 0xFF`; if `range & 0xFFFF != 0`: `s3 = lcg(.)`, `+0x282 = (hi(s3 * 0x10) + range) & 0xFFFF`; the random word is left at the last state.
- **direction `0x8002E814`**: the octant table, flipped by the signs (`0x18 + v`, `8 - v`, `0x18 - v`, `8 + v`), `& 0x1F`; the port `ScriptHelper.GetDirectionToTarget` is checked against it (domain: |component| < 2^31).
- **D1 `0x8007D9A4` / D9 `0x8007DE04`** (ComputeNewHp stubbed, validated by E20.c1b): `+0x277 = 1` when it returned non-zero (else unchanged), `+0x88 = 5` (D1) / `2` (D9), `+0x8C = direction(entity - hero)`; `ComputeNewHp(entity)` is called exactly once.
- Every helper: no other byte of the two entities changes (write-set check), no unwritten byte is read, instruction coverage 100% of the function except the instructions no input can reach (listed in the output), the second interpreter gives the identical text.

### 0.2 Ground of the bodies (b)
- **Corners** (`0x800370C4`, FACT `0x80037154-0x80037340`): for the corners (x1,y1), (x2,y1), (x1,y2), (x2,y2), `x1 = (PosX + off1E4) >> 16`, `x2 = (PosX + off1E4 + size1F0) >> 16`, `y` likewise with `+0x1E8`, `+0x1F4`; cell = column table `[0x80127168 + 2x]` clamped 0..51, row `y >> 4` clamped 0..59;
  slope bits (`byte2 & 3`) 0: `height << 4`; else `(height - 1) << 4` plus the slope term (1: `0x10`, or `0x10 - y mod 16` when no slope of type 2/3 was seen before in the same call; 2: `0x10` or `table[23 - x mod 24]`; 3: `0x10` or `table[x mod 24]`); all `<< 16`; stored raw at `+0x158 + 4k`;
  the result (stored at `+0x138` by the callers) is the maximum with 0. The independent model `hmodel.ground_model` computes the four values; the emulated binary must give them.
- **Floor** (`0x80037F28`, FACT `0x80037F2C-0x80037F3C`): `+0x134 = TerrainHeight + 1` for an entity without the flag `0x80` (Beannoid `0x103`, Muruta `0x30103`); for the hero (flag `0x80`) the loop over the collidable list (`[0x80127D2C]` = 0 here: the tick passes that rewrite it are not run) finds nothing: `+0x134 = TerrainHeight + 1` too.
- **Mapping to the DLL** (FACT `AlundraEntitySpawnFactory.cs` comment, ADR-0026): every height of the DLL is the binary's minus 1 (rest `PosZ = TerrainHeight` instead of `+ 1`). Hence the single formula to be checked in C#:
  `FloorHeight(DLL) = FloorHeight(binary) - 1`, `MapHeights(DLL)[k] = corner(binary)[k] - 1`, so that `FloorHeight - MapHeights[k]` and `|dz|` between the hero and a body are the binary's.
  The formula fails (STOP) if any enemy or hero case differs by another amount, or if one of the differences `floor - corner[k]` or `|dz|` differs.

## 1. How to run
```
python -B docs/plan-e14-annexe/e14a/oracle/tools/run_e14a0b.py      # exit code 0 = everything below passed
```
(see section 3 for the result files). Needs Python 3 with `capstone`, the disc file `D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE` (+ `DATA/BALANCE.BIN`),
`data-extracted/data/map_14.json map_115.json map_116.json map_alundra.json BALANCE.BIN.json`. Always `python -B`. Two complete runs give identical manifests (no clock, no host path, no hash-ordered iteration; the gzip files are written with mtime 0 and the manifest holds
the md5 of the DECOMPRESSED text).

## 2. Real code, stubs, harness writes

| Piece | Run as |
|---|---|
| The helpers of section 0.1, `0x800370C4` (corners), `0x80037F28` (floor), `0x80038064` (tile state, floor), `0x8002E814` (direction), D1 `0x8007D9A4`, D9 `0x8007DE04` | **real** (both interpreters) |
| The map-record spawner `0x8003A1B8` (-> `0x80039BD0`, `0x80039B28`, `0x80039AD0`, `InitializeEntity` `0x80039D04`, `SetEntityDimensions` `0x80039C40`, the spawn loot draw `0x80032A40`), the hero `InitializeEntity`, `InitPlayerStats` / `AddItem`, `0x80044520` and `0x80044550` (balance) | **real**, run on `emu.py` (the set-up); every call of a function under test goes through the interpreter under test |
| `ComputeNewHp` `0x8003A374` | **stub** in the D1 / D9 runs only (journalled: it must be called exactly once, with the entity; its result is the chosen input `killed`); the real function is the object of the E20.c1b oracle |
| Effects, sound, spawn of `machine.py` | stubs of `machine.py` (journalled; the spawner may reach them for the loot draw) |

Harness writes: every input field of a helper (rows of `out/h1`), the marker values `0x11111111` (the six forces), `0x7777` (`+0x88`), `0x5555` (`+0x8C`), `0x1234` (`+0x282`) so that a zeroing or a write is visible, the decoy `(box_direction + 1) & 3` at `+0x9C` in the `tapfront` runs (the DLL's
`AnimationDirection`: the helper must read `+0x98`), the cells of the real maps (word 0 of each cell only; word 1 is left unwritten so that a read of it would be reported), the map constants (`Gravity`, `ZViscosity`, `SlideEffectId`, `BalanceLevel`, `C`, `D`) of the map header,
the entity records of the bodies (20 bytes each, from the decoded record, with the spawner called with `a2 = 1`: a forced spawn, the window test of `0x8003A208-0x8003A25C` and the bit-`0x40` test being skipped - the Muruta of the hall have `SpriteDirection 0x80` and are spawned by script),
the hero by the real `InitializeEntity` at the pixel asked (PosZ argument 0: the function raises it to the terrain height + 1), the random word `0x12345678`.

## 3. Setup choices (h2)

| Item | Choice |
|---|---|
| Maps | 14 (Overworld 3,1: the 7 Beannoid records 0-6), 115 (Tarn's Manor hall: the 11 Muruta records 6, 7, 8, 9, 12, 13, 14, 24, 25, 26, 27), 116 (Tarn's Manor grate: the 11 Beannoid records 0, 1, 17, 24-27, 41-44); asserted from the data (`SpriteTableIndex` 79 / 91) |
| Bodies | the real spawner on the real sprite record (`SpriteRecords[79 or 91]`) and the real map record (position, `Height`, `SpriteDirection`, event codes); `+0x1E4..+0x1F8` (box offsets and sizes) are the record header's, written by `SetEntityDimensions` |
| Hero | the real `InitializeEntity` on the hero record, placed at the body's pixel + (0,0), (24,0), (-24,0), (0,16), (0,-16) (the places of the tile neighbours; those outside the 1248 x 960 px map are skipped) |
| Variants | 0 = the values the creation left (the spawner runs `0x800370C4` at `0x80039EE0` and `0x80038064` at `0x80039F30`); 1 = the end of a physics step at the same position (`0x800370C4`, its result stored at `+0x138` as `0x80037DF8` does, then `0x80038064`); 2 = the body displaced by (dx, dy) in {-36, -24, -12, 0, 12, 24, 36} x {-24, -16, -8, 0, 8, 16, 24} px (49 places around its record, including cells of other heights and slopes), the hero beside the record place |
| "Flat" and "raised" | read from the cells: rows whose body terrain is 0 (flat ground) and rows above it; both occur on maps 115 and 116, map 14 has raised ground only (its 7 Beannoid stand at 16 to 80 px, sweeps to 160) |
| Collidable list | `[0x80127D2C]` = 0 (the tick passes that rewrite it are not run): the hero's loop over supports finds nothing, `+0x134 = terrain + 1` for him too |

## 4. Results (FACT, `out/summary.txt`, `out/h1.txt`, `out/h2.txt`)

- **h1**: 9 helpers x 10,000 states = 90,000 real-code runs, each equal to the independent model and to the second interpreter; no unwritten-byte read; no entity byte changed outside the declared output set; every instruction of every function executed except the 6 that no input can reach (the fall-through after a dispatch on a value already shown to be 0 or 1 in `0x8008048C`, `0x800804C4`-`0x800804C8`; the four instructions after the flip-index tests of `0x8002E814`, `0x8002E8C0`, `0x8002E8C4`, `0x8002E8D8`, `0x8002E8DC`).
  Domain: the rows of `had` keep the target direction in 0..31 (the quarter-turn table has 32 entries); the rows of `atan` keep every component below 2^31 in magnitude (the minimum value is not a direction input).
- **h2**: 1,711 rows (variant 0 / 1 / 2: 145 / 145 / 1,421), every row of body and hero equal to the independent model (terrain = max(0, corners), `+0x134 = terrain + 1`), second interpreter identical, no unwritten-byte read (the second word of the cells is never read). Values for the ports:
  `floor - terrain = 1` raw unit for every body and hero row; `floor - corner[k]` is 1 on flat ground and the height difference + 1 elsewhere; the `|dz|` of the pair is the difference of the floors (0 to 7,340,032 raw = 112 px).
  The Muruta of the hall (records 24-27) are created at `PosZ` 128 px + 1 with a floor of 1 (terrain 0): they fall to the ground; the relative position uses the floor, not `PosZ`.
- **mutations**: 19 one-word patches, each caught (predicate written before the run + the independent model disagrees), 2 controls unchanged. Three first expectations of the mutation file were wrong as WRITTEN and were corrected from the instruction semantics before the corrected run, not from the output:
  M1 (removing the `bgez` makes the negation unconditional, so the word is `-dx`, not `dx`); M13 (the first mutation chosen for the direction routine, `slti 1 -> 0`, is an EQUIVALENT mutant: the first row of the octant table is zero, so the flip of `dy == 0` changes nothing; replaced by the flip bit of a negative `dx`);
  M18's probe (the bodies 0 and 1 of map 116 never meet a sloped cell; the probe now runs the bodies 24 and 25 whose sweep crosses sloped cells).
- **Two runs** of `tools/run_e14a0b.py` give the identical `out/manifest.txt` (about 2.5 minutes).

## 5. Limits and open issues

1. D1 / D9 run with `ComputeNewHp` stubbed: the call and its result are checked, not the damage (E20.c1b).
2. The hero's floor with a support (an entity or a platform under his feet) is not exercised here; the existing E2 tests (`AlundraFloorHeightTests`) cover the DLL side.
3. `0x80080030` (another walk start, spread 0x2000) and `0x80080260` (a second animation-direction helper without the animation argument) are not ported: no handler of E14.a1 calls them (C1 calls `0x8007FEF8`, C10 calls `0x80080144`; `dll-natives.md` section 2.4).
4. The sweep places of variant 2 include cells a body cannot reach; they check the formula of the ports, not the game's geometry.
5. The cells come from `data-extracted/data/map_<N>.json`; the DLL tests read the same maps from the exported project (`alundra-project/Maps/...tileMap`), so the C# comparison also checks the export against the extraction.
