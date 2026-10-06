# E19.f4a - export prediction (F4A-0)

Written and committed BEFORE any converter or DLL code of E19.f4a. Computed by `docs/plan-e19-f4-annexe/f4a-predict.py` from `data-extracted/`,
`portraits_table.tsv` and the export of this moment (`alundra-project/` after E19.f3b; a baseline in-place re-export at `30a845c`
changed only `report.json` timings, so the tree is exactly the export of HEAD). The script is independent of the converter (own uuid5, own
reading of the extraction) and asserts: the 25 ids recomputed from `uuid5(2b7f6b6a-..., "sprite:map_<map>_spritesheet.png:<signature>")`
equal `portraits_table.tsv`; the canonical record of each of the 25 banks (first map in numeric order) carries `DialoguePortrait` with the
table signature and size; the banks whose canonical header has bit `0x80` are exactly the 25; each bank's prefab id is recovered as
`uuid5("entity:<bank>/<folder>")` against the exported `.entity` folders and the 25 prefab ids equal the 25 entries of
`Data/sprite-records.json` whose `FlagsPortraitShadowType` has bit `0x80`.

## Paths (manifest SHA-1 of `alundra-project/`, without `Alundra.dll`, `Alundra.pdb`, `.casaeditor/`)

**Added (24)**: one `.sprite` per portrait bank, bank 15 (Bonaire, `cb5544da-8df5-58e5-8a09-dec693ec1d53`, already exported as
`Entities/Bonaire (homme endormi à jamais)/sprite_61779896438529.sprite`) excluded:

- A UI/Portraits/sprite_61780823376641.sprite
- A UI/Portraits/sprite_61780829669121.sprite
- A UI/Portraits/sprite_61778947473409.sprite
- A UI/Portraits/sprite_61778956913409.sprite
- A UI/Portraits/sprite_61779886998272.sprite
- A UI/Portraits/sprite_61780152289284.sprite
- A UI/Portraits/sprite_61780294369795.sprite
- A UI/Portraits/sprite_61780297516035.sprite
- A UI/Portraits/sprite_61780291225091.sprite
- A UI/Portraits/sprite_61780826525185.sprite
- A UI/Portraits/sprite_61779893290496.sprite
- A UI/Portraits/sprite_61778944329729.sprite
- A UI/Portraits/sprite_61778953769473.sprite
- A UI/Portraits/sprite_61780290699264.sprite
- A UI/Portraits/sprite_61779490640896.sprite
- A UI/Portraits/sprite_61779890144000.sprite
- A UI/Portraits/sprite_61781770242561.sprite
- A UI/Portraits/sprite_61782043394048.sprite
- A UI/Portraits/sprite_79371945118978.sprite
- A UI/Portraits/sprite_61781762908929.sprite
- A UI/Portraits/sprite_61780420724739.sprite
- A UI/Portraits/sprite_61778956910593.sprite
- A UI/Portraits/sprite_79373961528835.sprite
- A UI/Portraits/sprite_61778949046785.sprite

**Modified (3)**: `Data/sprite-records.json`, `AssetInfos.json`, `report.json`.

**Removed**: none.

## `Data/sprite-records.json`: 25 entries gain a `DialoguePortrait` object, the 370 others are identical to the byte

| bank | prefab id | SpriteAssetId | Width | Height |
|---|---|---|---|---|
| 0 | b8fd1216-399a-5f93-a7f9-c6f26b2a6800 | 0034fa33-3fe9-5697-bc28-a69c362e9d80 | 48 | 56 |
| 3 | 24147828-80a5-59d9-b3b1-af4dfe9c3361 | d2849bcc-5849-5e9b-b501-ace16721ed6a | 48 | 56 |
| 4 | 71132b03-71e4-56d3-83bf-ee3afc365fda | 56f5809a-a47b-564e-b7f7-a66be1f98b04 | 48 | 56 |
| 5 | b0cc0d6a-8919-5d5d-82ed-aa96c7c8b1f5 | b75d1157-2b15-57de-9ce8-284107e590dd | 48 | 56 |
| 6 | dd773d36-cee1-580f-ba2b-052962fbbce4 | 131749d6-e478-5ac5-91a9-50c7d5dd8160 | 48 | 56 |
| 8 | 008cdd94-fa0c-582d-9556-15d85436a7f7 | 6d799c59-b317-5e11-b509-9b348230597f | 48 | 56 |
| 9 | 43e06d18-cb40-5b63-a8e2-742620715034 | 19844fc1-4eaf-5759-8863-39d9561f6b08 | 48 | 56 |
| 10 | 467be6d7-ffdd-5f04-bab3-082942f81236 | 11fe45b0-bf11-5cb2-8f16-be53cc0cd0c5 | 48 | 56 |
| 11 | 88c52792-3327-567e-bdd6-c54ec89164a8 | 1f24c04c-7fef-5b92-ba26-9e554dad9b6b | 48 | 56 |
| 12 | fd3863f7-1c68-5d93-a3f4-21c128763702 | 27095be1-9756-5c02-b3fa-c3b0e23a57ef | 48 | 56 |
| 13 | 64c74f55-d512-5dce-9156-e53ee41afabd | 293c93b1-a811-539a-b469-070dbf9b7e22 | 48 | 56 |
| 15 | bf37c82b-413e-5ee0-8919-d6c15173f72e | cb5544da-8df5-58e5-8a09-dec693ec1d53 | 48 | 56 |
| 17 | 47554291-9e3b-5b3c-ac42-f00c65dfe565 | 7a4104c4-c6cf-53d1-86ea-889bbf69e217 | 48 | 56 |
| 18 | 9b582582-3bcd-5c72-8897-903b6a5690be | 8ac11043-27d2-5a56-b5ea-4bb33c959899 | 48 | 56 |
| 29 | 5dd57408-7d4d-5adc-90d5-fb2492d3276f | 4f9560fb-82d1-5959-bb9c-6ab3f0dc5774 | 48 | 56 |
| 30 | 4947091b-be71-541f-b47e-30fe0311b31e | b46a07f1-9c28-5ca1-b6cd-e0a57be21eee | 48 | 56 |
| 46 | 84266cc1-7c5f-5aa9-b7e6-84f91fb0eef5 | 9089d675-e045-541c-9266-625d44e1d2de | 48 | 56 |
| 83 | ba6e6deb-6b8d-5617-a55d-523d5da5e3b4 | 55200ff3-f0c1-5f3f-8fd2-c3491a173d9d | 48 | 56 |
| 93 | aa0c80af-5776-5da7-85a7-b5eed0f2f3ca | 6e734794-cd3f-54db-bf7d-ea584d3e1224 | 48 | 56 |
| 122 | 976170b3-0d02-59ee-bb38-1d3f5ba8dc1c | bf75c68e-424f-57c9-8f6a-535f33d769d7 | 48 | 72 |
| 123 | b3def1aa-03e4-5e3c-abf2-81d5974d26d1 | c76364e9-a088-503a-a8d0-0dd0926d0640 | 48 | 56 |
| 144 | 6813c3ea-3763-53f4-ac2b-c711b234032b | 37ef4276-e99b-59f9-a866-b5c8b6aced36 | 48 | 56 |
| 161 | 35223111-9435-54b1-99d1-69685a442eff | 706de6dd-0896-52ae-9d1a-fb8301fe07a9 | 48 | 56 |
| 162 | 8b54753a-2c19-5945-8dad-17672c183cfd | 009fb35f-d430-580b-8176-1320ba613900 | 48 | 72 |
| 253 | 30f5ecff-00a8-55ea-ab36-91ab18a6db33 | c60de559-cbd7-5afc-b883-14c685e0d437 | 48 | 56 |

(Bank 15 reuses the id of its animation sprite; 48 x 72 for banks 122 and 162.)

## `AssetInfos.json`

24 new `sprite` entries (the added files, name `sprite_<signature>`), nothing else.

## `report.json`

| counter | now | predicted |
|---|---|---|
| Sprites.DialoguePortrait | absent | 25 |
| Assets.Sprite | 6909 | 6933 |
| Verify.LoadableFilesOnDisk | 20028 | 20052 |
| Verify.Assets | 22418 | 22442 |
| Verify.Loaded | 20028 | 20052 |
| Verify.Loaded.sprite | 7199 | 7223 |
| Metrics.OutputFileCount | 23750 | 23774 |

`Warnings` (6), `WarningsByCategory`, `Errors` and every other counter unchanged; the `Metrics` durations and output size vary from run to run.

## Not changed

No texture (the portraits are cells of the map sheets exported since G0/G0b), no Yarn, no map, no entity prefab file, no UI screen.
