# Contre-vérification du rapport sm3/sm4 : citations au HEAD, verdicts, tests, banc, noms et manques

Racine : `D:/development/repo/alundra-casaengine-project-converter`. Lecture seule : rien n'a été construit ni lancé. Je n'ai pas écrit `sm34/check.md` : les consignes de la session interdisent les fichiers de rapport. Ce message est le rapport.

## 0. Verdict

- Le verdict de l'agent tient. Ni sm3 ni sm4 n'est une tranche exécutable. Une relecture de plan rendrait **REVISE** pour les deux (§8).
- **Citations.** J'ai rouvert les 48 lignes de la table :
  - 47 désignent au HEAD le même code que l'ancienne citation, au renommage près ;
  - une ligne ajoute une citation fausse, `WorldProxy.cs:3282` ;
  - quelques bornes sont imprécises.
- **Verdicts VRAI/FAUX.** Il y a des erreurs de fond et des affirmations trop fortes :
  - 3 erreurs de fond : E1 (S3-1), E2 (le test qui « rougirait »), E3 (S3-4) ;
  - 2 affirmations trop fortes : E5 et E6.
- **Manques omis.** 15 ajouts, M1 à M15 (§8). Les plus lourds :
  - `main` est au stade R2 et y a un fichier homonyme (M1) ;
  - A9/AA et B3 exigent un monde piloté (M3) ;
  - six opcodes sans site, dont `0xB4` et `0xB7` qu'aucune question ne couvre (M5) ;
  - aucune recette ni clause d'acceptation (M8) ;
  - couplage avec E20.g1a, devenue READY (M9).
- **HEAD a bougé** : `317b3360` au lieu de `e8087d74`. `git diff --stat e8087d74 HEAD` ne touche que `docs/plan-e20-heros.md` (+8 / −5). Les morceaux du diff commencent en 8068, 8899, 9014, 9046 et 9078 : aucune ligne E20 citée (6525, 6584, 6649, 6713, 8141, 8165) ne bouge.

## 1. Méthode

- **Citations.** Chaque ancienne ligne est relue à `9502f75` (`git show`) et confrontée à la ligne HEAD.
  - Scripts : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-state/sm34/v2/show.py`, avec `v2/pairs1.txt` et `v2/pairs2.txt`.
- **Sites.** `v2/sites.py` compare les 132 sites aux `events.json` d'`alundra-project` : octets identiques, entrée du programme ≤ pc, 0 écart.
- **Décodage.** `v2/decode.py` décode les programmes de façon linéaire, avec les tailles d'`Alundra/Events/EventOpcodeSizeTable.cs` au HEAD.
- **Types de tests.** Déclarations extraites par `git grep` à `9502f75` et au HEAD (`v2/types-*.txt`). Le diff ne montre que 3 ajouts : `AlundraScriptAssemblyRegistration`, `AlundraScriptNamesInTheDataTests`, `SimpleNameEntityLoader`. Aucun renommage. `git diff -M --name-status` montre 2 fichiers ajoutés et aucun déplacé. Le décompte absolu dépend de l'expression régulière : 750 → 753 avec la mienne, 635 → 638 chez l'agent.

## 2. Erreurs, omissions et corrections

**E1 [fond]. S3-1 est faux.**
- L'agent écrit : « types 4 et 11 disjoints, donc `B2 04 8B` vaut toujours 0, même après E20.g1a ».
- Or l'octet 1 `0x04` a le bit `0x80` clair : c'est l'**identifiant brut** 4 (`EntityRefId == 4`), pas le groupe 4. La branche brute est en `EntitySearchService.cs:105-118`. Le rapport le dit lui-même (report.md §3 : bit `0x80` clair = identifiant brut).
- Les 10 sites sont `B2 <id brut 3|4|5|6|8> 8B`, soit « l'entité N est portée (`PlatformEntity != null`) » (`skipped-sites.tsv` ; décodage de 258 B[6] @639-@666).
- `+0x28` n'est écrit non nul que pour un objet porté (`docs/plan-e20-heros.md:6525`). Les 10 sites, `B2 04 8B` compris, peuvent donc valoir 1 après E20.g1a.

**E2 [fond]. « sm4 fait rougir `UnknownOpcode_KnownSize_SkipsBySize` » est faux.**
- Le test vérifie une seule chose : `TargetAnimationId == 4` après `[B3, 0×8, 1A, 4, FF]` (`Alundra.Tests/AlundraEventProgramRunnerTests.cs:331-343`).
- Un `0xB3` porté rend 9, la taille de la table : `1A 4` tourne encore. Le test reste **vert, mais vidé de son objet** (INFÉRENCE, lecture).
- La conséquence ne change pas : il faut un nouveau témoin, comme lors des changements notés au commentaire `:333-334`.

**E3 [fond]. S3-4 « aucun test ne prouve que la liste suit les slots » est faux.**
- `AlundraEntitySlotWorldTests.cs:97` (`…AndTheListFollowsTheOrderOfTheSlots`), `:77` et `:117` le prouvent. L'ADR-0064, section « Consequences », les cite.
- Une réserve est réelle : une entrée sans slot (montage de test) reste à sa place (`:117`). Les tests T1 « premier résultat » de B4-B6 doivent donc poser des slots.
- Les sites réels (`B5 81`, `B5 80`, `B6 80`) n'ont qu'un résultat.

**E4 [citation]. `WorldProxy.cs:3282` ne concerne pas le héros.**
- C'est le recyclage d'une entité détruite : `RecycleDestroyedEntities`, `:3274-3284`, qui saute le héros en `:3276`.
- Seul `:2111` pose `EntityRefId = -1` au héros.
- Utile pour `0x88` : une entité recyclée perd son id brut.

**E5 [imprécision]. « Le plafond `0x32` n'existe que pour `0xBC` ».**
- `SetPlayerHpMax` plafonne aussi les **statistiques** à `0x32` (`Alundra/Hero/PlayerManager.cs:1857-1864`). Il sert au miroir de fin de `MovePlayer` (`:643`).
- Sur **l'entité**, seul `IncreaseHpMax` plafonne (`:1898`).
- `IncreaseHpMaxAndFill` occupe `:1875-1887`, pas `:1880-1890`.

**E6 [trop fort]. G8 : « ni lecture au binaire » pour `0x15`, `0x66`, `0x7A`.**
- `opcodes.tsv` donne leurs gestionnaires (`8003D710`, `8003F794`, `8003FB8C`) et un résumé :
  - `0x15` : message d'erreur sans enregistrement ;
  - `0x7A` : saut, miroir de `0x79`.
- Pour `0x66`, il existe des faits **[binaire]** : il écrit l'état de l'owner (`+0x234/+0x238`) et ne change pas l'entité logique (`docs/plan-e19-opcodes.md:424`, `:435`). L'écart sur le sens de la copie est tranché en O-E19-2 (`:12395`).
- Il manque bien une relecture complète et un oracle.

**E7 [précision]. « Les programmes B s'exécutent avec le héros comme entité de référence ».**
- Le code fait `player.LogicEntity = mapEvent.Entity ?? player` (`WorldProxy.cs:3206`), restauré en `:3212`.
- C'est donc le héros, sauf si un `0x42`/`0x43` antérieur de cet événement l'a changé (check.md O6).

**E8 [citations du banc]. Plusieurs lignes d'`AlundraArcSupport.cs` sont décalées.**

| Citation de l'agent | Ligne réelle |
|---|---|
| `:55-56`, convention « une exportation absente fait échouer » | `:90` |
| `ArcSpec` en `:62-69` | `:62-64` (`ArcArrival` est en `:66-69`) |
| Doc de `RealController` / `Prefabs` | `:52-58` |

**E9 [citations].** Dans `AlundraClassAndBranchOpcodesTests.cs`, `Context` occupe `:25-35` et `Run` `:47-67`. L'agent écrivait `:23-34` et `:55`.

**E10 [citations].** La table des arcs du §1.3 occupe `docs/plan-e19-opcodes.md:12013-12027` (A9 en `:12027`). Ses cartes sont 390, 476, 478, 392, 391, 416, 163 et 172 : la 389 n'y est pas.

**E11 [citations mineures].**

| Citation de l'agent | Correction |
|---|---|
| `sm1-sm4/README.md:3` (base) | `:4` |
| Mutants du rapport, L408 | `report.md:411` |
| D-E19-2 en `:50-51` | `:51-52` |
| `InitializeEventData` « méthode : 331-369 » | doc `:330-334`, signature `:335`, fin `:369` |
| Branche brute de la recherche, `:104-117` | `:105-118` |
| Type 11, `:245-253` | `:246-255` |

**E12 [attribution].** La règle « aucune tranche E19 en parallèle » n'est pas dans D-R-8 lui-même (`docs/plan-restructuration-dll.md:27-36`). Elle est dans l'ADR-0075 (`docs/decisions/0075-…md:23`) et dans le plan E19 (`docs/plan-e19-opcodes.md:12036-12037`).

**E13 [précision]. « Aucun test ne charge une des cartes de sm3 ».**
- Les documents d'événements de 258 et 298 sont chargés en lecture statique par `AlundraNativeEffectTests.cs:586` (sites `82 53` en B[5]).
- La 159 est chargée pour ses fonds par `BackdropStageDefinitionTests.cs:76`.
- Aucun de ces tests n'exécute un site sm3.

**E14 [précision]. Tests de séquence réelle sur 140 et 411.**
- Sites présents :
  - 140 : D[2] @2037 et D[16] @2125 (`0x72`) ;
  - 411 : B[4] @1056, B[5] @1164 et B[11] @1996 (`0x96`).
- Ni `0x72` ni `0x96` n'écrivent `Result` ni `_34`, et ils ne changent pas le pc au-delà de leur taille.
- Les assertions de ces tests (`:4969-5025`, `:5037-5094` : pc, opcode, `_34`, `Result`) ne peuvent donc pas bouger, même si un site est atteint (INFÉRENCE).

**E15 [omission].** L'erreur « `0x88`, 0 site » vient du rapport lui-même : son §4.4 ne cite que 2 des 3 sites (`report.md:310`, sans 306 E[15] @1122) et sa Q-sm-7 dit « Pas de site » (`:392`). Le plan l'hérite.

**E16 [précision]. `PredicateOpcodes` : les précédents se contredisent.**
- L1-2 l'a laissé inchangé (`docs/plan-e19-opcodes.md:6730-6731`) ; L2-2 y a ajouté `0x83` et `0x9F` (`:6823`).
- La règle 4 ne lit que la liste de la chaîne (`AlundraStoryChainOpcodeAudit.cs:435-441`), qui n'a aucun site sm3. Elle est donc inerte ici : choix de cohérence (P3), pas un blocage.

**E17 [omission].** O1 (validateur d'identifiant brut fatal) vaut aussi pour les 10 ids bruts de `B2` (3, 4, 5, 6 et 8 en 258 et 298), pas seulement pour `A9 18` (440).

**E18 [précision].** La mise à jour d'`ImplementedOpcodes` ne dépend pas de sm0. Le patron E19.l l'exige à chaque tranche (L1-R8 `:6687-6688`, L2-R5 `:6793-6794`).

**E19 [précision]. S4-7, conflit de fusion avec E20.**
- Le rapport écrit `0x97` en ligne, `SetPlayerMp(state, (short)(Mp + v1))` (`report.md:327`), sans helper `IncreaseMp`. Il n'en existe d'ailleurs aucun dans la DLL (`rg IncreaseMp` : 0).
- Le conflit avec `DecreaseMp` d'E20.h4 (`docs/plan-e20-heros.md:8141`, `:8165`) n'existe que si l'on choisit un helper dans `PlayerManager.cs`. C'est une décision à écrire.

**E20 [omission].** Q-R-4 attend aussi la validation de l'auteur (`docs/plan-restructuration-dll.md`, §11).

## 3. Table corrigée des citations

Base `9502f75`, HEAD `317b3360`. Les anciens chemins sont sous `Alundra/Scripts/`.

| Ancienne | HEAD | Contenu relu | Statut |
|---|---|---|---|
| `AlundraEventProgramRunner.cs:377-419` | `Alundra/Events/EventProgramRunner.cs:327-369` (doc 330, signature 335) | `InitializeEventData` : `CodeIndex`/`Sp`/`Codes` et `Parameters[0]` seulement (`:351-368`) | OK |
| `…:2153` | `:2103` (`default:` en 2102) | `return UnknownOpcode(command, state)` | OK |
| `…:3115-3141` | `:3065-3091` | `UnknownOpcode` | OK |
| `…:250-254` | `:200-204` | restauration de `LastTarget*` hors C | OK |
| `…:275-280` | `:225-230` | sauvegarde au départ A/D/E/F | OK |
| `…:760-762` | `:710-712` | `case 0x19` | OK |
| `…:1213-1222` | `:1163-1172` | `case 0x5D` | OK |
| `…:1879` | `:1829` | `0x2F` lit `_gameState.TickPad` | OK (le commentaire `:1826` dit encore `AlundraGameState`, Q-R-5) |
| `…:2011-2013` | `:1961-1963` | commentaire de `0x3F` | OK |
| `…:613-2154` | `:563-2104` | `Dispatch`, 158 `case` des deux côtés | OK |
| `…:452-466` | `:402-416` | boucle `0xFF`/`0x00` | OK |
| `…:478-479`, `:515`, `:137` | `:428-429`, `:465`, `:87` | taille pour la trace ; `Parameters[1] = 0` (aussi `:411`) ; `ProductionLoopBudget` | OK |
| `…:1990-2040` (`0x3E`/`0x3F`) | `:1940-1990` | `0x3E`, `0x3F`, `0x42` | OK |
| `EventOpcodeSizeTable.cs:142-246` | `Alundra/Events/EventOpcodeSizeTable.cs`, mêmes numéros | 3 lignes changées seulement (namespace, `cref`) | OK |
| `EventOpcodeSizeTable.cs:10` | `:11` | « 197 » | déjà décalée d'une ligne à la base |
| `EntitySearchService.cs:98-262` | `Alundra/Entities/EntitySearchService.cs:99-263` | brute `:105-118` ; type 4 `:157-167` ; type 7 `:195` ; type 8 `:210` ; type 11 `:246-255` | OK |
| `AlundraEntityScriptProxy.cs:351` | `Alundra/Entities/EntityScriptProxy.cs:364` | `XCollisionEntity` (proxy) | OK ; copie au clone `:2663` |
| `…:325` | `:338` | `int ForceZ` | OK |
| `…:1212-1213` | `:1225-1226` | `LastTargetAnimationId` et `LastTargetDirection` (`uint`) | OK |
| `…:76-77` | `:89-90` | `Hp`, `HpMax` (`Status` en `:88`) | OK |
| `…:81` | `:94` (doc) ; champ `:97` | `LifePulledFromStats` | OK (déjà décalée de 3 lignes) |
| `…:88` | `:101` | `PlatformEntity` | OK |
| `…:2399`, `:2411-2418` | `:2412`, `:2424-2431` | `MoveControllerAndPullPosition` : retour sans contrôleur `:2414-2417`, remise à `null` `:2424` | OK |
| `…:2596` | `:2609` | copie au clone | OK |
| `…:341-344` | `:354-357` | `TileX/Y/Z`, `RidingEntity` | OK |
| `AlundraWorldProxy.cs:2077` | `Alundra/Maps/WorldProxy.cs:2111` | héros `EntityRefId = -1` | OK ; **retirer `:3282`** (E4) |
| `…:2493` | `:2527` | `TickPad.Update` | OK |
| `…:2893` | `:2927` | `MovePlayerOnTickPad(PadState.FromTickPad…)` | OK |
| `…:2453-2454` | `:2487-2488` | les `.Clear()` des deux côtés ; les `.Add` sont en `:2528-2529` (anciens `:2494-2495`) | OK |
| `…:3231` | `:3265` | `RecycleDestroyedEntities`, porte `:3267-3270` | OK |
| `AlundraPlayerManager.cs:1750` | `Alundra/Hero/PlayerManager.cs:1763` | lecture de `XCollisionEntity` | OK |
| `…:610-631` | `:623-644` (`EndOfMovePlayer` en `:634`) | miroir de la vie | OK |
| `…:1873` | `:1886` | dans `IncreaseHpMaxAndFill` (`:1875-1887`) ; `IncreaseHpMax` en `:1893` | citation déjà fausse à la base |
| `…:1885` | `:1898` | plafond `0x32` de `0xBC` | OK (voir E5 : aussi `:1857-1864`) |
| `…:1893-1910` | `:1906-1923` (méthode `:1910`) | `RestoreLifeAndMagic`, `Snap` en `:1922` | OK |
| `…:1957` | `:1970` | `SetPlayerMp(GameState, short)` | OK |
| `AlundraGameState.cs:143` | `Alundra/Session/GameState.cs:152` | `TickPad` | OK |
| `AlundraTickPad.cs:31-49` | `Alundra/Hero/TickPad.cs:33-51` | setters privés ; `Update` `:56`, `Reset` `:101` | OK |
| `AlundraCellStore.cs:13-40` | `Alundra/Cells/ICellMutator.cs:14-38` (interface `:21-38`) | 3 écritures ; l'ancien `:37-40` (doc du store) est passé dans `Alundra/Cells/CellStore.cs` | OK |
| `AlundraMapSizeReader.cs` | `Alundra/Maps/MapSizeReader.cs:19` | 52 × 60 | OK |
| `AlundraEntitySpawnFactory.cs:493` | `Alundra/Entities/Spawning/EntitySpawnFactory.cs:497` | « No record: `EntityRefId = -1` » ; défaut du champ : 0 | OK |
| `EntitySupport.cs:71-75` | `Alundra/Physics/EntitySupport.cs:72-76` | `IsEligibleSubject` | OK |
| `AlundraHudDirector.cs:293` | `Alundra/UI/Hud/HudDirector.cs:300` | `SnapDisplayedLifeAndMagic` | OK |
| `AlundraEventProgramRunnerTests.cs:323` | `Alundra.Tests/AlundraEventProgramRunnerTests.cs:331` | `…SkipsBySize` | OK (le chemin bougera avec R5) |
| `IntroTraceHarnessTests.cs:322` | `:332` (ensemble jusqu'en `:387`) | `ImplementedOpcodes`, sans aucun des 16 opcodes | OK |
| `AlundraStoryChainOpcodeAudit.cs:128-338` | `:130-340` | `WalkMap` … | OK |
| `AlundraClassAndBranchOpcodesTests.cs:140-216` | `:143-219` | tests de `0x3F` | OK |
| `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` | même chemin, 1 ligne (en-tête) | — | OK |
| `docs/plan-e20-heros.md:6287` | `:6525` | « `+0x28` non nul : trois sites, tous le héros » | déplacé ; vaut aussi à `317b3360` |
| `docs/plan-e20-heros.md:6321` (« D-G-2 ») | D-G-2 en `:6584` (base `:6346`) | — | déjà fausse à la base |

Piège à noter dans toute traduction :
- à la base, `Alundra/Scripts/EventProgramRunner.cs` était l'**interface** ;
- au HEAD, `Alundra/Events/EventProgramRunner.cs` est **le runner**, et l'interface est passée dans `Alundra/Events/IEventProgramRunner.cs` (`table.json`, ADR-0075 règle 5).

## 4. Affirmations sur la DLL : table corrigée

| Affirmation | Verdict | Preuve |
|---|---|---|
| Aucun `case` pour les 16 opcodes (13 lus, plus `15`, `66`, `7A`) ; `default` → `UnknownOpcode` | VRAI | `EventProgramRunner.cs:2102-2103` ; `ImplementedOpcodes` ne les contient pas (`IntroTraceHarnessTests.cs:332-387`) |
| `UnknownOpcode` : saut par la taille, `Result` intact, un journal par opcode qui cite le **libellé** ; sans taille ou taille ≤ 0, rend 0 | VRAI | `:3065-3091`, libellé dans le message `:3083-3087` |
| Table de 198 entrées, en-tête « 197 » | VRAI | `EventOpcodeSizeTable.cs:11` ; `grep -c "{ 0x"` = 198 |
| Libellés faux ou vides (`B4`-`B6`, `97`, `88`, `A9`, `AA`) | VRAI | `:228-230`, `:199`, `:184`, `:217-218` |
| Aucun test ni trace n'épingle ces libellés | VRAI, avec une nuance | `docs/intro-programs-389.txt` imprime les libellés (`IntroTraceHarnessTests.cs:1792`) mais ne contient aucun des 16 opcodes (0 occurrence) ; seule mention en test : le commentaire `AlundraEventProgramRunnerTests.cs:333` |
| Recherche : 12 types, branche brute gardée par la référence | VRAI | `:105-118` ; égalité au binaire : INFÉRENCE (traduction python, `oracle.py:154`) |
| Types 4 et 11 disjoints, donc « `B2 04 8B` vaut toujours 0 » | **FAUX** (E1) | `0x04` est un id brut ; type 11 : `!IsPlayer && actif && PlatformEntity != null` (`:249`) |
| Héros `EntityRefId = -1` | VRAI | `WorldProxy.cs:2111` seulement (E4) |
| `Result` est un champ, `Parameters` un `int[10]` | VRAI | `EventProgramState.cs:32`, `:36` |
| `PlatformEntity` jamais écrit par la DLL | VRAI | seule écriture : `EntityScriptProxy.cs:2609` |
| E20.g1a écrira `PlatformEntity` | VRAI, et **g1a est READY** depuis le 2026-10-18 | `docs/plan-e20-heros.md:6584`, `:6649-6650`, `:6713` |
| `XCollisionEntity` remis à `null` puis réécrit à chaque pas, jamais sans contrôleur, lu par le héros | VRAI | `:2414-2417`, `:2424-2431` ; `PlayerManager.cs:1763` |
| `ForceZ` signé ; `LastTarget*` existent et la sauvegarde/restauration est faite | VRAI | `:338`, `:1225-1226` ; runner `:200-204`, `:225-230` |
| `Status` est un champ simple ; recyclage fermé par `GameplayBlockedMask` | VRAI | `:88` ; `WorldProxy.cs:3265-3270` ; le recyclage remet `EntityRefId = -1` (`:3282`) |
| Miroir de la vie, `SetPlayerMp` borné | VRAI | `PlayerManager.cs:634-644`, `:1970` ; `Snap` appelé une seule fois (`:1922`) |
| Plafond `0x32` seulement pour `0xBC` | **IMPRÉCIS** (E5) | les stats aussi, `:1857-1864` |
| `TickPad` sans setter public | VRAI | `TickPad.cs:33-51` |
| Pavé avancé en tête de tick, événements, puis `MovePlayer` | VRAI en monde piloté | `WorldProxy.cs:2527`, `:2568`, `:2927` ; hors monde piloté, voir M3 |
| Le premier `MovePlayer` du héros lit la direction dans `ButtonsHold` | VRAI (ajout) | `PlayerManager.cs:360` (`pad.ButtonsHold >> 0xc`) ; `FromTickPad` prend le stick dans l'échantillon de l'image (`PadState.cs:72-81`) ; aucun consommateur du stick hors `StickProbe` |
| `_squareOfTick` et `_choicePadOfTick` enregistrés avant les événements | VRAI | `:2528-2529` |
| Les programmes B tournent sur le héros | VRAI, nuancé (E7) | `WorldProxy.cs:3206`, `:3209`, `:3212` |
| `ICellMutator` sans lecture ; `GetWalkability` public | VRAI | `ICellMutator.cs:21-38` ; `CellStore.cs:136` ; `IEntityWorldContext.cs:116` (`=> null`) ; aucun faux `ICellMutator` dans les tests |
| 42 sites en 11 cartes (sm3), 90 en 50 cartes (sm4) | VRAI | recompté, et les 132 sites vérifiés contre l'export (`v2/sites.py`, 0 écart) |
| `0x88` « 0 site » (plan Q-sm-6) | FAUX | 3 sites : 306 E[14] @1110, 306 E[15] @1122, 442 E[1] @1312 |
| `0x15`, `0x66`, `0x7A` : 0 site | VRAI | `skipped-sites.tsv` |
| Site 161 B[1] @54 | VRAI | décodage : `64 81 F0 00 80 03 10 00` @36, `3B 06 0D 1D 1D 03 03` @44, `03 10 00` @51, `B3 00 10 …` @54, `00` @63, `02 EC FF` @64 ; sortie vers @67 |

## 5. Tests qui épinglent : table corrigée

| Test | Fichier:ligne | Effet d'une tranche |
|---|---|---|
| `UnknownOpcode_KnownSize_SkipsBySize` | `AlundraEventProgramRunnerTests.cs:331-343` | sm4 le laisse **vert mais vide** (E2). Il faut changer de témoin : `0x56` (taille 2, `EventOpcodeSizeTable.cs:132`, sm5) ou `0xB9` (2, `:233`, sm7) ; `0xBB` (1, `:235`) et `0xC2` (2, `:242`) appartiennent à sm8 |
| `…0x5F_SkipsEightBytes…`, `…NoKnownSize…` | `:346`, `:361` | aucun |
| `Oracle_AnOpcodeTheRunnerHandlesIsPorted…` | `AlundraStoryChainSkippedOpcodesTests.cs:103-110` | reste vrai ; `IsPorted` sonde le vrai runner et compte une exception comme « porté » (`AlundraStoryChainOpcodeAudit.cs:88-120`) |
| Liste de la chaîne | `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv`, en-tête seul | aucune ligne à retirer (0 site sur les 30 cartes, recompté) |
| `PredicateOpcodes` | `AlundraStoryChainOpcodeAudit.cs:57` | inerte ici ; précédents contradictoires (E16) |
| `ImplementedOpcodes` | `IntroTraceHarnessTests.cs:332` | à compléter (patron E19.l) ; aucune trace ne bouge (la 389 n'a aucun des 16 opcodes) |
| `AlundraTickPadTests` | `:15` | `Overwrite` s'ajoute sans casser l'existant |
| `EntitySearchServiceTests` | `:29-266` (type 4 `:123`, 7 `:173`, 8 `:207`, 11 `:243`) | réutilisables |
| Ordre des slots | `AlundraEntitySlotWorldTests.cs:77`, `:97`, `:117` | prouve l'ordre utilisé par B4-B6 (E3) |
| Recyclage | `AlundraDestroyedEntityRecyclingTests.cs:139-250` | précédent pour `0x88` |
| Vie du héros | `AlundraHeroLifeTests.cs:130`, `:348`, `:365` | précédent pour `0x96` |
| Séquences réelles 140 et 411 | `AlundraEventProgramRunnerTests.cs:4969`, `:5037` | insensibles (E14) |
| Chargements statiques 258/298 et fonds 159 | `AlundraNativeEffectTests.cs:586`, `BackdropStageDefinitionTests.cs:76` | aucun site exécuté (E13) |

Les arcs de corpus (`AlundraHeightTargetSitesTests`) touchent les cartes 36, 115, 127 et 363 : aucune n'est une carte sm3 ou sm4. Aucun autre programme de test ne contient ces opcodes (`rg` sur les tableaux : seul `:335`).

## 6. Banc, arcs et préalables des sites

- **Harnais.** Les citations de l'agent sont justes, sauf E8 et E9.
  - Une classe d'arc porte `[Collection(AlundraMusicPlayerSingletonCollection.Name)]` (`AlundraArcSupport.cs:91-92`).
  - Les 16 lignes zone / nom de monde sont exactes (`world-index.json`).
  - Le précédent d'arcs par site, `AlundraHeightTargetSitesTests.cs:21`, `:23`, `:32` et `:88`, est exact.
- **Oracle.** `oracle.py` fait 649 lignes ; `lib.py:3` pointe hors du dépôt ; `main` (`:589-649`) n'exporte aucun vecteur.
- **Préalables décodés au HEAD**, pour T3 :
  - **452 B[2]** :
    - héros en (13, 12, 1) (`3B` @117), au sol (`70`/`4B` @125-126) ;
    - **dialogue `0D 0C 01` @201 avant `96` @204** ; fin `11` @207.
  - **452 B[3]** : (16, 12, 1) @225, **dialogue `0D 0D 01` @327 avant `97` @330**, fin `11` @332. Il faut les assets de dialogue et `CloseDialogueWithTheButton` (plan §0.2.6, `:540-547`).
  - **424 C[3] @700** :
    - `30 DB 04 2E 00` @692 (drapeau `0x04DB` clair) ;
    - compteur `73 28` (40 ticks), remis à zéro à chaque `AA` faux (@702) : le héros pousse 40 ticks d'affilée, puis `57`, puis marche de `0x18`.
  - **440 B[9] @628** : `36 E8 83` @612 et `31 E8 83` @616 (T `0x83E8` posé), puis `2F 80 00 00` @621 (Carré).
  - **440 C[16] @1755** : `A9 81` pendant une marche du PNJ (`1A 01` @1751).
  - **444 D[15] @1302** : atteint seulement après `87 80 06` @1293 (touché par une attaque de type 6 ; `0x87` porté, `EventProgramRunner.cs:799`).
  - **475 B[2] @502** : drapeau `0x0666` (@76), héros en (44, 47, 1) (@80), `6E` @90 (`ForceAdjusted`), Haut au `2F` @94.
  - **54 B[14] @1226/@1244** : attend `ForceZ < 0` après l'animation 2, puis l'atterrissage (saut scripté d'E19.d2c1).
  - **306 E[14]/E[15]** : `1B`, `40 02 00`, `88 80 00`. **442 E[1]** : `BD`, `A3`, `88 80 02`. Il faut déclencher le programme E (désactivation).

## 7. Noms (ADR-0075)

- Les noms proposés par l'agent sont justes :
  - `Alundra.Events.EventProgramRunner` et `Alundra.Events.EventOpcodeSizeTable` ;
  - `Alundra.Hero.TickPad.Overwrite`, sur des propriétés `uint` ;
  - accesseur de lecture dans `Alundra.Cells.ICellMutator`, implémenté par `Alundra.Cells.CellStore` (seul implémenteur, `CellStore.cs:35`) ;
  - précédent d'aide : `Alundra.Combat.HitOpcodes` (`internal static`, `HitOpcodes.cs:13`) ;
  - prochain ADR libre : 0076.
- Le risque de collision de numéro d'ADR est réel : 8 tranches E20 annoncent « un ADR » sans numéro (`docs/plan-e20-heros.md:7907`, `:7993`, `:8143`, `:8258`, `:8397`, `:8508`, `:8629`, `:8793`, `:8992`).
- Règle 4 de l'ADR-0075 : tout nom de type neuf passe le contrôle d'unicité (`check_names.py`). Un fichier neuf dans la DLL sort `[hors table]` (`check_names.py:96-98`) tant que Q-R-7 n'est pas réglée.
- Noms de tests : pas de changement de type par R3/R4 (§1). Le patron actuel est un fichier plat `Alundra.Tests/Alundra<Sujet>OpcodesTests.cs`, `public sealed class`, `namespace Alundra.Tests`.

## 8. Manques bloquants : liste corrigée et verdict

### Communs

- **G1, calendrier** (corrigé par E12 et M1). Q-E19-SM1 n'a pas de réponse, et le chantier interdit le code (ADR-0075:23). C'est un blocage d'**autorité**, pas un défaut du plan : le plan peut être READY avec le prérequis « après la fusion du chantier, R5 compris ».
  - **M1.** `main` (`11cc63e3`) est au stade R2.
    - Le runner y est `Alundra/Events/AlundraEventProgramRunner.cs`.
    - `Alundra/Events/EventProgramRunner.cs` y est **l'interface** (`git ls-tree main`).
    - HEAD a 127 commits d'avance, non fusionnés.
    - Les citations du §3 ne valent que sur `chantier/restructuration-dll-r3` ou après sa fusion.
  - **M2.** R5 déplacera tous les fichiers de test cités. Sa liste `r5-files.tsv` est figée à 336 lignes et R5 promet « aucun test ajouté ni retiré » (`docs/plan-restructuration-dll.md:872`, `:879`, `:946`).
    - Des fichiers de test ajoutés avant R5 forcent à rejouer sa découverte (INFÉRENCE).
    - Il vaut mieux citer les tests par leur nom.
- **G2, liste fermée.** sm0 manque, comme l'agent l'a vu. Voir aussi E18.
- **G3, vecteurs.** Comme l'agent : aucun exporteur ni consommateur, et l'annexe est figée.
- **G4, valeurs.** Comme l'agent, plus les règles d'exécution par opcode (patron L1-R1…) et le montage (« audit des valeurs »).
- **M8, acceptation et recette.** Le §7 du rapport s'arrête à T5. Il manque :
  - la recette de l'auteur (patron L1-4/L2-4, acceptation 4 : `docs/plan-e19-opcodes.md:6767`, `:6845`) ;
  - la liste fermée du code de test touché ;
  - Release puis Debug, `cmp`, les six traces à l'octet (`:6763-6766`).
- **G5, arcs.** Le §6 fournit des faits pour commencer à les écrire. Voir aussi M3.
- **M3, banc.** Deux cas exigent un monde piloté :
  - `XCollisionEntity` n'est écrit qu'avec un contrôleur (`EntityScriptProxy.cs:2414-2417`) : les arcs A9/AA demandent `RealController: true` (`AlundraArcSupport.cs:52-58`) ;
  - dans un monde qui ne pilote pas les ticks (`WorldProxy.cs:821`), le `MovePlayer` du héros lit `SelfDrivenTickPad` (`EntityScriptProxy.cs:1544`, `:1554-1565`), que l'écrasement de `GameState.TickPad` n'atteint pas (INFÉRENCE).
- **G6, libellés.** Comme l'agent : textes manquants pour A9, AA, 88 et 97, et le libellé de 88 a deux propriétaires.
- **G7, numérotation des questions.** Comme l'agent, plus E15 (l'erreur sur `0x88` vient du rapport).
- **M5, opcodes sans site.** Ils sont six : `B4`, `B7`, `9E` (sm3) et `15`, `66`, `7A` (sm4). Q-sm-6 ne couvre que `9E` (et `88`, à tort). Il faut une politique unique.
- **M14, défauts de l'original.** La règle de l'auteur « un défaut avéré de l'original se corrige » (rappelée en `docs/plan-e19-opcodes.md:4372-4373`) doit être citée pour `0x88` à indice ≥ 3 et pour `0x9E` hors grille. Sans site, le défaut n'est pas avéré : la politique est à écrire.
- **G9, corrections de check.md.** Comme l'agent, plus E17.

### sm3 : REVISE

- Points à reprendre :
  - **S3-1 corrigé (E1).** Les 10 sites testent « l'id brut N est porté ».
    - Aujourd'hui, le `Result` laissé en place vaut 0 sur chaque chemin qui revient en @637 (258 B[6] : @682, @695 et @699 après des prédicats faux). Porter `B2` avant g1a ne change donc rien de visible (INFÉRENCE).
  - **M9, couplage avec E20.g1a.** g1a est READY (`docs/plan-e20-heros.md:6713`) et son T0 recense les appelants des recherches 4 et 11 (`:6649-6650`).
    - Il faut fixer l'ordre entre g1a et sm3.
    - Si g1a passe d'abord, l'épingle « Result 0 jusqu'à g1a » devient fausse.
  - **S3-2** (`9E`) : comme l'agent, plus le masque `0x0002`.
  - **S3-3** (T0 non exécutable tel qu'écrit) : comme l'agent.
  - **S3-4 retiré (E3)**, ramené au montage avec slots.
  - **S3-5** passe en P3 (E16).
- Ce que doit trancher l'auteur : Q-sm-4 et la politique M5 pour `B4`, `B7` et `9E`.

### sm4 : REVISE

- Points à reprendre :
  - **S4-1** (`B3`) : la question de conception est confiée à T0 ; elle devrait être tranchée avant READY. Ajouter les faits de M3 et de la table §4 (direction lue dans `ButtonsHold`, stick sans consommateur).
  - **S4-2 corrigé (E2)** : le test reste vert, il faut changer de témoin.
  - **M7, `0x96` sans vie tirée des stats.** Il faut une règle pour une cible qui ne porte pas sa vie (héros nu, PNJ, `96 80` en programme C).
    - Les précédents ont deux branches : `PlayerManager.cs:1893-1903` et `:1910-1923`.
    - Q-sm-2 reste à confirmer.
  - **S4-4/E19, `0x97`** : Q-sm-1, numéro d'ADR, et choix entre un helper et le code en ligne.
  - **S4-5, `0x88`** : 3 sites, indice ≥ 3 (M14), table `{2, 3, 4}` sans nom en DLL.
  - **M6, `0x15`, `0x66`, `0x7A`.** Aucune relecture complète ni oracle.
    - `0x7A` est un saut : le parcours statique devrait suivre sa cible (précédent L1-R4, `:6678-6679`).
    - `0x66` est un gestionnaire complexe (§0.2.3, O-E19-2).
    - Recommandé : les sortir de sm4.
  - **S4-6, `0x72`** : les arcs passent par D, E ou F ; 444 D[15] exige une attaque de type 6 (§6).
- Ce que doit trancher l'auteur : Q-sm-1, Q-sm-2, la politique M5 pour `15`, `66` et `7A`, et l'indice ≥ 3 de `0x88`.

### Hors sm3/sm4 (P4)

- **M12.** La phrase du plan « après sm1 à sm4 […] plus aucun prédicat à `Result` périmé » (`docs/plan-e19-opcodes.md:12079`) est fausse. `0xC2` en 7 B[10] @1508 est suivi d'un lecteur (`next_is_Result_consumer` = Y) et relève de sm8.

## 9. Inconnus

- **Exécution.** Rien n'a été exécuté : les « OK » viennent de la lecture du code.
- **Recherche contre le binaire.** L'égalité d'`EntitySearchService` et du binaire n'est prouvée que par une traduction python.
- **Monde piloté.** Que `DrivesEntityTicks` soit vrai dans un arc `RealController` est une INFÉRENCE (`_world?.Game != null`).
- **Arcs.** Les drapeaux exacts et l'atteignabilité en jeu des cartes des arcs ne sont pas mesurés.
- **Décompilation.** Les citations de la décompilation (sous-module analyseur) n'ont pas été relues.