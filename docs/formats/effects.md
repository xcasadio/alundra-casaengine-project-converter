# Effects (`*.effects.json`, `effects-global.json`)

Code: [`Readers/EffectBankReader.cs`](../../alundra-casaengine-project-converter/Readers/EffectBankReader.cs),
[`Writers/EffectWriter.cs`](../../alundra-casaengine-project-converter/Writers/EffectWriter.cs) (Phase 9, `Phase9.Effects`).
Plan: `docs/plan-e19-opcodes.md`, section "E19.g G1"; decisions D-E19-96 and D-E19-97; ADR-0043.

## What it is

The binary draws map and object effects (the doors of Inoa, the aura of map 476, the rays of map 163, sparks, bombs...) as
free quads cut from an effect sheet: each effect is an animation of image sets, each image a window of the sheet drawn on four
free corners with a PS1 blend mode. Map records create effects at load or by script; the tables hold what they play. None of
it was exported before E19.g G1, and the entity path cannot carry it (a sprite part is a rectangle plus flips, with no scale,
no free corners and no per-quad blend mode). Nothing in the editor or the engine loads these files: they are raw companions,
like `events.json` and the backdrop companion; the DLL reads them (E19.g G3).

## Where it is written

| Path | Content |
|---|---|
| `Maps/{Zone}/{Name}-{id}/effects/{Name}-{id}.effects.json` | Companion of each of the 157 maps that have effect records or an effect table. |
| `Maps/{Zone}/{Name}-{id}/effects/map_{id}_effectsheet.png` (+ `.texture`) | Sheet of the 86 maps that have their own table (a texture asset, two catalog entries each). |
| `Data/effects/effects-global.json` | The global table: the 29 effects shared by every map (`map_alundra.json`). |
| `Data/effects/map_alundra_effectsheet.png` (+ `.texture`) | Sheet of the global table. |

The 71 maps that only name the global table have a companion but no sheet (`SheetTextureAssetId` is `null`).

## Map companion

```json
{
  "MapIndex": 163,
  "SheetTextureAssetId": "1d63d53c-c074-5b0d-a330-3c96be3cdf0b",
  "Records": [
    {"X1":0,"X2":51,"Y1":0,"Y2":59,"Flags":192,"Effect":0,"X":20,"Y":18,"Z":2,"Anim":1}
  ],
  "Effects": [
    {
      "Animations": [
        {"Frames":[[10,0]],"End":"Loop"},
        {"Frames":[[10,1]],"End":"Destroy"}
      ],
      "ImageSets": [
        {"Idsv":0,"Images":[{"U":25,"V":0,"W":24,"H":31,"C":[-12,-40,12,-40,-12,-9,12,-9],"Semi":true,"Abr":1}]}
      ]
    }
  ]
}
```

(trimmed: the real file has 4 records, 3 animations and 3 image sets.) The members, the records, the animations and the image
sets sit on their own line; inside a line the layout is compact. Keys and number formats are fixed: the export is
byte-stable (double export).

- `MapIndex`: the Alundra map id. Absent from the global companion.
- `SheetTextureAssetId`: the id of the sheet's `.texture` wrapper in `AssetInfos.json`; `null` when the map has no table of
  its own. The `.texture` points at the raw PNG (point sampling, clamp).
- `Records` (map only; the index is the position in the map, the one the script opcodes name):
  `X1`, `X2`, `Y1`, `Y2` the inclusive tile box; `Flags` raw (`0x80` the effect is in the map table, otherwise in the global
  table; `0x40` the effect appears at load); `Effect` the table index; `X`, `Y`, `Z` the position in the binary's units
  (half tiles for `X` and `Y`, 8 px for `Z`; the DLL scales them); `Anim` the animation index in the table. The two bytes the
  binary never reads (`U1`, `U2`) are not exported. A record that names an absent table or animation is an export error.
- `Effects`: the tables (the map table is the one the `0x80` records name; index = position in the source).
  - `Animations`: index = case index of the table. The cases of a table are the first `AnimationCount` entries of the source's
    `AnimationOffsets` (the array is longer, the rest is not a case). 83 trailing cases with offset 0 are padding (no record or
    script names them; the binary would halt on them): they are dropped and counted. A zero offset followed by a non-zero one
    is an error (the indices would shift).
    `Frames` is the list of displayed frames `[ticks, image set index]`: the source's delay is `0x80 | ticks` with ticks 1 to
    127 (the binary reads a zero as 256 ticks, which the corpus never uses; that rule lives in the DLL). `End` is the last
    entry of the source, a pseudo-frame: raw delay 0 is `"Destroy"`, raw delay 1 is `"Loop"`, anything else is an error.
  - `ImageSets`: de-duplicated per table on the source's image set pointer (a table-relative offset), in order of first use.
    `Idsv` is the depth bias of the sort key (shifted left by 16 by the DLL). `Images` keep the order of the binary:
    `U`, `V`, `W`, `H` the window of the effect sheet (the source's `AtlasX`, `AtlasY`, `Swidth`, `Sheight`: the cell of the
    sheet, with the one-texel shift of mirrored quads already applied), `C` the four corners as eight signed bytes
    `X1, Y1, X2, Y2, X3, Y3, X4, Y4` (top-left, top-right, bottom-left, bottom-right; y down), `Semi` bit 3 of the source's
    `Spritesheet` (the quad is semi-transparent) and `Abr` its bits 4 and 5 (the PS1 blend mode; the page is bits 0 to 2).
    Mirrors are not exported: the corner order carries them (`X1 > X2` is a mirror in X, `Y1 > Y3` in Y; checked on the
    20 315 references of the corpus).
    Degenerate images (window 0 x 0, all corners 0: 23 in map 161) are dropped and counted; a set that becomes empty stays, so
    the indices are stable.

No field derived from a pointer is exported (`ImageSetId`, `MemoryAddress`, `BinOffset`, `Signature`): they change with the
extraction.

## Global companion

`{ "SheetTextureAssetId": ..., "Effects": [...] }`: the 29 tables, same shape; the 194 records that name them are in the map
companions.

## Full corpus

`report.json` counters (invariants checked on a full run): `Effects.Records` 544 (251 at load, 350 in a map table, 194 in the
global table), `Effects.Tables` 165 (136 map tables and 29 global), `Effects.Animations` 363, `Effects.AnimationSlotsDropped`
83, `Effects.Frames` 5148, `Effects.ImageSets` 2832, `Effects.Images` 12 307, `Effects.ImagesDegenerateDropped` 23,
`Effects.Companions` 157, `Effects.Sheets` 87, `Effects.UnresolvedRecords` 0. 748 of the images have an all-transparent cell in
their sheet (the PS1 draws nothing for them either). `Assets.Texture` does not change (only the tile maps count there).

The prediction of the export, derived independently from `data-extracted/`, and its checker are in
`docs/plan-e19-g1g3-annexe/` (`g1-predict.py`, `g1-export-prediction.md`).
