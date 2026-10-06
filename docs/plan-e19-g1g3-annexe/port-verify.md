# Counter-check of the "port" report (E19.g G1 effects export + G3 DLL reservoir) - 2026-10-06, read only

Labels: [B] re-run on the REAL binary (ALUN_CD.EXE France) in the MIPS interpreter (`emu.py` is the only shared helper; every probe below is rewritten
in this folder), [D] recomputed from `data-extracted/` + effect sheets, [F] re-read in the repository, [H] hypothesis.
Nothing was built, run, exported, edited, staged or committed inside the repository. Repository HEAD at verification time: `731dcab`
(the report was written against `eeed643`/`f4af632`: see section 3).

## 0. What I wrote here (independent of the report's scripts and outputs)
| File | What it proves |
|---|---|
| `mdis.py`, `xref.py` | own disassembler wrapper and address cross-reference (call sites, data refs) |
| `ora.py` | own oracle harness: real sprite-info loader `0x8002D808` on the raw `DATAS.BIN` blobs, real `0x8003C1A4`, `0x8003C410`, `0x8003BDD8`, `0x8003C094` |
| `t_load_all.py` | map entry on all 483 map files, 3 hero tiles each, versus a JSON model |
| `census*.py`, `sheets.py`, `cells.py` | counts of the export (tables, anims, frames, sets, images, deformation, mirror, semi/ABR, STP classes, annex cells) |
| `timeline.py` | every tick of every real animation (image bytes + IDSV + destroy/free ticks) versus the JSON model |
| `emit2.py` | the real emitter `0x8002DB48` on every distinct image set (vertices with fractional x/y/z, uv, semi code bit, tpage row/page, CLUT, OT slot) |
| `ops.py`, `ops2.py`, `golden.py` | the nine real opcode handlers (sizes, golden values on 476, 391, 135, 162, 129th creation, `0x48` gate) |
| `peak.py` | peak slots / quads per frame of load-time effects |
| `ref476.py`, `ref476_redux.py` | own PSX-rule rasteriser re-render of the 476 aura, then the PCSX-Redux rule of the G2b annex |

## 1. Verdicts per claim

### 1.1 Binary and data (all re-derived from scratch)
| # | Claim | Verdict | Evidence |
|---|---|---|---|
| B1 | 544 records, 251 with `0x40`, 350 map-table / 194 global-table | CONFIRMED | [D] `t_load_all.py`: 544; flags in {0,64,128,192}; `&0x80` 350 |
| B2 | spawn at load iff `flags & 0x40` and hero tile inside the inclusive zone; slots in record order from 0; position `x=(X*12+12)<<16`, `y=(Y*8+8)<<16`, `z=Z<<19`; bank = `flags&0x80` (set = MAP descriptor `0x80126E70`, clear = global `0x80126EC0`, `0x8003B9FC`); `+0x30` = record index | CONFIRMED | [B] 483 map files x hero tiles (0,0), (51,59), (20,30): 0 mismatch (slots, order, positions, bank, sprite, anim). All 483 maps are 52x60, so the (0,0,51,59) zone covers every tile |
| B3 | 245 slots at hero (0,0); 6 zone-restricted records (78/2, 109/4-6, 424/0, 445/0), all off the chain; 79 maps with load-time effects | CONFIRMED | [B] 245 at (0,0), 246 at (51,59) (the 78 record), 79 maps; the 6 records and zones reproduced |
| B4 | the binary's loader does not stop on a zero first word (544 = JSON) | CONFIRMED | no record has a zero zone; the real loader matched on all maps |
| B5 | 41 chain load-time records on 9 chain maps (10, 135, 162, 163, 169, 176, 183, 391, 478) | CONFIRMED | [D] with `ChainMaps` of `AlundraStoryChainOpcodeAudit.cs` |
| B6 | 165 tables (136 map on 86 maps + 29 global), 446 slots = 363 real + 83 padding, padding all trailing, 233 loop / 130 destroy, 5148 frames, 2832 image sets, 12 307 images + 23 degenerate (46 refs, all map 161 table 0 anim 3), 20 315 refs (20 269 non-degenerate), 16 610 = sum over (animation, distinct set) | CONFIRMED | [D] `census*.py`; padding slots have offset 0 and a terminator byte 4/8/12 (= the low byte of the first offset) |
| B7 | 0 unresolved records; all 136 map tables are named by a record of their own map; 17 of the 29 global tables are named by records (the 12 others are native-only) | CONFIRMED (the 17/12 split is new) | [D] |
| B8 | frame shown `Delay & 0x7F` ticks, terminator 0 destroy / 1 loop, image set at `record + ImageSetPointer + 2` (byte offset), IDSV first byte | CONFIRMED | [B] `timeline.py`: 363 anims, 20 357 ticks (two loop periods), compares image-list bytes (14 per image) and IDSV on every tick, 0 mismatch |
| B9 | destroy anim pushed on `life+1` ticks, slot freed at tick `life+2` (flag set the tick the terminator is read, still pushed that tick) | CONFIRMED | [B] 130/130; 476 aura anim 0: flag at tick 223, status 0 at 224. Code: push block `0x8003C490`, flag test `0x8003C468` |
| B10 | 163 rays vanish: pushed 11 ticks, freed at 12 (D-E19-55) | CONFIRMED | [B] four slots, ticks 1..11 pushed |
| B11 | frozen but still pushed while `PlayerControlFlags & 0x48`; no other bit stops the tick | CONFIRMED | [B] `0x08` and `0x40` each: 300 ticks, state unchanged, still pushed; `0x01` does not freeze |
| B12 | order in the binary frame: map events `0x8003C67C` -> entities `0x8003B388` -> effects `0x8003C410` (list reset before them at `0x8002E0F8`) | CONFIRMED | disassembly `0x8002E058..0x8002E110` |
| B13 | the nine opcodes: sizes 2,2,3,8,8,8,9,8,9; semantics (`0x90`/`0xA2` = `SpawnMapEffect(rec, force=1)`: no zone test, no `0x40` test, no-op past the record count; `0x91` frees now; `0x92` sets target anim; `0x93` x,y = u16<<16, z=(u16<<16)+1; `0x94` forces s16<<8; `0xA0` adds u16<<16 (wraps = signed); `0xA1`/`0xA3` entity pos + d<<16, no +1, nothing if search fails; all act on every slot with status!=0 and `+0x30`==record, destroy tick included; no Result, no wait) | CONFIRMED | [B] sizes returned by the real handlers; dispatch table at `0x80098FAC` maps 0x90-0x94 / 0xA0-0xA3 to `0x800405A8/D4, 0x80040628/80/71C, 0x80040D60/E10/F00/FAC` |
| B14 | golden values: 476 `0xA2/0x93 [0,960,160,48]` -> raw (62914560, 10485760, 3145729); 391: 5 load slots at (996,504),(996,600),(996,696),(996,792),(1020,792) anims 0,1,2,3,3, five `0xA2` placements then forces fy 12800/9216/25600/12800/12800; 135: altar (468,376,z64), `0xA0` `[0,0,8,0]` then `[0,-72,0,0]` -> (468,384) then (396,384); 162: 7 + 11 = 18 slots, record 5 holds 12; 129th creation returns 0 and changes nothing; 476 `B[2]` anim sequence 0,1,3,1,3,3,1,3,3,3,1,3 | CONFIRMED, except "four `0xA2`" for 391 (see C3) | [B] `golden.py`, operands read from the exported `Codes` |
| B15 | pool: 128 x 0x80 at `0x80138608`, lowest free index wins, cleared at every map load (128 writes at `0x8003C1A4`), template `0x8013C608` never written (single reference, lies in BSS beyond the EXE image) | CONFIRMED | disassembly + `xref.py` |
| B16 | anchor `((y-z)>>16)`, cull of the anchor only in [-128,448] x [-128,368] | CONFIRMED | `0x8002DB68..0x8002DBA8` |
| B17 | emitter: vertices, uv = (Sx,Sy),(Sx+W,Sy),(Sx,Sy+H),(Sx+W,Sy+H), semi = `Spritesheet` bit 3, ABR = bits 4-5, page = base(0 map / 11 global) + (`Spritesheet`&7), CLUT base 0x20 map / 0x60 global + Palette, OT slot `16*min(depth>>20,59)+6` | CONFIRMED | [B] `emit2.py`: 2832 sets, 12 330 packets (degenerate included), 0 mismatch with fractional x/y/z; tpage/CLUT checked against synthetic unique table values (my first run showed 9302 "bad" only because the tpage table is zero in the interpreter; fixed in `emit2.py`) |
| B18 | image 0 of a set is drawn LAST; larger key is drawn later | CONFIRMED (stronger than "[F/H]") | [B] OT head after the emitter = last submitted packet; the list sort `0x8002DA04` is DESCENDING (`slt pivot,key`) and unstable (Hoare), then submitted in that order with head insertion |
| B19 | depth key = `(y & 0xFFFF0000) + (z>>16) + (IDSV<<16)` with y after the forces; `ComputeEntityElevation(y, idsv)` gives the same row for mode 0 | CONFIRMED | `0x8003C2CC..0x8003C318`; `WallPlacementOverlay.cs:393-403`. (Edge: a negative z borrows from the y part; no record has Z<0) |
| B20 | 63 % deformed (8680 scaled + 4136 non-rectangular of 20 269 = 63.2 %); entities 31 % | CONFIRMED | [D] exact counts; entity figure from the G2b plan (30.8 %) |
| B21 | mirror flags equal the corner order (X1>X2, Y1>Y3) and `SourceX == Sx+1` iff mirrored X, on all refs | CONFIRMED | [D] 0 mismatch; mirrored X 3127, Y 282, both 353 |
| B22 | semi/ABR counts: ABR1 14 945, ABR2 452, ABR0 371, opaque 4501 (non-degenerate), no ABR3 | CONFIRMED | [D] |
| B23 | sheet classes of the 12 307 images: 2676 non-semi (0 with STP texels), 9631 semi = 9175 STP-only + 263 mixed + 193 no-STP; 748 all-transparent (7 maps x 97 = 25, 76, 84, 95, 411, 432, 476; 161: 35) | CONFIRMED | [D] `sheets.py`; alpha codes {0,128,255} only |
| B24 | G0 annex: 1435 cells, 20 269/20 269 refs land on the annex cell | CONFIRMED | [D] `cells.py` (key = file, page, palette, SourceX, SourceY, W, H, AtlasX, AtlasY): 0 missing, 0 count mismatch |
| B25 | load-time peak 9 slots / 15 quads (maps 271, 289, 357) | CONFIRMED | [B] 600 ticks per map |
| B26 | modes 1/3 only from native code (5 + 1 sites), mode 2 creator without a direct caller | CONFIRMED | call-site scan: `0x8003BE74` x5 (mode 1), `0x8003BFE8` x1 (`0x80038F48`), `0x8003BF44` no `jal` |
| B27 | native "warp" effects are item-use visuals (`g_warpLockTimer` = item id), "pickup" ones are HP/MP; no creator sits in the map-transition code | CONFIRMED | decompilation `PlayerManager.cs:1994`, `:3983`; creators at `0x80032DF8-0x80033C54` (14 HP/MP), `0x80034B30-0x80034FE4` (item use); static scan of the 70 `0x8003BDD8` call sites: 54 pass bank flag 0 (GLOBAL), 9 pass 1 (MAP table), 7 not decidable statically |
| B28 | no effect state in the save | CONFIRMED (indirectly) | the pool base is referenced only by effect code, the opcodes and two boss natives; load = warp = new world |
| B29 | 476 aura reference frame: probes (160,120)=(216,248,255), (150,100)=(144,168,200), (170,140)=(136,168,224), (120,90)/(160,60)=black reproduced by my own rasteriser | CONFIRMED for those, REFUTED as an exact oracle for (200,150) and the pixel count (see C2) | [D] `ref476.py`: same 3 colours, (200,150)=(48,48,96), 10 173 non-black (report: 10 183) |

### 1.2 Repository facts (re-read)
| # | Claim | Verdict | Note |
|---|---|---|---|
| R1 | nothing exported today but `Sprites/hero/hero_effects.json` (29 tables, raw), read by nothing | CONFIRMED | writer `SpriteWriter.cs:1022-1047`, test `SpriteWriterTests.cs:146-148`, docs only |
| R2 | closed list: 102 rows = 99 EFFET (0xA2 46, 0x92 19, 0x90 15, 0xA0 6, 0x93 5, 0x94 5, 0x91 2, 0xA3 1; maps 476 20, 10 14, 169 13, 176 12, 162 11, 135 11, 391 10, 174 3, 181 3, 183 2) + 3 `PREDICAT` on map 10 | CONFIRMED | `Data/story-chain-skipped-opcodes.tsv` |
| R3 | `MapsWithoutSkippedSite` 19 -> 28; assertion at `AlundraStoryChainSkippedOpcodesTests.cs:83`; array at `AlundraStoryChainOpcodeAudit.cs:49` | CONFIRMED | 9 maps lose their last row (only map 10 keeps 3) |
| R4 | `IsPorted` counts a degrading handler as ported, so the 99 rows must leave in the commit that adds the handlers | CONFIRMED | `AlundraStoryChainOpcodeAudit.cs:70-100` |
| R5 | arcs that pin the skips: A2/A4 `AlundraVisionArcTests.cs:32`, A6 `AlundraShipBlockArcTests.cs:33-37`, A20 `AlundraInoaDayOneArcTests.cs:39,71-77`, A10J/T-A10v/T-B9 `AlundraEntityContactArcTests.cs:171-174,290-292`, hero jump `AlundraHeroJumpArcTests.cs:64`, A13/A15/A17 via rule 5; A10/A11 (maps 165/164) untouched | CONFIRMED | grep of every `*Arc*.cs` for the nine opcodes finds exactly these (nothing else) |
| R6 | `UnknownOpcode_KnownSize_SkipsBySize` uses 0x93 (stays green with a degrading handler, premise stale) | CONFIRMED | `AlundraEventProgramRunnerTests.cs:323-335` |
| R7 | effect pass cannot live in the map-events loop (it `continue`s on no hero / blocked tick) | CONFIRMED | `AlundraWorldProxy.cs:2246-2279` (see C4 for the shifted numbers) |
| R8 | `_backdropStage` is a field initializer (`Clone()` trap), seams are default interface members, `IEntityWorldContext` pattern, backdrop push outside the gameplay gate | CONFIRMED | `:583`, `IEntityWorldContext.cs:168`, `:2380-2383` |
| R9 | `TextureAssetWriter.EnsureTexture` = 2 catalog entries per texture, PointClamp, `Save()` needed after it | CONFIRMED | 87 sheets -> 174 entries; `Phase9.Backdrops` runs before `Phase8.Verify` |
| R10 | `RenderSortKey2D` ascending, localOffset N-1-k puts image 0 last | CONFIRMED | `RenderSortKey2D.CompareTo` |
| R11 | engine: per-sprite PSX mode + two-window STP rule exist for rectangles; no free quad | CONFIRMED | `SpriteRendererComponent.cs:618-660, 787-795` |
| R12 | next engine ADR 0061, next parent ADR 0040 | CONFIRMED at HEAD `731dcab` | engine branches go to 0060 (audio-modern); parent has 0039 and f4b added none |
| R13 | `docs/formats/misc-data.md` says `Spritesheet` indices exceed 0-7 | CONFIRMED refuted | values are 0..42 only because bit 3 = semi and bits 4-5 = ABR; `&7` is 0..6 |
| R14 | "UNCONFIRMED" items (not decidable here, agree they stay open): the hero tile read at map entry for the 6 off-chain records; whether the engine's integer placement matches the DLL camera snapping for effects (`RenderProjectionComponent.SnapToPixel` exists, its use for a service-pushed quad is not established); whether the real PSX CLUT of the 476 aura palette 1 equals the static extraction (748 all-transparent images) | UNCONFIRMED | agree with the report's own labels |

## 2. Corrections (decision-relevant first)
- **C1 - O-E19-71 is no longer pending.** Commit `731dcab` (2026-10-06, after the report) records **D-E19-92**: deformed quads are drawn at the screen
  resolution (exact at x1 and for 1:1 quads, smoother than the PS1 elsewhere; ADR-0048 not revised, the gap is written in the ADR). The report's
  question 1 and the statement that G2b-1, G2e, G3c and G4 "wait for the pending G2b decision" are outdated: they wait only for the G2b plan ("plan a
  ecrire" in section 1.2o.5). The decision text names the ENTITY sprites; effects are 63 % deformed and additive, so G2e/G3c should state "D-E19-92
  applies to effect quads too" as an explicit assumption (one line for the author to veto), not a new question.
- **C2 - the reference frame and its "values writable in advance" rest on a superseded texel rule.** The report's `ref_frame.py` uses `psxraster`
  with integer sample points + floor. The G2b annex (committed in `731dcab`, `engine-notes.md` section 5, `redux.py`) found the hardware-verified rule
  (PCSX-Redux, `floor((u + 0.5))`, bias 0x8000) and recommends contract (C) "GPU-centre, SourceX window, test against an independent reference
  rasteriser written in the same convention". Re-rendering the 476 aura first frame with `redux.py`: probes (160,120), (150,100), (170,140) and the
  black ones are unchanged, but **(200,150) becomes (8,0,24) instead of (48,48,96)**, 3139 pixels differ from the report's frame, the non-black count
  is 10 098 (bias 0x8000) / 10 189 (bias 0) / 10 173 (my floor-rule frame, which reproduces the report's probes) versus 10 183, and 137 samples fall outside the
  SourceX crop (clamped to the cell). (The 3139 pixels are the difference between the Redux 0x8000 frame and my floor-rule frame; Redux bias 0 differs from it by only 308 pixels.)
  Plan rule: the G3c pixel proof must pin only interior probes (smooth regions) with the +-1 tolerance of G2a, or regenerate every probe with the
  reference rasteriser of the convention G2b-1 chooses; never pin a pixel count or an edge pixel from the report's frame.
- **C3 - map 391 has FIVE `0xA2` (pc 228, 236, 244, 252, 408) and five `0x94` (417, 425, 433, 441, 503)**, not four: the pool after them holds 10 slots
  (5 load slots ids 0..4 for records 0,1,2,3,4 + 5 created ids 5..9 for records 3,4,0,1,2, so every record has two slots and all ten get the forces of their
  record). The forces list (5 values) in the report is right.
- **C4 - line anchors shifted by +4 in `AlundraWorldProxy.cs`** (f4b added 4 lines, `f4af632`): `ticksThisFrame` `:2133`, pad loop `:2175`, map-events loop
  `:2246-2279`, gate re-read `:2261-2262`, camera block `:2356-2363`, backdrop push `:2380-2383`, fade `:2391`, HUD loop `:2414`, `CloseFrame` `:2453`;
  `AlundraEntityScriptProxy.cs` commented `SpriteRef`/`ActiveEffect` now `:393-395`; `AlundraEventProgramRunner` default `UnknownOpcode` call `:1868`,
  method `:2797`. Structure unchanged.
- **C5 - the checkout is no longer dirty.** `f4af632` (f4b) and `731dcab` (G2b annex + decisions D-E19-91..95) are committed: the DLL files listed as modified
  and the untracked `docs/plan-e19-g2b-annexe/` are in history; `AlundraWorldProxy.cs` risk becomes "rebase onto `731dcab`", not "working-area clash".
  Parent ADR 0040 is still free (f4b added no ADR); the plan file grew by ~90 lines (section 1.2o is at line 7026 now).
- **C6 - "Delays stay raw (1-127)"**: in the JSON `Delay` is `0x80 | ticks` (129..255, the 0x80 bit marks "frame" versus terminator); the export must write
  `Delay & 0x7F`. No frame has 0 ticks (the "0 shows 256" rule is never exercised).
- **C7 - converter notes, map 163**: the table has 3 real animations (0 loop, 1 and 2 destroy) + 1 padding slot; the 4 records use anim 1 (destroy, 10-tick frame,
  hence the 11-tick life of D-E19-55). Map 391: 4 single-frame loops of 10 ticks (right).
- **C8 - the first reason for "no `.sprite` for effect quads" is weak**: only 2 of the 1435 cells serve quads of different PSX modes. The decisive reasons are
  the free corners (63 % deformed, per-frame composition) and the plan's own rule (the DLL owns the animation machine, no `.anim2d`). The recommendation stands.
- **C9 - doc list at G1/G3**: also `docs/editeur-couverture-dll.md` (the companion table at line ~49 and the "renaming a world breaks its companions" danger in section 1:
  `effects/*.effects.json` and `Data/effects-global.json` are two more world-name-keyed companions), and, at the retirement of `hero_effects.json`,
  `docs/demarrage-nouvelle-partie.md:51`, `README.md:107`, `docs/formats/README.md:23`, `docs/formats/misc-data.md:97-125` (ADR-0030 is history, leave it).
- **C10 - G2b-1 must carry the "no face culling" entry flag for mirrored quads** (G2b annex `engine-notes.md` 1.4): 3762 of the 20 269 effect refs are mirrored
  (X 3127, Y 282, both 353); the PS1 has no culling, the engine's pass culls counter-clockwise. The report's engine section does not list it.
- **C11 - O-E19-71 and D-E19-92 were not in the plan when the report was written**; they are now (`docs/plan-e19-opcodes.md` section 1.2o.5, open-points table).

## 3. Missed, with plan impact
1. Four native sites spawn MAP records with `force=1` from boss/NPC programs (`0x80064884` x1, `0x80065204` x3) and nine native sites create effects from the MAP
   table (bank flag 1: `0x800633E0`, `0x8006358C`, `0x80063624`, `0x80076DEC`, `0x80078B2C`, `0x80079C00`, `0x80079E84`, `0x8007A3AC`, `0x8007A540`). They belong to
   E14 but justify exporting ALL 136 map tables and 29 global tables (done by the proposal) and a creator API that takes (bank, sprite, anim) and a record index.
2. [H] The load order matters for the tick count of the 163 rays: in the binary the spawn happens at map load and the first tick already pushes frame 0 (tick 1 of 11);
   the DLL must spawn before its first `Update` pass (and count the sticky first-frame tick) or the visible lifetime shifts by a tick. The plan's G3a test should pin it.
3. Effects submitted after entities in the binary list; equal keys fall to an unstable sort: the proposed stable slot id is a documented deviation, nothing observable on data.
4. Tests to add beyond the report's list: a closed-list guard that the 99 EFFET rows are gone AND map 10 keeps exactly the 3 PREDICAT rows; a rule-5 style test for
   the 6 zone-restricted records with a synthetic hero tile (zone (37,40,51,59) etc.).

## 4. Net effect on the proposed split
The split G1 / G3a / G3b / G2b-1 / G2e / G3c / G3d / G4 stands. Changes: (a) drop "waits for O-E19-71" (decided), keep "waits for the G2b plan"; (b) G3c's pixel proof
must follow the convention chosen by G2b-1 (C2); (c) G2b-1 gets the no-cull flag (C10); (d) G1/G3 doc list grows (C9). No product question remains open for G1, G3a, G3b;
for G2e/G3c the only one is the one-line confirmation that D-E19-92 covers effect quads.
