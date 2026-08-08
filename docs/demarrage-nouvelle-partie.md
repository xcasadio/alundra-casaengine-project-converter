# Démarrage d'une nouvelle partie — analyse et plan

Date : 2026-08-08
Portée : lancer « New Game » comme dans le jeu original, c'est-à-dire reproduire l'état initial
produit par `GameInitializer.InitializeGameState()` puis charger et afficher la **map 389**
(« Ship Klark (beginning) ») dans CasaEngine, avec le héros contrôlable.

Documents liés :
- [rapport-analyse-conversion-alundra-casaengine.md](rapport-analyse-conversion-alundra-casaengine.md) — inventaire des données et capacités moteur
- [plan-conversion-agent-ia.md](plan-conversion-agent-ia.md) — plan du convertisseur de données (phases 0→8)
- [guidelines-runtime-alundra-casaengine.md](guidelines-runtime-alundra-casaengine.md) — conventions techniques à respecter (repères, unités, nommage)

---

## 1. Ce qui est déjà fait

### 1.1 Convertisseur — phases 0 à 3 terminées

Sortie actuelle : `alundra-project/` (projet CasaEngine généré). Compteurs de `report.json` :

| Compteur | Valeur |
|---|---|
| `Assets.TileMap` / `Assets.TileSet` / `Assets.Texture` | 483 / 483 / 483 |
| `Maps` / `Maps.CellMetadata` | 483 / 483 |
| `Cells.WallTileStacks` | 163 881 |
| `Assets.Animation2d` | 9 620 |
| `Assets.Sprite` | 9 327 |
| `Sprites.Banks` | 395 (244 banques de map + banques héros) |
| `Sprites.QuadsRead` == `Sprites.QuadsConverted` | 160 355 (0 quad perdu) |
| `Sprites.Textures` | 104 |
| Erreurs / warnings | 0 / 2 |

Arborescence produite :

```
alundra-project/
├── AlundraGame.json          # ProjectSettings — FirstWorldLoaded et GameplayDllName VIDES
├── AssetInfos.json           # catalogue (~4,5 Mo)
├── Maps/<Zone>/<Nom>-<id>.{tileMap,tileset,tmj}  + map_<id>_tileset.{png,texture}
├── Sprites/bank_<Sector5Id>/…       # banques de map
├── Sprites/bank_hero_<Sector5Id>/…  # banques du héros (map_alundra.json)
├── Sprites/hero/hero_effects.json   # 29 SpriteEffects préservés bruts
├── Sprites/Textures/                # spritesheets
├── Animations/  Sounds/  Musics/  Dialogues/  UI/   ← VIDES
```

### 1.2 Ce que contient déjà la map 389

`Maps/The Klark/Ship Klark (beginning)-389.tileMap` est complet et exploitable :

- `map_size` 52 × 60, tuiles 24 × 16 (`tile_set_asset_id` renseigné) ;
- 4 tilelayers `Render_0..3` (3 120 tuiles chacun, `z_offset` 0 / 0,1 / 0,2 / 0,3) ;
- 3 object layers : **Portals (4)**, **MapEvents (7)**, **Entities (19)**, tous les champs natifs
  recopiés en `custom_properties` (voir exemples ci-dessous) ;
- `custom_properties.AlundraCells` : JSON inline avec `walkability`, `ground_property`, `slope`,
  `height`, `flags`, `tile_id`, `palette`, `tile`, `wall_tiles`, `wall_tiles_offset`
  (3 120 cellules) ;
- `custom_properties` de map : `MapId`, `Gravity`, `ZViscosity`, `SlideEffectId`, `BalanceLevel`.

Exemple d'entité (`Entities[0]`) :

```json
{ "name": "Entity_0", "x": 408.0, "y": 208.0,
  "custom_properties": { "SpriteTableIndex": "25", "SpriteDirection": "64",
    "XPos": "34", "YPos": "72", "Height": "46",
    "EventCodesA_LoadIndex": "133", "…": "…", "EntityName": "Bloc transparent (1×1×2)" } }
```

Exemple de portail (`Portals[0]`) :

```json
{ "name": "Portal_0", "custom_properties": {
    "X1": "18", "Y1": "38", "X2": "18", "Y2": "38",
    "DestMapId": "390", "DestTileX": "10", "DestTileY": "40", "ZLevel": "0", "Flags": "20481" } }
```

### 1.3 Les animations du héros existent

La banque du héros est `Sprites/bank_hero_0/`, 1 017 fichiers, nommage
`bankhero_0_anim<N>_<down|up|left|right>.anim2d`. **`bankhero_0_anim54_down.anim2d` existe** —
c'est exactement l'animation de départ demandée par le jeu original (`g_resetAnimationId = 0x36`,
`g_resetDirectionId = 0`).

---

## 2. Ce que fait le jeu original sur « New Game »

Chaîne d'appels (décompilation, `alundra-datas-analyser/AlundraTools/AlundraEngine/`) :

```
GameInitializer.Initialize()
  └─ InitializeAlundraSpriteResourcesFromFile()
       └─ InitializeSpriteTileLayouts()
            └─ InitializeGameState()      ← 0x80031700
  … puis GameEngine boucle : g_desiredMap != g_currentMap → LoadMap(389)
       └─ ResetEntityState()              ← place le héros
```

`InitializeGameState()` branche **`g_saveDataInRam.SlotData == 0`** = nouvelle partie
(`== 1` = chargement d'une sauvegarde, l'autre branche est du debug vers la map 11).

### 2.1 État initial exact (branche New Game)

| Donnée | Valeur | Source |
|---|---|---|
| Map de départ | **389** | `g_saveData.InitialMapId = 389` |
| Position du héros (tuiles) | **X=33, Y=59, Z=0** | `CameraTileX/Y/Z` → repris tels quels par `ResetEntityState()` comme position d'entité |
| Position du héros (pixels) | **(804, 952)**, Z=0 | `(33·24 + 12, 59·16 + 8)` |
| `playerTileX/Y/Z` (caméra initiale) | 33 / 35 / 0 → `g_cameraLookAt` = (804, 568, 0) | écrasé dès la 1re frame par le suivi du héros |
| Animation de départ | **54** (`0x36`) | `g_resetAnimationId` |
| Direction de départ | **0 = down** | `g_resetDirectionId` |
| HP / HP max | **10 / 10** | `SetPlayerHp(10)`, `SetPlayerHpMax(10)` |
| MP / MP max | **0 / 0** | `SetPlayerMp(0)`, `SetPlayerMpMax(0)` |
| Argent | **0** | `SetMoney(0)` |
| Arme équipée | **weaponId = 1** | `SetPlayerWeaponId(1)` |
| Objets de départ | **ids 1, 17, 25** | boucle 0..97 sur `g_itemDropProperties[i].Field3 & 0x80` |
| `g_saveData.LastMapId` | `0xFFFFFFFF` | — |
| `g_saveData.SaveSlotIndex` / `GameTime` | 0 / 0 | — |
| `g_mapTransitionEffectId` | 0 | pas d'effet de transition à l'ouverture |
| Flags de jeu | tous à 0 | `ResetGameFlags()` |
| `MapIdToInternalMapIndexTable[i]` | `= i` (identité) | `ResetGameFlags()` |
| Stats/inventaire remis à zéro avant | HpMax=1, Hp=1, MP=0, argent=0, Falcon=0, 256 compteurs d'objets à 0, `g_itemsCount = 99` | `InitializePlayerStatsAndItems()` |

> **Attention au nommage trompeur de la décompilation** : `g_cameraTargetX/Y/Z` sert de **position
> de spawn du héros** (`ResetEntityState()` la passe à `InitializeEntity`), tandis que
> `g_cameraLookAtX/Y/Z` est la **position de la caméra**, réécrite chaque frame par
> `UpdateEntities()` : `g_cameraLookAtX = g_entityFollowedByCamera.PosX >> 16`.
> Le héros est `g_entityFollowedByCamera` (`StaticVariables.PlayerEntity`).

### 2.2 Banque de sprites du héros

`ResetEntityState()` fait `GetSpriteFromSpriteTable(false, 0, …)` → **SpriteRecord n° 0 de
`map_alundra.json`**, donc `Sprites/bank_hero_0/` côté converti. Caméra native 320 × 236.

---

## 3. Comment CasaEngine lance un jeu

Chaîne de boot vérifiée dans le moteur :

```
Launcher/Program.cs
  EngineEnvironment.ProjectPath = dossier du .json projet
  new CasaEngineGame(<projet>.json)
    └─ ProjectSettingsHelper.Load()
         ├─ applique WindowTitle / résolution / IsMouseVisible…
         ├─ GameSettings.AssemblyManager.Load(GameplayDllName)   ← charge la DLL gameplay
         │     (Assembly.LoadFile(ProjectPath/<dll>), instancie l'unique IPlugin, .Initialize())
         └─ charge AssetInfos.json (AssetCatalog)
    └─ GameManager.EndLoadContent() → SetWorldToLoad(FirstWorldLoaded)
    └─ GameManager.UpdateWorld()
         ├─ AssetContentManager.Load<World>(id)
         ├─ World.LoadContent()
         │     ├─ LoadPlayerStartupSettings()   (asset .gameMode)
         │     ├─ instancie les entity_references
         │     ├─ InitializePlayerControllers() : spawn du DefaultPawn + PlayerController
         │     │     (place le pawn sur le PlayerStartComponent s'il en existe un)
         │     └─ crée le GameplayProxy du world (script_class_name)
         ├─ RuntimeViewBootstrapper.BootstrapViews()  (caméra/vue)
         └─ World.BeginPlay()
               ├─ StartGameplayModeAsset()  (gameplay_mode_asset_id → GameplayMode)
               └─ GameplayProxy.OnBeginPlay() sur le world puis sur chaque entité
```

Points structurants :

- **`ElementFactory.Create<T>(string typeName)`** résout les classes **par nom simple**, dans tous
  les assemblies chargés. C'est le pont entre le JSON (`script_class_name`,
  `player_controller_class`) et la DLL gameplay.
- Un `.world` contient : `entity_references`, `script_class_name`, `player_startup_settings_asset_id`
  (ou l'ancien `game_mode_asset_id`), `gameplay_mode_asset_id`, `environment`.
- Un `.entity` contient : `root_component` (+ `children_component`), `components`,
  `script_class_name`, `flow_graph`.
- Le projet de référence est **`CasaEngineMonogame/Projects/RPGDemo/`** (+ la DLL
  `Projects/CasaEngine.RPGDemo/`) : RPG top-down 2D, `character_link.entity` avec
  `AnimatedSpriteComponent` (liste d'ids `.anim2d`) + `DepthSortable2DComponent`
  (`render_pass: YSortedWorld`) + `script_class_name: "ScriptPlayer"`.

---

## 4. Écart : ce qu'il manque pour lancer la partie

| # | Manque | Nature | Bloquant pour « New Game » |
|---|---|---|---|
| 1 | Aucun `.world` généré (phase 6 du plan non faite) | Convertisseur | **Oui** |
| 2 | `AlundraGame.json` : `FirstWorldLoaded` et `GameplayDllName` vides | Convertisseur | **Oui** |
| 3 | Pas d'asset `.entity` pour le héros (liste des `.anim2d` de `bank_hero_0`) | Convertisseur | **Oui** |
| 4 | Pas de `PlayerStartupSettings` (`.gameMode`) | Convertisseur | **Oui** |
| 5 | Pas de DLL gameplay `AlundraGame.Gameplay` (IPlugin, GameplayProxy, controller) | Nouveau projet C# | **Oui** |
| 6 | Pas d'exécutable de lancement dédié (le `CasaEngine.Launcher` générique suffit en V1) | — | Non |
| 7 | Physique 2.5D (Walkability / GroundProperty / Slope / Height / WallTiles / gravité Z) | DLL gameplay | Non (V1 sans collision) |
| 8 | Interpréteur du bytecode événementiel (programmes A–F, map events) | DLL gameplay | Non (V1 sans events) |
| 9 | Portails / téléportation entre worlds | DLL gameplay | Non (V1 mono-map) |
| 10 | Audio (phase 4), textes + police (phase 5), UI/HUD + `BALANCE.BIN` (phase 7) | Convertisseur | Non |
| 11 | Table `g_itemDropProperties` (98 entrées) — vit dans `StaticVariables.cs` de la décompilation, **pas** dans `data-extracted` | Extraction à faire | Non (3 objets en dur suffisent en V1) |
| 12 | Parallaxe / `ScrollParameters`, palette swap | DLL gameplay / rendu | Non |

---

## 5. Plan d'implémentation

L'ordre est choisi pour obtenir le plus tôt possible une **image à l'écran**, puis un héros animé,
puis un héros qui bouge.

### E0 — Décisions préalables

- Nom de la DLL gameplay : `AlundraGame.Gameplay.dll`, projet
  `alundra-game-gameplay/AlundraGame.Gameplay.csproj` à la racine du repo, ajouté au `.slnx`.
  Sa sortie doit être copiée à la racine de `alundra-project/` (c'est là que
  `AssemblyManager.Load()` la cherche : `Path.Combine(EngineEnvironment.ProjectPath, fileName)`).
- Les `.world` sont générés dans `alundra-project/Worlds/<Zone>/<Nom>-<id>.world`
  (ajouter `Worlds` à `ProjectWriter.ContentFolders`).
- V1 = **une seule map jouable** (389). Les 482 autres `.world` sont générés mais non testés.

### E1 — Convertisseur : phase 6 minimale (worlds)

Nouveau `Writers/WorldWriter.cs` :

1. Un `.world` par map, contenant au minimum :
   - entité `tileMap` avec un `TileMapComponent` → `tile_map_data_asset_id` du `.tileMap` de la map ;
   - entité `camera` avec un `CameraTargeted2dComponent` (ou `Camera3dIn2dAxisComponent` en repli,
     comme RPGDemo) ;
   - entité `PlayerStart` avec un `PlayerStartComponent` positionné au spawn de la map
     (pour la map 389 : tuile 33/59/0 → voir la conversion de repère dans les guidelines).
2. `player_startup_settings_asset_id` → asset `.gameMode` commun `Worlds/AlundraPlayer.gameMode`
   (`default_pawn_asset_id` = entité héros, `player_controller_class` = `"AlundraPlayerController"`).
3. Les entités / portails / map events restent **là où ils sont déjà** : dans les `object_layers`
   du `.tileMap`. Ne pas les dupliquer en `entity_references` tant que la DLL gameplay ne sait pas
   les instancier — la DLL les lira depuis `TileMapData.ObjectLayers` au `OnBeginPlay`.
4. Enregistrer chaque `.world` au catalogue.

**Acceptation** : les 483 `.world` rechargent via `AssetContentManager.Load<World>()` sans exception.

### E2 — Convertisseur : entité héros + settings projet

1. `Writers/HeroWriter.cs` : génère `Sprites/hero/alundra.entity` —
   `AnimatedSpriteComponent` en root avec la liste des ids `.anim2d` de `bank_hero_0`,
   `DepthSortable2DComponent` (`render_pass: YSortedWorld`, `sort_mode: TopDownYUp`),
   `script_class_name: "ScriptAlundra"`.
   Écrire aussi `Worlds/AlundraPlayer.gameMode` qui le référence.
2. `ProjectWriter` : `FirstWorldLoaded = "Worlds/The Klark/Ship Klark (beginning)-389.world"`,
   `GameplayDllName = "AlundraGame.Gameplay.dll"`.
   > 1 017 fichiers dans `bank_hero_0` : mesurer le coût de chargement d'une entité qui liste
   > ~1 000 `.anim2d`. Si c'est trop lourd, ne lister en V1 que les animations réellement
   > utilisées (au minimum les 4 directions de l'anim 54) et charger le reste à la demande.

**Acceptation** : `dotnet run` du convertisseur produit un projet dont `AlundraGame.json` pointe
vers un world existant ; l'entité héros recharge via `Load<Entity>()`.

### E3 — DLL gameplay : squelette + boot « New Game »

Projet `AlundraGame.Gameplay` (net9.0-windows, référence `CasaEngine.csproj`) :

- `Plugin.cs : IPlugin` — point d'entrée obligatoire.
- `AlundraGameState.cs` — **port fidèle de `InitializeGameState()`, branche New Game** :
  `InitialMapId = 389`, spawn tuile (33, 59, 0), HP 10/10, MP 0/0, argent 0, `WeaponId = 1`,
  objets 1/17/25, `GameFlags` à 0, `MapIdToInternalMapIndexTable` identité, `GameTime = 0`,
  `ResetAnimationId = 54`, `ResetDirectionId = 0`. Garder les noms d'origine et les commentaires
  d'adresse (`// 0x80031700`) — cf. règles de rétro-ingénierie du repo moteur.
- `ScriptAlundra.cs : GameplayProxy` — `OnBeginPlay` : positionne le héros au spawn issu de
  `AlundraGameState` et joue `bankhero_0_anim54_down`.
- `AlundraPlayerController.cs : PlayerController`.
- `AlundraWorldProxy.cs : GameplayProxy` (script du world) — lit `TileMapData.CustomProperties`
  (`AlundraCells`, `Gravity`, `ZViscosity`) et les `ObjectLayers` ; en V1, se contente de logger
  les compteurs (19 entités / 4 portails / 7 map events pour la map 389) pour valider la lecture.

**Acceptation** : le launcher ouvre `alundra-project/AlundraGame.json`, la map 389 s'affiche,
le héros apparaît au bon endroit avec l'animation 54 vers le bas.

### E4 — Déplacement du héros (sans physique Alundra)

- Ajouter un `.buttonsMapping` (calqué sur `Projects/RPGDemo/buttonsMapping.buttonsMapping`)
  avec les actions Alundra : `MoveUp/Down/Left/Right`, `Action`, `Attack`, `Jump`, `Menu`.
- `AlundraPlayerController` : déplacement libre, choix de la direction (`down/up/left/right`) et
  bascule vers les animations de marche correspondantes.
- Caméra : `CameraTargeted2dComponent.Target` = entité héros (équivalent de
  `g_entityFollowedByCamera`).

**Acceptation** : le héros se déplace, la caméra le suit, les animations changent de direction.

### E5 — Collision 2.5D minimale

Composant `AlundraCellsComponent` dans la DLL : parse `AlundraCells` une fois, expose
`GetWalkability(x, y)`, `GetHeight(x, y)`, `GetSlope(x, y)`. Le controller bloque le déplacement
sur les cellules non marchables. La gravité Z, les pentes et les piles de murs viennent après.

### E6 — Portails

`AlundraPortalSystem` : teste la tuile du héros contre les zones `X1..Y2` des objets `Portal` ;
sur déclenchement, `GameManager.SetWorldToLoad(<world de DestMapId>)` et report du spawn
(`DestTileX/Y`, `ZLevel`). Nécessite une table `MapId → nom de fichier .world`, à générer par le
convertisseur en même temps que les worlds.

---

## 6. Points à trancher

1. **Où vivent les entités de map ?** Choix retenu par défaut : elles restent dans les
   `object_layers` du `.tileMap` et la DLL les instancie au runtime. L'alternative (les écrire en
   `entity_references` dans le `.world`) alourdit fortement les 483 worlds (9 631 entités au total)
   et fige des choix de composants avant que la DLL gameplay existe.
2. **Liste complète ou partielle des `.anim2d` sur l'entité héros** (cf. E2).
3. **`g_itemDropProperties`** : extraire la table depuis `StaticVariables.cs` vers un JSON de
   données, ou la coder en dur dans la DLL en V1 ? (3 objets seulement sont concernés au démarrage).
4. **Ids déterministes** : la phase 3 documente que les ids d'assets ne sont **pas** déterministes
   aujourd'hui (`ObjectBase.Id` a un setter privé). Les worlds vont référencer des ids d'assets ;
   si les ids changent à chaque run, les `.world` doivent être régénérés dans le même run que leurs
   dépendances. À valider avant de committer des golden files de worlds.

---

## 7. Suivi

| Étape | Statut |
|---|---|
| E0 — décisions | ⏳ |
| E1 — WorldWriter | ⏳ |
| E2 — entité héros + settings projet | ⏳ |
| E3 — DLL gameplay + boot New Game | ⏳ |
| E4 — déplacement + caméra | ⏳ |
| E5 — collision 2.5D minimale | ⏳ |
| E6 — portails | ⏳ |
