# Rapport d'analyse — Conversion des données Alundra (PSX) vers un projet CasaEngine

Date : 2026-07-13
Sources analysées :
- `data-extracted/` — export produit par `AlundraDataExtractor` (repo `alundra-datas-analyser`)
- `CasaEngineMonogame/` — submodule du moteur (référencé par le csproj du convertisseur)
- `alundra-casaengine-project-converter/` — projet console .NET 9 (actuellement vide)

---

## 1. Objet

Convertir l'intégralité des données extraites du jeu PSX Alundra (version France, PAL 50 Hz) en un
projet CasaEngine complet et chargeable : maps, tilesets, sprites animés, entités, sons, musiques,
polices, UI et textes. Ce rapport inventorie les données sources, cartographie les capacités du
moteur, établit la matrice de correspondance et identifie les écarts (ce que CasaEngine ne sait pas
encore gérer).

---

## 2. Inventaire des données sources (`data-extracted/`)

### 2.1 Vue d'ensemble

| Dossier | Contenu | Volume |
|---|---|---|
| `data/` | 483 maps natives (`map_N.json` + `map_N_tilesheet.png` + `map_N_spritesheet.png`), `map_alundra.json` (banque du héros), `BALANCE.BIN.json`, `ETC_RES.R.json`, `loading_screen.png` | 1454 fichiers |
| `data/tiled/` | Export Tiled 1.10 : `map_N.tmj`, `map_N_tileset.tsj` + `.png`, `map_N.alundra.json` (compagnon) | 1932 fichiers |
| `sound/bgm/` | 45 musiques WAV + `bgm.json` (métadonnées de boucle) | 45 WAV |
| `sound/sfx/` | 961 effets sonores (996 WAV, un par « tone ») + `sfx.json` | 996 WAV |
| `ui/` | `font3.png/json` (police bitmap, 256 glyphes), `wind.png/json` (atlas UI fenêtres, 277 pièces) | 4 fichiers |
| `memorycard/` | 3 frames PNG | 3 fichiers |

### 2.2 Statistiques agrégées (calculées sur les 483 maps)

| Métrique | Valeur |
|---|---|
| Taille des maps | **toutes 52 × 60 tiles** de **24 × 16 px** (3120 cellules/map) |
| Entités actives (`IsEnabled`) | 9 631 |
| Map events | 1 714 |
| Portails valides | 3 316 |
| Sprite records (par map) | 2 507 — soit **244 banques uniques** (dédup par `Sector5Id`) |
| Chaînes de dialogue par map (hors `#Disuse`) | ~9 873 |
| Cellules avec piles de murs (`WallTiles`) | 163 881 |

### 2.3 Format natif d'une map (`data/map_N.json`)

Décodage complet du binaire PSX, ~2,2 Mo par map. Blocs principaux :

- **`Info`** : `MapId`, `Gravity` (128), `ZViscosity`, `SlideEffectId`, `BalanceLevel`,
  32 palettes CLUT, `SpriteMapEntries[6]` (animations de tiles : `NumberOfFrame`, `TileHeight`,
  `FrameDuration`, `Index`), `Portals[64]` (`X1,Y1,X2,Y2` zone de déclenchement,
  `DestMapId`, `DestTileX/Y`, `ZLevel`, `Flags` ; entrée invalide si `X2==255 && Y2==255`).
- **`Map`** : `Width/Height` (52×60), `MapCopies[256]`, `MapTiles[3120]` — par cellule :
  `Walkability`, `GroundProperty`, `Slope`, `Height`, `TileId` (brut), `Palette`, `Tile`,
  `Flags`, et pile de murs optionnelle `WallTiles { Offset, Count, Tiles[] }` (rendu vertical 2.5D).
- **`SpriteInfo`** :
  - `Entities[128]` : zone d'activation (`XMin/YMin/XMax/YMax`), position (`XPos/YPos`, `Height`),
    `SpriteTableIndex` (→ banque de sprites), `SpriteDirection`, et **6 indices de programmes
    événementiels** : `EventCodesA_Load`, `B_Map`, `C_Tick`, `D_Touch`, `E_Deactivate`, `F_Interact`.
  - `MapEvents[64]` : zone (`X1,Y1,X2,Y2`) + `EventCodesBIndex` (déclenchement zonal).
  - `EventCodes` : tables d'offsets A–F + `Codes[]` — **bytecode événementiel du jeu** (scripts).
  - `SpriteTable[257]` → `SpriteRecords[257]` : banques de sprites (voir 2.5).
  - `SpriteEffectTable/Records`, 41 palettes sprites.
- **`ScrollParameters`** : fonds défilants/parallaxe — 2 couches, tilesheet 256×256 dédié,
  facteurs de parallaxe (`FactorX/YNum/Denom`), vitesses/périodes de scroll, LUT d'ondes
  (`WaveLut[256]`, effets sinusoïdaux), blend modes, animation cellulaire, couleur de fond.
- **`Strings[128]`** : dialogues de la map (français, codes de contrôle `\N`, `\C#`, etc.).

### 2.4 Export Tiled (`data/tiled/`)

Produit par le même extracteur (voir `alundra-datas-analyser/docs/alundra-tiled-map-exporter-usage.md`) :

- **`map_N.tmj`** — Tiled 1.10, orthogonal, 24×16. Couches :
  - `Render_*` (1 à N tilelayers) : packing minimal de l'ordre de rendu du jeu, propriétés custom
    `Z` / `RenderPlane` par couche.
  - Objectgroups `Portals`, `MapEvents`, `Entities` : tous les champs natifs sont recopiés en
    propriétés custom des objets (positions en pixels déjà calculées).
  - Propriétés de map : `MapId`, `Gravity`, `ZViscosity`, `SourceJson`, `AlundraCompanionJson`…
- **`map_N_tileset.tsj`** — tileset « compact » (uniquement les tiles utilisées) ; par tile :
  `TileId` (brut), `Palette`, `Tile`, et **animations Tiled** (`animation[].duration` en ms,
  converties depuis les frames PAL 50 Hz ; durée brute conservée dans
  `AnimationFrameDurationPsxFrames`).
- **`map_N.alundra.json`** — compagnon par cellule : tout ce qui ne rentre pas dans Tiled
  (`Walkability`, `GroundProperty`, `Slope`, `Height`, `WallTilesOffset`, `TileId`, `Palette`,
  `Tile`, `Flags` ; ordre `y * Width + x`).

> L'export Tiled est **à sens unique** (pas de réimport vers le format du jeu) — contrainte sans
> impact ici puisque la cible est CasaEngine.

### 2.5 Sprites et animations (`SpriteRecords` + `map_alundra.json`)

Chaque banque (`SpriteRecord`) contient :

- **Header** : `Sector5Id` (identifiant unique de la ressource), boîte de collision 3D
  (`OffsetX/Y/Z`, `SizeX/Y/Z`), flags (`CanPickup`, ombre/portrait), IDs de programmes
  (`ProgramLoad/Tick/Touch/Deactivate/Interact`), `BreakEffect`, `Contents`.
- **AnimSets[]** : jeux d'animations à 4 directions (`Down/Up/Left/Right` offsets), `Speed`,
  `Sfx`, `Acceleration`.
- **Animations → Frames[]** : `Delay` (timing), `CollisionData` 3D par frame
  (`OffsetX/Y/Z`, `Width/Depth/Height`), et **`Images[]` : 1 à N quads texturés par frame** —
  chaque quad référence `Spritesheet` (index), `Palette`, région source (`Sx,Sy,Swidth,Sheight`)
  et 4 coins destination (`X1..Y4`, permettant offsets, miroirs et retournements).

`map_alundra.json` est la banque du héros : `SpriteRecords` + 29 `SpriteEffects` +
spritesheet dédiée (`map_alundra_spritesheet.png`) + 128 chaînes globales.

**Point d'attention** : les PNG `map_N_spritesheet.png` sont des rendus avec une palette donnée ;
les frames référencent `(Spritesheet, Palette)` par quad — les variantes de couleur (ennemis
recolorés) nécessitent de générer un PNG par combinaison palette utilisée, ou un système de
palette-swap côté moteur (inexistant à ce jour).

### 2.6 Audio (`sound/`)

- **BGM** : 45 WAV stéréo. `bgm.json` : `SoundIndex`, `Frames`, `DurationSeconds`,
  **`LoopDetected`**, pics/RMS, `FirstAudibleFrame`. Les boucles sont des boucles de piste
  (pas de points intro→boucle explicites dans le JSON).
- **SFX** : 961 entrées dans `sfx.json` (996 WAV : une entrée peut avoir plusieurs `Tones`).
  Par tone : `File`, **`SampleRate` d'origine** (ex. 9604 Hz), **`LoopStart`/`LoopEnd`
  (en samples)**, `Repeat`. Champs séquenceur PSX conservés (`VabId`, `ProgramNumber`, `Note`,
  `MaxVoices`).

### 2.7 UI, police, textes globaux

- **`ui/font3.json`** : 256 glyphes — `Code`, `X`, `Y`, `Width`, `Height` (16×16), `Palette`.
  C'est une police bitmap classique (format BMFont-compatible après conversion).
- **`ui/wind.json`** : 277 pièces d'atlas (`U0`, `V0`, `Width`, `Height`, `PaletteIndex`) —
  bordures/fonds des fenêtres de dialogue et menus.
- **`data/ETC_RES.R.json`** : table de chaînes globale id → texte (FR) : titres de chapitres,
  noms de PNJ, noms + descriptions d'objets, messages système et Memory Card.
- **`data/BALANCE.BIN.json`** : 512 `BalanceRecords` (courbes `Level`, `Hp`,
  `OffsetToNextLevel`, `Values[11]`, `AnimVals[]`) — données d'équilibrage gameplay.

---

## 3. Capacités de CasaEngine (état des lieux)

Cartographie vérifiée sur le submodule `CasaEngineMonogame` (moteur C#/MonoGame, runtime + éditeur).

### 3.1 Projet et catalogue d'assets

- Projet = `{Nom}.json` à la racine (`WindowTitle`, `ProjectName`, `FirstScreenName`,
  `FirstWorldLoaded`, **`GameplayDllName`**…) + **`AssetInfos.json`** (catalogue : `id` GUID,
  `name`, `file_name`, `asset_type` inféré de l'extension).
- Classes : `CasaEngine/Framework/Assets/AssetInfo.cs`, `AssetCatalog.cs`.
- Exemple concret : `CasaEngine.Demos/Content/` (layout `Maps/`, `TileSets/`, `Textures/`, …).
- **Écriture** : les classes runtime n'ont que `Load(JObject)` ; la sérialisation vit dans
  **`CasaEngine.EditorServices`** (`EditorAssetWriterService.SaveAsset(fileName, object)` —
  public, `EditorJsonSaveHelper`). Le convertisseur devra référencer ce projet.

### 3.2 Tile maps

- Assets natifs : **`.tileMap`** (`TileMapData` : `map_size`, `tile_set_asset_id`, `layers[]`
  avec `z_offset` + ids de tiles, `ObjectLayers`, **`CustomProperties`**) et **`.tileset`**
  (`TileSetData` : `sprite_sheet_asset_id`, `tile_size`, `tiles[]` typées).
- Types de tiles : `Static`, **`Animated`** (`AnimatedTileData` : frames + durées), `Auto`.
- Collision par tile : `TileCollisionType` = `None` / `Blocked` / `NoContactResponse`
  + forme `collision` optionnelle + `is_breakable`.
- **Importeur Tiled existant** : `CasaEngine.EditorServices/Tiled/TiledMapImporter.cs` —
  `.tmx` **et `.tmj`**, orthogonal fini, **avec animations de tiles, objectgroups et
  propriétés custom** (vérifié dans le code : `ReadTileAnimationsJson`, gestion `objectgroup`).

### 3.3 Sprites et animations 2D

- **`.sprite`** (`SpriteData`) : `sprite_sheet_asset_id`, `location` (région texture),
  `hotspot` (origine), **`collisions[]`** (`Defense`/`Attack`, formes `Rectangle`/`Circle`/
  `Polygon`/…), `sockets[]`.
- **`.anim2d`** (`Animation2dData`) : type (`Loop`/`OneShot`), **`parts[]` multi-parties**
  (position, draw order, visibilité, **flip X/Y** par défaut) + **`tracks[]`** de keyframes
  temporelles (sprite, position, visibilité…). Le modèle multi-parts correspond bien aux frames
  Alundra composées de plusieurs quads.

### 3.4 Textures, audio, UI, dialogues, mondes

- **Textures** : PNG/JPG/BMP/TGA via `Texture2D.FromStream` (`Texture2DLoader.cs`) ;
  wrapper `.texture` (sampler state).
- **Audio** : `SoundEffect`/`Song` MonoGame ; classe `Framework/Audio/Sound.cs` minimale ;
  `AudioComponent` (3D). **Pas de format d'asset audio natif, pas d'exposition des loop points.**
- **UI** : MGUI (XAML, `UIScreenAsset`), rendu texte via **FontStashSharp**
  (TTF ; `StaticSpriteFont` compatible BMFont disponible dans la lib). **Pas d'asset police
  bitmap natif dans le moteur.**
- **Dialogues** : système Yarn (`.dialogue` compilé + `line_texts` pour les textes localisés).
- **Cutscenes** : `.cutscene` (arbre d'actions Sequence/Parallel/Wait/MoveTo…).
- **Monde/entités** : `.world` (`entity_references`), modèle entité-composants :
  `SpriteRendererComponent`, **`TileMapComponent`**, `Physics2dComponent` (formes 2D),
  `CharacterControllerComponent`, caméras, `WorldUIComponent`…
- **Scripting** : DLL gameplay externe (`GameplayDllName`) + compilation dynamique Roslyn
  (`CasaEngine.DotNetCompiler`).

---

## 4. Matrice de correspondance Alundra → CasaEngine

| # | Donnée source | Cible CasaEngine | Faisabilité | Écart / travail spécifique |
|---|---|---|---|---|
| 1 | Tilesheets, spritesheets, PNG divers | `Content/Textures/*.png` + entrées catalogue | **Directe** | Aucun |
| 2 | Tiles + couches `Render_*` | `.tileset` + `.tileMap` (ou import `.tmj` via `TiledMapImporter`) | **Directe** | Multi-couches et animations supportées |
| 3 | Animations de tiles (`SpriteMapEntries` / `.tsj`) | `AnimatedTileData` | **Directe** | Conversion durées PAL 50 Hz → secondes déjà faite dans l'export Tiled |
| 4 | Walkability / GroundProperty / Slope / Height / WallTiles (par cellule) | `CustomProperties` de `TileMapData` ou asset compagnon + composant gameplay | **Moyenne** | **Le moteur n'a pas de physique 2.5D top-down** (hauteur, pentes, gravité Z). Données à préserver telles quelles ; la physique sera dans la DLL gameplay |
| 5 | Banques de sprites (244 uniques) : frames multi-quads, collisions 3D | `.sprite` + `.anim2d` multi-parts | **Moyenne** | Quads multiples → parts ; collisions 3D → shapes 2D + Z conservé en custom ; palettes → 1 PNG par palette utilisée |
| 6 | Entités (9 631) | `.world` + entités avec composants custom (`AlundraEntityComponent`) | **Moyenne** | Composant à écrire (DLL gameplay) : zone d'activation, direction, banque, indices d'events |
| 7 | Portails (3 316) | Composant trigger custom + référence world cible | **Moyenne** | Pas de composant téléporteur natif |
| 8 | Map events (1 714) + **bytecode événementiel** | Blob conservé + **interpréteur C# à écrire** | **Complexe** | **Plus gros chantier.** Aucun support moteur ; la sémantique des opcodes vient du repo de décompilation |
| 9 | Dialogues par map + `ETC_RES.R.json` | `.dialogue` Yarn (`line_texts`) et/ou table de chaînes JSON | **Moyenne** | Mapper les codes de contrôle (`\N`, `\C#`…) ; encodage à normaliser en UTF-8 |
| 10 | BGM (45 WAV, boucles) | Copie WAV + manifest ; lecture `SoundEffect`/custom | **Directe→Moyenne** | Boucle de piste OK ; pas de format asset audio natif |
| 11 | SFX (961, loop points, sample rates) | Copie WAV + manifest | **Moyenne** | `SoundEffect` MonoGame accepte `loopStart/loopLength` à la construction, mais CasaEngine ne l'expose pas — petite extension moteur |
| 12 | Police bitmap `font3` | BMFont `.fnt` + PNG → `StaticSpriteFont` (FontStashSharp) | **Moyenne** | Générateur `.fnt` à écrire ; intégration MGUI à valider |
| 13 | Atlas UI `wind`, memorycard, loading screen | `.sprite` (277 pièces) + textures | **Directe** | Reconstruction des 9-slices de fenêtres = gameplay/UI custom ultérieur |
| 14 | `BALANCE.BIN.json` | Fichier data custom chargé par la DLL gameplay | **Directe** | Format libre, sémantique partiellement inconnue (`Values`, `AnimVals`) |
| 15 | `ScrollParameters` (parallaxe, ondes) | Données conservées + composant rendu custom | **Complexe** | Aucun support natif (parallaxe multi-couches, LUT d'ondes, blend modes) |

---

## 5. Réponse à la question : « CasaEngine a-t-il tout ce qu'il faut ? »

**Pour la conversion des données : oui, à une condition près** (référencer
`CasaEngine.EditorServices` pour l'écriture des assets). Tous les types d'assets nécessaires
existent : tilemap/tileset animé, sprite, animation 2D multi-parts, texture, world/entités,
dialogue, catalogue projet. Rien ne bloque la génération d'un projet complet où **100 % de
l'information source est préservée** (au besoin dans des `CustomProperties` ou des assets
compagnons JSON).

**Pour l'exécution du jeu (runtime) : non, il manque :**

1. **Physique 2.5D top-down** (hauteur Z, pentes, gravité, viscosité, piles de murs) —
   à implémenter en composants dans la DLL gameplay ; `Physics2dComponent` ne suffit pas.
2. **Interpréteur du bytecode événementiel** (programmes A–F par entité, map events) —
   à écrire en C# ; c'est le chantier dominant du portage gameplay.
3. **Loop points audio** non exposés (SFX `LoopStart/LoopEnd`, boucles BGM) — extension mineure.
4. **Police bitmap** — pas d'asset natif ; passer par BMFont + FontStashSharp.
5. **Parallaxe/effets d'ondes** des fonds — composant de rendu custom.
6. **Palette swap** — inexistant ; contourné en générant des PNG par palette.
7. **Composant portail/téléportation entre worlds** — trivial à écrire, mais absent.

Ces manques ne bloquent pas le convertisseur : ils définissent le périmètre de la
**DLL gameplay** (`GameplayDllName`) et d'éventuelles petites extensions moteur.

---

## 6. Décisions d'architecture recommandées

1. **Écrire les assets via les classes du moteur** (le csproj référence déjà
   `CasaEngine.csproj` ; ajouter `CasaEngine.EditorServices.csproj`) plutôt que d'émettre du JSON
   à la main : garantit la compatibilité de schéma et la stabilité en cas d'évolution du moteur.
2. **Source de vérité = JSON natifs (`data/map_N.json`)**, en réutilisant l'export Tiled comme
   raccourci pour tilemap/tileset (le `TiledMapImporter` du moteur lit directement les `.tmj`
   avec animations). Les données compagnons (`.alundra.json`) sont fusionnées ensuite.
3. **GUIDs déterministes** (UUID v5 à partir d'un namespace fixe + nom d'asset) pour que deux
   exécutions du convertisseur produisent des ids identiques (idempotence, diffs Git lisibles,
   références croisées stables).
4. **1 map = 1 `.world`** (483 worlds) ; portails = composants référençant le world cible.
5. **Dédupliquer les banques de sprites par `Sector5Id`** : 244 banques converties une fois,
   référencées par les 2 507 usages.
6. **Préserver le bytecode tel quel** (blob + indices) dans les données converties ; l'interpréteur
   est un chantier séparé, itératif, dans la DLL gameplay.
7. **Rapport de conversion** généré à chaque run (JSON : compteurs, warnings, données ignorées)
   pour vérifier les invariants (483 maps, 9 631 entités, 3 316 portails…).

---

## 7. Risques

| Risque | Impact | Mitigation |
|---|---|---|
| Sémantique incomplète de certains champs (`Walkability`, `GroundProperty`, `Values` de BALANCE…) | Moyen | Conversion sans perte (recopie brute) ; la sémantique se raffine plus tard via le repo de décompilation |
| Explosion combinatoire (spritesheet × palette) | Moyen | Ne générer que les combinaisons réellement référencées par les frames |
| Volume (483 worlds, ~3 000+ assets au catalogue) : perfs éditeur/AssetCatalog | Moyen | Test de charge tôt (Phase 1) sur le projet complet |
| Codes de contrôle des textes (`\N`, `\C#`, boutons PSX inline) | Faible | Table de correspondance vers le markup du système Dialogue ; caractères inconnus loggés |
| Timings PAL (50 Hz) vs boucle de jeu 60 FPS | Faible | Conversions en secondes dès l'export (déjà fait pour les tiles) ; documenter la base 50 Hz |
| Éditeur incapable d'afficher certains assets générés | Moyen | Test de chargement round-trip (`Load()` moteur) automatisé sur chaque asset généré |

---

## 8. Volumétrie cible estimée (projet CasaEngine généré)

| Type d'asset | Quantité estimée |
|---|---|
| Textures PNG | ~1 500 (tilesheets, spritesheets × palettes, UI) |
| `.tileset` | 483 |
| `.tileMap` | 483 |
| `.world` | 483 |
| `.sprite` | plusieurs milliers (frames de 244 banques + 277 pièces UI + divers) |
| `.anim2d` | ~1 000–2 000 (244 banques × animations × directions) |
| WAV | 1 041 |
| `.dialogue` / tables de chaînes | 483 + 1 globale |
| Police `.fnt` | 1 |

---

## 9. Conclusion

La conversion est **faisable sans modification structurelle du moteur**. Le travail se découpe en
deux volets bien distincts :

- **Le convertisseur de données** (ce repo) : lecture des JSON d'extraction → écriture d'assets
  CasaEngine via les classes du moteur. Aucun blocage identifié ; complexité concentrée sur les
  sprites multi-quads/palettes et la préservation des métadonnées par cellule.
- **Le runtime gameplay** (DLL gameplay + petites extensions moteur) : physique 2.5D, interpréteur
  d'événements, parallaxe, audio à loop points, police bitmap. Hors périmètre du convertisseur,
  mais le convertisseur doit préserver toutes les données dont ce volet aura besoin.

Le plan d'exécution détaillé pour un agent IA est dans
[plan-conversion-agent-ia.md](plan-conversion-agent-ia.md).
