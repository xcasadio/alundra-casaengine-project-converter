# Rapport sm3 / sm4 : citations traduites au HEAD et état de la proposition

Racine du dépôt : `D:/development/repo/alundra-casaengine-project-converter`. Les chemins ci-dessous sont relatifs à cette racine.

**Verdict : la proposition n'est pas convertible en tranche exécutable telle quelle.**
- Le code DLL qu'elle suppose est intact au HEAD, aux numéros de ligne près.
- Mais le filet `sm0` n'existe pas, le chantier de restructuration n'est pas clos, et aucun test, arc, vecteur ni libellé n'est nommé.
- Les points bloquants sont au §6.

**Méthode.**
- HEAD = `e8087d74`, branche `chantier/restructuration-dll-r3`. Au départ, HEAD était `b20eb8ce`. `git diff --stat b20eb8ce e8087d74 -- Alundra Alundra.Tests` est vide : le commit ajouté ne touche que `docs/plan-e20-heros.md`.
- Base de lecture de `sm1-sm4/` : `9502f75` (`sm1-sm4/README.md:3`). `git log 7d776b3..9502f75 -- Alundra Alundra.Tests` est vide, donc le rapport `sm` de `7d776b3` lit le même code.
- Pour traduire une ligne, j'ai comparé le fichier à `9502f75` et au HEAD, après avoir appliqué à chaque côté la table de renommage de `tools/dll-structure/table.json`.
  - « OK » = toutes les lignes de la plage sont retrouvées identiques au renommage près.
  - Scripts de travail : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-state/sm34/` (`linemap.py`, `run_batch.py`, `diff_summary.py`, `verify_sites.py`, `dump_codes.py`).
- Écart résiduel entre `9502f75` et HEAD sur les 11 fichiers cités :
  - des lignes `using` et `namespace` (R2) ;
  - 50 lignes sorties du runner (types coupés en R2 : `EventTraceKind`, `EventTraceRecord`) ;
  - des lignes de commentaire (`Alundra.Scripts.X` devenu `Hero.TickPad`, `Session.PlayerStats`) ;
  - le commit R0 `146e1d81` : trois blocs `catch (ScriptTypeResolutionException)` dans `WorldProxy` (+18 lignes) et `nameof(...)` devenu `typeof(...).FullName!` ×3 dans `EntitySpawnFactory`.
- Aucun build, test ni lancement .NET.
- Les citations de la décompilation (`EntityEventHandlers.cs`, `PlayerManager.cs`, `GameEngine.cs`, `EntityManager.cs`) ne sont pas dans la DLL : non vérifiées.

## 1. Citations traduites

Les anciens chemins sont sous `Alundra/Scripts/`.

| Ancienne | HEAD | Statut |
|---|---|---|
| `AlundraEventProgramRunner.cs:377-419` (`InitializeEventData`, sm-rapport L89) | `Alundra/Events/EventProgramRunner.cs:327-369` (la méthode : 331-369) | OK (la plage commence 3 lignes avant la méthode, déjà à `9502f75`) |
| `…Runner.cs:2153` (`return UnknownOpcode`, L148) | `EventProgramRunner.cs:2103` (`default:` en 2102) | OK |
| `…Runner.cs:3115-3141` (`UnknownOpcode`) | `EventProgramRunner.cs:3065-3091` | OK |
| `…Runner.cs:250-254` (restauration `LastTarget*` à la reprise d'un C) | `EventProgramRunner.cs:200-204` | OK |
| `…Runner.cs:275-280` (sauvegarde `LastTarget*` au départ A/D/E/F) | `EventProgramRunner.cs:225-230` | OK |
| `…Runner.cs:760-762` (`case 0x19`) | `EventProgramRunner.cs:710-712` | OK |
| `…Runner.cs:1213-1222` (`case 0x5D`) | `EventProgramRunner.cs:1163-1172` | OK |
| `…Runner.cs:1879` (`0x2F` lit `TickPad`) | `EventProgramRunner.cs:1829` | OK |
| `…Runner.cs:2011-2013` (commentaire `0x3F`, check.md R1) | `EventProgramRunner.cs:1961-1963` | OK |
| `…Runner.cs:613-2154` (`Dispatch`, plan §1.4) | `EventProgramRunner.cs:563-2104` | OK : 158 `case`, comme à `9502f75` |
| `…Runner.cs:452-466` (boucle `0xFF` / `0x00`, plan §1.4) | `EventProgramRunner.cs:402-416` | OK |
| `…Runner.cs:478-479`, `:515` (`Parameters[1] = 0`), `:137` | `:428-429`, `:465`, `:87` | OK |
| `EventOpcodeSizeTable.cs:184, 199, 217-218, 228-230, 231, 40-246` | `Alundra/Events/EventOpcodeSizeTable.cs`, mêmes numéros | OK (simple déplacement) |
| `EventOpcodeSizeTable.cs:10` (« 197 ») | même fichier, `:11` | OK, mais la citation était déjà fausse d'une ligne à `9502f75` |
| `EntitySearchService.cs:98-262` | `Alundra/Entities/EntitySearchService.cs:99-263` | OK |
| `AlundraEntityScriptProxy.cs:351` (`XCollisionEntity`) | `Alundra/Entities/EntityScriptProxy.cs:364` | OK |
| `…Proxy.cs:325` (`ForceZ`) | `EntityScriptProxy.cs:338` | OK |
| `…Proxy.cs:1212-1213` (`LastTarget*`) | `EntityScriptProxy.cs:1225-1226` | OK |
| `…Proxy.cs:76-77` (`Hp`, `HpMax`) | `EntityScriptProxy.cs:89-90` | OK |
| `…Proxy.cs:81` (`LifePulledFromStats`) | `EntityScriptProxy.cs:94` (commentaire) ; le champ est en `:97` (ancien `:84`) | OK, citation déjà décalée de 3 lignes |
| `…Proxy.cs:88` (`PlatformEntity`) | `EntityScriptProxy.cs:101` | OK |
| `…Proxy.cs:2399`, `:2411-2418` (`MoveControllerAndPullPosition` et son écriture) | `EntityScriptProxy.cs:2412`, `:2424-2431` | OK |
| `…Proxy.cs:2596` (copie de `PlatformEntity` au clone) | `EntityScriptProxy.cs:2609` | OK |
| `AlundraWorldProxy.cs:2077` (héros `EntityRefId = -1`) | `Alundra/Maps/WorldProxy.cs:2111` (et `:3282`) | OK |
| `…WorldProxy.cs:2493` (`TickPad.Update`) | `WorldProxy.cs:2527` | OK |
| `…WorldProxy.cs:2893` (`MovePlayerOnTickPad(FromTickPad…)`) | `WorldProxy.cs:2927` | OK |
| `…WorldProxy.cs:2453-2454` (`_squareOfTick`, `_choicePadOfTick`) | `WorldProxy.cs:2487-2488` (ajouts en `:2528-2529`) | OK |
| `…WorldProxy.cs:3231` (`RecycleDestroyedEntities`) | `WorldProxy.cs:3265` | OK |
| `AlundraPlayerManager.cs:1750` (lecture de `XCollisionEntity`) | `Alundra/Hero/PlayerManager.cs:1763` | OK |
| `…PlayerManager.cs:610-631` (miroir de vie en fin de `MovePlayer`) | `PlayerManager.cs:623-644` (`EndOfMovePlayer` : 634) | OK |
| `…PlayerManager.cs:1873` (« `IncreaseHpMax` ») | `PlayerManager.cs:1886` | OK, mais la citation était fausse à `9502f75` : cette ligne est dans `IncreaseHpMaxAndFill`. `IncreaseHpMax` est en `:1893` (ancien `:1880`) |
| `…PlayerManager.cs:1893-1910` (`RestoreLifeAndMagic`) | `PlayerManager.cs:1906-1923` (méthode : 1910) | OK |
| `…PlayerManager.cs:1885` (plafond `0x32`) | `PlayerManager.cs:1898` | OK |
| `…PlayerManager.cs:1957` (`SetPlayerMp`) | `PlayerManager.cs:1970` | OK |
| `AlundraGameState.cs:143` (`TickPad`) | `Alundra/Session/GameState.cs:152` | OK |
| `AlundraTickPad.cs:31-49` (setters privés) | `Alundra/Hero/TickPad.cs:33-51` | OK |
| `AlundraCellStore.cs:13-40` (`IAlundraCellMutator`) | `Alundra/Cells/ICellMutator.cs:16-39` (interface 21-38) | OK, type sorti de son fichier par R2 |
| `AlundraMapSizeReader.cs` (sans ligne : 52×60) | `Alundra/Maps/MapSizeReader.cs:19` | OK |
| `AlundraEntitySpawnFactory.cs:493` | `Alundra/Entities/Spawning/EntitySpawnFactory.cs:497` | OK |
| `EntitySupport.cs:71-75` (`IsEligibleSubject`) | `Alundra/Physics/EntitySupport.cs:72-76` | OK |
| `AlundraHudDirector.cs:293` (`SnapDisplayedLifeAndMagic`) | `Alundra/UI/Hud/HudDirector.cs:300` | OK |
| `AlundraEventProgramRunnerTests.cs:323` (opcodes.tsv, ligne `0xB3`) | `Alundra.Tests/AlundraEventProgramRunnerTests.cs:331` | OK |
| `IntroTraceHarnessTests.cs:322` (`ImplementedOpcodes`) | `IntroTraceHarnessTests.cs:332` | OK |
| `AlundraStoryChainOpcodeAudit.cs:128-338` | `AlundraStoryChainOpcodeAudit.cs:130-340` | OK |
| `AlundraClassAndBranchOpcodesTests.cs:140-216` (précédent `0x3E`/`0x3F`) | `AlundraClassAndBranchOpcodesTests.cs:143-219` | OK |
| `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` | même chemin, en-tête seul | OK |
| `docs/plan-e20-heros.md:6287` (`PlatformEntity` : `+0x28` n'est non nul que pour un objet porté) | `docs/plan-e20-heros.md:6525` | Déplacé |
| `docs/plan-e20-heros.md:6321` (« D-G-2 ») | D-G-2 est en `:6584` au HEAD, `:6346` à `9502f75` | Faux dès `9502f75` : la ligne 6321 y est la ligne `[DLL] Rien n'existe` |

Non traités (hors périmètre sm3/sm4) : les citations de sm1/sm2 (`RidingEntity`, `ScriptedMotion.cs:191, 719-720`, `EntityFlags.cs`, etc.).

## 2. Affirmations sur la DLL : encore vraies au HEAD ?

Historique des fichiers cités entre `9502f75` et HEAD :
- 81 commits touchent `Alundra/`, `Alundra.Tests/` ou `tools/AlundraTestSaves`.
- Tous ont pour sujet `refactor(dll)`, sauf `146e1d81` (R0, 2026-10-17 09:52). Le diff de `146e1d81` est décrit dans la méthode ci-dessus.
- Dans les fichiers cités, seuls `WorldProxy` et `EntitySpawnFactory` sont touchés par R0.
- Aucun commit sur le code des handlers ou de la recherche.

| Affirmation | Vrai ? | Preuve |
|---|---|---|
| Aucun `case` pour les 21 opcodes lus ni pour `0x15`, `0x66`, `0x7A` ; ils vont au `default` puis `UnknownOpcode`. | VRAI | `rg "case 0x(15\|3C\|…\|C2)"` dans `EventProgramRunner.cs` : 0 résultat pour les 29. `default` : `:2102-2103`. |
| `UnknownOpcode` saute par la taille, laisse `Result` périmé, journalise une fois par opcode ; sans taille ou taille ≤ 0, rend 0. | VRAI | `EventProgramRunner.cs:3065-3091`, `_loggedUnknownOpcodes.Add`. |
| La table a 198 entrées ; son en-tête dit 197. | VRAI | `EventOpcodeSizeTable.cs:11` ; `grep -c "{ 0x"` donne 198. |
| Libellés faux ou vides : `0xB4` « < 0 » (228), `0xB5` « >= 0 » (229), `0xB6` « != 0 » (230), `0x97` « Spend money » (199), `0x88` vide (184), `0xA9`/`0xAA` vides (217-218). | VRAI, tous inchangés | `EventOpcodeSizeTable.cs`. Aussi : `0x96` « Restore HP » (198), `0x72` (160), `0xB3` « Set pad buttons » (227), `0x9E` « Count walkable tile » (206). Les tailles de l'étendue valent celles du binaire. |
| Aucun test ni trace n'épingle ces libellés. | VRAI | `rg` sur les textes dans `Alundra.Tests` : seul le commentaire `AlundraEventProgramRunnerTests.cs:333`. |
| `EntitySearchService` : 12 types, branche brute gardée par `referenceEntity.IsLoadedNormalOrDeactivated`, candidat non testé, héros parcouru ; types 4 et 11 testent `PlatformEntity`. | VRAI | `EntitySearchService.cs:104-117` (brute), `:157-165` (type 4), `:245-253` (type 11). Que cela égale le binaire est une INFÉRENCE : preuve par la traduction python `dll_search` (`oracle.py:154`), pas par le C# exécuté. |
| Le héros a `EntityRefId = -1`. | VRAI | `WorldProxy.cs:2111`, `:3282`. |
| `Result` est un champ de `EventProgramState` ; `Parameters` est un `int[10]` ; `InitializeEventData` n'écrit que le pc et `Parameters[0]`. | VRAI | `EventProgramState.cs` ; `EventProgramRunner.cs:331-369` ; `Parameters[1] = 0` en `:411` et `:465`. |
| `PlatformEntity` n'est jamais écrit par la DLL. | VRAI | Seule écriture : la copie du clone, `EntityScriptProxy.cs:2609`. Les tests le posent à la main (§3). |
| E20.g1a écrira `PlatformEntity` ; les types 4 et 11 changeront. | À venir | `docs/plan-e20-heros.md:6584` (D-G-2), `:6649`. |
| `XCollisionEntity` est typé proxy ; réécrit à chaque pas de `MoveControllerAndPullPosition` (null avant l'appel), jamais écrit sans contrôleur ; lu par le contact du héros. | VRAI | `EntityScriptProxy.cs:364`, `:2412-2431` (retour anticipé en `:2414-2417`) ; `PlayerManager.cs:1763`. |
| `ForceZ` est un entier signé. | VRAI | `EntityScriptProxy.cs:338`. |
| `LastTargetAnimationId` / `LastTargetDirection` existent ; restauration et sauvegarde déjà faites. | VRAI | `EntityScriptProxy.cs:1225-1226` ; `EventProgramRunner.cs:200-204`, `:225-230`. |
| `Status` est un champ simple ; `case 0x19` / `0x5D` l'écrivent ; `FlagToDestroy` est recyclé. | VRAI | `EntityScriptProxy.cs:88`, `EventProgramRunner.cs:710-712`, `:1163-1172` ; `WorldProxy.cs:3265` (recyclage fermé par `GameplayBlockedMask` : `:3267-3270`). |
| `Hp`, `HpMax`, `LifePulledFromStats`, miroir en fin de `MovePlayer`, `SetPlayerMp(GameState, short)` bornée à `[0, MpMax]`, `Mp` et `MpMax` dans `PlayerStats`. | VRAI | `EntityScriptProxy.cs:89-90, 97` ; `PlayerManager.cs:634-644`, `:1970` ; `SnapDisplayedLifeAndMagic` n'est appelé que par `RestoreLifeAndMagic` (`PlayerManager.cs:1922`). |
| Le plafond `0x32` n'existe que pour `0xBC`. | VRAI | `PlayerManager.cs:1893-1900`. La vie max du héros peut atteindre 51 par `IncreaseHpMaxAndFill` (sans plafond, `:1880-1890`) : cohérent avec « 0x96 soigne en plein ». |
| `AlundraTickPad` (maintenant `TickPad`) n'a pas de setter public. | VRAI | `TickPad.cs:33-51` : setters privés ; seuls `Update` et `Reset` sont publics ; pas d'`Overwrite`. |
| Le pavé du tick est avancé en tête de tick, avant les événements de carte ; `MovePlayer` le lit dans le même tick. | VRAI | `WorldProxy.cs:2527` ; `RunMapEventsPass` appelé en `:2568` ; `MovePlayerOnTickPad(PadState.FromTickPad(GameState.TickPad, …))` en `:2927`. |
| La passe de boîte et de choix lit un enregistrement par tick fait en tête de tick, donc avant tout `0xB3`. | VRAI | `WorldProxy.cs:2528-2529`, lecture `:2530+`. |
| `0x2F` modes 0 et 1 lisent `TickPad`. | VRAI | `EventProgramRunner.cs:1829-1840`. |
| Les programmes B s'exécutent avec le héros comme entité de référence. | VRAI | `WorldProxy.cs:3209` : `runner.RunScript(player, ScriptHelper.ProgramBMap)`. |
| `ICellMutator` n'a que des écritures ; le rapport dit « il manque un accesseur en lecture ». | VRAI pour l'interface | `ICellMutator.cs:21-38` (3 écritures). Mais `CellStore.GetWalkability(x, y)` est public (`CellStore.cs:136`, déjà présent à `9502f75`) et `IEntityWorldContext.CellMutator` vaut `null` par défaut (`IEntityWorldContext.cs:116`). Aucun faux `ICellMutator` n'existe dans `Alundra.Tests`. |
| `IsEligibleSubject` est le prédicat de `0x3C` (sm1, repris par le type 4). | VRAI | `Physics/EntitySupport.cs:72-76`. |
| Sites : 42 en 11 cartes pour sm3 ; 90 en 50 cartes pour sm4. | VRAI | `skipped-sites.tsv` recomptée. Chaque opcode lu aux `pc` cités dans `alundra-project/Maps/**/events/*.events.json` du HEAD : 0 écart sur les 132 lignes. |
| Le site `161 B[1] @54` est `B3 00 10 00 00 00 00 00 00` dans la boucle `3B`, `03`, `B3`, `Break`, `Goto -20`. | VRAI | Octets décodés aux `pc` 44, 51, 54, 63, 64 : `3B 06 0D 1D 1D 03 03`, `03 10 00`, `B3 …`, `00`, `02 EC FF`. |
| `0x88` « 0 site » (plan, Q-sm-6). | FAUX | 3 sites : 306 `E[14] @1110`, 306 `E[15] @1122`, 442 `E[1] @1312` (`skipped-sites.tsv`, vérifiés dans les données). |
| `0x15 0x66 0x7A` : 0 site. | VRAI | `skipped-sites.tsv` : aucune ligne. |

## 3. Tests qui épinglent les opcodes ou les sites

| Test | Fichier:ligne | Ce qu'il épingle | Effet d'une tranche |
|---|---|---|---|
| `UnknownOpcode_KnownSize_SkipsBySize` | `Alundra.Tests/AlundraEventProgramRunnerTests.cs:331` | `[0xB3, 0×8, 0x1A, 4, 0xFF]` : `0xB3` sauté par sa taille 9, puis `TargetAnimationId = 4` (`:335-342`). Le commentaire `:333-334` liste les témoins successifs `0x93`, `0x4C`, `0x08`, `0xB3`. | sm4 le fait rougir. Il faut un nouveau témoin encore sauté : `0x56` (taille 2, ligne 132), `0xB9` (2, ligne 233), `0xBB` (1), `0xC2` (2). `0x15`, `0x66`, `0x7A` ne conviennent que si sm4 ne les porte pas. **Absent de la proposition.** |
| `UnknownOpcode_0x5F_SkipsEightBytes_AsTheBinary`, `UnknownOpcode_NoKnownSize_TerminatesScriptCall` | `AlundraEventProgramRunnerTests.cs:346`, `:361` | `0x5F`, `0xC5` | Aucun effet. |
| `Oracle_AnOpcodeTheRunnerHandlesIsPorted_…` | `Alundra.Tests/AlundraStoryChainSkippedOpcodesTests.cs:103-110` | `IsPorted(0x05/0x00/0xFF)` ; il existe au moins un opcode de taille > 0 non porté. | Reste vrai. |
| Oracle `IsPorted` | `AlundraStoryChainOpcodeAudit.cs:75` | Il lance `[op, 0…, 0xFF]` sur le vrai runner (`ProbeOpcode`) : porter un opcode le fait passer à « porté » tout seul. | Aucun changement de code. |
| Liste fermée de la chaîne | `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` (en-tête seul) | Aucun site de la chaîne. | **Aucune ligne à retirer** pour sm3 ni sm4 : aucun des 29 opcodes n'est sur les 30 cartes de la chaîne. Le « −42 » et le « −90 » des tâches T4 n'ont pas de liste à décrémenter (voir G2). |
| `PredicateOpcodes` | `AlundraStoryChainOpcodeAudit.cs:57` | `{0x52, 0x58, 0x82, 0x83, 0x84, 0x87, 0x95, 0x99, 0x9A, 0x9F}` (règle 4). | Précédent E19.l2 : on y ajoutait les prédicats portés (`0x83`, `0x9F`). Non traité par sm3 pour `A9 AA B2 B4-B7 9E`. |
| `ImplementedOpcodes` | `IntroTraceHarnessTests.cs:332` | Miroir écrit à la main ; sert aux étiquettes de trace (`:1788-1791`). | Le patron E19.l y ajoute les opcodes portés (L1-R8, L2-R5). Absent des tâches T2 de sm3 et sm4. |
| Tests de `TickPad` | `AlundraTickPadTests.cs:15` | `Update`, `Reset`, constantes. | `Overwrite` s'y ajoute sans casser l'existant. |
| Recherche d'entités | `EntitySearchServiceTests.cs:29-266` | Types 0 à 11 (dont 4 en `:123`, 7 en `:173`, 8 en `:207`, 11 en `:243`), id illégal, liste vide. | Réutilisables par A9, AA, B2 ; rien à changer. |
| Recyclage des morts | `AlundraDestroyedEntityRecyclingTests.cs:139-250` | Un `FlagToDestroy` est vu avant le recyclage, pas après ; porte du gel. | Réutilisable par `0x88`. |
| Vie du héros | `AlundraHeroLifeTests.cs` (utilise `LifePulledFromStats`) | Miroir vie / statistiques. | Réutilisable par `0x96`. |
| Tests « séquence réelle » qui chargent une carte de sm4 | `AlundraEventProgramRunnerTests.cs:4969` (Cave-140) et `:5037` (Lizardman's Lair (Boss)-411) | Ils exécutent B[4] / B[9] de ces cartes. | Elles contiennent des sites de sm4 (`0x72` en 140 `D[2] @2037`, `0x96` en 411 `B[4] @1056`). Les tests s'arrêtent vers `@1361` (Cave) et `@1747` (411), sans atteindre ces sites (INFÉRENCE : lecture des assertions, non exécuté). |

Aucun autre test ne contient un octet de programme avec ces opcodes (`rg "\b0x(B3|97|96|88|72|A9|AA|B2|B4|B5|B6|B7|9E|15|66|7A)\b"` dans `Alundra.Tests` : seuls des faux positifs, hors `:333-335`). Aucun test ne charge une des cartes de sm3 ni les cartes d'arcs de sm4 : 161, 306, 394, 40, 442, 444, 452 à 470, 475.

Types de tests : R3 et R4 n'ont renommé aucun type déclaré dans `Alundra.Tests`. Les déclarations passent de 635 à 638 : seulement `AlundraScriptAssemblyRegistration`, `AlundraScriptNamesInTheDataTests` et `SimpleNameEntityLoader`, ajoutés par R0. Les fichiers de test n'ont pas bougé (R5 attend).

## 4. Banc et arcs

**Arcs du plan §1.3 (`docs/plan-e19-opcodes.md:12001-12026`).** A0 à A9 sont des cartes de la chaîne (389, 390, 392, 391, 416, 163, 172, 476, 478). Aucun ne passe par un site de sm3 ou sm4.

**Harnais d'arc (`Alundra.Tests`).**
- `AlundraArcSupport.cs` :
  - `ArcSpec(Name, Zone, WorldName, Flags[], HeroTileX/Y/Z, FrameLimit, RealController, Prefabs, Arrival)` : `:62-69`
  - `ArcRun` : `:94` ; constructeur `:175`
  - `Give` `:151`, `Equip` `:161`, `SetMp(max, mp)` `:164`, `SetItem` `:171`
  - `OnInstruction` `:136`, `OnFrame` `:140`, `Entities` `:387`, `EntityByRecord` `:390`
  - `FirstIndexOf` `:393`, `Has` `:395`, `HasAfter` `:398`, `SkippedOrExceeded` `:414`
  - `OneFrame` `:430`, `Press` `:454`, `HoldDirections` `:463`, `PlaceHero` `:470`, `RunUntil` `:483`, `StuckMessage` `:497`, `CloseDialogueWithTheButton` `:510`
  - `ArcPrefabAssets` : `:675`
- `AlundraArcChecks.cs:14` et `AlundraArcSamples.cs:23`. Convention : une exportation absente fait échouer le test, jamais le saute (`AlundraArcSupport.cs:55-56`).
- `World` des arcs : dossier `Maps/<Zone>/<WorldName>`. Pour les cartes des sites, d'après `alundra-project/Maps/world-index.json` :

| Carte | Zone | Nom du monde |
|---|---|---|
| 440 | `Meia Dream` | `Meia Dream-440` |
| 424 | `Nirude` | `Nirude (inner)-424` |
| 421 | `Nirude` | `Nirude (inner)-421` |
| 54 | `Elenes Nightmare` | `Elenes Nightmare-54` |
| 160 | `Fairy cave` | `Fairy cave (underwater)-160` |
| 258 | `Inoa` | `Inoa (inner)-258` |
| 298 | `Inoa` | `Inoa (inner, destroyed)-298` |
| 161 | `Fairy cave` | `Fairy cave (Stenia)-161` |
| 452 | `Save Room` | `Save Room (Coal Mine)-452` |
| 394 | `Pub Entrance + Floor` | `Pub Entrance + Floor-394` |
| 475 | `Inoa` | `Inoa (inner, Yustel, Map)-475` |
| 40 | `Nirude’s Lair` (apostrophe typographique) | `Nirude’s Lair-40` |
| 140 | `Cave` | `Cave-140` |
| 444 | `Torla` | `Torla (inner)-444` |
| 306 | `Meia Dream` | `Meia Dream-306` |
| 442 | `Meia Dream` | `Meia Dream-442` |

- Précédent d'un arc par site du corpus : `AlundraHeightTargetSitesTests.cs` (classe `:21`, record `Site` `:23`, `AllSites()` `:32`, `ArcSpec` `:88`, vecteurs `Frames`, `FirstFrame`, `EndFrame`, `Calls`, `EndPosZ`). C'est le modèle de table à reprendre pour T3.

**Exécution d'un script sur une carte réelle, sans monde.**
- `MapEventProgramLoader.Load(projectRoot, "<WorldName>")` (`Alundra/Events/MapEventProgramLoader.cs:37`), puis `new EventProgramRunner(document, gameState, fakeContext)` et `RunOneScriptCall` (exemples `AlundraEventProgramRunnerTests.cs:4969`, `:5037`).
- Racine : `FindProjectRootForRealOpcodeOccurrences()` (`:3852`), qui se saute si `alundra-project/` manque. La convention diffère de celle d'`ArcRun` (échec).
- `FakeEntityWorldContext` : `AlundraEventProgramRunnerTests.cs:46`, `CellMutator` posable en `:83`. `AlundraClassAndBranchOpcodesTests` a un `Context` privé (`:23-34`) et un `Run(...)` (`:55`) : le patron des tests synthétiques de L1-1.
- Statique : `AlundraStoryChainOpcodeAudit.LoadMap(projectRoot, map)` (`:343`), `ReadRoots` (`:294`), `WalkMap` (`:130`).

**Oracle de valeurs de la proposition.**
- Il est dans `docs/plan-e19-sm-annexe/sm1-sm4/work/oracle/` (`oracle.py` 649 lignes, `emu.py`, `lib.py`).
- Il lit `ALUN_CD.EXE` à un chemin absolu hors dépôt (`oracle/lib.py:3`).
- Son `main()` (`oracle.py:589-649`) ne fait que comparer ses modèles python au binaire sur des mondes aléatoires. Il n'exporte aucun vecteur.

## 5. Noms nouveaux (règles de l'ADR-0075)

La proposition ne crée aucun type DLL ni aucun fichier DLL :
- 13 `case` dans `Alundra.Events.EventProgramRunner` (`Alundra/Events/EventProgramRunner.cs`) : 8 pour sm3 et 5 pour sm4.
- Libellés dans `Alundra.Events.EventOpcodeSizeTable`.
- La couture `AlundraTickPad.Overwrite(hold, pressed, released, interval)` s'écrit au HEAD `Alundra.Hero.TickPad.Overwrite(...)`, dans `Alundra/Hero/TickPad.cs`. Les propriétés cibles sont `ButtonsHold`, `ButtonsJustPressed`, `ButtonsReleased`, `ButtonsJustPressedByInterval` (`uint`).
- L'accesseur de lecture de case de `0x9E` n'est pas nommé. Au HEAD : membre de `Alundra.Cells.ICellMutator` (`Alundra/Cells/ICellMutator.cs`), qui serait implémenté par `Alundra.Cells.CellStore` (existe déjà : `GetWalkability`, `CellStore.cs:136`).
- Type d'aide purement fonctionnel : le précédent est `Alundra.Combat.HitOpcodes` (`Alundra/Combat/HitOpcodes.cs`). Il faudrait alors un nom sans préfixe, dans l'un des dossiers de domaine, un type principal par fichier.

Les tâches T0 de sm3 et sm4 placent leurs vecteurs dans `docs/plan-e19-sm-annexe/sm3/` et `sm4/` : aucun de ces dossiers n'existe. L'annexe est décrite « figée » (`README.md:3-6` de l'annexe).

La tâche T4 de sm4 prévoit un ADR pour `0x97` = `IncreaseMp` (Q-sm-1). Prochain numéro libre du dépôt : 0076, `docs/decisions` s'arrêtant à 0075. Risque de collision avec les ADR que les tranches E20 annoncent sans numéro (le plan E20 planifie `PlayerManager.DecreaseMp` en E20.h4 : `docs/plan-e20-heros.md:8141, 8165`).

Aucun test n'est nommé. Convention en vigueur avant R5 :
- fichier plat `Alundra.Tests/Alundra<Sujet>OpcodesTests.cs`, `public sealed class`, namespace `Alundra.Tests`, comme E19.l ;
- R5 (règle D-R « classes de test sans préfixe », `docs/plan-restructuration-dll.md:861+`) retirera le préfixe.
- Le nom final (sans préfixe seul, ou suivi du nom du type) dépend de Q-R-3 point 2 : ouverte.

**Garde des fichiers ajoutés (Q-R-7).**
- `check_names.py --tree --stage final` classe `[hors table]` tout `.cs` de `Alundra/` absent de `table.json` (`tools/dll-structure/check_names.py:96-98`).
- Il ne regarde pas `Alundra.Tests`. Q-R-7 (mode `--rules`, tranche R7) est encore une « proposition » ouverte.
- sm3 et sm4, tels que proposés, ne déclenchent pas cette garde (aucun fichier DLL ajouté).
- Elle se déclencherait dès qu'une tâche T2 sort un helper dans un nouveau fichier.

## 6. Manques bloquants

**Pour les deux tranches.**
- **G1. Calendrier.** Q-E19-SM1 (`docs/plan-e19-opcodes.md:12474`) n'a pas de réponse : après ou avant la restructuration ?
  - D-R-8 (ADR-0075) interdit toute tranche E19 en parallèle tant que le chantier court.
  - R5 attend Q-R-3 ; Q-R-5, Q-R-6 et Q-R-7 sont ouvertes.
  - HEAD est la branche du chantier : la tranche ne peut pas s'y faire.
- **G2. Prérequis `sm0` inexistant.** Aucun filet sur le corpus entier (510 sites).
  - `Alundra.Tests/Data/` ne contient que la liste de la chaîne, en-tête seul.
  - « une ligne de moins dans la liste fermée » (patron E19.l) n'a donc pas de liste à décrémenter pour ces opcodes.
  - Le format de la liste de `sm0` (colonne propriétaire, clé `(carte, créneau, programme, pc)`) n'est pas écrit.
  - Sans `sm0`, le patron E19.l exige de mettre à jour `ImplementedOpcodes` (`IntroTraceHarnessTests.cs:332`) dans chaque tranche ; la proposition ne le dit pas.
- **G3. Vecteurs T0 sans forme.**
  - Aucun exporteur n'existe : `oracle.py` compare des modèles, il ne produit pas de monde, d'octets, de `v0` ni de mots écrits.
  - L'oracle dépend d'un binaire hors dépôt (`D:\development\repo\Alundra Remake\…\ALUN_CD.EXE`).
  - Aucun consommateur C# n'est défini : où les tests lisent-ils les vecteurs ? le patron E19.l écrit les valeurs dans le code de test.
  - Ni la correspondance monde du binaire vers `EntityScriptProxy` (offsets `+0x28`, `+0x130`, `+0xB8`, `+0x88` vers champs) ni le dossier `docs/plan-e19-sm-annexe/smN/` (annexe figée, avec ses `SHA1SUMS.txt`) ne sont tranchés.
- **G4. T1 sans valeurs, sans noms, sans mutants.**
  - Le patron E19.l écrit les valeurs d'avance (`Result` de départ 7, drapeaux avant / après, `CodeIndex`).
  - Sm3 et sm4 ne donnent que des intitulés (« proxy nul, groupe vide, héros », « bornes incluses », « plafond `HpMax` »).
  - Les mutants ne sont listés que pour sm1 (rapport L408).
  - La liste fermée du « code de test existant touché » (clause d'acceptation de L1/L2) manque : y figurent au moins `AlundraEventProgramRunnerTests.cs:331`, `AlundraStoryChainOpcodeAudit.cs:57`, `IntroTraceHarnessTests.cs:332`.
- **G5. T3 sans arcs.** Seulement des numéros de carte et de pc.
  - Il manque pour chaque arc : zone et monde (§4 ci-dessus), drapeaux, tuile du héros ou arrivée, signal de fin prouvé par la trace, limite d'images, valeurs attendues.
  - Aucun test existant ne charge ces cartes.
  - Certains sites dépendent d'une entrée de l'arc, jamais écrite : `440 B[9] @628` « après un `2F` Carré » (`Press`) ; les programmes D, F et E (`40 F[2] @1153`, `140 D[2] @2037`, `444 D[15] @1302`, `306 E[14] @1110`, `442 E[1] @1312`) demandent un contact ou une interaction.
- **G6. Libellés.**
  - Les textes sont absents pour `0x97`, `0xA9`, `0xAA`, `0x88` (et pour `0x96`, `0xB2`, `0xB3`, `0x9E` si on les juge faux).
  - Sm3 T2 liste le libellé de `88` alors que `0x88` est porté en sm4 : double propriétaire.
  - Q-sm-7 du plan donne les libellés « à la tranche qui porte l'opcode ».
- **G7. Numérotation des questions.**
  - Le plan (§3, `:12475-12482`) a Q-sm-1 à Q-sm-8 ; le §1.4 annonce « Q-sm-1 à Q-sm-9 ». Le rapport (§6) en a 9.
  - Correspondance rapport vers plan : Q-sm-6 et Q-sm-7 du rapport (`0x9E` et `0x88`) fusionnent en Q-sm-6 du plan ; Q-sm-8 devient Q-sm-7 ; Q-sm-9 devient Q-sm-8.
  - Q-sm-6 du plan dit « `0x88` (0 site) », faux : 3 sites.
- **G8. `0x15`, `0x66`, `0x7A`.**
  - Le plan §1.4 les range dans sm4 ; le rapport sm1-sm4 n'en lit aucun, et ses tâches ne les citent pas.
  - Il n'existe ni lecture au binaire (seule une heuristique de taille, `size-check.tsv`), ni oracle, ni test, et aucune question « porter sans site ? » les concernant (Q-sm-6 ne vise que `0x9E` et `0x88`).
  - Les porter retire des témoins possibles du test `…SkipsBySize`.
- **G9. Corrections de check.md non reprises dans §7 (rapport figé).**
  - O1 (validateur d'identifiant brut fatal : test « id brut du corpus ∈ enregistrements de la carte », site `A9 18` de 440) ;
  - O2 (le masque de `0x9E` est `0x0002`, pas `0x0004`) ;
  - O3 (porte de gel `[0x800DC4B8] & 0x48` qui court-circuite carte et entités, pertinente pour `0xB3`) ;
  - R1 (« les boucles de la décompilation perdent le dernier slot » est réfuté ; ne pas le recopier) ;
  - T1 de sm3 « proxy nul » : le champ `XCollisionEntity` est de type proxy, le rapport écarte lui-même le piège `null == null` pour A9 / AA (§4.3) ; l'intitulé est ambigu.

**Spécifiques à sm3.**
- **S3-1. `0xB2` (Q-sm-4 ouverte).**
  - Les 10 sites sont tous `B2 <t> 8B`, soit type 11 en second.
  - Types 4 et 11 sont disjoints par construction (`EntitySearchService.cs:157-163` : `PlatformEntity == null` ; `:249` : `!= null`). Donc `B2 04 8B` vaut toujours 0, même après E20.g1a (INFÉRENCE de lecture).
  - Les autres (`03`, `05`, `06`, `08`) vaudront 0 jusqu'à g1a. T3 épingle ce 0 « à écrire dans le test » : g1a le cassera. Il manque un propriétaire pour cette ré-épingle.
- **S3-2. `0x9E` (Q-sm-6 ouverte), 0 site.**
  - Code sans site : à décider par l'auteur.
  - Accès hors grille non tranché (binaire sans borne ; en DLL `CellStore` indexe `_walkability[y*_width+x]`).
  - Le nom et la forme du nouvel accesseur manquent.
- **S3-3. T0 « vérifier en monde réel que `XCollisionEntity` et `ForceZ` du héros sont ceux du binaire au moment du script ».** Rien ne mesure le binaire en jeu : l'oracle n'exécute que des gestionnaires isolés. Non exécutable tel qu'écrit.
- **S3-4. B4 à B6 prennent le premier résultat.** Cela suppose que `SpawnedEntities` suit l'ordre des slots (ADR-0064, titre). Aucun test ne le prouve, et les sites réels (`B5 81`, `B5 80`, `B6 80`) n'ont qu'un résultat (types 0 et 1).
- **S3-5. Règle 4 de l'audit.** `PredicateOpcodes` (`AlundraStoryChainOpcodeAudit.cs:57`) n'est pas étendu aux prédicats portés. Précédent L2-2, non traité.

**Spécifiques à sm4.**
- **S4-1. `0xB3` (Q-sm-3, propriétaire « sm4 T0 »).**
  - La question de conception reste ouverte : `TickPad.Overwrite` suffit-il, ou faut-il réécrire `_squareOfTick` et `_choicePadOfTick` (`WorldProxy.cs:2528-2529`, enregistrés avant les événements de carte) ?
  - T1 ne donne pas les mots attendus ni le tick.
  - Le « test de sonde de l'ordre pavé / tick » (T0) n'a pas de spécification.
- **S4-2. Témoin `…SkipsBySize` (§3).** Absent des tâches.
- **S4-3. `0x96`.**
  - « pas d'appel Snap » : l'observable existe (`HudDirector.Instance.Hp/HpMax/Mp/MpMax` ; `SnapDisplayedLifeAndMagic` en `HudDirector.cs:300`) mais la tâche ne dit pas quelles valeurs comparer.
  - Q-sm-2 reste à confirmer (D-E19-2, `docs/plan-e19-opcodes.md:50-51`, tranche déjà : le binaire l'emporte).
- **S4-4. `0x97` (Q-sm-1).** Un ADR est demandé ; D-E19-2 couvre déjà la décision. Le numéro d'ADR à réserver est inconnu (§5).
- **S4-5. `0x88`.**
  - Indice ≥ 3 (table de trois mots `{2, 3, 4}` au binaire, octets sans rapport au-delà) : non tranché.
  - La table `0x80023D2C` n'a pas d'équivalent nommé en DLL.
  - Le libellé est réclamé par sm3 (G6).
- **S4-6. `0x72`.** T3 (« reprise d'un C avec l'animation validée ») ne dit pas comment un programme D / F interrompt un C dans l'arc (un seul programme par appel dans `ArcRun`) ; aucune valeur attendue.
- **S4-7. Couplage avec E20.** Le plan E20 prévoit de toucher `PlayerManager.cs` (`DecreaseMp`, `docs/plan-e20-heros.md:8141, 8165`), le fichier où vivrait l'`IncreaseMp` de `0x97` : conflit de fusion possible, non nommé.

## 7. Inconnus

- Pas d'exécution : je n'ai pas lancé `dotnet`, donc rien de plus que de la lecture de code. Les « OK » de lignes sont des comparaisons de texte. Que les 81 commits `refactor(dll)` soient neutres sur le comportement se lit ici dans les lignes citées, pas dans une exécution de la suite.
- `EntitySearchService.cs` égale-t-il le binaire ? La preuve reste la traduction python du rapport sm, jamais le C# exécuté.
- Les cartes 55 à 57 et la question Q-sm-8 : hors périmètre sm3 / sm4.
- Les conditions pour atteindre chaque site en arc (drapeaux, tuile, entité active, déclencheur) sont inconnues.
- Dates de Q-R-3, Q-R-5, Q-R-6, Q-R-7 : ouvertes au HEAD.
- Les citations de la décompilation (`EntityEventHandlers.cs:2879-2885`, `:1243-1271`, `EntityManager.cs:969`, `GameEngine.cs:1943`) n'ont pas été relues : sous-module analyseur, hors périmètre.
- Le fichier `…/sm-state/sm34/state.md` n'a pas été écrit : mes consignes interdisent les fichiers de rapport. Ce message fait foi.