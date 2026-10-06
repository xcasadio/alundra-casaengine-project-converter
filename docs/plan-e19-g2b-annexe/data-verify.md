# E19.g G2b - counter-check of the "data" surface report (read-only, 2026-10-06)

Everything below was re-derived from sources: the binary (capstone + the MIPS interpreter), `data-extracted/data/map_*.json`, the analyser and
converter sources, the engine sources, and (new) the PCSX-Redux checkout at `D:/development/repo/pcsx-redux`. The report's outputs were not reused.
Scripts and outputs are in this folder (`extract.py` -> `banks.pkl`, `census.py`, `slots*.py`, `model.py`, `compare.py`, `redux.py`,
`full_models.py` -> `full_models.txt`, `overshoot.py`, `bias_exact.py`, `emu_check.py`, `t_phase17_quads.py`, `t_phase18.py`, ...).
Legend: **[B]** binary, **[D]** data/sources, **[H]** hypothesis.

## Verdict table (per claim)

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 1 | `0x8002DB48` is the emitter for entity and effect images; its single caller is `0x8002E1D4` | CONFIRMED [B] | jal/j scan of the whole 174 592-word text plus lui/addiu/ori and data-word scans: one `jal` at `0x8002E1D4` (`xref.py`) |
| 2 | one POLY_FT4 per 14-byte image, code `0x2C`, `\|1` raw, `\|2` semi; flags bit 3 semi, bits 4-5 ABR | CONFIRMED [B] | disassembly `0x8002DC28-0x8002DE20`; `SetPolyFT4 0x800843B0` stores `0x2C`, `SetShadeTex 0x80084324` ors 1; `andi 8` / `andi 0x30; sra 4` at `0x8002DC30-0x8002DC58` |
| 3 | corners = bytes 6-13 signed, TL,TR,BL,BR, + integer anchor; UVs `(u,v),(u+w,v),(u,v+h),(u+w,v+h)` | CONFIRMED [B] | stores at `0x8002DCAC-0x8002DDEC` (vertex k at prim `+8,+0x10,+0x18,+0x20`; UV at `+0xC,+0x14,+0x1C,+0x24`) |
| 4 | no flip, rotation or scale field | CONFIRMED [B] | the routine reads bytes 0-13 of the image and nothing else |
| 5 | interpreter proof, 160 355 quads, 0 differences | CONFIRMED | own harness `emu_check.py` (real routine, 51 746 frames): 160 355 / 160 355 equal on vertices, UVs, code bit 1; also u+w, v+h <= 255 on every quad, all `|coord|` <= 128 |
| 6 | two triangles (TL,TR,BL),(TR,BL,BR): diagonal TR-BL; engine index buffer is TL-BR | CONFIRMED | second source: PCSX-Redux `gpu.cc` quad split `(1,3,2)+(0,1,2)`; engine `SpriteRendererComponent.cs:502-508,150`: slots TL,TR,BR,BL, indices `{0,1,2,0,2,3}` |
| 7 | pixels sampled at integer points, right/bottom excluded | CONFIRMED for **coverage**, REFUTED as a texel rule (see C1) | my exact top-left model vs the Redux walker port: coverage identical on all 91 778 283 deformed pixels (0 differences) |
| 8 | `SiImage` `Sx+1` shift; all 111 007 unscaled quads inside their cell | CONFIRMED [D] | `SourceX/Y` equal the rule on all 160 355 quads; 0 unscaled pixels outside the cell. Unscaled quads cannot discriminate the sampling phase (C1) |
| 9 | census 395 / 9 620 / 160 355; classes 111 007 / 40 763 / 6 397 / 2 188; 49 348 deformed (30.77 %) | CONFIRMED | `census.py`, independent classifier |
| 10 | 33 341 deformed refs semi, ABR1 32 634, ABR0 659, ABR3 48 | CONFIRMED | `Spritesheet` byte bits 3 and 4-5 |
| 11 | 1 923 animations, 16 533 frames, 6 432 deformed slots, 3 928 mixed, 68 970 keyframes | CONFIRMED with a caveat (C2) | 68 970 = visible frames of deformed slots; excludes the terminal-repeat keyframes |
| 12 | "4 843 of the 6 432 slots change their corners"; 1 639 animations | CORRECTED (C3) | reproduces only as "normalised affine matrix changes among the deformed frames" |
| 13 | 7 697 `.anim2d` untouched; 1 923 paths, all under `Entities` | CONFIRMED | all 9 620 `bank<key>_anim<i>_<dir>.anim2d` exist exactly once; my 1 923 paths equal the report's TSV |
| 14 | magnitude max 90.4 / p50 7.2 / p90 30.7 / p99 56.3; scale 0.026-9.9 (x), 8.0 (y); 36 tiny rects | CONFIRMED | `magnitude.py` (mirror-aware matching; without mirror-awareness the max is 118.7) |
| 15 | 162 of 395 banks (46 + 116) | CONFIRMED | |
| 16 | "224 of 483 maps load at least one" | CONFIRMED as defined, incomplete (C4) | 224 = maps whose own `SpriteRecords` hold a deformed native bank; 278 maps (270 enabled) spawn an entity record of a deformed bank |
| 17 | story chain: 56 banks, 3 deformed: `alundra_0` (92/376 anims, 25 ids), `170` on 476 (12/16, all ABR1, 1 416 + 204 + 152), `alundra_227` (4/12, 64 refs) on 163/170/177/184 | CONFIRMED | `chain.py`, `chain2.py`; the 25 ids equal the report's list; hero records appear in 164/165/172/179 only (the player is spawned, "every map" is inference) |
| 18 | today's path + corners wrong in 66.7 / 78.4 / 77.1 % of pixels; other diagonal 23.6 % of general pixels; unscaled exact | CONFIRMED relative to the report's own model; see C1 | `compare_full.txt` reproduces 98.4 %/66.7 %, 100 %/78.4 %, 100 %/77.1 %, 79.8 %, 23.60 % exactly |
| 19 | "equivalent GPU quad = PSX quad +0.5 px, TR-BL, UVs unchanged, bias 1/4096; exact for all scaled rects; <= 0.006 % elsewhere; 1/256 too large (0.6 %)" | numbers CONFIRMED, rule REFUTED (C1) | `bias_exact.py`: flips 0 / 0.0063 % / 0.0051 % at 1/4096, 0 / 0.54 % / 0.61 % at 1/256; exact-integer-u pixels 36.15 / 2.33 / 0.56 % |
| 20 | 8 096 refs / 265 992 px (0.29 %) read one texel beyond the cell; 1 px gutter makes it harmless (0.011 % wrong against true VRAM) | CONFIRMED in magnitude | `overshoot.py` + own VRAM rebuild (`vram.py`, validated 0 mismatch on 1.1 M texels / 1 080 quads): 8 096 / 265 992 reproduced; visible-wrong against true VRAM 8 489 px (0.0092 %), not 9 767 |
| 21 | S1-S6 corners, sizes, classes, flags | CONFIRMED | `samples_check.py` (covered pixel counts 1 444 / 1 120 / 1 820 / 245 / 1 344 / 544 also reproduced); S3: 3 px outside the cell, S5: 32 px outside, but all 32 true-VRAM texels are transparent (report: "9 differ in colour": not reproduced) |
| 22 | converter site `SpriteWriter` loop `:582-661`; sampler `:225-263`; inspector `:2057-2142`, `:2482`; engine ADR 0057, parent ADR 0039 | line numbers drifted, list incomplete (C5, C6) | see below |
| 23 | the real GPU's fixed-point interpolation and tie rule are "not established" | PARTLY RESOLVED (C1) | coverage tie rule: reproduced; fixed point: reproducible by the Redux port |
| 24 | open question A vs C | see C7 | |

## C1. The texel phase is wrong: the PSX samples `floor(u + 0.5)` at integer pixels (REFUTES "PSX-exact", all reference values)

Facts [D, third-party hardware captures stored in this machine, not captured by us]:
- `D:/development/repo/pcsx-redux/src/gpu/soft/polys.cc:513-525` (`beginSpan`): "Hardware affine UV sampler model (phase-17/18/19/20 verified on SCPH-5501):
  at integer pixel x, the sampled texel is floor((accum_16_16 + 0x8000) >> 16)", with vertex `u = in.u << 16`: a **constant +0.5 texel**, not scaled by the stride.
- `src/mips/tests/gpu-raster-phase17/raster-expected-phase17.h` (55 HW_VERIFIED) and `phase18` (31): "u_sampled = floor(u_real + 0.5)".
- I ported the Redux walker (`redux.py`: setupSections3, left/right sections, truncated 16.16 steps, the `+0x8000`, the quad split) and ran it on the captured vectors:
  **phase-18 triangle sweep: 30/30 equal to the hardware values (without the bias: 13/30 wrong); phase-17 quad battery Q1-Q5 (the shapes Alundra uses: trapezoid, skewed non-parallelogram, 90 degree twist, compression): 19/19 non-sentinel probes equal to the hardware values; the report floor model: 9 of those 19 wrong**
  (`t_phase18.py`, `t_phase17_quads.py`).
- Unscaled quads cannot discriminate: floor(u) and floor(u + 0.5) coincide when the stride is +-1 (4 000 random unscaled quads: today's path = floor model = Redux port, 0 differences). That is why every data-side check in the report passes.

Impact over the 49 348 deformed refs (91 778 283 px, `full_models.txt`, Redux port R vs the report's floor model F vs exact-rational `floor(u+1/2)` B):
- texel differs R/F: 66.0 % of pixels (64.9 / 74.2 / 74.9 % by class); **RGBA colour differs: 13.8 % of pixels** (13.0 / 19.2 / 19.1 %), opaque/transparent flips 3.9 %.
- the six samples: S1 17 %, S2 20 %, S3 4 %, S4 15 %, S5 20 %, S6 4 % of the covered pixels change colour (texel index 49-76 %).
- so `S1..S6`, `D1..D5`, every "wrong implementation" pixel count and the 0.006 % are expected values of a model that is not the PSX for scaled/deformed quads. They must be regenerated with the rule below.
- Corrected rule for an equivalent GPU quad: polygon moved +0.5 px (so the pixel-centre sample is the PSX integer sample point) **and UV +0.5 texel** (cell UV minus the cell shift on a mirrored axis, plus 0.5), then nearest `floor` with a small positive epsilon (exact `.5` ties exist: 2.95 % of scaled-rect pixels, and they round up). For an unscaled quad this reduces exactly to today's path (sample `p + 0.5`), so there is one rule for all quads and the unscaled ones stay bit-identical.
- Precision: B (exact + epsilon) differs from the hardware walker (R) in 0.56 % of texels / 0.12 % of colours (the hardware truncates the 16.16 gradients and the left-edge steps). A float shader cannot be tighter than that; the plan's tolerance should be about 0.15 % of colours, not 0.006 %, or the oracle should be the Redux port itself (`redux.py`, validated on 49 captured probes).
- epsilon: exact flips at 1/4096 under the corrected rule: 0 (scaled rect), 0.0144 % (parallelogram), 0.0119 % (general); at 1/256: 0.029 / 0.65 / 0.70 %.
- Coverage: the tie rule is settled: Redux coverage = exact top-left (perturbed sample) coverage on every one of the 91.8 M deformed pixels.
- Overshoot under the corrected rule: 8 978 refs, 379 977 px (0.414 %) outside the cell; against the true VRAM only 3 235 px (0.0035 %) would show a non-transparent texel the sheet gutter hides. The conclusion "no extractor change, no clamp" survives. The gutter is 1 px right and below each cell only (`GameMapHelper.cs:280`, cell `w+1` x `h+1`); a mirrored axis overshoots on the left/top, which is the previous cell's gutter, or the sheet edge (x = 0, y = 0) where `PointClamp` repeats the cell's own edge texel.

## C2. Keyframe guard

68 970 keyframes counts the visible frames of the 6 432 deformed slots. `SpriteWriter` also writes a terminal-repeat keyframe (re-emits the last displayed frame at the terminator time) in every Hold/Chain animation (2 657 + 1 756 of 9 620): on deformed slots that is **2 894 more** (1 791 of them deformed quads; 1 200 animations), i.e. **71 864** position-style keyframes. A guard "1 923 / 68 970" fails on the first implementation that mirrors the Position track; the plan must say which.

## C3. "Change their corners"

4 843 slots / 1 639 animations reproduce only for "the matrix (TR-TL)/W, (BL-TL)/H, (BR-TL) changes among the deformed frames of the slot". As stored corners (what a corner track holds) vary in 5 818 slots (all frames) or 5 227 (deformed frames only); relative to TL 5 602; 1 712 animations have such a slot.

## C4. Maps

224 counts only native banks listed in a map's own `SpriteRecords`. By entity records (the spawn path): 278 maps, 270 with an enabled record. The global `alundra_*` banks are in every map's reach.

## C5. Export side

`SpriteWriter.ConvertAnimation`: the per-part loop is `:570-683` at HEAD `94e0a46` (the report's `:582-661` drifted); the tracks are emitted by property (`Sprite`, `Position`, `Visible`, flips only when used). Mixed slots (3 928) have plain frames: a corner track needs a "no corners" keyframe or corners on all visible frames. `.anim2d` is 96.12 MiB (= 100.8 MB) as stated.

## C6. Engine sites (outside my surface, found while checking)

The sampler `switch (track.Property)` is at `:223` (Rotation case `:263`), and the engine already has a `Rotation` property (Step). A new property mirrors Rotation's footprint: `Animation2dCompositionAdapter`, `Animation2dData`, `Animation2dTrackData` (save/load), `AnimationAssetDataConverter`, `AnimationClipAsset` (3 sites), the sampler, `Animation2dTimelineControl`, **about ten switch sites in `Animation2dAssetInspectorPanel.cs`** (`:2142, 2500, 2732, 2803, 2866, 3219, 3932, 4080, 4292, 4569`), and tests (`Animation2dAuthoringDataTests` 8 refs, `AuthoringAssetJsonSerializerTests` 4). The "inspector `:2057-2142` and `:2482`" list is incomplete. ADR numbers: engine last = 0056 (branch `chantier/e19g2d-overlay-blend`), parent last = 0038: next 0057 / 0039, but provisional (other executors on f3b/f4a/f4b may take 0039+; a past collision on 0039 is in the session memory).

## C7. A vs C

D-E19-60 (`docs/plan-e19-opcodes.md:209`): "tous les pixels restent egaux" (the 320x240 image grows by an integer factor, every pixel stays equal). Option A (corners x k, centre sampling) cannot keep each PSX pixel a uniform k x k block on a deformed quad (a texel boundary inside a block: about the stride fraction of the blocks, 50-90 %). So C is what D-E19-60 already implies; the open item is the engine cost, not a product choice. I did not re-derive the report's 50-90 % / 66-79 % (definition-dependent); the direction is certain.

## Missed / to add to the plan

1. The corrected texel rule (C1) and the Redux port as oracle for the S-samples and the synthetic cases; pixel tolerance about 0.15 %.
2. One rule for all quads (unscaled bit-identical), instead of "unscaled keep today's path" plus a second path.
3. The corner track and terminal-repeat count (C2).
4. Mirrored axis on a rotated or sheared quad: `IsMirroredX = X1 > X2` also fires on quads that are not mirrored; the cell is then shifted +1 although the quad reads `[Sx, Sx+W]` (the overshoot of C1 counts these).
5. `Alundra` DLL never scales or flips entity sprites at runtime (grep of `Alundra/`), so a corner track needs no composition with a component-level flip.

## Not verified (no claim made)

Engine-surface facts (shader plumbing, `SnapToPixel`); the +30 MB estimate [H] (format-dependent); the report's `samples/` JSONs and PNGs (built on the floor model); the effect-quad figures (191 / 1 577) of the earlier discovery; zoom blocks (C7).
