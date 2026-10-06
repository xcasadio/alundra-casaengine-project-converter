# G3 (DLL effect reservoir, update, push, opcodes): discovery of 2026-10-06 (read only)

Labels: **[F]** fact (file:line / address), **[B]** proved by running the REAL routines of `ALUN_CD.EXE` (France) in the MIPS interpreter
(`oracle*.py` in this folder; `emu.py` + `lib.py` of the earlier sessions; only the interpreter's RAM is written), **[H]** hypothesis, **[D]** data.
Nothing was built, run, exported, edited, staged or committed inside the repository.

## 0. Proofs on the binary produced today (the "oracle", reusable as test values)

All with the real code: sprite-info loader `0x8002D808` (builds the descriptors `0x80126E70` map / `0x80126EC0` global from the raw `DATAS.BIN` sprite-info
sections), `SpawnMapEffect 0x8003C094`, map-entry init `0x8003C1A4`, `CreateEffectEntity 0x8003BDD8`, `UpdateEffects 0x8003C410` (which calls
`Animate 0x8003BBDC` and `Move 0x8003C284`), the emitter `0x8002DB48` and the opcode handlers `0x800405A8..0x8004071C`, `0x80040D60`, `0x80040F00`.
- `oracle_anims2.py`: **all 363 real animations (233 loop, 130 destroy), every tick**, 20 227 ticks incl. two loop periods: the per-tick image-list pointer
  and IDSV pushed by the binary equal the model built from the JSON of `data-extracted` (frame shown `Delay & 0x7F` ticks, terminator 0 destroy / 1 loop,
  image set at `record + ImageSetPointer + 2`), and a destroy animation is **pushed on `life + 1` ticks and its slot freed on tick `life + 2`**
  (130 / 130 identical: destroy flag set at tick `life+1`, status 0 at `life+2`, never pushed after). 0 mismatch.
- `oracle_load.py`: map entry `0x8003C1A4` on the **157 maps**, hero tile (0,0) and (51,59): the binary's own loader counts **544 records = JSON**
  (no early stop on a zero first word); slots = records with `flags & 0x40` and the hero tile inside the inclusive zone, in record order, in slots
  0..n-1; position `x=(X*12+12)<<16`, `y=(Y*8+8)<<16`, `z=Z<<19`, bank = `flags & 0x80`, sprite = EffectId, anim = AnimId, `+0x30` = record index. 0
  mismatch. 245 slots at hero (0,0) = 251 - the **6 zone-restricted records** (map 78 rec 2 zone (37,40,51,59); map 109 recs 4,5,6 zone (27,0,51,27);
  map 424 rec 0 zone (2,40,23,59); map 445 rec 0 zone (13,7,33,33)), all off the chain. **79 maps** have at least one load-time effect; at most 9 slots / 15
  quads per frame on any map (maps 271, 289, 357), so the draw budget is trivial. File: `load_oracle.json` (per-map slot lists).
- `oracle_emit.py`: the REAL emitter on **16 610 effect quad references** (every distinct image set of every animation, with a fractional position and z): vertices
  = `(x>>16) - camX + sbyte(Xi)`, `((y - z)>>16) - camY + sbyte(Yi)` (the `+1` of `0x93`/`0xA2` therefore moves the row), uv = (Sx,Sy),(Sx+W,Sy),(Sx,Sy+H),
  (Sx+W,Sy+H), semi bit = `Spritesheet` bit 3, tpage row = ABR (semi) else 0 and page = base + (`Spritesheet` & 7) (base 0 map / 0xB global), CLUT = base + `Palette`
  (0x20 map / 0x60 global), OT slot `16*min(depth>>20,59)+6`. 0 mismatch (so the JSON corner order is the vertex order, TL,TR,BL,BR).
- `oracle_ops.py`: golden sequences with the real handlers (values in section 4).
- Interpreter caveat: a `jal` into libc/BIOS is not stubbed in these runs; none of the above reaches one (the animation-error path would, never taken on real data).

## 1. Where the reservoir lives, how it is ticked, how it pushes

**[F] Frame structure of the DLL** (`AlundraWorldProxy.Update`, `Scripts/AlundraWorldProxy.cs:2102-2460`; `ticksThisFrame` `:2129`): entities' own `Update` run BEFORE the proxy (engine order, documented
`:2108-2116`); then, per RENDERED frame, with `ticksThisFrame = LogicTicksThisFrame(elapsedTime)` (`AlundraLogicClock`, `:621`): pad/inventory/save loop (`:2171`), the
dialogue-box + map-events loop per tick (`:2242-2275`, gate re-read per tick at `:2257-2259` via `GameplayBlockedMask` | `AlundraWarpDirector.Instance.IsTransitionInProgress`, D-T-6), the
pending-event triggers (`:2314-2317`), the camera block (`:2352-2360`), the backdrop push (`_backdropStage.PushFrame(IsTransitionInProgress ? 0 : ticksThisFrame, camera)`,
`:2376-2378`), the fade (`AlundraScreenFadeDirector.Advance/PushToAttachedService`, `:2387-2388`), the HUD per tick (`:2410`), the gameplay freeze (`:2439`), `_logicClock.CloseFrame()` (`:2449`).
`GameplayBlockedMask = MenuOpen | Unused40 = 0x48` (`AlundraGameState.cs:90`) = the binary's `& 0x48` (`0x8003C458`).
**[B/F] Binary order** (`0x8002E058`): map events (`0x8003C67C`) -> entities (`0x8003B388`) -> `UpdateEffects` (`0x8003C410`); then the frame is drawn. So an effect created by
a script or an entity this tick is animated and drawn the same tick.

**Proposal.** A per-world object `AlundraEffectStage` (name indicative), owned as a FIELD INITIALIZER of `AlundraWorldProxy` exactly like `_backdropStage` (`:583`, trap 9 of the S3
plan: `Clone()` returns a bare `new AlundraWorldProxy()`), containing:
- `AlundraEffectBank` (data: loaded once per world from the G1 companion + once per game from the global file; loader modelled on `BackdropLoader`: `Maps/world-index.json` lookup by the
  trailing `-{id}` of `World.Name`, degraded mode = one warning and no effects);
- `AlundraEffectReservoir` (pure logic, no engine type, headless-testable): 128 slots, the binary's creators, `Tick(blockedGate)`, the nine operations;
- `PushFrame(...)` building the per-frame quad list for the engine service (section 3).
Wiring: attach the engine service at `InitializeWithWorld` next to `_backdropStage.AttachService` (`:674-687`), load + map-entry spawn right after the tile-map/hero adoption (see "hero tile"
below); one `Tick` per logic tick in a loop placed after the map-events loop and before the camera block (the loop `:2242-2275` stops at `continue` when `PlayerEntity == null || tickBlocked`, `:2259`, so the
effect pass cannot live in it; it needs its own `for tick` loop reading the gate per tick), and `PushFrame` once per frame right after the backdrop push (`:2376`), outside the gameplay gate
("frozen but still drawn", `0x8003C490`).
- **Gating rule** [B]: per tick, if `(PlayerControlFlags & 0x48) != 0` -> no destroy, no animate, no move, but the slot is still pushed; else: destroy flag set -> status 0 and NOT pushed; else
  animate then move then push. While `IsTransitionInProgress` (departure) the DLL gives backdrops 0 ticks; effects the same (the binary's transition loop does not call the tick).
- **Update/draw order = slot order** (the loop is `i = 0..127`): push order follows slot index; equal-key ties in the binary go through an unstable quicksort, so use the slot index as the
  stable id (document as a deviation, nothing observable on real data).
- **Hero tile at map entry (only for the 6 zone-restricted records)** [F]: `0x8002DFE4` (called at `0x8002C3AC`) runs AFTER `0x80044520` and `0x8002CD54` in the map-load sequence
  (`0x8002C2E8..0x8002C3E0`); the zone test reads the hero entity's TileX/TileY (`0x80127D30 + 0x120/0x124`). In the arcs the hero is placed AFTER `InitializeWithWorld`
  (`AlundraArcSupport.cs:207-209` then `:237-250`), irrelevant on the chain (all 41 chain load-time records have the whole zone (0,0)-(51,59)); the plan must name which tile the DLL reads (the
  adopted pawn's `TileX/TileY` at the point it spawns) and test the 6 off-chain records with a synthetic fixture. Not established whether the binary's hero tile at that instant is the portal arrival tile.

## 2. The reservoir rules (binary), as the plan can write them

**[F] pool**: 128 slots x 0x80 bytes at `0x80138608`; status +0x68 0 free / 2 active (no other value is written); `GetFreeEffect 0x8003B9C4` = lowest free index, 0 when full (no break, no log);
`InitEffect 0x8003BB14` copies the 0x80-byte template at `0x8013C608` (BSS, zero in the interpreter) over the slot keeping +0, sets record ptr/index (`-1` when not from a record), mode, status 2, target
sprite/anim, and "current = complement of target" so the first update always loads sprite and frame 0. **[B]** the 129th creation returns null and changes nothing; freeing slot 5 then creating takes 5.
**Fields the DLL needs per slot**: slot index, record index (+0x30, -1 if none), mode (+0x34: 0 free, 1 attached, 2 frozen, 3 detached), entity (modes 1/3), x/y/z 16.16 (+0x3C/40/44),
offsets (+0x48/4C/50), forces (+0x54/58/5C), depth offset (+0x60), depth key (+0x64), status, target/current bank flag (+0x6C/6D), target/current sprite (+0x6E/6F), target/current anim (+0x70/71),
current frame pointer / first frame pointer (+0x74/78), ticks left (+0x7C, a byte: `0` shows 256 ticks), destroy flag (+0x7D), IDSV (+0x20), image count (+0x24).
**Animate** (`0x8003BBDC`): reload the table entry when target bank/sprite differ from current (lookup fails or entry 0/-1 -> destroy flag + no images); anim switch (`+0x70 != +0x71`): frame ptr =
`record + u16[record + 2*anim]`, counter 0, destroy flag cleared, first frame processed the same tick; otherwise `--counter` and process the next frame when it reaches 0; terminator 0 = counter 0xFF +
destroy flag, 1 = back to the first frame and continue, other = the binary prints and calls BIOS `0x38` (never reached on real data: the plan decides "log once + destroy" as a defensive port).
**Move** (`0x8003C284`): mode 0 `pos += forces`, depth = `(y & 0xFFFF0000) + (z >> 16) + (IDSV << 16)`; mode 1 (attached) entity status 0 -> mode 2, else `pos = entity pos + offset`, depth =
entity +0x1BC + offset (+0x60), status 4 -> mode 2, then falls into the mode-3 code (`pos += forces`, same status tests); mode 2 nothing; mode 3 `pos += forces` and the depth/status tests. No collision, no
tile query, no sound. **Modes 1 and 3 are only created by native code (5 + 1 sites), never by a map record or an opcode**; the DLL reservoir for G3a/G3b needs mode 0 only (+ mode 2 as the end state).
**Free**: `UpdateEffects` frees a slot whose destroy flag is set at its next active tick; `0x91` frees at once; map load frees all.
**Anchor/projection** [B]: screen x = `(x >> 16) - camX`, screen y = `((y - z) >> 16) - camY` (both arithmetic shifts of the 16.16 values, so `z = (z_px<<16) + 1` of `0x93`/`0xA2` puts a sprite one row above
`y - z` = exact multiples); culling of the ANCHOR only: x in [-128, 448], y in [-128, 368].
**Depth/OT** [B]: key `D` above; OT slot `16*min(D >> 20, 59) + 6`; the DLL's `WallPlacementOverlay.ComputeEntityElevation(posY, idsv)` (`WallPlacementOverlay.cs:393-403`: `((posY + (idsv<<16)) >> 20)` clamped
to 0x3B, `*16 + 6`) gives the same row for mode 0 (the `z >> 16` term is < 16 bits and never reaches bit 20; the low 16 bits of y never carry). 476 aura: y=160, IDSV 52 -> D=13893680 -> row 13 -> slot 214 (matches
`depth=13893680` read from the binary).
**Within-row order** [F/H]: the binary orders by the full key, larger = drawn later; image k of a set is submitted 0..N-1 and each is inserted at the head of its OT slot, so image 0 is drawn LAST (frontmost)
(G2b notes, section 1). The DLL entities use `DepthSortable2DComponent` with `SortCoordinate = round(-worldY*100)` (`DepthSortable2DComponent.cs:105-120`), a documented deviation from the binary's `z>>16`
low bits (`WallPlacementOverlay.cs:405-427`, "SECOND DEVIATION"). **Recommendation**: build the effect key as `RenderSortKey2D(YSortedWorld, SharedSortingLayer = YSortedWorld, 0,
ComputeEntityElevation(y, idsv), sortCoordinate = same projection as an entity at the same logical (x,y,z) (`AlundraEntitySpawnFactory.ResolveLogicalPosition`, `:471`), localSortOffset = N-1-k (image 0 last), stableId =
slot index)`: effects and entities then share the same documented deviation.

## 3. Push to the engine (the AlundraBackdropStage pattern)

**[F] Pattern**: `AlundraBackdropStage` (`Scripts/AlundraBackdropStage.cs`): `AttachService(ScrollingLayerService?)` (`:78`), `Load(world, projectPath)` pushes `Clear()`, `SetConfiguration`, `SetLayers`, `SetTint` once per world (`:181-209`, always clears first so a world with no data clears the previous one), `PushFrame(ticks, camera)` once per frame (`:480-493`); null service = no-op, and a production world (`world.Game != null`) with no service logs one warning. The engine side is a GPU-free `*Service` + a thin
`GameComponent` that resolves textures and submits through `SpriteRendererComponent` from its own `Update` (so the DLL's per-frame push, made from `GameManager.UpdateWorld`, is final): `ScrollingLayerService`/`ScrollingLayerComponent`, `CellularLayerService`/`CellularLayerComponent`
(`Framework/Rendering/CellularLayers/CellularLayerService.cs`, `Framework/Application/Components/CellularLayerComponent.cs:20-190`; registered `CasaEngineGame.cs:403-404`, order `ComponentOrder.cs:30-43`).
**Proposal**: a service with `Clear()`, `SetSheets(Guid globalSheet, Guid mapSheet)`, `SetFrame(IReadOnlyList<EffectQuad> quads)` where an `EffectQuad` = (sheet index, source rect in the sheet (U,V,W,H), four corner offsets (signed bytes, PSX
order TL,TR,BL,BR), integer anchor (ax, ay), PSX semi mode (`SpritePsxSemiTransparency`, existing public enum), `RenderSortKey2D`); the DLL builds the list from the reservoir each frame (a few dozen quads at most: load-time effects peak at 9 slots / 15 quads on any map, the 476 aura is 9-14 quads per frame; the capacity of 10 000 entries in `SpriteRendererComponent` is not a concern). The engine decides nothing about Alundra (no tick state in the engine: the DLL owns the animation machine, D-E19 "choix de conduite").
**[F] DLL conversion to world space**: entities store logical (x,y,z) px and `RenderProjectionComponent` derives the render position through the world's `SimulationSpacePolicy.DeriveRenderPosition` (`TopDownElevation`)
(`RenderProjectionComponent.cs:75-84`); for effects the DLL can pass the binary's own integer anchor `(x>>16, (y-z)>>16)` (PSX screen-space world px, Y down) and let the service flip Y; the rounding of `(y - z) >> 16` must be
done in the DLL (the engine cannot reproduce the borrow). **[H]** whether the engine's integer-PSX-pixel placement (`SnapToPixel`) already matches the camera snapping for entities: not established here.
Textures: asset ids from the companion (G1); the component acquires them like `CellularLayerComponent.LoadTexture`.
**Blocking dependency**: the draw call needs the engine's free four-corner quad (G2b-1). The service/component can be written against it; the DLL push (`PushFrame`) can be implemented and unit-tested against a service double earlier.

## 4. The nine opcodes

**[F] today**: sizes are right (`EventOpcodeSizeTable.cs:187-191, 203-206`: 0x90 2, 0x91 2, 0x92 3, 0x93 8, 0x94 8, 0xA0 8, 0xA1 9, 0xA2 8, 0xA3 9); no `case` in `AlundraEventProgramRunner.Dispatch` (`:566-1865`);
they fall in `default: return UnknownOpcode(...)` (`:1864`, `:2781`: warning once, trace `UnknownSkipped`, advance by size). The proxy has `//public SpriteRef SpriteRef` and `//public SpriteEffect? ActiveEffect;` commented
out (`AlundraEntityScriptProxy.cs:363-365`). `0x82` sub-cases `0x50`-`0x56` are degraded (`LogDegradedOpcodeOnce`, O-E19-39).
**Pattern for a host seam**: `IEntityWorldContext` default-interface members returning null/false (`IEntityWorldContext.cs:121-168`: `MusicPlayer`, `CameraSway`, `SetBackgroundLayerMask(int) => false`), implemented by
`AlundraWorldProxy` (`:534`); the handler degrades (trace `Degraded`, size returned) when the seam is absent. Adding `IAlundraEffectReservoir? EffectReservoir => null` follows it and breaks no test fake.
**Handlers [B]** (operand = bytes after the opcode; `v1` = record index, never a handle; every operation acts on ALL slots whose `+0x30` equals it, status != 0, including one in its destroy tick; no `Result`, no suspension):
| Op | size | effect | oracle values |
|---|---|---|---|
| 0x90 | 2 | `SpawnMapEffect(v1, force=1)`: no zone test, no 0x40 test; index >= record count -> nothing; position/anim from the record | 391 rec 0 at (996,504,0) anim 0 |
| 0x91 | 2 | status = 0 for every slot of the record (freed NOW, not drawn) | two slots of rec 3 freed in one instruction |
| 0x92 | 3 | `+0x70 = v2` (target anim) on every slot; switch happens the same tick (effects update after scripts) | 476: `0x92 [0,0]`, 220 ticks later `0x92 [0,1]`: curanim 1, destroy flag 0 |
| 0x93 | 8 | `x = u16<<16`, `y = u16<<16`, `z = (u16<<16) + 1` | 476 `[0, 960,160,48]` -> raw (62914560, 10485760, 3145729) |
| 0x94 | 8 | forces `+0x54/58/5C = s16 << 8` | 391 `0x94 [0, 0,50,0]` -> fy 12800; after 100 ticks y += 1280000 (19.53 px), `[2, ..100..]` -> fy 25600; `[1,..36..]` -> 9216 |
| 0xA0 | 8 | `x += u16<<16` (two's complement: signed px), y, z likewise, no +1 | 135 `[0, -72,0,0]`: x 468 -> 396; `[0,0,8,0]`: y 376 -> 384 |
| 0xA1 | 9 | search `v2` (first match, none -> nothing); `pos = entity Pos + d<<16` on every slot of `v1`, no +1 | not run (needs the entity search `0x8003C954`); no use on the chain |
| 0xA2 | 8 | `SpawnMapEffect(v1, 1)`; if created: `x=u16<<16`, `y=u16<<16`, `z=(u16<<16)+1`; anim stays the record's | 476 `[0, 960,160,48]` -> slot 0 raw (62914560, 10485760, 3145729), anim 1; 162: 7 load slots + 11 `0xA2` = 18 slots, record 5 has 12 |
| 0xA3 | 9 | search first (none -> nothing), else spawn and `pos = first match Pos + d<<16`, no +1 | 135 uses it (global effect 13) |
`0xA1/0xA3` in the DLL: `EntitySearchService.GetMatchingEntitiesBySearchType(entity, key, SpawnedEntities, PlayerEntity)` as `0x9F` does (`AlundraEventProgramRunner.cs:1465-1470`).
**[D] Operand domain on the chain**: all 99 listed EFFET sites name a record < count and (0x92) a real animation of a real table entry (0 violation, `dll` checks run today). A record index >= count is a no-op in the binary
(`0x90 [1]` on map 476 creates nothing); an animation index past the table would read garbage in the binary (the DLL must define a defensive rule: ignore + warn once).
**Corpus reach** [H, approximate]: a linear sweep over the 483 exported event programs (`opscan3.py`) finds the nine opcode bytes in ~188 maps (1515 byte matches, includes false positives: 363 maps stop on an unknown size);
the exact walkers are the chain audit's (30 maps, 99 sites). The opcodes will start acting everywhere once ported (no exposure risk: effects have no gameplay effect in the binary: no collision, no sound).

## 5. Save/load, warp, world lifetime

**[F]** `AlundraSaveGame` captures flags/stats/map/position (no effect field; `grep Effect` in `AlundraSaveGame.cs`: none); a load = a warp armed by `AlundraSaveGameDirector` (`AlundraWorldProxy.cs:2110-2118`: "a load armed by F9 posts the warp gate at once")
-> a new world -> a new `AlundraWorldProxy.InitializeWithWorld` (`:625`) -> fresh reservoir + load-time spawn. The binary does the same (pool cleared at every map load `0x8003C1A4`, no effect in the memory card).
Consequence: a scripted effect (the 476 aura) does not survive a save/load, exactly as in the original (F9 test saves land on a map start); no serialization to write. Warp departure: the reservoir is frozen (0 ticks)
and still pushed like the backdrops; the new world clears everything (`Clear()` of the service in `Load`, like `AlundraBackdropStage.Load`).
The `AlundraWorldProxy` is created per world; the per-game state (global effect bank) can be a static cache like `AlundraEtcStringTable.EnsureLoaded`.

## 6. Tests and arcs that move (exact list, current checkout)

**Closed list** (`Alundra.Tests/Data/story-chain-skipped-opcodes.tsv`, 102 lines today): **99 EFFET rows** (0xA2 46, 0x92 19, 0x90 15, 0xA0 6, 0x93 5, 0x94 5, 0x91 2, 0xA3 1; maps 476 20, 10 14, 169 13, 176 12,
135 11, 162 11, 391 10, 174 3, 181 3, 183 2) + 3 `PREDICAT coup recu` rows (map 10). `AlundraStoryChainSkippedOpcodesTests` rule 2 (`Rule2_EveryListedLineIsASiteReachedAndSkipped`) goes red as soon as the opcode is ported
(`IsPorted` = probe on the real runner with a bare host: a handler that degrades or needs a host counts as ported, `AlundraStoryChainOpcodeAudit.cs:70-100`): the 99 rows must leave in the same commit as the handlers
(plan convention). Consequences: **9 chain maps lose their last skipped site** (135, 162, 169, 174, 176, 181, 183, 391, 476) -> `AlundraStoryChainOpcodeAudit.MapsWithoutSkippedSite` 19 -> 28 (`:46-49`) and
`AlundraStoryChainSkippedOpcodesTests.cs:83` `Assert.Equal(19, ...)` -> 28; only map 10 keeps 3 lines. The `Pinned_by_arc` column text goes with the rows.
**Arcs that pin the skips** (all re-pinned in G3b; each must assert the pool instead of the skip):
- A2/A4, map 476 (`AlundraVisionArcTests.cs:31-32`): `AllowedSkippedOpcodes = { 0x4C, 0x92, 0x93, 0xA2 }` -> nothing skipped; add: after `B[2] @326` one slot of record 0, raw (62914560, 10485760, 3145729), anim sequence 0, 1, 3, 1, 3, 3, 1, 3, 3, 3, 1, 3 (`operands.txt`); `0x91 [0]` at the end frees it.
  (`0x4C` is ported since E19.f2a: the set already holds a dead entry.)
- A6, map 391 (`AlundraShipBlockArcTests.cs:33-37`): the exact list `0xA2` @228/236/244/252/408 and `0x94` @417/425/433/441/503 -> empty; add: 5 slots at load at (996,504),(996,600),(996,696),(996,792),(1020,792) anims 0,1,2,3,3, the four `0xA2`
  placements and forces (0,50,0), (0,36,0), (0,100,0), (0,50,0), (0,50,0) (oracle numbers in `oracle_ops.py` output).
- A20, map 162 (`AlundraInoaDayOneArcTests.cs:39, 71-77`): "exactly the eleven 0xA2 of B[9] skipped at frame 0" -> executed; pool: 7 load slots + 11 = 18, record 5: 12 slots (positions in `operands.txt`).
- A10J / T-A10v / T-B9, map 10 (`AlundraEntityContactArcTests.cs:171-174`, `:290-292`): the allowed sets lose `(0x90, 2689)`, `(0x90, 2828)`; `AlundraHeroJumpArcTests.cs:57-64` loses `(0x90, 6406)`, `(0x90, 2689)`, `(0x90, 2828)`. Map 10 also has
  1 load-time slot (record 4, flags 0xC0) and `0x92 [0,1]` @2864 rolls the boulder (62 frames then END).
- A13, A15 (map 176) and A17 (map 135) through `AlundraDay3SceneArcTests.cs:34-43` rule 5 (`Rule5UnlistedArcSites`: a skipped instruction seen in the arc must be a listed row): the 135 A-tier rows are the two `0xA0` of `B[5]` @536 and @549 (the other
  135 rows are X tier: `0xA2` @821, `0xA0` @1445/1462/1731/1741, `0xA3` @2490), the 176 rows are unpinned by name (12 rows). The rows leave and the opcodes execute: the rule has nothing left to flag, the arcs stay green; to re-pin: after A17's `0xA0` pair the
  record-0 slot of map 135 (the altar, load-time slot at (468, 376, z 64)) is at x = 396 then y = 384 (oracle values above).
- **A10/A11 (maps 165/164)** named in the plan are NOT in the effect rows: map 165 has one record (flags 0, global effect 14, not a load-time effect) and 164 none; these two arcs are unaffected (the plan list is generous).
- Unit tests: `AlundraEventProgramRunnerTests.UnknownOpcode_KnownSize_SkipsBySize` (`:322-336`) uses `0x93` as its "known size, not implemented" example: with a null seam the new handler degrades and returns 8, so the test
  stays green but its comment/premise is stale (repoint it to another unported opcode, like the 0x4C -> 0x08 precedent in its comment).
- Arcs on the 9 maps with load-time effects (10, 135, 162, 163, 169, 176, 183, 391, 478) get slots they did not have: no assertion moves (nothing reads the pool today); the headless arcs have no engine service, the stage must tolerate that.
- New tests: reservoir rules (oracle values), animation timeline (`anims_oracle.json`: 363 rows of destroy/free ticks; use a generated golden table, not 20 227 literals), load spawn per map (`load_oracle.json`), gating by `0x48`, 129th creation, `0x91` same tick, the +1 on `0x93/0xA2`, depth/row, the push list against a service double.

## 7. Native producers (D-E19-54) are a different size of work

**[F]** the 80 native creation sites sit in player/item/AI code the DLL does not have: `AlundraPlayerManager.cs:206-217, 255-266, 420` ("no effect system yet", `CreatePlayerAnimationEffects`, `AnimateWarpEffect`, `UpdatePlayerWeaponEffect`,
`UpdateItemEffectState` not ported). In the decompilation (`PlayerManager.cs`): `AnimateWarpEffect` (`:3983`) is a dispatch on `g_warpLockTimer`, which `TryUseItem` sets to the ITEM ID (`:1994`): it is the item-use visual
(timer values 0x1f, 0x23, 0x2c-0x2e, 0x2f, 0x30 Zorgia transition, 0x32 sprite-0x18 explosion), NOT the map-transition warp fade; the "pickup" ones are `IncreaseHp/Mp(Max)AndCreateEffect`, `RestoreHp/MpAndCreateEffect`, `AddLow/Medium/HugeHpAndSpawnEffect`
(14 sites 0x80032E50-0x80033A2C), which draw on `AlundraRandom` ordering, `g_offsetXList` tables and the HP system (O-E19-39: where the hero's life lives is not established). **[H]** the decision "warp and pickup natives with E19.g" can only be executed
as: (1) the creator API on the reservoir (`CreateEffect(bank, sprite, anim, x, y, z)` mode 0, attached/detached modes for later), (2) the scripted `0x82` sub-cases `0x53`/`0x54` (8 `0x53` sites, none on the chain), after O-E19-39, and
(3) the rest wired when the item/HP layer exists (E14/E16 territory). The decompilation is not ground truth for these (rule: the binary wins).
