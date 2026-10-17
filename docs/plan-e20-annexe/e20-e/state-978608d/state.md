# Lot E20.e (CheckAndTriggerTileEffect, 0x8003A7B0) : état au HEAD des rapports `docs/plan-e20-annexe/e20-e/`

HEAD `978608d`, branche `chantier/restructuration-dll-r3`. Rapports figés à `2010ec3` ; leurs citations de DLL sont lues à `9502f75` (identique à `2010ec3` pour `Alundra/` et `Alundra.Tests/`, voir §0). Lecture seule : rien n'a été écrit dans le dépôt, rien compilé, testé ni lancé. Les deux sous-modules ont des modifications locales de l'auteur (`CasaEngineMonogame` : `CasaEngine.Launcher/Program.cs` et `MGUI` ; `alundra-datas-analyser` : `MGUI`, `AlundraTools/log.txt`), non touchées. Scripts de travail (hors dépôt) : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20-e-state/e/` (`mapl.py`, `purecheck.py`, `puretests.py`, `decl.py`, `m14.py`).

**Verdict en trois lignes.** (1) Toutes les citations de DLL et de tests des deux rapports se retrouvent au HEAD, même code, lignes et noms déplacés (§1). (2) Toutes les affirmations sur « ce que la DLL a » et « ce qui manque » restent vraies, avec une nuance à corriger : « les natifs n'ont pas accès au magasin » est faux pour les natifs du registre (C61, futurs C94/E12), qui reçoivent un `IEntityWorldContext` ; c'est vrai seulement pour la voie du héros (§2, M1b). (3) Les tests qui épinglent les bouchons de l'herbe sont au nombre de cinq (§3), plus des pins d'amont à ne pas casser et un oracle MIPS de l'herbe déjà présent dans le dépôt et non cité par les rapports (§4).

## 0. Méthode, et preuve que le code cité n'a pas changé de sens

1. `git log 9502f75..HEAD -- Alundra Alundra.Tests` : 81 commits. 80 sont `refactor(dll)` (R2 déplacements et scissions, R3 renommages, R4 les neuf types cités par les données). Le seul autre est `146e1d8` (R0, partie parent, 2026-10-17 09:52). Aucun commit ne touche `Alundra/` ni `Alundra.Tests/` entre `9502f75` et `2010ec3` (les trois commits suivants, `b35a2c4`, `532cf3f`, `a6f003b`, sont des documents). Donc la DLL et les tests lus par les rapports sont ceux de `9502f75`.
2. Preuve ligne à ligne que la restructuration est pure (`purecheck.py` pour la DLL, `puretests.py` pour les tests) : à chaque fichier de `9502f75` on applique les renommages de `tools/dll-structure/table.json`, on retire `using`, `namespace` et `#nullable`, puis on compare l'ensemble des lignes à l'union des fichiers du HEAD qui en proviennent (scissions comprises).
   - DLL : 121 des 129 fichiers sont identiques. Les 8 autres : `AlundraEntitySpawnFactory` (trois `nameof(X)` devenus `typeof(X).FullName!`, R0), `AlundraWorldProxy` (trois blocs `catch (ScriptTypeResolutionException) { AbortCreation(...); throw; }` de R0, plus des qualifications de `cref`), `IEntityWorldContext`, `AlundraGameState`, `AlundraSpuVoiceVolume` (qualifications de `cref` dans des commentaires seulement), `AlundraDialogueDirector`, `AlundraEventProgramRunner`, `EntityStatus` (BOM, ligne `namespace`). Aucune ligne de logique n'a changé hors R0.
   - Tests : 331 fichiers à `9502f75`, 333 au HEAD (ajoutés : `AlundraScriptAssemblyRegistration.cs`, `AlundraScriptNamesInTheDataTests.cs`, R0). 58 fichiers ont un résidu, tous mécaniques : R0 `nameof(AlundraEntityScriptProxy)` -> `typeof(EntityScriptProxy).FullName!`, une garde `Hero == null` dans `AlundraArcSupport.cs` (R0), des chemins `Alundra/Scripts/HudPresenter.cs` -> `Alundra/UI/Hud/HudPresenter.cs` lus par un test, un littéral `"AlundraEffect"` -> `"Effect"`, des qualifications de `cref`. Aucun fichier de test cité par les rapports n'a de résidu.
3. Traduction des lignes : `git diff -M -U0 9502f75 HEAD` hunk par hunk (`mapl.py`), avec affichage de la ligne ancienne et de la ligne nouvelle, puis `rg` au HEAD pour les identifiants renommés (`table.json` : `AlundraWaveSwordAi` -> `Ai/Projectiles/WaveSwordAi.cs`, etc.).
4. Données : le script de recensement du rapport (`work/census_export.py`) a été rejoué en copie dans le scratchpad, lecture seule.

## 1. Citations traduites

Statut : OK = même code, ligne ou fichier déplacé ; OK renommé = même code, identifiant renommé par l'ADR-0075.

### 1.1 `report.md`

| # | Ancienne citation (ligne du rapport) | HEAD file:line | Statut |
|---|---|---|---|
| 1 | `alundra-datas-analyser/.../DatasBin/GameMapInfo.cs:14` (l.92, décompilation) | `alundra-datas-analyser/AlundraTools/AlundraEngine/DatasBin/GameMapInfo.cs:14` | OK, sous-module inchangé (`fc16872` aux deux dates), texte « BalanceLevel; //9 - AnimDeleteWall. NOM NON PROUVE » |
| 2 | `GameEngine.cs:2441-2527`, `EffectManager.cs:443-490`, `GameEngine.cs:844-870` (l.141, décompilation) | `.../AlundraEngine/GameEngine.cs:2441` (CATE) à `:2527` (appel d'effet `:2516-2527`) ; `.../Graphics/EffectManager.cs:443-444` (`RandomlySpawnItem`) ; `GameEngine.cs:844-845` (`ChooseRandomlyAnItem`) | OK (début de plage relu) |
| 3 | `AlundraWaveSwordAi.cs:34-37` stub (l.161) | `Alundra/Ai/Projectiles/WaveSwordAi.cs:37-40` (méthode `CheckAndTriggerTileEffect` `:40`) | OK renommé |
| 4 | `Tick` l'appelle à `:31` | `WaveSwordAi.cs:34` | OK |
| 5 | journal `StubCallsForTests` `:22-24` | `WaveSwordAi.cs:25-27` | OK |
| 6 | `AlundraPlayerAnimationEffects.PlayerGrassCut` `:214` (l.162) | `Alundra/Hero/PlayerAnimationEffects.cs:218` | OK renommé |
| 7 | appelé par `Create` `:61-63` | `PlayerAnimationEffects.cs:65-67` | OK |
| 8 | `Create` appelé aux deux modes `AlundraPlayerManager.cs:324` et `:337` | `Alundra/Hero/PlayerManager.cs:337` (`LockedMode`) et `:350` (`FreeMode`) | OK |
| 9 | `IsGrassCutState` `:191` (l.163 ; check l.26) | `PlayerAnimationEffects.cs:195` | OK (même liste de 15 états) |
| 10 | `HasFrameBox` `AlundraEntityScriptProxy.HitPass.cs:31` (l.167) | `Alundra/Entities/EntityScriptProxy.HitPass.cs:33` | OK |
| 11 | `EvaluateFrameBox` `:192-240` | `EntityScriptProxy.HitPass.cs:194-242` (méthode `:196`) | OK |
| 12 | `StoreHitBox` `:85-97` (l.168) | `EntityScriptProxy.HitPass.cs:87-99` | OK |
| 13 | appelé à la création `:76` | `EntityScriptProxy.HitPass.cs:78` | OK |
| 14 | fin de pas `AlundraEntityScriptProxy.cs:1597` | `Alundra/Entities/EntityScriptProxy.cs:1610` (dans `TickPhysics` `:1602-1611`) | OK |
| 15 | `HitBoxX/Y/Z`, `CollisionWidth/Depth/Height` `AlundraEntityScriptProxy.cs:476-484` (l.169) | `EntityScriptProxy.cs:489-497` | OK |
| 16 | `AlundraFrameBoxTimeline.cs` (l.170, sans ligne) | `Alundra/Combat/FrameBoxTimeline.cs` (`FrameBox` `:11`, `FrameBoxTimeline` `:22`, `TryGetBox` `:118`, lit `collision_keyframes` `:14`) | OK renommé |
| 17 | `IsWall` `AlundraPlayerAnimationEffects.cs:167-175` (l.173) | `PlayerAnimationEffects.cs:171-179` (le `+1` : `:178`) | OK |
| 18 | `AlundraCellsCollisionField.TryReadBinaryCell` `:384-397` (l.174) | `Alundra/Physics/CellsCollisionField.cs:272-285` | OK (fichier ramené à 326 lignes par la scission ; `CellsRecords.cs` et `WallTileStack.cs` à part) |
| 19 | `BinaryColumn`, `BinaryRow` `:181-184` (l.175) | `PlayerAnimationEffects.cs:185-188` | OK |
| 20 | `AlundraCellStore.cs:58` (l.178) | `Alundra/Cells/CellStore.cs:35` | OK renommé |
| 21 | tableaux aliasés `:78-90` | `CellStore.cs:55-67` | OK |
| 22 | `GetWallTileStack` `:176`, `CopyCellRectangle` `:211` | `CellStore.cs:153`, `:188` | OK |
| 23 | `SetCellBits` `:316`, `ClearCellBits` `:323`, bornes en dur `:339-340` | `CellStore.cs:293`, `:300`, `:316-317` (corps commun `ApplyCellBits` `:314`) | OK |
| 24 | événement `CellsMutated` `:76` | `CellStore.cs:53` | OK |
| 25 | `AlundraCellVisualSync` `:58` (l.183) | `Alundra/Cells/CellVisualSync.cs:59` | OK renommé |
| 26 | `ProcessCellWalls` `:327-426`, retrait `:337-354` | `CellVisualSync.cs:328-427`, `:338-355` | OK |
| 27 | `FlushPendingOverlayReconstruction` `:211` ; appelé `AlundraWorldProxy.cs:2616` (l.185) | `CellVisualSync.cs:212` (corps `:212-237`, `ClearSortedOverlayTiles` `:219`, `ReconstructionCount++` `:234`) ; `Alundra/Maps/WorldProxy.cs:2650` | OK |
| 28 | installation `AlundraWorldProxy.cs:1185-1240` (l.186) | `WorldProxy.cs:1219-1274` (`InstallCellAndOverlaySystems` `:1191`) | OK |
| 29 | `IEntityWorldContext.CellMutator` `IEntityWorldContext.cs:105` (l.187) | `Alundra/Maps/IEntityWorldContext.cs:116` (type `ICellMutator?`) | OK renommé |
| 30 | `IAlundraCellMutator` `AlundraCellStore.cs:18-35` | `Alundra/Cells/ICellMutator.cs:21-38` (fichier propre) | OK renommé |
| 31 | `AlundraEffectBank.cs:99`, `TryGetTable` `:119` (l.190) | `Alundra/Rendering/Effects/EffectBank.cs:16`, `:36` | OK renommé |
| 32 | `AlundraEffectReservoir.Create` `:221` (l.191) | `Alundra/Rendering/Effects/EffectReservoir.cs:101` (`SlotCount = 128` `:25`) | OK |
| 33 | `AlundraEffectStage.Emit` `:190`, `CreateNow` `:197` (l.192) | `Alundra/Rendering/Effects/EffectStage.cs:192`, `:199` | OK |
| 34 | `AlundraNativeEffects` `:9` | `Alundra/Rendering/Effects/NativeEffects.cs:12` | OK renommé |
| 35 | `IEntityWorldContext.EffectStage` `:199`, `IAlundraScriptHost.EffectStage` `:145` (l.193) | `IEntityWorldContext.cs:210`, `Alundra/Maps/IScriptHost.cs:156` | OK |
| 36 | `AlundraPlayerManager.cs:969, 1004, 1071` `PlaySfx` (l.194) | `PlayerManager.cs:982, 1017, 1084` | OK |
| 37 | `Sounds/sfx-manifest.json`, sons 31 et 84 | `alundra-project/Sounds/sfx-manifest.json` (jq : `id` 31 et 84 présents, `skip_reason` nul, 1 tone chacun) ; lu par `Alundra/Audio/SfxBank.cs:34` | OK |
| 38 | `AlundraLootRoll.Choose` `AlundraLootRoll.cs:51` (l.197) | `Alundra/Items/LootRoll.cs:54` | OK renommé |
| 39 | `AlundraRandom.Next` `AlundraRandom.cs:52` (l.198) | `Alundra/Rng/GameRandom.cs:54` (type `GameRandom`) | OK renommé |
| 40 | `RollLootAtCreation` `AlundraWorldProxy.cs:3429` (l.199) | `WorldProxy.cs:3469` | OK |
| 41 | `IEntityWorldContext.SpawnEntityBySpriteIndex` `:70` (l.200) | `IEntityWorldContext.cs:81` | OK |
| 42 | implémentée `AlundraWorldProxy.cs:3448` et `:3555` | `WorldProxy.cs:3488` (méthode) et `:3601` (implémentation explicite) | OK |
| 43 | `ForceZ` `:325`, `Bytes` `:1214`, `DelayOrAngleOrEntityId` `:1215`, `ItemState` `:1216`, `AIValues` `:1217`, `ContentsItemId`/`ContentsGameFlag` `:93-94` (l.201, `AlundraEntityScriptProxy.cs`) | `Alundra/Entities/EntityScriptProxy.cs:338`, `:1227`, `:1228`, `:1229`, `:1230`, `:106-107` | OK |
| 44 | `AlundraWorldProxy.DestroyEntity` « (1) loot: E20.e0 » `:3269`, « (4) release ... E20.e » `:3273` (l.204) | `WorldProxy.cs:3303`, `:3307` (méthode `:3301`) | OK |
| 45 | `plan-e20-heros.md:4789` (l.205) | `docs/plan-e20-heros.md:4789-4790` (la phrase commence à 4789, finit à 4790 ; même place à `9502f75`) | OK |
| 46 | `AlundraFlailBounceTests.cs:375, 395, 502, 507, 757` (l.208) | `Alundra.Tests/AlundraFlailBounceTests.cs:381, 401, 508, 513, 763` (lignes d'assertion sur `GrassCutStubName`) | OK |
| 47 | `AlundraWaveSwordLifeTests.cs:45, 122, 141` (l.209) | `AlundraWaveSwordLifeTests.cs:53, 130, 149` | OK (53 : remise à nul ; 130 et 149 : le journal posé dans `V2_...`) |
| 48 | `AlundraWaveSwordWiringTests.cs:43` (l.210) | `AlundraWaveSwordWiringTests.cs:51` | OK (remise à nul seulement, pas un pin) |
| 49 | `AlundraWaveSwordSupport.cs` (l.211, sans ligne) | `AlundraWaveSwordSupport.cs:102-107` (`WaveOracle.GrassCallTicks`) | OK |
| 50 | `AlundraPlayerManager.cs:1203-1206` (l.220) | `PlayerManager.cs:1216-1219` | OK |
| 51 | `AlundraBalanceTables.cs:151` (l.223) | `Alundra/Combat/BalanceTables.cs:27` (commentaire seul) | OK |
| 52 | `AlundraPlayerAnimationEffects.cs:204-211` (l.329) | `PlayerAnimationEffects.cs:208-215` | OK |
| 53 | `docs/plan-e19-sm-annexe/opcodes.tsv:160` (l.74, opcode 0x9E) | inchangé ; sa colonne DLL cite `AlundraEventProgramRunner.cs:3115 (UnknownOpcode)` -> `Alundra/Events/EventProgramRunner.cs:3065` ; `rg "0x9E"` dans ce fichier : aucune branche, opcode toujours non porté | OK |

### 1.2 `check.md`

Ses citations de DLL recouvrent celles du rapport : `IsGrassCutState :191` = ligne 9 ; `AlundraWaveSwordAi.cs:31/34-37` = lignes 3-4 ; `AlundraPlayerAnimationEffects.cs:61-63/191/214` = lignes 7, 9, 6 ; `AlundraPlayerManager.cs:324/337/1203-1206` = lignes 8 et 50 ; `AlundraCellStore.cs` (bornes) = ligne 23 ; `IEntityWorldContext.cs:70/105/199` = lignes 41, 29, 35. Une seule citation nouvelle :

| # | Ancienne citation | HEAD file:line | Statut |
|---|---|---|---|
| 54 | `AlundraHeroChargeTests.cs:661-669` (l.75) | `Alundra.Tests/AlundraHeroChargeTests.cs:667-675` (test `TheLockedBranch_ClearsTheChargeCounterAndThePendingShot_AfterCreatePlayerAnimationEffects1`, déclaration `:654`) | OK. Ce test pose bien `StubCallsForTests`, mais il épingle le bouchon d'atterrissage (état `0x2D`, mode 1, assertion `:678-679`), pas l'herbe |

### 1.3 Citations du plan §1.25 (résumé de ces lectures), traduites pour mémoire

| # | Ancienne citation | HEAD file:line | Statut |
|---|---|---|---|
| 55 | `AlundraWaveSwordAi.cs:34-37` (plan l.6628) | `WaveSwordAi.cs:37-40` | OK |
| 56 | `AlundraHeroChargeTests.cs:466-485` | `AlundraHeroChargeTests.cs:472-491` (`C10Crusher_TheReleaseTick101_...`, état `0x19` `:491`) | OK |
| 57 | `AlundraPlayerManager.cs:1213` | `PlayerManager.cs:1226` (`ProductionWiredWeaponTypes = { 27, 28, 29 }`) | OK |
| 58 | `TryUseItem` et `PlayerTryAction` bouchons `:919`, `:927` | `PlayerManager.cs:932`, `:940` | OK |
| 59 | `UpdatePlayerCarriedEntity` vide `:1271-1274` | `PlayerManager.cs:1284-1287` | OK |
| 60 | `AlundraWorldProxy.cs:3290` (O-E20-22) | `WorldProxy.cs:3324` | OK |

## 2. Affirmations sur la DLL

### 2.1 « Ce que la DLL a » (`report.md` §5)

| # | Affirmation | Verdict au HEAD | Preuve |
|---|---|---|---|
| A1 | `WaveSwordAi.CheckAndTriggerTileEffect` est un bouchon nommé, appelé par `Tick`, avec journal par thread | VRAI | `WaveSwordAi.cs:34`, `:40` (`=> StubCallsForTests?.Add(entity)`), `:25-27` ; C61 enregistré en production : `Ai/NativeSpriteRegistry.cs:41` ; types 27-29 câblés au tir : `PlayerManager.cs:1226` |
| A2 | `PlayerGrassCut` est un bouchon, appelé par `Create` dans les deux modes | VRAI | `PlayerAnimationEffects.cs:218` (corps = `Record(...)`), `:65-67` ; `Create` appelé à `PlayerManager.cs:337` et `:350` |
| A3 | `IsGrassCutState` égale la table `0x800228AC` (15 états) | VRAI | `PlayerAnimationEffects.cs:195` : `0x04 0x10 0x12 0x14 0x16 0x18 0x19 0x3F 0x40 0x41 0x42 0x44 0x46 0x49 0x4B` |
| A4 | C94 et E12 ne sont pas enregistrés | VRAI | `CreateForProduction` (`NativeSpriteRegistry.cs:36-42`) n'enregistre que Beannoïde, Muruta, WaveSword ; aucune occurrence de `0x8007C0D8` ni `0x8007F27C` dans `Alundra/` |
| A5 | Boîte : `HasFrameBox`, `EvaluateFrameBox`, `StoreHitBox` (= `0x80038064`), création et fin de pas, champs | VRAI | `HitPass.cs:33`, `:196`, `:87-99` ; `StoreHitBox` n'écrit `HitBoxX/Y/Z` (Pos + décalage) que si `EvaluateFrameBox()` est vrai (`:89-97`), règle du binaire ; la boîte est stockée à la création (`:78`) et à la fin du pas (`EntityScriptProxy.cs:1610`). Ordre dans `RunEntityBlock` : `MovePlayerOnTickPad` `WorldProxy.cs:2927`, puis `TickPrograms` (C61) `:2941`, puis `TickPhysics` `:2979` : CATE lit donc la boîte stockée au tick précédent, comme dit `check.md` §5 |
| A6 | La Z de la DLL est celle du binaire moins 1 ; `IsWall` ajoute `+1` | VRAI | `PlayerAnimationEffects.cs:178` ; ADR-0026 |
| A7 | `TryReadBinaryCell` lit la case par l'indexation du binaire | VRAI | `CellsCollisionField.cs:272-285` : renvoie faux hors champ, hauteur et marchabilité `& 0xFF` |
| A8 | `BinaryColumn`/`BinaryRow` existent | VRAI, avec une limite de réemploi | `PlayerAnimationEffects.cs:185-188` renvoient la valeur déjà bornée (`pixel <= 0 ? 0 : Math.Min(pixel / 24, 0x33)`). CATE a besoin de la colonne non bornée pour la position de l'effet (rapport §2, Q-E7) : ces deux assistants ne suffisent pas tels quels (INFÉRENCE de conception) |
| A9 | `CellStore` : tableaux aliasés, pile dense, `GetWallTileStack`, `CopyCellRectangle`, `SetCellBits`/`ClearCellBits` à bornes `0x33`/`0x3B`, événement `CellsMutated` | VRAI | `CellStore.cs:35-67`, `:153`, `:188`, `:293`, `:300`, `:316-317`, `:53` ; transitoire à chaque entrée de carte (D-E7-5, `:31`) |
| A10 | `CellVisualSync` retire les tuiles quand la pile est détruite et reconstruit l'overlay entier une fois par image | VRAI | `CellVisualSync.cs:338-355` (clés périmées retirées dès que `GetWallTileStack` rend nul) ; `:212-237` (tout vider puis tout ré-ajouter) ; appel `WorldProxy.cs:2650`, après `RunEntityBlock` (`:2582`, dans la boucle des ticks) : une coupe du tick est visible dès l'`Update` en cours |
| A11 | `IEntityWorldContext.CellMutator` ; `ICellMutator` n'a que trois opérations | VRAI | `IEntityWorldContext.cs:116`, `ICellMutator.cs:21-38`. Un seul implémenteur dans la DLL (`CellStore.cs:35`) ; les tests n'en définissent aucun (`rg` : `AlundraEventProgramRunnerTests.cs:83` et `IntroTraceHarnessTests.cs:1146` ne font que porter une valeur) : ajouter un membre à `ICellMutator` ne casse aucun faux de test |
| A12 | `EffectBank` lit `effects-global.json`, la table 2 y est | VRAI | `EffectBankLoader.cs:16` (doc), `EffectBank.cs:36` ; donnée : `alundra-project/Data/effects/effects-global.json`, `Effects[2]` = 1 animation, 9 images (première `[5,0]`), fin `Destroy`, 9 jeux d'images |
| A13 | `EffectReservoir.Create` = `CreateEffectEntity` mode 0, 128 emplacements | VRAI | `EffectReservoir.cs:25`, `:101` |
| A14 | `EffectStage.CreateNow` est réservé au héros (ADR-0057) ; `Emit` file l'opération | VRAI | `EffectStage.cs:192`, `:195-199` (doc « D-E20-28, amends ADR-0046 for the hero only ») |
| A15 | Accès aux effets depuis un natif ou le héros | VRAI | `IEntityWorldContext.cs:210`, `IScriptHost.cs:156` ; son : `IScriptHost.cs:127` (`SoundPlayer`), `PlayerManager.cs:982` |
| A16 | `LootRoll.Choose` = `ChooseRandomlyAnItem` exact, un tirage par maillon ; tirage à la création | VRAI | `LootRoll.cs:54-75` (un `draw()` par maillon, table `0x80028E0C`) ; `GameRandom.cs:54` ; `WorldProxy.cs:3469-3478` |
| A17 | `SpawnEntityBySpriteIndex(parent, isMapPage, index, x, y, z, direction)`, parent nul permis, l'appelant doit tester le retour nul | VRAI | `IEntityWorldContext.cs:81` et sa doc `:70-80` ; nouveauté : peut désormais lever `ScriptTypeResolutionException` (§4, N2) |
| A18 | Champs de l'objet lâché présents sur le proxy | VRAI | lignes 43 du §1.1 |
| A19 | e0c non fait : ni C255, ni A255, ni lâcher | VRAI | `rg "GiveItem|SpawnRandomLoot|SpawnContents"` dans `Alundra/` : aucune occurrence ; `Alundra/Ai/` ne contient que Beannoïde, Muruta, WaveSword, SaveBook ; bouchons `WorldProxy.cs:3303`, `:3307` |

### 2.2 « Ce qui manque » (`report.md` §6)

| # | Affirmation | Verdict au HEAD | Preuve |
|---|---|---|---|
| M1 | Aucune primitive de coupe dans le magasin ; `ClearCellBits(col,row,0x02,0)` n'efface que le bit | VRAI | `CellStore.cs:314-340` (`ApplyCellBits` n'écrit que `_walkability` et `_groundProperty`, jamais `_wallTilesOffset` ni `_wallTiles`). Contrainte à ajouter : `AlundraCellVisualSyncTests.cs:619` (`SetThenClearCellBits_NeverTriggersOverlayReconstruction`) épingle que les bits ne reconstruisent jamais l'overlay : la coupe doit être une opération distincte, pas un mode de `ClearCellBits` |
| M1b | « Les natifs n'ont pas accès au magasin ; seul `IEntityWorldContext` a `CellMutator` et `SpawnEntityBySpriteIndex` » | NUANCÉ (partiellement faux) | Un natif du registre est un `NativeSpriteHandler(EntityScriptProxy entity, IEntityWorldContext context, GameState state)` (`NativeSpriteRegistry.cs:20`), appelé avec `_worldContext` (`EventProgramRunner.cs:305`) : C61, C94 et E12 ont donc `context.CellMutator`, `context.SpawnEntityBySpriteIndex`, `context.EffectStage` sous la main (C61 ignore aujourd'hui `context`, `WaveSwordAi.cs:31`). Vrai seulement pour `PlayerAnimationEffects.Create(EntityScriptProxy player, int mode)` (`:56`), qui ne reçoit que le héros : `player.ScriptHost` est un `IScriptHost`, sans `CellMutator` ni `SpawnEntityBySpriteIndex` (`IScriptHost.cs`, `rg`), d'où le `host is IEntityWorldContext` de `PlayerManager.cs:1216`. Et le banc de test `ContactHost : IScriptHost` (`AlundraContactTestSupport.cs:52`) n'est pas un `IEntityWorldContext` |
| M2 | Le port de CATE et son câblage restent à faire | VRAI | les deux bouchons ci-dessus ; aucun `TileEffect` dans `Alundra/` |
| M3 | L'octet 9 est exporté mais lu par personne dans la DLL | VRAI | `rg BalanceLevel` hors docs : seuls `BalanceTables.cs:27` (commentaire) et `:114` (champ `BalanceLevel` d'un enregistrement de `balance.json`, autre chose). Précédent de lecture d'une propriété custom de carte : `BalanceTables.ResolveThreshold` (`BalanceTables.cs:173-178`, propriété `D`) et son test `AlundraBalanceTablesTests.cs:147-158`, qui pose `BalanceLevel = "2"` en leurre « the byte 9 of the header, NOT the threshold » |
| M4 | Données : rien ne manque pour CATE | VRAI | recensement rejoué en copie : 483 `.tileMap`, 165 cartes avec cases coupables, 9 089 cases, 8 989 avec pile, 18 342 tuiles toutes dans `AlundraWallPlacements` ; `"BalanceLevel": "2"` dans 483 fichiers ; effet 2 et sons 31 et 84 présents (A12, ligne 37). Boîtes : celles de la vague (27-29) sont épinglées par l'oracle (`AlundraWaveSwordSupport.cs`, `Describe` compare `hb=` et `size=`) ; celles des sprites 7 (C94) et 2 (E12) restent INCONNUES, ces sprites n'ayant pas de gestionnaire |
| M5 | Butin : C255, A255, effet du ramassage, `0x80032C7C`, `0x80032B90`, veto MP | VRAI | A19 |
| M6 | Coût de l'overlay : jusqu'à 5 037 entrées reconstruites par image | Mécanisme VRAI, chiffres non rejoués | `CellVisualSync.cs:212-237` ; les 1 816 + 2 360 de la carte 16 ne sont pas recomptés ici |

## 3. Tests qui épinglent les bouchons

### 3.1 Pins directs des bouchons de l'herbe (à faire évoluer avec la tranche)

| Test | HEAD file:line | Ce qu'il épingle |
|---|---|---|
| `AlundraFlailBounceTests.TheAttackStates_CallTheGrassCutStub_ThroughTheFirstDispatch` | décl. `:375` (Theory `:359-374`), assertion `:381` | chacun des 15 états journalise `GrassCutStubName` |
| `AlundraFlailBounceTests.TheOtherStates_DoNotCallTheGrassCutStub` | `:397` (attributs `:384-396`), assertion `:401` | les autres états ne le journalisent pas |
| `AlundraFlailBounceTests.TheStubsAreCalledInTheOrderOfTheBinary_FirstDispatch_SecondDispatch_Landing_Alarm` | `:502`, assertions `:508` et `:513` | listes exactes `[GrassCut, Landing, LowHpAlarm]` et `[GrassCut, Landing]` : casse si l'appel réel n'est plus journalisé sous ce nom ou ajoute une entrée |
| `AlundraFlailBounceTests.TheGrassCutStub_IsCalledAtEveryTickOfTheFlailStates_AndOnlyThere` | `:753`, assertion `:763` (sonde `RunProbe` `:600-640`, journal posé `:627`) | un appel à chaque tick des états `0x12 0x16 0x40 0x42` de l'oracle du fléau, jamais ailleurs |
| `AlundraWaveSwordLifeTests.V2_C61_CallsTheNamedStubOnceAtEachTickOfSPlus1ToSPlus25_AndNeverWithTheNativeSwitchOff` | `:122` (Theory ×3 `:118-121`), journal `:130` et `:149` | `WaveSwordAi.StubCallsForTests` : 25 appels, un par tick de S+1 à S+25, zéro quand les natifs sont éteints |
| `AlundraWaveSwordSupport.WaveOracle.GrassCallTicks` | `:102-107` | lit `docs/plan-e20-annexe/e20-f3/oracle/out/wave_grass2.txt` (ligne « wave (slot 1) calls from ticks ») : les ticks attendus du V2 |

Pas des pins, mais des remises à nul du journal : `AlundraFlailBounceTests.cs:55`, `AlundraWaveSwordLifeTests.cs:53`, `AlundraWaveSwordWiringTests.cs:51`.

Le rapport citait quatre fichiers (`FlailBounce`, `WaveSwordLife`, `WaveSwordWiring`, `WaveSwordSupport`) ; `check.md` ajoutait `HeroChargeTests`. Au HEAD, `WaveSwordWiringTests` n'a rien d'épinglé (une remise à nul) et `HeroChargeTests` épingle le bouchon d'atterrissage (voir 3.2), pas l'herbe.

### 3.2 Même journal, autres bouchons de `CreatePlayerAnimationEffects` (hors CATE, tranche E20.e2)

Ils partagent `PlayerAnimationEffects.StubCallsForTests` ; seuls les tests à liste exacte (3.1, ligne 3) sont sensibles à un appel ajouté dans les états d'attaque.
- `AlundraFlailBounceTests.cs` : `:351` (alarme PV bas), `:405` (course), `:422` et `:440` (marche), `:452`, `:472`, `:480` (atterrissage), `:521` et `:536` (appels de `MovePlayer`, modes 0 et 1), `:677` `AssertStubsAtTheTicksOfTheOracle` (`:684` atterrissage, `:687` marche), `:715`, `:729`, `:739`.
- `AlundraHeroChargeTests.cs:654` (atterrissage, état `0x2D`, mode 1).
- Ils commentent aussi des exclusions « E20.e » : `AlundraHeroAttackTests.cs:157`, `AlundraHeroRunTests.cs:20`, `AlundraHeroSwimTests.cs:19-20` et `:59`, `AlundraRunSupport.cs:333`, `:361`, `:402` (sons 11, 12, 21, 6 non comparés).

### 3.3 Tests dont les valeurs supposent que CATE ne tire ni ne coupe rien (INFÉRENCE : non touchés, à confirmer en lançant la suite après câblage)

- Banc `JumpHeroRig` : champ `FlatCells.Create()` de 40 x 40 cases de marchabilité 0 (`AlundraJumpTestSupport.cs:451-457`), hôte `ContactHost : IScriptHost` non `IEntityWorldContext`. Aucune case coupable, aucun accès au magasin : `Flail`, `HeroCharge` (assertions `rng_after` `:244`, `:367`, `:453`, `:494`, `:587`, `:616`), `HeroAttack`, `Run`, `Swim` ne devraient pas changer.
- Monde réel de la carte 14 (`HitArena`, 16 sites d'appel, dont `WaveRun` à (212, 96)) : les cases coupables de la carte 14 commencent à la rangée 11 (`m14.py` : 179 cases ; rangées 11 à 55 ; aucune dans les rangées 0 à 10). Les arènes sont à (212, 96), (232, 96), (100, 96), (100, 100) et (252 - dx, 80 + dy), soit les rangées 5 et 6 ; la nage (`AlundraHeroSwimArenaTests.cs:27-29`) est à la case (40, 48), sans case coupable dans les colonnes 38-42 ni les rangées 46-50 (et CATE n'est appelé que dans les 15 états d'attaque). `AlundraHitPassTests.cs:348` joue à (172, 424, z 80) = case (7, 26), avec une case coupable (7, 24, hauteur 5) à deux rangées : recouvrement non calculé. Aucune coupe attendue dans ces arènes, non mesuré.
- `AlundraTestSavesTests.cs:62-91` : liste exacte de 9 préréglages (`AllPresetNames` `:72`, `TheNinePresets_AreListed_InTheOrderOfTheTable` `:88-91`) ; `herbe-m14` (e-T5 du rapport) obligera à la modifier. `arsenal-a-m14` arrive en case (8, 6, 0) de la carte 14 (`Alundra/SaveGames/TestSaves.cs`) : sans case coupable à portée.

### 3.4 Pins de primitives sur lesquelles la tranche s'appuie

`AlundraCellVisualSyncTests.cs:619` (bits : jamais de reconstruction), `:690` (bits et grille de navigation), `:722` (programme 772, bits seuls) ; `AlundraCellStoreTests.cs:136` (aller-retour Set/Clear), `:160` (bornes), `:242` (copie indépendante) ; `AlundraCellStoreProductionTests.cs:51`, `:75` ; `AlundraFlailBounceTests.cs` autour de `:320-330` (`TryReadBinaryCell` sur les tableaux du magasin).

### 3.5 Bouchon voisin de `DestroyEntity` ((1) lâcher, (4) libération de l'effet attaché), pour e0c-T3 et E20.e

`AlundraDestroyEntityBreakEffectTests.TheDestruction_DrawsNoNumber_AndCreatesNoEntity_EvenWithContents` (`:205`, assertion `:216`, plage `:204-219`) ; `AlundraBeannoideOracleTests.RunE1Call` (`:338`, assertion `:370`) ; `AlundraMurutaOracleTests.RunE1Call` (`:420`, assertion `:450`) ; exclusions documentées `AlundraBeannoideOracleTests.cs:27` et `AlundraEnemyHandlerOracle.cs:14`. Trouvés par la critique d'e0c (`e20-e0c/check.md` §3), recoupés ici.

## 4. Nouveautés depuis le rapport

- **N1. Restructuration de la DLL (ADR-0075).** R2 à R4 faites, R6 faite, R5 (tests en miroir) en attente de Q-R-3. Noms et chemins : `tools/dll-structure/table.json` et `table.md` font autorité ; plus de préfixe `Alundra`, un dossier = un namespace `Alundra.<Domaine>`, un type principal par fichier. Les types que la tranche touche sont dans `Alundra/Cells/`, `Alundra/Hero/`, `Alundra/Ai/Projectiles/`, `Alundra/Items/`, `Alundra/Rendering/Effects/`, `Alundra/Maps/`, `Alundra/Entities/`. Contraintes pour écrire la tranche : (a) tout nouveau `.cs` de la DLL doit figurer dans la table, sinon `check_names.py --tree` signale `[hors table]` (ADR-0075, « Constrained ») ; (b) la puce « Programme » de l'ADR-0075 écrit que le chantier tourne sur `chantier/restructuration-dll` sans autre tranche (E20, E14, E19) en parallèle ; (c) R5 déplacera les 333 fichiers de test (306 renommés, classes sans préfixe `Alundra`, `docs/plan-restructuration-dll.md` §9) : citer les tests par classe et méthode, pas par `fichier:ligne`. Destination des fichiers de test : INCONNUE tant que Q-R-3 n'est pas répondue.
- **N2. R0 (`146e1d8`).** Noms de script par nom complet (moteur : sous-module passé de `781d3527` à `92d8e847`, ADR moteur 0075 à 0077 sans effet sur CATE). Trois `catch (ScriptTypeResolutionException)` relancent au lieu de journaliser : `WorldProxy.cs:1122` (chargement de carte), `:3444` (`SpawnEntityByRecordId`), `:3580` (`SpawnEntityBySpriteIndex`). Conséquence pour le butin de CATE : un préfabriqué dont le type de script ne se résout pas fait désormais lever l'appel au lieu de rendre `null` ; la tranche devra décider quoi faire du retour nul (déjà exigé) et de l'exception. Deux tests ajoutés : `AlundraScriptAssemblyRegistration.cs`, `AlundraScriptNamesInTheDataTests.cs`.
- **N3. Un oracle MIPS de l'herbe existe déjà, non cité par les rapports e20-e** (`rg wave_grass` dans `docs/plan-e20-annexe/e20-e/` : aucun résultat). Écrit par E20.f3a T0 (commit `33c45907`, 2026-10-15) : `docs/plan-e20-annexe/e20-f3/oracle/out/wave_grass.txt` et `.json` (13 coupes vers l'est, 10 vers le nord, avec case, position de l'effet `(108.0, 104.0)` = colonne x 24 + 12 et rangée x 16 + 8, `rng_after` par événement, son `0x1F` journalisé 13 et 10 fois), `wave_grass2.txt` (appels de CATE : héros 67 appels, vague 25 appels de t118 à t142, coupes par tick), `wave_grass_zwindow.txt` (fenêtre de Z : `cellZ` 16 et 32 px, boîte du héros z=16.00002, taille z 14.99998 ou 30.99998). Scripts : `oracle/tools/wave_grass.py`, `wave_grass2.py`, `wave_grass3.py` ; `e20-f3/README.md:58`. Directement réutilisable pour le e-T0 du rapport (« rejouer sur la machine MIPS du dépôt »).
- **N4. Annexe e0c, écrite après les rapports e20-e** (`docs/plan-e20-annexe/e20-e0c/`, `b35a2c4`, 2026-10-17) : C255, A255, `GiveItem`, `SpawnRandomLoot`, `SpawnContents` ré-exécutés sur la machine MIPS (0 écart) ; sa critique ajoute le point des drapeaux VRAM des objets lâchés (`DestroyOnVramFlags`, `CombinedVramFlagsOR` nourri seulement pour le héros : un butin tombé dans l'eau ou un trou ne disparaîtrait pas) et un T3 élargi (3.5). Les questions Q-E0C-1 à Q-E0C-7 sont dans le plan §2 (`docs/plan-e20-heros.md:6705-6711`).
- **N5. Les questions Q-E1 à Q-E9 du rapport ne sont pas dans le plan.** `grep` de `Q-E1`..`Q-E9` dans `docs/plan-e20-heros.md` : aucune occurrence ; le plan §1.25 (l.6641-6649) résume la lecture et renvoie au « §9 du rapport » pour les tâches. Elles ne vivent que dans `report.md` §8. Aucune n'a reçu de réponse de l'auteur.
- **N6. Écart entre le plan et la critique sur la disparition du buisson.** Plan §1.25 : « la tuile disparaît à l'image suivante » ; `check.md` §1 « Rendu » : « la disparition du buisson est donc immédiate ». Côté DLL : la coupe du tick (dans `RunEntityBlock`, `WorldProxy.cs:2582`) est reconstruite par `FlushPendingOverlayReconstruction` à `:2650` dans le même `Update`.
- **N7. Faits de la DLL utiles à la tranche et absents des rapports.** (a) Précédent de lecture d'une propriété custom de carte : `BalanceTables.cs:173-178` (pour Q-E3, octet 9). (b) Les natifs du registre reçoivent `IEntityWorldContext` (M1b). (c) ADR-0066 (le flux aléatoire avance d'un pas par tick par un rappel de la scène d'effets) et ADR-0072 (D-F3-6 : « limits deferred : real grass cutting and the loot ... the random stream drift ») cadrent l'ordre des tirages de Q-E2. (d) Infrastructure de sauvegardes de test déjà en place : `Alundra/SaveGames/TestSaves.cs`, `docs/test-saves.md`, neuf préréglages (3.3).
- **N8. ADR datés du 2026-10-17 ou après : aucun** (`grep Date` dans `docs/decisions/`). Le dernier est l'ADR-0075 (en-tête 2026-10-16, committé `f2bdb4b` le 2026-10-17 10:46). Les ADR-0073 (tirage du butin à la création) et 0074 sont du 2026-10-16, antérieurs aux rapports.

## 5. Inconnus

- **U1.** Aucun test n'a été exécuté : l'absence d'effet de la tranche sur les suites existantes (3.3) est une INFÉRENCE tirée des champs de test (`FlatCells` à zéro, cases de la carte 14) et non d'une mesure.
- **U2.** Boîtes de frame des sprites 7 (flèche chargée C94) et 2 (bombe E12), non enregistrés : l'existence de données de collision est établie par `check.md` §5, leur cohérence reste à contrôler (e-T0).
- **U3.** Comportement voulu quand `CellMutator` est nul (monde dégradé, ou banc de test sans `IEntityWorldContext`) : les rapports ne le disent pas ; le binaire n'a pas de mode dégradé. À spécifier dans la tranche.
- **U4.** Q-E1 à Q-E9 du rapport (ordre butin/coupe, moment de l'effet, octet 9, cases sans pile, persistance, repère de Z, colonnes hors table, participation au tick, renommage de « BalanceLevel ») : sans réponse ; Q-E6 (repère de Z) reste ouverte selon `check.md` §6 ; Q-E7 est fermée par `check.md` §3.2 (hors table : tableau de pointeurs d'entités à `0x80127B28`, mot global à `0x80127164` pour les indices négatifs, valeur inconnue).
- **U5.** Destination des fichiers de test après R5 (Q-R-3 non répondue).
- **U6.** Chiffres du coût de l'overlay (1 816 + 2 360 entrées de la carte 16, 5 037 au maximum) non recomptés ici.
- **U7.** Les recouvrements exacts des arènes de test avec les cases coupables de la carte 14 (par exemple (172, 424, z 80) contre la case (7, 24)) n'ont pas été calculés avec les boîtes réelles.
- **U8.** Lignes exactes du plan citées par `plan-e20-heros.md` §1.25 au-delà de `l.6628` : non nécessaires, non toutes relevées.
