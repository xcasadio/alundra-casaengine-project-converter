# Plan E7 — Mutation de tuiles à chaud (chantier DLL seule)

Date : 2026-08-28. Étape E7 de [plan-conversion-totale.md](plan-conversion-totale.md) §4. Chantier
**DLL + tests** (décision D-E7-1 : aucune modification moteur, aucun changement convertisseur, aucun
export à relancer). Plan passé par deux rondes de plan-verifier (REVISE sur la sémantique 0x54/0x55,
l'interaction goldens/oracle et l'état du sous-module ; READY en révision 2). Chaque tranche : un
commit, un verifier d'acceptation frais ET une passe adversariale de réfutation.

## 0. Décisions de l'utilisateur (2026-08-28, ne pas re-débattre)

| # | Décision |
|---|---|
| D-E7-1 | **Chantier DLL seule.** Le plan maître prévoyait une API moteur ; elle existe déjà : `TileMapComponent.SetTile/SetTileReference/RemoveTile` (rebuild partiel visuel + collision, `TileRevision`), `AddSortedOverlayTile`/`ClearSortedOverlayTiles` (pas de suppression unitaire), `NavigationGrid2D.SetCell` public. |
| D-E7-2 | **Hors périmètre, documentés** : opcode 0x56 (copie via la table `MapCopies`, non exportée) ; tuiles cassables (`CheckAndTriggerTileEffect`, combat futur) ; consommateur warp du bit gp 0x80 (E10) ; repacking de planes au runtime. |
| D-E7-3 | **Visuels par « adoption dans l'overlay »** : une cellule mutée voit toutes ses contributions visuelles re-dérivées depuis ses nouveaux champs (sol à `(x, y−height)`, mur k à `(x, y−height−offset+k+1)`, depth-slots portés du `WallPlacementReplayer` du convertisseur, comparés ligne à ligne — règle 3 des échelles) et re-soumises en entrées d'overlay ; l'overlay entier est reconstruit (clear + resubmit, ~1360 entrées) aux frames de mutation. Cas dégradé (warning, jamais atteint sur la 389 — précheck en tranche) : sol de cellule mutée vivant dans une couche plate — on ne mute **jamais** `TileMapData` (cachée par Guid → persistance parasite entre chargements). |
| D-E7-4 | **Goldens** : re-baseline de `intro-trace-389.txt`/`intro-programs-389.txt` dès E7.a, revue ligne à ligne, chaque écart expliqué dans le message de commit. **Les assertions de frame épinglées d'`IntroTraceHarnessTests` (554/555/678/801/1034/1202/1704…) sont intangibles** : le diff de ce fichier ne peut contenir que des ajouts ; toute dérive d'un jalon = arrêt et question à l'utilisateur, jamais un ajustement d'assertion. Les quatre traces du héros restent byte-identiques (leur harnais n'exécute pas de programmes). |
| D-E7-5 | **Réentrée de map fidèle** : mutations transitoires — records re-parsés et overlay reconstruit à chaque `InitializeWithWorld`, aucune écriture dans `TileMapData`. |
| D-E7-6 | **0x3B et 0x2F entrent au périmètre** (tranche E7.c) : absents du dispatch, ils gardent la boucle d'ouverture des écoutilles — sans eux rien ne s'ouvre au passage du joueur. |

## 1. Faits qui bornent le plan

Original (`alundra-datas-analyser/AlundraTools/AlundraEngine/`) :

- **0x85** `Script_133_085` (`EntityEventHandlers.cs:2440-2444`, taille 7) →
  `GameEngine.ChangeAreaTileProperties(srcX, srcY, w, h, dstX, dstY)` (`GameEngine.cs:2239-2322`) :
  copie row-major (y externe, x interne) de **6 champs** — Walkability, GroundProperty, Slope, Height,
  TileId, WallTilesOffset — plus copie profonde de la pile de murs (Offset/Count/Tiles ; source sans
  pile → pile destination **supprimée**). **Aucun clamp** (hors bornes = debug break, puis la copie
  procède). Le C# copie la pile en profondeur là où la PSX ne copiait que l'offset brut (commentaires
  `:2285`, `:2309-2310`).
- **0x54** `Script_84_054` (`:1589-1620`, taille 5) : `Walkability |= v3 ; GroundProperty |= v4`
  (**pose** des bits). **0x55** `Script_85_055` (`:1623-1654`, taille 5) : `&= ~v3 / &= ~v4`
  (**efface**). Clamp commun `x∈[0,0x33]`, `y∈[0,0x3b]` **codé en dur** (constantes de l'original,
  pas la taille de map ; la 389 fait exactement 52×60). Ni Slope, ni Height, ni TileId, ni piles.
- Visibilité automatique dans l'original : le rendu (`GraphicManager.RenderTiles:216-311`) et la
  collision (`PhysicsEngine.ComputeEntityGroundHeight:956-1073`, `GetCollisionFlags`) relisent les
  cellules à chaque frame/appel, aucune invalidation. Les `MapTile` sont mutés **en place**, jamais
  remplacés (les entités gardent des références `entity.MapTiles[i]`).
- Autres mutateurs de cellules (hors périmètre, D-E7-2) : 0x56 `Script_86_056` (table `MapCopies`),
  `CheckAndTriggerTileEffect` (`GameEngine.cs:2440-2527`, tuiles cassables). 0x9E lit un rectangle
  sans écrire.
- Bit gp 0x80 : via `tileFlags = walk | gp<<8 | slope<<16 | height<<24` (`PhysicsEngine.cs:1792`),
  `Slope_18c = flags >> 9` = 64 → supprime l'ombre (`EntityManager.cs:720-727`) ; consommateur de
  warp éventuel hors périmètre (E10).

Programmes de la 389 (`docs/intro-programs-389.txt`, autorité : le dump commité) :

- Les « 4 portes » B 130-133 (masqués 2-5, offsets 400/472/544/616) sont quatre **écoutilles** du
  pont, destinations 1×2 : (18,37), (15,27), (21,27), (16,41). Entrée de map : `0x55 [x,y+1,0,128]`
  puis `0x85` template **fermé**. Ouverture (joueur) : `0x3B` zone + `0x70` sol + `0x2F` direction →
  son 61, `0x85` mi-ouvert, `0x85` ouvert, `0x55` puis `0x54 [x,y+1,0,128]`, `0x11`.
- La trappe du marin 15 = l'écoutille (18,37), animée par le programme C masqué 15 (offset 1520)
  pendant l'intro : `0x85` (0,23) → (0,26) → `0x64` → (0,26) → (0,23) → (0,20) → `0x2E`.
- Le programme C masqué 5 (offset 772) bascule la **Walkability bit 1** des cellules
  (17,37),(18,37)/(17..19,38) selon la zone du joueur (masques `[.,.,1,0]`, flag temporaire 0x8005).
- 0x3B et 0x2F n'ont **pas de case** dans `Dispatch` (0x70 et 0x33 en ont). Le harnais force 0x3B
  pessimiste et 0x2F optimiste — forçages qui ne s'appliquent qu'aux opcodes `UnknownSkipped`.
- Piège d'annotation : le dump marque 0x33 `[NOT IMPLEMENTED]` alors que `Dispatch` a un case 0x33 —
  établir en E7.a la provenance des annotations (table statique vs dispatch) pour que les nouveaux
  handlers soient correctement annotés.

Preuve chiffrée (calculée sur `AlundraCells` de l'export réel, indices `y*52+x`) :

- Pour les **24 paires template↔destination** des 4 écoutilles : **Slope et Height identiques**
  (slope 4 partout ; A : h13/h8 ; B : h14/h7 ; C : h14/h7 ; D : h10/h5) ; Walkability identique
  (1 haut / 0 bas) ; tile_id de sol identiques par colonne (12424 ou 65535 en haut ;
  12777/12775/37353 en bas). **Seules varient** : la queue des piles de murs (53249/59/69 fermé ↔
  53250/60/70 mi ↔ 53251/61/71 ouvert) et gp de la rangée basse (0 fermé/mi, 128 ouvert = export).
  Conséquence : **aucune mutation 389 ne change la physique d'un PNJ** — les jalons de frame du
  harnais doivent rester intacts.
- Exception de forme : la pile exportée de (21,27) a offset −1 et 6 tuiles ; ses templates ont
  offset 0 et 7 tuiles (tuile 17166 en plus au sommet). Le premier 0x85 y change la **forme** de la
  pile — couvert par la re-dérivation de D-E7-3, et exercé par un test d'E7.a.
- Observables avant/après de la frame 1 : `(18,38).ground_property` 128 → 0 ; pile de (18,37)
  `[12434,12444,53251,53261,53271]` → `[12434,12444,53249,53259,53269]`. L'export livre les
  écoutilles **ouvertes** ; après E7 elles apparaissent **fermées** à l'entrée — effet utilisateur
  attendu.

DLL (ce repo) :

- `AlundraCellsCollisionField` **aliase** les tableaux parsés des records (`:149-156`) : la mutation
  d'un élément est instantanément visible par **toutes** les sondes (`ClampToGround`,
  `ComputeTerrainHeight`, `UpdateGroundSlope`/`Slope_18c`, `GetTileHeightAtOffset` lisent la même
  instance via `World.CollisionField`). Unique parse de production (`TryCreate` ←
  `InitializeWithWorld`). Seul précalcul : `_surfaceTagCache` (valeurs présentes au chargement).
  Le parse ignore aujourd'hui `tile_id`/`wall_tiles(_offset)`.
- `IEntityWorldContext` n'expose aucun accès aux cellules — E7.a ajoute le seam.
- 0x54/0x55/0x85 tombent dans `UnknownOpcode` (skip par taille, `Result` inchangé) ; tailles au
  `EventOpcodeSizeTable:111-113`.
- `WallPlacementOverlay` a déjà déplacé murs et sols élevés dans l'overlay trié à l'init (strip gaté
  sur gid exact + resubmit, une fois ; ni les records ni le `TileMapComponent` ne sont conservés).
- La grille `NavigationGrid2D` est construite une fois depuis la couche navigation ; seul
  consommateur runtime : le détour 0x1E. La 389 a 0 cellule bloquée (bit 0x40 absent partout).

## 2. Enveloppe

- **Résultat** : sur la 389, 0x54/0x55/0x85 (puis 0x3B/0x2F) sont implémentés fidèlement. À l'entrée
  de map les quatre écoutilles apparaissent fermées (visuel + gp 0x80 effacé) ; la trappe (18,37)
  s'anime pendant l'intro (marin 15) ; avec E7.c, le joueur debout sur la cellule basse, au sol,
  poussant vers l'écoutille, déclenche l'ouverture et la re-pose de gp 0x80 ; le programme 772
  bascule la marchabilité du trou selon la zone du joueur. Collision, sondes du héros, navigation et
  visuels voient chaque mutation.
- **Non-objectifs** : 0x56 ; tuiles cassables ; warp derrière les écoutilles (E10) ; mutation de sols
  en couche plate (dégradé loggé) ; persistance des mutations à la réentrée ; entité debout sur une
  cellule dont la **hauteur** change (aucune mutation 389 n'en change — preuve §1) ; autres maps.
- **Propriétaires** : DLL + `Alundra.Tests` (repo parent), un seul committeur.
- **Prérequis / état de départ** : E3/E4 livrées. Sous-module `CasaEngineMonogame` : HEAD `b73a2068`
  (enregistré par le superprojet) + une modification locale non commitée,
  `CasaEngine.Launcher/Program.cs` (chemin codé en dur, propriété de l'utilisateur, à ne **jamais**
  toucher ni stager). Builds et suites mesurés contre cet état. Repo parent par ailleurs propre.
- **Acceptation globale** : build `alundra-casaengine-project-converter.slnx -c Release` 0 erreur ;
  `Alundra.Tests` verts (538 + nouveaux) ; convertisseur 138 inchangé ; `--filter IntroTrace` vert,
  goldens re-baselinés commités avec diff expliqué ; `git status` vide sur les 4 hero-traces ;
  assertions de frame d'`IntroTraceHarnessTests` inchangées ; runtime utilisateur : écoutilles
  fermées à l'entrée, trappe animée pendant l'intro, et (E7.c) ouverture au passage du joueur.
- **Rollback** : une tranche = un commit (goldens inclus), revert simple.
- **Budget / arrêts** : 4 tranches, ≤ 2 tours de correctifs chacune. Arrêt et question à
  l'utilisateur si : dérive d'un jalon de frame épinglé ; précheck des 12 rectangles trouvant un sol
  en couche plate ; sémantique des params contredite par les données ; diff de goldens inexpliqué.

## 3. Tranches

### E7.a — Store de cellules + 0x54/0x55/0x85 (données seules, aucun visuel) ⏳

- **Parse étendu** : `AlundraCellsRecords` parse en plus `tile_id`, `wall_tiles_offset` et le
  dictionnaire épars `wall_tiles` (`{offset, tiles[]}`) — colonnes déjà exportées ; la colonne
  `flags` reste ignorée (l'original ne copie pas `MapTile.Flags`, propriété dérivée — écart documenté
  sur place).
- **Partage d'instance** : records parsés une fois, partagés entre le champ (constructeur aliasant
  existant ; chemin `TryCreate` depuis records ajouté si nécessaire) et le nouveau store.
- **`AlundraCellStore`** (DLL, sans dépendance moteur) :
  - `CopyCellRectangle` : port exact de `GameEngine.cs:2260-2321` (row-major, 6 champs, copie
    profonde de pile, source sans pile → pile détruite, pas de clamp — warning au lieu du debug
    break, écart documenté, dérivation à côté du code) ;
  - `SetCellBits` (0x54, `|=`) / `ClearCellBits` (0x55, `&= ~`) : ports de
    `EntityEventHandlers.cs:1589-1654`, clamp `[0,0x33]`/`[0,0x3b]` codé en dur comme l'original
    (dérivation documentée) ;
  - un callback interne « cellules mutées (liste) » auquel E7.b abonnera l'applier visuel ;
    E7.a ne l'abonne à rien.
- `_surfaceTagCache` : pré-rempli pour les 256 valeurs au constructeur (une mutation peut introduire
  une valeur absente au chargement ; aujourd'hui → SurfaceTag `""` ; micro-écart documenté).
- **Seam** : `IEntityWorldContext` gagne `IAlundraCellMutator? CellMutator` en membre d'interface par
  défaut (→ `null`) — `NoOpEntityWorldContext` et les fakes compilent sans modification (vérifié sur
  chaque implémentation) ; `HeadlessIntroSimulation` fournit un mutateur adossé à ses records réels.
- **Handlers `Dispatch`** : cases 0x54/0x55/0x85 → `CellMutator` ; retours 5/5/7 (taille, **pas 0** =
  suspension) ; `state.Result` intact ; trace kind `Implemented` ; `CellMutator` null → warning
  unique + skip par taille, trace kind `Degraded` (précédent 0xBD). Mettre à jour la source des
  annotations du dump (cf. anomalie 0x33).
- **Goldens** : régénération des deux fichiers d'intro, revue ligne à ligne, écarts expliqués dans le
  message de commit ; jalons intangibles (D-E7-4).
- **Acceptation** (règle 2 — production ou preuve par neutralisation) :
  1. synthétiques par opcode (patron `NewDocument`/`RunOneScriptCall`) : handler atteint (TraceSink
     `Implemented`), avance 5/5/7, mutation exacte, `Result` intact ; `CellMutator` null →
     `Degraded` + skip ;
  2. données réelles 389 : `CopyCellRectangle(0,20,1,2,18,37)` → (18,37) pile
     `[12434,12444,53249,53259,53269]`, (18,38) gp 0 (héritait 128) ;
     `CopyCellRectangle(0,39,1,2,21,27)` → (21,27) pile offset 0, 7 tuiles commençant par 17166
     (héritait offset −1, 6 tuiles) — changement de forme exercé ; aller-retour exact
     `Set`/`ClearCellBits` sur une cellule réelle ; clamp : (60,70) muté → cellule (0x33,0x3b) ;
  3. production call site (harnais headless, `RunMapEventsPass` réel) : après la frame 1,
     `(18,38).ground_property == 0` (export 128) **et** pile de (18,37) à queue 53249/59/69 (export
     53251/61/71) ; **neutralisation** : même run avec `CellMutator` null → valeurs d'export
     intactes et traces `Degraded` ;
  4. sondes : `TrySampleGround` avant/après mutation de hauteur et de marchabilité d'une cellule
     réelle change de résultat (même instance) ;
  5. suites : `Alundra.Tests` verts, convertisseur 138 inchangé, IntroTrace vert (goldens
     re-baselinés), hero-traces byte-identiques, assertions de frame inchangées, build 0 erreur.
- **Rollback** : revert du commit. **Budget** : un commit, ≤ 1 journée, ≤ 2 tours de correctifs.

### E7.b — Applier visuel + synchronisation navigation ⏳

- Modèle vivant des placements depuis `AlundraWallPlacements`/`AlundraFloorPlacements` + carte
  rawId→(tileset, id local) depuis les propriétés `TileId` du `.tileset` (couvre les 497 raw ids de
  la 389 ; ne **pas** coder `raw & 0x3ff`, faux pour les 36 tuiles synthétiques ≥ 960) ; stocker sur
  le proxy le `TileMapComponent` et les documents (aujourd'hui locaux d'`InitializeWithWorld`).
- Abonnement au callback d'E7.a : re-dérivation des positions/depth-slots (formules du
  `WallPlacementReplayer`, comparaison ligne à ligne) ; clear + resubmit de l'overlay aux frames de
  mutation ; précheck des 12 rectangles (aucun sol en couche plate) ; warnings dégradés.
- `NavigationGrid2D.SetCell` sur mutation de marchabilité (même formule `((walk|gp<<8) & 0x40) == 0`
  que le `NavigationWriter`).
- Tests sur fixture `World` réelle (patron `WallPlacementOverlayTests`) : swap de gids observé dans
  l'overlay après `CopyCellRectangle`, changement de forme de (21,27) produisant l'entrée
  supplémentaire, chemin non-visé prouvé par neutralisation.

### E7.c — 0x3B et 0x2F ⏳

- 0x3B (boîte TileX/Y/Z du joueur — ordre des params relevé dans `Script_59_03B :1223-1238`) et 0x2F
  (`Check moving in dir`). Re-baseline des goldens propre à la tranche : les 4624 occurrences de
  0x3B changent d'annotation ; contrôle de flux réel, résultat attendu identique au forçage
  pessimiste dans la fenêtre tracée (le joueur n'est jamais dans une zone d'écoutille).

### E7.d — Clôture ⏳

- Validation runtime par l'utilisateur (écoutilles fermées à l'entrée, trappe animée pendant
  l'intro, ouverture au passage du joueur) ; mise à jour de `plan-conversion-totale.md` (§4 E7,
  écarts) ; mémoire de session.
