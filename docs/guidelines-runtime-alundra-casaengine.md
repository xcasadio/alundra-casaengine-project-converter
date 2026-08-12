# Guidelines runtime — Alundra → CasaEngine

Date : 2026-08-08
Référence stable pour tout code de gameplay ou de conversion qui manipule des positions, des
timings ou des assets. À lire avant d'écrire du code dans la DLL gameplay ou dans un `Writer` du
convertisseur.

Documents liés : [demarrage-nouvelle-partie.md](demarrage-nouvelle-partie.md) ·
[plan-conversion-agent-ia.md](plan-conversion-agent-ia.md)

---

## 1. Constantes du jeu source

| Constante | Valeur | Source |
|---|---|---|
| Largeur d'écran | 320 | `StaticVariables.ScreenWidth` |
| Hauteur d'écran | 236 (224 en NTSC) | `StaticVariables.ScreenHeight` |
| Largeur de tuile | **24 px** | `StaticVariables.MapTileWidth` |
| Hauteur de tuile | **16 px** | `StaticVariables.MapTileHeight` |
| Taille de map | **52 × 60 tuiles** = 1248 × 960 px | uniforme sur les 483 maps |
| Fréquence | **PAL 50 Hz** | version France |
| Gravité par défaut | 128 | `Info.Gravity` |

---

## 2. Repères et unités — la table de conversion

C'est la source d'erreur n° 1. Trois systèmes d'unités coexistent dans les données sources.

### 2.0 Résolution et cadrage — deux valeurs, un seul réglage

**Écran natif d'Alundra : 320 × 236** (`AlundraEngine.StaticVariables.ScreenWidth` / `ScreenHeight`
de la décompilation ; le `//224` en commentaire est une estimation antérieure).

Côté CasaEngine, la surface de monde visible vaut **taille de la fenêtre ÷ `Zoom`**
(`Camera2dComponent.ComputeProjectionMatrix` :
`Matrix.CreateOrthographic(viewport.Width / Zoom, viewport.Height / Zoom, …)`). Le `viewport`
sérialisé dans l'asset caméra **ne compte pas** : `CameraComponent.InitializeWithWorld` l'écrase par
`Game.ScreenSizeWidth/Height`, donc c'est la fenêtre réelle qui décide, et `OnScreenResized` la suit.

Le cadrage dépend donc de **deux valeurs écrites dans deux fichiers différents** :

| Valeur | Fichier | Phase |
|---|---|---|
| Taille de fenêtre (`DebugWidth`/`DebugHeight`) | `AlundraGame.json` | 0 |
| `Zoom` de la caméra | `Entities/AlundraCamera.entity` | 6 |

**Règle : `fenêtre = N × (320 × 236)` et `Zoom = N`**, avec N entier. On retrouve alors exactement le
cadrage d'origine, et un texel de tileset couvre N × N pixels écran, ce qu'exige la checklist
pixel-perfect du moteur. Valeur actuelle : **N = 4**, soit une fenêtre 1280 × 944 — une tuile de
24 × 16 occupe 96 × 64 pixels écran, et on voit 13,3 × 14,8 tuiles sur les 52 × 60 d'une map.

> **Piège vécu.** Ces deux valeurs sont un seul réglage ; les laisser diverger produit un projet
> parfaitement valide mais mal cadré. La fenêtre était restée au défaut du moteur (1024 × 768) et le
> `Zoom` à 1 : le jeu affichait 1024 × 768 pixels de monde au lieu de 320 × 236, soit **10 fois trop
> de map à l'écran** — d'où l'impression de caméra « trop loin ». Un `Zoom` entier était nécessaire,
> pas suffisant. D'où `AlundraDisplay` côté convertisseur : une seule constante `PixelScale` alimente
> les deux fichiers, et un test relit les deux pour vérifier que `fenêtre / Zoom == 320 × 236`.
>
> Les pixels sont supposés carrés. L'original tournait sur un téléviseur 4:3 à pixels non carrés :
> un rendu 1:1 est donc 1,7 % plus large en proportion qu'à l'époque. Corriger cet écart imposerait
> une échelle non entière sur un axe et casserait le pixel-perfect — c'est assumé.

### 2.1 Positions d'entité dans `SpriteInfo.Entities` (`XPos`, `YPos`, `Height`)

Elles sont en **demi-tuiles**. L'extracteur Tiled applique déjà la division :

```
tileX      = XPos   / 2
tileY      = YPos   / 2
tileZ      = Height / 2        // élévation, en hauteurs de tuile
pixelX     = tileX * 24                    // = DisplayPixelX
pixelYEcran = (tileY - tileZ) * 16         // = DisplayPixelY  ← le Z est SOUSTRAIT du Y écran
```

Les propriétés `DisplayX`, `DisplayY`, `DisplayHeight`, `DisplayPixelX`, `DisplayPixelY` des objets
`Entity` du `.tileMap` contiennent déjà ces valeurs : **les utiliser plutôt que recalculer**.

### 2.2 Positions runtime d'entité (`Entity.PosX/PosY/PosZ`, `g_cameraTarget*`)

Ce sont des **pixels en virgule fixe 16.16** :

```
pixelX = PosX >> 16
```

Conversion depuis une coordonnée de tuile (celle du save, ex. spawn New Game) :

```
PosX = (tileX * 24 + 12) * 0x10000      // centre horizontal de la tuile
PosY = (tileY * 16 + 8)  * 0x10000      // centre vertical de la tuile
PosZ =  tileZ << 20                     // = (tileZ * 16) << 16 → 1 unité Z = 16 px
```

Exemple, spawn de la nouvelle partie (tuile 33 / 59 / 0) → pixels **(804, 952, 0)**.

### 2.3 Repère CasaEngine

Le rendu de tilemap place la tuile `(x, y)` à :

```
worldX = position.X + x * tileWidth
worldY = position.Y - y * tileHeight     // ← Y VERS LE HAUT
```

(`TileMapComponent.Draw` / `AddStaticTileQuad` : `top = -tileY * tileHeight`.)

**Règle : `worldY = -pixelY_Alundra`.** Le repère Alundra a Y vers le bas, CasaEngine Y vers le
haut. Cette négation est déjà appliquée par le convertisseur pour les positions de parts
d'animation (commit `ee27ed8`) ; elle doit l'être partout ailleurs.

Table de conversion complète pour une entité :

| Alundra | CasaEngine (world, tilemap à l'origine) |
|---|---|
| `pixelX` | `X = pixelX` |
| `pixelY` | `Y = -pixelY` |
| `tileZ` (élévation) | rendu : `Y += tileZ * 16` (le Z remonte à l'écran) ; tri : `Z` / `DepthSortable2DComponent` |

> Le Z d'Alundra n'est pas une profondeur de caméra : c'est une **élévation** qui décale le sprite
> vers le haut de l'écran et intervient dans le tri. Ne pas le mapper naïvement sur le Z de
> CasaEngine, qui sert l'ordre de rendu des couches (`zOffset` 0 / 0,1 / 0,2 / 0,3 ici).

---

## 3. Timings

- Toutes les durées sources (`Delay` de frame, `FrameDuration` de tile) sont en **frames PAL 50 Hz**.
- Conversion : `secondes = frames / 50f`. Le convertisseur utilise déjà
  `PsxFrameSeconds = 1f / 50f` (`SpriteWriter`).
- Ne jamais réintroduire un compteur de frames dans le code de gameplay : CasaEngine passe un
  `FrameTime` / `elapsedTime` en secondes.

---

## 4. Conventions de nommage des assets générés

**Toutes les données d'une map vivent dans un seul dossier**, `Maps/<Zone>/<Nom>-<MapId>/` (revu le
2026-08-09 ; il n'y a plus d'arbres `Worlds/` ni `Events/` de premier niveau) :

| Asset | Convention | Exemple |
|---|---|---|
| TileMap / TileSet / texture | `Maps/<Zone>/<Nom>-<MapId>/tilemap/<Nom>-<MapId>.{tileMap,tileset,tmj}` + `map_<MapId>_tileset.{png,texture}` | `Maps/The Klark/Ship Klark (beginning)-389/tilemap/Ship Klark (beginning)-389.tileMap` |
| World | `Maps/<Zone>/<Nom>-<MapId>/<Nom>-<MapId>.world` | `Maps/The Klark/Ship Klark (beginning)-389/Ship Klark (beginning)-389.world` |
| Table de chaînes de la map | `Maps/<Zone>/<Nom>-<MapId>/dialogues/<Nom>-<MapId>.strings.json` | — |
| Bytecode d'évènements de la map | `Maps/<Zone>/<Nom>-<MapId>/events/<Nom>-<MapId>.events.json` | — |
| Index des worlds | `Maps/world-index.json` | `MapId` → chemin du `.world` |
| Caméra (partagée par les 483 worlds) | `Entities/AlundraCamera.entity` | `Camera2dComponent`, `Target` (624, −480, 0), `Zoom` 1, `PixelSnap` |
| Banque de sprites | `Entities/<NomEntité>/` d'après `EntityNames.csv` ; repli `Entities/bank_<Clé>/` | `Entities/Alundra/`, `Entities/bank_hero_5/` |
| Animation 2D | `bank<Key>_anim<AnimSetIndex>_<down\|up\|left\|right>.anim2d` | `bankhero_0_anim54_down.anim2d` |
| Tables de chaînes globales | `Dialogues/{global-strings,control-codes}.json` | — |

`<Zone>` vient de `alundra-casaengine-project-converter/maps.json` (embarqué avec le convertisseur,
pas avec `data-extracted`) ; les maps absentes de ce fichier tombent dans `Uncategorized`.

Cette disposition est définie **une seule fois**, par `MapLocation`
(`alundra-casaengine-project-converter/Readers/MapCatalogReader.cs`) : aucun writer ne compose de
chemin de map lui-même. La changer se fait là.

**Ordre des directions** : `down, up, left, right` — c'est l'ordre des offsets `Down/Up/Left/Right`
des `AnimSets` d'Alundra, et `g_resetDirectionId = 0` signifie donc **down**.

---

## 5. Contrats du moteur à respecter

### 5.1 Résolution des classes par nom

`ElementFactory.Create<T>(typeName)` cherche par **nom de type simple, insensible à la casse**,
dans tous les assemblies chargés, et garde le **premier** en cas d'homonymie
(`GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First())`).

→ Les classes de la DLL gameplay référencées depuis le JSON (`script_class_name`,
`player_controller_class`) **doivent avoir un nom unique dans tout le processus**. Préfixer
`Alundra…` (`AlundraPlayerController`, pas `PlayerController`).

### 5.2 Chargement de la DLL gameplay

`AssemblyManager.Load(fileName)` fait `Assembly.LoadFile(Path.Combine(EngineEnvironment.ProjectPath, fileName))`
puis instancie **l'unique type implémentant `IPlugin`** et appelle `Initialize()`.

→ La DLL doit être copiée **à la racine du projet** (`alundra-project/`), pas dans un sous-dossier,
et contenir exactement une implémentation d'`IPlugin`.

### 5.3 Cycle de vie d'un `GameplayProxy`

```
Initialize(owner) → InitializeWithWorld(world) → [BeginPlay] OnBeginPlay(world)
  → Update(elapsedTime) … → OnEndPlay(world)
```

`Clone()` est abstrait et obligatoire. `OnBeginPlay` est le bon endroit pour lire les données de
map (`TileMapData.CustomProperties`, `ObjectLayers`) : à ce moment le world et les entités existent.

### 5.4 Écriture d'assets

Toujours via `CasaEngine.EditorServices` :
`EditorAssetWriterService.SaveAsset(fileName, asset)` + `EditorAssetCatalogService.Add(...)` /
`.Save()`. Ne jamais écrire le JSON d'un asset à la main : le schéma appartient au moteur.

### 5.5 Performance

Le moteur interdit les allocations dans `Update` / `Draw` (voir
`CasaEngineMonogame/.github/copilot-instructions.md`). Concrètement pour Alundra :
parser `AlundraCells` (3 120 cellules) **une fois** dans `InitializeWithWorld` / `OnBeginPlay`,
jamais par frame ; pas de LINQ ni d'interpolation de chaînes dans les boucles de gameplay.

---

## 6. Règles de portage depuis la décompilation

Héritées des instructions du repo `alundra-datas-analyser` et du repo moteur :

- **Préserver la logique et l'ordre d'exécution** d'origine ; ne pas « améliorer » spontanément.
- **Conserver les commentaires d'adresse** (`// 80031700`) sur les fonctions portées.
- **Ne pas renommer les champs de sémantique incertaine.** Si un nom de la décompilation est
  trompeur (cas de `g_cameraTargetX`, qui est en fait la position de spawn du héros), documenter
  le fait dans un commentaire plutôt que renommer silencieusement.
- **Séparer les faits des hypothèses** dans les commentaires.
- **Ne jamais jeter une donnée non comprise** : la recopier telle quelle dans les
  `CustomProperties` ou un JSON compagnon, et logger un warning dans le rapport de conversion.
- Attention aux **tailles et signes entiers** (`ushort`, `0xFFFFFFFF` pour `LastMapId`) et à la
  virgule fixe.

---

## 7. Déterminisme du convertisseur

- Trier explicitement toute collection avant écriture ; aucune dépendance à l'ordre de
  `Directory.GetFiles`.
- Pas de `DateTime.Now`, pas de `Guid.NewGuid()` dans un chemin de sortie.
- **Limite connue** : les ids d'assets ne sont **pas** déterministes aujourd'hui
  (`ObjectBase.Id` a un setter privé, `EditorAssetWriterService` sérialise l'id porté par l'objet).
  Conséquence : tout asset qui référence un autre asset par GUID doit être régénéré **dans le même
  run** que sa dépendance. À corriger si des golden files de `.world` sont committés.

---

## 8. Pièges rencontrés (à ne pas refaire)

| Piège | Détail |
|---|---|
| `g_cameraTarget*` vs `g_cameraLookAt*` | Le premier est le spawn du héros, le second la caméra (écrasée chaque frame par `UpdateEntities`). |
| Entités en demi-tuiles | `XPos`/`YPos`/`Height` sont des demi-tuiles, contrairement aux coordonnées de save qui sont en tuiles pleines. |
| Z soustrait du Y écran | `DisplayPixelY = (YPos/2 − Height/2) * 16`. |
| Y inversé | Alundra Y vers le bas, CasaEngine Y vers le haut. |
| Ordre de dessin des parts | Alundra dessine les images d'une frame **de l'arrière vers l'avant en ordre inversé** (commit `2929043`). |
| Palettes | Un PNG par combinaison spritesheet × palette réellement utilisée ; pas de palette swap dans le moteur. |
| `.texture` incomplet | Un wrapper `.texture` mal formé fait échouer le chargement dans l'éditeur (commit `8da356f`). |
