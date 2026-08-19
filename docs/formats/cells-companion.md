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

# Placement des tuiles de mur (`AlundraWallPlacements`)

Code : [`Writers/WallPlacementReplayer.cs`](../../alundra-casaengine-project-converter/Writers/WallPlacementReplayer.cs),
[`Readers/TileSetGidMapReader.cs`](../../alundra-casaengine-project-converter/Readers/TileSetGidMapReader.cs),
branché depuis [`Writers/CellMetadataWriter.cs`](../../alundra-casaengine-project-converter/Writers/CellMetadataWriter.cs)
(toujours Phase 2, même écriture read-modify-write du `.tileMap` que `AlundraCells`).

## Le problème

Les 4 couches "Render_*" du `.tileMap` sont des plans de tuiles **plats** : chaque cellule (sol ou
mur) y a été aplatie par l'exporteur Tiled de l'analyser
(`TiledMapExporter.CreateRendererOrderedTileLayers`, AlundraTools/AlundraDataExtractor/TiledMapExporter.cs:287-358)
selon une règle de "premier plan libre" — mais **quel plan** et **quel slot de profondeur PSX**
chaque tuile de mur a fini par occuper n'est enregistré nulle part. Sans cette information, le
gameplay ne peut pas isoler les tuiles de mur des couches plates pour les re-rendre triées en
profondeur.

## Comment c'est produit : rejeu + vérification, jamais une simple copie

`WallPlacementReplayer.Replay` **rejoue** l'algorithme d'empaquetage de l'exporteur à partir des
mêmes données de cellule que `AlundraCells` (`CellMetadataDocument.TileId`, `.Height`,
`.WallTiles`) :

- même ordre d'itération (`y` puis `x`, `index = y * width + x`) ;
- même séquence par cellule (la tuile de sol rivalise pour un plan avant les tuiles de mur de la
  pile, exactement comme `TiledMapExporter.cs:298-319`) ;
- même règle du premier plan libre, un nouveau plan n'étant créé que si tous les plans existants
  ont déjà leur cellule cible occupée (`AddRendererTile`, `TiledMapExporter.cs:338-358`) ;
- mêmes règles de rejet : `targetY = y - height - offset + stack_index + 1` hors bornes, ou gid nul
  (tuile vide `0xffff`) → la tuile est abandonnée, jamais placée, jamais enregistrée.

Le gid Tiled attendu pour chaque id de tuile brut n'est **pas recalculé** (l'algorithme
d'assignation de gid dépend aussi d'ids de frame d'animation synthétiques, hors sujet ici) mais lu
directement dans le tileset que l'exporteur a réellement produit : chaque tuile locale du
`.tsj` porte sa propre propriété personnalisée `TileId` (id brut PSX,
`TiledMapExporter.CreateTilesetJson`, ligne 143), et son gid vaut toujours `firstgid + id_local`
(`firstgid` fixé à 1). `TileSetGidMapReader` reconstruit ainsi `id brut → gid` sans dépendre du
mode de layout ("original" ou "compact").

Chaque placement prédit est ensuite **vérifié** contre les couches `Render_*` réellement importées
dans le `.tileMap` (celles que la Phase 1 a produites via l'import Tiled du moteur) : le
`local_tile_id` stocké à `(plan, x, y)` doit valoir `gid - firstgid`. **Tout désaccord, sur
n'importe quelle map, est une erreur de conversion** (`report.Errors`), jamais un avertissement —
un désaccord signifierait que ce rejeu ne peut pas être utilisé de façon fiable pour retirer les
tuiles de mur des couches plates au moment du jeu.

## Le slot de profondeur PSX (`depth_slot`)

`GraphicManager.RenderTiles` calcule la profondeur de chaque tuile de mur via
`DepthWallBlock(y, GetTileDepthSlot(wallTileId))`
(AlundraTools/AlundraEngine/Graphics/GraphicManager.cs:287,313-321), et `GetTileDepthSlot` regarde
`g_tileAnimDescriptorTable[tileId & 0x3ff].SpriteIndex`. Cette table de 960 entrées n'est **pas une
donnée par map** : `GameInitializer.CreateTileAnimDescriptors`
(AlundraTools/AlundraEngine/GameInitializer.cs:216-244) la construit de façon purement
algorithmique, indépendamment de tout paramètre (son propre argument `tileX` est écrasé avant
d'être utilisé) — 6 blocs consécutifs de 160 entrées, chacun rempli avec `SpriteIndex = son propre
index de bloc (0..5)`. Le "tableau" est donc une fonction fixe de l'id de tuile brut, reproduite
directement par `WallPlacementReplayer.ComputeDepthSlot` :

```
tileIndex = rawTileId & 0x3ff
depthSlot = tileIndex < 960 ? tileIndex / 160 : 0
```

## Schéma

Document columnar, mêmes raisons que `AlundraCells` (une clé JSON répétée par tuile de mur — 501 962
sur la conversion complète — coûterait cher une fois sérialisée comme chaîne dans une seule
propriété texte). `count` == la longueur de chacun des 8 tableaux.

| Champ | Type | Signification |
|---|---|---|
| `map_index` | int | Index de la map Alundra |
| `count` | int | Nombre de placements (une entrée par tuile de mur non vide et dans les bornes) |
| `cell_x[]` / `cell_y[]` | int[] | Cellule source (`x`, `y`) de la tuile de mur dans la grille Alundra |
| `stack_index[]` | int[] | Index dans `wall_tiles.{index}.tiles[]` (voir `AlundraCells` ci-dessus) |
| `plane[]` | int[] | Index de la couche `Render_{plane}` où la tuile a atterri |
| `x[]` / `y[]` | int[] | Cellule cible dans cette couche (`y = cell_y - height - offset + stack_index + 1`) |
| `gid[]` | int[] | Gid Tiled (1-based) tel qu'il apparaît dans `Render_{plane}.data` (`local_tile_id = gid - firstgid`) |
| `depth_slot[]` | int[] | Slot de profondeur PSX (0..5) tel que le moteur d'origine l'aurait résolu pour cette tuile |

## Compteurs et invariants (`report.json`)

- `WallPlacements.Emitted` : nombre total de placements émis sur la conversion (501 962 sur la
  conversion complète) — enregistré, pas une constante attendue à l'avance.
- `WallPlacements.StacksCovered` : nombre de piles de tuiles de mur parcourues par le rejeu. Doit
  **toujours** égaler `Cells.WallTileStacks` (163 881), puisque les deux compteurs sont incrémentés,
  par map, avec exactement la même valeur (`cellMetadata.WallTiles.Count`) — un écart signifierait
  qu'une map a été traitée par l'un des deux chemins mais pas par l'autre. Vérifié après chaque
  `ConvertMaps`, qu'il porte sur toutes les maps ou seulement un sous-ensemble `--maps`.
- Un compte indépendant (une deuxième passe, qui ne partage aucun état avec la boucle de placement)
  recalcule combien de tuiles de mur sont non vides et dans les bornes ; `document.count` doit lui
  être strictement égal, sans quoi c'est une erreur de conversion.

## Extrait réel

Deux entrées de `Maps/The Klark/Ship Klark (beginning)-389/tilemap/Ship Klark (beginning)-389.tileMap`
(`CustomProperties["AlundraWallPlacements"]`, désérialisé et réduit à une entrée pour lisibilité) :

```json
{
  "map_index": 389,
  "count": 774,
  "cell_x": [17],
  "cell_y": [17],
  "stack_index": [0],
  "plane": [0],
  "x": [17],
  "y": [6],
  "gid": [613],
  "depth_slot": [3]
}
```

Vérifié à la main : `Render_0.data[6 * 52 + 17]` vaut bien `612` (`gid - firstgid`, `firstgid = 1`).
Sur cette même map, les plans 0 à 3 sont tous utilisés par au moins un placement - la règle du
premier plan libre n'est donc pas triviale sur les données réelles.

# Placement des tuiles de sol élevées (`AlundraFloorPlacements`)

Code : même rejeu que ci-dessus, [`Writers/WallPlacementReplayer.cs`](../../alundra-casaengine-project-converter/Writers/WallPlacementReplayer.cs)
(`FloorPlacementDocument`), branché depuis la même fonction `WriteWallPlacements` dans
[`Writers/CellMetadataWriter.cs`](../../alundra-casaengine-project-converter/Writers/CellMetadataWriter.cs).

## Le problème

Dans l'original, les tuiles de SOL participent au même tri de profondeur unifié que les murs -
`DepthFloor(cellY, GetTileDepthSlot(tileId)) = cellY*16 + slot(0..5)`
(AlundraTools/AlundraEngine/Graphics/GraphicManager.cs:275,324-347). Un sol ÉLEVÉ (cellule
`Height > 0`, par exemple le pont supérieur d'un navire) dont la ligne de cellule est **au sud**
d'une entité (`cellY` de la cellule de sol > ligne d'ancrage de l'entité) se dessine **par-dessus**
cette entité. Le port initial gardait toutes les tuiles de sol à plat dans la passe Ground, ce qui
laissait les jambes d'une entité de niveau inférieur transpercer le bord du pont supérieur (observé
sur la map 389).

## Pourquoi seulement `Height > 0`

Un sol au niveau du sol (`Height == 0`) se dessine à la ligne d'écran de sa propre cellule. Une
entité ancrée sur une ligne **plus au nord** est le seul cas où l'original placerait ce sol devant
elle - mais ce cas ne peut jamais se produire visuellement : les sprites s'étendent **vers le haut**
depuis leur point d'ancrage, et une élévation ne fait que les remonter davantage à l'écran. Un sol à
`Height == 0` ne peut donc jamais chevaucher, à l'écran, une entité d'une ligne plus au nord - il n'y
a aucune divergence observable à laisser ce cas plat. Émettre `AlundraFloorPlacements` seulement pour
`Height > 0` couvre donc exactement les cas où le rendu plat diverge de l'original, sans coût inutile
sur le reste (163 881 cellules ont une pile de mur, mais bien plus de cellules ont un sol : borner
aux cellules élevées garde `FloorPlacements.Emitted` très inférieur à `Cells.CellCount` cumulé).

## Schéma

Document columnar, même convention que `AlundraWallPlacements`, sans colonne `stack_index` (chaque
cellule n'a qu'une seule tuile de sol, donc `(cell_x, cell_y)` suffit à l'identifier). `count` == la
longueur de chacun des 7 tableaux.

| Champ | Type | Signification |
|---|---|---|
| `map_index` | int | Index de la map Alundra |
| `count` | int | Nombre de placements (une entrée par tuile de sol élevée non vide et dans les bornes) |
| `cell_x[]` / `cell_y[]` | int[] | Cellule source (`x`, `y`) de la tuile de sol dans la grille Alundra |
| `plane[]` | int[] | Index de la couche `Render_{plane}` où la tuile a atterri |
| `x[]` / `y[]` | int[] | Cellule cible dans cette couche (`y = cell_y - height`) |
| `gid[]` | int[] | Gid Tiled (1-based) tel qu'il apparaît dans `Render_{plane}.data` |
| `depth_slot[]` | int[] | Slot de profondeur PSX (0..5), même formule `GetTileDepthSlot` que pour les murs |

Le rejeu émet les tuiles de sol de chaque cellule dans le même ordre que l'exporteur (sol avant murs,
donc elles rivalisent en premier pour les plans) mais ne les enregistre dans le document que si
`Height > 0` - la compétition de plan avec les murs reste identique dans les deux cas, seul
l'enregistrement change.

## Clé de tri côté moteur

`Alundra/Scripts/WallPlacementOverlay.ApplyFloor`/`ComputeFloorSortKey` retire chaque tuile de sol
élevée de sa couche plate (même mécanisme `TileMapComponent.RemoveTile` +
`AddSortedOverlayTile` que pour les murs) et la réinsère avec
`Elevation = cell_y*16 + clamp(depth_slot, 0, 5)` - **sans** le biais `+7` des murs. Un sol élevé de
ligne `cellY` se trie donc toujours en dessous du slot 6 (les entités triées en Y) de sa propre ligne
et des murs (slot 7+) de cette même ligne, et toujours au-dessus de n'importe quelle tuile d'une
ligne plus au nord - exactement l'ordre `DepthFloor`/`DepthEntity`/`DepthWallBlock` de l'original.

## Compteurs et invariants (`report.json`)

- `FloorPlacements.Emitted` : nombre total de placements de sol élevé émis sur la conversion
  complète - enregistré, pas une constante attendue à l'avance.
- Un compte indépendant (même principe que pour les murs) recalcule combien de tuiles de sol sont
  élevées (`Height > 0`), non vides et dans les bornes après application du seul décalage `Height` ;
  `document.count` doit lui être strictement égal, sans quoi c'est une erreur de conversion.
- Même vérification stricte que pour les murs : chaque `(plane, x, y)` prédit doit contenir le gid
  attendu dans les couches importées - tout désaccord, sur n'importe quelle map, est une erreur.

## Extrait réel

Map 389 (`Ship Klark (beginning)-389`) : 774 placements de mur et 477 placements de sol élevé, pour
1 251 tuiles au total réinsérées dans l'overlay trié en profondeur. Premier placement de sol de cette
map : cellule `(17, 16)`, plan 1, cible `(17, 4)`, gid `593`, slot de profondeur `3`. Vérifié à la
main : `Render_1.data[4 * 52 + 17]` vaut bien `592` (`gid - firstgid`, `firstgid = 1`).

