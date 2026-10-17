# g2 (g2a lancer, g2b briser) : état de la DLL et des tests au HEAD `a3ccb759`

Branche `chantier/restructuration-dll-r3`. Lecture seule. Aucun build, test ni lancement .NET. Ont tourné : `git`, `rg`, `python -B` (lecture de `alundra-project/Data/sprite-records.json`, `tools/dll-structure/check_names.py --table` et `--tree --stage final` sur le HEAD, puis `--tree --root` sur une copie jetable de l'arbre dans le scratchpad où j'ai ajouté deux fichiers factices). `git status` inchangé (` m CasaEngineMonogame`, ` m alundra-datas-analyser`, déjà présents au départ). Le HEAD a avancé pendant la lecture (`c65d4183` puis `a3ccb759`) : trois commits `docs(plan)` (g3 READY, g4a révisions 1 et 2), aucun code. Étiquettes : FAIT (file:line ou sortie citée), INFÉRENCE, INCONNU.

Révisions de référence : `9ecc7d5d` (HEAD de la découverte du §1.24, 2026-10-15 21:07, numéros de ligne du plan) ; `2010ec38` (HEAD du relevé `e20-g1a/report.md` et `check.md`, 2026-10-17 01:54) ; `993d00ae` (dernier commit avant R2) ; HEAD. Entre `2010ec38` et `993d00ae`, un seul commit touche `Alundra/` : `146e1d81` (R0, noms complets, +18 lignes dans `WorldProxy`). Après `993d00ae`, `git log --since=2026-10-14 -- Alundra Alundra.Tests | grep -v "refactor(dll)"` ne rend plus que `146e1d81` et des commits d'avant : tout le reste est R2 à R4 (déplacements et renoms, prouvés par outil). R5 non exécutée : les tests gardent leur dossier plat et leurs noms `Alundra*`, mais leurs références aux types de la DLL sont renommées (ex. `PlayerManager.WiredWeaponTypesForTests`). Décalage constant : `PlayerManager.cs` = ancien + 13 lignes (en-tête).

## 0. Ce qu'il faut retenir

1. Toutes les affirmations « la DLL n'a pas X » de g2a/g2b sont encore vraies : bouchons, états absents, champs jamais écrits, `DestroyEntity` sans effacement du porteur, `E2` absent, aucune clé `(C,23)` ni `(E,2)`. Les numéros de ligne se traduisent tous (§1).
2. Deux affirmations de test sont fausses : le registre de production a **7** clés (f3c), pas 6, et **deux** tests épinglent le compte, pas un (`AlundraNativeSpriteRegistryTests.cs:55` et `AlundraWaveSwordLifeTests.cs:181-183`). L'épingle de f3c n'est dans aucune liste prédite de g1a/g2b.
3. Prérequis : e0k2 et e0b2 sont faites (CONFIRMED 2026-10-16). f4 (`NativeSpriteTypeFilterForTests`, D-F4-7), f5 (D-F5-5) et f6a sont ⏳ et **absentes du code** : (b3) (arène) et (b6) (eau profonde) ne peuvent pas être écrites avant f4 et f6a ; (v7) (X + 1,5 dès T) exige D-F5-5 ou la garde étroite d'une ligne que Q-G1A-4 propose à g2a par défaut.
4. La garde de vitesse de g2a (P-G-3, Q-G1A-4) : la seule règle présente vise `JoinsBirthTick && CurrentAnimationId == ~0u` (`ScriptedMotion.cs:173-176`). Une caisse par enregistrement n'a jamais `JoinsBirthTick` (INFÉRENCE : posé par l'interface et par `0x2D/0x8A/0x8B`). Une garde « marqueur seul » déplacerait l'épingle `P3` (`AlundraMapStateTests.cs:150`), absente de la liste prédite « aucun test déplacé » de g2a.
5. `HasLogicalZ` n'est écrit qu'en un seul endroit (`WorldProxy.cs:3573`, naissance par indice de sprite) et sa doc dit « jamais par les créations depuis un enregistrement » (`EntityScriptProxy.cs:1376-1383`). `PutInHands` (D-G-13) sera le premier écrivain hors interface, en cours de vie : les quatre branchements W1 à W4 n'ont, par construction, jamais tourné sur une entité née d'un enregistrement.
6. `NativeSpriteRegistry.Register` lève sur une clé déjà prise (`Ai/NativeSpriteRegistry.cs:62`) et `EventProgramRunner.NativeSprites` est en lecture seule, rempli par `CreateForProduction()` (`Events/EventProgramRunner.cs:51`) : les tests de g2b phase A « qui enregistrent eux-mêmes les clés » lèvent dès que la phase B enregistre `(E,2)` (idem `(C,23)` pour g1a). `HitArena` éteint tous les natifs (`AlundraHitArena.cs:70`).
7. Ordre (§1.28) : g1a, g2a, g2b, **g3, puis g1b**. g2a et g2b restent inertes en jeu jusqu'à g1b ; g3 (s5) consomme leurs sorties ; g4b exige g2a et g2b. `D-G-7`, `O-G-1`, la phase B de g1b et la clause « e0b2 » de D-G-14 sont caducs.
8. Tout fichier neuf (`LiftableAi.cs`, `CarryRules.cs`) fait échouer `check_names.py --tree --stage final` en `[hors table]` (vérifié) tant que Q-R-7 n'est pas tranchée ; les noms `LiftableAi` et `CarryRules` n'entrent en collision avec rien (vérifié).

## 1. Citations traduites

Fichiers : `Alundra/Scripts/AlundraPlayerManager.cs` = `Alundra/Hero/PlayerManager.cs` ; `AlundraEntityScriptProxy(.HitPass).cs` = `Alundra/Entities/EntityScriptProxy(.HitPass).cs` ; `AlundraWorldProxy.cs` = `Alundra/Maps/WorldProxy.cs` ; `AlundraScriptedMotion.cs` = `Alundra/Physics/ScriptedMotion.cs` ; `AlundraNativeSpriteRegistry.cs` = `Alundra/Ai/NativeSpriteRegistry.cs` ; `EntitySupport.cs` = `Alundra/Physics/EntitySupport.cs` ; `AlundraMovementObstacleProbe.cs` = `Alundra/Physics/MovementObstacleProbe.cs` ; `AlundraEntityCollision.cs` = `Alundra/Physics/EntityCollision.cs` ; `EntityFlags.cs` = `Alundra/Entities/EntityFlags.cs` (+ `EntityPickupKind.cs` coupé) ; `AlundraEventProgramRunner.cs` = `Alundra/Events/EventProgramRunner.cs` ; `AlundraFrameSyncPasses.cs` = `Alundra/Maps/Loop/FrameSyncPasses.cs` ; `AlundraHitPass.cs` = `Alundra/Combat/HitPass.cs` ; `AlundraTestSaves.cs` = `Alundra/SaveGames/TestSaves.cs` (source : `tools/dll-structure/table.json`, champ `final`). `AlundraLiftableAi.cs` et `AlundraCarryRules` n'existent pas (§5). Origine : P = plan §1.24 (état `9ecc7d5d`), R = `e20-g1a/report.md`, C = `check.md` (état `2010ec38`).

### 1.1 DLL

| Ancienne | HEAD | Statut |
|---|---|---|
| `AlundraPlayerManager.cs:927` `PlayerTryAction => 0` (P, R) | `PlayerManager.cs:939-940` | VRAI, bouchon intact |
| `:919` `TryUseItem` (R) | `:931-932`, seam de test `:935-936` | VRAI |
| `:1272` / `:1271-1274` `UpdatePlayerCarriedEntity` vide (P, R) | `:1284-1287` | VRAI |
| `:395` appel avec 0 (P, R) | `:408` | VRAI |
| `:646` appel avec 2, nage (P, R) | `:659` (doc `:651`) | VRAI |
| `:322-332` / `:317-335` branche verrouillée sans mode 1 (P, R) | `:330-345` (`if` `:335`, `Create(LockedMode)` `:337`, `EndOfMovePlayer` `:343`) | VRAI : aucun `UpdatePlayerCarriedEntity(1)` |
| `:419 :438 :743 :787 :848` sites de `PlayerTryAction` (R, C) | `:432 :451 :756 :800 :861` | VRAI |
| `:444` échelle « NOT PORTED » (R) | `:457` | VRAI |
| `:386` garde d'échelle `CarriedEntity == null` (R) | `:399` (commentaire `:388`) | VRAI |
| `:1013 :1017` `IsGroundAttackGroup`, `IsAirAttackGroup` (P, R) | `:1026 :1030` | VRAI ; 0x09 et 0x0A y sont, rien ne les affecte |
| `:1269` `SpawnDirections` (R) | `:1282` (privé ; lu en `:1204` par `SpawnWeaponProjectile`) | VRAI |
| `:2168` `GetNumberOfItem` (R) | `:2181` | VRAI |
| `NoAttackVramFlag`, `NoJumpVramFlag` (P, D-G-5) | `:943` (usage `:963`), `:110` (usage `:1536`) | existent, privés |
| `ProductionWiredWeaponTypes` (D-G-8, modèle) | `:1226` = `{27, 28, 29}` ; `UnhandledWeaponSpriteCodes` `:1240` | CHANGÉ : plus vide (f3c) ; le contrôleur ne lit que la page globale (`TryGetPrefab(false, …)`) |
| `AlundraEntityScriptProxy.cs:88-89` / `:88-92` `PlatformEntity`, `CarriedEntity`, `RelativeWarpOffset*` (P, R) | `EntityScriptProxy.cs:101-105` | VRAI : jamais écrits (seule la copie `Clone` `:2609-2613`, ancien `:2596-2600`) ; `RelativeWarpOffset*` sans lecteur non plus |
| `:2355-2362` / `:2411-2416` écrivain de `XCollisionEntity` (P, R) | `:2424-2431` | VRAI |
| `:483` / `:489` `LogicContextEntity = null!` (P, R) | `:502` | VRAI ; affecté en `WorldProxy.cs:2108`, `EntitySpawnFactory.cs:535 :653` |
| `:1370` `HasLogicalZ` (R) | `:1383` | VRAI, mais un seul écrivain `WorldProxy.cs:3573` |
| `TickMotion` (D-G-3 ; e0k `:1550-1571`) | `:1614` (`TickPrograms` `:1573`, `TickPhysics` `:1602`) | DÉPLACÉ |
| `AlundraWorldProxy.cs:3240` / `:3267` `DestroyEntity(e, fx)` (P, R) | `WorldProxy.cs:3301` ; bouchons nommés (1) butin `:3303`, (4) effet attaché `:3307`, (6) porteur `:3315` ; boucle `RecycleDestroyedEntities` `:3265` (ancien `:3231`) | VRAI |
| `:3254` / `:3295` `DestroyEntity(e)` (P, R) | `:3329` (doc `:3320-3328`, « does not port … `PlatformEntity.CarriedEntity` cleanup » `:3324`, ancien `:3290`) | VRAI |
| `:2860-2920` ordre du tick (P) | `:2905-2985` `RunEntityBlock` : MovePlayer, programmes, `RunCountersPass` `:3000`, listes, horloge, physique, coups | VRAI |
| `:2930` commentaire « once per frame » (P) | `:2994-2995` | VRAI, toujours périmé |
| `:3553` commentaire « once per frame » (P) | `:3675-3676` | VRAI, toujours périmé |
| `EntitySupport.cs:44-59` / `:72-75` (R) | `:45-61` (filtre `FlagToDestroy` `:55`) / `:73-76` | VRAI |
| `AlundraMovementObstacleProbe.cs:48-49` (P, R) | `MovementObstacleProbe.cs:48-51` | VRAI (g3 le retire) |
| `EntityFlags.cs:274-290` `EntityPickupKind` (P, R) | `Entities/EntityPickupKind.cs:5-26` | DÉPLACÉ (coupé par R2) ; doc toujours inversée pour les genres 2 et 3 |
| `AlundraScriptedMotion.cs:165-170` vitesse lit `CurrentAnimationId` (R) | `ScriptedMotion.cs:171-176` | VRAI (nuance JoinsBirthTick, §2) |
| `:671-675` le héros hérite du déplacement de son porteur (R) | `:677-681` | VRAI |
| `:614-632` fiches de vitesse (R) | `:612-636` | VRAI |
| `AlundraNativeSpriteRegistry.cs` `CreateForProduction` `:29` (R) | `Ai/NativeSpriteRegistry.cs:36-44` | CHANGÉ : Beannoïde, Muruta, onde d'épée = 7 clés |
| `AlundraLiftableAi.cs` (P, D-G-9) | ABSENT | à créer (§5) |
| `StepAnimationClock` (P) | `FrameSyncPasses.cs:240` (`TakeZImpulse` `:325`) | VRAI |
| `BoxDirection` (D-G-10) | `EntityScriptProxy.HitPass.cs:39` (écrit `:111`) | VRAI |

### 1.2 Tests

| Ancienne | HEAD | Statut |
|---|---|---|
| `AlundraNativeSpriteRegistryTests.cs:51` `…RegistryHoldsTheSixHandlers…` `Count == 6` (P) ; R : `:51` | `:51` `ProductionRunner_RegistryHoldsTheSevenHandlersOfTheBeannoidTheMurutaAndTheSwordWave_AndTheSwitchIsOn`, assertion `:55` `6 + 1` | CHANGÉ (renommé, 7) |
| (inconnu du plan) | `AlundraWaveSwordLifeTests.cs:167` `V3_TheProductionRegistryHoldsSevenKeys_…`, assertion `:181-183` | NOUVEAU |
| `AlundraLadderClimbTests.cs:660-669` `CaseSixGate_CarryingEntity_DoesNotEnterClimbing` (R) | `:666-675` (`CarriedEntity = new Entity()` `:669`) ; `:242`, `:688` | DÉPLACÉ |
| `AlundraBeannoideOracleTests.cs:76`, `:351-359`, `:18` (R) | `:85` (`platform_28`), `:360-368` (X6 `Assert.Same(platformBefore, …)`), doc `:27` | DÉPLACÉ |
| `AlundraMurutaOracleTests.cs:73`, `:429-437` (R) | `:84`, `:440-448` | DÉPLACÉ |
| `AlundraNpcCharacterControllerMoverTests.cs:2210` (R) | `:2217` `playerProxy.PlatformEntity = sailorProxy.LogicContextEntity` | DÉPLACÉ |
| `AlundraDestroyEntityBreakEffectTests.cs:12` (R) | doc `:17` ; `:205` `TheDestruction_DrawsNoNumber_AndCreatesNoEntity_EvenWithContents` | DÉPLACÉ |
| `AlundraTestSavesTests.cs:85-87` (R) | `:88-91` `TheNinePresets_AreListed_InTheOrderOfTheTable` | DÉPLACÉ (g1b) |
| `AlundraPlayerManagerTests.cs:96` (R) | `:111-113` (état 0x20 non porté) | DÉPLACÉ |
| `AlundraMovementObstacleProbeTests.cs:130`, `AlundraEntityContactArcTests.cs:480`, `AlundraDialogueOpcodesProductionTests.cs:382` (R, g3) | `:126-134` ; `:478-485` ; `:383-388` | DÉPLACÉS (g3) |
| 7 fichiers « crate » (R) : `AlundraHeroApexAndCeilingTests`, `AlundraLogicalZStepTests`, `AlundraAbsoluteZWritesTests`, `AlundraMovementObstacleProbeTests`, `AlundraLogicalZWorldTests`, `AlundraHeroObjectTopsTests`, `AlundraShipCorridorArcTests` | tous présents dans `Alundra.Tests/` | VRAI |
| bancs `ArcRun`, `ContactWorld`, `HitArena`, `JumpHeroRig` (P, g1a) | `AlundraArcSupport.cs:94`, `AlundraContactTestSupport.cs:128`, `AlundraHitArena.cs:27`, `AlundraJumpTestSupport.cs:235` | VRAI |

Les citations de `check.md` (§2) et de `report.md` (§4, §6, §8) sont toutes dans les deux tables. Les chemins `Alundra/Scripts/...` du plan et des annexes n'existent plus (`ls Alundra/Scripts` : absent).

## 2. Affirmations sur la DLL

### 2.1 Toujours vraies (preuve au HEAD)

- Bouchons `PlayerTryAction`, `UpdatePlayerCarriedEntity`, mode 1 absent du verrou (§1.1). Aucun test ne nomme `PlayerTryAction`, `UpdatePlayerCarriedEntity`, `TryLift`, `PutInHands` (`rg` dans `Alundra.Tests` : 0 occurrence). `CarriedEntity` n'apparaît en test que dans `AlundraLadderClimbTests.cs:242 :669 :688`.
- États `0x05` à `0x0C`, `0x2E` à `0x30` sans constante ni cas dans `PlayerManager.cs` (constantes de `:92` à `:222`) ; `0x09` et `0x0A` seulement dans les deux groupes d'attaque (`:1026-1031`) ; aucune affectation `TargetAnimationId = 9/0x0A` dans `Alundra/`.
- `PlatformEntity` : 10 lignes de test dans 5 fichiers (Beannoïde 3, Muruta 3, `AlundraEntityCollisionTests.cs:98`, `AlundraNpcCharacterControllerMoverTests.cs:2217`, `EntitySearchServiceTests.cs:138 :247`) : « dix sites » de D-G-15 exact. `CarriedEntity` : 3 lignes dans un fichier. Les projections `["platform_28"]` (`new Entity { Name = "platform" }`, sans proxy) et l'épingle X6 sont intactes (D-G-11).
- `IsEligibleSubject` exclut `PlatformEntity != null` (`EntitySupport.cs:73-76`). `UpdateRidingEntities` ne boucle que sur les collidables (`:277-289`) : le `RidingEntity` d'un objet porté ou lancé n'est jamais recalculé (D-G-12 reste nécessaire).
- Chaîne de destruction : `DestroyEntity(e, fx)` écrit `FlagToDestroy`, `EventTrigger = -1`, l'effet de bris (`BreakEffect`, champ `:154`), mais le butin (1), l'effet attaché (4) et le porteur (6) sont des bouchons nommés (`WorldProxy.cs:3303-3315`). `IScriptHost.DestroyEntity(e, fx)` (`IScriptHost.cs:48`) existe ; `IEntityWorldContext` n'a que la version à un argument (`:90`) : E2 doit passer par `entity.ScriptHost` comme le fait la voie en ligne (`EventProgramRunner.cs:290`).
- Le choix du programme E existe (`EntityScriptProxy.cs:1718-1722` : `0x10` et `ForceAdjusted/IsOnGround`, `0x20` et `HitCounter`, `0x40` et `ForceResetAnimationFlag`) ; pente `DestroyOnSlidingSlope` `:1708-1711`, `DestroyOnVramFlags` `:1713-1716` (lit `CombinedVramFlagsOR & 0x8004`).
- Sondes de pente et de drapeaux de tuile : héros seulement (appel `RunHeroProbes()` `:1621`, définition `:1642` ; doc D-T-12 `:2031-2040`). Pour un non-héros, `Slope_18c`, `CombinedVramFlagsOR/AND` valent 0 : les drapeaux `0x100000` et `0x200000` des caisses (`0x3323E4`) ne déclenchent rien (O-G-2 ouvert). `CombinedVramFlagsAND` n'a pour lecteur que `PortalTrigger.cs:68`.
- Vitesse d'un non-héros : clé `CurrentAnimationId` (`ScriptedMotion.cs:171-176`). Impulsion Z de la fiche cible prise au tick du changement par `StepAnimationClock` (`FrameSyncPasses.cs:240-345`). `RunLogicalZPreXyStep` exige un contrôleur et une racine (`ScriptedMotion.cs:451-471`).
- `ForceResetAnimationFlag` est bien le `+0xAC` (commentaire `PlayerManager.cs:722`) ; posé à 1 par une fin de Hold (`EntitySpawnFactory.cs:325`). INCONNU : que l'animation 2 de la caisse finisse en Hold (non lu dans l'export).
- Données (`alundra-project/Data/sprite-records.json`, relues) : 17 en-têtes soulevables ; `ProgramTick == 23` sur 7 (banques 35, 36, 38, 41, 48, 182, `alundra_234`) ; `ProgramDeactivate == 2` sur 10 (les 7 de C23, plus les banques 34, 37 et 117, dont le C est 4) ; `BreakEffect` 10 sauf la banque 41 (2) et 234 (11) ; `Contents` 130 pour la caisse (35, 36), 247 (41), 255 (234) ; A, D, F à 0 ; fiches de la caisse : animations 1, 3, 4 à vitesse 256, 128, 384 et impulsion 1024, animation 2 à vitesse 0 (son 27) ; héros : animations 9 (vitesse 0, son 26) et 10 (accélération 15) présentes. Drapeaux `0x3323E4` (jarre `0x2323E4`) : `0x2000 NoObstacleSlide`, `0x20`, `0x40`, `0x80`, `0x100`, `0x100000`, `0x200000` ; ni `0x10` ni bit de classe.
- Le héros du monde suivant est une pièce neuve du moteur (`WorldProxy.cs:2087-2113` `playerController.Pawn`) et `AdoptPlayerPawn` ne copie pas `CarriedEntity` : INFÉRENCE pour Q-G1A-6 (non exécuté).
- `EntitySearchService` type 11 renvoie toute entité à `PlatformEntity != null` (`:249`), comme le binaire : une caisse portée sera trouvée (doc « riding something » trompeuse).

### 2.2 Fausses, changées ou à corriger

| Affirmation | Au HEAD |
|---|---|
| « Le registre épingle `Count == 6` » (P) ; « 7 → 8 → 9 » (R §6) | 7 clés ; deux épingles (§3). e0c ajoute deux clés (`C255`, `A255`) : « de 7 à 9 » (plan §1.26) ; l'ordre e0c/g1a/g2b décide des nombres, donc dérivation (D-G-17) |
| « Les tests enregistrent les clés eux-mêmes dans le registre du runner de test » (g2b phase A) | Vrai seulement avant la phase B : `Register` lève sur doublon (`NativeSpriteRegistry.cs:62`, test `Registering_TheSameKeyTwice_Throws…` `AlundraNativeSpriteRegistryTests.cs:239`) ; `NativeSprites` est `{ get; } = CreateForProduction()` |
| « E2 lit déjà `CombinedVramFlagsAND` » (D-G-14) | E2 n'existe pas ; le lecteur existant est le choix de programme sur `…OR & 0x8004` ; l'AND vaut 0 pour tout non-héros, la branche `+0x184 & 4` de E2 sera morte |
| « (E,0) des quatre objets sera nécessaire en g2b/g2c » (C §3) | E0, E1 et E10 sont traités en ligne par le runner (`EventProgramRunner.cs:284-290`, `IsInlineDeactivateCode` `:328` = `(uint)code <= 1 || code == 10`) : aucune clé de registre pour E0 |
| « La garde de D-F5-5 couvrira l'entité marquée par g1 » (P-G-3) | D-F5-5 n'est pas écrite (f5 ⏳). La règle en place ne vise que `JoinsBirthTick` (`ScriptedMotion.cs:173-176`) |
| « e0b2 : arête souple, peut être retirée » (D-G-14, ligne 6299) | Caduque : e0b2 est faite et CONFIRMED le 2026-10-16 (plan §1.17 titre). Elle écrit `ContentsItemId`/`ContentsGameFlag` à chaque création et avance le flux réel (`WorldProxy.cs:1119 :3440 :3566`, `:3469-3478`) |
| (b3) « arène : caisses vivantes, Beannoïde inerte, D-F4-7 » (g2b) | D-F4-7 n'est pas écrite (aucun `NativeSpriteTypeFilterForTests` dans `Alundra/`) ; `HitArena` éteint tous les natifs (`AlundraHitArena.cs:70`) : ni C23 ni E2 ne tournent dans l'arène existante |
| Commentaires périmés après g1a | `EntitySupport.cs:10-11 :36-37 :68-70` (« always null »), `AlundraTerrainHeightTests.cs:33-34`, `PlayerManager.cs:42 :75 :330 :651 :939 :1284 :1516` (« named stub, E20.g »), `WorldProxy.cs:3299 :3324`, `AlundraDestroyEntityBreakEffectTests.cs:17`, `AlundraBeannoideOracleTests.cs:27`, `EntityPickupKind.cs:12-24` |

### 2.3 Commits fonctionnels depuis `9ecc7d5d` (hors R2 à R4) et effet sur g2a/g2b

| Commits | Effet sur ce que supposent g2a/g2b |
|---|---|
| e0k1 (`b4f3938e`, `81b68b5f`) | aucun (entités nées par l'interface) |
| e0k2 (`181954f0`, `82a979da`, `2fb42f9c`) | prérequis rempli : `HasLogicalZ`, W1 `ScriptedMotion.cs:211-212`, W2 `EntityScriptProxy.cs:793-800 :850-852`, W3 `:1479-1485`, W4 `:1395 :2381-2384`, `GameplayFreeze.cs:141-142`. Un seul écrivain du marqueur |
| e0k3 (`65f20873`, `bef323b4`) | le pas d'un objet marqué est borné au bord de carte (`ScriptedMotion.cs:683-691`) : s'applique à l'objet lancé |
| e0a5 (`71b209d0`, `0630f054`), e0a4 (`683b9dcf`, `0ff26e4a`, `bda82292`) | `SwitchedAtCreation`, `JoinsBirthTick` pour les naissances par script ; pas de caisse de carte concernée (73 soulevables ne naissent qu'au script, g3 `critic-complet` Réfuté 6) |
| f3b (`5cfac5b8`, `2f16e610`) | effets de coup 4 et 5 attachés à la cible : les attendus de la touche (b3) sont dérivés (D-E0B-8) |
| f3c (`073de215` à `d56f0b03`) | registre à 7 clés, E10 en ligne (modèle de l'invariant), `ProductionWiredWeaponTypes = {27,28,29}`, nouvelle épingle V3 |
| e0b2 (`4753357c`, `b5b2c888`, `a8d1b8d5`) | prérequis rempli ; le butin de toute caisse est tiré à la création sur le flux réel ; rien ne tombe (bouchon (1)) ; `ArcRun` ré-ensemence (`AlundraArcSupport.cs:255-261`) |
| R0 (`146e1d81`) | +18 lignes dans `WorldProxy`, aucun effet |

### 2.4 Décisions, points ouverts, questions de la portée

- D-G-1 : partiellement remplacée (g1b après g3, §4). D-G-2, D-G-10, D-G-11, D-G-12, D-G-15, D-G-17 : tiennent (D-G-10 : l'épingle `AlundraShotDirectionTests.cs:162` `D4_TheBoxDirection_EqualsTheFacingOfTheHeadOfTheTick_AtTheTickOfTheShot` établit déjà que `BoxDirection` est le regard du tick précédent pour le tir). D-G-9 : noms à traduire (§5). D-G-13 : tient, mais premier écrivain du marqueur hors interface. D-G-14 : la partie e0b2 est caduque, la partie O-E0K-2 reste à f6a (T0, f6a ⏳). D-G-16 : O-G-1 caduc.
- O-G-1 caduc (g3). O-G-2 ouvert (f6a). O-G-3 ouvert (e0c ⏳, bouchon `WorldProxy.cs:3303`). O-G-4 ouvert (E20.c2b et E18 ⏳). O-G-5 ouvert (D5b en pause). O-G-6, O-G-8, O-G-9, O-G-10 ouverts (g2c, recette).
- Q-G1A-1 : défaut A tient pour les caisses ; l'enveloppe place h3 (ligne 11) avant g3 (ligne 12) : les graines et la bombe seraient portées et lancées avant les caisses (g1a, g2a seuls), INFÉRENCE sur l'ordre. Q-G1A-2 ouverte (c2b, E18 ⏳). Q-G1A-3 : les gestionnaires D portés sont Beannoïde 335 et Muruta 347 seulement (`Ai/NativeSpriteRegistry.cs:36-44`) : tient. Q-G1A-4 ouverte (f5 ⏳, contrainte P3). Q-G1A-5 : g1b/T0. Q-G1A-6 : voir 2.1.

## 3. Tests qui épinglent

Rien dans `Alundra.Tests` ne nomme les bouchons. Ce que g2a/g2b déplacent ou frôlent :

| Test (HEAD) | Pin | Tranche |
|---|---|---|
| `AlundraNativeSpriteRegistryTests.cs:51-55` `ProductionRunner_RegistryHoldsTheSeven…` | `Count == 6 + 1` | g1a phase C (+1), g2b phase B (+1), e0c (+2) |
| `AlundraWaveSwordLifeTests.cs:167`, `:181-183` `V3_TheProductionRegistryHoldsSevenKeys_…` | `(3 + 3 + 1, …)` ; l'invariant `UnhandledWeaponSpriteCodes` | idem ; **absent des listes prédites du plan** |
| `AlundraMapStateTests.cs:150` `P3_AMarkedEntityWhoseFirstAnimationIsValidated_KeysItsKinematicsOnItsCurrentAnimation_NotOnAnAskedOne` (classe `:131`) | une entité marquée née par l'interface garde la clé de son animation courante | g2a (garde de vitesse) ; D-F5-5 la réécrit |
| `AlundraRecordBirthTickTests.cs:364` `B6_R_TheKeyOfTheKinematics…` | clé `~0u` des naissances par script | g2a (doit rester vert) |
| `AlundraBirthTickProbePaTests.cs:218 :226` (PA3), appel `:202` ; `AlundraBirthTickTests.cs:458` | `TickScriptedNpc` sur entités nées par l'interface | g2a (garde) |
| `AlundraShotDirectionTests.cs:101 :118 :136 :162` | direction du tir par `BoxDirection` / `SpawnDirections` | g2a (réutilise la table) |
| `AlundraWeaponSpawnSeamTests.cs:238` `S5_TheProductionSetPassesTheInvariant…` | `ProductionWiredWeaponTypes` et invariant de fermeture | modèle de D-G-8 |
| `AlundraEntityDestructionTests.cs:95` `TR5_NativeEOfIndexZeroAndOne_DestroysTheEntity`, `:111` `TR5_NativeEOfAnotherIndex_DoesNotDestroy` (données 2, 3, 255, −1) | E0/E1 en ligne ; l'index 2 ne détruit pas (type −1 : reste vert après `(E,2)`) | g2b |
| `AlundraEntityDestructionTests.cs:30` `TR2_BuildCollidables_DropsAnEntityFlaggedForDestruction_AndKeepsTheOthers` | le filtre `FlagToDestroy` dont dépend la sortie immédiate de la caisse brisée | g2b, g3 (garde le filtre, O-G3-1) |
| `AlundraDestroyEntityBreakEffectTests.cs:205` `TheDestruction_DrawsNoNumber_AndCreatesNoEntity_EvenWithContents` ; doc `:17` | bouchon du butin et du porteur | g2b (b5), e0c |
| `AlundraBeannoideOracleTests.cs:360-370`, `AlundraMurutaOracleTests.cs:440-450` | `Assert.Same(platformBefore, entity.PlatformEntity)` (X6), `Assert.Empty(SpawnedEntities)` (C7) ; porteur sans proxy | g1a W5 (D-G-11) |
| `AlundraLadderClimbTests.cs:666-675` | `Entity` nu dans `CarriedEntity`, `MovePlayer` sans hôte | g1a (NRE possible), état Idle de g2a |
| `AlundraNativeSpriteRegistryTests.cs:239-250` | doublon de clé lève ; compte relatif | g2b (tests de phase A) |
| `AlundraHitArena.cs:70`, `AlundraWaveSwordLifeTests.cs:128 :147` | natifs éteints / rallumés à la main | g2b (b3) |
| `AlundraFlailBounceTests.cs:431-458` | les états `0x05 0x06 0x0A 0x0B 0x2E 0x2F 0x30` dans les listes de la passe d'effets | vert (états passés directement) |
| `AlundraArcSupport.cs:332`, `:342`, `:354-368` | gardes `ArcsWithoutEntityContact` et `ArcsWithoutSlide` (tout arc `RealController` doit finir à `TotalSlideCount == ArcsThatSlide[nom]`, 0 par défaut, toutes entités comprises) | g2a/g2b : la caisse a `0x2000` (pas de glissement), le héros d'un test qui longe un coin compterait |

## 4. Conséquences de g3 et nouveautés

1. **Ordre** : g1a, g2a, g2b, g3, g1b (§1.28 « Prérequis » ; enveloppe ligne 12). §1.24 (lignes 6296, 6299, 6368, 6534) dit encore g1b avant g3 et « g3 exige g1b » : inversé. Dépendants de g2a/g2b à ajouter : g3 (test s5 : une caisse portée et une caisse lancée ne bloquent pas), g4b (lancer vers l'ouest, la caisse se brise, `bin/out/a3_r3b.txt`). Recommandation du critique : une seule branche et une seule fusion pour g1a, g2a, g2b, g3 + g4a, g1b + g4b.
2. **Inertie** : D-E20-39 et §1.28 confirment qu'aucun chemin de production ne met un objet dans les mains avant g1b (après g3) ; (p1) de g2b et tous les tests de g2a passent par `PutInHands` appelé par le test. L'effet visible de g2a vient plus tôt par h3 (graines, bombe) si l'ordre de l'enveloppe est tenu.
3. **Sorties que g3 consomme** : caisse lancée = `0x80` effacé, `PlatformEntity` nul, `RidingEntity` nul (D-G-12) ; caisse brisée = hors de la liste dès `FlagToDestroy` (`EntitySupport.cs:55`, gardé par g3). À mettre en critère d'acceptation de g2a/g2b. Les tests de monde de g1a/g2a/g2b où le héros marche près de vraies caisses entrent dans la « liste prédite » de g3 (T0 d) ; préférer héros immobile ou bancs sans bits de levage.
4. **D-G-7 caduc** : `LiftContactEntity`, `FindLiftContact`, `requiredCandidateFlags`, phase B et compteurs k1 à k4 de g1b, O-G-1, ligne « Soulever | g1a, g1b » du tableau (6277), Q-G1 : à retirer. Aucun de ces éléments n'est dans g2a/g2b.
5. **`PlatformEntity` et contact** : g3 confirme que `PlatformEntity` est le `+0x28`, jamais écrit (critic-recon M3) ; g1a le pose. Le contact `XCollisionEntity` (premier candidat de la liste, `EntityScriptProxy.cs:2424-2431`) ne concerne pas g2a/g2b (la touche de E2 passe par `TouchingEntity`/`HitCounter`, `:498-499`, remis à zéro par `RunCountersPass` `WorldProxy.cs:3000`). Une caisse lancée n'est pas sujet (`0x80` effacé, `IsEligibleSubject`) : pas de contact d'entité, (v9) tient.
6. **Commentaires** : `EntitySupport.cs:10-11 :36-37 :68-70` deviennent faux à g1a, mais g3 les met à son périmètre ; `WorldProxy.cs:2994-2995` est dans le périmètre de g3 et dans les « corrections induites » de §1.24 : une seule tranche doit les prendre (celle qui touche le fichier en premier, g1a).
7. **Garde des fichiers neufs (Q-R-7, prérequis de g3)** : s'applique déjà à g1a et g2b (voir §5).
8. **Nouveautés du HEAD que la tranche ne connaissait pas** : points 3 à 6 du §0 ; le fait que `EventProgramRunner.cs:284-290` traite E0/E1/E10 en ligne ; `UnhandledWeaponSpriteCodes` ne sait lire que la page globale (D-G-8 demande les pages de carte) ; e0c prévoit de brancher le bouchon (1) de `DestroyEntity` et d'ajouter deux clés (plan §1.26) : si e0c passe avant g2b, (b5) et le test `:205` basculent ; la nouvelle convention de g4a pour un fichier de tests neuf (`Alundra.Tests/AlundraShipBarricadeArcTests.cs`, « selon la table de R5 si R5 est passée »).

## 5. Noms nouveaux

| Plan | Nom final (ADR-0075 : pas de préfixe `Alundra`, dossier = namespace) | Contrôle |
|---|---|---|
| `AlundraLiftableAi` dans `Alundra/Scripts/AlundraLiftableAi.cs` (D-G-9, g1a phase A pour C23, g2b phase A pour E2 et `Register`) | `LiftableAi`, `Alundra/Ai/Objects/LiftableAi.cs`, `namespace Alundra.Ai.Objects`, `internal static class` avec `Register(NativeSpriteRegistry)` (modèle `Ai/Projectiles/WaveSwordAi.cs:30`) ; précédent du dossier : `Ai/Objects/SaveBook.cs` | libre en DLL, moteur, MGUI, paquets, BCL (`scan_types.external_types()` + `collision_findings`, 0 résultat) ; aucun segment de namespace de ce nom |
| `AlundraCarryRules` (g1a phase A ; `ThrowCarried` de g2a en est une fonction pure) | `CarryRules`, `Alundra/Hero/CarryRules.cs`, `namespace Alundra.Hero` ; `CarryRules.ThrowCarried` | libre (même contrôle) |
| `PutInHands`, `TryLift`, `PlayerTryAction`, `ThrowCarried`, `ProductionWiredLiftableTypes`, `WiredLiftableTypesForTests` | membres de `PlayerManager` ou de `CarryRules`, pas de type nouveau | rien à contrôler |
| `LiftContactEntity`, `FindLiftContact`, `requiredCandidateFlags` | à ne pas créer (D-G-7 caduc) | - |
| `AlundraEventProgramRunner.NativeSpriteTypeFilterForTests` (D-F4-7, f4) | `EventProgramRunner.NativeSpriteTypeFilterForTests` (membre) | - |
| fichiers de tests de g2a/g2b (non nommés par le plan) | convention du HEAD : `Alundra.Tests/Alundra<Sujet>Tests.cs` à plat, sujet à choisir ; chemin et nom selon la table de R5 si R5 est passée (Q-R-3 ouverte) | - |
| annexe `docs/plan-e20-annexe/e20-g/` (T0 commun) | inchangé (n'existe pas ; `e20-g1a` et `e20-g3` existent) | - |
| ADR de g1a/g2a/g2b | prochain numéro libre au HEAD : 0076 (`docs/decisions` s'arrête à 0075) | à revérifier à l'écriture |

Preuve de la garde : sur une copie de l'arbre contenant `Alundra/Ai/Objects/LiftableAi.cs` et `Alundra/Hero/CarryRules.cs`, `check_names.py --tree --stage final --root <copie>` rend `[hors table] file … is not in the table at stage final` pour les deux, sans `[collision]` (2 findings). Sur le HEAD réel : `--table` et `--tree --stage final` rendent 0 finding, 1 avertissement (`TextBoxGpuHarness.cs:265`) ; `--table` ne lit que la table et ne peut pas tester un nom neuf : le contrôle de collision des deux noms proposés a été fait avec les fonctions de l'outil (`scan_types.external_types()` et `check_names.collision_findings`) sur les mêmes sources. `table.json` est gelée à `5434cf6` (« do not edit ») ; Q-R-7 propose un mode `--rules` (tranche R7) qui n'existe pas.

## 6. Inconnus

1. Aucun test ni build lancé : tout comportement de la DLL ici est lu dans le code.
2. Comportement de W1 à W4 d'e0k2 sur une caisse née d'un enregistrement marquée en cours de vie (contrôleur déjà posé, `LogicalZStepHeight` capturé au premier pas logique, `Controller.Teleport`) : sondes P-G-4 et P-G-5 du plan, non faites.
3. `BoxDirection` au tick du lancer (P-G-2) dans les états de portage : l'épingle `D4` de f3a établit la règle pour le tir seulement.
4. Discriminateur de la garde de vitesse de g2a qui laisse `P3` vert : à choisir (INFÉRENCE : un état « relâché », par exemple `+0x24 != 0` ou la rupture du lien, plutôt que le marqueur).
5. Forme finale de D-F4-7, D-F5-5 et de f6a (⏳), donc des points « à relire à l'exécution » (sortie de f6a, interrupteur d'arène).
6. Fin de l'animation 2 de la caisse par Hold (E2 `+0xAC == 1`) dans l'export ; chronologie T+22 à T+26 dans la DLL.
7. Ordre d'e0c par rapport à g2b (e0c est hors de l'enveloppe ordonnée, §1.26 « exécutable après la fin de la restructuration »).
8. Recette de g3 : « sur la 392 et sur la 14 » (§1.28 Acceptation) contre `e20-g3/dll/report.md` B3.3 (« 14 … 0 soulevable ») : carte 14 sans caisse, ou autre carte visée.
9. Résultat de Q-R-3 (noms et dossiers des tests) et de Q-R-7 (garde des fichiers neufs) : décident du nom des fichiers de tests et de la garde.
10. Valeur exacte de plusieurs propositions de `report.md` §8 non reprises dans le plan : (c7)/(c8) 950 et 700 vecteurs (`work/lst/goldens_g1a.json`, `goldens_carry_group.json`), (w13)-(w17), (v10)-(v12) (touche d'une cible de classe A par une caisse lancée, aussi demandée par (b3) de g2b) : le plan §1.24.1/§1.24.2 ne les contient pas.
