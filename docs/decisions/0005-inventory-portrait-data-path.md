# ADR-0005: The inventory's opening portrait reaches the DLL through map_alundra.json and a one-id index

- **Status**: Accepted
- **Date**: 2026-09-26
- **Source**: this chantier: `docs/plan-portrait-inventaire.md` §2.3 P2, approved by the author on 2026-09-26 (with the review fix recorded in its §7 journal)

## Context

- When the main inventory or the sub-inventory opens, the original game flies a portrait of Alundra from his head to the right of the screen. The portrait is sprite record 0's portrait image (page 2, palette 16, source (200, 56), 48x56, signature 61779762221058). `GraphicManager.GetAnimationImageByIndex(0)` reads it (`0x80057b40` in the France executable).
- No animation uses that image. The extractor only put animation images in the atlas (`GameMapHelper.EnumerateImages`), and `SpriteRecord` has no portrait field. So neither `map_alundra_spritesheet.png` nor `map_alundra.json` carried it, and the converter, which only reads animation quads, emitted no sprite for it.
- The extractor serialises the whole `GameMap`, for the global map and for every numbered map, with the same options: `IncludeFields = true` and no rule to omit null values. A null field added to `GameMap` would therefore appear in every `map_<n>.json`.
- The 88 item icons, by contrast, already had their signatures in animation frames. They reach the DLL through `ItemPortrait.csv`, then `ItemsWriter`, then `Data/item-icon-index.json`, then `AlundraItemTables.TryGetIconAssetId`.

## Decision

- **Extractor.** `GameMap` gets an `InventoryPortrait` field, a `SiImage` marked `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`. The extractor fills it for the global map only, after loading it, from `SpriteInfo.SpriteRecords[0].GetPortraitImageset(br).Images[0]`. `EnumerateImages` then yields it after every animation image, so the image enters the atlas and receives its `AtlasX/AtlasY`. Only record 0 is concerned. Every other map leaves the field null, and its JSON omits it.
- **Converter.** `SpriteBankReader.ReadInventoryPortrait` reads the field as a quad of the `map_alundra_spritesheet.png` atlas. `SpriteWriter` emits its `.sprite` through the same path, name (`sprite_<signature>`) and deterministic id (`SpriteAssetId(sheet, signature)`) as every quad, under `UI/Portraits/`.
- **Index.** The converter writes `Data/inventory-portrait.json` as `{ "SpriteAssetId": "<guid>" }`. This is the one id the gameplay DLL reads, the same contract as `item-icon-index.json`.
- **Missing portrait.** A `map_alundra.json` without the field is a converter warning, not an error: no index is written, and the inventory opens without its portrait.

## Consequences

- A full extraction changes exactly two files: `map_alundra.json` (one added field) and `map_alundra_spritesheet.png` (the 48x56 rectangle at (200, 568), empty before). Every other extracted file stays byte-identical. This was measured on 2026-09-26, and the rectangle equals an independent decode of `DATAS.BIN` texel by texel.
- The export gains one `.sprite`, one catalog entry and `Data/inventory-portrait.json`, and the atlas texture changes.
- The DLL resolves the portrait by id, never by name or path. Moving the `.sprite` file does not break it.
- The dialogue portraits use the same original system, but other records and another rest position (E12.c). They are not covered: extending the field to other records is a separate decision.
- An extraction made before this change still converts, with one warning and no portrait.
