# ADR-0033: The converter writes the PSX semi-transparency mode of each entity sprite

- **Status**: Accepted
- **Date**: 2026-10-03
- **Source**: `docs/plan-e19-opcodes.md`, section "1.2o.2 E19.g G2a" (decision D-E19-52, taken by the author on 2026-10-03, question Q-G2: the per-texel semi-transparency and the deformed quads also apply to entity sprites). Engine side: ADR-0051 of CasaEngineMonogame.

## Context

- The sheets of the entity sprites hold the semi-transparency of each texel as alpha (0 transparent, 128 for the "STP" texels, 255 opaque; G0 and G0b, ADR-0030 and ADR-0031). The blend rate of the semi-transparent texels is a property of the primitive of the original binary: `ALUN_CD.EXE` (France) reads the first byte of the image record at `0x8002DC28`; bit 3 sets the semi-transparent code bit and the ABR is `(b & 0x30) >> 4` (`0x8002DC30`-`0x8002DC58`); bits 6-7 are never read.
- The byte is `SpriteQuad.Spritesheet` of the converter. On the 160 355 real quads it equals `Signature & 0xFF`; the synthetic fixtures of the converter tests carry `Spritesheet` 0 and arbitrary low bytes of `Signature` (11 of the 17 literal quads, and the templated ones, would silently become semi-transparent if the mode were read on the signature).
- 1836 of the 6908 exported entity sprites are semi-transparent: ABR 1 1615, ABR 0 218, ABR 3 3, ABR 2 0 (ABR 2 exists only among effects). No non-semi-transparent sprite carries a STP texel (0 of 5073, plan G0b). The inventory portrait sprite has the byte `0x02`: no mode.

## Decision

- `SpriteWriter.EnsureSpriteData` sets the new engine field `SpriteData.PsxSemiTransparency` of each `.sprite` from `SpriteQuad.Spritesheet`: bit 3 clear gives `None`; bit 3 set gives `Mode0` to `Mode3` for the ABR `(Spritesheet >> 4) & 3`. It never reads `Signature` for this.
- The engine writes the field only when it is not `None` (`psx_semi_transparency`, by name), so the file of every other sprite keeps its bytes: the export differs from the previous one by exactly the 1836 sprite files (and `report.json`), and in each of them by that one line.
- The engine draws the two classes of texels in two passes (ADR-0051); the DLL changes nothing (it never sets the colour of `AnimatedSpriteComponent`).

## Consequences

- The 1836 sprites (the hero's 83 sprites, all ABR 1; enemies and objects, ABR 0; the 3 sand cloud sprites, ABR 3) draw their STP texels blended; every other sprite is unchanged.
- A sprite shared by quads of different modes is impossible: the mode is in the signature, and the sprite identity is `(sheet, Signature)`.
- Deformed quads stay drawn as rectangles until G2b; the effects (ABR 2 included) belong to G1/G3.
- Rolling back: the converter commit and the engine pointer; re-export in place gives the previous manifest.
