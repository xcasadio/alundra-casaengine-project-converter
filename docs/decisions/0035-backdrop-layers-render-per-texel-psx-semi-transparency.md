# ADR-0035: The backdrop layers render the PSX semi-transparency per texel, like the binary

- **Status**: Accepted
- **Date**: 2026-10-05
- **Source**: `docs/plan-e19-opcodes.md`, section "E19.g G2c" (decision D-E19-68, taken by the session in AUTO mode under the author's rule "the binary decides"; open item O-E19-54, audit of 2026-10-03 and two independent verifications of 2026-10-05). Extends ADR-0033 (the same mechanism for the entity sprites, D-E19-52) and the engine ADR-0053 (the two passes for the background layers, itself an extension of the engine ADR-0051). Replaces the "analysis missing, do not touch" gate of `docs/plan-e10-fondu.md` section 1.8.

## Context

- [binary] The tiles of the backdrops (`SPRT_16`, `0x8005BB24`) and their cells (`SPRT`, `0x8005BDD4`) are set once at load with the semi-transparency bit equal to `BlendMode != 0` (`SetSemiTrans`, `0x8005BC34`, `0x8005BC64`, `0x8005BFB4`, `0x8005C03C`); the rate of the layer is `BlendMode - 1`, by the primitive of mode set at the head of its slot (`0x8005B958`-`0x8005B9E4`); `Ground` only chooses the slot (`0x8005B8B0`). The colour table sent at `0x8005B2A0` equals the extracted `PaletteWords`, bit 15 included, on the 192 maps with a backdrop. A semi-transparent primitive only blends the texels whose bit 15 (STP) is set; the others are drawn opaque.
- The converter wrote every drawn texel of a backdrop sheet at alpha 255 (`BackdropImageBuilder.DrawTile`), and the DLL chose one blend per layer, only for `Ground` layers (`ResolveGroundLayerBlend`). Of the 224 layers of the corpus, 11 were wrong: the rain of maps 391 and 31 (720 texels with no STP bit, opaque in the binary) was drawn at 50 %, and layer 1 of maps 41, 44, 109 to 114 and 470 (the colour word `0x8C83`, 518 canvas pixels in 81 poses on map 44) was drawn opaque where the binary blends it. All the other semi-transparent layers are entirely STP and all the opaque layers are right.
- The engine draws a sheet with an STP-texel window since ADR-0051 (sprites) and ADR-0053 (the scrolling and cellular layers): a layer carrying a PSX mode draws its opaque texels (alpha 255) opaque and its STP texels (alpha 128) with the blend of the mode.

## Decision

- The converter bakes the STP texels of every backdrop sheet (tile canvases, animation frames, cell sheets) at alpha 128, any other drawn texel at alpha 255 and the colour word `0x0000` at alpha 0; the RGB is unchanged. The sheet is the content of the video memory: the semi bit and the rate of the layer stay in the `BlendMode` of its companion document.
- The DLL resolves the PSX mode of a layer from its `BlendMode` alone (`AlundraBackdropStage.ResolveLayerPsxSemiTransparency`): 0 `None`, 1 `Mode0`, 2 `Mode1`, 3 `Mode2`, 4 `Mode3`, any other value `None` (the binary would read a table past its end; no case in the corpus). The layer definitions carry that mode, an opaque blend and a white tint. `Ground` only chooses the render pass. The `Ground` gate of E10 (plan-e10-fondu section 1.8) falls.
- The delivery is locked: the converter, the engine pointer and the DLL arrive together, and the full export in place follows the build of the DLL. A test of the DLL on the real export reads the alpha 128 at the texel (46, 23) of `layer0` of map 389 and at the texel (0, 0) of `cellsheet0` of map 476, and the alpha 255 at the texel (2, 0) of `cellsheet0` of map 391: a stale export fails loudly.

## Consequences

- The 187 exported PNGs that hold STP texels change (alpha only); the rain of maps 391 and 31 does not change and is now drawn opaque like the binary; the sea of map 389 and the waves of maps 476, 478, 362 and 44 keep their appearance (all their texels are STP); layer 1 of maps 41, 44, 109 to 114 and 470 now blends its STP texels.
- A mixed state draws wrong: new sheets with the old DLL draw the 50 sea layers at a quarter, a new engine and DLL with the old sheets draw the sea and every additive or subtractive layer opaque. Nothing but the guard test sees it, so the three halves are delivered and verified together.
- The additive overlay of map 293 (open item O-E19-58) and the palette byte 30 of the tiles of maps 1, 13, 17, 153 and 439 (O-E19-59) stay out of this decision; what lies under layer 1 of map 44 (the port clears to black, the clear colour of the binary is not read) stays open.
- The formulas are the PSX ones on 8-bit colours, not the 5-bit arithmetic of the hardware (a few levels of difference), as for the sprites.
