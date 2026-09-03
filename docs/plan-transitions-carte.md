# Plan — Transitions de carte (portails), aller-retour 389 ↔ 390

Chantier successeur d'E12.d. Il livre la moitié non délivrée d'E10
(`docs/plan-conversion-totale.md:513-517`, « transitions de map ») : quitter une carte par un
portail, traverser le changement de monde du moteur, et arriver sur la carte suivante à la bonne
tuile, dans la bonne orientation, avec le fondu d'entrée déjà câblé par E10.a.

**Révision 4** — trois rondes de relecture indépendante en contexte neuf, une unité par relecteur.

| Unité | Statut |
|---|---|
| Enveloppe | **PRÊTE** (confirmée en ronde 3) |
| T0 — moteur | **PRÊTE** (confirmée en ronde 1) |
| T3 — détection | **PRÊTE** (confirmée en ronde 2) |
| T5 — arrivée | **PRÊTE** (confirmée en ronde 2) |
| T1 — état de session | corrigée en ronde 3, **correction non re-relue** |
| T2 — gel global | corrigée en ronde 3, **correction non re-relue** |
| T4 — départ | corrigée en ronde 3, **correction non re-relue** |

Vingt-neuf blocages levés au total, tracés en §1.7. Le plafond de relecture est atteint pour T1, T2
et T4 : leurs dernières corrections sont des rattachements d'oracle et des compléments de tableau,
tous prescrits mot pour mot par les relecteurs, mais **elles n'ont pas été soumises à une relecture
indépendante supplémentaire**. C'est signalé ici pour que l'approbation se fasse en connaissance de
cause.

**Priorité suivante non incluse ici** : E9 (backdrops dans le moteur) est la priorité utilisateur
juste après ce chantier ; son périmètre exact (migration du composant moteur `ScrollParameters`
contre les trois résidus seuls) reste à trancher avec l'utilisateur avant d'être planifié.

---

## 0. Cadre

### 0.1 Décisions utilisateur (2026-09-02, prises avant rédaction)

- **D-T-U1 — Acceptation en jeu = aller-retour 389 → 390 → 389.** Elle prouve d'un coup le fondu de
  départ, le fondu d'arrivée, la tuile et l'orientation d'arrivée, ET que l'intro du bateau ne
  rejoue pas au retour (donc que l'état de partie survit au changement de carte).
- **D-T-U2 — Déclencheurs en deux tranches.** Les portails d'abord (tranche nécessaire à
  l'acceptation) ; les opcodes `0x53` / `0x9B` / `0x9C` ensuite, dans une tranche approuvable et
  vérifiable séparément.
- **D-T-U3 — Fidélité : structure complète, seul l'effet 0 câblé.** Gel du monde au départ, fondu
  sortant 16 frames, canal son de départ, réinit d'entrée de carte, fondu entrant 16 frames, délai
  de warp 10 frames. Les effets 4/5/6/8 (glissements) sont ramenés à l'effet 0, déviation consignée.
- **D-T-U4 — Trois reports absorbés** : le gel global `MenuOpen` des entités (différé d'E12.d), le
  null-check moteur sur `AssetCatalog.GetByFileName`, et les caches session
  `SpriteRecordCatalog` / `AlundraSoundBank` (P4). **Non retenu** : l'action de cutscene
  `FadeScreen` inerte (E10 A4) reste différée.

### 0.2 Référence de suites AVANT chantier (mesurée)

| Suite | Résultat |
|---|---|
| `Alundra.Tests` | 711 / 711 vertes |
| `CasaEngine.Tests` | 1449 vertes, **18 échecs PRÉEXISTANTS** |

Les 18 échecs moteur ne viennent pas de ce dépôt de travail : ce sont des tests-gardes
d'architecture et de thème éditeur qui affirment sur des fichiers supprimés par des commits moteur
antérieurs (`878458a8`, `1ccc9238`) — `docs/casaengine-mgui-backend-extensibility.md` et
`CasaEngine.Editor/Game1.cs` ne sont plus suivis par git. Familles :
`CasaMguiBackendOwnershipTests` (5), `EditorControlTemplateAssetLoadingTests` (8), puis
`CutsceneDirectorTests`, `LightOverlayTests`, `EditorAssetWriterServiceTests`,
`MaterialDefinitionEditorRegistryTests`, `MonoGameBasicEffectUsageTests` (1 chacun).
**L'acceptation de la tranche moteur est donc « les 18 échecs connus inchangés, aucun nouveau »**,
jamais « suite verte ». Cette dette fait l'objet d'un chantier séparé, non ouvert ici.

### 0.3 Hors périmètre (explicite)

Effets de transition 1/3/4/5/6 et les cas spéciaux 8/9/10/0xB (fin de partie, menu principal, retour
au point de sauvegarde) ; portails même-carte 390 → 390 (`Flags 7` et `0x5007`) ; opcode `0x82`
(« Handle map trigger ») ; l'action de cutscene `FadeScreen` ; le rechargement d'assets VRAM par
carte ; la sauvegarde/restauration de partie.

---

## 1. Faits établis, vérifiés en session principale

Chaque fait ci-dessous a été relu directement dans le fichier cité, en session principale, pas
seulement rapporté par un agent. Les faits marqués **[R2]** ont été ajoutés ou corrigés à la
révision 2 après relecture indépendante.

### 1.1 Les données sont déjà exportées, complètes et exactes

- **§1.1.a** L'export des portails est **complet** : 3316 slots `DestMapId != 0` dans
  `data-extracted/data/map_*.json`, **3316 objets** dans la couche « Portals » des `.tileMap`,
  **zéro écart carte par carte** sur les 483 cartes, **zéro slot vivant** portant `X2 == 0xff` ou
  `Y2 == 0xff`. **Conséquence : aucun travail convertisseur dans ce chantier.**
- **§1.1.b** Chaque objet portail porte les 9 champs bruts plus son `Index`, en **valeurs chaînes** :
  `Index, X1, Y1, X2, Y2, DestMapId, DestTileX, DestTileY, ZLevel, Flags`
  (`TiledMapExporter.cs:411-420`). Le rectangle `x/y/width/height` de l'objet est un rectangle de
  RENDU décalé en élévation (`y = (Y1 - hauteurCellule) * 16`) : **le déclenchement doit lire les
  champs `X1..Y2`, jamais le rectangle**.
- **§1.1.c** Carte 389 : **4 portails**, tous vers la 390, tous `ZLevel 0`, tous `Flags 20481`
  (`0x5001` → RequiredFacing 1, ArrivalDirection 1, **TransitionEffect 0**, WarpBehavior 1). Les
  « 7 zones » évoquées ailleurs sont les 7 MapEvents de la carte.
  Carte 390 : 6 portails, dont les **4 retours réciproques** vers la 389 (`Flags 1` → RequiredFacing
  0, ArrivalDirection 0, TransitionEffect 0, WarpBehavior 1) et 2 portails même-carte hors périmètre.
- **§1.1.d** La paire d'acceptation est réciproque : portail 0 de la 389, `X1=X2=18, Y1=Y2=38`
  (**mono-cellule**), destination 390 tuile (10,40) ; le portail 0 de la 390 en (10,40) renvoie en
  389 (18,38).
- **§1.1.e** `Maps/world-index.json` associe l'id de carte au chemin de monde, et ces valeurs sont
  exactement les `file_name` d'`AssetInfos.json` (`WorldWriter.cs:501-527`) — donc directement
  consommables par `GameManager.SetWorldToLoad(string)`.
- **§1.1.f [R2] — mesures d'arrivée.** Les deux grilles font 52 × 60 (3120 cellules).
  Cellule d'arrivée sur la **390, tuile (10,40)** : `height = 4`, `walkability = 0`,
  `ground_property = 128`, `slope = 4`.
  Cellule d'arrivée au retour sur la **389, tuile (18,38)** : `height = 8`, `walkability = 0`,
  `ground_property = 128`, `slope = 4`.
  **Conséquence** : après `ClampToGround`, la pose d'arrivée sur la 390 vaut
  `PosZ = 4 * 16 << 16 = 4194304`, alors que le `ZLevel << 20` du portail vaut 0 — donc une mutation
  « sauter `ClampToGround` » est démontrablement non vide.

### 1.2 La séquence originale (relue dans la décompilation)

- **§1.2.a Déclenchement** (`PlayerManager.cs:3430-3485`). **[R2] Le site d'appel est
  `PlayerManager.cs:29`, c'est-à-dire AVANT le retour `BlockedByEntity` (`:31`) et AVANT la porte
  `InputBlockedMask` (`:38`)** — pas après.
  Si `CombinedVramFlagsAND & 0x4` (trou) → portail pris **inconditionnellement**, sans test
  d'orientation, sans touche et sans test de drapeaux de contrôle ;
  sinon exige `CombinedVramFlagsAND & 0x8000` (sol de portail), **`g_playerControlFlags == 0`**
  (test propre à la branche, à ne pas confondre avec la porte `InputBlockedMask` qui a un masque
  différent), un portail activé, la touche `SHORT_ARRAY_80022776[RequiredFacingDirection]` **tenue**,
  et **[R2]** `PlayerEntity.AnimationDirection == RequiredFacingDirection` — comparaison entre deux
  valeurs du **domaine 0..3**, à ne pas confondre avec `TargetDirection` dont le domaine est
  `{0x00, 0x08, 0x10, 0x18}`. Puis
  `HandleWarpTransition(portal, 0x36, g_cardinalDirectionTable[ArrivalDirectionIndex])`.
- **§1.2.b Recherche du portail** (`GameEngine.cs:2418-2438`) : balayage dans l'ordre des slots,
  test `X1 <= TileX <= X2 && Y1 <= TileY <= Y2`, **première correspondance gagne** ; une
  correspondance dont `DestMapId == 0` **renvoie null et bloque** au lieu de continuer le balayage.
- **§1.2.c Transfert** (`PlayerManager.cs:3488-3541`) : `g_isWarpDisabled != 0` → retour immédiat ;
  puis `g_mapTransitionEffectId = TransitionEffectId`,
  `g_desiredMap = MapIdToInternalMapIndexTable[DestMapId]`, et l'arrivée **préserve le décalage du
  joueur dans le rectangle source** :
  `deltaX = DestTileX*24 + (PosX>>16) - X1*24` ; `deltaY = DestTileY*16 + (PosY>>16) - Y1*16` ;
  `tileX = deltaX / 24` (la table `g_tileToWorldXTable` est une table de division : 52 blocs de 24
  entrées valant l'index du bloc, `GameInitializer.cs:304-319`) ; `deltaY /= 16` ;
  `cible = ((tileX*24 + 12) << 16, (deltaY*16 + 8) << 16, ZLevel << 20)`.
  `g_warpSoundEffectId = g_warpBehaviorTable[WarpBehaviorId]`.
  Effet 3 = téléportation sur place (ramené à 0 si la carte diffère) ; sinon `g_isGameEnding = 1`,
  `g_resetAnimationId = 0x36`, `g_resetDirectionId = directionId`, `g_cameraTarget* = cible`.
  **[R2]** `g_cardinalDirectionTable = { 0, 0x10, 0x08, 0x18 }` (`StaticVariables.cs:527`), table déjà
  portée à l'identique sous le nom `AnimationTables.CardinalDirectionTable`
  (`AnimationTables.cs:20`) : **l'index d'arrivée 1 des portails de la 389 donne donc `0x10`, pas 1.**
- **§1.2.d Fin de frame** : `HandleMapSoundEffects(g_desiredMap, g_warpSoundEffectId)` coupe les
  voix, bascule le BGM si la musique de destination diffère, puis joue le sfx ; `StartWarpTransition`
  capture la frame et arme l'effet — l'effet 0 arme `g_fadeStepFlags = 1` et un fondu soustractif
  de 16 frames.
  **[R2] Mesures sur le chemin d'acceptation** : `Maps/music-index.json` donne **index 25 pour la
  389 comme pour la 390** (aucune bascule BGM) ; le comportement de warp 1 des dix portails 389↔390
  vise le sfx **69** (`0x45`), dont `Sounds/sfx-manifest.json` dit `num_tones = 0`,
  `skip_reason = "no tones (NumTones=0)"` (**aucun échantillon jouable**). Le canal son de départ est
  donc structurellement porté et audiblement muet sur l'acceptation.
- **§1.2.e Frames de transition** (`GameEngine.cs:277-344`) : **[R2]** la frame de transition ne fait
  que pomper les pads, avancer l'effet et le son — **aucun update d'entités, aucun événement de
  carte**. C'est un **troisième mécanisme**, distinct des deux masques de drapeaux de contrôle : le
  jeu ne pose AUCUN drapeau de contrôle sur le chemin de warp (recherche exhaustive des écritures de
  `g_playerControlFlags` dans la décompilation : aucune sur ce chemin ; seul l'effet 10 remet le
  registre à zéro, `GameEngine.cs:331`). La boucle attend ensuite l'inactivité du son plus 2 frames.
  **[R3] L'original TERMINE explicitement son propre état de transition** : `_isWarpTransitionRunning`
  repasse à `false` en `GameEngine.cs:302`, juste avant `EndGame()`, donc **avant** la réinitialisation
  de carte et avant tout update d'entité de la carte d'arrivée. C'est le pendant obligatoire du gel,
  et il a un propriétaire nommé dans ce plan (T4).
- **§1.2.f Réinit de carte** : `ClearTemporaryFlags` (`GameEngine.cs:429-438`) vide les drapeaux
  temporaires ; `InitializeEntitySlots` vide tous les slots puis recrée le joueur à
  `g_cameraTarget*` avec `g_resetAnimationId` / `g_resetDirectionId` ; les entités ne sont peuplées
  que si leur zone d'activation contient la tuile d'arrivée ; `WarpPlayer` arme le fondu entrant et
  pose `g_warpDelayFrames = 10`.
  **[R2]** Les **seuls** consommateurs de `g_warpDelayFrames` sont la combinaison Start+Select
  (`GameEngine.cs:1523-1528`) et l'ouverture d'inventaire (`GameEngine.cs:1567-1574`) — **ni l'une ni
  l'autre n'est portée** : notre port n'a aucun chemin d'inventaire par bouton (`MenuOpen` n'est posé
  que par le directeur de dialogue, `AlundraDialogueDirector.cs:197-198`).
- **§1.2.g Opcodes** : `0x53` (taille 8) lit carte, tuile x/y/z, effet et sfx dans ses octets, sans
  portail ; `0x9B` / `0x9C` (taille 1) posent et lèvent `g_isWarpDisabled`.

### 1.3 Le moteur sait déjà changer de monde

- **§1.3.a** `GameManager.SetWorldToLoad(string)` (`GameManager.cs:119-122`) mémorise le nom et
  **diffère** : la bascule a lieu en tête du `UpdateWorld` suivant.
- **§1.3.b** Ordre de la frame de bascule (`GameManager.cs:73-116`, relu) : `Clear()` de l'ancien
  monde → chargement du nouveau (`GetByFileName` puis `Load(..., cache: false)`) → `ViewManager.Clear()`
  → `LoadContent` (qui exécute `InitializeWithWorld`) → `BootstrapViews` → `SyncPlayerViewAssignments`
  → `BeginPlay` → événements → **`Update` du nouveau monde dans la même frame**. L'ancien monde ne
  reçoit **pas** d'`Update` final.
- **§1.3.c** Ce qui survit : `AudioSystemComponent`, `ScreenEffectComponent`, `InputComponent`,
  `AssetContentManager`, `GameManager`, `ViewManager` — tous portés par le `Game`, pas par le monde.
  Ce qui meurt : les entités, la vue UI (donc l'écran de dialogue), le `PhysicsWorld`, les voix SFX
  possédées par le monde (`StopVoicesOwnedBy(world)`).
- **§1.3.d** `AssetCatalog.GetByFileName` renvoie **null** sur nom inconnu, et `GameManager.cs:83`
  déréférence sans contrôle → `NullReferenceException` opaque. C'est la cible du report absorbé.
- **§1.3.e** Chemin d'accès depuis la DLL : `world.Game.GameManager`, déjà utilisé ailleurs. **Aucune
  API moteur nouvelle n'est nécessaire pour demander le changement de monde.**
- **§1.3.f [R2]** Le `ScreenEffectService` retient son état jusqu'à `Clear()`
  (`ScreenEffectService.cs:50-66`) et vit sur le `Game` (`ScreenEffectComponent.cs:38,45`) : il
  survit donc à la bascule, et le nouveau monde repousse dès sa première frame
  (`AlundraWorldProxy.cs:1350-1351`). **Aucun flash n'est possible pendant la bascule.**

### 1.4 État de la DLL aujourd'hui

- **§1.4.a** La couche « Portals » est lue à l'initialisation puis **seulement comptée dans un log**
  (`AlundraWorldProxy.cs:511-518`). Aucune vérification par tick n'existe.
- **§1.4.b** `0x53`, `0x82`, `0x9B`, `0x9C` n'ont aucun `case` : ils tombent dans `UnknownOpcode`,
  qui saute par la table des tailles et journalise un avertissement par valeur d'opcode.
- **§1.4.c** `AlundraGameState` est **par monde** (initialiseur de champ, `AlundraWorldProxy.cs:181`)
  : au changement de carte, drapeaux de partie, drapeaux de contrôle et verrou d'interaction seraient
  perdus, et l'intro du bateau (gardée par le drapeau 860) **rejouerait**.
- **§1.4.d** Trois singletons de session existent déjà avec un contrat commun — `AttachToWorld`
  re-pointe sans toucher l'état, `InstallForMapEntry` applique le préambule d'entrée de carte,
  `ResetForTests` isole les tests : `AlundraMusicPlayer`, `AlundraScreenFadeDirector`,
  `AlundraDialogueDirector`. **[R2]** `AlundraMusicPlayer` n'a PAS d'`InstallForMapEntry` : sa
  musique d'entrée est déclenchée depuis `InstallAudioSystems`. Le test T7 de
  `AlundraScreenFadeDirectorTests` (`:370-412`) est le seul précédent d'un montage à deux mondes
  consécutifs, et **[R2]** le vrai coût d'un singleton de session est visible dans ce même fichier
  (`:23-43`) : collection xunit dédiée plus `ResetForTests` en constructeur ET en `Dispose`.
- **§1.4.e** `AdoptPlayerPawn` (`AlundraWorldProxy.cs:992-1171`) écrase la pose posée par le
  `PlayerStart` du moteur avec les constantes New Game (`CameraTileX/Y = 33/59`,
  `ResetAnimationId = 0x36`, `ResetDirectionId = 0`), puis `ClampToGround`, puis réécrit la
  transformation racine. Il tourne **avant** la boucle de peuplement des entités (`:531` contre
  `:544`), dont la porte de zone lit la tuile du joueur — même ordre que l'original.
  **[R2]** `ClampToGround` ne fait que **monter** `PosZ` (`AlundraEntityScriptProxy.cs:1192-1196`) :
  il est donc compatible avec un `ZLevel << 20` plus haut et ne peut pas l'écraser.
  **[R2]** Le suivi caméra est déjà rebranché à chaque installation de monde
  (`ArmFirstFrameSnap` en `:456`, `EntityFollowedByCamera` en `:1170`, après l'écriture de la pose).
- **§1.4.f** Le directeur de fondu expose déjà tout ce qu'un fondu de départ demande :
  `BeginFadeEffect(r, g, b, tpage, duration, persistLock)`, `IsSettled` (`:338`), et une garde de
  dessin `if (!_fadeGateActiveEnteringPush && _persistLock == 0) { _service.Clear(); return; }`
  (`:354`). Un `persistLock` non nul **maintient la couleur poussée indéfiniment**. Le fondu
  d'arrivée va de `0xff0000` vers `0` en soustractif (`:163-172`), donc un départ part de `0` vers
  `0xff0000`. `BeginFadeEffect` pose `_fadeActive = true` **avant** le calcul de pente, donc la
  machine divise et n'escamote pas (`:185-187`). `InstallForMapEntry` remet `_persistLock = 0`
  (`:159`). **Aucune modification du directeur de fondu n'est nécessaire.**
- **§1.4.g [R2]** L'effet 0 est codé en dur dans `InstallForMapEntry` (`:163-172`), et le point
  d'injection d'un id d'effet est `AlundraWorldProxy.InstallScreenFadeSystems` (`:800-804`), appelé
  en `:507`, donc **avant** `AdoptPlayerPawn` (`:531`).

### 1.5 Le gel global : trois mécanismes distincts, ne pas les confondre

- **Gel global de jouabilité** — `EntityManager.UpdateEntities` (`EntityManager.cs:377`) : quand
  `g_playerControlFlags & GameplayBlockedMask` est non nul, seul `UpdateEntityLists()` tourne ;
  sont sautés les entités détruites, les événements d'entité (donc `MovePlayer` et les programmes),
  les compteurs, l'animation, la physique, les effets, l'équilibrage. **[R2] Restent DEHORS de la
  porte, après le `else` (`EntityManager.cs:394-408`) : le tri en profondeur des entités visibles et
  la publication des sprites.** C'est **cela** le report absorbé, et il se déclenche sur
  `MenuOpen | Unused40` (`0x48`).
- **Blocage par entité** — `BlockedByEntity`, posé par `BlockEntitiesBy`
  (`EntityManager.cs:1315-1343`) et testé à `:815` : mécanisme séparé, **déjà porté**
  (`AlundraEntityScriptProxy.cs:1008`, `AlundraPlayerManager.cs:165`).
- **[R2] Boucle de transition de warp** — `AdvanceWarpTransitionFrame` (`GameEngine.cs:277-344`) :
  court-circuite tout le pipeline, **sans poser aucun drapeau de contrôle** (§1.2.e). C'est le
  mécanisme du gel de DÉPART, et il n'est ni l'un ni l'autre des deux précédents.

**[R2] Fait décisif** : `ForcedSequence` (`0x20`) appartient à `InputBlockedMask` (`0x34`) et **PAS**
à `GameplayBlockedMask` (`0x48`) — `AlundraGameState.cs:67,78,82`. Poser `ForcedSequence` ne ferme
donc **pas** la porte du gel global.

**[R2] Fait rassurant** : les boîtes de dialogue à monde fermé posent bien `MenuOpen`
(`AlundraDialogueDirector.cs:197-198`, mode de contrôle 0), qui **est** dans `GameplayBlockedMask`.
Le gel de T2 corrige donc réellement le défaut signalé par l'utilisateur.

**[R3] Partage EXHAUSTIF des passes de notre port**, de part et d'autre de la porte. La révision 2
proposait une liste partielle qui plaçait `SyncAnimation` du mauvais côté et omettait les passes qui
déplacent réellement les PNJ ; le tableau ci-dessous couvre **littéralement chaque appel** des deux
boucles, chacun une seule fois, d'un seul côté, avec la ligne de l'original qui le justifie.

`AlundraWorldProxy.Update` (`:1249-1396`) :

| Passe | Côté | Justification |
|---|---|---|
| `LogicTicksThisFrame` (`:1249`) | dehors | lecture mémo ; alimente le compteur de ticks lui-même |
| `TryWireDialoguePresenterOnce` (`:1253`) | dehors | câblage d'UI sans équivalent original ; une boîte doit pouvoir s'afficher pendant le gel |
| `RunMapEventsPass` (`:1265-1268`) | **dedans** | `RunMapEvents`, gardé par le même masque dans l'original (`GameEngine.cs:1667-1671`). **La porte doit le couvrir indépendamment de sa garde interne actuelle** (`:1409`), qui ne réagit qu'aux drapeaux de contrôle |
| `FlushPendingOverlayReconstruction` (`:1276`) | dehors | coalescence visuelle de mutations déjà appliquées ; no-op si rien n'est en attente |
| `RefreshUpdateProxiesAndCollidables` (`:1280`) | dehors | port d'`UpdateEntityLists`, que l'original exécute dans la branche `else` (`EntityManager.cs:391`) |
| `RunPendingEventTriggers` (`:1288-1291`) | **dedans** | seconde boucle d'`UpdateEntitiesEvents` (`EntityManager.cs:380`) |
| `RunWallInterleaveSortKeyPass` (`:1300`) | dehors | tri de profondeur, équivalent d'`UpdateVisibleEntitiesZSort` (`EntityManager.cs:394`) |
| `ResolveDebugCameraOnce` (`:1325`) | dehors | débogage |
| `UpdateCameraFollow` (`:1326-1330`) | dehors | le look-at de l'original est la dernière chose que fait `UpdateEntities`, après le `else` |
| `UpdateDebugCameraPan` (`:1331`) | dehors | débogage ; échantillonne le stick |
| `ApplyOriginalBackgroundClearColorOnce` (`:1340`) | dehors | rendu |
| `UpdateAndDrawBackdrop` (`:1341`) | dehors | rendu |
| `ScreenFadeDirector.Advance` + `PushToAttachedService` (`:1350-1351`) | dehors | les fondus doivent tourner pendant le gel — c'est le fondu de départ lui-même |
| `AlundraDialogueDirector.Tick` (`:1361-1364`) | dehors | l'avance de boîte est précisément ce que le gel ne doit pas arrêter |
| sonde de contact (`:1375-1383`) | **dedans** | déjà gardée par `GameplayBlockedMask` |
| `_logicClock.CloseFrame` (`:1391`) | dehors | comptabilité de frame |
| `_firstFrameStillOpen = false` (`:1396`) | dehors | comptabilité de frame |

`AlundraEntityScriptProxy.Update` (`:821-995`) :

| Passe | Côté | Justification |
|---|---|---|
| rapatriement de la pose depuis le root + `IsOnGround` (`:857-868`) | **dedans** | pont de pose physique ; l'original gèle la physique (`EntityManager.cs:385`) |
| branche PNJ : `PickEventTrigger`, `RunPickedEvent`, `AlundraScriptedMotion.TickScriptedNpc`, `EvaluateEntitySupport` (`:895-945`) | **dedans** | `UpdateEntitiesEvents` puis `UpdateEntitiesPhysics` (`EntityManager.cs:380,385`). **Ce sont ces passes qui déplacent réellement les PNJ** : les omettre laisserait une marche déjà lancée continuer |
| **[R7] publication de `LastPadState`** | **dehors** | **CORRIGÉ après régression en jeu.** C'est le port de `g_padState1`, rafraîchi par `PadManager.UpdatePads` depuis la boucle PRINCIPALE (`GameEngine.cs:1518`) et même depuis la boucle de transition de warp (`GameEngine.cs:280`) — jamais depuis `UpdateEntities`. L'original ne gèle donc JAMAIS sa manette. Le placer dedans a gelé l'entrée que la boîte de dialogue consomme pour avancer et se fermer, laissant `MenuOpen` posé à jamais : blocage complet observé en jeu |
| branche joueur : `MovePlayer`, `AlundraPlayerManager.Tick`, `UpdateGroundSlope`, `UpdateFloorHeight` (`:961-989`) | **dedans** | idem |
| `AlundraFrameSyncPasses.SyncAnimation` (`:993`) | **dedans** | **[R3] corrigé** — port d'`UpdateAnimation`, appelé par `UpdateEntitiesAnimation` **à l'intérieur** du `if` (`EntityManager.cs:384`). La révision 2 le plaçait dehors, ce qui aurait fait commuter d'animation, pendant le gel, une entité entrée avec `TargetAnimationId != CurrentAnimationId` |
| `AlundraFrameSyncPasses.SyncTransform` (`:994`) | dehors | publication de rendu, équivalent d'`EntityManager.cs:394-408` |

### 1.6 Les drapeaux de tuile : champ déclaré, jamais calculé

- **§1.6.a** `CombinedVramFlagsAND` et `CombinedVramFlagsOR` existent sur le proxy d'entité
  (`AlundraEntityScriptProxy.cs:200-201`) mais **ne sont écrits nulle part** : seul `Clone` les
  recopie. Ils restent à zéro. **[R2] Attention, `CombinedVramFlagsOR` a un consommateur VIVANT** :
  la transition `DestroyOnVramFlags` le teste avec le masque `0x8004`
  (`AlundraEntityScriptProxy.cs:1041`) — exactement les bits trou et sol de portail. Alimenter ce
  champ pour toutes les entités changerait donc un comportement existant.
- **§1.6.b** L'original les calcule dans `PhysicsEngine.cs:1740-1768`, branche gravité : pour chacun
  des 4 coins, le coin ne contribue que si `MapHeights[i] + 1 == ModdedPosZ`, sinon il contribue 0 ;
  puis OR et AND des quatre contributions. **Un seul coin disqualifié annule le AND.**
- **§1.6.c** **Déviation obligatoire, déjà établie et documentée dans ce port** : notre invariant de
  repos est `ModdedPosZ == TerrainHeight`, **sans le `+1`** de l'original
  (`AlundraEntityScriptProxy.cs:1255-1272`, vérifié sur les quatre traces héros de référence).
  Porter littéralement le `+1` rendrait la qualification **définitivement insatisfaisable**. La
  nouvelle sonde doit donc utiliser la même règle que `UpdateGroundSlope`, qui porte déjà le sens de
  la règle contre notre invariant.
- **§1.6.d** La donnée source est exportée et atteignable : `AlundraCells` porte `walkability` et
  `ground_property` par cellule, et le port reconstitue `MapTile.Flags` par
  `walkability | (groundProperty << 8)` (`AlundraCellsCollisionField.cs:295`). Donc le bit de sol de
  portail `0x8000` vaut `groundProperty & 0x80`, et le bit de trou `0x4` vaut `walkability & 0x4`.
- **§1.6.e** Vérifié sur la donnée : les 4 tuiles de portail de la 389 portent `ground_property = 128`
  (`0x80`) et `flags` avec le bit `0x8000` posé (tuile (18,38) → `0x08048000`) ; les tuiles voisines
  et la tuile de départ New Game (33,59) ne l'ont pas.
- **§1.6.f [R2] CORRIGÉ — l'accesseur existe déjà.**
  `AlundraCellsCollisionField.SampleGroundProperty(in Vector3)` (`:314`) **et**
  `AlundraCellsCollisionField.SampleRawWalkability(in Vector3)` (`:338`, livré par E7.a, déjà
  référencé par `AlundraCellStore.cs:156`) sont tous deux présents. **Rien à ajouter sur ce type.**

### 1.7 Corrections tracées

Consignées pour que personne ne les rejoue.

**Révision 1 (contre les rapports de reconnaissance)** : l'écart de comptage des portails était faux ;
le gel global n'est pas `BlockedByEntity` ; les effets 8 et 9 étaient inversés (l'effet 8 appelle
`InitializeMapChangeWarp`, l'effet 9 partage `InitStandardWarpEffect` avec l'effet 0) ;
`ResetWarpLockTimer` ne touche pas `g_warpDelayFrames` ; la garde de dessin du directeur de fondu
porte une négation ; les comptages d'opcodes par recherche textuelle dans le JSON ne sont pas
fiables et devront être obtenus en analysant les tableaux `Codes`.

**Révision 2 (contre la relecture indépendante du plan lui-même)** — quinze blocages levés :
`ForcedSequence` ne ferme pas la porte du gel global (D-T-6 refondue, §1.5) ; le site d'appel du
contrôle de warp est avant les deux portes, pas après (§1.2.a) ; `SampleRawWalkability` existait déjà
(§1.6.f) ; `CombinedVramFlagsOR` a un consommateur vivant (§1.6.a) ; `AnimationDirection` est du
domaine 0..3 (§1.2.a) ; la direction d'arrivée vaut `0x10`, pas 1 (§1.2.c) ; la cellule d'arrivée
n'est pas à hauteur 0 (§1.1.f) ; `ActiveCollisionEntity` ne vit pas sur l'état de partie (D-T-13) ;
le verrou d'interaction retient une référence forte vers le monde détruit (D-T-13) ; l'état de
session casse l'isolation des suites existantes (D-T-14) ; le délai de warp n'a aucun consommateur
porté (§1.2.f) ; le suivi caméra est déjà rebranché (§1.4.e) ; la liste du hors-gel était incomplète
(§1.5) ; la déclaration de dépendances entre tranches se contredisait (§3) ; T2 et T5 n'avaient pas
d'oracle rejouable.

**Révision 3 (ronde de clôture)** — neuf blocages de plus. Le corollaire de D-T-6 était faux : ne
poser aucun drapeau de contrôle est fidèle, mais la porte de gel vit sur un singleton de session et
**doit être levée** à l'entrée de la carte d'arrivée, exactement comme l'original le fait en
`GameEngine.cs:302` ; le risque est neutralisé, pas dissous, et T4 en est désormais propriétaire
(D-T-15). `SyncAnimation` était du mauvais côté de la porte (§1.5). Le tableau des passes était
partiel et omettait celles qui déplacent réellement les PNJ (§1.5, désormais exhaustif sur les deux
boucles). L'oracle de T2 n'était pas réalisable : le montage headless ne fait varier ni le joueur, ni
la caméra, ni le rendu, et reproduit `UpdateEntities` au lieu d'appeler le site de production — T2 a
maintenant deux oracles séparés. La clause « ordre de classes randomisé » n'était pas exécutable
(xunit 2.9.3, aucun ordonnanceur, collections déjà sérialisées) : remplacée par une vérification par
`--filter` classe par classe. D-T-14 ne couvrait que trois classes sur dix et son motif (parallélisme)
était périmé. L'acceptation de T1 ne couvrait que la moitié du tableau D-T-13. Le directeur de warp
n'était couvert par aucun câblage d'isolation.

**Révision 4 (post-plafond, corrections prescrites non re-relues)** — cinq blocages de plus, tous de
même nature : des acceptations qui nommaient un oracle incapable de les réfuter.
xunit instancie une classe de test neuve par méthode, donc la remise à zéro en constructeur porte
seule l'isolation et une mutation sur le seul `Dispose` était infalsifiable (D-T-14, T1).
L'acceptation de dégel de T4 exigeait un déplacement du joueur qu'aucun montage à deux mondes ne
produit, et une observation dans `AdoptPlayerPawn`, méthode privée derrière des retours anticipés
qu'aucun test ne franchit : elle est reformulée sur des états lisibles directement sur le directeur.
D-T-15 ne disposait que trois des six états du directeur — la **séquence de départ** manquante aurait
reposé la porte de gel à la frame suivant sa levée. Enfin deux mutations de T2 visaient le mauvais
oracle : l'avance de boîte n'est pas appelée depuis le site gardé dans le montage headless, et
`SyncTransform` n'est pas atteint par la boucle du proxy de monde.

---

## 2. Décisions de conception

- **D-T-1 — Aucun travail convertisseur.** §1.1 le prouve par la mesure. Toute tranche qui toucherait
  au convertisseur est hors périmètre et doit être remontée avant d'être écrite.
- **D-T-2 — Un directeur de transition, singleton de session, contrat identique aux existants.**
  `AlundraWarpDirector.Instance`, avec `AttachToWorld`, `InstallForMapEntry`, `ResetForTests`. Il
  détient la demande de départ, la séquence de départ, la porte de gel, la demande de changement de
  monde et l'enregistrement d'arrivée que le proxy suivant consomme. Raison : la séquence **enjambe**
  le changement de monde, donc elle ne peut pas vivre sur un objet reconstruit par monde.
  **[R3]** Étant de session, il porte les mêmes obligations que les trois directeurs existants : une
  disposition d'entrée de carte explicite (**D-T-15**) et le câblage d'isolation des tests
  (**D-T-14**).
- **D-T-3 — `AlundraGameState` devient de session, sans changer sa forme.** Le champ du proxy devient
  une référence obtenue du porteur de session ; la classe gagne un `InstallForMapEntry` et un
  `ResetForTests`. La disposition champ par champ est fixée par D-T-13.
- **D-T-4 — La pose d'arrivée passe par un enregistrement en attente, les constantes New Game en
  restant la valeur par défaut.** `AdoptPlayerPawn` consomme cet enregistrement (position 16.16,
  animation, direction) au lieu de lire les constantes. `ClampToGround` continue de tourner.
- **D-T-5 — Le fondu de départ réutilise `BeginFadeEffect` avec un verrou de persistance.**
  Départ = `BeginFadeEffect(0xff, 0xff, 0xff, tpage: 2, duration: 16, persistLock: 1)`. Le verrou
  maintient le noir poussé après stabilisation, y compris pendant la frame de bascule ;
  `InstallForMapEntry` du monde d'arrivée remet le verrou à zéro et arme le fondu entrant. Validé
  ligne à ligne en §1.4.f — **aucune modification du directeur de fondu**.
- **D-T-6 [R2, REFONDUE] — Le gel de départ est une porte PROPRE au directeur de warp, et n'est pas
  un drapeau de contrôle.** La révision 1 proposait de poser `ForcedSequence` et de s'appuyer sur le
  gel de T2 : c'était faux, ce bit n'appartient pas au masque que T2 teste (§1.5). La correction
  retenue est **plus fidèle que le minimum prescrit par les relecteurs** (qui suggéraient de choisir
  un bit du bon masque) : l'original ne pose **aucun** drapeau de contrôle sur le chemin de warp, il
  court-circuite le pipeline par une boucle dédiée (§1.2.e). Le directeur expose donc un prédicat
  `IsTransitionInProgress`, et le proxy garde les mêmes passes que la porte de T2, au même site.
  **[R3] Corollaire CORRIGÉ.** La révision 2 concluait « aucun drapeau posé, donc rien à lever » :
  c'était faux. Aucun **drapeau de contrôle** n'est posé, mais le prédicat vit sur un singleton de
  **session** qui survit à la bascule de monde ; laissé à vrai, il figerait joueur et PNJ pour
  toujours sur la carte d'arrivée, puisque celle-ci reçoit son `Update` dans la frame même de la
  bascule (§1.3.b). Le risque est donc **neutralisé par une levée explicite, pas dissous** :
  l'original fait exactement cela en `GameEngine.cs:302` (§1.2.e). **Propriétaire : T4**, qui doit
  livrer le contrat de session complet du directeur, avec la disposition d'entrée de carte du
  tableau D-T-15.
- **D-T-7 — Effets ≠ 0 ramenés à l'effet 0.** L'id d'effet est lu, transporté et journalisé, mais
  tout id non nul est traité comme 0. Déviation assumée (D-T-U3), sans conséquence sur le chemin
  d'acceptation où tous les portails portent l'effet 0.
- **D-T-8 — Le canal son de départ est porté dans sa structure, silencieux sur le chemin
  d'acceptation.** Mesuré en §1.2.d : même index de musique des deux côtés, et sfx de départ sans
  échantillon jouable. **Aucune régression audio possible sur l'acceptation** — c'est une mesure, pas
  une prédiction.
- **D-T-9 — Pas d'attente sur le son.** L'attente « son inactif + 2 frames » n'est pas portée : notre
  `AudioService` n'expose pas l'état d'inactivité que la condition teste, et sur le chemin
  d'acceptation aucune voix n'est en cours. Déviation consignée.
- **D-T-10 — La sonde de drapeaux de tuile porte le SENS de la règle, pas sa lettre.** Conformément à
  §1.6.c, la qualification d'un coin est `MapHeights[i] == ModdedPosZ`. Le commentaire de la nouvelle
  méthode doit citer §1.6.c et le bloc de doc existant, pour qu'une relecture future ne « corrige »
  pas la déviation en réintroduisant le `+1`.
- **D-T-11 — Le null-check moteur lève une exception explicite.** `GameManager.UpdateWorld` doit
  produire une `InvalidOperationException` nommant le chemin de monde introuvable, dans la forme déjà
  employée par `EndLoadContent` pour `FirstWorldLoaded`. Changement strictement additif.
- **D-T-12 [R2] — La sonde de drapeaux de tuile est réservée au JOUEUR.** Même restriction, même
  motif et même forme de documentation que `Slope_18c` (`AlundraEntityScriptProxy.cs:1281-1287`) :
  alimenter `CombinedVramFlagsOR` pour toutes les entités réveillerait la transition
  `DestroyOnVramFlags` (§1.6.a) et détruirait des PNJ posés sur des tuiles de portail. Le
  comportement des PNJ doit rester **strictement inchangé**, et l'acceptation de T3 doit le prouver.
- **D-T-13 [R2] — Disposition champ par champ d'`AlundraGameState`.** La liste est exhaustive ;
  aucune autre donnée d'instance n'existe sur ce type.

  | Champ | Disposition à l'entrée de carte | Raison |
  |---|---|---|
  | `GameFlags[1024]` | **conservé** | données de sauvegarde ; c'est ce qui empêche l'intro de rejouer |
  | `MapIdToInternalMapIndexTable[500]` | **conservé** | données de sauvegarde, écrites par l'opcode 0x38 |
  | `TemporaryFlags[1024]` | **vidé** | port de `ClearTemporaryFlags` (`GameEngine.cs:429-438`) |
  | `PlayerControlFlags` | **conservé** | l'original ne le remet pas à zéro au warp (§1.2.e) ; les bits périmés `MessageBox`/`MenuOpen` sont déjà nettoyés par `AlundraDialogueDirector.InstallForMapEntry` |
  | `LastPadState` | **conservé** | l'original est un global rafraîchi chaque frame ; conserver reproduit exactement ce que lirait un opcode 0x2F déclenché avant le premier update joueur de la carte d'arrivée |
  | `InteractLatchEntity` | **vidé** | référence FORTE vers un proxy du monde détruit : la conserver retiendrait tout le graphe du monde mort et pourrait faire courir une interaction sur une entité d'un monde nettoyé. L'original stocke un pointeur dans une table de slots que `InitializeEntitySlots` réinitialise à chaque entrée de carte : vider est donc **plus** fidèle que conserver |
  | 8 champs numériques du verrou (`InteractLatchFacing`, `…EntityX/Y/Z`, `…PlayerX/Y/Z`, `…Direction`) | **conservés** | valeurs pures, auto-invalidées par les huit tests d'égalité, exactement comme l'original |
  | **[R8]** `IsWarpDisabled` (ajouté par T3) | **remis à `false`** | port de `g_isWarpDisabled`, que l'original remet à zéro dans `InitializeEntitySlots` à chaque entrée de carte. Posé/levé par les opcodes `0x9B`/`0x9C` (T7), déjà respecté par le prédicat de T3 |

  **`ActiveCollisionEntity` ne figure pas dans ce tableau** : il ne vit pas sur `AlundraGameState`
  mais sur le proxy de monde (`AlundraWorldProxy.cs:239`) et sur l'interface d'hôte
  (`IAlundraScriptHost.cs:29`). Voir T1 pour son traitement.
- **D-T-14 [R3, ÉLARGIE ET CORRIGÉE] — Tout état de session introduit par ce chantier impose un
  câblage d'isolation des tests.** Faire d'un objet par monde un état de processus rend
  `Alundra.Tests` sensible à l'ordre.
  **Correction du motif** : la révision 2 invoquait « collection xunit dédiée » pour éviter le
  parallélisme — c'est périmé, `Alundra.Tests/xunit.runner.json` pose déjà
  `parallelizeTestCollections: false`.
  **[R4] L'élément porteur est la remise à zéro en CONSTRUCTEUR.** xunit instancie une classe de test
  neuve par méthode, donc le constructeur s'exécute avant chaque test : c'est lui qui garantit
  l'isolation. La remise à zéro en `Dispose` est conservée par symétrie avec le modèle
  `AlundraScreenFadeDirectorTests` (`:23-43`), mais elle est **redondante dès lors que le
  constructeur la porte partout** : elle est donc déclarée **hygiène, non couverte par
  l'acceptation**, sur le modèle déjà employé par D-T-8, D-T-9 et le point 4 de T1. La révision 3
  revendiquait à tort une mutation sur elle seule ; cette mutation était infalsifiable.
  **Portée** : les **quatre** porteurs de session introduits ou élargis par ce chantier —
  `AlundraGameState`, `SpriteRecordCatalog`, `AlundraSoundBank` (T1) et `AlundraWarpDirector` (T4).
  **Critère opérationnel** : *toute classe de test qui construit un `AlundraWorldProxy`*. Elles sont
  dix aujourd'hui : `AlundraCellVisualSyncTests`, `AlundraDialogueFramePassTests`,
  `AlundraDialoguePresenterWiringTests`, `AlundraInteractionPassTests`, `AlundraMusicPlayerTests`,
  `AlundraScreenFadeDirectorTests`, `AlundraScreenFadeCameraWiringTests`,
  `AlundraWorldProxyAudioInstallationTests`, `AlundraWorldProxyEntityManipulationTests`,
  `AlundraWorldProxyUpdateCharacterizationTests`. Une recherche de `new AlundraWorldProxy` ne doit
  laisser aucune classe non câblée.
  **[R3] Vérification EXÉCUTABLE, sans infrastructure nouvelle.** La révision 2 exigeait « une
  exécution à ordre de classes randomisé » : le harnais ne sait pas le faire (xunit 2.9.3, aucun
  ordonnanceur enregistré, collections sérialisées — donc l'ordre est déterministe et deux exécutions
  consécutives sont identiques). La clause devient :
  1. **chaque** classe câblée passe seule, via
     `dotnet test Alundra.Tests/Alundra.Tests.csproj --filter "FullyQualifiedName~<Classe>"` — ce qui
     prouve qu'aucune ne dépend d'un état laissé par une autre ;
  2. la suite complète passe.
  **[R4] Mutation falsifiable** : retirer la remise à zéro **entière** (constructeur ET `Dispose`)
  d'une classe câblée nommée fait tomber un test nommé et reproductible dans l'exécution complète,
  l'ordre étant déterministe. Aucune mutation ne porte sur le seul `Dispose`, qui est hygiène.
- **D-T-15 [R3] — Disposition d'entrée de carte du directeur de warp.** Même forme que D-T-13, et
  même motif : le directeur est de session, donc son état traverse la bascule.

  | État | Disposition à l'entrée de carte | Raison |
  |---|---|---|
  | `IsTransitionInProgress` (la porte de gel) | **remis à faux** | port de `_isWarpTransitionRunning = false` (`GameEngine.cs:302`), que l'original exécute avant la réinitialisation de carte. Sans cela, joueur et PNJ restent figés sur la carte d'arrivée |
  | enregistrement d'arrivée (position, animation, direction, id d'effet) | **CONSERVÉ** | il est consommé **après** les installations : `InstallScreenFadeSystems` et consorts tournent en `AlundraWorldProxy.cs:505-508`, `AdoptPlayerPawn` en `:531`. L'effacer à l'installation le détruirait avant son unique lecteur |
  | id d'effet de transition | **conservé jusqu'à consommation** | même raison ; lu par `InstallScreenFadeSystems` (T5) |
  | **[R4]** demande de départ (le latch posé par la détection T3) | **remis à zéro** | sinon un départ serait ré-armé dès la carte d'arrivée, sur la tuile de portail réciproque où le joueur atterrit précisément (§1.1.f : les deux cellules d'arrivée portent `ground_property = 128`) |
  | **[R4]** séquence de départ (compteur du fondu sortant, étape courante) | **remise à zéro** | une séquence encore en cours **repose la porte à la frame suivante** et annule la levée ci-dessus — c'est le trou que ce tableau prétend fermer |
  | **[R4]** demande de changement de monde (chemin en attente) | **remise à zéro** | sinon un second `SetWorldToLoad` pourrait être ré-émis depuis la carte d'arrivée |

  **[R4] Clause d'exhaustivité** : ce tableau couvre les **six** états que D-T-2 attribue au
  directeur. Tout état supplémentaire ajouté à l'exécution doit y recevoir sa ligne.

  **Contrainte d'ordonnancement** : la levée de la porte et la conservation de l'enregistrement
  doivent cohabiter dans le **même** `InstallForMapEntry`. C'est le point que la mutation de T4 doit
  éprouver.

---

## 3. Découpage en tranches

**[R2] Prérequis explicites** (la révision 1 se contredisait sur ce point) :

| Tranche | Prérequis |
|---|---|
| T0 (moteur) | aucun — parallélisable avec tout le reste |
| T1 (état de session) | aucun |
| T2 (gel global) | aucun — mais **touche le même site** que T4, donc jamais en parallèle d'elle |
| T3 (détection) | aucun sur le plan technique ; ordonnancé après T1 pour éviter un conflit de propriété sur `AlundraGameState.cs` |
| T4 (départ) | **T2** (le site de porte qu'elle réutilise) et **T3** (le déclencheur) |
| T5 (arrivée) | **T1** (l'état de session) et **T4** (l'enregistrement d'arrivée) |
| T6 (intégration) | T0 à T5 |
| T7 (opcodes) | T4 ; approuvable et livrable séparément, après T6 |

Seules **T0 et T1** peuvent réellement partir en parallèle : elles ne partagent aucun fichier. Toutes
les autres se suivent.

### T0 — Moteur : demande de changement de monde sûre *(sous-module)*

**Contenu** : le null-check de D-T-11 dans `GameManager.UpdateWorld`, plus un test moteur.
**Acceptation** : chemin de monde inconnu → `InvalidOperationException` nommant le chemin ; chemin
connu → comportement inchangé. **Les 18 échecs moteur connus inchangés, aucun nouveau.**
**Livrable** : commit sous-module + bump de pointeur dans le dépôt parent.
**Mutation** : retirer le contrôle → le test tombe sur `NullReferenceException`.
*(Déclarée prête telle quelle par sa relecture indépendante.)*

### T1 — État de partie en session

**Contenu** :
1. `AlundraGameState` devient de session (D-T-3), avec `InstallForMapEntry` et `ResetForTests`, et la
   disposition champ par champ **exactement** telle que D-T-13 la fixe.
2. **[R3]** Le câblage d'isolation de D-T-14 sur **les dix classes qui construisent un
   `AlundraWorldProxy`**, nommées dans D-T-14 — pas seulement trois. La remise à zéro couvre les
   **trois** porteurs de session livrés par cette tranche (`AlundraGameState`, `SpriteRecordCatalog`,
   `AlundraSoundBank`), en constructeur ET en `Dispose`.
3. Les caches `SpriteRecordCatalog` et `AlundraSoundBank` passent en session, avec la même remise à
   zéro pour tests et une clé par chemin de projet.
4. **[R2]** `ActiveCollisionEntity` : la remise à zéro d'entrée de carte est portée **sur le proxy de
   monde**, son porteur réel, et non sur `AlundraGameState` qui n'a pas ce champ. Elle est
   **explicitement déclarée inerte** tant que le proxy est reconstruit par monde (le champ vaut déjà
   `null` à la construction) — hygiène et fidélité, non couverte par l'acceptation. Le commentaire
   périmé d'`AdoptPlayerPawn` (`AlundraWorldProxy.cs:985`) est corrigé en conséquence.

**Acceptation [R3, libellé corrigé en R5] — les SEPT lignes du tableau D-T-13, chacune assertée**, sur un montage à deux
mondes consécutifs partageant le singleton :

| Ligne de D-T-13 | Assertion |
|---|---|
| `GameFlags` conservé | un drapeau de partie posé sur le monde 1 est encore posé sur le monde 2 |
| `TemporaryFlags` vidé | un drapeau temporaire posé sur le monde 1 est vidé sur le monde 2 |
| `MapIdToInternalMapIndexTable` conservé | **[R3]** une entrée NON identitaire écrite sur le monde 1 (via l'opcode 0x38) est inchangée sur le monde 2 |
| `PlayerControlFlags` conservé | **[R3]** un `ControlLocked` posé sur le monde 1 est encore posé après l'entrée de carte du monde 2 |
| `LastPadState` conservé | **[R3]** le dernier état de pad du monde 1 est encore lisible avant le premier update joueur du monde 2 |
| `InteractLatchEntity` vidé, 8 champs numériques conservés | le verrou d'entité est `null` après l'entrée de carte, les huit valeurs numériques sont inchangées, et aucune référence d'entité du monde 1 n'est atteignable depuis l'état de session |

Plus : le manifeste sfx n'est lu qu'une fois pour deux mondes.
**[R3] Et la clause d'isolation EXÉCUTABLE de D-T-14** : chacune des dix classes câblées passe seule
sous `--filter`, et la suite complète reste verte (711 + les nouveaux tests).
**Mutations** : supprimer le vidage des drapeaux temporaires → l'assertion correspondante tombe ;
remettre l'état par monde → l'assertion `GameFlags` tombe ; conserver `InteractLatchEntity` → celle de
non-atteignabilité tombe ; **[R3]** vider l'un des trois champs « conservés »
(`PlayerControlFlags`, `LastPadState`, `MapIdToInternalMapIndexTable`) dans `InstallForMapEntry` →
l'assertion correspondante tombe ; retirer la remise à zéro du `Dispose` d'un porteur → un test nommé
tombe de façon reproductible dans l'exécution complète.

### T2 — Gel global du monde *(report absorbé)*

**Contenu** : porter `EntityManager.cs:377` sur l'update des entités, en respectant **littéralement le
tableau exhaustif de §1.5** — chaque appel des deux boucles d'un seul côté de la porte.

**Acceptation [R3] — deux oracles, chacun sur ce qu'il peut réellement réfuter.** La révision 2
posait un oracle unique qui n'était pas réalisable : `HeadlessIntroSimulation` exclut par
construction le déplacement du joueur, la caméra et le rendu, et il **reproduit**
`UpdateEntities` au lieu d'appeler le code de production — or ce dépôt applique la règle « un miroir
ne tient que si le site de production porte son propre test ».

1. **Oracle headless, sur la donnée réelle de la 389** (précédent `AlundraDialogueOpcodesProductionTests`,
   qui ouvre bien une boîte de marin posant `MenuOpen`) : à boîte ouverte, **la pose et l'état de
   programme d'au moins un PNJ sont figés** frame après frame, **et** `CurrentAnimationId` d'une
   entité entrée avec `TargetAnimationId != CurrentAnimationId` **ne commute pas** (c'est
   l'assertion qui épingle `SyncAnimation` du bon côté) ; la boîte continue d'avancer et se ferme
   normalement. **Aucune assertion sur le joueur, la caméra ou le rendu** : ce montage ne les fait
   pas varier.
2. **Oracle de site de production**, dans le style d'`AlundraDialogueFramePassTests` /
   `AlundraInteractionPassTests`, qui appellent le vrai `AlundraWorldProxy.Update` : la porte posée
   dans le proxy est réellement exercée, et les passes « dehors » (fondus, avance de boîte, suivi
   caméra, `SyncTransform`) tournent encore pendant le gel.

**[R5] DETTE HÉRITÉE DE T1, à solder par cet oracle 2.** Constaté à la livraison de T1 : **aucun test
n'atteint le bloc d'installation d'`InitializeWithWorld`** — tous les tests qui appellent cette
méthode le font sur un monde sans entité `tileMap`, donc elle sort par son retour anticipé
(`AlundraWorldProxy.cs:489-501`) avant le bloc. Deux instructions de production posées par T1 sont
donc **vertes mais non épinglées** : `GameState.InstallForMapEntry()` et la remise à zéro
d'`ActiveCollisionEntity`. Supprimer la première laisserait les drapeaux temporaires jamais vidés à
l'entrée de carte, sans qu'aucun test unitaire ne tombe. C'est la famille « vert et inerte » que ce
dépôt connaît bien. Le montage nécessaire — un monde portant une vraie entité `tileMap`, dont le
matériel existe déjà dans `AlundraCellVisualSyncTests` (carte 389 réelle, `TileMapComponent`
headless) — est exactement celui que l'oracle 2 demande, d'où le report ici plutôt qu'un montage
construit deux fois. **T2 doit donc, en plus de son propre objet, faire franchir le bloc
d'installation et épingler ces deux instructions.**

**Acceptation en jeu** : pendant un dialogue avec un marin, Alundra ne se déplace plus et les PNJ ne
bougent plus. C'est la correction du défaut que l'utilisateur a signalé et accepté de différer.
**[R4] Rattachement des mutations à l'oracle qui peut RÉELLEMENT les détecter.** La révision 3 en
attribuait deux au mauvais oracle : l'avance de boîte n'est pas appelée depuis le site gardé dans le
montage headless (le harnais appelle `Tick()` lui-même, hors de toute porte), et `SyncTransform` n'est
jamais atteint par `AlundraWorldProxy.Update` — il vit en fin d'`AlundraEntityScriptProxy.Update`,
que le moteur invoque avant l'update du proxy de monde.

| Mutation | Oracle qui la détecte | Assertion qui tombe |
|---|---|---|
| retirer la porte | 1 | « PNJ figé » |
| mettre `SyncAnimation` dehors | 1 | « pas de commutation d'animation » |
| omettre les passes de motion de `AlundraEntityScriptProxy.Update` du côté gelé | 1 | « PNJ figé » — une marche déjà lancée continue |
| **[R4]** mettre `SyncTransform` dedans | **1** | la position racine d'une entité portant un `RootComponent` et **pas** de `Controller` — seule forme où `SyncTransform` écrit (`AlundraFrameSyncPasses.cs:168-183`) — cesse d'être publiée pendant le gel. L'oracle 1 pilote bien `AlundraEntityScriptProxy.Update` |
| **[R4]** élargir la porte à l'avance de boîte | **2** | « la boîte avance et se ferme » sous le vrai `AlundraWorldProxy.Update`, précédent `AlundraDialogueFramePassTests` qui pilote déjà `proxy.Update` avec une boîte `MenuOpen` ouverte |
| mettre le suivi caméra dedans | 2 | « la caméra continue » |

**[R6] Deux réserves du vérificateur de clôture de T2, différées avec leur propriétaire.**

- **La gravité moteur n'est pas gelée pour le héros.** `AdoptPlayerPawn` laisse au contrôleur du
  héros la vraie gravité de la carte, et `CharacterMotionSystem` du moteur tourne à chaque frame sans
  aucune porte côté Alundra. Or `SyncTransform` **n'écrit pas la racine** d'une entité à contrôleur
  (`AlundraFrameSyncPasses.cs:176-181`) : rien ne rappelle donc la racine à la pose logique gelée.
  Si une boîte `MenuOpen` s'ouvre alors que le héros est **en l'air**, sa racine continue de tomber
  pendant le gel et la pose logique importe la chute à la levée. L'original, lui, gèle toute la
  physique. Déclenchement étroit : seules les boîtes à monde fermé posent `MenuOpen`, et celles du
  chemin d'acceptation s'ouvrent joueur au sol. **Aucun effet sur l'acceptation de ce chantier.**
  **Propriétaire : T4.** Sa porte de départ vit au même site et hérite exactement du même trou —
  un départ de warp déclenché en l'air aurait le même symptôme. T4 doit donc trancher : soit geler
  aussi le contrôleur moteur pendant sa séquence, soit consigner la déviation avec sa raison.
- **La porte de `RunPendingEventTriggers` n'est épinglée par aucun test de site de production.** Le
  harnais headless appelle cette passe lui-même hors de toute porte. Le **placement** a été vérifié
  indépendamment et le tableau de mutations de T2 n'exigeait pas ce cas, donc la livraison est
  conforme. **Propriétaire : T4**, dont l'acceptation exige déjà « au moins un PNJ **et un programme
  d'événement d'entité** n'avancent pas d'un tick pendant les 16 ticks du fondu sortant » — le
  montage qu'elle demande couvre exactement cette passe.

**[R7] Risque structurel du gel, analysé après la régression, à connaître avant T4.** Le gel arrête
l'interprète de scripts. Or trois opcodes de dialogue **se ré-exécutent à chaque tick jusqu'à
satisfaction** : `0x39` (attendre la boîte), `0x44` (attendre le choix) et le couple `0x50`/`0x51`
(fermeture pilotée par le script). Une boîte ouverte en **mode de contrôle 0** (`MenuOpen`, qui gèle)
puis suivie d'un de ces opcodes serait un **interblocage garanti** : le script qui doit la fermer ne
tourne plus.

**La carte 389 est indemne, vérifié sur la donnée** (`docs/intro-programs-389.txt`) : les boîtes des
marins en mode 0 (textes 132/138/133/155/128/134/135/139) sont suivies d'un `0x05` puis d'un `0xFF End`
immédiat — aucune attente, la fermeture revient au directeur (pression ou minuterie), hors gel. Et
toute la chaîne à choix (`0x39`, `0x50 [4]`, `0x44`, `0x51`) est ouverte en **mode 1** (`MessageBox`),
qui bloque l'entrée du joueur mais **laisse le monde et les scripts tourner** — exactement la
distinction que les deux masques encodent.

À rouvrir si une autre carte combine mode 0 et attente scriptée.

### T3 — Détection des portails

**Contenu** :
1. La sonde de drapeaux de tuile (D-T-10, §1.6) alimentant `CombinedVramFlagsAND/OR`, **réservée au
   joueur** (D-T-12), réutilisant `SampleGroundProperty` et `SampleRawWalkability` qui **existent
   déjà** — **[R2] aucun accesseur nouveau sur `AlundraCellsCollisionField`**.
2. Le balayage de portails par ordre de slot, sémantique « première correspondance gagne, destination
   nulle bloque » (§1.2.b), lisant les champs `X1..Y2` et non le rectangle (§1.1.b).
3. La condition de déclenchement complète (§1.2.a), **[R2] branchée AVANT le retour `BlockedByEntity`
   et AVANT la porte `InputBlockedMask`**, à la position exacte de l'appel original
   (`PlayerManager.cs:29`). Le test propre à la branche portail reste `PlayerControlFlags == 0` ; la
   comparaison d'orientation porte sur **`AnimationDirection`** (domaine 0..3), jamais sur
   `TargetDirection`.

**Acceptation** :
- **[R2] Formulée sur la règle réelle** : le portail 0 de la 389 se déclenche quand **les quatre
  coins de l'empreinte du joueur qualifient et portent tous le bit `0x8000`**, que
  `AnimationDirection` vaut l'index de face requis, et que la touche correspondante est tenue.
- **[R2] Cas négatif obligatoire** : joueur dont la tuile est (18,38) mais dont l'empreinte déborde
  sur une cellule voisine sans le bit → **pas de déclenchement** (le portail est mono-cellule,
  §1.1.d).
- Aucun déclenchement si une seule condition manque, si un drapeau de contrôle est posé, ou si le
  warp est désactivé.
- **[R2] La branche trou se déclenche même avec un bit `InputBlockedMask` posé** — c'est ce que
  garantit le point d'insertion, et ce test échouerait avec l'insertion de la révision 1.
- **[R2]** Le comportement `DestroyOnVramFlags` des PNJ est **inchangé** (D-T-12).

**Mutations** : lire le rectangle de rendu au lieu de `X1..Y2` → les tuiles testées se décalent et le
test tombe ; réintroduire le `+1` de l'original → plus aucun coin ne qualifie et tous les tests de
déclenchement tombent ; continuer le balayage après une destination nulle → le test de blocage tombe ;
placer la sonde après la porte `InputBlockedMask` → le test de la branche trou tombe ; comparer
`TargetDirection` → le test d'orientation tombe ; étendre la sonde à toutes les entités → le test de
non-régression `DestroyOnVramFlags` tombe.

**[R8] Déviation consignée à la livraison de T3.** `IsWarpDisabled` est testé **en amont**, dans le
prédicat de détection, alors que l'original ne teste `g_isWarpDisabled` que dans
`HandleWarpTransition`, en aval. L'effet observable est identique : dans l'original, la détection
aboutit puis `HandleWarpTransition` sort immédiatement sans rien écrire. Le tester plus tôt évite
qu'une couture déjà câblée ne se comporte mal avant que le directeur n'existe.
**Conséquence pour T4 et T7** : le port de `HandleWarpTransition` **doit tout de même** porter le test,
car l'opcode `0x53` (T7) appelle cette même fonction sans passer par le prédicat de détection.

### T4 — Départ

**Contenu** :
1. `AlundraWarpDirector` avec **le contrat de session complet** (D-T-2) : `AttachToWorld`,
   `InstallForMapEntry`, `ResetForTests`, appelés depuis la séquence d'installation de monde comme
   les trois directeurs existants.
2. Le port de `HandleWarpTransition` (§1.2.c).
3. Le gel de départ par la porte propre du directeur (D-T-6), au même site que la porte de T2, sans
   aucun drapeau de contrôle.
4. **[R3] La FIN de transition** : `InstallForMapEntry` applique la disposition **D-T-15** — porte
   remise à faux (port de `GameEngine.cs:302`), enregistrement d'arrivée et id d'effet **conservés**
   pour leurs lecteurs de T5. C'est le pendant obligatoire du point 3, sans lequel joueur et PNJ
   resteraient figés sur la carte d'arrivée.
5. Le fondu sortant à verrou de persistance (D-T-5).
6. Le canal son de départ (D-T-8).
7. La demande `SetWorldToLoad` sur le chemin résolu par `world-index.json` (§1.1.e), émise seulement
   une fois le fondu stabilisé (`IsSettled`).
8. **[R3]** Le câblage d'isolation de D-T-14 étendu au directeur de warp : remise à zéro en
   constructeur ET en `Dispose` de toute classe qui construit un `AlundraWorldProxy`.

**Acceptation** :
- En simulation, la séquence complète frame par frame depuis le déclenchement jusqu'à la demande de
  changement de monde, avec la position d'arrivée exacte pour le portail 0 de la 389 —
  **cible `(10*24+12, 40*16+8) << 16` et `Z = 0`** avant clamp.
- **Gel** : pendant les 16 ticks du fondu sortant, **au moins un PNJ et un programme d'événement
  d'entité n'avancent pas d'un tick**, et le joueur non plus.
- **[R4] Dégel — reformulé sur ce que les montages EXISTANTS peuvent réfuter.** La révision 3
  demandait « le joueur avance de nouveau » et « l'enregistrement est présent quand
  `AdoptPlayerPawn` le consomme » : ni l'un ni l'autre n'est atteignable, aucun montage à deux
  mondes ne fait varier le joueur et `AdoptPlayerPawn` est privé derrière des retours anticipés
  qu'aucun test ne franchit. Deux clauses le remplacent :
  1. **Sur le montage à deux mondes de style T7** (`AlundraScreenFadeDirectorTests:370-412`) : après
     le second `InstallForMapEntry`, `IsTransitionInProgress` est **faux**, la demande de départ, la
     séquence et la demande de monde sont **remises à zéro**, **et** l'enregistrement d'arrivée plus
     l'id d'effet sont **encore lisibles sur le directeur**. L'ordre qui rend cette conservation
     nécessaire est justifié par des sites d'appel fixes : installations en
     `AlundraWorldProxy.cs:505-508`, `AdoptPlayerPawn` en `:531`.
  2. **Sur le site de production** (style oracle 2 de T2, vrai `AlundraWorldProxy.Update`) : une fois
     la porte fausse, les passes gelées **retournent**.
- **[R3] Isolation** : chacune des classes câblées passe seule sous `--filter`, suite complète verte.
- **En jeu** : Alundra quitte la 389 sur un fondu au noir.

**Mutations** : émettre la demande de monde avant stabilisation → le test d'ordre tombe ; verrou de
persistance à zéro → le test « le noir survit à la frame de bascule » tombe ; retirer la porte du
directeur → l'assertion PNJ/événement tombe (et pas seulement celle sur le joueur) ; **[R3]** retirer
la fin de transition → la clause 1 du dégel tombe ; **[R4]** effacer l'enregistrement d'arrivée dans
le même `InstallForMapEntry` → la clause « encore lisible sur le directeur » tombe ; **[R4]** laisser
la **séquence de départ armée** à l'entrée de carte → la porte se repose à la frame suivante et la
clause 2 du dégel tombe ; **[R4]** retirer la remise à zéro **entière** (constructeur ET `Dispose`)
du directeur dans une classe câblée nommée → un test nommé tombe dans l'exécution complète.

**[R9] Réserve du vérificateur de T4, propriétaire T5.** `AlundraWarpDirector.HasPendingArrival` est
posé par le départ et **n'est jamais consommé** : une fois un warp effectué, il reste vrai jusqu'à la
fin de la session. Tout changement de monde ultérieur **non issu d'un warp** présenterait donc un
enregistrement d'arrivée périmé à son lecteur. T5, qui est ce lecteur, doit l'effacer à la
consommation — et son acceptation doit contenir le cas « entrée de carte sans départ préalable
retombe sur les constantes New Game ».

### T5 — Arrivée

**Contenu et statut de chaque élément [R2]** :
1. **L'enregistrement d'arrivée consommé par `AdoptPlayerPawn`** (D-T-4) — **élément actif**, porte
   l'acceptation ci-dessous.
2. **L'id d'effet consommé par `InstallScreenFadeSystems`** au lieu d'être codé en dur (§1.4.g) —
   **élément actif mais observable seulement par le transport** : D-T-7 ramenant tout id non nul à 0,
   le rendu est identique à aujourd'hui. Acceptation dédiée : l'id transporté est bien celui du
   portail, et sa réduction à 0 est journalisée. Mutation : ignorer l'id transporté → ce test tombe.
3. **Le délai de warp de 10 frames** — **[R2] déclaré INERTE**, sur le modèle de D-T-8/D-T-9 : ses
   deux seuls consommateurs originaux (Start+Select, ouverture d'inventaire, `GameEngine.cs:1523-1528`
   et `:1567-1574`) ne sont pas portés. La structure est posée, rien ne la lit. Non couvert par
   l'acceptation, et dit comme tel.
4. **Le rebranchement du suivi caméra** — **[R2] RETIRÉ du périmètre** : déjà assuré à chaque
   installation de monde par `ArmFirstFrameSnap` et `EntityFollowedByCamera` (§1.4.e).

**Acceptation chiffrée** : arrivée sur la 390 à la tuile (10,40), position
`(10*24+12, 40*16+8) << 16` ; **[R2] `PosZ == 4 * 16 << 16 == 4194304` après `ClampToGround`** (la
cellule est à hauteur 4, §1.1.f, alors que le `ZLevel << 20` du portail vaut 0 — la mutation est donc
non vide) ; animation `0x36` ; **[R2] `TargetDirection == AnimationTables.CardinalDirectionTable[1] == 0x10`**
et `CurrentDirection == ~0x10` pour que la première synchro se déclenche ; le fondu entrant part du
noir sur 16 ticks.
**Mutations** : retomber sur les constantes New Game → l'arrivée se fait en (33,59) et le test tombe ;
sauter `ClampToGround` → `PosZ` vaut 0 au lieu de 4194304 et le test tombe ; écrire l'index d'arrivée
brut (1) au lieu de la valeur de table (`0x10`) → le test de direction tombe.

**[R10] Trois réserves du vérificateur de T5, différées à une passe de durcissement APRÈS la
validation en jeu** — volontairement, pour que l'essai de T6 mesure le chantier tel qu'il est plutôt
qu'un remaniement de dernière minute. Aucune n'est atteignable sur l'export livré.

- **Quatre sorties anticipées laissent l'enregistrement d'arrivée non consommé.**
  `InitializeWithWorld` sort avant `InstallWarpSystems`/`AdoptPlayerPawn` quand le monde n'a pas
  d'entité `tileMap` ou pas de `TileMapData` ; `AdoptPlayerPawn` sort avant sa consommation quand
  aucun contrôleur ne possède de pion. Sur les deux premières, `IsTransitionInProgress` reste
  également posé : la carte d'arrivée resterait gelée. **Même famille que le garde-fou d'abandon de
  T4** : un blocage muet. Le remède naturel est de faire tourner les dispositions d'entrée de carte
  des porteurs de session (état de partie et directeur de warp) **avant** la recherche du `tileMap`,
  dont elles ne dépendent pas — ce qui touche aussi le placement retenu par T1.
- **L'assertion d'animation d'arrivée ne discrimine pas** : l'animation du warp (`0x36`) est
  exactement `AlundraGameState.ResetAnimationId`, donc muter la ligne vers la constante New Game ne
  fait tomber aucun test. La valeur reste juste ; c'est la couverture qui est nulle sur cet élément.
- **`WarpDelayFramesForTests` est écrit par le code de production** alors que son nom annonce un
  miroir de test. À renommer le jour où le champ sera lu.

### T6 — Intégration : aller-retour en jeu

**Contenu** : aucune fonctionnalité nouvelle. Montage headless à deux mondes dans le style de T7
partageant les objets de session, et validation en jeu.
**Acceptation utilisateur** : partir de la 389, franchir le portail, arriver dans la 390, revenir par
le portail réciproque (arrivée en 389 à la tuile (18,38), `PosZ = 8 * 16 << 16` d'après §1.1.f),
**et constater que l'intro du bateau ne rejoue pas**. Suite DLL verte.

**[R11] T6 ACCEPTÉE EN JEU par l'utilisateur (2026-09-03).** L'aller-retour 389 → 390 → 389
fonctionne : fondu de départ, arrivée à la bonne tuile et dans la bonne orientation, retour, et
l'intro du bateau ne rejoue pas. **T0 à T6 sont donc closes.**

**Deux défauts MOTEUR préexistants ont été découverts et corrigés pendant cette validation.** Ni l'un
ni l'autre ne pouvait se voir avant ce chantier, puisque le jeu ne changeait jamais de monde.

1. **Ressources de composants jamais libérées au démontage d'un monde** (moteur `183f7d87`, pointeur
   `18b1691`). `Entity.Destroy` ne fait que lever des drapeaux ; rien ne détachait les composants, donc
   les tampons GPU par bloc de `TileMapComponent` restaient enregistrés sur le périphérique à chaque
   changement de carte. `ClearEntities` détache désormais l'arbre de composants des entités qu'il jette,
   ce qui couvre aussi sprites animés, contrôleurs et émetteurs sonores. Les deux abandons de dessin
   liés aux ressources journalisent maintenant une fois par composant au lieu de se taire.
2. **Données de carte partagées et mutées** (moteur `1df1fa95`, pointeur `968d3dc`) — **c'était la cause
   des tuiles disparues**. Le gestionnaire d'assets met les cartes en cache par identifiant et rien ne
   décharge jamais, donc chaque monde recevait la MÊME instance, dans laquelle
   `WallPlacementOverlay` retire les tuiles de murs et de sols pour les entrelacer en profondeur. Le
   calque trié qui les recueille meurt avec le monde : à la deuxième visite les tuiles avaient déjà
   été retirées, **774 placements de murs sur 774 et 582 de sols sur 582** ne correspondaient plus, et
   1356 tuiles n'étaient plus dessinées. Le composant travaille désormais sur une copie par monde ;
   seules les trois listes de tuiles par calque sont dupliquées, le reste est porté tel quel.
   **Diagnostic obtenu par le journal de partie**, pas par déduction : les deux hypothèses précédentes
   étaient fausses.

### T7 — Opcodes `0x53`, `0x9B`, `0x9C` *(tranche séparée, approuvable à part)*

**Contenu** : les trois `case` dans le répartiteur, réutilisant le directeur de T4 ; `0x53` fournit
carte, tuile, effet et sfx sans portail, et conserve l'animation et la direction courantes du joueur
au lieu de `0x36` ; `0x9B` / `0x9C` posent et lèvent le drapeau de désactivation déjà lu par T3.
**Acceptation** : oracle simulé — aucun `0x53` n'existe sur la 389, donc un programme d'événements
synthétique pilote les trois opcodes et vérifie l'état résultant ; le comptage des occurrences dans
le corpus se fait en analysant les tableaux `Codes`, jamais par recherche textuelle (§1.7).
**Mutations** : `0x53` qui pose `0x36` au lieu de conserver l'animation courante → le test tombe ;
`0x9B` sans effet → le test de blocage du déclenchement tombe.

---

## 4. Arrêts

Interrompre et remonter à l'utilisateur, sans contourner, si l'un de ces cas survient :

- une modification devient nécessaire dans le convertisseur ou dans `alundra-project/` (contredit
  D-T-1 et §1.1) ;
- `CasaEngineMonogame/CasaEngine.Launcher/Program.cs` se retrouve modifié ou indexé ;
- un fichier de `alundra-project/` est supprimé à la main ;
- l'édition moteur dépasse le null-check additif de T0, ou touche une API publique ou un
  comportement existant ;
- un **nouvel** échec apparaît dans `CasaEngine.Tests` au-delà des 18 connus, ou un échec apparaît
  dans `Alundra.Tests` — **[R2] y compris un échec dépendant de l'ordre d'exécution** ;
- un travail d'analyseur s'avère nécessaire ailleurs que dans le sous-module
  `alundra-casaengine-project-converter/alundra-datas-analyser` ;
- la fidélité exige de porter un effet de transition autre que 0, ou l'attente sur le son
  (contredit D-T-7 et D-T-9) ;
- **[R2]** la sonde de drapeaux de tuile doit être étendue au-delà du joueur (contredit D-T-12, et
  changerait le comportement `DestroyOnVramFlags` des PNJ) ;
- l'aller-retour d'acceptation exige de porter la sauvegarde de partie.
