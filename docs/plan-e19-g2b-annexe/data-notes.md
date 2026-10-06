# E19.g G2b discovery - data and binary surface (read-only, 2026-10-06)

Scope: free four-vertex quads and a corner track in `.anim2d` for ENTITY sprites (D-E19-52). Surface = the binary `ALUN_CD.EXE` (France) and
the data (`data-extracted/data/map_*.json`, `alundra-project/Sprites/Textures/*.png`). Nothing was built, run, exported, edited, staged or
committed inside the repository; everything is under this folder. Binary helpers: `e19j-disc/lib.py` (capstone), `f3-disc/binary/emu.py`
(MIPS interpreter). Rules for reading: **[B]** = verified on the binary (address), **[D]** = verified on the data, **[S]** = from PSX
documentation (psx-spx), not established on this machine, **[H]** = hypothesis.

Folders: `binary/` (disassembly, interpreter proof), `data/` (extraction, censuses, rasteriser, sample generators, `*.txt` outputs),
`samples/` (6 real quads + 5 synthetic cases: JSON, reference PNGs, wrong-implementation PNGs), `samples/synthetic/`.

## 1. How the binary draws an entity sprite frame [B]

- Entities and effects share ONE routine. Entities push their list element (`entity+0x194`) at `0x8003B4A4-0x8003B4FC` (fields copied: x
  `+0x114`, y `+0x118`, z `+0x11C`, depth key `+0x1BC` -> element `+4`, `+8`, `+0xC`, `+0x10`); effects push `slot+0x0C` (`0x8003C4B8`).
  `0x8002E130` sorts the list then calls `0x8002DB48` once per element; **`0x8002DB48` has a single caller (`0x8002E1D4`)** (jal scan of the
  whole text, `binary/xref2.py`). `binary/rou_DC28_DE50.txt` and `rou_E0E0.txt` hold the disassembly.
- Per element: screen x = `half(elem+6) - camX` (`0x80126F10`), screen y = `((elem+8 - elem+0xC) >> 16) - camY` (`0x80126F14`). The element is
  dropped (no prim) unless `x+128 < 0x241` and `y+128 < 0x1F1` (x in [-128, 448], y in [-128, 368], tested on the ANCHOR only).
  Integer anchor, no sub-pixel position.
- Per image (14 bytes, image set = depth byte, count byte, then the images; count `elem+0x18`, page base `elem+0x1C`, CLUT base `elem+0x20`):
  byte 0 flags (bits 0-2 page offset, bit 3 semi, bits 4-5 ABR), byte 1 CLUT offset, 2-3 `u,v`, 4-5 `w,h`, 6-13 four SIGNED-byte vertices
  `(x_i, y_i)`, i = 0..3 in the order **TL, TR, BL, BR of the texture rectangle** (`0x8002DC28-0x8002DDEC`).
- Each image becomes ONE `POLY_FT4` (0x28 bytes): code `0x2C` (`SetPolyFT4 0x800843B0`, prim init loop `0x8002DEA0`) `| 1` raw texture
  (`SetShadeTex(1)`, `0x80084324`, `0x8002DEAC`: no colour modulation) `| 2` when flags bit 3 (semi); tpage = table `0x800DC4F0` row
  `(semi ? flags>>4 & 3 : 0)`, 22 halfwords per row, index `pageBase + (flags & 7)`; CLUT = table `0x800CA0F0` index `clutBase + byte1`.
  Vertex i = `(screenX + sbyte(x_i), screenY + sbyte(y_i))` (16-bit stores, no clamp, no scale). UV i = `(u,v), (u+w,v), (u,v+h), (u+w,v+h)`.
- **There is no flip, rotation or scale field anywhere**: mirroring, 90-degree turns, scaling, rotation, shear and perspective-like shapes
  all live in the 8 vertex bytes. A "rectangle" is just the special case TL,TR,BL,BR = an axis-aligned rectangle of the source size.
- **Proof run** (`binary/validate_poly.py`): the REAL routine, run in the MIPS interpreter on every entity quad of the extraction (160 355),
  writes prims whose vertices equal anchor + sbyte for all 4 corners, whose UVs equal the formula above, and whose code bit 1 equals flags
  bit 3: **160 355 / 160 355, 0 differences**. No quad has `u+w > 255` or `v+h > 255` (no byte wrap). All |coordinates| <= 128.
- Draw order inside a frame: images are submitted 0..N-1 and each is inserted at the HEAD of its ordering-table slot, so image 0 is drawn
  last (frontmost): unchanged from what `SpriteWriter` already encodes (DrawOrder). Not part of G2b.

## 2. GPU rules that matter for an exact port [S] plus the evidence the data gives

- [S] psx-spx: a quad is drawn as TWO triangles, `(v1,v2,v3)` then `(v2,v3,v4)`: with the vertex order above, triangles `(TL,TR,BL)` and
  `(TR,BL,BR)`, **diagonal TR-BL**. The engine's index buffer is `{0,1,2, 0,2,3}` on slots TL,TR,BR,BL = diagonal **TL-BR** (G2 notes). The
  split changes nothing for rectangles and parallelograms (affine map is global) but changes **23.6 % of the pixels of every one of the
  2188 general quads** (`data/split_impact.txt`, A).
- [S] affine (no perspective) per triangle; no culling by winding (mirrored quads are normal).
- [D] **integer sample points, right/bottom exclusive.** `SiImage.cs` records that a mirrored quad names its source one texel early
  (`SourceX = Sx+1`; 177 564 of 208 830 mirrored quads have an unmirrored twin at exactly `Sx+1`, 514 of 532 crops pixel-identical). That is
  exactly what a rasteriser that samples `u` at the pixel's integer coordinate (not its centre) with a left/top-inclusive fill rule does: the
  leftmost pixel of a mirrored quad reads `u+w`, the rightmost `u+1`. My rasteriser (`data/psxraster.py`, unit test `test_raster.py`)
  reproduces it, and over all 111 007 unscaled quads (mirrored included) every sampled texel lies inside the exported cell `[SourceX,
  SourceX+W) x [SourceY, SourceY+H)` (`data/outofcell.py`: 0 outside). This is also why a GPU that samples at pixel centres gets every scaled
  or deformed quad wrong by up to one texel (section 5).
- [H/S] the real GPU's fixed-point interpolation (and its tie rule on slanted edges) is not reproduced bit-for-bit by an exact rational
  model; no emulator or hardware capture was available here. See "Not established".

## 3. Census over the extraction [D] (`data/census*.txt`, `chain_tracks.txt`, `predicted.py`)

Replicates `SpriteBankReader.ReadAllBanks` exactly: **395 banks, 9 620 animations, 61 366 frames, 160 355 quad references** (the numbers of the
earlier G2 discovery, recounted). Classes (corners vs the source size W x H):

| class | refs | share | note |
|---|---:|---:|---|
| `rect_src` (axis-aligned rectangle, size W x H, mirrors allowed) | 111 007 | 69.2 % | what the converter draws today |
| `rect_scaled` (axis-aligned rectangle, other size) | 40 763 | 25.4 % | scale x 0.026..9.9, y 0.026..8.0; 28 259 downscaled, 12 504 upscaled only, 33 353 scaled on both axes |
| `parallelogram` | 6 397 | 4.0 % | 4 748 rotations (orthogonal axes, maybe scaled), 751 pure axis-aligned transposes (90/270 degrees), 898 sheared |
| `general` | 2 188 | 1.4 % | all convex (no concave or crossing quad), perspective-like |

Deformed = everything but `rect_src`: **49 348 refs (30.77 %)**; non-rectangular 8 585 (5.35 %). Mirrors: X1 > X2 on 30 540 and Y1 > Y3 on
7 742 refs (both on 4 145), in all classes. No quad is empty; 36 scaled rectangles cover < 4 pixels. Semi: 33 341 deformed refs are semi (ABR1 32 634, ABR0 659,
ABR3 48), 16 007 are opaque.

- **Animations**: 1 923 of 9 620 hold a deformed quad (1 811 with a scaled rectangle, 405 with a parallelogram or general quad, 243 with a
  general quad); 16 533 of 61 366 frames; 6 432 part slots are ever deformed, of which **4 843 change their affine over the frames (the
  "corner track")** and 1 639 of the 1 923 animations contain at least one such slot (284 animations: constant deformation). **3 928 of the
  6 432 slots mix deformed and plain frames**, so a track needs an explicit "no corners" keyframe or corners on every frame of the slot
  (68 970 keyframes in that case, 49 348 of them deformed). The 7 697 other `.anim2d` are untouched.
- **Magnitude** (max distance of a corner to the matching corner of the unscaled source-size rectangle centred on the quad's centroid, px):
  max **90.4** (`alundra_21` Magie de feu Niv.2, scaled), p50 7.2, p90 30.7, p99 56.3; by class max 90.4 / 72.1 / 65.7 (scaled / parallelogram
  / general).
- **Where**: 162 of 395 banks (46 of the 151 global `alundra_*` banks, 116 of 244 native banks), 224 of 483 maps load at least one. Top banks:
  `156` Wilda (head) 2 672 + 1 144 + 608 refs, `alundra_0` (the hero) 2 110 + 1 241 + 348, `221` suction effect, `104`, `170` Rancune de
  Melzas, `223` Dragon, `213`, `196/197` P-Zoldia, `253` Zorgia (`data/deformed_banks.tsv`).
- **Hero** (`alundra_0`, animation id = AnimSet index): 92 of 376 animations, 25 animation ids (mine-cart ones have 2 directions): SprintDash 0x04, SwimmingSlow 0x0F, ChargeAttack
  Sword/FlailSteel 0x18/0x19, mine cart 0x1A/0x1B/0x1E/0x37, EnterSand/ExitSand/InSand/InSandMoving/InSandDash 0x20-0x24, SwimmingDash 0x28,
  PrepareSprint 0x29, StopSprint 0x2A, spell cast 0x32-0x34, 0x39, 0x3C, ChargedAttackFlailShortIron 0x48, 0x57, **StartVictoryPose 0x5B
  and VictoryPoseShine 0x5D** (names from `PlayerAnimation.cs`; that they play when an item is lifted is a hypothesis). Idle, walk, jump and the usual sword attacks are NOT deformed.
- **Story chain (the 30 maps of `AlundraStoryChainOpcodeAudit.ChainMaps`)**: banks referenced by entity records (SpriteDirection bit 0x80
  selects local vs global table, as `EntityPrefabLinkWriter`): 56 distinct banks, **3 with deformed quads**: `alundra_0` (the hero, every
  map), **`170` Rancune de Melzas on map 476** (12 of 16 animations, 1 416 scaled + 204 parallelogram + 152 general refs, all semi additive) and
  **`alundra_227` Coffre d'Anzes on 163, 170, 177, 184** (opening animation, 4 of 12 animations, 64 scaled refs, all semi additive). No other
  chain bank. (Effect banks - the 476 aura, 391 pieces, rais de la 163 - are a different surface: G1/G3, same emitter.)
- **Corner tracks of the chain banks** (`chain_tracks.txt`): Melzas set 0 (14 frames, 14 deformed slots, 12 animated, all four directions
  identical), set 1 (9 frames, 8 animated), set 2 (29 frames, 11 animated); chest set 1 (14 frames, 10 deformed slots, 3 animated).

## 4. Reference rasteriser and what it says about the port [D]+[S] (`data/split_impact2.txt`, `ambiguity.txt`)

Models, all against the exact-rational PSX model P (integer samples, top-left, TR-BL, affine, floor):

| model | rect_src | rect_scaled | parallelogram | general |
|---|---|---|---|---|
| N1 "add corners to today's path": TL-BR, pixel centres, cell UV | exact (0) | **98.4 % of refs, 66.7 % of pixels wrong** | 100 % / 78.4 % | 100 % / 77.1 % |
| N2 = N1 with the PSX split | exact | same | same | 100 % / 79.8 % |
| **D = geometry shifted by +0.5 px, PSX split TR-BL, UV in PSX coordinates (cell UV minus the cell shift), no bias, float64** | exact | exact | exact | exact |
| D, float32 plane-equation attributes, bias 0 | exact | 3.1 % of refs, 0.03 % px | 25 % / 0.05 % | 47 % / 0.07 % |
| D, float32, **bias 1/4096** | exact | **exact** | 3.8 % of refs, **0.006 % px** | 9.9 %, **0.005 % px** |
| D, float32, bias 1/256 | exact | exact | 49 % / 0.54 % | 65 % / 0.61 % |

Reading: (1) the half-pixel is the whole story: a GPU samples at the pixel centre, the PSX at the pixel corner, so the equivalent GPU quad is
the PSX quad moved by +0.5 px in x and y (coverage and interpolation then coincide) with the UVs unchanged; (2) after the shift, unscaled
quads sample EXACTLY on texel boundaries, so a tiny positive bias on the UV (1/4096 texel) is needed in float32 (36.2 % of the scaled-rectangle
pixels, 2.3 % of the parallelogram and 0.56 % of the general ones have an exactly integer `u` or `v`; 1.1 % of the parallelogram/general
pixels are within 1/256 of a boundary, which is why 1/256 is too big and 1/4096 right); (3) unscaled quads must keep today's path (pixel
centres, mid-texel: exact, robust); (4) the diagonal and the shift are both observable by a pixel test: every wrong variant above is caught.
- **At integer zoom k > 1** (the game renders at N x 320x240 with `Zoom = N`, `IntegerFit`; D-E19-60 "tous les pixels restent egaux"): a
  resolution-independent quad drawn at k times resolution (option A: corners x k, centre sampling) differs from the PSX picture enlarged k
  times in **50-90 % of the k x k pixels** of the six samples and 66-79 % over the corpus (`zoom_effect.txt`; always within one PSX pixel: a
  texel boundary at a slightly different place, silhouettes finer). To keep "all pixels equal" the quad must be evaluated per PSX pixel
  (option C: the pixel shader floors the screen position to the PSX pixel, evaluates the two triangles' edge and UV plane equations at that
  integer point, discards outside). The numbers of D apply to C unchanged (C evaluates at the integer sample directly: no shift, no bias needed
  beyond float rounding of the quotient; add 0.5 to the integer numerator before dividing).

**Texels outside the exported cell** (`outofcell*.txt`, `price_clamp.txt`): the cell is exactly `[SourceX,SourceX+W) x [SourceY,SourceY+H)` and
is exact for all 111 007 unscaled refs. For deformed quads the PSX reads at most **one texel beyond** it: 8 096 of 49 348 deformed refs
(2 691 mirrored scaled-up rectangles on the low side; 3 958 parallelograms, 1 447 general quads on the high side if unmirrored or the low side
if mirrored); 265 992 pixels (0.29 % of the 91.8 M deformed pixels). The Compact sheet puts a 1-pixel transparent gutter between cells
(`GameMapHelper.cs:276`), so reading beyond the cell mostly reads transparent: against the TRUE VRAM texel (rebuilt from `DATAS.BIN`,
`true_vram.py`, identical to the exported sheet on a checked cell) **9 767 pixels in 1 646 refs are wrong with no guard** (0.011 %), 23 102
pixels in 3 826 refs if the engine clamped to the cell. So: no extractor change and no shader clamp needed; document the deviation.
Worst real sample: S5 (32 pixels outside, 9 of them a different colour).

## 5. Reference samples [D] (`samples/`, generator `data/make_samples.py`)

Reference = P on the exported sheet cell (alpha code 255 / 128 / 0 kept, no blending), canvas = bbox of the corners, origin = the entity anchor.
Each JSON holds: bank/animset/direction/frame/image, decoded flags, S, size, atlas cell, the PSX corners (TL,TR,BL,BR, Y down) and the same
corners in the engine's local Y-up frame, today's `Position`, covered pixels, alpha histogram, reference PNG + SHA-1, 7 spot pixels
(x, y, cell u, cell v, exact fractions, RGBA), the colour/coverage pixel counts of six wrong implementations (also as PNGs), and the PNG built
from the TRUE VRAM texels.

| sample | quad | corners (PSX, TL TR BL BR) | px | what it pins |
|---|---|---|---:|---|
| S1 chest | `alundra_227` set1 dir0 frame5 img0, semi ABR1, chain 163/170/177/184 | (-20,-26) (18,-26) (-20,12) (18,12), 24x24 -> 38x38 | 1 444 | scaled up; no-shift variant wrong in 168 px |
| S2 hero sand | `alundra_0` set32 (EnterSand) frame2 img1, OPAQUE | (-18,-14) (17,-14) (-18,18) (17,18), 39x32 -> 35x32 | 1 120 | downscale on x; 244 px wrong without the half pixel |
| S3 Melzas general | `170` set0 frame9 img1, semi ABR1, chain 476 | (-16,-4) (27,5) (-24,37) (18,46), 32x31 | 1 820 | general: wrong diagonal gives another texel to 280 pixels, another colour to 16, 84 px of coverage without the shift; 3 px outside the cell |
| S4 hero victory | `alundra_0` set91 (StartVictoryPose) frame3 img0, OPAQUE | (-11,-46) (1,-54) (-3,-32) (10,-40), 16x16 | 245 | opaque general: diagonal 20 colours, shift 22 px coverage |
| S5 hero sand mirrored | `alundra_0` set32 frame2 img0, OPAQUE, mirrored X | (21,-18) (-21,-18) (21,14) (-21,14), 39x32 -> 42x32 | 1 344 | mirror + upscale: 32 px read the texel before the cell (9 differ) |
| S6 Melzas parallelogram | `170` set0 frame5 img0, semi ABR1, chain 476 | (-9,-2) (15,-6) (-5,20) (19,16), 32x31 | 544 | rotation + shear, 40 px of coverage without the shift |

Synthetic demo (`samples/synthetic/cases.json`, `texture_24x24.png` whose texel (tx,ty) is `(10tx+5, 10ty+5, 100, 255)` so a pixel says which
texel was read): D1 x2, D2 x0.75, D3 mirrored x1.5 (cell = columns 2..17, far column reads column 1), D4 parallelogram, D5 general trapezoid
(the other diagonal changes 389 of 540 pixels); each with full per-pixel expected (x, y, tx, ty), 6-8 probes (corners, centroid, diagonal,
uncovered pixels) and an expected PNG.

## 6. What a plan needs

**Rules.** (R-a) one quad per image, 4 corners in TL,TR,BL,BR order are the only geometry (no flip/rotation/scale); (R-b) the engine draws a
free quad with the PSX split (TR-BL), pixel-corner sampling (+0.5 px shift or per-PSX-pixel evaluation), UV = cell UV shifted by the cell
shift of the mirrored axes (`-1` texel on a mirrored axis), tiny UV bias, no culling; (R-c) unscaled quads (69.2 %) keep today's path
(bit-identical output); (R-d) converter: a part slot with at least one deformed frame gets corner keyframes (decision: corners on every
frame of the slot, or a "none" keyframe: 3 928 of 6 432 slots are mixed); store the 4 integer corners in the engine Y-up frame, relative to
the entity anchor or to the AABB centre WITHOUT today's odd-size half-pixel compensation (that compensation exists only because
`SpriteData.Origin` is integer; corner-driven drawing does not use the origin); FlipX/FlipY redundant on such slots (corner order carries
them); sprites/cells unchanged (`.sprite` keyed by signature, 6 908 unchanged); (R-e) the 1-texel overshoot is accepted and documented.

**Values writable in advance.** Corpus invariants: 395 / 9 620 / 61 366 / 160 355; classes 111 007 / 40 763 / 6 397 / 2 188; 1 923 `.anim2d`
change (paths in `data/predicted_anim2d_changes.tsv`, all found under `alundra-project/Entities`, 7 697 unchanged), 6 432 slots, 4 843
animated, 68 970 keyframes (49 348 deformed); size: the `.anim2d` set weighs 100.8 MB today, about 110-130 bytes per pretty-printed Vector2
keyframe, so roughly +30 MB for four Vector2 per keyframe [H]. Per-sample corners and spot pixels (section 5, JSON). The six wrong-implementation
pixel counts per sample are the mutation values for the tests (a test must fail for: other diagonal, no half-pixel, naive path, float32
without bias). Synthetic cases above.

**Export changes** (converter): corner track writer in `SpriteWriter` (loop at `:582-661`), `report.json` counters (deformed quads 49 348,
slots, keyframes), no sprite/texture change, no new sheet, no extractor change, no re-extraction. Export: 1 923 `.anim2d` + `report.json`
(the manifest before/after proves it); full in-place export, double export for determinism.

**Engine format change + ADR.** `.anim2d` gains a per-part corner property (4 Vector2 or four properties, Step interpolation only:
`Animation2dTrackProperty`/`Animation2dTrackData`, sampler switch `Animation2dCompositionSampler.cs:225-263`, part runtime state, bounds
calculator, editor inspector `Animation2dAssetInspectorPanel.cs:2057-2142, 2482`), a free-quad submission in `SpriteRendererComponent`
(and its per-PSX-pixel shader if option C), `AnimatedSpriteComponent.DrawComposedAnimation`. Engine ADR (next number after 0056) and parent
ADR (after 0038). The same free-quad service is what G1/G3 effects need (same emitter, 191 global + 1 577 deformed effect quads per the
earlier discovery, not re-verified here).

**Tests that would move.** None of the existing converter fixtures (`SpriteWriterTests`: all `rect_src` quads) or `Alundra.Tests`
(`HeroAnimationExportTests` only checks the file set of ids 0, 1, 54) should change; new tests: converter (real quads above -> exact keyframe values; an
animation without deformation emits no corner track), engine (free quad: the five synthetic cases, mutation counts), a back-buffer demo,
`Alundra.Tests` guard on the export (1 923 / 68 970). The G2a risk line "les quads deformes restent dessines en rectangle jusqu'a G2b" closes.

**Demo.** Synthetic cases + two real assets (S2 opaque, S3/S6 semi) captured from the back-buffer at zoom 1 AND zoom 4 (to price option A vs C),
probes from the JSON, ABR1 sample S1 for the G2a interaction (two draws per semi part: the same free quad twice, once per window).

**Recette** (author): 476 Rancune de Melzas (G2a-4 already looks at it), a chest opening on 163 (Coffre d'Anzes + the victory pose
`0x5B`/`0x5D` of the hero), sand (`0x20`) if reachable.

## 7. Not established

- Real GPU arithmetic: fixed-point UV gradients and slanted-edge tie rule are the documented top-left/affine rules, not captured on
  hardware or in an emulator here; the exact-rational model is an upper bound of fidelity. A pixel test against my reference should allow a
  small tolerance on parallelogram/general quads (measured sensitivity: 0.006 % pixels for a float32 GPU), none on scaled rectangles.
- Who writes `elem+0/+0x18/+0x1C/+0x20` (the animation tick) was not traced; irrelevant to the vertex rule.
- Which of the 25 deformed hero animation ids a chain playthrough reaches (not traced); chain reach is certain only for Melzas (476) and the
  chest (163, 170, 177, 184), plus the victory pose when an item is lifted (hypothesis).
- Whether the engine can feed a per-PSX-pixel shader cheaply (engine surface) and whether `SnapToPixel` keeps every entity anchor on an
  integer PSX pixel at all times (it is set in `AlundraEntitySpawnFactory.cs:591`, `AlundraWorldProxy.cs:1971`).

## 8. Questions

- **Open (product-adjacent)**: PSX-exact picture at any zoom (option C, the pixel shader evaluates the quad per PSX pixel; honours D-E19-60
  "all pixels stay equal") or resolution-independent quads (option A: simpler, differs by a sub-PSX-pixel texel placement in 50-90 % of
  the block pixels of deformed quads). Recommendation: C if the engine surface finds the shader parameter plumbing cheap, else A with the
  deviation written in the ADR. No other question: the 1-texel overshoot is priced (0.011 %) and accepted.
