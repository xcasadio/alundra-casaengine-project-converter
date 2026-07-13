# Plan d'exécution — Convertisseur Alundra → CasaEngine (pour agent IA)

Ce plan opérationnalise le [rapport d'analyse](rapport-analyse-conversion-alundra-casaengine.md).
Il est écrit pour être exécuté par un agent IA, phase par phase. Chaque phase a des entrées, des
sorties, des critères d'acceptation et une validation exécutable.

**Objectif final** : `dotnet run --project alundra-casaengine-project-converter -- <data-extracted> <sortie>`
génère un projet CasaEngine complet (catalogue + assets) dont chaque asset se charge sans erreur
via les classes du moteur, et dont une map s'affiche dans l'éditeur/launcher.

---

## 0. Prérequis

### 0.1 État des capacités moteur (vérifié le 2026-07-13)

| Besoin | Support CasaEngine | Statut |
|---|---|---|
| Catalogue projet (`{Nom}.json` + `AssetInfos.json`) | `AssetInfo`, `AssetCatalog` (`CasaEngine/Framework/Assets/`) | ✅ Présent |
| Écriture d'assets sur disque | `EditorAssetWriterService.SaveAsset()` (public), `EditorJsonSaveHelper` — **dans `CasaEngine.EditorServices`** | ⚠️ Présent mais **non référencé par le csproj du convertisseur** → à ajouter |
| Tilemap / tileset (+ tiles animées) | `TileMapData` (.tileMap), `TileSetData` (.tileset), `AnimatedTileData`, `CustomProperties` | ✅ Présent |
| Import Tiled `.tmj` (couches, animations, objets, propriétés) | `CasaEngine.EditorServices/Tiled/TiledMapImporter.cs` | ✅ Présent |
| Sprites (région, hotspot, collisions, sockets) | `SpriteData` (.sprite) | ✅ Présent |
| Animations 2D multi-parts (flip, draw order, keyframes) | `Animation2dData` (.anim2d) | ✅ Présent |
| Textures PNG | `Texture2DLoader` | ✅ Présent |
| Worlds + entités + composants | `.world`, `TileMapComponent`, `SpriteRendererComponent`… | ✅ Présent |
| Dialogues / textes localisés | Système Yarn `.dialogue` (`line_texts`) | ✅ Présent |
| Scripting gameplay | `GameplayDllName` (DLL C#), DotNetCompiler | ✅ Présent |
| Physique 2.5D top-down (Height/Slope/gravité Z/murs) | — | ❌ Absent → données préservées en custom, runtime hors périmètre |
| Interpréteur bytecode événements Alundra | — | ❌ Absent → bytecode préservé tel quel, interpréteur hors périmètre |
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

```
alundra-casaengine-project-converter/
├── Program.cs                    # CLI : <inputDir> <outputDir> [--maps 0,4,10] [--phase N]
├── Readers/                      # Lecture data-extracted (POCO + System.Text.Json ou Newtonsoft)
│   ├── AlundraMapReader.cs       # map_N.json
│   ├── AlundraSpriteBankReader.cs# SpriteRecords + map_alundra.json
│   ├── CompanionReader.cs        # map_N.alundra.json (cellules)
│   ├── SoundManifestReader.cs    # bgm.json / sfx.json
│   ├── UiReader.cs               # font3.json / wind.json
│   └── EtcResReader.cs           # ETC_RES.R.json / BALANCE.BIN.json
├── Model/                        # Modèle intermédiaire neutre (découplé des 2 formats)
├── Writers/                      # Génération assets via classes CasaEngine
│   ├── ProjectWriter.cs          # {Nom}.json + AssetInfos.json
│   ├── TileMapWriter.cs          # .tileset/.tileMap (via TiledMapImporter + fusion compagnon)
│   ├── SpriteWriter.cs           # .sprite/.anim2d + PNG par palette
│   ├── AudioWriter.cs            # WAV + manifest loop points
│   ├── TextWriter.cs             # dialogues/tables de chaînes
│   ├── FontWriter.cs             # .fnt BMFont + PNG
│   └── WorldWriter.cs            # .world + entités/portails/events
├── Ids.cs                        # GUIDs déterministes (UUID v5)
└── ConversionReport.cs           # compteurs, warnings, invariants → report.json
```

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

### Phase 0 — Bootstrap et projet vide

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

### Phase 1 — Textures, tilesets, tilemaps

**Entrées** : `data/tiled/map_N.{tmj,tsj,png}`, `data/map_N_tilesheet.png`.
**Sorties** : `Content/Textures/map_N_tileset.png`, `Content/TileSets/map_N.tileset`,
`Content/Maps/map_N.tileMap`, catalogue mis à jour.

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

### Phase 2 — Métadonnées gameplay par cellule

**Entrées** : `data/tiled/map_N.alundra.json` (+ `WallTiles` des JSON natifs).
**Sorties** : données par cellule accessibles au runtime.

1. Fusionner le compagnon dans le `.tileMap` : `CustomProperties` du `TileMapData` (clé →
   JSON sérialisé) **ou** asset compagnon `Content/Maps/map_N.cells.json` référencé en custom
   property — choisir selon la taille (3 120 cellules : mesurer l'impact sur le temps de
   chargement ; si > quelques Mo par map, préférer l'asset compagnon).
2. Y inclure : `Walkability`, `GroundProperty`, `Slope`, `Height`, `Flags`, piles `WallTiles`,
   et les propriétés de map (`Gravity`, `ZViscosity`, `SlideEffectId`, `BalanceLevel`).
3. Documenter le schéma dans `docs/formats/cells-companion.md`.

**Acceptation** : golden file `map_10.cells.json` ; rechargement sans perte (comparaison
round-trip source → converti → relu = identique champ à champ).

### Phase 3 — Sprites et animations

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

### Phase 4 — Audio

**Entrées** : `sound/bgm/*.wav`, `sound/sfx/*.wav`, `bgm.json`, `sfx.json`.
**Sorties** : `Content/Musics/`, `Content/Sounds/`, `Content/Sounds/sfx-manifest.json`.

1. Copier les WAV tels quels (pas de réencodage en V1) ; enregistrer au catalogue.
2. Générer un manifest préservant : id SFX → fichier(s) tone, `SampleRate`, `LoopStart/LoopEnd`,
   `Repeat`, `MaxVoices` ; id BGM → `LoopDetected`, durée.
3. Optionnel (si demandé) : extension moteur exposant `SoundEffect(buffer, sampleRate, channels,
   loopStart, loopLength)` — sinon documenter dans le manifest.

**Acceptation** : 1 041 WAV copiés + manifest ; test unitaire qui instancie un `SoundEffect`
MonoGame avec les loop points du manifest sur un SFX bouclé (ex. `sfx_0001`, LoopStart=28).

### Phase 5 — Textes, dialogues, police

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

### Phase 6 — Worlds, entités, portails, events

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

### Phase 7 — UI et divers

1. `wind.json` → 277 `SpriteData` sur `wind.png` (`Content/UI/`).
2. `memorycard/*.png`, `loading_screen.png` → textures cataloguées.
3. `BALANCE.BIN.json` → `Content/Data/balance.json` (recopie structurée, champs inconnus
   conservés sous leur nom d'origine).

**Acceptation** : catalogue complet, chargement round-trip OK.

### Phase 8 — Validation globale et finition

1. Run complet sur les 483 maps : `0 erreur`, warnings triés par type dans `report.json`.
2. Test automatisé « charge tout » : itérer `AssetInfos.json`, charger chaque asset via la classe
   moteur correspondante (c'est le filet de sécurité principal).
3. Mesurer : temps de conversion total, temps d'ouverture du projet dans l'éditeur, taille disque.
4. Démo : lancer le launcher sur `AlundraGame` et afficher un world (rendu tilemap + une entité
   avec `SpriteRendererComponent` jouant une animation).
5. Mettre à jour `README.md` (usage CLI) et `docs/formats/` (schémas compagnons).

**Acceptation finale** : commande unique → projet complet ; suite de tests verte ; démo visuelle.

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

Framework : xUnit ou NUnit selon ce qu'utilise `CasaEngine.Tests` (s'aligner sur l'existant).

---

## 4. Commandes utiles

```powershell
# Build
dotnet build alundra-casaengine-project-converter/alundra-casaengine-project-converter.csproj

# Conversion complète
dotnet run --project alundra-casaengine-project-converter -- data-extracted output/AlundraGame

# Itération rapide sur 3 maps de référence
dotnet run --project alundra-casaengine-project-converter -- data-extracted output/AlundraGame --maps 0,4,10

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

1. **DLL gameplay `AlundraGame.Gameplay`** : composants typés (entité, portail, map event),
   physique 2.5D (Walkability/Slope/Height/gravité), chargement du manifest audio avec loop points.
2. **Interpréteur du bytecode événementiel** (s'appuyer sur la décompilation du repo
   `alundra-datas-analyser` pour la sémantique des opcodes).
3. Rendu parallaxe/ondes (`ScrollParameters`).
4. Palette swap runtime (shader) en remplacement des PNG dupliqués.
5. Conversion des dialogues en Yarn branché (dépend de l'interpréteur).
