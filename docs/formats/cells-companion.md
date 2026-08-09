# Métadonnées de cellule (`AlundraCells`)

Code : [`Readers/CellMetadataReader.cs`](../../alundra-casaengine-project-converter/Readers/CellMetadataReader.cs),
[`Writers/CellMetadataWriter.cs`](../../alundra-casaengine-project-converter/Writers/CellMetadataWriter.cs)
(Phase 2).

## Ce que c'est

Les données de gameplay par cellule de la grille de map Alundra : walkabilité, propriété de sol,
pente, hauteur, tuile brute (id/palette/tuile), flags, et les piles de « tuiles de mur » (murs
empilables case par case, utilisés pour l'escalade/les obstacles verticaux).

## Où c'est écrit

Ce n'est pas un fichier séparé : le document est sérialisé en JSON (snake_case) puis stocké comme
une **chaîne** dans la clé `AlundraCells` de `TileMapData.CustomProperties`, à l'intérieur du
`.tileMap` que la Phase 1 a déjà produit pour la même map
(`Maps/{Zone}/{Name}-{id}/tilemap/{Name}-{id}.tileMap`). La Phase 2 recharge ce `.tileMap`, y ajoute la propriété, et le
réenregistre.

## Pourquoi ici et pas comme asset CasaEngine

`TileMapData` n'a pas de schéma pour des données de gameplay par cellule — seul son sac de
`CustomProperties` (chaîne → chaîne) peut porter une donnée arbitraire. Le format columnar choisi
ici est fait pour être compact une fois inséré dans cette seule propriété texte : un objet par
cellule (avec ses clés répétées 3120 fois par map) aurait multiplié la taille sérialisée pour rien.

## Pourquoi columnar

Plutôt qu'un tableau d'objets `{walkability, ground_property, ...}` par cellule, le document a un
tableau par champ (`walkability[]`, `ground_property[]`, …), tous alignés sur le même index de
cellule. Les noms de clé JSON ne sont donc écrits qu'une fois par carte au lieu d'une fois par
cellule (jusqu'à 3120 cellules par map), ce qui compte directement puisque ce JSON vit lui-même
comme une chaîne à l'intérieur d'un autre document JSON.

## Ordre des cellules

`index = y * width + x` (l'ordre natif de la grille Alundra). Les tableaux `Walkability`,
`GroundProperty`, `Slope`, `Height`, `TileId`, `Palette`, `Tile`, `Flags`, `WallTilesOffset` ont
tous exactement `cell_count` éléments dans cet ordre — aucun tri, aucune compaction.

## `wall_tiles` : une table creuse

La grande majorité des cellules n'ont pas de pile de tuiles de mur. `WallTilesOffset[i]` ne fait
que porter le pointeur brut d'origine ; le contenu réel de la pile (quand elle existe) est fusionné
séparément depuis le dump natif de la map (`data/map_N.json`, `Map.MapTiles[i].WallTiles`) et n'est
ajouté dans `wall_tiles` que pour les cellules qui en ont une. La clé est l'index de cellule (en
texte, une contrainte JSON), la valeur porte l'offset d'origine et la liste des ids de tuile
empilés. Sur les 483 maps de la conversion complète, 163 881 piles ont été retenues de cette façon
(`Cells.WallTileStacks` dans `report.json`) — un ordre de grandeur bien plus petit que
`483 maps × 3120 cellules`, ce qui justifie le choix d'un dictionnaire creux plutôt qu'un tableau
dense supplémentaire.

## Schéma

| Champ | Type | Signification | Champ source |
|---|---|---|---|
| `map_index` | int | Index de la map Alundra | (paramètre du writer) |
| `cell_count` | int | Nombre de cellules (`width * height`) | `Cells.length` du companion Tiled |
| `walkability[]` | int[] | Walkabilité de la cellule | `Cells[i].Walkability` (companion `map_N.alundra.json`) |
| `ground_property[]` | int[] | Propriété de sol | `Cells[i].GroundProperty` |
| `slope[]` | int[] | Pente | `Cells[i].Slope` |
| `height[]` | int[] | Hauteur | `Cells[i].Height` |
| `tile_id[]` | int[] | Id de tuile brut | `Cells[i].TileId` |
| `palette[]` | int[] | Palette | `Cells[i].Palette` |
| `tile[]` | int[] | Tuile brute | `Cells[i].Tile` |
| `flags[]` | int[] | Flags de cellule | `Cells[i].Flags` |
| `wall_tiles_offset[]` | int[] | Pointeur brut d'origine vers la pile de tuiles de mur (0 si aucune) | `Cells[i].WallTilesOffset` |
| `wall_tiles` | objet, clé = index de cellule (texte) | Piles de tuiles de mur non vides | `Map.MapTiles[i].WallTiles` (dump natif `map_N.json`) |
| `wall_tiles.{index}.offset` | int | Offset d'origine de la pile | `WallTiles.Offset` |
| `wall_tiles.{index}.tiles[]` | int[] | Ids de tuile empilés | `WallTiles.Tiles` |

Les champs de niveau map (`Gravity`, `ZViscosity`, `SlideEffectId`, `BalanceLevel`) sont déjà portés
par les propriétés personnalisées Tiled de la Phase 1 et ne sont pas dupliqués ici.

## Extrait réel

Extrait de `Maps/Ancient Shrine/Ancient Shrine - Golem-34/tilemap/Ancient Shrine - Golem-34.tileMap`
(`CustomProperties["AlundraCells"]`, désérialisé pour lisibilité) :

```json
{
  "map_index": 34,
  "cell_count": 3120,
  "walkability": [0, 0, 0, 0, 0, 0, "... 3120 valeurs ..."],
  "wall_tiles": {
    "296": { "offset": 0, "tiles": [58142] }
  }
}
```
