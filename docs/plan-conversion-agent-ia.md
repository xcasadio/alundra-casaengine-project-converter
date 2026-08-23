# Plan d'exécution — Convertisseur Alundra → CasaEngine (pour agent IA)

Ce plan opérationnalise le [rapport d'analyse](rapport-analyse-conversion-alundra-casaengine.md).
Il est écrit pour être exécuté par un agent IA, phase par phase. Chaque phase a des entrées, des
sorties, des critères d'acceptation et une validation exécutable.

**Objectif final** : `dotnet run --project alundra-casaengine-project-converter -- <data-extracted> <sortie>`
génère un projet CasaEngine complet (catalogue + assets) dont chaque asset se charge sans erreur
via les classes du moteur, et dont une map s'affiche dans l'éditeur/launcher.

> **État au 2026-08-09 — les 8 phases sont faites.** Un run complet produit 483 maps / 20 991 entrées
> de catalogue en 38 s, 0 erreur, et la passe de vérification recharge les 18 860 assets typés sans
> exception. Chaque phase porte ci-dessous une note « Réalisé » quand l'implémentation a tranché
> autrement que le texte d'origine — **le code fait foi**, ce plan est le récit de l'intention.
> Restent hors périmètre et documentés comme tels : les golden files (§3), la démo visuelle
> (phase 8 point 4) et tout le runtime gameplay (§6).

## Legende des statuts

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

---

## 0. Prérequis

### 0.1 État des capacités moteur (vérifié le 2026-07-13)

| Besoin | Support CasaEngine | Statut |
|---|---|---|
| Catalogue projet (`{Nom}.json` + `AssetInfos.json`) | `AssetInfo`, `AssetCatalog` (`CasaEngine/Framework/Assets/`) | ✅ Présent |
| Écriture d'assets sur disque | `EditorAssetWriterService.SaveAsset()` (public), `EditorJsonSaveHelper` — **dans `CasaEngine.EditorServices`** | ✅ Référencé par le csproj depuis la phase 0 |
| Tilemap / tileset (+ tiles animées) | `TileMapData` (.tileMap), `TileSetData` (.tileset), `AnimatedTileData`, `CustomProperties` | ✅ Présent |
| Import Tiled `.tmj` (couches, animations, objets, propriétés) | `CasaEngine.EditorServices/Tiled/TiledMapImporter.cs` | ✅ Présent |
| Sprites (région, hotspot, collisions, sockets) | `SpriteData` (.sprite) | ✅ Présent |
| Animations 2D multi-parts (flip, draw order, keyframes) | `Animation2dData` (.anim2d) | ✅ Présent |
| Textures PNG | `Texture2DLoader` | ✅ Présent |
| Worlds + entités + composants | `.world`, `TileMapComponent`, `SpriteRendererComponent`… | ✅ Présent |
| Dialogues / textes localisés | Système Yarn `.dialogue` (`line_texts`) | ✅ Présent |
| Scripting gameplay | `GameplayDllName` (DLL C#), DotNetCompiler | ✅ Présent |
| Physique 2.5D top-down (Height/Slope/gravité Z/murs) | refonte collisions livrée le 2026-08-12 (`ICollisionField`, `HeightGridCollisionField`, `SimulationSpacePolicy.TopDownElevation`, `CharacterControllerComponent`) — mover conscient de la politique encore à faire | ⚠️ Partiel (maj 2026-08-23) → étape E3 de [plan-conversion-totale.md](plan-conversion-totale.md) |
| Interpréteur bytecode événements Alundra | `Alundra/Scripts/AlundraEventProgramRunner.cs` (DLL gameplay) | ✅ Présent (maj 2026-08-23), étendu opcode par opcode selon [plan-conversion-totale.md](plan-conversion-totale.md) |
| Loop points audio (SFX `LoopStart/LoopEnd`) | `Sound.cs` minimal ; MonoGame `SoundEffect` le permet | ❌ Non exposé → manifest préservé, extension moteur optionnelle |
| Police bitmap | FontStashSharp `StaticSpriteFont` (BMFont) via MGUI | ⚠️ Possible, pas d'asset natif → générer `.fnt` |
| Parallaxe / ondes (`ScrollParameters`) | — | ❌ Absent → données préservées en JSON compagnon |
| Palette swap | — | ❌ Absent → générer 1 PNG par palette utilisée |

**Conclusion prérequis** : la conversion de données est intégralement réalisable. Les ❌ concernent
le runtime gameplay (hors périmètre de ce convertisseur) ; la règle est : **ne jamais jeter une
donnée dont le runtime aura besoin — la préserver dans des `CustomProperties` ou des JSON compagnons.**

### 0.2 Environnement

- Windows, .NET 9 (`net9.0-windows`), solution `alundra-casaengine-project-converter.slnx`.
- Submodule `CasaEngineMonogame` initialisé.
- Données sources : `data-extracted/` à la racine du repo.
- Outils shell : `rg`, `fd`, `jq` disponibles (voir conventions du repo moteur).

### 0.3 Décisions actées (ne pas re-débattre)

1. Écriture des assets **via les classes moteur** + `EditorAssetWriterService` (pas de JSON manuel).
2. Source de vérité : `data/map_N.json` natifs ; l'export Tiled (`.tmj/.tsj`) sert d'entrée au
   `TiledMapImporter` pour les tilemaps/tilesets.
3. **GUIDs déterministes** : UUID v5 (SHA-1) avec un namespace fixe du projet, clé = chemin
   logique de l'asset (ex. `tileset/map_10`). Deux runs → mêmes ids.
4. 1 map = 1 `.world` ; dédup des banques de sprites par `Sector5Id` (244 banques).
5. Sortie par défaut : `<repo>/output/AlundraGame/` (ne pas committer ; ajouter au `.gitignore`).
6. Timings : le source est PAL 50 Hz ; toutes les durées converties en **secondes**.
7. Textes : normaliser en UTF-8 ; conserver les codes de contrôle bruts (`\N`, `\C#`…) en V1.

---

## 1. Architecture du convertisseur

Arborescence réelle (le `Model/` intermédiaire n'a pas été nécessaire : chaque reader produit
directement son document de sortie) :

```
alundra-casaengine-project-converter/
├── Program.cs                    # CLI : <inputDir> <outputDir> [--maps 0,4,10] [--phase N] [--no-verify]
├── CliOptions.cs
├── Readers/                      # Lecture data-extracted (System.Text.Json)
│   ├── MapCatalogReader.cs       # maps.json → zones (MapLocation)
│   ├── CellMetadataReader.cs     # map_N.alundra.json + WallTiles natifs
│   ├── SpriteBankReader.cs       # SpriteRecords + map_alundra.json
│   ├── EntityNameCatalogReader.cs# EntityNames.csv → nom de dossier par banque
│   ├── SoundManifestReader.cs    # bgm.json / sfx.json
│   ├── UiSpriteReader.cs         # wind.json
│   ├── StringTableReader.cs      # ETC_RES.R.json + Strings[] par map
│   └── EventCodeReader.cs        # SpriteInfo.EventCodes (tables A–F + Codes)
├── Writers/                      # Génération assets via classes CasaEngine
│   ├── ProjectWriter.cs          # {Nom}.json + AssetInfos.json + arborescence
│   ├── MapDiscovery.cs
│   ├── TileMapWriter.cs          # .tileset/.tileMap (via TiledMapImporter)
│   ├── CellMetadataWriter.cs     # fusion compagnon → TileMapData.CustomProperties
│   ├── TextureAssetWriter.cs     # PNG + wrapper .texture (partagé phases 3/5/7)
│   ├── SpriteWriter.cs           # .sprite/.anim2d sous Entities/<Nom>/
│   ├── AudioWriter.cs            # WAV + manifests loop points
│   ├── TextWriter.cs             # tables de chaînes + inventaire des codes de contrôle
│   ├── FontWriter.cs             # .fnt BMFont + font3-charset.json
│   ├── WorldWriter.cs            # .world + world-index.json
│   ├── EventCodeWriter.cs        # compagnons de bytecode
│   └── UiWriter.cs               # sprites wind, textures UI, balance.json
├── Ids.cs                        # GUIDs déterministes (UUID v5)
├── AssetVerifier.cs              # passe « charge tout » (phase 8)
└── ConversionReport.cs           # compteurs, warnings, métriques, invariants → report.json
```

### Disposition de sortie (2026-08-09)

Toutes les données d'une même map vivent dans **un seul dossier**, au lieu des quatre arbres
parallèles `Maps/` + `Dialogues/` + `Events/` + `Worlds/` d'origine :

```
Maps/{Zone}/{Nom}-{id}/
    tilemap/    {Nom}-{id}.tileMap, .tileset, .tmj, map_{id}_tileset.png, map_{id}_tileset.texture
    dialogues/  {Nom}-{id}.strings.json
    events/     {Nom}-{id}.events.json
    {Nom}-{id}.world
Maps/world-index.json          # MapId -> chemin du .world
Dialogues/                     # uniquement les tables globales : global-strings.json, control-codes.json
Entities/<NomEntité>/          # banques de sprites (.sprite + .anim2d)
Sprites/Textures/, Sprites/hero/, Sounds/, Musics/, UI/, UI/Textures/, Data/
```

Les dossiers de premier niveau `Worlds/` et `Events/` n'existent plus. **`MapLocation`
(`Readers/MapCatalogReader.cs`) est la seule autorité sur cette disposition** : aucun writer ne
compose un chemin de map lui-même — ils en étaient six à le faire, et les copies étaient libres de
diverger. Changer la disposition se fait là et nulle part ailleurs.

Règles de code (héritées du repo moteur) :
- parsing déterministe, ordre de sortie stable entre runs (tri explicite) ;
- ne pas renommer les champs inconnus — les recopier tels quels ;
- séparer faits et hypothèses dans les commentaires ;
- pas de dépendance nouvelle sans justification.

**Modification préalable du csproj** (Phase 0) :

```xml
<ProjectReference Include="..\CasaEngineMonogame\CasaEngine.EditorServices\CasaEngine.EditorServices.csproj" />
```

---

## 2. Phases

### ✅ Phase 0 — Bootstrap et projet vide

**Entrées** : aucune. **Sorties** : squelette CLI + projet CasaEngine vide chargeable.

1. Ajouter la référence `CasaEngine.EditorServices` au csproj ; `dotnet build` doit passer.
2. Implémenter le CLI : `<inputDir> <outputDir>`, options `--maps <liste>` (sous-ensemble pour
   itérer vite) et `--phase <n>` (exécuter jusqu'à la phase n).
3. `ProjectWriter` : générer `AlundraGame.json` (ProjectName, WindowTitle, FirstWorldLoaded)
   + `AssetInfos.json` vide + arborescence `Content/{Maps,TileSets,Textures,Sprites,Animations,Sounds,Musics,Dialogues,UI}`.
   S'inspirer de `CasaEngine.Demos/Content/DemosGame.json`.
4. `Ids.cs` : UUID v5 déterministe + test unitaire (même clé → même GUID).
5. `ConversionReport` : squelette (compteurs par type, warnings, erreurs).

**Acceptation** : build OK ; le projet vide s'ouvre dans l'éditeur CasaEngine (ou à défaut, un test
qui charge `AlundraGame.json` + `AssetInfos.json` via `AssetCatalog` sans exception).

### ✅ Phase 1 — Textures, tilesets, tilemaps

**Entrées** : `data/tiled/map_N.{tmj,tsj,png}`, `data/map_N_tilesheet.png`.
**Sorties** : `Maps/{Zone}/{Nom}-{id}/tilemap/` (`.tileMap`, `.tileset`, `.tmj`, le PNG de tileset
et son `.texture`), catalogue mis à jour. Voir « Disposition de sortie » ci-dessous.

1. Copier les PNG de tilesets ; enregistrer chaque texture au catalogue (GUID déterministe).
2. Piloter `TiledMapImporter` sur chaque `map_N.tmj` (il gère couches `Render_*`, animations de
   tiles, objectgroups, propriétés). Vérifier le traitement des warnings retournés → report.
3. Renseigner `tile_set_asset_id`/`sprite_sheet_asset_id` avec les GUIDs du catalogue.
4. Sauver via `EditorAssetWriterService.SaveAsset`.
5. Golden files : committer les sorties de `map_0` (statique), `map_10` (27 tiles animées) comme
   références de non-régression dans `tests/golden/`.

**Acceptation** : `--maps 0,4,10` produit des assets qui se rechargent via `TileMapData.Load()` /
`TileSetData.Load()` sans exception ; `map_10` contient bien 27 tiles animées ; `map_0` s'affiche
dans l'éditeur.

### ✅ Phase 2 — Métadonnées gameplay par cellule

**Entrées** : `data/tiled/map_N.alundra.json` (+ `WallTiles` des JSON natifs).
**Sorties** : données par cellule accessibles au runtime.

1. Fusionner le compagnon dans le `.tileMap` : `CustomProperties` du `TileMapData` (clé →
   JSON sérialisé) **ou** asset compagnon `Content/Maps/map_N.cells.json` référencé en custom
   property — choisir selon la taille (3 120 cellules : mesurer l'impact sur le temps de
   chargement ; si > quelques Mo par map, préférer l'asset compagnon).
2. Y inclure : `Walkability`, `GroundProperty`, `Slope`, `Height`, `Flags`, piles `WallTiles`,
   et les propriétés de map (`Gravity`, `ZViscosity`, `SlideEffectId`, `BalanceLevel`).
3. Documenter le schéma dans [docs/formats/cells-companion.md](formats/cells-companion.md).

**Acceptation** : golden file `map_10.cells.json` ; rechargement sans perte (comparaison
round-trip source → converti → relu = identique champ à champ).

### ✅ Phase 3 — Sprites et animations

**Entrées** : `SpriteRecords` des 483 maps + `map_alundra.json` + spritesheets PNG.
**Sorties** : `Content/Sprites/bank_<Sector5Id>/…`, `.sprite`, `.anim2d`, PNG par palette.

1. Recenser les banques uniques par `Sector5Id` (attendu : **244** + héros) ; convertir chaque
   banque une seule fois.
2. Pour chaque frame : les `Images[]` (quads) deviennent des **parts** d'`Animation2dData` ;
   chaque région source unique `(Spritesheet, Palette, Sx, Sy, Sw, Sh)` devient un `SpriteData`
   avec `location` et `hotspot` calculés depuis les coins `X1..Y4` ; flips détectés quand les
   coins sont inversés.
3. Palettes : générer le PNG de la spritesheet dans chaque palette réellement référencée
   (nommage `bank_<id>_pal<k>.png`). Ne pas générer les combinaisons non utilisées.
4. Timing : `Delay` (frames 50 Hz) → `time_seconds` cumulés dans les tracks.
5. `CollisionData` 3D par frame : projeter en `Collision2d` (X/Y) et conserver Z
   (`OffsetZ`, `SizeZ`, `Depth`) en custom/companion.
6. AnimSets 4 directions → convention de nommage : `bank<id>_anim<j>_<down|up|left|right>`.
7. Héros : traiter `map_alundra.json` (29 `SpriteEffects` inclus, préservés en JSON compagnon).

**Acceptation** : une animation d'un PNJ de `map_4` (banque `Sector5Id=82`) jouée dans l'éditeur ;
tous les `.sprite`/`.anim2d` rechargent via `Load()` ; report : 244 banques, 0 quad perdu
(compteur quads lus = quads convertis).

**Réalisé — écarts assumés** (le code fait foi, cf. commentaires de `Writers/SpriteWriter.cs`) :

- Sortie sous **`Entities/<NomEntité>/`**, pas `Sprites/bank_<id>/` : `Readers/EntityNameCatalogReader.cs`
  résout chaque banque via `EntityNames.csv` (table partagée avec l'analyseur). Repli
  `Entities/bank_<clé>/` quand la table ne nomme pas la banque.
- **Palettes** : rien à générer — l'extracteur a déjà cuit la palette dans l'atlas exporté
  (`Signature` inclut `Palette`, `AtlasX/Y` sont calculés par map). Point 3 sans objet.
- **Ids non déterministes** pour `.sprite`/`.anim2d` : `ObjectBase.Id` a un setter privé et
  `EditorAssetWriterService.SaveAsset` sérialise l'id porté par l'objet. `Ids.For()` n'est utilisé
  que là où aucune classe moteur n'impose l'id (audio, worlds, police).
- **`CollisionData` 3D par frame convertie** (point 5), mais en `collision_keyframes`
  d'`Animation2dData` (schéma phase E du moteur) plutôt qu'en `Collision2d` + compagnon : le volume
  reste **3D**, une `ColliderFixture` portant une `Box`, donc rien n'est projeté ni déporté.
  - *Mapping* : Alundra donne le **coin minimum** (`OffsetX/Y/Z`) et les extents
    (`Width`→X est, `Depth`→Y profondeur au sol, `Height`→Z élévation), en pixels, origine aux pieds
    de l'entité. Le moteur pose une fixture par le **centre** de la boîte, d'où
    `local_position = Offset + Size / 2` ; taille en pixels bruts, rotation identité (les volumes
    Alundra sont des AABB).
  - *Espace logique, pas espace de rendu* : ces fixtures ne subissent **pas** la négation de Y
    appliquée aux positions des parts. `AnimatedSpriteComponent` pose les corps de la timeline
    depuis la transform logique de la racine de l'entité, jamais depuis l'espace où il rend, alors
    que les parts vivent en espace de rendu et gardent la négation historique. Les deux espaces
    cohabitent dans un même asset **par construction**.
  - *Ni profil ni tag* : `CollisionData` ne porte aucune sémantique attaque/défense — elle vit dans
    le bytecode d'événements, toujours hors périmètre. `collision_profile` et `tag` restent vides
    (le moteur retombe alors sur `Trigger`) ; inventer `AttackVolume`/`DamageableVolume` serait de
    la fiction.
  - *Doublons écrasés* : une keyframe n'est émise que quand le volume diffère de celui actif ; une
    frame qui perd son volume émet une keyframe à **liste de fixtures vide** (désactivation). Un
    hitbox constant — la grande majorité des animations — donne exactement une keyframe à `t=0`.
  - *Règle du terminator* : la frame terminale n'émet jamais. Elle porte un code de contrôle et non
    une durée, et se place exactement à la fin de l'animation, là où l'échantillonneur qui boucle ne
    peut jamais arriver. Un volume actif sur la dernière frame affichée reste donc actif jusqu'au
    bouclage, et c'est la keyframe à `t=0` (ou son absence) qui décide du cycle suivant. Aucune
    keyframe émise ne tombe sur la durée de l'animation.
  - Une animation sans aucune `CollisionData` n'a pas de clé `collision_keyframes` : ces assets
    restent identiques octet pour octet.
- **Boîte de corps par banque → prefab `.entity`** (ajout CONV-G2). En plus des hitbox par frame,
  l'en-tête d'un record (`SpriteRecord.Header` : `OffsetX/Y/Z` + `SizeX/Y/Z`) déclare **un** volume
  pour l'entité entière. Chaque banque dont les trois tailles sont > 0 reçoit
  `Entities/<NomEntité>/<NomEntité>.entity` : une entité dont le composant racine est un
  `CollisionComponent` portant **une** fixture `Box`.
  - *Mapping* : même convention que les volumes par frame — coin minimum + extents en pixels,
    origine aux pieds, donc `local_position = Offset + Size / 2`, rotation identité, **pas** de
    négation de Y (espace logique).
  - *Kinetic, donc Pawn* : `physics_type = Kinetic`, parce qu'un corps d'entité Alundra est déplacé
    par le code gameplay et jamais par une simulation, tout en devant rapporter ses contacts — ce
    que fait exactement le ghost object construit par un composant kinetic. `collision_profile`
    reste vide sur le composant **et** sur la fixture : la règle `PhysicsType` du moteur résout
    alors le profil en `Pawn`.
  - *Règle d'exclusion* : un en-tête dont `SizeX`, `SizeY` ou `SizeZ` vaut 0 ne déclare aucun corps,
    donc aucun prefab ; la banque est comptée dans `Entities.BodyPrefabsSkipped`.
  - *Banque vue dans plusieurs maps* : la boîte du premier record lu fait foi (comme le reste de la
    banque) ; une map ultérieure qui la contredit est ignorée mais comptée
    (`Sprites.BodyBoxConflicts`).
  - *Ids non déterministes*, comme tous les assets de cette phase : le document est produit par le
    sérialiseur d'entité du moteur, donc `ObjectBase.Id` est neuf à chaque run.
  - Compteurs réels : 384 prefabs écrits, 11 banques ignorées, 0 conflit de boîte.
- Compteurs réels : 395 banques (244 de map + banques héros), 160 355 quads lus == convertis,
  9 620 `.anim2d`, 7 185 `.sprite`, 104 spritesheets, 7 747 keyframes de collision sur 5 568
  animations (`Sprites.CollisionKeyframes`, `Sprites.AnimationsWithCollision`).

### ✅ Phase 4 — Audio

**Entrées** : `sound/bgm/*.wav`, `sound/sfx/*.wav`, `bgm.json`, `sfx.json`.
**Sorties** : `Content/Musics/`, `Content/Sounds/`, `Content/Sounds/sfx-manifest.json`.

1. Copier les WAV tels quels (pas de réencodage en V1) ; enregistrer au catalogue.
2. Générer un manifest préservant : id SFX → fichier(s) tone, `SampleRate`, `LoopStart/LoopEnd`,
   `Repeat`, `MaxVoices` ; id BGM → `LoopDetected`, durée.
3. Optionnel (si demandé) : extension moteur exposant `SoundEffect(buffer, sampleRate, channels,
   loopStart, loopLength)` — sinon documenter dans le manifest.

**Acceptation** : 1 041 WAV copiés + manifest ; test unitaire qui instancie un `SoundEffect`
MonoGame avec les loop points du manifest sur un SFX bouclé (ex. `sfx_0001`, LoopStart=28).

**Réalisé** : 45 BGM + 996 tons SFX = 1 041 WAV copiés tels quels, chacun inscrit au catalogue avec
un id **déterministe** (`Ids.For("sound/…")`) — le moteur n'ayant aucun loader audio, rien ne
dispute l'id. Manifests `Musics/bgm-manifest.json` et `Sounds/sfx-manifest.json` (961 enregistrements
SFX, dont 91 avec `SkipReason` conservés). Le point 3 (extension moteur exposant les loop points)
n'a pas été fait : hors périmètre du convertisseur, la donnée est dans le manifest.
Schéma : [docs/formats/audio-manifests.md](formats/audio-manifests.md).

### ✅ Phase 5 — Textes, dialogues, police

**Entrées** : `Strings[]` par map, `ETC_RES.R.json`, `ui/font3.{png,json}`.
**Sorties** : `Content/Dialogues/`, `Content/UI/font3.fnt` + PNG.

1. `ETC_RES.R.json` → table de chaînes globale (id → texte UTF-8). Format : `line_texts` d'un
   `.dialogue` Yarn ou JSON custom `Content/Dialogues/global-strings.json` (préférer le JSON
   custom en V1 : le contenu est une table, pas un graphe de dialogue).
2. `Strings[]` par map → `Content/Dialogues/map_N.strings.json` (index → texte). La conversion
   en vrais dialogues Yarn branchés dépend du bytecode → hors V1.
3. Codes de contrôle : conserver tels quels ; produire dans le report la liste des codes
   rencontrés (`\N`, `\C#`, boutons PSX…) pour la future table de mapping.
4. `font3` → générateur BMFont : `.fnt` texte (char id/x/y/width/height/xadvance) + PNG.
   Vérifier le chargement avec `StaticSpriteFont.FromBMFont` (FontStashSharp).

**Acceptation** : chaînes accentuées correctes en UTF-8 (échantillon : « Epée sacrée » etc. depuis
ETC_RES) ; `.fnt` chargé par FontStashSharp dans un test ; report listant les codes de contrôle.

**Réalisé — écarts assumés** :

- Tables par map en **`Maps/{Zone}/{Nom}-{id}/dialogues/{Nom}-{id}.strings.json`**, dans le dossier
  de la map (cf. « Disposition de sortie » plus bas), pas `map_N.strings.json`. `Dialogues/` ne garde
  que les deux tables qui n'appartiennent à aucune map : `global-strings.json` et
  `control-codes.json`.
- Codes de contrôle : la liste vit dans **`Dialogues/control-codes.json`** (code → occurrences +
  exemple) plutôt que dans `report.json`, qui n'en garde que le compteur `Text.ControlCodesDistinct`.
  27 codes distincts trouvés (`\N` 24 612, `\B` 17 063, `\W` 11 099, `\A` 6 972…).
- **`char id` du `.fnt` = point de code Unicode**, pas le code brut du jeu : les chaînes extraites
  sont déjà décodées, un `.fnt` indexé sur les codes bruts n'en rendrait aucune. Conversion = port de
  `TextDecoder.ConvertCp850ToLatin1` (branche FR/PAL). Codes bruts conservés dans
  `UI/font3-charset.json`. 42 codes sur 256 tombent sur un point de code déjà pris : celui que la
  table CP850 nomme l'emporte sur celui qui n'a fait que garder sa valeur d'octet.
- **Limite connue — le texte rend en chasse fixe** : Alundra avance de
  `g_fontCharWidthTable[code * 5]`, table qui vit dans l'exécutable et **n'est pas** dans
  `data-extracted`. Tous les `xadvance` valent la cellule de 16 px. C'est un vrai écart de fidélité ;
  extraire cette table est le correctif.
- Compteurs réels : 916 chaînes globales (562 vides), 483 tables de map, 61 824 chaînes, 214 glyphes
  dans le `.fnt`. Schémas : [text-tables.md](formats/text-tables.md), [font.md](formats/font.md).

### ✅ Phase 6 — Worlds, entités, portails, events

**Entrées** : `map_N.json` (Entities/Portals/MapEvents/EventCodes), assets des phases 1–3.
**Sorties** : `Content/Worlds/map_N.world` + JSON compagnons events.

1. `WorldWriter` : 1 `.world` par map — entité racine avec `TileMapComponent` (réf. `.tileMap`),
   caméra 2D par défaut.
2. Entités actives → `entity_references` avec, en V1, un stockage data-only : nom
   `Entity_<i>`, position monde (tile × 24/16, `Height`), et **toutes** les propriétés natives
   (zone d'activation, `SpriteTableIndex` → GUID de la banque, direction, indices A–F, `Contents`)
   en custom properties. Les composants gameplay typés viendront avec la DLL gameplay.
3. Portails → objets/entités avec propriétés `DestMapId` (→ nom du world cible), `DestTileX/Y`,
   `ZLevel`, `Flags`.
4. Map events → même approche (zone + `EventCodesBIndex`).
5. Bytecode : `Content/Events/map_N.events.json` — tables A–F + `Codes[]` bruts, non interprétés.
6. `AlundraGame.json` : `FirstWorldLoaded` = world de la map de départ (map_0 par défaut, à
   confirmer avec l'utilisateur).

**Acceptation** : les 483 `.world` rechargent sans exception ; invariants du report :
9 631 entités, 3 316 portails, 1 714 map events répartis conformément aux stats du rapport
d'analyse ; ouverture d'un world dans l'éditeur avec la tilemap visible.

**Réalisé — les points 2, 3 et 4 ont été tranchés autrement**, conformément à
[demarrage-nouvelle-partie.md](demarrage-nouvelle-partie.md) §5 (E1) et §6 point 1, qui font
autorité sur ce plan :

- **Les entités / portails / map events ne sont PAS dupliqués en `entity_references`.** Deux
  raisons : `Entity` n'a **aucun** sac de propriétés custom (le point 2 était irréalisable tel
  qu'écrit), et la phase 1 les a déjà tous préservés dans les `object_layers` du `.tileMap`
  (`Portals` / `MapEvents` / `Entities`, tous champs natifs en `custom_properties`). La DLL gameplay
  les lira depuis `TileMapData.ObjectLayers`.
- Chaque `.world` (`Maps/{Zone}/{Nom}-{id}/{Nom}-{id}.world`) contient donc, dans cet ordre : la
  référence `camera`, l'entité `tileMap` (`TileMapComponent`), l'entité `PlayerStart`.
- **Caméra (révisé le 2026-08-09, après la branche moteur `tilemap-render-spaces`)** : un **unique
  asset partagé `Entities/AlundraCamera.entity`**, portant un `Camera2dComponent`
  (`Target` = centre de map `(624, -480, 0)`, `Zoom` = 1, `PixelSnap` = true), référencé par
  `asset_id` depuis les 483 worlds.
  - *Pourquoi `Camera2dComponent`* : `CasaEngineMonogame/docs/engine/rendering-2d-3d-spaces.md`
    en fait le mode nominal 2D et déclare `Camera3dIn2dAxisComponent` — le choix initial de cette
    phase — **legacy**, à ne plus retenir pour un nouveau rendu 2D. Celui-ci reste une caméra
    *perspective* : seul le plan cible est à l'échelle 1:1, donc les layers d'une map Alundra
    (z 0 / 0,1 / 0,2 / 0,3) sont déformés ; sa distance est recalculée à chaque resize depuis la
    taille écran globale ; et il n'a ni zoom ni snap texel, donc aucun contrat pixel-perfect.
  - *Pourquoi partagée* : les 483 maps mesurent **toutes** 52 × 60 tuiles de 24 × 16 px (vérifié sur
    les 483 `.tmj`), donc un `Target` unique cadre correctement chacune. Ce n'est pas du partage à
    l'exécution : `EntityReference.Load` fait `Load<Entity>(AssetId).Clone()`, chaque world garde
    son instance.
  - *Pourquoi en premier* : `DefaultRuntimeViewBootstrapper` retient
    `world.Entities.Select(GetComponent<CameraComponent>).FirstOrDefault(c => c != null)` — la
    première entité portant une caméra devient la caméra par défaut de la vue.
  - *Cadrage* : la surface visible vaut **fenêtre ÷ `Zoom`**, et la fenêtre (`DebugWidth`/
    `DebugHeight` d'`AlundraGame.json`, phase 0) et le `Zoom` (phase 6) sont **un seul réglage écrit
    dans deux fichiers**. `AlundraDisplay` les dérive tous deux d'une constante `PixelScale` = 4 :
    fenêtre 1280 × 944, `Zoom` 4, donc 320 × 236 pixels de monde visibles — l'écran natif d'Alundra.
    Voir [guidelines §2.0](guidelines-runtime-alundra-casaengine.md) pour le piège correspondant.
  - *Hypothèse assumée* : le `Target` est le centre géométrique de la map. La vraie caméra d'Alundra
    **suit une entité désignée** — `g_entityFollowedByCamera`, relue chaque frame par
    `UpdateEntities` (`g_cameraLookAtX = suivie.PosX >> 16`). C'est le plus souvent Alundra, mais
    c'est une **variable, pas une constante** : les scripts la réassignent (cinématiques, boss). Le
    code runtime devra donc suivre l'entité couramment désignée, pas le joueur en dur. Le cadrage
    centré écrit ici n'est que celui d'un world **avant** tout code gameplay.
  - *Pourquoi pas `CameraTargeted2dComponent`*, qui modéliserait pourtant le suivi nativement (son
    `Target` **est** une `Entity`, avec dead zone et limites) : il dérive de `Camera3dComponent`,
    c'est donc une caméra perspective, qui retombe exactement sous le reproche que
    `rendering-2d-3d-spaces.md` adresse au composant remplacé ici. Aucun composant du moteur ne
    combine aujourd'hui suivi d'entité et projection orthographique pixel-perfect ; on garde la
    projection, parce que c'est elle qu'on ne peut pas rajouter après coup depuis le gameplay.
- **Politique d'espace de simulation** (ajout CONV-G2) : chaque `.world` déclare
  `"space_policy": "TopDownElevation"`. C'est l'espace du moteur pour un jeu dont X/Y est le plan du
  sol et Z l'élévation — le repère de toute la conversion. Sans cette clé, `World.Load` laisse
  `SpacePolicyName` vide et le moteur retombe sur la politique 3D générique, qui simulerait les
  corps des prefabs de la phase 3 dans le mauvais espace.
- **Spawn** : seule la map 389 en a un documenté (tuile 33/59/0 → `(804, -952, 0)`). Les 482 autres
  reçoivent un `PlayerStart` au centre de la map, compté à part
  (`Worlds.PlayerStartPlaceholders`) — elles s'atteignent par portail, pas par spawn.
- `player_startup_settings_asset_id` laissé vide : le `.gameMode` exige l'entité héros (étape E2 du
  doc de démarrage), le renseigner créerait une référence morte.
- Ajouté hors plan car demandé par E6 : **`Maps/world-index.json`** (`MapId` → chemin du world).
- Point 6 fait : `FirstWorldLoaded` = world de la map **389** (« Ship Klark (beginning) »), valeur
  fixée par le doc de démarrage — pas map_0. `GameplayDllName` reste vide (hors périmètre, cf. §6).
- Invariants vérifiés sur un run complet : 483 worlds, 9 741 entités dont **9 631** activées,
  **3 316** portails (`DestMapId != 0` ; les 30 912 emplacements bruts ne veulent rien dire),
  **1 714** map events, 365 344 mots de bytecode.
  Schémas : [events.md](formats/events.md), [world-index.md](formats/world-index.md).

### ✅ Phase 7 — UI et divers

1. `wind.json` → 277 `SpriteData` sur `wind.png` (`Content/UI/`).
2. `memorycard/*.png`, `loading_screen.png` → textures cataloguées.
3. `BALANCE.BIN.json` → `Content/Data/balance.json` (recopie structurée, champs inconnus
   conservés sous leur nom d'origine).

**Acceptation** : catalogue complet, chargement round-trip OK.

**Réalisé** : 277 `SpriteData` sous `UI/` (nommés `wind_000`..`wind_276` — plusieurs enregistrements
partagent le même rectangle, un nom dérivé du rectangle collisionnerait ; `PaletteIndex`, que
`SpriteData` ne sait pas porter, est préservé dans `UI/wind-sprites.json`). 18 textures UI sous
`UI/Textures/` (wind + 3 memory card + 13 closing + loading screen). `Data/balance.json` :
512 enregistrements, champs inconnus sous leur nom d'origine ; **seul le `FileName` de tête est
retiré** — c'est un chemin absolu de la machine d'extraction, donc de la provenance, pas de la
donnée de jeu. Schéma : [misc-data.md](formats/misc-data.md).

### ✅ Phase 8 — Validation globale et finition

1. Run complet sur les 483 maps : `0 erreur`, warnings triés par type dans `report.json`.
2. Test automatisé « charge tout » : itérer `AssetInfos.json`, charger chaque asset via la classe
   moteur correspondante (c'est le filet de sécurité principal).
3. Mesurer : temps de conversion total, temps d'ouverture du projet dans l'éditeur, taille disque.
4. Démo : lancer le launcher sur `AlundraGame` et afficher un world (rendu tilemap + une entité
   avec `SpriteRendererComponent` jouant une animation).
5. Mettre à jour `README.md` (usage CLI) et `docs/formats/` (schémas compagnons).

**Acceptation finale** : commande unique → projet complet ; suite de tests verte ; démo visuelle.

**Réalisé** :

- Le « charge tout » est devenu `AssetVerifier`, **une passe du convertisseur lui-même, active par
  défaut** (`--no-verify` pour l'éviter), pas seulement un test : ainsi il valide la vraie sortie de
  chaque run, ce qu'un test sur un projet jouet ne ferait jamais. Il charge chaque asset via la
  classe moteur correspondante et signale en plus les défauts d'intégrité du catalogue (fichier
  absent, id ou nom de fichier en double). Les extensions sans loader utilisable sans
  `GraphicsDevice` (`.wav` — le moteur n'a aucun loader audio, `.png`, `.tmj`, `.fnt`) sont
  vérifiées en existence, dans un compteur **distinct** : elles ne sont pas comptées comme chargées.
- La passe étant pilotée par le catalogue, elle est confrontée aux fichiers réellement présents pour
  ne pas pouvoir être aveugle : **catalogue vide sur un dossier plein d'assets = erreur** (« rien n'a
  été vérifié »), et un fichier chargeable absent du catalogue = warning (reliquat d'un run
  précédent — l'import Tiled du moteur renomme au lieu d'écraser). Sur un run propre,
  `Verify.LoadableFilesOnDisk == Verify.Loaded` et `Verify.UncataloguedFiles` vaut 0.
- **Ce que la passe ne couvre pas** : les ~980 JSON compagnons (`Dialogues/`, `Events/`, manifests
  audio, `wind-sprites.json`, `font3-charset.json`, `balance.json`, `world-index.json`). Ils ne sont
  pas des types d'assets CasaEngine, donc rien ne les recharge ; ils sont couverts par les tests
  unitaires et documentés dans [docs/formats/](formats/README.md).
- `report.json` : durée totale et par phase, taille et nombre de fichiers en sortie, et un
  regroupement des warnings par catégorie **en plus** de la liste brute (la liste brute reste, la
  perdre serait une régression).
- Point 3 partiel : le **temps d'ouverture du projet dans l'éditeur n'est pas mesuré** — il faut
  l'éditeur WPF et un humain. Aucune mesure de substitution n'a été inventée.
- Point 4 (**démo visuelle**) **non fait** : afficher un world dans le launcher exige l'entité héros,
  un `.gameMode` et la DLL gameplay, c'est-à-dire les étapes E2/E3 de
  [demarrage-nouvelle-partie.md](demarrage-nouvelle-partie.md), hors périmètre de ce plan.
- Point 5 fait : [README.md](../README.md) (usage CLI) et [docs/formats/](formats/README.md).

**Résultats du run complet de référence** (`data-extracted` → `alundra-project/`, une machine) :
483 maps, 20 991 entrées de catalogue, 18 860 assets chargés + 2 131 vérifiés en existence,
**0 erreur**, 6 warnings, 21 968 fichiers / 910 Mo, 38 s. Suite de tests : 70 verte.

Une passe de vérification adverse indépendante (contre-calcul de tous les compteurs depuis
`data-extracted`, corruptions délibérées d'assets, double run pour le déterminisme, comparaison
champ à champ des chaînes / manifests / bytecode) a confirmé ces résultats : 0 perte de donnée,
1 041 WAV identiques au bit près, 61 824 chaînes et 365 344 mots de bytecode sans écart, et le seul
octet qui bouge d'un run à l'autre dans un `.world` est `tile_map_data_asset_id` — exactement le
caveat documenté ci-dessus. Les défauts secondaires qu'elle a relevés (catalogue vide déclaré
« PASSED », `--maps abc` en stack trace, garde de run complet, code de contrôle en fin de chaîne)
ont été corrigés et couverts par des tests.

---

## 3. Stratégie de test

| Niveau | Contenu |
|---|---|
| Unit (Readers) | Parsing de `map_0` (vide), `map_4` (sprites), `map_10` (tiles animées + murs) ; champs clés vérifiés contre les valeurs connues du rapport |
| Unit (Ids) | Déterminisme des GUIDs |
| Golden files | Sorties committées pour `map_0`, `map_4`, `map_10` ; diff exact entre runs |
| Round-trip | Chaque asset généré rechargé via `Load()` moteur — aucune exception, champs == attendus |
| Invariants | Compteurs globaux du report vs statistiques du rapport d'analyse |
| Intégration | Ouverture éditeur/launcher (manuelle en V1, scriptable ensuite) |

Framework : xUnit (59 tests). 

**Golden files : non faits, et bloqués — pas oubliés.** Ils exigent une sortie identique d'un run à
l'autre, or les ids d'assets des phases 1 et 3 viennent de `ObjectBase.Id`, dont le setter est privé :
`EditorAssetImportService.ImportTiledMap` et `EditorAssetWriterService.SaveAsset` sérialisent un
`Guid` neuf à chaque run. Un golden file de `.tileMap`, `.tileset`, `.sprite` ou `.anim2d` échouerait
donc systématiquement, et le committer reviendrait à figer du bruit. Le reste de la sortie *est*
déterministe (`Ids.For()` pour l'audio, les worlds et la police ; collections triées ; aucun
`DateTime.Now`). **Préalable au chantier golden files** : exposer un id assignable côté moteur
(`CasaEngineMonogame`), puis basculer les phases 1 et 3 sur `Ids.For()`. Même constat côté
[demarrage-nouvelle-partie.md](demarrage-nouvelle-partie.md) §6 point 4. En attendant, le filet est
la passe `AssetVerifier` (phase 8), qui recharge toute la sortie réelle à chaque run.

---

## 4. Commandes utiles

```powershell
# Build
dotnet build alundra-casaengine-project-converter/alundra-casaengine-project-converter.csproj

# Conversion complète (~38 s, ~910 Mo) — alundra-project/ est la sortie de référence, gitignorée
dotnet run --project alundra-casaengine-project-converter -- data-extracted alundra-project

# Itération rapide sur 3 maps de référence (--phase N exécute les phases 0..N incluses)
dotnet run --project alundra-casaengine-project-converter -- data-extracted alundra-project --maps 0,4,10

# Sans la passe de vérification finale
dotnet run --project alundra-casaengine-project-converter -- data-extracted alundra-project --no-verify

# Inspection des données sources
jq '.SpriteInfo.Entities.Entities[] | select(.IsEnabled==1)' data-extracted/data/map_4.json
```

---

## 5. Risques et parades (rappel opérationnel)

- **Ne jamais supprimer une donnée non comprise** → recopie brute + warning dans le report.
- **Déterminisme** : trier toutes les collections avant écriture ; pas de `DateTime.Now`, pas de
  `Guid.NewGuid()`.
- **Mémoire** : ne pas charger les 483 maps simultanément — streaming map par map.
- **Encodage** : lire les JSON sources en UTF-8 ; valider les accents français sur échantillon.
- **Évolution moteur** : si un schéma d'asset moteur change, les golden files le détectent ;
  ne jamais forker le format — adapter le convertisseur.

## 6. Hors périmètre V1 (chantiers suivants, dans l'ordre conseillé)

> **Mise à jour 2026-08-23 : l'ordre et le contenu des chantiers runtime sont définis par
> [plan-conversion-totale.md](plan-conversion-totale.md)** (étapes E1–E15). Les exports que ce plan
> demande au convertisseur : `AnimSets[].Speed/Acceleration/IsZForceApplied/Sfx` (E4), couche
> TileMap `navigation.*` (E4), `.gameMode` + `.buttonsMapping` (E2), `.yarn` + `DialogueAsset` par map
> avec un nœud par chaîne (E12), données de placement des murs au format moteur (E8). La liste
> ci-dessous est conservée comme historique.


1. **DLL gameplay `AlundraGame.Gameplay`** : composants typés (entité, portail, map event),
   physique 2.5D (Walkability/Slope/Height/gravité), chargement du manifest audio avec loop points.
2. **Interpréteur du bytecode événementiel** (s'appuyer sur la décompilation du repo
   `alundra-datas-analyser` pour la sémantique des opcodes).
3. Rendu parallaxe/ondes (`ScrollParameters`).
4. Palette swap runtime (shader) en remplacement des PNG dupliqués.
5. Conversion des dialogues en Yarn branché (dépend de l'interpréteur).
