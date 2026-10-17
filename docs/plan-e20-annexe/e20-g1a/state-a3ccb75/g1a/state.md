# E20.g1a (plan 1.24.0 et 1.24.1) : état de la DLL et des tests au HEAD, lecture seule

## 0. Base, méthode, verdict court

- Dépôt `D:/development/repo/alundra-casaengine-project-converter`, branche `chantier/restructuration-dll-r3`. HEAD lu : `a3ccb759`. La session a démarré à `c65d4183` ; trois commits de docs sont arrivés pendant la lecture (`f841bed3`, `d0691b68`, `a3ccb759`) : ils ne touchent que le titre de 1.28 (g3 READY) et 1.29 (g4a). `git diff c65d4183..HEAD -- Alundra Alundra.Tests` est vide. §1.24 est identique à `c65d4183`.
- Bases des numéros cités. Plan : WorldProxy et EntityScriptProxy = `9ecc7d5` (2026-10-15 21:07) ; PlayerManager a les mêmes numéros à `9ecc7d5` et `2010ec3`. `report.md` et `check.md` : `2010ec3` (2026-10-17 01:54). Evidence g3 : `c84fd72` à `b6a2f92`. Entre `2010ec3` et HEAD, un seul commit de code hors restructuration : `146e1d81` (R0 parent : noms complets, `catch (ScriptTypeResolutionException)` dans WorldProxy).
- Restructuration pure (vérifiée, pas supposée) : comparaison de `146e1d81` à HEAD sur 10 fichiers (PlayerManager, EntityScriptProxy, WorldProxy, EntitySupport, MovementObstacleProbe, NativeSpriteRegistry, FrameSyncPasses, ScriptedMotion, EntityDepthKey, EntityFlags), commentaires, `using`, `namespace` et qualificateurs retirés, types renommés par `table.json` : 0 ligne de code différente, sauf WorldProxy (38 lignes : seulement des préfixes `Audio.`, `Dialogues.`, `Transitions.`, `Alundra.Entities.` devant un nom homonyme d'une propriété) et EntityFlags (l'enum `EntityPickupKind` est sortie dans son fichier). Scripts : `.../g1-state/g1a/purediff.py`, `verify_lines.py`.
- Décalages : PlayerManager +13 (lignes `using`) ; EntityScriptProxy +13 avant `:94` puis +19, plus les ajouts d'e0k2/e0b2 ; ScriptedMotion +6 ; EntitySupport +1 ; WorldProxy variable. Les 108 numéros de ligne HEAD de ce rapport ont été relus par script (une correction : l'appel de `RunOneMotionTick` est en `ScriptedMotion.cs:175`).

Verdict court :
1. Tout ce que la tranche dit que la DLL n'a pas (bouchons, champs jamais écrits, pas d'appel du mode 1, états sans cas, `DestroyEntity` sans effacement du porteur, aucune clé C23) est encore vrai. Seuls changent chemins, noms et numéros.
2. Périmé par des commits d'avant la restructuration : le registre a 7 clés (pas 6) et son test est renommé ; `ProductionWiredWeaponTypes` n'est plus vide (`{27, 28, 29}`) ; le marqueur `HasLogicalZ` (e0k2) a 9 lecteurs et sa doc dit « posé par l'interface seule », ce que D-G-13 contredit.
3. Absents du plan : `EntitySearchService` lit `PlatformEntity` (cases 4 et 11) ; `GameplayFreeze` lit le marqueur ; `PushLogicalPositionToRoot` (D-G-3) appelle aussi `ClampToGround()` et `Controller.Teleport` ; `EntityDepthKey` ne sert qu'aux ancres d'effets ; un test d'échelle met un `CarriedEntity` nu (risque de NullReference dans le corps de W1).
4. g3 inverse l'arête g1b/g3 ; D-G-7 est caduc ; le code de g1a ne change pas, mais l'ordre, le tableau de découpage, « Dépendants » et l'acceptation doivent être récrits.
5. Prérequis non écrits dans 1.24.1 : fin du chantier de restructuration (R5 attend Q-R-3) et garde des fichiers neufs (Q-R-7). Sans elle, tout fichier neuf est `[hors table]`.
6. Noms neufs : `LiftableAi` (`Alundra.Ai.Objects`) et `CarryRules` (`Alundra.Hero`) : 0 collision (`check_names.py --table` sur une copie de `table.json` en scratchpad).

## 1. Citations traduites

### 1.1 Plan 1.24 (tête, D-G, 1.24.0, 1.24.1, points ouverts)

| Ancienne | HEAD file:line | Statut |
|---|---|---|
| `AlundraPlayerManager.cs:927` (`PlayerTryAction` renvoie 0) | `Alundra/Hero/PlayerManager.cs:940` | VRAI, bouchon inchangé |
| `:1272` (`UpdatePlayerCarriedEntity` vide) | `Hero/PlayerManager.cs:1285-1287` | VRAI |
| `:395` (appel mode 0) | `Hero/PlayerManager.cs:408` | VRAI |
| `:646` (appel mode 2, nage) | `Hero/PlayerManager.cs:659` (dans `SwimTick`, après les deux remises à zéro `:657-658`) | VRAI |
| `:322-332` (branche verrouillée, pas de mode 1) | `Hero/PlayerManager.cs:335-345` (commentaire d'en-tête `:330-334`) | VRAI ; insertion du mode 1 entre `:342` et `:343` |
| `AlundraEntityScriptProxy.cs:88-89` (`PlatformEntity`, `CarriedEntity`) | `Alundra/Entities/EntityScriptProxy.cs:101-102` (offsets `:103-105`) | VRAI, jamais écrits en production (seule la copie de `Clone` `:2609-2613`) |
| `AlundraWorldProxy.cs:3240` (étape (6) de `DestroyEntity`) | `Alundra/Maps/WorldProxy.cs:3315` (doc `:3298-3299`) | VRAI, commentaire seul |
| `AlundraWorldProxy.cs:3254` (`DestroyEntity` à un argument) | `Maps/WorldProxy.cs:3329-3334` (doc `:3324`) | VRAI |
| `:483` (`LogicContextEntity = null!`) | `Entities/EntityScriptProxy.cs:502` | VRAI |
| `AlundraEntityScriptProxy.cs:2355-2362` (seul écrivain de `XCollisionEntity`) | `Entities/EntityScriptProxy.cs:2424-2431` (`EntityBlockCount++` `:2430`) | VRAI |
| `AlundraMovementObstacleProbe.cs:48-49` (masque `PickupKindMask`) | `Alundra/Physics/MovementObstacleProbe.cs:50-51` | VRAI ; retiré par g3 |
| `AlundraWorldProxy.cs:2860-2920` (ordre du tick) | `Maps/WorldProxy.cs:2905-2987` (`RunEntityBlock` ; MovePlayer `:2923-2928`, programmes `:2938`, re-scan `:2945`, compteurs `:2959`, listes `:2962-2966`, `BuildHitList` `:2968`, horloge `:2970`, physique `:2976`, effets actifs `:2982`, coups `:2985`) | VRAI avec ajouts |
| `AlundraWorldProxy.cs:2930` (commentaire « une fois par image ») | `Maps/WorldProxy.cs:2994-2995` | périmé confirmé (listes rafraîchies par tick, `:2962`) |
| `AlundraWorldProxy.cs:3553` (idem) | `Maps/WorldProxy.cs:3675` | périmé confirmé |
| `EntityFlags.cs:274-290` (`EntityPickupKind`) | `Alundra/Entities/EntityPickupKind.cs:9-25` (fichier propre depuis R2) ; aussi `Entities/EntityFlags.cs:110` | doc encore inversée (`:14-18`, `:20-24`) |
| `AlundraNativeSpriteRegistryTests.cs:51` (`...SixHandlers...`, `Count == 6`) | `Alundra.Tests/AlundraNativeSpriteRegistryTests.cs:51`, assertion `:55` | NOM ET VALEUR CHANGÉS : `ProductionRunner_RegistryHoldsTheSevenHandlersOfTheBeannoidTheMurutaAndTheSwordWave_AndTheSwitchIsOn`, `Assert.Equal(6 + 1, ...)` |
| `AlundraPlayerManager.cs` (périmètre) | `Alundra/Hero/PlayerManager.cs` | renommé |
| `AlundraEntityScriptProxy.cs` | `Alundra/Entities/EntityScriptProxy.cs` | renommé |
| `AlundraWorldProxy.cs` | `Alundra/Maps/WorldProxy.cs` | renommé |
| `AlundraEntityDepthKey.cs` | `Alundra/Entities/EntityDepthKey.cs` | renommé ; sert aux effets seulement (§2, A18) |
| `AlundraFrameSyncPasses.cs` | `Alundra/Maps/Loop/FrameSyncPasses.cs` | renommé |
| `EntityFlags.cs` | `Alundra/Entities/EntityFlags.cs` (+ `EntityPickupKind.cs`) | déplacé |
| `AlundraNativeSpriteRegistry.cs` (« une ligne ») | `Alundra/Ai/NativeSpriteRegistry.cs:36-43` (`CreateForProduction`) | renommé |
| `Alundra/Scripts/AlundraLiftableAi.cs` (nouveau) | NOT FOUND : `Alundra/Scripts/` n'existe plus (`ls` : absent) | nom final : §5 |
| `TickMotion` | `Entities/EntityScriptProxy.cs:1614-1636` ; appelé de `TickPhysics` `:1602-1611` | VRAI |
| `EvaluateEntitySupport` | `Entities/EntityScriptProxy.cs:739` | VRAI |
| `PushLogicalPositionToRoot` | `Entities/EntityScriptProxy.cs:2368` | VRAI (voir A17) |
| `MoveControllerAndPullPosition` | `Entities/EntityScriptProxy.cs:2412` | VRAI |
| `StepAnimationClock` | `Maps/Loop/FrameSyncPasses.cs:240` | VRAI |
| `RunHeroVerticalTick`, `SnapHeroAtApex` | `Alundra/Physics/ScriptedMotion.cs:251`, `:413` | VRAI (à ne pas diffuser) |
| `TickScriptedNpc` | `Physics/ScriptedMotion.cs:171-176` | VRAI |
| `CreateForProduction` | `Ai/NativeSpriteRegistry.cs:36` ; consommé `Events/EventProgramRunner.cs:51` | VRAI |
| `ProductionWiredWeaponTypes` (modèle) | `Hero/PlayerManager.cs:1226-1233` | VRAI mais non vide : `{27, 28, 29}` |
| `SpawnDirections` | `Hero/PlayerManager.cs:1282` | VRAI |
| `GetNumberOfItem` | `Hero/PlayerManager.cs:2181` | VRAI |
| `NoAttackVramFlag`, `NoJumpVramFlag` | `Hero/PlayerManager.cs:943`, `:110` | VRAI |
| `IsGroundAttackGroup`, `IsAirAttackGroup` (0x09, 0x0A) | `Hero/PlayerManager.cs:1026`, `:1030` | VRAI |
| `EndOfMovePlayer` | `Hero/PlayerManager.cs:634` | VRAI |
| `IEntityWorldContext.DestroyEntity` | `Alundra/Maps/IEntityWorldContext.cs:90` ; implémenté `Maps/WorldProxy.cs:3604` | VRAI |
| `EntitySupport.IsEligibleSubject` | `Alundra/Physics/EntitySupport.cs:73-76` | VRAI |
| `UpdateRidingEntities` | `Physics/EntitySupport.cs:277` | VRAI |
| `BoxDirection` | `Entities/EntityScriptProxy.HitPass.cs:39` | VRAI |
| `HasLogicalZ` (W1 à W4 d'e0k2) | `Entities/EntityScriptProxy.cs:1383` | VRAI, voir A16 |
| `AlundraWorldProxy.cs:3290` (O-E20-22, nettoyage au recyclage) | `Maps/WorldProxy.cs:3324` (doc), `RecycleDestroyedEntities` `:3265` | VRAI |
| `ArcsWithoutEntityContact` | `Alundra.Tests/AlundraArcSupport.cs:332` | VRAI |
| `AlundraStoryChainOpcodeAudit.ChainMaps` | `Alundra.Tests/AlundraStoryChainOpcodeAudit.cs:39` | VRAI (test non renommé, R5 non faite) |
| `JumpHeroRig`, `ContactWorld` | `Alundra.Tests/AlundraJumpTestSupport.cs:235`, `AlundraContactTestSupport.cs:128` | VRAI |
| UH10, UH14 | `Alundra.Tests/AlundraHeroObjectTopsTests.cs:240` (aussi `:291`, `:316`), `:447` | VRAI |
| `AlundraLadderClimbTests` (`CarriedEntity`) | `Alundra.Tests/AlundraLadderClimbTests.cs:242`, `:666-675` (`:669`), `:682-688` | VRAI |

### 1.2 `report.md` et `check.md` (numéros de `2010ec3`)

| Ancienne | HEAD file:line | Statut |
|---|---|---|
| `AlundraPlayerManager.cs:919`, `:927` | `Hero/PlayerManager.cs:932`, `:940` | VRAI |
| `:1271-1274` | `Hero/PlayerManager.cs:1284-1287` | VRAI |
| `:395`, `:646` | `:408`, `:659` | VRAI |
| `:317-335` (branche verrouillée) | `:330-345` | VRAI |
| sites `PlayerTryAction` `:419`, `:438`, `:743`, `:787`, `:848` | `:432`, `:451`, `:756`, `:800`, `:861` | VRAI |
| `:444` (commentaire « NOT PORTED » des états d'échelle) | `:456-458` | VRAI |
| `:386` (garde d'échelle) | `:399` | VRAI |
| `:1013`, `:1017` | `:1026`, `:1030` | VRAI |
| `:1269`, `:2168` | `:1282`, `:2181` | VRAI |
| `AlundraEntityScriptProxy.cs:88-92`, `:2596-2600`, `:2411-2416`, `:1370` | `Entities/EntityScriptProxy.cs:101-105`, `:2609-2613`, `:2424-2429`, `:1383` | VRAI |
| `AlundraWorldProxy.cs:3267`, `:3295`, `:3290`, `:3231` | `Maps/WorldProxy.cs:3301`, `:3329`, `:3324`, `:3265` | VRAI |
| `EntitySupport.cs:44-59`, `:72-75` | `Physics/EntitySupport.cs:45-60`, `:73-76` | VRAI |
| `AlundraScriptedMotion.cs:165-170`, `:671-675`, `:614-632` | `Physics/ScriptedMotion.cs:171-176`, `:677-681`, `:620-638` | VRAI (voir A19) |
| `AlundraBeannoideOracleTests.cs:76`, `:351-359`, `:18` | `:85`, `:360-368`, `:27` | VRAI |
| `AlundraMurutaOracleTests.cs:73`, `:429-437` | `:84`, `:440-448` | VRAI |
| `AlundraNpcCharacterControllerMoverTests.cs:2210` | `:2217` (méthode `EntitySearchService_Searches5And6_DrivenByRealRidingEntity_AndPlayerExcludedFromFunctions5To11`, `:2185`) | VRAI |
| `AlundraDestroyEntityBreakEffectTests.cs:12` | `:17` | VRAI |
| `AlundraTestSavesTests.cs:85-87` | `:88-90` | VRAI (`AllPresetNames` `:72`) |
| `AlundraLadderClimbTests.cs:660-669` | `:666-675` | VRAI |
| `AlundraPlayerManagerTests.cs:96` | `:100-113` (`0x20` en `:111`) | VRAI |
| `AlundraMovementObstacleProbeTests.cs:130`, `AlundraEntityContactArcTests.cs:480`, `AlundraDialogueOpcodesProductionTests.cs:382` | `:132`, `:485`, `:388` | VRAI (épingles de g3, pas de g1a) |
| `AlundraNativeSpriteRegistryTests.cs:51` (« registre à 7 clés ») | `:51`, assertion `:55` | VRAI : les preuves 2010ec3 disent déjà 7 ; seul le texte du plan (6) est périmé |

## 2. Affirmations sur la DLL

Historique des fichiers cités depuis `9ecc7d5` (commits hors restructuration, ancien chemin `Alundra/Scripts/`) :

| Fichier | Commits | Effet sur ce que suppose la tranche |
|---|---|---|
| `AlundraPlayerManager.cs` | `f616dc83` (f3c) | `ProductionWiredWeaponTypes` devient `{27, 28, 29}` ; rien sur le portage |
| `AlundraEntityScriptProxy.cs` | `181954f0`, `82a979da`, `2fb42f9c` (e0k2), `71b209d0` (e0a5), `4753357c` (e0b2), `bda82292` (docs e0a4) | marqueur `HasLogicalZ`, `ApplyVerticalForceLaw`, `SwitchedAtCreation`, `HeaderContents` (tous clonés sauf le marqueur) ; `TickMotion` inchangé |
| `AlundraWorldProxy.cs` | `2f16e610` (f3b), `81b68b5f` (e0k1), `181954f0` (e0k2 : poseur du marqueur), `0630f054` (e0a5), `4753357c` et `b5b2c888` (e0b2 : `RollLootAtCreation`), `683b9dcf` (e0a4), `146e1d81` (R0) | les deux `DestroyEntity` et `RunEntityBlock` inchangés dans leur ordre ; poseur du marqueur ajouté (`:3573`) |
| `AlundraScriptedMotion.cs` | e0k2 (`181954f0`, `82a979da`, `2fb42f9c`), e0k3 (`65f20873`, `bef323b4`), `bda82292` | `RunLogicalZPreXyStep`/`PostXyStep` et `ClampStepToMapEdge` pour les entités marquées |
| `AlundraNativeSpriteRegistry.cs` | `073de215` (f3c) | +1 clé (C, 61) |
| `AlundraFrameSyncPasses.cs` | `71b209d0`, `0630f054` (e0a5) | horloge du premier tick des entités nées par l'interface ; sans effet sur les caisses de carte |
| `EntityFlags.cs`, `EntitySupport.cs`, `AlundraEntityDepthKey.cs`, `AlundraMovementObstacleProbe.cs` | aucun | sans objet |

Aucun de e0a4, e0a5, e0b2, f3c ne change ce que g1a suppose, sauf f3c (registre) et, avant eux, e0k2 (marqueur, voir A16).

| # | Affirmation (source) | Etat au HEAD | Preuve |
|---|---|---|---|
| A1 | `PlayerTryAction` est un bouchon qui renvoie 0 ; `TryUseItem` un bouchon (tête 1.24, `report.md` §4) | VRAI | `Hero/PlayerManager.cs:940` ; `:932` renvoie `TryUseItemResultForTests ?? 1` (seam `:936-937`). Sites d'appel de `PlayerTryAction` : `:432`, `:451`, `:756`, `:800`, `:861` ; absent des états d'échelle (`:456-458`) |
| A2 | `UpdatePlayerCarriedEntity` est vide ; appels 0 et 2 | VRAI | `:1285-1287` ; mode 0 en `:408`, après `UpdatePlayerWeaponEffect` et `UpdateWeaponStepProgression` (`:406-407`), donc l'ordre de `0x80031D68-0x80031D78` ; mode 2 en `:659` |
| A3 | La branche verrouillée n'a pas l'appel du mode 1 ; ordre binaire : Create(1), deux remises à zéro, mode 1, annulation d'objet, END (D-G-4, `check.md` §3) | VRAI | HEAD : `Create` `:337`, remises à zéro `:341-342`, `EndOfMovePlayer` `:343`. Le commentaire `:330` dit que le mode 1 est sans effet (à corriger). `CancelItemMode` existe (`:740`, bouchon, appelé par la seule nage `:664`), n'est pas appelé dans la branche verrouillée |
| A4 | Les états 0x05 à 0x0C et 0x2E à 0x30 n'ont pas de cas ; 0x09/0x0A sont dans les groupes d'attaque | VRAI | Chaîne `if / else if` de `MovePlayer` `:411-617` ; `:1026-1031`. Nuance : `Hero/PlayerAnimationEffects.cs:198` (0x07 pas) et `:202` (0x06, 0x0A, 0x0B, 0x2E, 0x2F, 0x30 atterrissage) les connaissent déjà pour les effets |
| A5 | `CarriedEntity`, `PlatformEntity`, `RelativeWarpOffsetX/Y/Z` jamais écrits en production | VRAI | Seule écriture : `Clone` `:2609-2613`. Lecteurs : `PlayerManager.cs:399` (garde d'échelle) ; `Physics/EntitySupport.cs:76` ; **`Entities/EntitySearchService.cs:163` (case 4 : `PlatformEntity == null`) et `:249` (case 11 : `PlatformEntity != null`), non cités par le plan** |
| A6 | `IsEligibleSubject` exclut `PlatformEntity != null` ; `BuildCollidables` l'utilise | VRAI | `Physics/EntitySupport.cs:73-76`, `:45-60` |
| A7 | Les deux `DestroyEntity` ne touchent pas le porteur ; le recyclage ne efface rien d'autre | VRAI | `Maps/WorldProxy.cs:3301-3317` (étape (6) = commentaire `:3315`, étape (1) = commentaire `:3303`), `:3329-3334` ; `RecycleDestroyedEntities` `:3265` (doc `:3260` : « Nothing else is cleared »). Appelants : `EventProgramRunner.cs:290` (E0/E1), `:2459` (opcode 0x2E, 1 argument), `EntityScriptProxy.cs:1710`, `:1715` |
| A8 | La destruction à un argument s'appelle « par `IEntityWorldContext.DestroyEntity`, déjà là » (D-G-4) | VRAI | `IEntityWorldContext.cs:90`. `PlayerManager` atteint le monde par `host is IEntityWorldContext world` (`:1216`, modèle f3a) ; il n'y a pas de propriété directe |
| A9 | Ordre du tick : MovePlayer, programmes, compteurs, listes à chaque tick, horloge, physique (le héros en premier) | VRAI avec ajouts | `RunEntityBlock` `:2905-2987` ; héros en tête de `_tickEntities` (`CollectTickEntities` `:2865-2884`, doc `:2860-2864`). Ajouts : re-scan, `BuildHitList`, `RunActiveEffectsPass` |
| A10 | `RidingEntity` d'un objet porté n'est jamais recalculé | VRAI | `UpdateRidingEntities` ne parcourt que `collidables` (`Physics/EntitySupport.cs:277-290`) ; un objet avec `PlatformEntity` en sort |
| A11 | Piège « nul égale nul » : `LogicContextEntity = null!` | VRAI | `Entities/EntityScriptProxy.cs:502` |
| A12 | Doc d'`EntityPickupKind` inversée pour les genres 2 et 3 | VRAI | `Entities/EntityPickupKind.cs:14-24`. Les noms de membres (`LiftableUnlessItem3B`) portent eux-mêmes l'inversion ; `EntityFlags.Describe` les imprime (`Entities/EntityFlags.cs:238-241`) ; aucun test ne les épingle |
| A13 | Le registre n'a ni C23 ni E2 ; le test épingle `Count == 6` | PARTIEL | Pas de (C,23), pas de (E,2) : VRAI. Mais 7 clés : A1, C1, D1 (Beannoïde), A0, C10, D9 (Muruta), C61 (vague, f3c). Phase C : 8 |
| A14 | `CreateForProduction` et « son épingle » | VRAI | `Ai/NativeSpriteRegistry.cs:36-43`, sans paramètre ; il y manque `LiftableAi.Register(registry)`. L'interrupteur d'arène `NativeSpriteTypeFilterForTests` (D-F4-7) n'existe pas (f4 pas faite) ; `HitArena` coupe les gestionnaires natifs (`Alundra.Tests/AlundraHitArena.cs:70`) |
| A15 | Type des cartes = `0x100 +` indice de page (D-G-8, « à confirmer par le T0 ») | CONFIRMÉ | `Entities/Spawning/EntitySpawnFactory.cs:541` (`index \| 0x100`), `:677`, `:732` ; Beannoïde 335 = 0x100 + 79 (`Ai/Enemies/BeannoideAi.cs:24`) |
| A16 | Prérequis e0k2 : nom et points d'appel du marqueur | VRAI | `HasLogicalZ` = `internal bool`, `Entities/EntityScriptProxy.cs:1383` (doc `:1377-1382`). Poseur unique : `Maps/WorldProxy.cs:3573` (`SpawnEntityBySpriteIndex`). Lecteurs : W1 `Physics/ScriptedMotion.cs:211-217` (`RunLogicalZPreXyStep` `:465`, `RunLogicalZPostXyStep` `:540`) ; W2 `EntityScriptProxy.cs:793-794`, `:800`, `:852` ; W3 `:1480`, `:1485` ; W4 `:2382`, `ScriptedMotion.cs:525-530`, **`Maps/Loop/GameplayFreeze.cs:142`** ; e0k3 `ScriptedMotion.cs:685-687`. La doc `:1378-1380` dit « posé par l'interface SEULE, jamais par les créations depuis un enregistrement, jamais cloné » : D-G-13 (posé par `PutInHands` sur une entité d'enregistrement) la contredit |
| A17 | D-G-3 : branche en tête de `TickMotion`, « racine poussée à pixel entier (`PushLogicalPositionToRoot`) » | PARTIEL | Structure de `TickMotion` conforme (`:1614-1636` : héros d'abord, puis `Status.IsActive()` → `TickScriptedNpc`, puis `EvaluateEntitySupport`). Mais `PushLogicalPositionToRoot` (`:2368`) fait `ClampToGround()` (`:2375`, relève `PosZ` au sol le plus haut des quatre coins de la boîte) puis `Controller.Teleport(root)` (`:2380`) et, si marqué, remet la sentinelle de verticale (`:2382`). Non prévu par D-G-3. Champs copiables du porteur : `FloorHeight` `:365`, `TerrainHeight` `:366`. `ModdedPosX/Y/Z` (`:486`) ne sont écrits qu'à la création (`EntitySpawnFactory.cs:825-827`) |
| A18 | W6 : « un objet porté se trie au moins comme son porteur » (`AlundraEntityDepthKey.cs` ou `AlundraFrameSyncPasses.cs`) | PARTIEL | `EntityDepthKey` (doc `:7-11` : « Written deviation: no raise by ... a carrier ») ne sert qu'aux ancres d'effets (`EntityScriptProxy.cs:463`, `FrozenEffectAnchor`). La clé des sprites d'entités est `Maps/Loop/FrameSyncPasses.cs:454-474` (`RunWallInterleaveSortKeyPass`) → `Cells/WallPlacementOverlay.cs:380` sur PosY + IDSV, sans Z (doc `:360-379`). Le plan dit « ou » sans choisir |
| A19 | La marche portée démarre sans rampe : les fiches 5, 6, 7, 46, 47, 48 ont `Acceleration` 0 et la DLL les lit | VRAI | `alundra-project/Data/sprite-records.json`, héros `192c2eeb-...` : fiches 5/6/7/46/47/48 `Acceleration` 0 ; 6 et 46 `IsZForceApplied` 1280, `Sfx` 10, `Flags` 5 ; 7 `Speed` 208 ; fiche 1 `Acceleration` 1. Lecture par `ScriptedMotion.cs:617-638` : aucun code de marche. `TickScriptedNpc` lit `CurrentAnimationId` (exception `JoinsBirthTick`, `:175`) ; D-F5-5 n'est pas faite |
| A20 | Sites de mode 1/2 laissés « en bouchon nommé à leur place exacte » : blessure `0x80031C64`, sortie `0x80032778`, mort `0x800327AC` | A PRECISER | Aucun de ces trois sites n'a de code au HEAD (E20.c2b n'est pas portée : `PlayerManager.cs:127`, `:142`, `:722`) : il n'y a pas de bouchon à remplacer, seulement un commentaire à écrire |
| A21 | Garde d'échelle refusée en portant | VRAI | `PlayerManager.cs:394-402` (5ᵉ conjoint `CarriedEntity == null` `:399`), test existant `CaseSixGate_CarryingEntity_DoesNotEnterClimbing` |
| A22 | Phase C inerte au repos | VRAI avec nuance | Les 7 types de carte n'ont pas d'autre clé native. L'enregistrement remplace un « no-op compté » : `NativeHandlerRunCount` et `NativeHandlerRunCounts[(C,23)]` s'incrémentent (`EventProgramRunner.cs:300-304`), le journal « no registered handler » n'est plus écrit. Aucun test ne lit ces compteurs sur une carte de caisses (`AlundraNativeSpriteRegistryTests.cs:80-81` est synthétique ; `AlundraMurutaHallArcTests.cs:100` lit (D, 9)) |
| A23 | `GetNumberOfItem(state, 0x3B)` accessible | VRAI | `PlayerManager.cs:2181` (public static) |
| A24 | Dix sites de test pour `PlatformEntity` (D-G-15) | VRAI | `AlundraBeannoideOracleTests.cs:85`, `:360`, `:368` ; `AlundraMurutaOracleTests.cs:84`, `:440`, `:448` ; `AlundraEntityCollisionTests.cs:98` ; `AlundraNpcCharacterControllerMoverTests.cs:2217` ; `EntitySearchServiceTests.cs:138`, `:247` ; plus commentaire `AlundraTerrainHeightTests.cs:33-34`. `CarriedEntity` : 3 sites (`AlundraLadderClimbTests.cs:242`, `:669`, `:688`). `RelativeWarpOffsetX` : 3 (`AlundraWorldProxyEventPassTests.cs:437`, `:553`, `:557`). Total 12 écritures, comme `check.md` |
| A25 | Prérequis g1a : e0k2, e0a1, E14.a1b | VRAI | e0k2 ADR-0069 (marqueur, voir A16) ; la création par interface existe (`SpawnEntityBySpriteIndex`) ; `DestroyEntity(e, fx)` de E14.a1b est là (`WorldProxy.cs:3301`). e0b2 (ADR-0073) est faite : la variante du plan « g1a, g1b, puis g2a et g2b quand e0b2 est faite » (`:6299`) et la proposition de révision 10 de D-G-14 sont sans objet ; il reste e0c pour le butin (`WorldProxy.cs:3303`, « (1) loot: E20.e0. ») |

## 3. Tests qui épinglent

### 3.1 Bouchons et comportements remplacés ou touchés par g1a

- `AlundraNativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheSevenHandlersOfTheBeannoidTheMurutaAndTheSwordWave_AndTheSwitchIsOn` (`:51`, assertion `:55`, `TryGet` `:56-65`) : seul test qui bouge de certitude, 7 → 8 à la phase C, à dériver. Son frère `:242-249` est relatif et ne bouge pas.
- `AlundraLadderClimbTests.CaseSixGate_CarryingEntity_DoesNotEnterClimbing` (`:666-675`, `CarriedEntity = new Entity()` nu, sans `GameplayProxy`, `host: null`) : `MovePlayer` atteint `UpdatePlayerCarriedEntity(player, 0)` (`PlayerManager.cs:408`) après la garde (`:399`). Le corps de W1 doit tolérer un objet porté sans proxy (le plan ne couvre que `PlatformEntity` sans proxy, D-G-11). L'assertion (`NotEqual(Climbing, ...)`) reste vraie tant que `PlayerTryAction` est le bouchon (g1b la remplace). Autres sites du fichier : `:242` (`PlayerScriptHost`), `:688` (`MakeQualifyingIdlePlayer`), tous à `null`.
- Oracles E14 : `AlundraBeannoideOracleTests` et `AlundraMurutaOracleTests`, champ `platform_28` (`:85` / `:84`) et épingle X6 `Assert.Same(platformBefore, entity.PlatformEntity)` (`:368` / `:448`, méthode `RunE1Call`) : `PlatformEntity = new Entity { Name = "platform" }` sans proxy ; W5 ne doit ni lever ni toucher ce champ.
- `AlundraDestroyEntityBreakEffectTests` (13 appels de `DestroyEntity(`) : les deux surcharges ; verts attendus ; commentaire de classe `:17` et `AlundraBeannoideOracleTests.cs:27` (« clearing of the carrier is E20.g's ») à corriger. Autres fichiers qui atteignent `DestroyEntity(` : `AlundraNpcCharacterControllerMoverTests` (5), `IntroTraceHarnessTests` (4), `AlundraWorldProxyEntityManipulationTests` (4), `AlundraEffectOpcodeTests` (4), `AlundraEventProgramRunnerTests` (3), et une dizaine d'autres à 2.
- `AlundraEntityCollisionTests.SubjectGate_NotCollidable_NoEntityCollisionAnim_OrCarried_AnswersNull` (`:85-99`, ligne `:98`) : épingle ce dont (w1) dépend (un objet avec `PlatformEntity` n'est pas sujet) ; vert avant et après.
- `EntitySearchServiceTests.FunctionId4_OnTheGround_CollidableAndNotFlaggedAndNoPlatform` (`:123`, `:138`) et `FunctionId11_EveryEntityOnAPlatform` (`:243`, `:247`), `AlundraNpcCharacterControllerMoverTests.EntitySearchService_Searches5And6_...` (`:2185`, `:2217`) : verts ; ce sont les seuls tests de ce que les recherches de script verront d'un objet porté (hors case 4, dans la case 11).
- `AlundraLogicalZStepTests.A_TheMarker_IsPosedByTheInterfaceAndByItAlone_NotByTheHero_TheRecordCreations_TheRecycledEntities_NorTheClone` (`:99-117`, tuple `:114-116`, doc `:21`) : vert (rien n'est soulevé), mais nom et doc deviennent faux dès que `PutInHands` pose le marqueur sur une entité d'enregistrement. `AlundraMapEdgeWorldTests.cs:226` (`Assert.False(record.HasLogicalZ)`) : vert.
- `AlundraTestSavesTests.TheNinePresets_AreListed_InTheOrderOfTheTable` (`:88-90`) : ne bouge qu'avec le préréglage `porter-m<N>` (g1b selon 1.24.4, mais 1.24.0 le nomme déjà).
- `AlundraPlayerManagerTests.MovePlayer_InputBlocked_DoesNotChangeAnimationOrDirection` (`:121`) et `MovePlayer_AirStillOnTheGround_GoesBackToMoving_AnUnportedAnimationIsLeftUnchanged` (`:100`, `0x20` en `:111`) : traversent W2 et la chaîne sans objet porté ; effet attendu nul (le compteur est seulement remis à 0).
- Aucun test ne nomme `PlayerTryAction`, `UpdatePlayerCarriedEntity`, `TryLift` ni `PutInHands` (`rg` dans `Alundra.Tests`, `tools/AlundraTestSaves`, `alundra-casaengine-project-converter*` : 0 résultat) ; aucun test ne nomme l'un des 7 types câblés (`SpriteType = 291/292/294/297/304/438/234` : 0).

### 3.2 Portée chiffrée au HEAD (pour le recensement du T0)

- Appel direct de `PlayerManager.MovePlayer(` : 13 fichiers, 39 sites (`AlundraHeroChargeTests`, `AlundraHeroSwimBranchTests`, `AlundraHeroRunStatesTests`, `AlundraDialogueOpcodesProductionTests`, `AlundraNativeEffectTests`, `AlundraHeroLifeTests`, `AlundraHeroJumpStatesTests`, `AlundraLadderClimbTests`, `AlundraLoadingMapAirArrivalTests`, `AlundraPortalDetectionTests`, `AlundraPlayerManagerTests`, `AlundraSaveBookTests`, `IntroTraceHarnessTests`). Indirect : `ArcRun` 63 fichiers, `JumpHeroRig` 17, `ContactWorld` 15 (sur 307 fichiers `.cs` à la racine de `Alundra.Tests`). Tous atteignent `PlayerTryAction` et `UpdatePlayerCarriedEntity` avec `CarriedEntity == null`.
- `PlatformEntity` : 5 fichiers de code (liste A24) + 2 de texte (`AlundraTerrainHeightTests.cs:33-34`, `IntroTraceHarnessTests.cs:913`). `CarriedEntity` : 1 fichier. `Flags & 0x600` ou `PickupKindMask` : `AlundraEntityContactArcTests.cs:485`, `AlundraDialogueOpcodesProductionTests.cs:388`, `AlundraMovementObstacleProbeTests.cs:132` et `:342` (`extraFlags: 0x200u`) : ce sont des épingles de g3.
- 7 fichiers nomment « crate / caisse » (`AlundraAbsoluteZWritesTests`, `AlundraHeroApexAndCeilingTests`, `AlundraHeroObjectTopsTests`, `AlundraLogicalZStepTests`, `AlundraMovementObstacleProbeTests`, `AlundraLogicalZWorldTests`, `AlundraShipCorridorArcTests`) : même liste que `report.md`. Sous g3, les bancs (w3) et (w5) de g1a ne doivent pas reposer sur la traversée d'un objet à drapeau soulevable (UH10 est un coffre non soulevable).

## 4. Conséquences de g3 et nouveautés

### 4.1 Ordre et prérequis

- 1.28 (`:7064-7067`) et `critic-complet/` : g1a, g2a, g2b, puis g3 (avec g4a), puis g1b (avec g4b), une seule fusion (Q-G3-b A). g3 peut techniquement précéder g1a (aucune dépendance de code) mais ne doit pas.
- Dans la section 1.24 à récrire : le tableau de découpage (prérequis de g1b : ajouter g3) ; le paragraphe « Ordre interne » (`:6299`) ; « Dépendants » (`:6368`) qui dit « E20.g3 (retire la sonde de contact de g1b ; exige g1b et g2b) » alors que g3 exige g1a, g2a, g2b et g1b exige g3 ; D-G-1 ; D-G-16 et les O-G-1 (soulever en traversant) devenus sans objet ; Q-G1 (`:6364`), défaut A, que D-E20-39 a tranché B.
- Prérequis de programme absents de 1.24.1 : « fin du chantier de restructuration » (règle du programme : aucune autre tranche pendant le chantier ; R5 en attente de Q-R-3) et « garde des fichiers neufs » (Q-R-7, `docs/plan-restructuration-dll.md:1234`). 1.28 les écrit pour g3 ; g1a ajoute des fichiers aussi. D'autres sections récentes (e0c `:6769`, E20.e `:6924`) les écrivent déjà.

### 4.2 `PlatformEntity`, contact, D-G-7

- `PlatformEntity` : 1.28 (`:7042`, `:7064`) et `critic-recon/` (M3) font reposer g3 sur le fait que `PutInHands` l'écrit (sans lui la caisse portée reste dans `Collidables`, bloque les PNJ et fait plafond au-dessus du héros). L'acceptation proposée par `critic-recon/` : « la caisse levée n'est plus dans `Collidables` dès le tick du soulèvement, et le héros qui porte peut sauter » = (w1) et (w4) de 1.24.1. A nommer comme prérequis de g3 dans l'acceptation de g1a. Le tick est le bon : MovePlayer (`WorldProxy.cs:2927`) précède la passe des listes (`:2962`).
- L'effacement du bit `0x80` par le relâchement : 1.28 le rattache à g2a/g2b (« une caisse lancée s'efface de la liste »), mais le corps de C23 (c4 de g1a : `0x3323E4` → `0x332374`) le fait ; g2a ne donne que le déclencheur.
- Contact : g1a ne touche ni `XCollisionEntity` (`:2424-2431`) ni `EntityBlockCount`. D-G-7 (`LiftContactEntity`, `requiredCandidateFlags`, témoins k1 à k4) est caduc ; 1.24.1 ne le cite pas, la tête de 1.24 oui (`:6351`, `:6360`). g3 retire `skippedCandidateFlags` : aucune collision de signature avec g1a.
- Sémantique du contact établie par g3 (premier candidat de la liste, 0 si le pas complet est accepté même affleurant, 0 sans force, lu au tick suivant ; Carré le tick de l'affleurement donne l'épée) : sans effet sur g1a, qui n'en lit aucun ; elle ne concerne que le déclencheur de g1b. Le filtre `FlagToDestroy` (`Physics/EntitySupport.cs:55`, prémisse de D-E19-29 réfutée, O-G3-1) ne touche pas g1a : un objet porté est déjà hors liste.
- Marqueur : Q-G3-c / M6 de `critic-complet/` : D-G-13 ne pose le marqueur que sur l'objet saisi ; la caisse du dessus d'une pile garde l'ancienne règle de chute jusqu'à E19.h2 (g4b la mesure). Consigner dans D-G-13.
- Q-G1A-1 : sous 1.28, aucun objet n'est en main avant la fusion de g3 et g1b (option A de fait).

### 4.3 Ce que la tranche ignorait au HEAD

1. `EntitySearchService.cs:163`, `:249` lisent `PlatformEntity` (voir A5). Un objet porté sort de la case 4 et entre dans la case 11 : à écrire dans l'ADR et à tester une fois.
2. `GameplayFreeze.cs:142` et le poseur unique `WorldProxy.cs:3573` (A16) ; la doc du marqueur et le test `A_TheMarker_...` à retoucher.
3. `PushLogicalPositionToRoot` : `ClampToGround()` et `Teleport` par tick pour un objet porté (A17). Le clamp relève `PosZ` au sol le plus haut sous l'empreinte : un objet porté au-dessus d'un mur plus haut serait relevé, ce que le binaire ne fait pas (INFERENCE, non mesuré ; aucune sonde P-G ne le couvre).
4. Clé de tri : deux clés distinctes (A18).
5. Le registre a 7 clés ; le modèle d'ensemble câblé n'est plus vide ; `UnhandledWeaponSpriteCodes` (`PlayerManager.cs`, utilisé par `AlundraWeaponSpawnSeamTests.cs:243-244`) est le modèle de l'invariant de fermeture de D-G-8 ; `IsInlineDeactivateCode` (`EventProgramRunner.cs:328`) traite déjà E0, E1 et E10 en ligne (E2 ira au registre, D-G-9).
6. Tests plats : la racine de `Alundra.Tests` est plate (307 `.cs`), noms `Alundra*Tests` ; R5 non faite, Q-R-3 ouverte (nom ou sujet, préfixe retiré ou nom du type).
7. Les trois scénarios du critique nommés par le T0 (`c1_equality.py`, `c2_platform.py`, `c3_contact.py`) n'existent que dans le scratchpad de session (`.../scratchpad/disc-e20g/critic/`), pas dans le dépôt (`find docs -name c1_equality*` : rien). La machine d'oracle, `gl.py`, `ana.py`, `exe.py`, `k0lib.py`, `g1_lift.py` à `g18_c4_stone.py`, `run_all.py`, `oracle/`, les vecteurs de référence (`goldens_g1a.json` 950 vecteurs, `goldens_carry_group.json` 700) sont déjà versés dans `docs/plan-e20-annexe/e20-g1a/work/orc/` et `work/lst/` ; `e20-g3/` les réutilise par copie. L'annexe `docs/plan-e20-annexe/e20-g/` demandée par le T0 n'existe pas ; les annexes réelles sont `e20-g1a/` et `e20-g3/` : le T0 recopierait l'oracle une deuxième fois.
8. Corrections de `check.md` non reprises dans le plan : (a) les états 0x05 et 0x0B appellent `TryUseItem` en premier et n'écrivent pas `+0x8C` (le groupe de portage, oui) ; (b) `Balance` peut être nul en C# (`EntityScriptProxy.HitPass.cs:23`) alors que le binaire déréférence la fiche : traiter comme `AttackCount == 0` et tester ; (c) `Math.Abs(int.MinValue)` lève en C# (inatteignable, à garder pour les vecteurs) ; (d) le stub de récursion du golden `MoveEntityCarriedBranch` ; (e) « un objet porté ne blesse personne » n'est prouvé que pour caisses, jarres, herbe et pierre (la torche est UNKNOWN, g2c) ; (f) 950 vecteurs et non 850 ; (g) le type brut 35 de la carte 231 (placement d'une caisse que l'export lit comme « I05_Arc du chasseur », `critic-complet/` Réfuté 4) : absent des 7 types de la phase C. Ajouts de `report.md` §8 non repris dans les tâches : (c7) rejeu des 950 vecteurs, (c8) des 700, (w13) départ de marche à pleine vitesse, (w14) `CarriedEntity` sans proxy, (w15) objet détruit hors C23 (0x2E, E1, pente 4), (w16) héros reconstruit à l'arrivée n'emporte rien (Q-G1A-6), (w17) caisse portée ne marque aucun coup.
9. Ordre d'enveloppe : la phrase de 1.24 « g1a... après f6b, h0, h1 et A362 » (`:6267`) est un ordre, pas un prérequis d'API ; la ligne 10 de l'enveloppe (`:721`) ne donne à g1/g2 que e0a1, e0k2, E14.a1b (et e0b2 pour g2). h0/h1 ne sont pas faits (`UpdateItemUseDriver` `:1507` et `CancelItemMode` `:740` sont vides). Non revérifié au-delà.

## 5. Noms nouveaux

| Proposé par la tranche | Nom final (ADR-0075) | Remarques |
|---|---|---|
| `Alundra/Scripts/AlundraLiftableAi.cs`, classe `AlundraLiftableAi` (C23 ; E2 en g2b) | `Alundra/Ai/Objects/LiftableAi.cs`, `Alundra.Ai.Objects.LiftableAi` (`internal static`, comme `WaveSwordAi`) | Modèle : `Ai/Projectiles/WaveSwordAi.cs` (`Register(registry)`, `Tick`). Cohérent avec `Alundra/Ai/Objects/PickupAi.cs` annoncé par e0c (`:6772`). `check_names --table` : 0 collision |
| `AlundraCarryRules` (fonctions pures : pas, modes 0/1/2, table de transitions) | `Alundra/Hero/CarryRules.cs`, `Alundra.Hero.CarryRules` | Choix de domaine non dicté par l'arbre de l'ADR-0075 (aucun domaine « portage ») : `Hero` par proximité de `PlayerManager`. Tout type public secondaire (énum d'état, résultat) irait dans son propre fichier ; un type `internal` peut rester dans le fichier. 0 collision |
| `PutInHands` (couture interne) | membre de `Alundra.Hero.PlayerManager` (`Hero/PlayerManager.cs`), pas de fichier | Le « Périmètre » le place dans `AlundraPlayerManager.cs` |
| compteur de verrou du héros (D-G-4, champ du héros) | nom non donné par le plan | UNKNOWN : à nommer à l'exécution (champ de `Alundra.Entities.EntityScriptProxy`) |
| `ProductionWiredLiftableTypes`, `WiredLiftableTypesForTests` (D-G-8) | membres de `Alundra.Hero.PlayerManager` (modèle `:1226-1233`) | Introduits par g1b, pas g1a ; noms sans préfixe, inchangés |
| `LiftContactEntity` (D-G-7) | sans objet | caduc |
| tests (c1) à (c6), (w1) à (w12), (r1), (r2) | fichiers non nommés | Aujourd'hui `Alundra.Tests/Alundra<Sujet>Tests.cs` à plat (classe `public sealed class Alundra...Tests`, namespace `Alundra.Tests`). Nom après R5 : UNKNOWN (Q-R-3 : retirer le préfixe ou suivre le nom du type) ; `check_names.py` ne contrôle pas les noms de tests |
| `porter-m<N>` (préréglage de recette) | donnée de `TestSaves`, pas un type | g1b |
| annexe `docs/plan-e20-annexe/e20-g/`, ADR | hors DLL | prochain ADR libre : 0076 au HEAD (`ls docs/decisions`), à revérifier dans le README (d'autres plans en réservent) |

Un fichier neuf ne passe `check_names.py --tree --stage final` que si la table le connaît (`[hors table]`, `tools/dll-structure/check_names.py:96-98`) ; `table.json` est générée sur `5434cf6` (« do not edit »). D'où Q-R-7. D'autres sections prévoient déjà l'arrêt « un nom refusé par `check_names` ».

## 6. Inconnus

1. Quel jeu de règles remplace `--tree --stage final` pour les fichiers neufs (Q-R-7 : proposition R7 non tranchée) ; quand R5 est close (Q-R-3 ouverte).
2. Nom du champ du compteur de verrou ; domaine exact de `CarryRules` (Hero retenu par proximité) ; placement des 950 et 700 vecteurs de référence (annexe ou `Alundra.Tests/Data/`).
3. Effet de `ClampToGround()` et de `Teleport` par tick sur un objet porté (A17) : non mesuré (INFERENCE).
4. Quelle clé de tri vise (w10) : celle des sprites (`FrameSyncPasses.cs:454`) ou celle des effets (`EntityDepthKey`) ; le binaire a une seule clé `+0x1BC`.
5. Si `PutInHands` peut poser le marqueur sans réviser la doc et le test `A_TheMarker_...` (oui en comportement, non en texte).
6. Type brut 35 de la carte 231 (caisse ou « arc du chasseur » dans le binaire) : INCONNU côté binaire, question de convertisseur.
7. Si le téléport de même carte avale l'objet (O-G-8), le sort de l'objet au changement de carte (Q-G1A-6) : non lus au binaire (le plan l'écrit).
8. Données exactes des chaînes d'animation 5→12, 6→47, 10→45, 11→48, 46→48, 9→0 : non revérifiées ici (elles sont dans les fichiers d'animation, pas dans `sprite-records.json`).
9. Les faits du binaire (adresses, oracle, mutants) n'ont pas été relus : seules les affirmations sur la DLL, les tests et les chemins l'ont été.
10. Ordre exact de `_spawnedEntities` / `_collidables` après recyclage (slot ou spawn) : les deux critiques de g3 divergent (`critic-complet/` M4 contre le doc de `CollectTickEntities` `WorldProxy.cs:2860-2864`) ; sans effet sur g1a.
