# sm0 : état au HEAD (lecture seule)

Base : HEAD `b20eb8ce`, branche `chantier/restructuration-dll-r3`. Les anciens numéros de ligne sont ceux de `9502f75` (état lu par l'annexe sm1-sm4). Pour `Alundra/` et `Alundra.Tests/`, `7d776b3` (état de la première annexe) est identique (`git diff --stat 7d776b3 9502f75 -- Alundra Alundra.Tests` : vide). Étiquettes : FAIT (cité par fichier:ligne ou sortie de commande), INFÉRENCE, INCONNU.

Copie du rapport : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-state/sm0/state.md`. Les scripts de recomptage sont dans `sm-state/sm0/recount/` et `linemap.py` dans `sm-state/sm0/`.

## 0. Méthode et preuves

- Un seul commit non `refactor(dll)` touche la DLL ou les tests depuis `9502f75` : `146e1d81` (R0, partie parent, 2026-10-17 09:52).
  - Il ajoute 18 lignes à `Alundra/Maps/WorldProxy.cs` et modifie `EntitySpawnFactory.cs` de ±6 lignes.
  - Il écrit les noms complets de scripts dans 51 fichiers de tests et ajoute deux tests (`Alundra.Tests/AlundraScriptAssemblyRegistration.cs`, `Alundra.Tests/AlundraScriptNamesInTheDataTests.cs`), soit 53 fichiers de tests touchés.
  - `git log --follow 9502f75..HEAD -- <fichier>` hors `refactor(dll)` est vide pour Runner, SizeTable, EventProgramState, EntitySearchService, TickPad, EntityScriptProxy, GameState, PlayerManager, ICellMutator, MapEventProgramLoader, `AlundraStoryChainOpcodeAudit.cs`, `AlundraStoryChainSkippedOpcodesTests.cs`, `story-chain-skipped-opcodes.tsv` et `IntroTraceHarnessTests.cs`. Seuls `WorldProxy.cs` et `AlundraArcSupport.cs` montrent `146e1d81`.
  - Aucun commit de comportement ne touche le périmètre.
- Aucun fichier de test n'a été renommé ni déplacé (R5 n'est pas faite) : `git diff -M --name-status 9502f75 HEAD -- Alundra.Tests` ne montre que des `M` et 2 `A`. Les types de test gardent leur nom (`AlundraStoryChainOpcodeAudit`, `AlundraStoryChainSkippedOpcodesTests`, `AlundraEventProgramRunnerTests`, `IntroTraceHarnessTests`). Seules les références aux types de la DLL ont changé (R3/R4).
- Traduction des numéros : `linemap.py` compare `git show 9502f75:<ancien>` au fichier HEAD, après application des renommages `old_name -> final.name` de `tools/dll-structure/table.json`.
  - SAME : ligne identique au renommage près. CHANGED : ligne voisine modifiée par un renommage.
  - Le runner a un décalage uniforme de −50 lignes (classe-doc, `EventTraceKind` et `EventTraceRecord` sortis dans leurs fichiers).
  - Les 256 citations `dll_handler` de `docs/plan-e19-sm-annexe/opcodes.tsv` tombent toutes à −50, sur le `case 0xNN` attendu (ou sur `UnknownOpcode` pour les non portés) (`recount/opcodes_cites.py` : 256/256).
- Recomptage indépendant au HEAD (Python, lecture seule). C'est une copie de `census.py` pointée sur les chemins HEAD et sur l'export `alundra-project/` (daté du 2026-10-17 21:51 à 22:21, second export de R4). Résultats :
  - 483 cartes, 110 140 instructions atteignables, 171 valeurs d'opcode atteintes.
  - `Dispatch` HEAD : 158 `case` de premier niveau (`Alundra/Events/EventProgramRunner.cs:563-2105`) ; table : 198 entrées.
  - Opcodes de taille > 0 sans `case` : 29 (`15 3C 3D 56 5F 60 61 66 6A 6B 6C 6D 72 7A 88 96 97 9E A9 AA B2 B3 B4 B5 B6 B7 B9 BB C2`).
  - Sites atteints sur ces 29 : **510 dans 96 cartes, l'ensemble des clés (carte, créneau, programme, pc, opcode) est identique à `skipped-sites.tsv`**.
  - 0 site de taille nulle ou inconnue ; 0 site sur les 30 cartes de la chaîne ; 1 site sur les 7 cartes de combat (`0xB9`, carte 117 `B[1] @171`).
  - La variante « racines du C# » donne le même relevé (110 140). Elle omet les racines B d'entité que `census.py` ajoute et que `AlundraStoryChainOpcodeAudit.ReadRoots` n'ajoute pas.
  - Sommes SHA-1 de l'annexe : 43/43 et 95/95 OK.
- Non fait (interdit ici) : aucune exécution .NET. **Le parcours C# n'a donc jamais tourné sur les 483 cartes** : son équivalence avec le relevé Python reste une INFÉRENCE.

## 1. Citations traduites

Légende : `=` ligne et fichier inchangés ; `déplacé` même ligne au renommage près, autre numéro ou autre fichier ; `scindé` contenu sorti dans un autre fichier ; `PÉRIMÉ` la phrase que la citation soutient n'est plus vraie (voir §2).

### 1.1 Périmètre sm0 (plan §1.4, `report.md`, `check.md`)

| Ancienne | HEAD | Statut |
|---|---|---|
| `AlundraEventProgramRunner.cs:613-2154` (`Dispatch`, plan §1.4, report l.22, check) | `Alundra/Events/EventProgramRunner.cs:563-2104` (méthode `:563-2105`) | déplacé −50, type `EventProgramRunner` |
| `0x00`/`0xFF` : `:459-466` / `:452-457` | `:409-416` / `:402-407` | déplacé |
| `default` `:2152`, `return UnknownOpcode` `:2153` | `:2102`, `:2103` | déplacé |
| `UnknownOpcode` `:3115-3141` (plan §1.4, report, check `:3115`) | `:3065-3091` | déplacé |
| `:478-479` (taille pour le `TraceSink`) | `:428-429` | déplacé |
| `EventOpcodeSizeTable.cs:40-246` (198 entrées) ; commentaire `:10` / `:11` (« 197 ») | `Alundra/Events/EventOpcodeSizeTable.cs:40-246` ; `:10` / `:11` | = (seuls le namespace et un `cref` changent) |
| libellés `:228-230` (`B4-B6`), `:184` (`88`), `:217-218` (`A9`,`AA`), `:199` (`97`), `:152-155` (`6A-6D`), `:142-143` (`60`,`61`), `:231` (`B7`) | mêmes lignes, même fichier | = (aucun libellé corrigé) |
| `:617` / `:617-618` (`case 0x01`, rend 1) | `EventProgramRunner.cs:567-568` | déplacé |
| `:736` (`0x29`, adresse `0x8003DC40`) | `:686` | déplacé (dette inchangée) |
| `:389-396`, `:394` (repli `map_alundra` + journal) | `:339-346`, `:344` | déplacé |
| `:886-899`, `:887-890` (`0x40`, `v1 >= 6` ne fait rien) | `:836-849`, `:837-840` | déplacé |
| `RunScript :213-296` ; `InitializeEventData :385-419` (et `:377-419`) ; `RunOneScriptCall :428-518` ; `FillDataFromCommand :564-588` ; `RunSpriteEvent :314-374`, `:378` | `:163-246` ; `:335-369` (`:327-369`) ; `:378-468` ; `:514-538` ; `:264-324`, `:328` | déplacé −50 |
| `ProductionLoopBudget :137` ; garde `:436-446` ; `_slotAScratchState :187` ; `TraceSink :118` ; `:233-259` ; `:473` ; `:495-507` ; `:515` ; `:302` ; `:217-228` | `:87` ; `:386-396` ; `:137` ; `:68` ; `:183-209` ; `:423` ; `:445-457` ; `:465` ; `:252` ; `:167-178` | déplacé −50 |
| `0x42`/`0x43` `:2037-2074` ; `OpenDialog :2168-2204` | `:1987-2024` ; `:2118-2154` | déplacé |
| `0xA4 :1611-1624` ; `0xAC :1305`, `:2646-2654` ; `0x82 :1560`, `:3150-3189`, `:3172` ; `0x53 :1928-1965` ; `0x2F :1864-1896` ; `0xA8/0xBA :1475-1486` | `:1561-1574` ; `:1255`, `:2596-2604` ; `:1510`, `:3100-3139`, `:3122` ; `:1878-1915` ; `:1814-1846` ; `:1425-1436` | déplacé −50 |
| `Dispatch :849-875` (appels de `AlundraHitOpcodes`) ; `AlundraHitOpcodes.cs` (79 l.) | `EventProgramRunner.cs:799-825` ; `Alundra/Combat/HitOpcodes.cs` (81 l.) | déplacé, type renommé `HitOpcodes` |
| `EventTraceKind` `:44-62` du runner | `Alundra/Events/EventTraceKind.cs:47-65` ; `EventTraceRecord` dans `Events/EventTraceRecord.cs` | scindé |
| `EventProgramRunner.cs` (45 l.) ; `EventProgramState.cs` (57 l.) | `Alundra/Events/IEventProgramRunner.cs` (48 l.) ; `Alundra/Events/EventProgramState.cs` (57 l.) | renommé / = |
| `AlundraWorldProxy.cs:299`, `:874-876` | `Alundra/Maps/WorldProxy.cs:327`, `:902-904` | déplacé +28 (R0 +18, usings) |
| `AlundraWorldProxy.cs:3128`, `:3149`, `:3193`, `:2634` | `:3162`, `:3183`, `:3227`, `:2668` | déplacé |
| `AlundraEntityScriptProxy.cs:1663`, `:1761-1778` | `Alundra/Entities/EntityScriptProxy.cs:1676`, `:1774-1791` | déplacé +13 |
| `AlundraNativeSpriteRegistry.cs:29-36` | `Alundra/Ai/NativeSpriteRegistry.cs:36-43` | déplacé |
| `AlundraStoryChainOpcodeAudit.cs:128-338` (census.py) ; `:36-41` (backlog M-05) ; `:369` (R5, `const string relative`) | `Alundra.Tests/AlundraStoryChainOpcodeAudit.cs:130-340` ; `:38-43` ; `:369` (déjà au HEAD) | déplacé +2 ; fichier et type non renommés (R5) |
| `IntroTraceHarnessTests.cs:322` (`ImplementedOpcodes`) | `Alundra.Tests/IntroTraceHarnessTests.cs:332-387` ; usage `:1791` ; message d'en-tête `:1721` | déplacé +10 ; **miroir PÉRIMÉ** (§2, ligne 9) |
| `AlundraEventProgramRunnerTests.cs:404-433` (`FlagBranch`) ; `:320`/`:323`/`:338`/`:353` (3 tests `UnknownOpcode_*`) | `:412-441` ; `:330-343` (`KnownSize_SkipsBySize`, 0xB3), `:345-358` (`0x5F`), `:360-...` (`NoKnownSize`) | déplacé +8 |
| `AlundraDialogueOpcodesProductionTests.cs:17-31` ; `AlundraYarnVariableStorageTests.cs:120` | `:23-37` ; `:123` | déplacé |
| `opcodes.tsv` : 256 × `AlundraEventProgramRunner.cs:NNN` ; 9 citations de tests (`AlundraHeightWaitOpcodesTests.cs:250-298`, `AlundraEventProgramRunnerProgramIndexTests.cs:206`, `AlundraDialogueOpcodeDispatchTests.cs:320-350`, `AlundraDialogueBoxOracleComparisonTests.cs:110`, `AlundraZWaitOpcodesTests.cs:288-304`) | −50 sur le runner (256/256 ok) ; `:252-300`, `:209`, `:325-355`, `:113`, `:290-306` | déplacé (+0 à +5) |
| `EntitySearchService.cs` (plan §1.4, sans ligne) ; `:98-262` | `Alundra/Entities/EntitySearchService.cs:99-263` | déplacé +1 |
| `docs/plan-e19-m-annexe/backlog-2026-10-03.md:168-180` (tableau G7) | même fichier, tableau `:168-180` (en-tête `G7` `:166`) | = |

Citations de la décompilation (`EntityEventHandlers.cs`, `PlayerManager.cs:1767/4749`, `GameEngine.cs:1943`, `EntityManager.cs:48/969`) : sous-module analyseur, hors DLL, non traduites (« ne pas corriger la décompilation »).

### 1.2 Citations des autres tranches (pour mémoire, annexe sm1-sm4)

| Ancienne | HEAD |
|---|---|
| runner `:377-419`, `:1990-2040`, `:2011-2013`, `:~2011` (point d'insertion après `0x3F`), `:2691`, `:3312-3334`, `:250-254`, `:275-280`, `:760-762`, `:1213-1222`, `:1879`, `:2700-3050` | `:327-369`, `:1940-1990`, `:1961-1963`, `~:1986` (le `case 0x3F` va de `:1961` à `:1986`, `0x42` commence `:1987`), `:2641`, `:3262-3284`, `:200-204`, `:225-230`, `:710-712`, `:1163-1172`, `:1829`, `:2650-3000` |
| `AlundraEntityScriptProxy.cs:76-77`, `:81`, `:325`, `:341-343`, `:344`, `:351`, `:1212-1213`, `:2411-2418`, `:2596` | `EntityScriptProxy.cs:89-90`, `:94`, `:338`, `:354-356`, `:357`, `:364`, `:1225-1226`, `:2424-2431`, `:2609` |
| `AlundraWorldProxy.cs:2077`, `:2453-2454`, `:2493`, `:2893`, `:2955-2960`, `:3652-3653` | `WorldProxy.cs:2111`, `:2487-2488`, `:2527`, `:2927`, `:2989-2994`, `:3698-3699` |
| `EntitySupport.cs:71-75`, `:276-290`, `:296-307` ; `AlundraScriptedMotion.cs:191`, `:719-720` ; `EntityFlags.cs:103,136` ; `IEntityWorldContext.cs:24` | `Physics/EntitySupport.cs:72-76`, `:277-291`, `:297-308` ; `Physics/ScriptedMotion.cs:197`, `:725-726` ; `Entities/EntityFlags.cs:103,136` ; `Maps/IEntityWorldContext.cs:35` |
| `AlundraPlayerManager.cs:610-631`, `:1750`, `:1873`, `:1885`, `:1893-1910`, `:1957` | `Hero/PlayerManager.cs:623-644`, `:1763`, `:1886`, `:1898`, `:1906-1923`, `:1970` (méthodes : `IncreaseHpMax :1893`, `RestoreLifeAndMagic :1910`) |
| `AlundraGameState.cs:143` ; `AlundraTickPad.cs:31-49` | `Session/GameState.cs:152` ; `Hero/TickPad.cs:33-51` (setters toujours `private`) |
| `AlundraCellStore.cs:13-40` (`IAlundraCellMutator`) | `Cells/ICellMutator.cs:16-39` (interface `:21`, 3 écritures, aucune lecture) |
| `AlundraEntitySpawnFactory.cs:493` ; `AlundraMapSizeReader.cs` | `Entities/Spawning/EntitySpawnFactory.cs:497` ; `Maps/MapSizeReader.cs` |
| `AlundraClassAndBranchOpcodesTests.cs:140-216` | `:143-219` |
| `docs/plan-e20-heros.md:6287`, `:6321` | `:6525`, `:6559` (le texte « PlatformEntity est trompeur » y est toujours) |

## 2. Affirmations sur la DLL : vraies ou non au HEAD

| # | Affirmation (source) | Au HEAD | Preuve |
|---|---|---|---|
| 1 | `Dispatch` : 158 `case`, `0x00`/`0xFF` dans la boucle d'appel | VRAI | 158 lignes `            case 0x..:` dans `EventProgramRunner.cs:563-2105` ; boucle `:402-416` |
| 2 | Table : 198 entrées, commentaire « 197 », `0xC5-0xFE` absents | VRAI | 198 lignes `{ 0x.., new(` ; commentaire `:11` ; 58 octets absents (recompte) |
| 3 | 29 opcodes sautés par la taille, 9 de taille 0, 58 hors table | VRAI | recompte §0 ; tailles 0 : `0E 0F 13 14 18 76 77 9D AE` (lignes 56-66, 164-165, 205, 222) |
| 4 | `UnknownOpcode` saute par la taille (un avertissement par opcode), sans taille ou ≤ 0 rend 0 (fin d'appel), n'écrit pas `Result` | VRAI | `EventProgramRunner.cs:3065-3091` |
| 5 | 510 sites, 96 cartes, aucun sur la chaîne, un en combat (`0xB9`) | VRAI | recompte (clés identiques à `skipped-sites.tsv`) ; carte 117 `B[1] @171` |
| 6 | La liste fermée de la chaîne est vide | VRAI | `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` : 1 ligne (en-tête) |
| 7 | « Le test statique du corpus entier n'existe pas » | VRAI pour la liste et les règles 1 à 4 ; mais un test parcourt déjà les 483 cartes | `AlundraMissingAnimationWaitCorpusTests.cs:389-399` (`world-index.json`, `SitesOfMap` → `LoadMap` pour chaque carte, parcours propre `Census :70`) |
| 8 | `IsPorted` interroge le vrai runner (pas un miroir) | VRAI | `AlundraStoryChainOpcodeAudit.cs:75-121` (`ProbeOpcode`) |
| 9 | `IntroTraceHarnessTests.ImplementedOpcodes` est un miroir manuel | VRAI mais **périmé de 21 opcodes** | comparaison à `Dispatch` : manquent `4C 4D 4E 4F 71 86 87 90 91 92 93 94 95 A0 A1 A2 A3 BC C0 C1 C3` ; aucun opcode du miroir sans `case` (`:332-387`, usage `:1791`) |
| 10 | `Result` reste périmé quand un prédicat est sauté | VRAI | `UnknownOpcode` n'écrit pas `Result` ; `EventProgramState.cs:36` ; 292 des 510 lignes sont suivies d'un lecteur de `Result` (`03 04 4A 4B 79 7E 7F`) |
| 11 | `Parameters` : 10 entiers, marque d'attente en `[1]` remise à 0 après un opcode qui rend ≠ 0, non effacés par `InitializeEventData` | VRAI | `EventProgramState.cs:32` ; `EventProgramRunner.cs:465` ; `:335-369` (commentaire E19.m9) |
| 12 | `0x01` rend 1 (le binaire 0) | VRAI | `EventProgramRunner.cs:567-568` ; au moins 10 fichiers de tests ouvrent un programme par `0x01` (regex `NewDocument(0x01,` / `new[] { 0x01,`, borne basse) |
| 13 | `map_alundra` non portée : repli sur la table de la carte, journal une fois | VRAI | `EventProgramRunner.cs:339-346` ; 6 racines à bit `0x80` clair, toutes en carte 60 (`C[1]`, `C[4]` ×4, `C[10]`) ; **8 des 510 sites** (carte 60, `C[1] @164 6A`, `@168 6B`, `@185 6A`, `@189 6C` ; `C[10] @644 6A`, `@648 6B`, `@665 6A`, `@669 6C`) ne sont atteints que par ces racines (`recount/map60b.py`) |
| 14 | « Pas de plafond de 63 entités ni de réutilisation de créneaux » (report §6, plan sm9, M-12) | **FAUX** | `Alundra/Entities/EntitySlotTable.cs:14-60` (E20.e0a2, ADR-0064, `03c38630`) ; refus d'apparition table pleine : `WorldProxy.cs:1074-1080`, `:3399-3404`, `:3528-3532` ; `0x2D`/`0x8A`/`0x8B` passent par `SpawnEntityByRecordId` (`EventProgramRunner.cs:2356`, `:2388`, `:2419`) |
| 15 | « Rognage de bord non porté » (O-E19-17) | PARTIEL | `Physics/ScriptedMotion.cs:611-613`, `:684-692` : borné seulement pour `HasLogicalZ` ; « le héros et les entités nées d'un enregistrement gardent un pas non borné » |
| 16 | `EntitySearchService` égale le binaire pour les 12 types | INFÉRENCE (non rejoué) | code lu `Entities/EntitySearchService.cs:99-263`, inchangé depuis `9502f75` ; équivalence établie par l'annexe (traduction Python + relecture à la main du critique) |
| 17 | `PlatformEntity` n'est jamais écrit par la DLL (sauf copie de clone) | VRAI | `Entities/EntityScriptProxy.cs:101` (champ), seule affectation `:2609` ; des tests l'écrivent à la main (`AlundraEntityCollisionTests.cs:98`, `AlundraNpcCharacterControllerMoverTests.cs:2217`, oracles Beannoide/Muruta) |
| 18 | `TickPad` : setters privés, aucune couture ; `GameState.TickPad` avancé en tête de tick ; lu par `0x2F` et `MovePlayerOnTickPad` | VRAI | `Hero/TickPad.cs:33-51` ; `Session/GameState.cs:152` ; `Maps/WorldProxy.cs:2527` ; `EventProgramRunner.cs:1829` ; `WorldProxy.cs:2927` |
| 19 | `ICellMutator` n'a que des écritures (pas d'accesseur pour `0x9E`) | VRAI | `Cells/ICellMutator.cs:21-39` |
| 20 | Libellés faux/vides de la table (`6B-6D`, `88`, `97`, `A9`, `AA`, `B4-B6`) | VRAI, rien corrigé ; aucun test ne les épingle | table inchangée ; grep des libellés dans `Alundra.Tests` et `docs/*.txt` : seulement un commentaire (`AlundraEventProgramRunnerTests.cs:333`) |
| 21 | Aucun des 21 opcodes lus par sm1-sm4 n'a de `case` | VRAI | ils sont tous parmi les 29 sans `case` |
| 22 | `ProductionLoopBudget = 1024`, un avertissement par programme | VRAI | `EventProgramRunner.cs:87`, `:386-396` |
| 23 | Les annexes `docs/plan-e19-sm-annexe/*.py` se relancent | FAUX tel quel | elles ouvrent `Alundra/Scripts/EventOpcodeSizeTable.cs` et `.../AlundraEventProgramRunner.cs` (n'existent plus) ; annexe figée (D-R-6) ; il faut une copie aux chemins HEAD (ce que fait `recount/census_head.py`) |

## 3. Tests qui épinglent les opcodes sautés ou les sites

Ce que le plan nomme : seulement `IntroTraceHarnessTests.cs:322` (report §8). Au HEAD, au-delà :

1. **Liste fermée de la chaîne** : `Alundra.Tests/AlundraStoryChainSkippedOpcodesTests.cs` (21 `[Fact]`) : règles 1 à 4 sur 30 cartes (`:41-71`), `TheListIsOnTheChainAndTheThirtyMapsWithoutASiteHaveNone` (`:73-86`), `TheMapsWithTwoProgramsAtOnePcAreTwoLines_NoneSinceThe0x4CIsPorted` (`:88-96`), `Oracle_AnOpcodeTheRunnerHandlesIsPorted_...` (`:102-111`), 7 tests du parcours (`:132-194`), 7 tests de fonctions de règle (`:206-301`). `Corpus` (`:21-25`) = `ChainMaps.SelectMany(WalkExportedMap)`.
   - **`TheListIsOnTheChain...` (`:78-85`)** : `Assert.Empty(list.Select(l => l.Site.Map).Distinct().Except(ChainMaps))`, `MapsWithoutSkippedSite.Intersect(...)` vide, `ChainMaps ∩ ExemptCombatMaps` vide. Il devient rouge si les 510 lignes (96 cartes hors chaîne) entrent dans le fichier de la chaîne.
   - **`Oracle_...` (`:110`)** : `Assert.Contains(Enumerable.Range(0,255), op => taille > 0 && !IsPorted(op))` rougit quand le dernier opcode sauté est porté.
2. **Données** : `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` (en-tête seul), lue par `FindListPath` (`AlundraStoryChainOpcodeAudit.cs:367-385`) et par la règle 5 des arcs : `AlundraDay3SceneArcTests.cs:41-52` (`AssertSkippedAreListed`, appelée `:87`, `:132`).
3. **Épingles du saut par la taille dans le runner** (`AlundraEventProgramRunnerTests.cs`) :
   - `UnknownOpcode_KnownSize_SkipsBySize` `:330-343` (exemple **`0xB3`**, sm4) ;
   - `UnknownOpcode_0x5F_SkipsEightBytes_AsTheBinary` `:345-358` (`0x5F`) ;
   - `UnknownOpcode_NoKnownSize_TerminatesScriptCall` `:360-...` (`0xC5`).
   - Le plan documente lui-même le motif « repointé sur un opcode encore non porté » à chaque tranche (plan l.4878, l.9113, l.9176) ; sm4 ne le dit pas.
4. **Aucun test n'affirme le contenu de la table des tailles** : 17 occurrences de `EventOpcodeSizeTable.Entries` dans `Alundra.Tests`, toutes en lecture (décodage, oracle, trace). Les libellés n'apparaissent que dans les traces du harnais d'intro (`IntroTraceHarnessTests.cs:1485`).
5. **Arcs qui interdisent tout opcode sauté** (`ArcChecks.AssertNothingSkippedOrExceeded`, `AlundraArcChecks.cs:51-59`) : 18 fichiers de tests d'arcs, plus 4 fichiers de support/audit.
   - Aucun ne tourne sur une des 96 cartes à sites, sauf **la carte 117** (`0xB9` `B[1] @171`) : `AlundraLeverBasculeArcTests.cs:49`, `AlundraLeverPillarArcTests.cs:24`, `AlundraLeverSmokeTests.cs:74` et `:81` (par `LeverArcs.World117`, `AlundraLeverArcSupport.cs:69`).
   - Ils passent aujourd'hui parce que le chemin exécuté n'atteint pas ce site.
6. **Miroir manuel** `IntroTraceHarnessTests.ImplementedOpcodes` (`:332-387`, usage `:1791`) : il sert à l'étiquette `[implemented]`/`[NOT IMPLEMENTED]` de `docs/intro-programs-389.txt`, une des « six traces » dont l'acceptation exige `git diff --exit-code`.
   - Aucun des 21 opcodes manquants n'apparaît dans cette annexe (une seule occurrence de « NOT IMPLEMENTED » : l'en-tête, ligne 2). Remplacer le miroir par `IsPorted` ne la change donc pas.
   - Réécrire le message `:1721` (« reflect AlundraEventProgramRunner's V1 dispatch table ») la change.
   - Les ensembles de forçage `OptimisticPredicateOpcodes` (`:1459`) et `PessimisticPredicateOpcodes` (`:1479`) sont vides.
7. **Parcours dupliqué** : `AlundraMissingAnimationWaitCorpusTests.Census` (`:70`) recopie les règles de flot de `WalkProgram` (commentaire `:62-69`) ; aucun test ne vérifie que les deux restent d'accord.
8. **`0x01` comme bourrage** (M-03, sm9) : au moins 10 fichiers de tests (`AlundraZWaitOpcodesTests.cs:45`, ...).

Ce que chaque tranche déplacerait :

| Tranche | Lignes de liste | Tests existants touchés (prévisibles) |
|---|---|---|
| sm0 | 0 | `IntroTraceHarnessTests` (miroir) ; `TheListIsOnTheChain...` si la liste est étendue |
| sm1 | −187 (11 cartes) | aucun test d'opcode ; ajout d'arcs |
| sm2 | −129 (8 cartes) | aucun ; l'arc `60 C[1]` repose sur les 8 sites à bit clair (§6, B7) |
| sm3 | −42 (11 cartes) | aucun ; 10 sites `0xB2` sans effet visible avant E20.g1a |
| sm4 | −90 (50 cartes) | `UnknownOpcode_KnownSize_SkipsBySize` (0xB3) à repointer ; `TickPad` : couture |
| sm5/sm7/sm8 | −17 / −42 / −3 | le dernier porté fait rougir `Oracle_...` (`:110`) |

## 4. Banc et arcs (ce que sm0 et les tranches suivantes réutiliseraient)

- **Parcours statique** : `AlundraStoryChainOpcodeAudit.cs`.
  - Données : `ChainMaps :39`, `ExemptCombatMaps :46`, `MapsWithoutSkippedSite :51`, `WaitOpcodes :54` (8 valeurs `20 21 22 23 26 47 48 5F`), `PredicateOpcodes :57`, `KnownScenePredicateSites :61`.
  - Oracle et parcours : `IsPorted :75`, `ProbeOpcode :88`, `WalkMap :130`, `WalkProgram :163`, `ReadRoots :294`, `WalkExportedMap :356`.
  - `LoadMap :343` passe par `MapEventProgramLoader.Load` et attend un seul `*.tileMap` par carte, ce qui est vrai pour les 483 dossiers.
  - Liste : `FindListPath :367`, `ParseList :389` (11 colonnes : carte, créneau, programme, libellé, pc, opcode hexadécimal, nom, classe, tranche, niveau, épingle).
  - Règles : `Rule1UnlistedSites :411`, `Rule2StaleLines :419`, `Rule3WaitsOrUnsized :427`, `Rule4ScenePredicates :437`, `Rule5UnlistedArcSites :448`, `Describe :472`.
  - Types : `ProgramSite :16`, `ReachedSite :19`, `ListedSite :23`. La colonne « tranche » est le champ `Slice` : c'est le propriétaire possible.
- **Cartes du corpus** :
  - `SaveGameDirectorTestSupport.FindProjectRoot` (`SaveGameDirectorTestSupport.cs:70`) lève si l'export manque, selon la convention E19.e « un export absent fait échouer ».
  - `Maps/world-index.json` a 483 clés. Précédent de parcours des 483 cartes : `AlundraMissingAnimationWaitCorpusTests.cs:389-399`, qui utilise `ProjectRootFinder.Find` (`AlundraJumpTestSupport.cs:432`).
  - Volume lu : 483 `*.tileMap` = 197,6 Mo de JSON (max 0,61 Mo) et 483 `*.events.json` = 3,5 Mo. `xunit.runner.json` : `parallelizeTestCollections: false`.
- **Exécuter des octets réels sans monde** :
  - Charger le document (`MapEventProgramLoader.Load` ou `LoadMap`), vérifier les octets au pc réel, `new EventProgramRunner(document, new GameState())`, puis `RunOneScriptCall(entity, state)` avec `Codes = octets + 0xFF` et une entité nue (`AlundraEventProgramRunnerTests.cs:4155-4180`, `IsForceAdjusted_0x6E_RealOccurrence_AncientShrine27` ; montage `NewDocument :28`, `NewRunner :38`, `NewEntity :41`).
  - Départ à un pc : `AlundraRandomDirectionModesTests.cs:115-140`, `state.CodeIndex = 144`.
  - Passe d'événements de carte réelle : `AlundraInitializeEventDataTests.cs:47-90`, `WorldProxy.RunMapEventsPass`, `MaxIterationsPerCall = 1`, `TraceSink`.
- **Exécuter dans un monde réel (arcs)** :
  - `ArcSpec :62`, `ArcArrival :69`, `ArcInstruction :73`, `ArcRun :94` (ctor `:175`, `Trace :132`, `State :142`, `Log :373`, `EntityByRecord :390`, `FirstIndexOf :393`, `Has :395`, `HasAfter :398`, `SkippedOrExceeded :414`, `OneFrame :430`, `Press :454`, `PlaceHero :470`, `RunUntil :483`, `BuildWorld :620`) ; `ArcChecks` (`AlundraArcChecks.cs:17-59`) ; `LeverArcs.Spec` (`AlundraLeverArcSupport.cs:74`, constantes `:66-71`).
  - Les arcs de plan §1.3 (A0 à A9 et suivants) sont tous sur la chaîne (cartes 390, 476, 478, 392, 391, 416, 163, 172) : **aucun ne touche un des 510 sites**.
- **Aucun banc d'oracle de valeurs pour sm1-sm4 n'existe dans les tests.** L'oracle MIPS est dans `docs/plan-e19-sm-annexe/sm1-sm4/work/oracle/` et `check/` (il lit `ALUN_CD.EXE` hors dépôt). Les vecteurs (« monde, octets, `v0`, `Result`, mots écrits ») sont à produire en T0, sans format ni dossier créé.

## 5. Noms nouveaux (règles ADR-0075, garde Q-R-7)

Le plan ne nomme **aucun** type, fichier ou test pour sm0. Propositions (marquées PROPOSITION, à trancher) :

| Objet | Nom avant R5 (convention de `Alundra.Tests/`) | Nom après R5 / R7 |
|---|---|---|
| Classe de test du corpus | `AlundraCorpusSkippedOpcodesTests` dans `Alundra.Tests/AlundraCorpusSkippedOpcodesTests.cs`, `namespace Alundra.Tests` (PROPOSITION) | `CorpusSkippedOpcodesTests` dans `Alundra.Tests/Events/`, `namespace Alundra.Tests.Events` (INFÉRENCE d'après le plan R5 : « sans préfixe `Alundra` », miroir par domaine, `Arcs/` et `Support/` ; le dossier de l'audit est la question 6 de Q-R-3) |
| Liste fermée du corpus | `Alundra.Tests/Data/corpus-skipped-opcodes.tsv` (PROPOSITION) ou extension de `story-chain-skipped-opcodes.tsv` | Q-R-3 point 6 (rester ou suivre l'audit dans `Events/`) ; le littéral `AlundraStoryChainOpcodeAudit.cs:369` change alors |
| Aide du parcours / règles | méthodes dans `AlundraStoryChainOpcodeAudit` (4 types dans le fichier) | `StoryChainOpcodeAudit` (nom cité par le plan R5, `plan-restructuration-dll.md:889`) |
| Annexes de vecteurs | `docs/plan-e19-sm-annexe/sm1/` à `sm4/` (nommés par sm1-sm4 §7) | inchangé |
| ADR | « ADR du sens de `0x60`/`0x61` » (sm1) ; ADR de `0x97` = PM (Q-sm-1, sm4) | numéros libres à partir de `0076` (le dernier est `0075-the-gameplay-dll-...`) ; à revérifier avant chaque commit (collisions déjà vues) |
| Membres cités sous l'ancien nom | `AlundraTickPad.Overwrite(hold, pressed, released, interval)` (sm4) ; `IAlundraCellMutator` (accesseur de lecture, `0x9E`) ; `AlundraPlayerManager.SetPlayerMp` ; `CellMutator.CopyCellRectangle` (sm5) | `Alundra.Hero.TickPad.Overwrite` (`Alundra/Hero/TickPad.cs`) ; `Alundra.Cells.ICellMutator` (`Alundra/Cells/ICellMutator.cs`) ; `Alundra.Hero.PlayerManager.SetPlayerMp` (`:1970`) ; `ICellMutator.CopyCellRectangle` / `CellStore` |

Garde Q-R-7 :
- `check_names.py --tree --stage final` marque `[hors table]` tout fichier ajouté à `Alundra/` (`tools/dll-structure/check_names.py:98`). Il ne lit que la DLL (`scan_types.dll_decls`, `git ls-tree ... -- Alundra`).
- sm0 est « tests seuls » : aucun fichier de DLL, donc la garde actuelle ne se déclenche pas. La R7 proposée (`--rules`) contrôlerait aussi les tests (dossier = namespace, pas de préfixe `Alundra`, nom unique, aucun type au nom d'un segment).
- sm1 à sm4 ajoutent des `case`, des libellés et une méthode (`TickPad.Overwrite`) : aucun fichier ni type de DLL.
- Les fichiers à créer seraient dans sm5 (export `MapCopies`, côté convertisseur), sm6 (ombres de `0xAC`, propriétaire inconnu), sm7 (lecteur XA), sm9 (table globale `map_alundra`) : non nommés. Tout fichier de DLL ajouté avant R7 échoue le contrôle.

## 6. Manques qui bloqueraient une tranche exécutable

### Bloquants pour sm0

- **B1. Pas de tranche écrite.**
  - sm0 n'est qu'une ligne du tableau (plan `:12067`) et du rapport (§8). Les listes T0 à T5 de `sm1-sm4/report.md` §7 ne couvrent que sm1 à sm4 (« Prérequis commun : E19.sm0 »).
  - Il manque les tâches, les tests nommés, les valeurs attendues, les arrêts, le retour arrière et la ligne de revue. Le plan exige plan-verifier tranche par tranche (`:12-18`) et que chaque tranche soit « détaillée, relue et approuvée » (§1.1). Le plan §1.4 est « à approuver ».
- **B2. Où va la liste ?**
  - Le fichier existant est verrouillé à la chaîne par `AlundraStoryChainSkippedOpcodesTests.cs:73-86` (rouge avec 96 cartes hors chaîne) et lu par la règle 5 des arcs (`AlundraDay3SceneArcTests.cs:44`).
  - La proposition ne dit ni « second fichier » ni « test existant modifié » (un test existant qui change pour une autre raison est un arrêt, plan §5).
  - Q-R-3 point 6 (place du fichier de données) et point 5 (`AlundraStoryChainOpcodeAudit.cs` a 4 types : `ProgramSite`, `ReachedSite`, `ListedSite`, la classe) sont ouvertes.
- **B3. Format de la liste.**
  - `ParseList` (`:389-404`) lit 11 colonnes (carte, créneau, programme, libellé, pc, opcode, nom, classe, tranche, niveau, épingle). `skipped-sites.tsv` en a 9, dans un autre ordre (carte, nom de carte, créneau, programme, pc, opcode, opérandes, opcode suivant, lecteur de `Result`).
  - Il faut un convertisseur (jamais décrit) et le sens de `classe` et de `niveau` (le niveau `X` « hors chaîne » serait constant : 510 `X`).
  - Il faut aussi fixer la place du propriétaire : la colonne `Slice` existe déjà, mais « une colonne propriétaire » du plan n'est pas un champ de `ListedSite`.
- **B4. La règle de propriétaire a disparu.**
  - Le G7 d'origine (`backlog-2026-10-03.md:166-190`, plan `:10137-10140`) disait : « aucune ligne sans propriétaire, aucune ligne dont le propriétaire est fermé ».
  - sm0 ne garde que « manquante, périmée, attente sautée, taille nulle » et donne « propriétaire par défaut `à placer` » (report §8). Aucune règle ne rend une ligne `à placer` rouge.
  - Le vocabulaire des propriétaires n'est pas défini (sm1 à sm8, `E14`, `E18`, auteur). La correspondance opcode vers tranche n'existe que dans le tableau du plan (`:12067-12077`).
- **B5. Quand ? (Q-E19-SM1 ouverte).**
  - ADR-0075 : « aucune autre tranche (E20, E14, E19) en parallèle ». R5 n'est pas exécutée (elle attend Q-R-3 ; `plan-restructuration-dll.md:861`).
  - Un fichier de test ajouté avant R5 désynchronise la table de R5 (336 lignes ; `prove_r5.py --kind code` apparie par la table).
  - Ajouté après, il doit suivre les règles finales (dossier par domaine, sans préfixe) alors que la R7 de Q-R-7 (garde `--rules`, proposée) n'existe pas.
- **B6. « Aucune attente sautée » n'est pas vérifiable pour `0xBB`.**
  - La règle 3 ne connaît que 8 opcodes (`WaitOpcodes`, `:54`).
  - `0xBB` a 2 sites (carte 347 `B[6] @837`, carte 477 `B[1] @151`, tous deux suivis de `0xFF`). Son gestionnaire binaire de 396 octets « compte 60 ticks » (`opcodes.tsv`) et utilise le mémo `P2` (`sm1-sm4/report.md` §5).
  - INFÉRENCE qu'il suspend ; `report.md` §6 ne le met pas dans les suspensifs. Soit la règle est aveugle à `0xBB`, soit elle devient rouge dès le premier jour. À trancher (propriétaire E18).
- **B7. Huit sites de la carte 60 sont peut-être inexistants dans l'original.**
  - `C[1] @164/168/185/189` et `C[10] @644/648/665/669` (`6A`,`6B`,`6A`,`6C`) ne sont atteints que par les 6 enregistrements « Pot magique » à bit `0x80` clair. Le binaire lit pour eux la table globale `map_alundra`.
  - Cette table C est d'une entrée : `data-extracted/data/map_alundra.json`, `SpriteInfo/Header`, `EventCodesCSize` = 2 octets. L'annexe dit `[0]`, programme vide : INFÉRENCE.
  - sm0 les listerait comme sites réels, et sm2 (T3) y bâtit un arc `60 C[1]`. Propriétaire à désigner (sm9, ou « artefact du repli »).
- **B8. Aucun relevé épinglé ni arrêt de recoupement.**
  - L'acceptation devrait épingler 510 lignes, 96 cartes, les 29 opcodes sautés, les comptes par opcode et par tranche (187/129/42/90/17/42/3, vérifiés).
  - E19.e avait un arrêt « si le parcours C# ne redonne pas exactement les 198 lignes » (plan `:4184-4186`).
  - Ici le parcours C# sur 483 cartes n'a jamais tourné et sa durée est inconnue (197 Mo de JSON, collections xUnit en série).
- **B9. Deux énoncés du même test.** L'enveloppe (plan `:615`, ligne E19.m) écrit toujours « test statique : aucun opcode atteignable sauté dans le corpus hors E14 et E18 ». Le §1.2s (`:10137`) dit « ne peut pas passer de longtemps » (G7 « reste »). sm0 reprend M-05/G7 sans retirer G7 d'E19.m ni réécrire la règle. Même chose pour M-03 (`0x01`, « E19.m » et sm9).

### Inexactitudes du texte à corriger (non bloquantes)

- Plan `:12081-12082` : « Les 21 opcodes sautés atteints sont lus au binaire » est faux. Les 21 lus (`60 61 3C 3D 6A 6B 6C 6D 72 88 96 97 9E A9 AA B2 B3 B4 B5 B6 B7`) comptent 4 opcodes sans site (`3C 9E B4 B7`) et omettent 4 opcodes atteints (`56 B9 BB C2` = 17+42+2+1 = 62 sites non lus).
- Plan `:12079` : « plus aucun prédicat à `Result` périmé » après sm1-sm4 est faux. `0xC2` (carte 7 `B[10] @1508`, suivi de `03`) reste jusqu'à sm8. Plan `:12049` : « dix » prédicats suivis d'un lecteur, mais ce sont onze opcodes (`60 61 6B 6C 6D A9 AA B2 B5 B6 C2`).
- sm9 :
  - « plafond de 63 entités (M-12) » est fait (§2, ligne 14).
  - « rognage de bord » est partiel (ligne 15).
  - M-13 (recherches sans allocation), présent dans `report.md` SM9, a disparu de la ligne du plan.
  - M-03 (`0x01`) n'a aucune question numérotée au §3 (le rognage a O-E19-17, `:12409`).
- Numérotation : le plan `:12097` annonce « Q-sm-1 à Q-sm-9 (§3) » mais le §3 s'arrête à Q-sm-8. L'annexe a 9 questions (Q-sm-7 = `0x88` indice ≥ 3, Q-sm-8 = libellés, Q-sm-9 = cartes 55 à 57) et le plan les a décalées (plan Q-sm-7 = libellés, plan Q-sm-8 = cartes 55-57) : renvois croisés faux.
- La liste fermée ne couvre que les 29 opcodes sautés. Les 5 partiels (`2F 53 82 A4 AC`) et les no-op (`01 A8 BA`) n'ont aucune garde statique (sm6 compte 33 sites hors liste).
- Le motif d'E19.l impose « `IntroTraceHarnessTests.ImplementedOpcodes` reçoit les opcodes portés » (L1-R8, plan `:6687`). sm0 remplace le miroir mais ne retire pas la règle du motif. Ni le repointage de `UnknownOpcode_KnownSize_SkipsBySize` (sm4) ni le sort de l'`Oracle_...` (dernier opcode) ne sont écrits.
- Questions d'auteur qui touchent sm0 : **Q-E19-SM1** (quand, et propriétaire des ombres `0xAC`, hors des 510), **Q-R-3** points 1, 5 et 6, **Q-R-7**.
  - Les Q-sm-1 à Q-sm-9 ne bloquent pas sm0 (elles concernent sm1 à sm4).
  - Q-sm-6 (porter `0x9E` sans site ?) décide seulement si les 8 opcodes à 0 site (`15 3C 5F 66 7A 9E B4 B7`) restent « sautés ». Son texte du plan dit « `0x9E` et `0x88` (0 site) », alors que `0x88` a 3 sites (306 `E[14]` ×2, 442 `E[1]`) et appartient à sm4 (l'annexe parlait d'un indice ≥ 3, sans site).
  - Q-sm-8 (cartes 55-57, annexe Q-sm-9) ne change pas la liste.

## 7. Inconnus

- Résultat et durée du parcours C# sur les 483 cartes (jamais exécuté) ; équivalence avec Python : INFÉRENCE (même nombre d'instructions avec ou sans racines B d'entité).
- Si `0xBB` suspend (B6) ; si les 8 sites de la carte 60 existent dans l'original (B7) ; si les cartes 55 à 57 sont atteignables (Q-sm-9 de l'annexe).
- `EntitySearchService == binaire` : non rejoué ici.
- « 157 des 158 opcodes portés ont un test qui les nomme » : relevé heuristique de l'annexe, non revérifié.
- Placement final des fichiers de tests après R5 (`r5-files.tsv` est dans le scratchpad de la session R5, pas dans le dépôt).
- Comportement des boucles `0x8A` du corpus (cartes 28, 30, 134, 147, 159, 160) quand la table de créneaux est pleine : le plafond existe, le comportement de la boucle n'est pas mesuré.
- Si le moteur ou le convertisseur re-génère `alundra-project/` avec d'autres événements : le relevé ci-dessus vaut pour l'export du 2026-10-17 22:21.