# G2e (engine: effect quad service and component) and G3c (DLL: per-frame push, pixel proof): read-only discovery, 2026-10-06

Repository `D:\development\repo\alundra-casaengine-project-converter` at `chantier/e19-suite2` (HEAD `bbea80a`), engine submodule at `dd1f8afa`
(`chantier/e19-suite2`; the author's local change in `CasaEngine.Launcher/Program.cs` never touched). Nothing built, run, exported, edited,
staged or committed inside the repository; every script and output is in this folder. Labels: [B] proved on the real code of ALUN_CD.EXE
(France) in the MIPS interpreter, [F] fact read in the repository (file:line), [M] measured by a script of this folder, [H] hypothesis.

## 0. Files of this folder

| File | What |
|---|---|
| `g3c_model.py` | Independent pixel model: anchor, corners, mirror step back, sort order, STP blend, rule B(k) (imports `g2b1_predict.bk`, `inside`, `affine`) |
| `noise.py` | Noise zone (same definition as `g2b1_predict.noise_and_distance`) for any frame |
| `g3c_expected.py` -> `g3c_expected.json`, `ref_*_k{1,3}.png` | Reference frames, digests, noise counts, probes, B-vs-R counts of the three scenes |
| `emit_real_aura.py`, `emit_scene.py` | The REAL emitter `0x8002DB48` on the aura (ticks 0, 2, 6, 16, 30, 31) and on 163 and 391 (vertices, semi/ABR, W/H, OT slot) against the model |
| `anchor_fuzz.py`, `cull_probe.py` | 3000 random anchors/cameras/z on the real emitter; cull boundaries; the `+1` row |
| `sort_probe.py`, `sort_ties.py` | The REAL `0x8002E130` / quicksort `0x8002DA04`: order of the sorted list, behaviour on equal keys |
| `scan_mirror.py` | Census of mirrored images, gutter alpha, modes over the 12 307 exported images |
| `redux_nomask.py`, `run_476_redux.py` | Hardware walker R (PCSX-Redux port) without the 8-bit wrap, B-vs-R counts |
| `fxm.py`, `redux.py`, `g2b1_predict.py`, `mipsdis.py` | Copies (never edited originals) of the earlier helpers |

## 1. Engine at HEAD (G2e) [F]

- `SpriteRendererComponent.DrawPsxQuad(Texture2D texture, Rectangle sourceInTexture, Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft, Vector2 bottomRight, Color color, float z, in RenderSortKey2D sortKey, SpritePsxSemiTransparency mode, Rectangle scissorRectangle)`
  (`SpriteRendererComponent.cs:701`; three more overloads: device scissor `:710`, no key `:718` and `:726`). Corners in WORLD units, y up, data order TL, TR, BL, BR;
  the window is the RAW PS1 window (the producer steps a mirrored axis back one texel); `None` = 1 entry, any mode = 2 entries (opaque window (0.75;1],
  STP window (0.25;0.75]) of the same key/z/corners; `Mode3` colour (64,64,64); every field of the pooled entry assigned (`:765-794`); `NoCull`/`PsxQuad` true.
- Sorting: one list for the whole sprite pass, `List.Sort` (unstable) with `RenderSortKey2D.CompareTo` (pass, layer, order, elevation, coordinate, localOffset, stableId) when either entry has a key
  (`:553-569`). Depth: `LessEqual` with writes, "every sorted participant shares a coplanar Z" (`:230-236`, `TileMapComponent.cs:527-570`). Entities: render Z is always 0
  (`TopDownElevationSimulationSpacePolicy.DeriveRenderPosition` = (X, -(Y - Z), 0); `SimulationSpacePolicy.cs:159-176`).
- The pattern to copy: `CellularLayerService` (GPU-free) + `CellularLayerComponent : GameComponent` (`CellularLayerComponent.cs:20-190`): `Service`, `Update` resolves the sheets when
  `LayersVersion` changes (`AssetContentManager.Acquire<Texture>(id)`, `handle.Asset.Load(acm)`, `.Resource`; handles released on re-resolve and `Dispose`), submits from its OWN `Update`
  (after `GameManager.UpdateWorld`, where the DLL pushes), `Submit(renderer, ..., scissor)` headless-testable (`ResolveScissorRectangle` falls back to the screen size without a device),
  a sheet that fails to load is logged once and its quads skipped. Registered `CasaEngineGame.cs:403-404` (property `:66-67`), `ComponentUpdateOrder.CellularLayers` appended last (`ComponentOrder.cs:43`).
  Headless test pattern: `CellularLayerComponentSubmissionTests` and `SpriteRendererComponentPsxQuadTests` (uninitialised `CasaEngineGame`, device-less `Texture2D`, `_spriteDatas` read by reflection, `FillVertices()`).
- `IsFixedTimeStep` is false in `AlundraGame.json` (one Update per Draw), so a component that submits from `Update` does not double-submit.
- ADR numbers: engine local branches go to 0070 (`chantier/demo-browser` holds `0070-view-layout-area-and-window-ui.md`; `chantier/e19-suite2` 0069; `main` 0068): next free **0071**
  (the R-BR scan of `main`, audio-modern and `chantier/e19*` alone would say 0070: a collision with the author's branch; rescan all local branches, as G2b-2 did). Parent: 0046 on `chantier/e19-suite2`: next **0047**.
  Engine docs index: `docs/README.md:66-68`.

## 2. DLL at HEAD (G3c) [F]

- Stage (`Alundra/Scripts/AlundraEffectStage.cs`, public sealed): `Load(world, project)`/`Load(bank)`, `CaptureHeroTile`, `SpawnLoadEffects`, `Queue/BeginSource/EndSource/Emit`, `RunFrame(ticks, frozenOfTick)`; reservoir
  `Reservoir.Slots` (array of 128 `AlundraEffectSlot`), `ActiveSlots` (an iterator: allocates, do not use in the push). Slot (public): `Active`, `RecordIndex`, `Mode`, `X/Y/Z` 16.16, `TargetMapTable`,
  `CurrentImageSet` (null before the first update and after a defensive destroy), `Idsv`, `DepthKey` (uint, stored by `Move` at the last tick), `Destroying`; internal `CurrentMapTable` (the table the current
  set belongs to). Bank: `AlundraEffectBank.Map?.SheetTextureAssetId`, `.Global?.SheetTextureAssetId` (strings; null for the 71 maps with no table of their own); image: `U,V,W,H,C[8],Semi,Abr`.
- Proxy wiring: `_effectStage` field initializer (`AlundraWorldProxy.cs:590`), `Load` at `:703` (right after `_backdropStage.Load`, `:700`), `RunFrame` at `:2376`, camera block after it, backdrop push
  `_backdropStage.PushFrame(IsTransitionInProgress ? 0 : ticksThisFrame, _cameraDirector.ResolvedCamera)` at `:2423` (outside every gate, no early return in `Update` between `:2129` and the push).
  The backdrop service is attached before `Load` (`:687`, `:694`). `OnEndPlay` exists (`:2954`) but clears nothing of the backdrops; the new world's `Load` clears.
- Camera: `AlundraCameraMath.ToOriginalScrollSpace(target)` = `((int)Target.X - 160, -(int)Target.Y - 120)` (`AlundraCameraMath.cs:300`). The engine world is the original's absolute pixel space with Y flipped:
  PS1 screen (sx, sy) = world (scrollX + sx, -(scrollY + sy)). The harness camera of the G2b demo (`Zoom = k`, `Target = (w/(2k), -h/(2k))`) is scroll (0, 0).
- Entity sort key: `WallPlacementOverlay.ComputeEntityElevation(posY, idsv)` (`:393`, internal static) = `16 * min((posY + (idsv << 16)) >> 20, 59) + 6`; entity prefab `DepthSortable2DComponent`
  `render_pass YSortedWorld`, `sorting_layer 300`, `sort_mode TopDownYUp`, `sort_anchor (0,0)` (`Alundra.entity:542-554`), so the entity coordinate is `round(-renderY * 100)` = `100 * ((Y - Z) >> 16)` (render Y snapped by ceil).
  `ComputeEntityElevation((int)slot.DepthKey, 0)` gives the effect row from the STORED key with no new code.
- `Alundra/InternalsVisibleTo.Tests.cs` exposes internals to `Alundra.Tests` only: the harness needs a PUBLIC `PushFrame`. `Alundra.csproj` has an AfterBuild target copying `Alundra.dll` into `alundra-project/`
  (a tracked file): a harness must reference `Alundra.dll` by HintPath, never by `ProjectReference`.
- Tests with a non-null `world.Game` (uninitialised `CasaEngineGame`): 21 files; a `?.` chain on the new component gives null there; a NEW warning for a null service would be a log nobody asserts today but
  `BackdropStageLoadTests`/`AlundraWarpArrivalTests` capture logs: grep before choosing to warn.

## 3. Rules with their proof

### 3.1 Binary rules (all [B], by the real code)

| Rule | Proof (this folder) |
|---|---|
| anchor = `((x >> 16) - camX, ((y - z) >> 16) - camY)`, int32 arithmetic shifts; the `+1` of `0x93`/`0xA2` moves the row up (z raw 3145729 -> row 119, 3145728 -> 120) | `anchor_fuzz.py`: 3000 random (x, y, z incl. negative and +-1, camera incl. negative), 445 emitted, 0 mismatch; `cull_probe.py` |
| anchor cull: the whole element (all its images) is dropped unless -128 <= sx <= 448 and -128 <= sy <= 368 (inclusive), camera read at `0x80126F10/14` | `cull_probe.py` (x: -129 no, -128 yes, 448 yes, 449 no; y: -129 no, -128 yes, 368 yes, 369 no; also with camera (800, -8)), `anchor_fuzz.py` |
| vertices = anchor + (X1,Y1)(X2,Y2)(X3,Y3)(X4,Y4) in the data order TL, TR, BL, BR; semi bit and ABR; W, H | `emit_real_aura.py` (ticks 0, 2, 6, 16, 30, 31: 0 mismatch of 9, 8, 9, 7, 7, 7 quads), `emit_scene.py` (163: 4 elements, 8 quads; 391: 5 elements at two cameras: 0 mismatch) |
| OT slot of an element = `16 * min(key >> 20, 59) + 6`, key = stored `+0x64` | `emit_real_aura.py` (13 893 680 -> 214), `emit_scene.py` (163: 150; 391: 502, 598, 758, 806) |
| within one element image 0 is drawn LAST: OT chain of slot 214 = images 8, 7, 6, 5, 4, 3, 2, 1, 0 (first = drawn first) | `emit_real_aura.py` (chain walk) |
| across elements: list sorted DESCENDING by key (quicksort `0x8002DA04`, signed compare on element+0x10), emitted in that order, each inserted at the head of its OT slot: smaller key drawn first | `sort_probe.py` (the real `0x8002E130`) |
| equal keys: the result is an artefact of the unstable quicksort over the WHOLE list (entities and effects): measured P(lower index drawn on top) = 0.50 for lists of 5 to 64 elements | `sort_ties.py`. Visible case: 391 records 3 and 4 (slots 3, 4; same y 792, z 0, IDSV 11, opaque 48 x 39 images 24 px apart, overlap 24 x 39): effects alone give slot 3 on top; the DLL's stable rule puts slot 4 on top. Not reproducible without the entity list: a documented deviation to look at in the 391 recipe |
| a frame is pushed per rendered frame whatever the tick count: `0x48` freezes the update only (state kept and drawn); a destroy animation is drawn on ticks 0..S, freed at S + 1 | G3a/G3b annex (`binary-data-verify.md` B9, B11), unchanged |

### 3.2 Port conventions (checked by the independent model, not by the binary)

- Window: the exported `U, V, W, H` are the atlas CELL = the extractor's `SourceX` window; the raw PS1 window of a mirrored axis starts one texel before: `window = (U - mx, V - my, W, H)`, `mx = C[0] > C[2]`, `my = C[1] > C[5]`
  (G2b-1 contract: the producer gives the raw window; G2b-3 does the same for entities with `source_offset = (-mx, -my)`). The texel column U - 1 / row V - 1 is the transparent gutter on all 12 307 images
  (`scan_mirror.py`: 2338 mirrored in X, 396 in Y, 0 non-transparent gutter), EXCEPT that **519 mirrored-X images have U = 0 and 137 mirrored-Y images have V = 0**: the window origin is -1, the texel -1 is clamped to column 0 by
  `PointClamp` (the accepted G2b "overshoot": a pixel that reads below `Sx + 1`; the model clamps the same way). Document it; give one test row.
- Mode: `Semi ? (SpritePsxSemiTransparency)(Abr + 1) : None` (enum None 0, Mode0 1 ... Mode3 4). Real data: (non-semi) 2676, (semi, ABR1) 8906, (ABR2) 396, (ABR0) 329, no ABR3.
- Sheet: `CurrentMapTable ? sheet 0 (map companion `SheetTextureAssetId`) : sheet 1 (global companion)`; the 71 maps without a table of their own have a null map sheet (their effects use the global one).
- World corner = `(ax + Xi, -(ay + Yi))`; z = 0 (the render Z of entities).
- Sort key = `(YSortedWorld, YSortedWorld (300), 0, elevation, coordinate, N - 1 - k, slot index)`, elevation `= 16 * clamp(DepthKey >> 20, 0, 59) + 6` (arithmetic shift of the stored key), coordinate `= 100 * ay`
  (entity-consistent: `round(-renderY * 100)`; the binary orders by ground y then z, an existing documented deviation shared with entities, `WallPlacementOverlay.cs:405-427`).

## 4. Reference images (rule B(k), flat background (24, 40, 56), 320 x 240 PS1 pixels, scroll as listed)

Generated by `g3c_expected.py` from the export JSON and the sheet PNG only (no engine, no DLL). `B(k)` = the plan's screen-resolution rule (exact rationals); coverage by the top-left corner, texel `floor(u + 1/2)`,
ties resolved upward; blend of the engine's states (opaque replace, STP + ABR1 add saturated, ABR2 subtract saturated, ABR0 `(128 s + 127 d) / 255`, +-1). Noise zone: texel within 2^-10 of a boundary (not an exact tie) or pixel
corner within 2^-10 px of an edge; excluded from the comparison, as in G2b1.

| Scene (slot input, scroll) | k | quads | pixels not bg | noise px | exact ties kept | SHA-1 of the RGB image |
|---|---:|---:|---:|---:|---:|---|
| 476 aura, `0xA2 [0,960,160,48]` = raw (62914560, 10485760, 3145729), record 0 anim 1, slot tick 0 (frame 0), scroll (800, -8) -> screen anchor (160, 119) | 1 | 9 | 10 088 | 4 | 123 | `4705d5ad93182c772ac9868402177d20f76a1c8f` |
| same | 3 | 9 | 90 970 | 36 | 1107 | `fb4221a18310f4138cb1fc8f3c13c9be611247a5` |
| same | 4 | 9 | 161 759 | 128 | 480 | `592072209e3b56a226590b5a5c145a6e42d267b8` |
| 476 aura, slot tick 2 (frame 1, 8 quads) | 1 | 8 | 9 653 | 86 | 25 | `8af23eba71afe16b70ad9e28c167f72ab712e963` |
| same | 3 | 8 | 87 054 | 808 | 218 | `49d3ed534830b690110301948904e657efd72a88` |
| 476 aura, slot tick 16 (frame 8, 7 quads) | 1 | 7 | 7 863 | 0 | 157 | `b2b153f943dc3af15358b4ac466b6bacb833e26c` |
| same | 3 | 7 | 70 759 | 18 | 1065 | `59d52f094c25c99e3a502979bfede533841375fb` |
| 163 rays, 4 load-time records (anchors (252,136), (276,136), (324,136), (348,136), elevation 150), ticks 0..10 (one 10-tick frame), scroll (220, 40) | 1 | 8 | 3 840 | 0 | 0 | `8be82e40f923b8d1a9c06f5030faea08b6359e49` |
| same | 3 | 8 | 34 560 | 0 | 0 | `2356a8852057d58d42d235df903b4e11e561631d` |
| 391 pieces, 5 load-time records, scroll (836, 640) (slot 0 culled) | 1 | 6 | 8 100 | 0 | 0 | `801a081a47c2a7091c5918124890d931de0d5872` |
| same | 3 | 6 | 72 900 | 0 | 0 | `23edcdbfea2215bdf5593d4e99e82fb451b27101` |

(Frames 0, 1, 8 of animation 1 are shown at slot ticks 0-1, 2-3, 16-17: each frame lasts 2 ticks; verified on the real code up to tick 31.) PNGs: `ref_<scene>_k<k>.png`; probes (k = 1, B equals the hardware walker, margins
>= 1/16 texel and 1/16 px, not noise; 3 497 to 7 921 candidates per scene, eight spread ones listed) in `g3c_expected.json` (`probes_k1`).

- Hardware residual at k = 1 (B against the PCSX-Redux walker R, which the G2b annex validated on 49 hardware probes): aura 21, 4 and 20 pixels of 10 088, 9 653, 7 863 (0.21 %, 0.04 %, 0.25 %); 163 and 391: 0.
- Deviation of D-E19-92 at k = 3 on the aura (B(3) against a 3 x nearest-neighbour magnification of B(1)): 11 756, 12 236, 11 616 pixels = 12.7 %, 13.8 %, 16.1 % of the lit pixels. The 163 rays and 391 pieces are 1:1: exact at every factor.
- Float margin (the real stress of the G2b shader): the engine adds 1/4096 texel to the coordinates so that an exact tie resolves upward; with real sheets (up to 512 x 435, tallest 512 x 1105 = map 161) the interpolated
  t reaches ~1100, whose float32 ulp (1.2e-4) is half of that margin; the aura has 123 exact ties and 52 pixels between 2^-10 and 2^-8 texel at k = 1 (1107 and 1593 at k = 3): a real test of the epsilon that the 16 x 16 demo texture of G2b-1 never was.
  Any failing tie pixel is an ENGINE defect to fix (window-relative coordinates, a larger epsilon), never a tolerance.

## 5. What a pixel proof can and cannot show

- Engine demo (G2e): synthetic address texture, so engine-only; proves service + component + DrawPsxQuad against B(k), including a TALL texture (512 x 1105) for the epsilon, mirrored images at window origin -1, per-quad mode, image order.
- Real data end to end needs the real DLL stage, the real catalog and the real sheet; the proxy needs a world. Cleanest isolated route (H-iso): an out-of-repo harness (precedent: E13.c S3 harness, `capture-ecran-backbuffer-en-processus`)
  hosting a `CasaEngineGame` with the project of the real export (junction or copy of `AssetInfos.json` + the needed sheets, junction removed with `cmd /c rmdir`), `Alundra.dll` referenced by HintPath, a `Camera2dComponent` at `Zoom = k`,
  `Target = (scrollX + w/(2k), -(scrollY + h/(2k)))`, `AlundraEffectBankLoader.Load(project, "<map name>-<id>")`, `stage.Load(bank)`, `stage.AttachService(component.Service)`, `Reservoir.SpawnPlaced(0, 960, 160, 48)` (the opcode's own call),
  `stage.RunFrame(1, [false])` x (T + 1), `stage.PushFrame(...)`, then `GetBackBufferData` in process (`BackBufferProbe`/`PsxFreeQuadDemo` dump format: int32 w, h, k, RGBA) and a Python compare to `g3c_expected.json`
  (all non-noise pixels equal; blended pixels +-1; probes equal). It does not exercise the proxy wiring (covered by a proxy-level montage test with a real `EffectQuadService`) nor the game's virtual-resolution camera (covered by the pending G2b-4 and by G4).
- A capture of the real game (map 476 with the real script) cannot be compared pixel for pixel: the room has an animated cellular layer, entities and tiles under and over the aura.

## 6. Hazards found

1. Tie order of equal-key effects (see 3.1): deviation, 391 records 3 and 4.
2. 519 + 137 mirrored images at window origin -1 (clamped).
3. Float margin of the +1/4096 epsilon on tall sheets (see 4).
4. `ActiveSlots` is an iterator and the stage's `RunFrame` uses LINQ per frame: the push must index `Slots` and reuse its buffers.
5. `PushFrame` and `AttachService` must be public (the stage is public; `InternalsVisibleTo` is for `Alundra.Tests` only) if the out-of-repo harness is to call them; never `ProjectReference` Alundra from a harness.
6. A frame that persists in the service is stale if the proxy stops pushing (title screen): the new world's `Load` clears it; `OnEndPlay` clearing risks wiping the next world's sheets if its order against the next `Load` is not the end-before-begin one (not established).
7. Engine ADR 0070 already taken by `chantier/demo-browser` (use 0071; rescan every local branch right before the commit).
8. `hero_effects.json` (G1-R8: "G3 retires it") is still written and still in `alundra-project/Sprites/hero/`; retiring it is an export change (converter + doc list of C9), so it is not part of a DLL-only G3c.
9. The `PsxQuad` shader path has only run in the engine demo so far (G2b-4 pending): the first run under the Alundra camera is G4.
