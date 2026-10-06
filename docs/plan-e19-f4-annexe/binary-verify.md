# E19.f4, surface "binary": adversarial counter-check

Read-only. Nothing built, run (tests), exported, edited, staged or committed in the repository; `scratchpad/bak` and
`CasaEngine.Launcher/Program.cs` untouched. Everything written lives in this folder. The report's own outputs
(`../binary/*`) were used only as the SUBJECT of comparison (its `values.json` and its `f4_model.py` are compared against my
own runs); no verdict rests on them.

Method. Tags: **[bin]** re-read in the disassembly (capstone via `../../e19j-disc/lib.py`, own helpers `d.py`, `xr.py`);
**[emu]** the REAL code run in the MIPS interpreter `../../f3-disc/binary/emu.py` (CPU only) through MY OWN rig `vrig.py`
(own stubs, own slot dispatcher replica, own entity/table setup, nothing taken from `../binary/rig.py`); **[data]**
recomputed from `data-extracted` / `alundra-project`; **[code]** read in the repository.

Probes (all re-runnable from this folder): `xr.py` (xref over the image: jal/j, data words, lui+addiu/ori), `d.py`,
`vrig.py` (rig), `v1_open_close.py` (open at N, close at T, per-frame name + portrait), `v2_gates.py` (11 gating cases through
the three real handlers), `v3_compare_s1b.py` + `v4_retry.py` + `v8_s467.py` (my rig vs the report's `values.json`, scenarios
S1 to S7), `v5_72.py` (48 x 72 image), `v6_edge.py` (close during slide-in, double close, open while returning),
`v7_diff.py` (300 random raw sequences, my rig vs the report's model), `names_v.py` / `widths_v.py` (names and widths from
the exe), `census_v.py` (portrait census), `consec_v.py`, `ctx_v.py`, `ops_v.py` (opcode censuses).

## Verdicts

| # | Claim of the report | Verdict | Evidence |
|---|---------------------|---------|----------|
| 1 | Only `0x0D` (`0x8003D578`), `0x5C` (`0x8003F01C`), `0xC4` (`0x80041DA8`) open the portrait and the name; `TryOpenDialog` has these 3 callers; save book and warp texts open a bare box | CONFIRMED | xref: `0x80057C84` callers `0x8003D634 0x8003F0F8 0x80041E84`; `0x80059F6C` callers `0x8003D640 0x8003F104 0x80041EA0`; `0x800423F8` callers `0x8003D658 0x8003F11C 0x80041EB8`; `InitializeDialogMessage 0x800450F0` has 5 other callers (`0x8007BA00 0x8007C320 0x8007C684 0x8007C6D8 0x8007FDF8`) that never call either opener |
| 2 | Order in each handler: portrait open, name open, `TryOpenDialog`; both openers re-run on every retry tick | CONFIRMED | disassembly of the 3 handlers; [emu] `v2_gates.py` ("0D retry": return 0, name flags 5, portrait state 5) |
| 3 | `0x5C`/`0xC4`: entity = first match `[0]` of the search; no match = neither, the box still opens (returns 4 / 6); for `0xC4` the match gates both | CONFIRMED | `0x8003F044 beqz`, `0x80041DCC beqz`; [emu] "5C no match" (4, nothing, TryOpen called), "C4 no match" (6, nothing) |
| 4 | `0x0D` uses "the script's own entity" | **CONFIRMED WITH CORRECTION** | `a0` of the handler is the LOGIC entity: `RunScript` reads it from the owner's word `+0x230` before every instruction (`0x80042284 lw $s1,0x230($s4)`, passed as `a0`). It equals the owner unless `0x42`/`0x43` retargeted it. See "Corrections" 1 |
| 5 | Speaker id = `entity+0x68` = record byte, `0x100` added for a map sprite; hero 0 | CONFIRMED | `InitializeEntity 0x80039D04` stores caller `sp+0x10` at `+0x68` (`0x80039D70`); the three spawn paths (two by sprite index, one by record) add `0x100` under the `SpriteDirection & 0x80` test, the hero passes 0 (`0x8003199C sw $zero,0x10($sp)`) and the copy path `0x8003AC14` keeps the old `+0x68` (`0x8003A270 andi $s3,$s1,0x80`, `0x8003A304 addiu $a2,$a2,0x100`, `0x80039FC4`, `0x8003A134`) |
| 6 | DLL carrier is `proxy.SpriteType` (`-1` unresolved), not `SpriteTableIndex`; flag carried by `proxy.Flags` | CONFIRMED | `AlundraEntityScriptProxy.cs:94-108`, `EntityRecordMapper.cs:225-239`, `AlundraEntitySpawnFactory.cs:572,629`; `EntityFlags.HasPortrait = 0x00800000` already exists (`EntityFlags.cs:175`) |
| 7 | `0xC4` takes the id from `v2 \| v3 << 8`; the binary does not use operands 4-7 as a portrait rectangle (the decompilation does) | CONFIRMED | `0x80041E8C-0x80041EA0`; decompilation `EntityEventHandlers.cs:3694-3720` passes `Parameters[4..7]`; [emu] "C4 match explicit 0x111" gives id 273 and the record's rectangle |
| 8 | Portrait flag = `entity+0x6C` bit 23, sprite header byte `0x12` bit 7 | CONFIRMED | `0x8003D58C-0x8003D598`; `InitializeEntity 0x80039DA8-0x80039DD0` (`lhu 0x10(a2) \| lbu 0x12(a2) << 16`) |
| 9 | Name opener: blocked while `flags & 4` (open or closing), id outside `0x100..0x1FF`, string pointer 0 or empty; `OpenSlot(12)` runs the init at once | CONFIRMED | `0x80059F7C-0x80059FC8`; `OpenSlot 0x80047F94` calls the init (`jalr`) before returning; [emu] ids `5`, `0xFF`, `0x200`, `0x150` (no string) open nothing; retry while flags 6 is refused |
| 10 | 60 of 256 ids have a name, all plain letters, widths 19 to 56, rest text x min 92 | CONFIRMED | the table `0x800C400C + 4 id` is filled by `0x8002C6B4` from ETC string `0x100 + i`; `Etc.yarn` `Etc_0256..0511`: 60 non-empty, all `[A-Za-z]+`, lengths 3..8; widths recomputed from the exe's advance table `0x800993C4 + 20 c` (note the base: `0x800A0000 - 0x6C3C`): min 19, max 56, equal to `names_widths.json` on all 60 (width and rest x) |
| 11 | Slide: cells x = 320 + trunc(-256 (k-1)/15), 64 from pass 16, flags 5 -> 4 at pass 18 | CONFIRMED | `UpdateUiBoxesPosition 0x80047DD0` read (pass 16 and 17 set the end and decrement the mode, pass 18 returns 1); [emu] `v1_open_close.py`: 320 303 286 269 252 235 218 201 184 167 150 133 116 99 82 64 64 64, flags 4 at pass 18 |
| 12 | Text x = cfg.x + trunc((112 - w)/2), y 148, rendered once at open, clip (cfg.x+16, 148, 258, 34) clamped to 320 and never cutting a real name | CONFIRMED | `0x8005A4C4-0x8005A508`, `0x8005A5B8-0x8005A6E8`, init `0x8005A3A8-0x8005A3C8` (text prep only at init); [emu] Jess w 21: 365 348 ... 127 109; clip (320,148,0,34) at pass 1, (80,148,240,34) at rest. Left edge of the clip (cfg.x+16) is always left of the text (offset >= 28), right edge is the screen edge |
| 13 | Close at the text box trigger T, same frame, first closing pass at x 64; x 64 81 98 ... 302 320 320; name released at T+17; text box at T+18 | CONFIRMED (name side); T+18 taken from f2a | trigger `0x80045EF8-0x80045F08` (sfx 7, name close, portrait close) inside the slot-0 pass; slot 12 runs after slot 0 (`0x8004813C` loop); [emu] closes at T=22: 64 81 ... 302 320 320, released at 39 = T+17 (flags 0, cfg.x back to 64, update returns 0). The text box release (T+18) matches `AlundraDialogueDirector` doc ("18 passes after the trigger") but I did not re-derive the text box timeline |
| 14 | A close during the slide-in starts from the current x; a second close restarts from the current x; a close with the name absent is harmless | CONFIRMED | `0x80059FE0` rewrites the slide block unconditionally; [emu] `v6_edge.py` (start 235 -> 240 246 ...; second close at 132 -> 144) ; S2 (no name) shows no name pass |
| 15 | Portrait = one POLY_FT4 code `0x2C` (opaque, modulated), uv rectangle = first image record `*(hdr+0xC)` bytes `[4..7]`, clut/tpage from the ENTITY's own tables (`+0x1B0`, `+0x1B4`) | CONFIRMED | `SetPolyFT4 0x800843B0` sets `0x2C`; `0x8003D5B4-0x8003D5F8`; the extractor's `SiImageSet` layout (`[0]` depth, `[1]` count, then `SiImage` at `+2`) matches bytes `[2..7]`; [emu] `v5_72.py`: clut = table[`+0x1B4` + palette], tpage = table[`+0x1B0` + sheet] |
| 16 | Flight: 15 passes, rest (8,116), top-left anchored; size trunc(48 (15-c)/15) x trunc(56 (15-c)/15); rgb 127 + trunc(128 c/15) (255 -> 135); rest 128; return 127 -> 246; last return pass degenerate 0x0 rgb 0; gone at T+16 | CONFIRMED | `0x80057EBC-0x80058130` re-derived instruction by instruction (the `0x88888889` division by 15 is a truncating `/15`); [emu] first pass N+1 at the head 0x0 rgb 255, N+15 rgb 135, rest from N+16 (state 4, 48x56, rgb 128); return from T (rest, 48x56, rgb 127) to T+14 (3x3, rgb 246), T+15 degenerate at (8,116), T+16 nothing drawn |
| 17 | Return flight shrinks to the speaker's head point NOW (read at the close trigger, through the pointers captured at open) | CONFIRMED | `0x80057B9C-0x80057C0C`; [emu] `v7_diff.py` (entities and camera moved at random between open and close; an open ignored because the block is busy changes nothing but the entity memory the pointers read) |
| 18 | The on-screen quad is always 48 x 56; a 48 x 72 image is squashed | CONFIRMED | `0x80057E4C/0x80057E54` store the constants `0x30`, `0x38` at `+0x84/+0x88`; [emu] uv rectangle 48 x 72 (v 56..128), quad 48 x 56 |
| 19 | One portrait block shared with the inventory; wrappers rest (248,104) / (8,116), both overwriting the rest fields even when the open is ignored; open ignored while state != 0 (including the 15 return passes) | CONFIRMED | `0x80057C18` (`0xF8`, `0x68`, size args `0x30`, `0x38`), `0x80057C84` (`8`, `0x74`), `0x80057D30`; callers of the close `0x80057B84`: `0x80045F08` plus 4 inventory exits; [emu] `v6_edge.py`: an open at the script phase of T+15 (state just became 0) is accepted, first pass T+16 |
| 20 | Corpus: 331 portrait records in 184 maps, 324 x 48x56, 7 x 48x72 (indices 122 and 162), 25 indices, none in the global table | CONFIRMED | `census_v.py` over the 483 `map_N.json`: 331 / 184 / {(48,56): 324, (48,72): 7} / the same 25 indices (4 records of 122, 3 of 162); no mirrored image; the flag and the image always agree; `map_alundra.json` has 257 records, none flagged, none with a portrait (it has `InventoryPortrait` only) |
| 21 | All 25 portrait indices also have a name | CONFIRMED | `names_v.py` (id `0x100 + index` present for each) |
| 22 | Atlas regions of the portraits are filled | CONFIRMED | map 17, record 122: `AtlasX/Y` (147,81), 48 x 72, 2395 of 3456 opaque texels |
| 23 | Opcode census: `0x0D` 2207 sites / 317 maps, `0x5C` 765 / 106, `0xC4` 31 / 11, explicit ids `0x111 0x112 0x11D 0x16E 0x174 0x19B 0x1B0 0x1BC x16` | CONFIRMED | `ops_v.py`, `consec_v.py` (3003 sites); all 8 explicit ids have a name; `0xC4` search selectors: `0x80` x 26, `0x81` x 5 |
| 24 | "Visual only": the two blocks are touched only by their own functions, no arc moves | CONFIRMED | whole-image reference scan of `0x80180070..0x801800F7`, `0x80180100..0x80180117`, `0x80180240..0x8018029F` (my false positives from `lui 0x8018; addiu -0x19c0` identified and discarded): name block only in `0x80059F6C-0x8005A700`; portrait block only in `0x80057B64-0x80058200` and the DR_AREA chain `0x80044EE0`; the UI slot table `0x80153028` is read only by `OpenSlot`, the dispatcher, the OT chain, the flag tests of slots 4 and 6 (`0x80048194`, `0x800481C4`) and the slot-active test `0x80047C8C`, whose only two calls ask for slots 0 and 0xB (`0x80055584`, `0x80055594`: the inventory refuses to open during a text box); nothing tests slot 12 |
| 25 | Draw order: text frame (slot 0), text rows (2), portrait (3), name frame / choice (5), name text / labels (6) | CONFIRMED for the slots owned by f4; rows (2) and cursor order from f2a/f3 | portrait chained into slot 3 (`0x80044CBC`: `OT + 0xC`), name cells layer 5 (slot 12 entry word 6), name text/clip `0x80146F70` = slot 6, text frame layer 0; the UI OT is cleared with `0x8008511C` = forward `ClearOTag` (slot 0 drawn first); each prim is head-inserted (last inserted, first drawn); the splice at `0x80044CC4-0x80044F28` only puts one DR_MODE per slot (and the full-screen DR_AREA in slot 3) |
| 26 | The portrait and the name frame overlap the top 4 rows of the text frame | CONFIRMED | portrait bottom 172, name frame bottom 172, text frame top 168 |
| 27 | Retry-ahead: next speaker's portrait up to 3 frames, name 1 frame before its text box | CONFIRMED | [emu] `v4_retry.py`: portrait opens at N+37, name at N+39, second text box at N+40 (first passes N+38, N+40, N+41), whole table equal to the report's S5 |
| 28 | "Model reproduces the real binary on 80 random cases, 10 mutations caught" | CONFIRMED by independent means (not re-run as such) | my rig reproduces all 7 scenarios of `values.json` (name and portrait columns, 60 frames each, 0 diffs) and agrees with `f4_model.py` on 300 random raw sequences (0 diffs once my harness' pointer semantics matched the binary's; the first run, with a harness that replaced the speaker object instead of updating its memory, showed 67 differences, which proves the test is sensitive) |
| 29 | Converter does not read `DialoguePortrait`; extractor fills it | CONFIRMED | `rg DialoguePortrait`: analyser only (plus ADR-0030 and the plan); `SpriteBankReader` reads `InventoryPortrait` only; the name frame asset `g_textTilesConfiguration.sprite` (`91ca17ae-...`) exists |
| 30 | MGUI gap: overbright modulation; `BlendType` only in `MGUI.Shared` `DrawSettings` | CONFIRMED | `rg BlendType` in `CasaEngineMonogame`: `MGUI.Shared/Rendering/DrawSettings.cs`, the MonoGame integration renderers and the engine clip executors; none in `MGUI.Core`; the inventory portrait doc states the tint stays normal (author's D2) |
| 31 | "Tests that would move": `AlundraDialogueSpeakerOpcodeTests`, `AlundraTextBoxOracle`, inventory portrait tests, load-reset test | PARTLY CONFIRMED | the speaker test file does describe v1..v3 of `0xC4` as decoys (`:37-59`); `AlundraInventoryPortrait.RestX/RestY` are `const` used by `AlundraInventoryViewModel.cs:182-185`, `Instance` is used 15 times in `AlundraSaveGameLoadResetTests`; the other two files were not opened |
| 32 | Not proven: nearest-texel PSX sampling of a scaled quad | UNCONFIRMED (stays open) | no emulator of the GPU here |
| 33 | Not proven: nothing drawn in OT slots 7-9 | STRENGTHENED, not proven | the 13 UI slot entries use layers 0, 5, -1 only; no direct reference to `0x80146F74/78/7C`; the only slot-6 producers found by address are the choice labels (`0x8004FE28`), the name (`0x8005A544/0x8005A6A8`) and one prim anchored at the text-frame cfg `0x8009CFBC` + 16 in the `0x80059xxx` UI code (`0x80059D6C`, not identified; that code is a menu update, not the dialogue path) |
| 34 | Hazard: the name opener clears and refills the shared staging buffer | CONFIRMED (mechanism), occurrence negligible | `0x800472D0`: `bzero(0x80149CE8, 0x800)` then `RenderTextBitmap`; reachable only when a dialogue opcode retries while another text types: in the corpus the linear census finds ONE candidate pair (map 394, two `0x5C` 99 bytes apart with `Wait` opcodes between), see "Missed" 1 |

## Corrections to the report

1. **`0x0D` speaker = LOGIC entity, not "the script's own entity".** The handlers receive `a0` = logic entity, `a1` = owner
   (`0x80042284`, `0x800422C0-0x800422C8`). `0x42`/`0x43` rewrite the owner's word. The DLL already dispatches with the
   logic entity (`AlundraEventProgramRunner.cs:417-420`, `Dispatch(command, logic, entity, ...)`), so the plan must give
   `OpenDialog` the `entity` parameter of `Dispatch`, never the owner. Census (linear, same program, upper bound because
   control flow is ignored): 165 of 2207 `0x0D` sites and 256 of 765 `0x5C` sites follow a `0x42`/`0x43` in their program;
   64 maps. For `0x5C`/`0xC4` the logic entity is also the REFERENCE of the search (selector `0x80` = it).
2. **"font3 palette #8"** is palette 8 of `WIND.CL` (`*(0x80146E28 + 0x10)`), the palette of the text box bands, not the
   CLUT of `font3.png`. The two differ only at index 4 (O-E19-66 in the plan); no letter is affected.
3. Name and frame SPRTs are drawn raw and opaque (`SetShadeTex 1`, `SetSemiTrans 0` at `0x800474B8-0x800474D4`): no tint,
   no blend; one SPRT per display buffer (two in total), not "one".
4. "Gone at T+16": precisely, the degenerate pass is T+15 (state set to 0 in that pass), nothing is drawn from T+16 and a new
   open is accepted from the script phase of T+15.

## Missed by the report (would change or simplify the plan)

1. **Retry-ahead is practically absent from the corpus.** Linear census of the 3003 dialogue sites: 1 pair of dialogue
   opcodes with no `0x39`, jump or return between (map 394, `0x5C` at code offset 1030 then 1129, 27 opcodes between, among them
   several `0x37` Wait). So S5 and the staging hazard are theoretical; the per-attempt openers cost nothing to keep faithful
   (D-E19-81) but the oracle needs no dedicated S5 arc.
2. **Logic-entity retarget frequency** (Correction 1).
3. The `0x5C`/`0xC4` search already exists in the DLL (`EntitySearchService.GetMatchingEntitiesBySearchType`, slot order,
   already used by `AlundraEventProgramRunner` for `0x67` and `0xAC`, among others): `[0]` is `matches[0]`.
4. `EntityFlags.HasPortrait` exists; no new constant.
5. `AlundraInventoryViewModel` expresses the portrait as a `RenderTransform` relative to the `const` rest
   (`Translation = (X - RestX, Y - RestY)`, scale = drawn / full): making the rest a per-start value changes that view model
   and its tests, not only `AlundraInventoryPortrait`.
6. The save-load reset must also clear the name box (flags 0, slot closed, cfg.x 64): the binary resets both only at boot
   (`0x80044C28`, `0x80044C40`, from `0x80044BE4`, single caller `0x8002C238`).
7. The boot function `0x8005A0C8` is the only place that initialises the name frame cells (no per-open cell init); nothing to
   port, just do not look for one.

## Not re-derived here (taken from the f2a/f3 slices)

The text box timeline (T = N+22 for "AB" with Square held, first glyph N+19, release T+18), the text rows in OT slot 2, the
choice cursor order. The relative order "RenderScene (dispatcher then OT build, `0x8002BE5C/0x8002BE64`) before Update(0)" was
spot-checked in the main loop (`0x8002C3FC` RenderScene, update path after the VSync at `0x8002C414`).
