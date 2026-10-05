# E19.g G2c value annex (per-texel STP of the backdrop layers)

Written in advance by the read-only discovery of 2026-10-03 (session scratchpad `e19o54-disc/`, script `predict.py`) and checked by
two independent verifications on 2026-10-05 (`e19o54-verify-binary/`, raw `DATAS.BIN` census; `e19o54-verify-code/`, independent
bake from the converter code), against the export of `alundra-project/` at parent `0d5849f`.

| File | Content |
|---|---|
| `export_predicted_changes.txt` | Exact export manifest change under rule A: 187 backdrop PNGs (101 `-layerN.png`, 21 `-layerN-frameF.png`, 65 `-cellsheetP.png`, on 157 maps) and `report.json`; no file added or removed |
| `export_predicted_pngs.tsv` | The 187 PNGs with their map, kind, STP pixel count and drawn pixel count |
| `layer_stp_census.json` | Per backdrop layer (224 rows, raw disc data): Ground, BlendMode, kind, STP-set and STP-clear texel counts, the distinct CLUT words of each class |

Each changed PNG keeps its RGB; only the alpha of its STP texels goes from 255 to 128. The maps 391 and 31 (rain) do not change.
