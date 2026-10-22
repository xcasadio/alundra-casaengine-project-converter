# Préparation exécutable d'E19.sm2 (`0x6A 0x6B 0x6C 0x6D`, 129 sites) : rapport, 2026-10-18

Lecture seule sur le dépôt, les sous-modules, `alundra-project/`, `data-extracted/` et le binaire. Je n'ai lancé ni build, ni test C#, ni export.
Python avec `-B`, sur des copies des scripts d'annexe. Les étiquettes sont FAIT, INFÉRENCE et INCONNU.

Dossier de sortie, appelé `OUT` plus bas :
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm2`

## 0. Cadre et verdict

- **HEAD lu** : `31fc8b23`, branche `chantier/restructuration-dll-r3`.
  - FAIT : `git diff --stat 317b3360 HEAD -- Alundra Alundra.Tests alundra-project tools` est vide.
  - FAIT : le plan E19 et l'annexe SM n'ont pas bougé depuis `36ac0686`. Les derniers commits ne touchent que le plan E20.h1.
  - Les citations de la DLL sont celles de `translate_sm12.md` et `check_sm12.md`.
- **Binaire** : `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (FAIT, `sha1sum`). Les gestionnaires sont relus par ma propre désassemblée : `OUT/evidence/redisasm.txt`.
- **Verdict** : toute la matière d'une tranche exécutable est produite et rejouable (`OUT/tools/run_all.py`, 50 s). Ce qui reste bloquant n'est pas de la matière, ce sont des prérequis (§8).
  - R5.
  - Q-R-7 et la garde R7.
  - L'accord sur Q-E19-SM1.
  - sm0 exécutée.
- **Cinq constats qui changent le plan** :
  1. Aucun seuil du corpus n'atteint `0x8000`. Les seuils sont 32, 48, 60, 64, 72, 80, 96, 112, 120, 128 et 256 pixels. Le cas du seuil négatif est une propriété du gestionnaire, couverte par les vecteurs, pas un site réel.
  2. Les cartes 55 à 60 forment un îlot fermé. La 307 aussi (grappe 305-306-307). Seules la **344** (14 sauts depuis la 389) et la **442** (11 sauts) sont atteignables : 56 sites sur 129. Les 73 autres sont fermés par le port sans effet en jeu.
  3. Le « mémo » est sûr. 70 lectures, 59 précédées d'un `0x6A`, 11 sans (état neuf), 0 écrasement. Les 4 lectures masquées de la 307 par l'ancien `memo_flow.py` sont retrouvées.
  4. Les 11 lectures `0x6D` sans `0x6A` ne reçoivent aucun `0x6A` de l'entité, par aucun chemin `0x40`. La question INCONNU du rapport sm est donc tranchée côté export.
  5. Pour la 344, le programme C ne tourne que si l'un des jeux de drapeaux suivants est posé : `{1710}` ou `{1026, 1027}`. Le programme A[60] efface sinon le programme C.

## 1. Règles d'exécution SM2-R1 à R7 (noms du HEAD)

Les quatre gestionnaires sont des feuilles. FAIT : aucun `jal`, aucun accès à la pile, aucune lecture du statut `+0x10`. Il n'y a donc ni recherche par `EntitySearchService`, ni `PlayerEntity`, ni groupe, ni référence nulle. Les cas « groupe vide » et « référence nulle » n'existent pas pour sm2. Le texte décompilé (`EntityEventHandlers.cs:2095-2140`) calcule déjà la bonne formule : le port le recopie, avec `unchecked` en plus.

Le `entity` de `Dispatch` (`EventProgramRunner.cs:563`) est l'entité **logique** (`owner.LogicEntity ?? owner`, `:423`). `state` est l'état du **propriétaire**.

| Règle | Contenu |
|---|---|
| **R1, `0x6A`** (`0x8003F8DC`, taille 1) | `state.Parameters[2] = entity.PosX; [3] = PosY; [4] = PosZ`. `Result` intact. Retour 1. Aucun autre membre écrit. `Parameters[1]` est remis à 0 par la boucle (`:465`, binaire `0x8004232C`), pas par le gestionnaire. |
| **R2, `0x6B/6C/6D`** (`0x8003F908/94C/990`, taille 3) | `d = Parameters[k] - entity.PosK` en int32 qui boucle (`subu`), avec k = 2, 3, 4 pour X, Y, Z. Si `d < 0`, `d = -d` sans exception (`negu` : `int.MinValue` reste `int.MinValue`). `seuil = (v[1] \| v[2] << 8) << 16` comme int32 signé, négatif dès `v[2] >= 0x80`. `Result = (d < seuil) ? 0 : 1` en comparaison signée (`slt` puis `xori 1`). Les octets sont lus non signés (`FillDataFromCommand :514-538`). **Pas de `Math.Abs`** (lève sur `int.MinValue` ; l'occurrence existante `:2915-2916` n'est pas à toucher). **Pas de forme en pixels `>> 16`** (celle de `Walk`, qui diffère à partir de `0x8000`). Bloc `unchecked` explicite. Aucun `checked` ni `CheckForOverflowUnderflow` dans le dépôt (FAIT, `rg`). |
| **R3, cellules du mémo** | `Parameters[2..4]` sont les cellules des attentes `0x0B 0x1C-0x23 0x37` : aucun stockage neuf, aucune remise à zéro. Le mémo survit à `Break`, à `InitializeEventData` (écrit seulement `Parameters[0]` et le pc, `:368`), à l'effacement de `0x40` (`EndCall` ne touche que `Sp` et `Codes`) et aux appels A/D/E/F (état partagé `_slotAScratchState :137`). Écrasements fidèles : X par `0B 1C 1D 1E 1F 20-23 37`, Y par `0B 1E 1F`, Z par aucun opcode porté. Dans le binaire, `5F` écrit P2 à P6 et `BB` écrit P2 (`stateuse2` exécuté, `evidence/stateuse2_run.txt`). `0x6D` sans `0x6A` lit la valeur courante de la cellule (0 pour une entité neuve côté DLL). |
| **R4, rien d'autre** | Hôte nu suffisant (`NoOpEntityWorldContext`). Ni statut, ni journal, ni `Degraded`. La trace dit `Implemented` avec `Size` 1, 3, 3 et 3. |
| **R5, tailles et libellés** | Tailles égales au binaire (`EventOpcodeSizeTable.cs:152-155`). Libellés à corriger : `:153`, `:154` et `:155` deviennent « If entity x/y/z moved >= threshold from the saved position ». `:152` reste. Aucun test, aucune trace, aucun document ne lit ces chaînes (FAIT, `rg`). Détail : `OUT/proposed/EventOpcodeSizeTable.sm2.diff.txt`. |
| **R6, comptabilité** | `IntroTraceHarnessTests.ImplementedOpcodes` (`:332-387`, mirroir d'étiquettes) reçoit `0x6A 0x6B 0x6C 0x6D`. Les six traces (`docs/hero-trace-389-*.txt`, `intro-trace-389.txt`, `intro-programs-389.txt`) restent identiques : FAIT, aucune des quatre chaînes n'y apparaît. `PredicateOpcodes` reste inchangé (précédent E19.l1). Voir §6 pour la liste fermée. |
| **R7, noms** | Garde de noms (ADR-0075 et R7) : `Events/PositionMemoOpcodesTests.cs`, `PositionMemoVectors.cs`, `PositionMemoCorpusTests.cs`. Noms libres (FAIT, `rg`). Avant R5 ils auraient été `Alundra<Sujet>Tests`. Aucun type, aucun fichier neuf dans `Alundra/`. |

Code proposé non construit (cases, aide `MovedAtLeastTheThreshold`, points d'insertion `:2025` et `:2646`) : `OUT/proposed/EventProgramRunner.sm2.cs.txt`.

## 2. Oracle de valeurs figé

- **Fichier** : `OUT/vectors/vectors-sm2.tsv`, 1 114 lignes plus l'en-tête. sha256 `b42f0f4558719b4a2b9e66f571826a47e2b0cc7b9f22ee6e331089228420b72b`.
- **Deux exécutions donnent le même fichier** (`cmp`, FAIT). Graine 20261018, 14 s.
- **Les VRAIS gestionnaires** tournent dans l'interpréteur MIPS de l'annexe (copie), avec l'appel fidèle `a2 = a3 = STATE`.
- **Répartition** :

| Opcode | Lignes | Rouges aujourd'hui |
|---|---|---|
| `6A` | 31 | 30 |
| `6B` | 361 | 253 |
| `6C` | 361 | 258 |
| `6D` | 361 | 253 |

- **Groupes** :
  - Seuils autour de `d` valant seuil−1, seuil, seuil+1, 0, 1, 2, `int.MaxValue` et `int.MinValue`, des deux signes.
  - Seuils négatifs : 0x8000, 0x8001, 0xFF00, 0xFFFF.
  - Bouclage de `memo - pos` en int32.
  - Mémo à 0 (`0x6D` sans `0x6A`).
  - Autres axes aléatoires.
  - Bloc aléatoire.
  - 318 lignes ont `v[2] >= 0x80`.
- **Preuve d'empreinte** : chaque lecture et chaque écriture est journalisée. La génération s'arrête (STOP) si un octet lu n'a pas de membre de la DLL, si un octet mappé n'est pas lu, ou si un mot de la fenêtre `0x80100000..0x80190000` change hors des écritures mappées.

| Adresse du binaire | Membre de la DLL |
|---|---|
| `E+0x114`, `+0x118`, `+0x11C` | `EntityScriptProxy.PosX`, `PosY`, `PosZ` de l'entité logique |
| `S+0xC`, `+0x10`, `+0x14` | `Parameters[2]`, `[3]`, `[4]` de l'état du propriétaire |
| `S+0x2C` | `Result` |
| `S+0`, puis `*S+1` et `*S+2` | octets d'instruction `v[1]`, `v[2]` |

- **Deux modèles reproduisent chaque ligne** : le modèle du binaire (`subu`, `bgez`/`negu`, `slt`, `xori`) et la formule int32 de R2, telle que C# `unchecked` la calcule.
- **Recoupement avec les deux oracles de l'annexe** (copies de `oracle.py` et de `myoracle.py`), 3 000 états aléatoires par opcode : 0 écart (`evidence/crosscheck_annex.txt`).
- **Le propriétaire reçoit des positions leurres** (`0x0DEC0114`, `0x0DEC0118`, `0x0DEC011C`). Une implémentation qui lirait le propriétaire et non l'entité logique est tuée par 416 lignes.
- Le README du format et de la table de mapping : `OUT/vectors/README.md`.
- **Mutants de la formule sur les vecteurs** (`evidence/mutants_vectors.txt` et `mutants_rows.txt`) :

| Mutant | Lignes qui le tuent |
|---|---|
| `Math.Abs` | 168 |
| forme en pixels | 366 |
| seuil et distance non signés | 366 |
| `<=` à la place de `<` | 72 |
| sens inversé | 1 083 |
| soustraction 64 bits | 193 |
| pas de valeur absolue | 216 |
| seuil lu sur l'octet 1 seul | 193 |
| seuil en gros-boutiste | 348 |
| `0x6A` oublie Z | 30 |
| `0x6A` oublie Y | 30 |
| `0x6A` écrit `Result := 1` | 19 |
| `0x6A` écrit `Result := 0` | 24 |
| lecture du propriétaire | 416 |
| `0x6D` lit `Parameters[3]` | 127 |

  Deux mutants **équivalents** (prouvés, donc exclus du compte) :
  - `position - memo`, car |a−b| = |b−a| modulo 2³², `int.MinValue` compris.
  - `(sbyte)v[2]`, car `<< 16` expulse les bits de signe.

## 3. Tests nommés

Montage commun (copié du précédent `AlundraClassAndBranchOpcodesTests.cs:21-67`) : coureur nu `new EventProgramRunner(document, new GameState())`, propriétaire plus entité logique (`owner.LogicEntity`), `RunOneScriptCall`, `TraceSink`. Les assistants `NewDocument`, `NewRunner` et `NewEntity` d'`AlundraEventProgramRunnerTests :28, :38, :41` sont privés : un fichier neuf recopie le montage. L'export manquant fait ÉCHOUER le test en le nommant (`FindProjectRoot`, `SaveGameDirectorTestSupport.cs:70`), jamais un self-skip.

`Result` de départ vaut 7 pour les tests H, 1 pour les tests RB (avec 56b à 0). Tout `Result` écrit vaut 0 ou 1, donc **rouge = 7 ou valeur périmée**. Brouillon non construit : `OUT/proposed/PositionMemoOpcodesTests.cs.txt`. Valeurs : `evidence/handvalues.txt` et `proposed/RealBytesExpected.txt`.

| ID | Montage et départ | Attendu | Rouge aujourd'hui (saut par la taille) |
|---|---|---|---|
| **V6A, V6B, V6C, V6D** (4 `Fact`) | Une ligne du TSV : entité logique ≠ propriétaire leurre, sentinelles sur P0, P5-P9, `_30`, `_34`. | `Result`, `Parameters[2..4]`, `CodeIndex == v0`, `Parameters[1] == 0`, sentinelles intactes, positions intactes, première trace `Implemented` de taille `v0`. | 30/31, 253/361, 258/361 et 253/361 lignes rouges |
| **H1** | `[6A, FF]`, pos `(0x180000, 0x280000, 0x300000)`, mémo `(0x11111111, 0x22222222, 0x33333333)`, `Result` 7 | `Parameters[2..4]` = pos ; `Result` 7 ; `CodeIndex` 1 | mémo inchangé |
| **H2** (10 cas) | `[6B 78 00 FF]` mémo `0x100000`, pos `0x880000`, `0x87FFFF`, `0x880001` ; mémo `0x880000`, pos `0x100000`, `0x100001`, `0x10FFFF` ; `6C 10 00`, `6D 01 00` | 1, 0, 1 ; 1, 0, 0 ; `6C` : 1, 0 ; `6D` : 1, 0. `CodeIndex` 3. | 7 |
| **H3a/b/c** | `6B 00 80` mémo=pos ; `6B FF FF` mémo `0x80000000` pos 0 ; `6B 00 80` même mémo | 1, 0, 1 | 7 |
| **H4** | `6B 78 00` mémo `0x7FFFFFFF` pos `0xFFFFFFFF` (la différence boucle en `int.MinValue`) | 0, sans exception | 7 |
| **H5** (3 cas) | `[6D 00 01 FF]` état neuf (P4 = 0), PosZ `0x00FFFFFF`, `0x01000000`, `0x01000001` | 0, 1, 1 | 7 |
| **H6** | `[6A 00 6B 10 00 FF]`, entité logique ≠ propriétaire leurre ; pos `(0x500000, 0x600000, 0x700000)` puis X `+16 px` | `Parameters[2..4]` = pos de l'entité logique ; `Result` 1 | mémo 0 ; 7 |
| **H7a/b** | `[6A 00 6B 78 00 FF]`, X = 64 px puis `+120` ou `+119` | 1 ou 0 ; `CodeIndex` 5 ; P2 `0x400000` | 7 ; P2 = 0 |
| **H8** | Même programme ; après le tick 1, `InitializeEventData` sans exécution | `Parameters[2..4]` = `(0x400000, 0x50000, 0x60000)` | 0, 0, 0 |
| **H9X/Y/Z** | `[6A 37 03 <6B/6C/6D> 10 00 FF]`, 5 ticks, entité immobile | X : `Result` 1 (écrasé, P2 = 4) ; Y : 0 ; Z : 0 ; `CodeIndex` 6 | 7 |
| **H10** | Les trois libellés de la table | chaînes de R5 | anciens libellés |
| **RB-55** | Octets RÉELS de la carte 55, C[4], pc 272 : `6A 1A 00 / 6B 78 00 03 30 02 ...`. Position X 64 px, +119 px, +120 px. | Tick 1 : 272:6A 273:1A 275:00, mémo `(0x400000, 0xC80000, 0x80000)`. Tick 2 : 276:6B 279:03 282:30 287:02 273:1A 275:00, `Result` 0. Tick 3 : 276:6B 279:03 290:1A 292:0A 293:02 272:6A 273:1A 275:00, `Result` 1, P2 `0xB80000`, `TargetDirection` 16. | au tick 2 le `03` est pris (pc 290...) ; mémo 0 |
| **RB-55n** | Même programme, X qui diminue (200 px puis 81, 80) | Tour au tick 3 | idem |
| **RB-56, RB-56b** | 56 C[1] depuis le pc 93, `6D 00 01`, Z `0x00FFFFFF` puis `0x01000000` | RB-56 (`Result` de départ 1) : tick 2 `Result` 0, boucle ; tick 3 `Result` 1, `1B 1B 00`, `CodeIndex` 118. RB-56b (départ 0) : tick 2 `Result` 1. | tick 2 : le `03` est pris trop tôt (RB-56) ; la boucle continue (RB-56b) |
| **RB-59** | 59 C[6] (`2A 6A 1B 00 6D 20 00 03 ...`), Z 96 px puis +32 | Tick 1 : mémo `(0x500000, 0x3C0000, 0x600000)`. Tick 2 : `Result` 0. Tick 3 : `Result` 1, second `6A` (mémo Z `0x800000`). | mémo 0 ; `6A` @476 déjà au tick 2 |
| **RB-307** | 307 C[1], pc 424 : `6A 1B 78→474 [6E 7F 1B 7D] 6B 3C 00`. Le mémo traverse l'appel. X 37, 96, 97 px. | Ticks 1 et 2 : `Result` 0, `CodeIndex` 440. Tick 3 : `Result` 1 puis `6A` @444, second appel, `6B` @451 `Result` 0. | jamais de retournement |
| **RB-344** | 344 C[60], pc 2356 : Y `+64 px` puis X `+96 px`, deux mémos | Ticks : 3 → `Result` 1, `08` @2380 (`TargetDirection` 24), second `6A` ; 6 → tour X (`TargetDirection` 16, `6A` @2358). | `Result` périmé : les `04`/`08` sont pris à chaque passage |
| **RB-442** | 442 C[3], pc 512 : `6A 1A 00 30 30 6B 48 00 04 0A 02`, X 192, +71, +72 px | Tick 3 : `6B` `Result` 1, `0A` @532 (`TargetDirection` 16), `02`, `6A` @512, `CodeIndex` 516, P2 `0x1080000`. | idem RB-55 |
| **S1f** (15 cas) | La fonction de flot seule, sur des programmes fabriqués | voir §5 | n/a (fonction neuve) |
| **S1** (3 `Fact`) | Corpus entier : états des lectures, liste des lectures « neuves », comptes par carte | voir §5 | vert au premier passage |
| **S4** | Épingle : les 8 sites de la carte 60 (C[1] et C[10]) ne sont atteints que par des enregistrements dont l'indice brut a le bit `0x80` clair | `{rec 0 → C[1], rec 9 → C[10]}` | vert au premier passage |
| **S3** (facultatif) | Sonde des écrivains de P2-P4 (deux appels par opcode sur hôte nu, `RecordHeight` posé pour `22/23`) | écrivains portés = `{0B 1C 1D 1E 1F 20 21 22 23 37 6A}`, P4 par `6A` seul | limite : les opcodes qui exigent un hôte ne sont pas sondés (INFÉRENCE de complétude) |

**Rouge aujourd'hui, dénombré** (moteur Python de `evidence/killmatrix.txt`) : 38 tests (4 V + 26 H + 8 RB), dont 37 de comportement et 1 de libellé (H10). Les tests statiques sont vert-d'emblée, ce sont des gardes à preuve par mutants (§6).

Précédents suivis : E19.l1 L1-1 (`Result` de départ 7, rouge d'abord, règles numérotées) et `IsForceAdjusted_0x6E_RealOccurrence...` (`:4156`) pour les octets réels au pc réel. Je ne suis PAS le self-skip du second.

## 4. Cartes réelles : programmes, atteignabilité, arcs

### 4.1 Ce que sont les 129 sites

- 45 programmes, tous en créneau C. 13 structures d'opcodes (`tools/shapes2.py`).
- Chaque lecture est immédiatement suivie d'un `03` (42 fois) ou d'un `04` (28 fois). FAIT.

| Carte | Sites | Programmes (enregistrements) | Seuil | Zone de génération contient (33,59) | Atteignable depuis la 389 |
|---|---|---|---|---|---|
| 55 | 15 (6A 5, 6B 5, 6D 5) | C[4, 5, 7, 8, 9] (rec 3-9) puis C[20-24] (rec 29-33) | 120 puis 112 | oui pour les 5 premiers ; non pour C[20-24] | non |
| 56 | 4 (6D) | C[1-4] (rec 0-3) | 256 | non (x 0..15) | non |
| 57 | 2 (6D) | C[4], C[7] (rec 3, 8) | 80 | non | non |
| 59 | 20 (6A 10, 6D 10) | C[6, 8-11] (rec 8, 10-13) | 32 | oui | non |
| 60 | 24 (6A 12, 6B 2, 6C 2, 6D 8) | C[6-9] « Interrupteur à ballon » (16 sites) ; C[1] et C[10] « Pot magique » (8 sites) | 32 ; 96 et 128 | non (x 20..51, y 0..31) | non |
| 307 | 8 (6A 4, 6B 4) | C[1], C[2] (rec 0, 1) | 60 | non | non |
| **344** | 40 (6A 20, 6B 10, 6C 10) | C[60-67] « Bloc transparent » (rec 59-66) | 32, 48, 64, 96 | non (x 39..51, y 35..59) | **oui, 14 sauts** |
| **442** | 16 (6A 8, 6B 8) | C[3, 5-9, 22, 23] « Mur à boule de fer » (rec 0, 1, 2, 4, 5, 19-21) | 72 | non (x 4..20) | **oui, 11 sauts** |

Re-dérivation indépendante des 129 sites sur 483 cartes : mêmes 129 clés que `skipped-sites.tsv` (`evidence/census_sm2.txt`).

### 4.2 Atteignabilité (Q-sm-8 élargie)

Graphe statique : portails (`DestMapId`) plus tous les `0x53` de tous les programmes de toutes les tables (`evidence/reach_maps.txt`).

- Cartes 55 à 60 : les seules arêtes entrantes viennent de l'intérieur de {55..60}. FAIT.
- FAIT : le script de la 389 `B[1] @310` écrit `MapIdToInternalMapIndexTable[55] = 443` (Torla réelle). Les 66 sites `0x38` du corpus ne redirigent aucun autre identifiant vers {55..60, 305-307}.
- Carte 307 : seulement depuis la 305 (« Meia Dream (unused_) »), elle-même seulement depuis 306 et 307. Îlot fermé. INFÉRENCE : inatteignable en partie normale.
- Hypothèses non couvertes : les mécanismes natifs (E14), les sauvegardes forgées et les menus de débogage (INCONNU).

### 4.3 Verdict arc par arc

Précondition commune FAIT pour les huit cartes :
- Le fichier de dialogues de la carte existe.
- Le `.world` et le `.tileMap` existent.
- Chaque `PrefabAssetId` est dans `AssetInfos.json` et son `.entity` existe (`evidence`, `tools/prefab_check.py`).
- Les drapeaux testés par les programmes sont dans la banque temporaire (bit `0x8000`, vide au chargement) : `0x800A`, `0x81F4`, `0x80C8`, `0x82A8`, `0x82C9`, `0x82BD`.

Aucune de ces cartes n'a jamais été chargée par `ArcRun` (INCONNU, A20). Je ne peux pas le lancer. Le plan T0 doit donc comporter un **test de chargement jetable** par carte : une image dans `ArcRun`. Un échec retire l'arc de cette carte, pas la tranche. Limite d'images de chaque arc : INCONNU, à fixer par une première mesure (§1.3 du plan) puis × 2.

| Arc proposé | `ArcRun` maintenant ? | Remplaçant garanti (octets réels, §3) | Comportement d'aujourd'hui, décodé |
|---|---|---|---|
| **55 C[4]** | Précondition : la zone contient (33,59), pas d'`Arrival` nécessaire ; drapeau `0x800A` bit 10 clair. Signal de fin : `0A @292` du programme 272. Invariants en direct à `6A @272` (P2..P4 = pos) et `6B @276` (`Result == |P2−PosX| >= 120<<16`). Le flammèche bouge-t-elle de 120 px ? INCONNU. | **RB-55**, **RB-55n** | Le `6A` et le `6B` sont sautés ; `Result` (0 pour un état neuf) n'est jamais lu comme 1, donc le `03` n'est jamais pris : le patrouilleur ne se retourne jamais. Si `Result` valait 1, il se retournerait à chaque passage (RB-55 le montre). |
| **344 C[60]** | **Drapeaux à poser : `{1710}` ou `{1026, 1027}`** (sinon A[60] efface le C). `Arrival` obligatoire (zone x 39..51, y 35..59). Signal : `6A @2382` du programme 2356. Invariants à `2374` et `2398`. Le bloc invisible bouge-t-il ? INCONNU. | **RB-344** | `Result` neuf = 0, donc `04` @2377 pris : le bloc continue droit, ne tourne jamais (`08 18` jamais exécuté). |
| **59 C[6]** | Zone contient (33,59). Signal : second `6A @476`. Invariants à `6D @462`. | **RB-59** | `Result` 0 : `03` @465 jamais pris ; le ballon ne descend jamais, `6A` @476 jamais exécuté. |
| **60 C[1]** | **Non retenu.** Ces sites ne sont atteints que par repli (§4.4). | RB sur 60 C[6] (même forme que 59 C[6]) si l'auteur le veut | idem 59 |
| **56 C[1]** | `Arrival` obligatoire (zone x 0..15). Aucun `6A` ; état neuf. | **RB-56**, **RB-56b** | `Result` neuf 0 : le `03` @100 n'est jamais pris, la flamme ne monte pas au seuil de 256 px (comportement de boucle jusqu'à la sortie). |

Arcs que je recommande en plus, sur les deux cartes atteignables :
- **A-442** : `Meia Dream` / `Meia Dream-442`. Pas de drapeau. Arrivée en zone x 4..20, y 7..26, loin des rangées de murs (tuiles d'affichage 7 à 22 en x, 12 à 15 en y). Signal `0A @532` d'un des huit programmes.
- **A-344** : `Lake Shrine` / `Lake Shrine (inner)-344`, avec les drapeaux `{1710}`.
- Dans les deux cas : `RealController: true, Prefabs: true`, modèle `AlundraHeightTargetSitesTests.cs:76-90`, 0 glissement attendu (le héros ne bouge pas), et ils doivent finir sans `UnknownSkipped` sur `6A` à `6D`.

### 4.4 Les 8 sites de la carte 60 (O-SM0-1)

- FAIT : les six enregistrements « Pot magique » à bit `0x80` clair ont les indices bruts C = 1 (rec 0), 4 (rec 1 à 4) et 10 (rec 9). Seuls rec 0 → C[1] et rec 9 → C[10] exécutent des sites `6A-6D` (4 + 4 = 8).
- FAIT : le binaire lit la table globale `map_alundra` (`0x80041F28-0x80041F54`, base `0x80126EC0`, entrée = u16 à `table + 2·(indice & 0x7F)` sans borne). La table C globale exportée n'a qu'une entrée (`EventCodesCSize` 2).
- INFÉRENCE : les indices 1 et 10 lisent donc au-delà de la table C globale, donc ni la C[1] ni la C[10] de la carte 60. La DLL, elle, repli sur la table de la carte (`EventProgramRunner.cs:339-346`).
- Conséquence pour sm2 : aucune ; le port est le même, les 8 sites restent à propriétaire `sm2` (D-SM0-4). La carte 60 est de toute façon inatteignable. Je recommande de NE PAS écrire d'arc ni de test d'exécution sur ces deux programmes : seul l'épinglage S4.

## 5. Test statique du mémo (S1) et variante linéaire (S2)

**Comptes figés** (`evidence/memo_reads_table.tsv`, 70 lignes) :
- X : 29 lectures, toutes `SAVED`.
- Y : 12, toutes `SAVED`.
- Z : 29 = 18 `SAVED` + 11 `INIT`.
- `CLOBBERED` : 0.
- 59 lectures précédées d'un `0x6A`, 11 sans.
- Les 11 : 55 C[20..24] (pc 1397, 1493, 1589, 1685, 1781), 56 C[1..4] (97, 201, 305, 409), 57 C[4] @217 et C[7] @413.
- 4 appels `0x78` entre un `0x6A` et une lecture, tous sur la 307 : C[1] @430 et @448, C[2] @495 et @513, tous vers `@474` (`6E 7F 1B 7D`, aucun écrivain).

**S1, version complète** (recommandée) : analyse de flot avec états `Init / Saved / Clobbered` par pc et par axe. Référence : `OUT/tools/cfg2.py` (`dataflow`, `successors`, `reach_walkprogram`). Port C# non construit : `proposed/PositionMemoCorpusTests.cs.txt`.
- Les sites viennent de `AlundraStoryChainOpcodeAudit.WalkExportedMap`, sur les 483 cartes. La durée de ce parcours est INCONNUE (sm0 la mesure). Si sm0 ne partage pas son résultat, S1 reparcourt.
- Les arêtes sont celles de `WalkProgram`, mais écrites explicitement : appel `0x78/79/7B/7C` vers l'appelé ; retours `0x7D` (et `7E` à `81`) vers toutes les marques de retour du programme (`pc+3` ou `pc+5`).
- Écrivains = ceux du BINAIRE, un sur-ensemble de la DLL (`0x5F` et `0xBB` en plus), pour qu'un port ultérieur de ces deux-là soit contrôlé d'office. FAIT : les écrivains de la DLL sont ceux de `EventProgramRunner.cs:2655, 2660, 2715-2716, 2766, 2814, 2861, 2880, 2885, 2908-2909`. Aucun autre fichier de `Alundra/` n'utilise `Parameters` (FAIT, `rg`).
- **S1f** : 15 programmes fabriqués avec leurs états attendus, `evidence/memo_fabricated.txt`. Exemples : `6A 37 05 6B` → `Clobbered` ; `6A 37 05 6D` → `Saved` ; `6A 1E ... 6C` → `Clobbered` ; boucle arrière → `Init|Saved` ; appelé innocent → `Saved` ; appelé qui attend → `Clobbered` ; `6A 5F... 6D` → `Clobbered`.
- **Mutants de S1** (`evidence/memo_flow_mutants.txt`) :

| Mutant | Effet sur les 70 lectures |
|---|---|
| sans arêtes de retour (l'ancien `memo_flow.py`) | 66 lectures atteintes, X 25 au lieu de 29 : tué par S1 |
| `Break` termine le parcours | 4 lectures atteintes : tué |
| arêtes avant seulement | 66 lectures atteintes : tué |
| `0x37` ajouté aux écrivains de Z | Z : 11 `INIT` deviennent `Clobbered` : tué par S1 |
| `0x37` retiré des écrivains de X | **aucun effet sur le corpus** ; tué seulement par S1f cas 4 |

**S2, variante linéaire** (si l'auteur préfère 50 lignes) : pour chaque lecture, dernier `0x6A` avant elle dans le décodage linéaire du programme depuis son entrée ; aucun écrivain de l'axe entre les deux ; pour chaque appel entre les deux, décoder l'appelé jusqu'à `0x7D` et contrôler qu'il ne contient aucun écrivain. Attendus : 70 lectures, 59 avec `0x6A`, 11 sans, 0 écrasement, 4 appels contrôlés (appelé `6E 7F 1B 7D`). **Réserve** : le linéaire est aveugle à un saut arrière qui contourne le `0x6A` ; il donne ici les mêmes chiffres que S1. Choix du plan, défaut : S1.

## 6. Preuves de retrait, mutants, arrêts, budget, retour arrière, liste fermée

**Conséquences sur la liste de sm0** (même commit que le code) :

| Compte de la règle 5 | Avant | Après sm2 |
|---|---|---|
| lignes | 510 | **381** |
| instructions distinctes | 508 | **379** |
| cartes | 96 | **90** (quittent la liste : 55, 56, 57, 59, 307, 344 ; la 60 garde un `0x56`, la 442 un `0x88`) |
| opcodes | 21 | **17** |

Comptes par propriétaire, avant puis après : `sm1` 187 puis 187, `sm2` 129 puis **0**, `sm3` 42 puis 42, `sm4` 90 puis 90, `sm5` 17 puis 17, `sm7` 42 puis 42, `sm8` 3 puis 3. Par ailleurs : `ClosedOwners` reçoit `"sm2"` dans le même commit, et (k1) épingle aussi `ClosedOwners == { "sm2" }` pour qu'on ne puisse pas le retirer sans rouge.

**Preuve de retrait** (découle des définitions ci-dessus ; à rejouer par script en fin de tranche) :

| État | Rouge exactement |
|---|---|
| Code retiré seul (liste réduite gardée) | 37 tests sm2 (4 V + 25 H sans H10 + 8 RB) **et** la règle 1 de sm0 (129 sites atteints, sautés, non listés) |
| Libellés remis seuls | H10 seul |
| 129 lignes remises seules | règles 2 et 4 de sm0 (129 lignes périmées, propriétaire clos) et (k1) |
| Classe de tests et vecteurs retirés seuls | rien (aucun autre test ne les lit) |
| Aujourd'hui (avant la tranche) | les 38 tests sm2 |

**Mutants, un par règle au moins** (textes exacts dans `proposed/EventProgramRunner.sm2.cs.txt`, matrice : `evidence/killmatrix.txt`). La matrice a 17 mutants :
- M1 à M7 : R2 (`Math.Abs`, forme en pixels, non signé, `<=`, sens inversé, soustraction 64 bits, pas de valeur absolue).
- M8, M9 : R1 (Z oublié, écrit `Result`).
- M10 : lecture du propriétaire.
- M11 : `0x6D` lit `Parameters[3]`.
- M12, M13 : tailles.
- M14, M15 : R3 (`Break` ou la réinitialisation efface le mémo).
- M16 : R3 (l'attente épargne P2 : tué par H9X).
- M17 : libellé (tué par H10 seul).

Chacun est tué par au moins un test nommé. Exemples :
- M1 est tué par H3b, H3c, H4.
- M2 et M3 par H3a, H4.
- M6 par H3b, H4.
- M15 par H8 seul.
- M16 par H9X.
- M11 par H2Z et RB-59.

Les mutations réelles se font par script, avec restauration à l'octet (`git checkout -- <fichier>` puis `cmp`). Les deux mutants équivalents du §2 sont exclus du compte.

**Arrêts** :
- Une ligne de vecteur contredite par l'un des deux modèles, ou un octet lu sans membre (STOP du générateur).
- Une valeur écrite d'un test H ou RB contredite.
- Un mutant non rouge là où la matrice l'annonce.
- Les comptes de S1 (70, 59, 11, 0, 4) ou le sha256 du fichier de vecteurs qui changent.
- Un sha256 des `events.json` de l'export qui change : `evidence/export_hashes.txt` donne les huit fichiers et `world-index.json`.
- Un test existant rouge, ou un besoin de toucher un fichier de `Alundra/` autre que `EventProgramRunner.cs` et `EventOpcodeSizeTable.cs`.
- Un nom refusé par la garde R7.
- Un abandon de `--blame-hang-timeout` (la durée est à soumettre).
- R5 non faite, sm0 non exécutée, ou une réponse à Q-E19-SM1 autre que l'accord.

**Budget** : un `executor` (deux tentatives), `plan-verifier` avant, `verifier` frais après.

**Acceptation** (commandes) :
- Rejouer `vectors_sm2.py` deux fois (sha256 identique).
- `dotnet test Alundra.Tests/Alundra.Tests.csproj -c Debug --no-build --blame-hang-timeout 300s --filter <classes neuves>` rouge avant le code, vert après.
- Suite entière en Release à copie neutralisée (`-p:CustomAfterMicrosoftCommonTargets=<scratch>/e20e0a3b-exec/skipcopy.targets`), puis Debug en dernier, `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll`.
- `git diff --exit-code` sur les six traces.
- `rg -c "Math\.Abs" Alundra/Events/EventProgramRunner.cs` vaut toujours 2 ; `rg -n "Parameters\[4\]" Alundra` donne exactement 2 lignes (le `6A` et le `6D`, 0 aujourd'hui).
- `git diff` borné à la liste du §1 R5-R7.

**Retour arrière** : `git revert` du commit de code. L'annexe est un commit séparé. Commits proposés (anglais) :
- `docs(e19): sm2 annex, the position memo vectors and their tools`
- `feat(alundra): port the position memo opcodes 0x6A to 0x6D` (tests, code, 129 lignes retirées, libellés, miroir)
- `docs(plan): E19.sm2 done`

Recette de l'auteur : l'effet n'est visible qu'en 344 et 442. Atteindre ces cartes demande un préréglage de sauvegarde absent (FAIT : aucun préréglage 344 ni 442 dans `TestSaves.Presets`) ; l'ajouter est hors périmètre par défaut.

## 7. Questions à l'auteur (en termes concrets) et dépendances

Le tout se traite avec les défauts donnés, rien ne bloque sauf Q-E19-SM1.

| Question | Ce qu'on voit en jeu | Coût de chaque option | Défaut |
|---|---|---|---|
| **Q-sm2-1 : cadre Z du mémo** pour les 11 lectures `6D` sans `6A` (cartes 55-57, inatteignables) | Rien. Entité au repos : Z binaire ≡ 1 modulo 256 (ADR-0026), jamais un multiple de 65536, donc l'égalité exacte qui diffère ne se produit pas (INFÉRENCE). | A : cadre de la DLL (rien à faire). B : stocker et comparer `PosZ+1` (deux `+1` et une exception à la copie du mémo). | A |
| **Q-sm2-2 : arcs `ArcRun` en 442 et 344** | 442 (Meia Dream) : les huit murs à boule de fer se retournent après 72 px, ils dérivent sans fin aujourd'hui. 344 (Lake Shrine) : huit blocs invisibles tracent un parcours de 64 par 96 px, ils dérivent aujourd'hui. Effet de déplacement INFÉRENCE (animation). | Un test de chargement par carte, une mesure de la limite, des prefabs. Échec : l'arc tombe sans bloquer la tranche. | oui, après test de chargement |
| **Q-sm2-3 : trois libellés** (R5) | Rien. | Trois chaînes. | oui |
| **Q-sm2-4 : O-SM0-1** (pots de la carte 60) | Rien (carte inatteignable). | Porter `map_alundra` (sm9) est un autre chantier. | laisser le repli, épingle S4 |
| **Q-sm2-5 : périmètre** | 73 des 129 sites sont fermés sans effet en jeu. Seuls les 56 de 344 et 442 se voient. | Le code est le même. | fermer les 129 (D-SM0-4) |
| **Q-sm2-6 : ordre avec sm1** | Rien. Les deux modifient `EventProgramRunner.cs` et la table des tailles. | sm2 : aucun hôte, aucune recherche, ni `RidingEntity` ni E20.g1a ni D5b. sm1 : les trois. | sm2 d'abord |
| **Q-sm2-7 : préréglage de recette** | Atteindre 442 et 344 en partie normale. | Un ajout dans `Alundra/SaveGames/TestSaves.cs`. | pas dans sm2 |
| **ADR ?** | Rien. | Aucune décision d'architecture : un port fidèle. | aucun, la décision Z et les règles vont au plan |

**Dépendances** :
- **E20.g1a** : indépendante (rien ne lit `RidingEntity` ni `PlatformEntity`).
- **D5b** : indépendante (ni `EntitySupport.cs`, ni `ScriptedMotion.cs`, ni `EntityScriptProxy.cs`).
- **E20.c2a** : ✅ faite, hors sujet.
- **E18** (`0xBB`, sm8) : le sur-ensemble d'écrivains de S1 le couvre. Les deux sites de `0xBB` (347, 477) ne contiennent aucun `6A-6D`.
- **Q-sm-10** (`0x5F`, 0 site) : idem.
- **sm1** : voir Q-sm2-6.

## 8. Levée des manques de `check_sm12.md §8` pour sm2

| Manque | État |
|---|---|
| B2, règles d'exécution aux noms du HEAD | Levé (§1). |
| B3, tests nommés avec valeurs, rouges, arrêts, acceptation | Levé (§3 et §6). |
| B4, arcs T3 | Levé : feasibilité statique, remplaçants garantis, arcs facultatifs sur les cartes atteignables (§4). |
| B7, test statique non spécifié | Levé (§5). |
| B8, atteignabilité des 55 à 60 | Levé en FAIT statique (§4.2), INFÉRENCE sur la conclusion. |
| M1 vecteurs, M14 arithmétique 32 bits, M15 `6D` sans `6A`, M16 choix des arcs | Levés (§2, §1 R2, §3 H5/RB-56, §4.3). |
| M2 libellés, M3 `ImplementedOpcodes`, M4 ADR, M5 citations, M6 R5, M7 critiques | Levés (§1, §7). |
| **B0** (règle du chantier, `docs/plan-restructuration-dll.md:160`) et **B1** (sm0 exécutée) | **Restent** : prérequis, pas de matière. |

**Corrections et constats utiles aux rapports précédents** :
- La phrase « Pour sm2, seules 344, 307 et 442 sont des cartes du jeu » (E7 de `check_sm12`) est fausse pour la **307**. 344 et 442 sont atteignables ; la 307 est dans un îlot fermé.
- `memo_flow.py` ignorait 4 lectures (306). Les arêtes de retour de `cfg2.py` les retrouvent. Conclusion inchangée : 0 écrasement.
- La phrase « une lecture `0x6D` sans `0x6A` compare 0 à PosZ » est exacte (état neuf côté DLL). Que le binaire remette à zéro l'état d'une entité à la création n'a pas été trouvé (appel de remise à zéro absent de `0x80039A00-0x8003A400` et de `0x80038634`) : INCONNU, sans conséquence en jeu.
- Je n'ai pas pu exécuter le C# : tous les attendus C# sont dérivés par une seconde méthode (`tools/mini_prog.py`) croisée avec les vecteurs, jamais mesurés sur la DLL.

## 9. Fichiers produits (`OUT`)

- `vectors/vectors-sm2.tsv`, `vectors/README.md`.
- `proposed/` : `EventProgramRunner.sm2.cs.txt`, `EventOpcodeSizeTable.sm2.diff.txt`, `PositionMemoOpcodesTests.cs.txt`, `PositionMemoCorpusTests.cs.txt`, `RealBytesExpected.txt`.
- `evidence/` : désassemblages (`redisasm.txt`), `programs_decoded.txt` (45 programmes), `records_sm2.txt`, `entity_closure.txt`, `reach_maps.txt`, `remaps.txt`, `memo_reads_table.tsv`, `memo_fabricated.txt`, `memo_flow_mutants.txt`, `mutants_vectors.txt`, `mutants_rows.txt`, `killmatrix.txt`, `handvalues.txt`, `scenarios.txt`, `crosscheck_annex.txt`, `census_sm2.txt`, `corpus_counts.txt`, `export_hashes.txt`, `stateuse2_run.txt`.
- `tools/` : `run_all.py` (rejoue tout), `vectors_sm2.py`, `mini_prog.py`, `cfg2.py`, `memo_static.py`, `killmatrix.py` et les autres, plus les copies `emu.py`, `lib.py`, `oracle.py`, `myoracle.py`, `stateuse2.py`, `dh.py`, `exelib.py`, `mipsdis.py`.
- `SHA1SUMS.txt` (68 fichiers).