# sm12 : E19.sm1 (0x60 0x61 0x3C 0x3D) et E19.sm2 (0x6A 0x6B 0x6C 0x6D) traduits au HEAD

Lecture seule. Étiquettes : FAIT (commande ou ligne citée), INFÉRENCE, INCONNU. Chemins relatifs au dépôt `D:/development/repo/alundra-casaengine-project-converter` sauf mention. Rapport sauvegardé : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-state/sm12/state.md`. Les scripts de relevé (`linemap.py`, `hunks2.py`, `verify_sites.py`, `memo_check.py`, `riders_all.py`, `portals.py`) sont dans le même dossier.

## 0. Cadre

- **HEAD.** `317b3360`, branche `chantier/restructuration-dll-r3`. L'arbre est propre, sauf les sous-modules `CasaEngineMonogame` et `alundra-datas-analyser`, marqués `m`. Le HEAD a bougé de `b20eb8ce` à `317b3360` pendant le relevé : 3 commits `docs(plan)` qui ne touchent que `docs/plan-e20-heros.md` (FAIT : `git diff --stat b20eb8ce HEAD`).
- **Base de l'annexe.** `9502f75` (2026-10-17 00:39). `git log 9502f75..HEAD -- Alundra Alundra.Tests tools/AlundraTestSaves` donne 81 commits : 80 `refactor(dll)` (R2 déplacements, R3 renommages, R4 types cités) et `146e1d81` (R0 partie parent, 09:52).
- **Commits du 2026-10-16.** `0ff26e4a`, `073de215`, `683b9dcf`, `b5b2c888`, `4753357c` et `bda82292` sont ancêtres de `9502f75`. Ils sont donc déjà dans la base de l'annexe (FAIT : `git merge-base --is-ancestor`).
- **La DLL citée n'a changé que par la restructuration.**
  - Méthode : pour 16 fichiers cités, diff ligne à ligne de `9502f75:<ancien>` contre `HEAD:<nouveau>`, après application de la table `tools/dll-structure/table.json` (champ `final`).
  - Seules différences : lignes `namespace` et `using`, types coupés dans un autre fichier, `cref` de commentaires, et les 3 blocs `catch (ScriptTypeResolutionException)` de `146e1d81` dans `Alundra/Maps/WorldProxy.cs` (`:1121-1126`, `:3442-3447`, `:3579-3584`).
- **Les sites sont toujours dans l'export.** Les 316 sites (187 sm1 + 129 sm2) de `docs/plan-e19-sm-annexe/skipped-sites.tsv` ont le même opcode et les mêmes opérandes au même `pc` dans `alundra-project/Maps/**/events/*.events.json`, ré-exportés le 2026-10-17 21:51-21:52 (FAIT : `verify_sites.py`, 316 vérifiés, 0 écart).
- **Pièges de recherche.**
  - `.claude/worktrees/awesome-bun-0d3ca1/` et `.claude/worktrees/e13d-sub-inventory/` contiennent l'ancienne DLL (`Alundra/Scripts/...`) : un `rg` à la racine y trouve les anciens noms.
  - Les commentaires du code gardent les anciens noms de types (Q-R-5, ~300 occurrences).

## 1. Citations traduites

### 1.1 Correspondance des fichiers (stade `final` de `table.json`, arbre réel vérifié)

| Ancien (9502f75) | HEAD | Type (avant, après) |
|---|---|---|
| `Alundra/Scripts/AlundraEventProgramRunner.cs` | `Alundra/Events/EventProgramRunner.cs` | `AlundraEventProgramRunner`, `EventProgramRunner`. `EventTraceKind.cs` et `EventTraceRecord.cs` coupés dehors (décalage uniforme −50) |
| `Alundra/Scripts/EventProgramRunner.cs` (l'interface, 45 l.) | `Alundra/Events/IEventProgramRunner.cs` | `IEventProgramRunner` (+ `NoOpEventProgramRunner`) |
| `Alundra/Scripts/EventOpcodeSizeTable.cs` | `Alundra/Events/EventOpcodeSizeTable.cs` | inchangé |
| `Alundra/Scripts/EventProgramState.cs` | `Alundra/Events/EventProgramState.cs` | inchangé |
| `Alundra/Scripts/EntitySearchService.cs` | `Alundra/Entities/EntitySearchService.cs` | inchangé |
| `Alundra/Scripts/AlundraEntityScriptProxy.cs` | `Alundra/Entities/EntityScriptProxy.cs` (+ `.HitPass.cs`) | `AlundraEntityScriptProxy`, `EntityScriptProxy` |
| `Alundra/Scripts/AlundraWorldProxy.cs` | `Alundra/Maps/WorldProxy.cs` | `AlundraWorldProxy`, `WorldProxy` ; `AlundraMapEvent`, `MapEvent` (même fichier) |
| `Alundra/Scripts/EntitySupport.cs` | `Alundra/Physics/EntitySupport.cs` | inchangé |
| `Alundra/Scripts/EntityFlags.cs` | `Alundra/Entities/EntityFlags.cs` | `EntityPickupKind` coupé vers `EntityPickupKind.cs` |
| `Alundra/Scripts/AlundraScriptedMotion.cs` | `Alundra/Physics/ScriptedMotion.cs` | `ScriptedMotion` |
| `Alundra/Scripts/IEntityWorldContext.cs` | `Alundra/Maps/IEntityWorldContext.cs` | `NoOpEntityWorldContext` coupé vers `NoOpEntityWorldContext.cs` |
| `Alundra/Scripts/AlundraEntitySpawnFactory.cs` | `Alundra/Entities/Spawning/EntitySpawnFactory.cs` | `EntitySpawnFactory` |
| `Alundra/Scripts/AlundraPlayerManager.cs` | `Alundra/Hero/PlayerManager.cs` | `PlayerManager` |
| `Alundra/Scripts/AlundraMapSizeReader.cs` | `Alundra/Maps/MapSizeReader.cs` | `MapSizeReader` |
| `Alundra/Scripts/AlundraCellStore.cs` | `Alundra/Cells/CellStore.cs` ; interface dans `Alundra/Cells/ICellMutator.cs:21` | `AlundraCellStore`, `CellStore` ; `IAlundraCellMutator`, `ICellMutator` |
| `Alundra/Scripts/AlundraGameState.cs` | `Alundra/Session/GameState.cs` | `GameState` |
| `Alundra/Scripts/AlundraTickPad.cs` | `Alundra/Hero/TickPad.cs` | `TickPad` |
| `Alundra.Tests/*` | mêmes chemins, mêmes noms de classes de test | R5 non exécutée. Aucun nom de type de test ne diffère entre `9502f75` et HEAD. 2 fichiers neufs de R0 : `AlundraScriptAssemblyRegistration.cs`, `AlundraScriptNamesInTheDataTests.cs` |

**Piège de nom.**
- « `EventProgramRunner.cs` » dans le rapport général (§2) désigne l'interface, c'est-à-dire HEAD `IEventProgramRunner.cs`.
- « `AlundraEventProgramRunner.cs` » désigne la classe, c'est-à-dire HEAD `EventProgramRunner.cs`.
- Au HEAD, `EventProgramRunner.cs` est donc l'autre fichier.

### 1.2 Citations de la DLL

Sources : P = plan `docs/plan-e19-opcodes.md` §1.4 et §3 ; S = `sm1-sm4/report.md` ; C = `sm1-sm4/check.md` ; G = `report.md` général ; GC = `check.md` général ; O = `opcodes.tsv`. Statut « identique » : même ligne de code, seul le numéro change.

| Ancienne | HEAD | Statut |
|---|---|---|
| `AlundraEventProgramRunner.cs:613-2154` Dispatch [P, G, O] | `Alundra/Events/EventProgramRunner.cs:563-2104` | identique ; 158 `case` recomptés par script sur 563-2104 |
| `:2152-2153` `default` / `return UnknownOpcode` [S §4] | `:2102-2103` | identique |
| `:3115-3141` `UnknownOpcode` [P, S, C, O] | `:3065-3091` | identique |
| `:377-419` `InitializeEventData` [S §2] | `:327-369` (corps `:337-369`) | identique |
| `:1990-2040` précédents 0x3E/0x3F [S §4.1] | `:1940-1990` (0x3E `:1940-1959`, 0x3F `:1961-1985`, 0x42 dès `:1987`) | identique ; la plage mord de 5 lignes sur 0x42, déjà le cas en 9502f75 |
| `:2011` et `:~2011` (T2 : « 4 case après 0x3F ») [S §7] | `:1961` ; point d'insertion `:1986` (entre `:1985` et `case 0x42` `:1987`) | identique |
| `:2011-2013` commentaire N inclus [C R1] | `:1961-1963` | identique |
| `:2691` `IsRidingTheLogicEntity` [S §4.1] | `:2641` | identique |
| `:3312-3334` `EntityInArea` [S §4.1] | `:3262-3284` | identique |
| `:2700-3050` attentes à mémo [S §5] | `:2650-3000` (`Wait` `:2650`, `Walk` `:2708`, `WaitZDistance` `:2761`, `WaitHeightTarget` `:2791`, `RepeatAnimation` `:2856`, `WalkUntilBeyondRadius` `:2901`, `TryEngageDetour` `:2980`) | identique |
| `:617-618` 0x01 [GC] ; `:452-466` ; `:428-518` ; `:213-296` ; `:137` ; `:436-446` ; `:478-479` [G] | `:567-568` ; `:402-416` ; `:378-468` ; `:163-246` ; `:87` ; `:386-396` ; `:428-429` | identiques |
| sm3/sm4, pour mémoire : `:250-254`, `:275-280`, `:760-762`, `:1213-1222`, `:1879`, `:1611-1624`, `:3150-3189` [S, G] | `:200-204`, `:225-230`, `:710-712`, `:1163-1172`, `:1829`, `:1561-1574`, `:3100-3139` | identiques |
| `EventOpcodeSizeTable.cs:10-11` (« 197 ») [P, G, GC] | `Alundra/Events/EventOpcodeSizeTable.cs:10-11` | identique, même ligne |
| `:40-246` `Entries` [G] ; `:106` (0x3C) ; `:107` (0x3D) ; `:142` (0x60) ; `:143` (0x61) ; `:152-155` (0x6A-0x6D) [S, O] | mêmes lignes, dans `Events/` | identiques ; tailles 7,7,2,2,1,3,3,3 relues, libellés non corrigés |
| `:184`, `:199`, `:217-218`, `:228-231` (sm3/sm4) | mêmes lignes | identiques |
| `EntitySearchService.cs:98-262` [S §3] | `Alundra/Entities/EntitySearchService.cs:99-263` | identique (+1 `using Alundra.Maps;`) |
| `AlundraEntityScriptProxy.cs:19` `IsLoadedNormalOrDeactivated` [S §4.1] | `Alundra/Entities/EntityScriptProxy.cs:32` | identique |
| `:76-77` Hp/HpMax ; `:81` ; `:325` ForceZ ; `:341-343` TileX/Y/Z ; `:344` RidingEntity ; `:351` XCollisionEntity [S] | `:89-90` ; `:94` ; `:338` ; `:354-356` ; `:357` ; `:364` | identiques (+13) |
| `:1212-1213` LastTarget* ; `:1663` PickEventTrigger ; `:1761-1778` RunPickedEvent ; `:2411-2418` XCollisionEntity écrit [S, G] | `:1225-1226` ; `:1676` ; `:1774-1791` ; `:2424-2431` | identiques |
| `AlundraWorldProxy.cs:299` ; `:874-876` ; `:2077` ; `:2453-2454` ; `:2493` ; `:2893` [S, G] | `Alundra/Maps/WorldProxy.cs:327` ; `:902-904` ; `:2111` ; `:2487-2488` ; `:2527` ; `:2927` | identiques (+28 à +34) |
| `:2955-2960` note D-E20-13 [S §4.1] | `:2989-2994` | identique, mais le texte est périmé sur un point (voir A9) |
| `:3128` ; `:3193` ; `:2634` [G] | `:3162` ; `:3227` ; `:2668` | identiques |
| `:3652-3653` `BuildCollidables` + `UpdateRidingEntities` [S §4.1] | `:3698-3699` (méthode `RefreshUpdateProxiesAndCollidables` `:3687`) | identique ; appels aussi aux lignes `:1139`, `:2660`, `:2912`, `:2965` |
| `EntitySupport.cs:71-75` `IsEligibleSubject` [S §0.2, §4.1] | `Alundra/Physics/EntitySupport.cs:72-76` (corps `:74-76`) | identique |
| `:276-290` `UpdateRidingEntities` ; `:296-307` `UpdateRidingEntityOfHero` [S] | `:277-291` (corps `:277-289`) ; `:297-308` | identiques |
| `EntityFlags.cs:103,136` Gravity, NoRiders [S] | `Alundra/Entities/EntityFlags.cs:103,136` (Collidable `:92`) | identiques, même ligne |
| `AlundraScriptedMotion.cs:191` ; `:719-720` [S] | `Alundra/Physics/ScriptedMotion.cs:197` ; `:725-726` (TileZ `:727`) | identiques |
| `IEntityWorldContext.cs:24` `SpawnedEntities` [S] | `Alundra/Maps/IEntityWorldContext.cs:35` | identique |
| `AlundraEntitySpawnFactory.cs:493` [S §3] | `Alundra/Entities/Spawning/EntitySpawnFactory.cs:497` | identique (commentaire de doc) |
| `AlundraPlayerManager.cs:610-631` ; `:1750` ; `:1885` ; `:1957` (sm3/sm4) | `Alundra/Hero/PlayerManager.cs:623-644` ; `:1763` ; `:1898` ; `:1970` | identiques |
| `AlundraGameState.cs:143` ; `AlundraTickPad.cs:31-49` (sm4) | `Alundra/Session/GameState.cs:152` ; `Alundra/Hero/TickPad.cs:33-51` | identiques |
| `AlundraCellStore.cs:13-40` `IAlundraCellMutator` (sm3) | `Alundra/Cells/ICellMutator.cs` (interface `:21`) | type coupé, pas de correspondance ligne à ligne |
| `AlundraMapSizeReader.cs` (sans ligne) | `Alundra/Maps/MapSizeReader.cs` | fichier renommé |
| `opcodes.tsv`, colonne `dll_handler` : `Alundra/Scripts/AlundraEventProgramRunner.cs:3115` | `Alundra/Events/EventProgramRunner.cs:3065` | identique |

### 1.3 Citations des tests

| Ancienne | HEAD | Statut |
|---|---|---|
| `AlundraClassAndBranchOpcodesTests.cs:140-216` (0x3F) [S §4.1] | `Alundra.Tests/AlundraClassAndBranchOpcodesTests.cs:143-219` ; classe `:21`, `Context` `:25`, `Document` `:37`, `NewEntity` `:44`, `Run` `:47` | identique (+3 `using`) |
| `IntroTraceHarnessTests.cs:322` `ImplementedOpcodes` [G] | `Alundra.Tests/IntroTraceHarnessTests.cs:332` | identique |
| `AlundraStoryChainOpcodeAudit.cs:128-338` (port de census.py) [G] | `Alundra.Tests/AlundraStoryChainOpcodeAudit.cs:130-340` (`WalkMap` `:130`, `LoadMap` `:343`, `IsPorted` `:75`) | identique (+2) |
| `AlundraEventProgramRunnerTests.cs:404-433` [G] | `:412-441` | identique |
| `AlundraDialogueOpcodesProductionTests.cs:17-31` [G] ; `AlundraYarnVariableStorageTests.cs:120` [G] | `:23-37` ; `:123` | identiques |
| `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` [G] | même chemin ; en-tête seul (liste vide) | identique |
| sm1-sm4 T1/T3 : aucun nom de test cité | — | rien à traduire |

### 1.4 Citations de docs (hors DLL et tests, pour mémoire)

| Ancienne | HEAD | Statut |
|---|---|---|
| `docs/plan-e20-heros.md:6287` (`PlatformEntity` : +0x28 rangé par 3 sites, le héros) [S §0.9] | `:6525` | décalé de +238 |
| `docs/plan-e20-heros.md:6321` (E20.g1a, D-G-2) [S §4.3] | `:6559` (« Rien n'existe ») et `:6584` (D-G-2) | décalé de +238 |
| `docs/plan-e19-opcodes.md` §1.4 | `:12034-12097` ; Q-E19-SM1 `:12474` ; Q-sm-1 à Q-sm-8 `:12475-12482` | à jour |

## 2. Affirmations sur la DLL : vraies ou non au HEAD

| # | Affirmation (source) | Au HEAD | Preuve |
|---|---|---|---|
| A1 | `Dispatch` n'a aucun `case` pour 0x3C 0x3D 0x60 0x61 0x6A 0x6B 0x6C 0x6D ; 158 `case` [P, S] | VRAI | `rg "case 0x(3C\|3D\|60\|61\|6A\|6B\|6C\|6D):" Alundra` : 0 résultat. Étiquettes `case 0xNN:` sur `EventProgramRunner.cs:563-2104` : 158 |
| A2 | Table des tailles : 198 entrées, commentaire « 197 », 0xC5-0xFE absents [P] | VRAI | `EventOpcodeSizeTable.cs:11` ; 198 motifs `{ 0xNN, new(` |
| A3 | Tailles 7,7,2,2,1,3,3,3 égales au binaire ; libellés faux ou absents [S §4, C] | VRAI | `EventOpcodeSizeTable.cs:106-107,142-143,152-155` ; libellés « If entity x < threshold » etc. toujours là. Binaire : `Result` = 1 quand la distance est supérieure ou égale au seuil [C §2.4] |
| A4 | Un opcode non porté passe par `UnknownOpcode`, saute par sa taille, `Result` reste périmé, un avertissement par opcode ; sans taille : fin d'appel [P, S] | VRAI | `EventProgramRunner.cs:3065-3091` ; `default` `:2102-2103` ; `_loggedUnknownOpcodes.Add` |
| A5 | `EntitySearchService` égale le binaire pour les 12 types de groupe et l'identifiant brut [S §3, C §2.2] | VRAI à la relecture du code | `EntitySearchService.cs:99-263` relu contre les 12 entrées de S §3. Type 4 (`:157-169`) = Collidable ∧ AnimFlags&0x80==0 ∧ PlatformEntity==null ; type 1 renvoie `playerEntity` ; types 5 à 11 excluent `IsPlayer` |
| A6 | La branche brute parcourt aussi le héros, invisible tant que son `EntityRefId` vaut −1 ; les proxies nus de test valent 0 [S §3] | VRAI | `EntitySearchService.cs:105-117` ; `WorldProxy.cs:2111` ; `EntityScriptProxy.cs:124` (`public int EntityRefId;`, défaut 0) |
| A7 | `SpawnedEntities` contient le héros [S §4.1, pour 0x3D] | VRAI en production, NON GARANTI en test | `WorldProxy.cs:2304` `InsertBySlot(entity, 0)`, doc `IEntityWorldContext.cs:28-34`. La fausse `Context` des tests garde `Spawned` et `PlayerEntity` séparés (`AlundraClassAndBranchOpcodesTests.cs:25-35`) ; 0x3F teste `PlayerEntity` d'abord (`EventProgramRunner.cs:1965-1980`) |
| A8 | `RidingEntity` est typé `Entity?` et porte le `LogicContextEntity` du porteur ; écrit seulement par `UpdateRidingEntities` / `UpdateRidingEntityOfHero` (+ clone) [S §4.1] | VRAI | `EntityScriptProxy.cs:357` ; écritures `EntitySupport.cs:287`, `:305`, clone `EntityScriptProxy.cs:2662` |
| A9 | `RidingEntity` « recalculé une fois par image » (`WorldProxy.cs:3652-3653`, D-E20-13) [S §4.1] | **PÉRIMÉ** | Voir le détail ci-dessous |
| A10 | Le recalcul ne couvre que les collidables `(Flags & (Gravity\|NoRiders)) == Gravity`, les autres gardent leur dernière valeur ; la passe des compteurs n'efface pas `RidingEntity` [S §2, §4.1] | VRAI | `EntitySupport.cs:281-288` (le `continue` laisse l'ancienne valeur) ; `WorldProxy.cs:2991-2994` ; O-E20-9 (`docs/plan-e20-heros.md:9125`) ; l'étude D5b M4 (`docs/plan-e19-opcodes.md:4102-4123`) propose de l'effacer. Le binaire l'efface à `0x80038998` [S §2]. Un candidat qui cesse d'être collidable garde donc un `RidingEntity` périmé dans la DLL |
| A11 | `PlatformEntity` n'est jamais écrit par la DLL, sauf la copie du clone [S §0.9, C §2.8] | VRAI | déclaré `EntityScriptProxy.cs:101` ; seule affectation `:2609` (clone). Les autres correspondances de `rg "PlatformEntity\s*="` sont des `== null` et des commentaires (`EntitySearchService.cs:163`, `EntitySupport.cs:68,76`, `EntityCollision.cs:19`). Le plan E20 range l'écriture dans E20.g1a (`docs/plan-e20-heros.md:6584`, rien d'exécuté) |
| A12 | `IsEligibleSubject` est exactement le prédicat de 0x3C [S §0.2, §4.1] | VRAI | `EntitySupport.cs:74-76` ; `internal static`, accessible depuis `Alundra.Events` (même assembly) |
| A13 | `TileX/Y/Z` existent ; `TileZ = PosZ >> 20` [S §4.1] | VRAI | `EntityScriptProxy.cs:354-356` ; écrits `ScriptedMotion.cs:725-727`, `EntityScriptProxy.cs:895,999,1139` |
| A14 | Patron `EntityInArea` pour 0x3D [S §4.1] | PARTIEL | `EventProgramRunner.cs:3262-3284` fait une recherche (`GetMatchingEntitiesBySearchType`) puis le test de boîte ; 0x3D n'a pas de recherche. Le patron « boîte inclusive » est `case 0x3B` (`:1789-1810`), sur le héros seul |
| A15 | `Parameters` = 10 entiers, `Result` public, persistants pour B/C et à travers `InitializeEventData` [S §4.2, §2] | VRAI | `EventProgramState.cs:32` (`new int[10]`), `:36` (`Result`). `InitializeEventData` `:327-369` n'écrit que `Parameters[0]`, `CodeIndex`, `Codes`, `Sp`. `RunScript` `:163-246` (B et C reprennent, A/D/E/F partagent `_slotAScratchState`). Épinglé par `AlundraInitializeEventDataTests.cs` M9a-M9d (`:28`, `:47`, `:87`, `:111`) |
| A16 | `Parameters[4]` n'est écrasé par aucun opcode porté ; P2 par 0x0B 0x1C 0x1D 0x1E 0x1F 0x20-0x23 0x37 ; P3 par 0x0B 0x1E 0x1F [S §5] | VRAI | `rg "Parameters\[[3-9]\]" Alundra` : rien hors `EventProgramRunner.cs`, aucune `Parameters[4]`. Écritures P2/P3 : `Wait` `:2655`, `Walk` `:2715-2716`, `WaitZDistance` `:2766`, `WaitHeightTarget` `:2814`, `RepeatAnimation` `:2861,2880,2885`, `WalkUntilBeyondRadius` `:2908-2909`, détour `:2991-2992` |
| A17 | `Dispatch` reçoit l'entité logique et l'état du propriétaire [S §2] | VRAI | `RunOneScriptCall` `:378-468` : `logic = entity.LogicEntity ?? entity`, `Dispatch(command, logic, entity, ...)` ; signature `:563` |
| A18 | La DLL est à `PosZ` = binaire −1, sans effet sur `\|PosZ - mémo\|` [S §4.2] | VRAI | ADR-0026 (`docs/decisions/0026-absolute-z-writes-...md`) |
| A19 | Liste fermée de la chaîne vide ; aucun site sm1/sm2 sur les 30 cartes de la chaîne ni les 8 cartes centrales [P, G] | VRAI | `Data/story-chain-skipped-opcodes.tsv` (en-tête seul). Cartes sm1 {27,29,30,77,78,113,136,159,226,398,400}, sm2 {55,56,57,59,60,307,344,442} : aucune dans `ChainMaps` (`AlundraStoryChainOpcodeAudit.cs:39-43`) |
| A20 | Aucun arc existant ne passe par une carte sm1/sm2 | VRAI | Les `new ArcSpec(` visent les cartes 10, 135, 162, 165, 176, 178, 179, 185, 303, 390, 478, 36, 47, 115, 127, 260, 346, 363 et la famille « Overworld 3… ». Les seuls tests qui touchent ces cartes sans arc : `AlundraEventProgramRunnerTests.cs:4156-4181` (`IsForceAdjusted_0x6E_RealOccurrence_AncientShrine27`, lit le code réel de la 27, octet 546 = `0x6E`, convention « self-skip » si l'export manque) et `BackdropStageDefinitionTests.cs:76` (fond de la 159) |
| A21 | Sites : 0x60 172 (7 cartes), 0x61 14 (4), 0x3D 1, 0x3C 0 ; 0x6A 59, 0x6B 29, 0x6C 12, 0x6D 29 [P] | VRAI | recompté sur `skipped-sites.tsv` : sm1 = 187 sur 11 cartes, sm2 = 129 sur 8 cartes |
| A22 | 11 lectures de 0x6D (cartes 55, 56, 57) sans 0x6A dans leur programme [S §5] | VRAI | reproduit par balayage linéaire (`memo_check.py`) : 55 C[20-24] (5), 56 C[1-4] (4), 57 C[4] et C[7] (2). Toutes les lectures 0x6B/0x6C ont un 0x6A plus haut dans leur programme |
| A23 | Quatre lectures X de la 307 sont masquées par le CFG de `memo_flow.py` (appels `0x78`) [C O5] | VRAI | `307 C[1]` : `6A @426`, `78 2C 00 @430`, `6B 3C 00 @433` ; `6A @444`, `78 1A 00 @448`, `6B @451` ; idem C[2] `@498`, `@516` (décodage direct) |
| A24 | « 0 chemin d'écrasement » du mémo sur le corpus [S §5, C §2.4] | NON REJOUÉ | repose sur `memo_flow.py` (gelé) et la critique ; le test statique C# prévu par sm2 n'existe pas |
| A25 | « Les boucles de la décompilation sont exclusives, elle perd le dernier slot » [S §0.4, §1] | RÉFUTÉ par C R1 | la phrase subsiste dans `sm1-sm4/report.md` (§0.4, tableau « mode décompilation », §4.1) ; le plan `:12090-12092` porte déjà la correction |
| A26 | « Blocs poussables » des cartes 27, 29, 30, 113, 400 [P Q-sm-5] | INEXACT pour la 27 | `Ancient Shrine-27.tileMap`, couche `Entities` : `Index` 5 à 8 = « Objet étoile / lune / eau / soleil (transportable) » ; `Index` 1 à 4 = « Plaque à motif » (sprite 108). `sprite-records.json` : `ProgramTick` 4 pour les objets (INFÉRENCE : C4 natif = « pierres, tonneaux, blocs », O-E20-21 `docs/plan-e20-heros.md:9138`) |
| A27 | Mesure des cavaliers réels (flags) : à faire en T0 [S §4.1, §7] | PARTIELLEMENT FAIT ici | Voir le détail ci-dessous |

**Détail de A9.**
- Depuis E20.a (`b68fefca`, 2026-10-09, antérieur à l'annexe), `RefreshUpdateProxiesAndCollidables` tourne à l'étape « lists » de chaque tick :
  - dans `WorldProxy.cs:2962-2966` (appel `:2965`), dans `RunEntityBlock` `:2905`, appelé par tick `:2582` ;
  - et dans `:2907-2915` (monde gelé, appel `:2912`).
- Le chemin « une fois par image » (`:2655-2660`) ne sert que si `!DrivesEntityTicks` (`:821`, `_world?.Game != null`).
- Le commentaire `:2991-2994` répète « once per frame » et est lui-même périmé.
- Le héros est en plus recalculé en tête de chacun de ses ticks (`ScriptedMotion.cs:197`).

**Détail de A27.**
- Méthode : `Flags = MoreFlags | CanPickup<<8 | FlagsPortraitShadowType<<16` (`WorldProxy.cs:1992`, `EntitySpawnFactory.cs:740`) appliqué à `alundra-project/Data/sprite-records.json` (`riders_all.py`).
- Résultat : pour les 170 sites 0x60 à identifiant brut (cartes 27, 29, 30, 113, 159, 400), le propriétaire du programme C (la plaque) a le bit Collidable (`0x80`) et le candidat passe la grille `(Flags & 0x4100) == 0x100`.
- Valeurs : plaque `0x60C0`, `0x20C0` ou `0x32080` ; objet `0x332384` ou `0x3323E4`.
- Restent : les 2 sites `60 81` (carte 136, le héros : flags non lus), les 14 sites 0x61, et `AnimFlags & 0x80` (par animation).

## 3. Tests qui épinglent les opcodes sautés ou leurs sites

Rien ne nomme 0x3C 0x3D 0x60 0x61 0x6A-0x6D comme opcode dans `Alundra.Tests`. FAIT : `rg -i "0x(3C|3D|60|61|6A|6B|6C|6D)\b" Alundra.Tests` ne ramène que des constantes sans rapport, par exemple `AlundraHeroSwimBranchTests.cs:140` `[InlineData(0x3D)]` (identifiant d'animation). `opcodes.tsv` donne `tests_named_methods = 0` pour les huit.

| Garde ou test (HEAD) | Ce qu'il épingle | sm1 déplace | sm2 déplace |
|---|---|---|---|
| `IntroTraceHarnessTests.cs:332` `ImplementedOpcodes` (miroir à la main) | l'étiquette `[implemented]` / `[NOT IMPLEMENTED]` du dump de la carte 389 (`:1788-1791`) | + 0x3C 0x3D 0x60 0x61 (précédent E19.l1/l2, plan `:6688`, `:6794`). Étiquette seule : aucun site sur la 389 | + 0x6A-0x6D |
| `AlundraStoryChainOpcodeAudit.cs:75` `IsPorted` (exécute le vrai runner sur `[op, 0…, 0xFF]`) | « porté » = ni `UnknownSkipped` ni `UnknownNoSizeTerminated` | bascule seul, 4 opcodes | bascule seul, 4 opcodes |
| `AlundraStoryChainOpcodeAudit.cs:57` `PredicateOpcodes` (règle 4) | prédicats à `Result` qui ne doivent pas être sautés au niveau S | précédent : E19.l2 y a ajouté 0x83 et 0x9F. Facultatif ici (0 site de la chaîne) | idem |
| `Data/story-chain-skipped-opcodes.tsv` + `AlundraStoryChainSkippedOpcodesTests.cs:74-86` | liste fermée **de la chaîne seulement** ; le test interdit toute carte hors `ChainMaps` | **aucun effet** : liste déjà vide, aucun site sm1 sur la chaîne. La clause E19.l « une ligne de moins dans la liste fermée » n'a pas de référent sans sm0 | idem |
| `AlundraStoryChainSkippedOpcodesTests.cs:103-111` (oracle `IsPorted`) | au moins un opcode de taille connue reste sauté | reste vrai (0x56, 0xB9, 0xBB, 0xC2 restent) | idem |
| `AlundraEventProgramRunnerTests.cs:331` `UnknownOpcode_KnownSize_SkipsBySize` | utilise 0xB3 comme « sauté de taille connue » ; `:346` utilise 0x5F ; `:361` 0xC5 | rien | rien. sm4 devra changer l'opcode du premier (précédent du commentaire `:333-334` : 0x93, 0x4C, 0x08 déjà remplacés) |
| `AlundraInitializeEventDataTests.cs:28,47,87,111` (M9a-d) | `Parameters[1..9]` survivent à `InitializeEventData` | rien | appui du mémo (sm2 T1 « mémo qui survit à une réinitialisation ») |
| `AlundraZWaitOpcodesTests.cs`, `AlundraHeightWaitOpcodesTests.cs`, tests `Wait`/`Walk` de `AlundraEventProgramRunnerTests.cs` | écritures de P2/P3 par 0x20-0x23, 0x37, 0x1E | rien | l'« écrasement fidèle » de sm2 T1 doit s'appuyer sur eux |
| `EntitySearchServiceTests.cs` (18 tests), `AlundraClassAndBranchOpcodesTests.cs:143-219` (0x3E/0x3F) | recherche types 5/6 (`RidingEntity`), `RidingEntity` du héros | doivent rester verts (sm1 réutilise la recherche) | rien |
| `AlundraArcSupport.cs:342` `ArcsThatSlide` | tout arc `RealController` finit avec 0 pas de glissement le long d'un mur (sauf A10J = 1, A14 = 2), vérifié à `Dispose` (`:349-370`) | tout nouvel arc `RealController` y est soumis | idem |
| `AlundraArcSupport.cs:332` `ArcsWithoutEntityContact` | liste nommée d'arcs sans blocage par entité | un arc nouveau n'y est pas | idem |
| `AlundraMissingAnimationWaitCorpusTests.cs:164`, `AlundraDialogueSpeakerOpcodeTests.cs:232` (balayages du corpus par la table des tailles) | tailles seules | rien (aucune taille ne change) | rien |
| traces à l'octet (carte 389, `IntroTraceHarnessTests`) | dump des programmes de la 389 | rien (0 site sm1/sm2 sur la 389) | rien |

## 4. Banc et arcs

- **Arcs du plan §1.3** (`docs/plan-e19-opcodes.md:12013-12026`) : A0 à A9 sur les cartes 390, 476, 478, 392, 391, 416, 163, 172. Aucun ne touche une carte sm1/sm2. Ils vivent dans `AlundraShipArcTests.cs:90,133,201` (A0b, A1, A1c) et d'autres fichiers d'arcs. Aucun n'est réutilisable tel quel.
- **`ArcRun`** : `Alundra.Tests/AlundraArcSupport.cs:94`.
  - `ArcSpec` `:62-64` : `Name, Zone, WorldName, Flags[], HeroTileX/Y/Z, FrameLimit, RealController, Prefabs, Arrival`.
  - Constructeur `:175-324` : charge la carte réelle par `WorldProxy.InitializeWithWorld` et `FindProjectRoot()`, injecte les dialogues (`:269-275`).
  - Méthodes : `OneFrame` `:430`, `Press` `:454`, `PlaceHero` `:470`, `RunUntil` `:483` (échec nommé au `FrameLimit`), `Trace` `:132`, `SkippedOrExceeded` `:414`, `EntityByRecord` `:390`, `Entities` `:387`.
  - Crochets : `OnInstruction` `:136`, `OnFrame` `:140`.
- **Précédent d'arc sur une carte quelconque du corpus par numéro** : `AlundraHeightTargetSitesTests.cs:76-90` (33 instances de 17 sites réels, 0x21-0x23).
  - Le dossier de la carte est retrouvé par `Directory.GetDirectories(Maps).SelectMany(...).First(d => ...EndsWith("-" + map))`.
  - `ArcSpec` avec `RealController: true, Prefabs: true`, `Arrival`, `OnInstruction` / `OnFrame`.
  - Même motif : `AlundraRecordBirthClockTests.cs:290-303` (carte 36). C'est le modèle pour des « arcs sur des sites réels ».
- **Pas de fausse `IEntityWorldContext` partagée.** Chaque fichier de test déclare la sienne en privé (`AlundraClassAndBranchOpcodesTests.cs:25-35`, `AlundraDialogueFlagMarkerTests.cs:62`, `AlundraDialogueOpcodeDispatchTests.cs:75`, `AlundraDialogueYarnRenderingTests.cs:49`), avec ses aides `NewDocument` / `NewRunner` / `NewEntity` (`AlundraEventProgramRunnerTests.cs:28,38,41`). Un fichier de test neuf copie donc ce montage (précédent E19.l1 « nouveaux fichiers de tests », plan `:6692`).
- **Exécution synthétique d'un script** : `AlundraClassAndBranchOpcodesTests.cs:21-67`. `Context : IEntityWorldContext`, `Document(params int[])`, `NewEntity`, `Run(codes, entity, context, result, codeIndex, parameter0, trace)` fait `new EventProgramRunner(document, new GameState(), context)` puis `RunOneScriptCall(entity, state)`. `ResultBefore = 7`.
- **Exécution statique sur une carte réelle**.
  - `AlundraStoryChainOpcodeAudit.LoadMap` `:343` : index `Maps/world-index.json`, `MapEventProgramLoader.Load`, un `*.tileMap` unique.
  - `WalkMap` `:130`, `ReadRoots` `:294`, `FindListPath` `:367`.
  - Énumération des 483 cartes : `AlundraMissingAnimationWaitCorpusTests.cs:396`.
  - Racine de l'export : `SaveGameDirectorTestSupport.FindProjectRoot()` (`SaveGameDirectorTestSupport.cs:70`).
- **Oracle de valeurs de l'annexe** : `docs/plan-e19-sm-annexe/sm1-sm4/work/oracle/oracle.py` (649 lignes ; `m60 :228`, `m61 :235`, `m3c :253`, `m3d :258`, `m6a :263`, `m6b-m6d :277-287`), `emu.py`, `lib.py`.
  - Il lit `ALUN_CD.EXE` à `D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE` (`lib.py:3`). Le fichier existe, SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`, celui de l'annexe.
  - `oracle.py` n'imprime que des comptes d'écarts et des histogrammes (`oracle_binary.txt`) : il n'écrit aucun vecteur.
- **Précédent de lecteur de vecteurs dans les tests** : `Alundra.Tests/AlundraSpawnIndexOracle.cs:37-51`. Il cherche `docs/plan-e20-annexe/e20-e0a/oracle/out` en remontant depuis `AppContext.BaseDirectory` ; « les lignes sont les valeurs attendues », `:31`. Voir aussi `AlundraAttackOracle.cs`, `AlundraHitOracle.cs`, `AlundraLootOracle.cs`, `AlundraEnemyOracle.cs`. Aucune classe équivalente n'existe pour sm1/sm2.
- **Cartes d'arc possibles** (zone / monde, d'après `world-index.json`) : 27 `Ancient Shrine / Ancient Shrine-27` ; 400 `Desert / Desert _ Ancient Shrine-400` ; 226 `Inoa / Inoa-226` ; 398 `Lurvy House, Well / Lurvy House, Well-398` ; 344 `Lake Shrine / Lake Shrine (inner)-344` ; 55/56/59/60 `Torla Buggy_ with bar music and random texts / ... (early version of Torla)-NN`.

## 5. Noms nouveaux (ADR-0075 et garde de fichiers ajoutés)

**Règles ADR-0075** (`docs/decisions/0075-...md`, règles 1 à 4) : un dossier = un namespace `Alundra.<Domaine>` ; aucun préfixe `Alundra` dans les types de la DLL ; un type principal par fichier ; nom unique parmi DLL, moteur, MGUI, paquets, BCL.

**Garde Q-R-7** (`docs/plan-restructuration-dll.md:1234`, proposition) : après le chantier, tout fichier ajouté à la DLL fait échouer `python tools/dll-structure/check_names.py --tree --stage final` (`[hors table]`, `check_names.py:98`), car `table.json` est générée sur `5434cf6`. La tranche R7 (mode `--rules`) n'est qu'une proposition ouverte. La garde ne lit que la DLL (`scan_types.dll_decls`), pas les fichiers de tests.

| Élément proposé par sm1/sm2 | Nom final | Garde Q-R-7 |
|---|---|---|
| 8 blocs `case` dans `Dispatch` | `Alundra/Events/EventProgramRunner.cs`, namespace `Alundra.Events` ; aucun type, aucun fichier neuf | non déclenchée |
| aides privées éventuelles (comparaison null-sûre du porteur, test de seuil) | membres privés statiques de `EventProgramRunner` (modèle `IsRidingTheLogicEntity` `:2641`) ; non nommées par la proposition | non déclenchée (types imbriqués et membres ignorés) |
| 8 libellés de la table | `Alundra/Events/EventOpcodeSizeTable.cs:106-107,142-143,152-155` (modifs de texte) | non déclenchée |
| tests T1, test statique de sm2, arcs T3 | **aucun nom donné** dans le plan ni le rapport. Convention du HEAD : `Alundra.Tests/Alundra<Sujet>Tests.cs`, classe `Alundra<Sujet>Tests`, namespace `Alundra.Tests`. Après R5 (attend Q-R-3) : `Alundra.Tests/Events/<Sujet>Tests.cs`, classe sans préfixe. Indication non versionnée, scratchpad `r5-disc2/r5-files.tsv` : `AlundraClassAndBranchOpcodesTests` devient `Events/ClassAndBranchOpcodesTests.cs`, l'audit devient `Support/StoryChainOpcodeAudit.cs`, `IntroTraceHarnessTests` va dans `Arcs/` | non lue (fichiers de test). R5 interdit d'ajouter un test pendant son exécution (`docs/plan-restructuration-dll.md:869`) |
| vecteurs T0 | `docs/plan-e19-sm-annexe/sm1/` et `sm2/` (rapport §7) ; l'annexe `sm1-sm4/` est gelée (SHA1SUMS) | hors périmètre ADR-0075 |
| lecteur C# des vecteurs | non nommé. Convention du HEAD `Alundra.Tests/Alundra<Sujet>Oracle.cs` ; après R5, sans préfixe, dans `Support/` (INFÉRENCE d'après la table R5 du scratchpad) | — |
| fichier de données lu par un test C# (vecteurs, liste de sm0) | non nommé ; `Alundra.Tests/Data/` (même question que Q-R-3 point 6 pour la liste de la chaîne) | — |
| liste fermée du corpus entier (sm0) | non nommée : ni TSV, ni classe, ni propriétaire de colonne | — |
| ADR « du sens de 0x60/0x61 » (T4) | prochain numéro libre `0076` (FAIT : `docs/decisions/` va de 0001 à 0075 plus `README.md` et `template.md` ; aucun plan ne réserve 0076+). Nom `0076-<phrase-anglaise-en-minuscules>.md` + ligne dans `docs/decisions/README.md` ; non nommé par la proposition | — |

## 6. Manques qui bloquent la mise en tranche exécutable

Classés : [B] bloquant, [M] à combler avant de lancer.

1. **[B] sm0 n'existe pas.**
   - sm1 et sm2 « dépendent de sm0 » (plan `:12068-12069`). sm0 n'est qu'une ligne de tableau (`:12067`) et la ligne SM0 du rapport général (§8).
   - Il manque : tâches, fichier, classe, colonne « propriétaire » définie (la question 5 du tri du 2026-10-08 n'a pas de réponse, G §3), emplacement de la liste.
   - L'outil existant est limité à la chaîne : `ChainMaps` = 30 cartes (`AlundraStoryChainOpcodeAudit.cs:39`), liste vide, et le test `AlundraStoryChainSkippedOpcodesTests.cs:74-86` interdit toute carte hors chaîne.
   - La clause du motif E19.l « une ligne de moins dans la liste fermée » n'a donc aucun objet pour sm1/sm2. T4 (« liste fermée −187 », « −129 ») est inexécutable.
2. **[B] Q-E19-SM1 est ouverte** : quand (avant ou après la restructuration) et propriétaire des ombres de 0xAC.
   - R5 attend Q-R-3.
   - D-R-8 interdit une autre tranche en parallèle.
   - R5 ne doit « ajouter ni retirer aucun test » (`docs/plan-restructuration-dll.md:869`) et range 333 fichiers de test : le nom et l'emplacement de tout test neuf dépendent de cette réponse.
3. **[B] Aucun test nommé, aucune valeur.**
   - Le motif E19.l écrit pour chaque test l'état de départ et l'attendu (`Result` de départ 7, `CodeIndex`, flags, graine : plan `:6694-6716`, `:6800-6815`).
   - sm1 T1 et sm2 T1 ne donnent que des intitulés. « Comparaison signée » exige une tuile négative, non écrite. « 0x6D sans 0x6A (état neuf) » n'a pas de valeur attendue (phrase jugée confuse : C O7).
   - Pas de fichiers, pas de commits, pas de règles d'exécution R1..Rn.
4. **[B] Pas de preuve de retrait ni d'ensemble de rouges exact.**
   - Les tranches récentes en exigent (plan E20 : « preuve de retrait », « exact removal sets », dernières révisions h4a/h4b1).
   - sm1 donne seulement des mutants (permuter 60/61, borne exclusive, bits inversés, oubli du héros) sans règle de seuil ; sm2 n'en donne aucun.
   - Manquent aussi les arrêts (« une valeur contredite est un arrêt ») et la section « Acceptation » : suites `Alundra.Tests` en Release puis Debug (la Debug en dernier), `cmp` sans écart, six traces à l'octet (plan `:6760-6767`).
5. **[B] T0 « vecteurs d'oracle ».**
   - `oracle.py` existe mais ne produit pas de vecteurs.
   - Le format des vecteurs, le lecteur C# et le lieu des fichiers ne sont pas définis.
   - Dépendance dure à un `ALUN_CD.EXE` hors dépôt (`lib.py:3`) : les vecteurs doivent être figés, pas rejoués.
6. **[B] T3 arcs sans spécification.** §1.3 impose pour chaque arc : drapeaux posés, héros, signal de fin prouvé par la trace, limite d'images, valeurs écrites avant le code (`docs/plan-e19-opcodes.md:12003-12011`). Rien n'est écrit pour 27 C[2], 400 C[1], 226 B[3], 398 B[1], 55 C[4], 344 C[60], 59 C[6], 60 C[1], 56 C[1]. Particularités :
   - (a) Carte 27 : les objets 5 à 8 sont des objets transportables à `ProgramTick` 4, hors des plaques au chargement (INFÉRENCE : `DisplayX/DisplayY` distincts, par exemple plaque 1 en (33, 30), objet étoile en (33, 32)). « Posés sur les plaques » demande une pose à la main que la proposition ne décrit pas, et le porteur doit être posé exactement au sommet (`FindRidingEntity`, `EntitySupport.cs:311+`).
   - (b) 226 B[3] est un programme de carte qui attend une entité dans la zone (x 43, y 48-51, z 12). L'état de jeu qui le déclenche n'est pas donné.
   - (c) Aucune de ces cartes n'a été chargée par `ArcRun` (A20).
   - (d) Tout arc `RealController` neuf doit finir à 0 glissement (`AlundraArcSupport.cs:342`).
   - (e) 4 des 5 arcs de sm2 visent les cartes 55, 56, 59, 60 (voir I3).
7. **[M] Pas de recette en jeu définissable.**
   - Aucun site sur la chaîne.
   - Les puzzles des plaques ne sont pas résolubles tant que le transport (E20.g, « Rien n'existe » `docs/plan-e20-heros.md:6559`) et le C4 natif ne sont pas portés (Q-sm-5, O-E20-21).
   - L'équivalent de la « recette L-4 » d'E19.l manque.
   - Pour sm2, seules 344, 307 et 442 sont des cartes du jeu (64 sites sur 129).
8. **[M] Affirmation périmée à corriger avant d'écrire la tranche** : « RidingEntity recalculé une fois par image » (A9).
   - Le recalcul est par tick, dans l'étape « lists » avant la physique ; le binaire le fait dans la passe physique (`CheckRidingEntities` `0x800364C8` appelé de `0x80038364`).
   - La proposition ne mesure pas cet ordre (le script voit la valeur du tick précédent dans le binaire, S §2).
   - L'écart ne s'exprime que si un porteur ou un cavalier bouge dans le tick.
9. **[M] Écarts connus de `RidingEntity` non traités par sm1.**
   - Non effacé aux compteurs (A10, O-E20-9, étude D5b M4, Q-D5B-1 à 6 ouvertes).
   - Propriété de `EntitySupport.cs`, `ScriptedMotion.cs`, `EntityScriptProxy.cs` réservée à D5b tant qu'elle est en pause (plan `:7403`).
   - La proposition ne dit pas si sm1 attend D5b, ni comment un `RidingEntity` périmé est évité dans les tests (0x60 peut rendre 1 pour un candidat devenu non collidable).
10. **[M] Numérotation et comptes incohérents.**
    - Le plan annonce « Q-sm-1 à Q-sm-9 » (`:12097`) mais §3 n'en a que 8 (`:12475-12482`).
    - Le rapport en a 9 (0x9E et 0x88 sont deux questions dans le rapport, une seule, Q-sm-6, dans le plan). « Q-sm-8 » désigne donc les libellés dans le rapport et les cartes 55-57 dans le plan.
    - Q-sm-5 dit 186 sites (0x60 + 0x61), sm1 en ferme 187 (avec 0x3D).
    - Q-sm-7 énumère `0x6B-0x6D, 0x88, 0x97, 0xA9, 0xAA, 0xB4-0xB6`. Il omet les libellés de 0x60/0x61 (`:142-143`, listés par le rapport Q-sm-8). Il ne dit rien de `:106` (0x3C « If entity in zone with flag 0x80 », ambigu : le binaire teste Collidable posé et `NoEntityCollision` clair) ni de `:107`.
11. **[M] 0x3C (0 site) est dans sm1 sans question**, alors que le plan demande « vecteurs seulement ou sauté ? » pour 0x9E et 0x88 (Q-sm-6). Son test dépend de `PlatformEntity`, jamais écrit avant E20.g1a (A11) : le test pose la valeur à la main.
12. **[M] Les corrections obligatoires de la critique ne sont pas reportées** dans le rapport gelé ni dans le plan :
    - R1 : retirer la phrase « la décompilation perd le dernier slot » (le plan l'a fait, le rapport non).
    - O1 : l'identifiant brut invalide est fatal dans le binaire ; test peu coûteux « id brut du corpus appartient aux enregistrements de la carte ».
    - O3 : porte de gel `[0x800DC4B8] & 0x48`, l'ordre événements puis entités ne vaut que porte ouverte ; la DLL a le chemin `gateClosed`, `WorldProxy.cs:2907-2915`.
    - O5 : les 4 lectures de la 307 pour le test statique de sm2.
    - O7 : la phrase de 0x6D sans 0x6A.
    - Elles sont à écrire dans T0/T1 (C §7).
13. **[M] Test statique de sm2** (« porter memo_flow.py en test »).
    - Analyse sensible au flot (états INIT/SAVED/CLOB par pc) ; `WalkProgram` (`AlundraStoryChainOpcodeAudit.cs:163`) ne l'est pas.
    - Les ensembles d'écrivains `CLOB` sont copiés du binaire à la main (`memo_flow.py:8`) et rien ne les relie aux écrivains de la DLL (six méthodes privées, A16).
    - Les comptes attendus ne sont pas écrits. Par axe : 29 lectures X (25 + 4 de la 307), 12 Y, 29 Z dont 18 « 0x6A passé » et 11 sans 0x6A ; 70 lectures au total, cohérentes entre elles.
14. **[M] Hôtes de test.** Pour 0x3D « héros inclus » et pour 0x60 `60 81`, le cas doit parcourir `PlayerEntity` en plus de `SpawnedEntities` comme 0x3F (`EventProgramRunner.cs:1965-1980`). Sinon un test avec la fausse `Context` (héros hors de `Spawned`) ne voit pas le héros alors que la production le voit (A7). Non dit dans T2.
15. **[M] Arithmétique 32 bits de sm2.**
    - Le motif existant `Math.Abs(a - b) >> 16` (`EventProgramRunner.cs:2915`) lève `OverflowException` sur `int.MinValue` ; le binaire fait `negu` (retour sur 32 bits).
    - T0 cite « enroulement 32 bits » mais T2 ne dit pas d'écrire la valeur absolue sans `Math.Abs`.
    - Pas de `CheckForOverflowUnderflow` dans les `.csproj` : `<<` et `-` enroulent déjà.
16. **[M] T2/T4 omettent des mises à jour que le motif E19.l faisait** : `IntroTraceHarnessTests.ImplementedOpcodes` (R8), `PredicateOpcodes`, le texte de plan, les comptes de tests, la ligne « Fait le ». T4 prévoit un ADR « du sens de 0x60/0x61 » alors que E19.l1/l2 n'en ont pas écrit et que D-E19-2 (`:49`) tranche déjà « le binaire l'emporte » : à décider (numéro libre 0076).
17. **[M] Les citations de la proposition sont en noms d'avant R2-R4** (§1). Tout texte de tranche écrit à partir d'elle doit être retraduit ; le plan lui-même ne le fait pas (`:12041`, `:12090`, `:12481`).

## 7. Inconnus

- **I1.** Si le héros passe la grille `(Flags & 0x4100) == 0x100` (carte 136, deux sites `60 81`), et la valeur de `AnimFlags & 0x80` par animation des plaques et des cavaliers. La statique ne les donne pas.
- **I2.** Si le `RidingEntity` de la DLL égale le `+0x12C` du binaire au moment où le script C d'une plaque tourne (ordre « lists » avant physique contre `CheckRidingEntities` dans la physique ; porteur ou cavalier qui bouge ; `RidingEntity` non effacé). Aucune mesure dynamique n'existe dans le dépôt ; la tâche T0 « mesure des cavaliers réels » ne la demande pas.
- **I3.** Atteignabilité des cartes 55, 56, 57 (Q-sm-8/9). INFÉRENCE seulement : dans l'export, les portails (`DestMapId` des `*.tileMap`) qui mènent à 55-60 viennent tous de cartes du même ensemble {55..60} (`portals.py`). Les changements de carte par script `0x53` n'ont pas été relevés. 65 des 129 sites de sm2 sont sur 55, 56, 57, 59, 60 (27 + 7 + 2 + 29) ; les 64 autres sont sur 307, 344, 442.
- **I4.** Si les neuf cartes d'arc (§6.6) se chargent dans `ArcRun` sans autre montage (aucune n'y a été chargée) ; si le parcours statique C# sur les 483 cartes redonne les 510 sites du relevé Python (sm0 non exécuté).
- **I5.** Si les cartes 27/29/30/113/400 (puzzles à plaques) peuvent se résoudre en jeu : cela dépend du transport (E20.g) et du C4 natif (E14), tous deux non portés.
- **I6.** Comportement réel de `UnknownSkipped` sur les 8 opcodes dans les cartes concernées (`Result` périmé, boucles de plaque non résolues ou qui tournent à vide) : non mesuré.
- **I7.** Quel `Result` initial les programmes C des plaques voient au premier tick (état neuf `Result = 0`) : non utilisé par la proposition.
- **I8.** La table de tests de R5 (`scratchpad/r5-disc2/r5-files.tsv`, hors dépôt) est ici la seule indication des noms post-R5 ; Q-R-3 peut la changer.