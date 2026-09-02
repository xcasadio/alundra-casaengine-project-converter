# Plan Nettoyage convertisseur — purge des orphelins + GUID déterministes

Date : 2026-09-02. Déclencheur : la clôture d'E12 a mesuré (manifestes avant/après) que l'export
in-place accumule une génération orpheline de pages de tilesets par run (2 générations × 483 pages ×
2 fichiers sur disque aujourd'hui) et régénère TOUS les GUID d'assets à chaque run — aucun export
n'est comparable au bit près, la discipline de vérification par baselines ne vaut que pour les
fichiers sans ids.

Décisions utilisateur (2026-09-02) : **purge + GUID déterministes** (3 sous-tranches) ; la passe
générale « supprimer ce que je n'ai pas écrit ce run » est **différée** (seuls les tilesets
accumulent sur entrées identiques ; l'abandon de dossiers par renommage rejoint le danger n°1 du
chantier éditeur, `docs/editeur-couverture-dll.md` §1).

## §1 — Les faits (reconnaissance à 2 lecteurs, 2026-09-02, file:line vérifiés)

1. **UN seul site accumule** : l'import Tiled du MOTEUR. `ImportTiledMap` importe le PNG de tileset
   via `ImportTexture(..., avoidExistingDestinationFileCollisions: true)` — l'unique site qui passe
   `true` (`EditorAssetImportService.cs:97-103`, paramètre `:674-680`). L'anti-collision
   `CreateUniqueImportedFileName` (`:802-815`) suffixe `_2`, `_3`… tant que le candidat existe sur
   disque et n'est pas le fichier source (`IsExistingDifferentPath` `:817-821`) — la copie identique
   du run précédent compte comme « autre chemin », donc chaque re-run in-place ajoute exactement une
   génération. Le wrapper `.texture` hérite du nom suffixé (`:710-715`).
2. `.tileset`/`.tileMap` n'accumulent PAS : `CreateTileSetFileName` utilise la surcharge à HashSet
   (mémoire de run, pas de disque, `:292-300`/`:772-785`) et `SaveDocument` tronque en place
   (`EditorAssetWriterService.cs:55-66`). Tous les writers du convertisseur écrasent en place à des
   chemins déterministes (TextureAssetWriter.cs:49 `File.Copy overwrite:true`, AudioWriter.cs:166/200,
   TileMapWriter.cs:56, SpriteWriter.cs:649-650/788-792/427, FontWriter, TextWriter, EventCodeWriter,
   BackdropWriter, UiWriter, WorldWriter, PlayerSetupWriter, ProjectWriter — inventaire complet dans
   le rapport de reconnaissance). Rien ne supprime jamais sous `outputDirectory` (seuls deux dossiers
   temporaires %TEMP% sont nettoyés en `finally`, BackdropWriter.cs:147-150/NavigationWriter.cs:120-123).
3. **`AssetInfos.json` est reconstruit de zéro chaque run** : Phase 0 fait
   `EditorAssetCatalogService.Clear()` puis `Save()` réécrit tout (`ProjectWriter.cs:80-83/:106-109`,
   `EditorAssetCatalogService.cs:78-102`) ; le convertisseur ne recharge jamais l'existant. D'où :
   le catalogue ne référence que la dernière génération, et les ids non-`Ids.For` régénèrent.
4. **La comptabilité de la Phase 1 existe déjà** : `TiledMapImportResult.CreatedAssetFileNames`
   rend les chemins relatifs exacts de tout ce que l'import d'UNE carte a créé, noms suffixés
   compris (`TiledMapImporter.cs:1270-1279`, consommé pour compter seulement à
   `TileMapWriter.cs:74-82`).
5. **`AssetVerifier` connaît le mécanisme et le tolère** : la couverture catalogue n'énumère que les
   extensions CHARGEABLES et rétrograde les fichiers hors catalogue en avertissement — son
   commentaire nomme `map_N_tileset_2.png` (`AssetVerifier.cs:87-98`, `:109-124`, `:136-143`) ; les
   `.texture` orphelins sont vus, les `.png` sont INVISIBLES (`png` absent de `Loaders`, `:31-50`).
6. Précédent de forme pour la purge : `DeleteStaleBgm` (analyseur, décision D-X-7) — suppression
   CIBLÉE du fichier exact que l'item courant aurait écrit, jamais un balayage de dossier
   (`AlundraDataExtractor/Program.cs:150-161`).
7. **Ids déterministes : le dépôt est déjà aux ~70 %.** `Ids.For(key)` (UUIDv5, RFC 4122, namespace
   projet fixe, `Ids.cs:14-35`) est utilisé par AudioWriter (:86/:132/:168), WorldWriter
   (:235/:300/:356-363), PlayerSetupWriter (:71/:109/:170), NavigationWriter, FontWriter (:339).
   `docs/plan-conversion-agent-ia.md:515-524` nomme le reste comme LE bloqueur des golden-files.
   Le reste non déterministe, exhaustif :
   - `TextureAssetWriter.cs:53` (`new AssetInfo(Guid.NewGuid())`, clé stable `rawRelativePath` à
     `:51`) et `:57` (`wrapperId`, clé `wrapperRelativePath`) ;
   - Phase 1 : les 5 ids par carte (tmj, png, .texture, .tileset, .tileMap) passent TOUS par
     `EnsureAssetInfo` (`EditorAssetImportService.cs:857-876`) qui **réutilise d'abord l'entrée de
     catalogue au même nom de fichier** (`GetByFileName`) — pré-ensemencer le catalogue rend la
     Phase 1 déterministe SANS changement moteur. `SerializeAsset` écrase `rootObject["id"]` avec
     l'id catalogue (`:878-888`) : les ids `ObjectBase` de TileSet/TileMapData n'atteignent jamais
     les fichiers ;
   - Phase 3/7 : ~12 sites de construction dans SpriteWriter (:312/:321/:324/:336/:343/:349/:363/
     :403/:427/:455/:507/:746/:783) + UiWriter (:98), tous sérialisés, tous avec clé stable
     (bankKey/entityFolderName ; bank+AnimSet+direction ; (spritesheet, signature) ; `wind_{index}` ;
     bankKey+rôle de composant), bloqués par le setter PRIVÉ d'`ObjectBase.Id`
     (`ObjectBase.cs:10`, seul `Load(JObject)` assigne `:46-50`) ; `AssetInfo` pareil
     (`AssetInfo.cs:9/:21`). Fuite annexe : les shapes Box de fixtures sans `Name` sérialisent
     « Object {guid} » (`EditorJsonSaveHelper.cs:11-15` + `ObjectBase.cs:22`).
8. **La cohérence des références est gratuite** : aucun registre central — les ids circulent en
   mémoire dans le run (sprite→anim2d keyframes SpriteWriter.cs:582/:619, prefabAssetIdsByBankKey →
   Program.cs:75-118…) ou par relecture du fichier fraîchement écrit (WorldWriter.cs:286-302 recharge
   le `.tileMap` ; EntityPrefabLinkWriter/CellMetadataWriter/NavigationWriter load-patch-save en
   préservant l'id). Aucun consommateur ne re-dérive un id.
9. **Rien ne dépend d'ids frais entre runs** : catalogue reconstruit (fait 3), moteur charge les ids
   depuis les fichiers, `AssetContentManager` ne cache qu'en mémoire de session, la DLL consomme les
   ids lus des fichiers exportés et ne persiste aucune sauvegarde. La seule contrainte est
   l'unicité DANS un export — et elle échoue BRUYAMMENT (`AssetCatalog.cs:27` `Dictionary.Add`
   jette sur doublon d'id). Nuance connue : `_assetInfosByName` écrase silencieusement sur doublon
   de NOM (`:28-29`) — préexistant (les `sprite_{signature}` en collision inter-banques), inchangé
   par ce chantier.
10. Deux patrons sans-changement-moteur existent déjà pour contourner le setter privé (JObject à la
    main + `SaveDocument` : WorldWriter.cs:240-252 ; charger un JSON minimal via `Load()` puis
    `SaveAsset` : NavigationWriter.cs:84-90/:126-145) — impraticables pour l'arbre de prefab entier,
    dont la valeur est justement que le sérialiseur MOTEUR le construit (SpriteWriter.cs:291-298),
    et `EditorAssetJsonSerializer` est internal.

## §2 — Décisions

- **D-N-1 (utilisateur)** — périmètre : purge + déterminisme ; passe générale différée (fait 2 :
  seule la Phase 1 accumule sur entrées identiques).
- **D-N-2 — purge = PRÉ-nettoyage du dossier `tilemap/` de la carte, dans `TileMapWriter.ConvertMap`,
  immédiatement après `Directory.CreateDirectory`, donc AVANT le `File.Copy` du `.tmj`
  (`TileMapWriter.cs:56`) comme avant `ImportTiledMap`** (CORRECTION P2 de relecture : « avant
  ImportTiledMap » au pied de la lettre supprimait le `.tmj` fraîchement copié — l'import lit le
  SOURCE et aurait catalogué un fichier manquant, 483 erreurs par export) : supprimer tous les fichiers du dossier (possédé exclusivement par la
  Phase 1 — tmj, tileMap, tileset, pngs, textures y sont tous réécrits chaque run ; les phases
  2/3.5/6 relisent le `.tileMap` APRÈS la réécriture de Phase 1). Effet double : les noms deviennent
  STABLES à jamais (toujours `map_N_tileset.png` nu — le prérequis du pré-ensemencement D-N-4) et
  les 2×483×2 orphelins actuels disparaissent au prochain export. La purge ne s'applique qu'aux
  cartes réellement converties ce run (elle vit dans `ConvertMap`, donc respecte `--maps`/`--phase`
  par construction). AUCUNE suppression hors des dossiers `tilemap/` ; la purge vit dans le
  convertisseur — jamais à la main dans `alundra-project` (règle permanente).
- **D-N-3 — durcissement `AssetVerifier`, SCOPÉ AUX RUNS COMPLETS** (CORRECTION P1 de relecture :
  `--phase N` est un plafond et `--maps` ne filtre que les phases 1/2/3.5/6/9, pendant que la phase 0
  reconstruit toujours le catalogue de zéro — un run partiel laisse donc par CONSTRUCTION des
  milliers de chargeables hors catalogue, et une erreur inconditionnelle casserait l'itération à la
  carte, `Program.cs:46-56/:76-84/:126-145`). Donc : (a) chargeable hors catalogue → **erreur
  UNIQUEMENT pour un run complet** (pas de `--maps`, plafond de phase couvrant la dernière phase) ;
  tout run partiel garde l'avertissement, explicitement ; (b) même scope pour la nouvelle
  vérification : tout `.png` sous `Maps/**/tilemap/` doit avoir son wrapper `.texture` catalogué au
  même nom de base. Critère d'acceptation dédié : un run `--maps <id>` contre une sortie peuplée
  finit PASSED code 0 ; un run complet avec un chargeable périmé semé dans un `tilemap/` converti
  finit en erreur.
- **D-N-4 — déterminisme Phase 1 par PRÉ-ENSEMENCEMENT du catalogue** (zéro changement moteur),
  **PRÉCISÉ en relecture (P2)** — la réutilisation d'`EnsureAssetInfo` est un hit de dictionnaire
  ORDINAL sur le chemin relatif NON normalisé (`AssetCatalog.cs:44-48/:242-252`), et `SerializeAsset`
  réécrit aussi le champ `name` depuis l'entrée (`EditorAssetImportService.cs:878-888`) : chaque
  entrée pré-semée doit porter le `FileName` (sortie exacte de `Path.GetRelativePath`) ET le `Name`
  byte-identiques à ce que le moteur produirait. Les cinq, mot pour mot depuis les sites moteur :
  1. `.tmj` — Name = nom de fichier AVEC extension (`EditorAssetImportService.cs:503-514`) ;
  2. `.tileset` — Name = `{mapBaseName}_TileSet` (`:302-316`) ;
  3. PNG brut — Name = `{mapBaseName}_{fileName.png}` extension comprise (`:707`), nom de fichier
     destination = `Path.GetFileName` du chemin de la texture source (`:697-700`) ; **le nom de base
     du PNG n'est PAS dérivable de `MapLocation`, et n'est PAS non plus dans le `.tmj` (CORRECTION
     P1 ronde 2 : tous les `.tmj` d'Alundra référencent un tileset EXTERNE — l'entrée tileset porte
     `"source": "map_N_tileset.tsj"`, sans clé `image`)** : le writer résout `source` relativement
     au tmj, parse le `.tsj` et prend `Path.GetFileName` de sa valeur `image` — la dérivation
     exacte de la branche JSON de l'importeur (`TiledMapImporter.cs:325-333/:342-343`) ; repli :
     `image` lue directement sur l'objet tileset s'il est embarqué. La fixture N2 utilise un `.tsj`
     EXTERNE comme les vraies données, et le compteur « ensemencement sauté » doit rester à 0 sur un
     export complet réel ;
  4. wrapper `.texture` — Name = `{mapBaseName}_{fileNameSansExtension}` (`:711`) ;
  5. `.tileMap` — Name = `mapBaseName`.
  Si le `.tmj` source ne référence pas EXACTEMENT une image de tileset, le writer n'ensemence pas
  cette carte et loggue (compté au report) — l'import retombe sur `Guid.NewGuid`, visible au double
  export plutôt que silencieux. Ids : `Ids.For("<type>:" + cheminRelatif)`, clés préfixées par type.
  Critère : sur une carte fixture, les 5 entrées portent les ids `Ids.For` ET les champs `name`
  écrits sont byte-identiques à la sortie d'avant-changement.
- **D-N-5 — déterminisme TextureAssetWriter** : les deux `Guid.NewGuid` (:53/:57) passent à
  `Ids.For("texture-raw:"+rawRelativePath)` / `Ids.For("texture-wrapper:"+wrapperRelativePath)`.
- **D-N-6 — déterminisme Phase 3/7 par changement moteur ADDITIF, ÉNUMÉRÉ (CORRECTION P2 de
  relecture — `ObjectBase(Guid)` seul ne change rien : chaque site construit un type DÉRIVÉ)** :
  un constructeur protégé `ObjectBase(Guid id)` qui assigne **`Id` ET `Name`** (le ctor par défaut
  pose `Name = "Object " + Id`, `ObjectBase.cs:19-23` — un ctor laissant `Name` null sérialiserait
  un nom null), plus un ctor additif `(Guid id, ...)` chaîné sur CHACUN des types construits par le
  convertisseur : `Entity`, `SpriteData`, `Animation2dData`, `Box` (via l'abstrait
  `Shape3d(Shape3dType)`), et les six composants `TransformComponent` /
  `RenderProjectionComponent` / `AnimatedSpriteComponent` / `CollisionComponent` /
  `DepthSortable2DComponent` / `CharacterControllerComponent` (chaîne SceneComponent/
  EntityComponent) — **~12 fichiers moteur additifs**, aucun appelant existant modifié, `Load`
  continue d'écraser. SpriteWriter/UiWriter passent `Ids.For(<clé stable du fait 7>)` aux ~12
  sites ; **les Box de fixtures reçoivent `Ids.For(bankKey + rôle)` pour l'ID** (leur id est
  sérialisé — un `Name` stable seul laisserait le double export différer) ET un `Name` stable.
  Critères : test moteur — chaque type listé assigne Id + Name non nul par le nouveau ctor, et
  `Load(JObject)` écrase toujours ; test convertisseur — double conversion d'une banque fixture →
  `.entity`/`.anim2d`/`.sprite` byte-identiques, ids de shapes imbriquées comprises.
- **D-N-7 — l'oracle final est le DOUBLE EXPORT** : deux exports complets successifs depuis la même
  extraction → le diff de manifeste doit être **⊆ { report.json }** (il porte les durées de phases).
  Toute autre différence est un ARRÊT. C'est la preuve que le déterminisme est total, pas
  seulement par-site — elle attrape aussi tout non-déterminisme résiduel hors ids (ordre
  d'itération, DateTime) que la reconnaissance n'a pas re-vérifié au bit près.
- **D-N-8** — après la tranche finale, UN re-export remplace `alundra-project` (les ids changent une
  DERNIÈRE fois, vers leurs valeurs déterministes) ; suites + goldens re-vérifiés ; smoke utilisateur
  en jeu (le jeu démarre, la 389 se comporte comme avant) avant clôture.

## §3 — Trois tranches, exécution par agents

Chaque tranche : brief one-shot à un exécuteur (interdictions permanentes rappelées : ne jamais
supprimer `alundra-project/` à la main, ne jamais toucher `CasaEngine.Launcher/Program.cs`, tests en
premier plan), verifier de clôture avant commit.

- **N1 — purge + durcissement (convertisseur seul)**. `TileMapWriter.ConvertMap` : pré-nettoyage du
  dossier `tilemap/` (forme DeleteStaleBgm : itération ciblée + `File.Delete`, comptée dans le
  report — `Report.Increment("Phase1.StalePagesPurged")`). `AssetVerifier` : D-N-3. Tests
  (convertisseur) : un dossier `tilemap/` ensemencé de générations périmées → purgé, les comptes le
  prouvent ; la vérification échoue (erreur, plus avertissement) sur un chargeable hors catalogue ;
  le test png-sans-wrapper. **Mutations imposées** : purge supprimée → le test de purge tombe ;
  purge DÉPLACÉE après la copie du `.tmj` → le test « le tmj de destination existe et le catalogue
  n'a pas d'erreur fichier-manquant » tombe ; erreur redescendue en avertissement (run complet) → le
  test verifier tombe ; erreur appliquée à un run `--maps` → le test du run partiel tombe.
- **N2 — TextureAssetWriter + pré-ensemencement Phase 1 (convertisseur seul)**. D-N-4 + D-N-5.
  Tests : convertir la même carte deux fois (montage fixture) → les 5 ids Phase 1 et les ids
  texture identiques entre les deux runs ; les ids sont ceux d'`Ids.For` (valeur recalculée dans le
  test). **Mutations** : pré-ensemencement supprimé → ids différents entre runs → tombe ; clé sans
  préfixe de type → collision fabriquée dans le test → tombe.
- **N3 — moteur additif + Phase 3/7 (moteur + convertisseur)**. `ObjectBase(Guid)` + bascule des ~12
  sites + noms de fixtures. Tests moteur : le ctor assigne l'id (et `Load` continue d'écraser) ;
  tests convertisseur : double conversion d'une banque fixture → prefab/anim2d/sprite ids
  identiques ; plus aucun « Object {guid} » dans les fichiers écrits. **Mutation** : un site laissé
  en `Guid.NewGuid` → le test de double conversion tombe.
- **Acceptation finale (D-N-7/D-N-8)** : double export complet → diff ⊆ {report.json} ; puis
  re-export adopté, suites (convertisseur 141+n, `Alundra.Tests` 711 avec six goldens byte-identiques,
  moteur sans nouvel échec), smoke utilisateur en jeu, clôture.

**Budget** : 2 commits convertisseur (N1, N2), 1 commit moteur + 1 commit convertisseur (N3),
1 commit de clôture. Ordre strict N1 → N2 → N3 (N2 dépend des noms stables de N1).

**Arrêts** : un fichier supprimé hors d'un dossier `tilemap/` ; un diff de double export hors
report.json ; un golden qui bouge ; `Program.cs` du Launcher stagé ; toute suppression manuelle dans
`alundra-project`.


---

## Clôture — oracle final PASSÉ le 2026-09-02, smoke en jeu en attente

Tranches livrées : N1 `6370f20` (purge + verifier durci scopé), N2 `eaf0676` (pré-ensemencement
Phase 1 + TextureAssetWriter), N3 moteur `2f0d7000` (16 fichiers additifs) + convertisseur `5a91482`
(13 sites). Verifiers de clôture CONFIRMED sur les trois tranches — dont, en N2, des runs réels
partiels sur les vraies données (racines absolue ET relative, 483/483 tmj inspectés) et, en N3, la
re-preuve du baseline des 18 échecs moteur préexistants par restauration.

**D-N-7 PROUVÉ** : deux exports complets in-place successifs → **UN seul fichier différent sur
22 971 : `report.json`** (durées). Zéro ajout, zéro suppression. **D-N-8 fait** : l'adoption a
supprimé exactement les 1 932 orphelins (966 png + 966 texture, les deux générations) et basculé
19 521 fichiers vers leurs ids déterministes ; vérification interne PASSED en mode full-run durci
sur les deux runs (aucun orphelin hors-tilemap légué). Suites : convertisseur 152, moteur 1449+18
préexistants, `Alundra.Tests` 711 avec six goldens byte-identiques contre le projet adopté.

**La discipline de vérification du chantier entier change de régime** : désormais tout changement de
convertisseur se prouve par « double export → diff ⊆ {report.json} + le délta attendu », au bit
près, sur les 22 971 fichiers.

Différés consignés : `--maps` dupliqué → abort sur collision d'ids (P3, injoignable corpus réel) ;
repli tileset embarqué sans fixture (P4) ; assertion « Object » plein-fichier dépendante
d'EntityNames.csv (P4) ; asymétrie de casse spritesheet et unicité des bank keys (P4 préexistants,
désormais fail-fast). Reste dû : smoke utilisateur en jeu (le jeu démarre, la 389 se comporte comme
avant), puis E11.b sur décision utilisateur (plan rel u et commité, non exécuté — reporté par
l'utilisateur le 2026-09-02).
