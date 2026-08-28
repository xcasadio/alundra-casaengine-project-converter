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

### E7.a — Store de cellules + 0x54/0x55/0x85 (données seules, aucun visuel) ✅ `326917e`

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

#### Réalisé — écarts et dispositions (2026-08-28, `326917e`)

Livrée verte : `Alundra.Tests` **554** (538 + 16), convertisseur **138** inchangé, build 0 erreur,
IntroTrace vert, quatre traces du héros byte-identiques, jalons de frame intacts (22 `Assert.` avant
comme après). **Verifier d'acceptation CONFIRMED et passe adversariale CONFIRMED** — la première fois
sur ce chantier que l'adversariale ne réfute pas ; elle a prouvé les tests par quatre mutations
réelles (suppression de l'appel 0x85 → 4 échecs ; clamp 0x33→0x34 → 2 ; `Count` faussé → 1 ;
tableaux clonés au lieu d'aliasés → 9).

- **Défaut trouvé et corrigé en session principale avant les vérifications** : le portage avait
  supprimé le champ `Count` de la pile de murs, justifié par « `Count == Tiles.Length` à l'export » —
  vrai au chargement (`WallTiles.cs:19-26` alloue `Tiles = new ushort[Count]`), **faux après une
  copie**. L'original copie `Count` depuis la source (`GameEngine.cs:2297`) et son rendu s'arrête à
  `Count` (`GraphicManager.cs:277`) : sans ce champ, une copie vers une destination plus longue
  exposait une queue périmée qu'E7.b aurait dessinée en tuiles de mur fantômes. `Count` rétabli,
  `GetWallTileStack` restreint aux entrées visibles, test du cas manquant ajouté
  (`ChangeAreaTileProperties_0x85_ShorterSourceStack_HidesTheDestinationsStaleTail`). Latent sur la
  389 (sa seule variation de forme, (21,27), **agrandit** 6 → 7). Illustration de la règle 3 : la
  réutilisation d'une forme sans comparer le contrat.
- **Écart assumé, à ne pas redécouvrir** : `AlundraWorldProxy` **ne câble aucun mutateur** — en jeu,
  les trois opcodes prennent la branche `Degraded` (no-op). C'est le découpage voulu (E7.a n'a pas de
  visuel : muter les cellules sans redessiner donnerait des écoutilles fermées en collision et
  ouvertes à l'écran). **Le câblage du proxy est un item explicite d'E7.b** ci-dessous — sans quoi
  la tranche resterait verte et inerte en jeu, exactement le mode d'échec d'É1.
- **Rejeté** : « le diff du fichier de harnais ne peut contenir que des ajouts » (D-E7-4) est violé à
  la lettre — 4 lignes modifiées, dont la reflowée du `HashSet ImplementedOpcodes`, table que la
  tranche devait précisément mettre à jour, plus la signature du constructeur du harnais. **Aucune
  assertion touchée** : l'intention de D-E7-4 (pas de dérive d'oracle) est tenue. La règle reste
  « aucune assertion modifiée », pas « aucune ligne modifiée ».
- **Provenance des annotations du dump** (question ouverte d'E7.a, close) : le `[implemented]` /
  `[NOT IMPLEMENTED]` d'`intro-programs-389.txt` vient d'un `HashSet<int> ImplementedOpcodes` tenu à
  la main dans `IntroTraceHarnessTests`, **sans lien avec `Dispatch`** — d'où l'anomalie 0x33
  (implémenté depuis des tranches, jamais ajouté au set). Corrigé au passage, même table, une ligne.
- **Différés en E7.b** (P4, aucun ne bloque) : les tests sur données réelles se **sautent
  silencieusement** si `alundra-project/` est absent (7 des 16 passeraient à vide — patron
  préexistant des tests de champ, mais contraire à la leçon du §2.8 de `plan-oracle-heros.md`) ;
  le pré-remplissage des 256 tags n'a aucun test ; le jumeau de neutralisation n'assère pas le kind
  `Degraded` au niveau production (couvert seulement en synthétique) ; une clé `wall_tiles` malformée
  est ignorée sans avertissement ; `GetWallTileStack` rend le tableau vivant, pas une copie ;
  `CellsMutated` alloue à chaque appel même sans abonné.

### E7.b — Applier visuel + synchronisation navigation ✅ `9493b78`

#### Faits établis par la reconnaissance (2026-08-28) — ils changent la conception

1. **Le `plane` ne survit pas à l'initialisation.** `WallPlacementOverlay.Apply/ApplyFloor` n'utilise
   `plane` que pour le **strip** (`GetTileReference(plane,x,y)` puis `RemoveTile(plane,x,y)`) ;
   `AddSortedOverlayTile(tileReference, x, y, in sortKey)` ne le reçoit pas. Une fois les 1356 tuiles
   (774 murs + 582 sols) déplacées dans l'overlay, l'ordre de dessin est **entièrement** décidé par
   la clé de tri. **Conséquence majeure** : l'applier n'a jamais à rejouer le *packing* de planes du
   convertisseur — le plus gros piège identifié (le packing est un algorithme **global et à état** :
   muter (21,27) déplace la tuile 14 de (21,35), cellule jamais mutée, du plane 1 au plane 2 —
   mesuré par ré-implémentation du `Replay` reproduisant l'export à l'identique).
2. **Formules de position** (vérifiées sur les 28 entrées réelles des écoutilles) : sol à
   `(x, y − height)` ; mur k à `(x, y − height − offset + k + 1)`. Le `+1` est la **pré-incrémentation
   de la boucle** de l'original (`GraphicManager.cs:279` incrémente `dy` avant de dessiner) : aucune
   tuile de mur ne se dessine sur sa propre ligne de base. `offset` est **signé** ((21,27) vaut −1).
3. **Deux `y` distincts** — piège de rendu « presque correct » : la **position** utilise la ligne
   calculée ci-dessus, la **clé de tri** utilise le `y` de la **cellule source**
   (`GraphicManager.cs:246, :287` passent la variable de boucle `y`). Élévation sol
   `cellY*16 + clamp(slot,0,5)`, mur `cellY*16 + 7 + clamp(slot,0,6)`, entités slot 6 entre les deux.
4. **Depth slot** : fonction **pure** du raw id — `(raw & 0x3ff) < 960 ? (raw & 0x3ff)/160 : 0`,
   plage 0..5. Vérifié : raw 17176 → 792 → slot 4 (l'export dit 4) ; raw 53251 → 3 → slot 0.
5. **Carte rawId → id local** : propriété par tuile `TileId` du `.tileset` exporté, **stockée en
   CHAÎNE** (`"12388"`), 623 tuiles, toutes distinctes donc injective. `raw & 0x3ff` est faux pour
   les 36 tuiles synthétiques (id local 972 ↔ raw 37353, alors que `37353 & 0x3ff` = 489, une autre
   tuile). `localId = gid − 1` (firstgid 1).
6. **Un seul tileset visuel** : les quatre couches `Render_*` n'ont **pas** de `tile_sources` (donc
   index 0) ; le tileset 1 ne sert qu'à la couche Navigation. `TileMapComponent.TileSetData`
   (public, index 0) suffit — l'index > 0 n'est pas exposé.
7. **Pas de surface hors écran** : aucun `TileMapSurfaceComponent` dans la DLL ni dans le `.world` de
   la 389 → la carte est dessinée dans la passe principale, donc l'absence de bump de `TileRevision`
   par les opérations d'overlay (vraie, vérifiée) est **inerte ici**. À re-vérifier si une map passe
   un jour par la surface.
8. **Pas de suppression unitaire dans l'overlay** (confirmé exhaustivement) : `clear + resubmit` est
   la seule stratégie, pas un choix. Coût : ~1356 allocations de `Tile` par reconstruction, aucun
   buffer GPU ni corps physique. `AddSortedOverlayTile` **lève** (n'avertit pas) sur référence vide,
   index de tileset inconnu, id inconnu ou tuile `Auto` → pré-valider avant de soumettre.
9. **Ce qui change réellement sur la 389** : *aucun* sol. Les 12 rectangles laissent hauteur, pente,
   marchabilité et tile_id des destinations **inchangés** ; seules bougent la queue des piles de murs
   (gids 2/12/22 ↔ 3/13/23 ↔ 4/14/24) et, pour (21,27) seule, la **forme** de la pile (6 → 7 tuiles,
   nouvelle position de dessin (21,14)). Les 0x54/0x55 ne touchent que marchabilité et
   ground_property : **aucun effet visuel**.
10. **Le cas dégradé D-E7-3 est prouvé inatteignable sur la 389** : les 5 cellules de destination qui
    ont un sol sont toutes dans `AlundraFloorPlacements` ; les 3 autres ont `tile_id 65535` (pas de
    sol). Sur toute la carte, les 477 cellules `height ≠ 0` avec un sol y sont **toutes** (0
    exception). **Le précheck doit tester « a un sol ET est dans les placements », pas « a un sol »**
    — sinon quatre fausses alertes par entrée de map.
11. **(21,14), la seule position créée par une mutation**, porte aujourd'hui `Render_0 = 986` et
    `Render_1 = 633` — mais ces deux tuiles **sont** des placements ((21,24) sol, (21,35) mur k14),
    donc retirées des couches plates à l'init : la position est libre au runtime. **À vérifier par
    test, pas à supposer.**
12. **Le programme C masqué 5 (offset 772) mute (17,37), (17,38) et (19,38)** — trois cellules qui ne
    sont **pas** des destinations d'écoutille et qui portent leurs propres piles et sols. L'applier
    doit gérer toute cellule mutée, pas seulement les 12 rectangles.

#### Faits ajoutés à la relecture (plan-verifier, 2026-08-28)

13. **Le document de placement est un SUR-ENSEMBLE de l'overlay réel.** `Apply`/`ApplyFloor` sautent
    entièrement toute entrée dont le gid ne correspond pas à la tuile plate vivante (ni strip, ni
    resubmit ; un seul `Logs.WriteError` agrégé, aucune valeur de retour —
    `WallPlacementOverlay.cs:255-266, :308-319`). Ensemencer le modèle depuis les **documents**
    resoumettrait ces entrées à la première reconstruction alors que leur tuile plate est toujours en
    place → **double dessin**, précisément ce que l'ensemencement prétend éviter.
14. **Trous `0xffff` dans les piles** : la boucle du convertisseur saute les tuiles vides **mais leur
    `stackIndex` consomme sa ligne** — le `k` de la formule est l'**indice brut** dans le tableau, pas
    un compteur compacté (`WallPlacementReplayer.cs:226-235`). Un applier qui compacte décale d'une
    ligne toutes les entrées après un trou. **Réel sur la 389** : 34 des 233 piles contiennent des
    `0xffff`, certains **intérieurs** avec des tuiles après (ex. (13,35), (11,35)).
    **Portée exacte** (correction de relecture) : la reconstruction **rejoue des entrées mémorisées**,
    elle ne re-dérive rien — la règle du `k` brut ne s'exécute donc que pour une cellule **mutée**.
    Aucun des 12 templates ni des 4 destinations ne porte de trou : le risque est **latent partout,
    sauf pour une mutation dont la source en porte un**. C'est exactement ce que l'item 2 ter doit
    fabriquer, sinon il ne peut pas échouer.
15. **Une cible hors carte est abandonnée SILENCIEUSEMENT** à l'export (`PlaceTile` rend −1, jamais
    enregistrée, aucune trace — `:242-247, :476`). L'applier doit s'aligner : **pas d'avertissement**
    par occurrence et par frame.
16. **L'overlay n'est pas observable publiquement** : `TileMapComponent` n'expose que
    `SortedOverlayTileCount` (`:131`) ; la liste et le type d'entrée sont privés (`:28, :84`) et le
    moteur est intouchable (D-E7-1). Précédent au dépôt : `WallPlacementOverlayTests.cs:595-596`
    accède déjà à des champs privés par réflexion.
17. **`AlundraWorldProxy.InitializeWithWorld` n'est pas atteignable en test** : il exige un
    `CasaEngineGame` vivant et un catalogue d'assets peuplé, et aucun test ne l'appelle
    (`AlundraWorldProxyTests.cs:21-24`).

#### Conception

- **Modèle vivant des contributions**, porté par le proxy, **ensemencé depuis ce que `Apply`/
  `ApplyFloor` ont RÉELLEMENT soumis** — les deux méthodes rendent désormais la liste des entrées
  soumises (changement DLL, autorisé), au lieu d'être lues depuis les documents bruts (fait 13).
  L'overlay contient ainsi exactement l'ensemble retiré des couches plates, sans quoi une tuile
  serait dessinée deux fois ou disparaîtrait. Les 105 promotions de « clôture » (sols de hauteur 0
  partageant une position avec un placement) sont héritées telles quelles, sans rejouer leur règle
  globale.
- **Testabilité du câblage (fait 17)** : le bloc d'`InitializeWithWorld` qui installe champ, store,
  `CellMutator`, grille de navigation et overlay est **extrait en une méthode interne** appelée à la
  fois par `InitializeWithWorld` et par les tests (montage `World` + `TileMapComponent` sans
  `CasaEngineGame`). C'est ce chemin — et non un store construit à la main — que traversent les
  tests d'acceptation.
- **Sur mutation** : re-dériver les contributions des seules cellules mutées (positions, gids, depth
  slots, `cellY` source) et **comparer au modèle**. Si rien ne diffère → **aucune reconstruction**
  (rend les 0x54/0x55 gratuits). Sinon : remplacer les entrées de ces cellules, puis
  `ClearSortedOverlayTiles` + resubmit du modèle entier. **Coalescer par frame** (l'entrée de map
  déclenche 4 × 0x85 : une seule reconstruction).
- **Bornes de pile** : itérer le `Count` visible via `GetWallTileStack` (E7.a), **jamais**
  `Tiles.Length` — la formule du convertisseur (`WallPlacementReplayer.cs:226`) itère `Tiles.Length`
  et la recopier verbatim réintroduirait le défaut corrigé en E7.a.
- **Trous dans les piles (fait 14)** : parcourir les indices `0..Count−1` en **conservant l'indice
  brut comme `k`** ; une entrée `0xffff` est ignorée **sans compacter** — sa ligne reste consommée.
  Compacter décalerait d'une ligne toutes les tuiles situées après un trou.
- **Hors carte (fait 15)** : entrée simplement ignorée, **sans avertissement** — l'export fait de
  même en silence ; avertir par frame serait à la fois infidèle et bruyant.
- **`stableId`** : conserver celui de l'init pour toute entrée déjà présente ; une entrée nouvelle
  reçoit un id au-delà du maximum. (Les égalités de clé n'arrivent qu'entre cellules distinctes de
  même ligne et même slot, donc visuellement disjointes ; le déterminisme suffit.)
- **Câblage du proxy (item n°1, sans quoi la tranche est verte et inerte en jeu)** : `AlundraWorldProxy`
  construit le `AlundraCellStore` depuis les records déjà parsés dans `InitializeWithWorld` (à côté
  de l'installation du champ), **surcharge `IEntityWorldContext.CellMutator`**, et retient le
  `TileMapComponent` et les deux documents (aujourd'hui locaux `:497, :533, :544`).
- **Navigation** : sur mutation de marchabilité/gp, recalculer `((walk | gp<<8) & 0x40) == 0` (formule
  du `NavigationWriter`) et `NavigationGrid2D.SetCell` en **reportant le masque de couches** de la
  cellule existante (`CanEnter` exige `IsWalkable` **et** une intersection non vide : une cellule
  marchable avec `NavigationLayerMask.None` est inatteignable).
- **Dégradé, jamais fatal** : id brut absent de la carte, sol muté hors placements, position hors
  carte → avertissement et entrée ignorée. Ne jamais laisser `AddSortedOverlayTile` lever en pleine
  frame.

#### Acceptation

**Observation de l'overlay (fait 16)** : tous les items qui assèrent le CONTENU de l'overlay le lisent
par **réflexion sur `_sortedOverlayTiles`** (champs `TileReference`/`GridX`/`GridY`/`SortKey`), via un
unique helper de test — jamais sur le modèle vivant côté DLL, qui resterait correct si l'applier
n'appelait plus `AddSortedOverlayTile`. **Mutation obligatoire à exécuter et rapporter** : supprimer
l'appel `AddSortedOverlayTile` de l'applier doit faire échouer les items 2, 3, 4 et 6.

1. **Câblage** : un test traverse le **chemin de production** (la méthode interne extraite,
   fait 17) et observe que `((IEntityWorldContext)proxy).CellMutator` est non nul **et** que
   l'abonnement à `CellsMutated` est posé sur le `TileMapComponent` réel. **Mutation** : supprimer la
   ligne d'abonnement dans le proxy fait échouer ce test (prouvé, pas raisonné).
2. **Swap de gids** (données réelles, fixture `World` du patron `WallPlacementOverlayTests`) : après
   `CopyCellRectangle(0,20,1,2,18,37)`, les entrées d'overlay de (18,37) portent les gids 2/12/22
   (fermé) au lieu de 4/14/24 (ouvert), aux **mêmes** positions (18,25..29) et avec les **mêmes**
   clés de tri ; neutralisation : en retirant **l'abonnement posé par la production**, les gids
   restent 4/14/24.
2 bis. **Entrée désaccordée jamais resoumise (fait 13)** : avec un document portant une entrée dont
   le gid ne correspond pas (patron `Apply_MismatchedGid_LeavesTileInPlaceAndSkipsOverlay`), après
   une mutation et une reconstruction, cette entrée n'est **pas** soumise et sa tuile plate reste
   unique (pas de double dessin).
2 ter. **Trou de pile (fait 14)** : la règle du `k` brut ne s'exécute que sur une cellule **mutée**,
   donc le test doit en **fabriquer** une — `CopyCellRectangle` prenant pour **source** une cellule
   dont la pile porte un `0xffff` intercalé suivi de vraies tuiles ((13,35) ou (11,35) sur la 389,
   sinon un store synthétique). La tuile qui suit le trou est soumise à
   `y = cellY − height − offset + k + 1` avec `k` = son **indice brut**. **Mutation obligatoire** :
   faire compacter les indices à l'applier doit faire échouer cet item (prouvé, pas raisonné). Et
   aucune alerte n'est émise pour une entrée tombant hors carte (fait 15).
3. **Changement de forme** : après `CopyCellRectangle(0,39,1,2,21,27)`, l'overlay porte **7** entrées
   pour (21,27), la nouvelle en (21,14) avec le gid 783 (raw 17166) et le slot 4 ; et **rien** ne
   subsiste en couche plate à (21,14) sur les 4 planes (vérification du fait 11).
4. **Clés de tri exactes** : pour au moins une entrée de mur et une de sol, la clé re-dérivée est
   **égale champ par champ** à celle produite à l'init par `WallPlacementOverlay` (test qui échoue si
   l'on utilise la ligne de dessin au lieu du `y` source — le piège du fait 3).
5. **Aucun effet visuel des bits** : un `SetCellBits`/`ClearCellBits` réel ne déclenche **aucune**
   reconstruction d'overlay (compteur de reconstructions observé), et l'overlay est identique avant
   et après.
6. **Sols invariants** : après les 12 rectangles, les entrées de sol des 8 cellules de destination
   sont **inchangées** (position et gid) — conforme au fait 9 ; et aucun avertissement de cas
   dégradé n'est émis (fait 10).
7. **Navigation, par le chemin câblé** : le test **injecte une grille** dans le proxy (le résolveur
   réel dégrade à `null` sans `AssetContentManager` vivant), puis mute par
   `SetCellBits`/`ClearCellBits` **du store câblé** — donc via le handler `CellsMutated` posé par la
   méthode interne d'installation, jamais en appelant le helper de synchronisation directement.
   Marchabilité portant le bit **0x40** (inatteignable sur la 389 — ce bit est absent de toute la
   carte, donc un test 389 seul ne distinguerait pas un `SetCell` correct d'un no-op) : la cellule de
   navigation bascule et le **masque de couches est conservé**. **Deux mutations obligatoires** :
   retirer l'appel `NavigationGrid2D.SetCell` du handler doit faire échouer cet item ; abandonner le
   masque de couches doit faire échouer l'assertion de masque.
8. **Cellules hors rectangles** : les mutations du programme 772 sur (17,37)/(17,38)/(19,38) sont
   traitées sans avertissement et sans changement visuel (elles ne touchent que des bits).
9. **Reprise des différés d'E7.a** : les tests sur données réelles **échouent** (message nommant
   l'export manquant) au lieu de se sauter ; test du pré-remplissage des 256 tags ; assertion du kind
   `Degraded` au niveau production sur le jumeau de neutralisation ; avertissement sur clé
   `wall_tiles` malformée ; `GetWallTileStack` ne rend plus le tableau vivant ; `CellsMutated`
   n'alloue pas sans abonné.
10. **Suites** : build 0 erreur ; `Alundra.Tests` verts (554 + nouveaux) ; convertisseur 138 ;
    IntroTrace vert **et goldens inchangés** (E7.b ne change aucun opcode ni flux — un diff de golden
    dans cette tranche est un signal d'arrêt) ; quatre traces du héros byte-identiques.

- **Limites documentées** (à écrire dans le code) : le jeu de « clôture » et l'invariant de conflit
  résiduel sont des fonctions **globales** de la carte, calculées à l'export seulement ; l'applier ne
  les recalcule pas. Prouvé sans effet pour les copies de la 389 (105 entrées de clôture avant comme
  après, 4 planes) — mais une future map ou un futur opcode pourrait l'exiger. Le packing de planes
  n'est pas rejoué non plus (sans objet pour l'overlay, fait 1).
- **Rollback** : revert du commit. **Budget** : un commit, ≤ 1 journée, ≤ 2 tours de correctifs.
  **Arrêts** : si un golden bouge ; si le précheck signale un sol hors placements sur la 389 ; si une
  clé de tri re-dérivée diffère de celle de l'init.

### E7.b-bis — Phase des tuiles animées de l'overlay (moteur) ✅ moteur `1c5bf445`, pointeur `1215f3b`

**Décision utilisateur du 2026-08-28** : corriger **dans le moteur**, conformément à la règle
permanente du chantier (un défaut de rendu se corrige dans `CasaEngineMonogame`, jamais contourné en
aval). Écart assumé à **D-E7-1** (« DLL seule ») : E7 gagne un commit sous-module + bump de pointeur.

#### Le défaut (mesuré, pas déduit)

`AddSortedOverlayTile` fabrique **une nouvelle instance de `Tile` par appel**
(`TileMapComponent.CreateOverlayTile:930-966`) et `ClearSortedOverlayTiles:753-773` désenregistre puis
jette les anciennes. Un `AnimatedTile` neuf repart à `_currentFrameIndex = 0`,
`_elapsedFrameMilliseconds = 0` (`AnimatedTile.cs:11-12`). Comme la reconstruction d'E7.b est un
`clear + resubmit` intégral (seule stratégie possible, il n'existe aucune suppression unitaire),
**chaque mutation d'écoutille remet à l'image 0 les 223 entrées animées** de la 389 (114 murs +
109 sols sur 1356, soit 16 % — 18 tuiles animées distinctes, ids locaux 800-804 et 807-819), et les
**désynchronise définitivement** des tuiles animées identiques restées dans les couches plates, qui
gardent leur phase. Couture visible entre des tuiles censées animer ensemble. Observé : indices
d'image 4,4,4,4,4 → 0,0,0,0,0 après un flush.

#### Conception (additive, **aucun contrat public modifié**)

- **Cache d'instances par référence**, porté par le composant :
  `Dictionary<TileMapTileReference, Tile>`. `AddSortedOverlayTile` consulte le cache avant de créer ;
  `CreateOverlayTile` n'est appelé qu'au **premier** usage d'une référence. Les entrées d'overlay
  partageant une même référence partagent alors une instance — **plus fidèle à l'original**, dont
  `GetAnimatedTileId` est une fonction **globale** de la frame (`GraphicManager.cs:313-322`) : deux
  tuiles animées de même id y sont en phase par construction.
- **La phase survit parce que l'INSTANCE survit, pas parce qu'elle reste enregistrée**
  (correction de relecture, ronde 1). `ClearSortedOverlayTiles` **garde son comportement actuel** :
  il désenregistre de `_animatedTiles`. `_hasAnimatedTiles` conserve donc exactement sa sémantique,
  `HasAnimatedTiles` reste vrai ssi une animée est réellement vivante, et
  `TileMapSurfaceComponent.ShouldRedraw` comme `ShouldUpdateWhenConditional` sont **intouchés** — les
  trois problèmes qu'un enregistrement permanent aurait créés. Le test existant
  `ClearSortedOverlayTiles_UnregistersAnimatedOverlayTilesFromTheUpdateLoop` reste **valide et non
  amendé**. Entre le `Clear` et le resubmit d'une même reconstruction aucun `Update` ne tourne : la
  phase ne dérive pas pendant cet intervalle.
- **Comptage de références, obligatoire dès qu'on partage** : une instance animée partagée par N
  entrées ne doit être enregistrée qu'**une fois** dans `_animatedTiles`, sinon elle serait mise à
  jour N fois par frame et **l'animation tournerait N fois trop vite**. Le composant tient donc un
  compteur par instance mise en cache : `Add` enregistre au passage 0 → 1, `Clear` décrémente et
  désenregistre au passage 1 → 0. Coût O(1) par entrée, contenu de `_animatedTiles` **identique à
  aujourd'hui** (enregistrée ssi ≥ 1 entrée vivante l'utilise).
- **Égalité du type de clé (correction de relecture, ronde 1)** : `TileMapTileReference` est un
  `readonly struct` **sans** `Equals`/`GetHashCode`/`IEquatable`, donc
  `EqualityComparer<T>.Default` retomberait sur le comparateur boxant de `ValueType` — une
  allocation par consultation, sur un chemin par frame, interdite par les règles d'allocation du
  moteur. La tranche implémente donc `IEquatable<TileMapTileReference>` + `Equals`/`GetHashCode` sur
  le struct (**additif** : aucun renommage, aucun champ sérialisé touché).
- **Cycle de vie** : le cache est vidé **uniquement** dans `InitializeWithWorld` (`:174`), là où
  l'overlay est déjà vidé — au chargement de monde, jamais entre deux reconstructions.
- **Bénéfice secondaire** : la reconstruction n'alloue plus ~1356 `Tile` par frame de mutation
  (mesure d'E7.b) mais zéro après échauffement — **à condition** que l'égalité ci-dessus soit en
  place, sans quoi le boxing remplacerait simplement une allocation par une autre.
- **Non-objectifs** : aucune API de suppression unitaire (hors périmètre) ; aucune modification du
  chemin des couches plates ; aucun changement de `TileRevision` ni de `HasAnimatedTiles` (la 389 ne
  passe pas par la surface hors écran — fait 7 d'E7.b — mais ce contrat reste intact pour les maps
  qui y passent).

#### Acceptation

1. **Test moteur de phase** (`CasaEngine.Tests\TileMap\TileMapComponentSortedOverlayTests.cs`) : une
   entrée d'overlay animée avancée de plusieurs images, puis `ClearSortedOverlayTiles` +
   ré-`AddSortedOverlayTile` de la **même référence** → `CurrentFrameIndex` **conservé** et instance
   **identique** (`ReferenceEquals`). **Mutation** : rétablir la création systématique fait échouer
   ce test.
2. **Partage par référence** : deux entrées d'overlay de même référence partagent l'instance et sont
   donc en phase ; deux références différentes ne la partagent pas.
3. **Enregistrement unique et comptage** : après N ajouts de la même référence animée, l'instance
   n'est enregistrée qu'**une fois** dans `_animatedTiles` — un test avance le temps et vérifie que
   l'animation progresse à la vitesse **nominale**, pas N fois trop vite. Après un `Clear`, elle est
   désenregistrée (compteur retombé à 0) ; après `InitializeWithWorld`, le cache est vide.
3 bis. **`HasAnimatedTiles` inchangé** : sans aucune animée en couche plate, `HasAnimatedTiles` est
   vrai après `AddSortedOverlayTile` d'une animée et **faux** après `ClearSortedOverlayTiles` — la
   sémantique d'aujourd'hui. Cet item échoue si l'implémentation laisse les instances enregistrées.
3 ter. **Aucun boxing sur le chemin par frame** : `EqualityComparer<TileMapTileReference>.Default`
   résout bien vers l'implémentation `IEquatable`, et N re-ajouts d'une référence déjà en cache
   n'allouent rien de mesurable (`GC.GetAllocatedBytesForCurrentThread`).
3 quater. **CYCLE COMPLET SUR CACHE CHAUD — l'item qui décide de la tranche.** L'enregistrement dans
   `_animatedTiles` vit aujourd'hui **à l'intérieur de `CreateOverlayTile`** (`:954-955`), l'appel même
   que le cache court-circuite au deuxième usage d'une référence. Une implémentation qui laisse
   l'enregistrement là et nulle part ailleurs **gèle définitivement** les tuiles animées de l'overlay
   après la première reconstruction (le `Clear` les désenregistre, le re-`Add` depuis le cache ne les
   réenregistre jamais) — une régression **pire** que le défaut corrigé, et tous les items ci-dessus
   la laisseraient passer : une tuile gelée conserve son instance, son `CurrentFrameIndex` et ne
   « saute » pas. Le test exerce donc le cycle entier : `Add` d'une animée → avance du temps →
   `ClearSortedOverlayTiles` → re-`Add` de la **même** référence → nouvelle avance du temps, et
   assère (a) que `CurrentFrameIndex` **continue de progresser** après le re-`Add`, (b) que
   `HasAnimatedTiles` est **de nouveau vrai**, (c) qu'après N re-`Add` de la même référence l'instance
   est présente **exactement une fois** dans `_animatedTiles` et que la vitesse reste **nominale**.
   **Mutation obligatoire** : ne réenregistrer qu'au sein de `CreateOverlayTile` (donc pas au hit de
   cache) doit faire échouer cet item.
4. **Non-régression, sans amender l'oracle** : les tests existants de
   `TileMapComponentSortedOverlayTests` — **dont**
   `ClearSortedOverlayTiles_UnregistersAnimatedOverlayTilesFromTheUpdateLoop` et
   `SavedEntity_NeverContainsSortedOverlayTiles` — restent verts **inchangés** ; la conception révisée
   ne change aucun contrat, donc **aucun amendement de test existant n'est autorisé** dans cette
   tranche (s'il en faut un, c'est le signe que la conception a dérivé : arrêt et question à
   l'utilisateur). `CasaEngine.Tests` sans **nouvel** échec (18 préexistants). Rappel opérationnel :
   le `.sln` n'inclut pas `CasaEngine.Tests` — le builder explicitement avant
   `dotnet test --no-build`.
5. **Côté DLL, aucune régression** : `Alundra.Tests` 568 et convertisseur 138 inchangés après bump du
   pointeur ; goldens d'intro et quatre traces du héros byte-identiques.
6. **Runtime (utilisateur, avec E7.d)** : les tuiles animées ne sautent plus quand une écoutille
   s'ouvre ou se ferme.

- **Découpage et discipline de staging** (première tranche du programme à commiter dans le
  sous-module — le rappeler ici parce que le sous-module porte une modification **non commitée et
  propriété de l'utilisateur**, `CasaEngine.Launcher/Program.cs`, chemin codé en dur) :
  1. **commit moteur**, en stageant **nommément** les seuls chemins :
     `CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs`,
     `CasaEngine/Framework/Assets/TileMap/TileMapTileReference.cs`,
     `CasaEngine.Tests/TileMap/TileMapComponentSortedOverlayTests.cs`.
     **`git add -A` et `git commit -a` sont interdits dans le sous-module.**
     `CasaEngine.Launcher/Program.cs` doit rester **modifié et non stagé**, avant comme après.
  2. **bump du pointeur** dans le repo parent (le seul chemin stagé y étant `CasaEngineMonogame`).
  **Acceptation de la discipline** : après les deux commits, `git -C CasaEngineMonogame status
  --short` montre toujours `CasaEngine.Launcher/Program.cs` modifié, et il est absent de la liste des
  fichiers du commit moteur.
- **Rollback** : revert des deux commits (le fichier de l'utilisateur n'ayant jamais été stagé, il
  survit aux deux). **Budget** : deux commits, ≤ 2 tours de correctifs. **Arrêts** : si le partage
  d'instance exige d'amender un test moteur existant ; si le comptage de références fait diverger la
  vitesse d'animation observable — **ou si une tuile animée de l'overlay cesse d'animer après une
  reconstruction** (le mode d'échec symétrique, couvert par l'item 3 quater) ; si `HasAnimatedTiles`
  ne peut pas garder sa sémantique.

#### Réalisé — écarts et dispositions d'E7.b et E7.b-bis (2026-08-28)

**E7.b** (`9493b78`) : `Alundra.Tests` **568** (554 + 14), convertisseur **138**, build 0 erreur,
goldens d'intro et quatre traces du héros byte-identiques. Verifier d'acceptation **REFUTED** puis
CONFIRMED après correctif ; passe adversariale **REFUTED** sur un défaut réel (ci-dessous).

- **Défaut de test corrigé avant commit (P2, item 4)** : le test des clés de tri restait vert alors
  qu'on supprimait `AddSortedOverlayTile` de l'applier — il pilotait `SetCellBits(18,37,0,0)`, une
  mutation **sans effet visuel par construction** (c'est l'objet de l'item 5), donc rien n'était
  reconstruit et l'entrée lue était celle posée à l'**initialisation**. Réécrit pour traverser une
  vraie reconstruction (`CopyCellRectangle(0,20,1,2,18,37)`) et couvrir aussi une entrée de **sol**,
  que le plan exigeait. Mutation rejouée en session principale : l'item tombe désormais avec les
  autres.
- **Preuve indépendante forte** : la passe adversariale a re-dérivé en Python les 582 sols et 774
  murs depuis les tableaux de cellules bruts — **0 écart sur 1356** — puis rejoué les 12 rectangles
  et comparé l'overlay entrée par entrée : 1357 contre 1357, aucun manquant, aucun en trop.
  Elle a aussi vérifié empiriquement que (21,14) ne porte **aucune** tuile plate sur les 4 planes,
  donc pas de double dessin (fait 11 confirmé, non supposé).
- **Différés P3/P4 assumés** : l'item 6 n'exerce que 4 des 12 rectangles (équivalents par la preuve
  §1) ; « aucun avertissement » n'est jamais asséré faute de puits de log capturable dans
  `Alundra.Tests` ; l'item 2 n'épingle pas les valeurs de gids (E7.a épingle déjà la pile brute) ;
  le flush précède le rattrapage D3, donc une mutation issue de cette passe n'est visible qu'à la
  frame suivante (les 0x85 de la 389 viennent des passes antérieures — latent) ; `ProcessCellWalls`
  n'a pas la garde anti-double-dessin de `ProcessCellFloor` (0 désaccord de gid sur la 389 —
  latent) ; un sol de hauteur 0 hors placements muté est ignoré **sans** avertissement alors que
  D-E7-3 en demande un (aucun sol ne change sur la 389 — latent, détection exacte demanderait un
  instantané des ids initiaux).

**E7.b-bis** (moteur `1c5bf445`, pointeur `1215f3b`) : **les deux passes CONFIRMED**. Moteur 1420
tests avec les **mêmes 18 échecs préexistants** nommément identiques ; diff du fichier de tests
moteur purement additif (168 ajouts, 0 suppression) ; `CasaEngine.Launcher/Program.cs` resté modifié,
non stagé, absent du commit — discipline de staging tenue.

- **Le piège que la relecture a sauvé** : l'enregistrement dans `_animatedTiles` vivait **dans**
  `CreateOverlayTile`, l'appel même que le cache court-circuite. L'implémentation évidente aurait
  **gelé** les tuiles animées de l'overlay après la première reconstruction — pire que le défaut
  corrigé — et tous les critères écrits jusqu'alors l'auraient laissée passer (une tuile gelée garde
  son instance, garde son image, ne saute pas). Mutation rejouée en session principale : seul l'item
  3 quater tombe.
- **Correction P4 appliquée après verdict** (commentaire seul, aucun comportement) : la doc de
  `SortedOverlayTile.Tile` décrivait encore le modèle « une instance par entrée » et aurait conduit
  un lecteur à poser un état par entrée sur une instance désormais **partagée**, corrompant les
  entrées sœurs. Réécrite ; 90 tests TileMap re-vérifiés verts.
- **Lacunes de phase résiduelles, NON introduites par la tranche et non fermées par elle** (P3, à ne
  pas redécouvrir) : (1) une instance en cache **gèle** tant que sa référence n'a aucune entrée
  vivante, donc une référence qui quitte l'overlay puis revient reste déphasée — atteignable si une
  écoutille se referme après s'être ouverte ; (2) une référence soumise pour la **première fois** en
  cours de partie naît à l'image 0, déphasée des tuiles plates de même id qui animent depuis le
  chargement, et le cache rend cet écart permanent. Les deux existaient déjà avant la tranche.

### E7.c — 0x3B et 0x2F ⏳

- 0x3B (boîte TileX/Y/Z du joueur — ordre des params relevé dans `Script_59_03B :1223-1238`) et 0x2F
  (`Check moving in dir`). Re-baseline des goldens propre à la tranche : les 4624 occurrences de
  0x3B changent d'annotation ; contrôle de flux réel, résultat attendu identique au forçage
  pessimiste dans la fenêtre tracée (le joueur n'est jamais dans une zone d'écoutille).

### E7.d — Clôture ⏳

- Validation runtime par l'utilisateur (écoutilles fermées à l'entrée, trappe animée pendant
  l'intro, ouverture au passage du joueur) ; mise à jour de `plan-conversion-totale.md` (§4 E7,
  écarts) ; mémoire de session.
