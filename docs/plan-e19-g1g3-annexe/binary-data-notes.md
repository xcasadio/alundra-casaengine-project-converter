# E19.g G1 / G3 discovery - binary and data surface (read-only, 2026-10-06)

Scope: the effect reservoir of `ALUN_CD.EXE` (France), its draw path, the nine effect opcodes, the load-time spawn, the native creators
(D-E19-54), a census of the extraction, and a model validated against the REAL code. Nothing was built, run, exported, edited, staged or
committed in the repository. Everything is under this folder (`g1g3-disc/`).

Legend: **[B]** = verified on the binary (address), **[R]** = verified by running the REAL code in the MIPS interpreter, **[D]** = verified on
the data (extraction / DATAS.BIN), **[S]** = from PSX documentation, **[H]** = hypothesis.

## 0. Folder map, how to re-run

| Folder | Content |
|---|---|
| `binary/fxm.py` | the effect machine: the REAL code in `f3-disc/binary/emu.py` fed with DATAS.BIN sprite-info sections (real parser 0x8002D808 run on the section), map load 0x8003C1A4, tick 0x8003C410, the nine handlers, the three native creators |
| `model/fxpool.py` | the python model (pool, InitEffect, animation, position modes 0/1/2/3, tick, nine opcodes, creators), fed by `data/fx_all.json` (the JSON of `data-extracted`), never by the binary's memory |
| `binary/cmp.py` | real vs model, every map at load (157 maps x 2 hero tiles x 300 ticks) |
| `binary/fuzz.py`, `binary/mutate.py` | random op/creator/tick/entity sequences compared after every action; 15 mutants of the model |
| `binary/all_anims.py` | all 363 real animations of the 165 effects, real vs model over 2 cycles |
| `binary/validate_emit.py` | the REAL emitter 0x8002DB48 on all 20 315 effect quad references |
| `binary/json_vs_disc.py` | the extraction's effect JSON against DATAS.BIN byte for byte: 544 records, 363 animations, 5 148 frames, 20 315 images, **0 difference** |
| `data/export_sample.py` | a candidate shape of the per-map effect document (samples for 391, 476 and the global bank: 3 KB, 161 KB, 427 KB indented) |
| `binary/native53.py` | the native `IncreaseHpMaxAndCreateEffect` (0x800333AC), real vs model, 60 random cases incl. a nearly full pool and the shared PRNG |
| `binary/ops_footprint.py` | RAM footprint of the nine handlers |
| `binary/scenarios.py` -> `scenarios_out.txt` | pinned scenarios (391, 476, 163, 135, pool rules) |
| `data/*.py` | census (`census2.py`, `ops_scan.py`, `ops82.py`, `chain_fx.py`, `auto_fx.py`, `deform_by_effect.py`, `sprites_count.py`), `g1_predict.py` (+ `g1_predicted_sprites.tsv`), `anim_table.tsv` |

Results of the validation runs (all done in this session; the extraction JSON equals DATAS.BIN byte for byte on every effect structure, so the model fed by the JSON is also a proof of the data): load/tick 157 maps x 2 tiles x 300 ticks **0 mismatch**; fuzz 660 runs x 200 steps on 11
maps + 20 x 120 after the last change **0 mismatch**; mutation check **15/15 caught** (one mutant was a no-op by construction, "A1 no entity check",
left out of the count); 363 animations **0 mismatch**; emitter 20 315 / 20 315 quads (vertices, uv, semi bit, tpage, clut) **0 mismatch**; native 0x53
60 random cases **0 mismatch** (seed progression included).

## 1. The reservoir [B][R]

- Base 0x80138608, **128 slots x 0x80**, template (all zero, read-only, only reader 0x8003BB24; right after the pool at 0x8013C608; the event array
  starts at 0x8013C688) copied over a slot by `InitEffect` 0x8003BB14, keeping the slot index at `+0x00`.
- Allocation `0x8003B9C4`: the **lowest-index slot whose status `+0x68` is 0**; none -> returns 0 (nothing else: no break, no message).
  Slot ids written once by 0x8003C17C (called from game init 0x8002DF78). Status is only ever 0 (free) or 2 (active).
- Free: status set to 0 by (a) `0x91`, immediately; (b) `UpdateEffects` at the **next** active tick after the end (destroy flag `+0x7D`); (c) map load.
  `DestroyEntity` also frees `*(entity+0x1B8)` but no code stores a pointer there [B] (dead link).
- Five creators, **84 call sites** (rescanned: 70 + 5 + 1 + 8, no function pointer to any creator): `CreateEffectEntity` 0x8003BDD8 (mode 0, free; 70),
  attached 0x8003BE74 (mode 1; 5), **fixed depth 0x8003BF44 (mode 2; no caller: dead)**, detached 0x8003BFE8 (mode 3; 1), `SpawnMapEffect` 0x8003C094 (8:
  opcodes 0x90, 0xA2, 0xA3 = 3; map load = 1; native AI C97 0x800648A8 and C100 0x80065304/0x80065540/0x80065624 = 4).
- Slot layout (offsets) [B], confirmed field by field by the `cmp.py` state comparison [R]: `+0 id`, `+4 map record ptr`, `+8 sprite record ptr`,
  `+0x0C..` the sprite ref (same struct as an entity's `+0x194`: `+0x0C` image list, `+0x10/14/18` x/y/z, `+0x1C` depth key, `+0x20` image-set bias,
  `+0x24` image count, `+0x28` texture-page base, `+0x2C` CLUT base), `+0x30` record index (-1 when not from a record), `+0x34` mode 0..3, `+0x38` entity,
  `+0x3C/40/44` x/y/z (16.16), `+0x48/4C/50` offset to entity, `+0x54/58/5C` forces (16.16 per tick), `+0x60` depth offset, `+0x64` depth key, `+0x68` status,
  `+0x6C/6D` target/current bank, `+0x6E/6F` target/current sprite, `+0x70/71` target/current anim, `+0x74` frame ptr, `+0x78` first frame ptr, `+0x7C`
  delay counter, `+0x7D` destroy flag. InitEffect sets *current = bitwise complement of target* (sprite, anim) and `current bank = target bank ^ 1`, so the first
  update always loads the sprite and starts frame 0.
- **Per-tick order** [B] (0x8002E0F8-0x8002E110): reset the sprite list, `RunMapEvents` 0x8003C67C, `UpdateEntities` 0x8003B388 (hero, entity scripts,
  `UpdateActiveEffects` 0x80038E84 and `UpdateBalanceRecords` 0x80039300 create effects here), then **`UpdateEffects` 0x8003C410**; the frame is drawn after.
  So a script or an AI that creates or changes an effect sees it animated and drawn in the SAME tick. The sprite list holds 192 pointers (0x8011CB60 to
  the count at 0x8011CE60) = 64 entities + 128 effects exactly [B].
- `UpdateEffects` [B][R]: for each slot with status 2: if `(0x800DC4B8 & 0x48) == 0` (= the DLL's `GameplayBlockedMask`, `AlundraGameState.cs:90`):
  destroy flag set -> status 0 and **skip (not drawn)**; else animate then move. **Always** (blocked or not): copy x/y/z and the depth key into the ref and push
  `slot+0x0C`. So blocked = frozen but drawn, pending destroys wait.
- Animation 0x8003BBDC [B][R]: frame = 3 bytes `[0x80 | delay(7 bits), u16 image-set byte offset]`, terminator 1 byte (`0` destroy, `1` loop; anything else = debug
  print + BIOS 0x800831E0, never in the corpus). `--counter` (byte) every tick; reaching 0 processes the next frame. Consequences, **pinned by 363/363 real
  animations** [R]: a frame of delay d is shown d ticks (first update = tick 0); a **destroy animation is drawn on ticks 0..S (S = sum of delays) and freed at
  tick S+1** (130/130 END animations: `freed = ticks_one_pass + 1`); delay 0 would show 256 ticks (no frame has it). A loop re-processes the first frame (a
  1-frame loop keeps its images). Image set = `[bias byte][count byte][count x 14-byte images]`, image-set pointer `0xFFFF` = no image (none in the corpus).
  `0x92` setting the SAME anim as the current one does not restart it (compare `+0x70` with `+0x71`) [R, mutant caught]. A sprite lookup that fails (index >= table
  count at descriptor `+0x20`, entry 0 or -1) sets the destroy flag with no images.
- Position 0x8003C284 [B][R]: mode 0: `x += fx; z += fz; y += fy`, `depth = (y & 0xFFFF0000) + (z >> 16, arithmetic) + (bias << 16)`. Mode 1 (attached): entity
  status 0 -> mode 2; else `pos = entity pos + offset`, `depth = entity+0x1BC + doff`, status 4 -> mode 2; **then falls into mode 3**: `pos += forces`, status test again.
  Mode 2: nothing. Mode 3 (detached): `pos += forces`, `depth = entity+0x1BC + doff` while the entity lives, status 0 or 4 -> mode 2. No collision, no tile query, no
  sound anywhere. An attached effect outlives its entity (frozen, still animating).
- Map load [B][R] (0x8002DFE4 -> 0x8003C1A4, order: CLUT upload, texture upload, entity init 0x8003B24C, map events init 0x8003C510, **effects 0x8003C1A4**): status of all 128
  slots -> 0, then `SpawnMapEffect(i, 0)` for i = 0.. while the record's first u32 (X1 Y1 X2 Y2) is non-zero. The record count at descriptor `+0x24` is the same rule (first zero
  word, max 0x80) [B 0x8002D8F4-0x8002D970]; corpus: no record with a zero first word (0 of 544), so the analyser's count equals the binary's [D].
  `SpawnMapEffect(index, force)`: index >= count -> 0; **only when force == 0**: hero tile (entity 0 `+0x120/+0x124`, 0x80127D30) inside the INCLUSIVE zone, then the record
  must have flag 0x40; force != 0 skips both. Position `x = (X*12+12) << 16`, `y = (Y*8+8) << 16`, `z = Z << 19`; bank = `flags & 0x80` (map table, else global),
  sprite = byte 5, anim = byte 9. [The decompilation tests the zone the other way round and walks the event records: the binary wins, as the plan already says.]
  Hero tile at load: the tile fields are written by 0x80038064, called from the entity creation function 0x80039D04 (so the hero's start tile is known before
  0x8003C1A4) [B]; that the hero entity is created before 0x8003C1A4 inside 0x8003B24C is **[H]**. Only 6 off-chain records depend on it (zones `(27,0)-(51,27)` x3 on 109,
  `(37,40)-(51,59)` on 78, `(2,40)-(23,59)` on 424, `(13,7)-(33,33)` on 445); 537 of 544 have the whole zone (0,0)-(51,59).
- Effect banks [B]: map bank = descriptor 0x80126E70, texture-page base 0, CLUT base 0x20; global = descriptor 0x80126EC0, page base 0xB, CLUT base 0x60.
  Effect sprite table at sprite-info header `+0x10` (u32 offsets, <= 0x100, ends on 0, -1 = hole); map effect records at header `+0x04` (12 bytes, <= 0x80).
  Map textures: up to 5 pages at VRAM (0x140 + 0x40k, 0); effect quads of the corpus only use page indices 0..2 (map) and 11, 13..17 (global); CLUT indices 32..44 (map) and
  96..111 (global) [R, `validate_emit_out.txt`].

## 2. Draw path [B][R]

- Effects push `slot+0x0C` into the SAME list as entities (0x8003C4B8); `0x8002E130` sorts it by depth key (quicksort 0x8002DA04, descending) and calls **`0x8002DB48`
  (single caller 0x8002E1D4)** once per element; the image emitter is shared with entities.
- Screen anchor `x = (x16 >> 16) - camX`, `y = ((y16 - z16) >> 16) - camY` (arithmetic shifts): the `+1` that `0x93`/`0xA2` add to z makes `(y - z) >> 16` one row higher
  (z = 48<<16|1 with y = 160<<16 gives row 111, not 112). Culling on the anchor only, x in [-128, 448], y in [-128, 368]: invisible by construction (a quad reaches at most 128
  px from its anchor), no need to port.
- **Per image (14 bytes) one POLY_FT4**: byte 0 flags (bits 0-2 page offset, bit 3 semi, bits 4-5 ABR), byte 1 CLUT offset, bytes 2-5 u, v, w, h, bytes 6-13 four signed-byte
  vertices TL, TR, BL, BR relative to the anchor; uv = (u,v), (u+w,v), (u,v+h), (u+w,v+h); raw texture (no modulation). **[R] 20 315 / 20 315 effect quad references give exactly
  these vertices/uv/semi bit/tpage/clut** (tpage = table 0x800DC4F0 [abr][pageBase + (flags & 7)], clut = table 0x800CA0F0 [clutBase + byte 1]; tables rebuilt with the standard
  GetTPage/GetClut formulas, 0x8002C088-0x8002C144).
- **OT slot = `16 * min(depth >> 20 (unsigned), 59) + 6`** [R, 10 keys incl. 0x80000000+ -> 950; same for opaque and semi]. Mode-0 depth >> 20 = `(Y_px + bias) >> 4`: z (at most 2 040 px) is added to the low 16 bits and never carries into the bucket.
  Examples [R]: 391 piece with bias 69 at y 696 -> OT 758; 476 aura at (960,160,48) bias 52 -> OT 214 (depth 13 893 680 = 0x00D40030). Inside one OT slot images are inserted at the HEAD, the
  list being sorted descending: **larger depth key drawn last (on top); image 0 of a set is drawn last (frontmost)** [from the G2b discovery, not re-run here; the sort is an unstable quicksort: equal keys have no defined order].
- The DLL's entity key (`WallPlacementOverlay.ComputeEntityElevation`, `:393-403`) reproduces the row bucket (`rowIndex*16 + 6`, clamp 59) but NOT the fine order (its own doc: fine order comes from -worldY), so
  effect-vs-entity ties inside one slot will be approximate whatever G3 does; an effect's own key can be exact (full 32-bit key known per slot per tick).
- Data on blending [D]: 20 315 quad references, semi 15 768 (**77.6 %**: ABR1 additive 14 945, ABR2 subtractive 452, ABR0 average 371, no ABR3). STP is per texel in the cell (G0 alpha code 255/128/0).
- Deformed share [D] (classes of the 4 corners vs the source size, the 46 all-zero quads of map 161 excluded): 20 269 references = rect_src 7 453, rect_scaled 8 680, parallelogram 3 170, general 966 -> **63.2 % deformed**
  (the first count of 20 315 includes the 46 zero quads as rect_src). **But the effects the chain uses by map record are almost all rect_src**: 391 (6 refs, all), 163 (6/6), Inoa smoke and doors 162/169/176/183/478 (4/4 each), 10 (125 + 4 + 4, all),
  135 altar (3) and its map effect 1 (88, all). The heavy cases are the **476 aura (1 185 refs: 193 rect_src, 464 scaled, 412 parallelogram, 116 general; 944 semi)** and the **global effects**: 3 (145 of 184 deformed), 8 (32 of 37 scaled; Nadia's
  three bursts on 174/181), 12 (7 of 9), 13 (27 of 29; the `0xA3 [1]` of 135), 14 (156 of 204), while global 24 (the 35 ambient flames) is 17/17 rect_src.
- 103 of the 1437 effect sprites (**1684 refs, 8.3 %**) are fully transparent cells (blank texture region or a palette whose used entries are 0x0000: e.g. 476 palette 1 and the 64x24 region
  (48,80) of page 0 all index 0, entry 0 = 0x0000 in every palette) [D]: invisible on the PSX too (texel word 0 is never drawn); 181 of them are in the 476 aura. Keep them in the export (faithful), the engine may skip.

## 3. The nine opcodes [B][R]

Handlers (table 0x80098FAC), `a2` = pointer to the code pointer, they return their size and **never suspend**. [R] on 200 random calls on 5 maps: the returned sizes are
0x90:2, 0x91:2, 0x92:3, 0x93:8, 0x94:8, 0xA0:8, 0xA1:9, 0xA2:8, 0xA3:9 (= `EventOpcodeSizeTable.cs:187-191, 203-206`), and **the only RAM bytes they change are in the pool** (plus the
entity-search result 0x8013D8D8, written by 0x8003C954 for A1/A3): no `Result`, no wait key, no flag.

| op | operands | semantics (all on **every active slot of that record index** `+0x30 == b1`, including one in its destroy tick; a record index >= count creates nothing) |
|---|---|---|
| 0x90 | b1 | `SpawnMapEffect(b1, force 1)`: record position/anim, no zone test, no 0x40 test; never checks for an existing slot of the same record |
| 0x91 | b1 | status 0 immediately, on every slot with status != 0 of that record |
| 0x92 | b1, anim | `ta = anim` (byte); the switch happens at the next update of the same tick (effects update after scripts) |
| 0x93 | b1, u16 x, u16 y, u16 z | `x = x<<16, y = y<<16, z = (z<<16) + 1` (32-bit wrap: operands >= 0x8000 are negative) |
| 0x94 | b1, s16 fx, fy, fz | forces `= s16 << 8` (16.16 per tick) |
| 0xA0 | b1, u16 dx, dy, dz | `pos += d<<16` (wraps: 0xFFB8 = -72 px), no +1 |
| 0xA1 | b1, key, u16 dx, dy, dz | `0x8003C954(self, key)` first match of the search buffer; none -> nothing; else `pos = entity pos + d<<16` on every slot of b1, no +1 |
| 0xA2 | b1, u16 x, y, z | spawn(b1, 1); if created: `x<<16, y<<16, (z<<16)+1`; the anim stays the record's |
| 0xA3 | b1, key, u16 dx, dy, dz | search first (none -> nothing, no spawn), then spawn(b1, 1) and `pos = first match + d<<16` (no +1) |

No handler returns a value to the script, none writes a handle: a script names an effect by its record index; effects made by native code (`+0x30 = -1`) are unreachable from scripts.

Corpus [D] (`data/ops_scan.py`, linear sweep per program with the DLL's size table, **cross-checked: it finds all 99 EFFET lines of `Data/story-chain-skipped-opcodes.tsv`**): **135 maps
use the opcodes (10 chain, 125 off-chain), 1 435 sites**: 0x90 108, 0x91 30, 0x92 344, 0x93 17, 0x94 19, 0xA0 18, 0xA1 6, 0xA2 455, 0xA3 438. Two sites of 476 (pc 504 `0x93`, pc 512 `0x91`)
are not in the TSV (the audit walk does not reach them: [H] code after the scene's last exit). All operands are valid [D]: 0 record index >= count, **344 / 344 `0x92` animations are real
animations of their record's effect** (no phantom slot, no out-of-range anim: the model raises on one, the binary would read garbage). The 99 TSV lines by tier: S 34, A 41, O 8, X 16.
Spawn opcodes create through the map table 426 times and the global table 575 times (global effect 14: 401 sites, 13: 49, 22: 42, 23: 17, 26: 14, 15: 8, 11: 10, 8: 9, ...).

## 4. Effects spawned at load [B][R][D]

251 records have flag 0x40 (**251 on 82 maps**, not 157), 215 on the map table, 36 on the global table (35 of them global effect 24, 1 global effect 14); **246 loop forever, only 5 end**: map 13 record 4
(anim 4, 24 ticks) and the four rays of map 163 (anim 1, 10 ticks -> drawn on ticks 0..10, **freed at tick 11 = D-E19-55's "11 ticks"**) [R, `scenarios_out.txt` S3]. At most 9 ambient slots on one
map (271, 289, 357), 15 quads at most on one frame of any map (worst case sum); the pool is never near 128 at load. Chain maps with ambient effects: 478 (5 smoke), 162/169/176/183 (6-7 smoke: 6 records `0xC0`, record 5 is the door overlay
0x80 on 169/176/183 and 0xC0 on 162), 391 (5 pieces), 163 (4 rays), 10 (1: record 4), 135 (1: the altar, record 0, flags 0xC0), none on 476, 174, 181, 165, 172, 179.

## 5. Native effects and D-E19-54 [B][R]

**Finding (changes D-E19-54).** The "warp effects" of the plan (`AnimateWarpEffect`, sites 0x80034ACC-0x80034EC4, the five functions below) are **not map-warp effects**. `g_warpLockTimer`
(0x80127164) is written only at 0x80034A90 by the item-use function 0x8003499C (the decompilation's `UseItem`) with the **item id**, and zeroed at 0x80032964, 0x800351A4, 0x800364A8; `AnimateWarpEffect`
(0x800350C0, jump table 0x800233EC read in the binary, 20 entries for ids 0x1F..0x32) switches on that id: 0x1F -> 0x80034ACC (carried entity of sprite-table index 1 destroyed, effect sprite 8), 0x23 -> 0x80034B54 (carried index 2, effect 0xC), 0x2C -> 0x80034BDC (no effect),
0x2D/0x2E -> 0x80034C54 (entities of sprite index 0x13 replaced by effect 0x1A), 0x2F -> 0x80034D2C (index 0x14: effect 0x13 anim 2 + destroy), 0x30 -> 0x80034E08 (no effect), 0x32 -> 0x80034EC4
(16 random effects of sprite 0x12 per entity of index 0x18). They belong to bombs and magics (item-use system, **E14**), triggered by things the DLL does not have. **No effect is created by a map
transition or a door**: the binary's map change clears the pool and the transition loop neither updates nor draws effects (SD5).

**The "pickup" family** = the HP/MP functions 0x80032E50-0x80033A2C (14 sites, global sprite 0xE except where noted), names from the decompilation (`PlayerManager.cs`): IncreaseHpAndCreateEffect 0x80032E50, AddLowHp 0x80032EEC,
AddMediumHp 0x80032F84, AddHugeHp 0x800330FC, RestoreHp 0x80033274, **IncreaseHpMax 0x800333AC**, IncreaseMp 0x800335AC, RestoreMp 0x800336F0, IncreaseMpMax 0x8003382C, RestoreHpAndMp 0x80033A2C. They are reached by
the item pickup/consumption dispatcher `FUN_80033dbc` (item ids 0x45-0x56) and by **`0x82` sub-cases 0x50-0x56** through `HandleMapTriggerCommand` 0x80034108. In the corpus [D] (`data/ops82.py`) only **sub-case 0x53 (life vessel) is
used by scripts: 8 sites on 5 maps (134, 258, 298, 302, 398), none on the chain** (O-E19-39); the other pickup effects need drops/consumables (E14). Today the DLL degrades `0x82` 0x50-0x56 (`AlundraEventProgramRunner.cs:2854`).

**`0x82 0x53` = `IncreaseHpMaxAndCreateEffect` 0x800333AC, validated [R] on 60 random cases** (hero position from the player entity 0x80127D30 `+0x114/+0x118/+0x11C`, **not** the entity argument): HpMax + 1, Hp = HpMax, sound 0x31, then
8 effects (global sprite 14, anim 0, `CreateEffectEntity` mode 0, z + 0x100000 = 16 px) displaced on an ellipse `x += offX[j*4] << 13, y += offY[j*4] << 13` with forces `-(offX << 9), -(offY << 9)`
(`offX` = halfwords at 0x80023654 + 8j = [0, -543, -768, -543, 0, 543, 768, 543], `offY` at 0x80023694 + 8j = [512, 362, 0, -362, -512, -362, 0, 362]: a 96 x 64 px ellipse), then 4 sparks with forces from the
shared PRNG (seed word 0x80098708, `seed' = seed * 0x7D2B89DD + 0xE06A02E7`; per spark **only if the creation succeeded**: `r1 = lcg(seed); r2 = lcg(r1); fx = ((r1 * 0x20001) >> 32) - 0x10000;
fy = ((r2 * 0x30001) >> 32) - 0x18000; fz = 0x40000; seed = r2`). The DLL has `AlundraRandom` with the same constants and seed (`AlundraEventProgramRunner.cs:1420-1424`), so the draw order is reproducible.
`model_53` in `binary/native53.py` is the oracle (12 slots; with 120 slots already used only 8 are created and the PRNG advances 0 times for the 4 failed sparks).

**Census of the 80 native sites** (84 - 3 opcodes - 1 load; function names from the decompilation, `binary/names.json`): player 30 (PlayerTryAttack 0x8002EAF4 sparks, UpdatePlayerWeaponEffect 0x8002F49C, CreatePlayerAnimationEffects 0x8002FB1C x6,
MovePlayer 0x80031B50 sprite 9, the 14 pickup sites, the 5 item-use sites, 0x80032D74 dead), common engine 10 (UpdateActiveEffects 0x80038E84 x4, 0x80039188, UpdateBalanceRecords 0x80039300 x2 attached, DestroyEntity 0x8003A59C attached
break effect, 0x8003A648, CheckAndTriggerTileEffect 0x8003A7B0), native AI C 33 + E 6 + boss helper 1, **none of them used by a sprite record of the chain maps** (earlier census) except what the player and the common engine do on every map.
Whatever is not scripted is visible on the chain only through player movement/attack/item code the DLL does not have.

**Recommendation for the scope question:** G3 should build the native creator API (three creators + forces/offset fields, the pool is the only thing they need) and port only what a DLL surface reaches today: **`0x82 0x53`**
(and the six other `0x82` sub-cases once their consumers exist); the rest follows its consumer (E14 combat/items, the movement code for dust/splash). See question Q-1.

## 6. Census of the extraction [D] (`data/census*.py`, `fx_all.json`)

- **544 map effect records on 157 maps** (flags 0x00 158, 0x40 36, 0x80 135, 0xC0 215; other flag bits 0; bytes 10-11 all 0): **251 auto (0x40)**, **350 map table / 194 global table, 0 unresolved**; zones: 537 whole, 7 partial (above).
  Max 13 records on a map (6, 357); 86 maps carry both a table and records, 71 only records (global table), 0 table without records; **all 136 map-table effects are used by a record of their own map**; 17 of the 29 global effects are used by
  records (the 12 others - 0, 1, 2, 4, 5, 7, 10, 17, 18, 21, 25, 27 - only by native code).
- **Tables: 136 map effects on 86 maps + 29 global = 165** (`NumSpriteEffects == len(SpriteEffectRecords)` on all). **Animations: 363 real (313 map + 50 global; 130 END, 233 LOOP) + 83 padding slots** (offset 0 in `AnimationOffsets`: the analyser's count = first offset / 2
  includes them; all one frame, never named by a record or a script). Display frames 5 148 (all with an image set, none with `0xFFFF`), delays never 0. LOOP periods 2..96 ticks (median 10), END lifetimes freed at tick 3..231 (median 51), max 25 quads on one frame.
- Unique image sets (by pointer, per effect) 2 832; **quad references 20 315** (map 18 226, global 2 089), semi 15 768, classes above; the 46 zero-size references of map 161 are all-zero quads in animation 3 of effect 0 (unused by the record, anim 0): ignore.
- **Sprites for G1 [D]**: 1 435 cells (page, palette, source region) = G0's 1 435; **1 437 `.sprite` if one per (sheet, signature)** (2 cells are used with two modes: alundra sheet (0,179,24x24) None + Mode1 and (0,0,112x80) None + Mode1); modes: None 545, Mode1 832, Mode2 31,
  Mode0 29; global 122, maps 1 315, on **87 sheets** (`data/map_<n>_effectsheet.png`, `map_alundra_effectsheet.png`); **every atlas rect is inside its sheet** (1437/1437). **The ids are predictable**: `uuid5(2b7f6b6a-2d63-4e33-9f0e-0f7f2b6a5c11, "sprite:<sheet>:<signature>")` reproduces
  the converter's `Ids.For` (checked on a real exported sprite) - `data/g1_predicted_sprites.tsv` lists the 1 437 (sheet, signature, id, atlas rect, mode, refs).
- Story chain [D]: see `data/chain_fx.py` output; ambient/opcode tables per chain map in section 4 and in the plan's TSV.

## 7. Values a plan can pin (all produced by the real code, model equal)

Pool rules: 128 creations succeed, the 129th returns null and changes nothing; freeing slot k makes the next creation take k (`scenarios_out.txt` S5: 391, 5 + 123 x `0x90 [3]` = 128, one more = 128, `0x91 [4]` -> 127, next `0x90 [3]` takes slot 4).

391 (load, hero tile (26,30)): slots 0..4 = records 0..4 at (996,504,0), (996,600,0), (996,696,0), (996,792,0), (1020,792,0) px, anims 0,1,2,3,3, bank 1; after tick 0: image-set byte offsets 24, 40, 56, 100, 100; bias 0, 0, 69, 11, 11; n 1, 1, 3, 1, 1; delay counter 10;
depth keys 0x01F80000, 0x02580000, 0x02FD0000, 0x03230000, 0x03230000 -> OT 502, 598, 758, 806, 806. `0x94 [r, 0,0, fy,0, 0,0]` with fy 50, 36, 100, 50 (records 0..3, pc 417-441): forces y = 12800, 9216, 25600, 12800; after 100 ticks y grew by 1 280 000, 921 600, 2 560 000, 1 280 000
(19.5312 px for record 0) on every slot of that record, including the load-time copy.

476 aura: not spawned at load (flags 0x80 only). `0xA2 [0, 0x03C0, 0x00A0, 0x0030]` -> slot 0, x 62 914 560, y 10 485 760, **z 3 145 729** (low 16 bits = 1), `+0x70` anim 1 (the record's), `+0x30` 0; `0x92 [0, 0]`; **first update (tick 0)**: anim 0, delay counter 2, image set @552,
bias 52, 1 image, depth 13 893 680, OT 214; the frame changes every 2 ticks (set offsets t0..t5 = 552, 552, 568, 568, 584, 584); **drawn on ticks 0..222, destroy flag at tick 222, freed at tick 223**; `0x92 [0, 1]` applied after <= 222 updates -> the aura lives on in anim 1 (loop, 16 frames x 2 ticks);
applied after 223 updates -> the slot is dead (anim 0, dead flag): the script's `0x37 [220]` wait leaves a 2-tick margin (221, 222 pass).

163: four rays (records 0..3, map effect 0, anim 1: one frame of delay 10 then END): active on ticks 0..10, 0 on ticks >= 11. 135: altar (record 0 at (468,376,64)): `0xA0 [0, dx 0, dy +8, dz 0]` then `[0, dx 0xFFB8, 0, 0]` -> x -4 718 592 (-72 px), y +524 288 (+8 px).
Static upper bounds (every effect opcode of the map once in pc order, branches ignored, `arcs_counts.py`): 162: 7 at load, 18 after the eleven `0xA2` of id 5; 169: 6 -> 19; 176: 6 -> 18; 183: 6 -> 8; 391: 5 -> 10 (2 per record, as the forces then hit both copies); 10: 1 -> 8; 135: 1 -> 3; 174/181: 0 -> 3. The arcs' real values come from their own runs.

Per-animation table: `data/anim_table.tsv` (363 rows: bank, effect, anim, term, frames, ticks_one_pass, last_drawn_tick, freed_tick, max_quads, distinct_sets) = the lifetime values of every effect, all equal between the real code and the model.

## 8. What the plans need

### 8.1 Slices (executable, in dependency order)

1. **G1 - converter export** (no engine, no DLL; depends on G0/G0b, done): `EffectBankReader` (JSON -> records / effects / animations / frames / image sets / quads: drop the 83 padding slots, the 46 zero-size quads; terminator -> End/Loop; counters),
   the writers (per-map and global effect documents; the 87 effect sheets as textures; 1 437 `.sprite` through the existing `EnsureSpriteData` keyed by (sheet, signature), sheet = `map_<n>_effectsheet.png`), invariants, `hero_effects.json` kept until G3 consumes the new data.
2. **G2b - engine, free quads** (already planned, O-E19-71 pending): `DrawQuad` (texture + source rect + 4 corners + mode + sort key) as the SINGLE shared entry (the G2b engine notes already say so). Needed for 63 % of effect refs and the whole 476 aura.
3. **G2e - engine, effect quad service** (new, small, after G2b): a service + thin component (pattern `ScrollingLayerService`) the DLL pushes a per-frame list to: `{sprite id, 4 corners in world px, sort key}`; inert while nothing is pushed. Rect_src quads may use the existing sprite path (`DrawSprite` with sort key and PSX mode) so
   G3 does not have to wait for G2b for the map effects of 391/163/Inoa/10/135 (0 deformed quads in them); the global effects 3, 8, 12, 13, 14 (174/181, the optional/off-chain tiers, 135's `0xA3`) and the 476 aura wait for G2b; deformed quads are counted and reported, never drawn wrongly.
4. **G3a - DLL, pool and tick**: (a) pure model class (pool, bank types, InitEffect, animation, modes 0-3, update, creators) with tests copied from the validated oracle; (b) loading the bank documents; (c) map-load spawn after the hero tile is known and before the first tick; (d) the per-tick pass inside the world proxy's
   tick loop after `RunMapEventsPass` (see 8.4); (e) the per-frame push. Visible result: ambient effects on 82 maps.
5. **G3b - DLL, opcodes**: nine handlers in `AlundraEventProgramRunner.Dispatch` (0xA1/0xA3 through `EntitySearchService`), TSV loses its 99 rows, arcs re-pinned. Independent of the engine (state asserts on the pool).
6. **G3c - native creators and `0x82 0x53`** (optional, see Q-1).
7. **G4 - recipe** (author) after G2b/G2e: 476 first.

### 8.2 Rules to write down

R-pool (128, lowest free, silent drop, id kept, template zero); R-spawn (force 0: inclusive zone with the hero tile, then 0x40; force 1: neither; position `(X*12+12, Y*8+8, Z*8)` px; bank `flags & 0x80`); R-tick (events -> entities -> effects; `GameplayBlockedMask` = 0x48 freezes the update but not the push; during a warp transition no tick);
R-anim (delay d = d ticks; END = drawn S+1 ticks, freed the next; LOOP; same-anim `0x92` does not restart; anim switch in the same tick); R-modes (0 free, 1 attached then falls into 3, 2 frozen, 3 detached); R-opcodes (table of section 3, +1 on z for 0x93/0xA2 only, wrap at 32 bits, no Result, no suspension, record index); R-draw (anchor `(x>>16, (y-z)>>16)`, key
`(y & 0xFFFF0000) + (z>>16) + (bias<<16)`, bucket `16*min(key>>20 unsigned, 59)+6`, larger key and lower image index on top, per-image semi/ABR from the sprite); R-clear (map load clears everything; effects are not saved).

### 8.3 Values writable in advance, and how

- G1 invariants (all from `fx_all.json`, reproducible by `data/*.py`): 544 / 251 (82 maps) / 350 / 194 / 0 unresolved / 157 maps; 136 + 29 = 165 effects on 87 banks; 363 animations (313 + 50; 130 END, 233 LOOP) + 83 padding dropped; 5 148 display frames; 2 832 image sets; 20 315 quad refs of which 46 all-zero (map 161) are dropped = 20 269 exported
  (map 18 180, global 2 089); classes of the 20 269: rect_src 7 453, rect_scaled 8 680, parallelogram 3 170, general 966; semi 15 768 (ABR1 14 945, ABR2 452, ABR0 371); **1 437 `.sprite`** (87 sheets; None 545, Mode1 832, Mode2 31, Mode0 29), 122 global.
  Ids and atlas rects: `data/g1_predicted_sprites.tsv`. The export manifest effect: +87 textures (+ raw/wrapper catalog entries), +1 437 `.sprite`, + one effect document per map (157) + the global one, `AssetInfos.json`, `report.json`; nothing else (hero_effects.json unchanged until retired).
- G3 oracle: `model/fxpool.py` + `binary/*.py` regenerate any state; `anim_table.tsv`; `scenarios_out.txt`; `native53.py` (`model_53`); the fuzz seeds in `fuzz.py` are deterministic (seed = 1000*map + n).
- The 99 TSV lines and the six arc files list the exact `(opcode, pc)` pairs to un-skip (section 8.5).

### 8.4 DLL integration hazards found

- **Update after the scripts, per tick.** The DLL runs the entity scripts BEFORE the world proxy (the engine updates entities first, `AlundraWorldProxy.cs` doc at the top of `Update`, then the tick loop at `:2242` with `RunMapEventsPass`), the binary the other way round. For effects only one thing matters: the pool pass must run **after** every
  script pass of the tick it belongs to. An effect created by an entity script at tick k of a multi-tick frame must not receive the updates of ticks < k: stamp the creation tick (the entity loop knows it) and skip a slot until its tick, or the first frame of such an effect is eaten.
- Gate: `GameplayBlockedMask` OR `AlundraWarpDirector.Instance.IsTransitionInProgress` (the proxy already uses the pair, `:2257`); the push continues when blocked.
- The pool and its clock must belong to the world proxy (one per map, cleared by construction at load), reached by the runner through the host interface (`IAlundraScriptHost` / `IEntityWorldContext`), like the entity search.
- Position units: record/opcode values are PSX pixels, 16.16; the engine is Y-up: use the same projection as entities (`ResolveLogicalPosition`), anchor row `(y - z) >> 16`.
- An animation index outside the record or on a padding slot is garbage in the binary: raise in tests, destroy + warn once in production. A creator on a failed sprite lookup leaves a dead slot (freed next tick), not an error.

### 8.5 Tests and arcs that would move

- `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv`: the **99 EFFET rows leave** (`AlundraStoryChainSkippedOpcodesTests.cs:50`, rule 2, follows the `IsPorted` oracle: 0xA2 46, 0x92 19, 0x90 15, 0xA0 6, 0x93 5, 0x94 5, 0x91 2, 0xA3 1; maps 476, 391, 162, 169, 174, 10, 176, 181, 135, 183). The 3 `PREDICAT coup recu` rows are not concerned.
- Arc files: `AlundraVisionArcTests.cs:31-32` (A2/A4, allowed `{0x4C, 0x92, 0x93, 0xA2}` -> `0x4C` only), `AlundraShipBlockArcTests.cs:34-37` (A6, ten exact sites), `AlundraInoaDayOneArcTests.cs:39-77` (A20, eleven `0xA2` at frame 0), `AlundraEntityContactArcTests.cs:171-174, 290-292` (A10J `0x90` @2689, @2828),
  `AlundraHeroJumpArcTests.cs:57-64` (`0x90` @6406, @2689, @2828), `AlundraDay3SceneArcTests.cs:34-43` (rule 5 on 176 and 135: A13 `:80`, A15 `:170`, A17 `:255`). The hero/intro traces of maps 389/390 are not touched (no effect data on 389 and 390; effects made from map records never draw the shared PRNG, only the native creators do, e.g. `0x82 0x53`).
- Converter: `SpriteWriterTests.cs:146-148` (`hero_effects.json`) and `docs/formats/misc-data.md:97-125`, `docs/demarrage-nouvelle-partie.md:51`, ADR-0030 only when the file is retired; the new reader/writer tests; the full-run invariants gate; the double export; `AssetVerifier` loads 1 437 more sprites.
- Apart from `hero_effects.json` (converter test above), no test of the 2 539 `Alundra.Tests` or the 418 converter tests reads effect data today.

### 8.6 Engine needs (report, never work around)

`DrawQuad` with free corners and PSX split (G2b); the effect quad service (G2e) with an explicit `RenderSortKey2D`; the PSX per-texel semi mode already exists per sprite (`SpriteData.PsxSemiTransparency`, G2a, ADR-0051); no new shader; ABR3 unused by effects (0 quads). Fully transparent cells (1 684 refs) may be skipped by the service.

## 9. Questions (only the genuinely open product decision)

**Q-1 (D-E19-54 rests on a misreading)**: "warp effects" do not exist as map-transition visuals; the five `AnimateWarpEffect` sites are item-use effects (bombs, magic) with E14, and the only "pickup" effect a script can reach today is `0x82 0x53` (8 sites, 5 off-chain maps). Confirm: G3 = native creator API + `0x82 0x53`, everything else with its consumer (E14, movement)?
(Q-G3, Q-G5 stand: ambient effects on 82 maps, rays of 163 gone after 11 ticks.)
Not a question but a sequencing decision for the plan: G3a/G3b can land with rect_src quads before G2b, or wait; the 476 recipe needs G2b either way (84 % of the aura is deformed).

## 10. Not established

- Hero entity creation before 0x8003C1A4 inside 0x8003B24C (only 6 off-chain records depend on the hero tile at load).
- Tick-by-tick equality of sort order for equal depth keys (unstable quicksort 0x8002DA04), prim buffer overflow behaviour (512 prims per buffer, a debug print at 0x8002E1F0 only).
- The meaning of the 2 unreachable 476 sites (pc 504, 512) and the exact scenes behind the native item ids (0x1F, 0x23, 0x2C-0x32 names are the decompilation's).
- PSX GPU rules (STP bit, texel word 0 transparent, ABR formulas) are documentation, as in G0/G2a; the converter-side alpha code already encodes them.
- That a real-time 50 Hz tick maps to the DLL's `FixedTickSeconds = 1/50` exactly (it does by construction: `AlundraScriptedMotion.cs:52`, max 4 ticks per frame).
