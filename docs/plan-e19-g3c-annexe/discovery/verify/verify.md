# Counter-check of the G2e / G3c discovery (surface g2e-g3c), 2026-10-06, read only

Repository `D:\development\repo\alundra-casaengine-project-converter` at `chantier/e19-suite2` (HEAD `bbea80a`), engine `dd1f8afa`. Nothing built, run, exported, edited, staged or
committed inside the repository (status identical before and after: the author's `CasaEngine.Launcher/Program.cs` and a 0-byte `AlundraTools/log.txt` of 2026-10-03 only).
All probes are in this folder. Labels: [B] real code of ALUN_CD.EXE (France) in the MIPS interpreter, [F] fact read in the repository, [M] measured by a script of this folder.

## 0. Verdict

The report is sound on its mechanics: every binary rule, every engine and DLL fact I re-derived holds, and 4 of its 11 reference digests are reproduced to the bit by a renderer
written from scratch. One decision-carrying defect: **the B(k) reference of the aura frames carries a coverage tie rule that is not the plan's rule, not the hardware walker's, and
not the GPU's; 1 to 3 pixels per aura frame are wrong in the reference**. Two proof-coverage gaps (no mirrored quad, no ABR0/ABR2 in the real-data scenes) and four nits follow.

## 1. Confirmed claims (with my own evidence)

### Binary [B]
| Claim | My evidence |
|---|---|
| Anchor `((x>>16)-camX, ((y-z)>>16)-camY)`, int32 arithmetic shifts; cull inclusive -128..448 x -128..368, whole element dropped | Disassembly of `0x8002DB48` read by hand (`lh 6(a0)`, `subu`, `sra 0x10`, `sltiu 0x241` / `0x1f1`), then `emit_fuzz.py`: 2500 trials on a hand-built element (random images, positions including 32-bit extremes, z +-1, cameras), 209 emitted, 2291 culled, **0 mismatch** (count, vertices, UV bytes, semi bit, cull). Independent of `fxm.py` and of the effect machine. |
| OT slot `16*min(key>>20,59)+6`; image 0 drawn last (chain head = image N-1) | Same fuzz: chain of the expected slot equals N-1..0 on every emitted case (0 chain mismatch). The binary has no lower clamp: a negative key indexes below the OT (see N5). |
| Across elements the list is sorted DESCENDING (signed compare of element+0x10), so the smaller key is drawn first | Disassembly of the Hoare partition `0x8002DA04` (`slt $v0,$t0,key` keeps greater keys left) and `sort_check.py` on the REAL `0x8002E130`: 300 random lists of 2-40 elements with distinct keys, drawn order = ascending key, **0 mismatch**. |
| Equal keys: unstable, P(lower index on top) about 0.5 | `sort_check.py`: 35 387 equal-key pairs, lower index on top 47.1 %. Deterministic for tiny lists (2 or 3 equal elements: higher index on top). |
| 391 slots 3 and 4: the binary alone draws slot 3 on top, the DLL's stable rule slot 4 | `tie391.py`: the REAL machine, real five slots, cam (836, 640): OT 806 draws the slot at screen x 184 (slot 4) first, slot 3 last. Keys 502/598/758/806 confirmed (real depth values 33030144, 39321600, 50135040, 52625408, 52625408). |
| Real slot positions `((X*12+12)<<16, (Y*8+8)<<16, Z<<19)`, depth key `(y&0xFFFF0000)+(z>>16)+(idsv<<16)` | `slots_check.py` on the real machine, maps 163 and 391: positions and depth keys equal the DLL formulas for all 9 slots (idsv 0, 0, 0, 69, 11, 11). |
| The polygon is drawn raw (no modulation), so `Color.White` is right; per-frame budget 0x200 quads | Loop at `0x8002DE88`: `SetPolyFT4`, `SetShadeTex(p, 1)` (code |= 1), colour bytes 0x80; overflow test `slti $s1, 0x200` at `0x8002E1F0`. |
| One frame is the state after the last tick of the frame; the render copy (x, y, z, key) is taken in the push block of `UpdateEffects` for every status-2 slot, frozen or not; a slot freed this tick is not pushed | Disassembly `0x8003C410-0x8003C4D0`; list reset at `0x8002E0F0-0x8002E104` (once per RenderScene call). |
| `0xA2 [0,960,160,48]` gives raw (62914560, 10485760, 3145729), row 119 at scroll (800,-8) (120 without the +1) | Operands decoded by hand; `AlundraEffectReservoir.Place` (`z = (z<<16)+1`); (y-z) = 7340031 -> floor 111 +8 = 119. |

### Engine [F]
`DrawPsxQuad` at `SpriteRendererComponent.cs:701` with the quoted signature; four overloads; two entries per mode; `PsxQuadTexelShift = 0.5 + 1/4096`; corners go into slots TR, BR, BL, TL (PS1 diagonal);
`NoCull` / `PsxQuad` set; sampler `PointClamp` (`:239`, `:441`); pattern `CellularLayerComponent` (`Update` resolves on a version change, submits from its own `Update`, handles released); `ComponentOrder.cs:43`
`CellularLayers` is the last value; `CasaEngineGame.cs:66-67` property and `:404` construction; `IsFixedTimeStep` false in `AlundraGame.json`; entity prefab `YSortedWorld`, sorting layer 300, `TopDownYUp`
(`Alundra.entity:539-553`); `SortPrecision` 100, `DeriveRenderPosition` `(X, -(Y-Z), 0)`, snap floor X / ceil Y (so coordinate `100*floor(Y-Z)` = the report's `100*ay`); `RenderPass2D.YSortedWorld = 300`.
`PsxQuad.fx` interpolates NORMALIZED coordinates and multiplies by `TextureSize` (so the float margin remark is about a float32 product near 1100, correct).
**ADR numbers**: engine, all local branches plus worktrees: highest 0070 (`chantier/demo-browser`), so 0071; parent: highest 0046 (`chantier/e19-suite2`), so 0047 (rescan before the commit, as the report says).

### DLL [F]
`AlundraEffectStage` / `AlundraEffectReservoir` / `AlundraEffectBank` API as described (public `Slots`, iterator `ActiveSlots`, internal `CurrentMapTable`, `DepthKey` stored by `Move` for mode 0 only and every DLL slot is mode 0 until G3d,
`Load` creates a fresh reservoir); proxy: `_effectStage` `:590`, attach-before-load `:687/:694`, `Load` `:703`, `SpawnLoadEffects` `:2160`, `RunFrame` `:2376`, backdrop push `:2423`; the only `continue` between the head of
`Update` and the push is inside the tick loop `:2295`, no early `return`; `ToOriginalScrollSpace` `AlundraCameraMath.cs:300`; `ComputeEntityElevation` internal static, `EntityRowMax = 0x3B`; `InternalsVisibleTo("Alundra.Tests")` only;
`Alundra.csproj` target `CopyGameplayDllToProject` AfterBuild copies into the tracked `alundra-project/` (a `ProjectReference` from a harness would trigger it).

### Data [M]
`scan_images.py` (own scan of the 158 exported documents): 12 307 images, 2338 mirrored in X, 396 in Y, **519 mirrored-X at U=0 and 137 mirrored-Y at V=0**, 0 non-transparent gutter texel (X or Y), 0 window outside its sheet,
modes (non-semi 2676, ABR1 8906, ABR2 396, ABR0 329, no ABR3), 87 sheets, tallest 512 x 1105 (map 161), widest 512, 2832 image sets. All equal to the report.
Sheet chain: all 87 ids resolve in `AssetInfos.json` to a `.texture` (sampler Point / Clamp) whose `texture_asset_id` resolves to an existing png.

### Reference digests (my renderer `my_render.py`, written from the plan text, sharing no code with `g3c_model.py` / `g2b1_predict.py`)
Exactly reproduced (SHA-1 of the RGB image, background (24,40,56)): **163 rays k=1** `8be82e40...`, **k=3** `2356a885...`; **391 pieces k=1** `801a081a...`, **k=3** `23edcdbf...`; pixel counts 3840 / 34560 / 8100 / 72900.
Scene facts reproduced: 9, 8, 7 quads for aura frames 0, 1, 8 (frames last 2 ticks: delays [2, set] x 16), 8 quads for 163, 6 for 391 (slot 0 culled), sort order, ABR1 additive, opaque 391.
B(1) against the hardware walker R (using their `redux_nomask.py`): 21, 4, 20 pixels, 0 and 0 on 163 and 391 (reproduced from their reference images).
Float margin: a best-case float32 simulation (`f32_tie.py`, 3436 exact ties on a 1105-texel sheet, 2556 on 435) resolves every tie upward: the +1/4096 epsilon is enough in the best case (the real GPU interpolator may do worse, see section 4).

## 2. Refuted claims

### R1 (decision-carrying). "Every pixel outside the noise zone must equal B(k)" is not satisfiable by a correct engine on the aura frames
The aura references of the report (6 of the 11 digests) contain pixels that are **not drawn by the plan's own rule**. B(k) in `g2b1_predict.py` resolves a coverage tie (an edge function exactly 0)
with a perturbation of the sample point (`tie_sign`, +1e-9, +1/(1e9+7)), not with the top-left rule that the plan (section "Regle de texel corrigee"), ADR-0068 ("covers by the top-left rule") and Direct3D / OpenGL use.
The two rules agree on axis-aligned edges, steep edges and the corners of rectangles, so the G2b-1 demo (0 differing coverage pixel in its 14 cases, `tie_gpu.py`) could not see it. They disagree at the apex pixel of a triangle whose
two edges are not both top/left, e.g. a vertex with a shallow right edge.
- Evidence 1, the hardware walker: `redux_vertex.py`: R does NOT cover (170,1) of tick 0 nor (217,5) of tick 16; the report's B covers them. `cov_stat.py`: 1500 random integer quads, 1.63 M pixels:
  **top-left rule vs R: 0 coverage mismatch; the report's tie rule vs R: 1944 mismatches**.
- Evidence 2, the aura: with the top-left rule my renders differ from the report's reference images by exactly one pixel at k=1 (ticks 0, 2, 16: (170,1), (170,1), (217,5)), two at k=3 for ticks 0 and 2 ((519,4), (528,5)),
  three at k=3 for tick 16 ((651,15), (655,16), (659,17)); `tie_count.py`: disagreements grow as 2k per frame (2, 4, 6, 8 at k=1..4), 0 on 163 and 391. None of these pixels is in the report's noise zone
  (`noise_476_a1_*.npy`: False), because its definition keeps `d2 == 0` ("exact ties kept").
- Evidence 3: with the top-left rule the **coverage of my aura frames equals R's exactly** (0 lit-pixel difference on the 3 frames); the 20 / 3 / 19 pixels that still differ from R are colour (texel) differences only, the documented walker residual.
  The report's "B(1) vs R: 21, 4, 20" therefore contains one coverage artefact per frame that is the reference's, not the hardware's.
- Plan impact: at the first aura frame the G3c proof (and the G2e demo quad "with the aura's real structure") would show 1 to 3 differing pixels outside the noise zone. The report's instruction "a failing tie pixel is an engine
  defect to fix, never a tolerance" would then push the executor to change the ENGINE to draw pixels that the PS1 does not draw. The fix is in the reference: regenerate B(k) with the top-left tie rule
  (it equals the plan text and ADR-0068; it leaves the G2b-1 predictions unchanged: 0 differing pixel on its cases at k=1 and k=3), or put exact coverage ties of triangle vertices in the noise zone.
- Digests under the top-left rule (mine; informational, noise pixels included as in the report): aura tick 0 k=1 `de9e3afd56e6b1b131a49328ab1140ca06dfc568` (10 087 lit), tick 2 k=1 `607a9e7d2191a26b79505160234876dce51a8609` (9 652),
  tick 16 k=1 `dba859ed08f61882f0531da37d6c34e1ad091dbc` (7 862); tick 0 k=3 `bda9e52c6a2ebbafd14de98d1be96a0433045cbb` (90 968), tick 2 k=3 `44577bffda90382a6cf0300ab044a75ef7d6f3c8` (87 052),
  tick 16 k=3 `c440a862e56ae8369fc0c1e24fb033fd3a755dea` (70 756); tick 0 k=4 `55029d770bab8dd5ba75bb45ba0e88d3a780d11d` (161 756; the report says 161 759). The report's 163 / 391 digests stand.
- Confidence: high for "the reference is wrong against R, the plan text and ADR-0068"; the GPU's behaviour at those exact pixels is analytic (top-left), not measured (nothing run here).

### R2 (minor, wrong statement). "These frames use ABR 1 and 2, which are exact"
The three real-data scenes use ABR1 (additive) and opaque only (aura 8 + 1 opaque, rays 8 ABR1, pieces 6 opaque; `my_render` census). **No ABR0 and no ABR2 quad is in any proof scene.**
ABR0 / ABR2 effects are in maps 378 (ABR2, 390 images), 7, 396, 77, 78 (ABR0) and the globals 0 (ABR2) and 21 (ABR0). The ABR to mode mapping `Abr + 1` is therefore proved at pixel level on synthetic data only (G2e demo "ABR 0 to 2").

### R3 (nit). "(157 companions plus the global one) resolves ... to a `.texture` whose PNG exists"
Only **87 of the 158 documents carry a `SheetTextureAssetId`** (86 maps + the global one); the other 71 are null by design. The guard must allow the null; as worded it would be understood as 158.

## 3. Missed items with plan impact

| # | Item | Evidence | Plan impact |
|---|---|---|---|
| M1 | Tie rule of B(k) (R1) | section 2 | Regenerate the aura references (and the G2e aura-structure demo reference) with the top-left rule before any proof; add to the plan that exact coverage ties at triangle vertices are decided by the top-left rule (ADR-0068 text), and keep `g2b1_predict.tie_sign` out of the new annex. |
| M2 | **No real-data scene contains a mirrored quad or a window origin below 0** | `mirror census`: 0 / 0 in all 5 scenes; the 16 frames of the aura animation 1 contain no mirrored image | The mirror step-back (`window = (U-mx, V-my)`), the rule the DLL owns and the one with 519 + 137 images at the clamp, is proved at pixel level only by the engine demo and unit tests. Add real-data scenes (proposals below). |
| M3 | ABR0 / ABR2 not in the real-data scenes | R2 | Add one scene or accept the synthetic proof; keep a DLL unit row for `Abr 0..3 -> Mode0..3`. |
| M4 | The demo cannot use the asset catalog | `CellularLayerComponent.ResolveTextures(Func<Guid,Texture2D>)` is a public seam | The G2e component needs the same public loader seam (the report says `Acquire<Texture>` only) so the synthetic 512 x 1105 texture can be handed to it. |
| M5 | Guard wording | R3 | Allow null sheet ids in the guard (71 documents). |

Proposed real-data scenes for M2 / M3 (hand-placed with the public DLL calls `SpawnPlaced`, `SetAnimation`, `Create`; my digests, top-left rule, background (24,40,56), scroll (800,-8), anchor (160,119) as the aura; **unverified against the DLL**, they are my model's values):
- `476 effect 0, animation 3 (Loop), frame 2`: 14 quads, 3 mirrored, 13 scaled, ABR1 + opaque, no origin below 0: k=1 11 787 lit `44417c5ee8e73983d9f531c55af1a58ac79f4cf9`; k=3 106 203 lit `b9438a7e5a91a16d13f8d60fa5ff08e2db88363e`
  (frame 10: 8 quads, 2 mirrored: k=1 8 403 lit `a8701ce495f83f98773b7f9deddbea5d7cb98a75`, k=3 75 591 lit `c21e5781f3c00136a51a54ea34cd69a13612f435`). Reachable with `SpawnPlaced(0,960,160,48)` then `SetAnimation(0,3)`.
- `map 327 effect 0, animation 3, frame 3` (Arena Melzas 1): 3 quads, all mirrored, one at window origin -1 (U=0), ABR1: k=1 514 lit `b39feb41c72bd285b4ec11c585dbc6438d7bfac2`; k=3 4 626 lit `d5e110bfc89e3b53e787ba17934fadb82aeecc77`. Reachable with `Reservoir.Create(true, 0, 3, ...)`.
- For ABR0 / ABR2: map 378 effect 1 (390 ABR2 images, 195 mirrored and scaled, 88 at origin 0) or map 7 effect 1 (188 ABR0 images, 83 mirrored): the cheapest way to cover M2 and M3 together; pick one frame the same way.

## 4. Unconfirmed items (not decidable here)

- What the engine's GPU draws at the exact tie pixels of the aura: analytic (top-left, supported by R and ADR-0068), not measured; first measurable at the G2e demo with the aura-structured quad.
- The real GPU interpolator's error on the 1105-tall sheet (my best-case float32 simulation shows 0 failure; a real rasterizer may cost a few ulps of the normalized coordinate = 6.6e-5 texel each against a margin of 2.44e-4).
- The residual of D-E19-92 at k=3: I get 11 473 / 12 141 / 11 384 pixels = 12.6 % / 13.9 % / 16.1 % of lit (report: 11 756 / 12 236 / 11 616 = 12.7 % / 13.8 % / 16.1 %); the percentages agree within 0.1 point, the absolute counts differ by 100-300 (different counting basis, not decision-carrying).
- The hardware residual of the walker R itself (49 hardware probes of the G2b annex) was taken as given.
- Feasibility of the out-of-repo capture harness (HintPath, junction of the export, `GetBackBufferData`) and the camera `Zoom = k` convention: not run.
- The cell / texel mapping between the original VRAM and the exported sheets (`raw = cell - 1` on a mirrored axis): consistent with `docs/formats/effects.md`, the memory note and G2b-3, not re-derived from the VRAM.

## 5. Nits (no plan change)

- N1 `ComputeEntityElevation((int)DepthKey, 0)` has no lower clamp while the report writes `clamp(...,0,59)`: the binary has none either (my fuzz shows a negative key indexes below the OT) and real keys are non-negative; harmless.
- N2 Passing `DepthKey` as `posY` with `idsv = 0` is correct because the stored key already contains `idsv << 16`.
- N3 The effect coordinate `100*ay` omits IDSV and orders by (y-z) while the binary orders a row by the full key (y + idsv, then z): effect-vs-effect order inside one row can differ when IDSV or z differ (the report already files the entity-side deviation; 391 and the aura are unaffected: distinct rows or one element).
- N4 `PushFrame` reads the live slot x/y/z, the binary pushes render copies taken at the last update: they differ only after an operation applied at the end of a frame without a following update (rare, one frame early at most).
- N5 `RunFrame` allocates per frame (LINQ `OrderBy`/`ToList`), the report's remark about `ActiveSlots` is right and the same applies to existing code.

## 6. Probes in this folder

`emit_fuzz.py` (emitter, 2500 hand-built elements), `sort_check.py` (real `0x8002E130`, order and ties), `tie391.py` (391 tie on the real machine), `slots_check.py` (real slot positions and keys), `scan_images.py` (census),
`my_render.py` (independent renderer, rule B(k) with the top-left tie rule), `redux_vertex.py`, `mine_vs_r.py`, `cov_stat.py` (coverage rules against the hardware walker), `tie_gpu.py` (G2b-1 demo cases and its real GPU dumps: no pixel distinguishes the rules),
`tie_count.py` (ties and disagreements per scene and factor), `dbg_pix.py`, `mirror_scenes.py` (proposed real mirrored scenes), `f32_tie.py` (float32 best case), `scan_list.py`, `scan_prim.py`, `scan_tmpl.py`, `mipsdis.py` (disassembly helpers).
Reference images of my renders: `my_<scene>_k<k>.png`; digests: `my_digests_*.json`.
