# ADR-0044: The quad corners of deformed entity images are exported as a Corners track appended to the .anim2d

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: `docs/plan-e19-opcodes.md` (E19.g G2b, G2b-3, D-E19-92, D-E19-97); annex `docs/plan-e19-g2b-annexe/` (`engine-notes.md` 3, `g2b3-predict.py`, `g2b3-prediction.json`); engine ADR-0069 (the `Corners` track)

## Context

The binary (ALUN_CD.EXE, France) draws every entity image as one `POLY_FT4` with four free signed-byte vertices (emitter `0x8002DB48`): scaling, rotation, shear and mirroring all
live in those eight bytes, there is no flip, rotation or scale field. The converter kept only the centre of the corners (the part `Position`), two mirror flags and the crop size, so it
drew every image as a rectangle. Of the 160 355 quad references of the 395 de-duplicated banks, 49 348 (30.8 %) are not a rectangle of the source size (mirrors excepted), in 1923 of
the 9 620 `.anim2d`. The engine learned a `Corners` track in G2b-2 (engine ADR-0069): offsets in pixels (y up) from the part `Position`, a source window offset, step sampling, drawn as a
free PSX quad. The converter had to write it.

## Decision

- A part with at least one deformed image (anything but a rectangle of the source size, mirrors included: `X1 == X3`, `X2 == X4`, `Y1 == Y2`, `Y3 == Y4`, `|X2 - X1| == W`,
  `|Y3 - Y1| == H`) gets a `Corners` track, **appended after every existing track** of the animation (in part order), so no existing track name (`Track N`) moves. The `Sprite`, `Position`,
  `Visible` and flip tracks, the `.sprite` and `.texture` files and the catalog do not change.
- Keys sit on the times of the part's `Sprite` keys (the cumulative `float` time of the writer), **only where the state changes** (active corners or the rectangle; the initial state is the
  rectangle): a plain image after a deformed one writes a disabled key; a hidden image writes none; the repeated last image of a `Hold` or `Chain` animation writes none (same state).
- An active key holds the raw PS1 corners (y flipped) minus the `Position` of the image, in the data order top-left, top-right, bottom-left, bottom-right, and
  `source_offset = (-mx, -my)` with `mx = X1 > X2`, `my = Y1 > Y3` (a mirrored axis names its source window one texel early, the rule of the extractor).
- Report counters: `Sprites.QuadsDeformed`, `Sprites.CornerTracks`, `Sprites.CornerKeyframes` (active and disabled keys together), written only when not zero.
- The prediction was written before the code, from `data-extracted/` and without the converter (`g2b3-predict.py`), and the export is proved equal to it key by key.

## Consequences

- Measured on the full export: 1923 `.anim2d` change (exactly the predicted list) and `report.json`; 22 179 files are byte-identical; 6 432 `Corners` tracks, 45 237 keys (38 910 active, 6 327
  disabled), 49 348 deformed quads; every key time is a `Sprite` key time of the same part; each changed file minus its `Corners` tracks equals the old file. A second export differs only by
  `report.json`.
- An engine that does not know the `Corners` property cannot load these files (the property is parsed by name): converter, engine pointer and export travel together. Alundra.Tests guards the
  chest of Anzes (`Entities/Coffre d’Anzes/bankalundra_227_anim1_down.anim2d`: ten tracks, their key counts and first keys, unchanged duration of 1.1 s) and fails, never skips, when the file is missing.
- The durations of the animations do not change (the keys fall on existing frame times), so the logical clock of the DLL is unaffected; the DLL does not read corners.
- 30.8 % of the entity image references change their look (the recette G2b-4 is the author's); the rule at screen resolutions above x1 is the engine's (D-E19-92).
- The effects (G1/G3) use the same quad service with their own companions; they are not part of this export.
