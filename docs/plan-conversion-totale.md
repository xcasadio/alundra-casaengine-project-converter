# Plan de conversion totale d'Alundra vers CasaEngine

Date : 2026-08-23. **Ce document remplace l'ordre des chantiers de
[intro-roadmap.md](intro-roadmap.md) §4 et les étapes E3–E6 de
[demarrage-nouvelle-partie.md](demarrage-nouvelle-partie.md) §5.** Les faits de la décompilation
consignés dans ces documents (et dans `intro-roadmap.md` §1–§3) restent l'autorité de fidélité.

## 1. Objectif et posture

Convertir **totalement** le jeu dans un format moderne : les données d'Alundra sont converties en
assets CasaEngine et le gameplay s'exécute sur les **systèmes du moteur** — pipeline graphique (que
l'on fait évoluer), moteur physique, moteur de navigation, Yarn Spinner, moteur UI (MGUI), et plus
tard le moteur de particules.

Le jeu original n'est pas conçu ainsi : la conversion **créera des bugs**. La méthode est de
procéder par **petites étapes**, chacune exerçant **un composant moteur et une conversion de
données**, testable seule, pour trouver étape par étape les stratégies de conversion. Un écart de
comportement est acceptable s'il est documenté (fait / hypothèse / écart) et s'il vient de l'usage
d'un système moteur à la place du système original ; un écart qui vient d'une donnée mal convertie
ou d'un défaut moteur se corrige à la source (convertisseur ou moteur), jamais par un contournement
dans la DLL.

## 2. Décisions actées le 2026-08-23 (ne pas re-débattre)

| # | Décision | Conséquence |
|---|---|---|
| D1 | **Hybride** : le bytecode (slots A–F, MapEvents) est **interprété au runtime** par `AlundraEventProgramRunner`, étendu opcode par opcode en appelant les systèmes du moteur ; les programmes simples sont **convertis progressivement** en assets moteur (dialogues → Yarn, cinématiques → `.cutscene`) quand le composant est prêt. | L'interpréteur reste le cœur ; chaque opcode porté devient un pont vers un système moteur. |
| D2 | **Scripts joués par chaque entité** : `AlundraEntityScriptProxy.Update` choisit et exécute son propre slot (A–F) avec le runner partagé du world, dans l'ordre d'update du moteur. Le world ne s'occupe que des **MapEvents** (programmes B sur l'entité joueur) et des services partagés. | Inversion de la décision « machine d'états dans le world » de 2026-08-20. |
| D3 | **Ré-exécution conservée** : l'original rejoue dans la même frame une entité déclenchée par une autre (`do/while` d'`UpdateEntitiesEvents`). Le world garde une **boucle de rattrapage** après l'update des entités pour rejouer immédiatement celles dont `EventTrigger` a été posé pendant la frame. | Le world conserve une vue de toutes les entités scriptées. |
| D4 | **Tout sur le moteur physique** : le héros est un pawn possédé (`PlayerController` + `CharacterControllerComponent`, `MovePlayer` réécrit sur le moteur) ; toutes les entités bougent via le `CharacterControllerComponent` et un `HeightGridCollisionField` construit depuis `AlundraCells`. `PhysicsEngine.cs` **n'est pas porté**. Seules les vitesses/accélérations (`AnimSets.Speed/Acceleration`, à exporter) alimentent le contrôleur. | Chantier moteur : mover conscient de la politique d'espace (`TopDownElevation`), prérequis listés dans `CasaEngineMonogame/docs/engine/character-controller-features.md`. |
| D5 | **Navigation pour la marche scriptée aussi** : 0x1E/0x1F deviennent des `MoveTo` vers la destination (position + direction × distance) résolus par la grille de navigation construite depuis `AlundraCells` ; les PNJ contournent les obstacles (écart assumé). La navigation servira aussi à l'IA native plus tard. | Convertisseur : propriétés `navigation.*` sur une couche TileMap. |
| D6 | **Dialogues : un nœud Yarn par chaîne** : un `.yarn` par map, un nœud par id de chaîne ; 0x0D démarre le nœud, 0x5C expose les choix Yarn. Pas de reconstruction de séquences depuis le bytecode. | Convertisseur : `strings.json` → `.yarn` (+ `DialogueAsset` compilé). |
| D7 | **Particules : aucune conversion pour l'instant.** | Pas d'étape particules dans ce plan. |
| D8 | **Pipeline graphique — vers le moteur** : profondeur murs/sols (overlay DLL → `TileMapComponent`/tri moteur), backdrops/parallaxe/ondes (`BackdropRenderer` DLL + `ScrollParameters` → composant moteur), fondu/teinte/transitions (→ post-process ou composant de transition moteur). Le palette swap reste hors plan. | Trois chantiers moteur, chacun avec plan-verifier. **Bilan 2026-09-06** : la profondeur murs/sols (E8) est arrivée par `AddSortedOverlayTile` pendant E9, et non par un déplacement de code hors de la DLL — voir E8 pour la frontière retenue (D-E8-1). |
| D9 | **UI en MGUI** : vues XML + `font3.fnt` (FontStashSharp) ; le `DialogueService`/Yarn y branche sa boîte et ses choix. | Moteur : les vues de dialogue (`DialogueBoxView`, `ChoiceListView`) sont encore planifiées (⏳). |

## 3. Architecture runtime cible

### 3.1 Répartition des responsabilités

```
World (.world) — AlundraWorldProxy
  OnBeginPlay : lit le tilemap (records, AlundraCells, MapEvents), construit les services partagés
               (AlundraGameState, AlundraEventProgramRunner, EntitySearchService, IEntityWorldContext,
               HeightGridCollisionField, grille de navigation), crée le héros (port de ResetEntityState)
               puis spawne les records (gates de SpawnEntity : zone joueur + SpriteDirection & 0x40)
  Update     : (1) MapEvents — port de RunMapEvents : programmes B sur l'entité joueur, un
                   EventProgramState par map-event
               (2) boucle de rattrapage (D3) : rejoue les entités dont EventTrigger a été posé
                   pendant la frame, jusqu'à stabilité (miroir du do/while original)
               (3) suivi caméra (g_entityFollowedByCamera → CameraTargeted2dComponent.Target)
               (4) services de frame (compteurs, flags de contrôle)

Entité scriptée — AlundraEntityScriptProxy (prefab par banque + CharacterControllerComponent)
  Update     : pick du slot (A Load / C Tick / D Touch / E Deactivate / F Interact — port de la
               phase « pick » d'UpdateEntitiesEvents:806-870) puis RunScript(slot) ou RunSpriteEvent ;
               reprise B/C sur son EventProgramState ; les opcodes de mouvement pilotent le
               CharacterControllerComponent (SetMoveIntent / MoveTo / impulsion Z)

Héros — pawn possédé par AlundraPlayerController (PlayerController) + CharacterControllerComponent
  Update     : MovePlayer réécrit : lecture des intentions (.buttonsMapping), gates
               g_playerControlFlags (InputBlockedMask), animation ; il exécute aussi les programmes B
               des MapEvents (le world lui confie l'EventProgramState du map-event)
```

FAIT (`CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:443-491`) : le moteur met à
jour **les entités d'abord** (`Entity.Update` → composants, enfants, `GameplayProxy.Update`) puis le
`GameplayProxy` du world. Les MapEvents tournent donc en fin de frame N, avant les entités de la
frame N+1 : même ordre relatif que l'original (`RunMapEvents` → `UpdateEntities`), à la première
frame près (écart documenté).

### 3.2 Correspondance systèmes originaux → systèmes moteur

| Système original (décompilation) | Système moteur | Étape |
|---|---|---|
| `UpdateEntitiesEvents` pick/run + do/while | `AlundraEntityScriptProxy.Update` + boucle de rattrapage du world | E1 |
| `RunMapEvents` | `AlundraWorldProxy.Update` (MapEvents) | E1 |
| `ResetEntityState`, `PlayerManager.MovePlayer` | pawn + `PlayerController` + `CharacterControllerComponent` | E2 |
| `PhysicsEngine` (forces, gravité, sol, murs) | `CharacterControllerComponent` + `HeightGridCollisionField` (`AlundraCells`) + `SimulationSpacePolicy.TopDownElevation` | E3 |
| 0x1E/0x1F/0x5B/0x5A/0x1B/0x16/0x17 (déplacement scripté) | `MoveTo` (navigation) / `SetMoveIntent` / impulsion verticale du contrôleur ; vitesse = `AnimSets.Speed` | E4 |
| `g_entityFollowedByCamera`, 0x67/0x68/0x69, `InitializeScrollingMode` | `CameraTargeted2dComponent` (Target = entité, dead zone, limites) | E5 |
| `g_playerControlFlags`, 0x10/0x11 | `PlayerInput.IsInputEnable` / `CharacterControlMode` (Script/Cutscene) | E6 |
| `ChangeAreaTileProperties` (0x85), 0x55/0x54 | API moteur de mutation de tuiles + reconstruction collision/navigation | E7 |
| `WallPlacementOverlay` + interleave (DLL) | `TileMapComponent` / tri de profondeur moteur | E8 |
| `BackdropRenderer` (DLL), `ScrollParameters` | composant moteur de couches défilantes | E9 |
| `WarpPlayer` (fondu), teinte plein écran | post-process / composant de transition moteur | E10 |
| `LoadMapSounds`, 0xBD, `HandleMapSoundStreaming` | audio moteur (BGM/SFX exportés en Phase 4) | E11 |
| 0x0D/0x39/0x44/0x50/0x51/0x5C, `UIManager` boîte de message | `YarnDialogueRunner` + `DialogueService` + vues MGUI | E12 |
| HUD (`HudManager`) | vues MGUI | E13 |
| `SpriteEventHandlers` (IA native) | navigation (steering/poursuite) + scripts C# par type de sprite | E14 |
| Programme B 129 (cinématique) | `.cutscene` (`CutsceneDirector`) — conversion hybride (D1) | E15 |

## 4. Étapes

Chaque étape : **un commit**, **un verifier frais**, `plan-verifier` avant toute étape marquée
« moteur ». Les étapes sont petites et indépendantes autant que possible ; l'ordre ci-dessous est
l'ordre conseillé, les dépendances sont explicites. Statuts : ⏳ à faire · 🚧 en cours · ✅ fait ·
🧪 fait, non vérifié · ⚠️ bloqué.

Oracle transverse : le harnais headless `Alundra.Tests/IntroTraceHarnessTests.cs` (chronologie
`intro-roadmap.md` §0 : flags 0x83E8 → 0x83EA → 0x83E9 → 860 → 0x11). Il doit être adapté à chaque
étape qui change l'architecture (E1) et sert ensuite de non-régression. **Depuis E4 (décision E4-1),
le harnais simule la cinématique fidèle : l'oracle est à durées réelles, 0x11 à la frame 1704**
(jalons 554/1034/1202/1704 — voir `plan-e4-deplacement-scripte.md` E4.f et la table §0
d'intro-roadmap).

### E1 — Scripts par entité, MapEvents dans le world ✅ (DLL — verifier CONFIRMED ; validé en jeu par l'utilisateur le 2026-09-07)

- **But** : appliquer D2 et D3 sans changer le comportement observable sur la map 389.
- **Contenu** :
  - `AlundraEntityScriptProxy.Update` : port de la phase « pick » (`EntityManager.cs:806-870`) pour
    sa propre entité, puis `RunScript(slot)` / `RunSpriteEvent` ; reprise B/C et `g_clearProgramState`
    (`EntityEventHandlers.cs:232-392`) dans le runner.
  - **Entité joueur minimale** (validé le 2026-08-23) : le world spawne le prefab
    `Entities/Alundra/Alundra.entity` (script `AlundraEntityScriptProxy`, 376 animations
    `bankalundra_0_anim{N}_{dir}`) comme slot 0, **avant** les records (les gates de spawn lisent sa
    tuile), à la position New Game `(33×24+12, 59×16+8) << 16`, Z 0, animation 54 direction 0 (bas),
    `Status = Normal` — port minimal de `ResetEntityState` (`GameEngine.cs:648-670`). Il est exclu du
    pick/run (l'original boucle de 1..n) et sert d'exécutant aux MapEvents. Pas de contrôleur, pas
    d'input, pas de caméra (E2, E5, E6).
  - `AlundraWorldProxy.Update` : port de `RunMapEvents` (`GameEngine.cs:1667-1718`, un
    `EventProgramState` par map-event, exécution sur l'entité joueur) ; boucle de rattrapage (D3) :
    après l'update des entités, rejouer celles dont `EventTrigger` a été posé pendant la frame,
    jusqu'à stabilité (miroir de la phase 2 d'`UpdateEntitiesEvents`) ; retrait de
    `RunEntityEventsPass` du chemin de production.
  - Spawn dynamique réel (`IEntityWorldContext.SpawnEntityByRecordId` déjà présent) ; gates de
    spawn au chargement déjà portées (`ShouldSpawnRecord`).
  - Harnais adapté à la nouvelle architecture.
- **Acceptation** (validée le 2026-08-23) : harnais adapté → même chronologie de flags et 0x11 à la
  même frame (926) ou écart expliqué par l'ordre d'update ; `dotnet test Alundra.Tests` vert ; map 389
  au runtime : Loads joués comme avant, héros visible en (33,59) avec l'animation 54 vers le bas, rien
  de cassé. En production les prédicats non portés (0x07/0x70…) laissent `Result` inchangé : la chaîne
  de l'intro s'arrête au bloc 18 jusqu'à E4 — écart attendu, pas un bug d'E1.
  **Correction datée (2026-09-07)** : E4 est désormais livré et clos, donc l'arrêt au bloc 18 n'est
  plus le comportement attendu — l'intro se déroule maintenant jusqu'au bout, y compris la restitution
  du contrôle après le bloc 18. Un arrêt au bloc 18 serait aujourd'hui une régression. Confirmé en jeu
  le 2026-09-07 (New Game sur la map 389, chronologie complète jusqu'à la restitution du contrôle).
- **Dépendances** : aucune. **Prochaine étape à lancer après validation.**

**Réalisé — écarts (2026-08-23)** :

- **Ordre par entité** : `AlundraEntityScriptProxy.Update` fait pick → run → sync animation → sync
  transform pour lui-même ; le world ne fait plus que MapEvents (`RunMapEventsPass`) puis la boucle de
  rattrapage (`RunPendingEventTriggers`), après que le moteur a déjà mis à jour toutes les entités
  (`World.Update`). Conséquence : une entité plus loin dans l'ordre d'itération voit les effets du script
  d'une entité plus tôt dans l'ordre *dans la même frame* (c'était déjà vrai dans l'ancien
  `RunEntityEventsPass`, qui itérait aussi séquentiellement) ; ce qui change, c'est que les MapEvents
  tournent maintenant *après* les entités au lieu d'avant, d'où un décalage d'environ 1 frame sur les
  évènements pilotés par un flag que B1 pose (0x83EA à 705 au lieu de 704, 0x83E9 à 783 au lieu de 782,
  flag 860 à 786 au lieu de 785, spawn du bloc 18 à 783 au lieu de 785) — la frame du `0x11` final reste
  identique (926), ainsi que les 3 spawns pilotés directement par B1 (7/8/9 à 555/678/801, inchangés) et
  le flag 0x83E8 (554, inchangé, posé par B1 lui-même sans dépendre d'une entité).
- **Latence d'une frame documentée** : tout ce que la passe monde change (déplacement du joueur par 0x64
  dans un MapEvent, entité rattrapée par `RunPendingEventTriggers`) n'est visible aux entités qu'à partir
  de leur prochaine frame de sync — conforme à la doc de `AlundraEntityScriptProxy.Update`.
- **`EventTrigger` initialisé à `ProgramUnknown`** : nécessaire (pas dans la décompilation, jamais mis à
  zéro explicitement par l'original) parce qu'un `AlundraEntityScriptProxy` fraîchement construit a
  `EventTrigger = 0` (= `ProgramALoad`) par défaut C#, et la nouvelle boucle de rattrapage
  (`RunPendingEventTriggers`) peut désormais voir une entité spawnée *dans la même frame* par les
  MapEvents (0x2D/0x8B) avant la fin de la frame — sans ce correctif elle aurait fait tourner son
  programme A sans jamais passer par `PickEventTrigger` (donc sans transition Loaded → Normal). Ajouté
  dans `AlundraWorldProxy.ApplyRecord`/`SpawnPlayerEntity`.
- **`LogicEntity` distinct de `LogicContextEntity`** : le champ original `Entity.LogicContextEntity` (type
  original = l'équivalent de `AlundraEntityScriptProxy`, utilisé par `RunMapEventsPass` pour cibler
  l'entité logique du map-event) n'a **pas** été fusionné avec le `LogicContextEntity` existant du proxy
  (qui est un pointeur moteur vers sa propre `Entity` CasaEngine, posé une fois au spawn) — un nouveau
  champ `AlundraEntityScriptProxy.LogicEntity` porte la sémantique de l'original.
- **`g_clearProgramState`** : mécanisme ajouté (`AlundraEventProgramRunner.ClearProgramStateRequested`)
  mais aucun opcode porté ne le positionne encore (seul 0x40, non porté, le ferait) ; simplification
  documentée : l'original re-teste le flag après *chaque* opcode et distingue nettoyer l'état de l'entité
  en cours d'exécution de celui d'une autre entité ciblée — ce port ne re-teste qu'une fois, après le
  retour de `RunOneScriptCall`, et ne nettoie que l'état de l'appel en cours.
- **Entité joueur minimale** : spawnée par nom de catalogue `"Alundra"` (résolu via
  `AssetCatalog.Get("Alundra").Id`, le même id que `SpriteRecordCatalog.TryGet` utilise pour son en-tête
  sprite-records.json) plutôt que par un `PrefabAssetId` de record — il n'y a pas de record `Entities`
  pour le héros. `EntityRefId = -1` (pas un slot de la table de records).
- **Gate de spawn joueur** : `ShouldSpawnRecord` gagne une surcharge à 5 arguments (tuile joueur) utilisée
  uniquement quand `PlayerEntity != null` ; la boîte XMin/XMax/YMin/YMax n'a aucun effet observable sur la
  389 (les 7 records couvrent toute la map), donc le compte de spawn au chargement reste 14.
- **Risque connu (P4, différé, non corrigé ici)** : le pick/run par entité dépend maintenant de la
  `TickPolicy` du moteur (`World.cs:456 ShouldUpdateThisFrame`) — un prefab qui ne porte qu'un
  `AnimatedSpriteComponent` (pas de composant physique/collision) résout en `TickPolicy.Conditional`,
  gardée par `CurrentAnimation != null && UpdateAnimatedSprites`, ce qui pourrait sauter l'`Update` de
  l'entité (donc son pick/run de script) certaines frames. Sur la 389, les 14 prefabs chargés et le héros
  portent tous un `CollisionComponent` (`TickPolicy.EveryFrame`), donc ce risque ne s'exprime pas ici ;
  mais 12 des 396 prefabs convertis au total n'ont aucun composant collision/physique et tomberaient dans
  ce cas — à vérifier avant de généraliser au-delà de la 389.

- **Différés après la revue de clôture (verifier CONFIRMED)** : N1 (P3) — `EntitySearchService` n'exclut le
  joueur que pour la fonction 3 ; l'original l'exclut aussi dans la branche par id et les fonctions 5–11
  (`GameEngine.cs:1942, 2010-2091`, boucles depuis le slot 1). Inatteignable tant que le joueur n'a ni
  `EntityRefId` ≥ 0 ni `RidingEntity`/`ParentEntity`/etc. (E2) — **à corriger en E2**. N2 (P4) — citations
  `GameInitializer.cs:363-367` à décaler de +4 dans `AlundraGameState`, et `FillDataFromCommand` ne remet pas à
  zéro `[1..9]` sur le chemin de fin de programme (inobservable : `RunOneScriptCall` sort sur 0xFF).

### E2 — Héros : pawn possédé ✅ (convertisseur + DLL — verifier CONFIRMED ; validé en jeu par l'utilisateur le 2026-09-07)

- **But** : le héros existe comme pawn du moteur, visible en (33,59) avec l'animation 54 vers le bas.
- **Contenu** : convertisseur — `.gameMode` (`default_pawn_asset_id` = `Entities/Alundra.entity`,
  `player_controller_class = "AlundraPlayerController"`), `.buttonsMapping` Alundra (actions
  Move/Action/Attack/Jump/Menu), `PlayerStart` déjà émis ; DLL — `AlundraPlayerController`,
  `AlundraGameState` complet (port d'`InitializeGameState`, `GameInitializer.cs:331-436`), port de
  `ResetEntityState` (`GameEngine.cs:648-670`) sur le pawn, `MovePlayer` minimal (gates
  `InputBlockedMask`, `PlayerManager.cs:17-60`) ; l'entité joueur devient l'exécutant des MapEvents.
- **Acceptation** : New Game → map 389, héros visible au bon endroit ; le pad le déplace (sans
  collision) ; l'export complet reste à 0 erreur.
- **Dépendances** : E1.
- **Réalisé (convertisseur, E2-A, 2026-08-23)** — trois assets, ids déterministes (`Ids.For`, donc
  stables d'un run à l'autre) car `PlayerStartupSettings`/`ButtonsMapping` héritent d'`ObjectBase`
  dont `Id` a un setter privé (même contournement que la caméra/les worlds de `WorldWriter` : JSON
  écrit à la main puis `EditorAssetWriterService.SaveDocument`) :
  - `Entities/Alundra/Alundra.gameMode` (catalogue `"AlundraPlayer"`, id `Ids.For("gameMode:alundra")`,
    `default_pawn_asset_id` = id de `Entities/Alundra/Alundra.entity` (résolu via la clé de banque
    `"alundra_0"` retournée par `SpriteWriter.ConvertSprites`), `player_controller_class` =
    `"AlundraPlayerController"`, `ai_controller_class`/`hud_class` aux valeurs par défaut du moteur).
  - `Data/Alundra.buttonsMapping` (catalogue `"AlundraButtons"`, id
    `Ids.For("buttonsMapping:alundra")`) : comme le `.gameMode` ci-dessus, écrit à la main en JObject
    brut (pas via les classes moteur `ButtonsMapping`/`InputMapping` — `ObjectBase.Id` a un setter privé
    donc `EditorAssetWriterService.SaveAsset` ne peut jamais leur donner un id déterministe), au format
    exact que lit `ButtonsMapping.Load`/`InputMapping.Load`/`KeyButton.Load`, puis
    `EditorAssetWriterService.SaveDocument` (voir `PlayerSetupWriter`'s propre doc de classe) : 9 actions
    (`MoveUp/Down/Left/Right`, `Jump`, `Attack`, `UseItem`, `Sprint`,
    `Menu`), clavier flèches/Espace/X/C/Maj-gauche/Échap + alternative manette (D-pad pour le
    déplacement — un seul emplacement `alternative_key_button` existe, donc pas de place pour le
    stick gauche en plus ; PSX→pad : Cross→A, Square→X, Circle→B, Triangle→Y, cf.
    `Alundra/Scripts/PlayerManager.cs:413/549/964/1904/430`).
  - Les 483 `.world` référencent désormais tous le même `player_startup_settings_asset_id` (celui du
    `.gameMode` ci-dessus), passé à `WorldWriter.ConvertWorlds` en paramètre plutôt que recalculé
    (Phase 6 tourne après Phase 3, qui seule connaît l'id du prefab héros).
  - `Data/sprite-records.json` gagne un tableau `"AnimSets"` par prefab (un élément par index
    d'AnimSet déclaré dans le record source, dans l'ordre) :
    `{ "Anim": <index>, "Speed": <int>, "Acceleration": <int>, "IsZForceApplied": <int>, "Sfx": <int>,
    "Flags": <int>, "Unknown": <int> }`, lu par `SpriteBankReader.ReadAnimSetHeader` depuis
    `SpriteInfo.SpriteRecords[].AnimSets[]` (extracteur), pour que la vitesse de marche du héros
    vienne des données originales plutôt que d'être inventée (`AnimSets[54].Speed = 0,
    Acceleration = 64` ; `AnimSets[1].Speed = 208, Acceleration = 1`, vérifiés sur data-extracted).
    Compteur `SpriteRecords.AnimSetsExported` (2405 sur le corpus complet).
- **Réalisé (DLL, E2-B, 2026-08-23) — écarts** : le moteur (pas le convertisseur) spawn et possède
  désormais le pawn héros (`World.LoadContent` → `InitializePlayerControllers`) ; `AlundraWorldProxy`
  n'en spawn plus un second — `AdoptPlayerPawn` (remplace `SpawnPlayerEntity`) retrouve le contrôleur via
  `world.PlayerControllers.OfType<AlundraPlayerController>()` et applique l'état logique New Game
  (position/anim/direction) au pawn déjà en place. Déplacement **logique uniquement** (décision
  utilisateur du jour même) : pas de `CharacterControllerComponent`, pas de gravité, pas de collision
  avant E3 — seul `AlundraPlayerManager.Tick` (intégration cinématique 16.16 à pas fixe 50 Hz, catch-up
  plafonné à 4 ticks/frame) déplace `PosX`/`PosY`.
  - **Porté** : `PlayerManager.MovePlayer` (PlayerManager.cs:17-951) restreint à `BlockedByEntity` (31-36),
    la branche verrouillée `InputBlockedMask` (38-57, no-op documenté), l'en-tête libre `Flags |= Gravity`
    (59-60), la résolution pad→direction (`g_directionByButtons`, 199-205), le cas Idle(0x00)/Moving(0x01)
    simplifié (361-383 : direction + bascule Idle/Moving, sans `TryUseItem`/`PlayerTryAction`/
    `CheckEntityInteraction`/`PlayerTryAttack`) et la bascule LoadingMap(0x36) (914-922, **écart
    documenté** — voir ci-dessous).
  - **Non porté (hors périmètre E2)** : `CheckAndExecuteWarp`, poids/armes, la branche de mort HP==0
    (82-170), le tuile-attribut `0x80` (172-196), le switch de pente non plat (207-353, `Slope_18c` reste
    0 = sol plat en V1), `UpdatePlayerWeaponEffect`/`UpdateWeaponStepProgression`/
    `UpdatePlayerCarriedEntity` (355-357), toute autre valeur de `TargetAnimationId` (saut, sprint,
    attaque, portage, escalade, nage, sable, sort, dégâts… 385-943), la fin `UpdateItemEffectState`/
    `SetPlayerHpMax`/`SetPlayerHp` (947-950).
  - **Écart documenté — LoadingMap → Idle** : l'original ne quitte `LoadingMap` que si `IsOnGround == 0`
    (914-922, sinon `break` = reste bloqué) ; `IsOnGround` n'existe pas en V1 (pas de gravité/collision).
    Un port littéral bloquerait le héros dans la pose LoadingMap pour toujours. `AlundraPlayerManager`
    fait donc basculer `LoadingMap` vers `Idle` dès le premier tick `MovePlayer` non verrouillé, avant le
    switch Idle/Moving (même tick) — le pad fonctionne donc dès la frame où le joueur reprend la main.
  - **Intégration cinématique** (`AlundraPlayerManager.Tick`, port de `PhysicsEngine.UpdateEntityPhysics`
    1579-1597, `IncrementForce` 1551-1576, la moitié « sol plat » d'`ApplyEntityForces` 1514-1547 —
    `TileAttributes` reste 0 donc `XForceTable[0]`/`YForceTable[0]` = 0, pas de
    `PreviousAdjustedForceX/Y`, pas de clamp écran — et `PosX += dx` 421-422, sans la boucle de collision
    environnante) : vitesse/accélération lues dans `AnimSets` (E2-A) via un index par anim
    (`SpriteRecordHeader.AnimSets`, `AlundraEntityScriptProxy.AnimSetsByAnim`), tables
    `g_offsetXList`/`g_offsetYList` copiées dans `AnimationTables.OffsetXList`/`OffsetYList`. Scénario
    vérifié à la main (Speed 208, Acceleration 1, direction 0x18 est) : tick1 ForceX=79872, tick2=159744,
    puis régime permanent 159744/tick — reproduit par `AlundraPlayerManagerTests`.
  - **Contrôleur/pawn** : `world.PlayerControllers` (`IReadOnlyList<PlayerController>`,
    CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:76) et `Controller.Pawn`
    (CasaEngineMonogame/CasaEngine/Framework/Gameplay/Controller.cs:20) — pas d'`Update` moteur sur
    `Controller`/`PlayerController` (aucun appelant dans `World.cs`), donc `AlundraPlayerController` ne
    peut pas se piloter seule : `AlundraEntityScriptProxy.Update` (branche `IsPlayer`) l'interroge chaque
    frame via `IAlundraScriptHost.PlayerController` (nouveau membre, implémenté par `AlundraWorldProxy`,
    résolu une fois dans `InitializeWithWorld`). `.buttonsMapping` enregistré une fois par session
    (`AlundraPlayerController.EnsureInputMappingsRegistered`, appelé depuis `InitializeWithWorld` — charge
    l'asset via `AssetContentManager.Load<ButtonsMapping>` puis délègue à `RegisterMappings`, qui
    n'ajoute que les mappings dont le nom n'est pas déjà `InputMappingManager.Contains` — idempotent par
    nom, pas par une seule sonde globale).
  - **Harnais headless** (`IntroTraceHarnessTests`) : construit son propre proxy joueur sans
    `AlundraPlayerController` ; `IAlundraScriptHost.PlayerController => null` y rend la branche `IsPlayer`
    de `AlundraEntityScriptProxy.Update` un no-op — trace toujours à la frame 926, sans régression.
- **Correctif (verifier E2, F1, 2026-08-23)** : le moteur (CasaEngineMonogame fe19e1e6, sous-module) a
  gagné l'`AssetLoader<ButtonsMapping>` (manquant jusque-là — `EnsureInputMappingsRegistered` devait
  contourner l'échec de chargement) et `InputMappingManager.Contains`/`TryGet` (match ordinal). La DLL
  charge maintenant `.buttonsMapping` normalement et `RegisterMappings` garde l'idempotence **par nom**
  (`Contains` par action, pas une seule sonde sur `"MoveUp"` — corrige l'avis A5 : un enregistrement
  partiel précédent n'aurait pu bloquer que les actions déjà présentes). Un échec de chargement (checkout
  moteur non reconstruit/fusionné) loggue désormais un WARNING nommant l'asset et le type d'exception,
  au lieu d'avaler l'erreur silencieusement. `BuildPadState`/`ComputePadState` sautent une action non
  enregistrée via `Contains` (pas de try/catch sur le chemin chaud).
- **Écarts** :
  - **A1 — EditorPreview** : `GameplayExecutionPolicies.EditorPreview` a
    `InitializePlayerControllers = false`, donc aucun contrôleur/pawn n'est créé par le moteur dans ce
    mode ; `AdoptPlayerPawn` loggue un warning une fois et ne spawn aucun héros de secours (décision
    assumée, pas de fallback) — le gate de spawn des enregistrements retombe sur la surcharge sans
    joueur (`ShouldSpawnRecord(record, out reason)`).
  - **A2/A3 — différés** : `PlayerInput.GetButtonState` alloue une instance `Engine.Input.ButtonState`
    par appel côté moteur (9 allocations/frame côté `BuildPadState`, une par action mappée) ; l'
    accumulateur `PhysicsTickAccumulator` est remis à 0 (pas seulement plafonné) au déclenchement du
    catch-up de `AlundraPlayerManager.Tick` — les deux sont des micro-optimisations sans effet
    observable au rythme New Game/map 389 actuel, reportées (pas de ticket dédié pour l'instant).
- **Correctif fins d'animation (2026-08-23)** : bug signalé par l'utilisateur (« l'animation
  d'Alundra est trop rapide »). Le convertisseur exportait TOUTE animation en `AnimationType.Loop`
  (`SpriteWriter.cs:376` avant correctif) et ignorait la signification de la frame de contrôle
  finale — documentée par `SpriteBankReader`/`SpriteFrame.TerminatorCode` mais jamais lue. Dans
  l'original (`alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/EntityManager.cs:257-281`,
  `UpdateAnimation`), en atteignant la dernière frame, la frame de contrôle finale (sans image ;
  `TerminatorCode` = `SiFrame.Delay` brut) décide de trois issues :
  - **Loop** — `Delay == 1` (:257-263) → retour à la frame 0, `AnimCompleteCounter++`.
  - **Hold** — `Delay != 1` et `(Delay & 0x80) == 0` (toujours vrai dans les données livrées) et
    `(TransformIndexLow & 0x80) != 0` (:267-275) → `NextFrameDelay = 0x7fffffff`,
    `ForceResetAnimationFlag = 1` : gel sur la dernière pose affichée.
  - **Chain** — même garde, `TransformIndexLow & 0x80 == 0` (:277-281) → `TargetAnimationId =
    TransformIndexLow` (**octet complet, non masqué** — `EntityManager.cs:277` affecte la valeur
    brute sans `& 0x7f`, et `SiFrame.TransformIndexLow` est lui-même un `byte` non masqué, voir
    `SiFrame.cs`) puis récursion immédiate de `UpdateAnimation`, même tick.
  Recensement sur le corpus réel (scan brut par map, avant dédoublonnage des banques — les
  compteurs du convertisseur, dédoublonnés, font foi) : 90280 direction-animations, 51911 Loop,
  22596 Hold, 15773 Chain (846 en chaîne vers elles-mêmes). L'anim 54 du héros (LoadingMap,
  frames 10/10/3 ticks) se termine par `Delay 0 / TransformIndexLow 0` → joue une fois puis
  enchaîne sur l'anim 0 (Idle) ; exportée en Loop elle bouclait toutes les 0,48 s (frames de 10, 10, 3 et 1 ticks) — le « trop
  rapide » observé. L'anim 0 (Idle) se termine par `Delay 1` → Loop (déjà correct).
  - **Classification** (`Readers/AnimationEndClassifier.cs`, nouveau) : `AnimationEndKind { Loop,
    Hold, Chain }`, lu sur la dernière frame de l'animation (`SpriteFrame.IsTerminator`). Absence de
    frame de contrôle finale (jamais observée sur les 92452 animations du corpus réel) → repli sur
    Loop (comportement historique) + compteur `Sprites.AnimationsMissingTerminator`.
  - **Règle du terminal Once** : pour Hold/Chain, `AnimationType.Once` remplace `Loop`, et la
    keyframe terminale (à `t_end`, durée de la dernière frame affichée) répète les valeurs de la
    DERNIÈRE FRAME AFFICHÉE (sprite, position, flips, `visible = true`) au lieu de tout masquer —
    `Animation2dCompositionSampler` clampe `CurrentTime` à `DurationSeconds` en `Once` et échantillonne
    les pistes à cette position, donc la pose retenue à l'arrêt est celle de la keyframe terminale ;
    sans ce correctif le sprite aurait disparu à la fin au lieu de se figer. Le rendu Loop reste
    identique octet pour octet (vérifié par diff avant/après sur
    `bankalundra_0_anim54_down.anim2d` : `animation_type: Loop → Once`, dernière keyframe de chaque
    piste dupliquée à `t=0.48s` avec `visible: true`, aucun autre octet changé).
  - **Schéma `Data/sprite-records.json`** : chaque entrée `IdsvAnimDirs` gagne `"End":
    "Loop"|"Hold"|"Chain"` et, si `Chain`, `"ChainTo": <id anim>`. Nouveaux compteurs de rapport :
    `Sprites.AnimationsLoop/Hold/Chain`, `Sprites.AnimationsChainSelf`. Sur l'export complet réel :
    9620 paires (anim, direction) exportées → 5207 Loop, 2657 Hold, 1756 Chain (119 en boucle sur
    elles-mêmes).
  - **Côté DLL** (`Alundra/Scripts`) : `SpriteRecordCatalog`/`AnimDirIdsv` lit `End`/`ChainTo` (tolère
    l'absence → Loop, export antérieur). `AlundraEntityScriptProxy.AnimationEndByAnimDirection`
    (même clé packée que `IdsvByAnimDirection` : `anim*4+direction`) est construit une fois au spawn
    (`ApplySpawnInitialization`/`AdoptPlayerPawn`), sans entrée Loop (rien à ponter). Un seul
    abonnement par entité, au spawn, à `AnimatedSpriteComponent.AnimationFinished`
    (`AlundraWorldProxy.SubscribeAnimationEndBridge`/`OnAnimationFinished`, délégué statique
    partagé, aucune fermeture par appel) : Hold → `ForceResetAnimationFlag = 1` (lu par la passe de
    pick pour `DeactivateOnAnimationEnd`) ; Chain → `TargetAnimationId = ChainTo`, repris au même
    tick par `AlundraEntityScriptProxy.Update` → `SyncAnimation` (le composant tourne AVANT le
    `GameplayProxy.Update` de la même entité, donc l'effet est same-tick comme la récursion
    originale, sans appel direct à `SyncAnimation` depuis le pont). Pas de désabonnement à la
    destruction : `DestroyEntity` ne fait que marquer `FlagToDestroy` (invisibilité, pas de
    suppression — hors périmètre V1), l'abonnement vit donc aussi longtemps que l'entité elle-même ;
    un appel résiduel sur une entité `FlagToDestroy` est sans effet (`SyncAnimation`/
    `RunPendingEventTriggers` l'ignorent déjà). L'`AnimCompleteCounter++` par boucle de l'original
    n'est PAS ponté (rien ne le lit côté V1 ; `AnimationFinished` ne se déclenche même pas pour une
    animation Loop, qui boucle sans jamais « finir »).
  - **Suppression de l'écart E2 LoadingMap** : `AlundraPlayerManager.MovePlayer` portait auparavant
    « LoadingMap → Idle inconditionnel » (écart documenté, faute de pont d'animation). Remplacé par
    le port fidèle de `PlayerManager.cs:914-922` (`if IsOnGround != 0, break` ; sinon Jump — Jump
    non porté, no-op documenté) ; `AlundraWorldProxy.AdoptPlayerPawn` fixe le stub
    `IsOnGround = 1` pour le héros jusqu'à E3 (pas de gravité/collision). La sortie de LoadingMap
    passe donc désormais par la chaîne d'animation (anim 54 → 0) livrée ci-dessus, comme
    l'original.
  - **Tests** : `alundra-casaengine-project-converter.Tests` (129, +7) — classification sur données
    réelles (héros anim 0 Loop, anim 54 Chain→0, un exemple Hold réel du corpus), keyframe terminale
    Once qui répète la dernière frame affichée, sortie Loop inchangée. `Alundra.Tests` (304, +10) —
    parsing `End`/`ChainTo`, comportement du pont Hold/Chain (appel direct du handler), LoadingMap
    reste figé jusqu'au déclenchement de la chaîne, `IntroTraceHarnessTests` toujours à la frame 926
    (ne dépend pas des animations, confirmé).

### E3 — Collisions : champ de hauteur `AlundraCells` + mover conscient de la politique ✅ (plan et tranches : docs/plan-e3-collisions.md ; validé en jeu le 2026-09-07)

- **But** : le héros marche sur le pont, est bloqué par les murs, suit la hauteur des cellules.
- **Contenu** : moteur — mover conscient de la politique `TopDownElevation`, helper pied/demi-hauteur
  (prérequis de `character-controller-features.md`) ; `HeightGridCollisionField` alimenté depuis
  `AlundraCells` (walkability, height, slope ; Z-élévation Alundra ↔ axe d'élévation du champ) ;
  convertisseur — si le champ doit être un asset, l'émettre (sinon construit par la DLL au
  `OnBeginPlay`).
- **Acceptation** : test moteur du mover sur une grille de hauteur synthétique ; au runtime, le héros
  ne traverse ni murs ni vide, monte/descend les pentes de la 389.
- **Dépendances** : E2. **À valider avant lancement** : représentation exacte Z-élévation / unités
  (cf. `guidelines-runtime-alundra-casaengine.md` §2).

### E4 — Déplacement scripté des entités ✅ (plan et tranches : docs/plan-e4-deplacement-scripte.md ; validé en jeu le 2026-09-07)

- **But** : les marins de l'intro marchent, sautent, atterrissent (durées réelles).
- **Contenu** : convertisseur — exporter `AnimSets[].Speed`, `Acceleration`, `IsZForceApplied`,
  `Sfx` dans `sprite-records.json` (`SpriteBankReader.ReadAnimSet` ne lit que `PreloadedAnims`) ;
  couche TileMap `navigation.*` depuis `AlundraCells` ; DLL — `CharacterControllerComponent` sur les
  prefabs d'entités (déjà des corps `Pawn` en Phase G2), opcodes 0x5B/0x5A (direction + anim →
  `SetMoveIntent` à la vitesse de l'anim), 0x1E/0x1F (→ `MoveTo` via navigation, D5 ; fin = distance
  atteinte ou collision), 0x1B (impulsion verticale), 0x16/0x17 (gravité), 0x07 (zone), 0x70 (au sol
  depuis le contrôleur), 0x19, 0x0A, 0x49/0x4B, 0x38.
- **Acceptation** : harnais (prédicats optimistes retirés pour 0x07/0x70) ; au runtime, la
  chronologie de l'intro se déroule avec des marins qui bougent.
- **Dépendances** : E3.

### E5 — Caméra suivant une entité désignée ✅ (plan et tranches : docs/plan-e5-camera.md ; **runtime VALIDÉ par l'utilisateur le 2026-08-26**)

- **But** : le plan sur la mouette, la descente avec le bloc 10, le suivi des marins 11/12, le retour
  sur Alundra.
- **Contenu** : `CameraTargeted2dComponent` (Target = entité, dead zone, limites de map) remplaçant la
  caméra debug ; `g_entityFollowedByCamera` comme variable du world (0x67/0x68/0x69) ; destruction de la
  cible gérée ; `InitializeScrollingMode` et limites.
- **Acceptation** : au runtime, la caméra suit chaque cible de la chronologie §0.
- **Dépendances** : E1 (E4 pour voir les mouvements). **À valider** : `CameraTargeted2dComponent`
  dérive de `Camera3dComponent` (perspective) — pixel-perfect à vérifier ou à faire évoluer.

### E6 — Contrôle joueur : verrou / libération ✅ (DLL — livrée par anticipation dans E4.c, close le 2026-08-26)

- **But** : 0x10 retire le contrôle, 0x11 le rend ; jalon « l'intro se joue jusqu'au contrôle ».
- **Contenu** : `g_playerControlFlags` dans `AlundraGameState` ; pont vers `PlayerInput.IsInputEnable`
  / `CharacterControlMode.Script` ; branche verrouillée de `MovePlayer`.
- **Acceptation** : au runtime, le pad est inerte jusqu'à la frame de 0x11 puis déplace Alundra.
- **Dépendances** : E2 (E4/E5 pour la scène complète).

**Constat de clôture (2026-08-26)** — le contenu d'E6 avait déjà été livré par la tranche E4.c
(`07be483`), sauf le « pont moteur » qui est délibérément écarté (décision E6-1 ci-dessous) :

- `AlundraGameState.PlayerControlBits` porte les bits nommés et les deux masques utiles
  (`InputBlockedMask` 0x34, `GameplayBlockedMask` 0x48) ;
- 0x10/0x11 posent et retirent `ControlLocked` (`AlundraEventProgramRunner.cs:460/464`) ;
- `MovePlayer` teste `InputBlockedMask` **exactement au site de l'original** (`PlayerManager.cs:38`),
  et sa branche verrouillée est un no-op documenté (les cinq effets de bord de l'original —
  `CreatePlayerAnimationEffects(1)`, reset du timer de warp, purge des cooldowns d'effet,
  `UpdatePlayerCarriedEntity(1)`, `AnimateWarpEffect()` — sont tous dormants sur une New Game) ;
- `RunMapEventsPass` teste `GameplayBlockedMask` (port d'`EntityManager.cs:377`) ;
- vérifié au runtime par la trace d'intro : `0x10 Player lose control` est dispatché **frame 1**,
  `0x11` **frame 1704**, les deux `Implemented` ;
- couvert par `MovePlayer_InputBlocked_DoesNotChangeAnimationOrDirection`,
  `MovePlayer_LoadingMapWhileInputBlocked_StaysLoadingMap`,
  `MovePlayer_ControlLocked_DebugFlagInactive_StillBlocked` et
  `MovePlayer_ControlLocked_DebugFlagActive_ReadsThePad`.

**Décision E6-1 — pas de pont vers `PlayerInput.IsInputEnable` / `CharacterControlMode`**, pour deux
raisons établies par reconnaissance :

1. **Cela casserait le flag de debug de la décision E4-3.** `AlundraPlayerController.BuildPadState`
   passe par `PlayerInput.GetButtonState`, lui-même filtré par `IsInputEnable`
   (`PlayerInput.cs:76/117/149`). Mettre `IsInputEnable = false` sous verrou viderait le pad **en
   amont** de la porte d'`MovePlayer` — or `ALUNDRA_DEBUG_IGNORE_CONTROL_LOCK` ne contourne que cette
   porte-là. Le flag deviendrait inopérant en silence.
2. **Ce serait moins fidèle.** L'original n'a **aucun** interrupteur global d'entrée : il teste le
   masque à chaque site consommateur — `PlayerManager.cs:38` (déplacement), `:1906` (usage d'objet),
   `:3437` (menu), `:1297` (`HpRegenBlockedMask` 0x7C), `EntityManager.cs:377` (map events),
   `SpriteEventHandlers.cs:289` (trésor), `GameEngine.cs:1523/1567` (warp). Notre port reproduit déjà
   les deux seuls sites dont le système existe.

**Sites de la variable non portés, tous hors périmètre faute du système correspondant** (relevé
exhaustif) : écritures depuis les gestionnaires d'UI/inventaire/carte mémoire (`MenuOpen`,
`MessageBox`), mort du joueur (`PlayerManager.cs:145`), respawn (`GameEngine.cs:331` remet à 0),
séquences forcées et sand-cape (`FunctionTypeC.cs`), opcodes 0xC0/0xC1 (`ForcedWeapon`), et
`SpriteEventHandlers.cs:277` (le programme F pose le verrou) — ce dernier est **inatteignable** chez
nous : `AlundraWorldProxy.ActiveCollisionEntity` n'est jamais assigné en production, donc
`ProgramFInteract` n'est jamais élu. Le masque `HpRegenBlockedMask` (0x7C) n'est pas porté non plus,
faute de système de régénération. À reprendre avec les étapes qui apportent ces systèmes (E12
dialogues, E13 HUD, puis combat/objets).

**Limite connue** : aucun test ne pinne la décision E6-1 elle-même. Les tests du flag de debug passent
un `AlundraPadState` directement à `MovePlayer`, donc ils n'échoueraient pas si quelqu'un vidait le pad
en amont via `IsInputEnable` — c'est un trou de couverture assumé, faute de harnais d'entrée headless.

### E7 — Mutation de tuiles à chaud ✅ CLOSE (validée en jeu par l'utilisateur, 2026-08-28)

Plan détaillé : [plan-e7-mutation-tuiles.md](plan-e7-mutation-tuiles.md). Tranches E7.a `326917e`,
E7.b `9493b78`, E7.b-bis (moteur `1c5bf445` + pointeur `1215f3b`), E7.c `e5d73bb`, clôture **E7.d**.
**Validée en jeu par l'utilisateur** : écoutilles fermées à l'entrée, trappe animée pendant l'intro,
ouverture au passage du joueur, tuiles animées sans saut. Seule réserve, attendue et hors périmètre :
**pas de son** — `0xBD` est un no-op dégradé et le son 61 de la trappe attend **E11**.

- **But (reformulé par la reconnaissance)** : les « portes B 130-133 » sont **quatre écoutilles** du
  pont (destinations 1×2 : (18,37), (15,27), (21,27), (16,41)), et la « trappe du marin 15 » est la
  première d'entre elles. L'export livre les écoutilles **ouvertes** ; ce sont les programmes
  d'entrée de map qui les ferment.
- **Écart au contenu prévu** : **aucune API moteur de mutation n'a été nécessaire** — la prémisse
  « `TileMapComponent` n'expose qu'un overlay » était fausse (`SetTile`/`SetTileReference`/
  `RemoveTile` existent, avec reconstruction partielle). Le chantier a été **DLL seule**
  (décision D-E7-1), à une exception : **E7.b-bis**, un correctif moteur décidé par l'utilisateur
  parce que reconstruire l'overlay remettait à l'image 0 les 223 tuiles animées de la carte.
  Aucun changement convertisseur, aucun export relancé.
- **Livré** : 0x54/0x55/0x85 portés fidèlement (store de cellules partageant ses tableaux avec le
  champ de collision, donc toutes les sondes du héros voient une mutation instantanément) ; applier
  visuel re-dérivant les seules cellules mutées et reconstruisant l'overlay une fois par frame ;
  synchro de la grille de navigation ; puis 0x3B et 0x2F, qui rendent les écoutilles ouvrables par le
  joueur. **0x2F n'est pas un test de direction** malgré son nom : c'est un test de bouton du pad.
- **Acceptation** : tests — `Alundra.Tests` 589, convertisseur 138, moteur sans nouvel échec (18
  préexistants), goldens d'intro re-baselinés en ré-étiquetage prouvé pur, quatre traces du héros
  byte-identiques. **Runtime : VALIDÉ par l'utilisateur le 2026-08-28** — écoutilles fermées à
  l'entrée de map, trappe animée pendant l'intro, ouverture au passage du joueur poussant vers le
  haut, tuiles animées sans saut.
- **Dépendances** : E3 (champ) — satisfaite. E8 n'était pas un prérequis.
- **Non portés, documentés** : opcode 0x56 (copie via la table `MapCopies`, non exportée) ; tuiles
  cassables (`CheckAndTriggerTileEffect`, combat) ; consommateur warp du bit `GroundProperty` 0x80
  (E10) ; les snapshots de pad `ButtonsReleased`/`ButtonsJustPressedByInterval`.

### E8 — Profondeur murs/sols dans le moteur ✅ CLOSE — livrée par un autre chemin (2026-09-06)

- **But d'origine** : retirer `WallPlacementOverlay` et l'interleave de la DLL ; le `TileMapComponent`
  et le tri de profondeur moteur rendent murs, sols et sprites dans le bon ordre.
- **Ce qui s'est réellement passé** : le but est atteint, mais pas par le chemin décrit ici. Le
  mécanisme moteur existe, et il a été écrit pendant la lignée E9 :
  `TileMapComponent.AddSortedOverlayTile` / `ClearSortedOverlayTiles`, documenté dans
  `CasaEngineMonogame/docs/engine/tilemaps-gestion-profondeur.md:285` sous le titre « Overlay runtime
  de tuiles triées **(implémenté)** », dont le texte nomme le cas d'usage visé : « le cas typique
  étant des murs PSX qui doivent s'intercaler avec les personnages ».
  **`WallPlacementOverlay` n'est donc pas un contournement dans la DLL** : c'est du code de jeu qui
  pilote une API moteur conçue pour lui. Le tri des murs et des sols contre les entités passe déjà
  par le moteur, via `RenderSortKey2D` et le chemin sprite trié.
- **Pourquoi le reste ne bouge pas — D-E8-1** : ce qui subsiste dans la DLL est irréductiblement
  spécifique au jeu — les constantes PSX (`RowStride = 16`, `WallDepthBias = 7`,
  `EntityDepthSlot = 6`, `EntityRowMax = 0x3B`), le schéma `AlundraWallPlacements` /
  `AlundraFloorPlacements`, et la boucle qui appelle déjà l'API moteur. Les porter dans le moteur
  créerait un type générique **sans aucun consommateur moteur**, ce que `CasaEngineMonogame/AGENTS.md`
  §9.2 interdit explicitement (« Pas d'abstraction pour un système simple »). La frontière passe donc
  ici : le moteur fournit le tri, le jeu fournit ses constantes et ses données.
- **La variante « rôles cuits par couche » est réfutée, pas reportée — D-E8-2** : elle consistait à
  faire segréger murs et sols surélevés par le convertisseur dans leurs propres couches marquées
  `depth.role`, pour que le classement par couche du moteur suffise. Mesuré sur les **483 cartes
  exportées** : **85,4 %** des groupes `(carte, Plane)` de murs contiennent déjà au moins deux
  `DepthSlot` distincts, donc un rôle par couche ne peut pas reproduire un slot par tuile ; la
  fidélité complète demanderait **jusqu'à 12 couches par carte** au lieu de 2 (médiane 11) ; et la
  389 a besoin de **jusqu'à 4 tuiles de même rôle sur une même cellule**, qu'une couche dense par
  cellule perdrait en silence. Trois relecteurs indépendants ont tenté de réfuter ce constat en
  re-dérivant les chiffres eux-mêmes ; les trois ont confirmé. Le document du moteur énonce d'ailleurs
  l'inverse de la variante : « La TileMap ne doit pas tout trier tile par tile » — les couches fixes
  restent chunkées, et l'overlay est l'exception prévue pour les tuiles individuelles.
  La piste `depth.spawnAsEntity` est écartée sur le volume : **501 962 placements de murs** sur le
  corpus.
- **Acceptation** : sans objet sous cette forme — aucun rendu n'a changé. L'ordre murs/sols/entités
  est celui validé en jeu par l'utilisateur lors d'E7 (mutation de tuiles, 2026-08-28) et d'E9
  (backdrops, 2026-09-04), tous deux appuyés sur ce même chemin trié.
- **Écart gelé — D-E8-3** : le tri des entités de l'original (`UpdateVisibleEntitiesZSort`,
  `EntityManager.cs:1010`) propage la profondeur le long des plateformes et des entités collidables
  (`EntityManager.cs:1042-1080`). Le portage n'en a que le seau grossier `PosY + IDSV`. Cet écart est
  **définitif et assumé** ; il ne rouvre pas E8. Il redeviendra un chantier si un défaut visible
  apparaît.
- **Préalable livré** : le clamp de seau `0x3B` de l'original (`GraphicManager.cs:344`) manquait au
  portage sans être documenté — un troisième écart non signalé, en plus des deux que le commentaire
  de `ComputeEntityElevation` énumère. Corrigé en tranche séparée (`5d66e10`), avec deux tests qui
  épinglent la borne. Le commentaire est corrigé au passage : le départage fin de l'original vient de
  `PosZ >> 16` (`EntityManager.cs:1036`), pas des bits bas de `PosY`.
- **Ce qui reste, et qui n'appartient pas à ce plan** : `TileMapDepthSettings` analyse `Elevation`,
  `RenderPass`, `SortingLayer` et `SortAnchor`, et **rien ne les lit au rendu** — seul
  `ShouldRenderTiles` est consommé (`TileMapComponent.cs:428,589,1655`, seuls sites). Ce sont les
  étapes 4 et 5 de la migration que le moteur s'est lui-même écrite. C'est une dette **du moteur**,
  utile à tous ses projets et sans rapport avec Alundra : elle relève d'un chantier dans
  `CasaEngineMonogame/ai-agent/tasks/`, pas d'une étape de ce plan.

### E9 — Backdrops, parallaxe, ondes dans le moteur ✅ (moteur, plan-verifier)

- **But** : retirer `BackdropRenderer` de la DLL ; un composant moteur de couches défilantes
  (`ScrollParameters` : vitesses, périodes, déformation).
- **Acceptation** : map 389 (mer) identique avant/après.
- **Dépendances** : aucune.
- **Clos** : E9.a (résidus B1-B5, ancrage de la toile) puis E9.b (le mécanisme de rendu déménagé dans
  le moteur, `docs/plan-e9b-backdrops-moteur.md`) — S0 moteur `dcbb55ff` + bump `29a84e2`, S1 DLL
  `3798b75`, amendement du critère visuel `975248c`, S2 bascule `e808568`. Validé en jeu par
  l'utilisateur sur les maps 389, 159 et 321.
- **E9.c** (`docs/plan-e9c-defauts-321.md`) : la validation en jeu de la 321 a révélé deux défauts
  antérieurs à E9.b (nuages rouges au lieu de bleus ; nuages passant devant les os de la rangée du
  haut au lieu de derrière). Corrigés : C1 convertisseur (inversion rouge/bleu du décodage palette
  des fonds) `71c57da` ; C2 moteur (profondeur de fond distincte de la profondeur caméra) sous-module
  `0be1e9d2`, commit parent `0458c6b`. **Validée en jeu par l'utilisateur** (« tout est ok »).

### E10 — Fondu, teinte, transitions dans le moteur ✅ CLOSE — **validée en jeu le 2026-09-01**

Plan et tranches : `docs/plan-e10-fondu.md` §6. Fondu d'entrée validé sur la 389, puis les nuages
après deux correctifs nés de la recette. Commits : moteur `1f837ed6`, parent `96f440e`, `692ec4c`,
`767e9e6`. Restes dispositionnés en P3/P4 dans le plan, aucun bloquant.

- **But** : `WarpPlayer` effet 0 (fondu 16 frames à l'entrée), teinte plein écran, transitions de map.
- **Acceptation** : fondu d'entrée visible sur la 389.
- **Dépendances** : aucune.

### E11 — Audio ✅ CLOSE — **validée en jeu le 2026-08-30**

Plans : `docs/plan-e11-audio.md` §6 (bruitages, `3b1eb24`) et `docs/plan-e11c-musique.md` §6
(musique, `0b1d2d9` + analyseur `f216b32`), verifier CONFIRMED avant chaque validation. La réserve
laissée par E7 est close : la trappe ne s'ouvre plus en silence. **E11.b** — le solde des opcodes
audio (`0xA5`, `0xAB`/`0xBF`, `0xA6`/`0xA7`, anti-doublon, table par carte) — **livrée et validée en
jeu le 2026-09-07** (`docs/plan-e11b-opcodes-audio.md`, dernière section ; `3507527`, `1985846`,
`e159d0d`, clôture `1f7e122`), avec T7 et le son de départ de warp (`docs/plan-transitions-carte.md`
§T7, D-T-8). _(Ce paragraphe disait encore « reportée » jusqu'au 2026-09-24 : il n'avait pas été
mis à jour après la livraison.)_ Suites ouvertes, déclarées non bloquantes par leur plan : la
projection du mixage stéréo [B1-a], `IsBgmActivated` non modélisé, la garde d'index de carte
toujours satisfaite, et `0xAB` absent du corpus. **[B1-a] et `IsBgmActivated` sont fermés le
2026-09-25** (`docs/plan-audio-mix-exact-muet.md`, décision D-E13D-37 ; recette de l'auteur en
attente) : mixage stéréo exact tonalité par tonalité, son coupé par réglage du projet, id de `0xBF`
sur deux octets. Restent la garde d'index, `0xAB` absent du corpus, et la suite S1 de ce
chantier (`0xA7` jouerait la musique là où l'original la charge seulement). **S1 est fermée le
2026-09-25** (`docs/plan-bgm-demarrage-binaire.md`, ADR-0004 ; **validée en jeu par l'auteur le 2026-09-26**) : lu dans
`ALUN_CD.EXE`, la BGM ne démarre que par `StopAllSound` ; charger ne joue pas, `InitializeBgm` arrête ; le
port suit désormais l'exécutable pour `0xA5`, `0xA6`, `0xA7`, l'entrée de carte, le départ de warp, et les
21 cartes d'index `−1`, muettes ; la garde d'index de carte est désormais portée (index brut, négatif compris).

- **But** : BGM de la 389 (`LoadMapSounds`), SFX 44/45/46/61 de 0xBD, streaming.
- **Acceptation** : sons audibles aux frames de la chronologie.
- **Dépendances** : E1.

### E12 — Dialogues Yarn + boîte MGUI ✅ CLOSE — **validée en jeu le 2026-09-02**

Plans : `docs/plan-e12-dialogues.md` (E12.a dialogue jouable, E12.b largeurs proportionnelles) et
`docs/plan-e12d-interaction-joueur.md` (`774255f`, verifier CONFIRMED). Le marin 12 pose sa question
en `font3` proportionnel, en français, avec le choix OUI/NON complet, ouvert par l'appui Carré.
**Reste E12.c** — la fidélité fine (machine à écrire, pagination, curseur, blips), explicitement
datée « plus tard » par son propre plan, plus un trou convertisseur : la table de chaînes partagée
`map_alundra.json` n'est pas exportée.

- **But** : parler aux marins (slot F, Tick 140 gardé par 0x800C).
- **Contenu** : convertisseur — `.yarn` par map, un nœud par chaîne (D6), `DialogueAsset` compilé ;
  moteur — vues de dialogue MGUI (`DialogueBoxView`, `ChoiceListView`, ⏳ dans
  `yarn_spinner_integration.md`), police `font3.fnt` ; DLL — 0x0D/0x39/0x44/0x50/0x51/0x5C sur
  `YarnDialogueRunner`/`DialogueService`, slot F via `g_activeCollisionEntity`, `MessageBox`/`MenuOpen`.
- **Acceptation** : dialogue du marin 12 jouable avec choix.
- **Dépendances** : E2, E6. **À valider** : nommage des nœuds, codes de contrôle (`\N`, `\C#`…) →
  balises Yarn, chasse fixe de la police.

### E9.d — Mode cellulaire des fonds ✅ CLOSE — **validée en jeu le 2026-09-07**

- **But** : porter le second mode de rendu des fonds, le mode **cellulaire** (`Mode == 2`), que E9 n'a
  pas touché. Aujourd'hui `docs/formats/backdrops.md:117` l'annonce : « paramètres bruts exportés,
  rendu non implémenté » — les données sortent du convertisseur, rien ne les dessine.
- **Pourquoi devant E13** : **90 cartes du corpus ont une couche cellulaire** et n'affichent donc
  aucun fond. C'est le plus gros défaut visible qui subsiste, et il pèse plus lourd à l'écran que le
  HUD ou la conversion hybride. E9 a pu se clore sans lui parce qu'aucune des trois cartes de recette
  (389, 159, 321) n'exerce ce mode.
- **Contenu** : les cellules et leurs types (dont `WaveX`), et le `WaveLut` — 256 entiers dont la
  reconnaissance a établi qu'il n'a **aucun lecteur** dans le chemin des couches de tuiles et dont les
  seuls consommateurs sont ces cellules (`plan-e9-backdrops-residus.md` §1.3).
- **Acceptation** : quatre cartes de recette retenues — 420, 391, 271, 443 (`plan-e9d-mode-cellulaire.md` D1) — aucune des
  cartes déjà validées ne convient.
- **Dépendances** : aucune. Le mécanisme moteur de couches défilantes livré en E9.b est le point de
  départ.

## File des chantiers après E9.d (arbitrée avec l'auteur le 2026-09-07)

L'ordre ci-dessous prime sur la numérotation E13 → E15 tant qu'il n'est pas épuisé.

1. **T7 et E11.b — FAIT le 2026-09-07.** Les deux tranches ont été livrées le jour même où cet ordre a
   été arrêté, puis validées en jeu par l'auteur (« pour moi c'est bon ») : T7 `8773965`
   (`docs/plan-transitions-carte.md` §T7), E11.b `3507527`, `1985846`, `e159d0d`
   (`docs/plan-e11b-opcodes-audio.md`), clôture commune `1f7e122`. Le son de départ de warp est porté
   dans sa structure (D-T-8), muet sur l'aller-retour 389 ↔ 390 par les données. _(Relevé le
   2026-09-24 : ce point disait encore « en cours ». Branche « effet 3 » de `0x53` gelée, 0 occurrence
   sur 329.)_

2. **Dette moteur : `TileMapDepthSettings` — FAIT le 2026-09-07.** Plan archivé dans le sous-module
   (`ai-agent/tasks/archive/tilemap-depth-settings-tasks.md`), quatre tâches ✅, moteur 1618 verts,
   portage 815 inchangé, verifier CONFIRMED sur les 484 `.tileMap` réels. Les étapes 4 et 5 sont
   livrées telles que réellement conçues — la passe repliée dans le Z des couches chunkées, la file de
   sprites existante pour les couches en tri dynamique — et l'étape 6 reste explicitement non faite.
   **Pour le portage : rien ne bouge**, ses 483 cartes ne portent qu'un `depth.role = CollisionOnly`
   sur leur couche de navigation. Une trouvaille hors chantier à garder : le pion du RPGDemo porte un
   Z = 0,3 périmé qui masque la profondeur en jeu.
   _(Texte d'origine : « LE PROCHAIN CHANTIER. »)_
   `CasaEngine/Framework/Assets/TileMap/TileMapDepthSettings.cs` analyse `Elevation`, `RenderPass`,
   `SortingLayer` et `SortAnchor`, les valide, les couvre de tests unitaires — et **le rendu n'en lit
   aucun** : seul `ShouldRenderTiles` est consommé, à trois sites de `TileMapComponent`. Ce sont les
   **étapes 4 et 5** de la migration que le moteur s'est lui-même écrite dans
   `CasaEngineMonogame/docs/engine/tilemaps-gestion-profondeur.md` (les étapes 1 à 3 sont faites).
   **Ce chantier n'appartient pas au portage** : c'est une dette du moteur, utile à tous ses projets,
   et son plan va donc dans `CasaEngineMonogame/ai-agent/tasks/` selon les conventions du sous-module,
   pas ici. Découvert en creusant E8 (voir la clôture d'E8 pour la démonstration).

3. **Les validations en jeu jamais enregistrées — FAIT le 2026-09-07.** **E1 à E4** portaient « visuel
   runtime à valider par l'utilisateur » depuis leur clôture ; l'auteur a joué une New Game sur la
   map 389 et confirmé les neuf points attendus (chargement, héros, MapEvents, marins, intro jusqu'au
   bout, tri profondeur, pad, collisions) — enregistré dans les sections E1-E4 ci-dessus et dans
   `docs/plan-e3-collisions.md` / `docs/plan-e4-deplacement-scripte.md`. **Correction sur l'inventaire
   lui-même** : ce point citait aussi `docs/plan-camera-ordre-frame.md` comme finissant sur « reste la
   validation en jeu » — en réalité ce chantier (et le correctif de cadence qui l'accompagne) était
   déjà validé en jeu le 2026-08-29, voir `plan-camera-premiere-frame.md:155` (« La caméra est bonne au
   début de la map. »). C'est l'inventaire qui avait tort, pas le dossier.

Ensuite seulement : E13, E14, E15 — sachant qu'E14 porte aussi le blocage physique entité↔entité,
rattaché à elle par décision d'E12.d (le joueur traverse encore les PNJ).

### E13 — HUD MGUI 🚧 (découpée en quatre étapes le 2026-09-19, sur décision de l'auteur)

- **But d'origine** : cœurs/magie/argent (`HudManager`, `BALANCE.BIN` exporté en Phase 7).
- **Dépendances** : E12 (infrastructure MGUI).
- **Découpage** (plan détaillé : `docs/plan-e13-hud.md`) :
  - **E13.a — La jauge permanente ✅ close, validée en jeu.** Cœurs, magie, argent : état joueur,
    directeur au tick logique, écran MGUI composé des sprites déjà exportés, translation au tick, touche
    F1 de recette (validée le 2026-09-19). **C4**, la jauge qui s'assombrit avec le décor au warp, est
    livrée sur le chantier moteur « effet d'écran au-dessus de l'interface » (moteur `19fcd84c`, parent
    `7d6a37a`) et passée en jeu par l'auteur le 2026-09-20 avec la recette C5.b ; mergée dans `main` le
    2026-09-21 (`8dcfd81`).
  - **E13.b — Les fonds des cases arme et accessoire ✅ close, validée en jeu** (tranche C6, `ae8a24e`).
    Deux quads gouraud de 24×32 à quatre
    couleurs de sommet, semi-transparents, aux abscisses 16 et 48, qui suivent le glissement de la
    jauge et sont dessinés même quand la case est vide. DLL seule, aucune cuisson : MGUI peint un quad
    à quatre couleurs de coin. Tranche C6 du plan E13.
  - **E13.c — Les icônes d'arme et d'accessoire ✅ close le 2026-09-21, S4 passée en jeu par
    l'auteur, mergée dans `main` (`ea633ad`).** La reconnaissance pensait les portraits absents de l'export ; la mesure S1.a a montré
    que **les 88 portraits exploitables ont déjà leur `.sprite` émis**, si bien qu'il n'a fallu ni
    ré-extraction ni cuisson. Faits : les tables des objets, des portraits et du déverrouillage en CSV
    dans l'analyseur (S1.a `6176ea3`, S1.b `a0904b8`, S1.c `c304201`) ; les trois catalogues republiés
    bruts par le convertisseur, chacun prouvé par double export (S2 `0f1cfd4`, mergée ; S2.b `7018e6a`) ;
    dans la DLL, la chaîne arme → objet → icône et l'inventaire de la nouvelle partie portés ligne à
    ligne, l'épée dans la case gauche du HUD (S3 `0135c92`), centrée dans sa case sur décision de
    l'auteur (`cdb7097`). Analyseur mergé dans son `master` (`c204009`).
    Au démarrage : l'épée de base à gauche, la case d'accessoire vide comme dans l'original. **Décisions du 2026-09-19** : E13.c se fait avant E13.d
    et lui livre l'extraction, la table des objets et l'arme équipée ; on extrait le portrait de
    **tous** les objets dont la colonne d'icône est renseignée ; la recette ne force **aucun**
    accessoire équipé, fidélité stricte. Plan : `docs/plan-e13c-icones-hud.md`.
  - **E13.d — La gestion de l'inventaire : ✅ close, validée en jeu** — inventaire principal le 2026-09-24 (D6) ; sous-inventaire et bascule L1/R1 le 2026-09-25 (`docs/plan-e13d-sous-inventaire.md`, SI1 à SI12 : sous-inventaire, bascule, puis contre-vérification complète de l'inventaire principal contre l'exécutable, défauts de l'original corrigés, texte déroulant partagé, délai de warp de 0,2 s) (approuvée le 2026-09-21) : plan
    `docs/plan-e13d-inventaire.md`, relu READY, décisions de l'auteur du 2026-09-21 enregistrées, dont
    les boîtes en image cuite depuis la copie A. Décision du
    2026-09-19 : porter l'original tel quel ; amendée le 2026-09-21 pour le dessin : pas au pixel près,
    icônes centrées dans leur boîte. Établi dans le code d'origine, masques lus par ET binaire donc
    « l'un quelconque » : en jeu, `Start`, `L2` ou `R2` ouvre l'inventaire principal
    (`GameEngine.cs:1567-1576`, sous conditions : aucun verrou de contrôle, pas de warp, `Select` non
    maintenu) ; **L1 et R1 n'ont aucun gestionnaire en jeu** et ne servent qu'à basculer entre
    l'inventaire principal et le sous-inventaire une fois ouvert (`MainInventoryManager.cs:853-858`,
    `SubInventoryManager.cs:371-377`) ; `Start`, `L2` ou `R2` referment et la jauge réapparaît
    (`MainInventoryManager.cs:846-851`). Taille : deux gestionnaires d'environ 3300 lignes et seize
    méthodes publiques, une table d'objets de 100 lignes (et 98 enregistrements de déverrouillage)
    vivant seulement dans la décompilation, republiées depuis par E13.c ; le portage n'avait (au
    2026-09-19) ni écran d'inventaire, ni L1/L2/R1/R2/Select dans son état de pad — ajoutés par D1 et D5 ;
    l'équipement et les compteurs d'objets, eux, existent depuis E13.c S3. La table des objets, l'équipement et les écrans
    d'inventaire n'avaient jusqu'ici aucune étape dans cette feuille de route. **Découpage décidé le
    2026-09-19** : l'inventaire principal d'abord, ouverture, fermeture et équipement, puis le
    sous-inventaire et la bascule L1/R1. Le plan de l'inventaire principal est écrit ; celui du
    sous-inventaire l'est depuis le 2026-09-24, après la recette D6 : `docs/plan-e13d-sous-inventaire.md`.
    Il établit dans l'exécutable France que `Triangle` referme aussi les deux inventaires (masque `0x813`,
    que la décompilation avait perdu).

### E14 — IA native ⏳

- **But** : `SpriteEventHandlers` (~120 handlers) en scripts C# par type de sprite, sur la navigation
  (poursuite, patrouille). Hors intro : seuls A0/E0 sont requis avant (E1).
- **Dépendances** : E4, E5.

### E15 — Conversion hybride des programmes simples ⏳ (convertisseur)

- **But** (D1) : traduire en assets moteur les programmes qui s'y prêtent — programme B 129 →
  `.cutscene` (`CutsceneDirector` : Wait, MoveTo, Sequence/Parallel existent ; caméra/dialogue/gates
  ⏳), dialogues → Yarn (E12).
- **Dépendances** : E5, E6, E12 et les commandes de cutscene manquantes côté moteur.

## 5. Règles de travail

- Fidélité **de comportement observable** dès qu'un système moteur remplace un système original ;
  fidélité **de structure** (ordre, noms, adresses) pour ce qui reste porté (interpréteur, état de
  jeu, gates). Chaque écart est documenté (fait / hypothèse / écart) dans le code et dans l'étape.
- Aucune supposition sur une donnée ou une API non vérifiée : question à l'utilisateur avant
  l'étape (rubrique « À valider »).
- Convertisseur : toute modification ⇒ export complet depuis zéro (`report.json` à 0 erreur) ; tout
  writer qui ajoute au catalogue appelle `EditorAssetCatalogService.Save()`.
- Moteur : commit dans le submodule puis bump du pointeur ; le launcher tourne depuis le checkout
  standalone `D:\development\repo\CasaEngineMonogame` (fetch + merge après chaque commit moteur).
- Validation : `dotnet build alundra-casaengine-project-converter.slnx -c Release` ;
  `dotnet test Alundra.Tests -c Release` et
  `dotnet test alundra-casaengine-project-converter.Tests -c Release` : **0 échec** — le contrat est
  l'absence d'échec, pas le total, qui croît à chaque test ajouté (à titre indicatif : 357 et 130 au
  2026-08-24) ; moteur : `CasaEngine.Tests` (à builder explicitement, le `.slnx` ne l'inclut pas) :
  aucun échec au-delà des **18 préexistants** (18 échecs / 1206 au 2026-08-24), tout nouvel échec =
  régression.

## 6. Suivi

| Étape | Statut | Commit |
|---|---|---|
| E1 scripts par entité + MapEvents | ✅ close (validée en jeu le 2026-09-07) | 92f1be5 |
| E2 héros pawn | ✅ close (validée en jeu le 2026-09-07) | voir git log |
| E3 collisions (E3.0/a/b/c/c-bis/d.0/d) | ✅ close (validée en jeu le 2026-09-07) | voir git log |
| E4 déplacement scripté (E4.0/a/b/c/d/f) | ✅ close (validée en jeu le 2026-09-07) | voir git log ; moteur a9267735 |
| E5 caméra | ✅ close (runtime VALIDÉ par l'utilisateur le 2026-08-26) | cc1fc60 + 1507afc |
| E6 contrôle joueur | ✅ close (livrée par anticipation dans E4.c, le 2026-08-26) | voir E4.c |
| E7 mutation de tuiles | ✅ close (validée en jeu) | `326917e`, `9493b78`, moteur `1c5bf445`+`1215f3b`, `e5d73bb` |
| E8 profondeur murs/sols moteur | ✅ close (livrée par `AddSortedOverlayTile`, écrit en E9 ; variante par couche réfutée sur 483 cartes) | préalable `5d66e10` |
| E9 backdrops moteur | ✅ close (validée en jeu) | E9.a `82ad020`,`75dc032`,`394cf55`,`14d94e0` ; E9.b moteur `dcbb55ff`+`29a84e2`, DLL `3798b75`, amendement `975248c`, bascule `e808568` ; E9.c `71c57da`, moteur `0be1e9d2`, parent `0458c6b` |
| E10 fondu/transitions moteur | ✅ close (validée en jeu le 2026-09-01) | moteur `1f837ed6`, parent `96f440e` + `692ec4c` + `767e9e6` |
| E11 audio | ✅ close (validée en jeu le 2026-08-30) ; **E11.b ✅ validée en jeu le 2026-09-07** avec T7 ; **suites D-E13D-37 (mixage stéréo exact, son coupé, `IsBgmActivated`) livrées le 2026-09-25, recette de l'auteur en attente** ; **suite S1 (démarrage BGM fidèle à l'exécutable) livrée le 2026-09-25, validée en jeu et mergée le 2026-09-26** | `3b1eb24` ; `0b1d2d9` + analyseur `f216b32` ; E11.b `3507527`, `1985846`, `e159d0d` ; T7 `8773965` ; clôture `1f7e122` ; suites : `docs/plan-audio-mix-exact-muet.md`, parent `f06b1bd`…`88c19ce`, moteur `7c017ae5`, `af5246ca`, analyseur `18a8546`, `8348d7f`, `b79b45a` ; S1 : `docs/plan-bgm-demarrage-binaire.md`, ADR-0004, parent `acba5ea`…`417d766`, analyseur `e495d7f` |
| E12 dialogues Yarn + MGUI | ✅ close (validée en jeu le 2026-09-02) — **E12.c** (fidélité fine) datée « plus tard » | E12.d `774255f` ; voir `plan-e12-dialogues.md` |
| **E9.d mode cellulaire des fonds** | ✅ close (validée en jeu le 2026-09-07 sur 420, 391, 271, 443) | convertisseur `e1b9844`, moteur `53fed7df`+`342377a5`, DLL `ae30b42` |
| E13.a HUD, la jauge permanente | ✅ close (validée en jeu ; C4 et C5.b le 2026-09-20) | parent `329b19b`, `d9fac87`, `3cf8dd9`, `babb16f`, `48df065`, `8cb52c9`, C4 `7d6a37a` mergée `8dcfd81` ; moteur `19fcd84c` |
| E13.b HUD, fonds des cases arme et accessoire | ✅ close (C6, validée en jeu) | `ae8a24e` |
| E13.c HUD, icônes d'arme et d'accessoire | ✅ close (S4 passée en jeu le 2026-09-21) | analyseur `6176ea3`, `a0904b8`, `c304201`, mergé `c204009` ; parent `0f1cfd4`, `7018e6a`, `0135c92`, `cdb7097`, mergé `ea633ad` |
| E13.d inventaire principal (puis sous-inventaire et L1/R1) | ✅ close (principal validé en jeu le 2026-09-24 ; sous-inventaire, L1/R1 et suites SI7-SI12 validés le 2026-09-25, mergés par l'auteur dans `main`) | `docs/plan-e13d-inventaire.md` ; `docs/plan-e13d-sous-inventaire.md` : analyseur `8f403d5`, parent `45bb0e2`, `a3901af`, `dc3fe1a`, `5f12e53`, suites `4e411ef`…`192f497`, merge `3537807` |
| E14 IA native | ⏳ | |
| E15 conversion hybride | ⏳ | |
