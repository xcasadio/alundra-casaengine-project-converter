# Plan — Restructuration de la DLL de gameplay Alundra

Statut : 🚧 ; enveloppe READY ; **R0 faite et CONFIRMED le 2026-10-17** (recettes de l'auteur à faire) ; **R1 faite et CONFIRMED le
2026-10-17** (`1c5b741` à `e188a58`, ADR-0075) ; R2 détaillée et READY. Démarre après E20.e0a4 (plan E20, §1.16.4).

Décisions : voir ADR-0075 (`docs/decisions/0075-the-gameplay-dll-is-restructured-one-folder-one-namespace-no-alundra-prefix.md`) ; table des noms : `tools/dll-structure/table.md`.

## 0. Décisions de l'auteur

- **D-R-1** (2026-10-16) — La DLL est restructurée : un dossier = un namespace `Alundra.<Domaine>`, en anglais ; plus de préfixe `Alundra`
  dans les noms de types ; les tests suivent la même arborescence ; les très gros fichiers ne sont découpés que plus tard, dans un chantier
  à part. La proposition de l'inventaire du 2026-10-16 est validée (arborescence du §2), avec `Hero` et `Maps` à la place de `Player` et
  `World`.
- **D-R-2** (2026-10-16) — Le moteur crée un objet « script » (proxy d'entité, proxy de monde, contrôleur du joueur, mode de jeu, view model
  d'écran) **par le nom complet de sa classe** (namespace compris) et **seulement dans l'assembly de gameplay**, jamais ailleurs. C'est un
  défaut du moteur, corrigé dans le moteur (tranche R0).
- **D-R-3** (2026-10-16) — Les types du moteur aujourd'hui nommés par chaîne sont créés par le code : un `player_controller_class` vide crée
  directement le `PlayerController` du moteur, sans recherche ; le `.gameMode` de RPGDemo passe à vide ; les démos et SandBoxGame posent
  `ScriptArcBallCamera` par une API du moteur (`Entity.SetGameplayProxy`), plus par un nom.
- **D-R-4** (2026-10-16) — Un nom introuvable, ambigu ou du mauvais genre lève une **exception au chargement**, qui cite la clé, le fichier,
  la valeur et l'assembly consultée, avec un indice (« vouliez-vous dire X.Y.Z ») quand un type de même nom court existe.
- **D-R-5** (2026-10-16) — **Deux exports** : R0 seul d'abord (moteur, puis données réexportées avec les noms complets actuels,
  `Alundra.Scripts.…`, vérifiable en jeu), puis la restructuration, puis un second export pour les classes citées par les données (R4).
- **D-R-6** (2026-10-16) — Les annexes d'oracles sont figées (preuves copiées avec sommes de contrôle) : leurs scripts ne sont pas corrigés,
  l'ADR du chantier donne la table ancien chemin → nouveau chemin ; les scripts de `research/` sont corrigés.
- **D-R-7** (2026-10-16) — L'auteur accorde une relecture de clôture de plus pour R0 (après la révision 3).
- **D-R-8** (2026-10-16, réponse à Q-R-1) — `chantier/e20-heros` est fusionnée dans `main` d'abord (avec les sous-modules : moteur et
  analyseur, pointeurs vérifiés), puis le chantier part de `main` ; la fusion se fait sur le feu vert explicite de l'auteur, une fois
  E20.e0a4 close, par une branche d'intégration puis une avance rapide. **Feu vert donné par l'auteur le 2026-10-16, pour après
  E20.e0a4.** Relevé du 2026-10-16 : avance rapide pure partout (parent : `main` ancêtre de `chantier/e20-heros`, 293 commits ; moteur : `main`
  `b2205d9f` ancêtre de `781d3527`, 33 commits ; MGUI `develop` et analyseur `master` déjà aux commits pointés) ; mise à jour des
  références par une avance rapide qui refuse tout autre cas (`git fetch . <branche>:main`), sans toucher l'arbre de travail.
  **Fusion faite le 2026-10-16** après la clôture d'E20.e0a4 (`37a6df7`) : parent `main` `8e023d8..37a6df7` (pointeurs moteur `781d3527`,
  analyseur `fc16872`) ; moteur `main` `b2205d9f..781d3527` ; MGUI `develop` et analyseur `master` inchangés ; rien poussé. Branche du
  chantier : `chantier/restructuration-dll`, créée depuis `main`.
- **D-R-9** (2026-10-16) — R0 démarre sur le texte de la révision 4, sans autre relecture du plan : le plan propre du moteur
  (`CasaEngineMonogame/ai-agent/tasks/`) recense d'abord, dans le code, **tous** les filets qui rattrapent la création d'un objet script
  (moteur et DLL) et applique la règle de D-R-4 à chacun ; un filet que la règle ne peut pas traverser sans changer un autre comportement
  est un arrêt et une question.
- **D-R-10** (2026-10-17, réponse à Q-R-2) — Dans l'**aperçu de l'éditeur**, une entité dont le script est introuvable reste écrite au
  journal et omise, comme aujourd'hui : l'exception de D-R-4 sort en jeu et en session de jeu de l'éditeur, pas dans l'aperçu (option A :
  un drapeau de la politique d'exécution, faux pour l'aperçu). **En plus, l'éditeur affiche une icône d'avertissement à côté du nom du
  script** quand ce nom ne se résout pas. Le plan du moteur l'ajoute à R0 (son O2 est fermé par cette décision).
- **D-R-11** (2026-10-17, précise D-R-10) — L'éditeur n'affichait plus le script : la section « Script » de l'inspecteur WPF a disparu
  le 2026-03-30 avec le projet WPF (moteur `dc2f4d25`) ; l'auteur : « cela doit être affiché dans l'inspector obligatoirement ; c'est un
  bug ». R0 la rétablit (choix du script, annulable, avec l'icône). Dans l'aperçu, une entité **et le monde** dont le script ne se résout
  pas sont **gardés sans script** (visibles, sélectionnables, l'icône sur leur ligne Script), au lieu d'être omis ou de faire planter
  l'éditeur ; en jeu et en session de jeu, l'exception sort. « Game Mode » et « Default Pawn », perdus en même temps, restent hors de R0.
- Règle du moteur (D-R-2), notée aussi en mémoire : « le moteur, quand il crée les objets script, ne doit chercher que dans la DLL ».

## 1. Faits établis (découverte du 2026-10-16, lecture seule)

Relevés et scripts : scratchpad de la session,
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/dll-structure/`
(`report_inventory.md` et `inventory/inventory.md` : les 129 fichiers ; `report_couplings.md` et `couplings/` : les couplages par nom ;
`check_names.py` : le contrôle des collisions ; `report_r0_engine.md` et `r0-engine/` ; `report_r0_data.md` et `r0-data/`, dont
`predict_names.py`).

**La DLL.** 132 fichiers suivis : 129 `.cs` dans `Alundra/Scripts/`, tous au namespace `Alundra.Scripts`, plus `Plugin.cs` (namespace
`Alundra`), `InternalsVisibleTo.Tests.cs` et `Alundra.csproj` ; 45 390 lignes ; 220 types de premier niveau, dont 148 préfixés.
`Alundra.csproj` n'a aucun `<Compile>` : déplacer des fichiers ne demande aucune édition du projet. 311 des 329 fichiers de tests et
`tools/AlundraTestSaves/Program.cs` font `using Alundra.Scripts;` ; l'outil est dans la solution, donc tout déplacement doit le suivre.

**La fabrique du moteur** (`CasaEngineMonogame/CasaEngine/Framework/Assets/ElementFactory.cs:70-92`) cherche par nom court, sans casse, dans
tout l'AppDomain sauf les assemblys collectibles, puis dans les assemblys de scripts enregistrées, et prend le premier trouvé ; un nom absent
lève `ArgumentNullException`, un type du mauvais genre donne `null` en silence (`:25`).
- Créations d'objets script depuis une chaîne : proxy d'entité (`script_class_name`, `Entity.cs:583`, création `:214`, la comparaison `:212`
  sur `GetType().Name`) ; proxy de monde (`World.cs:938`, `:332`) ; contrôleur du joueur (`player_controller_class`,
  `PlayerStartupSettings.cs:18`, `World.cs:440`) ; mode de jeu (`mode_class_name`, `GameplayModeAsset.cs:18,25`, inutilisé par Alundra) ;
  view model de l'aperçu d'écran (`view_model_type`, `UIScreenDesignTimeDataLoader.cs:87-113`, éditeur seulement).
- Types du moteur qui restent sur l'ancien chemin : le `"type"` des composants (`Entity.cs:589,594`, `SceneComponent.cs:442`), le dialogue
  « Add Component » de l'éditeur.
- La DLL de gameplay : au runtime `AssemblyManager.Load` (`AssemblyManager.cs:13,24`, `Assembly.LoadFile`) ne l'enregistre pas comme
  assembly de scripts ; dans l'éditeur `EditorScriptAssemblyService.cs:48` l'enregistre (`RegisterScriptAssembly`, contexte collectible,
  désenregistrement `:66`).
- Types du moteur nommés par chaîne aujourd'hui : le `PlayerController` par défaut (`PlayerStartupSettings.cs:9`, et
  `Projects/RPGDemo/RpgGameMode.gameMode:5`) ; `ScriptArcBallCamera` (`CasaEngine.Demos/Demo.cs:29`, 17 démos ;
  `Projects/SandBoxGame/SandBoxGame.cs:53`). RPGDemo porte aussi 8 noms courts de ses propres scripts dans 6 fichiers de données et
  `RPGDemo/Weapons/ThrowableWeapon.cs:34`.
- Tests du moteur concernés : `GameplayProxyStateSerializationTests` (9 sites), `GameplayModeRunnerTests` (un type imbriqué),
  `LocalMultiplayerTests:149-181` (le `PlayerController` du moteur), `UIScreenPreviewBuilderDesignTimeDataTests` (un view model imbriqué),
  `ElementFactoryReloadTests` (`"TestScriptProxy"`) ; les composants par nom court restent verts (`TransformComponentTests:27`,
  `MigratedCollisionAssetTests:40`).
- Docs du moteur qui posent la règle du nom court : `screen-authoring-conventions.md:229,237-242`, `gameplay-csproj-scaffolding.md:162-165`,
  `gameplay-mode.md:28,30,45`, `play-in-editor.md:55-58`. Prochain ADR libre du moteur : **ADR-0075**. Le sous-module est au commit
  `781d3527` (branche `chantier/e20-heros`) ; une modification locale de l'auteur (`CasaEngine.Launcher/Program.cs`) ne doit jamais partir
  dans un commit.

**Les données.** Les noms cités, écrits en dur par le convertisseur (il ne référence pas la DLL) : `script_class_name` des 483 `.world`
(`WorldWriter.cs:142`, écrit `:380`), des 395 `.entity` (`SpriteWriter.cs:406`), `player_controller_class` de `Alundra.gameMode`
(`PlayerSetupWriter.cs:49`, `:84`) ; et, écrits à la main, les six `alundra-project/UI/Screens/*.design.json:2` (`view_model_type`). Tests qui
verrouillent ces chaînes : `PlayerSetupWriterTests.cs:53,60`, `SpriteWriterBodyPrefabTests.cs:59,141` (aucun ne verrouille le nom des
`.world`) ; côté DLL, six tests d'écran comparent `view_model_type` à `nameof` ; 65 lignes `GameplayProxyClassName = nameof(…)` dans
`Alundra.Tests`, 3 dans la DLL (`AlundraEntitySpawnFactory.cs:438,509,588`). 44 fichiers de tests (78 arcs `Prefabs: true`) chargent les
préfabs réels de l'export : le moteur, l'export et la DLL doivent concorder à chaque passage des tests. Les sauvegardes ne contiennent
aucun nom de type. L'export complet dure environ une minute (`report.json`) ; le protocole de preuve existe
(`docs/plan-e20-annexe/e20-e0x/manifest.py`, `diff_manifests.py` : manifeste SHA-1, chemins autorisés).

**Les collisions** (contrôle de `check_names.py` contre le moteur, MGUI, MonoGame, les paquets et la BCL) : sans préfixe, quatre noms
collisionnent (`PlayerController`, `MusicPlayer`, `Random`, `SoundBank`) ; les 143 autres sont libres. Un namespace `Alundra.N` masque tout type
`N` importé : noms de dossiers interdits `World`, `Player`, `Camera`, `Dialogue`, `TextBox`, `Text`, `Debug`, `Math`, `Entity`.

## 2. Arborescence cible et règles de nommage

```text
Alundra/
  Plugin.cs, Properties/InternalsVisibleTo.cs
  Maps/          WorldProxy (+ MapEvent), IScriptHost, IEntityWorldContext, WorldIndexTable, MapSizeReader, MapSpriteIndices
    Loop/        LogicClock, FrameSyncPasses, GameplayFreeze
  Session/       GameState, PlayerStats, ChapterFlags
  Entities/      EntityScriptProxy (+ .HitPass), EntityFlags, EntityStatus, EntitySlotTable, EntitySearchService, EntityDepthKey,
                 SpriteRecordCatalog, AnimationTables
    Spawning/    EntitySpawnFactory, EntityRecordMapper
  Events/        EventProgramRunner, IEventProgramRunner (+ NoOp), EventProgramState, EventProgramDocument, EventOpcodeSizeTable, ScriptHelper
  Ai/            NativeSpriteRegistry, EnemyHelpers ; Enemies/ (BeannoideAi, MurutaAi) ; Projectiles/ (WaveSwordAi) ; Objects/ (SaveBook)
  Hero/          PlayerManager, HeroController, PadState, TickPad, PlayerAnimationEffects
  Combat/        Damage, HitPass, HitOpcodes, BalanceTables, FrameBoxTimeline
  Items/         ItemTables, EquipmentRecords, WeaponTables, LootRoll
  Rng/           GameRandom, LibcRandom
  Physics/       ScriptedMotion, CellsCollisionField, EntityCollision, EntitySupport, TerrainProbe, MovementObstacleProbe
  Cells/         CellStore, CellVisualSync, WallPlacementOverlay
  Cameras/       CameraDirector, CameraMath, CameraSway
  Rendering/     Backdrops/ (BackdropStage, BackdropDocument, BackdropLoader) ; Effects/ (EffectBank, EffectQuadBuilder, EffectReservoir,
                 EffectStage, FrozenEffectAnchor, NativeEffects)
  Audio/         BgmPlayer, BgmFadeDirector, MusicIndexTable, SfxBank, SoundGroupIndexTable, SoundPlayer, SpuVoiceVolume
  Transitions/   WarpDirector, PortalTrigger, PortalScanner, PortalRecord, ScreenFadeDirector
  Dialogues/     DialogueDirector, DialogueBox, DialogueNameBox, ChoiceBox, EtcStringTable, Font3Advances, TextProgress ;
                 Yarn/ (YarnBindings, YarnVariableStorage, DialogueCapturePresenter)
  UI/            Hud/, Inventory/, SubInventory/, Messages/ (les écrans TextBox), Choice/, Speech/ (DialoguePresenter), Saving/ (les écrans de sauvegarde), Shared/
  SaveGames/     SaveGame, SaveGameDirector, SaveGameRules, EngineSaveSlots, ISaveSlots, TestSaves
  Probes/        RandomShadow, SlotShadow, StickProbe
Alundra.Tests/   la même arborescence par domaine, plus Arcs/ (scénarios de bout en bout) et Support/ (outillage partagé)
```

Règles : (1) un dossier = un namespace ; (2) plus de préfixe `Alundra` ; (3) un type principal par fichier, les types secondaires publics
dans leur propre fichier ; (4) un nom n'existe qu'une fois parmi la DLL, le moteur, MGUI, MonoGame, les paquets et la BCL, contrôlé par
script à chaque tranche ; (5) les renommages imposés : `AlundraPlayerController` → `HeroController`, `AlundraMusicPlayer` → `BgmPlayer`,
`AlundraRandom` → `GameRandom`, `AlundraSoundBank` → `SfxBank`, `AlundraEventProgramRunner` → `EventProgramRunner` (l'actuel
`EventProgramRunner.cs` devient `IEventProgramRunner.cs`) ; (6) ne changent pas : le nom d'assembly `Alundra`, les clés de données
(`AlundraCells`, `AlundraWallPlacements`, `AlundraFloorPlacements`, `AlundraCompanionJson`), les noms d'assets, le format des sauvegardes ;
(7) les plans, ADR et annexes historiques ne sont pas réécrits (D-R-6).
La lecture de la règle (3) (types secondaires publics, types internes, type principal d'un fichier) est écrite dans l'ADR-0075 ; la table complète par type est `tools/dll-structure/table.md`.

## 3. Enveloppe du programme

| # | Tranche | Résultat | Prérequis |
|---|---|---|---|
| R0 | Moteur : objets script par nom complet, DLL seule ; premier export | le moteur ne résout plus un objet script ailleurs que dans la DLL, par nom complet ; les données portent les noms complets actuels | E20.e0a4 close |
| R1 | ADR et table des noms | l'ADR du chantier (règles, arborescence, table complète ancien → nouveau nom et chemin, générée), le script de contrôle des collisions versé | R0 |
| R2 | Déplacements | chaque fichier dans son dossier et son namespace, sauf les 9 types cités par les données (restent dans `Alundra.Scripts` jusqu'à R4) ; aucun changement de comportement | R1 |
| R3 | Renommages | plus de préfixe, sauf les 9 types cités par les données ; préfixes de journal et tests qui les lisent suivis | R2 |
| R4 | Les 9 types cités par les données ; second export | `WorldProxy`, `EntityScriptProxy`, `HeroController` et les 6 view models à leur place et sous leur nom ; convertisseur, `design.json`, export | R3 |
| R5 | Tests en miroir | `Alundra.Tests` dans la même arborescence, sans préfixe | R4 (peut suivre R2 et R3 domaine par domaine si la relecture de R1 le préfère) |
| R6 | Documentation vivante | `docs/formats/`, guides, `stick-probe.md`, scripts de `research/` | R4 |

**Règles du programme.**
- Aucune autre tranche (E20, E14, E19) pendant le chantier : il touche tous les fichiers de la DLL et des tests.
- Chaque tranche : suites complètes `Alundra.Tests` en Release (copie neutralisée) et en Debug, zéro échec ; aucun test ne change de
  valeur attendue (seuls les noms, chemins et `using` bougent, plus les valeurs écrites par R0 et R4) ; garde des traces ; `cmp` de la DLL
  Debug ; le contrôle des collisions vert ; une recette courte de l'auteur en jeu (nouvelle partie jusqu'au premier écran jouable, F5, F6,
  F9).
- Déplacements par `git mv` (l'historique reste suivi) ; renommages par outil (Roslyn ou l'IDE), jamais par remplacement de texte global
  (des clés de données portent le préfixe sans être des types).
- Branche : depuis `main`, après la fusion d'E20 (D-R-8).
- Budget : R2 et R3 sont mécaniques (`mech-executor`, une passe par domaine, un commit par domaine) ; R0 et R4 demandent du jugement
  (`executor`) ; `plan-verifier` avant chaque tranche ; un verifier frais après chaque tranche, deux pour R0 et R4 (moteur, données).
- Retour arrière : `git revert` de la tranche ; pour R0 et R4, revert du commit parent et réexport depuis le commit précédent.

## 4. Tranche R0 — Moteur : objets script par nom complet, DLL seule ; premier export 🧪 (faite et CONFIRMED le 2026-10-17 : moteur `d4524e84` à `fcc5280b`, parent `146e1d8`, ADR-0075 du moteur ; restent les recettes de l'auteur, en jeu et dans l'éditeur)

**Relecture (2026-10-16).** Enveloppe (§0 à §3) : **READY**. R0 : **REVISE**, trois blocages corrigés (FIX, révision 1) : (1) le lieu
de l'exception de D-R-4 et le chemin de son contexte, sans casser les chargements sans DLL (vérification du convertisseur) ; (2) un seul
propriétaire de l'enregistrement de l'assembly par chemin (le rechargement à chaud de l'éditeur) ; (3) la prédiction de l'export versée
dans une annexe avant tout code (T0).
**Révision 2 (2026-10-16).** Seconde relecture : **REVISE**, un blocage ; disposition de la session principale (cap de deux relectures
automatiques atteint) : **FIX** — le fichier du message de D-R-4 ne peut pas venir du catalogue par l'identifiant : un clone de préfab
reçoit un `Id` neuf et perd son `AssetId` (`ObjectBase.cs:37-42`, `EntityReference.cs:58,75`), et une entité en ligne n'a pas encore son
monde à `Initialize` (`World.cs:788-789`). La règle d'origine du fichier est réécrite sur les vrais chemins, avec les tests qui la
prouvent. Nouvelle époque de relecture : une seule relecture de clôture suit.
Relecture de clôture : **REVISE**, un blocage : la boucle `World.InternalAddEntities` (`World.cs:784-801`) rattrape déjà toute exception de
l'`Initialize` d'une entité (`catch (Exception e) { Logs.WriteException(e); }`) ; elle couvre les entités en ligne, celles des références
de préfab (`:297-304`, `:471-475`) et le pion par défaut (`:380`, `:384`) : l'exception de D-R-4 serait journalisée et avalée, l'entité
omise. **Révision 3 (2026-10-16)**, conforme à D-R-4 : un type d'exception dédié traverse ce filet par un filtre ; toutes les autres
exceptions gardent le comportement d'aujourd'hui. Le cap de relecture étant atteint, la tranche est en pause jusqu'à ce que l'auteur
accorde une relecture de clôture de plus.
Relecture accordée (D-R-7) : **REVISE**, un blocage : la DLL crée ses entités elle-même (`AlundraEntitySpawnFactory.cs:434-443`,
`:507-514`, `:588-593` appellent `entity.Initialize()`, où le proxy est créé) sous trois `catch (Exception ex)` qui journalisent et
omettent l'entité (`AlundraWorldProxy.cs:1093`, `:3401`, `:3531`) ; le proxy existant, l'`Initialize` du moteur ne le recrée plus : sur le
chemin réel d'Alundra, l'exception de D-R-4 serait avalée. **Révision 4 (2026-10-16)** : la même règle aux trois sites de la DLL, et un
test du chargement réel par `AlundraWorldProxy`.

**But.** Le moteur crée un objet script depuis une chaîne en ne cherchant que dans l'assembly de gameplay enregistrée, par nom complet
exact ; les types du moteur se créent par le code ; un nom introuvable, ambigu ou du mauvais genre lève une exception qui dit tout. Le jeu
charge les données réexportées avec les noms complets actuels (`Alundra.Scripts.AlundraWorldProxy`, etc.).

**Partie moteur** (sous-module `CasaEngineMonogame`, branche dédiée `chantier/script-types-full-name` depuis `781d3527`, plan propre dans
`CasaEngineMonogame/ai-agent/tasks/` selon l'AGENTS.md du moteur, ADR-0075 ; la modification locale de `CasaEngine.Launcher/Program.cs`
n'est jamais indexée).
- `ElementFactory` : une recherche dédiée aux objets script (par exemple `CreateScriptObject<T>` et `TryResolveScriptType`) sur le registre
  existant des assemblys de scripts : un cache par `Type.FullName`, casse exacte, construit à partir des seules assemblys de scripts,
  invalidé seulement à l'enregistrement et au désenregistrement (pas de reconstruction en plein jeu, pas d'allocation par appel au spawn :
  AGENTS.md du moteur §9.3) ; seuls les types publics de premier niveau, avec un constructeur sans paramètre et du bon genre, sont
  acceptés (D-R-4 sinon). Le cache ne doit pas épingler le contexte collectible de l'éditeur.
- **Enregistrement de l'assembly de gameplay : un propriétaire par chemin.** Runtime : `AssemblyManager.Load` enregistre l'assembly
  **seulement avec le chargeur par défaut** (`Assembly.LoadFile`) ; s'il recharge, il désenregistre d'abord celle qu'il avait enregistrée.
  Avec un chargeur fourni (l'éditeur : `GameEditor.cs:326`, `EditorScriptAssemblyService.LoadShadowCopy`), `AssemblyManager` n'enregistre
  rien et ne garde aucune référence : le service de l'éditeur reste le seul à enregistrer et désenregistrer (`EditorScriptAssemblyService.cs:36,
  48, 59-76`), donc le contexte collectible précédent reste collectable au rechargement (`EditorScriptReloadCoordinator.cs:89`).
- **Où part l'exception de D-R-4 : à la création, jamais au chargement.** `Entity.Load` et `World.Load` (`Entity.cs:549,583`,
  `World.cs:925,938`), `PlayerStartupSettings.Load` et `GameplayModeAsset` ne font que lire la chaîne : un chargement sans assembly de
  gameplay enregistrée (la phase 8 du convertisseur, `AssetVerifier.cs:44,48,51` ; les tests du convertisseur ; l'éditeur avant la
  construction de la DLL) continue de marcher. La résolution, et l'exception, ont lieu aux cinq créations : `Entity.InitializePrivate`
  (`:210-214`), `World.CreateGameplayProxy` (`:325-332`), la création du contrôleur (`World.cs:440`), celle du mode de jeu, et
  `UIScreenDesignTimeDataLoader` (`:87-113`), qui ne lève jamais : il convertit l'exception en son résultat `Fail` existant (`:47-49`,
  `:110-118`) en gardant le message. **Le contexte du message** : la clé (`script_class_name`, `player_controller_class`,
  `mode_class_name`, `view_model_type`), la valeur, l'assembly consultée (son nom et son chemin, ou « aucune assembly de gameplay
  enregistrée »), l'indice « vouliez-vous dire » (un type de même nom court dans l'assembly), et le fichier : celui de l'asset de
  l'objet, dans cet ordre : (1) son `FileName` (posé par `AssetContentManager.LoadNew`, `:237-239`, et gardé par le clone,
  `ObjectBase.cs:37-42`) ; (2) sinon `AssetCatalog.Get(AssetId)?.FileName` ; (3) pour une entité en ligne d'un monde (ni `FileName` ni
  `AssetId`), le monde complète le message (ci-dessous) ;
  (4) sinon « fichier inconnu », écrit tel quel. Le proxy du monde cite le `FileName` du monde ; le contrôleur du joueur et le mode de
  jeu citent le `FileName` du `PlayerStartupSettings` ou du `GameplayModeAsset` acquis (le `.gameMode`) ; le view model de l'aperçu cite
  le fichier de données de conception qu'il lit.
- **Le type d'exception et le filet du monde.** L'exception de D-R-4 est un type dédié du moteur, `ScriptTypeResolutionException` (nom
  à confirmer contre les collisions par l'exécuteur), qui porte la clé, la valeur, l'assembly et le fichier. Le `catch (Exception e)` de
  `World.InternalAddEntities` (`World.cs:797-800`) prend un filtre `when (e is not ScriptTypeResolutionException)` : toute autre
  exception reste journalisée et l'entité omise, comme aujourd'hui ; l'exception de D-R-4 sort de la boucle. Pour une entité en ligne
  (sans `FileName` ni `AssetId`), la boucle la rattrape **par ce seul type**, la complète du `FileName` du monde (« entité en ligne du
  monde <fichier> », l'exception d'origine en cause) et la relance ; une entité qui a déjà son fichier n'est pas réécrite. Qui la voit :
  au runtime, le chargement du monde (`GameManager`, `LoadCopy<World>`) la laisse remonter jusqu'au démarrage du jeu, qui s'arrête avec
  ce message ; dans l'éditeur, la frontière que nomme T0 du plan du moteur (arrêt et question si elle n'existe pas). Un chargement réussi
  ne change pas. L'éditeur qui ouvre un monde sans DLL construite : la frontière qui
  rattrape aujourd'hui un échec de création est nommée par T0 du plan du moteur ; s'il n'y en a pas (l'éditeur s'arrêterait), c'est un
  arrêt et une question à l'auteur.
- Appelants basculés : `Entity.cs:214` et la comparaison `:212` en nom complet (sinon chaque `Initialize` recrée le proxy et perd son état
  cloné) ; `World.cs:332` ; `World.cs:440` (vide = le `PlayerController` du moteur créé directement, D-R-3) ; `GameplayModeAsset.cs:18` ;
  `UIScreenDesignTimeDataLoader.cs:113`. Les composants restent sur `Create`/`Load` tels quels.
- D-R-3 : `PlayerStartupSettings` (défaut vide), `RpgGameMode.gameMode` (vide), `Entity.SetGameplayProxy(...)` pour `Demo.cs:29` et
  `SandBoxGame.cs:53` ; RPGDemo : ses 8 noms et `ThrowableWeapon.cs:34` en noms complets.
- Tests du moteur : un initialiseur de module enregistre l'assembly de test ; les cas nouveaux (nom complet résolu ; nom court refusé avec
  l'indice ; nom complet d'un type du moteur introuvable ; assembly non enregistrée ; deux assemblys au même nom complet : ambiguïté citant les
  deux ; mauvais genre ; casse ; désenregistrement puis contexte collecté ; `AssemblyManager` enregistre avec le chargeur par défaut et
  n'enregistre rien avec un chargeur fourni ; **rechargement de l'éditeur** : une assembly de scripts compilée chargée deux fois par
  `AssemblyManager.Load` avec `AssemblyLoader = EditorScriptAssemblyService.LoadShadowCopy` — le premier contexte collectible est
  collecté (`Unload()` vrai) et seule la seconde assembly est enregistrée (dans le projet de tests qui référence `EditorServices`) ; proxy
  cloné gardé, tué par le mutant « `:212` en nom court » ; monde, contrôleur, mode, données de conception (`Fail` avec le message) ;
  **message de D-R-4**, sur les vrais chemins de création : (i) une entité chargée par `AssetContentManager.LoadCopy<Entity>(id).Clone()`
  puis initialisée avec un mauvais `script_class_name` — le message contient le chemin relatif de son asset ; (ii) **le chargement réel
  d'un monde** dont une entité en ligne a un mauvais `script_class_name` lève l'exception, qui nomme le fichier du monde ; (ii-b) le
  chargement réel d'un monde qui référence un préfab au mauvais `script_class_name` lève l'exception, qui nomme le chemin relatif du
  `.entity` du préfab, pas celui du monde ; (ii-c) un monde dont une entité échoue pour une **autre** raison journalise l'erreur, omet
  l'entité et finit de charger, comme aujourd'hui ; (iii) un monde au mauvais `script_class_name` — son fichier ; (iv) un contrôleur au
  mauvais `player_controller_class` — le fichier `.gameMode` ; chaque message contient aussi la clé, la valeur, l'assembly consultée et
  l'indice ; (i) et (ii) sont rouges contre une version qui cherche le catalogue par `Id`, (ii) et (ii-b) rouges contre le moteur
  d'aujourd'hui (l'exception y serait avalée) ; un chargement (`Load`) sans assembly enregistrée ne lève pas ; aucune DLL à la création : message explicite) ; les tests
  existants touchés réécrits (types imbriqués sortis au premier niveau).
- Docs du moteur : les quatre docs du nom court, ADR-0075.
- Validation du moteur (AGENTS.md §6) : les deux `.sln`, `CasaEngine.Tests` construit explicitement et vert, RPGDemo et les démos lancés
  (preuve de démarrage).

**État (2026-10-16) : en pause sur Q-R-2.** Fait : T0 de la partie parent (`be8f2f4`, ci-dessous) ; la branche du moteur
`chantier/script-types-full-name` créée depuis `main` `781d3527` ; le plan du moteur versé (`b4ed711c`,
`CasaEngineMonogame/ai-agent/tasks/script-types-full-name-tasks.md`) ; son T0.1, l'inventaire des filets autour d'une création d'objet
script (`a82118fd` : 13 filets, F1 à F13, avec fichier, ligne et règle ; départ `CasaEngine.Tests` 4 252 / 0 échec). **Arrêt (D-R-9, D8
du plan du moteur)** : dans l'éditeur, hors session de jeu, aucune frontière ne rattrape une exception de création (`Program.cs` de
l'éditeur relance tout ; `GameEditor.Update` ne rattrape que pendant une session de jeu) ; un projet ouvert sans DLL construite
n'enregistre aucune assembly de scripts, et l'aperçu de l'éditeur crée les proxies (`InitializeGameplayOnLoad = true`) : une entité au
script introuvable, aujourd'hui journalisée et omise (`World.cs:797`), ferait planter l'éditeur. Question Q-R-2 (§11) ; rien d'autre n'est
fait : aucun code, le pointeur du sous-module n'est pas commité (le moteur est sur `a82118fd`, branche du chantier).

**T0 de la partie parent** ✅ (faite le 2026-10-16 : annexe `docs/plan-restructuration-annexe/r0/`, export de référence au HEAD
`3f28554` avec `m0`/`m1` identiques sauf `report.json`, 24 104 fichiers ; convertisseur 854 tests, `Alundra.Tests` 5 092 en Release)
(commit de docs, avant tout code du moteur ou du parent). Annexe `docs/plan-restructuration-annexe/r0/` :
`predict_names.py` (copié du scratchpad `dll-structure/r0-data/`), des copies de `manifest.py` et `diff_manifests.py`
(`docs/plan-e20-annexe/e20-e0x/`), avec leurs sommes de contrôle ; le README donne les valeurs attendues : 886 chemins autorisés (les
879 sorties du convertisseur, les 6 `design.json`, `report.json`), +16 octets par fichier (+14 064 sur les sorties du convertisseur,
+96 sur les `design.json`), nombre de fichiers inchangé, `AssetInfos.json` inchangé, `report.json` seul différent au second export ; la
liste des chemins autorisés et les SHA-1 prédits versés ; le manifeste de départ `m0` de l'export actuel.

**Partie parent** (un seul commit parent : le pointeur du sous-module, le convertisseur, les `design.json`, la DLL, les tests et l'export
doivent concorder).
- Convertisseur : les trois littéraux en noms complets actuels (`Alundra.Scripts.AlundraWorldProxy`, `Alundra.Scripts.AlundraEntityScriptProxy`,
  `Alundra.Scripts.AlundraPlayerController`) ; leurs tests ; un verrou nouveau sur le nom écrit dans les `.world` ; les commentaires « simple
  name » corrigés.
- `alundra-project/UI/Screens/*.design.json` : les six `view_model_type` en noms complets.
- **Les trois filets de création de la DLL** (`AlundraWorldProxy.cs:1093`, la boucle du chargement de carte ; `:3401`,
  `SpawnEntityByRecordId` ; `:3531`, `SpawnEntityBySpriteIndex`) : chacun reçoit, avant son `catch (Exception ex)`, un `catch` du seul
  `ScriptTypeResolutionException` qui fait `AbortCreation(entity, added, slot)` puis relance ; toute autre exception garde le journal et
  l'omission d'aujourd'hui. Tests d'`Alundra.Tests` : le chargement réel d'un monde par `AlundraWorldProxy` dont un enregistrement
  désigne un préfab au mauvais `script_class_name` lève l'exception, qui nomme le chemin relatif du `.entity` du préfab (rouge contre la
  DLL d'aujourd'hui : avertissement et entité omise) ; une autre faute à la même création (`CreationFaultHookForTests`) journalise,
  omet et finit le chargement, comme aujourd'hui.
- DLL et tests : `nameof(X)` → `typeof(X).FullName!` là où le nom va dans `GameplayProxyClassName` (3 sites de la DLL, 65 lignes de tests) et
  dans les six tests d'écran ; un initialiseur de module d'`Alundra.Tests` enregistre l'assembly de la DLL ; un test garde-fou lit
  `Alundra.gameMode`, un `.entity` et un `.world` réels et compare leurs chaînes à `typeof(X).FullName`.
- Export 1 (protocole d'E20.e0x : prédiction par `predict_names.py`, manifeste avant, T1 où seul `report.json` change, l'export, comparaison
  à la prédiction, second export identique ; chemins autorisés : les 879 sorties, les 6 `design.json`, `report.json`) dans une fenêtre
  calme : aucun build ni test en cours, ni jeu ni éditeur ouverts (l'auteur les ferme).

**Résultats de la partie parent ✅ (2026-10-17, un commit parent).** Moteur : pointeur du sous-module sur `chantier/script-types-full-name`
`fcc5280b` (T1.1 à T5.1 faites, CasaEngine.Tests 4 314 / 0 échec). Convertisseur : les trois littéraux en noms complets
(`Alundra.Scripts.AlundraWorldProxy`, `...AlundraEntityScriptProxy`, `...AlundraPlayerController`), leurs tests (`PlayerSetupWriterTests`,
`SpriteWriterBodyPrefabTests`) et un verrou nouveau dans `WorldWriterTests` (le `script_class_name` du `.world` écrit et relu par le moteur), les
commentaires « simple name » corrigés. DLL : trois `catch (ScriptTypeResolutionException)` (`AbortCreation` puis `throw;`) avant les trois
`catch (Exception ex)` de `AlundraWorldProxy` (chargement de carte, `SpawnEntityByRecordId`, `SpawnEntityBySpriteIndex`) ; trois `nameof` →
`typeof(AlundraEntityScriptProxy).FullName!` dans `AlundraEntitySpawnFactory`, 69 dans les tests, 6 dans les tests d'écran. Tests nouveaux
(`AlundraScriptNamesInTheDataTests`, 5 cas) : le garde-fou des données (`Alundra.gameMode`, `Alundra.entity`, un `.world`, contre `typeof(X).FullName` et
`ElementFactory.TryResolveScriptType`), le chargement réel de la carte 14 dont les préfabs portent le nom court (l'exception nomme le `.entity` du préfab ;
rouge quand les trois `catch` sont neutralisés par un filtre `when (false)`, restauré octet pour octet), une autre faute à la même création qui journalise,
omet et finit le chargement. Initialiseur de module `AlundraScriptAssemblyRegistration` (la DLL est enregistrée auprès du moteur). Écart : `ArcRun.Dispose`
plantait (`Hero` nul) quand le constructeur échouait après la construction du proxy et masquait l'exception d'origine ; `TotalSlideCount` renvoie 0 sans
héros. Export 1 (fenêtre calme, aucun processus du jeu, de l'éditeur ou de test) : `m1b` (les six `design.json` édités à la main) ne diffère de `m1` que par
ces six fichiers, chacun égal à son SHA-1 prédit ; `m2` contre `m1` : 0 ajouté, 0 retiré, 886 changés, tous dans les 886 chemins autorisés, les
885 SHA-1 prédits égaux (0 écart), `AssetInfos.json` inchangé, 24 104 fichiers ; export 2 (`m3`) : seul `report.json` diffère ; `Verification: PASSED
(20139 loaded, 2477 existence-checked)`, `Errors` 0. Suites : convertisseur 854 / 0 échec ; `Alundra.Tests` 5 097 / 0 échec en Release (copie neutralisée)
et en Debug ; `cmp` de la DLL Debug égal ; garde des traces sans écart. Reste : les relectures fraîches (moteur, données) et la recette de l'auteur en jeu
(nouvelle partie, les entités bougent, F5, F6, F9) et dans l'éditeur lancé (recette de T3.2).

**Vérification (2026-10-17) : CONFIRMED** par trois verifiers frais. **Moteur** (adversarial, avant la partie parent) : les deux `.sln`,
`CasaEngine.Tests` 4 314 / 0 échec, les rouges d'avant rejoués, la portée de tolérance de D9/D11 sur tous ses chemins (chargement, préfab,
proxy de monde, `AddEntity` puis `Update`, refermée après une exception), la ligne Script de l'inspecteur (14 cas, `EntityDetailsScriptRowTests`),
mutants inventés tués ou prouvés équivalents ; **rejeu** : l'annexe et la prédiction rejouées (885 SHA-1 égaux, `m3` identique au manifeste
pris par le verifier), les suites (convertisseur 854, `Alundra.Tests` 5 097 en Release et en Debug), le `cmp`, la garde des traces ;
**adversarial (parent)** : aucun nom court restant dans les données ni dans un chemin du convertisseur, les filets de la DLL, le pointeur,
rien sur `main`, rien poussé. **Aucun constat P0 à P2.** Reportés (P3 et P4, sans changer le candidat CONFIRMED) :
- P3 : la condition « constructeur public sans paramètre » d'`IsInstantiableScriptType` n'a pas de test (un tel type serait listé, puis
  `MissingMethodException` au lieu de l'exception dédiée) ;
- P3 : le `throw;` des filets de `SpawnEntityByRecordId` et `SpawnEntityBySpriteIndex` n'a pas de test dédié (seul le chargement de carte en a) ;
- P3 : `CasaEngineMonogame/docs/editor/ui-screen-editor/screen-authoring-conventions.md:229` garde un exemple au nom court ;
- P4 : la comparaison d'`Entity.cs:233` en `OrdinalIgnoreCase` reste verte (pas de test de casse sur le proxy cloné) ; le fichier de repli
  d'une portée imbriquée non remis à sa fermeture (sans test) ; `AbortCreation` avant le `throw;` de la boucle de carte sans test ; un
  commentaire au nom court dans `alundra-casaengine-project-converter/Writers/SpriteWriter.cs:64` ; le rapprochement d'`OutputSizeBytes` non
  consigné par P1 (la prédiction par SHA-1 et par taille de chaque fichier, elle, est exacte) ; les commits moteur et parent de R0 portent
  « Claude Sonnet 5.5 » au lieu d'« Opus 5.5 » dans leur ligne d'attribution (non réécrits : rien n'est poussé, une réécriture d'historique
  n'est pas demandée).
Constats préexistants, hors tranche, signalés à l'auteur : dans RPGDemo, `NullReferenceException` dans `Entity.Destroy` →
`AnimatedSpriteComponent.OnEnabledValueChange` quand un rocher lancé touche le joueur (~25 s de jeu) ; dans SandBoxGame,
`NullReferenceException` à la première image de `DrawBoxWithEffect` (effet nul) ; `EditorScriptAssemblyService.Unload()` rend faux en Debug
(limite consignée dans l'ADR-0075 du moteur).

**Recettes de l'auteur.** En jeu : nouvelle partie jusqu'au premier écran jouable, les entités bougent (leurs proxies existent), F5, F6,
F9. Dans l'éditeur (T3.2 du plan du moteur) : (a) un monde et une entité montrent leur ligne « Script » (nom complet), sans avertissement ;
(b) un nom de script faussé dans une copie de travail d'un fichier de données de RPGDemo (rétablie après) : le monde s'ouvre, l'entité est
dans la hiérarchie, l'avertissement et son message sont dans l'inspecteur ; (c) l'aperçu d'un préfab au script faussé s'ouvre avec
l'avertissement ; choisir un script dans la liste, puis annuler.

**Acceptation.** L'annexe de T0 existe à un commit antérieur au premier commit de code ; `CasaEngine.Tests` vert et ses nouveaux cas
rouges sur le moteur d'avant (nommés) ; les deux `.sln` du moteur ; démos et RPGDemo démarrent ; `Alundra.Tests` Release et Debug, zéro
échec ; la suite du convertisseur verte et sa phase 8 `Verification: PASSED` sur l'export de R0 ; le `diff_manifests.py` versé, contre la
liste versée des chemins autorisés, sort 0 pour l'export 1, et seul `report.json` diffère à l'export 2 ; les SHA-1 égaux à la prédiction ; recette de l'auteur : nouvelle partie, les entités bougent (leurs proxies existent), F5, F6, F9.

**Arrêts.** Un appelant de la fabrique non recensé qui crée un objet script ; un besoin de garder le nom court ; un fichier exporté hors des
chemins autorisés qui change ; un test qui change de valeur attendue ; un démarrage de démo ou de RPGDemo qui échoue.

**Retour arrière.** Revert du commit parent (pointeur, convertisseur, DLL, tests) et réexport depuis le commit précédent ; la branche du
moteur reste non fusionnée.

## 5. Tranche R1 — ADR, table des noms, contrôle des noms ✅ (faite et CONFIRMED le 2026-10-17, `1c5b741` à `e188a58`, ADR-0075 ; détaillée le 2026-10-16, révision 1 **READY**)

**Relecture (2026-10-16).** R1 : **REVISE**, cinq blocages corrigés (FIX, révision 1) : (1) la table donne pour **chaque type** son
fichier, son namespace et son nom à chaque étape (r2, r3, final), ce qui tranche le cas des types secondaires des fichiers cités par les
données ; (2) `Plugin.cs` et `InternalsVisibleTo.Tests.cs` sont dans la table et les fichiers de la DLL se recensent par git, sans
`bin/`, `obj/` ni `artifacts/` ; (3) le contrôle de l'arbre liste tous les écarts, prend une racine en option, et ses preuves couvrent un
arbre conforme et quatre écarts distincts ; (4) la règle (3) du §2, approuvée, est appliquée et non écartée (voir « Lecture du §2 ») ;
(5) un propriétaire, des chemins exclusifs et un budget. Seconde relecture (fraîche) de la révision 1 : **READY**.

**But.** La règle et la cible écrites une fois pour toutes, avant tout déplacement : l'ADR du chantier avec D-R-1 à D-R-9, l'arborescence,
les règles de nommage, la **table complète** par type (ancien fichier et ancien nom → fichier, namespace et nom à chaque étape), qui sert
aussi de table ancien chemin → nouveau chemin aux annexes figées (D-R-6) ; l'outil qui génère la table et contrôle les noms, versé dans le
dépôt, et dont R2, R3 et R4 se servent comme preuve mécanique.

**Lecture du §2 (arborescence approuvée, règle (3)).** Une entrée de l'arborescence est un fichier ; « (+ X) » veut dire « X dans le même
fichier » (`WorldProxy (+ MapEvent)`, `IEventProgramRunner (+ NoOp)`, `EntityScriptProxy (+ .HitPass)`). La règle (3) (« un type principal
par fichier, les types secondaires publics dans leur propre fichier ») s'applique à tout type secondaire **public** que l'arborescence ne
garde pas par « (+ X) » ; un type secondaire **interne** reste dans le fichier de son type principal, sauf s'il a sa propre entrée dans
l'arborescence (`SlotShadow` dans `Probes/`, `PortalScanner` dans `Transitions/`). Le type principal d'un fichier est celui qui porte le
nom du fichier, sauf `EventProgramRunner.cs` (principal : `IEventProgramRunner`).

**Faits (2026-10-16, prototypes du scratchpad `dll-structure/r1/`, `gen_table.py` et `gen_table2.py`).**
- 129 fichiers sous `Alundra/Scripts/` (aucun manquant, aucun en trop), plus `Alundra/Plugin.cs` (type `Plugin`, namespace `Alundra`,
  ne bouge pas) et `Alundra/InternalsVisibleTo.Tests.cs` (aucun type ; devient `Alundra/Properties/InternalsVisibleTo.cs` en R2) ;
  `Alundra/Screens/` est un dossier vide non suivi, hors table.
- 219 types de premier niveau, dont 148 changent de nom ; 42 fichiers en déclarent plusieurs ; 76 types secondaires prennent leur propre
  fichier (74 publics et les 2 internes de l'arborescence ; `NoOpEventProgramRunner` reste avec l'interface ; 13 internes restent avec
  leur type principal, dont `AlundraMapEvent`) : **205 fichiers** à l'arrivée ; aucun chemin en double à aucune étape, aucun nom en
  double dans un même namespace (hors la classe partielle `EntityScriptProxy`).
- Les fichiers cités par les données (les 9 types, 10 fichiers avec `.HitPass`) restent dans `Alundra/Scripts/`, namespace
  `Alundra.Scripts`, jusqu'à R4. Leurs types secondaires publics (`AlundraPadState`, les view models d'éléments `ChoiceFrameViewModel`,
  `HudImageViewModel`, etc., qu'aucune donnée ne cite : les `.xaml` ne les nomment qu'en commentaire) sortent dès R2 vers leur dossier
  final ; l'interne `AlundraMapEvent` reste dans `AlundraWorldProxy.cs`, prend son nom `MapEvent` en R3 dans `Alundra.Scripts`, et son
  namespace final en R4 avec son fichier.
- Collisions (`check_names.py` sur `couplings/scan_types.py` : sources du moteur, de MGUI, de NvgSharp, types publics des paquets et de la
  BCL lus dans leurs XML de documentation) : **aucune** pour les noms finaux ni pour les segments de namespace. Un type au nom d'un
  segment : `SaveScreen` dans `UI/SaveScreen`, d'où le dossier **`UI/Saving`** (libre ; règle (4) du §2) ; plus aucun cas dans la DLL ;
  dans les tests, la classe imbriquée `Session` (`Alundra.Tests/UI/TextBoxGpuHarness.cs:262`) porte le nom du segment `Session` :
  imbriquée, elle ne masque le namespace que dans le corps de sa classe (avertissement de l'outil, pas un échec).

**Tâches.**
- T1 — L'outil `tools/dll-structure/` (Python, lecture seule sur le dépôt, `python -B`) :
  - `scan_types.py` : le module de lecture des déclarations (repris de `couplings/scan_types.py`, sans ses sorties) : pour la DLL, les
    fichiers `.cs` de `git ls-files --cached --others --exclude-standard -- Alundra` hors `bin/`, `obj/`, `artifacts/`, lus dans l'arbre
    de travail (ou sous la racine `--root`) ; moteur, MGUI et NvgSharp au HEAD de leur sous-module ; paquets et BCL par leurs XML (la liste
    des paquets et versions est écrite dans le script ; un paquet listé sans XML trouvé est une **erreur**, jamais un silence) ;
  - `gen_table.py` : la table depuis l'arborescence du §2 et la lecture ci-dessus (dictionnaires écrits dans le script, repris des
    prototypes), lue à une **révision de base épinglée** dans le script (le HEAD du dépôt au début de R1, après R0 ; la table ne bouge
    plus quand R2 à R4 déplacent les fichiers) ; sortie versionnée `tools/dll-structure/table.json`, une ligne par type : ancien fichier,
    ancien nom, accès, principal ou non, scindé ou non, cité par les données ou non, et pour `r2`, `r3`, `final` : fichier, namespace, nom ;
    plus les deux fichiers sans type de la racine (`Plugin.cs` avec son type ; `InternalsVisibleTo.Tests.cs` → `Properties/InternalsVisibleTo.cs`
    dès `r2`) ; et `table.md` (lisible, par fichier final) ; rejouée, elle redonne les mêmes octets (`git diff --exit-code`) ; elle échoue
    si un fichier n'a pas de type principal connu, si deux types scindés visent un même chemin, ou si un nom revient deux fois dans un
    namespace (hors partielle) ;
  - `check_names.py --table` : les noms prévus contre le moteur, MGUI, NvgSharp, les paquets et la BCL (collision de type ; segment de
    namespace qui masquerait un type de même nom) ; un nom de type égal à un segment de namespace de la DLL ; liste **tous** les cas,
    code de sortie non nul s'il y en a ;
  - `check_names.py --tree --stage r2|r3|final [--root <dossier>]` : l'arbre contre la table à cette étape, en listant **tous** les écarts :
    fichier de la table absent à son chemin de l'étape ; type absent sous son nom de l'étape, ou déclaré dans un autre namespace ou un
    autre fichier ; fichier `.cs` de la DLL hors de la table ; puis les contrôles de collision sur les noms réels ; code de sortie non nul
    au moindre écart ;
  - `selftest.py <dossier de travail hors dépôt>` : construit depuis la table des arbres synthétiques (un fichier par chemin de l'étape,
    `namespace` et déclarations minimales) et vérifie le code de sortie et l'écart signalé de `--tree --stage r2 --root` sur : (a) l'arbre
    conforme (0, aucun écart) ; (b) un fichier déplacé qui garde `namespace Alundra.Scripts;` (1, « namespace ») ; (c) un type sous son nom
    de `r3` à l'étape `r2` (1, « nom ») ; (d) un `.cs` en trop (1, « hors table ») ; (e) un fichier manquant (1, « absent ») ; et
    `--table` sur une copie de la table où un nom final devient `Vector2` (1, « collision ») ;
  - `README.md` (anglais) : usage, source des fichiers, limites (un paquet sans XML n'est pas contrôlé par le script : la construction
    reste juge, une ambiguïté y est `CS0104`).
  Acceptation de T1 : `selftest.py` vert (les six cas) ; `--table` vert ; `--tree --stage r2` sur l'arbre réel d'avant R2 rouge, avec un
  nombre d'écarts égal au nombre de types et de fichiers qui bougent en R2 (compté dans la table) ; la table rejouée identique.
- T2 — L'ADR (le prochain numéro libre de `docs/decisions/README.md`, 0075 au 2026-10-16 ; skill `adr`) : contexte (D-R-1 à D-R-9 et la
  demande de l'auteur), décision (dossiers = namespaces `Alundra.<Domaine>` ; pas de préfixe `Alundra` ; la lecture du §2 ci-dessus pour la
  règle (3) ; les noms spéciaux `HeroController`, `BgmPlayer`, `GameRandom`, `SfxBank`, `EventProgramRunner` / `IEventProgramRunner` ; les
  dossiers interdits `World`, `Player`, `Camera`, `Dialogue`, `TextBox`, `Text`, `Debug`, `Math`, `Entity`, et `SaveScreen` ; aucun type au
  nom d'un segment ; les 9 types cités par les données bougent seuls en R4 avec le second export ; annexes figées, lues par la table),
  conséquences, renvoi à l'ADR moteur de R0 ; la table (`table.md`) jointe à l'ADR ; l'index des ADR ; la ligne « Decisions: see ADR-NNNN »
  en tête de ce plan.
- T3 — Ce plan : §2 corrigé (`UI/SaveScreen/` → `UI/Saving/` ; la lecture de la règle (3) en renvoi à l'ADR), R1 ✅ avec ses résultats.

**Propriétaire, chemins, budget.** Un `executor` (outil à écrire, jugement borné), chemins exclusifs : `tools/dll-structure/**`,
`docs/decisions/0075-*` et `docs/decisions/README.md`, et dans ce plan les seuls §2 et §5 ; R1 commence après la clôture de R0 (prérequis
de l'enveloppe) et n'écrit dans le plan qu'alors. Budget : une passe d'exécution, une passe de correction au plus ; un verifier frais à la
fin. Un commit par tâche.

**Acceptation.** L'acceptation de T1 ; les 131 fichiers d'origine, les 219 types et les 205 fichiers d'arrivée couverts ; l'ADR indexé ;
§2 et ADR et table disent la même chose (`SlotShadow` dans `Probes`, `Saving`, la règle (3)). Aucun fichier `.cs` touché ; aucune
construction requise.

**Arrêts.** Une collision ou un type au nom d'un segment que le §2 ne résout pas ; un fichier sans type principal que la lecture du §2 ne
tranche pas ; une révision de base qui ne contient pas le résultat de R0.

**Résultats (2026-10-17).** T1 `1c5b741`, T2 `f2bdb4b`, T3 ce commit ; aucun `.cs` touché, aucune construction, aucun export.
- Outil `tools/dll-structure/` (`scan_types.py`, `gen_table.py`, `check_names.py`, `selftest.py`, `README.md`, `table.json`, `table.md`), table lue à la
  révision épinglée `5434cf6`. Comptes : 129 fichiers `Alundra/Scripts` + `Plugin.cs` + `InternalsVisibleTo.Tests.cs` (131), **219 types** de premier
  niveau (148 renommés, 42 fichiers à plusieurs types, 76 types scindés, 10 lignes citées par les données = 9 types, `EntityScriptProxy` partielle en deux
  fichiers), **205 fichiers d'arrivée** (identiques aux trois étapes) ; aucun chemin ni nom en double. Identique au prototype du scratchpad (0 écart sur les 219 × 3 étapes).
- `check_names.py --table` : 0 collision, 0 segment masquant un type, 1 avertissement (`Session` imbriqué dans `Alundra.Tests/UI/TextBoxGpuHarness.cs:262`). Un paquet
  listé sans XML est une erreur ; `Portable.Xaml`, sans XML, est nommé dans `PACKAGES_WITHOUT_XML` (limite écrite dans le README).
- `check_names.py --tree --stage r2` sur l'arbre réel d'avant R2 : rouge, **524 écarts** = 196 fichiers absents + 120 `.cs` hors table + 208 types hors place, soit
  exactement le décompte tiré de la table (fichiers d'arrivée sans fichier d'origine à ce chemin, fichiers d'origine hors des chemins d'arrivée, types dont le triplet
  fichier / namespace / nom de `r2` diffère de l'origine).
- `selftest.py` : six cas verts (conforme 0 ; namespace 1 `[namespace]` ; nom 1 `[nom]` ; fichier en trop 1 `[hors table]` ; fichier manquant 1 `[absent]` ; `--table` avec
  `Vector2` 1 `[collision]`) plus la table intacte 0.
- Table rejouée : `git diff --exit-code -- tools/dll-structure` vide. ADR-0075 écrit et indexé ; §2 et ADR et table concordent (`Probes/SlotShadow`, `UI/Saving`, règle (3)).
- Restent : un verifier frais (budget de la tranche) ; la ligne « Statut » en tête et la ligne R1 du §3 disent encore « en cours » / ⏳ (hors des chemins de R1, à mettre à jour par la clôture).

**Vérification de R1 (2026-10-17) : CONFIRMED** par un verifier frais (rejeu de la table au bit près ; couverture recomptée par un lecteur
indépendant : 131 fichiers, 219 types, 76 scindés, 205 fichiers d'arrivée ; règles d'étape ; chaque contrôle de `check_names.py` déclenché à
la main ; `--tree --stage r2` sur l'arbre réel : 524 écarts, ensembles comparés un à un ; `selftest.py` rejoué et ses six cas tués par des
mutations de l'outil ; l'ADR et le plan ; le périmètre du diff). Aucun constat P0 à P2. Reportés : **P3** la liste `PACKAGES` de l'outil
n'a pas 8 paquets avec XML que la DLL tire (Autofac, Cyotek.Drawing.BitmapFont, FontStashSharp.Rasterizers.StbTrueTypeSharp,
Google.Protobuf, MonoGame.Extended.Content.Pipeline, SharpGLTF.Runtime, System.Memory, System.ValueTuple) ni StbImageSharp et
StbTrueTypeSharp (sans XML) : **repris en T0 de R2** ; P3 le selftest ne garde pas tous les contrôles (segment, masquage, `[fichier]`,
paquet sans XML), qui marchent à la main ; P4 deux fichiers non scindés vers un même chemin ne sont pas refusés par `gen_table` (aucun cas
réel) ; P4 des types imbriqués des XML comptés comme de premier niveau (conservateur).

## 6. Tranche R2 — Déplacements et scissions ⏳ (détaillée le 2026-10-17 ; révision 2, relecture de clôture **READY**)

**Relecture (2026-10-17).** REVISE, trois blocages corrigés (FIX, révision 1) : (1) les `using`, les qualifications et les chemins se
calculent depuis l'emplacement **courant** de chaque type, à chaque domaine ; (2) chaque domaine fait deux commits, un `git mv` pur puis
les scissions et les `using`, pour que git suive l'historique de chaque fichier, et `prove_r2.py` apparie les fichiers par la table ;
(3) la cible de construction est la solution `alundra-casaengine-project-converter.slnx` (elle compte `tools/AlundraTestSaves`).
Seconde relecture : REVISE, deux blocages ; cap de deux relectures atteint, dispositions de la session principale, toutes FIX, puis une
relecture de clôture (révision 2) : (4) la règle des `using` couvre la méthode d'extension de la DLL (`EntityStatusExtensions.IsActive`,
appelée par `AlundraHitOpcodes.cs:67` sans nommer `EntityStatus`) ; (5) `apply_r2.py` et `prove_r2.py` appartiennent à l'exécuteur pendant
les domaines, pour corriger les seules règles de `using` et de qualification, par un commit d'outil suivi du rejeu du temps 2.

**But.** Chaque type de la DLL dans le fichier, le dossier et le namespace de l'étape `r2` de `tools/dll-structure/table.json`, **sans aucun
changement de nom de type ni de comportement** ; les fichiers des 9 types cités par les données restent dans `Alundra/Scripts/` (namespace
`Alundra.Scripts`), leurs types secondaires publics sortent vers leur dossier final (`AlundraPadState` → `Hero/`, les view models d'éléments
→ `UI/<écran>/`) ; `AlundraMapEvent` reste dans `AlundraWorldProxy.cs`.

**Faits (2026-10-17, HEAD `e188a58`).** La table : 131 fichiers d'origine → 205 fichiers à l'étape `r2` (76 types scindés) ; 11 fichiers ne
bougent pas (les 10 fichiers cités et `Plugin.cs`) ; `--tree --stage r2` sur l'arbre d'aujourd'hui : 524 écarts (196 absents, 120 hors
table, 208 types hors place). `Alundra.csproj` n'a aucune liste `Compile` (le SDK prend tous les `.cs`) : un déplacement ne touche pas le
projet. Références à suivre : 316 fichiers de `Alundra.Tests` et `tools/` portent `using Alundra.Scripts;` ; les qualifications et `cref`
`Alundra.Scripts.<T>` d'un type qui bouge (par exemple `AlundraWorldProxy.cs:370,375,380,699`, `AlundraSpuVoiceVolume.cs:92-94`,
`TextBoxGpuHarness.cs:472,490,496,501`, `AlundraLogicClockTests.cs:9`, `AlundraWorldProxyLogicClockTests.cs:12,26`,
`AlundraYarnVariableStorageProbeTests.cs:19`) ; le seul test qui lit des sources par chemin : `AlundraHudPresenterTests.cs:442,443,460`
(`AlundraHudPresenter.cs`, `AlundraHudScreen.cs`, `AlundraHudDirector.cs`, tous trois vers `UI/Hud/`). Le convertisseur ne référence pas la
DLL : ses littéraux `Alundra.Scripts.*` nomment les types cités, qui ne bougent pas avant R4 ; ses commentaires qui citent un type qui bouge
(`MusicIndexCatalogReader.cs:17`, `NavigationWriter.cs:23`, `SpriteWriter.cs:92`) suivent en R6 avec la documentation.

**Copie neutralisée** : `-p:CustomAfterMicrosoftCommonTargets=C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets`
(une construction Release qui copie remplacerait la DLL Debug du projet et couperait F5, F6, F9) ; le commit (1) du domaine `UI` laisse
`AlundraHudPresenterTests` rouge jusqu'au commit (2) (il lit les sources par chemin) : le commit (1) est construit, pas testé.

**Propriétaire, chemins, budget.** Un `executor` (le script de déplacement s'écrit une fois, avec jugement ; l'application est ensuite
mécanique, deux commits par domaine). Chemins : `Alundra/**` (hors `bin/`, `obj/`), `Alundra.Tests/**`, `tools/AlundraTestSaves/**`,
`tools/dll-structure/` (T0 ; puis, de T1 à T21, `apply_r2.py` et `prove_r2.py` pour les seules corrections de leurs règles de `using` et de
qualification), ce plan (§6). Aucun export, aucune donnée, aucun fichier du convertisseur, aucun sous-module. Le
jeu et l'éditeur fermés pendant toute la tranche (les constructions Debug copient la DLL dans `alundra-project/`). Budget : une passe
d'exécution, une passe de correction au plus ; un verifier frais à la fin.

**Tâches.**
- T0 (aucun code de la DLL) : (a) compléter `PACKAGES` de `tools/dll-structure/scan_types.py` avec les paquets que la DLL tire
  (`Alundra/obj/project.assets.json`), et `PACKAGES_WITHOUT_XML` avec StbImageSharp et StbTrueTypeSharp ; `check_names.py --table` vert ;
  `gen_table.py` rejoué identique (la révision de base épinglée ne change pas) ; un commit ; (b) le script de l'étape, versé dans
  `tools/dll-structure/apply_r2.py`, en deux temps pour un domaine donné. **Temps 1** (`--move`) : `git mv` des fichiers du domaine
  vers leur chemin `r2`, sans changer un octet (le code d'avant compile tel quel : le SDK prend tous les `.cs`). **Temps 2** (`--fix`) : la
  coupe des types scindés **dont la cible est dans ce domaine** (la déclaration avec ses commentaires de documentation, ses attributs et sa
  ligne vide de tête, copiée à l'octet dans un fichier neuf à son nom, avec l'en-tête du fichier où elle se trouve à ce moment : ses
  `using`, sa directive `#nullable` s'il en a une, et le `namespace` de l'étape ; retirée de ce fichier) ; la ligne `namespace` des fichiers
  du domaine (jamais celle d'un fichier de test) ; puis, pour **tous** les fichiers `.cs` de la DLL, des tests et de `tools/AlundraTestSaves`, les `using` recalculés à partir de
  **l'emplacement courant de chaque type**, c'est-à-dire après les déplacements et coupes déjà faits (ce domaine et les précédents) : les
  namespaces courants des types de la table que le fichier nomme (code sans commentaires ni chaînes, `cref` compris), **et des classes
  d'extension de la DLL dont le fichier appelle une méthode** (aujourd'hui la seule : `EntityStatusExtensions`, `EntityStatus.cs:48,55`,
  dont `.IsActive(` est appelé par exemple par `AlundraHitOpcodes.cs:67` ; le script recense les méthodes `(this …)` de la DLL à chaque
  domaine), sauf le sien, ajoutés
  après les `using` existants dans l'ordre alphabétique ; `using Alundra.Scripts;` gardé tant que le fichier nomme un type **encore** dans
  `Alundra.Scripts` (quel qu'il soit), retiré sinon ; une qualification `Alundra.Scripts.<T>` (code et `cref`) et un des trois chemins de
  `AlundraHudPresenterTests` ne sont réécrits qu'à l'étape où leur type ou leur fichier bouge, vers son namespace ou son chemin du moment.
  Exemple : `AlundraSlotShadow` vit dans `AlundraEntitySlotTable.cs`, qui part dans `Entities/` au domaine `Entities` ; il y est sous
  `Alundra.Entities` jusqu'au domaine `Probes`, où il est coupé vers `Probes/AlundraSlotShadow.cs` (ses utilisateurs importent
  `Alundra.Entities`, puis `Alundra.Probes`) ; de même, les types secondaires publics des fichiers cités sont coupés au domaine de leur
  cible, le fichier cité restant dans `Alundra/Scripts/`. Pas de `global using` interne. Le script refuse de tourner si l'arbre n'est pas
  propre hors des chemins de la tranche. (c) Une preuve versée avec le script, `tools/dll-structure/prove_r2.py`, qui **apparie l'ancien et le nouveau chemin par
  `table.json`** (jamais par la détection de renommage de git) : un commit du temps 1 ne contient que des renommages à 100 % ; pour un
  commit du temps 2, chaque type scindé a son bloc égal au bit près dans le fichier neuf et absent du fichier d'où il vient, et chaque
  ligne changée d'un fichier existant est une ligne `using`, `#nullable`, une ligne `namespace` d'un fichier de la DLL (jamais d'un test), une
  qualification réécrite par la règle ci-dessus ou un des trois chemins ; à la fin, aucune qualification ni `cref` `Alundra.Scripts.<T>` ne reste pour un type qui a bougé (le projet
  ne produit pas de fichier de documentation : un `cref` périmé ne se verrait pas à la construction). (d) Les comptes de départ : `Alundra.Tests` en Release (copie neutralisée) et en Debug.
- T1 à T20 — un commit par domaine, dans l'ordre du §2 : `Maps` (avec `Loop/`), `Session`, `Entities` (avec `Spawning/`), `Events`, `Ai`
  (avec ses sous-dossiers), `Hero`, `Combat`, `Items`, `Rng`, `Physics`, `Cells`, `Cameras`, `Rendering`, `Audio`, `Transitions`,
  `Dialogues` (avec `Yarn/`), `UI` (tous ses écrans et `Shared/`), `SaveGames`, `Probes`, puis `Properties/InternalsVisibleTo.cs`. Pour
  chacun, **deux commits** : (1) `apply_r2.py <domaine> --move`, construction de la solution
  `alundra-casaengine-project-converter.slnx` en Release (copie neutralisée), `prove_r2.py` sur ce commit, commit
  `refactor(dll): move the <domaine> files into their folders (R2, git mv only)` (aucun test : le contenu est identique à l'octet) ;
  (2) `apply_r2.py <domaine> --fix`, construction de la solution en Release (copie neutralisée) — une erreur restante ne se corrige que
  par une règle de `using` ou de qualification du script (rien d'autre : sinon arrêt) : l'arbre revient à l'état du commit (1)
  (`git checkout --` et `git clean` des seuls fichiers neufs de ce domaine, preuve par `git status`), la règle corrigée entre dans un
  commit d'outil `chore(tools): <règle> (R2)`, puis le temps 2 est rejoué —, `prove_r2.py` vert, `Alundra.Tests` Release vert,
  même compte qu'en T0, `check_names.py --tree --stage r2` dont les écarts restants ne portent que sur les domaines pas encore faits,
  commit `refactor(dll): put the <domaine> domain into Alundra.<Domaine> (R2)`, par chemins (fichiers neufs compris).
- T21 — Clôture : `check_names.py --tree --stage r2` **vert (sortie 0)** ; la solution construite en Release (copie neutralisée) puis en
  Debug (normal), `tools/AlundraTestSaves/AlundraTestSaves.csproj` compris, 0 erreur ; `Alundra.Tests` Release et Debug, même compte
  qu'en T0, zéro échec ; `cmp` de la DLL Debug ; garde des traces ; `git log --follow` retrouve l'historique d'avant R2 de **chacun** des
  120 fichiers déplacés (131 moins les 10 fichiers cités et `Plugin.cs` ; script), nommément `Rendering/Effects/AlundraEffectBank.cs` et `Entities/SpriteRecordCatalog.cs`, très scindés ; `prove_r2.py` sur tout le diff de la tranche ; ce plan (§6 ✅ et résultats).

**Avancement.** T0 faite le 2026-10-17 : (a) `PACKAGES` complétée avec les 8 paquets de `project.assets.json` qui ont un XML (Autofac, Cyotek.Drawing.BitmapFont, FontStashSharp.Rasterizers.StbTrueTypeSharp, Google.Protobuf, MonoGame.Extended.Content.Pipeline, SharpGLTF.Runtime, System.Memory, System.ValueTuple) et `PACKAGES_WITHOUT_XML` avec StbImageSharp et StbTrueTypeSharp (MonoGame.Content.Builder.Task et les deux MonoGame.Library.* n'ont aucun assembly managé), `check_names.py --table` vert, `gen_table.py` rejoué identique (commit `4e76598`) ; (b) `tools/dll-structure/apply_r2.py` ; (c) `tools/dll-structure/prove_r2.py` ; (d) comptes de départ : `Alundra.Tests` **5097 réussites, 0 échec** en Release (copie neutralisée) et en Debug, `cmp` de la DLL Debug identique, garde des traces verte. Les deux scripts ont été rejoués en entier (20 domaines, deux commits chacun) dans un clone jetable hors dépôt : `check_names.py --tree --stage r2` vert, `prove_r2.py --all` OK, `Alundra.Tests` et `tools/AlundraTestSaves` compilent sans erreur en Release, `AlundraHudPresenterTests` vert ; cinq mutations de preuve (valeur de test, ligne de la DLL, `using` injustifié, bloc coupé modifié, `namespace` d'un test) toutes refusées.

**Avancement T1 à T20 (2026-10-17).** T1 à T20 faites : deux commits par domaine (Maps à Probes), un seul pour `Properties` (`8cb5433`, déplacement pur, pas de temps 2). `prove_r2.py` vert à chaque commit, `Alundra.Tests` Release 5097 réussites, 0 échec après chaque domaine, garde des traces verte, `check_names.py --tree --stage r2` à 0 écart (1 avertissement : le `Session` imbriqué de `TextBoxGpuHarness`). **Écarts, T1 à T10** (agent interrompu) : (1) le commit (2) a été fait AVANT la suite de tests ; (2) ces 20 commits portent `Co-Authored-By: Claude Sonnet 5.5` au lieu de `Claude Opus 5.5` (non réécrits) ; la suite Release n'avait pas tourné sur `b12c268` (Physics) : lancée au début de T11, verte (5097, garde OK). À partir de `Cells` : tests verts AVANT le commit (2), attribution `Claude Opus 5.5`. T21 reste à faire.

**Acceptation.** Les preuves de T21 ; aucune valeur attendue de test modifiée (`prove_r2.py`) ; aucun type renommé ; les 10 fichiers cités
toujours dans `Alundra/Scripts/` sous `Alundra.Scripts` ; recette courte de l'auteur (nouvelle partie jusqu'au premier écran jouable, F5,
F6, F9).

**Arrêts.** Un test qui change de valeur ou de compte ; une erreur de construction qui demande plus qu'un `using` ou une qualification ;
un des 9 types cités déplacé ou renommé ; `prove_r2.py` rouge ; un processus du jeu ou de l'éditeur pendant une construction Debug.

**Retour arrière.** `git revert` des commits de la tranche, dans l'ordre inverse (aucune donnée ni export touchés).

## 7. Tranche R3 — Renommages ⏳

**But.** Plus de préfixe : chaque type sous son nom de l'étape `r3` (les 9 types cités gardent le leur jusqu'à R4) ; les fichiers renommés
comme leurs types (`git mv`).

**Règles.** Renommages par outil (Roslyn ou l'IDE, §3), jamais par remplacement de texte global ; l'outil choisi et prouvé en T0 de la
tranche ; les littéraux de journal qui portent un nom de classe suivent, et les tests qui les lisent avec eux ; les clés de données
(`AlundraCells`, `AlundraWallPlacements`, `AlundraFloorPlacements`, `AlundraCompanionJson`) et les noms d'assets ne bougent pas. Les
propriétés typées par une interface et nommées comme une classe (`WorldProxy`, environ vingt sites, par exemple
`ScreenFadeDirector => AlundraScreenFadeDirector.Instance`) sont qualifiées par le namespace. `AlundraEventProgramRunner` →
`EventProgramRunner` et `EventProgramRunner.cs` → `IEventProgramRunner.cs` dans le même commit. Un commit par domaine, chacun construit et vert.

**Acceptation.** Comme R2 avec `--stage r3`, plus : aucun type préfixé hors des 9 de R4 (recherche).

## 8. Tranche R4 — Les 9 types cités par les données ; second export ⏳

**But.** `WorldProxy` (+ `MapEvent`, `Alundra.Maps`), `EntityScriptProxy` (`Alundra.Entities`), `HeroController` (`Alundra.Hero`) et les
six view models (`Alundra.UI.<écran>`) à leur place et sous leur nom ; le convertisseur écrit les noms complets finaux ; les six
`design.json` aussi ; le second export prouvé comme le premier (prédiction versée avant le code, allowed-paths, double export).

**Règles.** Même protocole que la partie parent de R0 (annexe `r4/` avant tout code ; un seul commit parent pour le convertisseur, les
`design.json`, la DLL et les tests ; export en fenêtre calme).

**Acceptation.** Comme R0 (partie parent) ; `check_names.py --tree --stage final` vert ; plus aucun type préfixé ; plus aucun fichier dans
`Alundra/Scripts/` (le dossier disparaît).

## 9. Tranche R5 — Tests en miroir ⏳

**But.** `Alundra.Tests/<Domaine>/…`, namespaces `Alundra.Tests.<Domaine>`, plus `Arcs/` et `Support/` ; les classes de test sans préfixe ;
aucune valeur attendue changée. Le classement de l'inventaire (`report_inventory.md` §7) sert de base, recensé à nouveau en T0.

## 10. Tranche R6 — Documentation vivante ⏳

**But.** `docs/formats/` (liens vers `Alundra/Scripts/*.cs`), `docs/guidelines-runtime-alundra-casaengine.md` (§5.1 : la règle « préfixer
`Alundra…` » et le nom court), `docs/editeur-couverture-dll.md`, `docs/stick-probe.md:45` ; les scripts de `research/` qui ouvrent des sources
par chemin ; les annexes figées ne bougent pas (D-R-6).

## 11. Questions à l'auteur

- **Q-R-2** — **Tranchée le 2026-10-17 (D-R-10)** : l'aperçu tolérant (A), avec une icône d'avertissement à côté du nom du script dans
  l'éditeur. Question d'origine (2026-10-16 ; O2 du plan du moteur) — Que doit faire **l'éditeur** quand il ouvre
  un monde ou l'aperçu d'un préfab dont la DLL de scripts n'est pas construite ? Aujourd'hui, une entité au script introuvable est
  journalisée et omise ; avec la règle de D-R-4 (exception), l'éditeur planterait à l'ouverture. (A) **recommandé** : l'exception sort en jeu
  et en session de jeu de l'éditeur, mais pas dans l'aperçu de l'éditeur, où l'entité reste journalisée et omise comme aujourd'hui (un
  drapeau de la politique d'exécution, `ThrowOnScriptTypeResolution`, faux pour l'aperçu ; coût : un champ et un test ; le proxy de
  monde, le contrôleur et le mode plantent déjà l'éditeur dans ce cas, inchangés). (B) l'éditeur rattrape l'exception à ses points
  d'entrée (mise à jour hors session, ouverture d'un monde, aperçus) et laisse le monde à moitié chargé : plus de code, plus de points à
  tenir. (C) l'éditeur refuse d'ouvrir un monde tant que la DLL n'est pas construite : on ne pourrait plus ouvrir un monde pour l'éditer
  avant de construire ses scripts.
- **Q-R-1** — **Tranchée le 2026-10-16 (D-R-8)**. Question d'origine : branche : la restructuration touche tous les fichiers. La faire sur une branche créée depuis `chantier/e20-heros` (qui
  contient tout E20, non fusionné dans `main`), ou fusionner d'abord `chantier/e20-heros` dans `main` (votre décision) et partir de `main` ?
  Recommandation : fusionner E20 d'abord, sinon toute reprise d'E20 sur l'ancienne arborescence entrera en conflit.
