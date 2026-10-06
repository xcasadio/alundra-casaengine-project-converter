# E19.g G2b (free four-vertex quads + corner track in .anim2d) - surface "engine, converter, DLL" - read-only, 2026-10-06

Scope read: parent repo `D:\development\repo\alundra-casaengine-project-converter` (branch `chantier/e19-suite`), engine submodule
`CasaEngineMonogame` (HEAD `33324030` = tip of `chantier/e19g2d-overlay-blend`, pinned by the parent; working tree: only the author's local
`CasaEngine.Launcher/Program.cs`, never touched), converter, DLL `Alundra/`, `data-extracted/`, `alundra-project/` (export, read only).
Nothing built, run, exported, edited, staged or committed inside the repository. Everything below under this folder.
Notation: **[F]** fact (file:line or address read), **[H]** hypothesis, **[M]** measured by a script of this folder.
Paths: `E/` = `CasaEngineMonogame/CasaEngine/`, `C/` = `alundra-casaengine-project-converter/`.

## 0. Answers in one screen

1. The engine already has its own batcher (not MonoGame `SpriteBatch`): every queue entry stores **four independent vertices**; only the
   submission API (`SpriteRendererComponent.DrawSprite`, core at `:826-887`) writes a fixed unit square and a scale/rotation/translation
   world matrix. A free quad = a new submission that writes four arbitrary corners into the same fields (WorldMatrix = translation only).
   Index buffer (`:150`, `{0,1,2, 0,2,3}`) and `FillVertices` (`:485-513`) need **no change** if the four corners are written in the slot
   order TR, BR, BL, TL (the diagonal 0-2 is then TR-BL, which is the PS1 split). Culling is the only render-state problem (section 1.4).
2. The binary draws every entity image as one POLY_FT4 with four free signed-byte vertices; data order of `X1..Y4` = FT4 vertex order
   (TL, TR, BL, BR); UV corners (Sx,Sy), (Sx+w,Sy), (Sx,Sy+h), (Sx+w,Sy+h); no scale, no rotation, no per-entity transform
   (emitter `0x8002DC28-0x8002DE20`, see 6.1). So the data's X1..Y4 are the whole deformation.
3. Today's export loses every deformation: `SpriteWriter.ConvertAnimation` keeps only the bounding-box centre (Position), `flipX = X1 > X2`,
   `flipY = Y1 > Y3`, and the crop size (`:618-657`). **[M]** 160 355 quad references in 395 banks / 9 620 `.anim2d`: plain 89 740
   (55.96 %), mirrored 21 267 (13.26 %), **axis-aligned scaled 40 763 (25.42 %)**, parallelogram 6 397 (3.99 %), general 2 188 (1.36 %);
   **1 923 `.anim2d`** hold at least one non-plain, non-mirrored quad (49 348 refs = 30.77 %).
4. Export change (predictable now): exactly **1 923 `.anim2d`** (+ `report.json`); `.sprite`, `.texture`, `.entity`, `AssetInfos.json`,
   `Data/sprite-records.json` unchanged; the other **7 697 `.anim2d` byte-identical**. List: `predicted_changes_paths.txt` (162 entity folders).
   Per file the new tracks are fully predictable (`predicted_corner_tracks_per_file.json`): 6 432 `Corners` tracks, 45 237 keyframes with the
   change-only rule (38 910 enabled + 6 327 disable), 68 970 with the every-displayed-frame rule. Growth ~17 MB (estimate [H]).
5. DLL: **nothing to change**. It never reads corners, flips, tracks or part states (section 4). Tests of the DLL do not assert on tracks.
6. Engine format change = new track property + keyframe type + JSON key; **three** serializer/cloner sites must follow (silent-loss traps,
   section 2.3). ADR number: main's last is 0055; **0056 is already claimed twice in flight** (G2d branch and `chantier/audio-modern`
   0056-0060), so pick 0061 (or ask); parent's next free is 0039 on `chantier/e19-suite`.
7. Biggest open point for the plan: the **texture sampling convention** on scaled quads (section 5): 98.4 % of the 40 763 axis-aligned
   scaled refs would show at least one different texel row/column between "GPU pixel-centre" and "PS1 integer-position" sampling; psx-spx
   does not document the position. Not decidable from the repository.

## 1. Engine: how a sprite is drawn today **[F]**

### 1.1 Entities
- `AnimatedSpriteComponent.DrawComposedAnimation` (`E/Framework/Scene/Entities/Components/AnimatedSpriteComponent.cs:507-550`): for each
  visible part in `DrawPartIndices` order: sprite from `_spriteById`, `partPosition = Position + part.Position * scale` (entity `Scale`
  multiplies the part position and the sprite size), `spriteEffects` = component `SpriteEffect` | part flips (`:565-579`), then
  - entity with `DepthSortable2DComponent` (every converted entity: `SpriteWriter.WriteEntityPrefab`): `_spriteRenderer.DrawSprite(sprite, partPosition,
    part.Rotation, scale, Color, Position.Z, sortKey, spriteEffects, sprite.SpriteData.PsxSemiTransparency)` (`:542-543`), sort key from
    `BuildPartSortKey` (`:587-597`: base key + `DrawOrder`/`SourceIndex` as `LocalSortOffset`/`StableId`);
  - otherwise the `zOrder` path (`:547-548`), opaque, no PSX mode (G2a left it out of scope).
- The DLL never sets `Color`, `Scale` or `SpriteEffect` on the sprite (grep of `Alundra/Scripts`, `Screens`, `Plugin.cs`: only a commented line
  in `AlundraEntityScriptProxy.cs:388`).

### 1.2 The batcher (`E/Framework/Application/Components/SpriteRendererComponent.cs`)
- `SpriteDisplayData` struct (`:16-38`): `TopLeft, TopRight, BottomLeft, BottomRight` (`VertexPositionTexture`), `Color`, `Texture`,
  `WorldMatrix`, `ScissorRectangle`, `SortKey`, `HasSortKey`, `BlendMode`, `AlphaMin/AlphaMax` (ADR-0051), `IgnoresDepth` (ADR-0034).
  Entries are pooled in a `Stack<SpriteDisplayData>` (`:63`, `GetSpriteDisplayData` `:889-893`): **a reused struct keeps its stale
  fields; every new field must be assigned in the one private core** (precedent test `ReusedPooledEntry_DoesNotKeepTheWindowOfItsPreviousUse`,
  `E/../CasaEngine.Tests/Rendering/SpriteRendererComponentPsxSemiTransparencyTests.cs:96-109`).
- Core `DrawSprite` (`:826-887`): UV corners from the source rectangle with flips (`:841-853`), vertex positions fixed to the unit square
  `_vertexTopLeft(-0.5, 0.5)`, `...` (`:140-143`, `:856-859`), `WorldMatrix = Transformation(scale = (sw * scaleX, sh * scaleY), rot Z, translation)` (`:862-868`).
- `FillVertices` (`:485-513`) sorts (`List.Sort`, unstable, `CompareSpriteDisplayData` `:515-531`: sort key if either has one, else
  `WorldMatrix.Translation.Z`) and writes the slots **0 = TopLeft, 1 = TopRight, 2 = BottomRight, 3 = BottomLeft**; grows the staging array
  beyond `NbSprites` (ADR-0051). Index buffer `{0,1,2, 0,2,3}` (`:150`) = triangles TL-TR-BR and TL-BR-BL: **diagonal TL-BR**.
- `Draw` (`:206-288`): per entry `DrawIndexedPrimitives(TriangleList, i * 4, 0, 2)` (`:277`); state per **contiguous run**: blend state
  (`:250-254`), depth state (`:256-260`, `IgnoresDepth`), alpha window (`:262-267`); `RasterizerState.CullCounterClockwise` for the whole
  pass (`:224`), `SamplerState.PointClamp` (`:225`). `Flush` (`:175-204`) saves and restores depth, rasterizer, sampler 0 and blend state.
- Shader `E/Content/Shaders/SpriteBatch.fx`: vertex shader `POSITION + TEXCOORD0` only, `mul(float4(pos,1), mul(World, ViewProj))`; pixel shader
  rejects raw alpha outside `AlphaWindow` then `(tex * Color).a <= 0.01`. No change needed for free quads.
- Camera: `Camera2dComponent` is orthographic (`:84` `Matrix.CreateOrthographic`) => hardware interpolation is affine (w = 1), same as the PS1.
- PSX semi-transparency of G2a: `DrawSprite(Sprite, ..., SpritePsxSemiTransparency)` (`:618-652`) and the internal `Texture2D` overload
  (`:785-803`, ADR-0053): two entries of the same key and z, opaque window (0.75;1] then STP window (0.25;0.75] with `GetPsxBlendMode`
  (`:654-662`), `Mode3` colour (64,64,64) (`:57`). A quad submission must reuse exactly this.
- MonoGame `SpriteBatch` is not used by the sprite pass and could not draw four free corners anyway; the engine's own batcher can.

### 1.3 What a free quad needs (design, not yet decided)
New submission, e.g. `DrawQuad(Texture2D texture, Rectangle sourceInTexture, Vector2 topLeft, topRight, bottomLeft, bottomRight /*corner of the
texture, world, Y up*/, Color color, float z, in RenderSortKey2D sortKey, Rectangle scissor, SpritePsxSemiTransparency mode)`:
- UVs: `sourceInTexture` corners / texture size, order TL, TR, BL, BR **fixed to the corner, never flipped**: the mirror is in the geometry
  (data: `X1 > X2`). This also keeps `SourceX/SourceY` meaning (section 5).
- Slots: write PSX v1 (TR) to slot 0, v3 (BR) to slot 1, v2 (BL) to slot 2, v0 (TL) to slot 3 => triangles (TR,BR,BL) and (TR,BL,TL) = the
  PS1 pair (v0,v1,v2)+(v1,v2,v3) [psx-spx: "Four-point polygons are ... two Three-point polygons, the first consisting of Vertices 1,2,3, and
  the second of Vertices 2,3,4", fetched 2026-10-06]. `FillVertices`, index buffer, capacity code: untouched.
- WorldMatrix = translation (centre x, y, z); vertices = offsets from the centre (keeps float precision; the sort path reads
  `WorldMatrix.Translation.Z` when no key).
- PSX mode: same two-entry rule, both entries carry the same corners.
- Both sort paths (sorted and `zOrder`) can use the same method (`hasSortKey` flag) - recommended, 2 lines, avoids dead data on entities
  without `DepthSortable2DComponent` (G2a left the `zOrder` path opaque; keep or lift: plan decision).

### 1.4 Culling (the one real render-state issue)
The pass culls counter-clockwise. With the slot order above an unmirrored quad is clockwise in Y-up (front). A **mirrored** quad (data
`X1 > X2` or `Y1 > Y3`: the 21 267 plain-mirrored refs use UV flips and stay on the old path, never affected; **12 870** deformed refs are mirrored: 10 514 scaled,
1 763 parallelograms, 593 general [M]) reverses the winding and would be culled. A self-intersecting general quad can even have
one triangle each way. Options: (a) per-entry `NoCull` flag, per contiguous run like `IgnoresDepth` (`RasterizerState.CullNone`), recommended:
zero effect on existing entries; (b) global `CullNone` for the pass (changes nothing for rotations, but a negatively scaled sprite stops being
culled); (c) per-quad reorder by signed area (swap slots 1 and 3, keeps the diagonal) - fails for bow-ties. The PS1 has no culling.

### 1.5 Bounding box / culling of entities
`World.Draw` (`E/Framework/Scene/World/World.cs:810-833`) draws only entities returned by `SpatialServices.WorldIndex.Query(frustum)`;
the index holds `entity.GetBoundingBox()` (`:806`, moved when dirty `:553-557`). `AnimatedSpriteComponent.GetBoundingBox` (`:937-949`) =
`Animation2dBoundsCalculator.TryCalculateLocalBounds` (`E/Framework/Assets/Animations/Animation2dBoundsCalculator.cs:8-41`): per part the
sprite rectangle + `Position` + `Rotation`, **no flips, no scale**. Upscaled quads exceed it: edge popping near the screen border. The calculator
should use the corners when a part has them (its only test is `CasaEngine.Tests/Animation/Animation2dAuthoringDataTests.cs:910`; the editor
panel also calls it `:522`, `:1664`). Pre-existing weakness: the dirty flag is set only when the *first visible sprite* changes (`:640-665`).

## 2. `.anim2d` format and runtime **[F]**

### 2.1 Today
- Enum `Animation2dTrackProperty { Sprite, Position, Visible, DrawOrder, FlipX, FlipY, Rotation }` (`Animation2dTrackData.cs:7-16`); one list per
  value type in `Animation2dTrackData` (`:35-45`); JSON `sprite_keyframes`, `position_keyframes`, `visible_keyframes`, `draw_order_keyframes`,
  `flip_keyframes`, `rotation_keyframes` (`:60-65`); `Interpolation` is `Step` only (`:18-21`, sampler throws otherwise `:218-221`).
- Property parsed by `Enum.Parse<T>(..., true)` (`E/Core/Serialization/JsonHelper.cs:187-190`): **an unknown property name throws on load**:
  an old engine cannot read a new file (lock-step delivery, as G2c).
- Sampler: `ApplyTracks` resets each part to its defaults every call (`RuntimeState.ApplyDefaults`) then applies each track step-wise
  (`Animation2dCompositionSampler.cs:178-196`, `ApplyTrack` `:216-272`, last key at or before the time).
- `Animation2dPartRuntimeState` (`:5-37`): `SpriteId, Position, Rotation, DrawOrder, Visible, FlipX, FlipY`, reset from `Animation2dPartData`.
- `GetDurationSeconds` (`Animation2dData.cs:78-103`) = last key time over every list: a new list **must be added** there, and its key times
  must not exceed the existing maximum or logical animation ends change (the DLL's logical clock reads `DurationSeconds`).
- Track names are **derived from the index** (`EnsureTrackNames` `:55-76`, `GetDefaultTrackName` `:368-371`) and serialized
  (`"name": "Track N"`; sample `Entities/Alundra/bankalundra_0_anim1_down.anim2d`). Inserting a track in the middle renames every later track =
  extra diff in every file. **Append the new tracks after all existing ones** (new names `Track N+1...`; nothing existing changes).

### 2.2 Proposed model (for the plan; names indicative)
- `Animation2dTrackProperty.Corners` appended at the end (numeric values of existing members unchanged; JSON uses names).
- `Animation2dQuadKeyframeData(float TimeSeconds, bool Enabled, Vector2 TopLeft, Vector2 TopRight, Vector2 BottomLeft, Vector2 BottomRight)`;
  `List<...> CornerKeyframes` in the track; JSON `corner_keyframes` `[ { time_seconds, enabled, top_left{x,y}, top_right, bottom_left, bottom_right } ]`
  written only when non-empty (so existing files re-save byte-identical through the engine serializer, which the converter uses).
- Meaning: offsets in pixels, **Y up, from the part's `Position` track value**, entity `Scale` applied like `Position`; corner = corner of the
  texture window (TL, TR, BL, BR in the data order), geometry carries the mirror. `Enabled = false` = "draw the part as a rectangle, as today"
  (initial state before the first key = disabled). Active corners **replace** the rectangle: part `Rotation` and part flips are ignored for that
  frame (the converter keeps writing the flip tracks unchanged, so old tracks stay byte-identical). Component-level `SpriteEffect` flip
  (never set by the DLL): decide (ignore + document, or swap the UV columns/rows).
- `Animation2dPartRuntimeState`: `HasCorners` + 4 `Vector2` (reset to false by `ApplyDefaults`); sampler `ApplyTrack` case `Corners`.
- Corners relative to `Position` (not absolute) keeps the existing Position track as the single place that moves a part and absorbs the
  converter's half-pixel compensation for odd crops.
- Why not scale/skew tracks: 3.99 % + 1.36 % of refs are parallelograms and genuinely general quads; four corners is the one exact encoding
  of the binary's data.

### 2.3 Sites that must follow (silent-loss traps)
| Site | Fact | Failure if forgotten |
|---|---|---|
| `Animation2dTrackData.Load` (`:47-67`) + new loader | reads each list | corners never loaded |
| `Animation2dCompositionAdapter.CloneTrack` (`:87-129`) | copies each list by hand into the immutable composition the sampler uses | **sampler never sees corners, no error** |
| `EditorAssetJsonSerializer.SaveAnimation2dTrackData` (`E/../CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:248-266`) | writes each list when non-empty (the converter writes through it) | corners not written |
| `Animation2dAssetInspectorPanel.SerializeAnimationTrack` (`CasaEngine.Editor/Controls/Animation2dAssetInspectorPanel.cs:3774-3792`) | the editor's **undo/redo snapshot** (`Document = SerializeAnimationData`, `:3633`, compared with `JToken.DeepEquals`) | **undo/redo drops the corners silently** |
| `Animation2dData.GetDurationSeconds` (`:78-103`) | max over lists | durations unchanged only if keys are on frame times (they are) |
| Editor panel/timeline switches on the property (about 15 sites, e.g. `:2110`, `:2480`, `:2720`, `:3920`, `:4033`, `:4060`, `:4703`) | all are statement switches or have `_ =>` defaults, none throws | the lane is simply not shown/edited (acceptable for G2b; document) |
| `Animation2dBoundsCalculator` | rectangle + rotation only | popping at screen edge (1.5) |
| `CasaUIAssetProvider` (UI animated images) | reads `part.Position` only | UI animations never carry corners |

### 2.4 Docs to update (engine)
`docs/engine/animation2d-composed-format-v1.md` (track table `:57-62`, sampler, "unverified" notes), `docs/engine/sprite-psx-semi-transparency.md`
(line "Quads with four free vertices are not covered (G2b of the Alundra port)" in *Limits*, and add the quad path), `docs/editor/animation2d_editor_casaengine.md`
(one line: the lane is not shown), `ai-agent/README.md` table row + `ai-agent/tasks/e19g2b-free-quads-tasks.md` (template: `e19g2d-overlay-blend-tasks.md`).

## 3. Converter **[F]**

- Reader: `SpriteQuad` already carries `X1..Y4`, `Width`, `Height`, `Spritesheet`, `Signature`, `AtlasX/Y` (`C/Readers/SpriteBankReader.cs:5-15`,
  `ReadQuad :558-578`). No reader change. (`IsMirroredX/Y` and `SourceX/Y` are not read: not needed, the atlas cell already is the SourceX window.)
- Writer: `SpriteWriter.ConvertAnimation` (`C/Writers/SpriteWriter.cs:500-692`): per part `Sprite`, `Position`, `Visible`, `FlipX`/`FlipY` (only if
  used) tracks; per frame `center = ((minX+maxX)/2 - originOffset.X, -(minY+maxY)/2 + originOffset.Y)` with `originOffset = (w/2f - w/2, h/2f - h/2)`
  (`:618-647`), `flipX = X1 > X2`, `flipY = Y1 > Y3` (`:648-649`), tracks added at `:660-671`. The `.sprite` is keyed by `(sheet, Signature)` and holds only
  the crop rectangle + origin + PSX mode (`EnsureSpriteData :911-944`): **no deformation is ever in a `.sprite`**.
- Change: after the existing loop, classify each frame's quad (plain/mirror = axis-aligned, `X1==X3, X2==X4, Y1==Y2, Y3==Y4`, `|X2-X1| == w`, `|Y3-Y1| == h`),
  compute for each part its state per displayed frame (disabled | corners), emit `CornerKeyframes` on **change only** (precedent: collision keyframes
  "emitted only when a frame's volume differs from the one currently active", README "Per-frame collision volumes"), keys on the **same float `time`** as
  the Sprite keys, hidden frames skipped, terminal repeat frame of Hold/Chain gives no key (same state), and **append the `Corners` tracks after all
  parts' tracks** (section 2.1). Parts that never have a deformed frame get nothing.
- Report counters to add (report.json changes anyway): e.g. `Sprites.QuadsDeformed` (49 348), `Sprites.CornerTracks` (6 432), `Sprites.CornerKeyframes` (45 237).
  Existing invariant `Sprites.QuadsRead == Sprites.QuadsConverted` untouched.
- `AssetVerifier` loads every `.anim2d` through `new Animation2dData().Load(...)` (`C/AssetVerifier.cs:39`): sees the new property only with the new engine.
- Docs: `SpriteWriter` class doc (`:37-40`, "becomes the part's per-frame Position instead, at the corners' geometric center" stays true, add the corner track), root
  `README.md` (a "Per-frame quad corners" paragraph next to "Per-frame collision volumes"), a parent ADR (next free **0039** on `chantier/e19-suite`; check in-flight work of
  f3b/f4a/f4b before taking it).

### 3.1 Prediction of the export, written in advance **[M]** (scripts here)
`classify_quads.py` reproduces the converter's bank dedupe (map_alundra first, maps ascending, first occurrence wins; 395 banks, 9 620 animations, 160 355 refs, all equal to
the README/plan figures) and classifies every quad:

| class | refs | % | `.anim2d` containing it |
|---|---|---|---|
| plain | 89 740 | 55.96 | 8 164 |
| axis-aligned mirror (same size) | 21 267 | 13.26 | 2 472 |
| **axis-aligned scaled** | 40 763 | 25.42 | 1 811 |
| parallelogram (rotation/shear) | 6 397 | 3.99 | 405 |
| general (trapezoid-like) | 2 188 | 1.36 | 243 |
| degenerate (zero width/height) | 0 | 0 | 0 |

Scaled quads are mostly **non-integer and mostly downscales** (`scale_ratios.py`): both axes < 1 on 22 710, both >= 1 on 12 504, mixed 5 549; most
frequent (rx, ry): (1.0, 0.8) 957, (0.5, 0.5) 908, (0.975, 0.975) 788, (0.982, 0.984) 688, (0.875, 0.875) 675; 38 520 of 40 763 have a non-(inverse-)integer ratio on at least
one axis. Parallelograms: 6 365 with both edges slanted, 32 with one axis edge.
`predict_corner_tracks.py`: **1 923 files / 6 432 `Corners` tracks / 45 237 keys (38 910 enabled, 6 327 disable)**, or 68 970 keys if every displayed frame of a deformed part
gets one; every corner offset is a multiple of 0.5 (exact in float32, 0 exceptions); per-file prediction fingerprint
`403ba8ed60b702c536aea0320b766e4718899f15` (SHA-1 of the sorted JSON of `predicted_corner_tracks_per_file.json`). Heaviest banks: 156 (4 424 deformed refs, 52 files),
`alundra_0` the hero (3 699, 92 files; sets 0, 1, 54 idle/walk/loading are all plain; set 25: 1 913 deformed refs, sets 72, 51, 4, 91, 60 next), `alundra_24` (2 424), 253, 170, 214, 220.
`predict_export_paths.py` resolves the 1 923 names to export paths (no duplicates, all present; 162 entity folders); 7 697 unchanged.
Proof sketch for the plan (G0b style): parse old and new `.anim2d`, drop the appended `Corners` tracks of the new one, canonical JSON equal to the old; the dropped tracks equal the
predicted keys (part, time, state, corners) exactly; times equal a Sprite key time of the same part; 7 697 files SHA-1 equal; second export identical outside `report.json`.
Size growth ~17 MB [H] (404 B per enabled key at the serializer's indentation, 74 B per disable key; export 1.29 GB).

### 3.2 Converter test values written in advance (relative offsets from `Position`, Y up; `corner_examples.py`)
| case (Swidth x Sheight, X1 Y1 X2 Y2 X3 Y3 X4 Y4) | class | Position | TL | TR | BL | BR |
|---|---|---|---|---|---|---|
| 16x16: -12 -20 12 -20 -12 4 12 4 (x1.5) | scaled | (0, 8) | (-12, 12) | (12, 12) | (-12, -12) | (12, -12) |
| 16x16: -8 -16 8 -16 -4 0 12 0 (shear) | parallelogram | (2, 8) | (-10, 8) | (6, 8) | (-6, -8) | (10, -8) |
| 16x16: -4 -16 4 -16 -8 0 8 0 (trapezoid) | general | (0, 8) | (-4, 8) | (4, 8) | (-8, -8) | (8, -8) |
| 16x16: 12 -20 -12 -20 12 4 -12 4 (mirrored x1.5) | scaled, flipX true kept | (0, 8) | (12, 12) | (-12, 12) | (12, -12) | (-12, -12) |
| 23x31: -11 -40 13 -38 -9 -6 13 -5 (odd, half-pixel) | general | (0.5, 23) | (-11.5, 17) | (12.5, 15) | (-9.5, -17) | (12.5, -18) |
| 16x16: -8 -16 8 -16 -8 0 8 0 | plain | no `Corners` track | | | | |
All 27 quads of the existing converter tests (`test_quads.py`) are plain rectangles (corner deltas equal Swidth/Sheight): **no existing converter test moves**.

## 4. DLL **[F]**
- No code reads `X1..Y4`, flips, part states, `Animation2dTrackData`, `RuntimeState`, `GetBoundingBox` or `SpriteRendererComponent` in `Alundra/Scripts`, `Alundra/Screens`,
  `Plugin.cs`. It selects animations by name suffix (`AlundraFrameSyncPasses.cs:472`, `Animation2dData.Name.EndsWith`) and reads durations through the engine. No `QueryEntities` by box.
- `Alundra.Tests`: `.anim2d` appears in `HeroAnimationExportTests` (file existence only) and montages building `Animation2dData` by hand (`AlundraJumpTestSupport.cs:65-69`,
  `AlundraAnimationClockDriveTests.cs:192-196`, `AlundraRepeatAnimationOpcodeTests.cs:343`: Sprite tracks only). Real-data arcs load prefabs but never draw.
  **No test moves; nothing to write on the DLL side** (G2a-R5 precedent: "rien"). Still run `Alundra.Tests` Release then Debug on the new export (acceptance of the precedents).
- The DLL reads engine durations: the new track must not extend `GetDurationSeconds` (keys on existing frame times; checked by a test on the real export for the hero set 25).

## 5. Sampling convention and UV rule (the unresolved precision point) **[F]+[H]**
- Binary UVs (emitter): corner 1 (Sx, Sy), corner 2 (Sx + Swidth, Sy), corner 3 (Sx, Sy + Sheight), corner 4 (Sx + Swidth, Sy + Sheight) - note `Sx + Swidth`, not `- 1`.
  A mirrored quad has the same UVs but corner 1 on the right: the GPU walks `Sx + w` down to `Sx + 1` => the extractor's `SourceX = Sx + 1` crop
  (`alundra-datas-analyser/.../SiImage.cs:30-46`, 177 564 of 208 830 mirrored quads have a twin at `Sx + 1`) and the atlas cell hold the **SourceX window**.
- With UVs = the SourceX window corners and GPU pixel-centre sampling, **the 1:1 cases (plain and mirrored) reproduce the hardware exactly** (the existing, validated
  path); this is what a free quad with identity-sized corners gives too (test: a corner track with 1:1 corners must equal the old path pixel for pixel).
- For scaled quads the result depends on where the texture coordinate is evaluated inside a pixel. psx-spx (fetched 2026-10-06, problemkaputt.de) says: split (1,2,3)+(2,3,4);
  affine only; polygons drawn "up to <excluding> their lower-right coordinates"; **evaluation position: not documented**. [H] usual emulator practice = at the integer
  position (pixel top-left), not at the centre. `sampling_convention.py` (exact rationals): for the 40 763 axis-aligned scaled refs, **98.4 % (40 126) differ on at least one
  texel row/column between the two conventions**, and **2 691** (mirrored and upscaled on an axis) need under the integer-position hypothesis a texel just *outside* the SourceX crop (the
  atlas has a 1 px margin, transparent).
- Consequence for the plan: pick the contract explicitly. (C) GPU-centre, SourceX window, simplest, exact for 1:1, approximate elsewhere (document, test against an
  independent reference rasteriser written in the same convention); (H) shift the UVs by half a pixel step (`u' = u - 0.5 du/dx - 0.5 du/dy`; per-triangle for general quads, i.e.
  six vertices, or one shift for the 99 % parallelogram/axis cases) plus a bias against boundary instability, and extend the atlas by the missing column for those 2 691 refs
  (extractor, G0-style re-extraction) - larger and unprovable without hardware. Recommendation: (C) now, record (H) as a measured delta and a possible follow-up.

## 6. Binary cross-check (for the engine rules; the binary surface owns the full analysis)
### 6.1 Emitter `0x8002DC28-0x8002DE20` (read in `../binary/rou_DC28_DE50.txt`, decoded here)
Record offsets (14 bytes, `+0xe` per image): `Sx` at `t0-0xb`, `Sy` `-0xa`, `Swidth` `-9`, `Sheight` `-8`, `X1,Y1..X4,Y4` from `-7` (signed bytes, `sll/sra 0x18`) each **added to the entity's screen
x (`t5`) or y (`t4`)**; primitive 0x28 bytes (`addiu a3, 0x28`): `x0,y0 = X1+base, Y1+base` at `a3-0x1a/-0x18`, `x1,y1` at `-0x12/-0x10`, `x2,y2` at `-0xa/-8`, `x3,y3` at `-2/0` (POLY_FT4 offsets 8, 0x10, 0x18, 0x20);
`u0,v0 = Sx,Sy` (`a3-0x16/-0x15`), `u1,v1 = Sx+Swidth, Sy` (`-0xe/-0xd`), `u2,v2 = Sx, Sy+Sheight` (`-6/-5`), `u3,v3 = Sx+Swidth, Sy+Sheight` (`+2/+3`).
Bit 3 of the first record byte -> semi (G2a facts). No scale, rotation or matrix in this routine. Data order of `X1..Y4` = FT4 vertex order 0..3 => PS1 triangles (v0,v1,v2) and (v1,v2,v3), diagonal v1-v2 = TR-BL.
[F] reading of the dump; the binary surface should confirm.

## 7. Precedents (structure of the slices) **[F]**
- G2a: engine branch `chantier/e19g2a-psx-semi` from `61358ac0`, plan `ai-agent/tasks/e19g2a-psx-semi-tasks.md`, commits plan / E1 behaviour / E2 capacity / E3 demos+doc / ADR-0051; converter
  `SpriteWriter.ReadPsxSemiTransparency` (`e8231cf`), parent ADR-0033; demo `CasaEngine.Demos/Demos/PsxSemiTransparency/PsxSemiTransparencyDemo.cs` + `BackBufferProbe.cs` (`GetBackBufferData`, ±1 per channel, probes at
  world positions through `Viewport.Project`), launched from `CasaEngine.Demos/` with `CASAENGINE_START_DEMO="..."` (+ `CASAENGINE_DEMO_PIXELS_PATH`, `CASAENGINE_CAPTURE_SCREENSHOT_PATH`); tests at queue level
  (`GetSpriteDatas` by reflection, `FillVertices()` internal, `AlphaWindowWriter` seam), red first with the API added without behaviour.
- G2c: branch stacked on the previous slice's tip (`chantier/e19g2c-backdrop-stp` from `a885f226`), engine ADR-0053, parent ADR-0035, delivery locked (converter + engine + DLL together), guard test on the real export.
- G2d: branch `chantier/e19g2d-overlay-blend` from `main` `ebeb81c9`, engine ADR-0056, parent ADR-0037; **the parent pins its tip `33324030`**, so G2b must be stacked on it, not on `main`.
- Parent plan text to extend: `docs/plan-e19-opcodes.md` section 1.2o, new `1.2o.5 E19.g G2b`; the G2a risk line "Les quads déformés restent dessinés en rectangle jusqu'à G2b" (`:7366`); decisions D-E19-52, D-E19-65.
- Build notes from the precedents: `CasaEngine.Tests` is not in the `.sln` (build it explicitly, `--blame-hang-timeout 300s`; baseline 2 740 tests at G2d); converter tests baseline 621; export in place with
  `Alundra.dll` lock check; manifest SHA-1 (23 741 files outside DLL/pdb/`.casaeditor/`), double export, six traces to the byte.

## 8. Engine tests: new, and which could move
New (queue level, headless): quad entry = 1 entry (no mode) / 2 entries (mode) with the same corners, same key and z; slot order TR, BR, BL, TL after `FillVertices()` (positions and UVs read slot by slot); UVs fixed to the
source rectangle corners, no flip; `NoCull` set on quad entries and **false on a reused pooled entry**; WorldMatrix translation only; sampler: `Corners` track step semantics (disabled before first key, enabled values, disable key),
`HasCorners` reset by `ApplyDefaults`; `CloneTrack` copy (load a corner track, read through the sampler); load/save round trip incl. absent list (old file byte-identical when re-saved); `GetDurationSeconds`; bounds
calculator with corners; editor snapshot `SerializeAnimationTrack` keeps the list; `DrawComposedAnimation` calls the quad path for a part with corners (seam). Demo (device): section 9.
Existing tests expected **not** to move (precedent rule: a moved assertion is a stop): `SpriteRendererComponent{BlendMode,Capacity,PsxSemiTransparency}Tests`, `AnimatedSpriteComposedSortTests`, `AnimatedSpriteWorldInitializationTests`,
`Animation2dAuthoringDataTests` (builds its 7 tracks by hand, `:543`), `Animation2dCompositionSamplerLoopWrapTests`, `AnimatedSpriteLogicalEndClockTests`, `AnimatedSpriteCollisionTimelineTests`, `CasaUIAssetProviderAnimatedImageTests`,
the layer/backdrop tests (they pass `Texture2D` overloads). Converter: none (section 3.2 last line). DLL: none.

## 9. Demo and values writable in advance
Engine demo (new class next to `PsxSemiTransparencyDemo`, own title for `CASAENGINE_START_DEMO`), one `AnimatedSpriteComponent` entity per case built in code like the G2a demo (`Animation2dData` with a `Corners` track),
a **8 x 8 "address" texture** (texel (i, j) = (32 i + 16, 32 j + 16, 128), no two texels alike), plain background, `BackBufferProbe` probes (>= 1.5 px from any edge and from the diagonal):
- x4 upscale (control, 1:1-equivalent): pixel (x, y) shows texel (x // 4, y // 4); a quad with identity-sized corners equals the old rectangle path pixel for pixel;
- mirrored X x4: the horizontally mirrored texel;
- parallelogram (both splits agree): expected from one affine map;
- **trapezoid** (the two splits disagree: `demo_expected.py` shows 6 of 6 probes differ between PS1 split TR-BL and the engine's TL-BR split, e.g. pixel (12.5, 4.5) -> texel (2, 1) = (80, 48, 128) vs (1, 1));
- one deformed quad in `Mode1` (STP texel additive, opaque texel opaque) and one in `Mode0`, reusing the G2a values (opaque (60,40,20), STP (120,80,40) on (100,150,200) -> Mode1 (220,230,240), Mode0 (110,115,120), alpha 191);
- a mirrored quad (culling): visible, i.e. `NoCull` honoured; and a control entity without `Corners`: unchanged.
`demo_expected.py` is pure arithmetic (two triangles, affine, centres at i + 0.5, floor, prints the margin to each edge); the plan copies its output as the table. A red run = the same demo on the code before the quad path
(corner track ignored: every case reads the rectangle).

## 10. Risks / unknowns for the plan
1. Sampling convention (section 5): 98.4 % of scaled refs differ at >= 1 texel row/column; not provable without hardware; psx-spx silent. Needs an explicit contract.
2. 2 691 mirrored+upscaled refs read a texel outside the crop under hypothesis (H); transparent margin today.
3. Silent-loss sites (CloneTrack, editor undo snapshot) and the pooled-struct stale field (`NoCull`).
4. Old engine + new files = load failure (`Enum.Parse`); delivery must be locked (engine + converter + export together, guard test on the real export like G2c-R4).
5. Entity bounding box does not follow the quad (edge popping), pre-existing dirty-flag weakness.
6. Visible change is large: 30.77 % of entity refs change look (scaled shadows/sprites, hero set 25 swing etc.); recette needs named animations (the hero sets above are candidates; not identified by name in this surface).
7. ADR numbers: engine 0056 claimed twice already (G2d vs audio-modern); parent 0039 may be taken by f3b/f4a/f4b.
8. The effects (G1/G3) will need the same quad API with sprites from effect sheets and a per-frame service; design `DrawQuad` as the single shared entry (texture + source rectangle + corners + mode), not tied to `Sprite`.
9. The `zOrder` path (entities without `DepthSortable2DComponent`) stays opaque in G2a; decide whether the quad path covers it (recommended yes).

## 11. Files in this folder
`test_quads.py` (27 test quads plain), `classify_quads.py` (+ `anim_classes.json`), `scale_ratios.py`, `mirror_upscale.py`, `predict_corner_tracks.py` (+ `predicted_corner_tracks_per_file.json`),
`predict_export_paths.py` (+ `predicted_changes_paths.txt`, 1 923 lines), `top_banks.py`, `hero_sets.py`, `corner_examples.py`, `sampling_convention.py`, `demo_expected.py`.
