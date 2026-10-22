# Plan — Restructuration de la DLL de gameplay Alundra

Statut : 🚧 ; enveloppe READY ; **R0 faite et CONFIRMED le 2026-10-17** (recettes de l'auteur à faire) ; **R1 faite et CONFIRMED le
2026-10-17** (`1c5b741` à `e188a58`, ADR-0075) ; **R2 faite et CONFIRMED le 2026-10-17** (`7a15453` à `27eca9b`, recette de l'auteur à faire) ; **R3 faite et CONFIRMED le 2026-10-17** (`8e1adeb` à `a193671`, recette de l'auteur à faire) ; **R4 faite et CONFIRMED le 2026-10-17** (`6032da0` à `ce6d335`, recette de l'auteur à faire) ; R5 détaillée et READY ; Q-R-3 répondue le 2026-10-22 (D-R-13), **R5 faite et CONFIRMED le 2026-10-22** (`61d2d46b` à `613c6c9e`, mode AUTO accordé par l'auteur ; révision 3 ; D-R-21 ; recette de l'auteur à faire) ; R6b décidée (D-R-20) ; **R7, R8 et R6b détaillées le 2026-10-22 (§12 à §14), puis la fusion finale (§15), en mode AUTO (D-R-22)** ; R7 et R8 validées par l'auteur le 2026-10-22 (D-R-15, D-R-14), à détailler ; **R6 faite et CONFIRMED le 2026-10-17** (`7674ffa` à `2964d9a`). Démarre après E20.e0a4 (plan E20, §1.16.4).

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
- **D-R-12** (2026-10-17) — R3 : la relecture de clôture rend REVISE (un oubli de procédure dans l'étape par domaine) ; l'auteur décide
  « corriger et exécuter » : le correctif demandé est appliqué tel quel, sans nouvelle relecture, et le verifier frais de fin de tranche le
  contrôle.
- **D-R-13** (2026-10-22, réponse à Q-R-3) — Rangement des tests (R5) : (1) un fichier de tests se range **par le type de son nom**,
  pas par le domaine qu'il exerce ; (2) les tests des types renommés perdent **seulement le préfixe** (`AlundraMusicPlayerTests` →
  `MusicPlayerTests`, pas `BgmPlayerTests`) ; (3) `Arcs/` ne reçoit que **les 26 scénarios** de bout en bout (les harnais de traces :
  D-R-19) ; (4) les types partagés déclarés dans un fichier de test **ne vont pas dans `Support/`** : les `using` s'ajoutent ; (5) **un
  type principal par fichier**, comme la DLL, selon la lecture de la règle (3) de l'ADR-0075 (appliquée aux tests par P-R5-1, §9) ; (6)
  `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` **reste à sa place** ; (7) les gardes d'export sans type (`CornerTrack`,
  `ZeroDurationLoop`) vont dans **un dossier dédié** (`ExportGuards/`, D-R-19). Les points 4 et 5 se croisent pour les types partagés :
  D-R-16. ADR-0080.
- **D-R-14** (2026-10-22, réponse à Q-R-5) — Les quelque 300 anciens noms restés dans les commentaires de la DLL et des tests se corrigent
  dans une tranche dédiée, **R8** : mécanique, outillée comme R3, commentaires seulement, après R5 (qui renomme aussi les classes de test)
  et R7.
- **D-R-15** (2026-10-22, réponse à Q-R-7) — La tranche **R7** est validée : `check_names.py --rules`, outil seul, après R5, contrôle
  l'arbre vivant de la DLL et des tests contre les règles de l'ADR-0075, sans la table, et devient la garde des tranches suivantes.
- Q-R-4 et Q-R-6 : l'auteur a répondu « corriger » le 2026-10-22 sans dire quoi : précisions demandées (§11). Réponses au 2e tour :
  D-R-16 à D-R-18.
- **D-R-16** (2026-10-22, 2e tour, Q-R-3b (a)) — Les types partagés déclarés dans un fichier de test (les 5 collections xUnit, les hôtes
  d'aides) vont **chacun dans son propre fichier**, dans le dossier de leur fichier d'origine, pas dans `Support/` : la coupe de R2,
  vers le domaine du fichier d'origine. Restent (b), le nom du dossier dédié, et (c), les harnais de traces (Q-R-3b).
- **D-R-17** (2026-10-22, 2e tour, Q-R-4 « 1 ») — Le choix (1) est corrigé : `docs/intro-roadmap.md` n'est plus traité comme vivant
  mais comme un document historique ; les corrections que R6 y a faites sont annulées, et R5 (T22) ne touche plus ses lignes 26 et 63.
  Les choix (2) à (5) de R6 sont validés. Lecture de la session (l'auteur a répondu « 1 » à « lesquels corriger ») : un document
  vivant se corrige, l'autre choix est historique. **Sous réserve de Q-R-4b** (le « comment » n'a pas de réponse) : rien n'est annulé
  avant la confirmation ; l'annulation serait la tranche R6b (§3). **Remplacé au 3e tour** : à Q-R-4b, l'auteur répond « corrige les
  documents » : rien n'est annulé, `docs/intro-roadmap.md` reste vivant et se corrige ; la portée (ce fichier seul ou tous les documents
  vivants, numéros de ligne compris) attend Q-R-4c ; R6b porte cette correction (tranché par D-R-20).
- **D-R-20** (2026-10-22, 4e tour, Q-R-4c : « tous les documents vivants ») — R6b corrige **tous les documents vivants** : en plus des
  chemins et des noms (faits par R6), les **numéros de ligne** qu'ils citent dans la DLL, les tests et les outils sont recalculés partout
  (le choix (3) de Q-R-4 est corrigé) ; `docs/intro-roadmap.md` en fait partie. Les documents historiques, les audits (en lecture seule)
  et les annexes figées (D-R-6) ne changent pas. R6b passe après R5 (qui déplace les tests) ; R8 ne change que des noms dans les
  commentaires, ligne pour ligne, donc les numéros recalculés par R6b restent justes (R8 le vérifie : aucune ligne ajoutée ni retirée).
- **D-R-19** (2026-10-22, 3e tour, Q-R-3b (b) et (c) : « oui, oui ») — Les gardes d'export sans type de la DLL vont dans **`ExportGuards/`**
  (namespace `Alundra.Tests.ExportGuards`) ; **`Arcs/`** reçoit les 26 scénarios de bout en bout **et les harnais de traces**. Avec D-R-13 et
  D-R-16, Q-R-3 est entièrement tranchée : ADR-0080.
- **D-R-18** (2026-10-22, 2e tour, Q-R-6 « c ») — Le choix (c) est corrigé : les dossiers temporaires des anciennes exécutions de tests,
  nommés par les anciens préfixes, sont effacés à la fin de R5 : recensés par chemin exact, envoyés à la Corbeille un par un, chacun
  vérifié dans la Corbeille (un dossier que la Corbeille refuse est un arrêt et une question, jamais une suppression définitive). Les
  choix (a) (preuves de fin de R3 et R4 gelées) et (b) (commentaires inchangés en R5, R8 les corrige) sont validés.
- **D-R-21** (2026-10-22, pendant l'exécution de R5, réponses de l'auteur) — (1) Les six fichiers d'aide qui ne servent qu'aux scénarios
  (`ArcChecks`, `ArcSamples`, `ArcSnap`, `LeverArcs`, `LeverCell`, `LeverStampedSounds`) restent dans `Arcs/` avec eux. (2) Les 77 fichiers
  dont le nom ne désigne ni un type de la DLL ni un dossier, rangés par l'exécution d'après leur sujet (24) ou d'après les types
  qu'ils utilisent (53), gardent ce rangement (`tools/dll-structure/r5-files.tsv`, colonne `basis`). (3) Décision de la session, par la
  règle de D-R-13 (point 2) : la classe imbriquée `AlundraHudComposerEquipmentBackgroundsTests` (dans `HudComposerTests`), que la table
  des types de premier niveau ne couvrait pas, perd seulement son préfixe (`HudComposerEquipmentBackgroundsTests`), dans un commit à part
  avant la clôture.
- **D-R-22** (2026-10-22, ordre de l'auteur) — « Fini tous les R* et merge dans main a la fin (de meme pour les sous modules, develop
  pour MGUI) » : R7, R8 et R6b se détaillent et s'exécutent dans cet ordre, en mode AUTO (chacune relue puis vérifiée par des agents
  neufs), puis la fusion locale du §15 ; rien n'est poussé. R6b passe en dernier : ses numéros se recalculent quand plus rien ne bouge.
  Lecture de D-R-20 pour R6b : P-R6b-1 (§14).
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
Alundra.Tests/   la même arborescence par domaine, plus Arcs/ (scénarios de bout en bout et harnais de traces), Support/ (outillage partagé) et ExportGuards/ (gardes d'export sans type de la DLL ; D-R-19)
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
| R7 | Garde des règles (D-R-15, 2026-10-22 ; §12) | `check_names.py --rules` : l'arbre vivant de la DLL et des tests contrôlé contre les règles de l'ADR-0075, sans la table | R5 |
| R6b | Reprise de R6 (D-R-17, D-R-20 ; §14, après R8, D-R-22) | tous les documents vivants, `docs/intro-roadmap.md` compris : les numéros de ligne qu'ils citent recalculés partout, en plus des chemins et des noms de R6 | R5 |
| R8 | Anciens noms des commentaires (D-R-14, 2026-10-22 ; §13) | les quelque 300 anciens noms des commentaires de la DLL et des tests corrigés, mécaniquement, commentaires seulement, ligne pour ligne (aucune ligne ajoutée ni retirée : les numéros de R6b restent justes, D-R-20) | R5, R7 |

**Règles du programme.**
- Aucune autre tranche (E20, E14, E19) pendant le chantier : il touche tous les fichiers de la DLL et des tests. Depuis le 2026-10-22,
  le chantier va jusqu'à R8 (R7, R8 et R6b sont dans l'enveloppe, D-R-14, D-R-15, D-R-17).
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
- **Fusion du 2026-10-17 (demande de l'auteur : « quand tu auras fini ta tâche, merge dans main et repars d'une nouvelle branche à partir de
  main »).** R0, R1 et R2 fusionnés dans `main` : branche d'intégration `integration/restructuration-r2` (fusion de `main` `4714a1d`, qui
  portait déjà une partie de R2 et le pointeur moteur `1c49709a`, commit `aa4bb74` ; pointeur moteur monté au `main` du moteur `92d8e847`
  à la demande de l'auteur, commit `11cc63e`) ; validation de l'état fusionné : la solution en Release (copie neutralisée) et en Debug sans
  erreur, `Alundra.Tests` 5 097 / 0 échec dans les deux, le convertisseur 854 / 0 échec, `cmp` identique, garde des traces verte,
  `check_names.py` sans constat ; avance rapide de `main` `4714a1d..11cc63e`, rien poussé. Le chantier continue sur
  `chantier/restructuration-dll-r3`, créée depuis `main`.

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

## 6. Tranche R2 — Déplacements et scissions 🧪 (détaillée le 2026-10-17 ; révision 2, relecture de clôture **READY**)

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

**Clôture T21 (2026-10-17).** Faite, sur `40db875` : `check_names.py --tree --stage r2` sortie 0 (0 écart, 1 avertissement connu) ; solution `.slnx` et `tools/AlundraTestSaves` construites en Release (copie neutralisée) puis en Debug, 0 erreur ; `Alundra.Tests` **5097 réussites, 0 échec** en Release (5 min 40 s) et en Debug (5 min 53 s), même compte qu'en T0 ; `cmp` de la DLL Debug avec `alundra-project/Alundra.dll` identique ; garde des traces verte après chaque suite ; `git log --follow` retrouve l'historique d'avant R2 de chacun des 120 fichiers déplacés (dont `AlundraEffectBank.cs` et `SpriteRecordCatalog.cs`) ; `prove_r2.py --all` : 20 commits de déplacement, 19 commits de correction, état final OK. Statut 🧪 : reste la recette de l'auteur (nouvelle partie jusqu'au premier écran jouable, F5, F6, F9).

**Acceptation.** Les preuves de T21 ; aucune valeur attendue de test modifiée (`prove_r2.py`) ; aucun type renommé ; les 10 fichiers cités
toujours dans `Alundra/Scripts/` sous `Alundra.Scripts` ; recette courte de l'auteur (nouvelle partie jusqu'au premier écran jouable, F5,
F6, F9).

**Arrêts.** Un test qui change de valeur ou de compte ; une erreur de construction qui demande plus qu'un `using` ou une qualification ;
un des 9 types cités déplacé ou renommé ; `prove_r2.py` rouge ; un processus du jeu ou de l'éditeur pendant une construction Debug.

**Retour arrière.** `git revert` des commits de la tranche, dans l'ordre inverse (aucune donnée ni export touchés).

**Vérification de R2 (2026-10-17) : CONFIRMED** par un verifier frais (`7a15453` à `27eca9b`). Rejoué : `check_names.py --tree --stage
r2` à 0 écart (207 fichiers, 220 types) ; la solution en Release (copie neutralisée) et en Debug, y compris sans construction
incrémentale ; `Alundra.Tests` 5 097 / 0 échec dans les deux ; la garde des traces ; le `cmp` ; `prove_r2.py --all` ; une preuve
indépendante (appariement par la table, corps comparés à l'octet : chaque bloc scindé est un bloc contigu de l'origine, aucune ligne perdue,
le BOM gardé ; les seules lignes de corps changées sont 7 qualifications réécrites ; dans les tests et `tools/AlundraTestSaves`, 1 292 `using`
ajoutés, 95 `using Alundra.Scripts;` retirés, aucune ligne `namespace`, 8 paires de qualifications ou de chemins) ; aucun type renommé ;
les 10 fichiers cités en place ; `git log --follow` retrouve l'historique complet des 120 fichiers déplacés (renommages R100) ; aucun
`cref` périmé introduit (construction avec un fichier de documentation dans le scratchpad) ; le périmètre des chemins. **Aucun constat P0
à P3.** Reportés (P4) : les `using` ajoutés suivent l'ordre des domaines, pas l'ordre alphabétique (par exemple
`AlundraHudPresenterTests.cs:13-19`) ; le message d'erreur d'`AlundraHudPresenterTests.cs:477` cite encore l'ancien chemin ; les 20
premiers commits de la tranche portent « Claude Sonnet 5.5 » dans leur ligne d'attribution (non réécrits). Reste la recette de l'auteur.

## 7. Tranche R3 — Renommages 🧪 (faite et CONFIRMED le 2026-10-17, `8e1adeb` à `a193671` ; révision 3, D-R-12 ; reste la recette de l'auteur)

**Relecture (2026-10-17).** REVISE, quatre blocages corrigés (FIX, révision 1) : (1) l'outil se restaure hors ligne avec Roslyn **4.8.0**
en net9.0, prouvé par une sonde (4.14.0 ne se restaure pas sans réseau) ; (2) l'outil vérifie sémantiquement chaque renommage et refuse un
domaine au moindre écart ; la preuve de T0 inclut une collision réelle avec un membre et un conflit insoluble ; `prove_r3.py` raisonne
occurrence par occurrence (code, chaîne, commentaire) ; (3) les deux renommages de fichiers d'`Events` font deux commits ; (4) le relevé
des littéraux a une classe pour les chemins de fichiers renommés et une classe « gardé ». Seconde relecture : REVISE, deux blocages ;
cap de deux relectures atteint, dispositions de la session principale, FIX, puis une relecture de clôture (révision 2) : (5)
`AlundraMapEvent` → `MapEvent`, seul type renommé en R3 qui reste dans `Alundra/Scripts/`, est renommé au temps (2) du domaine `Maps`
(règle : un type qui reste dans `Alundra/Scripts/` appartient au domaine de son dossier final) ; (6) un littéral qui nomme un autre type
de la table que la classe qui écrit est renommé avec le type nommé, au temps (2) du domaine de ce type, et ne change pas en R3 si ce type
est l'un des 9 cités. Relecture de clôture : REVISE, un blocage (P2) : l'étape par domaine ne citait que la classe (1), et rien
n'exigeait que le relevé soit appliqué en entier ; sur la décision de l'auteur (D-R-12), le correctif demandé est appliqué tel quel
(révision 3 : l'étape par domaine applique (1), (1b) et (3) ; `prove_r3.py` refuse une entrée du relevé non appliquée ; T21 contrôle la
complétude du relevé), sans nouvelle relecture ; le verifier frais de fin de tranche contrôle ce point.

**But.** Chaque type sous son nom de l'étape `r3` de la table, chaque fichier à son chemin `r3` : plus de préfixe `Alundra`, sauf les 9
types cités par les données (R4) ; **aucun changement de comportement** ; les clés de données et les noms d'assets intacts.

**Faits (2026-10-17, HEAD `27eca9b`).** La table : 148 lignes renommées (147 types, la classe partielle `AlundraEntityScriptProxy` comptant deux fois), dont 138 types en
R3 (les 9 cités attendent R4 ; leurs types
secondaires non cités, par exemple `AlundraPadState` → `PadState` et `AlundraMapEvent` → `MapEvent`, se renomment ici) ; les fichiers
renommés comme leurs types (`r3_path`). Les noms spéciaux : `AlundraMusicPlayer` → `BgmPlayer`, `AlundraRandom` → `GameRandom`,
`AlundraSoundBank` → `SfxBank`, `AlundraEventProgramRunner` → `EventProgramRunner` (et le fichier `Events/EventProgramRunner.cs`, qui
porte l'interface `IEventProgramRunner`, devient `Events/IEventProgramRunner.cs` dans le même commit). Le relevé du 2026-10-16
(`scratchpad/dll-structure/couplings/member-clash.txt`) : des propriétés portent déjà le nom futur d'un type, parfois d'un autre type
(par exemple `WorldProxy.ScreenFadeDirector` de type `IAlundraScreenFadeDirector`, `DialogueDirector`, `SoundPlayer`, `MusicPlayer`,
`BgmFadeDirector` ; `GameState` sur 32 sites, du même type) ; un renommage sémantique doit qualifier là où le nom simple se lierait au
membre. Les littéraux de chaîne qui portent un nom de type (`couplings/lit-dll.txt` : 180 occurrences dans 48 fichiers, surtout des
préfixes de journal, par exemple `AlundraWorldProxy` 38, `AlundraEventProgramRunner` 20 ; `lit-tests.txt` : 56 lignes de tests) ; des
clés de données portent le préfixe sans être des types (`AlundraCells`, `AlundraWallPlacements`, `AlundraFloorPlacements`,
`AlundraCompanionJson`, §2 règle (6)). **Sonde du 2026-10-17** (scratchpad `r3-probe/`, le cache NuGet local `C:/Users/casad/.nuget/packages` comme
seule source, sans réseau) : `Microsoft.CodeAnalysis.Workspaces.MSBuild` et `Microsoft.CodeAnalysis.CSharp.Workspaces` **4.14.0** en
net10.0 ne se restaurent pas (NU1101 : `Microsoft.VisualStudio.Setup.Configuration.Interop` absent ; en net9.0, `System.Reflection.Metadata`,
`System.Threading.Tasks.Dataflow` et `System.Collections.Immutable` 9.0.0 manquent) ; les mêmes paquets en **4.8.0** avec
`Microsoft.Build.Locator` 1.7.8, en net9.0, se restaurent (deux avertissements NU1603 de version voisine) et se construisent ; le
localisateur enregistre le MSBuild du SDK **9.0.319** ; `MSBuildWorkspace` ouvre `Alundra.Tests/Alundra.Tests.csproj` et charge `Alundra`
(209 documents) et `Alundra.Tests` (337), **0 erreur de compilation** (langage C# 12 pour Roslyn 4.8 : le code n'utilise rien de plus
récent).

**Propriétaire, chemins, budget.** Un `executor` (l'outil de renommage et le relevé des littéraux s'écrivent avec jugement ;
l'application est ensuite mécanique). Chemins : `Alundra/**` (hors `bin/`, `obj/`), `Alundra.Tests/**`, `tools/AlundraTestSaves/**`,
`tools/dll-structure/` (T0 ; puis l'outil de renommage et `prove_r3.py` pour les seules corrections de leurs règles, par un commit d'outil
suivi du rejeu), ce plan (§7). Aucun export, aucune donnée, aucun fichier du convertisseur ni de `docs/` hors ce plan (R6), aucun
sous-module, aucun nom de classe de test (R5). Le jeu et l'éditeur fermés pendant la tranche. Copie neutralisée comme en R2. Budget : une
passe d'exécution, une passe de correction au plus ; un verifier frais à la fin.

**Tâches.**
- ✅ **Levé le 2026-10-17 : l'auteur a fermé le lanceur ; T0 relancée.** **ARRET T0 (2026-10-17, avant tout travail).** Verification de depart : `CasaEngine.Launcher.exe` (PID 52432, lance sur `Projects/RPGDemo/RPGDemo.json`) tourne, et `alundra-project/Alundra.dll` est verrouillee (PermissionError a l'ouverture en r+b). Arret de la tranche : l'auteur ferme le lanceur, puis T0 reprend.
- ✅ T0 (aucun code de la DLL) : (a) **l'outil de renommage**, `tools/dll-structure/rename-tool/` (une petite console C# en net9.0, hors de la solution,
  `Microsoft.CodeAnalysis.Workspaces.MSBuild` 4.8.0, `Microsoft.CodeAnalysis.CSharp.Workspaces` 4.8.0 et `Microsoft.Build.Locator` 1.7.8,
  restaurés avec le cache local comme seule source : `dotnet restore --source C:/Users/casad/.nuget/packages` ; le localisateur enregistre
  le MSBuild du SDK 9 ; si la restauration ou le chargement échoue, arrêt et question) : elle ouvre `Alundra.Tests/Alundra.Tests.csproj`
  (qui charge `Alundra`) et `tools/AlundraTestSaves/AlundraTestSaves.csproj` dans un même espace de travail et, pour un domaine, selon
  `table.json` (ancien nom `r2` → nom `r3`) : **avant**, relève pour chaque type du domaine toutes ses références (`SymbolFinder`, `cref` et
  `nameof` compris) et les diagnostics d'erreur des compilations ; applique `Renamer.RenameSymbolAsync` à chaque type, sans renommer dans
  les commentaires ni les chaînes, avec la résolution de conflits de Roslyn ; **après**, exige que chaque emplacement relevé se lie au
  symbole renommé, qu'aucun autre identifiant ne change de liaison (les symboles liés de chaque identifiant des fichiers touchés,
  comparés avant et après), et qu'aucun diagnostic d'erreur nouveau n'apparaisse ; au moindre écart, elle refuse le domaine **sans rien
  écrire** (sortie non nulle) ; sinon elle écrit les fichiers changés en gardant leurs fins de ligne et leur BOM. Preuves sur une copie de
  l'arbre hors du dépôt : `AlundraRandom` → `GameRandom` (sans collision) ; `AlundraScreenFadeDirector` → `ScreenFadeDirector`, dont le nom
  simple devient celui de la propriété `ScreenFadeDirector` (de type `IAlundraScreenFadeDirector`) d'`AlundraWorldProxy`, référencée comme
  classe dans le code aux lignes 695, 1382, 1383, 2754 et 2755 et dans des `cref` aux lignes 338, 689 et 1371 : la référence est qualifiée par Roslyn, la solution se construit, `prove_r3.py` est vert ; un
  conflit insoluble fabriqué (deux types renommés vers le même nom dans un même namespace) : l'outil sort en erreur sans rien écrire. (b) **le relevé des littéraux**, versé dans `tools/dll-structure/r3-literals.tsv` : chaque littéral de chaîne de la DLL, des tests
  et de `tools/AlundraTestSaves` qui contient un ancien nom de type de la table (les trous d'une chaîne interpolée sont du code, hors du
  relevé), classé en (1) **nom de la classe qui écrit** (préfixe de journal ou message qui la nomme) : renommé avec elle, et les tests qui
  le lisent avec lui ; (1b) **nom d'un autre type de la table** (par exemple « … is not backed by AlundraPadState » écrit par
  `AlundraEventProgramRunner`, « … stays with AlundraGameState » écrit par `AlundraYarnVariableStorage`, ou le préfixe « AlundraWorldProxy: »
  écrit par `AlundraCameraDirector` et `AlundraEntitySpawnFactory`) : renommé avec le type nommé, au temps (2) du domaine de ce type, et
  les tests qui le lisent avec lui ; si le type nommé est l'un des 9 cités, inchangé en R3 (il change en R4 avec lui) ; (2) **clé de données, nom d'asset, nom d'une donnée exportée** : jamais changé ; (3) **chemin ou nom d'un fichier
  source renommé par R3** (par exemple les `"AlundraHudPresenter.cs"`, `"AlundraHudScreen.cs"`, `"AlundraHudDirector.cs"` lus par
  `AlundraHudPresenterTests.cs:449,450,467`) : réécrit au commit (2) du domaine qui renomme le fichier ; (4) **sens établi, gardé** (un nom
  de collection xUnit, la prose d'un harnais de traces, un message qui cite l'histoire) : inchangé, noté ; (5) **sens non établi** : arrêt
  et question. (c) `tools/dll-structure/prove_r3.py`, qui apparie les fichiers par la table et raisonne **occurrence par occurrence** (un lexeur C#
  sépare le code, les chaînes — verbatim `@"…"`, brutes `"""…"""` et interpolées `$"…"`, `$$"""…{{…}}…"""` (présentes par exemple dans
  `AlundraBgmFadeDirectorTests.cs`) —, les caractères, les trous des chaînes interpolées, les lignes de préprocesseur et les commentaires) : un commit de déplacement ne contient que des
  renommages à 100 % ; pour un commit de renommage, dans le code (`cref` et `nameof` compris), un ancien nom ne change que vers son nom
  `r3` selon la table, avec au plus une qualification de namespace ou un `global::` ajouté par Roslyn ; dans une chaîne, il ne change que
  pour un littéral des classes (1), (1b) et (3) du relevé, au commit du domaine que le relevé lui donne et exactement comme il le dit ; dans un commentaire, il ne change pas ; tout autre
  changement est refusé ; **chaque entrée (1), (1b) ou (3) du relevé doit changer à son commit, exactement vers la valeur prévue (une
  entrée non appliquée est un refus)** ; preuves de T0 : une mutation qui change un littéral (2) ou (4) est refusée, une mutation qui omet
  un littéral (1b) est refusée. (d) Les comptes de départ.
  **Résultat de T0 (2026-10-17, branche `chantier/restructuration-dll-r3`).** Vérification de départ refaite : aucun processus `CasaEngine.Launcher`, `CasaEngine.Editor` ni jeu, `alundra-project/Alundra.dll` ouverte en r+b sans erreur. Départ : Release 5097 réussis / 0 échec, Debug 5097 / 0, `cmp` des deux DLL identique, garde des traces verte. (a) `tools/dll-structure/rename-tool/` (Roslyn 4.8.0, restauré hors ligne ; l'espace de travail charge `Alundra`, `Alundra.Tests` et `AlundraTestSaves`, 550 documents, 0 erreur de départ ; usage : `dotnet tools/dll-structure/rename-tool/bin/Debug/net9.0/RenameTool.dll <racine> <Domaine>` après `dotnet build -c Debug` de l'outil ; il refuse un domaine sans rien écrire au moindre écart de liaison, de jeton ou de diagnostic). Preuves sur une copie hors du dépôt : `AlundraRandom` → `GameRandom` (domaine `Rng`, 2 types, 40 fichiers, aucune qualification, solution construite 0 erreur, `prove_r3` vert) ; `AlundraScreenFadeDirector` → `ScreenFadeDirector` (domaine `Transitions`, 6 types, 105 fichiers, 558 références, 7 qualifications ajoutées par Roslyn dont `Transitions.ScreenFadeDirector.Instance` dans `AlundraWorldProxy` aux lignes 695, 1382, 1383, 2754 et 2755, les `cref` des lignes 338, 689 et 1371 restant liés au type, solution construite 0 erreur, `prove_r3` vert) ; conflit insoluble fabriqué (`AlundraRandom` et `AlundraLibcRandom` renommés tous deux `Foo` dans `Alundra.Rng`) : sortie 1, rien écrit (arbre identique). (b) `tools/dll-structure/r3_literals.py` et `r3-literals.tsv` : 211 entrées pour 206 littéraux ; classes : 170 (1), 26 (1b), 4 (3), 11 (4), aucune (2) ni (5) ; les (3) sont `"AlundraHudPresenter.cs"`, `"AlundraHudScreen.cs"`, `"AlundraHudDirector.cs"` et le message de `AlundraHudPresenterTests.cs:477` (domaine `UI`) ; les (1b) dont le type nommé est cité (`AlundraWorldProxy`, `AlundraEntityScriptProxy`, `AlundraPlayerController`) ou non renommé restent inchangés en R3 ; `AlundraGameState` (littéral de `AlundraYarnVariableStorage`) se renomme avec `Session`, `AlundraPadState` (littéral de l'exécuteur d'événements) avec `Hero` ; l'entrée est PAR NOM (une même chaîne peut recevoir deux noms à deux domaines) ; `r3_literals.py apply <Domaine>` réécrit les entrées (1), (1b), (3) du domaine, `check --stage before|after` contrôle la complétude ; aucun nom de type renommé dans les données du projet (seulement des commentaires XAML, hors périmètre). (c) `tools/dll-structure/prove_r3.py` (modes commit, `--worktree`, deux dossiers, `--all --base`) : recompose l'après attendu à partir de l'avant, octet par octet, avec le lexeur de R2 ; preuves : mutation d'un littéral (4) (`"AlundraScreenFadeDirector singleton"` → `"ScreenFadeDirector singleton"`) refusée, omission d'un littéral (1b) (`AlundraPadState` laissé dans le message de l'exécuteur, domaine `Hero`) refusée, un renommage de fichier modifié d'un octet refusé comme n'étant pas à 100 %. (d) comptes de départ ci-dessus. Écart : ces commits portent `Co-Authored-By: Claude Sonnet 5.5` (la ligne imposée par l'harnais, exacte pour le modèle qui les écrit) et non `Claude Opus 5.5` comme le demandait la consigne.
- ✅ **T1 à T10 faites (2026-10-17, `chantier/restructuration-dll-r3`)** : `Maps`, `Session`, `Entities`, `Events` (trois commits), `Ai`, `Hero`, `Combat`, `Items`, `Rng`, `Physics`, soit 21 commits de domaine (`9fc141a` à `1b11420`, avec quatre commits d'outil intercalés). Pour chaque domaine : construction Release (copie neutralisée) 0 erreur après le commit (1) et après le commit (2), `prove_r3` vert sur les deux commits, `Alundra.Tests` Release 5097 réussis / 0 échec avant chaque commit (2), garde des traces verte, `check_names.py --tree --stage r3` sans écart restant sur les types et les fichiers du domaine (les écarts restants ne portent que sur les domaines pas encore faits). `AlundraMapEvent` → `MapEvent` est renommé au temps (2) de `Maps` ; `EventProgramRunner.cs` → `IEventProgramRunner.cs` puis `AlundraEventProgramRunner.cs` → `EventProgramRunner.cs` (`R100` chacun). Corrections d'outil (commits d'outil, sans arrêt de la tranche) : (i) `prove_r3 --worktree` compare la forme LF que git commiterait (l'arbre de travail est en CRLF, `core.autocrlf=true`) ; (ii) `r3_literals.py` ne cartographiait que les fichiers des types principaux : le fichier d'un type scindé (`AlundraPadState.cs`, domaine `Hero`) échappait au `git mv` et à la preuve ; la carte couvre toutes les lignes de la table, le commit (1) de `Hero` est refait (réinitialisation douce de son seul commit local, jamais publié) et les preuves de déplacement des autres domaines rejouées vertes ; (iii) l'outil de renommage réécrit textuellement les `cref` que `SymbolFinder` ne rapporte pas bien que le modèle sémantique les lie (cas réel : `<see cref="AlundraScriptedMotion"/>` dans le commentaire de documentation d'une classe imbriquée de `AlundraNpcCharacterControllerMoverTests.cs`, domaine `Physics`), puis vérifie leur liaison comme toute autre référence ; (iv) `prove_r3` ne lit les valeurs `cref` que dans les commentaires de documentation (`///`, `/**`) : un commentaire `//` ordinaire ne change jamais (cas réel : `AlundraGroundSlopeTests.cs:356`). Les neuf preuves de renommage déjà faites ont été rejouées vertes après (iv).
- ✅ **T11 à T20 faites (2026-10-17, `chantier/restructuration-dll-r3`)** : `Cells`, `Cameras`, `Rendering`, `Audio`, `Transitions`, `Dialogues`, `UI`, `SaveGames`, `Probes` (`54e24da` à `6632b83`) ; `Properties` n'a aucun type à renommer (0 ligne de la table), donc aucun commit. Pour chaque domaine : `git mv` en `R100` et `prove_r3 --kind move` vert, construction Release (copie neutralisée) 0 erreur après les deux commits, `prove_r3 --kind rename` vert, `Alundra.Tests` Release 5097 réussis / 0 échec avant chaque commit (2), garde des traces verte, aucun écart restant de `check_names.py --tree --stage r3` (la sortie complète de l'arbre est maintenant vide). Deux corrections d'outil (commits d'outil, temps (2) rejoué depuis l'état du commit (1)) : (v) `Rendering` : un test lit un préfixe de journal par un fragment de nom, `Contains("AlundraEffect", w)` (début de `AlundraEffectReservoir`), que le relevé ne voyait pas faute d'un nom entier de la table ; le test échouait (1 sur 5097). Le relevé et `prove_r3` connaissent maintenant ce fragment (`Info.partial`, `AlundraEffect` → `Effect`, classe 1, domaine `Rendering`) et l'entrée est ajoutée à `r3-literals.tsv` (`AlundraEffectReservoirTests.cs:342`) ; un balayage de tous les littéraux de la DLL, des tests et de l'outil de sauvegardes n'a trouvé aucun autre fragment de ce genre lu par un test (`AlundraEffectLoader`, `AlundraDialogue`, `AlundraMap` et `AlundraGame` sont des clés ou de la prose, gardés). (vi) `SaveGames` : `Alundra.Tests/AlundraSaveGameDirectorKeysTests.cs` avait un ancien nom dans la branche `#else` d'un `#if DEBUG` (inactive dans le projet chargé par Roslyn) que l'outil ne liait pas ; l'erreur de construction Release et `prove_r3` l'ont vu ; l'outil réécrit maintenant textuellement, avant la vérification, les anciens noms des régions de préprocesseur inactives (`DisabledTextTrivia`), et la construction dans l'autre configuration est le juge (Debug, en T21). Les 7 écrans XAML de `alundra-project/UI/Screens/` citent d'anciens noms de types UI dans des commentaires seulement : hors périmètre. Écart : ces commits portent `Co-Authored-By: Claude Sonnet 5.5` comme ceux de T0 à T10.
- T1 à T20 — par domaine, dans l'ordre de R2 (un type appartient au domaine de son dossier final ; `AlundraMapEvent`, qui reste dans
  `Alundra/Scripts/AlundraWorldProxy.cs`, est renommé au temps (2) de `Maps`, sans `git mv`), **deux commits** : (1) `git mv` des fichiers du domaine vers leur nom `r3` (le contenu
  identique à l'octet), construction de la solution en Release (copie neutralisée), `prove_r3.py`, commit
  `refactor(dll): rename the <domaine> files after their types (R3, git mv only)` ; le domaine `Events` en fait **deux** : d'abord
  `EventProgramRunner.cs` → `IEventProgramRunner.cs`, puis `AlundraEventProgramRunner.cs` → `EventProgramRunner.cs`, chacun construit,
  passé par `prove_r3.py` et renommé à 100 % (`git show -M --name-status` : `R100`) ; (2) l'outil de renommage sur les types du
  domaine, puis, selon le relevé, les littéraux de classe (1) des types du domaine, de classe (1b) dont le type nommé appartient au
  domaine, et de classe (3) dont le fichier est renommé par le domaine, construction, `prove_r3.py` vert, `Alundra.Tests` Release vert, même
  compte, `check_names.py --tree --stage r3` dont les écarts restants ne portent que sur les domaines pas encore faits, commit
  `refactor(dll): drop the Alundra prefix in the <domaine> domain (R3)`. Une erreur de construction ne se corrige que par une règle de
  l'outil (commit d'outil, rejeu du temps 2 depuis l'état du commit (1)) : sinon arrêt.
- ✅ **T21 faite (2026-10-17, `chantier/restructuration-dll-r3`)** : `check_names.py --tree --stage r3` sortie 0 (0 écart, un avertissement sur un type imbriqué de test `Session`) ; les seuls types `Alundra*` restant dans `Alundra/` sont les 9 cités (`AlundraWorldProxy`, `AlundraEntityScriptProxy` en deux parties, `AlundraPlayerController`, `AlundraHudViewModel`, `AlundraInventoryViewModel`, `AlundraSubInventoryViewModel`, `AlundraChoiceViewModel`, `AlundraSaveScreenViewModel`, `AlundraTextBoxViewModel`) ; solution Release (copie neutralisée) et Debug 0 erreur ; `Alundra.Tests` Release et Debug 5097 réussis / 0 échec (le compte de T0) ; `cmp alundra-project/Alundra.dll` et la DLL de `bin/Debug` identiques ; garde des traces verte ; `git log --follow` retrouve `Scripts/EventProgramRunner.cs` pour `Events/IEventProgramRunner.cs` et `Scripts/AlundraEventProgramRunner.cs` pour `Events/EventProgramRunner.cs` ; clés de données de la règle (6) toujours présentes (`AlundraCells`, `AlundraWallPlacements`, `AlundraFloorPlacements`, `AlundraCompanionJson`) ; `prove_r3.py --all --base 2ce88ec` OK (48 commits et l'état final, 138 types, 130 fichiers déplacés, 116 entrées du relevé appliquées). Correction d'outil (commit d'outil `b98b6f8`) : la première exécution de `--all` donnait 12 violations de l'état final, toutes des faux positifs de l'outil, aucune dans la DLL : (vii) le contrôle de fin ne gardait pas les littéraux de classe 4 (noms de collections xUnit, prose des harnais, usage de l'outil) dont la colonne du nouveau nom diffère du nom (11), et signalait l'ancien chemin `Events/EventProgramRunner.cs`, devenu le chemin `r3` de la classe `EventProgramRunner` (1). Reste : le verifier frais de fin de tranche et la recette de l'auteur.
- T21 — Clôture : `check_names.py --tree --stage r3` **vert (sortie 0)** ; aucun type déclaré dans `Alundra/` dont le nom commence par
  `Alundra` ou `IAlundra`, hors des 9 cités (recherche) ; la solution en Release (copie neutralisée) puis en Debug, 0 erreur ; `Alundra.Tests`
  Release et Debug, même compte qu'en T0 ; `cmp` ; garde des traces ; `git log --follow` retrouve l'historique d'avant R2 de chaque fichier renommé (nommément
  `Events/IEventProgramRunner.cs` celui de `Scripts/EventProgramRunner.cs`, et `Events/EventProgramRunner.cs` celui de
  `Scripts/AlundraEventProgramRunner.cs`) ; `prove_r3.py --all`, qui contrôle aussi qu'aucune entrée (1), (1b) ou (3)
  du relevé ne garde son ancien nom, hors des types cités (R4) ; les clés de données de la règle (6) toujours présentes (recherche) ; ce plan (§7 et résultats).

**Acceptation.** Les preuves de T21 ; aucune valeur attendue de test modifiée hors des littéraux des classes (1), (1b) et (3) du relevé ; recette courte de
l'auteur (nouvelle partie jusqu'au premier écran jouable, F5, F6, F9).

**Arrêts.** Un conflit de renommage que Roslyn ne résout pas ; un littéral de classe (5) ; une occurrence changée que `prove_r3.py` n'explique pas ;
un test qui change de valeur ou de compte ; une clé de données ou un nom d'asset touché ; un des 9 types cités renommé ; un paquet absent du
cache.

**Retour arrière.** `git revert` des commits de la tranche, dans l'ordre inverse.

**Vérification de R3 (2026-10-17) : CONFIRMED** par un verifier frais (`2ce88ec` à `a193671`, 50 commits). Rejoué : aucun processus du jeu ;
`check_names.py --tree --stage r3` à 0 constat (207 fichiers, 220 types) ; plus aucun type déclaré `Alundra*` ou `IAlundra*` dans `Alundra/`
hors des 9 cités (tous dans `Alundra/Scripts/`) ; la solution en Release (copie neutralisée) et en Debug ; `Alundra.Tests` 5 097 / 0 échec
dans les deux, comme en T0 ; `prove_r3.py --all` et une vérification indépendante du verifier (remplacement mot à mot dans les jetons de code,
chaînes selon le relevé, commentaires inchangés) : 0 changement inexpliqué ; chaque entrée (1), (1b), (3) du relevé appliquée, (2) et (4)
intactes ; les clés de données de la règle (6) présentes et inchangées ; l'historique d'`Events` (deux `R100`) et `git log --follow` des
fichiers renommés ; l'outil de renommage restauré hors ligne et ses preuves de collision et de conflit insoluble rejouées ; le `cmp` ; la garde
des traces ; le périmètre des chemins. **Aucun constat P0 à P3.** Reportés (P4) : les 50 commits portent « Claude Sonnet 5.5 » dans leur
ligne d'attribution (la ligne vient du harnais des exécuteurs ; rien n'est poussé, réécriture possible sur demande) ; 60 chaînes de tests
dérivées du nom des classes de test (par exemple les dossiers temporaires `"AlundraSoundPlayerTests_"`) suivent les classes de test en R5 ;
`prove_r3.py` corrigé une fois en cours de tranche (`b98b6f8`, faux positifs), sans effet sur la vérification indépendante.

## 8. Tranche R4 — Les 9 types cités par les données ; second export 🧪 (faite et CONFIRMED le 2026-10-17, `6032da0` à `ce6d335` ; révision 2 ; reste la recette de l'auteur)

**Relecture (2026-10-17).** REVISE, deux blocages corrigés (FIX, révision 1) : (1) les outils de R2 et R3 ne savent pas traiter les
types cités (leur table s'arrête à l'étape `r3`, où ces types n'ont pas bougé ; `apply_r2.py` indexe les anciens noms ; l'outil de
renommage saute les paires où `r2 == r3` ; `prove_r3.py` ne connaît que les paires r2→r3) : T0 verse un `apply_r4.py` et un
`prove_r4.py`, l'outil de renommage est appelé avec les 9 paires explicites, et l'ordre interne de T2 est fixé ; (2) le relevé des
littéraux de R4 (51 entrées) est versé en T0 avec la classe et la valeur cible de chacune, la prose de harnais écrite dans une trace
versionnée étant gardée. Seconde relecture : REVISE, un blocage ; cap de deux relectures atteint, disposition de la session
principale, FIX, puis une relecture de clôture (révision 2) : (3) T1 se prouve par `prove_r4.py` (mode `move` : les 10 fichiers appariés
`r3` → `final` par la table, chacun renommé à 100 %, aucun autre fichier changé), pas par `prove_r3.py`.

**But.** Les 9 types que les données citent à leur place finale et sous leur nom final (`final` de `tools/dll-structure/table.json`) :
`Alundra.Maps.WorldProxy` (avec `MapEvent`), `Alundra.Entities.EntityScriptProxy` (et sa partie `.HitPass`), `Alundra.Hero.HeroController`,
`Alundra.UI.Hud.HudViewModel`, `Alundra.UI.Inventory.InventoryViewModel`, `Alundra.UI.SubInventory.SubInventoryViewModel`,
`Alundra.UI.Messages.TextBoxViewModel`, `Alundra.UI.Choice.ChoiceViewModel`, `Alundra.UI.Saving.SaveScreenViewModel` ; le convertisseur écrit
ces noms complets ; les six `alundra-project/UI/Screens/*.design.json` aussi ; le second export prouvé comme le premier (D-R-5) ; le dossier
`Alundra/Scripts/` disparaît.

**Faits (à remesurer en T0).** R0 a posé le motif (§4 : annexe `docs/plan-restructuration-annexe/r0/` avec `predict_names.py`, qui prend des
paires `ancien:nouveau` par clé — `entity`, `world`, `controller`, `vm` — et rend les 886 chemins autorisés et le SHA-1 prédit de chaque
fichier changé ; `manifest.py`, `diff_manifests.py` ; un export de référence avant tout code ; un seul commit parent qui garde tous les tests
verts). Les noms actuels dans les données : `Alundra.Scripts.AlundraEntityScriptProxy` (395 `.entity`), `Alundra.Scripts.AlundraWorldProxy`
(483 `.world`), `Alundra.Scripts.AlundraPlayerController` (`Alundra.gameMode`), les six `Alundra.Scripts.Alundra*ViewModel` (`design.json`,
versionnés). Le convertisseur les écrit en dur (`WorldWriter.cs:142`, `SpriteWriter.cs:406`, `PlayerSetupWriter.cs:49`) et ses tests les
verrouillent (`WorldWriterTests.cs:113-114`, `PlayerSetupWriterTests.cs:53,60`, `SpriteWriterBodyPrefabTests.cs:59,141`). La DLL et les tests
les citent par `typeof(X).FullName` (R0), qui suit le renommage. Les littéraux de R3 de classe (1b) qui nomment un des 9 types cités
(`tools/dll-structure/r3-literals.tsv`, par exemple le préfixe « AlundraWorldProxy: » écrit par `CameraDirector` et `EntitySpawnFactory`)
changent ici. L'outil de renommage de R3 (`tools/dll-structure/rename-tool/`) sert en mode `--rename Old=New`
(paires explicites) ; son mode par domaine saute les paires où `r2 == r3`, ce qui est le cas des 9 types cités. `apply_r2.py` ne lit que
l'étape `r2` et indexe les anciens noms ; `prove_r3.py` ne connaît que les paires r2→r3 et les sujets de commit de R3 : ni l'un ni l'autre
ne s'applique tel quel. Le relevé `tools/dll-structure/r3-literals.tsv` porte 51 entrées qui nomment un type cité : 40 de classe (1) (les
préfixes de journal « AlundraWorldProxy: … », « AlundraEntityScriptProxy: … », « AlundraPlayerController: … », et les tests qui les lisent)
et 11 de classe (1b), avec `new_name == name` (aucune valeur cible versée) ; trois d'entre elles (`HeroTraceHarnessTests.cs:80,84,87`,
l'en-tête `SixItemHeader` recopié « verbatim per docs/plan-oracle-heros.md §1.3 ») sont de la prose de harnais écrite dans les quatre
traces versionnées `docs/hero-trace-389-*.txt` (`HeroTraceHarnessTests.cs:604`, `:702-703`).

**Propriétaire, chemins, budget.** Un `executor` (jugement : la prédiction, l'ordre code → export → tests). Chemins : `Alundra/**`,
`Alundra.Tests/**`, `tools/AlundraTestSaves/**`, `tools/dll-structure/` (T0 ; puis `apply_r4.py` et `prove_r4.py` pour les seules
corrections de leurs règles), `alundra-casaengine-project-converter/Writers/{WorldWriter,SpriteWriter,PlayerSetupWriter}.cs`
et leurs tests, `alundra-project/UI/Screens/*.design.json`, l'export (`alundra-project/`, en place, fenêtre calme), l'annexe
`docs/plan-restructuration-annexe/r4/`, ce plan (§8). Rien dans les sous-modules. Budget : une passe d'exécution, une passe de correction au plus ;
**deux verifiers frais** (rejeu, adversarial), comme R0.

**Tâches.**
- T0 ✅ (aucun code de la DLL, avant tout code) : (a) l'annexe `docs/plan-restructuration-annexe/r4/` : copies de `predict_names.py`,
  `manifest.py`, `diff_manifests.py` de l'annexe `r0/`, avec leurs sommes ; la prédiction pour les noms finaux (les neuf paires
  ancien → nouveau tirées de `table.json` : `entity=`, `world=`, `controller=` et six `vm=`) : la liste des chemins autorisés, le SHA-1
  prédit et la taille de chaque fichier changé, le delta d'octets par clé (le script le calcule), `AssetInfos.json` inchangé, nombre de
  fichiers inchangé, `report.json` seul différent au second export ; l'**export de référence au HEAD** en fenêtre calme : `m0`,
  l'export, `m1`, seul `report.json` diffère ; les comptes de départ (`Alundra.Tests` Release et Debug, convertisseur). (b) **L'outillage
  de R4**, versé dans `tools/dll-structure/` : `apply_r4.py`, qui applique aussi les littéraux de `r4-literals.tsv` (étape (3) de T2), et qui calcule, depuis l'emplacement `final` des 10 fichiers et les noms
  courants (`r3`) de tous les autres types, la ligne `namespace` des 10 fichiers, les `using` de tous les fichiers de la DLL, des tests et
  de `tools/AlundraTestSaves` (règle de l'emplacement courant de R2, méthode d'extension comprise) et la réécriture des qualifications
  `Alundra.Scripts.<T>` (code et `cref`) vers le namespace final ; et `prove_r4.py`, qui apparie les fichiers par l'étape `final` : T1 (`--kind move`) = les
  10 renommages à 100 %, appariés `r3` → `final` ; T2 = seulement des lignes `namespace` des 10 fichiers, des lignes `using`, des qualifications `Alundra.Scripts.`
  → namespace final, des noms ancien → final dans le code et les `cref` (au plus une qualification ajoutée par Roslyn), les littéraux du
  relevé de R4 exactement, et les changements nommés du convertisseur et des `design.json` ; preuves sur une copie jetable de l'arbre : le mode `move` vert sur un `git mv` pur des 10 fichiers, une
  mutation d'un octet en T1 refusée, d'une valeur de test, d'une ligne de corps ou d'un `using` injustifié est refusée ; le T2 rejoué sur la copie
  compile et `prove_r4.py` y sort 0. (c) **Le relevé des littéraux de R4**, `tools/dll-structure/r4-literals.tsv` : les 51 entrées de
  `r3-literals.tsv` qui nomment un type cité, chacune avec sa classe de R4 et sa valeur cible exacte : (1) et (1b) renommées vers le nom
  final ; (4) gardées, dont **la prose de harnais écrite dans une trace versionnée** (`HeroTraceHarnessTests.cs:80,84,87`) ; (5) arrêt et
  question. Un commit d'outil pour (b) et (c), un commit de docs pour (a).
- T1 ✅ — `git mv` des 10 fichiers cités vers leur chemin final, sans changer un octet (ils gardent `namespace Alundra.Scripts` : la solution se
  construit telle quelle), construction, `prove_r4.py --kind move` (les 10 fichiers appariés `r3` → `final` par la table, chacun
  renommé à 100 %, aucun autre fichier changé), commit `refactor(dll): move the data-cited files into their folders (R4, git mv only)`.
- T2 ✅ — **un seul commit** qui garde tout vert, dans cet ordre : (1) l'outil de renommage en mode `--rename` sur les 9 paires explicites
  (`AlundraEntityScriptProxy` une seule fois pour ses deux parties), quand la solution compile encore sous `Alundra.Scripts` (sa
  vérification sémantique comprise) ; (2) `apply_r4.py` : la ligne `namespace` des 10 fichiers, les `using`, les qualifications ; (3) les
  littéraux (1) et (1b) du relevé de R4 ; (4) les trois littéraux du convertisseur et leurs tests ; (5) les six `design.json` ;
  construction ; **export 1** en fenêtre calme (aucune construction ni test en cours, `Alundra.dll` non verrouillée, aucun lanceur,
  éditeur ni jeu) : `m2` contre `m1` avec les chemins autorisés, chaque fichier égal à son SHA-1 prédit, `AssetInfos.json` inchangé ;
  **export 2** aussitôt : seul `report.json` diffère ; la suite du convertisseur et sa phase 8 `Verification: PASSED` ; `Alundra.Tests`
  Release (copie neutralisée) et Debug ; `cmp` ; garde des traces (les traces versionnées ne changent pas) ; `prove_r4.py` sur le diff de
  T2 (chaque entrée (1)/(1b) du relevé appliquée, chaque entrée (4) intacte) ; commit
  `refactor(dll): the data-cited types at their final place and name; second export (R4)`. Une erreur ne se corrige que par une règle
  des outils de T0 (commit d'outil, rejeu de T2 depuis l'état de T1) : sinon arrêt.
- T3 ✅ — Clôture : `check_names.py --tree --stage final` **vert (sortie 0)** ; aucun type déclaré dans `Alundra/` dont le nom commence par
  `Alundra` ou `IAlundra` ; `Alundra/Scripts/` n'existe plus ; `rg -uu` sur `alundra-project/` : aucun `Alundra.Scripts.` dans
  `script_class_name`, `player_controller_class`, `view_model_type` ; `git log --follow` sur les 10 fichiers ; ce plan (§8 et résultats).

**Résultats de T0 ✅ (2026-10-17).** (a) Annexe `docs/plan-restructuration-annexe/r4/` (commit `6032da0`, avant tout code) : les trois scripts
copiés de `r0/` (mêmes sommes) ; prédiction pour les noms finaux : 886 chemins autorisés (879 sorties du convertisseur, 6 `design.json`,
`report.json`), 885 SHA-1 prédits, `PROBLEMS: 0` ; deltas d'octets -2 370 (entités, 395 x -6), -4 830 (mondes, 483 x -10), -12 (`.gameMode`),
soit -7 212 sur les sorties du convertisseur, et -22 sur les six `design.json` ; 24 104 fichiers, `AssetInfos.json` inchangé. Comptes de départ :
`Alundra.Tests` **5 097** en Release (copie neutralisée) et **5 097** en Debug (`cmp` de la DLL égal), convertisseur **854** ; garde des traces
sans écart. Export de référence au HEAD `7ce82c2` en fenêtre calme : `m0` (`01e37b68...`) puis `m1` (`fe367de0...`), seul `report.json` diffère,
`Verification: PASSED (20139 loaded, 2477 existence-checked)`, `OutputFileCount` 24 107. (b)-(c) Outillage (commit `5c61743`) :
`tools/dll-structure/apply_r4.py` (sous-commandes `move`, `fix`, `literals`, `literals-gen`, `literals-check`), `prove_r4.py`
(`--kind move|code|end`, `--commit REV` ou `--index` pour le répertoire indexé) et `r4-literals.tsv` (51 entrées : 40 de classe 1, 8 de classe 1b,
3 de classe 4 gardées, `HeroTraceHarnessTests.cs:80,84,87` ; les cinq entrées d'`IntroTraceHarnessTests` sont de classe 1b : leur texte va dans le
registre de l'harnais, jamais dans `docs/intro-trace-389.txt`, ce que la garde des traces contrôle). Choix de l'outil : un littéral se retrouve
par son texte, non par sa ligne (les lignes `using` de l'étape (2) déplacent les lignes) ; `fix` ne calcule les `using` que pour les 10 types qui
changent de namespace (les `using` des autres types ne bougent pas) ; la preuve lit les numéros de ligne du relevé sur le fichier d'avant sans ses
lignes `using Alundra.Scripts;`. Preuves sur une copie jetable (clone local de la branche, références du moteur rendues absolues, hors dépôt) : le mode
`move` vert sur un `git mv` pur des 10 fichiers ; un octet changé dans un fichier déplacé, ou un autre fichier modifié, refusés ; T2 rejoué
(l'outil de renommage sur les neuf paires : 316 fichiers, 2 924 références et 254 015 identifiants vérifiés, 0 qualification ajoutée ; `fix` ; `literals` :
48 littéraux dans 6 fichiers ; les noms complets du convertisseur et des `design.json`) : la copie **compile** (Alundra.Tests et AlundraTestSaves, 0 erreur,
la DLL ne porte plus `Alundra.Scripts`) et `prove_r4.py --kind code` sort 0 ; huit mutations refusées (une valeur de test, une ligne de corps, un `using`
injustifié, le namespace d'un fichier déplacé, un préfixe de journal non renommé, la prose gardée renommée, un `design.json` changé au-delà de la clé,
une ligne du convertisseur changée au-delà du nom complet) ; la copie restaurée sort 0.

**Résultats de T1 et T2 ✅ (2026-10-17).** T1 (`1ecb199`) : `apply_r4.py move`, 10 fichiers déplacés sans changer un octet, construction Release 0 erreur,
`prove_r4.py --kind move --index` OK. T2 (`15804ae`, un seul commit) : l'outil de renommage sur les 9 paires (316 fichiers, 2 924 références, 254 015 identifiants,
0 qualification ajoutée), `fix` (316 fichiers), `literals` (48 littéraux dans 6 fichiers), noms complets du convertisseur et de ses tests, six `design.json`
(`view_model_type` seul). `prove_r4.py --kind code --index` OK (2 116 occurrences de code, 843 `cref`, 4 qualifications, 48 littéraux réécrits et 3 gardés,
183 `using` ajoutés, 306 retirés). Convertisseur 854 réussis. Export 1 en fenêtre calme : exit 0, `Verification: PASSED (20139 loaded, 2477 existence-checked)` ;
`m2` contre `m1` : 886 fichiers changés (879 sorties, 6 `design.json`, `report.json`), 0 ajouté, 0 retiré, 0 hors ensemble autorisé, les 885 SHA-1 égaux à la
prédiction, `AssetInfos.json` inchangé. Export 2 : seul `report.json` diffère (`Errors` 0). `Alundra.Tests` Release (copie neutralisée) **5 097** réussis,
Debug **5 097** réussis, `cmp` de la DLL égal, garde des traces sans écart.

**Résultats de T3 ✅ (2026-10-17).** `check_names.py --tree --stage final` : sortie 0 (207 fichiers, 220 types de premier niveau, 0 constat, 1 avertissement :
le type imbriqué `Session` de `Alundra.Tests/UI/TextBoxGpuHarness.cs:265` porte le nom d'un segment de namespace, il ne le masque que dans sa classe). Aucun
type déclaré dans `Alundra/` ne commence par `Alundra` ni `IAlundra`. `Alundra/Scripts/` : aucun fichier suivi, et le dossier vide laissé sur disque est supprimé.
`rg -uu` sur `alundra-project/` : aucun `Alundra.Scripts` (ni dans `script_class_name`, `player_controller_class`, `view_model_type`, ni ailleurs hors binaires).
`git log --follow` sur les 10 fichiers : l'historique remonte jusqu'à leur création (de 5 à 202 commits chacun), les renommages sont suivis. Restes de texte,
non traités (commentaires, hors des 9 types ou hors périmètre de la preuve) : `Alundra.Tests/AlundraScriptNamesInTheDataTests.cs:21` (doc XML qui cite
encore `Alundra.Scripts.AlundraEntityScriptProxy`), commentaires du convertisseur (`WorldWriter.cs:141,376`, `SpriteWriter.cs:64,92,322,420`,
`PlayerSetupWriter.cs:59`, `NavigationWriter.cs:23`, `MusicIndexCatalogReader.cs:17`) : à décider avec R6 (documentation). Le nom du fichier de test
`AlundraScriptNamesInTheDataTests` relève de R5.

**Acceptation.** Les preuves de T2 et T3 ; l'annexe de T0 à un commit antérieur au premier commit de code ; aucune valeur attendue de test
modifiée hors des noms complets écrits par le convertisseur et des littéraux (1b) prévus ; recette de l'auteur : nouvelle partie, les
entités bougent (leurs proxies existent), F5, F6, F9 ; dans l'éditeur, la ligne « Script » d'un monde et d'une entité sans avertissement.

**Arrêts.** Un chemin hors de la liste autorisée qui change ; un SHA-1 différent de sa prédiction ; un test qui change de valeur hors des
cas prévus ; un processus du jeu, du lanceur ou de l'éditeur pendant un export ; un conflit de renommage non résolu.

**Retour arrière.** Revert des commits de la tranche et réexport depuis le commit précédent.

**Vérification de R4 (2026-10-17) : CONFIRMED** par deux verifiers frais (rejeu, adversarial ; `7ce82c2` à `ce6d335`). Rejoué : l'annexe
`r4/` et la prédiction (885 SHA-1 et tailles égaux, chemins autorisés, `AssetInfos.json` inchangé, nombre de fichiers inchangé) ; le manifeste
pris par le verifier égal à `m3` ; `rg -uu` : tous les `script_class_name`, `player_controller_class` et `view_model_type` aux noms complets
finaux ; la suite du convertisseur ; la solution en Release et en Debug ; `Alundra.Tests` 5 097 / 0 échec dans les deux ; `check_names.py --tree
--stage final` à 0 ; plus aucun type `Alundra*` dans `Alundra/` ; `Alundra/Scripts/` absent ; `prove_r4.py` sur T1 et T2 ; `git log --follow`
des 10 fichiers ; le `cmp` ; la garde des traces ; mutants inventés (convertisseur, `design.json`, ligne `namespace`, valeur de test) tous
attrapés. **Aucun constat P0 à P2.** Reportés : **P3** `prove_r4.py --kind end` sort 1 sur l'état livré, à cause du seul commentaire
`alundra-casaengine-project-converter/Writers/SpriteWriter.cs:92` (`Alundra.Scripts.WallPlacementOverlay`), un reste de R6 : à rejouer vert
après R6 ; **P4** des commentaires et des `<c>` gardent les anciens noms (≈ 96 fichiers, dont les en-têtes des six `UI/Screens/*.xaml`,
`xunit.runner.json`, `AlundraScriptNamesInTheDataTests.cs:21,64`) : Q-R-5 ; **P4** les commits de T1 à T3 portent « Claude Sonnet 5.5 » (la
ligne vient du harnais des exécuteurs), ceux de T0 « Claude Opus 5.5 ». Reste la recette de l'auteur : nouvelle partie, les entités bougent,
F5, F6, F9 ; dans l'éditeur, la ligne « Script » d'un monde et d'une entité sans avertissement.

## 9. Tranche R5 — Tests en miroir 🧪 (faite et CONFIRMED le 2026-10-22, `61d2d46b` à `613c6c9e`, en mode AUTO ; révision 3, **READY** à la seconde relecture de l'époque ; D-R-21 ; reste la recette de l'auteur)

**Relecture (2026-10-17).** REVISE, deux blocages corrigés (FIX, révision 1) : (1) le hachage de `Alundra.dll` ne pouvait pas rester
égal : la version informative de l'assembly porte le commit (`Alundra/obj/Debug/net9.0-windows/Alundra.AssemblyInfo.cs:16`,
`1.0.0+ce6d335…`, Source Link du SDK, aucune propriété ne l'éteint) ; « la DLL ne change pas » se prouve par ses sources ; (2) les
réponses de Q-R-3 qui demandent une coupe ou un déplacement de fichier n'avaient pas de mécanique : chaque réponse a maintenant son
effet écrit sur les outils (« Effet des réponses »), avec la coupe de R2 et le déplacement du fichier de données ; toute autre réponse
est un arrêt avant T0a. Seconde relecture (révision 1) : **READY**. L'exécution attend les réponses à Q-R-3 (et, pour les choix
autonomes, Q-R-6).

**Révision 2 (2026-10-22, après les réponses de l'auteur ; nouvelle époque de relecture).** Q-R-3 tranchée (D-R-13, D-R-16, D-R-19,
ADR-0080) : les points 1, 2, 3 (harnais compris) et 7 (`ExportGuards/`) passent par la table ; les points 4 et 5 par la coupe de R2,
chaque type partagé vers le dossier de son fichier d'origine ; le point 6 ne déplace rien. Q-R-6 tranchée (D-R-18) : (a) et (b) validés,
(c) corrigé : une tâche T23 envoie à la Corbeille les dossiers temporaires orphelins. Q-R-4 (D-R-17) : les lignes de
`docs/intro-roadmap.md` : Q-R-4c tranchée (D-R-20), T22 corrige ses trois lignes T comme prévu (chemins et noms d'après R5), et les
numéros de ligne de tous les documents vivants sont le travail de R6b, après R5.

**Révision 2b (2026-10-22, après une critique indépendante de la révision 2, avant sa première relecture).** Un P1 et cinq P2 corrigés :
« un type principal par fichier, comme la DLL » suit la lecture de la règle (3) de l'ADR-0075, avec une règle pour les fichiers sans
type à leur nom (P-R5-1) ; `ExportGuards` a son pas (T20b) ; les réponses « garder » et « déplacer » et les conditionnels des tâches
sont retirés ; T23 ne prend que des dossiers vieux de plus de 24 heures, avec ses préconditions, un essai à blanc et sa méthode ; les
scripts de la découverte et le relevé des littéraux d'avant R5 sont versés en T0a ; les comptes des faits sont marqués provisoires.

**Relecture de la révision 2b (2026-10-22, relecteur neuf) : REVISE**, un blocage, reproduit (FIX) : un littéral du relevé L1 peut être
dans un bloc que P-R5-1 coupe au même commit (`AlundraSoundBankTests.cs:187` et `:279`, dans les classes publiques
`AlundraSoundBankAttributeTests` et `AlundraSoundBankChainTests`), ou sous un bloc coupé (`AlundraMusicPlayerTests.cs:107`, `:164`, sous
la collection de la musique) ; la règle du fichier neuf ne permettait pas ces changements et les entrées se repéraient par leur ligne.
**Révision 3** : les entrées Q1, L1 et de capture se repèrent par leur occurrence au commit parent ; une entrée d'un bloc coupé le suit
dans le fichier neuf ; deux preuves de T0c de plus. **Seconde relecture de l'époque (relecteur neuf, révision 3) : READY.**

**But.** `Alundra.Tests/<Domaine>/…`, namespaces `Alundra.Tests.<Domaine>`, plus `Arcs/`, `Support/` et `ExportGuards/`, sur le modèle de la DLL (un
dossier = un namespace) ; les classes de test sans préfixe `Alundra` ; **aucune valeur attendue changée, aucun test ajouté ni retiré** ;
aucun fichier de `Alundra/` touché (la DLL ne diffère de celle d'avant que par le commit inscrit dans sa version informative).

**Faits (découverte du 2026-10-17 à `7ce82c2`, `scratchpad/r5-disc/` : `disc-report.md`, contre-vérifiée par `check-report.md` ; rejouée
le 2026-10-17 à `211a757`, après R4, dans `scratchpad/r5-disc2/`).** Le rejeu prend la DLL de tests de `bin/Debug` (construite après le
dernier commit de sources, `15804ae`), relance `IlScan` puis `evidence.py`, `classify.py`, `couplings.py`, `build.py`, `extras.py` ; une
seule correction des scripts : `common.py` reconnaît aussi les noms finaux de la table (après R4, l'arbre est à l'étape `final` ; sans elle,
les 9 types déplacés par R4 étaient inconnus). Résultat : `r5-files.tsv` identique à celui du 2026-10-17 sur ses 336 lignes pour le
dossier, le chemin, la décision et la confiance ; seuls des numéros de ligne changent (R4 a retiré des `using Alundra.Scripts;`).
Comptes : 333 `.cs` (308 en `Alundra.Tests`, 20 en `Alundra.Tests.UI`, 5 en `Alundra.Tests.Scripts`), 432 types de premier niveau dont
298 préfixés, 44 fichiers à plusieurs types, 2 classes partielles sur deux fichiers (`AlundraLootRollWorldTests`,
`AlundraRandomPerTickTests`), 3 024 `Fact`/`Theory` (5 097 cas exécutés) ; 333 fichiers changent de dossier, 306 aussi de nom ; aucune
collision (types de la DLL, du moteur, de MGUI, des paquets, de la BCL, segments de namespace) ; deux avertissements sur des types
imbriqués (`Session`, `UI/TextBoxGpuHarness.cs`, et `NoOpEventProgramRunner`). Couplages (`couplings.tsv`) : **Q1** 39 références
qualifiées à un type de test (`using static Alundra.Tests.X;` surtout) ; **L1** 81 littéraux qui portent un nom de classe de test
(`r5-literals.tsv` : 25 préfixes de dossiers temporaires, 50 préfixes de message, 6 « Not used by … ») ; aucun test n'asserte sur ces
chaînes ; **X1** 5 `CollectionDefinition`, dont la musique (`[Collection]` posé dans 98 fichiers) et le générateur (19) ; **F1**
`Alundra.Tests/Data/story-chain-skipped-opcodes.tsv`, lu par `StoryChainOpcodeAudit.FindListPath` ; **R1** les sondes de la DLL
(`Probes/RandomShadow.cs`, `Probes/SlotShadow.cs`) testent `Namespace.StartsWith("Alundra.Tests")`, vrai après R5, et journalisent le nom
simple de la classe de test, sans consommateur ; **T1** `tools/dll-structure/r3-literals.tsv`, `r4-literals.tsv`, `prove_r3.py --all` et
`prove_r4.py --kind end` lisent des chemins de `Alundra.Tests/` : après R5, leurs contrôles de fin ne se rejouent qu'au commit de leur
clôture. **Capture de namespace** (prouvée par une compilation jetable) : dès qu'un fichier existe dans `Alundra.Tests.<Seg>`, un `Seg.X`
écrit dans n'importe quel fichier de `Alundra.Tests.*` désigne ce namespace avant `Alundra.<Seg>` (`CS0234`) : six sites à écrire
`Alundra.<Seg>.X` au pas du domaine qui crée le namespace (`AlundraDamageTests.cs`, `Items.ItemTables` et `Combat.BalanceTables` ;
`AlundraHeroLifeTests.cs`, deux ; `IntroTraceHarnessTests.cs`, `Dialogues.DialogueDirector`, deux). Les `using` à ajouter se recalculent
à l'application (le critique en compte 368 paires dans 221 fichiers, la découverte 356 dans 219 : la colonne ne sert pas d'entrée).
**Inconnu** : l'ordre d'exécution des classes par xUnit peut dépendre de leurs noms complets ; R5 est la première tranche qui les
change ; les collections et les remises à zéro existantes protègent les états partagés connus, sans preuve contre un nouvel ordre.
**Ces comptes sont ceux de la classification d'avant les réponses de l'auteur** (révision 2b) : T0a les recalcule avec les règles
tranchées (le recomptage de la critique trouve, sur les 44 fichiers à plusieurs types, 19 sans type à leur nom, et non 15).

**Choix de l'auteur (Q-R-3, §11).** Sept choix de structure : nom ou sujet (79 fichiers, 593 tests), noms des tests des types renommés
(`BgmPlayer`, `GameRandom`, `SfxBank`, `HeroController`), périmètre d'`Arcs/`, types partagés déclarés dans un fichier de test (collections
xUnit, hôtes d'aides), un type principal par fichier (44 fichiers), le fichier de `Data/`, les gardes d'export sans type de la DLL. Les onze
classements contestés par le critique (`check-report.md` §2.B) suivent la règle choisie au point 1 ; une ligne que la règle ne tranche
pas est une question, pas un choix de l'exécuteur. **Réponses du 2026-10-22 (D-R-13, D-R-16, D-R-19 ; ADR-0080)** : point 1, par le type
du nom du fichier ; point 2, seulement le préfixe retiré ; point 3, `Arcs/` reçoit les 26 scénarios de bout en bout et les harnais de
traces, rien d'autre ; point 4, aucun type partagé dans `Support/` ; point 5, un type principal par fichier, comme la DLL (P-R5-1 ci-dessous) ; chaque type
partagé (les collections xUnit qui ne sont pas seules dans leur fichier, les hôtes d'aides) va dans son propre fichier, dans le dossier
d'arrivée de son fichier d'origine ; point 6, le fichier de données reste à sa place ; point 7, les deux gardes d'export dans `ExportGuards/`.

**Effet des réponses de Q-R-3 sur les outils** (T0a les applique ; T0b à T0c doivent couvrir celles que l'auteur a données).
- Points 1 (nom ou sujet), 2 (noms des tests des types renommés), 3 (périmètre d'`Arcs/`) et 7 (gardes d'export) : **la table seule**
  (`r5-files.tsv`, `r5-types.tsv`) ; aucun outil ne change.
- **P-R5-1 — lecture de « un type principal par fichier, comme la DLL »** (révision 2b ; précision de la session, à partir de la lecture
  de la règle (3) écrite dans l'ADR-0075 ; l'auteur peut la contester) : (i) dans un fichier de tests, chaque type secondaire **public**
  va dans son propre fichier, un type secondaire **interne** reste avec le type principal ; (ii) chaque type **partagé** (utilisé par un
  autre fichier) va dans son propre fichier, quelle que soit sa visibilité (D-R-16) ; (iii) un fichier où aucun type de premier niveau
  ne porte son nom prend le nom de son **premier type public déclaré** (à défaut, de son premier type déclaré), par le `git mv` du temps
  (1), et ses autres types suivent (i) et (ii) ; T0a liste ces fichiers et le type choisi pour chacun ; (iv) un type coupé va dans le
  dossier d'arrivée de son fichier d'origine ; un type déjà seul dans son fichier (par exemple `UI/MguiDataBindingCollection.cs`) n'est
  pas coupé. Un cas que (i) à (iv) ne tranchent pas est un arrêt.
- Points 4 et 5 (D-R-13, D-R-16, P-R5-1) : la coupe de R2 (§6, temps 2),
  reprise par `apply_r5.py --code` : au temps (2) du domaine du fichier d'arrivée du type coupé, sa déclaration, avec ses commentaires de
  documentation, ses attributs et sa ligne vide de tête, copiée à l'octet dans un fichier neuf à son chemin de `r5-files.tsv`, avec
  l'en-tête du fichier où elle se trouve à ce moment (ses `using`, sa directive `#nullable` s'il en a une) et le `namespace` de son
  arrivée ; retirée de ce fichier ; puis les `using` recalculés depuis l'emplacement courant de chaque type, puis l'outil de renommage.
  Exemple (révision 2, D-R-16) : la collection de la musique, déclarée dans `AlundraMusicPlayerTests.cs`, est coupée dans son propre
  fichier, à son chemin de `r5-files.tsv` sous `Audio/`, au temps (2) du domaine `Audio`, celui de son fichier d'origine ; les fichiers des
  domaines passés avant `Audio` la trouvent encore à son ancien emplacement, et les `using` recalculés au temps (2) de chaque domaine
  suivent son emplacement courant. Une classe partielle n'est jamais coupée.
  `prove_r5.py --kind code` apparie les fichiers par la table, jamais par la détection de renommage de git : un fichier neuf n'est permis
  que s'il est l'arrivée d'un type coupé de ce domaine et si son contenu est l'en-tête plus le bloc retiré de son fichier source au
  commit parent (renommages de la table, ligne `namespace`, `using` et entrées Q1, L1 et de capture du bloc mis à part) ; le fichier
  source perd exactement ce bloc, et un fichier qui perd tous ses types sauf un est renommé par le `git mv` du temps (1) selon (iii),
  jamais supprimé. **Repérage des entrées** (révision 3) : une entrée Q1, L1 ou de capture se repère par son **occurrence au commit
  parent** (son fichier, son texte exact et son rang parmi les occurrences de ce texte dans ce fichier), jamais par son numéro de ligne ;
  une entrée prise dans un bloc coupé suit ce bloc dans son fichier neuf et y est acceptée exactement comme le relevé le dit ; une coupe
  qui retire des lignes au-dessus d'une entrée ne l'invalide pas. Cas connus : `AlundraSoundBankTests.cs:187` et `:279` (dans les blocs
  coupés `AlundraSoundBankAttributeTests` et `AlundraSoundBankChainTests`), `AlundraMusicPlayerTests.cs:107` et `:164` (sous la collection
  coupée).
- Point 6 : la réponse est **« rester »** (D-R-13) : `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` et son littéral
  (`AlundraStoryChainOpcodeAudit.cs:369`) ne changent pas, et `prove_r5.py` refuse tout changement de l'un ou de l'autre.
- **Un cas que ces règles ne tranchent pas** (un mécanisme qui n'est ni un changement de table ni cette coupe) rouvre la tranche pour une
  relecture : arrêt avant T0a.

**Choix de la session principale (mode autonome, réversibles, soumis à l'auteur en Q-R-6 ; tranchés le 2026-10-22, D-R-18 : (a) et (b)
validés, (c) corrigé par T23).** (a) Les preuves de fin de R3 et R4
(`r3_literals.py check --stage after`, `prove_r3.py --all`, `prove_r4.py --kind end`) sont **gelées** à leur commit de clôture (`a193671`,
`ce6d335`) : un paragraphe de `tools/dll-structure/README.md` le dit, rien n'est remappé ; (b) les commentaires du code ne changent pas
(Q-R-5 ; R8 les corrige, D-R-14) ; les trois lignes T de R6 (`docs/intro-roadmap.md:26,63`, `docs/formats/dialogues-yarn.md:123`) se
corrigent en T22 (Q-R-4c tranchée, D-R-20 : les numéros de ligne de tous les documents vivants sont recalculés ensuite par R6b) ; (c) ~~les dossiers
temporaires des anciennes exécutions, nommés par les anciens préfixes, restent orphelins (hors du dépôt)~~ **corrigé (D-R-18)** : T23 les
envoie à la Corbeille.

**Propriétaire, chemins, budget.** Un `executor` (T0 : l'outil et la table s'écrivent avec jugement ; ensuite l'application est
mécanique). Chemins : `Alundra.Tests/**` (hors `bin/`, `obj/`, `TestResults/`), `tools/dll-structure/` (T0 ; puis les outils pour les
seules corrections de leurs règles, par un commit d'outil suivi du rejeu), les trois lignes T des docs (T22), ce plan (§9) ; hors du dépôt, on
n'efface que les dossiers temporaires orphelins de T23, vers la Corbeille (les copies et listes de travail hors du dépôt de T0b et T0d restent). Aucun fichier de
`Alundra/`, du convertisseur, des données, d'un sous-module ; aucun export. Le jeu, l'éditeur et le lanceur fermés pendant la tranche
(vérifié en T0 : `alundra-project/Alundra.dll` s'ouvre en écriture). Release toujours avec la copie neutralisée
(`-p:CustomAfterMicrosoftCommonTargets=…/e20e0a3b-exec/skipcopy.targets`) ; Debug en dernier. Budget : une passe d'exécution, une passe de
correction au plus ; un verifier frais à la fin.

**Tâches.**
- T0a (aucun test touché) — **les réponses de l'auteur à Q-R-3** appliquées aux règles de la découverte (`classify.py`, `decisions.py`),
  la découverte rejouée au HEAD, la table versée : `tools/dll-structure/r5-files.tsv` (une ligne par fichier : ancien chemin, nouveau
  chemin, namespace, domaine du commit ; un fichier neuf issu d'une coupe y a une ligne sans ancien chemin, avec son fichier source) et
  `tools/dll-structure/r5-types.tsv` (ancien nom complet → nouveau nom complet, une ligne par type de premier niveau ; les scissions de
  P-R5-1, avec le fichier d'arrivée de chaque type, et la liste des fichiers renommés par (iii)) ; versés aussi, comme en R2 à R4, les
  scripts de la découverte (`common.py`, `evidence.py`, `classify.py`, `decisions.py`, `couplings.py`, `build.py`, `extras.py`) et
  `tools/dll-structure/r5-literals.tsv`, **dans ses valeurs d'avant R5** (T0c et T23 le lisent) ; contrôles : chaque
  `.cs` couvert une fois, chaque type une fois, aucun chemin en double (insensible à la casse), aucune collision
  (`check_names.collision_findings`), aucun type au nom d'un segment ; un commit d'outil.
- T0b — **l'outil de renommage** étendu : `FindType` cherche aussi dans le projet `Alundra.Tests` (aujourd'hui la seule compilation
  `Alundra`, `rename-tool/Program.cs`) et lit ses paires dans `r5-types.tsv` ; mêmes garanties qu'en R3 (références relevées avant,
  liaisons inchangées après, aucun diagnostic nouveau, refus sans écrire) ; preuve sur une copie hors du dépôt : un domaine de tests
  renommé, solution construite, et un conflit fabriqué refusé.
- T0c — `tools/dll-structure/apply_r5.py` : pour un domaine, (1) `git mv` des fichiers vers leur chemin de `r5-files.tsv` (nom de fichier
  compris) ; (2) la ligne `namespace`, les `using` recalculés depuis l'emplacement courant de chaque type de test (comme `apply_r2.py`), les
  39 références Q1, les littéraux L1 de `r5-literals.tsv` (81 à la découverte), les six sites de capture au pas du domaine qui crée leur
  namespace, et la coupe des points 4 et 5 (P-R5-1) décrite ci-dessus (le fichier de données ne bouge pas, point 6) ; puis l'outil de
  renommage. `tools/dll-structure/prove_r5.py`, sur le
  modèle de `prove_r3.py` (lexeur de R2, occurrence par occurrence) : un commit (1) ne contient que des renommages à 100 % selon la table ;
  un commit (2) ne change que la ligne `namespace`, des `using` ajoutés ou retirés, les noms selon `r5-types.tsv` (avec au plus une
  qualification ajoutée par Roslyn), les entrées Q1, L1 et de capture de son domaine, exactement comme le relevé le dit (repérées par leur
  occurrence au commit parent, pas par leur ligne : une entrée d'un bloc coupé est acceptée dans le fichier neuf), et, s'il coupe, les
  fichiers neufs et les blocs retirés que « Effet des réponses » permet ; chaque entrée du domaine doit changer ; tout autre changement
  est refusé. Preuves de T0 : une mutation d'une valeur attendue, d'un littéral hors relevé et
  d'un commentaire, chacune refusée ; une entrée L1 omise refusée ; pour la coupe : un bloc coupé modifié d'un octet, une
  coupe faite à un autre domaine, un type coupé deux fois et un type interne non partagé coupé, chacun refusé ; le fichier de données
  déplacé ou son littéral changé, refusé ; **un bloc coupé qui porte une entrée L1** (sur une copie du domaine `Audio` : la coupe de
  `AlundraSoundBankTests.cs`, lignes 187 et 279) accepté quand le littéral change comme le relevé le dit, refusé quand il reste
  inchangé ou change autrement ; **une entrée L1 sous un bloc coupé** (`AlundraMusicPlayerTests.cs:107`, `:164`) acceptée à sa nouvelle
  ligne (révision 3).
- T0d — **comptes de départ** : aucun processus du jeu ; solution en Release (copie neutralisée) et en Debug, 0 erreur ; `Alundra.Tests`
  Release et Debug, 5 097 réussis / 0 échec, **deux exécutions complètes** chacune ; `dotnet test --list-tests` (la liste des noms complets
  des tests, gardée hors du dépôt) ; le commit de base (`git rev-parse HEAD`) ; l'absence de `Directory.Build.props` et
  `Directory.Build.targets` à la racine et dans `Alundra/` (rien d'autre que `Alundra/` ne nourrit la DLL) ; `cmp` de la DLL Debug avec
  `alundra-project/Alundra.dll` ; garde des traces.
- T1 à T21 — par domaine, dans l'ordre de la découverte (`Support` en premier ; `Maps` avec `Loop`, `Session`, `Entities` avec
  `Spawning`, `Events`, `Ai` et ses trois sous-dossiers, `Hero`, `Combat`, `Items` avec la classe partielle `LootRollWorldTests`, `Rng` avec
  `RandomPerTickTests`, `Physics`, `Cells`, `Cameras`, `Rendering` avec `Backdrops` et `Effects`, `Audio`, `Transitions`, `Dialogues` avec
  `Yarn`, `UI` et ses huit sous-dossiers d'un coup, `SaveGames`, `Probes`, puis **T20b** `ExportGuards` (les deux gardes d'export, D-R-19 ; révision 2b) ; `Arcs` en dernier), **deux commits** : (1) `apply_r5.py
  <Domaine> --move`, construction de la solution en Release (copie neutralisée), `prove_r5.py --kind move`, commit
  `refactor(tests): move the <domaine> tests into their folder (R5, git mv only)` ; (2) `apply_r5.py <Domaine> --code`, construction,
  `prove_r5.py --kind code` vert, `Alundra.Tests` Release 5 097 / 0, commit `refactor(tests): <domaine> tests in their namespace, without
  the Alundra prefix (R5)`. Une erreur de construction ne se corrige que par une règle de l'outil (commit d'outil, rejeu du temps (2)
  depuis l'état du commit (1)) : sinon arrêt.
- T22 — Clôture : plus de dossier `Alundra.Tests/Scripts/`, plus de fichier directement sous `Alundra.Tests/UI/` (seulement ses
  sous-dossiers), plus de `.cs` à la racine de `Alundra.Tests/` (y restent le projet, `xunit.runner.json` et `Data/`, point 6) ; plus aucun type `Alundra*` ni `IAlundra*` déclaré dans
  `Alundra.Tests/` (recherche) ; un mode `check_names.py --tests` (chaque fichier à son chemin de `r5-files.tsv`, namespace = dossier,
  aucun préfixe, aucune collision), sortie 0 ; la solution en Release (copie neutralisée) puis en Debug, 0 erreur ; `Alundra.Tests`
  Release et Debug, 5 097 / 0, **deux exécutions complètes** chacune ; la liste `--list-tests` égale à celle de T0d par `r5-types.tsv`
  (même multi-ensemble de noms de méthodes et de cas, chaque classe renommée selon la table) ; `git diff --quiet <base de T0d> HEAD --
  Alundra/ CasaEngineMonogame` sort 0 (les sources de la DLL et le pointeur du moteur qu'elle référence) et toujours aucun
  `Directory.Build.*` (les sources de la DLL inchangées ; la DLL n'est pas comparée à l'octet, sa
  version informative portant le commit) ; `cmp` de la DLL Debug de l'état final avec `alundra-project/Alundra.dll`, où la construction
  Debug la copie ; garde des traces ; `git log --follow` retrouve l'historique d'avant R5 de cinq fichiers tirés de domaines
  différents et des deux classes partielles ; le paragraphe de gel de `tools/dll-structure/README.md` (choix (a)) ; les trois lignes T des
  docs ; ce plan (§9 et résultats).
- T23 (révision 2b, D-R-18) — **Les dossiers temporaires orphelins**, après T22.
  (1) **Sélection** : les enfants directs du dossier temporaire de l'utilisateur (`Path.GetTempPath()`, où les tests créent les leurs, par
  exemple `AlundraBgmFadeDirectorTests.cs:466` avant R5) dont le nom est l'un des 25 préfixes de la classe `temp-folder-prefix` du
  `r5-literals.tsv` versé en T0a (valeurs d'avant R5) **suivi d'un GUID** (la forme `Guid.NewGuid()` ou `.ToString("N")` des sites), et
  dont la **dernière écriture date de plus de 24 heures** : les autres branches et les cinq autres worktrees du dépôt portent encore les
  anciens noms et peuvent créer ces dossiers à tout moment, et aucune exécution de tests ne dure 24 heures. Les autres dossiers
  temporaires « Alundra… » ou « alundra-… », dont R5 ne change pas le préfixe, ne sont pas visés.
  (2) **La liste** (chemin, taille, nombre de fichiers, plus long chemin, date) s'écrit avant toute action et se verse au plan ; une liste
  vide termine T23 (c'était le cas au relevé du 2026-10-22).
  (3) **Préconditions**, sinon le dossier reste et c'est une question : moins de 10 Mo, aucun chemin de plus de 200 caractères, la Corbeille
  active sur le volume (`NukeOnDelete` à 0, lu dans `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\BitBucket\Volume`) et sa
  capacité libre suffisante.
  (4) **Essai à blanc d'abord** : un dossier jetable de la même forme, créé pour l'essai sous le dossier temporaire, envoyé par la même
  méthode, retrouvé dans la Corbeille (`Shell.Application`, `NameSpace(10)`, son nom et son emplacement d'origine), puis restauré et
  renvoyé ; une méthode qui échoue à l'essai n'est pas utilisée.
  (5) **Envoi** : chaque dossier, **un à la fois**, par cette méthode, puis **vérifié dans la Corbeille** avant le suivant. Jamais de
  suppression définitive (mémoire « Corbeille : suppression silencieuse » : une suppression « vers la Corbeille » d'un arbre que Windows
  ne sait pas recycler l'efface sans message ; les préconditions et l'essai à blanc écartent ce cas, la vérification le détecte).
  Aucun autre dossier n'est touché ; aucun commit (hors du dépôt), sauf le compte rendu dans ce plan.

**Acceptation.** Les preuves de T22 ; la liste de T23 et, pour chacun de ses dossiers, sa présence dans la Corbeille ; aucune valeur attendue de test modifiée, aucun test ajouté, retiré ou sauté ; recette courte de
l'auteur (nouvelle partie jusqu'au premier écran jouable, F5, F6, F9), demandée par la règle du programme bien qu'aucun source de la DLL
ne change.

**Arrêts.** Un test qui change de valeur, de compte ou d'issue (dont un échec qui n'apparaît que dans le nouvel ordre d'exécution :
question, pas de correction) ; la liste des tests qui diffère autrement que par les noms de la table ; un fichier de `Alundra/` qui change ; une réponse de Q-R-3
qui demande un mécanisme non décrit dans « Effet des réponses » ; un
conflit de renommage que Roslyn ne résout pas ; une occurrence changée que `prove_r5.py` n'explique pas ; un fichier hors des chemins ; un
classement que les réponses de l'auteur ne tranchent pas ; un paquet absent du cache ; en T23, un dossier que la Corbeille refuse ou qui n'y
apparaît pas après l'envoi, ou un dossier dont le nom ne suit pas un ancien préfixe : arrêt et question, rien d'autre n'est envoyé.

**Retour arrière.** `git revert` des commits de la tranche, dans l'ordre inverse ; les dossiers de T23 se restaurent depuis la Corbeille.

**Résultats de l'exécution (2026-10-22).** Mode AUTO accordé par l'auteur pour R5 seule ; branche `chantier/restructuration-dll-r3`, HEAD de départ `ce9c36f0`. Faits : T0a à T0d, T1 à T21 (22 domaines), le renommage de D-R-21 (3), T22 et T23. Aucun fichier de `Alundra/`, du moteur, de `alundra-datas-analyser/`, des données ni du convertisseur touché (la DLL ne change pas, T22) ; rien poussé, rien fusionné.

- **T0a ✅ (`61d2d46b`).** Les réponses de l'auteur à Q-R-3 et P-R5-1 appliquées à `tools/dll-structure/r5-discovery/` (`decisions.py` : les entrées structurelles, `Arcs` pour les 26 scénarios et harnais, `Support` pour les trois tests de l'outillage et l'initialiseur de module, `ExportGuards` pour les deux gardes ; les entrées « sujet » ne servent que si le nom ne décide rien ; `build.py` : le nom d'un type de la DLL, puis le premier mot, puis le sujet, puis les références ; la coupe de P-R5-1). Versés : `r5-files.tsv` (414 lignes : 333 `.cs` existants, 78 fichiers neufs des coupes, 3 fichiers qui ne bougent pas), `r5-types.tsv` (432 types), `r5-literals.tsv` (81 entrées L1, 25 préfixes de dossiers temporaires, 50 préfixes de message, 6 « Not used by », avec leur rang d'occurrence et leur domaine), `r5-qualified.tsv` (45 entrées : 33 `using static`, 5 qualifications en ligne, 1 `using` d'espace de noms, 6 captures), les scripts, `il_refs.tsv` (la preuve IL de la découverte) et `r5_lib.py --check-tables` (0 problème : chaque `.cs` couvert une fois, chaque type une fois, aucun chemin en double insensible à la casse, namespace = dossier, aucune collision, aucun type au nom d'un segment). Comptes : 411 `.cs` après R5 (333 + 78), 22 domaines (`Support` 56 fichiers, `Entities` 47, `UI` 43, `Hero` 38, `Dialogues` 35, `Arcs` 33, `Rendering` 26, `Maps` 24, `Ai` 18, `Events` 17, `Audio` 12, `Combat` 9, `Items` 9, `SaveGames` 9, `Rng` 8, `Transitions` 7, `Session` 4, `Physics` 4, `Cells` 4, `Cameras` 4, `Probes` 2, `ExportGuards` 2) ; fondement du classement des 333 fichiers existants : nom d'un type 127, premier mot 69, structurel 32, aides sans test 28 (par leurs utilisateurs), **sujet 24 et références 53** (le nom ne nomme ni un type ni un dossier : 77 fichiers, colonne `basis` de `r5-files.tsv`, à contester par l'auteur) ; 78 coupes (65 types partagés, 8 publics, 5 publics et partagés) ; 20 fichiers renommés par P-R5-1 (iii) (liste dans `r5-files.tsv`, colonne `rule` = `iii-*`) ; aucune entrée de relevé qu'une coupe déplace ou décale (contrôle de `extras.py`).
- **T0b ✅ (`212faac0`).** `rename-tool/` : `--r5 <Domaine> [--types r5-types.tsv]`, `--in-tests`, la forme courte d'une classe d'attribut. Preuve sur un clone jetable hors dépôt (références du moteur rendues absolues) : les 12 classes du domaine `Audio` renommées (114 fichiers, 125 références, 104 205 identifiants vérifiés, 0 qualification ajoutée), `Alundra.Tests` et `tools/AlundraTestSaves` compilent en Release ; deux conflits fabriqués (deux classes vers `Foo`, une classe vers le nom d'une autre) refusés sans rien écrire.
- **T0c ✅ (`65a6518c`).** `apply_r5.py` (`--move`, `--code`, `--no-rename`, `--only-domain`, `--repo`, `--dry-run` ; `--move` retire les dossiers qu'il vide), `prove_r5.py` (`--kind move|code`, `--commit`, `--index`, `--worktree`, `--before-dir/--after-dir`), `selftest_r5.py`, `r5_lib.py`. **Rejeu complet sur un clone jetable : les 22 domaines, deux commits chacun, `prove_r5.py` vert à chaque commit, `Alundra.Tests` compile après chaque domaine (Release), état final = les tables (411 fichiers, 432 types, aucun dossier `Scripts/`, aucun `.cs` à la racine ni sous `UI/`), et la liste `--list-tests` du clone, ramenée par `r5-types.tsv`, égale à celle de T0d hors 87 cas d'une théorie qui lit les données exportées (absentes du clone).** Preuves (mutations refusées, la base acceptée) : sur `Audio` 15 cas (une valeur attendue, un littéral hors relevé, un commentaire, une entrée L1 omise, un bloc coupé changé d'un octet, une coupe prouvée à un autre domaine, un type coupé deux fois, un fichier coupé absent, le littéral du fichier de données changé, les entrées L1 dans les blocs `SoundBankAttributeTests` et `SoundBankChainTests` coupés de `SoundBankTests.cs` — acceptées quand elles changent comme le relevé le dit, refusées inchangées ou changées autrement — et sous la collection coupée de `MusicPlayerTests.cs` — acceptées à leur nouvelle ligne, refusées sinon) ; sur `Support` 7 cas (une entrée Q1 omise, une ligne `namespace` non changée, un `using` ajouté sans raison, un `using` retiré, un type interne non partagé coupé, un fichier que la table ne fait pas) ; le fichier de données déplacé (`git mv`, mode `--worktree`) refusé, remis en place.
- **T0d ✅ (base `65a6518c`).** Aucun processus du jeu, de l'éditeur ni du lanceur avant chaque construction ; `alundra-project/Alundra.dll` s'ouvre en écriture. Solution `.slnx` en Release (copie neutralisée) puis en Debug : 0 erreur. `Alundra.Tests` : **Release 5 097 / 0 échec deux fois** (6 min 56 s, 6 min 47 s), **Debug 5 097 / 0 échec deux fois** (6 min 51 s, 6 min 26 s) ; `dotnet test --list-tests` en Release et en Debug : 5 097 noms, les deux listes égales (gardées hors du dépôt, `scratchpad/r5-exec/list-tests-release.txt` et `list-tests-debug.txt`). Aucun `Directory.Build.props` ni `Directory.Build.targets` à la racine ni dans `Alundra/`. `cmp` de la DLL Debug de `Alundra/bin/Debug/net9.0-windows/` avec `alundra-project/Alundra.dll` : identique. Garde des traces : après chaque suite, `git diff --ignore-cr-at-eol --quiet` sur `docs/hero-trace-389-*.txt` et `docs/intro-trace-389.txt` sort 0 (les quatre `hero-trace` apparaissent « modifiées » dans `git status` après une suite à cause des seules fins de ligne ; remises à l'état du commit par `git checkout` après chaque suite).
- **T1 à T21, T20b ✅ (22 domaines, deux commits chacun, du 2026-10-22).** Dans l'ordre : `Support` (`3f55eb3d` move, `928d6bfc` code), `Maps` (`609771d5`, `537cc7ec`), `Session` (`604f07a2`, `1fe06761`), `Entities` (`979921cd`, `b213b789`), `Events` (`4fa5e0ad`, `9972c512`), `Ai` (`4857bb4f`, `7c16327d`), `Hero` (`1f76688b`, `deefb42b`), `Combat` (`f75f6a94`, `fa5e5e55`), `Items` (`78b5eb1f`, `bb871b52`), `Rng` (`4c2b946a`, `9f3b7173`), `Physics` (`70b3524c`, `0b34594e`), `Cells` (`7267ed6c`, `b1c80ca2`), `Cameras` (`b595b3d1`, `48387a3d`), `Rendering` (`e81d1915`, `eac7228d`), `Audio` (`96215bdd`, `408f5f97`), `Transitions` (`2d39c8cc`, `b85cce16`), `Dialogues` (`8531f209`, `f25ce5a9`), `UI` (`f1672098`, `710c7f5c`), `SaveGames` (`097812fd`, `027a0307`), `Probes` (`3dc28399`, `b9961ba9`), T20b `ExportGuards` (`84887639`, `60a64b48`), `Arcs` (`adb24e5a`, `968d1aba`). Messages exactement ceux du plan (`move the <domaine> tests into their folder (R5, git mv only)` ; `<domaine> tests in their namespace, without the Alundra prefix (R5)`). Les preuves de chaque domaine (`prove_r5.py --kind move|code` vert, construction Release, `Alundra.Tests` 5 097 / 0) ont été faites par les exécutions de ces domaines ; elles ne sont pas rejouées domaine par domaine ici, T22 prouve l'état final.
- **D-R-21 (3), classe imbriquée ✅ (`968e468b`).** `HudComposerTests+AlundraHudComposerEquipmentBackgroundsTests` devient `HudComposerEquipmentBackgroundsTests` (seul le préfixe tombe). Outils d'abord (`4fc8b17d`, puis `b4fadfaa` pour la lecture du code de sortie 1 de `git grep` = aucune occurrence) : `tools/dll-structure/r5-nested-types.tsv` (relevé des types imbriqués, une ligne, versé ; `r5-types.tsv` ne couvre que les types de premier niveau), `rename-tool --r5-nested <tsv>` (le type est cherché dans sa classe englobante, mêmes garanties qu'en R3 : références relevées avant, liaisons inchangées après, aucun diagnostic nouveau, refus sans écrire), `prove_r5_nested.py`, `cmp_test_lists.py` (comparaison des listes `--list-tests` avec `r5-types.tsv` et `r5-nested-types.tsv`), et le mode `check_names.py --tests` (laissé non commité par la clôture précédente : il suit le plan, gardé, sa ligne de README écrite). Le renommage a été fait par l'outil : 1 fichier, 0 référence hors déclaration, 1 006 identifiants vérifiés, 0 qualification ajoutée ; `prove_r5_nested.py` : le commit ne change que ce nom à sa déclaration (le fichier neuf, avec l'ancien nom remis, égale l'ancien à l'octet ; une seule ligne changée ; l'ancien nom ne figure plus dans aucun `.cs` suivi) ; construction Release 0 erreur ; `Alundra.Tests` Release 5 097 / 0 échec (5 min 59 s).
- **T22 ✅ (clôture ; état `b4fadfaa` pour les sources, seuls la documentation et les outils de preuve changent ensuite).** `check_names.py --tests` : 411 fichiers, 432 types de premier niveau, 0 constat (un avertissement connu : `Session` imbriqué dans `UI/Shared/TextBoxGpu.cs`). Plus de dossier `Alundra.Tests/Scripts/`, plus de `.cs` à la racine ni directement sous `UI/` (y restent le projet, `xunit.runner.json`, `Data/` et son fichier de données, point 6), plus aucun type `Alundra*` ou `IAlundra*` déclaré (recherche et `check_names`). Solution `.slnx` en Release (copie neutralisée) puis en Debug : 0 erreur, aucun processus du jeu, de l'éditeur ni du lanceur avant chaque construction. `Alundra.Tests` : **Release 5 097 / 0 échec deux fois** (6 min 10 s, 5 min 40 s), **Debug 5 097 / 0 échec deux fois** (6 min 26 s, 6 min 16 s) ; aucun test ajouté, retiré ni sauté (0 ignoré). `--list-tests` : 5 097 noms en Release et en Debug, les deux listes égales comme ensembles de noms, et chacune égale à celle de T0d par `r5-types.tsv` et `r5-nested-types.tsv` (`cmp_test_lists.py` : IDENTICAL deux fois). `git diff --quiet 65a6518c HEAD -- Alundra/ CasaEngineMonogame` sort 0 ; aucun `Directory.Build.*` à la racine ni dans `Alundra/` ; `cmp` de la DLL Debug (`Alundra/bin/Debug/net9.0-windows/Alundra.dll`) avec `alundra-project/Alundra.dll` : identique. Garde des traces : verte après chaque suite (`git diff --ignore-cr-at-eol --quiet` sur les traces ; fins de ligne remises par `git checkout`). `git log --follow` retrouve l'historique d'avant R5 de cinq fichiers de domaines différents (`Audio/BgmFadeDirectorTests.cs` 13 commits d'avant R5, `Maps/DestroyedEntityRecyclingTests.cs` 14, `Dialogues/ChoiceBoxMachineTests.cs` 7, `Rendering/Backdrops/BackdropCellularPushProductionTests.cs` 13, `Hero/AttackOracleRow.cs` 1) et des quatre fichiers des deux classes partielles (`Items/LootRollWorldTests.cs` 10 et `.Rolls.cs` 5, `Rng/RandomPerTickTests.cs` 5 et `.Advance.cs` 12). Le paragraphe de gel de `tools/dll-structure/README.md` (choix (a)) est écrit (« Frozen proofs (after R5) »). Les trois lignes T des documents sont corrigées : `docs/intro-roadmap.md:26` (`Alundra.Tests/Arcs/IntroTraceHarnessTests.cs`), `docs/intro-roadmap.md:63` (`Alundra.Tests/Events/EventProgramRunnerTests.cs`), `docs/formats/dialogues-yarn.md:123` (`YarnVariableCorpusTests`) ; les numéros de ligne des documents vivants restent le travail de R6b (D-R-20).
- **T23 ✅ (liste vide).** Sélection exacte du plan (enfants directs du dossier temporaire `C:\Users\casad\AppData\Local\Temp`, nom = l'un des anciens préfixes de la classe `temp-folder-prefix` de `r5-literals.tsv` — 14 préfixes distincts pour les 25 entrées —, suivi d'un GUID, dernière écriture, dossier compris, de plus de 24 heures) : le dossier compte 144 508 entrées, **aucune** ne commence par l'un de ces préfixes (les tests nettoient leurs dossiers ; les dossiers « Alundra… » restants, `AlundraBugReports`, `AlundraCasaEngine*Bake`, `AlundraCasaEngineConverterTests`, `alundra-conv-g1-export`, `alundra-font-tests`, ne suivent aucun de ces préfixes et ne sont pas visés). Une liste vide termine T23 : ni essai à blanc, ni envoi à la Corbeille, rien supprimé.
- **Écarts et suites.** (1) L'ordre d'exécution de xUnit n'a produit aucun échec dans le nouvel ordre (4 exécutions complètes vertes). (2) Les réponses de l'auteur (D-R-21) tranchent les points (1) et (2) de « À soumettre » ; (3) et (4) restent des constats. (3) Restent : la recette courte de l'auteur (nouvelle partie jusqu'au premier écran jouable, F5, F6, F9) et le titre du §9, que la session principale change après sa vérification.
- **À soumettre à l'auteur (rien n'est tranché à sa place).** (1) Les 77 fichiers dont le nom ne désigne ni un type ni un dossier de la DLL sont rangés par leur sujet (colonne `basis` = `subject` ou `refs`) : la règle « par le type de son nom » ne les tranche pas. (2) `Arcs/` compte 33 fichiers : les 26 scénarios et harnais, `HeadlessIntroSimulation` (coupé de `IntroTraceHarnessTests.cs`, donc dans son dossier, P-R5-1 (iv)) et six fichiers d'aide qui ne servent qu'aux scénarios (`ArcChecks`, `ArcSamples`, `ArcSnap`, `LeverArcs`, `LeverCell`, `LeverStampedSounds`), classés par leurs utilisateurs comme toute aide ; si « rien d'autre » (point 3) exclut ces six, ils vont dans `Support/`. (3) P-R5-1 (iii) donne à 19 fichiers d'aide le nom de leur premier type déclaré (par exemple `AlundraArcSupport.cs` devient `Support/ArcSpec.cs`, `AlundraDialogueSpeakerOracle.cs` devient `Support/SpeakerAnnex.cs`) : les noms de fichier ne disent plus ce qu'ils contiennent. (4) Les suites complètes durent près de 7 minutes (et non 4) : la marge sous les 10 minutes d'une commande est étroite pour T1 à T21.

**Vérification (2026-10-22, verifier neuf) : CONFIRMED.** Rejoué par lui : les 44 preuves `prove_r5.py` (move et code, 81 entrées L1) et
`prove_r5_nested.py` sur `968e468b` ; une comparaison indépendante des lignes de `Alundra.Tests` avant et après (seuls changent les
en-têtes `#nullable` des fichiers coupés, la forme courte `[GpuFact]`, les captures et qualifications relevées ; mêmes comptes de
`Fact`, `Theory`, `GpuFact`, `InlineData`, `MemberData`, un seul `Skip`) ; les listes `--list-tests` de Release et de Debug égales à
celles de T0d par les tables ; `git diff --quiet ce9c36f0 HEAD -- Alundra/ CasaEngineMonogame` à 0 ; la solution en Release (copie
neutralisée) puis en Debug sans erreur, `cmp` de la DLL Debug identique ; `Alundra.Tests` 5 097 / 0 en Release et en Debug ;
`check_names.py --tests` à 0 ; T23 : aucun des 144 669 enfants du dossier temporaire ne suit un ancien préfixe, rien n'a été envoyé ni
supprimé. Remarques P4 : les suites réécrivent les fins de ligne des traces de héros (contenu inchangé, remis par `git checkout`) ; la
modification locale de l'auteur dans `CasaEngine.Launcher/Program.cs` entre dans les constructions sans toucher au pointeur. Reste la
recette courte de l'auteur (nouvelle partie jusqu'au premier écran jouable, F5, F6, F9).

## 10. Tranche R6 — Documentation vivante 🧪 (faite et CONFIRMED le 2026-10-17, `7674ffa` à `2964d9a` ; révision 2 ; restent Q-R-4 et Q-R-5)

**Relecture (2026-10-17).** REVISE, trois blocages corrigés (FIX, révision 1) : (1) les lignes qui demandent du jugement ont leur texte
cible fixé ci-dessous (« Cibles fixées ») et l'exécutant devient un `executor` ; (2) les lignes F ont une tâche, un texte et un
contrôle ; (3) T0 nomme ses générateurs, ses modes et ses chemins. Seconde relecture : REVISE, un blocage P2 (le vérificateur de membres
lit `hits.tsv`, que seul `check/cscan.py` écrit, non nommé) : FIX en révision 2 (T0 (b) nomme `cscan.py`, l'ordre et le dossier, et remplace
`members.py` par `check/decl.py`, plus strict, avec sa sortie attendue ; T3 nomme `prove_research.py`) ; nouvelle époque, une seule relecture de clôture.

**But.** Les documents vivants, les scripts de `research/` et les commentaires du convertisseur qui citent un ancien chemin ou un ancien nom
de la DLL disent le chemin et le nom finaux de `tools/dll-structure/table.json` ; la règle de nommage des guides dit celle de l'ADR-0075 ;
les plans, leurs annexes (D-R-6) et les ADR ne sont pas réécrits.

**Faits (découverte du 2026-10-17, contre-vérifiée, `scratchpad/r6-disc/` : `occurrences.tsv`, 232 lignes, 13 colonnes ; le critique
confirme l'ensemble, aucune ligne manquante qui compte).** Périmètre : 185 fichiers suivis (docs hors `plan-*.md`, `plan-*-annexe/**` et
`decisions/` ; `research/**` ; le convertisseur et ses tests, commentaires seulement ; les README de `tools/` ; le README racine). Classes :
**A** chemin vers un source de la DLL (24 lignes : `docs/formats/save-game.md:3-6`, `cells-companion.md:288,304`, `sprite-records.md:5`,
`docs/intro-roadmap.md:29,101,227,325`, `research/census/README.md:46,65`, des commentaires du convertisseur) ; **B** nom de type en prose ou en
exemple (47 lignes, par exemple `docs/formats/effects.md:14,16`, `docs/editeur-couverture-dll.md:50-54`, `docs/stick-probe.md:45` — un message
de journal renommé par R3 : `StickProbe:` —, `docs/census-0x24-waits.md:17`, que `research/census/waits-0x24/report_v2.py:103` réécrit : script
et doc ensemble) ; **C** la règle de nommage, `docs/guidelines-runtime-alundra-casaengine.md` §5.1 (l. 191-200 : « `ElementFactory.Create<T>`
cherche par nom de type simple… Préfixer `Alundra…` »), fausse deux fois depuis R0 (résolution par nom complet, ADR-0075 du moteur) et R1
(ADR-0075 du parent) ; **D** histoire, gardée (41 : `docs/demarrage-nouvelle-partie.md`, daté du 2026-08-08 ; `rapport-analyse…md:201` ; les
traces `docs/*.txt`, lues par les gardes des tests ; les rapports datés de `research/census/` ; `tools/dll-structure/README.md`) ; **E**
scripts de `research/` qui ouvrent un source par chemin (10 lignes, `open(ROOT + "/Alundra/Scripts/X.cs")` : `AnimationTables.cs`,
`EventOpcodeSizeTable.cs`, `AlundraEventProgramRunner.cs`), qui lèvent `FileNotFoundError` aujourd'hui ; après la correction du chemin, leurs
trois lectures rendent des valeurs identiques à celles du source d'avant (sonde `prove_research.py`) ; **T** chemins ou classes de test
(3 lignes) : ils attendent R5. Le critique ajoute : trois citations qualifiées nomment un membre qui a changé de classe ou disparu
(`intro-roadmap.md:70,227` `AlundraWorldProxy.ShouldSpawnRecord` → `EntitySpawnFactory` ; `:37` `RunEntityEventsPass`, supprimé ; un autre
`ResolveLogicalPosition` → `EntitySpawnFactory`) : le membre de chaque citation qualifiée se vérifie avant de substituer ; des numéros de ligne
cités étaient déjà faux à la base : on ne corrige que le chemin et le nom ; deux commentaires `m.py:13` « lines 512..1447 ».

**Choix (session principale, mode autonome du 2026-10-17 ; réversibles, soumis à l'auteur en Q-R-4).** `docs/intro-roadmap.md` est vivant
(ses sections 0 à 3 se déclarent valides) : ses A et B se corrigent ; `docs/demarrage-nouvelle-partie.md`, daté, reste tel quel (D) ; les
numéros de ligne cités ne se recalculent pas ; les lignes T vont à R5 ; `docs/editeur-couverture-dll.md:64-66` (« `script_class_name` … aucun
sélecteur », faux depuis D-R-11) se corrige ; le commentaire du convertisseur `PlayerSetupWriter.cs:27` (un chemin `Alundra/Alundra.Tests/Scripts/
PlayerManager.cs` qui n'a jamais existé ; ses numéros de ligne sont ceux de la décompilation) vise le fichier de la décompilation
(`alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/PlayerManager.cs`). Hors de R6 : les ~300 anciens noms restés dans les
**commentaires** du code de la DLL et des tests (R3 et R4 laissent les commentaires intacts) : question Q-R-5.

**Cibles fixées (révision 1).** Ces lignes ne se substituent pas mécaniquement : leur texte final est celui des blocs ci-dessous, au
caractère près (indentation et repli de ligne compris, les lignes sources étant repliées comme dans le fichier). Chaque bloc « avant » est
le texte exact au HEAD `ee175f7` ; un « avant » qui ne correspond plus arrête la tranche.

1. `docs/guidelines-runtime-alundra-casaengine.md:138` (le membre est `internal static` de `EntitySpawnFactory`,
   `Alundra/Entities/Spawning/EntitySpawnFactory.cs:566`) :
   ```text
   avant : Alundra bruts, écrite par `AlundraWorldProxy.ResolveLogicalPosition` (les trois sites qui posaient
   après : Alundra bruts, écrite par `EntitySpawnFactory.ResolveLogicalPosition` (les trois sites qui posaient
   ```
2. `alundra-casaengine-project-converter/Writers/WorldWriter.cs:206` :
   ```text
   avant :     /// the DLL itself writes (<c>AlundraWorldProxy.ResolveLogicalPosition</c>) - a RenderProjectionComponent
   après :     /// the DLL itself writes (<c>EntitySpawnFactory.ResolveLogicalPosition</c>) - a RenderProjectionComponent
   ```
3. `docs/intro-roadmap.md:70` (`EntitySpawnFactory.cs:71,83,115`) :
   ```text
   avant :   0x2D/0x8B. Le harnais réutilise `AlundraWorldProxy.ShouldSpawnRecord`, qui porte déjà cette règle.
   après :   0x2D/0x8B. Le harnais réutilise `EntitySpawnFactory.ShouldSpawnRecord`, qui porte déjà cette règle.
   ```
4. `docs/intro-roadmap.md:227` (les anciens numéros `288-323` désignaient l'ancien fichier : ils tombent) :
   ```text
   avant :   `AlundraWorldProxy.ShouldSpawnRecord` (`Alundra/Scripts/AlundraWorldProxy.cs:288-323`) porte déjà ces
   après :   `EntitySpawnFactory.ShouldSpawnRecord` (`Alundra/Entities/Spawning/EntitySpawnFactory.cs`) porte déjà ces
   ```
5. `docs/intro-roadmap.md:37-38` (`RunEntityEventsPass` n'existe plus : `Alundra/Maps/WorldProxy.cs:2423` renvoie à
   `EntityScriptProxy.Update`, `Alundra/Entities/EntityScriptProxy.cs:1286`) :
   ```text
   avant :
   - la passe pick/run déjà portée, `AlundraWorldProxy.RunEntityEventsPass`, réutilisée **sans
     modification** ;
   après :
   - la passe pick/run déjà portée (alors `AlundraWorldProxy.RunEntityEventsPass`, retirée depuis : voir
     `EntityScriptProxy.Update`), réutilisée **sans modification** ;
   ```
6. `docs/guidelines-runtime-alundra-casaengine.md:192-200` (classe C). La ligne 192 `### 5.1 Résolution des classes par nom` devient
   `### 5.1 Résolution des classes par nom complet` ; les lignes 193 à 200 (vide, paragraphe de trois lignes, vide, paragraphe « → » de
   trois lignes) deviennent le bloc suivant ; la ligne 201 (vide) et `### 5.2` restent :
   ```text

   Le moteur crée un objet script (proxy d'entité ou de monde, contrôleur de joueur, mode de jeu, view model
   d'écran) d'après le **nom complet** de sa classe, namespace compris, casse exacte, et **seulement dans
   l'assembly de gameplay enregistrée** (ADR-0075 du moteur). Un nom vide, introuvable, ambigu ou qui désigne
   un type du mauvais genre lève `ScriptTypeResolutionException` à la création ; son message donne la clé, la
   valeur, le fichier, les assemblies cherchées et un indice. Dans l'aperçu de l'éditeur, l'entité ou le monde
   reste sans script et l'inspecteur affiche un avertissement sur sa ligne « Script ». Les composants (le
   `"type"` des JSON) restent résolus par nom simple, insensible à la casse (`ElementFactory.Create<T>`).

   → Les clés `script_class_name`, `player_controller_class` (vide = le `PlayerController` du moteur),
   `mode_class_name` et `view_model_type` portent le nom complet (`Alundra.Maps.WorldProxy`,
   `Alundra.Entities.EntityScriptProxy`, `Alundra.Hero.HeroController`, `Alundra.UI.Hud.HudViewModel`). Dans
   la DLL, un dossier = un namespace `Alundra.<Domaine>`, sans préfixe `Alundra` sur les types, un nom unique
   parmi la DLL, le moteur, MGUI, MonoGame, les paquets et la BCL (contrôlé par
   `tools/dll-structure/check_names.py`), aucun type nommé comme un segment de namespace (ADR-0075 du parent).
   ```
   Faits : `ElementFactory.cs:39-42` et `:51-57` du moteur `92d8e847` ; `ScriptTypeResolutionException.cs:5-11` ; les valeurs citées
   sont celles de `alundra-project` (`rg -uu`) ; `mode_class_name` : `GameplayModeAsset.cs:18,25`.
7. `docs/editeur-couverture-dll.md:64-66` (classe F ; état lu au moteur `92d8e847`, `CasaEngine.Editor/Controls/EntityDetailsPanel.cs` :
   `AddScriptRow` appelé pour le monde l. 655 et pour l'entité l. 859 ; `AddWorldModeRows` l. 982-1028 : « Game Mode » sur
   `PlayerStartupSettingsAssetId`, « Gameplay Mode » sur `GameplayModeAssetId` ; `space_policy` seulement sérialisé,
   `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs:41`). Les trois lignes deviennent ces cinq, la case reste `[ ]` :
   ```text
   - [ ] **`space_policy`** (monde) — préservé au save mais **aucun sélecteur**. Les trois autres clés de ce
     branchement ont leur ligne dans l'inspecteur (moteur `92d8e847`) : `script_class_name` (monde ET entité)
     la ligne « Script » (choix parmi les classes de la DLL, avertissement si le nom ne se résout pas ; D-R-11
     de `docs/plan-restructuration-dll.md`), `player_startup_settings_asset_id` la ligne « Game Mode »,
     `gameplay_mode_asset_id` la ligne « Gameplay Mode ».
   ```
8. `alundra-casaengine-project-converter/Writers/PlayerSetupWriter.cs:27-28` (classe F ; le chemin cité n'a jamais existé ; les numéros
   `PlayerManager.cs:413`, `:430`, `:549`, `:1904` sont ceux de la décompilation — Cross, Triangle, Square, Circle à ces lignes —, pas ceux de
   `Alundra/Hero/PlayerManager.cs`, dont les mêmes lignes sont des commentaires ou des accolades) :
   ```text
   avant :
   /// PSX to Xbox-pad button table (Alundra/Alundra.Tests/Scripts/PlayerManager.cs line references are to
   /// the gameplay DLL's own copy, which reproduces the original's control scheme):
   après :
   /// PSX to Xbox-pad button table (the PlayerManager.cs line references are to the decompilation,
   /// alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/PlayerManager.cs, which reproduces the
   /// original's control scheme):
   ```

**Propriétaire, chemins, budget.** Un `executor` (l'application est mécanique selon le relevé et les « Cibles fixées » ; le jugement borné
qui reste : vérifier le membre de chaque citation qualifiée du relevé). Chemins : les fichiers des classes A, B, C, E, F du relevé (docs vivants, `research/census/**`, commentaires du convertisseur et de
ses tests), `tools/dll-structure/r6-occurrences.tsv` et `tools/dll-structure/apply_r6.py` (pas `tools/dll-structure/README.md`, classe D),
ce plan (§10). Aucun code de la DLL ni des tests ; aucune donnée ; aucun export. Budget : une passe, une passe de correction au plus ; un
verifier frais.

**Tâches.**
- T0 : le relevé régénéré au HEAD par les générateurs de la découverte (dossier `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/r6-disc`), copiés dans deux
  dossiers de travail neufs de l'exécuteur (sans aucune sortie préexistante) et rejoués dans cet ordre :
  (a) `disc/scan.py` et `disc/classify.py` copiés ensemble ; `python -B scan.py` écrit `raw.json` à côté de lui, puis
  `python -B classify.py` lit ce `raw.json` et écrit `occurrences.tsv` et `classified.json` à côté de lui (les lignes D, F et
  `demarrage-nouvelle-partie.md:176` y sont codées à la main, `MANUAL`, `classify.py:239-247`) ;
  (b) `check/cscan.py` et `check/decl.py` copiés ensemble ; `python -B cscan.py` écrit `hits.tsv` et `sweep.json` à côté de lui, puis
  `python -B decl.py`, lancé avec ce dossier comme répertoire courant (il lit `./hits.tsv`), imprime `DECL` ou `NODECL` pour chaque
  citation qualifiée `AncienType.Membre` selon que le membre est **déclaré** dans le fichier final du type (plus strict que
  `check/members.py`, qui ne cherche que le nom et laisse passer un membre déplacé appelé depuis ce fichier). Attendu, rejoué le
  2026-10-17 au HEAD `ee175f7` : exactement cinq `NODECL`, les lignes des cibles 1 à 5 (`WorldWriter.cs:206`, guidelines `:138`,
  intro-roadmap `:37`, `:70`, `:227`) ; tout autre `NODECL` arrête la tranche ;
  la sortie de (a) diffère de `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/r6-disc/disc/occurrences.tsv` seulement par des lignes expliquées (un écart non expliqué
  arrête la tranche) ;
  versé dans `tools/dll-structure/r6-occurrences.tsv`, chaque ligne A, B, C, E, F avec une cible non vide et unique (les « Cibles fixées »
  ci-dessus, au caractère près, pour les lignes qu'elles nomment ; les cibles 5 à 8 remplacent une plage de lignes par un bloc : le relevé
  porte pour elles la plage, le bloc « avant » et le bloc « après ») ; `tools/dll-structure/apply_r6.py` : mode `apply`
  (remplace chaque ligne ou plage exactement par sa cible ; refuse une ligne ou une plage dont le texte a changé depuis le relevé) et mode `--check` (aucune ligne
  A, B, C, E, F ne garde son ancien texte ; aucun fichier hors des chemins de la tranche n'a changé depuis la base ; plans, annexes, ADR et
  traces `docs/*.txt` intacts) ; un commit d'outil.
- T1 — Classes A, B et F (docs, `research/`, commentaires du convertisseur et de ses tests), un commit par zone (docs, `research/`,
  convertisseur), selon le relevé et les « Cibles fixées » ; les citations qualifiées dont le membre a changé de classe pointent vers sa classe réelle ; une citation d'un membre supprimé
  devient la mention de sa suppression ou se retire de la phrase, sans inventer.
- T2 — Classe C : `docs/guidelines-runtime-alundra-casaengine.md` §5.1 remplacée par le texte des « Cibles fixées », qui couvre : la résolution des objets script par **nom complet**, casse
  exacte, dans l'assembly de gameplay seule (ADR-0075 du moteur ; les composants restent résolus par nom simple, `WorldWriter.cs:487`) ; les
  clés `script_class_name`, `player_controller_class` (vide = le `PlayerController` du moteur), `mode_class_name`, `view_model_type` ; les noms
  de la DLL : un dossier = un namespace `Alundra.<Domaine>`, plus de préfixe, un nom unique contrôlé par `tools/dll-structure/check_names.py`,
  aucun type nommé comme un segment (ADR-0075 du parent).
- T3 — Classe E : les dix ouvertures de source de `research/census/` vers les chemins finaux, les deux commentaires « lines 512..1447 » ;
  preuve par `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/r6-disc/disc/prove_research.py` (copié, `python -B`, lecture seule : il lit l'ancien source par `git show 5434cf6:<chemin>`
  et le nouveau dans l'arbre) : les trois lectures (`EventOpcodeSizeTable`, les tableaux d'`AnimationTables`, les `case` du `Dispatch`) rendent les mêmes valeurs que
  sur le source de `5434cf6` ; `report_v2.py` régénère `docs/census-0x24-waits.md` à l'identique hors des noms (s'il le peut sans
  `ALUN_CD.EXE` ni `capstone` : sinon le doc se corrige à la main et la régénération est notée non faite).
- T4 — Clôture : `apply_r6.py --check` : aucune ligne A, B, C, E, F du relevé ne garde son ancien texte ; `prove_r4.py --kind end`
  sort 0 (le commentaire `SpriteWriter.cs:92` corrigé ferme le P3 de R4) ; aucun fichier hors des
  chemins de la tranche n'a changé ; les plans, annexes, ADR et traces intacts (`git diff --stat`) ; la suite du convertisseur verte (seuls
  des commentaires ont changé : la construction suffit, la suite le confirme) ; ce plan (§10 et résultats).

**Acceptation.** Les preuves de T3 et T4 ; aucune ligne de code changée hors des chemins de `research/` ; le diff des commentaires du
convertisseur limité aux lignes du relevé.

**Arrêts.** Une citation dont la cible n'est pas établie ; une ligne du relevé changée depuis le relevé ; un fichier gelé (plan, annexe, ADR,
trace) touché.

**Résultats (2026-10-17, exécution par un `executor`).** T0 : générateurs rejoués au HEAD `211a757` dans deux dossiers neufs ; (a) `occurrences.tsv` identique octet pour octet à celui de la découverte (232 lignes, aucun écart à expliquer) ; (b) `decl.py` : 28 `DECL`, exactement les cinq `NODECL` attendus (cibles 1 à 5), aucun autre. Le relevé versé `tools/dll-structure/r6-occurrences.tsv` compte 74 lignes (par ligne ou plage : 41 B, 18 A, 12 E, 2 F, 1 C ; les 84 occurrences du relevé de découverte groupées par ligne, plus les deux commentaires « lines 512..1447 » en classe E), cibles fixées 1 à 8 au caractère près. `apply_r6.py` (modes `apply` et `--check`) : `apply` appliqué une fois (74 lignes, 35 fichiers), `--check` sort 0, `prove_r4.py --kind end` sort 0 (le P3 `SpriteWriter.cs:92` est fermé). T3 : `prove_research.py` rend `identical=True` pour les trois lectures (`EventOpcodeSizeTable` 198 entrées, `AnimationTables` 3 tableaux, `Dispatch` 158 cases) ; `report_v2.py` ne peut pas régénérer `docs/census-0x24-waits.md` (il lit `census_v2.pkl`, absent, qui demande `gstatic.pkl`, `census.pkl` et une exécution complète du recensement, non versionnés) : le doc est corrigé à la main, avec la même phrase que `report_v2.py:103` ; régénération notée non faite. Suite du convertisseur : 854 réussies, 0 échec. Jugements : les deux commentaires « lines 512..1447 » (déjà faux à la base) deviennent « (parsed from the switch of Dispatch) » ; `WorldWriter.cs:376` « (Alundra/Scripts/) » → « (Alundra/Maps/) » et `research/census/README.md:46` → `Alundra/Events/` (les sources cités sont dans ces dossiers). Écart au plan : T2 (classe C) est dans le commit des docs, la même zone que T1 docs. Reste (🧪) : un verifier frais sur l'ensemble, les réponses de l'auteur à Q-R-4 et Q-R-5, les lignes T (chemins de tests) qui attendent R5.

**Vérification de R6 (2026-10-17) : REFUTED** par un verifier frais, sur un seul constat (**P2**, introduit, borné) : la cible fixée 2
n'était pas appliquée au caractère près, `alundra-casaengine-project-converter/Writers/WorldWriter.cs:206` portait 8 espaces devant `///`
au lieu de 4 (la cellule « after » du relevé aussi, d'où un `--check` vert). Tout le reste tient : le rejeu du relevé depuis `211a757`
rend les 35 fichiers à l'octet ; seules les lignes du relevé changent ; les 84 occurrences A, B, C, E, F couvertes ; aucun fichier hors
de la tranche ; `prove_r4.py --kind end` vert ; convertisseur 854 / 0 ; `prove_research.py` identique ; la ligne corrigée à la main de
`docs/census-0x24-waits.md` égale à la chaîne de `report_v2.py:103` ; les textes nouveaux exacts au moteur `92d8e847` et à la
décompilation ; mutations de `apply_r6.py` (ancien texte remis, nouveau texte changé, fichier gelé ou hors tranche, ligne changée avant
`apply`, second `apply`) toutes refusées. Écarts de l'exécuteur jugés corrects. Avis non bloquants (**P4**) : `--check` autorise tout ce
plan et ne contrôle pas les lignes hors relevé des 35 fichiers (le verifier l'a fait à part) ; `research/census/README.md:46` ne nomme pas
`Alundra/Entities/AnimationTables.cs`, que trois scripts lisent aussi (déjà le cas à la base). **FIX** (session principale) : la ligne et la
cellule ramenées à 4 espaces ; `apply_r6.py --check` 0, `prove_r4.py --kind end` 0, convertisseur 854 / 0 ; un verifier frais sur l'état
corrigé. Les dates « 2026-10-18 » de ce plan (R4, R6, Q-R-4, Q-R-5) étaient fausses : tout s'est passé le 2026-10-17 ; corrigées.

**Seconde vérification de R6 (2026-10-17) : CONFIRMED** par un verifier frais (`211a757` à `2964d9a`) : les 8 cibles fixées au
caractère près, indentation comprise (la cible 2 à 4 espaces) ; les 35 fichiers reconstruits depuis `211a757` et le relevé, égaux à
l'octet, fins de ligne comprises ; `apply_r6.py --check` 0 ; rien hors de la tranche (dans ce plan, hors du §10, les seules
substitutions de date) ; `prove_r4.py --kind end` 0 ; convertisseur 854 / 0 ; le paragraphe de vérification exact. Avis **P4** : la suite
a tourné avec les modifications locales des sous-modules — **REJECT** : ce sont `CasaEngine.Launcher/Program.cs` (le chemin local de
l'auteur, hors des projets de test) et, dans `alundra-datas-analyser/`, le pointeur `MGUI` et `AlundraTools/log.txt` (l'analyseur
n'est pas dans le graphe de construction du convertisseur) : sans effet. Les deux avis P4 de la première vérification restent notés
(DEFER, sans effet sur la tranche).

**Retour arrière.** `git revert` des commits de la tranche.

## 11. Questions à l'auteur

- **Q-R-3** — **Tranchée le 2026-10-22 (D-R-13)** ; trois précisions en Q-R-3b. Question d'origine (2026-10-17 ; bloquait R5 ; découverte `scratchpad/r5-disc/`, contre-vérifiée) — Le rangement des tests demande des choix de
  structure que la règle « les tests suivent la même arborescence, sans préfixe » ne tranche pas. En termes concrets :
  1. **Nom ou sujet.** 79 fichiers de tests (593 tests) portent le nom d'un type mais exercent surtout d'autres domaines (par exemple les
     15 `AlundraWorldProxy*Tests`, qui testent la caméra, l'audio, les apparitions). Les ranger par le type de leur nom (simple, prévisible)
     ou par le domaine qu'ils exercent le plus (plus juste, mais sujet à discussion fichier par fichier) ?
  2. **Les noms qui ont changé dans la DLL.** Retirer seulement le préfixe (`AlundraMusicPlayerTests` → `MusicPlayerTests`, alors que le type
     s'appelle `BgmPlayer`) ou suivre le nom du type (`BgmPlayerTests`, `GameRandomTests`, `SfxBankTests`, `HeroControllerTests`) ?
  3. **`Arcs/`.** Seulement les 26 scénarios de bout en bout et les harnais de traces, ou aussi les ~40 fichiers qui pilotent `ArcRun` dans un
     domaine (19 en `Entities`, 6 en `Hero`…) ?
  4. **Types partagés déclarés dans un fichier de test** (les collections xUnit de la musique, utilisée par 98 fichiers, et du générateur ;
     des hôtes d'aides comme `AlundraWorldProxyGlobalFreezeTests`) : les sortir dans `Support/` (une scission, pas un simple déplacement) ou
     les laisser et ajouter les `using` (98 `using Alundra.Tests.Audio`) ?
  5. **Un type principal par fichier** pour les 44 fichiers de tests à plusieurs types (15 n'auront plus de type au nom du fichier) :
     scinder comme la DLL (règle (3)) ou garder les fichiers entiers ?
  6. `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` : rester à sa place (zéro risque) ou suivre l'audit dans `Events/` ?
  7. Les gardes d'export sans type de la DLL (`CornerTrack`, `ZeroDurationLoop`) : `Entities` ou un dossier dédié ?
  En attendant, R5 n'est pas exécutée ; sa découverte sera regénérée après R4 (les numéros de ligne changent).
  **Réponse de l'auteur (2026-10-22)** : « 1. Les ranger par le type de leur nom 2. Retirer seulement le préfixe 3. Seulement les 26
  scénarios 4. les laisser 5. Un type principal par fichier 6. rester à sa place 7. dossier dédié ».
- **Q-R-3b** — **Tranchée le 2026-10-22 (D-R-16, D-R-19)**. Question d'origine (2026-10-22 ; bloquait T0a de R5) — Trois précisions sur les réponses à Q-R-3 :
  (a) **points 4 et 5 ensemble.** Les types partagés (les 5 collections xUnit, dont celle de la musique déclarée dans
  `AlundraMusicPlayerTests.cs`, et les hôtes d'aides) sont des types secondaires de fichiers à plusieurs types : le point 5 les scinde dans
  leur propre fichier, le point 4 les laisse. Lecture proposée : chacun dans son propre fichier, dans le dossier de son fichier d'origine
  (par exemple `Audio/`), pas dans `Support/`. Ou bien les garder dans leur fichier d'origine (une exception au point 5) ?
  (b) **point 7** : le nom du dossier dédié. Proposé : `ExportGuards/` (namespace `Alundra.Tests.ExportGuards`), d'après les noms des deux
  fichiers (`AlundraCornerTrackExportGuardTests.cs`, `AlundraZeroDurationLoopExportGuardTests.cs`).
  (c) **point 3** : « seulement les 26 scénarios » comprend-il les harnais de traces, que l'option de la question y mettait aussi ?
  **Réponse de l'auteur (2026-10-22, 2e tour)** : « chacun dans son propre fichier » : (a) tranché (D-R-16). **(b) et (c) restent sans
  réponse.** **3e tour** : « b oui, c oui » : `ExportGuards/`, et les harnais de traces dans `Arcs/` (D-R-19).
- **Q-R-4** — **Tranchée sous réserve de Q-R-4b** (2026-10-22, « 1 », D-R-17). Question d'origine (2026-10-17, choix faits en mode autonome pour R6, réversibles) — Valider ou corriger : `docs/intro-roadmap.md` traité comme
  vivant (ses chemins et noms corrigés) ; `docs/demarrage-nouvelle-partie.md` (daté du 2026-08-08) gardé tel quel ; les numéros de ligne
  cités dans les docs non recalculés (seuls le chemin et le nom changent) ; `docs/editeur-couverture-dll.md:64-66` corrigé (la ligne
  « Script » existe de nouveau) ; le commentaire `PlayerSetupWriter.cs:27` pointé vers la décompilation.
  **Réponse de l'auteur (2026-10-22)** : « à corriger ». **Précision demandée** : lesquels des cinq choix corriger, et comment ? (1)
  `docs/intro-roadmap.md` traité comme vivant ; (2) `docs/demarrage-nouvelle-partie.md` gardé tel quel ; (3) les numéros de ligne des
  docs non recalculés ; (4) la correction de `docs/editeur-couverture-dll.md:64-66` ; (5) le commentaire `PlayerSetupWriter.cs:27`.
  R6 est faite : la correction sera une petite reprise de R6.
- **Q-R-4b** — **Tranchée le 2026-10-22 (D-R-17)** : « corrige les documents », portée en Q-R-4c. Question d'origine (2026-10-22) — Ta réponse « 1 » à Q-R-4 : lecture proposée : `docs/intro-roadmap.md` (la feuille de route du
  2026-08-23 pour l'intro, chantier clos) redevient un **document historique** : les chemins et noms que R6 y a corrigés (commit
  `542e62c9`, 9 lignes) reviennent aux anciens, et R5 ne touche plus ses lignes 26 et 63 (tranche R6b). Ou voulais-tu autre chose pour
  ce fichier (par exemple corriger aussi ses numéros de ligne) ? Recommandé : la lecture proposée.
  **Réponse de l'auteur (2026-10-22, 3e tour)** : « corrige les documents » : la lecture proposée n'est pas retenue, rien n'est annulé ;
  `docs/intro-roadmap.md` reste vivant et se corrige (R6b). La portée : Q-R-4c.
- **Q-R-4c** — **Tranchée le 2026-10-22 (D-R-20)** : « tous les documents vivants ». Question d'origine (2026-10-22 ; bloquait la relecture de R5 et le détail de R6b) — « Corrige les documents » : est-ce `docs/intro-roadmap.md`
  seul, corrigé entièrement (ses chemins, ses noms et aussi ses numéros de ligne, que R6 n'a pas recalculés), ou **tous les documents
  vivants** (le choix (3) de Q-R-4 : les numéros de ligne cités dans les docs, non recalculés par R6, le seraient partout) ?
- **Q-R-5** — **Tranchée le 2026-10-22 (D-R-14)** : « corriger », dans une tranche R8. Question d'origine (2026-10-17) — Les renommages de R3 et R4 n'ont pas touché les **commentaires** du code : environ 300 anciens noms
  (`AlundraWorldProxy`, `AlundraEventProgramRunner`…) restent dans les commentaires de la DLL (≈ 60 fichiers) et des tests (≈ 90 fichiers),
  plus quelques chemins `Alundra/Scripts/…` (par exemple `HeroController.cs:42`, `AlundraScreenFadeCameraWiringTests.cs:24,29,96`). Les
  corriger dans une tranche dédiée (mécanique, outillée comme R3, commentaires seulement), ou les laisser ?
- **Q-R-6** — **Tranchée le 2026-10-22 (D-R-18)** : « c ». Question d'origine (2026-10-17, choix faits en mode autonome pour R5, réversibles) — Valider ou corriger : les preuves de fin de R3 et R4
  (`prove_r3.py --all`, `r3_literals.py check --stage after`, `prove_r4.py --kind end`), qui lisent des chemins de `Alundra.Tests/`, sont
  gelées à leur commit de clôture (`a193671`, `ce6d335`) plutôt que remappées après R5 ; les commentaires du code ne changent pas en R5
  (comme en R3 et R4, voir Q-R-5) ; les dossiers temporaires des anciennes exécutions de tests, nommés par les anciens préfixes, restent
  orphelins sur la machine.
  **Réponse de l'auteur (2026-10-22)** : « corriger ». **Précision demandée** : lequel des trois choix ? (a) remapper les preuves de fin
  de R3 et R4 sur les chemins de R5 au lieu de les geler ; (b) corriger les commentaires dès R5, au lieu d'attendre R8 (D-R-14) ; (c)
  effacer les dossiers temporaires orphelins des anciennes exécutions (sur la machine, hors du dépôt).
- **Q-R-7** — **Tranchée le 2026-10-22 (D-R-15)** : « une petite tranche R7 ». Proposition d'origine (2026-10-18) — Après le chantier, une tranche qui **ajoute un fichier** à la DLL fait échouer
  `check_names.py --tree --stage final` (`[hors table]`, `tools/dll-structure/check_names.py:96-98`), et `table.json` est générée sur la
  base `5434cf6` (« do not edit ») ; l'ADR-0075 ne règle ce cas que pendant le chantier. Proposé : une petite tranche R7, outil seul, après
  R5 : un mode `check_names.py --rules` qui contrôle l'arbre vivant de la DLL et des tests contre les règles de l'ADR-0075 (un dossier = un
  namespace, aucun préfixe `Alundra`, un nom unique parmi la DLL, le moteur, MGUI, MonoGame, les paquets et la BCL, aucun type au nom d'un
  segment), sans la table, et qui devient la garde des tranches suivantes ; la table reste l'archive du chantier. Valider ou corriger.
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

## 12. Tranche R7 — La garde des règles : `check_names.py --rules` ✅ (faite et CONFIRMED le 2026-10-22, `354982f3` à `08aff8e7`, ADR-0081 ; READY à la première relecture ; mode AUTO, D-R-22)

**But.** Une garde permanente, **sans table**, de l'arbre vivant de la DLL (`Alundra/`) et des tests (`Alundra.Tests/`) contre les règles
de l'ADR-0075 et de l'ADR-0080, qui prend le relais des modes à table quand le chantier est fini (D-R-15) : une tranche qui ajoute un
fichier conforme passe, un nom ou un dossier hors des règles est refusé.

**Faits (découverte du 2026-10-22 à `7a81a3be`, lecture seule, `scratchpad/r7-disc/`).**
- `tools/dll-structure/check_names.py` (211 lignes) a trois modes exclusifs : `--table`, `--tree --stage r2|r3|final [--root R]`, `--tests`
  (`:183-186`) ; sortie « N finding(s), M warning(s) », code 1 sur un constat, 2 sur une erreur d'environnement (`:200-207`). Les modes à
  table refusent **tout fichier neuf**, même conforme (`[hors table]`) ; en `--tests`, un namespace faux sort en `[nom]`, pas en
  `[namespace]` (la colonne de la table est lue, pas la déclaration).
- Fonctions réutilisables : `collision_findings` (`check_names.py:30-50`), `scan_types.parse` (`scan_types.py:107-141`), `dll_decls`
  (`:168-180`), `external_types` (`:213-280` : 6 projets du moteur, 5 de MGUI, 3 dossiers de NvgSharp, 20 paquets lus dans leur XML,
  3 sans XML, la BCL `Microsoft.NETCore.App.Ref/9.0.*`). `parse` et `collision_findings` sont importés par des outils de preuve gelés
  (`prove_r2.py:197`, `r5_lib.py:330`, `:368-372`, `r5-discovery/`) : les changer changerait ces preuves.
- Un prototype en lecture seule des règles (1), (2), (3), (4) avec segments et doublons rend, sur l'arbre vivant (DLL 207 fichiers,
  220 déclarations de premier niveau, 34 imbriquées ; tests 411, 432, 321), **0 constat et 1 avertissement** : le type imbriqué `Session`
  (`Alundra.Tests/UI/Shared/TextBoxGpu.cs:266`), nommé comme un segment. Le lecteur est recoupé par la table `TypeDef` des DLL Debug :
  identique (seul écart, `AutoGeneratedProgram`, généré par le SDK de test). 35 cas de mutation sur des copies jetables : 0 cas inattendu.
- Trous de la règle (4) pour les tests : 13 paquets vus par `Alundra.Tests` ne sont pas dans `PACKAGES` (dont `xunit.assert`) et le
  projet `CasaEngine.Compiler` manque aux projets du moteur : un type `Assert` déclaré dans les tests passerait. Limites du lecteur : une
  déclaration qui n'est pas en début de ligne est invisible (`DECL_RE` ancrée, `scan_types.py:102`) ; les littéraux bruts `"""` ne sont
  pas masqués (`STRIP_RE`, `:93` ; 13 fichiers de tests en ont, sans effet aujourd'hui).
- Aucun CI ni hook ne lance `check_names.py` : la garde n'existe que par les plans. E19.sm0 (`docs/plan-e19-opcodes.md`, §1.4.1, prérequis,
  acceptation, arrêts) et 25 lignes du plan d'E20 la citent comme prérequis (« Q-R-7 », « la fin du chantier »).

**Décisions de la session (dans le cadre de D-R-15, de l'ADR-0075 et de l'ADR-0080 ; l'auteur peut les contester).**
- **P-R7-1 — Les règles.** (1) Un dossier = un namespace : racines `Alundra` et `Alundra.Tests`, segments séparés par des points, un seul
  `namespace` par fichier, un segment de dossier qui est un identifiant ; un fichier sans type (par exemple
  `Alundra/Properties/InternalsVisibleTo.cs`) se saute. (2) Aucun préfixe `Alundra` ni `IAlundra` sur un nom de type, imbriqués compris,
  dans les deux arbres, ni sur un nom de fichier ou de dossier. (3) Un type principal par fichier, la lecture de l'ADR-0075 : le nom du
  fichier, sans suffixe `.Partie`, est celui d'un type de premier niveau ; tout type public de premier niveau porte le nom du fichier ;
  l'exception écrite par l'ADR-0075 (`Alundra/Events/IEventProgramRunner.cs` et `NoOpEventProgramRunner`) et les parties de classe
  partielle `Nom.Partie.cs` sont admises. (4) Unicité : aucun nom de premier niveau égal à un type externe (moteur, MGUI, NvgSharp,
  paquets, BCL) ni à un autre nom de premier niveau de la DLL ou des tests (les parties d'un même type partiel exceptées) ; aucun type
  nommé comme un segment, aucun segment qui masque un type externe ; les noms de dossiers que l'ADR-0075 écarte (`World`, `Player`,
  `Camera`, `Dialogue`, `TextBox`, `Text`, `Debug`, `Math`, `Entity`, `SaveScreen`) sont refusés en second filet. (5) Le miroir de
  l'ADR-0080 : tout dossier de tests a son pendant dans la DLL, sauf `Arcs`, `Support` et `ExportGuards`. La règle (3) et le miroir ne
  sont pas dans la liste de Q-R-7 mais sont des règles des ADR que l'auteur a validés ; ils rendent 0 constat aujourd'hui.
- **P-R7-2 — Ce qui reste hors règle.** Un dossier neuf est permis (pas de liste fermée de domaines : ce serait une table) ; un type
  imbriqué nommé comme un segment reste un **avertissement** (ADR-0075) ; les collisions des types imbriqués et les noms de membres
  (par exemple les méthodes de test `AlundraSoundBank_GetOrCreate_…`) ne sont pas contrôlés ; l'existence des 9 types cités par les
  données, `tools/AlundraTestSaves/` et les fichiers qui ne sont pas des `.cs` (par exemple sous `Alundra.Tests/Data/`) sont hors de la
  garde.
- **P-R7-3 — Lecture.** L'arbre de travail, fichiers non suivis compris (un fichier neuf se contrôle avant son commit), `bin/`, `obj/` et
  `artifacts/` exclus ; `--root <dossier>` lit `Alundra/` et `Alundra.Tests/` sous ce dossier (pour les copies de preuve).
- **P-R7-4 — Un module neuf, rien de gelé ne change.** Les règles vivent dans `tools/dll-structure/rules.py`, appelé par le mode
  `check_names.py --rules` ; `scan_types.parse`, `strip`, `external_types` et `collision_findings` ne changent pas (les preuves gelées les
  importent). `rules.py` ajoute : le masquage des littéraux bruts `"""` avant la lecture ; un constat `[lecture]` quand les accolades ne
  s'équilibrent pas après masquage ou quand une déclaration de type apparaît ailleurs qu'en début de ligne ; la couverture complète des
  tests pour la règle (4) (les 13 paquets et le projet `CasaEngine.Compiler`, ajoutés dans `rules.py`, en plus d'`external_types`).
- **P-R7-5 — Sortie.** Les étiquettes existantes, plus `[doublon]`, `[dossier]`, `[lecture]`, `[principal]` et `[miroir]` ; un
  avertissement ne change pas le code de sortie (0) ; un sous-module, un paquet ou la BCL absents : code 2, écrit au README.
- **P-R7-6 — Le sort des modes à table.** `--table`, `--tree`, `--tests`, `selftest.py` et les preuves `prove_r2.py` à `prove_r5.py`
  deviennent l'**archive du chantier** : ils se rejouent à leurs commits de clôture, le README le dit (son paragraphe « Frozen proofs »
  s'étend) ; à partir de R7, « le contrôle des collisions vert » de la règle du programme (§3) et des tranches suivantes est
  `check_names.py --rules` à 0.
- **P-R7-7 — Preuve versée.** `tools/dll-structure/selftest_rules.py <dossier hors dépôt>` rejoue sur des copies jetables (`git init` hors
  du dépôt) les cas de mutation de la découverte : refusés avec leur étiquette (namespace faux dans la DLL, dans un test, dans
  `Plugin.cs`, type global, deux `namespace` dans un fichier, `.g.cs` hors `obj/`, dossier non identifiant, dossier `World`, préfixe sur un
  type, sur un imbriqué, sur un fichier, sur une classe de test, collision `Vector2`, `Random`, `Assert` de xunit, `YarnDialogueCompiler`,
  doublon dans la DLL, test nommé comme un type de la DLL, partiel sur deux namespaces, type nommé `Audio`, accolade orpheline,
  déclaration hors début de ligne, secondaire public dans un fichier, dossier de tests sans pendant) ; acceptés (fichier neuf conforme
  dans un dossier existant ou neuf, partiel sur deux fichiers d'un namespace, namespace en bloc, fichier racine des tests en
  `Alundra.Tests`, `.cs` sous `obj/`, littéral brut qui contient une ligne de déclaration). S'y ajoute, en T1, le recoupement du lecteur
  par la table `TypeDef` des DLL Debug construites (une fois, résultat consigné ici).
- **P-R7-8 — Documents.** Le README de `tools/dll-structure` (en-tête des scripts qui écrivent, ligne d'`apply_r6.py`, ligne `--rules`,
  étiquettes, limites, archive) ; un ADR neuf, ADR-0081 (la garde par règles après le chantier ; il complète l'ADR-0075, dont la phrase
  sur les fichiers ajoutés pendant le chantier reste vraie pour le chantier) ; `docs/guidelines-runtime-alundra-casaengine.md:207` cite
  `--rules`. Les plans d'E19 et d'E20 se mettent à jour à leurs révisions (pas dans R7).

**Périmètre.** `tools/dll-structure/` (`check_names.py` : le mode `--rules` et son aiguillage seulement ; `rules.py` et
`selftest_rules.py` neufs ; `README.md`), `docs/decisions/0081-…` et l'index, la ligne 207 des guidelines, ce plan (§12 et résultats).
**Hors périmètre** : tout fichier de `Alundra/`, `Alundra.Tests/`, du convertisseur, des données, des sous-modules ; les fonctions
existantes de `scan_types.py` et de `check_names.py`.

**Propriétaire, budget.** Un `executor` ; une passe d'exécution, une de correction au plus ; un verifier frais.

**Tâches.**
- T1 — `rules.py`, le mode `--rules` ; sur l'arbre vivant : 0 constat, l'avertissement `Session` seul, sortie consignée ; le recoupement
  par la table `TypeDef` des DLL Debug (la solution construite en Release avec la copie neutralisée, puis en Debug en dernier) ; commit
  d'outil.
- T2 — `selftest_rules.py` et ses cas (P-R7-7), tous comme attendu ; `git diff` vide sur les fonctions existantes de `scan_types.py` et
  de `check_names.py` (seuls l'aiguillage et l'appel du mode neuf s'ajoutent) ; commit d'outil.
- T3 — Le README, l'ADR-0081 et son index, la ligne des guidelines ; `Alundra.Tests` en Release, une exécution, 5 097 / 0 (aucune source
  compilée ne change : la suite le confirme) ; `git diff --quiet <base> HEAD -- Alundra/ Alundra.Tests/ CasaEngineMonogame
  alundra-datas-analyser` ; ce plan (résultats) ; commit de documents.

**Acceptation.** `check_names.py --rules` rend 0 sur l'arbre vivant (l'avertissement `Session` listé) ; `selftest_rules.py` : chaque cas
comme attendu ; le lecteur égal à la table `TypeDef` ; les fonctions existantes inchangées ; l'ADR-0081 et le README.

**Arrêts.** Un constat sur l'arbre vivant (une vraie violation des règles : question, pas de correction dans R7) ; un cas de mutation qui
ne rend pas son étiquette ; un écart entre le lecteur et la table `TypeDef` ; un changement d'une fonction gelée ; un sous-module, un
paquet ou la BCL absents.

**Retour arrière.** `git revert` des commits de la tranche.

### Résultats de l'exécution (2026-10-22)

Mode AUTO (D-R-22), un `executor`, trois commits d'outil et de documents : T1 `354982f3` (`rules.py`, le mode `--rules`, `rules-extra-types.tsv`, `gen_extra_types.py`, `dump_public_types.ps1`, `dump_typedefs.ps1`, `cmp_typedef.py`), T2 `19a946c6` (`selftest_rules.py`, lecture du fichier de types externes tolérante aux fins de ligne CRLF), T3 le commit de documents (README, ADR-0081 et son index, ligne 207 des guidelines, ce paragraphe).
- **Arbre vivant.** `python -B tools/dll-structure/check_names.py --rules` : `rules: 207 DLL files, 411 test files, 652 top-level types, 355 nested types`, **0 constat, 1 avertissement** (le type imbriqué `Session`, `Alundra.Tests/UI/Shared/TextBoxGpu.cs:266`), code de sortie 0. Les 652 types de premier niveau = 220 (DLL) + 432 (tests) ; 355 imbriqués = 34 + 321, comme à la découverte.
- **Couverture de la règle (4).** `gen_extra_types.py` lit la table `TypeDef` des assemblies de compilation des 13 paquets absents de `PACKAGES` : 1 568 types publics de premier niveau (`rules-extra-types.tsv`), plus les sources de `CasaEngine.Compiler` au HEAD du moteur. Aucune collision sur l'arbre vivant.
- **`selftest_rules.py`.** 39 cas, **0 inattendu** : 13 acceptés (base vivante, fichier neuf conforme dans un dossier existant, dossier neuf de la DLL, fichier neuf de tests, dossier neuf avec son pendant de tests, fichier neuf sous `Arcs/`, partiel sur deux fichiers, namespace en bloc, fichier racine des tests, `.cs` sous `obj/`, littéral brut qui contient une ligne de déclaration, littéral brut à accolade non équilibrée, type imbriqué nommé comme un segment : avertissement seul) ; 26 refusés avec leur étiquette (namespace faux dans la DLL, dans un test, dans `Plugin.cs`, type global, deux `namespace` dans un fichier, `.g.cs` hors `obj/` : `[namespace]` ; dossier non identifiant, dossier `World` : `[dossier]` ; préfixe sur un type, un imbriqué, un fichier, une classe de test : `[nom]` ; `Vector2`, `Random`, `Assert` de xunit, `YarnDialogueCompiler` : `[collision]` ; doublon dans la DLL, test nommé comme un type de la DLL, partiel sur deux namespaces : `[doublon]` ; type nommé `Audio` : `[segment]` ; accolade orpheline, déclaration hors début de ligne, déclaration après la ligne `namespace` : `[lecture]` ; secondaire public, fichier dont aucun type ne porte le nom : `[principal]` ; dossier de tests sans pendant : `[miroir]`). Deux exécutions de la ligne de commande réelle : sortie 0 (arbre conforme) et 1 (namespace faux).
- **Recoupement `TypeDef`** (`cmp_typedef.py`, une fois en Release avec la copie neutralisée, une fois en Debug, la construction Debug en dernier) : DLL, 220 déclarations de premier niveau lues contre 219 types `TypeDef` distincts non générés (un type partiel déclaré deux fois) et 34 imbriqués contre 34 ; tests, 432 lues (430 distinctes) contre 431 `TypeDef` (430 + `AutoGeneratedProgram`, généré par le SDK de test, seul écart toléré) et 321 imbriqués contre 321. **Égal**, aucun type lu en trop ni manquant.
- **Fonctions gelées.** `git diff 53b689bd -- tools/dll-structure/scan_types.py` vide ; `check_names.py` : trois hunks (docstring, l'argument `--rules`, l'aiguillage dans `main`), aucune des fonctions existantes ne change.
- **Sources.** `git diff --quiet 53b689bd HEAD -- Alundra/ Alundra.Tests/ CasaEngineMonogame alundra-datas-analyser` : vide. `Alundra.Tests` en Release (copie neutralisée), une exécution au premier plan : **5 097 réussies, 0 échec, 0 ignoré** (5 min 53). Les quatre traces `docs/hero-trace-389-*.txt` sont ressorties modifiées pour les seules fins de ligne (`git diff --ignore-cr-at-eol` vide) et ont été remises par `git checkout`.
- **Décisions de l'exécution.** (a) Les types des 13 paquets sont versés en instantané (`rules-extra-types.tsv`, à régénérer quand un paquet du projet de tests change) plutôt que lus à chaque exécution : `pwsh` ne devient pas une dépendance de la garde. (b) La règle (3) lit `Nom.Partie.cs` comme une partie de classe partielle : le type qui porte le nom doit être `partial` (un constat `[principal]` sinon). (c) `--root` passe par `git ls-files` quand le dossier est un dépôt, par un parcours du dossier sinon.
- **Écart.** Aucun. Le README ne décrivait pas `apply_r6.py` : sa ligne est ajoutée (P-R7-8).

**Vérification (2026-10-22, verifier neuf) : CONFIRMED.** Rejoué par lui : `--rules` sur l'arbre vivant (0 constat, l'avertissement
`Session`) ; `selftest_rules.py` (39 cas, 0 inattendu) ; 21 violations de sa main sur des copies jetables (préfixe sur un imbriqué de la
DLL, namespace hors dossier, types publics secondaires, collisions avec xunit et la BCL, dossiers, miroir, doublon, lecture), toutes
refusées, et des fichiers neufs conformes acceptés ; le lecteur égal à la table `TypeDef` des DLL Debug ; `scan_types.py` inchangé et,
dans `check_names.py`, seuls la docstring, l'option et l'aiguillage ; aucune source de `Alundra/`, `Alundra.Tests/` ni des sous-modules
changée. Avis P4 (reportés) : A1 la suite `Alundra.Tests` en Release n'est pas relancée par lui (consignée par l'exécuteur ; aucun fichier
compilé ne change ; elle repasse avec R8) ; A2 une ligne ancienne du README de `tools/dll-structure` cite encore `TextBoxGpuHarness.cs` pour
l'avertissement `Session` (`UI/Shared/TextBoxGpu.cs:266` aujourd'hui) ; A3 `selftest_rules.py` accepte un refus dont les étiquettes
contiennent celles attendues, même si d'autres s'y ajoutent.

## 13. Tranche R8 — Les anciens noms des commentaires ⏳ (détaillée le 2026-10-22 ; mode AUTO, D-R-22 ; REVISE, révision 1, **READY** à la seconde relecture)

**Relecture (2026-10-22, relecteur neuf) : REVISE**, deux blocages corrigés (FIX, révision 1) : (1) les deux fichiers qui ne sont pas
des `.cs` n'avaient ni mode d'application ni preuve (le lexeur et Roslyn ne lisent que du C#) : un mode propre et sa preuve (P-R8-5) ;
(2) deux des quatre `IntroTraceHarnessTests.cs` ne citent aucun type et la règle (c) ne les tranchait pas : chaque cas à la main a
maintenant sa cible écrite (P-R8-3). Prochaine relecture : un relecteur neuf.

**But.** Les anciens noms de R3, R4 et R5 qui restent dans les **commentaires** du code de la DLL et des tests deviennent les noms
d'aujourd'hui, mécaniquement, **ligne pour ligne** (D-R-14, D-R-18 (b), D-R-20) ; aucun octet de code ni de chaîne ne change.

**Faits (découverte du 2026-10-22 à `7a81a3be`, lecture seule, `scratchpad/r8-disc/`).**
- 444 anciens noms de type : 147 de la DLL (R3 et R4, `tools/dll-structure/table.json`), 296 classes de test (R5, `r5-types.tsv`), une
  classe imbriquée (`r5-nested-types.tsv`) ; aucun n'est aussi un nom d'aujourd'hui.
- Dans les commentaires : **811 occurrences** (307 dans 64 fichiers de la DLL, 504 dans 118 fichiers de tests ; 608 en `//`, 203 en `///`,
  dont 158 en `<c>` et 2 en `<see cref>` : `Alundra/Session/GameState.cs:36`, `Alundra/UI/Hud/IHudView.cs:24`) ; plus 74 noms de fichier
  nus (66 de la table), 4 chemins `Alundra/Scripts…`, 5 chemins de tests, `Alundra.Scripts.AlundraEntityScriptProxy` et
  `Alundra.Tests.UI.TextBoxGpu`. Le chiffre de Q-R-5 (« ≈ 300 ») date d'avant R5.
- Dans le code : aucun ancien nom comme identifiant entier (lexeur Python et Roslyn d'accord) ; 3 noms de méthodes de test en embarquent
  un. Dans les chaînes : 14 littéraux gardés exprès par R3 et R4 (classe 4) et un message au chemin périmé
  (`Alundra.Tests/UI/Hud/HudPresenterTests.cs:479`).
- Un prototype sur copie : 879 éditions, 836 lignes, 191 fichiers ; même nombre de `\n` et de `\r` ; ajouts = suppressions par fichier ;
  le flux des jetons et des trivias hors commentaire identique (Roslyn, 618 fichiers). Seuls 4 renommages ne sont pas « le préfixe en
  moins » (`AlundraMusicPlayer` → `BgmPlayer`, `AlundraPlayerController` → `HeroController`, `AlundraRandom` → `GameRandom`,
  `AlundraSoundBank` → `SfxBank`) ; leurs classes de test ne perdent que le préfixe (D-R-13) : une substitution par sous-chaîne serait
  fausse pour 14 noms de la table.
- À ne pas toucher : les noms de la décompilation et de l'analyseur (`AlundraEngine`, `AlundraTools`, `AlundraDisplay.cs`, `AlundraGame.cs`,
  `…Gameplay.Scripts…`) ne sont jamais d'anciens noms ; l'échange de fichiers `EventProgramRunner.cs` (ancien fichier de l'interface) et
  `AlundraEventProgramRunner.cs` (devenu `EventProgramRunner.cs`) fait 11 faux restes pour un contrôle naïf.
- 17 occurrences ne sont pas mécaniques, et 2 jetons sont hors de la table (`AlundraArcSupport`, le `cref` vers
  `AlundraDialogueSpeakerOracle`, qui ne vise aucun type depuis `f4af632d`).
- Hors des `.cs` : `Alundra.Tests/xunit.runner.json` (`_comment`, 3 noms) et `Alundra.Tests/Alundra.Tests.csproj` (un commentaire XML).

**Décisions de la session (dans le cadre de D-R-14 ; l'auteur peut les contester).**
- **P-R8-1 — Périmètre.** Les commentaires (`//`, `///`, `/* */`) des `.cs` suivis de `Alundra/` et `Alundra.Tests/`, plus le `_comment` de
  `Alundra.Tests/xunit.runner.json` et le commentaire XML de `Alundra.Tests/Alundra.Tests.csproj`. Hors périmètre : les chaînes et le code
  (les 14 littéraux gardés, le message de `HudPresenterTests.cs:479`, les 3 noms de méthodes de test), les XAML de `alundra-project/`,
  les sous-modules, `docs/` (R6b), `tools/`.
- **P-R8-2 — Correspondance.** Par nom entier (bornes d'identifiant, du plus long au plus court), jamais par sous-chaîne ; un nom qualifié
  se réécrit en entier (`Alundra.Scripts.AlundraEntityScriptProxy` → `Alundra.Entities.EntityScriptProxy`,
  `Alundra.Tests.UI.TextBoxGpu` → `Alundra.Tests.UI.Shared.TextBoxGpu`) ; un ancien nom de fichier des tables prend son nouveau nom de
  base (unique dans l'arbre).
- **P-R8-3 — Les cas à la main**, chacun écrit dans le relevé avec sa raison et contrôlé un par un par le verifier ; **révision 1 : chaque
  cas a sa cible écrite ci-dessous** (la première version donnait des règles) : (a) **gardés** :
  `AlundraDisplay.cs`, `AlundraGame.cs`, `AlundraGame.json` (décompilation, analyseur, données) et la citation
  `Alundra/Scripts/PlayerManager.cs:…` de `Alundra/Hero/HeroController.cs:42` (elle cite une note d'un plan gelé) ; (b) `Alundra/Scripts`
  (le dossier) dans `Alundra.Tests/Transitions/ScreenFadeCameraWiringTests.cs:25`, `:30` et `:97` devient **`Alundra/`** : les trois
  phrases parlent de toute la DLL (« nothing under … references / overrides ») ; (c) les quatre `IntroTraceHarnessTests.cs` deviennent
  **`Arcs/HeadlessIntroSimulation.cs`** (avec la racine et le séparateur d'origine : `Alundra.Tests\…` reste à barres inverses,
  `Alundra.Tests/…` à barres droites) : `Alundra.Tests/Audio/SoundOpcodesProductionTests.cs:14` et
  `Alundra.Tests/Cells/CellStoreProductionTests.cs:15` passent par le type `HeadlessIntroSimulation` ; `Alundra/Events/EventProgramRunner.cs:61`
  parle du fil de traçage `TraceSink` que la simulation branche (`Alundra.Tests/Arcs/HeadlessIntroSimulation.cs:270`) ;
  `Alundra/Physics/EntitySupport.cs:17` parle de la cinématique simulée qui appelle `TryFindSupport`, dans ce même fichier ; et
  `UI/TextBoxGpuHarness.cs` (`Alundra.Tests/UI/Speech/Font3GlyphTests.cs:133`) devient **`UI/Shared/TextBoxGpu.cs`**, le fichier qui
  déclare le type cité ; (d) `<c>AlundraArcSupport</c>` (`Alundra.Tests/Events/MissingAnimationWaitCorpusTests.cs:282`, « resolves its
  prefab ») devient **`<c>ArcPrefabAssets</c>`**, le type qui résout aujourd'hui les préfabs (`Alundra.Tests/Support/ArcPrefabAssets.cs:55`) ;
  (e) `<see cref="AlundraDialogueSpeakerOracle"/>` (`Alundra.Tests/Dialogues/DialogueSpeakerMachineTests.cs:72`, « the C# oracle of »
  ce fichier d'aides) devient **`<c>Alundra.Tests/Support/SpeakerAnnex.cs</c>`** : le fichier qui l'a remplacé ne déclare aucun type unique
  de l'oracle (`SpeakerAnnex`, `OracleSlide`, `OracleNameBox`, `OraclePortrait`…). Un cas qui ne figure pas ici est un arrêt.
- **P-R8-4 — Les citations de lignes** `Fichier.cs:NNN` dans les commentaires (60) gardent leurs numéros : R8 est ligne pour ligne, R6b
  ne traite que les documents. Elles sont listées dans les résultats comme suite possible.
- **P-R8-5 — Outils.** Le relevé `tools/dll-structure/r8-comments.tsv` (fichier, ligne, rang, genre, ancien, nouveau, classe, raison),
  généré une fois à l'état de départ ; `apply_r8.py` (n'édite que les portées de commentaire du lexeur de R2, `apply_r2.py`, un événement
  par jeton ; lecture et écriture binaires, BOM et fins de ligne gardés) ; `prove_r8.py` (l'après attendu est l'avant plus les seuls
  événements du relevé, tout autre octet identique ; mêmes fichiers, même BOM, mêmes nombres de `\n` et de `\r` ; relevé entièrement
  appliqué ; à la fin, aucun ancien nom en commentaire hors des entrées gardées et de l'alias `EventProgramRunner.cs` ; ajouts =
  suppressions par fichier) ; un second contrôle indépendant, Roslyn, versé comme `rename-tool` : `tools/dll-structure/comment-xcheck/`
  (le flux des jetons et des trivias hors commentaire identique avant et après, sur tous les `.cs` du périmètre). Le filtre
  `startswith("'")` des outils gelés n'est pas corrigé (sans effet ici). **Les deux fichiers qui ne sont pas des `.cs`** (révision 1)
  ont un mode propre, `apply_r8.py --other` et `prove_r8.py --other` : dans `Alundra.Tests/xunit.runner.json`, seule la **valeur** de
  la clé `_comment` change (repérée par l'analyse JSON ; les autres clés et valeurs identiques à l'octet, le JSON toujours valide avec
  les mêmes clés) ; dans `Alundra.Tests/Alundra.Tests.csproj`, seul le **contenu** du commentaire XML `<!-- … -->` des lignes 32 à 34
  change (le reste identique à l'octet, le XML toujours valide) ; l'après égale l'avant plus les événements du relevé, mêmes nombres de
  lignes, même BOM, mêmes fins de ligne.
- **P-R8-6 — Mutations refusées** (`tools/dll-structure/selftest_r8.py`, versé) : un identifiant de code, un littéral, un commentaire hors
  relevé, une entrée omise, un nom de la décompilation (`AlundraDisplay.cs`), le mot `Alundra` seul, une substitution par sous-chaîne
  (`AlundraMusicPlayerTests` → `BgmPlayerTests`), un saut de ligne ajouté, un BOM ou des fins de ligne changés, un nom gardé modifié ;
  pour le mode `--other`, une autre clé ou valeur du JSON changée et du XML changé hors du commentaire ; et l'arbre au départ et à
  l'arrivée accepté.
- **P-R8-7 — Découpage.** Un commit d'outil (relevé, outils, mutations), un commit pour `Alundra/`, un pour `Alundra.Tests/` (avec le
  `.json` et le `.csproj`), un commit de plan et de résultats. Pas d'ADR (D-R-14 suffit).

**Périmètre.** `Alundra/**/*.cs` et `Alundra.Tests/**/*.cs` (commentaires seulement), les deux fichiers de P-R8-1, `tools/dll-structure/`
(outils et relevé de R8), ce plan. **Hors périmètre** : le reste (P-R8-1).

**Propriétaire, budget.** Un `executor` (relevé et cas à la main avec jugement, application mécanique) ; une passe d'exécution, une de
correction au plus ; un verifier frais.

**Tâches.**
- T0 — Le relevé à l'état de départ (les 811 noms, les noms de fichier, chemins et noms qualifiés, les cas de P-R8-3 classés), les
  outils, `comment-xcheck`, les mutations de P-R8-6 toutes refusées et les deux états acceptés ; commit d'outil.
- T1 — `apply_r8.py` sur `Alundra/`, `prove_r8.py` et `comment-xcheck` verts, la solution en Release (copie neutralisée) ; commit.
- T2 — Idem sur `Alundra.Tests/` ; les deux autres fichiers par `apply_r8.py --other`, `prove_r8.py --other` vert ; commit.
- T3 — Les portes : la solution en Release (copie neutralisée) puis en Debug, 0 erreur ; `Alundra.Tests` en Release puis en Debug,
  5 097 / 0, une exécution chacune ; `cmp` de la DLL Debug avec `alundra-project/Alundra.dll` ; la garde des traces (fins de ligne remises
  par `git checkout`) ; `check_names.py --rules` à 0 (R7) ; la fin de `prove_r8.py` (aucun ancien nom hors gardés) ; ce plan (résultats,
  les 60 citations de lignes listées) ; commit.

**Acceptation.** `prove_r8.py` (et `--other`) et `comment-xcheck` verts sur chaque commit ; aucune ligne ajoutée ni retirée ; aucun octet
de code ni de chaîne changé dans les `.cs` (dans les deux autres fichiers, seuls la valeur de `_comment` et le contenu du commentaire
XML changent) ; plus aucun ancien nom en commentaire hors des cas gardés ; les cas à la main justifiés ; les portes de T3.

**Arrêts.** Une occurrence que les règles ne tranchent pas ; un refus de `prove_r8.py` ou de `comment-xcheck` ; un nombre de lignes qui
change ; un octet de code ou de chaîne qui change ; un test qui échoue.

**Retour arrière.** `git revert` des commits de la tranche.

### Résultats de l'exécution (2026-10-22)

Faite en mode AUTO (D-R-22), un `executor`, une passe, sur `chantier/restructuration-dll-r3` depuis `55fac9af`. Quatre commits : `621c7581` (T0, outils : `r8_lib.py`, `gen_r8.py`, `apply_r8.py`, `prove_r8.py`, `selftest_r8.py`, `comment-xcheck/`, le relevé `r8-comments.tsv`, `README.md` des outils), `4a12753e` (T1, `Alundra/`), `a9d1dd8e` (T2, `Alundra.Tests/` avec le `.json` et le `.csproj`) et le commit de ce plan (T3, avec les corrections d'outil faites en cours de route : modes `.cs` et `--other` de `prove_r8.py` disjoints, `--from REV`, lecture du relevé tolérante aux fins de ligne CRLF, `gen_r8.py` figé par défaut sur `55fac9af`, 30 cas au lieu de 29 dans `selftest_r8.py` ; elles ne changent rien aux preuves de T1 et de T2, rejouées sur les commits). Aucun ADR (P-R8-7).

**Le relevé (T0).** 902 lignes dans 196 fichiers : 815 noms de type, 66 noms de fichier, 1 nom qualifié de la DLL (`Alundra.Scripts.AlundraEntityScriptProxy` → `Alundra.Entities.EntityScriptProxy`), 1 nom qualifié de test (`Alundra.Tests.UI.TextBoxGpu` → `Alundra.Tests.UI.Shared.TextBoxGpu`), 10 cas à la main appliqués (`hand` : trois `Alundra/Scripts` → `Alundra/`, quatre `IntroTraceHarnessTests.cs` → `Arcs/HeadlessIntroSimulation.cs`, `UI/TextBoxGpuHarness.cs` → `UI/Shared/TextBoxGpu.cs`, `AlundraArcSupport` → `ArcPrefabAssets`, le `cref` de l'oracle → `<c>Alundra.Tests/Support/SpeakerAnnex.cs</c>`) et 9 gardés avec leur raison (`kept` : `AlundraDisplay.cs` six fois, `AlundraGame.cs`, `AlundraGame.json`, `Alundra/Scripts/PlayerManager.cs` de `HeroController.cs:42`). Les 815 noms de type = les 811 occurrences de la découverte dans les `.cs` plus les 4 des deux autres fichiers. Les 879 événements automatiques de la découverte (prototype) et ceux du relevé sont les mêmes, un à un (fichier, ligne, ancien, nouveau). Toute occurrence hors des règles et des cas écrits est un arrêt du générateur : il n'y en a eu aucune. Le relevé rejoué à `55fac9af` redonne les mêmes octets (`gen_r8.py --check`).

**Les mutations (T0).** `selftest_r8.py` : 30 cas, 0 faux. Acceptés : le départ vers l'arrivée (`.cs`, la DLL seule avec `--scope dll`, les tests seuls avec `--scope tests`, `--other`). Refusés : le départ vers le départ, un identifiant de code, un littéral, un commentaire hors relevé, une entrée omise, `AlundraDisplay.cs` réécrit, le mot `Alundra` seul, la substitution par sous-chaîne (`AlundraMusicPlayerTests` → `BgmPlayerTests`), une ligne ajoutée, un BOM retiré, des fins de ligne changées, deux noms gardés modifiés (`AlundraGame.json`, `Alundra/Scripts/PlayerManager.cs`), un fichier de test changé sous `--scope dll`, un fichier retiré, et pour `--other` une valeur du JSON, une clé du JSON, le `_comment` avec un mauvais nom, le XML changé hors de son commentaire, le commentaire XML avec un mauvais nom, le départ. Plus les deux cas de Roslyn : l'arrivée acceptée, un littéral changé refusé. Une proof ne regarde que son domaine : `--other` ne lit pas les `.cs`, le mode `.cs` ne lit pas les deux autres fichiers (un commit qui porte les deux, T2, se prouve par les deux exécutions).

**T1 (`4a12753e`).** `apply_r8.py --scope dll` : 68 fichiers, 340 événements, 326 lignes changées. `prove_r8.py --scope dll --xcheck` vert avant et après le commit (Roslyn : 618 fichiers, 68 textuellement différents, 0 non équivalent). Solution `.slnx` en Release (copie neutralisée) : 0 erreur.

**T2 (`a9d1dd8e`).** `apply_r8.py --scope tests` : 124 fichiers, 549 événements, 519 lignes ; `apply_r8.py --other` : 2 fichiers, 4 événements, 2 lignes (la valeur de `_comment`, une ligne ; le commentaire XML des lignes 32 à 34, une ligne, BOM gardé). `prove_r8.py --scope tests --xcheck` (Roslyn : 124 différents, 0 non équivalent) et `prove_r8.py --other` verts avant et après le commit. Sur toute la plage (`--commit HEAD --from 55fac9af`) : `all` = 192 fichiers, 889 événements, 845 lignes, `other` = 2 fichiers, 4 événements, 2 lignes ; ajouts = suppressions (847 lignes en tout) ; aucun octet hors commentaire ne change ; les seuls noms qui restent en commentaire sont les 9 gardés.

**T3, les portes (état `a9d1dd8e`).** Solution `.slnx` et `tools/AlundraTestSaves` en Release (copie neutralisée) puis en Debug : 0 erreur. `Alundra.Tests` : **Release 5 097 réussis / 0 échec** (6 min 11 s), **Debug 5 097 / 0 échec** (5 min 44 s), une exécution chacune. `cmp` de `Alundra/bin/Debug/net9.0-windows/Alundra.dll` avec `alundra-project/Alundra.dll` : identique. Garde des traces : `git diff --ignore-cr-at-eol --quiet -- docs/` sort 0 après chaque suite (les quatre `hero-trace-389-*.txt` remises par `git checkout`). `check_names.py --rules` : 0 écart (l'avertissement connu du type imbriqué `Session` de `TextBoxGpu.cs`). Fin de `prove_r8.py` : plus aucun ancien nom hors des gardés.

**Écarts.** (1) Les chiffres de la découverte (811 occurrences) comptent les `.cs` seuls ; le relevé en porte 815 avec les deux autres fichiers. (2) Les deux modes de preuve sont disjoints (le contrat parlait d'un `--other` « de plus ») ; la preuve d'un commit qui porte les deux fichiers se fait en deux exécutions. (3) Roslyn est versé comme `comment-xcheck/CommentXcheck.dll` (le `Xcheck.dll` de la découverte, `equiv` seul est utilisé par la preuve). (4) Les commits portent `Co-Authored-By: Claude Sonnet 5.5`, la ligne imposée par l'harnais.

**Suite possible : les 60 citations de lignes `Fichier.cs:NNN` des commentaires (P-R8-4), gardées telles quelles** (fichier du commentaire : ligne du commentaire, citation avec le nom d'aujourd'hui du fichier cité ; 7 visent la décompilation ou l'analyseur : `AlundraDisplay.cs:32` six fois et `AlundraGame.cs:199,236`) :
- `Alundra.Tests/Arcs/HeadlessIntroSimulation.cs` : 81 `ScriptedMotion.cs:60-61` et `WorldProxy.cs:55-56` ; 817 `ScriptedMotion.cs:248-249` ; 956 `WorldProxy.cs:645-654`.
- `Alundra.Tests/Arcs/InoaDayOneArcTests.cs` : 393 `EntitySpawnFactory.cs:667-669`. `Alundra.Tests/Audio/BgmFadeDirectorTests.cs` : 366 `WorldProxy.cs:2136-2148`. `Alundra.Tests/Dialogues/DialogueOpcodesProductionTests.cs` : 349 `WorldProxy.cs:1018-1037`.
- `Alundra.Tests/Dialogues/Yarn/YarnVariableStorageTests.cs` : 56 et 190 `EventProgramRunner.cs:449` ; 137 `EventProgramRunner.cs:1416` ; 222 `EventProgramRunner.cs:457`.
- `Alundra.Tests/Entities/GlobalFreezeEntityUpdateTests.cs` : 45 et 133 `ScriptedMotion.cs:233-241` ; 150 `WorldProxy.cs:1539` ; 160 `FrameSyncPasses.cs:241` ; 210 `FrameSyncPasses.cs:168-183`. `Alundra.Tests/Entities/GroundSlopeTests.cs` : 288 `CellsCollisionField.cs:135`.
- `Alundra.Tests/Hero/HeroFallAndPadJumpTests.cs` : 389 `CellsCollisionField.cs:382-391`. `Alundra.Tests/Hero/LadderClimbTests.cs` : 372 `ScriptedMotion.cs:117-130`.
- `Alundra.Tests/Maps/WorldProxyGlobalFreezeTests.cs` : 138 `WorldProxy.cs:506-518` ; 140 et 307 `WorldProxy.cs:527/535`. `Alundra.Tests/SaveGames/SaveGameApplyTests.cs` : 343 `EventProgramRunner.cs:1416`.
- `Alundra.Tests/Transitions/WarpDepartureTests.cs` : 177 `WorldProxy.cs:515-520` ; 731 `WarpDirector.cs:387`. `Alundra.Tests/UI/Hud/HudComposerTests.cs` : 299 `HudDirector.cs:462-473` ; 387 `HudDirector.cs:462`. `Alundra.Tests/UI/Hud/HudDirectorTests.cs` : 24 `GameState.cs:194/196`. `Alundra.Tests/UI/Inventory/CursorAnimationTimingTests.cs` : 12 `InventoryDirector.cs:520-524`.
- `Alundra/Ai/Objects/SaveBook.cs` : 69 `EventProgramRunner.cs:704-710`. `Alundra/Dialogues/Yarn/YarnVariableStorage.cs` : 19 `EventProgramRunner.cs:1416` ; 20 `EventProgramRunner.cs:449-457` ; 89 `EventProgramRunner.cs:449`. `Alundra/Events/EventProgramRunner.cs` : 2056 `EntityScriptProxy.cs:87` ; 3290 `EntityScriptProxy.cs:134-136`. `Alundra/Hero/PlayerManager.cs` : 1745 `EntityScriptProxy.cs:828-867`.
- `Alundra/Maps/Loop/GameplayFreeze.cs` : 36 `ScriptedMotion.cs:117-130` ; 123 `ScriptedMotion.cs:140-144`. `Alundra/Maps/WorldProxy.cs` : 1522 `BackdropStage.cs:60-66`. `Alundra/Physics/TerrainProbe.cs` : 78 `CellsCollisionField.cs:295`. `Alundra/Transitions/PortalTrigger.cs` : 40 `HeroController.cs:28-31`.
- `Alundra/SaveGames/SaveGame.cs` : 58 `PlayerManager.cs:660-671` ; 61 `PlayerManager.cs:704-714` ; 62 `HudDirector.cs:164` ; 65 `PlayerManager.cs:746-757` ; 66 `HudComposer.cs:320-331` ; 69 `PlayerManager.cs:822-834` ; 81 `WorldProxy.cs:1563-1565`.
- `Alundra/UI/Hud/HudComposer.cs` : 119 `HudDirector.cs:462-473`. `Alundra/UI/Hud/HudDirector.cs` : 44 `GameState.cs:194` et `GameState.cs:196` ; 52 `GameState.cs:196` ; 65 `EventProgramRunner.cs:443-448`.
- La décompilation et l'analyseur (gardées) : `Alundra/Rendering/Backdrops/BackdropStage.cs:148` `AlundraGame.cs:199,236` ; `AlundraDisplay.cs:32` dans `Alundra/UI/Choice/ChoiceScreen.cs:29`, `Hud/HudScreen.cs:42`, `Inventory/InventoryScreen.cs:40`, `Messages/TextBoxScreen.cs:30`, `Saving/SaveScreen.cs:37` et `SubInventory/SubInventoryScreen.cs:33`.

Mesure de la découverte (48 citations mesurables contre la base `5434cf6`) : 1 pointe encore la même ligne, 33 ont bougé de façon unique, 1 texte a changé, 13 sont indéterminées. Ce n'est pas le travail de R8 (ligne pour ligne) ni de R6b (les documents) : une tranche à part, si l'auteur le veut.

Statut : R8 faite et prouvée par `prove_r8.py` et Roslyn ; reste le verifier frais de fin de tranche (hors de cette exécution) et la recette de l'auteur.

## 14. Tranche R6b — Les numéros de ligne des documents vivants ⏳ (détaillée le 2026-10-22 ; mode AUTO, D-R-22 ; relue avec le §15 :
REVISE sur le §15 seulement, révision 1, **READY** à la seconde relecture)

**But.** Dans **les 21 documents vivants**, chaque citation de ligne désigne au HEAD ce que sa phrase cite (D-R-17, D-R-20) ; les
documents historiques, les audits, les annexes figées et les plans ne changent pas.

**Faits (découverte du 2026-10-22 à `7a81a3be`, lecture seule, `scratchpad/r6b-disc/`).**
- Les documents vivants, au sens de R6 (§10, `apply_r6.py:110-118`) : `docs/census-0x24-waits.md`, `docs/editeur-couverture-dll.md`,
  `docs/formats/` (`README`, `audio-manifests`, `backdrops`, `cells-companion`, `dialogues-yarn`, `effects`, `events`, `font`,
  `map-sprite-indices`, `misc-data`, `save-game`, `sprite-records`, `world-index`), `docs/guidelines-runtime-alundra-casaengine.md`,
  `docs/intro-roadmap.md`, `docs/stick-probe.md`, `docs/test-saves.md`, `README.md` (racine), `research/census/README.md`.
- Ils portent **105 citations** (111 plages) : 101 vers la décompilation (`docs/intro-roadmap.md` 94, `docs/formats/cells-companion.md` 7),
  **une seule vers la DLL** (`docs/intro-roadmap.md:325`, `GameState.cs:275-294`), 2 vers le moteur (`docs/guidelines-runtime-alundra-casaengine.md:142`
  et `:229`), une vers un autre document (`docs/formats/backdrops.md:20`) ; aucune vers les tests ni les outils.
- La citation de la DLL se suit sans perte : écrite à `59fac902` contre `Alundra/Scripts/AlundraGameState.cs`, elle est aujourd'hui
  `Alundra/Session/GameState.cs:368-387` (+93, lignes identiques). La citation que R6 a retirée de `docs/intro-roadmap.md:227`
  (`AlundraWorldProxy.cs:288-323`, écrite à `3a880a63`) se retrouve identique en `Alundra/Entities/Spawning/EntitySpawnFactory.cs:59-94`.
- Décompilation (pointeur de l'analyseur `fc16872`) : 61 citations visent un fichier inchangé depuis leur écriture ; sur les 33 autres,
  30 gardent leurs numéros et 3 sont décalées d'une ligne (`docs/intro-roadmap.md:61`, `:276`, `:242`). Sur 44 débuts de plage contrôlés
  contre la déclaration du membre que la phrase nomme, 39 sont exacts ; écarts : `:61` et `:276` (les décalages ci-dessus), `:67` et
  `:223` (`SpawnEntity` cité à 684 et 680, déclaré à 681), `:183` (une sous-plage de `RunScript`). Les 7 de `cells-companion.md` visent
  des fichiers inchangés.
- Moteur (pointeur `92d8e847`) : les 2 citations des guidelines sont fausses (`SimulationSpacePolicy.cs:135-137` ne porte plus la
  formule de rendu ; `World.cs:443-491` tombe dans `GetPlayerIndices` et la fin de `CreateLocalPlayerController`).
- Document vers document : `docs/formats/backdrops.md:20` cite `docs/editeur-couverture-dll.md:57-59` ; le paragraphe est aujourd'hui à
  58-60 (une ligne ajoutée au-dessus par `a3fa8810`).
- Méthode éprouvée : le commit d'écriture W par « pickaxe » sur la citation, le fichier cité à W sous son nom d'avant R6 (colonne
  `before` de `tools/dll-structure/r6-occurrences.tsv`), un `git diff --histogram` de W au HEAD, et un repli par contenu.
- Hors des documents : les scripts de `research/` (3 citations de la DLL) et les commentaires du convertisseur (3 de la DLL, 15 vers des
  fichiers de l'analyseur modifiés) ; les commentaires du code de la DLL et des tests (84 citations de nos sources).

**Décisions de la session (l'auteur peut les contester).**
- **P-R6b-1 — Lecture de D-R-20.** D-R-20 écrit « les numéros de ligne qu'ils citent dans la DLL, les tests et les outils » ; la question
  Q-R-4c, à laquelle l'auteur a répondu « tous les documents vivants », parlait des « numéros de ligne cités dans les docs … recalculés
  partout ». Avec une seule citation vers la DLL, la lecture étroite viderait la tranche : R6b vérifie **toutes** les citations des
  21 documents vivants, quelle que soit leur cible ; le code cité (décompilation, moteur) ne change jamais, seule la citation.
- **P-R6b-2 — Périmètre.** Les 21 documents vivants. Hors périmètre, listés comme suites possibles : les scripts de `research/` et les
  commentaires du convertisseur (les deux autres familles de R6, qui ne sont pas des documents), les commentaires du code (R8 ne change
  que des noms), les plans, annexes, ADR, audits, traces `docs/*.txt` et `census0b*.md` (gelés ou historiques).
- **P-R6b-3 — Méthode, par cible.** DLL, tests, outils : W, le fichier à W, le diff jusqu'au HEAD, le repli par contenu. Décompilation et
  moteur, au pointeur actuel des sous-modules : la plage citée doit porter ce que la phrase nomme (le membre, le bloc décrit) ; si le
  fichier a changé depuis W, le diff de l'historique du sous-module ; si la phrase nomme un membre, le début de la plage est la ligne de sa
  déclaration. Document vers document : l'historique du document cité. Chaque citation reçoit une classe : juste, recalculée (décalage),
  recalculée par contenu, ou indéterminée (arrêt et question).
- **P-R6b-4 — La citation retirée par R6** (`docs/intro-roadmap.md:227`) est rétablie avec ses numéros d'aujourd'hui
  (`Alundra/Entities/Spawning/EntitySpawnFactory.cs:59-94`, lignes identiques), puisque la tranche corrige les documents.
- **P-R6b-5 — Forme et garde.** La forme `Fichier.cs:a-b` reste ; pas de garde permanente des citations (elles s'useront avec E14, E19 et
  E20). La preuve de R6 (`apply_r6.py --check`, `r6-occurrences.tsv`) est **gelée** à son commit de clôture `2964d9ae`, comme celles de R3
  et R4 (le README le dit).
- **P-R6b-6 — Outils.** Le relevé `tools/dll-structure/r6b-citations.tsv` (document, ligne, citation avant, cible, W, méthode, citation
  après, classe, sondes du contenu) et `prove_r6b.py` : dans les 21 documents, seuls changent les numéros des citations relevées (et la
  citation rétablie) ; pour chaque citation, les lignes de début et de fin de la plage au HEAD égalent les sondes du relevé ; fins de
  ligne (CRLF) gardées.

**Périmètre.** Les 21 documents vivants, `tools/dll-structure/` (relevé, `prove_r6b.py`, README), ce plan. **Hors périmètre** : P-R6b-2 ;
tout fichier de code, de données, des sous-modules.

**Propriétaire, budget.** Un `executor` (les citations demandent du jugement : lire la phrase et la cible) ; une passe d'exécution, une de
correction au plus ; un verifier frais.

**Tâches.**
- T0 — Le relevé des 105 citations (plus la citation rétablie), la méthode de P-R6b-3 appliquée, chaque citation classée avec ses sondes ;
  `prove_r6b.py` et ses refus (un chiffre changé hors relevé, un mot changé, une plage dont la sonde ne correspond pas) ; commit d'outil.
- T1 — L'application dans les 21 documents, `prove_r6b.py` vert ; le paragraphe de gel de R6 au README ; ce plan (résultats : comptes par
  classe, chaque citation changée) ; commit. Aucune construction ni suite : aucun fichier compilé ne change (`git diff --stat` le
  montre).

**Acceptation.** `prove_r6b.py` vert ; chaque citation des 21 documents est juste au HEAD (contrôle par ses sondes), ou classée
indéterminée avec la question écrite ; seuls les 21 documents, `tools/dll-structure/` et ce plan changent.

**Arrêts.** Une citation dont la cible ou l'intention ne s'établit pas ; un document hors de la liste qui changerait ; une ligne de code
d'un sous-module qu'il faudrait changer.

**Retour arrière.** `git revert` des commits de la tranche.

### Résultats de l'exécution (2026-10-22)

Mode AUTO (D-R-22), un `executor`, une passe. T0 `546b7be2` (outils et relevé), T1 (documents, README, ce plan). Aucune construction, aucune suite : aucun fichier compilé ne change (`git diff --stat 33528d55 HEAD` ne liste que quatre documents vivants, `tools/dll-structure/` et ce plan ; aucun pointeur de sous-module ne bouge).

- **Relevé** `tools/dll-structure/r6b-citations.tsv` : **113 lignes** = les 105 citations des 21 documents en **112 plages** (le §14 comptait 111 ; la différence est une erreur de décompte de la découverte, les 105 citations sont les mêmes) plus la citation rétablie (P-R6b-4). Par cible : décompilation 108 plages, DLL 1, moteur 2, document 1 (aucune vers les tests ni les outils, comme prévu). Seuls 4 des 21 documents portent des citations : `docs/intro-roadmap.md` (100 plages), `docs/formats/cells-companion.md` (9), `docs/guidelines-runtime-alundra-casaengine.md` (2), `docs/formats/backdrops.md` (1) ; les 17 autres n'en portent aucune.
- **Classes** : 77 **justes** (numéros gardés, contrôlés sur le contenu aux pointeurs), 10 **recalculées par décalage** (les deux bornes bougent du même écart), 25 **recalculées par contenu**, 1 **rétablie**, **0 indéterminée** (aucun arrêt).
- **Méthode** (P-R6b-3) : DLL et document cité par l'historique (W par « pickaxe », diff `--histogram`) ; décompilation (`fc16872`) et moteur (`92d8e847`) lus avec `git show` à leur pointeur, jamais dans l'arbre de travail du sous-module (qui porte les changements de l'auteur). Règle de décision : une plage qui nomme un membre commence à sa déclaration et finit à son accolade fermante ou à sa dernière instruction (`whole`) ; une plage « partie » commence à la déclaration et finit sur une ligne de code dans le membre ; un bloc ou une instruction décrits par la phrase commencent et finissent sur des lignes de code (jamais une ligne vide, un commentaire ou le membre suivant).
- **Constat** : sur les 108 plages de la décompilation, 77 sont justes et 31 sont recalculées (8 par décalage, 23 par contenu) ; les 2 plages du moteur étaient fausses (recalculées par contenu) ; celle de la DLL est décalée de +93 (lignes identiques) ; celle du document de +1. Les écarts par contenu viennent surtout de bornes qui tombaient sur une ligne vide, un commentaire ou le membre suivant, qu'un contrôle de décalage seul ne voit pas. Appui indépendant : les commentaires de `EntitySpawnFactory.cs` citent `GameEngine.cs:715-718`, `690-711` et `648-672`, les numéros retenus ici pour la porte 0x40, la boîte de zone et `ResetEntityState`.
- **Fidélité aux décisions** : P-R6b-1 (toutes les cibles), P-R6b-2 (aucun autre document), P-R6b-4 (rétablie avec `:59-94`), P-R6b-5 (forme gardée, pas de garde permanente, preuve de R6 gelée : paragraphe au README), P-R6b-6 (relevé, `prove_r6b.py`, 17 verdicts d'auto-test). Écart de lecture à signaler : `:59-94` de P-R6b-4 est repris à la lettre, mais au HEAD cette plage commence dans le commentaire XML de `ShouldSpawnRecord` et s'arrête dans le second `if` (la méthode complète est `:83-100`) ; classée `rétablie`, sondes écrites au relevé, à trancher par l'auteur si l'on veut la plage du membre.
- **Preuve** : `prove_r6b.py --files` à 0 (A : le relevé couvre les 112 plages de la base ; B : chaque document est la base plus les modifications du relevé et rien d'autre, fins de ligne CRLF gardées ; C : les plages lues sont la colonne `after` ; D : début et fin de chaque plage égalent les sondes aux arbres épinglés, déclaration et accolade fermante pour les membres) ; `prove_r6b.py --selftest` : 17 verdicts attendus (arrivée acceptée ; départ, chiffre hors citation, mot changé, plage changée d'un seul côté, citation en trop ou manquante, ligne vide, sonde fausse, début hors déclaration, fin trop courte ou débordante, ligne ou colonne du relevé manquante, document en LF refusés).

Plages modifiées (36 lignes du relevé sur 113, la citation rétablie comprise ; les 77 autres sont justes et gardent leurs numéros) :

| Document:ligne | Désigné | Avant | Après | Classe |
|---|---|---|---|---|
| `formats/backdrops.md:20` | `docs/editeur-couverture-dll.md` (`docs/editeur-couverture-dll.md`) | 57-59 | 58-60 | recalculée (décalage) |
| `guidelines-runtime-alundra-casaengine.md:142` | `DeriveRenderPosition` (`SimulationSpacePolicy.cs`) | 135-137 | 159-162 | recalculée par contenu |
| `guidelines-runtime-alundra-casaengine.md:229` | `Update` (`CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs`) | 443-491 | 552-621 | recalculée par contenu |
| `intro-roadmap.md:60` | `Script_51_033` (`EntityEventHandlers.cs`) | 1112-1126 | 1112-1129 | recalculée par contenu |
| `intro-roadmap.md:61` | `Script_139_08B` (`EntityEventHandlers.cs`) | 2557-2575 | 2558-2579 | recalculée par contenu |
| `intro-roadmap.md:67` | `SpawnEntity` (`GameEngine.cs`) | 684-717 | 681-718 | recalculée par contenu |
| `intro-roadmap.md:79` | `Script_59_03B` (`EntityEventHandlers.cs`) | 1223-1238 | 1223-1240 | recalculée par contenu |
| `intro-roadmap.md:97` | `GameEngine.cs` (`GameEngine.cs`) | 1690-1697 | 1692-1698 | recalculée par contenu |
| `intro-roadmap.md:188` | `EntityEventHandlers.cs` (`EntityEventHandlers.cs`) | 343-358 | 345-363 | recalculée par contenu |
| `intro-roadmap.md:189` | `EntityEventHandlers.cs` (`EntityEventHandlers.cs`) | 382-391 | 380-389 | recalculée (décalage) |
| `intro-roadmap.md:196` | `Update` (`GameEngine.cs`) | 1500-1592 | 1500-1587 | recalculée par contenu |
| `intro-roadmap.md:197` | `GameEngine.cs` (`GameEngine.cs`) | 1517 | 1518 | recalculée (décalage) |
| `intro-roadmap.md:201` | `UpdateEntities` (`EntityManager.cs`) | 367-395 | 367-394 | recalculée par contenu |
| `intro-roadmap.md:208` | `GameEngine.cs` (`GameEngine.cs`) | 222-229 | 225-229 | recalculée par contenu |
| `intro-roadmap.md:212` | `GameEngine.cs` (`GameEngine.cs`) | 168-219 | 169-219 | recalculée par contenu |
| `intro-roadmap.md:220` | `InitializeEntitySlots` (`GameEngine.cs`) | 621-646 | 621-645 | recalculée par contenu |
| `intro-roadmap.md:223` | `SpawnEntity` (`GameEngine.cs`) | 680-760 | 681-758 | recalculée par contenu |
| `intro-roadmap.md:225` | `GameEngine.cs` (`GameEngine.cs`) | 714-717 | 715-718 | recalculée (décalage) |
| `intro-roadmap.md:227` | `EntitySpawnFactory.cs` (`EntitySpawnFactory.cs`) | — | 59-94 | rétablie |
| `intro-roadmap.md:237` | `ResetEntityState` (`GameEngine.cs`) | 648-670 | 648-672 | recalculée par contenu |
| `intro-roadmap.md:242` | `StaticVariables.cs` (`StaticVariables.cs`) | 12815 | 12814 | recalculée (décalage) |
| `intro-roadmap.md:250` | `Script_16_010+Script_17_011` (`EntityEventHandlers.cs`) | 680-693 | 680-691 | recalculée par contenu |
| `intro-roadmap.md:275` | `Script_85_055` (`EntityEventHandlers.cs`) | 1623-1652 | 1623-1654 | recalculée par contenu |
| `intro-roadmap.md:276` | `Script_133_085` (`EntityEventHandlers.cs`) | 2440-2444 | 2441-2445 | recalculée (décalage) |
| `intro-roadmap.md:276` | `ChangeAreaTileProperties` (`GameEngine.cs`) | 2239-2300 | 2239-2322 | recalculée par contenu |
| `intro-roadmap.md:277` | `Script_59_03B` (`EntityEventHandlers.cs`) | 1223-1238 | 1223-1240 | recalculée par contenu |
| `intro-roadmap.md:280` | `Script_7_007` (`EntityEventHandlers.cs`) | 539-570 | 539-582 | recalculée par contenu |
| `intro-roadmap.md:281` | `Script_30_01E` (`EntityEventHandlers.cs`) | 793-826 | 793-829 | recalculée par contenu |
| `intro-roadmap.md:286` | `Script_31_01F` (`EntityEventHandlers.cs`) | 832-843 | 832-841 | recalculée par contenu |
| `intro-roadmap.md:325` | `InstallForMapEntry` (`GameState.cs`) | 275-294 | 368-387 | recalculée (décalage) |
| `intro-roadmap.md:328` | `InitializeEntitySlots+ResetEntityState` (`GameEngine.cs`) | 621-670 | 621-672 | recalculée par contenu |
| `intro-roadmap.md:336` | `GameEngine.cs` (`GameEngine.cs`) | 1517 | 1518 | recalculée (décalage) |
| `intro-roadmap.md:349` | `GameEngine.cs` (`GameEngine.cs`) | 1561-1578 | 1562-1577 | recalculée par contenu |
| `intro-roadmap.md:350` | `GameEngine.cs` (`GameEngine.cs`) | 1580 | 1579 | recalculée (décalage) |
| `intro-roadmap.md:351` | `GameEngine.cs` (`GameEngine.cs`) | 1582 | 1581 | recalculée (décalage) |
| `intro-roadmap.md:378` | `ChangeAreaTileProperties` (`GameEngine.cs`) | 2239-2300 | 2239-2322 | recalculée par contenu |


## 15. Fusion finale (D-R-22) ⏳ (relecture du 2026-10-22 avec R6b : REVISE, révision 1 : arrêts, retour arrière et portes de la
branche d'intégration ajoutés)

Après R7, R8 et R6b faites et CONFIRMED :
- **Parent** : `main` reçoit `chantier/restructuration-dll-r3` par avance rapide (`git fetch . chantier/restructuration-dll-r3:main`, qui
  refuse tout autre cas, sans toucher l'arbre de travail) ; si `main` a bougé entre-temps, une branche d'intégration depuis le chantier
  reçoit `main`, puis, avant que `main` avance sur elle, toutes ces portes passent : la solution en Release (copie neutralisée) puis en
  Debug, 0 erreur ; `Alundra.Tests` et la suite du convertisseur en Release et en Debug, 0 échec ; la garde des traces ;
  `check_names.py --rules` à 0 (R7) ; `cmp` de la DLL Debug avec `alundra-project/Alundra.dll`.
- **Sous-modules** : le `main` du moteur, le `master` de l'analyseur et le `develop` de MGUI doivent contenir les pointeurs du parent
  (`git merge-base --is-ancestor <pointeur> <branche>`) ; au 2026-10-22 c'est le cas (`92d8e847`, `fc16872`, `3db3c769`). Un pointeur qui
  ne serait pas contenu : avance rapide de la branche s'il en est le descendant, sinon arrêt et question.
- Rien n'est poussé. Les modifications locales de l'auteur (le lanceur du moteur, le pointeur MGUI de l'analyseur, `AlundraTools/log.txt`,
  les outils non suivis) ne sont ni touchées ni indexées.
- Après la fusion : la branche du chantier reste ; la recette courte de l'auteur (nouvelle partie jusqu'au premier écran jouable, F5, F6,
  F9) reste due pour R2 à R8.
- **Tâche suivante, demandée par l'auteur le 2026-10-22** : renommer les 122 sauvegardes `.sav` de `tools/Saves/` (fichiers de l'auteur,
  non suivis) pour que leur nom suive la règle des emplacements du moteur (`[a-z0-9_-]`, 32 caractères au plus, hors noms réservés de
  Windows : `CasaEngineMonogame/CasaEngine/Framework/SaveGames/SaveGameNames.cs:18`) ; un nom hors règle est sauté en silence par
  l'énumération des emplacements (`SaveGameFileStorage.cs:281`), d'où « on ne peut pas les charger ». Le fichier ne contient pas son
  nom : renommer suffit, sans rien indexer ; la table ancien → nouveau est gardée.
- **Arrêts** : un conflit de fusion (code, documents ou pointeur de sous-module) ; une erreur de construction ; un test qui échoue ; une
  porte qui ne passe pas ; un pointeur de sous-module que sa branche ne contient pas et dont elle n'est pas l'ancêtre. Dans chaque cas :
  arrêt et question à l'auteur, aucune résolution par jugement (en AUTO, ce serait sortir du périmètre).
- **Retour arrière** : avant toute fusion, les SHA de `main` du parent et de `main` (moteur), `master` (analyseur) et `develop` (MGUI)
  sont relevés et écrits ici ; chaque référence se remet à son SHA par `git update-ref refs/heads/<branche> <sha> <sha attendu>` dans son
  dépôt (sans toucher l'arbre de travail) ; la branche d'intégration éventuelle se supprime (`git branch -D`, une branche que l'on vient
  de créer).
