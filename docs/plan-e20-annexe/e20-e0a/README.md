# E20.e0a1 - oracle of the apparition by sprite index, catalogue and interface (T0: prediction and starting state)

Written and committed BEFORE any code of E20.e0a1 (DLL or oracle run). Contract: `docs/plan-e20-heros.md`, section 1.16 (head) and 1.16.1 (revisions 1 and 2, READY at the closing review),
decisions D-E0A-1..7 and D-E20-33..35. This file is the annex of the slice: T0 holds the prediction, the measured values are added by the later tasks (section "Results" at the end).

## What is proved

The real `SpawnWarpEntity` (`0x80039F58`, which calls `0x80039B28` for the page lookup, `0x80039AD0` for the free slot, `0x80039D04` for `InitializeEntity`, `0x80038AB4` for the first
animation switch and `0x80032A40` for the creation roll) of `ALUN_CD.EXE` (France), run in the emulated machine of the E14/E20 oracles with both sprite pages installed, against the DLL's
`SpawnEntityBySpriteIndex(parent, isMapPage, index, x, y, z, direction)`. Nothing in the repository calls the interface (no behaviour change).

## How to run (from the repository root)

- `python -B docs/plan-e20-annexe/e20-e0a/oracle/run_e0a.py` writes `oracle/out/` (about 40 s; exit 0 = every check of `summary.txt` is PASS). Two runs give the same `manifest.txt`.
- `python -B docs/plan-e20-annexe/e20-e0a/oracle/mutants.py` writes `oracle/out/mutations.txt` (about 6 min; exit 0 = every mutant CAUGHT).
- Needs Python 3 with capstone, the disc EXE and `BALANCE.BIN` named in the e14a notes, and the map JSONs of `data-extracted/data/` (read only). Nothing outside `oracle/out/` is written.
- `oracle/tools/` is byte-identical to `docs/plan-e14-annexe/e14a/oracle-a1/tools` plus three discovery probes; `oracle/checksums.txt` lists the SHA-1 of every tool and of `espawn.py` (the harness: its
  `DATA` path is relative to the repository root, and it gained the `raised` option and six decoded fields).

## Vectors (oracle) and what the DLL tests compare

| Set | Content | Predicted count |
|---|---|---|
| V1 | global page, one per valid index, parent hero, flat field, direction `0x10`, Z 0 | 151 |
| V2 | map page, one per valid number (one creation image per number on every map where it is valid), source map = the lowest map where it is valid | 244 |
| V3 | geometry: the ten hero projectile types (3, 4, 7, 11, 12, 14, 15, 27 to 29) and items 1 and 2 at Z in {0, hero Z (1 raw), 16 px, -5 px}, the four directions, the three parents, prefabs with `OffsetZ != 0` above the ground (page 0 n. 20 at 50 px, n. 64, n. 242, map page n. 153 and others), a terrain raised by 32 px | at least 40 |
| V4 | validity of the 256 indices of each of 483 maps, plus the invalid probes (-1, 256, 300, `0xFFFFFFFF`, `0x80000000`, entries 0 and `0x1000`) | 123 648 calls, 2 507 valid pairs, 65 empty maps, at most 22 per map, 244 distinct numbers |
| V5' | table: five spawns, status 4 stays busy until the slot pass, 63 accepted then the 64th and 65th refused with a message each, lowest free slot first, a dirty free slot | 63 accepted |
| V6' | numbers of the map page valid on map 14 (the full interface path of the DLL runs on map 14) | measured in T1 (the file lists `[79, 136, 144, 153, 199]`) |
| V7 | the spawn calls journalled by the AT9 traces (`docs/plan-e20-annexe/e20-b/oracle/out/at9`): types 3, 4, 11 and 14, on the ground and in the air | 8 |

Other predictions: creation roll (`Contents & 0x80`) on 54 numbers (6 on the global page, 48 on the map page, excluded from the comparison of `+0x3C`, `+0x40` and the generator word, D-E0A-3);
creation sound on 7 prefabs of the global page (7, 8, 9 -> `0x21`; 17 -> `0x37`; 27, 28, 29 -> `0x1E`) and 9 numbers of the map page (159, 170, 192, 194, 213, 242, 249, 254, 255), 3 of them (192, 194, 249)
with the `0x100` flag in the sound id; the second interpreter (`mips.py`) identical on at least 159 calls; at least 23 one-word mutants, all CAUGHT.

## Fields compared (the projection, about 28 DLL fields)

DLL name <- offset of the entity in the binary. `PosZ` and `ModdedPosZ` of the DLL are the binary's minus 1 (ADR-0026); the DLL's `FloorHeight` and `MapHeights[k]` are the binary's minus 1 as well
(`UpdateEnemyGroundHeights`); `TerrainHeight` is the raw probe. The frame of the interface is the DLL's (D-E0A-1): it adds the raw unit itself, the shared core takes the binary's argument.

| DLL | binary | | DLL | binary |
|---|---|---|---|---|
| `Hp`, `HpMax` | `+0x14`, `+0x18` (balance sheet of `SpriteType`, threshold 0) | | `ModX`, `ModY`, `ModZ` | `+0x1E4`, `+0x1E8`, `+0x1EC` |
| `EntityRefId` | `+0x48` (-1) | | `Width`, `Height`, `Depth` | `+0x1F0`, `+0x1F4`, `+0x1F8` |
| `SpriteType` | `+0x68` (index, plus `0x100` for the map page) | | `PosX`, `PosY` | `+0x114`, `+0x118` |
| `Flags` | `+0x6C` | | `PosZ` | `+0x11C` minus 1 |
| `SpriteProgramIndexes[0..5]` | `+0x70..+0x84` | | `ModdedPosX`, `ModdedPosY` | `+0x1D8`, `+0x1DC` |
| `ProgramIndexes[0..5]` | `+0x4C..+0x60` (zeros) | | `ModdedPosZ` | `+0x1E0` minus 1 |
| `TargetAnimationId`, `TargetDirection` | `+0x88`, `+0x8C` | | `TileX`, `TileY` | `+0x120`, `+0x124` |
| `TerrainHeight` (recomputed from the field) | `+0x138` | | `FloorHeight` | `+0x134` minus 1 |
| `MapHeights[0..3]` | `+0x158..+0x164` minus 1 | | parent, root | `+0x0C`, `+0x08` (`ParentEntity`, `ChildEntity`) |
| `HitAnimByte`, `CurrentAttack != null`, `HasFrameBox` | `+0xB4`, `+0x1C8 != 0`, `+0x1D4 != 0` | | sound requested | journal of `0x800490FC` |

Not compared in e0a1 (written down so that the later tasks do not forget them): `CurrentAnimationId`, `CurrentDirection`, `AnimationDirection` (the first switch of the animation is e0a3); case and series
(`+0x00`, `+0x04`, e0a2); status (the DLL's `Loaded` stands for the binary's state 1); `EventTrigger` (-1 in the DLL, 0 in the binary: the first tick is the next frame until e0a3, tested apart);
`+0x3C`, `+0x40` and the generator word (the creation roll, e0b); the self pointer `+0x230` (`LogicContextEntity`); the stored hit box (`+0x1FC..`); the palette and the sheet
(`+0x1B0`, `+0x1B4`): the oracle fixes them per page (global page: palette `0xB`, sheet `0x60`; map page: palette 0, sheet `0x20`) and the DLL does not store them (nothing reads them: the drawing comes from the
prefab), decision of this slice; `TileZ` (the DLL leaves what the record path leaves: the binary rewrites it at every physics tick).

## Starting state (T0 measures, 2026-10-15, quiet window: no export and no suite running on the tree)

- Branch `chantier/e20-heros`, base `1ccc3c3`.
- `alundra-project/Data/sprite-records.json`: 2 571 585 bytes, SHA-1 `88d723066fd2e6232a6f815dad8de9b0c05c64e4`.
- `alundra-project/Data/map-sprite-indices.json`: 30 913 bytes, SHA-1 `63a3e6189d1f1375232bb2c54e1d1dcb385e360d`.
- Tests: `Alundra.Tests` 4 752 (`--list-tests`, Release), converter 854 (`--list-tests`, Release).
- Python 3.14.2.

## Results

(Added by the last task of the slice.)
