# Plan — Restructuration de la DLL de gameplay Alundra

Statut : ⏳ proposé le 2026-10-16, à relire (enveloppe et tranche R0). Démarre après E20.e0a4 (plan E20, §1.16.4).

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
  UI/            Hud/, Inventory/, SubInventory/, Messages/ (les écrans TextBox), Choice/, Speech/ (DialoguePresenter), SaveScreen/, Shared/
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
- Branche : à décider par l'auteur (Q-R-1).
- Budget : R2 et R3 sont mécaniques (`mech-executor`, une passe par domaine, un commit par domaine) ; R0 et R4 demandent du jugement
  (`executor`) ; `plan-verifier` avant chaque tranche ; un verifier frais après chaque tranche, deux pour R0 et R4 (moteur, données).
- Retour arrière : `git revert` de la tranche ; pour R0 et R4, revert du commit parent et réexport depuis le commit précédent.

## 4. Tranche R0 — Moteur : objets script par nom complet, DLL seule ; premier export ⏳

**Relecture (2026-10-16).** Enveloppe (§0 à §3) : **READY**. R0 : **REVISE**, trois blocages corrigés (FIX, révision 1) : (1) le lieu
de l'exception de D-R-4 et le chemin de son contexte, sans casser les chargements sans DLL (vérification du convertisseur) ; (2) un seul
propriétaire de l'enregistrement de l'assembly par chemin (le rechargement à chaud de l'éditeur) ; (3) la prédiction de l'export versée
dans une annexe avant tout code (T0).
**Révision 2 (2026-10-16).** Seconde relecture : **REVISE**, un blocage ; disposition de la session principale (cap de deux relectures
automatiques atteint) : **FIX** — le fichier du message de D-R-4 ne peut pas venir du catalogue par l'identifiant : un clone de préfab
reçoit un `Id` neuf et perd son `AssetId` (`ObjectBase.cs:37-42`, `EntityReference.cs:58,75`), et une entité en ligne n'a pas encore son
monde à `Initialize` (`World.cs:788-789`). La règle d'origine du fichier est réécrite sur les vrais chemins, avec les tests qui la
prouvent. Nouvelle époque de relecture : une seule relecture de clôture suit.

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
  `AssetId`), le monde complète le message : `World.InternalAddEntities` rattrape l'exception de D-R-4 levée par `entity.Initialize()`
  et la relance avec le `FileName` du monde (« entité en ligne du monde <fichier> », l'exception d'origine gardée comme cause) ;
  (4) sinon « fichier inconnu », écrit tel quel. Le proxy du monde cite le `FileName` du monde ; le contrôleur du joueur et le mode de
  jeu citent le `FileName` du `PlayerStartupSettings` ou du `GameplayModeAsset` acquis (le `.gameMode`) ; le view model de l'aperçu cite
  le fichier de données de conception qu'il lit. L'éditeur qui ouvre un monde sans DLL construite : la frontière qui
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
  puis initialisée avec un mauvais `script_class_name` — le message contient le chemin relatif de son asset ; (ii) une entité en ligne
  d'un monde chargé — le message nomme le fichier du monde ; (iii) un monde au mauvais `script_class_name` — son fichier ; (iv) un
  contrôleur au mauvais `player_controller_class` — le fichier `.gameMode` ; chaque message contient aussi la clé, la valeur, l'assembly
  consultée et l'indice ; (i) et (ii) sont rouges contre une version qui cherche le catalogue par `Id` ; un chargement (`Load`) sans assembly enregistrée ne lève pas ; aucune DLL à la création : message explicite) ; les tests
  existants touchés réécrits (types imbriqués sortis au premier niveau).
- Docs du moteur : les quatre docs du nom court, ADR-0075.
- Validation du moteur (AGENTS.md §6) : les deux `.sln`, `CasaEngine.Tests` construit explicitement et vert, RPGDemo et les démos lancés
  (preuve de démarrage).

**T0 de la partie parent** (commit de docs, avant tout code du moteur ou du parent). Annexe `docs/plan-restructuration-annexe/r0/` :
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
- DLL et tests : `nameof(X)` → `typeof(X).FullName!` là où le nom va dans `GameplayProxyClassName` (3 sites de la DLL, 65 lignes de tests) et
  dans les six tests d'écran ; un initialiseur de module d'`Alundra.Tests` enregistre l'assembly de la DLL ; un test garde-fou lit
  `Alundra.gameMode`, un `.entity` et un `.world` réels et compare leurs chaînes à `typeof(X).FullName`.
- Export 1 (protocole d'E20.e0x : prédiction par `predict_names.py`, manifeste avant, T1 où seul `report.json` change, l'export, comparaison
  à la prédiction, second export identique ; chemins autorisés : les 879 sorties, les 6 `design.json`, `report.json`) dans une fenêtre
  calme : aucun build ni test en cours, ni jeu ni éditeur ouverts (l'auteur les ferme).

**Acceptation.** L'annexe de T0 existe à un commit antérieur au premier commit de code ; `CasaEngine.Tests` vert et ses nouveaux cas
rouges sur le moteur d'avant (nommés) ; les deux `.sln` du moteur ; démos et RPGDemo démarrent ; `Alundra.Tests` Release et Debug, zéro
échec ; la suite du convertisseur verte et sa phase 8 `Verification: PASSED` sur l'export de R0 ; le `diff_manifests.py` versé, contre la
liste versée des chemins autorisés, sort 0 pour l'export 1, et seul `report.json` diffère à l'export 2 ; les SHA-1 égaux à la prédiction ; recette de l'auteur : nouvelle partie, les entités bougent (leurs proxies existent), F5, F6, F9.

**Arrêts.** Un appelant de la fabrique non recensé qui crée un objet script ; un besoin de garder le nom court ; un fichier exporté hors des
chemins autorisés qui change ; un test qui change de valeur attendue ; un démarrage de démo ou de RPGDemo qui échoue.

**Retour arrière.** Revert du commit parent (pointeur, convertisseur, DLL, tests) et réexport depuis le commit précédent ; la branche du
moteur reste non fusionnée.

## 5. Questions à l'auteur

- **Q-R-1** — Branche : la restructuration touche tous les fichiers. La faire sur une branche créée depuis `chantier/e20-heros` (qui
  contient tout E20, non fusionné dans `main`), ou fusionner d'abord `chantier/e20-heros` dans `main` (votre décision) et partir de `main` ?
  Recommandation : fusionner E20 d'abord, sinon toute reprise d'E20 sur l'ancienne arborescence entrera en conflit.
