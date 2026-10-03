# E19.g G0b value annex (entity sheets in the Compact layout)

Written in advance by the read-only value audit of 2026-10-03 (session scratchpad `e19g0b-audit/`, script `predict_g0b.py`
with `e19g0-audit/g0lib.py`), against `data-extracted/` as left by G0 (4537 files) and the analyser at `d8d9230`.

| File | Content |
|---|---|
| `predicted_changes.txt` | Exact `diff -rq` of the re-extraction against `data-extracted/`: 903 `M` (484 entity sheets, 419 map JSON), no `A`, no `D` |
| `expected_entity_sheets_compact.tsv` | Per entity sheet: old and new size, SHA-256 of the decoded RGBA pixels in the Compact layout, opaque and STP texel counts, images wrong in the Original layout |
| `expected_portraits_compact.tsv` | The inventory portrait and the 331 dialogue portraits: old and new atlas position |
| `json_atlas_changes.tsv` | Per map JSON: entity references whose `AtlasX/AtlasY` change |
| `export_predicted_changes.txt` | Exact export manifest change: 7014 `M` (104 sprite textures, 6908 `Entities/**/*.sprite`, `UI/Portraits/sprite_61779762221058.sprite`, `report.json`) |
| `export_textures.tsv` | Per exported sprite texture: new size and RGBA SHA-256 |
| `item_icons_wrong.tsv` | The 23 of 85 item icons that show the wrong pixels in the Original layout |
| `summary.txt`, `checks.txt` | Audit counts and side checks (story-chain folders, shared signatures of `map_alundra`) |

Not versioned (size), kept in the scratchpad and reproducible with the script:

| File | SHA-256 |
|---|---|
| `expected_cells_compact.tsv` (46 497 cells: new and old `AtlasX/AtlasY` per signature) | `48cabd1da95435cdd5016f7e8fd369830ecd2a60269c1e66a3d6c538e712c50f` |
| `export_sprites.tsv` (6909 `.sprite` files: old and new location, crop check) | `8c3bd6fdf289742c3f39b8714608c587bfdf5f9eb0f5caad1ccd55391a667de0` |

The new PNG bytes cannot be predicted (encoder); sheets are proven by size and decoded RGBA pixels.
