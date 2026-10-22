# Critique indépendante de la préparation d'E19.sm2 (`0x6A 0x6B 0x6C 0x6D`, 129 sites), 2026-10-22

Lecture seule respectée. Je n'ai lancé ni build, ni test C#, ni export. Python avec `-B`, sur mes propres scripts. Les fichiers datés du 2026-10-18 dans le dossier de sortie viennent d'une tentative antérieure. Ils ne servent pas ici, mais ils concordent avec mes mesures.

Dossier de sortie, noté `OUT` plus bas : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm2-critic`

HEAD lu : `2b0c3f8b`. FAIT : `git diff --stat 317b3360 HEAD -- Alundra Alundra.Tests alundra-project tools` est vide. Les deux commits après `31fc8b23` ne touchent que le plan E20.h1.

## 0. Verdict global

- **Aucun P0 ni P1.** Toutes les valeurs qui portent une décision tiennent. Je les ai refaites de zéro :
  - l'arithmétique du gestionnaire (R1 et R2) ;
  - le mémo (R3) ;
  - les 129 sites, leur atteignabilité et les comptes de sm0 ;
  - les 1 114 vecteurs et les valeurs des tests RB (octets réels, pcs réels).
- **Deux P2 à corriger avant exécution.** Chacun ferait déclencher un arrêt à tort. Le premier est la matrice des mutants (§2), contre la règle d'arrêt « un mutant non rouge là où la matrice l'annonce ». Le second est la paire de commandes de recherche de l'acceptation (§2).
- Le reste est du P3 ou du P4, surtout des erreurs de documentation.
- La préparation n'est exécutable qu'une fois les P2 corrigés **et** les prérequis levés. Les prérequis sont R5, Q-R-7 avec R7, l'accord sur Q-E19-SM1, et sm0 exécutée.

## 1. Méthode

Tout est écrit de zéro, rien n'est repris de leurs scripts. Les scripts sont dans `OUT/tools/`.

- **Binaire.**
  - Désassemblage capstone des quatre gestionnaires par la table `0x80098FAC` (`evidence/my_handlers_6A_6D.txt`).
  - Relecture de `RunScript` `0x8004205C` et d'`InitializeEventData` `0x80041EE4`.
  - Interpréteur MIPS à moi (`myemu.py`) qui exécute les VRAIS octets des gestionnaires.
- **DLL.** Lecture de `Dispatch`, `RunOneScriptCall`, `UnknownOpcode`, de tous les accès à `Parameters`, de `EntitySearchService` et de `EventProgramDocument`.
- **Corpus.** Mon propre parcours (`mycorpus.py`, suivant `WalkProgram`), mon propre flot du mémo (`my_memoflow.py`) et mon propre graphe des cartes (`my_reach.py`).
- **Tests.** Mes propres modèles des tests du brouillon et des 17 mutants (`my_kill.py`, `my_rb.py`).

## 2. Verdict par fait (CONFIRMÉ / RÉFUTÉ / NON ÉTABLI)

### Binaire

| # | Fait | Verdict | Preuve |
|---|---|---|---|
| F1 | Gestionnaires `0x8003F8DC/908/94C/990` à l'indice `0x6A..0x6D` de la table `0x80098FAC`, tailles 1, 3, 3, 3. | CONFIRMÉ | Les pointeurs sont à `0x80099154..60`. Un seul exemplaire de chaque dans toute l'image. |
| F2 | R1 : `0x6A` copie `E+0x114/118/11C` vers `S+0xC/0x10/0x14`, `Result` intact, retour 1. | CONFIRMÉ | Les quatre gestionnaires sont des feuilles : ni `jal`, ni pile, ni `E+0x10`. |
| F3 | R2 : `d = subu`, `negu` si `d < 0` (`int.MinValue` reste négatif), seuil `(b1 \| b2<<8)<<16` signé, `slt` puis `xori 1`. | CONFIRMÉ | 1 114/1 114 vecteurs rejoués sur mon émulateur, plus 120 000 exécutions biaisées vers les bornes, 0 écart (`fuzz_formula.py`). |
| F4 | `a0` est l'entité logique, lue à `0x80042284` (`[owner+0x230]`). | CONFIRMÉ | |
| F5 | « Appel fidèle `a2 = a3 = STATE`, pointeur d'instruction à `STATE+0`, `Parameters[0]` à `STATE+4` » (README et §2 du rapport). | **RÉFUTÉ** | `0x800420B4` pose `s2 = s3+4`, puis `a2 = s2` (`0x800422C8`) et `a3 = s3` (`0x800422E0`). Le pointeur d'instruction est à `S+4`. `S+0` est le début de programme, soit `Parameters[0]` (`0x80041F5C/60`). Aucune valeur ne change, car les gestionnaires ne lisent jamais `S+0` ni `S+4` hors de ce pointeur. C'est une erreur de doc (P3). |
| F6 | `Parameters[1]` remis à 0 par la boucle (`sw $zero,8($s3)` à `0x8004232C`). | CONFIRMÉ | |
| F7 | Écrivains binaires des cellules. P2 : `0B 1C 1D 1E 1F 20 21 22 23 37 5F 6A BB`. P3 : `0B 1E 1F 5F 6A`. P4 : `5F 6A`. | CONFIRMÉ | `my_statewrites2.py`, avec fermeture récursive sur les `jal` qui reçoivent l'état. |
| F8 | Table globale `map_alundra` lue quand le bit `0x80` est clair (base `0x80126EC0`, `u16` à `table+2·(i&0x7F)`, sans borne). | CONFIRMÉ | `0x80041EE4..0x80041F54`. |

### DLL

| # | Fait | Verdict | Preuve |
|---|---|---|---|
| F9 | Correspondance octet vers membre : `E+0x114/118/11C` = `PosX/PosY/PosZ` de l'entité logique (`Dispatch`, `EventProgramRunner.cs:563`, `logic = LogicEntity ?? entity`). `S+0xC/0x10/0x14` = `Parameters[2..4]` (`P[0]=S+0`, `P[1]=S+8`). `S+0x2C` = `Result`. | CONFIRMÉ | Disposition de `EventProgramState`, `FillDataFromCommand` (octets 0..255). |
| F10 | « Rouge aujourd'hui » : `Dispatch` retombe sur `UnknownOpcode` (saut par la taille, type de trace `UnknownSkipped`, `Size` = taille de la table). `Result` et `Parameters` ne bougent pas, et le `03`/`04` suivant relit un `Result` périmé. | CONFIRMÉ | Mon mode « aujourd'hui » reproduit leurs valeurs « today » sur 7 scénarios sur 8. Une exception à l'ordre prévu : voir F26 (RB-56). |
| F11 | Écrivains de la DLL = sous-ensemble du binaire (sans `5F`/`BB`). | CONFIRMÉ | `rg Parameters` : lignes 2655-2660, 2715-2716, 2766, 2814, 2861, 2880-2885, 2908-2909, aucun autre fichier. |
| F12 | Aucun overflow vérifié dans le dépôt. `Math.Abs` n'est présent qu'aux lignes 2915-2916. | CONFIRMÉ | `rg` sur les `.csproj`, `.props`, `.targets` et sur `checked(`. |
| F13 | Points d'insertion `:2025` (avant `case 0x6E` `:2026`) et `:2641-2645` (avant `Wait`). | CONFIRMÉ | |
| F14 | Libellés `:153-155` : aucune dépendance, ni test, ni trace, ni doc. | CONFIRMÉ | `rg` hors décompilation et sous-modules. |
| F15 | Les six traces (`docs/hero-trace-389-*`, `intro-trace-389.txt`, `intro-programs-389.txt`) ne contiennent aucune des quatre chaînes. Aucun test existant ne cite `0x6A..0x6D`. | CONFIRMÉ | |

### Corpus et cartes

| # | Fait | Verdict | Preuve |
|---|---|---|---|
| F16 | 129 sites : 483 cartes, 110 140 instructions, 8 cartes. Comptes `6A` 59, `6B` 29, `6C` 12, `6D` 29. 45 programmes, tous en créneau C. Les clés sont égales à `skipped-sites.tsv`. | CONFIRMÉ | `census_own.py` : par carte 55:15, 56:4, 57:2, 59:20, 60:24, 307:8, 344:40, 442:16. |
| F17 | Seuils `{32, 48, 60, 64, 72, 80, 96, 112, 120, 128, 256}` px, aucun `>= 0x8000`. Chaque lecture est suivie d'un `03` (42) ou d'un `04` (28). | CONFIRMÉ | |
| F18 | Mémo : 70 lectures (X 29, Y 12, Z 29). 59 avec `0x6A`, 11 sans. 0 écrasement. 4 appels `0x78` sur la 307. | CONFIRMÉ | Mon flot reproduit les 70 lignes et leurs états, identiques aux leurs. |
| F19 | Les 11 lectures `6D` sans `6A` lisent un état neuf, car aucune entité concernée ne reçoit de `6A`. | CONFIRMÉ | Aucun `0x40` n'est atteint en 55, 56, 57. Les entités des C[20..24] de la 55 sont distinctes de celles des C[4..9]. |
| F20 | Atteignabilité. Les cartes 55 à 60 et la 307 sont un îlot fermé (seulement des arêtes intérieures). 344 est à 14 sauts de la 389 et 442 à 11. | CONFIRMÉ (statique) | Mon BFS donne les mêmes distances. Sans aucun identifiant remappé par un portail : 17 et 11, donc robuste. Les 66 sites `0x38` ne redirigent rien vers {55..60, 305..307} sauf `[55]=443`. |
| F21 | Comptes sm0 après sm2 : 381 lignes, 379 instructions, 90 cartes, 17 opcodes. Les cartes 55, 56, 57, 59, 307, 344 quittent la liste. La 60 garde un `0x56`, la 442 un `0x88`. | CONFIRMÉ | Recomptés sur `skipped-sites.tsv`. |
| F22 | 344 : le programme C ne vit que si `{1710}` ou `{1026,1027}`. | CONFIRMÉ | A[60] : `30 AE 06`, `31 02 04`, `31 03 04` mènent à `40 02 00` (efface C). Les chemins par `63 80 80 20` se terminent sur le `Break` `@547`, qui clôt un programme A, donc sans effacer C. |
| F23 | Carte 60 : les 8 sites ne sont atteints que par des enregistrements à bit `0x80` clair (rec 0 vers C[1], rec 9 vers C[10]). | CONFIRMÉ | |
| F24 | Préconditions d'arc : `.world`, `.tileMap`, dialogues, chaque `PrefabAssetId` dans `AssetInfos.json` avec son fichier. Zones de génération : 344 x39..51 y35..59, 442 x4..20 y7..26, aucune ne contient (33,59). | CONFIRMÉ | Contrôlé pour les 8 cartes. |
| F25 | Hachages d'export (16 fichiers), SHA-1 de l'EXE, sha256 du TSV. | CONFIRMÉ | Tous identiques à ceux du rapport. |
| F26 | Mouvement d'un patrouilleur sur la durée d'un arc, signaux de fin, limites d'images. | NON ÉTABLI | Rien n'a été exécuté, la préparation le dit elle-même (INFÉRENCE ou INCONNU). |
| F27 | Cadre Z (ADR-0026) : binaire = DLL + 1, binaire ≡ 1 mod 256 au repos. Le cas d'égalité exacte sur les 11 lectures inatteignables est donc improbable. | CONFIRMÉ (ADR lu) | Option A de Q-sm2-1 justifiée. |
| F28 | Le binaire remet-il à zéro l'état d'une entité à la création, et l'état d'une entité recyclée (ADR-0024) ? | NON ÉTABLI | Sans conséquence : seules les 11 lectures « neuves » (cartes inatteignables) sont concernées. |

### Tests

| # | Fait | Verdict | Preuve |
|---|---|---|---|
| F29 | Vecteurs : 31, 361, 361, 361 lignes. Lignes qui changent la sortie : 30, 253, 258, 253. | CONFIRMÉ | |
| F30 | Valeurs des RB (traces, `Result`, `CodeIndex`, mémos, direction) pour 55, 55n, 56, 56b, 59, 307, 344, 442. | CONFIRMÉ (port) | Mon modèle de l'interpréteur, écrit depuis la source de la DLL, redonne 100 % des valeurs « port ». |
| F31 | Valeurs « today » des RB. | CONFIRMÉ sauf un | **RB-56 tick 3** : ils donnent `93:1B 96:00 Result=1 CodeIndex=97`. La bonne valeur est `118:6F 119:03 122:30 127:02 114:1B 117:00 Result=0 CodeIndex=118`. Cause : `0x6F` est porté au HEAD (`:831`, `Result = CollidedWithEntityZ = 0`) et ils ne l'avaient pas modélisé. Ce tick n'est pas asserté (P3). |
| F32 | 38 tests rouges aujourd'hui (4 V + 26 H + 8 RB). 37 sans H10. | CONFIRMÉ | Tous rouges dans mon modèle. Les 34 H/RB passent sur le port, les 4 V sont rejoués à part. |
| F33 | Chaque mutant est tué par au moins un test nommé. | CONFIRMÉ | |
| F34 | Les listes de tests rouges par mutant (rapport §6, spec du brouillon, `killmatrix.txt`). | **RÉFUTÉ en partie** | Voir C1. |
| F35 | Comptes de lignes tuant chaque mutant sur les vecteurs : 168, 366, 72, 1083, 216, 193, 348, 416, 127, 30, 19. | CONFIRMÉ sauf un | « Forme en pixels » 366 : c'est **282**. En C# `-int.MinValue` reste négatif, donc `>> 16` donne −32768 et non +32768. Le 366 est celui du mutant « non signé ». |
| F36 | Deux mutants équivalents : `position - memo`, et `(sbyte)v[2]`. | CONFIRMÉ | |
| F37 | Preuves de retrait (code seul : 37 tests ; libellés seuls : H10 ; classes de tests seules : rien). | CONFIRMÉ | Les règles internes de sm0 ne sont pas vérifiables, sm0 n'existe pas (NON ÉTABLI pour cette partie). |
| F38 | Noms libres (`PositionMemo*`, `MemoState`, `MemoFlow`). | CONFIRMÉ | `rg` sur le dépôt et le moteur. L'acceptation par la garde R7 est NON ÉTABLIE : R7 n'existe pas encore. |
| F39 | Acceptation « `rg -c "Math\.Abs" EventProgramRunner.cs` vaut toujours 2 ». | **RÉFUTÉ** | Le commentaire du brouillon (ligne 37) écrit `Math.Abs` : le compte serait 3. |
| F40 | Acceptation « `rg -n "Parameters\[4\]" Alundra` donne exactement 2 lignes ». | **RÉFUTÉ** | Le brouillon a 4 lignes : 2 en code et 2 en commentaire (lignes 13 et 28). |

## 3. Corrections, par gravité

### P2 (à corriger avant exécution)

- **C1. Matrice des mutants fausse, avec une règle d'arrêt qui la lit.** L'arrêt « un mutant non rouge là où la matrice l'annonce » se déclencherait à tort. Voici les ensembles rouges exacts, d'après les tests du brouillon, mon modèle (`OUT/evidence/my_killmatrix.txt`) et ses comparaisons (`OUT/evidence/cmp_killmatrix.txt`). V = les vecteurs.

| Mutant | Rouge exactement (tests H/RB) | Écart avec leur liste |
|---|---|---|
| M1 `Math.Abs` | H3b, H3c, H4 | rien |
| M2 forme en pixels | H3a, H3c ; V 282 lignes | ils donnent H4 (faux), il manque H3c |
| M3 non signé | H3a, H4 | rien |
| M4 `<=` | H2X-120, H2X<-120, H2Y-16, H2Z-1, H3c, H5-01000000, H6, H7-120, RB-55, 55n, 56, 56b, 59, 307, 344, 442 | H6 oublié |
| M5 inversé sur la ligne `0x6B` seule | H2X (6 cas), H3a, H3b, H3c, H4, H6, H7 (2), H9-6B, RB-55, 55n, 307, 344, 442 ; V 361 | leur liste suppose les trois lignes |
| M6 | H3b, H4 | rien |
| M7 | H2X-120, H2X+1, H2Y-16, H2Z-1, H5 (2), H6, H7-120, H9-6B, RB-55, 56, 56b, 59, 307, 344, 442 | H6 oublié |
| M8 `6A` sans Z | H1, H6, H8, H9-6D, RB-55, 55n, 59, 307, 344, 442 | ils ajoutent H9X et H9Y (faux) |
| M9 `6A` écrit `Result:=1` | **H1 seul** ; V6A 19 lignes | ils ajoutent H7, H8, H9 (faux) |
| M10 lit le propriétaire | **H6 seul** ; V 416 lignes | ils listent 21 tests (faux) |
| M11 | H2Z-0.99, RB-59 | rien |
| M12 | les 19 tests de M5 | H6 oublié |
| M13 `6A` rend 2 | H1, H6, H7 (2), H9 (3), RB-55, 55n, 59, 307, 344, 442 | ils ajoutent H8 (faux), H6 oublié |
| M14 | H6, H7 (2), H8, RB-55, 55n, 59, 307, 344, 442 | H6 oublié |
| M15 | H8 | rien |
| M16 | H9-6B, H9-6C, H9-6D | rien |
| M17 | H10 | rien |

  - **Cause de M8, M9, M10 et M13.** Leur modèle n'est pas celui du brouillon. Les tests H, RB et V hors H6 prennent l'entité logique égale au propriétaire, donc M10 y est équivalent. L'assertion de H7, H8 et H9 est écrasée par la lecture suivante, donc M9 y est invisible.
  - **Correction.** Remplacer la matrice par la sortie de mon `my_kill.py` ou, mieux, régénérer les ensembles par script sur les tests finaux. Reformuler l'arrêt en « un mutant sans aucun test rouge ». Une correction possible, plus robuste : pour M10 et M9, écrire un test ciblé (logique ≠ propriétaire hors H6, ou `Result` de départ différent).
- **C2. Commandes d'acceptation incohérentes avec le brouillon.** Les corriger en :
  - `rg -c "Math\.Abs\(" Alundra/Events/EventProgramRunner.cs` (vaut 2 : les commentaires n'ont pas de parenthèse) ;
  - `rg -n "state\.Parameters\[4\]" Alundra/Events/EventProgramRunner.cs` (2 lignes : le `6A` et le `6D`).

### P3 (à corriger)

- **C3.** README et rapport §2 : réécrire la disposition (F5). Le pointeur d'instruction est à `S+4`, pas à `S+0`. `S+0` est `Parameters[0]`. Retirer « fidèle » ou le justifier. Réécrire aussi la ligne du tableau de correspondance « `S+0` puis `*S+1`, `*S+2` » en `*(S+4)+1`, `*(S+4)+2`.
- **C4.** Mutant « forme en pixels » : 282 lignes, pas 366 (F35).
- **C5.** `RealBytesExpected.txt`, RB-56 tick 3, colonne « today » : mettre la valeur de F31, ou la retirer.
- **C6.** Tests V : l'assertion `Parameters[1] == 0` est vide, car `Parameters[1]` vaut déjà 0 au départ. Il faut une sentinelle non nulle (par exemple `0xA5A50001`) pour qu'elle réfute la boucle. Les assertions de sentinelles (`P0`, `P5..9`, `_30`, `_34`) et de positions inchangées ne sont réfutées par aucun mutant. Ajouter un mutant (un `6A` qui écrit aussi `Parameters[5]`) ou le noter.
- **C7.** Le brouillon de `PositionMemoCorpusTests` ne contient que `MemoFlow`, S1f et un commentaire. Les trois `Fact` de S1 et S4 ne sont pas écrits. Les écrire, ou les décrire au plan avec leurs comptes (70 lectures, 59 avec `6A`, 11 sans, 0 écrasement, 4 appels).
- **C8.** Arcs (T3) : à traiter comme facultatifs, hors acceptation. Pour A-344, le bloc se déplace sous l'effet de `0x89` pour l'entité de l'enregistrement 11 (recherche `0x0B` = identifiant brut d'enregistrement, cible), et du mouvement d'animation. Sa limite d'images et son signal sont non mesurés, donc non conformes au §1.3 « valeurs écrites avant le code ». Aucun de ces arcs ne peut conditionner la tranche.
- **C9.** « (k1) épingle aussi `ClosedOwners == {"sm2"}` » n'est vrai que si sm2 est la première tranche close. Écrire plutôt « l'ensemble des tranches closes à ce moment ».

### P4

- **C10.** Restauration des mutations : copie sauvegardée plus `cmp`, pas `git checkout --` (qui détruirait le port s'il n'est pas validé).
- **C11.** S3 (la sonde des écrivains de la DLL) est le seul garde contre un écrivain neuf de P2..P4 côté DLL. S1 ne contrôle que l'ensemble du binaire. Je recommande de le rendre obligatoire.
- **C12.** Noter que seuls 60 C[1] et C[10] (cartes inatteignables) font tourner un `0x42`/`0x43` autour de leurs lectures : c'est la seule situation « logique ≠ propriétaire » du corpus. Elle est couverte par H6 et par les vecteurs, jamais par un RB.
- **C13.** L'ordre des arguments de `Assert.Equal` pour `(Pc, Opcode)[]` contre `IReadOnlyList` : compile et passe, sans souci.

## 4. Cas manquants

- **Remarque (`rec` 60 C[1] / C[10]).** Voir C12 : un RB à logique ≠ propriétaire serait le seul test de la situation réelle. Facultatif, car la carte est inatteignable.
- **Aucun cas bloquant n'a été trouvé.** J'ai cherché :
  - la boucle sans suspension : seule la 307 en a une, et c'est un artefact du sur-ensemble des retours (`7D` revient en vrai à `_34`, donc au dernier point) ;
  - le seuil négatif réel : il n'y en a aucun ;
  - l'écrasement du mémo par `Break`, `InitializeEventData` et `EndCall` : aucun ;
  - le partage d'état entre créneaux : la tranche ne concerne que le créneau C.
- **Programmes avec lecture sans sauvegarde** : seuls les 11 lus « état neuf » (cartes 55, 56, 57), tous inatteignables.

## 5. Verdict par tâche proposée

| Tâche | Verdict | Corrections |
|---|---|---|
| A. Annexe : vecteurs, outils, README | OK avec réserve | C3, C4, C5. Les 1 114 lignes sont justes. |
| B. Tests rouges d'abord (V, H, RB, S1, S1f, S4, S3) | Corriger | C1, C6, C7, C11. Tous les cas sont bien rouges aujourd'hui (38). |
| C. Code, libellés, miroir `ImplementedOpcodes`, retrait des 129 lignes et `ClosedOwners` dans un seul commit | OK | C9. Le code proposé est correct et compilable en lecture. `unchecked` explicite, pas de `Math.Abs`, retour 1 et 3. |
| D. Arcs A-442, A-344 et tests de chargement jetables | Non exécutable en l'état | C8. Facultatif, jamais bloquant. |
| E. Mutations par script, acceptation, verifier frais | Corriger | C1, C2, C10. |
| F. Plan « done » | OK | |

## 6. Fichiers

Dans `OUT/tools/` :
- `mybin.py`, `mydis.py`, `myemu.py` : lecture du binaire, désassemblage, interpréteur MIPS.
- `replay_tsv.py` : rejoue les 1 114 vecteurs.
- `fuzz_formula.py` : les 120 000 exécutions biaisées vers les bornes.
- `my_statewrites.py`, `my_statewrites2.py` : écrivains de P2..P4 dans le binaire.
- `mycorpus.py`, `census_own.py`, `my_memoflow.py` : corpus, recensement et flot du mémo.
- `my_reach.py` : graphe des cartes.
- `my_rb.py`, `my_kill.py`, `cmp_kill.py`, `my_vecmut.py` : scénarios RB, matrice des mutants, comparaison, mutants sur vecteurs.
- `my_cycles.py`, `my_op40.py`, `showprog.py` : cycles sans suspension, `0x40` des cartes 55-57, décodage d'un programme.

Dans `OUT/evidence/` :
- `my_handlers_6A_6D.txt`, `my_runscript.txt` : désassemblages.
- `my_statewrites2.txt` : écrivains.
- `my_sites_6A_6D.json`, `my_memo_reads.tsv` : sites et lectures.
- `my_reach.txt` : atteignabilité.
- `my_killmatrix.txt`, `cmp_killmatrix.txt` : matrice corrigée et comparaison.