# ADR-0031: The entity sprite sheets are written in the Compact layout, one cell per Signature

- **Status**: Proposed (decision D-E19-65 taken by the session in AUTO mode, to be confirmed by the author)
- **Date**: 2026-10-03
- **Source**: `docs/plan-e19-opcodes.md`, section "E19.g G0b" (decision D-E19-65); value annex `docs/plan-e19-g0b-annexe/`. It amends the "Original layout" line of ADR-0030.

## Context

- The extractor wrote the entity sheets (`data/map_<n>_spritesheet.png`, `data/map_alundra_spritesheet.png`) in the `Original` layout, the default of `--spritesheet-layout` (`Program.cs:147`): one cell per VRAM region. The palette variants of one region share its cell and the last one drawn wins.
- 11 803 of the 46 497 unique images were not in their own cell. In the export, 2702 of the 6909 sprites placed on an entity sheet showed pixels that were not theirs (2700 in colour, 2 by the STP bit only), in 172 of the 364 entity folders: story characters with another character's palette, the Rancune de Melzas almost black, 23 of the 85 item icons. The original game reads each quad from VRAM with its own palette, so this was a defect of the port.
- The `Compact` layout already existed (one cell per `Signature`, 512 wide, 1 px padding) and goes through the same `SaveSpriteSheet` function as `Original`: alpha code, odd column, portraits and position stamping apply unchanged. The effect sheets do not depend on it.
- The converter reads only `AtlasX/AtlasY` of each reference and writes them to the `.sprite`; sprite ids come from the sheet name and the `Signature`.

## Decision

- The entity sheets are written in the `Compact` layout: one cell per `Signature`, tallest first then by `Signature`, 512 wide, 1 px padding, height `max(h, 1)`.
- The default of `--spritesheet-layout` becomes `Compact` (`Program.cs:147`, `GameMapHelper.SaveSpriteSheet`), so that a re-extraction with default options does not silently return to `Original`. The doc comments and the usage table of the analyser follow. `Original` stays available by option.
- No other extractor code changes; the rules of ADR-0030 (alpha code, odd column, dialogue portraits) hold in this layout.
- The converter, the DLL and the engine do not change.

## Consequences

- A re-extraction changes exactly 903 files of `data-extracted/` (all `M`: the 484 entity sheets, 419 map JSON), listed in `docs/plan-e19-g0b-annexe/predicted_changes.txt`. The 484 sheets go from 256 x 2048 to 512 x H (H from 1 to 1494). All 914 789 entity references get new `AtlasX/AtlasY`; effect references do not move.
- The next export changes exactly 7014 files (`docs/plan-e19-g0b-annexe/export_predicted_changes.txt`): 104 sprite textures, 6908 `Entities/**/*.sprite` and `UI/Portraits/sprite_61779762221058.sprite` (only `location.x/y`), and `report.json`. All 6909 crops equal their own decode.
- 172 entity folders regain their own colours in game; the textures are no longer all powers of two (661 exported PNGs already were not).
- The inventory portrait moves from (200, 568) to (431, 121); the test documentation that quoted the old position was updated, no test value changed.
- Sprites of the Original layout shared a cell between palette variants (6 pairs of `map_alundra` differ only by the semi/ABR bits and keep two cells with identical pixels, which E19.g G2a needs).
