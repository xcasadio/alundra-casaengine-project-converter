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
| D8 | **Pipeline graphique — vers le moteur** : profondeur murs/sols (overlay DLL → `TileMapComponent`/tri moteur), backdrops/parallaxe/ondes (`BackdropRenderer` DLL + `ScrollParameters` → composant moteur), fondu/teinte/transitions (→ post-process ou composant de transition moteur). Le palette swap reste hors plan. | Trois chantiers moteur, chacun avec plan-verifier. |
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
étape qui change l'architecture (E1) et sert ensuite de non-régression.

### E1 — Scripts par entité, MapEvents dans le world ⏳ (DLL)

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

### E2 — Héros : pawn possédé ⏳ (convertisseur + DLL)

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

### E3 — Collisions : champ de hauteur depuis `AlundraCells` + mover ⏳ (moteur, plan-verifier)

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

### E4 — Déplacement scripté des entités ⏳ (convertisseur + DLL)

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

### E5 — Caméra suivant une entité désignée ⏳ (DLL, éventuellement moteur)

- **But** : le plan sur la mouette, la descente avec le bloc 10, le suivi des marins 11/12, le retour
  sur Alundra.
- **Contenu** : `CameraTargeted2dComponent` (Target = entité, dead zone, limites de map) remplaçant la
  caméra debug ; `g_entityFollowedByCamera` comme variable du world (0x67/0x68/0x69) ; destruction de la
  cible gérée ; `InitializeScrollingMode` et limites.
- **Acceptation** : au runtime, la caméra suit chaque cible de la chronologie §0.
- **Dépendances** : E1 (E4 pour voir les mouvements). **À valider** : `CameraTargeted2dComponent`
  dérive de `Camera3dComponent` (perspective) — pixel-perfect à vérifier ou à faire évoluer.

### E6 — Contrôle joueur : verrou / libération ⏳ (DLL)

- **But** : 0x10 retire le contrôle, 0x11 le rend ; jalon « l'intro se joue jusqu'au contrôle ».
- **Contenu** : `g_playerControlFlags` dans `AlundraGameState` ; pont vers `PlayerInput.IsInputEnable`
  / `CharacterControlMode.Script` ; branche verrouillée de `MovePlayer`.
- **Acceptation** : au runtime, le pad est inerte jusqu'à la frame de 0x11 puis déplace Alundra.
- **Dépendances** : E2 (E4/E5 pour la scène complète).

### E7 — Mutation de tuiles à chaud ⏳ (moteur, plan-verifier)

- **But** : la trappe du marin 15 (0x85), les portes B 130-133 (0x55/0x54).
- **Contenu** : moteur — API publique de mutation de tuile (id, collision, propriétés) avec
  reconstruction du rendu, du champ de collision et de la grille de navigation pour le rectangle
  modifié (`TileMapComponent` n'expose qu'un overlay aujourd'hui) ; DLL — 0x85 = copie de cellules
  (`GameEngine.cs:2239-2300` : walkability, ground, slope, height, TileId, pile de murs), 0x55/0x54.
- **Acceptation** : test moteur de mutation ; au runtime, la trappe s'ouvre et se referme.
- **Dépendances** : E3 (champ), E8 (si les murs passent au moteur avant).

### E8 — Profondeur murs/sols dans le moteur ⏳ (moteur, plan-verifier)

- **But** : retirer `WallPlacementOverlay` et l'interleave de la DLL ; le `TileMapComponent` / le tri de
  profondeur moteur rendent murs, sols et sprites dans le bon ordre.
- **Contenu** : à concevoir avec `CasaEngineMonogame/docs/engine/tilemaps-gestion-profondeur.md` ;
  convertisseur — émettre la donnée de placement des murs sous la forme attendue par le moteur.
- **Acceptation** : map 389 visuellement identique avant/après (captures comparées).
- **Dépendances** : aucune (indépendant du gameplay).

### E9 — Backdrops, parallaxe, ondes dans le moteur ⏳ (moteur, plan-verifier)

- **But** : retirer `BackdropRenderer` de la DLL ; un composant moteur de couches défilantes
  (`ScrollParameters` : vitesses, périodes, déformation).
- **Acceptation** : map 389 (mer) identique avant/après.
- **Dépendances** : aucune.

### E10 — Fondu, teinte, transitions dans le moteur ⏳ (moteur, plan-verifier)

- **But** : `WarpPlayer` effet 0 (fondu 16 frames à l'entrée), teinte plein écran, transitions de map.
- **Acceptation** : fondu d'entrée visible sur la 389.
- **Dépendances** : aucune.

### E11 — Audio ⏳ (DLL)

- **But** : BGM de la 389 (`LoadMapSounds`), SFX 44/45/46/61 de 0xBD, streaming.
- **Acceptation** : sons audibles aux frames de la chronologie.
- **Dépendances** : E1.

### E12 — Dialogues Yarn + boîte MGUI ⏳ (convertisseur + moteur + DLL)

- **But** : parler aux marins (slot F, Tick 140 gardé par 0x800C).
- **Contenu** : convertisseur — `.yarn` par map, un nœud par chaîne (D6), `DialogueAsset` compilé ;
  moteur — vues de dialogue MGUI (`DialogueBoxView`, `ChoiceListView`, ⏳ dans
  `yarn_spinner_integration.md`), police `font3.fnt` ; DLL — 0x0D/0x39/0x44/0x50/0x51/0x5C sur
  `YarnDialogueRunner`/`DialogueService`, slot F via `g_activeCollisionEntity`, `MessageBox`/`MenuOpen`.
- **Acceptation** : dialogue du marin 12 jouable avec choix.
- **Dépendances** : E2, E6. **À valider** : nommage des nœuds, codes de contrôle (`\N`, `\C#`…) →
  balises Yarn, chasse fixe de la police.

### E13 — HUD MGUI ⏳

- **But** : cœurs/magie/argent (`HudManager`, `BALANCE.BIN` exporté en Phase 7).
- **Dépendances** : E12 (infrastructure MGUI).

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
  `dotnet test Alundra.Tests -c Release` (233) ; `dotnet test alundra-casaengine-project-converter.Tests -c Release`
  (115) ; moteur : `CasaEngine.Tests` (18 échecs préexistants, tout nouvel échec = régression).

## 6. Suivi

| Étape | Statut | Commit |
|---|---|---|
| E1 scripts par entité + MapEvents | ✅ (verifier CONFIRMED ; visuel runtime à valider par l'utilisateur) | 92f1be5 |
| E2 héros pawn | ⏳ | |
| E3 collisions champ de hauteur | ⏳ | |
| E4 déplacement scripté | ⏳ | |
| E5 caméra | ⏳ | |
| E6 contrôle joueur | ⏳ | |
| E7 mutation de tuiles | ⏳ | |
| E8 profondeur murs/sols moteur | ⏳ | |
| E9 backdrops moteur | ⏳ | |
| E10 fondu/transitions moteur | ⏳ | |
| E11 audio | ⏳ | |
| E12 dialogues Yarn + MGUI | ⏳ | |
| E13 HUD | ⏳ | |
| E14 IA native | ⏳ | |
| E15 conversion hybride | ⏳ | |
