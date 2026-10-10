# ADR-0049: The world pushes the effect quads to the engine every rendered frame, ordered like the entities

- **Status**: Accepted; the sort of anchored slots amended by ADR-0057 (D-E20-29)
- **Date**: 2026-10-08
- **Source**: `docs/plan-e19-opcodes.md` (E19.g G3c, rules G3C-R1 to G3C-R5); annex `docs/plan-e19-g3c-annexe/` (`discovery/notes.md`, `discovery/verify/verify.md` which wins, `g3c_model.py`, `g3c_expected.json`, `g3c_compare.py`); engine ADR-0071 (effect quad service and component)

## Context

The effect reservoir (ADR-0045) animates up to 128 slots like the binary, but nothing drew them. The engine now has the mechanism (engine ADR-0071, G2e): `EffectQuadService` holds the texture asset ids of the sheets and the last pushed frame of
`EffectQuad`s, `EffectQuadComponent` resolves the sheets and submits the quads from its own `Update` (after `GameManager.UpdateWorld`, where the DLL pushes) with `DrawPsxQuad`. The DLL has to turn the slots into quads.

The binary emits one quad per image of the slot's current image set (`0x8002DB48`): anchor `((x >> 16), (y - z) >> 16)` minus the camera, whole slot dropped when the anchor is outside -128..448 x -128..368, corners in data order, drawn
through the ordering table at slot `16 * min(key >> 20, 59) + 6` with the key stored by the last update. Within a slot image 0 is drawn last; between slots the smaller key is drawn first (quicksort `0x8002E130` over the list of the entities and
the effects).

## Decision

- **Construction** (`AlundraEffectQuadBuilder.Build`, pure): the rules G3C-R1 to R4 of the plan. Anchor with arithmetic 32-bit shifts (the `+ 1` that `0x93` and `0xA2` add to z moves the aura of map 476 from world row 112 to 111);
  elimination of the whole slot on the original scroll (`AlundraCameraMath.ToOriginalScrollSpace` of the resolved camera target), none without a camera; corners `(ax + Xi, -(ay + Yi))`; raw window `(U - mx, V - my, W, H)` with
  `mx = C0 > C2`, `my = C1 > C5`; mode `Semi ? Abr + 1 : None`; sheet 0 (map companion) for an image set of the map table, 1 (global companion) otherwise.
- **Key**: the entities' bucket (pass `YSortedWorld`, layer 300, order 0: `alundra-project/Entities/Alundra/Alundra.entity` lines 544 to 553), elevation `WallPlacementOverlay.ComputeEntityElevation((int)DepthKey, 0)` on the stored key, coordinate
  `100 * ay` (the world row, like the entities), local offset `N - 1 - k` (image 0 last), stable id the slot index.
- **Push** (`AlundraEffectStage`): `AttachService(EffectQuadService?)` and `PushFrame(Camera2dComponent?)` are public (a harness outside the repository drives them). `Load` does `Clear` then `SetSheets(map, global)`; `AttachService` does the
  same at once when a bank is already loaded, so the order of the two does not matter. The sheet ids are the companions' strings parsed to `Guid` (`Guid.Empty` for a null or unreadable one; the parse is copied, the backdrop stage's is private).
  `AlundraWorldProxy` attaches `world.Game?.EffectQuadComponent?.Service` next to the backdrop service, before `Load`, and pushes right after `_backdropStage.PushFrame(` at every rendered frame, outside every gate (a tick frozen by `0x48`, a warp
  departure with 0 tick, a frame without tick). An empty frame is pushed when nothing is active. The slots are walked by index over `Slots` with reused buffers (no allocation per frame); a null service does nothing and logs nothing.
- **Proof**: the prediction `g3c_model.py` (export only, rule B(k) with the top-left tie rule, committed before the code) and the out-of-repository pixel harness (G3c-3) compare the pushed frames to it; the unit tests pin the rules and the
  wiring (`AlundraEffectQuadPushTests`).

## Consequences

- **Equal-key deviations** (the binary's order comes from an unstable quicksort over the whole list of entities and effects, P = 0.5 for equal keys; the DLL is deterministic): (1) two slots of the same key: the DLL puts the higher slot index
  on top; the visible case is map 391 slots 3 and 4 (same row, z 0, IDSV 11, opaque 48 x 39 images 24 px apart), where the binary alone draws slot 3 on top; (2) two slots of the same key with several images are drawn image by image
  interleaved (the local offset comes before the stable id: slot 3 image 1, slot 4 image 1, slot 3 image 0, slot 4 image 0) when the binary keeps the images of an element together; the proof scenes are not affected (the 391 slots have one
  image each); (3) between slots the binary sorts on the full integer key `(y & 0xFFFF0000) + (z >> 16) + (IDSV << 16)`, the DLL on the 16-px bucket (`DepthKey >> 20`) then on the coordinate `100 * ay`, so two slots of the same bucket whose z or IDSV differ
  can be inverted (a deviation already shared with the entities, `WallPlacementOverlay`); an effect and an entity with the same pass, layer, elevation and coordinate are separated by their local offsets and stable ids, which the binary does not fix either.
- The push reads the live slot position and key, the binary pushes render copies taken by the last update: they differ only when an operation applies at the end of a frame without a following update (at most one frame).
- 519 mirrored-X images have `U = 0` and 137 mirrored-Y images `V = 0`: their raw window starts at -1, which the engine's clamped sampler reads as column or row 0 (the accepted overshoot of G2b); one unit row pins it.
- The aura scene differs from the PS1 in the way the engine's screen-resolution rule (engine ADR-0068) does at a magnification of 3 (D-E19-92); at 1:1 the coverage equals the hardware walker's.
- **Residual**: the production line that takes the service (`world.Game?.EffectQuadComponent?.Service`) is covered only by the author's recipe G4 (the proxy test attaches a real `EffectQuadService` by hand, like the backdrop tests); the harness covers neither the
  proxy wiring nor the game's virtual-resolution camera (G2b-4 and G4).
- `hero_effects.json` is still exported (retiring it changes the export, outside G3c); native creators of modes 1 and 3 and the entity-borne effects come with E14.
