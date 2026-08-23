# Feuille de route : intro d'Alundra (New Game → map 389) jusqu'au contrôle joueur

Date : 2026-08-23. Étape 0 du chantier « faire jouer la scène d'intro jusqu'au moment où le joueur prend
le contrôle ». Ce document est produit par un **harnais de trace headless** qui rejoue le flux New Game sur
la map 389 avec l'interpréteur existant, plus une lecture croisée de la décompilation
(`alundra-datas-analyser/AlundraTools/AlundraEngine/`, autorité de fidélité). FAIT = lu dans le code ou
observé dans la trace ; HYPOTHÈSE = inféré.

Résultat en une phrase : **la cinématique d'intro est le programme B 129 du map-event 0, exécuté par
l'entité joueur, qui verrouille le contrôle (0x10), orchestre par flags les Ticks des entités 10, 11, 12,
15 et 18, et rend la main (0x11) quand le bloc 18 pose le flag 860 — 926 frames dans la trace (les
déplacements des PNJ y sont de durée nulle, faute de physique).**

## 0. Méthode

### Le harnais

`Alundra.Tests/IntroTraceHarnessTests.cs` (test `IntroTrace_Map389_ProducesOrderedOpcodeAndSystemTrace`)
rejoue headless (sans rendu) la structure de frame de l'original, en utilisant :

- le vrai interpréteur (`Alundra/Scripts/AlundraEventProgramRunner.cs`), étendu d'une sonde de trace
  `TraceSink` (interne, nulle par défaut : un test de nullité par opcode, aucune allocation sur le chemin
  sans sonde) qui rapporte chaque opcode dispatché avec son `EventTraceKind` (`Implemented` / `Degraded` /
  `UnknownSkipped` / `UnknownNoSizeTerminated` / `End` / `Break`) et une référence vers l'`EventProgramState`
  en cours ;
- le mapping réel des records (`EntityRecordMapper.Map`) et le chargeur réel des programmes
  (`MapEventProgramLoader.Load`) sur `alundra-project/Maps/The Klark/Ship Klark (beginning)-389/` — les
  données rejouées sont celles que le convertisseur écrit, pas des données synthétiques ;
- la passe pick/run déjà portée, `AlundraWorldProxy.RunEntityEventsPass`, réutilisée **sans
  modification** ;
- un port headless de `RunMapEvents` (`GameEngine.cs:1667-1718`) : les 7 map-events exécutent leur
  programme B sur l'entité joueur, chacun avec son propre `EventProgramState` ;
- une extension **trace-only** de l'interpréteur aux slots B–F via les internals
  `InitializeEventData`/`RunOneScriptCall`, en reproduisant la politique de reprise de
  `EntityEventHandlers.RunScript` (`:232-296`) ; **le runner de production continue de traiter B–F comme
  des no-ops comptés** ;
- un registre des systèmes absents, journalisés la première fois que l'original les appellerait, dans
  son ordre d'appel.

Pour relancer (le test s'auto-ignore si `alundra-project/` est absent ; il régénère
`docs/intro-trace-389.txt` et `docs/intro-programs-389.txt`) :

```
dotnet test Alundra.Tests -c Release --filter IntroTrace
```

### Ce qui a été porté dans la DLL pour rendre la trace fidèle

| Opcode | Handler original | Pourquoi maintenant |
|---|---|---|
| 0x36 « Wait until flag on » | `Script_54_036`, `EntityEventHandlers.cs:1166-1176` | primitive de synchronisation inter-entités de toute l'intro (retourne 3 si le bit est posé, 0 = suspension sinon ; le nom « Wait flag off » d'`EventCodeDebugger` est trompeur) |
| 0x33 « Check flags on » | `Script_51_033`, `:1112-1126` | prédicat pur sur 4 paires (flag, bit) |
| 0x8B « Spawn entity next to entity » | `Script_139_08B`, `:2557-2575` | spawn du bloc 10 (travelling caméra) par B1 ; port via `IEntityWorldContext.SpawnEntityByRecordId` + `EntitySearchService` ; déviation : null-check du spawn là où l'original déréférence sans test |

Tests unitaires ajoutés dans `Alundra.Tests/AlundraEventProgramRunnerTests.cs` (2 par opcode).

### Déviations du harnais (toutes documentées dans le code)

- **A. Filtre de spawn au chargement** : `GameEngine.SpawnEntity` (`GameEngine.cs:684-717`) ne spawne un
  record au chargement que si `(SpriteDirection & 0x40) != 0` (et si le joueur est dans sa zone
  `XMin..YMax`). Sur la 389 : 14 entités au chargement, les records 7/8/9/10/18 n'arrivent que par
  0x2D/0x8B. Le harnais réutilise `AlundraWorldProxy.ShouldSpawnRecord`, qui porte déjà cette règle.
- **B. Spawn dynamique réel** : `SpawnEntityByRecordId` construit une entité depuis le record
  (`Status = Loaded`, direction par `AnimationTables.CardinalDirectionTable`), l'ajoute à la simulation à
  partir de la frame suivante (la passe itère un instantané de la liste, comme la pick-phase de
  l'original ne voit une entité `Loaded` qu'à la frame suivante) et émet `frame | SPAWN | record N`.
- **C. Prédicats optimistes** : les prédicats sautés qui dépendent d'un système absent posent
  `Result = 1` — 0x07/0x2F/0x70 (physique de déplacement), 0x39/0x44/0x51 (dialogue). Sans cela l'idiome
  « prédicat ; If false goto en arrière » boucle à l'infini (c'est ce qui bloquait le bloc 18 sur
  `0x70 → 0x04`).
- **D. Prédicat pessimiste** : 0x3B « Check player in area » (`Script_59_03B`, `:1223-1238`, lit
  `PlayerEntity.TileX/Y/Z`) pose `Result = 0`. Le runner n'a pas encore de joueur (lot 2) ; pendant
  l'intro le joueur est figé par B1 en (444, 920, 80) → tuile (18, 57), et **aucune** des boîtes 0x3B de
  la map — (18,18,38,38,8,8), (15,15,28,28,7,7), (21,21,28,28,7,7), (16,16,42,42,5,5),
  (15..21,32..40,25..30) — ne la contient : `Result = 0` est exactement ce que l'original calcule ici.
- **E. Conditions d'arrêt** : (a) 0x11 dispatché sur le joueur en contexte **map-event 0** (B2–B5 sont
  des scripts de porte qui utilisent aussi 0x10/0x11) ; (b) 300 frames sans qu'aucune paire (contexte,
  pc) inédite ne s'exécute (progression, pas opcode distinct : un Wait qui se ré-entre n'est pas un
  progrès) ; (c) plafond 3600 frames.
- **F. Garde-fous diagnostiques** : `AlundraEventProgramRunner.MaxIterationsPerCall` (null par défaut,
  20 000 dans le harnais) et un plafond cumulé côté harnais. Silencieux dans la trace finale ; ils ont
  servi à prouver que le chemin « skip » sur 0x36 bouclait.

### Limites connues du harnais (inatteignables sur la map 389, à traiter au lot 1)

Relevées par le verifier ; aucune n'influence la trace car les branches concernées ne s'exécutent jamais
ici : (1) le reset « joueur hors zone » de `RunMapEventsPass` remet à zéro `EventData.Sp` du map-event au
lieu du `EventProgramState` du joueur (`GameEngine.cs:1690-1697` zéroe aussi `ChildEntity`,
`RelativeWarpOffsetX`, `Index`) — les 7 zones couvrent toute la map ; (2) `RunMapEventsPass` omet
`MapEventProgramId = ProgramBMap`, `EventTrigger = i`, `LogicContextEntity = mapEventEntity` — inertes
tant qu'aucun opcode 0x66 ne re-cible l'entité logique ; (3) le slot F zéroe les forces de l'entité au lieu
de celles du joueur (`EntityEventHandlers.cs:268-273`) — aucun slot F n'est atteint. Le port de production
(lot 1) doit suivre l'original, pas le harnais.

### Mise en garde — ce qui est fidèle et ce qui est compressé

Fidèles : les attentes 0x37 et les synchronisations 0x36 sont suspensives et comptées en frames ; l'ordre
et l'espacement des spawns/flags ci-dessous (370 → 554 → 704 → 782 → 785 → 926) reflètent les Waits réels.
Compressés : tout déplacement/animation piloté par 0x1E/0x1F/0x5B/0x5A/0x1B/0x07 et le test de sol 0x70
ont une durée nulle (sautés ou optimistes) — le bloc 18 « atterrit » instantanément, les marins marchent
en zéro frame. Les branches à flags utilisent l'état New Game (tout à zéro) plus les flags que la trace
pose elle-même.

### Résultat

- Arrêt : condition (a), 0x11 sur le joueur en contexte map-event 0 à la **frame 926**.
- 19 entités au total (14 au chargement + 5 dynamiques : 7, 8, 9, 10, 18) ; 7 map-events actifs.
- Trace : 16 710 lignes compactées / 25 897 dispatches bruts + 33 lignes système.
- 19 opcodes non implémentés distincts rencontrés ; 20 implémentés/dégradés ; 33 systèmes absents ou
  partiels ; 0 garde-fou déclenché ; 0 opcode sans taille connue.

### Chronologie de l'intro (trace)

| Frame | Évènement |
|---|---|
| 0 | Bloc d'entrée de map (§3 : `ClearTemporaryFlags` … `LoadMapSounds`) ; joueur slot 0 en tuile (33,59), anim 0x36 |
| 1 | B1 (map-event 0, prog 129) : `0x30` flag 860 off → continue ; 2× `0x38` ; `0x67` caméra → entité 6 ; `0x10` verrou ; `0x64` joueur → (804, 872, 0) ; Break. Les 14 entités chargées passent Loaded → Normal (slot A) |
| 2 | B1 : `0x64` joueur → (444, 920, 80) puis 6× `Wait(60)` (réels, ~62 frames chacun). Ticks 139/140/143 se suspendent sur `0x36` (0x83E8 / 0x83E9 / 0x83EA) |
| 370 | B1 : `0x8B` spawne le record 10 à côté de l'entité 6 (+64, +48) |
| 371-374 | Bloc 10 (Tick 138) : `0x67` caméra → lui-même, `0x5B`, `0x07` (optimiste), `0x1B`, `0x19` se désactive → slot E → IA native E0 |
| 554 | B1 : `0x05` flag **0x83E8** → libère le Tick 139 (marin 11) ; `0x67` caméra → entité 11 |
| 555 / 678 / 801 | B1 : `0x2D` active 7, 8, 9 (mouettes, Ticks 135-137 : envol `0x1B`, `0x07`, retour) avec 2× `Wait(60)` entre chaque |
| 555-700 | Marin 11 : regarde dans 4 directions (`0x09`/`0x37(15)`), `0x17`, `0x1F`, `0x5B`, `0x1B` (saut), `0x07`, `0x16`, marche (`0x1F`), `0x5A` |
| 704 | Marin 11 : `0x05` flag **0x83EA** → libère le Tick 143 (marin 15) |
| 704-740 | Marin 15 : `0x5B`/`0x1F`, `0xBD` son 61, 5× `0x85` (trappe), `0x64`, `0x2E` se détruit |
| 782 | Marin 11 : `0x05` flag **0x83E9** → libère le Tick 140 (marin 12) : `0x67` caméra → lui, marche, `0x2D` active 18 |
| 785 | Bloc 18 (Tick 146) : `0x67` caméra → lui-même, `0x1E`, `0x1B`, `0x70` (optimiste) → `0x05` flag **860**, `0x67` caméra → fonction 1 (joueur), `0x2E` se détruit |
| 923-925 | B1 : 3× Break |
| **926** | B1 : `0x36` flag 860 déjà posé → avance ; **`0x11` : le joueur reprend le contrôle** ; `0xFF` |

## 1. Faits élucidés dans la décompilation

Références dans `alundra-datas-analyser/AlundraTools/AlundraEngine/`.

### 1.1 Qui pilote la cinématique : les map-events (slot B) sur l'entité joueur

- FAIT `GameEngine.cs:1667-1718` (`RunMapEvents`, 0x8003c67c) : à chaque frame, pour chaque
  `g_mapEvents[i]` dont `(ProgramBMap & 0x7F) != 0`, si la tuile du **joueur** (`PlayerEntity.TileX/TileY`)
  est dans `[X1..X2] × [Y1..Y2]` du record :
  `playerEntity.ProgramIndexes[ProgramBMap] = mapEvent.ProgramBMap`,
  `playerEntity.EventProgramState.CopyFrom(mapEvent.EventData)`, `RunScript(playerEntity, ProgramBMap)`,
  puis l'état est recopié dans `mapEvent.EventData`. Chaque map-event a donc son propre curseur
  résumable, et c'est **toujours l'entité joueur qui exécute**. Hors zone, l'état est réinitialisé
  (`EventProgramState.Sp = 0`, `ChildEntity = null`).
- FAIT `GameEngine.cs:476-583` (`InitializeMapEvents`) : un `MapEvent` par record dont
  `EventCodesBIndex != 0`, `Entity = PlayerEntity`, `EventData = new EventProgramState()` (`Codes == null`
  ⇒ le premier `RunScript` passe par `InitializeEventData`).
- FAIT données (calque `MapEvents` du tilemap 389) : **7 map-events de zone (0,0)-(51,59) = toute la
  map**, programmes B 129..135 (bit 0x80 = table locale, index masqués 1..7). Le héros spawnant en
  (33,59), les 7 programmes B tournent dès la première `Update(1)` de l'entrée de map.
- FAIT `EntityManager.cs:806-921` (`UpdateEntitiesEvents`) : la passe d'entités n'assigne jamais le slot B
  (seulement A/C/D/E/F) et **exclut le slot 0 (joueur)** de sa boucle (`for i = 1..`). Le slot B n'existe
  que via `RunMapEvents` — ce qui clôt l'inconnue « qui déclenche B ».
- FAIT `EntityEventHandlers.cs:239-264` (`RunScript`) : B et C reprennent `entity.EventProgramState` si
  `Codes != null` ; pour C, si `entity.MapEventProgramId != 2`, `TargetAnimationId/Direction` sont
  restaurés depuis `LastTarget*`. Tout autre slot (dont A) réinitialise via `InitializeEventData` sur
  l'état partagé `g_eventProgramState` (`:234`). Fin de programme : `0xFF` ⇒ fin ; `0x00` ⇒
  `Parameters[1] = 0`, `CodeIndex++`, fin (reprise à l'instruction suivante au prochain appel) ; handler
  renvoyant 0 ⇒ suspension (`:304-377`). `g_clearProgramState` remet `Codes = null` (`:343-358`,
  `:382-391`).
- FAIT (annexe A, trace §0) : le programme B 129 est la cinématique ; B 130-133 sont les quatre portes
  (0x3B zone joueur → 0x85 animation de tuiles → 0x11), B 134 vérifie les flags 861-868 (tous les marins
  interrogés) pour poser 870, B 135 est la boucle d'ambiance sonore (`0xBD` 44 + attentes + `0x49`).

### 1.2 Ordre d'exécution d'une frame

- FAIT `GameEngine.cs:1500-1592` (`Update`) : scroll-in (`g_mapOffsetX -= 8`, `g_mapOffsetY -= 6`) →
  `_padManager.UpdatePads()` (`:1517`) → test Select+Start → **`UpdateWorld()`** → `g_warpDelayFrames--` →
  test ouverture inventaire → `SoundManager.HandleMapSoundStreaming()` → `Random.Next()`.
- FAIT `GameEngine.cs:1638-1664` (`UpdateWorld`, 0x8002e058) : `RunMapEvents()` → `UpdateEntities()` →
  `EffectManager.UpdateEffects()`.
- FAIT `EntityManager.cs:367-395` (`UpdateEntities`) : si `(g_playerControlFlags & GameplayBlockedMask) == 0` :
  `UpdateDestroyedEntities` → `UpdateEntitiesEvents` → `UpdateEntitiesCounters` → `UpdateEntityLists` →
  `UpdateEntitiesAnimation` → `PhysicsEngine.UpdateEntitiesPhysics` → `UpdateActiveEffects` →
  `UpdateBalanceRecords` ; sinon seulement `UpdateEntityLists`. Puis `UpdateVisibleEntitiesZSort`.
- FAIT `EntityManager.cs:808` : **`PlayerManager.MovePlayer()` est la première instruction de
  `UpdateEntitiesEvents`**, avant la passe pick/run des entités 1..n : le « tick » du joueur est
  `MovePlayer` (`PlayerManager.cs:17`), pas un programme.
- FAIT `GameEngine.cs:222-229` : `RenderScene()` précède `Update(0)`.

### 1.3 Ce que l'entrée de map exécute (New Game → 389)

- FAIT `GameEngine.cs:168-219` (bloc `g_isGameEnding != 0` de `MainLoop`) : `InitializeStaticVariable` →
  `LoadMap(389)` (`:179`) → `ClearTemporaryFlags()` (`:429`, 64 mots à 0) → `ResetCameraAndLoadVRAMAssets()`
  (`:445`, `g_isCameraScrolling = 1`) → `InitializeItems(CurrentMap.Info.D)` →
  **`LoadMapAndInitializeEntities`** (`:466` : `InitializeEntitySlots` → `InitializeMapEvents` →
  `EffectManager.InitializeEffectSlots`) → **`WarpPlayer(posX, posY, posZ, g_mapTransitionEffectId)`**
  (`:878` ; effet 0 ⇒ fondu 0xff0000 → 0 en 0x10 frames, `g_warpDelayFrames = 10`) →
  `InitializeScrollingMode()` → `HudManager.InitializeHudPositionBeforeHide()` → `LoadMapSounds(389)` →
  **`Update(1)`** (première frame complète, scripts compris) → `GraphicManager.ResetDebugRenderingState()`.
- FAIT `GameEngine.cs:621-646` (`InitializeEntitySlots`) : `EntityManager.InitializeEntitySlots()` ;
  `g_numberOfEntities = 0` ; **`ResetEntityState()`** (héros) ; `SpawnEntity(null, i, 0)` pour chaque
  record ; enfin `g_entityFollowedByCamera = PlayerEntity`.
- FAIT `GameEngine.cs:680-760` (`SpawnEntity`, 0x8003a1b8) : avec `notCheckSpawnZone == 0`, un record n'est
  spawné que si `PlayerEntity.TileX ∈ [XMin, XMax]`, `TileY ∈ [YMin, YMax]` **et `(SpriteDirection & 0x40)
  != 0`** (`:714-717`) — le héros doit exister avant les records ; 0x2D/0x8B passent `1` et ignorent ces
  deux gates. Sur la 389, 14 records spawnent au chargement ; 7/8/9/10/18 attendent un spawn scripté.
  `AlundraWorldProxy.ShouldSpawnRecord` (`Alundra/Scripts/AlundraWorldProxy.cs:288-323`) porte déjà ces
  règles.

### 1.4 Création du héros et état de jeu

- FAIT `GameInitializer.cs:331-436` (`InitializeGameState`, 0x80031700) : `g_resetAnimationId = 0x36`,
  `g_resetDirectionId = 0`, `g_mapTransitionEffectId = 0`, `g_desiredMap = g_saveData.InitialMapId (389)`,
  `g_cameraTargetX/Y = (CameraTileX/Y × 24|16 + 12|8) << 16` avec tuile (33, 59), `g_cameraTargetZ = 0 << 20`,
  `g_gameplayTime = 0`. Stats : HP 10/10, MP 0/0, argent 0, arme 1, objets 1/17/25
  (`docs/demarrage-nouvelle-partie.md` §2.1).
- FAIT `GameEngine.cs:648-670` (`ResetEntityState`, 0x80031974) : `GetSpriteFromSpriteTable(false, 0)` =
  **record 0 de la table globale** (banque `Entities/Alundra`) ; `InitializeEntity(PlayerEntity, null,
  spriteRecord, null, 0, -1, g_cameraTargetX/Y/Z, g_resetAnimationId, g_resetDirectionId, 0xb, 0x60)` ;
  `Status = Normal` (pas `Loaded` : le héros n'a pas de Load) ; `Hp/HpMax` depuis `PlayerManager` ;
  `g_activeCollisionEntity = null` ; timers de warp à 0 ; `g_currentWeaponFlags` depuis l'arme courante.
- FAIT `StaticVariables.cs:12815` : `g_playerControlFlags` n'est initialisé nulle part explicitement (zéro
  BSS) ; seul `GameEngine.cs:331` le remet à 0 (sortie de warp effet 10). Au New Game le joueur est libre
  tant qu'aucun script n'a exécuté 0x10 — B1 le fait à la frame 1.
- FAIT `PlayerControlFlags.cs:58,61` : `GameplayBlockedMask = MenuOpen | Unused40` (0x48) ;
  `InputBlockedMask = ControlLocked | MessageBox | ForcedSequence` (0x34). Conséquence : **le verrou de
  script 0x10 (`ControlLocked`) ne stoppe ni `RunMapEvents` ni `UpdateEntities` ; il ne bloque que
  `MovePlayer`** (`PlayerManager.cs:38`), qui joue alors `CreatePlayerAnimationEffects(1)`,
  `UpdatePlayerCarriedEntity(1)`, `AnimateWarpEffect()` et sort.
- FAIT `EntityEventHandlers.cs:680-693` : 0x10 `|= ControlLocked`, 0x11 `&= ~ControlLocked`. Critère
  « le joueur prend le contrôle » = exécution de 0x11 par B1 (`g_playerControlFlags` redevient 0) puis
  `MovePlayer` qui lit le pad.

### 1.5 Conséquences pour le port (décisions d'architecture)

- Le driver des map-events (port de `RunMapEvents`) vit dans `AlundraWorldProxy.Update`, **avant** la
  passe d'entités, exécute sur l'entité joueur avec un `EventProgramState` par map-event.
- L'entité joueur est un `AlundraEntityScriptProxy` de slot 0 créé par le port de `ResetEntityState`
  avant le spawn des records (gate de zone), `Status = Normal`, jamais parcourue par pick/run.
- `MovePlayer` devient le tick du joueur, en tête de la passe d'événements, gardé par `InputBlockedMask`.
- Le runner doit porter la politique de reprise de `RunScript` (`:239-296`) et `g_clearProgramState`,
  pas seulement des handlers.
- `g_entityFollowedByCamera` est une variable réassignée 6 fois pendant l'intro (6 → bloc 10 → 11 → 12 →
  bloc 18 → joueur) et dont la cible peut être détruite : la caméra suit une référence, pas « le héros ».

## 2. Inventaire ordonné des opcodes non implémentés

Ordre de première apparition dans la trace (926 frames). Handlers dans `EntityEventHandlers.cs` ; la
sémantique est lue dans le corps du handler.

| # | 1re frame | Contexte | Opcode | Nom (`EventCodeDebugger`) | Handler original | Système | Sémantique | Occ. | Politique trace |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 1 | Map-event 0 (B 129) | 0x38 | Set MapIdToInternalMapIndex | `Script_SetSaveMapIdToInternalMapIndex_038` `:1202-1207` | état de jeu | `g_saveData.MapIdToInternalMapIndexTable[v1\|v2<<8] = v3\|v4<<8` | 2 | — |
| 2 | 1 | Map-event 0 | 0x67 | Camera follow entity | `Script_103_067` `:2070-2075` | caméra | `g_entityFollowedByCamera = premier match de la recherche v1` | 6 | — |
| 3 | 1 | Map-event 0 | 0x10 | Player lose control | `Script_16_010` `:680-684` | contrôle joueur | `g_playerControlFlags \|= ControlLocked` | 1 | — |
| 4 | 1 | Map-event 1 (B 130) | 0x55 | Set unwalkable | `Script_85_055` `:1623-1652` | cellules | clamp (x,y) puis `Walkability &= ~v3`, `GroundProperty &= ~v4` sur une cellule | 4 | — |
| 5 | 1 | Map-event 1 | 0x85 | Set map tiles | `Script_133_085` `:2440-2444` → `GameEngine.ChangeAreaTileProperties` `GameEngine.cs:2239-2300` | cellules + rendu | copie un rectangle (x,y,w,h) de cellules vers (dx,dy) : walkability, ground, slope, height, **TileId, pile de murs** | 9 | — |
| 6 | 2 | Map-event 1 | 0x3B | Check player in area | `Script_59_03B` `:1223-1238` | test joueur | `Result = joueur.TileX/Y/Z ∈ boîte v1..v6` | 4624 | pessimiste (D) |
| 7 | 2 | Map-event 5 (B 134) | 0x4B | If false restart | `Script_75_04B` `:1476-1486` | flux | si `Result == 0` : `CodeIndex = Parameters[0]` (début du programme) | 3 | — |
| 8 | 3 | Entité 6 (Tick 134) | 0x1B | Fly | `Script_27_01B` `:743-747` | physique | `ForceZ = ((v2<<8\|v1) << 16) >> 8` (impulsion verticale signée) | 21 | — |
| 9 | 3 | Entité 6 (Tick 134) | 0x07 | Check entity in area | `Script_7_007` `:539-570` | test entité | `Result = premier match de v1 dans la boîte tuiles v2..v7` | 12 | optimiste (C) |
| 10 | 3 | Entité 13 (Tick 141) | 0x1E | Walk | `Script_30_01E` `:793-826` | physique | mémorise la position au 1er passage, suspend jusqu'à `\|ΔX\|` ou `\|ΔY\|` ≥ seuil (pixels) | 148 | — |
| 11 | 3 | Entité 13 (Tick 141) | 0x0A | Reverse direction | `Script_10_00A` `:599-603` | direction | `TargetDirection = (TargetDirection + 0x10) & 0x1f` | 146 | — |
| 12 | 3 | Entité 13 (Tick 141) | 0x49 | Restart | `Script_73_049` `:1455-1459` | flux | saut relatif vers `Parameters[0]` (début du programme) | 3 | — |
| 13 | 371 | Bloc 10 (Tick 138, spawn dyn.) | 0x5B | Turn entity with anim | `Script_91_05B` `:1713-1732` | animation/direction | pour chaque match de v1 : `TargetAnimationId = v2`, `TargetDirection = ResolveDirectionFromParam(v3)` | 29 | — |
| 14 | 374 | Bloc 10 (Tick 138) | 0x19 | Deactivate entity | `Script_25_019` `:729-733` | statut | `Status = Deactivated` (⇒ slot E à la frame suivante) | 1 | — |
| 15 | 619 | Marin 11 (Tick 139) | 0x1F | Walk with collision | `Script_31_01F` `:832-843` | physique | comme 0x1E, ou avance si `ForceAdjusted != 0` (collision) | 18 | — |
| 16 | 624 | Marin 11 (Tick 139) | 0x16 | High gravity | `Script_22_016` `:715-719` | physique | `Flags \|= Gravity` | 2 | — |
| 17 | 643 | Marin 11 (Tick 139) | 0x5A | Turn entity | `Script_90_05A` `:1694-1710` | direction | pour chaque match de v1 : `TargetDirection = ResolveDirectionFromParam(v2)` | 1 | — |
| 18 | 785 | Bloc 18 (Tick 146, spawn dyn.) | 0x70 | Is above ground | `Script_112_070` `:2161-2165` | physique | `Result = IsOnGround` (posé par `UpdateEntitiesPhysics`) | 1 | optimiste (C) |
| 19 | 926 | Map-event 0 (B 129) | 0x11 | Player gain control | `Script_17_011` `:687-691` | contrôle joueur | `g_playerControlFlags &= ~ControlLocked` | 1 | — (arrêt (a)) |

Les 4 624 occurrences de 0x3B sont le re-test fidèle, chaque frame, des 4 scripts de porte B 130-133
en attente du joueur. Opcodes référencés par la map mais **non atteints** sous New Game : 0x54 (Set
walkable, portes), 0x2F (Check moving in dir, portes), 0x0D/0x39/0x44/0x50/0x51 (dialogue : slots F et bloc
gardé par 0x800C du Tick 140), 0x59/0x27 (idem).

### Opcodes implémentés ou dégradés rencontrés

| Opcode | Nom | 1re frame / contexte | Occ. | Note |
|---|---|---|---|---|
| 0x30 | If flag on | 1 / map-event 0 | 5 | |
| 0x64 | Set entities position | 1 / map-event 0 | 3 | |
| 0xBD | Play sound 2 | 1 / map-event 6 | 15 | dégradé : sons 44 (ambiance), 45 (mouettes), 46, 61 (trappe) non joués |
| 0x37 | Wait | 1 / map-event 6 | 2240 | |
| 0x17 | Low gravity | 1 / entité 0 (Load) | 10 | |
| 0xAC | Set entity shadow size | 1 / entité 6 (Load) | 4 | |
| 0x31 | If flag off | 1 / entité 6 (Load) | 929 | |
| 0x05 | Flag on | 1 / entité 6 (Load) | 6 | flags 200, 0x83E8, 0x83EA, 0x83E9, 860 |
| 0x1A | Set anim | 1 / entité 6 (Load) | 25 | |
| 0x04 | If false goto | 2 / map-event 1 | 3713 | |
| 0x33 | Check flags on | 2 / map-event 5 | 3 | porté dans ce lot |
| 0x09 | Set direction | 2 / entité 6 (Tick) | 9 | |
| 0x36 | Wait until flag on | 2 / entité 11 (Tick) | 2183 | porté dans ce lot |
| 0x02 | Goto | 2 / entité 12 (Tick) | 1070 | |
| 0x03 | If true goto | 3 / entité 5 (Tick) | 924 | |
| 0x62 / 0x63 | Set / Clear entities flags | 4 / entité 6 (Tick) | 4 + 4 | |
| 0x8B | Spawn entity next to entity | 370 / map-event 0 | 1 | porté dans ce lot |
| 0x2D | Activate entity | 555 / map-event 0 | 4 | |
| 0x2E | Destroy entity | 740 / entité 15 (Tick) | 2 | |

## 3. Systèmes absents, dans l'ordre d'appel original

| # | 1re frame | Fonction originale | file:line | Rôle | État dans la DLL |
|---|---|---|---|---|---|
| 1 | 0 | `GameEngine.ClearTemporaryFlags` | GameEngine.cs:429 | vide `g_temporaryFlags` à l'entrée de map | non porté (`AlundraGameState` part de zéro, rien ne re-vide) |
| 2 | 0 | `GameEngine.ResetCameraAndLoadVRAMAssets` | GameEngine.cs:445 | reset caméra/scroll | non porté |
| 3 | 0 | `GameEngine.InitializeItems` | GameEngine.cs:454 | table d'objets de la map | non porté |
| 4 | 0 | `InitializeEntitySlots` → `ResetEntityState` (joueur slot 0) | GameEngine.cs:621-670 | création du héros | non porté (approximé dans le harnais) |
| 5 | 0 | `GameEngine.InitializeMapEvents` | GameEngine.cs:476-583 | 7 slots `MapEvent` depuis les records | non porté en production (porté dans le harnais) |
| 6 | 0 | `EffectManager.InitializeEffectSlots` | GameEngine.cs:472 | pool d'effets | non porté |
| 7 | 0 | `GameEngine.WarpPlayer` | GameEngine.cs:878-973 | fondu d'entrée, `g_warpDelayFrames = 10` | non porté |
| 8 | 0 | `GameEngine.InitializeScrollingMode` | GameEngine.cs:214 | mode de scroll caméra | non porté |
| 9 | 0 | `HudManager.InitializeHudPositionBeforeHide` | GameEngine.cs:215 | HUD | non porté |
| 10 | 0 | `GameEngine.LoadMapSounds` | GameEngine.cs:216 | BGM/SFX de la map | non porté (audio exporté en Phase 4, non branché) |
| 11 | 0 | `GraphicManager.ResetDebugRenderingState` | GameEngine.cs:218 | debug | sans objet |
| 12 | 1 | `PadManager.UpdatePads` | GameEngine.cs:1517 | lecture manette | non branché au proxy |
| — | 1 | `RunMapEvents` | GameEngine.cs:1667-1718 | slot B sur le joueur | **porté dans le harnais seulement** |
| 13 | 1 | `PlayerManager.MovePlayer` | EntityManager.cs:808 / PlayerManager.cs:17 | tick du joueur | non porté (non-goal documenté de `RunEntityEventsPass`) |
| 14 | 1 | IA native `RunSpriteEvent` (`g_entityEventFunctionsByType`) | SpriteEventHandlers.cs:243 | handlers natifs par sprite | no-op compté ; l'intro n'exige que A0 `SetSpawnFlagFromZPos` (FunctionTypeA.cs:9) et E0 `FUN_8007ed10` = `DestroyEntity` (FunctionTypeE.cs:8) ; C0/D0/F0 sont vides |
| 15 | 1 | `EntityManager.UpdateDestroyedEntities` | EntityManager.cs:379 | recyclage des slots | non porté |
| 16 | 1 | `EntityManager.UpdateEntitiesCounters` | EntityManager.cs:381 | compteurs par entité | non porté |
| 17 | 1 | `EntityManager.UpdateEntityLists` | EntityManager.cs:383 | listes actives/visibles | non porté |
| 18 | 1 | `EntityManager.UpdateEntitiesAnimation` | EntityManager.cs:384 | timing d'animation | **partiel** : `RunAnimationSyncPass` résout la cible, le timing est délégué à CasaEngine |
| 19 | 1 | `PhysicsEngine.UpdateEntitiesPhysics` | PhysicsEngine.cs:10 | forces, gravité, sol, murs | non porté |
| 20 | 1 | `EntityManager.UpdateActiveEffects` | EntityManager.cs:386 | effets | non porté |
| 21 | 1 | `EntityManager.UpdateBalanceRecords` | EntityManager.cs:387 | combat | non porté |
| 22 | 1 | `EntityManager.UpdateVisibleEntitiesZSort` | EntityManager.cs:394 | tri profondeur | partiel (`RunWallInterleaveSortKeyPass`) |
| 23 | 1 | `EffectManager.UpdateEffects` | GameEngine.cs:1663 | effets | non porté |
| 24 | 1 | `Update` : `g_warpDelayFrames--`, inventaire | GameEngine.cs:1561-1578 | décompte warp, menu | non porté |
| 25 | 1 | `SoundManager.HandleMapSoundStreaming` | GameEngine.cs:1580 | streaming son | non porté |
| 26 | 1 | `Random.Next` | GameEngine.cs:1582 | RNG fidèle | non porté (rien ne consomme d'aléatoire) |
| 27 | 1 | suivi caméra `g_cameraLookAt = suivie.Pos >> 16` | EntityManager.cs (UpdateEntities) | caméra | non porté (caméra debug seulement) |

(33 lignes `SYSTEM` dans la trace : les 26 ci-dessus + une par entité passant par `RunSpriteEvent`.)

## 4. Ordre des chantiers

### 4.1 Ce que l'intro exige réellement

Le déroulé sous New Game est une **chaîne de synchronisation par flags** entre B1 (exécuté par le joueur)
et les Ticks des entités 10, 11, 12, 15 et 18, plus les mouettes 6-9 en décor animé (chronologie §0).

FAITS qui bornent le périmètre :

- **Aucun dialogue avant la prise de contrôle** : 0x0D n'apparaît que dans les programmes F et dans le
  bloc du Tick 140 gardé par le flag temporaire 0x800C, que seul F12 (interaction) pose. Le chantier
  dialogue passe **après** le jalon.
- **IA native réduite à deux handlers** : les banques 25/146/161 ont tous leurs `Program*` natifs à 0
  (`alundra-project/Data/sprite-records.json`) ; `SpriteEventHandlers.cs:24,50,155,203,233` :
  A0 = `FunctionTypeA.SetSpawnFlagFromZPos` (entité 18, `AIValues[0] = PosZ >> 16`), C0/D0/F0 vides,
  E0 = `FunctionTypeE.FUN_8007ed10` = `DestroyEntity(entity, -1)` (bloc 10 après 0x19).
- **Le mouvement scripté passe par la physique** : 0x5B/0x1E/0x1F ne déplacent rien ; ils posent
  `TargetDirection`/`TargetAnimationId` et attendent que `PhysicsEngine.UpdateEntityPhysics`
  (`PhysicsEngine.cs:1579-1597`) dérive `TargetForceX/Y = g_offsetXList[dir] × AnimationSet.Speed` avec
  `Acceleration`, puis que `UpdateEntitiesForces`/`MoveEntity` intègrent. **Gap d'export** : le convertisseur
  ne lit que `AnimSets[].PreloadedAnims` (`alundra-casaengine-project-converter/Readers/SpriteBankReader.cs`,
  `ReadAnimSet`) et perd `Speed`, `Acceleration`, `Flags|Unknown` (= `IsZForceApplied`) et `Sfx`, présents
  dans `data-extracted/data/map_389.json` (`SpriteInfo.SpriteRecords[].AnimSets[]`).
- **0x85 copie des cellules** (`ChangeAreaTileProperties`) : walkability, ground, slope, height, `TileId`
  et pile de murs d'un rectangle source vers une destination — la trappe (entité 15) modifie le rendu et
  les collisions à chaud. Visuel : ne conditionne pas la prise de contrôle.
- **Caméra** : `g_entityFollowedByCamera` change 6 fois pendant l'intro, et deux de ses cibles (blocs 10
  et 18) sont détruites après coup.

### 4.2 Lots, dans l'ordre (un lot = commit + verifier frais)

| # | Lot | Contenu (port ligne à ligne) | Débloque |
|---|---|---|---|
| **1** | **Moteur de script complet** | `RunScript` complet : reprise B/C sur `entity.EventProgramState`, `g_clearProgramState`, `Last*` pour C (`EntityEventHandlers.cs:232-392`) ; driver `RunMapEvents` sur l'entité joueur avec un état par map-event (`GameEngine.cs:1667-1718`, `:476-583`) ; spawn dynamique réel via `IEntityWorldContext` (gates de `SpawnEntity` `:684-717`, `ParentEntity`). Opcodes purs de l'intro : 0x38, 0x10/0x11 (flags de contrôle dans `AlundraGameState`), 0x67/0x68/0x69 (variable « entité suivie »), 0x19, 0x59, 0x5A/0x5B, 0x07, 0x0A, 0x16, 0x27, 0x49/0x4B, 0x3B (lit le joueur), 0x2F, 0x70 (lit `IsOnGround` — stub à 1 tant que le lot 3 n'existe pas, **déviation documentée**). IA native A0/E0 via `SpriteProgramIndexes`. | La chaîne de flags tourne en production (oracle = chronologie §0 : mêmes frames de pose de flags, même frame de 0x11) même si rien ne bouge à l'écran. |
| **2** | **Héros et état de jeu** | `InitializeGameState` New Game (`GameInitializer.cs:331-436`) ; `ResetEntityState` (`GameEngine.cs:648-670`) = proxy joueur slot 0, banque `Entities/Alundra`, anim 0x36 dir 0, tuile (33,59,0), `Status = Normal`, hors pick/run, **créé avant** le spawn des records ; `MovePlayer` minimal (`PlayerManager.cs:17-60` : `InputBlockedMask`, branche verrouillée no-op, branche libre = pad + déplacement E4) ; `g_entityFollowedByCamera = PlayerEntity`. Côté convertisseur : `.gameMode`/controller si le moteur l'exige (`docs/demarrage-nouvelle-partie.md` E2/E3). | Le joueur existe, est placé par 0x64, verrouillé par 0x10, libéré par 0x11 ; déplacement après 0x11. |
| **3** | **Physique des entités** | `UpdateEntitiesPhysics` (`PhysicsEngine.cs:10`), `UpdateEntitiesForces` (`:1365`), `UpdateEntityPhysics` (`:1579`), `MoveEntity`/`ComputeZPosition`/`ComputeXYPosition` (`:71/:109/:364`), `ComputeEntityGroundHeight` (`:956`, cellules `AlundraCells`), `UpdateTileAttributes` (`:1678`) ; clamp au sol d'`InitializeEntity:127-136` ; `UpdateEntitiesCounters`. **Prérequis export** : `Speed`/`Acceleration`/`IsZForceApplied`/`Sfx` par `AnimSet` dans `sprite-records.json` (+ test convertisseur + compteur `report.json`), puis `AnimationTables` côté DLL. 0x1E/0x1F/0x1B/0x07/0x70 deviennent réels. | Sauts, atterrissages, marches des entités 11/12/15/18, envol des mouettes, durées réelles. |
| **4** | **Caméra, fondu, audio** | suivi de `g_entityFollowedByCamera` (`g_cameraLookAt = suivie.Pos >> 16`), `InitializeScrollingMode`, limites de scroll ; `WarpPlayer` effet 0 (`GameEngine.cs:878-903`) et scroll-in `g_mapOffsetX/Y` (`:1504-1515`) ; `LoadMapSounds` + BGM de la 389, SFX 44/45/46/61 de 0xBD, `HandleMapSoundStreaming`. | L'intro est regardable : plan sur la mouette, descente sur le pont, suivi des marins, retour sur Alundra. |
| **5** | **Modification de tuiles à chaud** | 0x85 (`GameEngine.cs:2239-2300`), 0x55/0x54 : copier cellules + `TileId` + pile de murs ; côté moteur, invalidation du `TileMapComponent` et de l'overlay de murs pour le rectangle modifié — chantier **moteur**, plan-verifier requis. | Trappe de l'entité 15 ; portes B 130-133 après contrôle. |
| **6** | **Dialogue et interaction** (post-jalon) | slot F via `g_activeCollisionEntity` (`MovePlayer`), 0x0D/0x39/0x44/0x50/0x51/0x5C, boîte de message (police/strings Phase 5), `MessageBox`/`MenuOpen`. | Parler aux marins (F 139-145, Tick 140), B 134. |

Pourquoi cet ordre : le lot 1 est testable **headless** avec ce harnais (la chronologie §0 devient
l'oracle) ; le lot 2 rend le jalon observable (verrou → libération) sans attendre la physique ; le lot 3
est le plus gros et dépend d'un export supplémentaire, il ne doit bloquer ni 1 ni 2 ; 4 et 5 sont
visuels/sonores et se valident à l'œil ; 6 n'est pas sur le chemin critique.

### 4.3 Acceptation du jalon

1. Harnais : après les lots 1-3, plus aucun `UnknownSkipped` ni prédicat optimiste avant la frame de 0x11,
   et la chronologie des flags 0x83E8 / 0x83EA / 0x83E9 / 860 est reproduite par le runner de production.
2. Runtime : New Game → map 389, fondu d'entrée, caméra sur la mouette puis descente, marins animés,
   trappe, retour caméra sur Alundra, **le pad déplace Alundra après 0x11**.
3. Aucun workaround : chaque écart visuel est remonté à sa cause (export / DLL / moteur).

## Annexes

### A. Programmes référencés par la map 389

Désassemblage complet dans `docs/intro-programs-389.txt` (linéaire, un bloc par programme, tag
`[implemented]` / `[degraded]` / `[NOT IMPLEMENTED]` par opcode). Aucun programme D (Touch) ni E
(Deactivate) n'est référencé par un record de la 389.

**Slot A (Load)**, 13 programmes (ids 133-145) :

| Id | Offset | Opcodes | Rôle sous New Game |
|---|---|---|---|
| 133 | 132 | 0x17 | blocs 0-5 : gravité faible |
| 134 | 136 | 0xAC, 0xBD, 0x31, 0x64, 0x62, 0x63, 0x1A, 0x2D, 0x00, 0x05 | mouette 6 : flag 860 off → flag 200, anim 10 |
| 135 | 176 | 0xAC, 0x31, 0x64, 0x62, 0x63, 0x1A, 0x00 | mouette 7 : flag off → anim 10 |
| 136 / 137 | 212 / 220 | 0xAC, 0x1A | mouettes 8/9 : anim 10 |
| 138 | 228 | 0x17 | bloc 10 |
| 139 | 232 | 0x1A, 0x31, 0x64 | marin 11 : anim 0 ; flag 860 off → fin |
| 140 | 252 | 0x1A, 0x31, 0x64, 0x09 | marin 12 : anim 0 ; flag off → fin |
| 141 / 142 | 272 / 276 | 0x1A | marins 13/14 : anim 1 |
| 143 | 280 | 0x1A, 0x31, 0x2E | marin 15 : anim 5 ; flag off → fin (flag on : détruit) |
| 144 / 145 | 292 / 296 | 0x1A | marins 16/17 : anim 9 |

**Slot B (Map)**, 7 programmes (ids 129-135, un par map-event) :

| Id | Offset | Opcodes | Rôle |
|---|---|---|---|
| 129 | 300 | 0x30, 0x38, 0x67, 0x10, 0x64, 0x00, 0x37, 0x8B, 0x05, 0x2D, 0x36, 0x11 | **cinématique d'intro** (gardée par flag 860) |
| 130-133 | 400 / 472 / 544 / 616 | 0x55, 0x85, 0x00, 0x3B, 0x04, 0x70, 0x2F, 0x1A, 0x10, 0xBD, 0x37, 0x54, 0x11 | 4 portes : joueur dans la zone → animation de tuiles → contrôle rendu |
| 134 | 688 | 0x00, 0x33, 0x4B, 0x05 | flags 861-868 tous posés → flag 870 |
| 135 | 724 | 0xBD, 0x37, 0x00, 0x49 | boucle d'ambiance (son 44) |

**Slot C (Tick)**, 12 programmes (ids 133-143, 146) :

| Id | Offset | Opcodes | Rôle |
|---|---|---|---|
| 133 | 772 | 0x00, 0x3B, 0x03, 0x02, 0x30, 0x55, 0x54, 0x05, 0x49, 0x31, 0x06 | bloc 5 : bascule la walkability des cellules (17..19, 37..38) selon la présence du joueur dans 15..21×32..40 (flag temporaire 0x8005) |
| 134 / 135 | 868 / 936 | 0x30, 0x09, 0xBD, 0x00, 0x1B, 0x07, 0x04, 0x1A, 0x62, 0x63, 0x37 | mouettes 6/7 : envol/retour (gardé par 860) |
| 136 / 137 | 1004 / 1064 | 0x09, 0xBD, 0x00, 0x1B, 0x07, 0x04, 0x1A, 0x62, 0x63, 0x37 | mouettes 8/9 : envol/retour |
| 138 | 1124 | 0x67, 0x5B, 0x00, 0x07, 0x04, 0x1A, 0x1B, 0x19 | bloc 10 : travelling caméra descendant, puis désactivation |
| 139 | 1168 | 0x30, 0x36, 0x09, 0x37, 0x00, 0x17, 0x1A, 0x1F, 0x5B, 0x1B, 0x07, 0x04, 0x16, 0x5A, 0x05, 0x1E, 0x0A, 0x02 | marin 11 : attend 0x83E8, regarde, saute, marche, pose 0x83EA puis 0x83E9, boucle de marche |
| 140 | 1356 | 0x30, 0x02, 0x00, 0x36, 0x10, 0x59, 0x27, 0x31, 0x0D, 0x39, 0x06, 0x11, 0x50, 0x44, 0x51, 0x03, 0x05, 0x67, 0x5B, 0x1F, 0x1E, 0x2D | marin 12 : sous New Game attend 0x83E9, caméra, marche, active 18 ; bloc dialogue gardé par 0x800C |
| 141 / 142 | 1504 / 1512 | 0x00, 0x1E, 0x0A, 0x49 | marins 13/14 : va-et-vient |
| 143 | 1520 | 0x36, 0x5B, 0x1F, 0xBD, 0x85, 0x37, 0x64, 0x00, 0x2E | marin 15 : attend 0x83EA, trappe (0x85), se détruit |
| 146 | 1608 | 0x67, 0x17, 0x5B, 0x1E, 0x1A, 0x1B, 0x00, 0x70, 0x04, 0x05, 0x2E | bloc 18 : caméra, chute, au sol → flag 860, caméra → joueur, se détruit |

**Slot F (Interact)**, 7 programmes (ids 139-145) : 0x27 Face player, 0x0D Dialog, 0x05 flags 861-868 ;
F 140 (marin 12) pose seulement le flag 0x800C qui déclenche le dialogue dans son Tick.

### B. Fichiers générés

- `docs/intro-trace-389.txt` — trace ordonnée (format `frame | contexte | pc | opcode | kind | params`,
  lignes `frames A-B (×N)` compactées, `SYSTEM` et `SPAWN`).
- `docs/intro-programs-389.txt` — désassemblage statique de tous les programmes ci-dessus.

### C. Entités et map-events de la map 389

Map-events (calque `MapEvents`, 7 records, tous de zone (0,0)-(51,59)) : `EventCodesBIndex` 129..135.

Entités (calque `Entities`, 19 records) :

| Index | Nom | (XPos, YPos, Height) | SpriteDirection | Spawn au chargement (bit 0x40) | A | C | F |
|---|---|---|---|---|---|---|---|
| 0-4 | Bloc transparent | divers | 0x40 | oui | 133 | 0 | 0 |
| 5 | Bloc transparent | (38,74,46) | 0x40 | oui | 133 | 133 | 0 |
| 6 | Marin-passager-mouette | (66,71,0) | 0xC0 | oui | 134 | 134 | 0 |
| 7 | Marin-passager-mouette | (16,71,0) | 0x80 | **non** (0x2D par B1) | 135 | 135 | 0 |
| 8 | Marin-passager-mouette | (56,95,8) | 0x80 | **non** (0x2D par B1) | 136 | 136 | 0 |
| 9 | Marin-passager-mouette | (16,97,8) | 0x80 | **non** (0x2D par B1) | 137 | 137 | 0 |
| 10 | Bloc transparent | (70,70,0) | 0x00 | **non** (0x8B par B1) | 138 | 138 | 0 |
| 11 | Marin-passager-mouette | (38,72,50) | 0xC0 | oui | 139 | 139 | 139 |
| 12 | Marin-passager-mouette | (46,83,10) | 0xC2 | oui | 140 | 140 | 140 |
| 13 | Marin-passager-mouette | (28,42,20) | 0xC3 | oui | 141 | 141 | 141 |
| 14 | Marin-passager-mouette | (26,90,10) | 0xC1 | oui | 142 | 142 | 142 |
| 15 | Marin-passager-mouette (banque 161) | (32,76,16) | 0xC0 | oui | 143 | 143 | 143 |
| 16 | Marin-passager-mouette | (26,96,16) | 0xC0 | oui | 144 | 0 | 144 |
| 17 | Marin-passager-mouette | (34,108,16) | 0xC2 | oui | 145 | 0 | 145 |
| 18 | Bloc transparent | (36,106,20) | 0x00 | **non** (0x2D par le Tick 140) | 0 | 146 | 0 |

Banques : 25 (bloc transparent), 146 et 161 (marins/mouettes) ; toutes avec `Program*` natifs à 0.
