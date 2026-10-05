# ADR-0037: The backdrop overlay tint draws with the PSX mode of the map, like the binary

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: `docs/plan-e19-opcodes.md`, section "E19.g G2d" (open item O-E19-58, decision D-E19-82 for the raw `BGColorA` in the companion; discovery of 2026-10-05, `o58-disc` and its independent verification `o58-disc-verify`, then two plan reviews until READY). Extends ADR-0035 (the PSX mode of the backdrop layers) to the full-view overlay tint, and the engine ADR-0056 (the tint of the scrolling layers carries a PSX mode, itself an extension of the engine ADR-0053).

## Context

- [binary] The byte `header+0x23` (`Infos.BGColorA`) of a map sets its overlay: 0 draws nothing (gates `0x8005B5E8` at load and `0x8005B760` each frame); 1 to 4 draw an untextured `TILE` of 320 by 240 at (0, 0), code `0x62`, semi-transparent with the rate `BGColorA - 1`, read in the same table as the layers (`*(u16*)(0x8018CF66 + 2v)`, `0x8005BAAC`-`0x8005BAC4`); 101 to 104 would draw a `POLY_G4` gradient. The colour is `Data[header+0x10]`, a single entry of duration 0 on the 16 maps with a tint. [PSX] A primitive without a texture blends every pixel.
- Of the 330 maps, `BGColorA` is 0 on 314, 1 on 15 (maps 18, 19, 96 to 99, 271, 289, 357, 403 to 406, 450, 481) and **2 on map 293 alone**, the burning Inoa, colour (50, 0, 0). The port averaged every tint (the decompilation lost the read of the rate and always showed an alpha of 0.5, hence the converter comment "BGColorA 1 and 2 render identically"): the 15 maps of rate 1 were right, the burning Inoa was darkened by half where the binary reddens it a little (a mid grey (128, 128, 128) gave (89, 64, 64) instead of (178, 128, 128)).
- The converter only kept `OverlayEnabled = BGColorA != 0`, the DLL handed the engine `(R, G, B, 128)` and the engine drew the tint with `AlphaBlend` whatever the map.

## Decision

- The converter exports the raw `BGColorA` as `OverlayBlendMode` in the backdrop companion (D-E19-82), omitted when 0 (`WhenWritingDefault`), set only when the tint is enabled.
- The DLL resolves the mode of the tint through the resolver of the layers (`AlundraBackdropStage.ResolveLayerPsxSemiTransparency`: 1 `Mode0`, 2 `Mode1`, 3 `Mode2`, 4 `Mode3`); any other value, in particular the 0 of an export that predates the field, gives `Mode0`, today's drawing. The colour is `(R, G, B, 255)`: the engine alone bakes the alpha of the average (engine ADR-0056).
- The delivery is ordered: the engine first (the DLL does not compile without the constructor), then the converter and the full export in place, then the DLL. A test of the DLL on the real export reads `OverlayBlendMode` 2 on the companion of map 293: a stale export fails loudly.

## Consequences

- Only 16 companions change (one line each, 26 bytes), no PNG, no texture, no catalog; map 293 now adds its colour to the scene, the 15 other maps draw the same pixels as before.
- An old export read by the new DLL draws the average (field absent, 0); a new export read by an old DLL ignores the unknown field: no mixed state breaks.
- Not verified, the same before and after: whether the tint quad covers the whole view at the depth of the camera against the static tiles of each pass; map 293 is out of the current chain, so the in-game check may be impossible for now (the engine demos prove the pixels).
- The 4-corner gradient variant (`BGColorA` of 101 to 104) stays unmodelled: no map has it.
- The formulas are the PSX ones on 8-bit colours, not the 5-bit arithmetic of the hardware, as for the layers and the sprites.
