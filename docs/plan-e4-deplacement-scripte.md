# Plan E4 — Déplacement scripté des entités (PNJ sur le mover + navigation)

Date : 2026-08-24. Étape E4 de [plan-conversion-totale.md](plan-conversion-totale.md) (décisions D4/D5).
Patron de travail : [plan-e3-collisions.md](plan-e3-collisions.md) — enveloppe + tranches chiffrées,
plan-verifier avant toute tranche moteur, verifier frais après chaque tranche.

## 0. Décisions de l'utilisateur (2026-08-24, ne pas re-débattre)

| # | Décision |
|---|---|
| E4-1 | **Harnais : cinématique simulée fidèle.** Le harnais d'intro intègre par entité la cinématique de l'original (TargetForce = offset[dir] × Speed, IncrementForce, gravité, sol via le champ `AlundraCellsCollisionField` réel, sans murs). La trace gagne des durées réelles calculables → **nouvel oracle chiffré** ; `docs/intro-trace-389.txt` et la chronologie §0 d'intro-roadmap sont régénérés (la frame finale ne sera plus 926). 0x07/0x70 retirés des prédicats optimistes. |
| E4-2 | **Navigation = chemin seul.** La grille `NavigationGrid2D` (couche `navigation.*` émise par le convertisseur, D5) fournit le CHEMIN via `TryFindPath` ; la marche reste l'intégration 16.16 fidèle (vitesse `AnimSets`) poussée au mover par `Move` — conforme E3-4. Ligne dégagée → marche droite identique à l'original ; obstacle → suivi de waypoints (écart D5 assumé). Le `CharacterControllerNavigationDriverComponent` n'est **pas** utilisé en E4 ; son incompatibilité `TopDownElevation` (intent `(X, −Z)` codé en dur, vitesse des réglages moteur) est consignée comme différé pour E14. |
| E4-3 | **Flag de debug ignorant le verrou 0x10** : réglage DLL jamais actif par défaut, neutralisant `InputBlockedMask` dans `MovePlayer`, livré dans E4 (permet de valider marche/collisions au pad à tout moment de l'intro). |

## 1. Faits qui bornent le plan

Original (`alundra-datas-analyser/AlundraTools/AlundraEngine/`) :

- **Forces scriptées** : `UpdateEntityPhysics` (`PhysicsEngine.cs:1579-1598`) — recalcul uniquement au
  changement de (`AnimationSet.Speed`, `TargetDirection`, `AnimationSet.Acceleration & 0xf`) :
  `TargetForceX/Y = g_offsetXList/YList[TargetDirection] × AnimationSet.Speed`, pas
  `ForceStepX/Y = |TargetForce − Force| >> Acceleration` ; **l'AnimSet est celui de l'anim COURANTE**
  (`entity.AnimationSet`), pas de `TargetAnimationId` (le site de réassignation d'`AnimationSet` est à
  relever en E4.b). `IncrementForce` (`:1551-1576`) rapproche `Force*` de `TargetForce*` par `ForceStep*`
  — déjà porté bit à bit (`Alundra/Scripts/AlundraPlayerManager.cs:282-307`).
- **`ForceAdjusted`** : remis à 0 en tête d'`UpdateEntitiesPhysics` (`PhysicsEngine.cs:17`), posé par
  `ApplyEntityForces` (clamps écran/map, `:1527-1543`) et par la résolution de collision
  (`MoveEntity` → `ComputeXYPosition`). Sémantique consommée par 0x1F : « le mouvement a été raboté ».
- **`IsOnGround`** : `UpdateTileAttributes` (`:1704`) — `(FloorHeight < PosZ) ? 0 : 1`.
- **Verticale** : entités avec `Flags & Gravity` : `ForceZ −= Gravity << 8` par tick (`:1385`, `:1462`),
  clamp `±(ZViscosity << 8)` (`:1393-1400`, `:1469-1472`) ; atterrissage par clamp (`:123-135`) ; les
  entités SANS le flag gardent leur `ForceZ` (vol linéaire) ; branche `IsZForceApplied` (AnimSet) lue
  `:1381-1486` (impulsion verticale portée par l'animation — à trancher en E4.b sur les données réelles
  de l'intro).
- **Handlers** (`Gameplay/Scripts/EntityEventHandlers.cs`) :
  - 0x1E `Script_30_01E` (`:793-829`) : seuil = `(v2<<8|v1)` **pixels** ; 1er passage (signature dans
    `EventProgramState.Parameters[1]`) mémorise `PosX/PosY` dans `Parameters[2..3]` ; suspend (retour 0)
    jusqu'à `|ΔX|>>16 > seuil` **OU** `|ΔY|>>16 > seuil` ; retour 3. Ne pose NI anim NI direction
    (c'est 0x5B/0x5A qui les posent ; la marche vient de la physique permanente).
  - 0x1F `Script_31_01F` (`:832-841`) : délègue à 0x1E ; sort AUSSI si `ForceAdjusted != 0`.
  - 0x1B `Script_27_01B` (`:743-747`) : `ForceZ = (((v2<<8)|v1) × 0x10000) >> 8` (impulsion signée).
  - 0x16/0x17 `Script_22_016`/`Script_23_017` (`:715-726`) : `Flags |= / &= ~Gravity`.
  - 0x07 `Script_7_007` (`:539-582`) : `Result` = au moins une entité matchée par v1 dans la boîte
    tuiles v2..v7 (`TileX/Y/Z`) ; retour 8.
  - 0x70 `Script_112_070` (`:2161-2165`) : `Result = IsOnGround` ; retour 1.
  - 0x5B `Script_91_05B` (`:1713-1733`) : pour chaque match de v1 : `TargetAnimationId = v2`,
    `TargetDirection = ResolveDirectionFromParam(v3)` ; retour 4.
  - 0x5A `Script_90_05A` (`:1694-1710`) : idem sans anim ; retour 3.
  - 0x0A `Script_10_00A` (`:599-603`) : `TargetDirection = (TargetDirection + 0x10) & 0x1f` ; retour 1.
  - 0x27 `Script_39_027` (`:973-978`) : `TargetDirection = GetDirectionToTarget(playerPos − entityPos)`.
  - 0x19 `Script_25_019` (`:729-733`) : `Status = Deactivated` ; retour 1.
  - 0x49 `Script_73_049` (`:1455-1459`) : saut vers `Parameters[0]` (début de programme).
  - 0x4B `Script_75_04B` (`:1476-1487`) : si `Result == 0`, même saut ; sinon retour 1.
  - 0x38 (`:1202-1207`) : `g_saveData.MapIdToInternalMapIndexTable[v1|v2<<8] = v3|v4<<8` ; retour 5.
  - 0x10/0x11 (`:680-693`) : `g_playerControlFlags |= / &= ~ControlLocked`.
- **Directions** : `ResolveDirectionFromParam` (`GameEngine.cs:2325-2382`) — 8 modes par les 3 bits
  hauts (0 direct, 1 relatif à `TargetDirection`, 2 cardinal via `g_cardinalDirectionTable`, 3 vers le
  joueur + offset, **4/5 random** (consomment `Random.Next()` — RNG non porté), 6 direction du joueur
  + offset, 7 direction de warp) ; directions 5 bits (0-31) indexant `g_offsetXList/YList` (déjà
  copiées dans `AnimationTables`). `GetDirectionToTarget` (`ScriptHelper.cs:23+`) : octants par
  `DivTable` + table, port bit à bit possible.
- **Recherche d'entités** : `GetMatchingEntityBySearchType` — porté (`Alundra/Scripts/
  EntitySearchService.cs:93-246`, fonctions 0-11 ; différé connu : 5-11 n'excluent pas le joueur).

Moteur (`CasaEngineMonogame/`, submodule) :

- `CharacterControllerComponent.Move(Vector3)` (`:345-369`) : ignoré seulement en mode `Disabled`
  (`:347`) ; **retourne le déplacement effectif** et mémorise `_lastRequestedDisplacement`/
  `_lastActualDisplacement` — le signal « mouvement raboté » de 0x1F existe sans changement moteur.
  Pas d'API publique de vitesse verticale (seul `RequestJump`, `:282-291`, bufferisé, `JumpSpeed` des
  réglages) → **tranche moteur E4.0**. `Settings` mutable au runtime (clone + validation, `:68-77`).
- `CharacterMotionSystem` (`Framework/Scene/CharacterMotion/CharacterMotionSystem.cs`) :
  `RefreshEntityRegistrations` (`:272-278`) enregistre **tout** `CharacterControllerComponent` du
  monde (possédé ou non) via `_world.Entities` + événements ; `UpdateControllers` (`:245-251`) les
  met à jour en tête de frame — les PNJ à contrôleur sont pilotés sans possession.
- `NavigationGrid2D` (`Framework/AI/Navigation/NavigationGrid2D.cs`) : cellules **carrées**
  (`CellSize` unique, `:19-50`) ; espace monde **X-Z codé en dur** (`GetWorldPosition :129-133`,
  `TryGetCellFromWorld :135-142`) ; `TryCreateFromTileMap` (`:60-96`) : couche repérée par
  `navigation.role = "grid"` (`:9-15`, `:156-169`), grille aux dimensions `MapSize` de la map,
  marchabilité par TUILE du tileset (`TileData.CustomProperties["navigation.walkable"]`, repli
  `CollisionType != Blocked`, `:171-193`) + `navigation.defaultWalkable/defaultCost` sur la couche ;
  `TryFindPath` → `GridPathfinder2D` (A* 4/8 voisins, `AllowDiagonalMovement`,
  `PreventDiagonalCornerCutting`, coût diagonal √2). Conséquence E4-2 : la DLL consomme la grille en
  « espace cellule » (`cellSize = 1`) et fait elle-même px ↔ cellule (24×16) et Y-logique ↔ Z-grille.
- `CharacterControllerNavigationDriverComponent` (`:101-159`) : intent `(toTarget.X, −toTarget.Z)`
  codé en dur (`:128`) + `SetMoveIntent` (vitesse des réglages moteur) → **non utilisé** (E4-2),
  différé E14.
- `TileMapDepthSettings.ShouldRenderTiles` (`Framework/Assets/TileMap/TileMapDepthSettings.cs:64`) :
  une couche `Role = CollisionOnly` (ou `ObjectSource`) n'est **pas rendue** (`TileMapComponent.cs:382,
  :543`) — la couche navigation peut être invisible et sans coût de rendu par frame.

DLL / convertisseur / harnais (ce repo) :

- Dispatch actuel du runner (`Alundra/Scripts/AlundraEventProgramRunner.cs:410-505`) : 0x01-0x06,
  0x09, 0x17, 0x1A, 0x2D, 0x2E, 0x30, 0x31, 0x33, 0x36, 0x37, 0x62-0x65, 0x8B, 0xAC, 0xBD.
  **0x10/0x11 absents** : rien ne pose `AlundraGameState.PlayerControlFlags` (`AlundraGameState.cs:91`)
  aujourd'hui — les gates E2 (`AlundraPlayerManager.cs:122`, `AlundraWorldProxy.cs:1298`) existent mais
  ne sont jamais alimentées. **0x17 est déjà implémenté** (flag seul) — à étendre au pont contrôleur.
  Opcodes inconnus : skip par taille, `Result` inchangé (`:735-761`).
- Harnais (`Alundra.Tests/IntroTraceHarnessTests.cs`) : prédicats optimistes = HashSet
  {0x07, 0x2F, 0x70, 0x39, 0x44, 0x51} (`:682-690`), mutation `State.Result = 1` au trace-hook ;
  régénère `docs/intro-trace-389.txt`/`intro-programs-389.txt` ; arrêts (a) 0x11 sur map-event 0,
  (b) 300 frames sans progression, (c) 3600 frames.
- Proxy (`Alundra/Scripts/AlundraEntityScriptProxy.cs`) : `TargetDirection :85`,
  `TargetAnimationId :84`, `ForceX/Y :98`, `FinalForce* :103`, `IsOnGround :121`, `Flags :82`,
  `AnimSetsByAnim :224` (**héros seul** — posé par `AdoptPlayerPawn`, `AlundraWorldProxy.cs:1095` ;
  `ApplySpawnInitialization :904-968` ne le pose pas), `Controller :186`,
  `MoveControllerAndPullPosition :606-619`, `PushLogicalPositionToRoot :575-588`,
  `ClampToGround :489-543` ; pull racine → `Pos*`/`IsOnGround` en tête d'`Update` (`:317-323`).
- `AlundraPlayerManager` : accumulateur 50 Hz + catch-up 4 (`:190-208`), `RunOneTick :211-278`
  (lookup `AnimSetsByAnim`, `IncrementForce`, route contrôleur), `IncrementForce :282-307` générique.
- Convertisseur : `Writers/SpriteWriter.cs:361-374` — `CharacterControllerComponent` sur le seul
  héros (`bank.IsAlundraBank && bank.Sector5Id == 0`) ; `AnimSets` (Speed/Acceleration/
  IsZForceApplied/Sfx/Flags/Unknown) déjà exportés dans `sprite-records.json` (`:91-97`) — **le
  « gap d'export » listé par plan-conversion-totale §4 E4 est déjà comblé (E2-A)**.
  `CellMetadataWriter.ConvertMap` (`:61-115`) écrit `AlundraCells` en `CustomProperties` de la map.
- Données 389 : 19 records (annexe C d'intro-roadmap), banques 25/146/161 ; programmes C 138/139/140/
  143/146 = les marcheurs/sauteurs de l'intro (annexe A) ; paramètres réels (seuils 0x1E/0x1F, anims
  de marche, impulsions 0x1B, modes de direction v3) à relever dans `docs/intro-programs-389.txt`
  au début de chaque tranche concernée.

## 2. Enveloppe du programme

- **Résultat** : sur la 389, la chronologie de l'intro se déroule au runtime avec des marins/blocs qui
  marchent, sautent et atterrissent à durées réelles (bloc 10 : travelling ; marin 11 : regards, saut,
  marche ; marin 15 : trappe ; marin 12 : marche ; bloc 18 : chute, sol → flag 860) ; au bout, 0x11
  rend la main et le pad déplace Alundra (E2/E3). Le harnais est le nouvel oracle chiffré (E4-1). La
  grille de navigation D5 est émise par le convertisseur et consommée par la DLL (E4-2).
- **Non-objectifs** : IA native (E14) ; caméra (E5 — `0x67` reste no-op compté) ; pont moteur complet
  0x10/0x11 → `PlayerInput.IsInputEnable`/`CharacterControlMode` (E6 — E4 livre le minimum
  `PlayerControlFlags`) ; 0x85/0x55/0x54 (E7) ; dialogue (E12) ; collisions entité-entité
  (`TouchingEntity`/`RidingEntity`/plateformes) ; glissade par attribut de tuile ; RNG fidèle (arrêt
  si un programme de la 389 exerce les modes random 4/5 de `ResolveDirectionFromParam`) ; driver de
  navigation moteur (différé E14) ; correction du différé « EntitySearchService 5-11 n'excluent pas
  le joueur » (inchangé tant que `RidingEntity`/`ParentEntity` ne sont pas peuplés).
- **Propriétaires** : moteur (submodule — E4.0, commit propre + bump), convertisseur + DLL + harnais
  (repo parent). Un seul committeur par repo à la fois. Ne jamais toucher
  `CasaEngineMonogame/CasaEngine.Launcher/Program.cs`.
- **Prérequis** : E3 livré (`ded262d`, moteur `e828affa`) ; checkout standalone mergé.
- **Acceptation globale** : tests des tranches + `dotnet build` solution 0 erreur ; DLL 357+ verts ;
  convertisseur 130+ verts ; moteur sans nouvel échec (18 préexistants sur 1205) ; export complet
  0 erreur, compteurs `Worlds 483`, `Entities.Prefabs 395`, `QuadsRead == QuadsConverted 160355`,
  `Assets.Animation2d 9620` inchangés ; harnais : nouvelle trace à durées réelles, ordre des jalons
  de la chronologie §0 conservé (0x83E8 → 0x83EA → 0x83E9 → 860 → 0x11), frames justifiées par
  calcul ; runtime (utilisateur) : l'intro se joue, les marins bougent, le pad répond après 0x11.
- **Rollback** : une tranche = un commit ; revert (+ pointeur de submodule pour E4.0) ; les assets se
  régénèrent par export.
- **Budget/arrêts** : un commit + un verifier frais par tranche, au plus deux tours de correctifs par
  tranche ; plan-verifier avant la tranche moteur (E4.0) ; arrêts par tranche ci-dessous + arrêt
  global si l'ordre des jalons de la trace change (analyse avant tout ajustement, question à
  l'utilisateur si l'écart n'est pas explicable par une durée réelle).

## 3. Tranches

### E4.0 — Moteur : vitesse verticale scriptable ✅ (moteur a9267735, verifier CONFIRMED)

- **Pourquoi** : 0x1B pose une impulsion verticale signée arbitraire ; le contrôleur n'expose que
  `RequestJump` (`JumpSpeed` des réglages, bufferisé, exige des conditions de saut).
- **Scope (API additive)** : `CharacterControllerComponent.SetVerticalVelocity(float velocityAlongUp)`
  — remplace la composante le long de l'axe `up` de la politique (base d'E3.c) de la vitesse interne,
  laisse h1/h2 inchangées ; ignorée en mode `Disabled` (même gate que `Move`, `:347`) ; utilisable au
  sol comme en l'air : **toute composante montante > 0 fait perdre le sol au prochain `Update`** —
  gate existant `Dot(velocity, up) > 0` en tête d'`UpdateGround`
  (`CharacterControllerComponent.cs:1091-1095`) ; l'API n'ajoute AUCUNE remise à zéro de l'état de
  sol ni modification d'`UpdateGround` (purement additive). Note d'API additive selon
  `.github/copilot-instructions.md`.
- **Acceptation** (tests `CasaEngine.Tests/Physics/`, monde `TopDownElevation` + champ, patron E3.c,
  réglages E3.c — `Gravity 1250`, `MaxFallSpeed 800`, `GroundSnapDistance 4`, dt `1/50`) :
  (1) au sol, `SetVerticalVelocity(+160)` puis `Update(1/50)` répétés → décolle dès le premier
  `Update` (gate `Dot > 0`, bien que 160/50 = 3,2 px < snap 4), monte, retombe, `IsGrounded`
  redevient vrai à la hauteur du sol ; (2) en l'air, `SetVerticalVelocity(−800)` → atteint le sol
  plus tôt qu'en chute libre ; (3) sous Y-up sans champ, la composante Y de la vitesse est
  remplacée, X/Z conservées ; (4) mode `Disabled` → no-op ; tests existants inchangés ;
  `CasaEngine.Tests` sans nouvel échec.
- **Rollback** : revert submodule + pointeur. **Budget** : un commit, ≤ 2 h. **Arrêt** : si la vitesse
  interne du contrôleur n'est pas écrivable proprement le long de `up` sans casser un test existant.

#### Réalisé — écarts (2026-08-24)

- Implémentation conforme au scope (`CharacterControllerComponent.cs:383-392` : gate `Disabled`,
  `ResolveUp()`, `Velocity = Velocity − up·Dot(Velocity, up) + up·v`) ; diff purement additif
  (0 suppression), 4 tests `CharacterControllerSetVerticalVelocityTests.cs` verts, suite moteur
  1210/18 échecs préexistants (mêmes noms). Verifier CONFIRMED avec dérivation indépendante du
  décollage (gate `Dot(velocity, up) > 0` en tête d'`UpdateGround`, `:1113-1117` après ajout).
- **Différés (avis P4 du verifier)** : (1) la branche step-support d'`UpdateGround` (`:1102-1109`)
  tourne AVANT le gate montant et peut avaler une impulsion posée le même tick qu'une montée de
  marche horizontale — préexistant (identique pour `JumpSpeed`), à re-sonder en E4.b/E4.d quand
  impulsion et marche scriptée se combinent ; (2) une entrée non finie (NaN/Inf) empoisonnerait la
  vitesse — le site d'appel DLL (0x1B) convertit un entier 16.16, fini par construction, pas de
  garde moteur ajoutée.

### E4.a — Convertisseur : couche navigation + contrôleurs PNJ ✅ (94a871e, verifier CONFIRMED)

- **Couche navigation (D5)** : chaque `.tileMap` dont la map a des `AlundraCells` gagne une couche
  `Navigation` : `custom_properties` `navigation.role = "grid"`, `navigation.defaultWalkable = "false"` ;
  `Depth.Role = CollisionOnly` (non rendue, `TileMapDepthSettings.cs:64`) ; `tiles[]` = tuile W
  (marchable) ou B (bloquée) par cellule selon `((walkability | ground_property << 8) & M) == 0`,
  **M = 0x40** — **fait relevé le 2026-08-24 (arrêt du pré-check exécuté)** : les quatre marcheurs
  scriptés de l'intro (records 11/12 banque 146, 15 banque 161, 18 banque 25) ont
  `MoreFlags = 0x80` (Collidable seul ; `Flags` complets `0x3A180`/`0x83A180`,
  `data-extracted/data/map_389.json`, banque 25 canonique dans `map_10.json`) — ni ClassB (bit 3)
  ni ClassA (bit 0), donc leur masque mover réel `WalkabilityMaskFor(Flags)` vaut **0x40**. La
  justification initiale du plan (« marins = classe B, M = 0x41 ») était une hypothèse réfutée par
  les données. Sous M = 0x40, seul le bit 6 de `walkability` bloque dans la grille (`gp << 8` ne
  recoupe jamais 0x40) : la grille code les murs universels, les restrictions de classe restent au
  mover par entité. Écart documenté : une entité future ClassA/ClassB aura un masque mover plus
  strict que la grille — la navigation peut proposer un chemin que le mover bloque (le contournement
  0x1E re-navigue sur blocage).
- **Tileset partagé** : un `Navigation.tileSet` (2 `TileData` : `navigation.walkable = "true"/"false"`)
  + texture minimale, ajouté aux `tile_set_asset_ids` de chaque map ; catalogue via
  `EditorAssetCatalogService.Save()`. **Vérification en début de tranche** : ce que
  `TileMapComponent.Initialize`/`LoadTileSets` exige d'une couche `CollisionOnly` au chargement
  runtime (**arrêt** si une texture réelle par tuile est requise et qu'un asset trivial ne suffit pas).
- **Contrôleurs PNJ** : tout prefab avec boîte de corps positive (aujourd'hui héros + ~383 ; les 11
  sprite-only : aucun) reçoit `CharacterControllerComponent` : `Radius = min(largeur, profondeur)/2`,
  `Height = max(hauteur Z de la boîte, 2×Radius)` (valeurs capsule nominales — le sweep Box lit la
  fixture, même logique qu'E3.d), `SkinWidth 0.5`, `StepHeight 3`, `GroundSnapDistance 4`,
  `Gravity 0`, `MaxFallSpeed 0`, `WalkabilityMask 0` (écrasés au runtime par entité),
  `control_mode = "Script"` (`Move` honoré — seul `Disabled` l'ignore, `:347` ; aucun input ne vise
  les PNJ). Le héros garde ses réglages E3.d (`control_mode` défaut `Player`).
- **Non-goals** : aucune consommation runtime (E4.b/E4.d) ; pas de `navigation.cost`/`layers` (V1).
- **Acceptation** : export complet 0 erreur, compteurs historiques inchangés + nouveaux compteurs
  (`Navigation.Layers = 483` si toutes les maps ont des cells — sinon compte réel + compteur de maps
  dégradées ; `Navigation.WalkableCells/BlockedCells` ; `Entities.CharacterControllers`) ; tests
  convertisseur : la couche de la 389 a `navigation.role = grid` et `CollisionOnly` ; formule M = 0x40
  sur cellules synthétiques (`walkability 0x40` → B ; `walkability 1` → W ; `ground_property 128` →
  W — les restrictions de classe ne sont PAS dans la grille) ; cellules réelles de la 389 :
  (18,57) → W, et le compte de cellules B de la 389 == valeur relevée dans `AlundraCells` (assertion
  du compte réel, même si 0 — les murs de la 389 peuvent être des différences de hauteur, jamais
  codées dans la grille, écart déjà documenté) ; un prefab PNJ à corps (banque 146) porte le
  contrôleur avec les valeurs dérivées de SA boîte ; un sprite-only n'en porte pas ; le héros garde
  `Radius 7.5/Height 32` ; round-trip `Entity.Load`.
- **Rollback** : revert + export. **Budget** : un commit, ≤ 1 journée. **Arrêts** : listés ci-dessus.

#### Réalisé — écarts (2026-08-24)

- **Pré-check 1 déclenché puis résolu** : les marcheurs de l'intro ne sont pas ClassB (voir la
  révision M = 0x40 ci-dessus) — plan corrigé avant implémentation.
- **Pré-check 2 (fait)** : `TileMapComponent.InitializeWithWorld` (`:190-210`) construit un `Tile`
  par cellule non vide de TOUTE couche, sans gate sur `Depth.Role` — la couche `CollisionOnly` a
  donc besoin de `TileData` valides + texture chargeable : `Data/Navigation.tileset` (2 tuiles
  Static 24×16) + `Data/Navigation.png` (24×16) partagés, catalogués. `LoadTileSets` exige un
  `TileSize` uniforme : vérifié 24×16 sur les 483 maps.
- **Écart — banque dégénérée** : `alundra_244` (boîte 1×1×1 px → Radius 0,5 == SkinWidth 0,5,
  rejeté par `Validate`) reste sans contrôleur, compté
  (`Entities.CharacterControllersSkippedDegenerateBody 1`) et loggé — 383 contrôleurs émis
  (héros + 382), 11 sprite-only sans.
- **Compteurs** : `Navigation.Layers 483`, `Navigation.TileSets 1`, `WalkableCells 1344209`,
  `BlockedCells 162751` (somme = 483×3120) ; historiques inchangés. Sur la 389 : 0 cellule B
  (les murs du pont sont des différences de hauteur — hors grille, écart documenté).
- **Verifier CONFIRMED** : re-dérivation indépendante des 1 506 960 cellules (0 écart) ; le vrai
  `NavigationGrid2D.TryCreateFromTileMap` exécuté sur les 483 maps exportées → 0 échec, compteurs
  reproduits ; 383/383 réglages relus par `CharacterControllerSettings.Load` ; héros inchangé.
  Avis P4 différé : `TileMapComponent.Initialize` non exécuté end-to-end (GraphicsDevice requis) —
  à couvrir par la validation runtime utilisateur.

### E4.b — DLL : les PNJ bougent sur le mover ⏳ (DLL)

- **Spawn** (`ApplySpawnInitialization`) : `AnimSetsByAnim = header.AnimSets` (même source que le
  héros, `AlundraWorldProxy.cs:1095`) ; cache `Controller`/`RenderProjection` ; APRÈS l'affectation de
  `Flags` : `ApplyGravitySettingsToController()` (nouveau helper : `Settings.Gravity/MaxFallSpeed` =
  valeurs de la map (1250/800 sur la 389, formules E3.d) si `Flags & Gravity`, sinon 0/0) et
  `Settings.WalkabilityMask = WalkabilityMaskFor(Flags)` ; `ClampToGround` au spawn (port du clamp de
  spawn, `EntityManager.cs:127-136` — relire la sémantique exacte en tranche).
- **Mover scripté par frame** (toute entité à contrôleur non-joueur, dans
  `AlundraEntityScriptProxy.Update` après le pull racine) : port d'`UpdateEntityPhysics`
  (`PhysicsEngine.cs:1579-1598`) — recalcul au changement, AnimSet de l'anim courante (relever le
  site de réassignation d'`AnimationSet` dans la décompilation ; à défaut d'équivalent exact,
  utiliser l'anim courante synchronisée et documenter l'écart), `Acceleration & 0xf` ; accumulateur
  50 Hz par entité (même patron/catch-up 4 que le héros) ; par sous-pas : `IncrementForce` (helper
  partagé extrait d'`AlundraPlayerManager.IncrementForce :282-307`, chemin héros inchangé) puis
  `MoveControllerAndPullPosition`. Aucune allocation ni log par frame (état dans le proxy).
- **Ponts verticaux** : 0x1B — stocke `ForceZ` 16.16 et appelle
  `Controller.SetVerticalVelocity(ForceZ × 50f / 65536f)` (sans contrôleur : `ForceZ` seul, consommé
  par le harnais E4.e) ; 0x16/0x17 — `Flags` puis `ApplyGravitySettingsToController()` (0x17 existe
  déjà : étendre, ne pas dupliquer) ; 0x70 — `Result = IsOnGround` (pullé du contrôleur). Trancher
  `IsZForceApplied` sur les données réelles : si les anims de l'intro (mouettes 0x1B, blocs) ont
  `IsZForceApplied != 0`, porter la branche `:1381-1486` ; sinon consigner non-porté avec le fait.
- **Non-goals** : 0x1E/0x1F (E4.d) ; opcodes de flux (E4.c) ; navigation.
- **Acceptation** (patron `AlundraCharacterControllerAdoptionTests` : monde réel 389 + champ + pawn
  banque 146 construit à la main, réglages chargés de l'export réel, auto-skip sans
  `alundra-project/`) : (1) anim de marche + direction posées (équivalent 0x5B) → position après N
  ticks = valeur calculée à la main depuis `Speed`/`Acceleration`/offsets réels (régime transitoire
  ET permanent, comme le scénario E2 208/1) ; (2) impulsion 0x1B réelle du bloc 18 (programme 146) →
  altitude max et frame d'atterrissage calculées, `IsOnGround` 0 → 1 ; (3) `0x17` : l'entité en l'air
  ne tombe pas ; `0x16` : tombe, atterrit au sol du champ réel, vitesse clampée à `MaxFallSpeed` ;
  (4) patron « propriété de la racine » d'E3.d rejoué sur un PNJ (100 frames, écart borné, pas de
  croissance) ; DLL 357+ verts.
  **Note harnais** : le forçage optimiste du harnais ne s'applique qu'aux opcodes `UnknownSkipped`
  (`IntroTraceHarnessTests.cs:719-729`) ; rendre 0x70 `Implemented` le ferait disparaître alors que
  le harnais n'a ni contrôleur ni cinématique (`IsOnGround` = 0 par défaut,
  `AlundraEntityScriptProxy.cs:121`) — l'idiome `0x70 → 0x04` du bloc 18 bouclerait sans fin et la
  trace n'atteindrait plus 926. Cette tranche étend donc la déviation du harnais : le forçage
  `Result = 1` de 0x70 s'applique AUSSI au kind `Implemented` (déviation harnais-seule, documentée,
  retirée en E4.e). Acceptation : la trace s'arrête toujours par la condition (a) (0x11) aux mêmes
  jalons et à la frame 926 ; **arrêt** si un jalon bouge (analyser avant d'ajuster).
- **Rollback** : revert + export si besoin. **Budget** : un commit, ≤ 1,5 journée. **Arrêts** : la
  note harnais ; divergence du pull racine (test 100 frames).

### E4.c — DLL : opcodes de flux, direction et contrôle ⏳ (DLL)

- **Scope** : 0x38 (table `MapIdToInternalMapIndexTable` dans `AlundraGameState` — ajouter le champ
  s'il manque), 0x19, 0x0A, 0x49, 0x4B (patron des sauts existants 0x02/0x03/0x04), 0x27 (port de
  `GetDirectionToTarget` : `DivTable` + table d'octants, `ScriptHelper.cs:23+`, adresse citée),
  0x5A/0x5B (port complet de `ResolveDirectionFromParam`, `GameEngine.cs:2325-2382`, 8 modes ;
  `EntitySearchService` pour v1 ; **relever d'abord les paramètres v3 réels des programmes de la
  389** dans `intro-programs-389.txt` — **arrêt** si un programme atteint les modes 4/5 (random) :
  question RNG à l'utilisateur), 0x07 (boîte tuiles via `EntitySearchService` + `TileX/Y/Z` ;
  ajouter `TileZ = PosZ >> 20` si absent du proxy), 0x10/0x11 (`PlayerControlFlags |= / &= ~
  ControlLocked` — le pont moteur complet reste E6), **flag de debug E4-3** (réglage DLL, ex.
  variable d'environnement lue une fois, jamais actif par défaut, neutralise `InputBlockedMask` dans
  `MovePlayer` ; loggué une fois quand actif).
- **Acceptation** : ≥ 2 tests unitaires par opcode (patron `AlundraEventProgramRunnerTests`, données
  synthétiques + au moins un cas aux paramètres réels de la 389 pour 0x5B/0x27/0x07) ; flag debug :
  un test « verrou posé + flag actif → `MovePlayer` lit le pad » ; harnais : même mécanisme qu'E4.b —
  le forçage `Result = 1` de 0x07 est étendu au kind `Implemented` (les entités ne bougent pas avant
  E4.e, et 0x07 réel rendrait `Result` faux pour les programmes 134-139/138 qui l'utilisent en
  `0x07 → 0x04`) ; 0x5A/0x5B/0x0A/0x27 posent direction/anim sans mouvement (aucune cinématique
  harnais avant E4.e) ; trace régénérée, jalons et frame finale identiques (926) ; **arrêt** si un
  jalon bouge ; DLL verts.
- **Rollback** : revert. **Budget** : un commit, ≤ 1 journée.

### E4.d — DLL : marche naviguée 0x1E/0x1F ⏳ (DLL)

- **Grille** : au `InitializeWithWorld`, `NavigationGrid2D.TryCreateFromTileMap(tileMapData,
  tileSets, cellSize: 1f)` (tilesets résolus comme le `TileMapComponent` les charge) → grille 52×60
  en « espace cellule » ; adaptation DLL documentée : cellule = `(x/24, y/16)` clampée comme le
  champ ; monde-grille = `(cx+0.5, 0, cy+0.5)` avec **Y-logique ↔ Z-grille** (le driver moteur n'est
  pas utilisé, E4-2). Absence de couche/grille → mode dégradé (marche brute, un avertissement).
- **0x1E** : port fidèle (signature `Parameters[1]`, position mémorisée `[2..3]`, seuil px, fin
  `|ΔX| OU |ΔY| > seuil`, retours 0/3). Par défaut la marche libre continue (aucune intervention).
  **Contournement D5** : si, pendant un 0x1E actif, le dernier sous-pas a été raboté (`Move` retourné
  < demandé au-delà d'un epsilon), calculer une destination projetée = position mémorisée +
  `(signe(offsetX[dir]) , signe(offsetY[dir])) × (seuil + marge)` , `TryFindPath`, puis re-dériver
  `TargetDirection` par tick vers le waypoint courant (`GetDirectionToTarget`) jusqu'à la fin fidèle
  (distance). Ligne dégagée → comportement identique à l'original.
- **0x1F** : 0x1E **+ fin si mouvement raboté** (équivalent `ForceAdjusted` — D5 : « fin = distance
  atteinte ou collision ») ; PAS de contournement pour 0x1F (l'original sort, la navigation ne
  s'applique qu'à 0x1E). Écart documenté : l'original teste le `ForceAdjusted` de la frame, la DLL
  teste le dernier sous-pas.
- **Acceptation** : (1) 0x1F réel du marin 11 (programme 139 — relever seuil/direction) sur la 389 :
  fin à la frame calculée (distance/vitesse AnimSet réels) ; (2) 0x1E avec obstacle (cellules
  bloquées réelles ou monde synthétique) : contournement par waypoints, fin à distance atteinte —
  échoue sans la grille ; (3) 0x1F contre un mur : fin anticipée ; (4) grille : une cellule
  `walkability 0x40` (réelle si la 389 en a, sinon synthétique) est non marchable, (18,57) l'est ;
  mode dégradé sans
  couche ; harnais : jalons inchangés. **Politique harnais temporaire** : sans cinématique (E4.e pas
  encore livrée), les positions du harnais n'évoluent pas et un 0x1E/0x1F réel y suspendrait sans
  fin ; cette tranche ajoute donc au harnais une déviation documentée « fin immédiate de 0x1E/0x1F »
  — même mécanisme de déviation « kind `Implemented` » qu'E4.b/E4.c (0x70/0x07), harnais-seul,
  retirée en E4.e — **arrêt** si ce n'est pas isolable proprement du chemin de production.
- **Rollback** : revert. **Budget** : un commit, ≤ 1 journée. **Arrêts** : `TryCreateFromTileMap`
  rejette la couche émise ; politique harnais temporaire non isolable.

### E4.e — Harnais : cinématique simulée fidèle + nouvel oracle ⏳ (harnais + docs)

- **Mini-cinématique** (E4-1) : par frame, pour chaque entité du harnais : mêmes helpers partagés
  qu'E4.b (recalcul des forces, `IncrementForce`, intégration `Pos* += Force`), verticale fidèle
  (`ForceZ −= Gravity << 8` si `Flags & Gravity`, clamp `±ZViscosity << 8`, impulsions 0x1B), sol via
  `AlundraCellsCollisionField` réel (4 coins de la boîte du header, max, `IsOnGround = sol ≥ PosZ`,
  atterrissage par clamp) ; **sans murs ni navigation** (déviation documentée : les trajets de
  l'intro sont dégagés). Aucune duplication : les helpers viennent d'E4.b.
- **Retraits** : 0x07/0x70 quittent le HashSet optimiste (`:682-690`) ET les déviations
  « kind `Implemented` » ajoutées par E4.b/E4.c/E4.d (0x70, 0x07, fin immédiate de 0x1E/0x1F) sont
  retirées — la cinématique simulée rend leurs valeurs réelles ; 0x2F/0x39/0x44/0x51 restent
  optimistes (dialogue/portes, hors périmètre, jamais atteints sous New Game pour 0x2F).
- **Nouvel oracle** : trace régénérée ; le test d'arrêt vérifie l'ORDRE des jalons (0x83E8 → 0x83EA →
  0x83E9 → 860 → 0x11) et leurs NOUVELLES frames, chacune justifiée par un calcul à la main
  (distances des programmes / vitesses AnimSets / gravité) consigné en commentaire du test ;
  `docs/intro-trace-389.txt`/`intro-programs-389.txt` régénérés ; `intro-roadmap.md` §0 mis à jour
  (nouvelle chronologie, l'ancienne conservée en note « durées nulles ») ; ce plan et
  `plan-conversion-totale.md` §6 mis à jour.
- **Acceptation** : ordre des jalons conservé ; chaque frame de jalon = valeur calculée (tolérance
  ±1 frame par jalon, chaque tolérance expliquée) ; plafond 3600 frames non atteint ; DLL 357+ et
  convertisseur 130+ verts ; export complet inchangé.
- **Rollback** : revert. **Budget** : un commit, ≤ 1,5 journée. **Arrêts** : ordre des jalons
  différent (analyse, puis question à l'utilisateur si l'écart n'est pas une durée réelle
  explicable) ; une marche qui ne finit jamais (bug de seuil/vitesse — analyser, ne pas masquer).

## 4. Ordre et dépendances

E4.0 (moteur, plan-verifier) → E4.a (convertisseur) → E4.b → E4.c → E4.d → E4.e. E4.c ne dépend que
d'E4.b pour la stabilité des jalons du harnais ; E4.d dépend d'E4.a (couche) et d'E4.b (mover) ;
E4.e dépend de tout. Après le commit moteur E4.0 : bump du pointeur + rappel du fetch/merge du
checkout standalone. Validation par tranche : builds/tests/export selon les règles du chantier
(export complet à chaque tranche convertisseur ; `Alundra/Alundra.csproj` rebuild après export).

## 5. Suivi

| Tranche | Statut | Commit |
|---|---|---|
| E4.0 vitesse verticale moteur | ✅ (verifier CONFIRMED) | moteur a9267735 |
| E4.a couche navigation + contrôleurs PNJ | ✅ (verifier CONFIRMED) | 94a871e |
| E4.b PNJ sur le mover | ⏳ | |
| E4.c opcodes flux/direction/contrôle + debug 0x10 | ⏳ | |
| E4.d marche naviguée 0x1E/0x1F | ⏳ | |
| E4.e harnais cinématique + nouvel oracle | ⏳ | |
