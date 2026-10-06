# G2e (engine side of the effects): discovery of 2026-10-06 (read only)

Scope: engine submodule `CasaEngineMonogame` at `33324030` (tip of `chantier/e19g2d-overlay-blend`, pinned by the parent; the author's local `CasaEngine.Launcher/Program.cs` never touched),
read only. Builds on `docs/plan-e19-g2b-annexe/engine-notes.md` (G2b discovery of the same day; its facts are not repeated, only the parts effects change). Labels: **[F]** fact (file:line), **[H]** hypothesis.

## 1. What an effect needs from the engine, in one list

An effect frame is a list of textured quads with four FREE corners (PSX order TL,TR,BL,BR, signed bytes), each with its own PSX semi-transparency mode (None/Mode0..Mode2 on real data; Mode3 exists), sampling an
alpha-coded cell of an effect sheet (alpha 255 opaque / 128 STP / 0 transparent), ordered inside the world's Y-sorted layer by a key the DLL computes. **63 % of the 20 315 effect quad references are deformed**
(8680 axis-aligned scaled, 4136 non-rectangular; only 7499 are plain), against 31 % for entity quads (G2b): drawing effects as rectangles would be visibly wrong (the 476 aura beams are rotated and sheared).
So the free-quad primitive is not optional for effects and is the SAME primitive as G2b's (G2b notes section 10 item 8: design `DrawQuad` as the single shared entry: texture + source rectangle + corners + mode, not tied to `Sprite`).

**[F] Gaps (engine facts today)**: `SpriteRendererComponent` (`Framework/Application/Components/SpriteRendererComponent.cs`): the queue entry `SpriteDisplayData` already stores four independent vertices (`:16-38`), but the only writers fix them to the unit square
(`DrawSprite` core `:826-887`, `_vertexTopLeft ...`, `WorldMatrix = scale/rot/translation`); index buffer `{0,1,2, 0,2,3}` on slot order TL,TR,BR,BL = diagonal TL-BR, whereas the PS1 split is (v0,v1,v2)+(v1,v2,v3) = diagonal TR-BL
(G2b notes 1.3: write the corners into slots TR,BR,BL,TL to get the PS1 split with no change to `FillVertices` or the index buffer); the pass culls counter-clockwise (`:224`): a mirrored quad (3762 of the 20 315 effect refs have X1>X2 or Y1>Y3) would vanish
without a per-entry "no cull" state (G2b 1.4). **No effect service/component exists**; `PsxSemiTransparency` is applied by the two-entry rule (opaque window (0.75;1] then STP window (0.25;0.75] in the mode's blend state, Mode3 colour (64,64,64)) in the internal
`DrawSprite(Texture2D, Rectangle, Point, Vector2, float, ..., in RenderSortKey2D, bool, SpriteEffects, Rectangle scissor, SpritePsxSemiTransparency)` (`:785-803`, ADR-0051/0053), which the layer components use; a quad entry must reuse exactly this rule.

## 2. The service pattern to copy (AlundraBackdropStage / ScrollingLayerService)

**[F]** GPU-free `*Service` holding data and per-frame state + a thin `GameComponent` that resolves textures and submits through the shared `SpriteRendererComponent` from ITS OWN `Update`, after the DLL pushed this frame's state from `GameManager.UpdateWorld`:
`CellularLayerService` (`Framework/Rendering/CellularLayers/CellularLayerService.cs`) + `CellularLayerComponent` (`Framework/Application/Components/CellularLayerComponent.cs:20-190`: `Service`, `Update` resolves textures on a `LayersVersion` change, advances, submits through the scissor-explicit keyed
overload `Submit(renderer, cameraTarget, scissor)`; textures through `_game.AssetContentManager.Acquire<Texture>(id)` released on re-resolve `:130-165`; `ResolveScissorRectangle` falls back to the screen size without a device `ScrollingLayerComponent.cs:336-358`);
registered in `CasaEngineGame.cs:401-405` (`public ... Component { get; private set; }` `:65-67`), update order appended to `ComponentUpdateOrder` (`ComponentOrder.cs:30-43`, "appended last purely to keep existing enum values stable").
**Proposed engine surface** (names indicative): `EffectQuadService` { `Clear()`, `SetSheets(Guid[] sheetTextureAssetIds)`, `SetFrame(ReadOnlySpan<EffectQuad>)`, `Version` counters } and `EffectQuadComponent` (resolve sheets on version change, submit the frame). An `EffectQuad` is data only:
(sheet index, source rectangle (U,V,W,H) in the sheet, anchor (integer PSX pixel, Y down) + four corner offsets (sbyte pairs), PSX mode, `RenderSortKey2D`). The DLL owns the animation machine and pushes the frame list (the plan's "choix de conduite"); the engine stores no tick state.
Frame lifetime: like `ScrollingLayerService.SetFrame` the list stays until the next push; a world with no effects pushes an empty list (and `Clear()` at world load).
**Texture sampling of the sheet**: PointClamp wrapper (all converter textures); effect cells are separated by the Compact layout's 1-px transparent gutter (G0), so a deformed quad reading one texel outside its cell reads transparent (the G2b "overshoot", 0.011 % of deformed pixels: accept, document).

## 3. Sorting and blending specifics for effects

- Key (DLL-built, engine neutral): `RenderSortKey2D(YSortedWorld, SharedSortingLayer, 0, elevation, coordinate, localOffset, stableId)` as the walls/entities (`WallPlacementOverlay.cs:375-431`); `RenderPass2D.Effects` (500, above the world) is the WRONG pass (effects sort with entities
  in the binary). Compare order: pass, layer, order, elevation, coordinate, local offset, stable id (`RenderSortKey2D.CompareTo`). Image 0 of a set is frontmost: `localOffset = N-1-k`.
- Blend states already exist per PSX mode (`GetPsxBlendMode` `:654-662`: Mode0 AlphaBlend (vertex alpha 128 = (B+F)/2; back-buffer alpha 191, G2a risk), Mode1 Additive, Mode2 Subtractive = ReverseSubtract, Mode3 additive with the front colour (64,64,64)). Real data: ABR1 14 945, ABR2 452, ABR0 371 refs; 0 ABR3.
- Mixed STP/opaque texels inside one quad: measured today on the G0 sheets (`mixed.py`, 12 307 distinct images): 2676 non-semi (0 with an STP texel), 9631 semi = 9175 STP-only, 263 mixed (opaque + STP), 193 with the semi bit but no STP texel (drawn opaque by the GPU). The two-entry rule
  already handles all of them (opaque window then STP window; the empty window draws nothing); an optional converter hint (`HasOpaque`/`HasStp` per image, computed from the sheet alpha) would save the empty draw on the 9175 STP-only images; not needed for correctness. Worst real frame ~27 quads.
- `NoCull` is an entry-level render state in the same family as `IgnoresDepth` (`:37`, per contiguous run `:244-260`): a pooled entry keeps stale fields, so the new field must be assigned in the single private core (precedent test `ReusedPooledEntry_DoesNotKeepTheWindowOfItsPreviousUse`).

## 4. The open decision that blocks rendering (O-E19-71, not yet written in the plan)

G2b discovery section 8: **PSX-exact at any zoom** (option C: a per-PSX-pixel evaluation of the two triangles in the pixel shader, keeps D-E19-60 "all pixels stay equal" when the game renders N x 320x240 with `IntegerFit`) versus **resolution-independent quads** (option A: simpler, the quad is
rasterised at N x resolution; differs by a sub-PSX-pixel texel placement in 50-90 % of the NxN pixels of deformed quads, 66-79 % over the corpus; always within one PSX pixel). The recommendation of that discovery: C if the shader parameter plumbing is cheap, else A with the deviation in the ADR.
**For effects the choice has more weight than for entities** (63 % deformed, large additive glows: the 476 aura covers ~200x212 px) and must be taken ONCE for both (one engine primitive). G1 and every DLL step up to the unit-tested push are independent of it; only the engine draw (G2b-1) and what sits on top
(effect component, demos, recipe) wait for it. The pixel-exact reference pipeline of section 5 works for either option (it evaluates at integer PSX pixels: option C reproduces it exactly, option A is compared at zoom 1 only or with the documented tolerance).

## 5. Values writable in advance for the engine demo / G3 pixel proof (produced today)

`ref_frame.py` (this folder) builds an INDEPENDENT reference frame from `data-extracted` JSON + the G0 effect sheet + the documented PSX rules of `g2b-disc/data/psxraster.py` (two triangles v0v1v2 + v1v2v3, integer sample points, top-left rule, affine uv, floor; STP-only blending
with the quad's ABR; image 0 drawn last), no engine and no binary involved. `ref_476_e0_a1_f0.png`: the vision aura (map 476, effect 0, animation 1, first displayed frame, 9 quads, anchor (160,120) in a 320x240 frame, black background): a hooded silhouette inside a
light-blue ellipse, three rotated light beams and a purple swirl; 10 183 non-black pixels; probes (RGB, 8-bit sheet colours, +-1 tolerance as in G2a): (160,120) = (216,248,255); (150,100) = (144,168,200); (170,140) = (136,168,224); (200,150) = (48,48,96); (120,90) and (160,60) = black (0,0,0).
The same script renders any (map, effect, animation, frame): it is the G0-annex-style source of expected pixels for the 391 pieces, the 163 shafts, the Inoa smoke, the 135 altar, the 10 boulder. It reproduces the binary's integer anchor `((y-z)>>16)` only if given it (here the anchor is passed directly).
The engine demo would follow the G2a demo shape (`CasaEngine.Demos/Demos/PsxSemiTransparency/PsxSemiTransparencyDemo.cs` + `BackBufferProbe.cs`: `GetBackBufferData`, +-1 per channel, probes through `Viewport.Project`, `CASAENGINE_START_DEMO` launched from `CasaEngine.Demos/`): synthetic cases of the G2b annex (`samples/synthetic/cases.json`:
D1 x2, D2 x0.75, D3 mirrored x1.5, D4 parallelogram, D5 trapezoid) PLUS one real effect frame from the table above, at zoom 1 and at zoom 4 (prices A against C).

## 6. Engine tests that would be new / that must not move (precedent rules: a moved assertion is a stop)

New (queue level, headless): quad entry = 1 or 2 entries with the same corners/key; slot order after `FillVertices()`; `NoCull` set and false on a reused pooled entry; the effect service push/clear/version; the component submits the pushed frame in order with the sheet resolved (loader double) and skips a quad whose sheet failed to
load (log once, like `CellularLayerComponent`); `Clear()` empties the next submission; sort-key passthrough; mode per quad; `CasaEngine.Tests` is not in the `.sln` (build it apart, `--blame-hang-timeout 300s`; baseline 2740 at G2d, to recount).
Must not move: `SpriteRendererComponent{BlendMode,Capacity,PsxSemiTransparency}Tests`, `AnimatedSpriteComposedSortTests`, the layer/backdrop tests, `Animation2d*` tests (G2b list, `engine-notes.md` section 8).
ADR numbers: engine main ends at 0055, `chantier/e19g2d-overlay-blend` holds 0056, `chantier/audio-modern` holds 0056-0060 (git ls-tree today) -> the next free engine number is 0061 (G2b-1 first, G2e after), parent next free 0040 (0039 = f4a today).
Docs to write: `docs/engine/` page for the effect service (like `docs/engine/scrolling-layers.md`, `cellular-layers.md`), update `docs/engine/sprite-psx-semi-transparency.md` "Limits" (the free-quad sentence), `ai-agent/README.md` row + a task file.

## 7. Risks (engine)

1. Everything above stacks on the pending G2b-1 decision and branch (`e19g2d-overlay-blend` tip is what the parent pins; both G2b-1 and G2e must be stacked on it, not on main).
2. The engine component's `Update` order relative to the DLL push: copy `CellularLayers` (appended last).
3. Back-buffer alpha written by Mode0 (191) in captures; irrelevant to the on-screen result.
4. The world's `SimulationSpacePolicy` is not applied to effect quads if the DLL passes PSX screen-space anchors (the proposal): the service needs only a Y flip and the camera; check the engine's `Camera2dComponent` scroll vs the DLL's `AlundraCameraMath.ToOriginalScrollSpace` (used for backdrops, `AlundraBackdropStage.PushFrame`) so effects and tiles use the same origin [H, not verified here].
