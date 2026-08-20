# Fonds défilants (`*.backdrop.json`)

Code : [`Readers/BackdropReader.cs`](../../alundra-casaengine-project-converter/Readers/BackdropReader.cs),
[`Writers/BackdropImageBuilder.cs`](../../alundra-casaengine-project-converter/Writers/BackdropImageBuilder.cs),
[`Writers/BackdropWriter.cs`](../../alundra-casaengine-project-converter/Writers/BackdropWriter.cs)
(Phase 9).

## Ce que c'est

`Map.ScrollParameters` : les (jusqu'à deux) couches de décor défilant PSX d'une map - le fond
parallaxe (mer ouverte, ciel...) que l'original dessine derrière (ou, pour les couches marquées
`Ground`, comme une surimpression proche du premier plan) le reste de la scène. Sans cet export,
tout ce qu'aucune tuile de cellule ne couvre affiche la couleur de fond du moteur (turquoise, visible
sur la coque du bateau et sous le bord de certaines maps).

Format source dérivé de `GraphicManager.RenderAllTileLayers`/`RenderLayerToBuffer` (@ 0x8005B670 /
0x8005B848, `alundra-datas-analyser/AlundraTools/AlundraEngine/Graphics/GraphicManager.cs`) et de
`AlundraEngine.DatasBin.ScrollParameters` (`.../AlundraEngine/DatasBin/ScrollScreen.cs`) :

- Chaque couche a un `Mode` : `1` = grille de tuiles 40x30 (16px), `2` = "cellulaire" (sprites
  indépendants animés/déplacés), ou désactivée (`0`).
- Une couche `Mode 1` a sa grille de tuiles dans le blob `Data`, à l'offset `Graphics + 0x8100`
  (2 octets/tuile : index de tuile dans la feuille 256x256, index de palette 0-7) - la deuxième
  moitié (`+0x960`) n'est utilisée par la couche 1 que lorsque les **deux** couches sont en mode 1
  simultanément ; sinon une couche 1 seule en mode 1 relit la même première moitié que la couche 0.
- `LayerInfos.Ground` ne bascule pas simplement arrière-plan/premier plan : `Ground == 0` place la
  couche à une profondeur `-0x10000000 + ordre` (très en dessous de tout sol/mur/entité - un vrai
  fond) ; `Ground != 0` la place à `SpriteDepth.BackgroundUI - 1000 + ordre`, une valeur énorme
  proche de `int.MaxValue` malgré son nom - dessinée **après** tout sol/mur/entité, juste sous
  l'UI : une surimpression (observée sur la couche mer de la map 389, `Ground=1`,
  `BlendMode=Average` : des ombres de nuages qui dérivent au-dessus de l'eau, pas la mer
  elle-même). Les deux cas sont exportés à l'identique ; c'est au moteur consommateur de choisir le
  bucket de profondeur à partir de `ground`.
- Le parallaxe caméra est `cameraX * FactorXNum / FactorXDenom` (et l'équivalent Y) ; l'auto-scroll
  avance d'`ScrollXSpeed` par tick et d'un pixel de plus tous les `|ScrollXPeriod|` ticks (direction
  = signe de `Speed` XOR signe de `Period`). Les deux offsets bouclent sur un canevas fixe de
  640x480 (40x30 tuiles de 16px) : c'est exactement la grille exportée, donc le moteur consommateur
  n'a qu'à répéter la texture pour obtenir le défilement infini.

## Ce qui est exporté

- Pour chaque couche `Mode 1` ("Tiles") non vide : une texture 640x480 pré-rendue (composition
  tuiles + palette, un seul instantané - pas d'animation de tuile ni de scroll baked in),
  enregistrée comme n'importe quelle texture du convertisseur (`.texture` + PNG brut au catalogue)
  sous `Maps/{Zone}/{Name}-{id}/backdrop/{Name}-{id}-layer{N}.png`.
- Un compagnon JSON brut, **pas** un asset CasaEngine (même convention que `events.json`) :
  `Maps/{Zone}/{Name}-{id}/backdrop/{Name}-{id}.backdrop.json`.
- Rien n'est écrit pour une map dont `ScrollParameters.Infos.Enabled` est faux.

## Différé (paramètres bruts exportés, rendu non implémenté)

- Les couches `Mode 2` ("Cellular") : sprites indépendants (oiseaux, nuages, écume...) déplacés par
  caméra/dérive périodique/piste sinusoïdale (`CellType.WaveX`, table `WaveLut`) - un système bien
  plus riche qu'une grille de tuiles. Les paramètres bruts (`Cellular`, `Cells[]`, `WaveLut` au
  niveau map) sont exportés ; aucune texture n'est produite pour ces couches.
- L'incrustation plein écran (`LiningInfos.BGColorR/G/B/A`, `RenderTileOverlayLayer` @ 0x8005BA40) :
  un rectangle/dégradé semi-transparent indépendant des couches de tuiles - non exporté du tout.
- L'animation de tuile (`LayerInfos.AnimTimer` + le décalage V par `AnimFrameCounter`) : la texture
  exportée correspond à l'image statique `AnimFrameCounter == 0`.

## Schéma

Racine :

| Champ | Type | Signification |
|---|---|---|
| `MapIndex` | int | Index de la map Alundra |
| `Enabled` | bool | `ScrollParameters.Infos.Enabled` |
| `AnimNum` | int | Nombre de sous-images d'animation de tuile |
| `WaveLut` | int[256]? | Table de la sinusoïde `WaveX`, partagée par les deux couches ; absente si `WaveLUT == 0` |
| `Layers[]` | objet | Une entrée par couche (0 et 1, toujours 2 entrées) |

`Layers[]` :

| Champ | Type | Signification |
|---|---|---|
| `LayerId` | int | 0 ou 1 |
| `Mode` | string | `"Tiles"`, `"Cellular"` ou `"Disabled"` |
| `DepthOrder` | int | 1 pour la couche 0, 0 pour la couche 1 - voir la note `Ground` ci-dessus |
| `Ground` | bool | Bucket de profondeur (voir ci-dessus) |
| `BlendMode` | int | `LayerInfos.BlendMode` (0=aucun, 1=moyenne, 2=additif, 3=soustractif, 4=additif atténué) |
| `AnimTimer` | int | `LayerInfos.AnimTimer` |
| `TextureAssetId` | guid? | Id catalogue du `.texture`, seulement pour `Mode == "Tiles"` non vide |
| `Width` / `Height` | int | 640/480 pour une couche `Tiles` exportée, 0 sinon |
| `Scrollar` | objet? | Facteurs de parallaxe et auto-scroll, seulement pour `Mode == "Tiles"` |
| `Cellular` | objet? | Paramètres cellulaires + `Cells[]`, seulement pour `Mode == "Cellular"` |

`Scrollar` : `FactorXNum`/`FactorXDenom`, `FactorYNum`/`FactorYDenom`, `ScrollXSpeed`/`ScrollXPeriod`,
`ScrollYSpeed`/`ScrollYPeriod` - copie directe de `AlundraEngine.DatasBin.ScrollScreen`.

`Cellular` : `CountBase`, `AWaveY`, `AWavePhase`, `AWaveAmp`, `BWaveY`, `BWavePhase`, `BWaveWeight`,
`Divisions`, `Cells[]` - copie directe de `AlundraEngine.DatasBin.Cellular`. Chaque `Cells[]` :
`PalDex`, `U0`/`V0`/`U1`/`V1` (rectangle dans la feuille de tuiles), `Type`, `X0`/`Y0`,
`CamXNum`/`CamXDen`/`CamYNum`/`CamYDen` (facteurs de parallaxe caméra), `DX`/`PeriodX`/`DY`/`PeriodY`
(dérive périodique) - copie directe de `AlundraEngine.DatasBin.Cell`.

## Compteurs

- `Backdrop.Maps` : maps dont `Enabled` est vrai (un compagnon JSON écrit).
- `Backdrop.Layers` : total de couches inspectées (2 par map ci-dessus, y compris désactivées).
- `Backdrop.Layers.Tiles` / `.Cellular` / `.Disabled` : répartition par mode.
- `Backdrop.LayersExported` : couches `Tiles` ayant produit une texture (une grille entièrement
  vide, elle, ne produit ni texture ni erreur - il n'y a simplement rien à dessiner).

## Extrait réel

Map 389 (`Ship Klark (beginning)-389`), couche 0 - correspond à `ScrollXPeriod`/`ScrollYPeriod`
10/5 vus dans `data-extracted/data/map_389.json` :

```json
{
  "LayerId": 0,
  "Mode": "Tiles",
  "DepthOrder": 1,
  "Ground": true,
  "BlendMode": 1,
  "AnimTimer": 1,
  "TextureAssetId": "…",
  "Width": 640,
  "Height": 480,
  "Scrollar": {
    "FactorXNum": 1, "FactorXDenom": 1,
    "FactorYNum": 1, "FactorYDenom": 1,
    "ScrollXSpeed": 0, "ScrollXPeriod": 10,
    "ScrollYSpeed": 0, "ScrollYPeriod": 5
  }
}
```
