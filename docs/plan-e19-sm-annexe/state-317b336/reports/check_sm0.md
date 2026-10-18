# Contre-vérification de « sm0 : état au HEAD »

`check.md` n'a pas été écrit : la consigne de session interdit les fichiers de rapport, donc ce rapport est ce message. Mes scripts (lecture seule) sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-state/sm0/verify/` : `pair.py`, `offsets.py`, `mirror.py`, `testtypes.py`, `skipped_check.py`, `map60_check.py`, `tsv_cites.py`, `volume.py`, `census_mine.py`, `census_cs.py`.

## 0. Verdict

- **Table des citations : juste sur le fond.** J'ai rouvert chaque ligne ancienne (`9502f75`) contre HEAD. Le code est le même au renommage près. Les erreurs sont mineures : bornes hors fichier, une attribution de commit, des comptes.
- **Verdicts VRAI/FAUX : tous tiennent**, avec quelques nuances (§2).
- **Liste des manques : juste mais incomplète.** J'ajoute huit manques (B10 à B17).
- **sm0 comme tranche : REVISE.** Elle n'est pas exécutable en l'état.

Méthode du recomptage : copie de `census.py` aux chemins HEAD, avec mon propre contrôle de sortie.
- 483 cartes, 110 140 instructions, 171 opcodes distincts.
- 510 sites, 96 cartes. Le multiensemble des clés est égal à `skipped-sites.tsv` (`skipped_check.py` : `equal multisets: True`).
- Variante sans racines B d'entité (les racines du C#) : `census-sites.tsv` identique à l'octet. Ma sortie est identique à celle de l'agent, et `census-op0x40.tsv` est identique à l'annexe.
- Sommes SHA-1 : 43/43 et 95/95 OK.

## 1. Erreurs, omissions, corrections

1. **Base fausse.** HEAD est `317b3360`, pas `b20eb8ce`. Sans effet : `git diff b20eb8ce HEAD` ne touche que `docs/plan-e20-heros.md`, à partir de la ligne 8071. `:6525` et `:6559` restent exacts.
2. **Ligne 14 (plafond de 63 entités).**
   - `EntitySlotTable.cs:14-60` dépasse le fichier, qui a 56 lignes : la classe est en `:14-56`.
   - Le refus n'est pas dans `03c38630` (phase A, « nothing reordered, nothing refused ») mais dans `5e369030` (« refuses the 64th »).
   - Les deux sont ancêtres de `7d776b3` : l'affirmation de l'annexe était **déjà fausse le jour où elle a été écrite**, la restructuration n'y est pour rien.
3. `ICellMutator.cs:16-39` et `:21-39` : le fichier a 38 lignes. La doc est en `:14-20`, l'interface en `:21-38`.
4. `EventOpcodeSizeTable.cs` : le diff change le namespace et **deux** `cref`, pas un.
5. **Sites de `0x88`** : `306 E[14] @1110`, `306 E[15] @1122`, `442 E[1] @1312` (`skipped-sites.tsv`). Pas « 306 `E[14]` ×2 ».
6. **§3.5 (arcs gardés).**
   - `AssertNothingSkippedOrExceeded` est appelé par **12** fichiers de tests, et défini en `AlundraArcChecks.cs:59`.
   - Toute la famille de gardes (`AssertNothingSkippedOrExceeded`, `AssertSkippedWithin`, `SkippedOrExceeded`, règle 5) apparaît dans **18 fichiers de tests + 2 de support** (`AlundraArcChecks.cs`, `AlundraArcSupport.cs`), pas « 18 + 4 ».
7. **§3.2** : `AssertSkippedAreListed` est appelée **5 fois**, pas 2. Ce sont `AlundraDay3SceneArcTests.cs:87`, `:132`, `:185`, `:303`, `:408` (cartes 176, 179, 176, 135, 178).
8. **§3.4** : 14 occurrences de `EventOpcodeSizeTable.Entries` dans `Alundra.Tests`, pas 17 (18 mentions du type en tout). Toutes en lecture : la conclusion tient.
9. **Tableau §3, ligne « sm5/sm7/sm8 » : faux.**
   - `0x5F` n'a **aucune tranche** dans sm0 à sm10. C'est une attente de taille 8 à 0 site. 28 des 29 opcodes sautés ont une tranche (`skipped_check.py` : `skipped opcodes without slice: 5F`).
   - `Oracle_…` (`AlundraStoryChainSkippedOpcodesTests.cs:110`) reste donc vert tant que `0x5F` est sauté, et aussi si Q-sm-6 garde `0x9E` ou `0x88` sautés.
10. `:~2011` (sm1-sm4 §7) désignait le **début** du `case 0x3F`, soit HEAD `:1961`. Le point d'insertion « après `0x3F` » est HEAD `:1986`, `0x42` commence en `:1987`. La lecture de l'agent est juste.
11. **Ligne 17** : d'autres tests écrivent `PlatformEntity` : `EntitySearchServiceTests.cs:138` et `:247`.
12. **« Le parcours C# n'a jamais tourné sur les 483 cartes » : à nuancer.**
    - `AlundraStoryChainOpcodeAudit.LoadMap` tourne déjà sur les 483 cartes dans M07 : `AlundraMissingAnimationWaitCorpusTests.cs:8` (`using static`), `:295` (`LoadMap(root, map)`), `:396-400` (boucle sur `world-index.json`).
    - Le chargeur et l'hypothèse `.Single()` du `*.tileMap` sont donc déjà exercés. Seuls `WalkMap` et `ReadRoots` sur 483 cartes sont nouveaux.
    - La durée de M07 n'est consignée nulle part : INCONNU.
13. **Volume lu** : 207,1 Mo en 10^6 octets (= 197,5 Mio), max 0,64 Mo, événements 3,7 Mo. Seule l'unité diffère.
14. **Garde Q-R-7** : `check_names.py --tree` lit aussi les types imbriqués des tests (`test_warnings`, `:49-58`, appelée `:77` et `:123`). Ce ne sont que des avertissements : seuls les fichiers de la DLL donnent `[hors table]`.
15. **« Les Q-sm ne bloquent pas sm0 » : faux pour la colonne propriétaire.**
    - Q-sm-4 décide du propriétaire de 10 lignes (`0xB2` : sm3 ou E20.g1a).
    - Q-sm-6, tel qu'écrit, peut laisser sautées les 3 lignes de `0x88`.
    - O-E19-36 (l'auteur) porte 42 lignes (`0xB9`).
16. **B7, table globale.**
    - FAIT : la table C globale n'a qu'une entrée (`data-extracted/data/map_alundra.json` : `EventCodesCSize` 2, `EventCodesCTable` `[0]`).
    - Les index 1, 4 et 10 tombent au-delà de cette entrée. Ce que lit le binaire est INCONNU : même « programme vide » n'est pas établi.
17. **B2 : deux tests rougissent, pas un.** Si les 510 lignes entrent dans le fichier de la chaîne, la règle 2 rougit aussi. `Rule2StaleLines(TheList(), CorpusSkipped())` (`AlundraStoryChainSkippedOpcodesTests.cs:50-55`) ne parcourt que `ChainMaps` (`:21-29`) : les 510 lignes hors chaîne y seraient toutes « périmées ».
18. **B6 : preuve renforcée, toujours INFÉRENCE.**
    - `0xBB` écrit `+0x8` (la marque d'attente) et `+0xC`, comme `0x1C`, `0x20` et `0x37` (`result-check.tsv`, ligne BB).
    - Ses constantes `v0` sont 1 et 477 (`size-check.tsv`).
19. **`opcodes.tsv`** contient **11** citations de tests distinctes, pas 9 : les 4 de `AlundraEventProgramRunnerTests.cs` (`:323`, `:338`, `:353`, `:404-433`), plus `…ProductionTests.cs:17-31` et `…YarnVariableStorageTests.cs:120`. Toutes sont bien traduites (`tsv_cites.py`).
    - Le runner : 256/256 à −50, dont 158 `case`, 96 `UnknownOpcode` et 2 dans la boucle d'appel.
20. **Décalage de `WorldProxy` non uniforme** : +28 jusqu'à l'ancienne ligne 1092, +34 jusqu'à 3408, +40 jusqu'à 3538, +46 après (`offsets.py`). Les numéros donnés restent exacts.
21. **Deux citations sans ligne à `9502f75`.**
    - `:320` vient de l'ancien état du plan (`:578`).
    - `:369` vient de `plan-restructuration-dll.md:923`, déjà à HEAD. À `9502f75`, cette ligne était `:367`.

Vérifié sans écart : les types de test. Aucun n'a été renommé par R3 ou R4 (`testtypes.py` : rien de disparu ; nouveaux : `AlundraScriptAssemblyRegistration`, `AlundraScriptNamesInTheDataTests`, `SimpleNameEntityLoader`). Seules les références aux types de la DLL ont changé.

## 2. Table corrigée des citations (HEAD `317b3360`)

### 2.1 Périmètre sm0

Toutes les lignes `=` sont confirmées par `pair.py` (contenu identique au préfixe `Alundra` près).

| Ancienne (`9502f75` = `7d776b3`) | HEAD | Statut |
|---|---|---|
| Runner `:613-2154` (`Dispatch`) | `Alundra/Events/EventProgramRunner.cs:563-2104` (méthode `:563-2105`, 158 `case`) | −50 |
| `0x00` / `0xFF` `:459-466` / `:452-457` | `:409-416` / `:402-407` | −50 |
| `default` `:2152`, `return UnknownOpcode` `:2153` | `:2102`, `:2103` | −50 |
| `UnknownOpcode` `:3115-3141` | `:3065-3091` | −50 |
| `:478-479` | `:428-429` | −50 |
| `EventOpcodeSizeTable.cs:40-246`, commentaire `:10`/`:11` | `Alundra/Events/EventOpcodeSizeTable.cs:40-246`, `:11` (« 197 ») | = (namespace et 2 `cref` changent) |
| Libellés `:142-143`, `:152-155`, `:184`, `:199`, `:217-218`, `:228-230`, `:231` | mêmes lignes | = |
| `0x01` `:617-618` ; `0x29` `:736` | `:567-568` ; `:686` | −50 |
| Repli `map_alundra` `:389-396`, `:394` | `:339-346`, `:344` | −50 |
| `0x40` `:886-899`, `:887-890` | `:836-849`, `:837-840` | −50 |
| `RunScript :213-296`, `InitializeEventData :385-419` (`:377-419`), `RunOneScriptCall :428-518`, `FillDataFromCommand :564-588`, `RunSpriteEvent :314-374`, `:378` | `:163-246`, `:335-369` (`:327-369`), `:378-468`, `:514-538`, `:264-324`, `:328` | −50 |
| `:137`, `:436-446`, `:187`, `:118`, `:233-259`, `:473`, `:495-507`, `:515`, `:302`, `:217-228` | `:87`, `:386-396`, `:137`, `:68`, `:183-209`, `:423`, `:445-457`, `:465`, `:252`, `:167-178` | −50 |
| `0x42`/`0x43` `:2037-2074` ; `OpenDialog :2168-2204` | `:1987-2024` ; `:2118-2154` | −50 |
| `0xA4 :1611-1624` ; `0xAC :1305`, `:2646-2654` ; `0x82 :1560`, `:3150-3189`, `:3172` ; `0x53 :1928-1965` ; `0x2F :1864-1896` ; `0xA8`/`0xBA :1475-1486` | `:1561-1574` ; `:1255`, `:2596-2604` ; `:1510`, `:3100-3139`, `:3122` ; `:1878-1915` ; `:1814-1846` ; `:1425-1436` | −50 |
| `Dispatch :849-875` ; `AlundraHitOpcodes.cs` | `:799-825` (appels `:806`, `:821`) ; `Alundra/Combat/HitOpcodes.cs` (81 l.) | −50, type renommé |
| `EventTraceKind` `:44-62` | `Events/EventTraceKind.cs:47-65` (+ `Events/EventTraceRecord.cs`) | scindé |
| `EventProgramRunner.cs` (45 l.) ; `EventProgramState.cs` (57 l.) | `Events/IEventProgramRunner.cs` (48 l., interface `:14`, NoOp `:34`) ; `Events/EventProgramState.cs` (57 l.) | renommé / = |
| `AlundraWorldProxy.cs:299`, `:874-876` | `Maps/WorldProxy.cs:327`, `:902-904` | +28 |
| `:3128`, `:3149`, `:3193`, `:2634` | `:3162`, `:3183`, `:3227`, `:2668` | +34 |
| `AlundraEntityScriptProxy.cs:1663`, `:1761-1778` | `Entities/EntityScriptProxy.cs:1676`, `:1774-1791` | +13 |
| `AlundraNativeSpriteRegistry.cs:29-36` | `Ai/NativeSpriteRegistry.cs:36-43` | +7 |
| Audit `:128-338`, `:36-41` | `Alundra.Tests/AlundraStoryChainOpcodeAudit.cs:130-340`, `:38-43` | +2, fichier non renommé |
| Audit `:369` (déjà à HEAD) | `:369` (à `9502f75` : `:367`) | — |
| `IntroTraceHarnessTests.cs:322` | `:332-387` (le set), usage `:1791`, en-tête `:1721` | +10, miroir PÉRIMÉ (21 opcodes) |
| RunnerTests `:404-433` ; `UnknownOpcode_*` `:323`, `:338`, `:353` | `:412-441` ; méthodes `:331`, `:346`, `:361` (`[Fact]` `:330`, `:345`, `:360`) | +8 (`:320` = ancien état du plan) |
| `…ProductionTests.cs:17-31` ; `…YarnVariableStorageTests.cs:120` | `:23-37` ; `:123` | +6 / +3 |
| `opcodes.tsv` : 256 citations du runner et 11 de tests | runner −50 (256/256) ; `HeightWait :252-300`, `ProgramIndex :209`, `DialogueDispatch :325-355`, `OracleComparison :113`, `ZWait :290-306` | — |
| `EntitySearchService.cs:98-262` | `Entities/EntitySearchService.cs:99-263` | +1 |
| `backlog-2026-10-03.md:168-180` | = (titre G7 `:166`, tableau `:170-178`, règle des propriétaires `:181-184`) | = |

### 2.2 Autres tranches

Toutes confirmées par `pair.py`, sauf la ligne marquée « corrigé ».

| Ancienne | HEAD |
|---|---|
| Runner `:377-419`, `:1990-2040`, `:2011-2013`, `~:2011`, `:2691`, `:3312-3334`, `:250-254`, `:275-280`, `:760-762`, `:1213-1222`, `:1879`, `:2700-3050` | `:327-369`, `:1940-1990`, `:1961-1963`, `0x3F` en `:1961-1986` (insertion avant `:1987`), `:2641`, `:3262-3284`, `:200-204`, `:225-230`, `:710-712`, `:1163-1172`, `:1829`, `:2650-3000` |
| `EntityScriptProxy` `:76-77`, `:81`, `:88`, `:325`, `:341-343`, `:344`, `:351`, `:1212-1213`, `:2411-2418`, `:2596` | `:89-90`, `:94`, `:101`, `:338`, `:354-356`, `:357`, `:364`, `:1225-1226`, `:2424-2431`, `:2609` |
| `WorldProxy` `:2077`, `:2453-2454`, `:2493`, `:2893`, `:2955-2960`, `:3652-3653` | `:2111`, `:2487-2488`, `:2527`, `:2927`, `:2989-2994`, `:3698-3699` |
| `EntitySupport.cs:71-75`, `:276-290`, `:296-307` ; `ScriptedMotion :191`, `:719-720` ; `EntityFlags :103`, `:136` ; `IEntityWorldContext :24` | `Physics/EntitySupport.cs:72-76`, `:277-291`, `:297-308` ; `Physics/ScriptedMotion.cs:197`, `:725-726` ; `Entities/EntityFlags.cs:103`, `:136` ; `Maps/IEntityWorldContext.cs:35` |
| `PlayerManager :610-631`, `:1750`, `:1873`, `:1885`, `:1893-1910`, `:1957` | `Hero/PlayerManager.cs:623-644`, `:1763`, `:1886`, `:1898`, `:1906-1923` (`RestoreLifeAndMagic :1910`), `:1970` |
| `GameState :143` ; `TickPad :31-49` | `Session/GameState.cs:152` ; `Hero/TickPad.cs:33-51` |
| `AlundraCellStore.cs:13-40` (corrigé) | `Cells/ICellMutator.cs:14-38` (interface `:21-38`, 3 écritures, aucune lecture) |
| `EntitySpawnFactory :493` ; `AlundraMapSizeReader.cs` ; `ClassAndBranch…Tests :140-216` | `Entities/Spawning/EntitySpawnFactory.cs:497` ; `Maps/MapSizeReader.cs` ; `:143-219` |
| `plan-e20-heros.md:6287`, `:6321` | `:6525`, `:6559` (inchangés à `317b3360`) |

## 3. Affirmations sur la DLL (verdicts corrigés)

- **1 à 4 : VRAI.**
  - 158 `case` dans `Dispatch`.
  - Table : 198 entrées, 58 octets absents (`C5..FE`).
  - 29 opcodes sautés, 9 de taille 0 : `0E@56 0F@57 13@61 14@62 18@66 76@164 77@165 9D@205 AE@222`.
  - `UnknownOpcode` (`:3065-3091`) saute par la taille ; sans taille, il rend 0 (fin d'appel).
- **5 : VRAI** (mon recomptage).
- **6 : VRAI.**
- **7 : VRAI**, avec la nuance du point 12 de la §1.
- **8 : VRAI.**
- **9 : VRAI**, miroir périmé de 21 opcodes : `4C 4D 4E 4F 71 86 87 90 91 92 93 94 95 A0 A1 A2 A3 BC C0 C1 C3`. Aucun opcode du miroir n'est sans `case` (`mirror.py`).
- **10 : VRAI.** 292 des 510 lignes sont suivies d'un lecteur de `Result`, sur 11 opcodes.
- **11 : VRAI.**
- **12 : VRAI.** 11 fichiers de tests commencent un programme par `0x01`.
- **13 : VRAI.** 8 sites de la carte 60 ne sont atteints que par les racines à bit `0x80` clair (`map60_check.py`). Nuance du point 16 de la §1.
- **14 : FAUX dès l'annexe** (point 2 de la §1). Refus en `WorldProxy.cs:1074-1080`, `:3399-3404`, `:3528-3532`.
- **15 : PARTIEL.** Le rognage existe seulement pour `HasLogicalZ`, porté par E20.e0k3 (`ScriptedMotion.cs:610-612`, `:684-692`).
- **16 : INFÉRENCE.**
- **17 : VRAI**, avec le point 11 de la §1.
- **18 et 19 : VRAI.**
- **20 : VRAI.** Seul un commentaire cite un libellé (`AlundraEventProgramRunnerTests.cs:333`).
- **21 et 22 : VRAI.**
- **23 : FAUX tel quel.** 8 scripts de l'annexe et `sm1-sm4/work/{corpus.py, oracle/oracle.py}`, `check/corpus_copy.py` lisent `Alundra/Scripts/…`. Le README de l'annexe dit déjà de les relancer depuis une copie.

## 4. Tests qui épinglent les opcodes sautés (liste corrigée)

1. **Liste de la chaîne** : `AlundraStoryChainSkippedOpcodesTests.cs`, 21 `[Fact]`.
   - Corpus et règles : `:42`, `:50`, `:58`, `:66`, `:74`, `:89`, `:103`.
   - Parcours : 7 tests (`:133-186`). Règles sur entrées fabriquées : 7 tests (`:207-283`).
   - Si les 510 lignes hors chaîne entraient dans ce fichier, **deux** tests rougiraient : `:74` (`Except(ChainMaps)`, `:81`) et la règle 2 (`:50`).
2. **Règle 5** : `AlundraDay3SceneArcTests.cs:41-52`, appelée `:87`, `:132`, `:185`, `:303`, `:408`.
3. **Épingles du saut par la taille** (`AlundraEventProgramRunnerTests.cs`) : `:331` (`0xB3`, à repointer en sm4), `:346` (`0x5F`, sans tranche), `:361` (`0xC5`).
4. **Gardes des arcs.**
   - `AssertNothingSkippedOrExceeded` : 12 fichiers. Toute la famille : 18 fichiers de tests.
   - Sur les 96 cartes à sites, **seule la carte 117** est gardée : `AlundraLeverBasculeArcTests.cs:49`/`:228`, `AlundraLeverPillarArcTests.cs:24`/`:81`, `AlundraLeverSmokeTests.cs:74`, `:81`, `:34`.
   - Arcs `ArcRun` **non gardés** sur une des 96 cartes : la 260, `AlundraControllerlessVerticalStepTests.cs:301` et `:367` (site `0xB9` `C[7] @780`).
5. **Miroir du harnais d'intro** : `IntroTraceHarnessTests.cs:332-387`, usage `:1791`, en-tête `:1721`. Seule la ligne 2 de `docs/intro-programs-389.txt` dit « NOT IMPLEMENTED ». Remplacer le miroir ne change donc que si l'en-tête est réécrit.
6. **Parcours dupliqué dans M07** : `AlundraMissingAnimationWaitCorpusTests.cs`, `Census :70`, commentaire `:62-69`. Il réutilise `LoadMap` (`:295`).
7. **Table des tailles** : 14 lectures de `Entries`, aucune assertion sur son contenu.

## 5. Banc et noms

Les lignes du banc sont confirmées : membres de l'audit (`:16`, `:19`, `:23`, `:39` à `:472`), `ArcRun` (`AlundraArcSupport.cs:62` à `:620`), `ArcChecks` (`AlundraArcChecks.cs:14`), `LeverArcs` (`AlundraLeverArcSupport.cs:66-74`), montages réels (`AlundraEventProgramRunnerTests.cs:4156`, `AlundraRandomDirectionModesTests.cs:135`, `AlundraInitializeEventDataTests.cs:59-75`).

Les noms proposés restent des PROPOSITIONS, sous les mêmes réserves (Q-R-3 points 1, 5 et 6 ; Q-R-7). Prochain ADR libre : 0076.

## 6. Manques bloquants (liste corrigée)

### Gardés, avec corrections

- **B1. Aucune tranche écrite.** S'ajoutent à ce qui manque :
  - les commandes de build et de test : Release avec copie neutralisée, `--blame-hang-timeout` ;
  - la ligne de revue : le tableau du plan `:12-18` s'arrête à E19.m.
- **B2. Où va la liste ?** Deux tests rougissent, pas un (point 17 de la §1).
- **B3. Format de la liste** : `ParseList` attend 11 colonnes, `skipped-sites.tsv` en a 9, dans un autre ordre.
- **B4. Règle des propriétaires perdue.** Aucune règle ne rend rouge une ligne « à placer » ou dont le propriétaire est fermé. Il faut aussi une source qui dise quels propriétaires sont fermés.
- **B5. Quand ?** Q-E19-SM1 est ouverte, et ADR-0075 interdit toute tranche en parallèle du chantier. En plus :
  - un test ajouté avant R5 change le compte de R5 (« 5 097 / 0 », `plan-restructuration-dll.md:978`) ;
  - il demande une ligne dans `r5-files.tsv` (336 lignes, `:879`), pas encore versé dans `tools/dll-structure/` ;
  - R5 prévoit une garde `check_names.py --tests` (`:983`).
- **B6. `0xBB` est-il une attente ?** Avec la preuve du point 18 de la §1, mais toujours INFÉRENCE.
- **B7. Huit sites peut-être absents de l'original** (carte 60), avec la nuance du point 16 de la §1.
- **B8. Aucun relevé épinglé, aucun arrêt de recoupement.**
  - Les trois relevés (annexe, agent, le mien) sortent **du même `census.py`**, et le critique de l'annexe n'a rien relancé.
  - Aucun parcours d'une autre implémentation n'existe. Le parcours C# sera le premier : l'arrêt « exactement 510 lignes, 96 cartes, 21 opcodes, 187/129/42/90/17/42/3 » est indispensable.
  - Chargement : déjà exercé par M07. Durée : INCONNU.
- **B9. Deux énoncés du même test** : la ligne E19.m (`:615`) et §1.2s (`:10137`).

### Nouveaux

- **B10. Mauvais modèle de tranche.** Le plan impose le motif d'E19.l à toutes les tranches. sm0, sm9 et sm10 n'ont pas de gestionnaire : le bon précédent pour sm0 est E19.e E1 (`:4171-4215`).
  - Liste copiée à l'identique d'une découverte recalculée par un auditeur avec son propre parcours.
  - Arrêt si le C# ne redonne pas exactement les lignes.
  - Rouge d'abord, une mutation par règle, chaque règle rouge seule, mutations réelles faites par script.
- **B11. Commits non bissectables dans sm1 à sm8.**
  - Avec une liste du corpus, porter un opcode atteint fait rougir la règle 2 tant que ses lignes restent.
  - Le code et le retrait des lignes doivent donc aller dans le même commit (précédent : L2-1 et L2-2 fusionnés, `:6820`).
  - Les tâches de `sm1-sm4/report.md` §7 séparent T2 (code) et T4 (liste) : c'est contradictoire.
- **B12. Carte de combat 117.** Son site (`0xB9` `B[1] @171`) est sur une carte exemptée (`ExemptCombatMaps :46`, E14). Il faut décider : listée (propriétaire sm7) ou exemptée.
- **B13. `0x5F` sans propriétaire.** Il reste sauté pour toujours avec le découpage proposé. Cela touche l'`Oracle_…`, la clôture d'E19 et la colonne propriétaire.
- **B14. Un pc partagé.** Les 510 lignes ne font que 508 instructions : `440 C[2]`, `C[3]`, `C[4] @1140` (`0xAA`) partagent un pc. C'est à épingler, comme E19.e l'a fait pour la chaîne.
- **B15. Périmètre flou.**
  - Le rapport (§8) met dans SM0 le remplacement d'`ImplementedOpcodes` par `IsPorted`, et un test « zéro `LoopBudgetExceeded` » plus tard.
  - La ligne du plan (`:12067`) ne dit ni l'un ni l'autre, et la règle L1-R8 (`:6687`) resterait en vigueur.
- **B16. Dépendance à l'export.** La liste ne vaut que pour l'export du 2026-10-17 21:51 (date de `world-index.json`). sm5 exige un export complet : il faut un arrêt « si une ré-exportation change les clés », avec preuve par double comptage.
- **B17. Propriétaires suspendus aux questions de l'auteur** : Q-sm-4 (10 lignes), Q-sm-6 (3 lignes), O-E19-36 (42 lignes).

### Inexactitudes du plan relevées par l'agent

Toutes confirmées :
- « 21 lus » au lieu des 21 atteints (62 sites non lus) ;
- `0xC2` reste un prédicat périmé après sm4 ;
- « dix » prédicats au lieu de onze ;
- la ligne sm9 est périmée (plafond de 63 fait, rognage partiel, M-13 disparu) ;
- M-03 n'a pas de question au §3 ;
- Q-sm-1..9 contre 8 au §3 ;
- Q-sm-6 cite `0x88` comme « 0 site ».

## 7. Inconnus

- La durée de M07 et celle d'un parcours C# des 483 cartes.
- Si `0xBB` suspend.
- Ce que le binaire lit aux index C 1, 4 et 10 de la table globale.
- Si les cartes 55 à 57 sont atteignables.
- Le comportement des boucles `0x8A` quand la table des créneaux est pleine.